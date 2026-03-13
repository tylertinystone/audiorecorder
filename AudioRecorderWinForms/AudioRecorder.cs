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

                if (!TryRecordWithSource(path, "WindowRecordingSource", targetWindowHandle)
                    && !TryRecordWindowHandle(path, targetWindowHandle))
                {
                    HandleRecordingError("录制失败：当前版本不支持窗口源录制接口。");
                }

                return;
            }

            if (!TryRecordWithSource(path, "DisplayRecordingSource", 0))
            {
                recorder.Record(path);
            }
        }
        catch (Exception ex)
        {
            HandleRecordingError($"启动录制失败：{ex.Message}");
        }
    }

    private bool TryRecordWithSource(string path, string sourceTypeName, object sourceArg)
    {
        if (recorder is null)
        {
            return false;
        }

        var asm = typeof(Recorder).Assembly;
        Type? sourceType = null;
        foreach (var t in asm.GetTypes())
        {
            if (string.Equals(t.Name, sourceTypeName, StringComparison.Ordinal))
            {
                sourceType = t;
                break;
            }
        }

        if (sourceType is null)
        {
            return false;
        }

        var source = CreateSource(sourceType, sourceArg);
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

    private static object? CreateSource(Type sourceType, object sourceArg)
    {
        foreach (var ctor in sourceType.GetConstructors())
        {
            var ps = ctor.GetParameters();
            if (ps.Length != 1)
            {
                continue;
            }

            if (TryConvertArg(sourceArg, ps[0].ParameterType, out var converted))
            {
                try
                {
                    return ctor.Invoke(new[] { converted! });
                }
                catch
                {
                }
            }
        }

        try
        {
            var source = Activator.CreateInstance(sourceType);
            if (source is null)
            {
                return null;
            }

            foreach (var propertyName in new[] { "Handle", "Hwnd", "WindowHandle", "DisplayIndex", "MonitorIndex" })
            {
                var property = sourceType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property is null || !property.CanWrite)
                {
                    continue;
                }

                if (TryConvertArg(sourceArg, property.PropertyType, out var converted))
                {
                    property.SetValue(source, converted);
                    return source;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool TryConvertArg(object value, Type targetType, out object? converted)
    {
        converted = null;

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (value is nint nvalue)
        {
            if (targetType == typeof(IntPtr) || targetType == typeof(nint))
            {
                converted = (IntPtr)nvalue;
                return true;
            }

            if (targetType == typeof(long))
            {
                converted = (long)nvalue;
                return true;
            }

            if (targetType == typeof(int))
            {
                converted = unchecked((int)nvalue);
                return true;
            }
        }

        if (value is int ivalue)
        {
            if (targetType == typeof(int))
            {
                converted = ivalue;
                return true;
            }

            if (targetType == typeof(long))
            {
                converted = (long)ivalue;
                return true;
            }
        }

        return false;
    }

    private bool TryRecordWindowHandle(string path, nint targetWindowHandle)
    {
        if (recorder is null)
        {
            return false;
        }

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

            if (!TryConvertArg(targetWindowHandle, ps[1].ParameterType, out var converted))
            {
                continue;
            }

            try
            {
                method.Invoke(recorder, new[] { (object)path, converted! });
                return true;
            }
            catch
            {
            }
        }

        return false;
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
