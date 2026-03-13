using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioRecorderWinForms;

public sealed class LiveStreamConfigForm : Form
{
    private readonly CheckBox enabledCheckBox;
    private readonly TextBox rtmpUrlTextBox;
    private readonly TextBox streamKeyTextBox;
    private readonly TextBox ffmpegPathTextBox;

    public LiveStreamConfigForm(LiveStreamSettings settings)
    {
        Text = "YouTube 直播推流设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(620, 260);

        enabledCheckBox = new CheckBox { Text = "启用录制同时推流", Location = new Point(20, 20), AutoSize = true, Checked = settings.Enabled };

        var urlLabel = new Label { Text = "RTMP 地址:", Location = new Point(20, 58), AutoSize = true };
        rtmpUrlTextBox = new TextBox { Location = new Point(110, 55), Width = 480, Text = settings.RtmpUrl };

        var keyLabel = new Label { Text = "Stream Key:", Location = new Point(20, 93), AutoSize = true };
        streamKeyTextBox = new TextBox { Location = new Point(110, 90), Width = 480, Text = settings.StreamKey };

        var ffmpegLabel = new Label { Text = "FFmpeg 路径:", Location = new Point(20, 128), AutoSize = true };
        ffmpegPathTextBox = new TextBox { Location = new Point(110, 125), Width = 480, Text = settings.FfmpegPath };

        var note = new Label
        {
            Text = "提示：需系统已安装 FFmpeg；系统声音采集依赖 dshow 设备（常见为 virtual-audio-capturer）。",
            Location = new Point(20, 158),
            Width = 570,
            Height = 34
        };

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(430, 196), Width = 75 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(515, 196), Width = 75 };

        Controls.Add(enabledCheckBox);
        Controls.Add(urlLabel);
        Controls.Add(rtmpUrlTextBox);
        Controls.Add(keyLabel);
        Controls.Add(streamKeyTextBox);
        Controls.Add(ffmpegLabel);
        Controls.Add(ffmpegPathTextBox);
        Controls.Add(note);
        Controls.Add(ok);
        Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    public void ApplyTo(LiveStreamSettings settings)
    {
        settings.Enabled = enabledCheckBox.Checked;
        settings.RtmpUrl = rtmpUrlTextBox.Text.Trim();
        settings.StreamKey = streamKeyTextBox.Text.Trim();
        settings.FfmpegPath = ffmpegPathTextBox.Text.Trim();
    }
}
