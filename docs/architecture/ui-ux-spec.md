# UI / UX Specification

日期：2026-07-05  
状态：设计草案，后续需用原型和 UI 自动化验证。

## 设计目标

- Windows 本地工具感：像 1Password/Bitwarden 的管理密度，Raycast/Cursor 的命令效率，Obsidian/Notion 的组织能力。
- 几千条 alias 默认不卡顿：列表虚拟化、搜索索引、批量操作预演是基础能力。
- 不做营销页、不做大卡片堆叠、不做低信息密度 hero。
- 所有危险操作都可解释、可预演、可撤销或进入回收站。

## 主窗口布局

| 区域 | 内容 | 交互 |
| --- | --- | --- |
| Title bar / command strip | 搜索框、同步状态、快速生成、命令面板入口 | `Ctrl+K` 命令，`Ctrl+N` 新建 alias，`Ctrl+Shift+C` 复制 alias |
| Left rail | 工作区、智能视图、标签树、Provider、域名、回收站 | 键盘上下移动；标签可拖拽归组 |
| Main table | Alias 虚拟化表格 | 多选、排序、列隐藏、保存视图 |
| Right inspector | 详情、历史、规则、Provider 状态、风险解释 | 可折叠；编辑有 dirty state |
| Bottom status | 已选数量、过滤条件、同步队列、错误摘要 | 点击打开队列/问题面板 |

默认不使用嵌套卡片。只有重复列表项、Modal、导入预览、批量预演可以使用 card。

## 主表格列

| 列 | 默认 | 说明 |
| --- | --- | --- |
| Status | 是 | Active / Disabled / Expired / Deleted / Conflict / Remote Missing |
| Alias | 是 | 支持部分遮罩、复制按钮、二维码按钮 |
| Purpose / Site | 是 | 网站、用途、项目 |
| Provider | 是 | SimpleLogin、addy.io、Fastmail 等 |
| Domain | 是 | shared/custom/catch-all 标记 |
| Tags | 是 | 颜色标签；最多显示 3 个，其余折叠 |
| Last activity | 是 | 收件/同步/编辑时间 |
| Risk | 是 | 复用、catch-all、站点阻止、Provider advisory |
| Recipient | 默认隐藏 | 真实邮箱默认部分遮罩 |
| Created / Expires | 可选 | 用于审计和自动过期 |

## 搜索语法

| 示例 | 含义 |
| --- | --- |
| `github` | 全文搜索 alias/site/notes/tags |
| `tag:work provider:simplelogin status:active` | 组合过滤 |
| `risk:high` | 高风险 alias |
| `domain:example.com catchall:true` | 指定域与 catch-all |
| `used:<30d` | 最近 30 天有活动 |
| `expires:<7d` | 7 天内过期 |
| `recipient:me` | 按脱敏收件人标签搜索 |

## 命令面板

命令必须可被键盘访问，且每条命令声明是否离线、是否联网、是否危险：

- Generate alias
- Generate alias for current clipboard URL
- Import aliases
- Export encrypted backup
- Sync selected provider
- Bulk disable selected aliases
- Rotate selected aliases
- Explain provider capabilities
- Open risk dashboard
- Undo last operation
- Restore from trash

## 生成 Alias 面板

面板左侧为策略，右侧为候选列表和风险解释：

- Random strong：默认推荐。
- Readable：用于人工沟通，但显示更低隐私等级。
- Site-aware：从 URL 提取站点名，显示“会泄露站点用途”的提示。
- Rule template：保存为团队/工作区规则。
- AI-assisted：默认关闭，需要用户显式启用，且先显示数据出境说明。

每个候选显示：

- Alias
- Entropy/risk
- Provider compatibility
- Domain availability
- Copy / create / pin

## 批量操作预演

批量操作不能直接执行。必须进入预演页：

| 区域 | 内容 |
| --- | --- |
| Summary | 将影响多少 alias、多少 Provider、多少无法执行 |
| Changes | 每个 alias 的 before/after |
| Provider conflicts | 哪些 Provider 不支持该操作 |
| Safety | 是否可 undo、是否进回收站、是否需要远端删除 |
| Execute | 明确按钮文案，例如 “Disable 128 aliases” |

## Dashboard

默认模块：

- Alias 总量、活跃/禁用/过期/冲突。
- Provider 同步状态。
- 高风险项：复用、catch-all、泄露、Provider advisory。
- 待轮换：长期未更新、站点泄露、收件异常增长。
- 近期活动：创建、导入、同步、删除、恢复。
- 数据健康：最近备份、同步包加密状态、数据库版本。

## Accessibility / Keyboard

- 所有主流程必须支持键盘完成。
- 图标按钮必须有 tooltip 和 accessible name。
- 颜色标签不能作为唯一信息来源，必须有文字/形状辅助。
- 表格焦点、选中、批量状态必须清楚。
- 高风险操作必须能被屏幕阅读器读出影响范围。

## 状态设计

| 状态 | UI 表达 |
| --- | --- |
| Offline | 顶部网络/同步状态显示离线；本地功能可用 |
| Provider auth expired | Provider 行显示需重新授权；不弹全局阻塞 |
| Security advisory | Provider badge 警告；详情说明来源和日期 |
| Sync conflict | 表格状态列 + 冲突队列 |
| Import dry-run errors | 不写库，逐行错误可导出 |
| Clipboard copied | 短 toast；支持自动清理倒计时 |

## 性能预算

| 操作 | 目标 |
| --- | --- |
| 启动打开 10k alias | P95 < 1.5s 首屏可交互 |
| 搜索 10k alias | P95 < 100ms UI 反馈 |
| 搜索 50k alias | P95 < 250ms UI 反馈 |
| 选择 5k alias | 不触发主表全量重绘 |
| 批量预演 5k alias | P95 < 1s 生成预览摘要 |
| 导入 50k CSV | streaming dry-run，内存不过度增长 |

这些预算是设计目标，必须用 Benchmark 校准，不可在未测量时宣称达标。

