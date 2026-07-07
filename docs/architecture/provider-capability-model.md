# Provider Capability Model

日期：2026-07-05  
状态：设计草案；来自官方文档、GitHub Issue、社区反馈和安全调研。

## 为什么不能做“统一 Alias API”

Email alias 服务表面都是“创建一个转发地址”，实际差异很大：

- SimpleLogin 有 alias、mailbox、contact、custom domain、signed suffix/reverse alias 等概念。
- addy.io 有 aliases、recipients、rules、domains/usernames、GPG、webhooks、on-the-fly 创建语义。
- Fastmail Masked Email 基于 JMAP/邮箱账户，mask 与邮箱生态绑定。
- Cloudflare Email Routing 本质是 DNS/路由规则与 destination address，不是 alias 产品。
- Apple/DuckDuckGo/Firefox Relay 偏生态级 masked email，自动化 API 能力有限或不稳定。

因此本项目必须以“Provider 能力矩阵 + 操作计划”作为核心抽象，而不是给所有服务套一个 CRUD。

## ProviderProfile

每个 Provider adapter 必须声明：

```text
provider_type
display_name
auth_methods
network_mode
entities
capabilities
limits
rate_limits
privacy_risks
security_state
supported_import_formats
supported_export_formats
dangerous_operations
```

`security_state` 取值：

```text
healthy
degraded
security_advisory
manual_only
unsupported
```

## Capability 分类

| Capability | 含义 | 例子 |
| --- | --- | --- |
| `alias.create.random` | 远端随机创建 alias | SimpleLogin random alias、Fastmail MaskedEmail |
| `alias.create.custom` | 指定 local-part 创建 alias | SimpleLogin custom alias、addy.io custom alias |
| `alias.create.on_the_fly` | 收到邮件时自动创建 | addy.io shared/custom domain catch-all |
| `alias.update.metadata` | 更新说明、名称、标签、备注 | Provider 支持差异大 |
| `alias.disable` | 禁用但保留 | Apple Hide My Email、SimpleLogin、addy.io |
| `alias.delete` | 删除远端对象 | 大多支持，但恢复语义不同 |
| `alias.restore` | 从删除/禁用恢复 | 不一定支持 |
| `recipient.manage` | 管理转发收件人 | addy.io recipients、SimpleLogin mailbox |
| `domain.manage` | 管理自定义域 | SimpleLogin、addy.io、Cloudflare、Fastmail |
| `domain.catch_all` | catch-all/通配路由 | addy.io、Cloudflare、部分 SimpleLogin |
| `reply.via_alias` | 通过 alias 回复 | SimpleLogin/addy.io reverse alias 类能力 |
| `send.from_alias` | 主动从 alias 发信 | Provider 支持差异大 |
| `rules.manage` | 条件规则/路由规则 | addy.io rules、Cloudflare routing rules |
| `webhook.receive` | 远端事件回调 | addy.io/部分服务 |
| `stats.read` | 活动/使用统计 | 服务差异很大 |
| `export.remote` | 从 Provider 拉完整导出 | 少数支持 |
| `import.remote` | 向 Provider 批量导入 | 少数支持 |

Capability 必须带限定：

```text
supported: true/false/partial/manual
scope_required
offline_available
idempotency
undo_support
remote_side_effect
rate_limit
data_loss_risk
```

## 标准操作模型

UI 不直接调用 Provider adapter，而是生成 `OperationPlan`：

```text
OperationPlan
- operation_id
- workspace_id
- intent
- local_preconditions
- provider_preconditions
- affected_alias_ids
- steps[]
- expected_local_changes
- expected_remote_changes
- reversible
- rollback_plan
- dry_run_warnings[]
```

每个 step：

```text
OperationStep
- step_id
- provider_account_id
- capability_required
- local_entity_ref
- remote_entity_ref
- request_shape_hash
- can_run_offline
- requires_network
- retry_policy
- idempotency_key
- redaction_policy
```

批量操作流程：

1. UI 创建 intent。
2. Core 解析为 OperationPlan。
3. Provider registry 验证每个目标是否支持。
4. Dry-run 输出可执行项、不可执行项和风险。
5. 用户确认后执行。
6. 本地写入 AuditEvent 和 SyncEvent。
7. 远端失败时标记 partial failure，不覆盖本地事实。

## Provider 差异表

