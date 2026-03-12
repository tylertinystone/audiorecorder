using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioRecorderWinForms;

public sealed class RecordingStatusForm : Form
{
    private readonly Label statusLabel;
    private readonly Label timeLabel;

    public RecordingStatusForm()
    {
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;
        Size = new Size(260, 90);

        statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.Red,
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            Location = new Point(14, 12),
            Text = "● 正在录制系统声音"
        };

        timeLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Consolas", 16, FontStyle.Bold),
            Location = new Point(16, 38),
            Text = "00:00:00"
        };

        Controls.Add(statusLabel);
        Controls.Add(timeLabel);
    }

    public void ShowAtTopRight()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Right - Width - 16, area.Top + 16);
        UpdateElapsed(TimeSpan.Zero);
        Show();
    }

    public void UpdateElapsed(TimeSpan elapsed)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<TimeSpan>(UpdateElapsed), elapsed);
            return;
        }

        timeLabel.Text = $"{elapsed:hh\\:mm\\:ss}";
    }
}
