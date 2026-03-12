# AudioRecorderWinForms

一个基于 C# WinForms 的 Windows 系统声音录制小工具：

- 使用 `WASAPI Loopback` 录制系统播放音频。
- 点击托盘菜单“开始录制系统声音”后，先选择输出 WAV 文件保存位置。
- 录制期间会在屏幕右上角显示红色“正在录制”状态与已录制时长。
- 托盘菜单可停止录制。
- 默认运行在系统托盘，不占任务栏。
- 可通过“状态窗口置顶”菜单控制状态浮窗是否始终置顶。

## 运行

```bash
dotnet restore
dotnet run --project AudioRecorderWinForms/AudioRecorderWinForms.csproj
```

> 仅支持 Windows。
