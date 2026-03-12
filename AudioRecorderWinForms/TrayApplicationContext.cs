using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioRecorderWinForms;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem startItem;
    private readonly ToolStripMenuItem stopItem;
    private readonly ToolStripMenuItem topMostItem;

    private readonly AudioRecorder recorder = new();
    private readonly RecordingStatusForm statusForm = new();

    public TrayApplicationContext()
    {
        startItem = new ToolStripMenuItem("开始录制系统声音", null, StartRecording);
        stopItem = new ToolStripMenuItem("停止录制", null, StopRecording) { Enabled = false };
        topMostItem = new ToolStripMenuItem("状态窗口置顶") { Checked = true, CheckOnClick = true };
        topMostItem.CheckedChanged += (_, _) => statusForm.TopMost = topMostItem.Checked;

        var menu = new ContextMenuStrip();
        menu.Items.Add(startItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(topMostItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        trayIcon = new NotifyIcon
        {
            Text = "系统声音录制器",
            Icon = SystemIcons.Shield,
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.DoubleClick += StartRecording;

        recorder.ElapsedChanged += (_, elapsed) => statusForm.UpdateElapsed(elapsed);
        recorder.RecordingStopped += (_, path) =>
        {
            statusForm.Hide();
            startItem.Enabled = true;
            stopItem.Enabled = false;
            trayIcon.ShowBalloonTip(1500, "录制完成", $"文件已保存：{path}", ToolTipIcon.Info);
        };
        recorder.ErrorOccurred += (_, message) =>
        {
            statusForm.Hide();
            startItem.Enabled = true;
            stopItem.Enabled = false;
            MessageBox.Show(message, "录制错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
    }

    private void StartRecording(object? sender, EventArgs? e)
    {
        if (recorder.IsRecording)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "选择录音文件保存位置",
            Filter = "WAV 文件 (*.wav)|*.wav",
            DefaultExt = "wav",
            AddExtension = true,
            FileName = $"SystemAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        recorder.Start(dialog.FileName);
        startItem.Enabled = false;
        stopItem.Enabled = true;
        statusForm.TopMost = topMostItem.Checked;
        statusForm.ShowAtTopRight();
    }

    private void StopRecording(object? sender, EventArgs? e)
    {
        recorder.Stop();
    }

    protected override void ExitThreadCore()
    {
        recorder.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        statusForm.Dispose();
        base.ExitThreadCore();
    }
}
