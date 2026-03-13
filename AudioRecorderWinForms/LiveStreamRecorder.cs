using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AudioRecorderWinForms;

public sealed class LiveStreamRecorder : IDisposable
{
    private Process? process;
    private readonly System.Timers.Timer timer;
    private DateTime startTime;

    public bool IsRunning => process is not null && !process.HasExited;

    public event EventHandler<TimeSpan>? ElapsedChanged;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<string>? ErrorOccurred;

    public LiveStreamRecorder()
    {
        timer = new System.Timers.Timer(500);
        timer.Elapsed += (_, _) => ElapsedChanged?.Invoke(this, DateTime.Now - startTime);
    }

    public bool Start(string outputPath, CaptureTargetMode mode, nint targetWindowHandle, string? windowTitle, LiveStreamSettings settings)
    {
        if (IsRunning)
        {
            return true;
        }

        var rtmp = settings.FullRtmpUrl;
        if (string.IsNullOrWhiteSpace(rtmp) || !rtmp.StartsWith("rtmp", true, CultureInfo.InvariantCulture))
        {
            ErrorOccurred?.Invoke(this, "直播地址无效，请检查 RTMP 地址和 Stream Key。");
            return false;
        }

        try
        {
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
        }
        catch
        {
            // 非致命：目录创建失败时由 FFmpeg 自身报错，主流程可回退到本地录屏
        }

        var videoInput = mode == CaptureTargetMode.SpecificWindow
            ? BuildWindowInput(targetWindowHandle, windowTitle)
            : "desktop";

        var args =
            $"-y -f gdigrab -framerate 30 -i {Quote(videoInput)} " +
            "-f dshow -i audio=\"virtual-audio-capturer\" " +
            "-map 0:v -map 1:a -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 128k " +
            $"{Quote(outputPath)} -map 0:v -map 1:a -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 128k -f flv {Quote(rtmp)}";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = settings.FfmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Exited += (_, _) =>
            {
                timer.Stop();
                if (process.ExitCode == 0)
                {
                    RecordingStopped?.Invoke(this, outputPath);
                }
                else
                {
                    var err = process.StandardError.ReadToEnd();
                    ErrorOccurred?.Invoke(this, $"FFmpeg 退出码 {process.ExitCode}。{Environment.NewLine}{err}");
                }

                process.Dispose();
                process = null;
            };

            if (!process.Start())
            {
                ErrorOccurred?.Invoke(this, "FFmpeg 启动失败（将回退本地录屏）。");
                return false;
            }

            startTime = DateTime.Now;
            timer.Start();
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"启动直播推流失败（将回退本地录屏）：{ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            if (process is null || process.HasExited)
            {
                return;
            }

            process.StandardInput?.WriteLine("q");
            if (!process.WaitForExit(2000))
            {
                process.Kill(true);
            }
        }
        catch
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(true);
            }
        }
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
            }
            process.Dispose();
            process = null;
        }
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static string BuildWindowInput(nint targetWindowHandle, string? windowTitle)
    {
        if (targetWindowHandle != 0)
        {
            return $"hwnd={targetWindowHandle}";
        }

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            return $"title={windowTitle}";
        }

        return "desktop";
    }
}
