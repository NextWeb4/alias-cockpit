# Alias Marker And Saved Input Persistence Audit

## 第一性原理分析

用户期望行为：
- 输入邮箱地址可以保存，并能在下次打开或后续操作中重新选择。
- 某一个生成出的 alias 可以标记“用在哪里”和用途。
- 被标记的 alias 在结果列表中颜色变化。
- 标记和保存邮箱必须重启后仍存在。

实际缺口：
- 旧主页面只生成和复制 alias，没有把输入邮箱或 alias 使用关系写入本地状态。
- SQLite alias 模型已有 `site` / `purpose`，但没有颜色字段，也没有按地址读取能力。
- 没有保存输入邮箱地址的独立表。

不变量：
- alias 地址是标记的唯一键；重新生成同一个地址时必须能找回同一份标记。
- 输入邮箱历史只保存经过当前 Gmail/Outlook 裂变规则验证后的规范地址。
- 本功能只能读写本机 SQLite，不得调用 Provider HTTP adapter，不得联网。
- SQLite 仍未加密，不得保存 token、密码、恢复码或其他 secret。

根因：
- 数据层已能保存 alias 元数据，但 UI 没有接入仓储。
- 颜色和输入邮箱历史是当前 schema 的缺失能力，不是生成算法问题。

最小修复点：
- 给 `AliasRecord` 增加 `AliasColor`。
- 给 `IAliasRepository` 增加 `GetByAddressAsync`。
- 增加 `ISavedEmailAddressRepository` 和 SQLite `saved_email_addresses` 表。
- 主页面增加保存邮箱、选择历史邮箱、选中 alias 标记和颜色渲染。

## 开源方案审计

| 方案名称 | 来源 | 许可证 | 核心能力 | 优点 | 缺点 | 维护状态 | 与当前项目的契合度 | 可能冲突点 | 是否采用 | 采用方式 |
|---|---|---|---|---|---|---|---|---|---|---|
| 现有 SQLite + Microsoft.Data.Sqlite | 当前项目已采用的 NuGet/SQLite 方案 | SQLite public domain / Microsoft.Data.Sqlite MIT | 本地表、事务、FTS、迁移 | 已接入项目、离线、可测试、无新增依赖 | 数据库尚未加密 | 已审计并在项目中稳定使用 | 敏感字段不能明文保存 | 采用 | 扩展 alias schema，新增 saved email 表 |
| System.Text.Json 本地文件 | .NET 标准库 | MIT | 保存简单配置/历史 | 无新依赖、实现简单 | 与 alias SQLite 主数据源割裂，难以搜索/迁移/同步 | 随 .NET 维护 | 中 | 会形成旁路数据 | 不采用 | 不引入 UI 私有 JSON |
| 新设置/偏好库 | NuGet/GitHub 生态 | 视具体库而定 | 用户设置持久化 | 快速 | 新依赖、许可证和维护需额外审计 | 未审计 | 低 | 不能承载 alias 元数据 | 不采用 | 当前收益不足 |
| LiteDB/其他嵌入式 DB | NuGet/GitHub 生态 | 多为 MIT/其他 | 文档型本地存储 | 简单 | 与既有 SQLite 架构冲突，迁移成本高 | 未重新审计 | 低 | 双数据库复杂度 | 不采用 | 保留现有 SQLite |

## 冲突检查

| 检查项 | 结论 |
|---|---|
| 与现有技术栈 | 兼容，继续使用 .NET 8 + WinUI + Microsoft.Data.Sqlite |
| 与目录结构 | 兼容，Core 放接口/模型，Infrastructure 放 SQLite，App 只通过接口调用 |
| 与运行方式 | 兼容，仍是本地 exe |
| 与构建方式 | 兼容，无新包 |
| 与数据库设计 | 兼容，增量添加 `aliases.color` 和 `saved_email_addresses` |
| 与配置系统 | 无冲突 |
| 与权限模型 | 无新权限 |
| 与离线/联网模式 | 无联网变化 |
| 与许可证 | 无新增许可证风险 |
| 与用户需求 | 满足保存输入邮箱、标记使用位置、颜色变化 |

## 验证方式

- `SqliteAliasRepositoryTests` 验证 color 持久化和按地址读取。
- `SqliteSavedEmailAddressRepositoryTests` 验证保存邮箱去重和最近使用排序。
- `AliasCsvImportExportTests` 验证 color 导入导出不丢失。
- 发布前继续运行 `scripts\verify-release.ps1`。
