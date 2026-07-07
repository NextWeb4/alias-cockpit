# Dependency Audit

日期：2026-07-05  
状态：首批工程骨架依赖审计。

## 本轮允许引入

| 依赖 | 来源 | 许可证 | 核心能力 | 优点 | 缺点 | 维护状态 | 契合度 | 可能冲突点 | 是否采用 | 采用方式 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Microsoft.WindowsAppSDK 2.2.0 | NuGet / Microsoft | package license file, Microsoft | WinUI 3 / Windows App SDK | Windows 原生、Fluent、官方维护；与当前 `net8.0-windows` self-contained 发布运行兼容 | 仍有 Windows SDK 打包复杂度，发布目录体积较大；在 `net10.0-windows` self-contained 下仍触发 WinRT 启动期类型/方法加载异常 | 活跃 | 高 | 必须重新跑 build/publish/启动验证；不要回退到 1.8，不要改回 net10 目标框架，除非重新验证启动 | 采用 | App 项目锁 2.2.0 |
| Microsoft.Windows.SDK.BuildTools 10.0.26100.7705 | NuGet / Microsoft | Microsoft SDK 组件 | Windows SDK build tasks | WinUI 构建必需 | 体积大，平台绑定 | 活跃 | 高 | 需固定版本保证可重复构建 | 采用 | App 项目锁版本 |
| Microsoft.Windows.SDK.BuildTools.WinApp 0.3.1 | NuGet / Microsoft | MIT | `dotnet run` 支持 | 便于 CLI 启动 packaged WinUI | 仍需验证运行行为 | 活跃 | 中高 | 仅用于本地运行体验 | 采用 | App 项目锁版本 |
| CommunityToolkit.Mvvm 8.4.2 | NuGet / Microsoft CommunityToolkit | MIT | MVVM base/commands | 官方模板使用，减少样板代码 | 源生成需注意 build 诊断 | 活跃 | 高 | 不应把业务逻辑放进 ViewModel | 采用 | App 层 ViewModel |
| xunit 2.9.3 | NuGet | Apache-2.0 | 单元测试 | 成熟、简单 | 非 Microsoft 官方默认 | 活跃 | 高 | 无明显冲突 | 采用 | Core 单元测试 |
| xunit.runner.visualstudio 3.1.5 | NuGet | Apache-2.0 | `dotnet test` 集成 | 稳定版，避免 preview | 仅测试依赖 | 活跃 | 高 | 无 | 采用 | 测试项目 |
| Microsoft.NET.Test.Sdk | NuGet / Microsoft | MIT | 测试 SDK | 官方测试入口 | 需锁稳定版本 | 活跃 | 高 | 无 | 采用 | 测试项目 |
| Microsoft.Data.Sqlite 10.0.9 | NuGet / Microsoft | MIT | SQLite ADO.NET provider | 官方维护、轻量、可直接控制 schema/migration | 不提供整库加密；需要自己管理 SQL 和迁移 | 活跃 | 高 | 加密需后续 SQLCipher/字段加密补强 | 采用 | Infrastructure SQLite 仓储 |
| SQLitePCLRaw.bundle_e_sqlite3 3.0.3 | NuGet / Eric Sink | Apache-2.0 | SQLite native bundle | 让 Microsoft.Data.Sqlite 在本地测试/运行稳定加载 SQLite | 增加 native bundle 体积 | 活跃 | 高 | 发布包体积增加 | 采用 | Infrastructure 运行时依赖 |

## 本轮不引入

| 依赖 | 未采用原因 |
| --- | --- |
| SQLCipher | 需要打包/许可证/原生依赖二次验证 |
| CsvHelper / Sylvan.Data.Csv | 第一版导入导出先用小范围标准库实现，避免未审计 CSV 依赖；复杂 CSV 兼容性后续再评估 |
| EF Core | 当前 schema 小且需要严格 migration/FTS 控制；ORM 复杂度超过收益 |
| BenchmarkDotNet | Benchmark 项目下一步再加，避免骨架一次性过重 |
| 图表/二维码库 | UI 先建立信息架构，不提前引入 |
| Provider SDK | 先定义 adapter，不引入服务特定 SDK |

## 冲突检查

- 与用户需求：WinUI 是 Windows 本地应用，符合“不是网页”。
- 与离线边界：构建/restore 需要联网；应用运行不因这些依赖改变联网策略。
- 与许可证：当前首批依赖未发现 AGPL/GPL 风险。
- 与复杂度：只引入骨架必要依赖，数据库/加密/图表/QR/Benchmark 后续按功能审计。
- 与离线模式：SQLite 为本地文件数据库，不新增运行时联网行为。
- 与安全边界：本轮只落明文开发数据库，token/secret 不入库；整库加密和 Credential Manager 后续单独落地。

## 2026-07-05 追加依赖决策

- 将 `Microsoft.WindowsAppSDK` 从 `1.8.260317003` 升级到 `2.2.0`。
- 触发原因：`win-x64` 自包含 folder publish 的 `AliasCockpit.App.exe` 在本机 .NET 10 运行时下启动后立即退出，Windows Application 日志显示 Windows App SDK bootstrap 触发 `System.TypeLoadException: Could not load type 'System.Runtime.InteropServices.ICustomQueryInterface'`。
- 继续验证发现：Windows App SDK 2.2.0 在 `net10.0-windows` self-contained 下仍会在 WinRT Runtime 自动初始化阶段触发 `Environment.SetEnvironmentVariable` MissingMethod 和 `System.Collections.Generic.List<T>` TypeLoad 异常。
- 最终处理：项目源码目标框架切到 `net8.0` / `net8.0-windows10.0.26100.0`；`.slnx` 仍由 `.tools\dotnet` 的 .NET 10 SDK 编排，`.tools\dotnet` 中补齐 .NET 8 runtime 用于 testhost/benchmark。
- 冲突检查：未新增产品运行依赖类型，仍是 Microsoft 官方 WinUI/Windows App SDK；net8 build/test/publish、发布目录启动和 portable artifact 启动验证通过；应用运行时联网边界不变。
- 回滚方式：若后续必须回到 Windows App SDK 1.8 或 net10 TFM，需要同时重新审计 WinRT Runtime 兼容性并重新验证 publish exe 启动。
