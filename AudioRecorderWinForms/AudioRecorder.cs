using NAudio.Wave;
using System;

namespace AudioRecorderWinForms;

public sealed class AudioRecorder : IDisposable
{
    private WasapiLoopbackCapture? capture;
    private WaveFileWriter? writer;
    private readonly System.Timers.Timer timer;
    private DateTime startTime;
    private string outputPath = string.Empty;

    public bool IsRecording => capture is not null;

    public event EventHandler<TimeSpan>? ElapsedChanged;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<string>? ErrorOccurred;

    public AudioRecorder()
    {
        timer = new System.Timers.Timer(500);
        timer.Elapsed += (_, _) => ElapsedChanged?.Invoke(this, DateTime.Now - startTime);
    }

    public void Start(string path)
    {
        if (IsRecording)
        {
            return;
        }

        try
        {
            outputPath = path;
            capture = new WasapiLoopbackCapture();
            writer = new WaveFileWriter(path, capture.WaveFormat);

            capture.DataAvailable += (_, a) => writer?.Write(a.Buffer, 0, a.BytesRecorded);
            capture.RecordingStopped += OnCaptureStopped;

            startTime = DateTime.Now;
            timer.Start();
            capture.StartRecording();
        }
        catch (Exception ex)
        {
            Cleanup();
            ErrorOccurred?.Invoke(this, $"启动录制失败：{ex.Message}");
        }
    }

    public void Stop()
    {
        if (!IsRecording)
        {
            return;
        }

        capture?.StopRecording();
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        timer.Stop();

        if (e.Exception is not null)
        {
            Cleanup();
            ErrorOccurred?.Invoke(this, $"录制过程中出现错误：{e.Exception.Message}");
            return;
        }

        Cleanup();
        RecordingStopped?.Invoke(this, outputPath);
    }

    private void Cleanup()
    {
        capture?.Dispose();
        capture = null;

        writer?.Dispose();
        writer = null;
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        Cleanup();
    }
}
