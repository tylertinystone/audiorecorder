# AudioRecorderWinForms

一个基于 C# WinForms 的 Windows 托盘录制工具，支持**录屏 + 系统声音**，并可选同步推送 YouTube 直播。

- 托盘菜单开始/停止录制，默认常驻系统托盘。
- 点击开始前可选择视频保存位置（MP4）。
- 录制期间在右上角显示红色录制状态与已录制时长。
- 支持两种录制目标：
  - 全屏录制
  - 指定窗口录制（可先选择目标窗体）
- 支持状态窗体是否置顶。
- 可在“**YouTube 推流设置**”中配置 RTMP 地址、Stream Key、FFmpeg 路径和代理地址，开启后将边录制边推流。

## 解决“创建 output 文件失败”

- 推流时已改为 **双输出**（本地 MP4 + RTMP）而不是 `tee`，减少 Windows 路径转义导致的失败。
- 推流音频优先使用 **WASAPI 默认输出回环**（更接近本地 MP4 的系统声音采集）；若不可用再尝试 `virtual-audio-capturer`。
- 两种系统音频方案都不可用时，会自动降级为“仅画面推流”（避免 FFmpeg 直接退出）。
- 录制前会尝试自动创建输出目录（如果不存在）。
- 若 FFmpeg 推流启动失败，会自动回退到最初的本地录屏（窗口句柄）方式。
- 窗口模式恢复为最初实现方式：优先使用窗口句柄调用 `Record(path, nint/IntPtr)`，不可用时回退 `Record(path)`。
- 本地录屏前会先确保输出目录存在，减少 `failed to create output folder` 报错。

## YouTube 同步推流说明

1. 在 YouTube Live 获取 RTMP 地址与 Stream Key。
2. 托盘菜单打开“`YouTube 推流设置...`”，填写参数并启用（如需代理可填写 `http://127.0.0.1:7890`）。
3. 开始录制后，会同时保存 MP4 文件并推送到 RTMP。

> 依赖 FFmpeg（本机可执行）；系统声音优先走 WASAPI 回环，代理优先级为：设置页填写 > 环境变量（HTTP(S)_PROXY/ALL_PROXY）> Windows 系统代理。

## 运行

```bash
dotnet restore
dotnet run --project AudioRecorderWinForms/AudioRecorderWinForms.csproj
```

> 仅支持 Windows。
