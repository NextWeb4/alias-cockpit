# AGENTS.md

## 1. 项目结构

- 当前目录 `D:\Codex\BM` 已初始化为 WinUI/.NET 本地应用项目：根目录包含 `AliasCockpit.slnx`、`Directory.Build.props`、`Directory.Packages.props`、`.editorconfig`、`AGENTS.md`。
- `src/AliasCockpit.App/` 是 Windows WinUI 3 本地应用壳，只负责窗口、页面、ViewModel、剪贴板和桌面交互；当前主页面是本地 Email Alias Expander 工具，并通过 Core 仓储接口保存输入邮箱历史和 alias 标记。
- `src/AliasCockpit.App/Assets/AppIcon.ico` 是应用图标源；必须同时通过 App 项目的 `ApplicationIcon`、`MainWindow.AppWindow.SetIcon` 和 MSI 快捷方式 `Icon` 写入，不得只替换 PNG/ICO 文件后直接打包。
- `src/AliasCockpit.Core/` 是纯业务核心，当前包含 Alias 生成算法、Gmail/Outlook 邮箱别名裂变、保存输入邮箱接口、熵估算、审计事件模型、Provider 能力模型、ProviderAccount 和 Provider adapter 抽象，不得引用 WinUI、HTTP、SQLite 或系统剪贴板。
- `src/AliasCockpit.Infrastructure/` 是基础设施层，当前包含 SQLite alias/saved email/provider account/audit log 仓储、Windows Credential Manager secret store、SimpleLogin/addy.io mock adapter 和 SimpleLogin/addy.io HTTP adapter；不得反向引用 App/UI。
- `tests/AliasCockpit.Core.Tests/` 是核心单元/压力测试。
- `tests/AliasCockpit.App.Tests/` 是 App/ViewModel 单元测试，不启动 WinUI 窗口；当前覆盖有标记/未标记筛选行为。
- `tests/AliasCockpit.Infrastructure.Tests/` 是 SQLite 集成测试、Windows Credential Manager 集成测试和 mock Provider adapter 测试。
- `benchmarks/AliasCockpit.Benchmarks/` 是无第三方依赖的基础性能测量入口。
- `docs/research/` 保存调研证据，`docs/architecture/` 保存方案选型与产品架构，`docs/security/` 保存安全威胁模型。
- `.tools/` 用于本地隔离工具链和下载缓存，已通过 `.gitignore` 忽略，不属于产品源码。
- 新增代码前必须先明确应用边界：Windows 本地应用、邮箱别名生成/管理/同步/导入导出，不得把主产品实现成网页应用。
- 后续新增目录时必须保持职责清晰：应用 UI、核心业务、数据持久化、同步、Provider/API 集成、安全/加密、测试与文档应分层放置。
- 调研、选型、安全、架构文档必须继续放在 `docs/` 对应子目录，不得散落在根目录。
- GitHub 发布审计记录位于 `docs/architecture/github-publishing-audit-2026-07-07.md`；Release 说明放在 `docs/release/`，不要把 release 说明散落到根目录。

## 2. 运行命令

