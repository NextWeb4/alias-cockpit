# Toolchain Conflict Check

日期：2026-07-05  
状态：开发前工具链检查。

## 当前事实

- 当前目录不是 git 仓库。
- 本机全局已安装 .NET runtime 8/9，但未发现全局 .NET SDK。
- 已在项目本地 `.tools/dotnet` 安装并验证 .NET SDK 10.0.301，并补齐 .NET 8 runtime 8.0.28。
- Node.js、npm、Rust、Python、Git、SQLite CLI 可用。
- 用户要求 Windows 本地应用，不是网页。

## 调研结论

- Microsoft .NET support policy 显示 .NET 10 是 2026 年当前 LTS，支持到 2028；但当前 WinUI/WinRT 发布运行组合必须以 .NET 8 runtime 交付。
- Microsoft Windows App SDK / WinUI 3 官方文档定位为现代 Windows 桌面应用 UI 框架。
- .NET SDK-style Windows 桌面项目可使用 `net10.0-windows` TFM；本项目暂不采用它作为产品 TFM，因为已复现 Windows App SDK / WinRT Runtime 启动期兼容问题。
- Electron/Tauri 主 UI 与“不是网页”的明确需求冲突。

## 冲突检查

| 检查项 | 结论 |
| --- | --- |
| 与现有技术栈 | 当前无代码技术栈，不冲突 |
| 与目录结构 | 需要新增 `src/`, `tests/`, `.tools/` 或等价目录 |
| 与运行方式 | 当前无运行命令，后续需补 |
| 与构建方式 | 当前无构建命令，后续需补 |
| 与数据库设计 | SQLite 方案独立，不冲突 |
| 与配置系统 | 当前无配置系统，不冲突 |
| 与权限模型 | WinUI/.NET 可调用 Windows Credential Manager/DPAPI |
| 与离线/联网 | SDK 安装需要联网；应用运行设计仍离线优先 |
| 与许可证 | .NET/Windows App SDK 许可可接受；NuGet 依赖需逐项审计 |
| 与用户需求 | WinUI 符合 Windows 本地应用目标 |

## 推荐处理

- 使用项目本地 `.tools/dotnet` 安装 .NET 10 SDK，避免修改系统全局安装。已完成。
- 安装证据：`.\.tools\dotnet\dotnet.exe --info` 显示 SDK 10.0.301；`.\.tools\dotnet\dotnet.exe --list-runtimes` 显示 .NET 8.0.28 和 10.0.9 runtime。
- 若 WinUI 模板或构建依赖 Visual Studio/Windows SDK 阻塞，再评估 WPF 或 Rust native fallback。
- 创建代码骨架后必须更新 `AGENTS.md` 中运行、测试、构建、lint/format 命令。

## WinUI 探针记录

- `dotnet new search winui --columns-all` 找到 Microsoft 官方模板包 `Microsoft.WindowsAppSDK.WinUI.CSharp.Templates`。
- 模板安装成功，但版本为 `0.0.6-alpha`，因此正式项目不能盲信模板默认输出。
- `winui-mvvm` 探针项目构建通过。
- 模板参数未按预期写入 `TargetPlatformMinVersion`，正式项目必须手动设置。
- 手动锁定 WindowsAppSDK `1.8.260317003` 后探针构建通过，0 警告 0 错误。

## 安装记录

- `dotnet-install.ps1 -Channel 10.0` 经 `aka.ms` 下载失败。
- 通过官方 release metadata 解析到 SDK `10.0.301` 直接下载地址。
- 使用 `curl.exe` 下载 `dotnet-sdk-10.0.301-win-x64.zip` 成功。
- SHA-512 与官方 metadata 匹配：
  `38456e992c4df0ff0ac9fc5f28ff09a88543c0fc4e4deedffda9c4ebaf852c4519addacf28814ea77ea42ce2d37db812fae5ba1fe25f06364ca5a6027036387f`
- 解压至 `.tools/dotnet`。
- 追加安装 `.NET SDK 8.0.422` 到 `.tools/dotnet8`，SHA-512 与官方 metadata 匹配；将 `.tools/dotnet8\shared` 中的 .NET 8.0.28 runtime 复制到 `.tools/dotnet\shared`，用于 net8 testhost、benchmark 和 WinUI 运行验证。

## 来源

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- .NET 10 download: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- Windows App SDK docs: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
- Windows App SDK downloads: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
- WinUI 3 docs: https://learn.microsoft.com/en-us/windows/apps/winui/winui3/
