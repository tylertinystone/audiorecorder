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
    private readonly ToolStripMenuItem fullScreenModeItem;
    private readonly ToolStripMenuItem windowModeItem;
    private readonly ToolStripMenuItem pickWindowItem;
    private readonly ToolStripMenuItem liveConfigItem;

    private readonly AudioRecorder recorder = new();
    private readonly LiveStreamRecorder liveRecorder = new();
    private readonly RecordingStatusForm statusForm = new();
    private readonly LiveStreamSettings liveSettings = new();

    private CaptureTargetMode captureMode = CaptureTargetMode.FullScreen;
    private WindowItem? selectedWindow;

    public TrayApplicationContext()
    {
        startItem = new ToolStripMenuItem("开始录制（屏幕+系统声音）", null, StartRecording);
        stopItem = new ToolStripMenuItem("停止录制", null, StopRecording) { Enabled = false };
        topMostItem = new ToolStripMenuItem("状态窗口置顶") { Checked = true, CheckOnClick = true };
        topMostItem.CheckedChanged += (_, _) => statusForm.TopMost = topMostItem.Checked;

        fullScreenModeItem = new ToolStripMenuItem("录制全屏") { CheckOnClick = true, Checked = true };
        windowModeItem = new ToolStripMenuItem("录制指定窗口") { CheckOnClick = true };
        pickWindowItem = new ToolStripMenuItem("选择目标窗口...", null, PickWindow);
        liveConfigItem = new ToolStripMenuItem("YouTube 推流设置...", null, OpenLiveConfig);

        fullScreenModeItem.Click += (_, _) => SetMode(CaptureTargetMode.FullScreen);
        windowModeItem.Click += (_, _) => SetMode(CaptureTargetMode.SpecificWindow);

        var menu = new ContextMenuStrip();
        menu.Items.Add(startItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(fullScreenModeItem);
        menu.Items.Add(windowModeItem);
        menu.Items.Add(pickWindowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(liveConfigItem);
        menu.Items.Add(topMostItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());

        trayIcon = new NotifyIcon
        {
            Text = "录屏录音工具",
            Icon = SystemIcons.Shield,
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.DoubleClick += StartRecording;

        recorder.ElapsedChanged += (_, elapsed) => statusForm.UpdateElapsed(elapsed);
        recorder.RecordingStopped += (_, path) => HandleStopped(path);
        recorder.ErrorOccurred += (_, message) => HandleError(message);

        liveRecorder.ElapsedChanged += (_, elapsed) => statusForm.UpdateElapsed(elapsed);
        liveRecorder.RecordingStopped += (_, path) => HandleStopped(path);
        liveRecorder.ErrorOccurred += (_, message) => HandleError(message);
    }

    private void OpenLiveConfig(object? sender, EventArgs? e)
    {
        using var form = new LiveStreamConfigForm(liveSettings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            form.ApplyTo(liveSettings);
            trayIcon.ShowBalloonTip(1200, "推流设置", liveSettings.Enabled ? "已启用录制同步推流" : "已关闭同步推流", ToolTipIcon.Info);
        }
    }

    private void HandleStopped(string path)
    {
        statusForm.Hide();
        startItem.Enabled = true;
        stopItem.Enabled = false;
        trayIcon.ShowBalloonTip(1500, "录制完成", $"文件已保存：{path}", ToolTipIcon.Info);
    }

    private void HandleError(string message)
    {
        statusForm.Hide();
        startItem.Enabled = true;
        stopItem.Enabled = false;
        MessageBox.Show(message, "录制错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void SetMode(CaptureTargetMode mode)
    {
        captureMode = mode;
        fullScreenModeItem.Checked = mode == CaptureTargetMode.FullScreen;
        windowModeItem.Checked = mode == CaptureTargetMode.SpecificWindow;
    }

    private void PickWindow(object? sender, EventArgs? e)
    {
        using var picker = new WindowPickerForm();
        if (picker.ShowDialog() == DialogResult.OK && picker.SelectedWindow is not null)
        {
            selectedWindow = picker.SelectedWindow;
            SetMode(CaptureTargetMode.SpecificWindow);
            trayIcon.ShowBalloonTip(1200, "窗口已选择", selectedWindow.Title, ToolTipIcon.Info);
        }
    }

    private void StartRecording(object? sender, EventArgs? e)
    {
        if (recorder.IsRecording || liveRecorder.IsRunning)
        {
            return;
        }

        if (captureMode == CaptureTargetMode.SpecificWindow && selectedWindow is null)
        {
            PickWindow(sender, e);
            if (selectedWindow is null)
            {
                return;
            }
        }

        using var dialog = new SaveFileDialog
        {
            Title = "选择录屏文件保存位置",
            Filter = "MP4 视频 (*.mp4)|*.mp4",
            DefaultExt = "mp4",
            AddExtension = true,
            FileName = $"ScreenRecord_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var started = false;
        if (liveSettings.Enabled)
        {
            started = liveRecorder.Start(dialog.FileName, captureMode, selectedWindow?.Title, liveSettings);
            if (started)
            {
                trayIcon.ShowBalloonTip(1000, "直播中", "正在录制并推送到 YouTube RTMP", ToolTipIcon.Info);
            }
            else
            {
                var handleFallback = captureMode == CaptureTargetMode.SpecificWindow ? selectedWindow!.Handle : 0;
                recorder.Start(dialog.FileName, captureMode, handleFallback);
                started = true;
                trayIcon.ShowBalloonTip(1400, "推流失败已回退", "已自动切换为本地录屏（句柄录制方式）", ToolTipIcon.Warning);
            }
        }
        else
        {
            var handle = captureMode == CaptureTargetMode.SpecificWindow ? selectedWindow!.Handle : 0;
            recorder.Start(dialog.FileName, captureMode, handle);
            started = true;
        }

        if (!started)
        {
            return;
        }

        startItem.Enabled = false;
        stopItem.Enabled = true;
        statusForm.TopMost = topMostItem.Checked;
        statusForm.SetModeText(captureMode, selectedWindow?.Title);
        statusForm.ShowAtTopRight();
    }

    private void StopRecording(object? sender, EventArgs? e)
    {
        recorder.Stop();
        liveRecorder.Stop();
    }

    protected override void ExitThreadCore()
    {
        recorder.Dispose();
        liveRecorder.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        statusForm.Dispose();
        base.ExitThreadCore();
    }
}
