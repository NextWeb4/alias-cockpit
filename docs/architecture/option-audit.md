# Open Source / Architecture Option Audit

日期：2026-07-05  
阶段：方案审计，尚未安装依赖。

## 当前本机工具链事实

| 工具 | 状态 |
| --- | --- |
| .NET runtime | 已安装 8.0/9.0 runtime 和 WindowsDesktop runtime |
| .NET SDK | 已通过项目内 `.\.tools\dotnet\dotnet.exe` 可用，可 build/test/publish |
| Node.js / npm | Node v24.17.0, npm 11.13.0 |
| Rust | rustc/cargo 1.96.0 可用 |
| Python | 3.12.7 可用 |
| Git | 2.53.0.windows.2 可用 |
| GitHub CLI | 未安装 |
| SQLite CLI | 3.44.3 可用 |

## Windows 本地应用技术选型

| 方案名称 | 来源 | 许可证 | 核心能力 | 优点 | 缺点 | 维护状态 | 与当前项目契合度 | 可能冲突点 | 是否采用 | 采用方式 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| WinUI 3 + Windows App SDK + C# | Microsoft | MIT/微软组件许可 | Windows 原生 UI、Fluent、MSIX/桌面集成 | 最符合 Windows 本地与 Fluent；可用 UI Automation/FlaUI；系统凭据/DPAPI 集成自然 | 当前机器缺 .NET SDK；WinUI 打包/模板复杂度较高 | 官方活跃 | 高 | 需要安装 SDK；项目只面向 Windows | 暂定首选 | 第一版骨架前安装/确认 SDK，再落地 |
| WPF + C# | Microsoft | MIT/微软组件许可 | 成熟 Windows 桌面 UI | 稳定、易测、WindowsDesktop runtime 已有 | Fluent 体验需较多自定义；现代 UI 控件弱于 WinUI | 稳定维护 | 中高 | UI 现代感不足 | 备选 | 若 WinUI 工具链阻塞，作为 fallback |
| Avalonia + .NET | Avalonia | MIT | 跨平台 XAML UI | 控件成熟，MVVM 友好，跨平台 | 不是 Windows 原生；仍需 .NET SDK | 活跃 | 中 | 用户目标是 Windows 本地，不要求跨平台 | 暂不采用 | 仅借鉴 MVVM/虚拟化经验 |
| Rust + eframe/egui | crates.io/GitHub | MIT/Apache-2.0 | 原生可执行文件、即时模式 UI | 当前机器 Rust 可用；无 WebView；性能好；二进制部署简单 | 不是真正 Fluent 原生控件；复杂可访问性/自动化 UI 测试较弱 | 活跃 | 中 | UI 风格与 Windows Fluent 有偏差 | 备选 | 若 .NET SDK 无法安装，做 native fallback |
| Rust + Iced | crates.io/GitHub | MIT | Rust 原生 GUI | 架构更声明式；非 Web | 控件生态/数据表成熟度需验证 | 活跃 | 中 | 大表格/复杂桌面 UX 风险 | 暂不采用 | 只借鉴 Elm-like 架构 |
| Slint | Slint | GPLv3/commercial/royalty-free 条款需复核 | Rust/C++ native UI | 性能好、设计工具友好 | 许可证/商业使用边界需审计 | 活跃 | 中 | 许可证风险 | 暂不采用 | 不进入第一版 |
| Tauri 2 | Tauri | MIT/Apache-2.0 | WebView 桌面壳 | 轻量、Rust 后端、开发快 | UI 本质是 WebView；与“不是网页”有冲突 | 活跃 | 低 | 明确需求冲突 | 不采用 | 可借鉴命令隔离/权限模型 |
| Electron | Electron | MIT | Chromium 桌面壳 | 生态最大 | 体积大、Web App 感强、安全面大 | 活跃 | 低 | 明确需求冲突 | 不采用 | 不使用 |
| Flutter Windows | Google | BSD-3-Clause | 跨平台 native shell | UI 流畅，工具链好 | 不是 Windows 原生；Dart 生态与 Provider/API 适配需额外成本 | 活跃 | 中低 | 技术栈引入收益不足 | 暂不采用 | 不进入第一版 |

### 暂定结论

- 首选 WinUI 3 + C#/.NET：最贴近“Windows 本地应用 + Fluent Design + 高密度桌面工具”的目标。
- 当前状态：项目已使用本地 `.tools` 内的 .NET SDK 和 Windows App SDK 成功 build/test/publish。
- 明确不采用 Electron/Tauri 作为主 UI：会与用户“不是网页”的明确要求冲突。

## 数据与安全库选型

