using ScreenRecorderLib;
using System;
using System.IO;
using System.Reflection;

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

                if (!TryRecordWithSource(path, "ScreenRecorderLib.WindowRecordingSource", targetWindowHandle))
                {
                    HandleRecordingError("录制失败：当前版本不支持窗口源录制接口。");
                }

                return;
            }

            if (!TryRecordWithSource(path, "ScreenRecorderLib.DisplayRecordingSource", 0))
            {
                recorder.Record(path);
            }
        }
        catch (Exception ex)
        {
            HandleRecordingError($"启动录制失败：{ex.Message}");
        }
    }

    private bool TryRecordWithSource(string path, string sourceTypeName, nint handle)
    {
        if (recorder is null)
        {
            return false;
        }

        var asm = typeof(Recorder).Assembly;
        var sourceType = asm.GetType(sourceTypeName);
        if (sourceType is null)
        {
            return false;
        }

        var source = CreateSource(sourceType, handle);
        if (source is null)
        {
            return false;
        }

        var recordMethod = FindRecordMethod(sourceType);
        if (recordMethod is null)
        {
            return false;
        }

        recordMethod.Invoke(recorder, new[] { (object)path, source });
        return true;
    }

    private static object? CreateSource(Type sourceType, nint handle)
    {
        try
        {
            return Activator.CreateInstance(sourceType, new object[] { handle });
        }
        catch
        {
            try
            {
                return Activator.CreateInstance(sourceType, new object[] { (IntPtr)handle });
            }
            catch
            {
                return null;
            }
        }
    }

    private static MethodInfo? FindRecordMethod(Type sourceType)
    {
        foreach (var method in typeof(Recorder).GetMethods())
        {
            if (method.Name != "Record")
            {
                continue;
            }

            var ps = method.GetParameters();
            if (ps.Length != 2 || ps[0].ParameterType != typeof(string))
            {
                continue;
            }

            if (ps[1].ParameterType == sourceType || ps[1].ParameterType.IsAssignableFrom(sourceType))
            {
                return method;
            }
        }

        return null;
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
