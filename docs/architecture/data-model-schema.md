# Data Model And SQLite Schema Draft

日期：2026-07-05  
状态：schema 草案；开发时必须用 migration 管理。

## 设计原则

- Local-first：本地数据库保存用户可迁移的事实，不把 Provider 当唯一事实源。
- Provider-aware：保留远端引用和能力差异，不把不同服务硬塞进同一字段。
- Event-backed：危险操作、同步、导入导出必须有审计事件。
- Encryptable：schema 不依赖明文 secret；token 通过 `secret_ref` 指向系统凭据。
- Importable：每个实体支持 source/import batch，便于 dry-run、回滚、追踪。

## 表分组

| 分组 | 表 |
| --- | --- |
| Workspace | `workspaces`, `views`, `settings` |
| Provider | `provider_accounts`, `provider_capabilities`, `remote_objects` |
| Alias | `aliases`, `domains`, `recipients`, `contacts`, `rules` |
| Organization | `tags`, `alias_tags`, `projects`, `favorites` |
| Activity | `usage_events`, `risk_findings`, `audit_events` |
| Sync | `sync_devices`, `sync_events`, `sync_conflicts`, `tombstones` |
| Import/Export | `import_batches`, `import_rows`, `export_jobs` |

## SQLite DDL 草案

当前实现先落地最小可运行 schema：`aliases`（含 `site` / `purpose` / `color` 标记）+ `alias_search` FTS5 + `saved_email_addresses` + `provider_accounts` + `audit_events` + `tombstones`。`provider_accounts` 只保存账户元数据和 `secret_ref`，不保存明文 secret；`saved_email_addresses` 和 alias 标记是未加密用户元数据，不得存放 token、密码或恢复码；`audit_events.redacted_summary_json` 必须在写入前脱敏；`tombstones` 用于防止删除对象在后续同步中被离线设备复活。下面完整草案仍作为后续 migration 目标。

```sql
CREATE TABLE workspaces (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE provider_accounts (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  provider_type TEXT NOT NULL,
  display_name TEXT NOT NULL,
  secret_ref TEXT,
  auth_state TEXT NOT NULL,
  security_state TEXT NOT NULL,
  last_sync_at TEXT,
  capability_hash TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE domains (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  provider_account_id TEXT REFERENCES provider_accounts(id),
  domain TEXT NOT NULL,
  domain_type TEXT NOT NULL,
  catch_all_enabled INTEGER NOT NULL DEFAULT 0,
  dns_status TEXT NOT NULL,
  risk_level TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  UNIQUE(workspace_id, domain)
);

CREATE TABLE recipients (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  provider_account_id TEXT REFERENCES provider_accounts(id),
  label TEXT NOT NULL,
  email_hash TEXT NOT NULL,
  email_ciphertext BLOB,
  remote_ref TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE aliases (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  provider_account_id TEXT REFERENCES provider_accounts(id),
  domain_id TEXT REFERENCES domains(id),
  primary_recipient_id TEXT REFERENCES recipients(id),
  address TEXT NOT NULL,
  address_hash TEXT NOT NULL,
  local_part TEXT NOT NULL,
  status TEXT NOT NULL,
  purpose TEXT,
  site TEXT,
  notes_ciphertext BLOB,
  generation_strategy TEXT,
  entropy_bits REAL,
  provider_remote_id TEXT,
  provider_remote_version TEXT,
  source TEXT NOT NULL,
  expires_at TEXT,
  last_used_at TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  UNIQUE(workspace_id, address_hash)
);

CREATE TABLE tags (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  parent_id TEXT REFERENCES tags(id),
  name TEXT NOT NULL,
  color TEXT NOT NULL,
  sort_order INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  UNIQUE(workspace_id, parent_id, name)
);

CREATE TABLE alias_tags (
  alias_id TEXT NOT NULL REFERENCES aliases(id),
  tag_id TEXT NOT NULL REFERENCES tags(id),
  PRIMARY KEY(alias_id, tag_id)
);

CREATE TABLE rules (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  alias_id TEXT REFERENCES aliases(id),
  domain_id TEXT REFERENCES domains(id),
  provider_account_id TEXT REFERENCES provider_accounts(id),
  rule_kind TEXT NOT NULL,
  condition_json TEXT NOT NULL,
  action_json TEXT NOT NULL,
  provider_remote_id TEXT,
  enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE usage_events (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  alias_id TEXT NOT NULL REFERENCES aliases(id),
  event_type TEXT NOT NULL,
  source TEXT NOT NULL,
  count INTEGER NOT NULL DEFAULT 1,
  first_seen_at TEXT NOT NULL,
  last_seen_at TEXT NOT NULL,
  metadata_json TEXT
);

CREATE TABLE risk_findings (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  alias_id TEXT REFERENCES aliases(id),
  provider_account_id TEXT REFERENCES provider_accounts(id),
  finding_type TEXT NOT NULL,
  severity TEXT NOT NULL,
  title TEXT NOT NULL,
  evidence_json TEXT NOT NULL,
  status TEXT NOT NULL,
  created_at TEXT NOT NULL,
  resolved_at TEXT
);

CREATE TABLE audit_events (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  device_id TEXT NOT NULL,
  operation_id TEXT NOT NULL,
  entity_type TEXT NOT NULL,
  entity_id TEXT NOT NULL,
  operation TEXT NOT NULL,
  before_hash TEXT,
  after_hash TEXT,
  redacted_summary_json TEXT NOT NULL,
  undo_payload_ciphertext BLOB,
  created_at TEXT NOT NULL
);

CREATE TABLE tombstones (
  id TEXT PRIMARY KEY,
  workspace_id TEXT NOT NULL REFERENCES workspaces(id),
  entity_type TEXT NOT NULL,
  entity_id TEXT NOT NULL,
  deleted_at TEXT NOT NULL,
  purge_after TEXT,
  reason TEXT NOT NULL
);
```

## 搜索索引

建议使用 SQLite FTS5：

```sql
CREATE VIRTUAL TABLE alias_search USING fts5(
  address,
  local_part,
  site,
  purpose,
  tag_names,
  provider_name,
  content=''
);
```

索引内容必须脱敏控制：

- 允许索引 alias 地址、用途、站点、标签。
- 真实收件邮箱默认不进入全文索引。
- notes 若加密，不进入 FTS；可选用户明确允许本地明文索引。

## 状态枚举

Alias status：

```text
active
disabled
expired
deleted
remote_missing
conflict
pending_create
pending_update
pending_delete
```

Domain type：

```text
provider_shared
custom_domain
catch_all_domain
manual_record
```

Source：

```text
local_generated
provider_sync
import
manual
browser_extension
api
```

## 加密字段

| 字段 | 原因 |
| --- | --- |
| `recipients.email_ciphertext` | 真实收件邮箱敏感 |
| `aliases.notes_ciphertext` | 备注可能包含账号、用途、身份信息 |
| `audit_events.undo_payload_ciphertext` | undo 可能包含敏感 before/after |
| 同步包整体 | Git/网盘泄露风险 |

`address` 暂时明文保存，因为产品核心需要展示和复制 alias。若用户启用高隐私模式，可提供“锁定后隐藏地址”的 UI 状态，但运行时仍需解密。

## Migration 要求

- 所有 schema 变更必须通过 migration 版本。
- Migration 必须可在空库、旧库、损坏中断恢复场景测试。
- 升级前创建加密备份或 sqlite backup。
- 不允许破坏已有导入导出格式，除非提供迁移器。
