# Email Protocol Security Notes

日期：2026-07-05  
状态：安全调研笔记。

## Alias 与邮件协议的边界

Alias manager 管理的是地址、转发、规则、状态和本地元数据。它不能替代邮件服务器，也不能保证投递。

必须向用户解释：

- Alias 可降低真实邮箱暴露。
- Alias 不能隐藏浏览器指纹、支付信息、手机号、收货地址、IP 或账号行为。
- Forwarding 可能影响 SPF/DMARC。
- 从 alias 回复/发信的能力取决于 Provider，不是本地 App 自己能保证。

## SPF / DKIM / DMARC / ARC / SRS

| 协议 | 与 Alias/Forwarding 的关系 | 设计影响 |
| --- | --- | --- |
| SPF | 检查发送 IP 是否被 envelope sender 域授权；转发时常失效 | App 应提示 forwarding deliverability 风险 |
| DKIM | 邮件内容和部分 header 签名；转发修改内容可能破坏签名 | Provider tracker removal/rewriting 可能影响 DKIM |
| DMARC | 要求 SPF 或 DKIM 与 From 域对齐 | 转发服务需要 ARC/SRS/重写策略 |
| ARC | 记录中间转发链的认证结果 | 高级 Provider 风险说明中可展示 |
| SRS | 重写 envelope sender 以减少 SPF 转发失败 | 完整邮件服务器/转发服务侧能力，桌面 App 不实现 |

本项目不实现 MTA、SMTP relay、DKIM 签名或 SRS。它只检查/解释 Provider 和域名配置风险。

## Catch-all 风险

优点：

- 注册任意站点无需预先创建 alias。
- 迁移自定义域时容错。
- 识别泄露来源。

风险：

- 攻击者可向任意 local-part 发垃圾邮件。
- 如果 local-part 规则可预测，枚举成本低。
- 用户容易忘记哪些地址真实存在。

UI 要求：

- Catch-all 域名必须有风险 badge。
- 对高价值网站不默认推荐 catch-all 可读前缀。
- 支持把 on-the-fly 收到的地址转为受管理 alias。

## Tracking 与隐私

Email tracking 包括：

- tracking pixel
- link redirect
- unique recipient token
- remote image load
- unsubscribe token

Alias manager 的职责：

- 标注 Provider 是否声称移除 tracker。
- 记录某 alias 是否出现异常收件量。
- 支持泄露/垃圾邮件标记和轮换。

不应承诺：

- 本地 App 可以删除所有 tracker。
- 只用 alias 就能匿名。

## OAuth / API Token

要求：

- 优先最小权限 token。
- 不保存主邮箱密码。
- token 不进 URL、不进日志、不进导出。
- OAuth refresh token 使用系统凭据保护。
- Provider adapter 必须声明所需 scope。

## SMTP / IMAP / POP3

第一版不直接接入 IMAP/POP3 收信，也不内置 SMTP 发信。

原因：

- 会扩大敏感数据面：完整邮件正文、附件、联系人。
- 密码/授权模型更复杂。
- 与“alias 管理工作台”的核心目标不一致。

后续可作为插件：

- 只读取 header/alias activity。
- 不下载正文。
- 明确权限和脱敏策略。

## 参考

- SPF: RFC 7208
- DKIM: RFC 6376
- DMARC: RFC 7489
- ARC: RFC 8617
- Cloudflare Email Routing docs: https://developers.cloudflare.com/email-routing/
- Fastmail developer docs: https://www.fastmail.com/dev/

