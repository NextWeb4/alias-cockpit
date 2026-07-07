# Sync Design

日期：2026-07-05  
状态：本地优先同步设计草案。

## 同步目标

- 不建设中心云服务。
- 支持用户自选 Git/网盘/文件夹同步。
- 同步包必须加密。
- Provider sync 与 device sync 分离。
- 冲突必须可解释，不静默覆盖。

## 同步层次

| 层 | 说明 |
| --- | --- |
| Local DB | 当前设备 SQLite，加密或字段加密 |
| Event Log | 本地审计/变更事件 |
| Encrypted Bundle | 可放入 Git/网盘的同步包 |
| Device Merge | 多设备事件合并 |
| Provider Sync | 与 SimpleLogin/addy/Fastmail 等远端合并 |

## 同步包内容

```text
bundle_version
workspace_id
device_id
schema_version
created_at
base_event_id
events[]
snapshots optional
checksum
```

必须加密：

```text
Argon2id/PBKDF2 derived key or imported sync key
AES-256-GCM or XChaCha20-Poly1305
authenticated metadata
```

具体算法由技术栈和成熟库决定；不得自造加密。

## Event 类型

```text
alias.created
alias.updated
alias.disabled
alias.deleted
alias.restored
tag.created
tag.assigned
provider.linked
provider.synced
domain.updated
rule.updated
import.applied
export.created
```

每个 event：

```text
event_id
workspace_id
device_id
entity_type
entity_id
logical_clock
timestamp
operation
payload_ciphertext or redacted_payload
prev_hash
event_hash
```

## Merge 规则

| 冲突 | 自动处理 | 原因 |
| --- | --- | --- |
| 两设备给同 alias 添加不同标签 | 自动合并 | set union 安全 |
| 两设备改不同 notes | 冲突 | 可能覆盖用户意图 |
| 一边禁用一边编辑 purpose | 可合并，保留 disabled | 状态更保守 |
| 一边删除一边更新 | 冲突，默认保留 tombstone | 避免复活已删数据 |
| 远端缺失、本地活跃 | 标记 `remote_missing` | 不静默删除 |
| 远端禁用、本地活跃 | 冲突/安全提示 | 远端可能被用户禁用 |

## Tombstone

删除不是立即物理删除：

```text
tombstone
- entity_type
- entity_id
- deleted_at
- purge_after
- reason
```

优点：

- 防止离线设备重新同步旧数据导致复活。
- 支持回收站。
- 支持审计和 undo。

## Provider Sync

Provider Sync 不直接修改本地事实，而是产生 `provider_snapshot` 和 `sync_event`：

1. Pull remote snapshot。
2. Normalize to provider-neutral model。
3. Compare with local state。
4. Produce changeset。
5. Apply safe changes automatically。
6. Put ambiguous changes into conflict queue。

安全默认：

- 远端删除不自动删除本地。
- token 失效不影响本地搜索/导入导出。
- Provider security advisory 可暂停远端写操作。

## Git 同步

第一版建议只支持“加密文件夹同步”，Git 是用户可选承载层：

- App 写入 `workspace.bundle.enc` 或 append-only bundle files。
- 不自动执行 `git push`，除非用户明确启用。
- 若启用 Git，必须显示 remote URL 和隐私风险。
- 文件名不得包含邮箱、域名、站点名。

## 回滚

每个危险 OperationPlan 必须带 rollback：

- 本地 rollback：使用 `undo_payload_ciphertext` 恢复。
- Provider rollback：只有 Provider 支持时才声明可远端回滚。
- 不支持远端 rollback 时 UI 必须明确显示。

## 测试

- 两设备并发添加标签。
- 删除/恢复跨设备同步。
- 远端删除与本地更新冲突。
- token 失效时本地数据不变。
- 同步包解密失败不破坏本地库。
- Git folder 中只有加密包，无明文 alias。