| 方案名称 | 来源 | 许可证 | 核心能力 | 优点 | 缺点 | 维护状态 | 契合度 | 冲突点 | 是否采用 | 采用方式 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SQLite | https://sqlite.org | Public Domain | 本地关系型数据库 | 稳定、单文件、可迁移、FTS5 支持搜索 | 默认不加密 | 极成熟 | 高 | 需补加密方案 | 采用 | 本地主数据存储 |
| SQLCipher | https://www.zetetic.net/sqlcipher/ | BSD-style/商业支持 | SQLite 文件加密 | 成熟 AES-256 加密 SQLite | 原生库打包复杂度较高 | 成熟 | 高 | 需确认 NuGet/打包方式 | 候选采用 | 用于加密数据库或二阶段落地 |
| DPAPI / Windows Credential Manager | Microsoft | Windows 平台能力 | 本机用户级密钥/凭据保护 | 与 Windows 安全模型契合；不用自造密钥库 | 不适合跨设备直接同步密钥 | 官方 | 高 | 多设备同步需独立密钥导出流程 | 采用 | Token 与本地 DB key wrapping |
| Microsoft.Data.Sqlite | Microsoft/NuGet | MIT | .NET SQLite provider | 与 .NET 生态匹配 | SQLCipher 支持需额外方案 | 活跃 | 高 | 加密需额外审计 | 候选采用 | 若 WinUI/.NET 落地 |
| LiteDB | GitHub/NuGet | MIT | .NET embedded document DB | 简单，支持加密 | 查询/迁移/FTS/生态弱于 SQLite | 活跃度需复核 | 中 | 长期可迁移性较弱 | 暂不采用 | 不作为主库 |
| RocksDB/LevelDB | 多源 | Apache/BSD | KV 存储 | 性能强 | 过重，不适合关系/筛选/导入导出 | 成熟 | 低 | 复杂度超过收益 | 不采用 | 无 |
| CsvHelper / Sylvan.Data.Csv | NuGet | 需复核 | CSV 导入导出 | 成熟解析，避免手写 CSV | 需许可证/安全审计 | 活跃 | 中 | 导入文件攻击面 | 候选 | 开发前二次审计 |

## Provider / API 适配策略

| Provider | 直接复用 | 借鉴设计 | 不采用 | 适配要点 |
| --- | --- | --- | --- | --- |
| SimpleLogin | 官方 HTTP API 语义、用户授权模型 | Alias/mailbox/domain/contact 概念 | AGPL 代码 | API token 存 Credential Manager；Provider 能力矩阵 |
| addy.io | 官方 API 语义、rules/recipients/webhooks | GPG、recipient、规则系统 | AGPL 代码 | 高级规则可视化；导入导出字段映射 |
| Firefox Relay | masks/custom domain 公开行为 | tracker/removal 提醒、低心智模型 | 服务器代码 | 若无稳定公开 API，则只做导入/手工记录或后续适配 |
| DuckDuckGo Email Protection | 用户帮助文档语义 | tracker removal、private duck address 模型 | 无公开批量 API 依赖 | 初期作为手工 Provider/导入源 |
| Cloudflare Email Routing | Cloudflare API/Email Workers | DNS/routing/catch-all 检查 | 不内置 Workers 复杂模板 | 需要 DNS 权限提示、最小权限 token |
| Fastmail Masked Email | JMAP/API 能力 | 标准化 masked email 模型 | 锁定式设计 | 适配 JMAP；注意 OAuth/应用密码 |
| Apple Hide My Email | 用户概念 | create/deactivate 简化模型 | 私有生态/API 抓取 | 仅支持导入/手工记录，不做非官方自动化 |
| Proton Pass Alias | Proton/SimpleLogin 公开能力 | 密码管理器联动 | 私有客户端实现 | 优先通过 SimpleLogin API 适配 |

## 方案冲突检查

| 检查项 | 结论 |
| --- | --- |
| 与现有技术栈是否冲突 | 当前没有项目技术栈；WinUI 3 需要新增 .NET SDK/Windows App SDK |
| 与目录结构是否冲突 | 当前为空；后续需要 `src/`, `tests/`, `docs/`, `benchmarks/` |
| 与运行方式是否冲突 | 当前无运行命令；必须建立唯一启动命令 |
| 与构建方式是否冲突 | 当前无构建命令；WinUI 需明确 MSIX/unpackaged 策略 |
| 与数据库设计是否冲突 | 当前无数据库；SQLite 适合 |
| 与配置系统是否冲突 | 当前无配置系统；需设计加密配置/普通配置分离 |
| 与权限模型是否冲突 | 需要最小权限 API token；不得要求邮件账户主密码 |
| 与离线/联网模式是否冲突 | 本地搜索/编辑/导入导出离线；Provider 同步显式联网 |
| 与许可证是否冲突 | AGPL/GPL 项目只研究不复制；MIT/BSD/Apache 候选库优先 |
| 与用户明确需求是否冲突 | Electron/Tauri 主 UI 与“不是网页”冲突，已拒绝 |

## 开发前必须完成

- 已确认项目内 `.tools` 的 .NET SDK + Windows App SDK 可 build/test/publish；后续只需保持该工具链命令同步到 `AGENTS.md`。
- 对候选 NuGet 依赖做许可证复核：CommunityToolkit.Mvvm、Microsoft.Data.Sqlite、FlaUI、BenchmarkDotNet、CSV/QR/图表库。
- 写出第一版 Provider 能力矩阵，避免把不同服务强行抽象成同一种 alias。
- 建立测试命令、lint/format 命令和构建命令后更新 `AGENTS.md`。

## 追加审计

- Gmail/Outlook 本地邮箱别名裂变工具选型记录：`docs/architecture/email-alias-expander-audit-2026-07-05.md`。
