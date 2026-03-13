using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioRecorderWinForms;

public sealed class WindowPickerForm : Form
{
    private readonly ListBox windowsList;

    public WindowItem? SelectedWindow => windowsList.SelectedItem as WindowItem;

    public WindowPickerForm()
    {
        Text = "选择要录制的窗口";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(520, 380);

        windowsList = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 280,
            DisplayMember = nameof(WindowItem.Title)
        };

        var refreshButton = new Button
        {
            Text = "刷新",
            Width = 80,
            Location = new Point(220, 292)
        };
        refreshButton.Click += (_, _) => ReloadWindows();

        var okButton = new Button
        {
            Text = "确定",
            Width = 80,
            Location = new Point(310, 292),
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "取消",
            Width = 80,
            Location = new Point(400, 292),
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(windowsList);
        Controls.Add(refreshButton);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        ReloadWindows();
    }

    private void ReloadWindows()
    {
        windowsList.Items.Clear();
        foreach (var item in NativeWindowHelper.GetRecordableWindows())
        {
            windowsList.Items.Add(item);
        }

        if (windowsList.Items.Count > 0)
        {
            windowsList.SelectedIndex = 0;
        }
    }
}
