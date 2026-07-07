# Email Alias Manager Research Brief

日期：2026-07-05  
阶段：Research / Reverse Engineering，尚未进入编码。

## 调研方法

- 搜索范围：GitHub、官方文档、公开帮助中心、Reddit/Hacker News/Product Hunt 搜索结果、Awesome/self-hosted 列表、Issue/PR/Roadmap。
- 当前项目状态：空目录初始化，仅有 `AGENTS.md` 与本调研文档；尚未选择最终技术栈，尚未引入依赖。
- 目标不是复刻任何服务，而是设计一个 Windows 本地、离线优先、可同步、可导入导出、可扩展 Provider 的 Alias 工作台。

## 主要资料源

| 类别 | 来源 | 用途 |
| --- | --- | --- |
| 开源服务 | https://github.com/simple-login/app | SimpleLogin 后端/Web App 架构、Issue、许可证、Provider 语义 |
| 浏览器扩展 | https://github.com/simple-login/browser-extension | 生成别名的浏览器工作流参考，代码 MIT 但不直接复制 |
| 开源服务 | https://github.com/anonaddy/anonaddy | addy.io/AnonAddy 核心能力、API 语义、GPG/收件人/规则 |
| 开源扩展/App | https://github.com/anonaddy/browser-extension, https://github.com/anonaddy/addy-android | Alias 快速生成、移动端体验参考 |
| 开源服务 | https://github.com/mozilla/fx-private-relay | Firefox Relay masks、跟踪器阻断、custom domain 相关 Issue |
| 官方文档 | https://duckduckgo.com/duckduckgo-help-pages/email-protection/ | DuckDuckGo Email Protection 的私有地址、tracker removal、回复流程 |
| 官方文档 | https://developers.cloudflare.com/email-routing/ | Cloudflare Email Routing、catch-all、routing rules、Email Workers |
| 官方文档 | https://www.fastmail.help/hc/en-us/articles/4406536368911-Masked-Email | Fastmail Masked Email 用户体验与 1Password 集成 |
| 官方文档/API | https://www.fastmail.com/developer/ | Fastmail/JMAP 生态，适合标准化 Provider 适配 |
| 官方支持 | https://support.apple.com/en-us/105078 | Apple Hide My Email 生成、转发、停用模型 |
| 官方支持 | https://proton.me/support/pass-email-alias | Proton Pass / SimpleLogin Alias 集成 |
| 开源服务 | https://github.com/forwardemail/forwardemail.net | Forward Email 的加密转发、DNS/SMTP 能力参考 |
| 自托管邮件 | https://github.com/mailcow/mailcow-dockerized, https://mailinabox.email/, https://mailu.io/ | 完整邮件服务器、域名/转发/alias 管理边界 |
| 竞品 | https://github.com/aliasvault/aliasvault | 密码管理器 + Alias + 自托管的融合方向 |
| 对比资料 | https://github.com/fynks/email-aliasing-comparison | Email aliasing 服务横向对比参考 |

## GitHub 项目信号

使用 GitHub REST API 于 2026-07-05 拉取公开仓库元数据：

| 项目 | Stars | Open issues | License | 语言 | 观察 |
| --- | ---: | ---: | --- | --- | --- |
| simple-login/app | 6763 | 247 | AGPL-3.0 | Python | 成熟、活跃、Provider 语义完整；许可证不适合直接复用代码 |
| anonaddy/anonaddy | 4733 | 60 | AGPL-3.0 | PHP | API、规则、收件人、GPG 能力强；代码不可直接合入闭源/非 AGPL 项目 |
| mozilla/fx-private-relay | 1755 | 32 | NOASSERTION | Python | 用户信任强，custom domain/兼容性 Issue 明显 |
| forwardemail/forwardemail.net | 1612 | 49 | NOASSERTION | JavaScript | 邮件基础设施完整，但范围远超本地 Alias 管理器 |
| aliasvault/aliasvault | 2906 | 161 | AGPL-3.0 | TypeScript | Password manager + alias 是趋势；不可复制代码，需独立设计 |
| mailcow/mailcow-dockerized | 13044 | 504 | GPL-3.0 | JavaScript | 强自托管邮件服务器，不适合作为桌面 App 依赖 |
| mail-in-a-box/mailinabox | 15349 | 613 | CC0-1.0 | Python | 邮件服务器自动化理念可借鉴，不应内置邮件服务器 |
| mailu/mailu | 7341 | 119 | NOASSERTION | Python | 容器化邮件栈，属于 Provider/基础设施层 |
| bitwarden/clients | 13176 | 1196 | NOASSERTION | TypeScript | 密码管理器集成 alias 的 UX 参考，不复用代码 |

## Issue / PR 证据

| 来源 | 证据 | 对本项目的启发 |
| --- | --- | --- |
| simple-login/app#1266 | Bulk / mass editing of aliases | 批量修改不是锦上添花，是重度用户刚需 |
| simple-login/app#491 | Import/Export aliases and data | 导入导出要从第一版数据模型就考虑，不可后补 |
| simple-login/app#88 | Migrating from existing custom solution/domain | 迁移向导、dry-run、字段映射是核心 UX |
| simple-login/app#301 | Catch-all self-hosted setup | Catch-all 行为要与普通 alias 区分，不能用一个布尔值糊住 |
| mozilla/fx-private-relay#3379 | BYO custom domain request | 自定义域/域名选择是长期痛点 |
| mozilla/fx-private-relay#3604 | 某些网站阻止 Relay 地址 | 需要“站点兼容性评分”和生成策略回退 |
| mozilla/fx-private-relay PR#2882 | Reply-to-replies | 从别名发信/回复链路是用户容易误解的高风险功能 |
| anonaddy/anonaddy#237 | Configuration import/export backup | 备份/恢复应覆盖配置，不只是 alias 表 |
| aliasvault/aliasvault#731 | v1.0 roadmap | 自托管 + 密码/alias 融合仍在快速演进 |
| aliasvault/aliasvault#2034 | Group emails by alias/filter inbox | Alias 管理不止“创建地址”，还要能解释收件流量 |

