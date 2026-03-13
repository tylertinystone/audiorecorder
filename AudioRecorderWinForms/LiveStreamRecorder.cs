using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AudioRecorderWinForms;

public sealed class LiveStreamRecorder : IDisposable
{
    private const string DefaultDshowAudioDevice = "virtual-audio-capturer";
    private Process? process;
    private readonly System.Timers.Timer timer;
    private DateTime startTime;

    public bool IsRunning => process is not null && !process.HasExited;

    public event EventHandler<TimeSpan>? ElapsedChanged;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? WarningOccurred;

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

        var includeSystemAudio = HasDshowAudioDevice(settings.FfmpegPath, DefaultDshowAudioDevice);
        var args = BuildArguments(videoInput, outputPath, rtmp, includeSystemAudio);

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
            var currentProcess = process;
            currentProcess.Exited += (_, _) =>
            {
                timer.Stop();
                if (currentProcess.ExitCode == 0)
                {
                    RecordingStopped?.Invoke(this, outputPath);
                }
                else
                {
                    var err = currentProcess.StandardError.ReadToEnd();
                    ErrorOccurred?.Invoke(this, $"FFmpeg 退出码 {currentProcess.ExitCode}。{Environment.NewLine}{err}");
                }

                currentProcess.Dispose();
                if (ReferenceEquals(process, currentProcess))
                {
                    process = null;
                }
            };

            if (!process.Start())
            {
                ErrorOccurred?.Invoke(this, "FFmpeg 启动失败（将回退本地录屏）。");
                return false;
            }

            if (!includeSystemAudio)
            {
                WarningOccurred?.Invoke(this, "未找到音频设备 virtual-audio-capturer，本次直播将仅推送画面（无系统声音）。");
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

    private static string BuildArguments(string videoInput, string outputPath, string rtmp, bool includeSystemAudio)
    {
        if (!includeSystemAudio)
        {
            return
                $"-y -f gdigrab -framerate 30 -i {Quote(videoInput)} " +
                $"-map 0:v -c:v libx264 -preset veryfast -pix_fmt yuv420p -an {Quote(outputPath)} " +
                $"-map 0:v -c:v libx264 -preset veryfast -pix_fmt yuv420p -an -f flv {Quote(rtmp)}";
        }

        return
            $"-y -f gdigrab -framerate 30 -i {Quote(videoInput)} " +
            $"-f dshow -i audio={Quote(DefaultDshowAudioDevice)} " +
            "-map 0:v -map 1:a -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 128k " +
            $"{Quote(outputPath)} -map 0:v -map 1:a -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 128k -f flv {Quote(rtmp)}";
    }

    private static bool HasDshowAudioDevice(string ffmpegPath, string audioDeviceName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var probe = Process.Start(psi);
            if (probe is null)
            {
                return false;
            }

            var stderr = probe.StandardError.ReadToEnd();
            probe.WaitForExit(3000);

            return stderr.Contains(audioDeviceName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

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
