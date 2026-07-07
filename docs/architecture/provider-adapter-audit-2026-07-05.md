# Provider Adapter Audit

日期：2026-07-05
状态：已落地 Core 抽象、SQLite ProviderAccount 仓储、SimpleLogin/addy.io mock adapter，以及 SimpleLogin/addy.io HTTP adapter 基础能力。

## 当前项目审计

- 技术栈：WinUI 3 + .NET，Core 必须保持 UI/HTTP/SQLite 无关；Infrastructure 可承载 SQLite、Windows Credential Manager、未来 Provider HTTP adapter。
- 已有安全边界：Provider secret 只能通过 `ISecretStore` / `WindowsCredentialManagerSecretStore` 保存，SQLite 只保存 `secret_ref`。
- 已有 Provider 能力模型：`ProviderProfile`、`ProviderCapabilityDescriptor`、`CapabilitySupport` 已存在，但缺少 ProviderAccount、adapter 接口和账户持久化。
- 当前目标：在已建立 mock 边界后，最小接入 SimpleLogin 与 addy.io 官方 REST API 的安全请求基础：API key 校验、random/custom alias 创建、disable/delete 管理操作。

## 候选方案对比

| 方案名称 | 来源 | 许可证 | 核心能力 | 优点 | 缺点 | 维护状态 | 与当前项目契合度 | 可能冲突点 | 是否采用 | 采用方式 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 直接调用 SimpleLogin 官方 REST API | https://github.com/simple-login/app/blob/master/docs/api.md | API 文档公开；服务端代码 AGPL，不复用代码 | API key 校验、random alias、custom alias、toggle/delete、stats 等 | 与 Provider v1 目标一致；不需要第三方 SDK；官方文档明确 `Authentication` header | disable 是 toggle 语义，必须先读状态；真实 API 需要速率限制和错误模型 | 官方仓库维护中 | 高 | 不能复制 AGPL 服务端实现；真实网络需明确用户配置 | 部分采用 | 已实现校验、random/custom 创建、disable、delete |
| 直接调用 addy.io 官方 API | https://app.addy.io/docs/ | API 文档公开；不复用服务端代码 | aliases、recipients、rules、domains、webhooks | API 覆盖面广，适合高级能力矩阵；官方文档明确 Bearer token | 模型复杂，recipient/rules/webhook 不应硬塞进 alias CRUD | 官方文档维护中 | 高 | 真实接入会改变联网边界；高级资源需要单独 operation model | 部分采用 | 已实现校验、random/custom 创建、disable、delete |
| 查找 .NET 第三方 SDK | NuGet/GitHub 搜索 | 各项目不一 | 封装 API client | 可能减少 HTTP 样板 | 未发现足够成熟、官方、覆盖 SimpleLogin+addy.io 的通用 .NET SDK | 不稳定 | 低 | 许可证和维护风险高；可能掩盖 Provider 差异 | 不采用 | 保留未来重新审计入口 |
| 复用 SimpleLogin/addy.io 服务端代码 | GitHub 官方/社区代码 | SimpleLogin AGPL；其他项目不一 | 完整服务端逻辑 | 行为接近真实服务 | 与本地 Windows 客户端边界不匹配；许可证风险高 | 活跃 | 低 | AGPL/GPL 代码复用风险；会把产品变成服务端项目 | 不采用 | 只研究概念，不复制实现 |
| 自研统一 Provider CRUD | 项目内部 | 项目自有 | 单一 CRUD 接口 | 表面简单 | 会抹平 Provider 差异，导致禁用/删除/恢复/recipient/rules 语义错误 | 可控但风险高 | 低 | 与已有 capability model 冲突 | 不采用 | 改用 ProviderProfile + OperationPlan/Adapter |
| Core 抽象 + Infrastructure mock adapter | 项目内部 | 项目自有 | 可测试边界、能力矩阵、secret_ref 验证、离线 dry-run | 不引入依赖；后续可与真实 HTTP adapter 并存 | 不能证明真实 Provider API 可用 | 当前可维护 | 高 | 需要文档明确 mock 不等于真实接入 | 采用 | 保留作为离线计划和测试用 adapter |

## 采用结论

- 直接复用：.NET 标准库能力、现有 `ISecretStore`、现有 Provider capability model、现有 SQLite 基础设施模式。
- 借鉴设计：SimpleLogin 与 addy.io 官方 API 的资源边界、endpoint shape、能力差异。
- 直接采用：SimpleLogin 官方文档中的 `Authentication` header、`GET /api/user_info` 校验、random alias 创建、custom alias options/mailbox/create 流程、alias toggle/delete；addy.io 官方文档中的 Bearer token、`GET /api/v1/api-token-details` 校验、`POST /api/v1/aliases` 创建、active alias deactivate、alias delete。
- 不采用：第三方 .NET SDK、AGPL/GPL 服务端代码、统一 CRUD 抽象、addy.io recipient/rules/webhook 真实 HTTP。
- 需要适配：Core 增加 `ProviderAccount`、`IProviderAdapter`、create alias plan/result；Infrastructure 增加 `SqliteProviderAccountRepository`、mock adapter、SimpleLogin HTTP adapter 与 addy.io HTTP adapter。
- 需要保留：现有 alias 生成、SQLite alias 仓储、Windows Credential Manager secret store、ProviderProfile 能力矩阵。
- 需要替换：暂无替换；本轮是新增边界。

