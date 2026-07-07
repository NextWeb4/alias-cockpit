# Secrets And Crypto Design

日期：2026-07-05  
状态：开发前安全设计。

## 目标

- Token 与密钥不落入普通数据库、日志、导出、测试快照。
- 数据库和同步包可在本地设备丢失或云盘泄露时降低风险。
- 所有加密使用成熟库和平台能力，不自造算法。

## Secret 分类

| Secret | 存储位置 | 说明 |
| --- | --- | --- |
| Provider API token | Windows Credential Manager | SQLite 只保存 `secret_ref` |
| OAuth refresh token | Windows Credential Manager | adapter 负责刷新 |
| DB encryption key | DPAPI wrapping | 绑定 Windows 用户 |
| Sync key | 用户显式创建/导入 | 可跨设备，必须有恢复流程 |
| Export password | 不保存 | 只用于导出/导入 |

## Windows Credential Manager / DPAPI

优先策略：

1. 为每个 Provider account 生成稳定 `secret_ref`。
2. token 写入 Windows Credential Manager。
3. SQLite 中只保存 `secret_ref`、provider、scope hash。
4. DB key 由随机生成，使用 DPAPI 当前用户保护。
5. 同步密钥不自动写入同步目录。

当前实现状态：

- `AliasCockpit.Core.Secrets.ISecretStore` 已定义 secret store 边界。
- `AliasCockpit.Infrastructure.Security.WindowsCredentialManagerSecretStore` 已通过 `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFree` 接入 Windows Credential Manager。
- Credential target name 格式为 `AliasCockpit/<secret-key>`，测试使用 `AliasCockpit.Tests/<secret-key>`。
- secret key 只允许小写字母、数字、`/`, `_`, `-`, `.`，最长 180 字符。
- CredentialBlob 当前限制为 2560 UTF-8 bytes，超出直接拒绝。
- 集成测试会写入唯一测试凭据并在 finally 删除。
- `ProviderAccount` 已生成稳定 `secret_ref`，格式为 `provider-token/{provider_account_id}`。
- `SqliteProviderAccountRepository` 只保存 provider metadata、auth/security state 和 `secret_ref`，不保存明文 secret。
- SimpleLogin/addy.io mock adapter 缺少 `ISecretStore` 中的 secret 时拒绝 mock execution，不发真实网络请求。
- `SimpleLoginHttpProviderAdapter` 已支持真实 API key 校验、random alias 创建和 custom alias 创建；secret 只进入 `Authentication` header，不进入请求 body，测试会检查 body 不含 secret。
- SimpleLogin custom alias 只使用官方返回的 `signed_suffix`，并通过 `/api/v2/mailboxes` 选择默认/已验证 mailbox；不得在本地伪造 signed suffix 或硬编码 mailbox id。
- `AddyIoHttpProviderAdapter` 已支持真实 API token 校验、random alias 创建和 custom alias 创建；token 只进入 `Authorization: Bearer` header，不进入请求 body，测试会检查 body 不含 token。
- addy.io adapter 不传 `recipient_ids` 时使用服务端默认 recipient，避免本地硬编码真实收件人 ID。
- SimpleLogin/addy.io disable/delete 只使用 remote alias id 和 secret header，不把 token 写入 body；delete 属于危险操作，后续 UI 必须二次确认并记录审计事件。
- 批量 Provider delete 的 Core dry-run 会强制 `RequiresExplicitConfirmation=true`，`ProviderBatchOperationExecutor` 在未确认时会拒绝执行且不会调用 adapter；不得由 UI 或应用服务绕过。
- `AuditEvent` 和 `Tombstone` 已在 Core 建模，`SqliteAuditLogRepository` 已落地 `audit_events` / `tombstones` 持久化；写入 `redacted_summary_json` 前必须由调用方完成 token、真实收件邮箱和敏感备注脱敏。

需要验证：

- 应用打包/非打包模式下凭据 target name 是否稳定。
- 便携版路径变化是否影响 DPAPI。
- Windows 用户切换/重装后恢复流程。
- Windows Credential Manager 在用户备份/还原和企业策略下的行为。

## 数据库加密策略

候选：

- SQLCipher 整库加密。
- SQLite 明库 + 敏感字段 envelope encryption。
- 两者组合：整库加密为默认，字段加密用于高敏字段和导出。

暂定：

- 第一版开发时优先保证敏感字段和同步包加密。
- 若 SQLCipher 打包/许可证/平台验证通过，升级为整库加密默认。
- 不为了加密引入不可维护的 native 打包复杂度。

## 同步包加密

要求：

- 使用认证加密：AES-256-GCM 或 XChaCha20-Poly1305。
- 使用成熟 KDF：Argon2id 或 PBKDF2，取决于最终平台库。
- 包含 version、kdf params、salt、nonce、ciphertext、tag。
- 文件名不泄露邮箱/域名/站点。

## 日志红线

永不记录：

- token / refresh token
- OAuth authorization code
- DB key / sync key / export password
- 完整真实收件邮箱
- 完整 Provider 请求/响应体

默认脱敏：

- alias address
- domain 可配置脱敏
- notes/tags 不进诊断包

## 导出安全

导出类型：

| 类型 | 内容 | 默认 |
| --- | --- | --- |
| Redacted CSV | alias、状态、标签、Provider 名称 | 默认 |
| Full encrypted backup | 完整字段、真实邮箱、notes、历史 | 需密码/密钥 |
| Provider migration package | 用于迁移到其他 Provider 的字段 | 需 dry-run |

CSV 防公式注入：

- 以 `=`, `+`, `-`, `@`, tab, CR 开头的字段必须转义。

## 测试证明

- 搜索 SQLite dump 不应出现 token。
- 搜索日志目录不应出现 token/完整真实邮箱。
- 导出 redacted CSV 不应出现真实收件邮箱。
- 同步目录不应出现明文 alias 清单。
- 恶意导入文件不能写任意路径或触发公式注入。
- Credential Manager round-trip 测试必须证明 secret 不依赖 SQLite。

## 参考

- Microsoft Credential Management API: https://learn.microsoft.com/en-us/windows/win32/secauthn/credential-management
- `CREDENTIALW`: https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentialw
- `CredWriteW`: https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew
- `CredReadW`: https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw
- .NET DPAPI `ProtectedData`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata
