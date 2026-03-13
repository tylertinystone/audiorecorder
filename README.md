# AudioRecorderWinForms

一个基于 C# WinForms 的 Windows 托盘录制工具，支持**录屏 + 系统声音**（已恢复到稳定的本地录屏方式）。

- 托盘菜单开始/停止录制，默认常驻系统托盘。
- 点击开始前可选择视频保存位置（MP4）。
- 录制期间在右上角显示红色录制状态与已录制时长。
- 支持两种录制目标：
  - 全屏录制
  - 指定窗口录制（可先选择目标窗体）
- 支持状态窗体是否置顶。

## 运行

```bash
dotnet restore
dotnet run --project AudioRecorderWinForms/AudioRecorderWinForms.csproj
```

> 仅支持 Windows。