## 冲突检查

| 检查项 | 结果 |
| --- | --- |
| 与技术栈冲突 | 无；继续使用 .NET/WinUI/Core+Infrastructure 分层 |
| 与目录结构冲突 | 无；Core 放抽象，Infrastructure 放 SQLite/mock adapter |
| 与运行方式冲突 | 无；不改变 WinUI 启动 |
| 与构建方式冲突 | 无；未引入新依赖 |
| 与数据库设计冲突 | 无；新增 `provider_accounts` 保存 metadata 和 `secret_ref` |
| 与配置系统冲突 | 无；未新增配置文件 |
| 与权限模型冲突 | 无；secret 仍经 `ISecretStore` |
| 与离线/联网模式冲突 | 无阻塞；SimpleLogin/addy.io HTTP adapter 具备联网能力，但 App 尚未默认调用，离线功能不依赖它 |
| 与许可证冲突 | 无；未复用外部代码 |
| 与用户需求冲突 | 无；仍是 Windows 本地应用，不是网页 |

## 第一版实现边界

- `ProviderAccount` 保存 provider type、display name、auth/security state、last sync、`secret_ref`。
- `secret_ref` 由 `SecretKey.ForProviderToken(accountId)` 生成；账户模型不接收也不持有明文 secret。
- `SqliteProviderAccountRepository` 只保存账户元数据和 `secret_ref`。
- `IProviderAdapter` 支持 credential validation、create plan、create result。
- `IProviderAdapter` 已扩展为支持 create/disable/delete 三类 alias 操作；delete 被视为危险操作，UI 执行前必须 dry-run/确认。
- `ProviderBatchOperationPlanner` 已落地批量 disable/delete dry-run；空选择、缺 alias address、Provider 不支持能力都会阻断执行。
- Delete batch plan 一律要求显式确认，即使每个 item 都可执行，顶层 `CanExecute` 仍会因为 destructive warning 为 false。
- `ProviderBatchOperationExecutor` 已落地执行门禁；blocked plan 或未确认 delete 会被拒绝，且不会调用 Provider adapter。
- `AuditEvent` / `Tombstone` 和 `SqliteAuditLogRepository` 已落地；后续 Provider disable/delete UI 接入时必须在执行结果后追加脱敏审计事件，delete 还必须追加 tombstone。
- `SimpleLoginMockProviderAdapter` 和 `AddyIoMockProviderAdapter` 只模拟能力矩阵和计划，不调用真实 API。
- `SimpleLoginHttpProviderAdapter` 调用官方 API：`GET /api/user_info` 校验 API key，`POST /api/alias/random/new?hostname=...` 创建随机 alias。
- SimpleLogin custom alias 创建流程：`GET /api/v5/alias/options?hostname=...` 选择匹配 domain 的 `signed_suffix`，`GET /api/v2/mailboxes` 选择默认且已验证 mailbox，然后 `POST /api/v3/alias/custom/new?hostname=...`。
- SimpleLogin HTTP adapter 只把 secret 放在 `Authentication` header，不写入请求 body；测试验证 body 不包含 secret。
- 如果 SimpleLogin 没有返回匹配 domain 的 signed suffix，adapter 返回 `provider.suffix_not_found`，不会猜测或伪造 suffix。
- SimpleLogin disable 先 `GET /api/aliases/{id}` 读取状态，只有当前启用时才 `POST /api/aliases/{id}/toggle`，避免 toggle 误启用。
- SimpleLogin delete 使用 `DELETE /api/aliases/{id}`，返回本地 `Deleted` 快照。
- `AddyIoHttpProviderAdapter` 调用官方 API：`GET /api/v1/api-token-details` 校验 Bearer token，`POST /api/v1/aliases` 创建 random/custom alias。
- addy.io random alias 使用 `format=uuid`；custom alias 使用 `format=custom` 和 `local_part`；不传 `recipient_ids` 时使用 addy.io 默认 recipient。
- addy.io token 只进入 `Authorization: Bearer` header，不写入请求 body；测试验证 body 不包含 token。
- addy.io disable 使用 `DELETE /api/v1/active-aliases/{id}`；delete 使用 `DELETE /api/v1/aliases/{id}`。
- 测试用内存 secret store 验证缺少 secret 时 mock execution 会被拒绝。

## 回滚方案

- 删除新增 ProviderAccount / adapter / SQLite repository / HTTP adapter 文件和对应测试。
- 保留现有 alias 仓储、生成算法和 Credential Manager 实现不受影响。
- 因未新增包和真实网络行为，回滚不涉及依赖或配置迁移。
