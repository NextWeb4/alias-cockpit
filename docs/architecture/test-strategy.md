# Test / Benchmark / Review Strategy

日期：2026-07-05  
状态：开发前测试计划。

## 测试金字塔

| 层级 | 覆盖 | 工具候选 |
| --- | --- | --- |
| Unit | 生成算法、模板、熵、领域规则、冲突合并、导入映射 | xUnit/NUnit 或 Rust test，取决于最终技术栈 |
| Integration | SQLite migration、Credential Manager/DPAPI、Provider mock server、加密导出 | Testcontainers/mock HTTP/临时数据库 |
| UI Automation | 搜索、筛选、批量预演、导入 dry-run、设置、错误状态 | WinUI 优先 FlaUI/WinAppDriver；fallback 视技术栈确定 |
| Security tests | 日志脱敏、secret 不落库、恶意导入、路径穿越、CSV injection | Snapshot + property tests + malicious fixtures |
| Stress | 10k/50k alias，批量操作，导入导出 | BenchmarkDotNet 或 Criterion |
| Review gates | lint/format、license audit、dependency audit、architecture boundary check | 开发后补具体命令 |

## 必测不变量

- `Math.Random` 或非 CSPRNG 不得出现在 alias 生成路径。
- Token 不得存在 SQLite 明文字段、日志、导出文件、测试快照。
- 导入 dry-run 不应改变数据库。
- Provider 同步失败不应丢本地改动。
- 删除产生 tombstone；恢复必须保留审计历史。
- 批量操作预演与实际执行影响数量一致。
- 批量 Provider disable/delete 必须先生成 `ProviderBatchOperationPlan`；delete 必须 `RequiresExplicitConfirmation=true`。
- 搜索结果必须受 workspace 隔离。
- 导出后再导入必须 round-trip 保留 alias、标签、状态、Provider 引用和审计必要字段。

## Fixture 计划

| Fixture | 用途 |
| --- | --- |
| `simplelogin-export-small` | SimpleLogin 导入映射 |
| `addy-export-small` | addy.io 规则/recipient 导入 |
| `generic-csv-dangerous` | CSV formula injection、超长字段、重复 alias |
| `provider-conflict` | 远端删除/本地修改冲突 |
| `large-10k` | 表格/搜索/导入压力 |
| `large-50k` | Benchmark 上限 |
| `secret-redaction` | 日志脱敏快照 |

Fixture 不得包含真实邮箱、真实 token 或用户个人数据。

## Benchmark 指标

| Benchmark | 指标 |
| --- | --- |
| Generate aliases | 每秒生成量、冲突率、熵分布 |
| Search | FTS 查询 P50/P95、UI debounce 后响应 |
| Import dry-run | 行/秒、错误收集内存 |
| Bulk preview | 影响计算耗时 |
| Sync merge | event 数量、冲突数量、耗时 |
| Startup | 首屏可交互时间 |

## 自动 Review 标准

- 新依赖必须有许可证、维护状态、替代方案、回滚说明。
- Provider adapter 必须有能力矩阵和 mock 测试。
- Provider 危险操作必须有 dry-run planner 测试和 fake HTTP 测试；delete 不得绕过确认模型。
- UI PR 必须证明键盘可达与大列表性能。
- 安全相关 PR 必须更新 threat model 或说明无需更新。
- 导入导出 PR 必须提供恶意/失败 fixture。
- 同步 PR 必须提供冲突测试和不丢数据证明。

## 当前状态

- 已落地测试命令：`.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal`。
- 已新增 App/ViewModel 测试项目：`tests/AliasCockpit.App.Tests`，当前覆盖有标记/未标记筛选。
- 已落地 format 命令：`.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal`。
- 已落地基础 UI smoke：`scripts\verify-ui-smoke.ps1` 会启动发布后的 WinUI exe，通过桌面输入 Gmail 地址、标签和数量，再点击 Copy all 并断言剪贴板包含预期别名。
- 已落地完整发布验证：`scripts\verify-release.ps1` 串联 build/test/benchmark/format/publish/zip/MSI/setup EXE/process smoke/basic UI smoke。
- 已覆盖：生成算法、CSV dry-run、alias color CSV round-trip、SQLite alias/saved email/provider account 仓储、Credential Manager、Provider mock adapter、SimpleLogin/addy.io HTTP fake handler、Provider batch dry-run planner、Provider batch executor confirmation gate。
- 未落地：完整 UI 自动化、真实账号端到端测试、同步冲突测试、数据库加密测试。
