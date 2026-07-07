# ADR 0002: Local-first SQLite With Encrypted Secrets

日期：2026-07-05  
状态：Proposed

## Context

应用需要离线搜索、批量管理、导入导出、同步、审计、回收站、撤销和几千到几万条 alias 的性能。

## Options

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| SQLite + FTS5 | 稳定、单文件、可迁移、查询能力强 | 默认不加密 |
| SQLCipher | 成熟 SQLite 加密 | native 打包和许可证/发布需验证 |
| LiteDB | .NET 简单、内置加密 | 查询/迁移/FTS/长期生态弱于 SQLite |
| JSON 文件 | 简单 | 并发、查询、迁移、性能差 |
| RocksDB/LevelDB | 高性能 KV | 过重，不适合关系/搜索/导入导出 |

## Decision

采用 SQLite 作为本地事实源，FTS5 做搜索索引。

当前实现状态：

- `src/AliasCockpit.Infrastructure` 使用 `Microsoft.Data.Sqlite` 和 `SQLitePCLRaw.bundle_e_sqlite3`。
- WinUI shell 当前开发数据库路径为 `%LocalAppData%\AliasCockpit\aliases.sqlite`。
- 已实现 `aliases` 表和 `alias_search` FTS5 表。
- 已实现批量 upsert、搜索和基础集成测试。
- 当前数据库未加密，且不得保存 token/secret。

安全策略：

- Provider token 使用 Windows Credential Manager。
- DB key 使用 DPAPI wrapping。
- 高敏字段字段级加密。
- SQLCipher 作为整库加密候选，开发前做打包验证。
- 同步包必须加密，不直接同步明文 SQLite。

## Consequences

- schema 必须 migration-first。
- 需要测试 SQLite FTS 在 10k/50k alias 下性能。
- 需要测试加密字段不进入 FTS。
- 需要提供导出/备份格式，不能把 SQLite 文件当唯一可迁移格式。

## Conflict Check

| 检查项 | 结果 |
| --- | --- |
| 与离线要求 | 符合 |
| 与同步要求 | 符合，但不能直接同步明文库 |
| 与安全要求 | 需 SQLCipher/字段加密/DPAPI 配合 |
| 与性能要求 | 符合，需 Benchmark 证明 |
| 与许可证 | SQLite public domain；SQLCipher 需二次审计 |