| Provider | 主要实体 | 强项 | 弱项/风险 | 第一版集成等级 |
| --- | --- | --- | --- | --- |
| SimpleLogin | Alias, Mailbox, Domain, Contact | 功能成熟，生态稳定，自定义域，reverse alias | AGPL 代码不可复用；批量治理需本地补强 | API adapter v1 |
| addy.io | Alias, Recipient, Rule, Domain, Username, Webhook | API/规则/GPG/recipient 强，高级用户友好 | 模型复杂，普通用户心智负担高 | API adapter v1 |
| Fastmail | MaskedEmail, Mailbox/JMAP | 标准化 JMAP，邮箱内体验好 | 锁定 Fastmail，部分能力需账户权限 | API adapter v2 |
| Cloudflare Email Routing | Address, Route, Rule, DNS | 域名/DNS/catch-all 强，可编程 | 不是 alias manager；主动发信不在主路径 | Domain routing adapter v2 |
| Firefox Relay | Mask, Domain, Relay | 新手友好，Mozilla 信任，tracker 叙事 | API/批量/自定义域能力需持续核验 | Import/manual first |
| DuckDuckGo Email Protection | Duck address, private address | tracker removal 叙事强，低心智 | 无稳定公开批量 API | Manual/import first |
| Apple Hide My Email | Random address | 系统级体验 | 自动化差；2026-07 公开安全争议 | Manual only |
| Proton Pass Alias | SimpleLogin-backed alias | 与密码管理器结合 | Proton 生态锁定；API 需核验 | SimpleLogin path first |

## Provider Adapter 接口草案

```text
IProviderAdapter
- GetProfile()
- ValidateCredentials()
- PullSnapshot(cursor)
- PlanCreateAlias(request)
- ExecuteCreateAlias(plan_step)
- PlanUpdateAlias(request)
- ExecuteUpdateAlias(plan_step)
- PlanDisableAlias(request)
- ExecuteDisableAlias(plan_step)
- PlanDeleteAlias(request)
- ExecuteDeleteAlias(plan_step)
- PullActivity(alias_ref, cursor)
- ExportSnapshot()
```

不是所有方法都必须支持；不支持时必须返回结构化结果：

```text
UnsupportedOperation
- capability
- provider_type
- reason
- suggested_local_fallback
```

## 当前实现状态

已落地第一版可测试边界：

- Core：`ProviderAccount`、`ProviderAuthState`、`IProviderAccountRepository`、`IProviderAdapter`、`ProviderRegistry`、create/disable/delete alias request/reference/plan/result、`ProviderBatchOperationPlanner`、`ProviderBatchOperationExecutor`。
- Infrastructure：`SqliteProviderAccountRepository`、`SimpleLoginMockProviderAdapter`、`AddyIoMockProviderAdapter`、`SimpleLoginHttpProviderAdapter`、`AddyIoHttpProviderAdapter`。
- 安全边界：ProviderAccount 只保存 `secret_ref`，明文 secret 必须走 `ISecretStore`。
- 联网边界：SimpleLogin/addy.io HTTP adapter 已具备真实联网能力，覆盖 API key 校验、random/custom alias 创建、disable 和 delete；App 尚未默认调用；mock adapter 仍不发真实 HTTP 请求。
- Dry-run 边界：批量 disable/delete 必须先经过 `ProviderBatchOperationPlanner`。Delete plan 一律 `RequiresExplicitConfirmation=true`，且会产生 destructive warning。
- 执行边界：`ProviderBatchOperationExecutor` 会拒绝 blocked plan；delete 未传入显式确认时不会调用 adapter。

真实 addy.io recipient/rules/webhook、SimpleLogin/addy.io 同步执行流落地前必须重新核验官方 API、鉴权方式、速率限制、错误响应、许可证和联网提示。

## 错误模型

| 错误 | 含义 | UI 行为 |
| --- | --- | --- |
| `auth.expired` | token 失效 | Provider badge，允许本地继续 |
| `capability.unsupported` | Provider 不支持 | Dry-run 中列出并跳过 |
| `rate_limited` | 速率限制 | 队列延迟，不重复弹窗 |
| `remote_conflict` | 远端版本变化 | 进入冲突队列 |
| `remote_missing` | 本地有，远端无 | 标记状态，不自动删除 |
| `validation_failed` | alias/domain/recipient 无效 | 指向字段错误 |
| `security_advisory` | Provider 风险状态 | 暂停危险操作或要求二次确认 |

## 来源

- SimpleLogin: https://github.com/simple-login/app, https://simplelogin.io/docs/
- addy.io API: https://app.addy.io/docs/
- Fastmail developer/JMAP: https://www.fastmail.com/dev/
- Fastmail Masked Email help: https://www.fastmail.help/hc/en-us/articles/4406536368911-Masked-Email
- Cloudflare Email Routing: https://developers.cloudflare.com/email-routing/
- Firefox Relay: https://github.com/mozilla/fx-private-relay
- DuckDuckGo Email Protection: https://duckduckgo.com/duckduckgo-help-pages/email-protection/
- Apple Hide My Email: https://support.apple.com/en-us/105078
