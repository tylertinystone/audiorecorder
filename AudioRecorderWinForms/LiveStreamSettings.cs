namespace AudioRecorderWinForms;

public sealed class LiveStreamSettings
{
    public bool Enabled { get; set; }
    public string RtmpUrl { get; set; } = "rtmp://a.rtmp.youtube.com/live2";
    public string StreamKey { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string ProxyUrl { get; set; } = string.Empty;

    public string FullRtmpUrl => string.IsNullOrWhiteSpace(StreamKey)
        ? RtmpUrl
        : $"{RtmpUrl.TrimEnd('/')}/{StreamKey.Trim()}";
}
