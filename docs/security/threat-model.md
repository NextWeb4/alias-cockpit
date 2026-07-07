# Security Threat Model

日期：2026-07-05  
状态：第一版草案，开发前必须继续细化。

## 资产

- API Token、OAuth refresh token、Provider app password。
- 真实收件邮箱、别名地址、域名、catch-all 状态。
- 标签、项目、备注、网站用途，可能暴露用户生活/工作图谱。
- Provider ID、远端对象 ID、规则、webhook URL。
- 本地数据库、导出文件、同步包、日志、崩溃报告。
- 生成算法配置和站点兼容性历史。

## 对手模型

| 对手 | 能力 | 防护重点 |
| --- | --- | --- |
| 本机其他普通用户 | 可访问部分文件/备份 | 数据库加密、Windows 用户级密钥、文件权限 |
| 恶意软件 | 可读进程内存/剪贴板/文件 | 无法完全防御；减少明文驻留、剪贴板超时、日志脱敏 |
| 网络中间人 | 观察/篡改流量 | TLS、证书校验、token 不进 URL、最小重试日志 |
| 恶意导入文件 | 超大文件、公式注入、路径穿越、畸形 JSON/CSV | streaming parser、大小限制、dry-run、导出转义 |
| Provider 泄露/失效 | 远端数据变化/token 失效 | 本地备份、Provider 状态隔离、token 最小权限 |
| 同步仓库泄露 | 读取 Git/网盘同步包 | 同步包强制加密、metadata 最小化 |
| Provider 隐私漏洞 | Alias 服务本身可能暴露真实邮箱 | Provider 风险公告、手工降级、避免承诺匿名 |

## 关键不变量

- API token 不得写入 SQLite 普通表、日志、测试快照、导出文件。
- 真实收件邮箱默认不出现在日志和普通导出中。
- 离线功能不得隐式联网。
- 批量删除/禁用/覆盖必须可预演并可恢复。
- 生成 alias 必须使用加密安全随机数；可读模式也必须满足最低熵。
- Provider 同步失败不得破坏本地数据。
- 导入文件未经 dry-run 不得直接写入主库。
- Alias 只能降低邮箱暴露和关联风险，不得在 UI 或文档中承诺匿名。

## Secret 存储

推荐：

- Token 存 Windows Credential Manager，数据库只保存 `secret_ref`。
- 本地数据库加密密钥由 DPAPI 保护，绑定当前 Windows 用户。
- 跨设备同步密钥必须由用户显式创建/导入，不自动上传。
- 导出完整数据时默认加密，密码派生使用成熟 KDF；不自造算法。

禁止：

- 把 token 放入 `appsettings.json`、SQLite 明文字段、环境变量模板、日志。
- 为了测试方便加入固定 token。
- 把同步密钥与同步包放在同一目录且无额外保护。

## Alias 生成安全

| 风险 | 说明 | 控制 |
| --- | --- | --- |
| 可预测别名 | 自增、日期、网站名裸露会被枚举 | CSPRNG、最低熵、风险提示 |
| 过度语义化 | `bankname-realname@domain` 泄露用途 | 隐私等级：可读/半匿名/强匿名 |
| 站点阻止 | 常见 alias 域、`+tag` 被拒 | 兼容性数据库、格式 fallback |
| Catch-all 枚举 | 攻击者可猜测任意地址 | catch-all 风险提示、速率/规则建议 |
| 复用别名 | 多站点复用会降低隔离 | 复用检测和轮换建议 |

## 网络与 Provider

- 每个 Provider adapter 必须声明能力、权限、速率限制、是否支持删除/恢复/禁用。
- 每个 Provider 必须支持风险状态：`healthy`, `degraded`, `security_advisory`, `manual_only`。
- API 请求不得把 token 放入 query string。
- 错误日志只保留 provider、endpoint 类别、HTTP 状态、request_id，不保留完整邮箱/token。
- 支持系统代理/自定义代理，但代理配置属于敏感配置。
- OAuth/Token 过期只影响对应 Provider，不阻塞本地 App。
- 当前 Provider token 存储边界已落到 `ISecretStore` / Windows Credential Manager；SQLite 只允许保存未来 `secret_ref`。

## 日志脱敏规则

| 数据 | 默认显示 | 日志/诊断 |
| --- | --- | --- |
| alias 地址 | UI 可显示 | `g***b@example.com` 或 hash |
| 真实收件邮箱 | UI 默认部分隐藏 | hash |
| token | 永不显示 | 永不记录 |
| 域名 | UI 可显示 | 可记录，但用户可选择脱敏 |
| 备注/标签 | UI 可显示 | 默认不进诊断包 |
| 请求/响应体 | 不显示 | 默认不记录；debug 也需红线过滤 |

## 导入导出安全

- CSV 导出防公式注入：以 `=`, `+`, `-`, `@` 开头的单元格必须转义。
- 导入文件大小、行数、字段长度必须有限制。
- 导入前显示新增/修改/冲突/无法识别项，不直接写库。
- 完整导出包含真实邮箱、备注、Provider ID 时必须二次确认。
- 加密导出需要记录版本、KDF 参数、schema 版本和校验。

## 同步安全

- Git/网盘同步只写加密包，不写明文 SQLite。
- 同步包 metadata 最小化，避免文件名暴露邮箱/域名。
- 每个设备有独立 device key 和 device_id。
- Merge 失败时进入冲突队列，不静默覆盖。
- 删除使用 tombstone，避免另一个设备离线后“复活”已删除 alias。

## 需要后续验证

- SQLCipher/DPAPI 组合在 WinUI 打包模式下的密钥迁移行为。
- Windows Credential Manager 在卸载/重装、便携版、不同用户下的行为。
- Provider API 的最小权限 token 能否满足批量/同步。
- 10k/50k alias 下 UI 虚拟化与 SQLite FTS 查询性能。
- UI 自动化工具在 WinUI 3 下的稳定性。
- Apple Hide My Email 2026-07 漏洞后续状态；在公开修复前保持 `security_advisory/manual_only` 处理。