- 本地 SDK 验证：`.\.tools\dotnet\dotnet.exe --info`。
- 本地 runtime 验证：`.\.tools\dotnet\dotnet.exe --list-runtimes` 必须能看到 `Microsoft.NETCore.App 8.0.28` 和 `10.0.9`；当前 `.slnx` 由 .NET 10 SDK 编排，产品项目目标框架为 .NET 8。
- 桌面应用运行候选命令：`.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj`。
- 桌面应用当前主页面做本地 Gmail/Outlook 邮箱别名裂变，并读写 `%LocalAppData%\AliasCockpit\aliases.sqlite` 中的保存输入邮箱历史与 alias 标记；启动时不得调用 Provider HTTP adapter，不得联网。
- SQLite 开发库 `%LocalAppData%\AliasCockpit\aliases.sqlite` 尚未加密，只能保存 alias 元数据、颜色、使用位置、用途、保存的输入邮箱地址和未来 `secret_ref`，不得存放 token/secret。
- Provider token / API secret 必须通过 `WindowsCredentialManagerSecretStore` 或后续等价 secret store 存储，SQLite 只能保存 `secret_ref`。
- Provider account metadata 通过 `SqliteProviderAccountRepository` 保存；SimpleLogin/addy.io HTTP adapter 支持 API key 校验、random/custom alias 创建、disable 和 delete，App 不得默认联网调用它们。
- 当前自动门禁验证 build/test/benchmark/format/publish/prune/zip/MSI/setup EXE/process smoke/basic UI smoke；完整 UI 自动化仍需后续补充。
- 完整发布验证脚本：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1`；该脚本会执行 build/test/benchmark/format/publish、裁剪当前未使用的 WinAppSDK AI/WebView/Widgets/诊断文件、重建 portable zip、生成 MSI、生成 setup EXE 安装器、验证 MSI 数据库、抽取 setup EXE 确认内嵌 MSI、检查 zip 根目录 exe，对 publish/artifact 两处 exe 做 5 秒启动冒烟，并通过 UI 输入 Gmail 地址验证复制出的生成别名。
- 当前可发布 x64 exe：`.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal`，输出位于 `src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe`。
- 发布后必须至少做一次启动冒烟验证：从 `publish` 目录启动 `AliasCockpit.App.exe`，确认进程不会立即退出；当前 Windows App SDK 2.2.0 + .NET 8 self-contained 组合已通过该验证。
- 当前便携交付包位于 `artifacts\AliasCockpit-win-x64-portable.zip`；它必须包含完整 publish 目录内容，不能只包含单个 exe。更新该包后必须检查 zip 根目录包含 `AliasCockpit.App.exe` 并从解压/复制后的 artifact 目录做启动冒烟验证。
- 当前 MSI 交付包位于 `artifacts\AliasCockpit-win-x64.msi`；它必须由 `scripts\package-msi.ps1` 从完整 publish 目录生成，不能只打包单个 exe。
- 当前 EXE 安装包位于 `artifacts\AliasCockpit-win-x64-setup.exe`；它必须由 `scripts\package-setup-exe.ps1` 生成，是内嵌 MSI 的 WiX Burn 安装器，不得把 `publish\AliasCockpit.App.exe`、快捷方式或 zip 内 app exe 当成“exe 安装包”交付。
- MSI 和 setup EXE 的安装入口必须显示 `Assets\AppIcon.ico`；`package-msi.ps1` 负责 Start Menu shortcut / ARPPRODUCTICON，`package-setup-exe.ps1` 负责 Burn `IconSourceFile`。
- GitHub Release 附件必须上传 `artifacts\AliasCockpit-win-x64-setup.exe`、`artifacts\AliasCockpit-win-x64.msi` 和 `artifacts\AliasCockpit-win-x64-portable.zip`；这些构建产物不得提交进 Git 历史。
- GitHub 发布脚本为 `scripts\publish-github-release.ps1`；它必须通过 `GITHUB_TOKEN`、`GH_TOKEN` 或 Codex GitHub 集成 token helper 临时取 token，不得把 token 写入文件、Git remote 或日志。
- 发布目录瘦身使用 `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir <publish-dir>`；该脚本只允许删除已审计的未用 WinAppSDK AI/WebView/Widgets/Workloads/诊断文件，执行后必须重新通过 publish 启动冒烟和 UI smoke。
- 不得删除 `Microsoft.InteractiveExperiences.Projection.dll`；实测删除后 WinUI 发布程序启动失败。
- MSI 构建工具固定为 WiX CLI `5.0.2`，通过 `.\.tools\dotnet\dotnet.exe tool install wix --version 5.0.2 --tool-path .tools\wix` 恢复到 `.tools\wix`；它是构建工具，不得作为产品运行依赖加入 App/Core/Infrastructure。
- setup EXE 使用 WiX BAL 扩展 `WixToolset.Bal.wixext` `5.0.2`；该扩展只用于构建安装器，不得作为产品运行依赖。
- MSI 生成后必须能通过 `.\.tools\wix\wix.exe msi validate artifacts\AliasCockpit-win-x64.msi`；`scripts\package-msi.ps1` 会把 Windows App SDK 本地化资源导致的 `File.Language` 表项归一化为 `0`。
- 清理构建缓存使用 `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts`；只允许删除工作区内 `bin/obj` 和已知 artifact，不得删除 `.tools`、SQLite 用户数据或源码目录。

## 3. 测试命令

- 单元/压力/SQLite 集成测试：`.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal`。
- 新增核心生成算法、邮箱裂变规则、导入导出、同步、加密、Provider 适配逻辑时必须配套测试。

## 4. 构建命令

- 构建：`.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal`。
- Benchmark：`.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release`。
- x64 文件夹发布：`.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal`。
- 裁剪发布目录：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish`。
- 发布交付前优先运行 `scripts\verify-release.ps1`，不要只运行 publish 命令后直接交付 zip。
- GitHub 仓库目标为 `NextWeb4/alias-cockpit`；首次发布 tag 使用 `v1.0.0`，release body 以 `docs/release/v1.0.0.md` 为准。
- 单独生成 MSI：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-msi.ps1`。
- 单独生成 EXE 安装包：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-setup-exe.ps1`。
- 发布到 GitHub 并上传 Release 附件：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-github-release.ps1`。
- 清理构建缓存：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts`。
- 基础 UI smoke 脚本：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-ui-smoke.ps1 -ExePath artifacts\AliasCockpit-win-x64-portable\AliasCockpit.App.exe`；该脚本依赖桌面会话和剪贴板，不适合无桌面 CI。
- 当前 x64 文件夹发布已关闭 trimming，避免 WinUI/WinRT 裁剪风险；不要在未验证完整 GUI 启动前重新启用 `PublishTrimmed=true`。
- 不得把 `Microsoft.WindowsAppSDK` 回退到 1.8 系列，除非同时切换到兼容的 .NET 8/9 工具链并重新验证发布 exe；1.8 在当前 .NET 10 自包含发布下会启动崩溃。
- 不得把 App/Core/Infrastructure 目标框架改回 `net10.0` 或 `net10.0-windows`，除非先验证 Windows App SDK / WinRT Runtime 已不再触发 `ICustomQueryInterface`、`SetEnvironmentVariable` 或 `List<T>` 启动期 TypeLoad/MissingMethod 异常。
- 不得把 WiX 6/7 作为默认 MSI 构建工具，除非先重新完成 OSMF/EULA 许可证审计并更新 `docs/architecture/msi-packaging-audit-2026-07-05.md`。

## 5. 代码风格

- Format/lint 检查：`.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal`。
- 自动修复格式：`.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verbosity minimal`。
- 当前 `dotnet format` 可能输出非阻塞 workspace load warning；只要退出码为 0 且 build/test 通过，可视为格式门禁通过。
- `Directory.Build.props` 显式使用 `LangVersion=latest`，原因是 App ViewModel 使用 CommunityToolkit.Mvvm 的 partial property 源生成语法；删除前必须先改写 ViewModel。
- 不得在缺少格式化规则时混用多种代码风格。

## 6. 模块边界

- UI 层不得直接实现邮箱别名生成算法、Provider API、加密、同步冲突解决或数据库访问细节。
- 核心业务层必须可被单元测试直接调用，不依赖窗口、托盘、剪贴板或网络。
- Gmail/Outlook 邮箱裂变规则必须保留在 `src/AliasCockpit.Core/Tools/EmailAliasExpander.cs`；App 层只能读取输入、展示结果和调用剪贴板。
- `EmailAliasExpansionResult.Aliases` 必须表示当前已生成分类结果的去重合集；`DotAliases` 和 `PlusAliases` 各自受 `Count` 限制，但 `Aliases` 不得再按单类 `Count` 二次截断。
- 创作者信息必须硬编码为 `HaoXiang Hwang`、`https://nextweb4.github.io/`、`didadida1688@gmail.com`；UI、窗口标题和安装器元数据不得改为读取配置或环境变量。
- 创作者网站和邮箱在 UI 中必须是可点击链接；网站使用 HTTPS URL，邮箱使用 `mailto:`。
- 修改应用图标时必须同步检查 `AliasCockpit.App.csproj` 的 `ApplicationIcon`、`MainWindow.xaml.cs` 的 `AppWindow.SetIcon`、`scripts\package-msi.ps1` 的 shortcut icon 和 `scripts\package-setup-exe.ps1` 的 bundle icon。
- Alias 标记状态必须在 ViewModel 侧支持 `marked` / `unmarked` 筛选，并在列表行内显示 Marked/Unmarked 状态；不得只依赖颜色区分。
- 保存输入邮箱历史必须通过 `ISavedEmailAddressRepository`；alias 使用位置、用途、颜色必须通过 `IAliasRepository` / `AliasRecord` 保存，不得写 UI 私有 JSON 旁路数据。
- 主页面当前使用最小 XAML + C# 构建控件，原因是 WinUI XAML 编译器在复杂强类型绑定下无详情失败；重新引入复杂 XAML 前必须先跑 build 验证。
- Infrastructure 可引用 Core，但 Core 不得引用 Infrastructure；App 只能通过 Core 接口使用仓储能力。
- Provider 适配层必须隔离 SimpleLogin、addy.io、Fastmail、Cloudflare 等外部服务差异。
- Provider 适配层必须先声明 `ProviderProfile` 能力矩阵；UI 或应用服务不得假设所有 Provider 都支持相同的创建、禁用、删除、恢复、recipient、rules 或 webhook 语义。
- Secret 存储必须独立成模块，默认使用 Windows Credential Manager 或等价系统凭据能力，不得落入普通日志或明文配置。
- Windows Credential Manager 适配器位于 `src/AliasCockpit.Infrastructure/Security/`；Core 只能依赖 `ISecretStore` 接口。
- Provider account 只能保存 `secret_ref`，格式由 `SecretKey.ForProviderToken(providerAccountId)` 生成；任何真实 token/API secret 不得进入 `ProviderAccount`、SQLite、测试快照或文档示例。
- SimpleLogin custom alias 必须使用 `/api/v5/alias/options` 返回的 `signed_suffix` 和 `/api/v2/mailboxes` 返回的 mailbox id；不得在本地伪造 signed suffix 或硬编码 mailbox id。
- addy.io alias 创建不传 `recipient_ids` 时依赖服务端默认 recipient；不得在测试、文档或默认配置中硬编码真实 recipient id。
- Provider delete 是危险操作；UI 或应用服务接入前必须有 dry-run、二次确认和审计事件，不得直接从列表按钮绕过确认调用 HTTP adapter。
- 批量 Provider disable/delete 必须先调用 Core 的 `ProviderBatchOperationPlanner` 生成 dry-run；`ProviderBatchOperationPlan.CanExecute` 为 false 时不得执行。
- 批量 Provider delete 必须通过 `ProviderBatchOperationExecutor` 的 `explicitlyConfirmed=true` 路径执行；不得直接循环调用 adapter 的 `DeleteAliasAsync`。
- 本地数据层必须为后续 SQLite、加密、导入导出和同步预留清晰边界。

## 7. 禁止事项

- 不得在未完成调研、方案审计和冲突检查前引入新依赖或确定架构。
- 不得编造当前不存在的启动、测试、构建、lint 或 format 命令。
- 不得把 API Token、OAuth Token、恢复密钥、邮箱地址样本写入日志、测试快照或文档示例。
- 不得把 token/secret 写入 `%LocalAppData%\AliasCockpit\aliases.sqlite`；SQLite 只允许保存 alias 元数据、保存输入邮箱历史和未来 `secret_ref`。
- 不得把密码、API token、恢复码或真实账号敏感备注写进 alias 的 `site`、`purpose`、`tags` 或保存邮箱字段。
- Credential Manager 测试必须使用唯一 key 并在 finally 中删除测试凭据。
- 不得让离线功能依赖外部网络请求。
- 不得把 `SimpleLoginMockProviderAdapter` 或 `AddyIoMockProviderAdapter` 当成真实同步；addy.io recipient/rules/webhook、SimpleLogin/addy.io 同步执行流落地前必须重新核验官方 API、鉴权、速率限制、错误响应和许可证。
- 不得为了接入某个开源库而重构无关模块。
- 不得复制竞品 UI 或开源项目实现。
- 参考 `mail.nnioj.com` 工具页时只允许借鉴公开可观察的运行行为，不得复制其源码、样式或打包资源。

