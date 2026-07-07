# Alias Generation Design

日期：2026-07-05  
状态：算法设计草案。

## 生成目标

Alias 生成不是“随机字符串生成器”。它要在三类目标之间平衡：

- 隔离性：不同网站/项目不复用 alias。
- 不可预测性：攻击者不能枚举或猜测。
- 可用性：用户能理解、搜索、口头沟通或识别用途。

## 策略

| 策略 | 推荐场景 | 示例 | 风险 |
| --- | --- | --- | --- |
| Strong Random | 默认、隐私最高 | `x7q9v2k4@example.com` | 不易读 |
| Readable Random | 电话/人工沟通 | `river-lamp-872@example.com` | 词表过小会可枚举 |
| Site-aware | 注册网站 | `gh-7q2m@example.com` | 可能泄露站点用途 |
| Rule Template | 团队/项目规范 | `{{project}}-{{rand4}}@example.com` | 低熵模板危险 |
| Provider-native | 让 Provider 生成 | SimpleLogin/Fastmail native | 可迁移性弱 |
| Compatibility Fallback | 网站阻止 alias | 换域/换格式 | 需要记录站点经验 |

## 熵预算

最低建议：

| 用途 | 最低熵 |
| --- | ---: |
| 普通网站注册 | 40 bits |
| 金融/重要身份 | 60 bits |
| Catch-all 可猜测前缀 | 64 bits |
| 可读词组模式 | 50 bits |

计算方式：

```text
entropy_bits = log2(candidate_space)
```

示例：

- 8 位 base32：`32^8 = 2^40`
- 10 位 base32：`2^50`
- 3 个 2048 词表单词：`2048^3 = 2^33`，不足，需要数字/更多词。

## 随机源

必须使用 OS CSPRNG：

- .NET: `RandomNumberGenerator`
- Rust: `rand_core::OsRng`

禁止：

- `System.Random`
- 时间戳加随机
- 自增 ID
- 低位 UUID 截断且不评估熵

## 生成流程

```text
GenerationRequest
- workspace_id
- provider_account_id optional
- domain_id
- site optional
- purpose optional
- tags[]
- strategy
- privacy_level
- min_entropy_bits
- compatibility_profile optional
```

流程：

1. 标准化站点输入：URL -> registrable domain -> display label。
2. 读取 domain/provider capability。
3. 生成 N 个候选。
4. 过滤易混字符、保留字、已有冲突、Provider 不允许字符。
5. 计算 entropy/risk。
6. 标注是否暴露站点用途。
7. 给出候选列表，不直接远端创建。

## 站点兼容性

本地维护 `site_compatibility`：

```text
site_domain
blocked_patterns
accepted_patterns
last_failure_reason
provider_domain_reputation
notes
```

用途：

- 某网站阻止 `+tag`，自动不推荐 plus-addressing。
- 某网站阻止已知 relay 域，推荐 custom domain。
- 某网站不允许短 local-part，调整长度。

## 域名自动选择

评分：

```text
score = privacy_score + compatibility_score + deliverability_score + ownership_score - exposure_penalty
```

考虑因素：

- Provider shared domain 是否常被封。
- 用户 custom domain 是否过度暴露身份。
- Catch-all 是否启用。
- 该站点历史是否接受该域。
- 重要站点优先强隔离，不复用 catch-all 可读前缀。

## AI-assisted 设计

AI 只用于生成“候选语义标签/用途短名”，不是安全随机源。

默认关闭。启用时必须显示：

- 将发送哪些上下文。
- 是否包含真实网站/备注/标签。
- 是否使用本地模型或外部 API。
- 生成结果仍需本地 CSPRNG 后缀。

禁止：

- 把 API token、真实收件邮箱、完整 alias 清单发给模型。
- 用 AI 输出替代熵生成。

## 回归测试

- 同一 request 生成大量 alias 不应冲突超过预期。
- 每种策略最低熵达标。
- Provider 字符限制被正确应用。
- 站点兼容性 fallback 生效。
- `System.Random`/非 CSPRNG 不出现在生成路径。

