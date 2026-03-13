using ScreenRecorderLib;
using System;

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
            recorder = Recorder.CreateRecorder(BuildOptions());
            recorder.OnRecordingComplete += (_, _) => HandleRecordingComplete();
            recorder.OnRecordingFailed += (_, e) => HandleRecordingError($"录制失败：{e.Error}");

            startTime = DateTime.Now;
            timer.Start();
            IsRecording = true;

            if (mode == CaptureTargetMode.SpecificWindow && targetWindowHandle != 0 && TryRecordWindow(path, targetWindowHandle))
            {
                return;
            }

            recorder.Record(path);
        }
        catch (Exception ex)
        {
            HandleRecordingError($"启动录制失败：{ex.Message}");
        }
    }


    private bool TryRecordWindow(string path, nint targetWindowHandle)
    {
        if (recorder is null)
        {
            return false;
        }

        var method = typeof(Recorder).GetMethod("Record", new[] { typeof(string), typeof(nint) })
            ?? typeof(Recorder).GetMethod("Record", new[] { typeof(string), typeof(IntPtr) });

        if (method is null)
        {
            return false;
        }

        method.Invoke(recorder, new object[] { path, targetWindowHandle });
        return true;
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