## 8. 完成标准

- 需求实现前必须有调研记录、方案对比、采用/拒绝理由和冲突检查。
- 每个核心功能必须至少覆盖单元测试；涉及 UI 的功能必须补充 UI 自动化测试；涉及大量数据的列表/搜索/同步必须补充压力测试或 Benchmark。
- 完成时必须说明是否有新依赖、许可证风险、联网行为变化、架构冲突和未解决风险。

## 9. Review 标准

- 优先检查安全边界：凭据存储、数据库加密、日志脱敏、网络请求、导入文件处理和同步冲突。
- 优先检查模块边界：UI 是否越权访问数据层或 Provider，核心逻辑是否可测。
- 优先检查性能：几千条 Alias 的搜索、过滤、排序、统计和批量操作不能依赖低效全量 UI 重绘。
- 优先检查回归测试：生成算法、导入导出、同步、删除/恢复、过期/禁用必须有可验证用例。

## 10. 常见风险

- Alias 生成规则过于可预测，可能降低隐私保护效果。
- Catch-all 和转发 Provider 的语义不同，强行抽象可能导致禁用、删除、恢复行为不一致。
- 同步功能可能引入明文泄露、冲突覆盖、版本回退和离线写入丢失。
- 导入导出可能泄露邮箱、标签、备注、Provider ID、Token 或历史事件。
- 保存输入邮箱历史和 alias 使用位置会暴露用户账号使用关系；导出、同步或截图前必须提醒这是敏感元数据。
- UI 高信息密度容易牺牲可读性和键盘可达性，需要用真实数据规模验证。
- 新增外部服务 SDK 可能改变联网边界或许可证义务，必须先审计。
- mock Provider adapter 只能证明项目边界和计划模型，不能证明真实 Provider API 可用；真实接入前必须增加 mock server/HTTP 层测试。SimpleLogin/addy.io HTTP adapter 已有 fake `HttpMessageHandler` 测试，但仍未做真实账号端到端验证。
- Gmail 点号别名只适用于 Gmail/Googlemail 地址；Google Workspace 自定义域不得默认套用点号规则。
- `+tag` 别名可能被第三方网站拒绝，UI/文档不得承诺所有注册表单可用。
