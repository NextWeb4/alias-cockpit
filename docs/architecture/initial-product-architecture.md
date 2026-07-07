# Initial Product Architecture

日期：2026-07-05  
状态：草案，基于第一轮调研。尚未编码。

## 产品定位

名称暂定：Alias Cockpit。

它不是邮箱服务、不是临时邮箱、不是密码管理器、不是网页控制台。它是 Windows 本地应用，用来统一生成、管理、同步、导入导出不同 Provider 下的 Email Alias。

## 核心原则

- Local-first：本地数据库是事实源；Provider 同步是适配层行为。
- Offline-capable：没有网络时仍能搜索、规划批量变更、导入导出、查看历史。
- Provider-aware：不同 Provider 能力不同，UI 必须展示差异，而不是假装统一。
- Secure by default：Token 不进数据库明文字段；日志默认脱敏；导出默认可加密。
- High-density UX：面向几千条 alias，默认列表/搜索/筛选/批量操作高效。
- Reversible operations：禁用、删除、批量修改、同步覆盖必须有预演、撤销、历史。

## 模块边界

| 模块 | 职责 | 禁止 |
| --- | --- | --- |
| AppShell/UI | 窗口、导航、命令面板、列表、详情、设置 | 直接调用 Provider API；直接读写数据库表；拼接 SQL |
| Core.Domain | Alias、Domain、Provider、Rule、Tag、Workspace、AuditEvent 的领域模型 | 引用 UI 框架、HTTP、SQLite、Credential Manager |
| AliasGeneration | 随机/可读/规则/站点感知/AI 可选生成策略 | 使用非加密随机；把真实邮箱写入生成日志 |
| Provider.Abstractions | Provider 能力矩阵、同步命令、错误模型 | 假设所有 Provider 支持相同行为 |
| Provider.* | SimpleLogin、addy.io、Fastmail、Cloudflare 等适配器 | 泄露 token；绕过统一网络/日志策略 |
| Persistence | SQLite schema、迁移、FTS、事务、事件日志 | 保存 API token 明文；保存未经脱敏的请求/响应 |
| Secrets | Windows Credential Manager/DPAPI、密钥包装 | 自造加密算法；把密钥写入普通配置 |
| Sync | Git/文件夹/加密包同步、冲突解决 | 未加密上传隐私数据；静默覆盖 |
| ImportExport | CSV/JSON/Provider dump 解析、dry-run、字段映射 | 导入时直接落库；跳过冲突预览 |
| Telemetry/Logs | 本地诊断、错误报告导出 | 默认联网遥测；记录完整邮箱/token |
| Tests/Benchmarks | 单测、UI 自动化、压力/基准测试 | 只靠手工验证 |

## 数据模型草案

| 实体 | 关键字段 | 说明 |
| --- | --- | --- |
| Workspace | id, name, default_profile_id | 工作区隔离个人/公司/项目 |
| Alias | id, address, provider_id, domain_id, status, purpose, site, created_at, updated_at, expires_at, risk_score | 本地 alias 主体 |
| ProviderAccount | id, provider_type, display_name, capability_hash, secret_ref | `secret_ref` 指向 Credential Manager，不保存 token |
| Domain | id, domain, provider_id, mode, catch_all_enabled, dns_status | 区分 custom domain、shared domain、catch-all |
| Recipient/Mailbox | id, email_hash, display_label, provider_ref | 默认脱敏显示真实邮箱 |
| Tag | id, name, color, parent_id | 支持颜色标签和树状组织 |
| AliasTag | alias_id, tag_id | 多标签 |
| Rule | id, alias_id/domain_id, condition_json, action_json, provider_capability | 规则需要能映射到 addy/Cloudflare 等 Provider |
| UsageEvent | id, alias_id, event_type, count, first_seen, last_seen, source | 用于统计和泄露线索 |
| AuditEvent | id, actor_device_id, operation, before_hash, after_hash, timestamp | Undo/Redo、版本历史、同步冲突 |
| Tombstone | entity_id, entity_type, deleted_at, purge_after | 回收站/同步删除语义 |

## Alias 生成策略

| 策略 | 输入 | 输出 | 安全要求 |
| --- | --- | --- | --- |
| Random Strong | 域名、长度、字符集、熵目标 | `k9v4p-...@domain` | 使用 CSPRNG；显示熵估计；避免易混字符 |
| Readable Random | 词表、长度、分隔符 | `river-lamp-42@domain` | 词表本地；随机选择；防止过短导致可枚举 |
| Site-aware | URL/domain/category/project | `github.4f7a@domain` 或 Provider 支持的格式 | 不默认暴露过多真实意图；给出隐私等级 |
| Rule-based | 模板、日期、项目、标签 | `{{site}}-{{year}}-{{rand}}` | 模板预览；冲突检测；不可低于最低熵 |
| AI-assisted | 用户描述、用途、语气 | 候选 alias 名称 | 默认离线关闭；不得把真实邮箱/token 发给模型 |
| Compatibility fallback | 网站阻止某格式时 | 替代域/替代格式 | 本地记录站点兼容性，避免重复踩坑 |

