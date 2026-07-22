<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/English-0969da?style=flat-square" alt="English"></a>
  <a href="README.zh-CN.md"><img src="https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-c8102e?style=flat-square" alt="简体中文"></a>
  <a href="README.ja.md"><img src="https://img.shields.io/badge/%E6%97%A5%E6%9C%AC%E8%AA%9E-8250df?style=flat-square" alt="日本語"></a>
</p>

# Alias Cockpit

一款本地优先的 Windows 桌面邮箱别名工作台，用于生成、标记、保存、导入、导出以及按需同步邮箱别名。

![最近提交](https://img.shields.io/github/last-commit/NextWeb4/alias-cockpit?style=flat-square)
![仓库大小](https://img.shields.io/github/repo-size/NextWeb4/alias-cockpit?style=flat-square)
![GitHub 星标](https://img.shields.io/github/stars/NextWeb4/alias-cockpit?style=flat-square)
![C# 与 .NET 8](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)

## 当前范围

Alias Cockpit 是一个持续开发中的 WinUI 3/.NET 应用。当前主界面是离线 Gmail/Outlook 邮箱别名扩展器，支持保存输入历史、站点/用途/颜色标记、已标记/未标记筛选和复制操作。仓库还包含：

- 核心别名生成、熵估算、CSV 导入导出预演、审计事件、墓碑记录和提供商能力模型；
- 别名、保存地址、提供商账号和审计数据的本地 SQLite 仓储；
- Windows Credential Manager 密钥存储；
- SimpleLogin 与 addy.io 模拟适配器，以及 HTTP 适配器基础；
- xUnit 单元、压力、ViewModel 和基础设施测试；
- 可重复执行的生成、CSV 与 SQLite 基准测试；
- 文件夹发布、便携 ZIP、MSI、setup EXE 和 GitHub Release 工具。

加密同步、高级提供商同步和完整 UI 自动化尚未完成。应用正常启动时不会调用真实提供商 API。

## 环境要求

- Windows 10 2004（`10.0.19041.0`）或更高版本。
- 仓库忽略的本地 `.tools\dotnet` 工具链。现有项目文档记录：`.slnx` 由 .NET 10 SDK 编排，产品项目使用 .NET 8 Runtime。
- 运行 UI 冒烟测试需要桌面会话。

`.tools\dotnet` 目录不会包含在全新克隆中。使用仓库命令前需要自行准备兼容的 SDK/Runtime；当前快照没有为这个被忽略的工具链提供自动安装器。

## 运行

```powershell
.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj
```

应用在以下位置读写本地元数据：

```text
%LocalAppData%\AliasCockpit\aliases.sqlite
```

该开发数据库目前未加密。它可以保存别名元数据、输入邮箱历史、标记、审计数据和提供商 `secret_ref`，但不得保存提供商令牌或 API 密钥。

## 典型工作流

1. 输入 Gmail 或 Outlook 地址，按行填写标签，并选择要生成的候选数量。
2. 在地址和目标表单支持时启用 Gmail 点号别名和/或 `+tag` 别名；不支持的域名会禁用点号选项。
3. 查看结果摘要，然后使用“全部、点号、加号、已标记、未标记”筛选器缩小列表。
4. 选中一个别名并保存站点、用途和颜色标记；需要重复使用的地址可以保存到本地历史。
5. 复制选中的别名或筛选后的结果。提供商操作仍是明确的后续操作；生成本地别名不会调用提供商。

## 构建、测试与格式检查

```powershell
.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release
.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal
```

仓库文档指出 `dotnet format` 可能出现非阻塞的 workspace-load warning；应结合退出码以及 build/test 结果判断门禁。

## 发布打包

准备交付文件时运行完整发布验证：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1
```

该脚本会执行构建、测试、基准测试和格式门禁，发布应用，裁剪已经审计的未使用 WinAppSDK 文件，重建便携 ZIP、MSI 和 setup EXE，校验包内容，对发布版与便携版 EXE 做启动冒烟，并运行基础 UI 冒烟测试。

也可以分别执行：

```powershell
.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-msi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-setup-exe.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-github-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts
```

预期产物：

| 产物 | 用途 |
| --- | --- |
| `src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe` | 完整文件夹发布中的应用 EXE，不是安装包或单文件包 |
| `artifacts\AliasCockpit-win-x64-portable.zip` | 包含完整发布目录的便携包 |
| `artifacts\AliasCockpit-win-x64.msi` | WiX 生成的每机安装 MSI，安装可能需要提权 |
| `artifacts\AliasCockpit-win-x64-setup.exe` | 内嵌 MSI 的 WiX Burn 安装器 |

发布后的 EXE 必须与相邻 WinUI/.NET Runtime 文件一起保留。WiX 仅用于构建。`scripts\publish-github-release.ps1` 以 `NextWeb4/alias-cockpit` 和 `v1.0.0` 为目标，仅在运行时从 `GITHUB_TOKEN`、`GH_TOKEN`、Git Credential Manager 或 Codex 集成 helper 读取凭据。

## 项目结构

| 路径 | 职责 |
| --- | --- |
| `src/AliasCockpit.App/` | WinUI 3 应用壳、主页面、ViewModel、剪贴板和桌面集成 |
| `src/AliasCockpit.Core/` | 不依赖 UI 的别名、生成、CSV、审计、提供商、密钥、安全和扩展器契约 |
| `src/AliasCockpit.Infrastructure/` | SQLite、Windows Credential Manager 和提供商适配器 |
| `tests/AliasCockpit.App.Tests/` | 不启动 WinUI 窗口的 ViewModel 测试 |
| `tests/AliasCockpit.Core.Tests/` | 领域行为的单元与压力测试 |
| `tests/AliasCockpit.Infrastructure.Tests/` | SQLite、凭据存储和提供商适配器集成测试 |
| `benchmarks/AliasCockpit.Benchmarks/` | 生成、CSV 预演与 SQLite 基线 |
| `docs/` | 调研、架构决策、安全模型和发布说明 |
| `scripts/` | 品牌、发布、打包、清理、Release 与冒烟测试自动化 |

## 数据与安全边界

- Gmail 点号别名仅适用于 Gmail/Googlemail 地址，不应默认套用于 Google Workspace 自定义域。
- 并非所有第三方表单都接受 `+tag` 别名。
- 提供商令牌必须由 `WindowsCredentialManagerSecretStore` 保存；SQLite 只保存密钥模型生成的引用。
- 不要在站点、用途、标签、标记或保存地址字段写入密码、Token、恢复码或其他敏感信息。
- 批量禁用/删除提供商别名前必须先生成计划；删除还需要明确确认和审计记录。
- HTTP 适配器被显式调用时可以验证密钥并执行支持的别名操作，但模拟适配器和伪造 HTTP 处理器不能证明真实账号端到端可用。

## 作者

- HaoXiang Huang
- [Rays688888@Gmail.com](mailto:Rays688888@Gmail.com)
- <https://nextweb4.github.io/>

图标源文件为 `src/AliasCockpit.App/Assets/AppIcon.ico`；应用 EXE、WinUI 窗口、开始菜单快捷方式、“添加/删除程序”条目和 setup bundle 都会使用它。

## 维护与贡献

- 领域行为放在 `src/AliasCockpit.Core/`，操作系统与提供商集成放在 `src/AliasCockpit.Infrastructure/`，界面工作放在 `src/AliasCockpit.App/`；同时在对应测试项目补充覆盖。
- 修改持久化、凭据、提供商操作或发布边界前，请先阅读[原生 UI 架构决策](docs/architecture/adr/0001-windows-native-ui-stack.md)、[测试策略](docs/architecture/test-strategy.md)和[威胁模型](docs/security/threat-model.md)。
- 常规修改需要运行上文的构建、测试、基准测试和格式命令；打包修改还必须走完 `scripts\verify-release.ps1` 并检查每个产物。
- 行为、命令、产物名、安全限制或许可发生变化时，必须同步更新三份 README。

## 许可证

审计时未在仓库中发现 `LICENSE` 文件。在所有者另行声明授权条款前，不应把该仓库视为已经授予开源许可。