## 竞品逆向对比

| 产品/项目 | 核心功能 | 用户喜欢的点 | 典型短板/吐槽 | 本项目保留 | 本项目创新方向 |
| --- | --- | --- | --- | --- | --- |
| SimpleLogin | Alias、mailbox、custom domain、browser extension、reply/send | 功能完整，Proton 背书，API/自托管生态成熟 | 批量治理、迁移、桌面本地工作台不足 | Provider 适配、mailbox/domain 概念 | 本地统一数据库、跨 Provider 批量操作、风险评分 |
| addy.io / AnonAddy | Alias、recipients、rules、GPG、webhooks/API | 高级用户喜欢规则与自定义能力 | UX 对普通用户偏工程化 | 规则、recipient、GPG/加密语义 | 规则可视化、变更预演、导入冲突解释 |
| Firefox Relay | Mask、一键生成、Mozilla 信任、tracker blocking | 上手快，隐私叙事清晰 | Custom domain/兼容性/高级管理能力有限 | 快速生成、隐私保护提示 | 站点兼容性记忆、策略化生成 |
| DuckDuckGo Email Protection | Duck 地址、tracker removal、简单转发 | 简单、低心智负担 | API/批量/迁移能力弱 | Tracker 风险提醒 | 本地 tracking 分类标签，不把用户锁死到单服务 |
| Cloudflare Email Routing | DNS 路由、catch-all、Email Workers | 域名用户强大，可编程 | 需要懂 DNS/Workers，发信能力不是主目标 | Domain routing、catch-all、Worker hook | 非工程用户的 DNS/route 检查向导 |
| Fastmail Masked Email | Masked Email、1Password 集成、JMAP 生态 | 与邮箱/密码管理深度融合 | 锁定 Fastmail，跨服务管理弱 | 标准 API/JMAP 思路 | Provider-agnostic masked email 抽象 |
| Apple Hide My Email | iCloud+ 随机地址、系统级集成 | 系统体验顺滑，低学习成本 | 非 Apple 生态受限，导出/API 弱 | 简单创建/停用模型 | Windows 上的系统托盘/快捷键/剪贴板体验 |
| Proton Pass Alias | 密码管理器与 Alias 联动 | 注册时同时生成密码和 alias | 仍以 Proton/SimpleLogin 生态为中心 | 密码管理器联动概念 | 兼容 Bitwarden/1Password/KeePass 等外部工具 |
| Forward Email | 加密邮件转发、DNS/SMTP | 技术透明，功能深 | 作为桌面 alias manager 过重 | 转发/DNS 检查理念 | 只做 Provider 适配，不内置邮件系统 |
| AliasVault | 密码管理器 + Alias + 自托管 | 一体化隐私工具趋势明显 | AGPL，且不是专注本地 Windows 桌面工作台 | 账户/alias 联动 | 独立本地 alias cockpit，可选连接密码库 |
| Mailcow/Mail-in-a-box/Mailu | 完整自托管邮件服务器 | 控制权最大 | 运维复杂，不适合普通桌面用户 | 作为外部 Provider/导入源 | 绝不把桌面 App 变成邮件服务器 |

## 用户痛点归纳

- 批量治理：重命名、换收件人、换域、禁用、删除、恢复、导出，不应逐条操作。
- 迁移困难：不同服务字段模型不一致，导入导出经常丢 metadata。
- 站点兼容性：部分网站阻止已知 alias 域或 `+tag` 风格地址。
- Catch-all 风险：方便但会扩大垃圾邮件面，且很难追踪首次泄露来源。
- 回复/发信困惑：用户容易误解“从 alias 回复”和“主动从 alias 发信”的差异。
- 隐私错觉：Alias 能隐藏真实地址，但不能自动解决浏览器指纹、支付信息、手机号、收货地址关联。
- 锁定风险：Apple/Fastmail/Proton/DuckDuckGo 的体验好，但 API/导出/跨平台能力差异大。
- 高级服务太工程化：Cloudflare、mailcow、Mailu 强大但需要 DNS/SMTP/运维知识。

## 初步产品定位

这个应用应被设计成“本地 Alias Cockpit”，而不是又一个转发服务：

- 本地持久化：用户拥有自己的 alias 清单、标签、规则、历史、导入导出映射。
- Provider-agnostic：SimpleLogin、addy.io、Fastmail、Cloudflare 等都只是适配器。
- 离线优先：没有网络也能搜索、标记、批量规划、导出加密备份。
- 安全默认：凭据进系统凭据库，数据库/导出文件加密，日志脱敏。
- 大数据量体验：几千到几万条 alias 下搜索/过滤/批量编辑仍顺畅。
- 可解释：每个 alias 有来源、用途、风险、状态、Provider 能力差异说明。

## 当前不采用的方向

- 不内置临时邮箱收件箱服务：会引入滥用、合规、投递与隐私风险。
- 不内置完整邮件服务器：mailcow/Mailu/Mail-in-a-box 已经成熟，桌面 App 不应承担 MTA/SMTP 运维。
- 不复制 SimpleLogin/addy/AliasVault 代码：AGPL/GPL/NOASSERTION 许可证风险明显，且产品目标不同。
- 不以 Tauri/Electron 作为首选：虽然开发快，但用户明确要求“Windows 本地应用（不是网页）”，WebView/Chromium 壳存在目标冲突。

