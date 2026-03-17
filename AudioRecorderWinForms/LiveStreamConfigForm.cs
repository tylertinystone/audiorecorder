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
    private readonly TextBox proxyTextBox;

    public LiveStreamConfigForm(LiveStreamSettings settings)
    {
        Text = "YouTube 直播推流设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(700, 300);

        enabledCheckBox = new CheckBox { Text = "启用录制同时推流", Location = new Point(20, 20), AutoSize = true, Checked = settings.Enabled };

        var urlLabel = new Label { Text = "RTMP 地址:", Location = new Point(20, 58), AutoSize = true };
        rtmpUrlTextBox = new TextBox { Location = new Point(110, 55), Width = 560, Text = settings.RtmpUrl };

        var keyLabel = new Label { Text = "Stream Key:", Location = new Point(20, 93), AutoSize = true };
        streamKeyTextBox = new TextBox { Location = new Point(110, 90), Width = 560, Text = settings.StreamKey };

        var ffmpegLabel = new Label { Text = "FFmpeg 路径:", Location = new Point(20, 128), AutoSize = true };
        ffmpegPathTextBox = new TextBox { Location = new Point(110, 125), Width = 465, Text = settings.FfmpegPath };

        var proxyLabel = new Label { Text = "代理(可选):", Location = new Point(20, 163), AutoSize = true };
        proxyTextBox = new TextBox { Location = new Point(110, 160), Width = 560, Text = settings.ProxyUrl };

        var browseButton = new Button
        {
            Text = "浏览...",
            Location = new Point(585, 123),
            Width = 85
        };
        browseButton.AccessibleName = "浏览FFmpeg路径";
        browseButton.AccessibleDescription = "选择 ffmpeg.exe 可执行文件";
        browseButton.Click += (_, _) => BrowseFfmpegPath();

        var note = new Label
        {
            Text = "提示：优先用 WASAPI 抓系统声音；若失败回退到 dshow 的 virtual-audio-capturer。代理可填 http://127.0.0.1:7890",
            Location = new Point(20, 193),
            Width = 650,
            Height = 34
        };

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(510, 230), Width = 75 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(595, 230), Width = 75 };

        Controls.Add(enabledCheckBox);
        Controls.Add(urlLabel);
        Controls.Add(rtmpUrlTextBox);
        Controls.Add(keyLabel);
        Controls.Add(streamKeyTextBox);
        Controls.Add(ffmpegLabel);
        Controls.Add(ffmpegPathTextBox);
        Controls.Add(proxyLabel);
        Controls.Add(proxyTextBox);
        Controls.Add(browseButton);
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
        settings.ProxyUrl = proxyTextBox.Text.Trim();
    }


    private string TryGetInitialDirectory()
    {
        var path = ffmpegPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        if (System.IO.Directory.Exists(path))
        {
            return path;
        }

        var dir = System.IO.Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            : dir;
    }

    private void BrowseFfmpegPath()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 FFmpeg 可执行文件",
            Filter = "可执行文件 (*.exe)|*.exe|批处理文件 (*.cmd;*.bat)|*.cmd;*.bat|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = TryGetInitialDirectory(),
            FileName = string.IsNullOrWhiteSpace(ffmpegPathTextBox.Text) ? "ffmpeg.exe" : ffmpegPathTextBox.Text
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            ffmpegPathTextBox.Text = dialog.FileName;
        }
    }
}
