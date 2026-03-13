# AudioRecorderWinForms

一个基于 C# WinForms 的 Windows 托盘录制工具，支持**录屏 + 系统声音**，并可选同步推送 YouTube 直播。

- 托盘菜单开始/停止录制，默认常驻系统托盘。
- 点击开始前可选择视频保存位置（MP4）。
- 录制期间在右上角显示红色录制状态与已录制时长。
- 支持两种录制目标：
  - 全屏录制
  - 指定窗口录制（可先选择目标窗体）
- 支持状态窗体是否置顶。
- 可在“**YouTube 推流设置**”中配置 RTMP 地址、Stream Key 和 FFmpeg 路径，开启后将边录制边推流。

## 解决“创建 output 文件失败”

- 推流时已改为 **双输出**（本地 MP4 + RTMP）而不是 `tee`，减少 Windows 路径转义导致的失败。
- 录制前会尝试自动创建输出目录（如果不存在）。
- 若 FFmpeg 推流启动失败，会自动回退到最初的本地录屏（窗口句柄）方式。
- 窗口模式下不再自动切到全屏；若窗口句柄录制失败会直接报错，便于定位窗口捕获问题。
- 窗口录制会优先使用第一版同类方式（`WindowRecordingSource`）再回退句柄重载，提高普通窗口兼容性。
- `ScreenRecorderLib` 已切换到第一版兼容思路（较早稳定版本）以优先恢复普通窗口录制能力。
- 本地录屏前会先确保输出目录存在，减少 `failed to create output folder` 报错。

## YouTube 同步推流说明

1. 在 YouTube Live 获取 RTMP 地址与 Stream Key。
2. 托盘菜单打开“`YouTube 推流设置...`”，填写参数并启用。
3. 开始录制后，会同时保存 MP4 文件并推送到 RTMP。

> 依赖 FFmpeg（本机可执行），系统声音输入默认使用 `virtual-audio-capturer`（dshow 设备）。

## 运行

```bash
dotnet restore
dotnet run --project AudioRecorderWinForms/AudioRecorderWinForms.csproj
```

> 仅支持 Windows。
