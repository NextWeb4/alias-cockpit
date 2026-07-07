# ADR 0001: Windows Native UI Stack

日期：2026-07-05  
状态：Proposed

## Context

用户明确要求 Windows 本地应用，不是网页。应用需要高密度列表、快捷搜索、过滤、统计 Dashboard、托盘/快捷键、本地凭据、SQLite、UI 自动化测试。

当前本机事实：

- .NET runtime 已安装。
- .NET SDK 未安装。
- Node/Rust/Python 可用。
- 不存在已有代码栈。
- 2026-07 官方支持策略显示 .NET 10 是当前 LTS，但当前 Windows App SDK / WinRT Runtime 发布产物在 `net10.0-windows` self-contained 启动时会触发类型/方法加载异常。

## Options

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| WinUI 3 + Windows App SDK + C# | Windows 原生、Fluent、系统集成好、适合桌面工具 | 当前缺 .NET SDK；打包复杂 |
| WPF + C# | 成熟、稳定、Windows 桌面生态强 | 现代 Fluent 与控件体验弱一些 |
| Avalonia | MVVM 与跨平台好 | 不是 Windows 原生目标 |
| Rust egui/Iced | 当前 Rust 可用、native binary | Fluent/可访问性/复杂数据表风险 |
| Tauri/Electron | 开发快、生态大 | WebView/Chromium，与“不是网页”冲突 |

## Decision

采用 WinUI 3 + Windows App SDK + C#，当前产品目标 TFM 为 `net8.0-windows10.0.26100.0`。

工具链探针结果：

- Microsoft 官方模板可用，但模板包版本显示 `0.0.6-alpha`。
- 模板默认/最新线会生成 WindowsAppSDK 2.2.0，并在 Debug 输出中带来 AI/WebView/ONNX 等当前不需要的依赖面。
- WindowsAppSDK `1.8.260317003` 在 .NET 10 self-contained 发布下触发 `ICustomQueryInterface` TypeLoadException。
- WindowsAppSDK `2.2.0` 在 `net10.0-windows` self-contained 发布下仍触发 WinRT Runtime 类型/方法加载异常。
- 正式项目采用 WindowsAppSDK `2.2.0` + `net8.0-windows10.0.26100.0`，已通过 build/test/publish 和 5 秒启动冒烟验证。

Fallback：

1. 若 WinUI 工具链无法安装或构建失败，评估 WPF。
2. 若 .NET SDK 无法使用，评估 Rust egui/Iced native fallback。

明确不采用 Electron/Tauri 作为主 UI。

## Consequences

- 项目本地 `.tools/dotnet` 已安装 .NET 10 SDK 10.0.301，并补齐 .NET 8 runtime 8.0.28；`.slnx` 由 .NET 10 SDK 编排，产品发布运行时使用 .NET 8。
- 正式 App 项目需要手动锁 WinUI 依赖版本，不完全依赖模板默认输出。
- 需要选择 WinUI 数据表/虚拟化方案并实测 10k/50k alias。
- 测试工具优先评估 FlaUI/WinAppDriver。
- `AGENTS.md` 中命令待工具链落地后更新。

## Conflict Check

| 检查项 | 结果 |
| --- | --- |
| 与用户明确需求 | WinUI 符合；Electron/Tauri 冲突 |
| 与当前项目 | 当前无代码，无冲突 |
| 与本机工具链 | 缺 .NET SDK，阻塞开发 |
| 与测试目标 | WinUI UI 自动化需验证 |
| 与许可证 | Microsoft/WinUI 生态整体可接受，具体依赖再审计 |