## Provider 能力矩阵草案

| 能力 | SimpleLogin | addy.io | Firefox Relay | DuckDuckGo | Cloudflare | Fastmail | Apple | Proton/SimpleLogin |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| API 创建 alias | 是 | 是 | 待确认/有限 | 无公开批量 API | 路由/API | 是/JMAP | 无公开通用 API | 是，偏 SimpleLogin |
| Custom domain | 是 | 是 | 用户强需求/能力受限 | 否 | 是 | 是 | 否 | 是 |
| Catch-all | 是/自托管需配置 | 是 | 有限制 | 否 | 是 | 视域名配置 | 否 | 是 |
| 从 alias 回复 | 是 | 是 | 有 reply 链路 | 有限制 | 非主目标 | 邮箱内支持 | 生态内 | 是 |
| 批量编辑 | 部分/用户需求明显 | API 可做 | 弱 | 弱 | 可脚本化 | 需 API | 弱 | 部分 |
| Webhook | 有/需核验 | 有 | 弱 | 否 | Workers | 生态能力 | 否 | 视 SimpleLogin |
| 导入导出 | 有需求 | 有需求 | 弱 | 弱 | 配置可导出 | 需实现 | 弱 | 部分 |

## UX 结构

- 左侧：Workspace、智能视图、标签树、Provider、域名、回收站。
- 顶部：全局搜索/命令面板，支持 `site:`, `tag:`, `provider:`, `status:`, `risk:`, `domain:`。
- 中央：虚拟化 Alias 表格，默认列为状态、alias、用途/站点、Provider、标签、最近使用、风险、操作。
- 右侧：详情/历史/规则/同步状态；可折叠，避免低密度卡片堆叠。
- Dashboard：泄露风险、近期活动、过期/待轮换、Provider 同步错误、catch-all 风险。
- 批量操作：选择后进入“预演模式”，显示影响数量、Provider 差异、不可执行项、回滚方式。
- 快捷体验：全局热键生成、托盘菜单、一键复制、二维码、最近 alias、剪贴板自动清理。

## 同步模型

第一版不直接做中心云服务。优先支持：

- 本地加密备份：用户选择路径，导出加密包。
- Git folder sync：用户自己选择 Git 仓库/同步文件夹，应用只读写加密同步包。
- 多设备：每个设备有 device_id；事件日志合并；冲突进入人工解决队列。
- Provider sync：每个 Provider adapter 拉取远端状态，与本地事件合并，不静默覆盖。

冲突类型：

- 同一 alias 在两个设备被修改不同标签/备注：自动合并。
- 同一 alias 状态一边禁用、一边删除：保守进入冲突队列。
- Provider 远端已删除、本地仍活跃：标记 remote_missing，不立即删除。
- Token 失效：只暂停 Provider，不影响本地数据。

## 导入导出

必须先 dry-run：

- 识别来源格式：SimpleLogin CSV/JSON、addy.io export、Fastmail/JMAP、Cloudflare routes、通用 CSV、手工表格。
- 字段映射：address、domain、recipient、status、created_at、notes、tags、provider_id、forward_to。
- 冲突策略：跳过、合并、覆盖、复制为新 alias。
- 隐私策略：导出默认脱敏真实邮箱；完整导出必须二次确认并支持加密。

## 测试策略

- 单元测试：生成算法、熵估算、模板解析、Provider 能力矩阵、导入映射、同步冲突。
- 集成测试：SQLite migration、加密 key wrapping、Provider mock server、CSV/JSON round-trip。
- UI 自动化：搜索/过滤、批量操作预演、导入 dry-run、详情编辑、错误状态。
- 压力测试：10k/50k alias 搜索、筛选、排序、批量标记、启动加载、数据库迁移。
- Benchmark：生成 10k alias、FTS 查询、同步 merge、导入解析。
- 安全测试：日志脱敏快照、secret 不落库、恶意 CSV/JSON、路径穿越、超大导入文件。

## 第一版开发切片建议

1. 工具链落地：WinUI 3/.NET SDK 或正式 fallback，生成项目骨架，更新 `AGENTS.md` 命令。
2. Core.Domain + SQLite schema + 单元测试。
3. AliasGeneration 五种本地策略，不接 AI/网络。
4. UI：高密度列表、搜索、详情、标签、批量预演。
5. Import/Export：通用 CSV/JSON + dry-run。
6. Secrets + ProviderAccount：Credential Manager/DPAPI 接入。
7. Provider v1：SimpleLogin + addy.io 二选二，使用 mock server 测试。
8. Sync v1：加密本地备份 + Git folder encrypted bundle。

