using ScreenRecorderLib;
using System;
using System.IO;

namespace AudioRecorderWinForms;

public enum CaptureTargetMode
{
    FullScreen,
    SpecificWindow
}

public sealed class AudioRecorder : IDisposable
{
    private Recorder? recorder;
    private readonly System.Timers.Timer timer;
    private DateTime startTime;
    private string outputPath = string.Empty;

    public bool IsRecording { get; private set; }

    public event EventHandler<TimeSpan>? ElapsedChanged;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<string>? ErrorOccurred;

    public AudioRecorder()
    {
        timer = new System.Timers.Timer(500);
        timer.Elapsed += (_, _) => ElapsedChanged?.Invoke(this, DateTime.Now - startTime);
    }

    public void Start(string path, CaptureTargetMode mode, nint targetWindowHandle)
    {
        if (IsRecording)
        {
            return;
        }

        try
        {
            outputPath = path;
            EnsureOutputDirectory(path);

            recorder = Recorder.CreateRecorder(BuildOptions());
            recorder.OnRecordingComplete += (_, _) => HandleRecordingComplete();
            recorder.OnRecordingFailed += (_, e) => HandleRecordingError($"录制失败：{e.Error}");

            startTime = DateTime.Now;
            timer.Start();
            IsRecording = true;

            if (mode == CaptureTargetMode.SpecificWindow)
            {
                if (targetWindowHandle == 0)
                {
                    HandleRecordingError("录制失败：未选择有效窗口句柄。");
                    return;
                }

                if (TryRecordWindow(path, targetWindowHandle))
                {
                    return;
                }

                HandleRecordingError("录制失败：当前 ScreenRecorderLib 缺少可用的窗口录制接口（已尝试 WindowRecordingSource 与句柄重载）。\n请改为全屏模式或更换兼容版本。");
                return;
            }

            recorder.Record(path);
        }
        catch (Exception ex)
        {
            HandleRecordingError($"启动录制失败：{ex.Message}");
        }
    }

    private static void EnsureOutputDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return;
        }

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private bool TryRecordWindow(string path, nint targetWindowHandle)
    {
        if (recorder is null)
        {
            return false;
        }

        // 方案1（第一版思路）：优先尝试 WindowRecordingSource + Record(path, source)
        if (TryRecordWithWindowSource(path, targetWindowHandle))
        {
            return true;
        }

        // 方案2：尝试 Record(path, IntPtr/nint) 句柄重载
        var method = typeof(Recorder).GetMethod("Record", new[] { typeof(string), typeof(nint) })
            ?? typeof(Recorder).GetMethod("Record", new[] { typeof(string), typeof(IntPtr) });

        if (method is null)
        {
            return false;
        }

        try
        {
            method.Invoke(recorder, new object[] { path, targetWindowHandle });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryRecordWithWindowSource(string path, nint targetWindowHandle)
    {
        if (recorder is null)
        {
            return false;
        }

        var asm = typeof(Recorder).Assembly;
        var windowSourceType = asm.GetType("ScreenRecorderLib.WindowRecordingSource");
        if (windowSourceType is null)
        {
            return false;
        }

        object? source = null;

        try
        {
            source = Activator.CreateInstance(windowSourceType, new object[] { targetWindowHandle });
        }
        catch
        {
            try
            {
                source = Activator.CreateInstance(windowSourceType, new object[] { (IntPtr)targetWindowHandle });
            }
            catch
            {
                return false;
            }
        }

        var recordWithSource = typeof(Recorder).GetMethod("Record", new[] { typeof(string), windowSourceType });

        if (recordWithSource is null)
        {
            foreach (var m in typeof(Recorder).GetMethods())
            {
                if (m.Name != "Record")
                {
                    continue;
                }

                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType.IsAssignableFrom(windowSourceType))
                {
                    recordWithSource = m;
                    break;
                }
            }
        }

        if (recordWithSource is null)
        {
            return false;
        }

        try
        {
            recordWithSource.Invoke(recorder, new[] { (object)path, source! });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Stop()
    {
        if (!IsRecording)
        {
            return;
        }

        recorder?.Stop();
    }

    private static RecorderOptions BuildOptions()
    {
        return new RecorderOptions
        {
            RecorderMode = RecorderMode.Video,
            VideoOptions = new VideoOptions
            {
                Framerate = 30,
                IsFixedFramerate = true,
                BitrateMode = BitrateControlMode.Quality,
                Quality = 70
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = true,
                IsOutputDeviceEnabled = true,
                IsInputDeviceEnabled = false
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = true,
                IsMouseClicksDetected = true
            }
        };
    }

    private void HandleRecordingComplete()
    {
        timer.Stop();
        Cleanup();
        RecordingStopped?.Invoke(this, outputPath);
    }

    private void HandleRecordingError(string message)
    {
        timer.Stop();
        Cleanup();
        ErrorOccurred?.Invoke(this, message);
    }

    private void Cleanup()
    {
        IsRecording = false;

        if (recorder is not null)
        {
            recorder.Dispose();
            recorder = null;
        }
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        Cleanup();
    }
}
