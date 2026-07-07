# Email Alias Expander Option Audit

日期：2026-07-05

## 当前需求

- 参考 `https://mail.nnioj.com/zh/tools/?tool=gmail` 的运行逻辑，做成本项目内的 Windows 原生 exe。
- 工具必须本地运行，不在生成时调用外部网络。
- 不复制目标站点代码或 UI，只借鉴输入/输出行为。

## 当前项目审计

- 技术栈：WinUI 3 + .NET，本地 Windows 应用。
- 核心边界：生成规则必须放在 `AliasCockpit.Core`，App 层只做窗口、剪贴板和控件交互。
- 现有依赖：已有 `CommunityToolkit.Mvvm`、`Microsoft.Data.Sqlite`、Windows App SDK；本次不需要新增依赖。
- 构建/测试：使用 `.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal` 和 `.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal`。
- 联网边界：App 启动和本地邮箱裂变工具不得默认调用 SimpleLogin/addy.io HTTP adapter。

## 运行逻辑审计

- 目标页面行为：输入邮箱地址、标签、生成数量，选择 Gmail 点号别名和 `+tag` 别名，输出全部/点号/标签分类结果。
- Gmail/Googlemail：去掉原始 local part 中已有点号和 `+` 后缀，统一输出为 `gmail.com`，支持点号组合和 `+tag`。
- Outlook/Hotmail/Live/MSN：去掉原始 `+` 后缀，支持 `+tag`，不支持 Gmail 点号组合。
- 数量边界：限制到 1 到 256。
- 标签规则：按逗号/换行分隔，转小写，只保留 `a-z`、数字、`.`、`_`、`-`。
- 去重规则：基础地址、点号别名、`+tag` 别名合并后去重并截断到数量上限。

## 候选方案对比

| 方案名称 | 来源 | 许可证 | 核心能力 | 优点 | 缺点 | 维护状态 | 与当前项目契合度 | 可能冲突点 | 是否采用 | 采用方式 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 参考目标站点运行行为 | `mail.nnioj.com` 公开页面 | 未声明，不复制代码 | Gmail/Outlook 别名裂变 UI 与规则 | 与用户指定目标一致；可快速落地；无需新增依赖 | 不能复制源码或视觉实现；需要自行测试规则 | 站点当前可访问 | 高 | 若复制实现会有许可证/版权风险 | 采用 | 只借鉴行为，自研 Core 规则 |
| 自研小型 Core 生成器 | 当前项目 | 项目自有 | 地址解析、点号组合、标签组合、去重、数量限制 | 代码少、可测、无联网、无新依赖 | 需要维护边界条件测试 | 项目内维护 | 高 | Gmail Workspace 等复杂语义不覆盖 | 采用 | `EmailAliasExpander` |
| GitHub `gmail-alias-generator` | GitHub | MIT | Gmail plus/dot web tool | MIT，概念相近 | 0 stars/无 release；Web 工具；不覆盖 Outlook 规则 | 维护信号弱 | 低 | 引入/复制收益低 | 不采用 | 仅确认同类方案存在 |
| GitHub `email-trick` | GitHub | MIT | Gmail dots/plus 变体 | MIT，概念相近 | Web/静态工具；功能简单；无 .NET 集成价值 | 维护信号一般 | 低 | 复制代码无必要 | 不采用 | 仅借鉴问题域 |
| Google Gmail API .NET SDK | NuGet `Google.Apis.Gmail.v1` | Google/NuGet 包许可 | Gmail API 读写邮件、标签管理 | 官方 API SDK | 解决的是 Gmail API，不是地址变体生成；会引入 OAuth/联网边界 | 活跃 | 低 | 显著增加依赖、权限和联网风险 | 不采用 | 无 |

## 采用结论

- 直接复用：.NET 标准库、现有 WinUI/CommunityToolkit 工具链。
- 借鉴设计：目标站点的输入项、输出分类、数量上限和 Gmail/Outlook 支持范围。
- 不采用：目标站点源码、第三方 Web 工具源码、Gmail API SDK。
- 新增模块：`src/AliasCockpit.Core/Tools/EmailAliasExpander.cs` 和主窗口工具 UI。
- 保留模块：现有 Provider adapter、SQLite alias/saved email/provider 仓储、Credential Manager secret store。
- 替换范围：主页面从 alias 管理列表切换为本地邮箱别名裂变工具；未替换 Core alias 生成器。

## 冲突检查

| 检查项 | 结论 |
| --- | --- |
| 与现有技术栈 | 不冲突，继续使用 WinUI/.NET |
| 与目录结构 | 不冲突，Core 规则在 `src/AliasCockpit.Core/Tools/`，UI 在 App |
| 与运行方式 | 不冲突，仍通过 WinUI exe 运行 |
| 与构建方式 | 不冲突；为避开 WinUI XAML 编译器复杂绑定问题，主页面采用最小 XAML + C# 构建控件 |
| 与数据库设计 | 不冲突；本地裂变工具只写保存输入邮箱历史和 alias site/purpose/color 标记，不写 token/secret |
| 与配置系统 | 不冲突，不新增配置 |
| 与权限模型 | 不冲突，不申请 Gmail/OAuth 权限 |
| 与离线/联网模式 | 不冲突，本功能不联网 |
| 与许可证 | 不复制第三方实现，未引入新依赖 |
| 与用户需求 | 符合“做一个 exe”的要求 |

## 风险

- Gmail 点号规则只适用于普通 Gmail/Googlemail 地址；Google Workspace 自定义域不应套用点号规则。
- `+tag` 可能被部分网站拒绝，工具只生成候选地址，不保证第三方表单接受。
- 当前 UI 自动化尚未补充，已用 Core 单元测试覆盖生成规则。

## 参考来源

- 目标工具页：`https://mail.nnioj.com/zh/tools/?tool=gmail`
- Google Gmail Blog: `https://gmail.googleblog.com/2008/03/2-hidden-ways-to-get-more-from-your.html`
- GitHub 同类工具：`https://github.com/niewiemczego/gmail-alias-generator`
- NuGet Gmail API SDK：`https://www.nuget.org/packages/Google.Apis.Gmail.v1`
