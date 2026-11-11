# 逐操作验收与记录框架规范 (Operation-Layered Acceptance Framework Specification)

版本：`operation-layered-v1`  
分支：`feat/operation-acceptance-recording`  
基准：`56825d659fb56f95ae266f4c94f50db7ca90fece`  

---

## 1. 核心目标与原则

本框架落实用户对“逐操作独立对照真实 Parasolid (PK) 验收”的明确要求，纠正以往将多个独立建模操作混杂在同一个用例退出码中、将多步耗时直接求和当作单步 API 比值的历史问题：
1. **同输入、同参数、同 API 独立对标**：每个实际建模操作（不仅 blend/Boolean，包含所有产生几何或拓扑变化的 API）独立与真实 Parasolid 在相同输入和参数下对照验证。
2. **前步通过不被后步失败倒扣**：先执行并通过的建模步骤，其真实验证结果不可因后续步骤的失败而被置为失败；组合用例保留每个步骤的独立结果与组合整体状态。
3. **主辅检查与内部表示清晰解耦**：
   - `primaryChecks`：核心几何/拓扑等价性检查（例如 `PK_DEBUG_BODY_compare`、`PK_BODY_check`、拓扑计数、物性等）。
   - `auxiliaryChecks`：兼容性分类、诊断探测等辅助项（如 510 探测，不取代 primary 也不被删除）。
   - `representationContracts`：内部几何表示与生存期合同（如内存泄漏检查、tag 有效性、端点参数等）。
4. **历史数据严格只读与字节不可变**：已归档的旧 run、日志、manifest 保持字节不变。旧 schema 缺少逐操作字段时统一解析为 `legacy-aggregate` / 未拆分，严禁从旧 case exit 反推捏造单步通过或比值。

---

## 2. 字段合同 (Schema Contract)

### 2.1 Scenario 级别字段
在用例（Scenario）层级，增加或规范以下字段：
- `operationSchemaVersion`: 字符串，如 `"operation-layered-v1"`。
- `scenarioId`: 唯一用例标识符，保持与用例 `id` 对应。
- `scenarioStatus`: `"pass" | "fail" | "missing" | "unsupported" | "timeout" | "diagnostic"`。
- `firstFailedOperationId`: 字符串或 `null`，记录首个失败的 operation ID。
- `legacyExitCode`: 原始进程退出码整数（如 `0`、`1`、`134`）。
- `scenarioElapsedMs`: 用例总执行耗时（毫秒）。
- `scenarioWallElapsedMs`: 用例真实挂钟耗时（毫秒）。
- `scenarioCapturedApiMs`: `{ "gm": double?, "pk": double? }`，所有捕获到 API 时间的求和对象。
- `operations`: 数组，按执行顺序记录每个操作的详细记录。

### 2.2 Operation 级别字段
每个操作对象包含：
- `operationId`: 稳定唯一标识符，如 `"op-1"`、`"op-construct"`。
- `ordinal`: 整数，执行次序（从 0 开始）。
- `name`: 操作描述名称。
- `role`: 场景内角色，`"primary" | "lifecycle" | "precondition" | "auxiliary"`。
- `api`: 真实被调用的 Parasolid 兼容 API 名称（如 `PK_BODY_fix_blends`、`PK_BODY_boolean`）。
- `options`: 实际传入的 options 结构化参数或 `null`。
- `parameterHash`: 参数 hash（SHA256 十六进制）。
- `inputProvenance`: 输入来源与工具谱系描述。
- `inputHashes`: `{ "gm": { ... }, "pk": { ... } }`，GM 与 PK 各自输入实体的 hash/tag。
- `preconditions`: 前置质量与条件检查数组。
- `outputHashes`: `{ "gm": { ... }, "pk": { ... } }`，输出实体的 hash/tag。
- `returns`: `{ "gm": int, "pk": int }`，双方底层 API 返回的错误码。
- `status`: 综合状态，`"pass" | "fail" | "not-run" | "unverified"`。
- `primaryStatus`: 核心检查状态，`"pass" | "fail" | "not-run" | "unverified"`。
- `auxiliaryStatus`: 辅助检查状态，`"pass" | "fail" | "none"`。
- `representationStatus`: 内部表示状态，`"pass" | "fail" | "none"`。
- `hasAuxiliaryFailure`: 布尔值，标识是否存在辅助项失败。
- `primaryChecks`: 归属于本操作的核心检查列表。
- `auxiliaryChecks`: 归属于本操作的辅助检查列表。
- `representationContracts`: 归属于本操作的内部表示检查列表。
- `timing`: API-only 计时边界对象：
  - `boundary`: `"api-only"`。
  - `measurementKind`: `"api-elapsed-wall"`。
  - `gmMs`: GM API 耗时毫秒数。
  - `pkMs`: PK API 耗时毫秒数。
  - `coldKind`: `"fresh-process-first-api" | "subsequent-in-process" | "unknown"`。
  - `warmup`: 预热次数。
  - `samples`: 样本数。
  - `correctnessQualified`: 布尔值，当且仅当本操作独立为 primary pass 且双方 API 均返回 0 时为 true。
  - `ratio`: qualified 性能倍率（`gmMs / pkMs`），未合格时为 `null`。
  - `diagnosticRatio`: 诊断用原始倍率（可选）。

---

## 3. 真实可追溯案例对比

### 3.1 案例一：单操作用例 (Single-Operation)
用例：`short-edge.a1-r0.5-t1e-07-reversefalse-default`
- **Scenario 表现**：
  - `scenarioStatus`: `"pass"`
  - `performance.kind`: `"single-operation"`
  - `performance.ratioGmOverPk`: `10840.43`
  - `correctnessQualified`: `true`
- **Operation 表现**：
  - 拥有单个操作 `op-1`，API 为 `PK_BODY_fix_blends`，`role: "primary"`，`status: "pass"`。
  - `coldKind`: `"fresh-process-first-api"`。
  - 核心检查包含 `Result body count`、`BODY_check`、严格对比等全部通过。

### 3.2 案例二：多步骤组合用例 (Scenario-Aggregate)
用例：`solid-blend.plane-nurbs-modified-support-bore`（修复了历史误标为单一 2187.57x blend 的问题）
- **Scenario 表现**：
  - `scenarioStatus`: `"pass"`
  - `performance.kind`: `"scenario-aggregate"`
  - `performance.ratioGmOverPk`: `null`（**严格扣留组合倍率，杜绝将求和耗时误导为单步性能**）
  - `scenarioCapturedApiMs`: `{ "gm": 6243.03, "pk": 3.21 }`
- **Operations 拆分**：
  1. `op-1` (`PK_BODY_fix_blends`):
     - `role`: `"primary"`
     - `status`: `"pass"`
     - `gmMs`: `1785.20ms`, `pkMs`: `1.93ms`
     - `coldKind`: `"fresh-process-first-api"`
     - `ratio`: `925.55x` (`correctnessQualified: true`)
  2. `op-2` (`PK_BODY_boolean`):
     - `role`: `"primary"`
     - `status`: `"pass"`
     - `gmMs`: `4457.84ms`, `pkMs`: `1.28ms`
     - `coldKind`: `"subsequent-in-process"`
     - `ratio`: `3483.50x` (`correctnessQualified: true`)

---

## 4. 确认的 7 处归属规则修复映射 (Rules A1–A7)

| 规则编号 | 涉及文件 | 原问题 | 修复实现与验证证据 |
|:---|:---|:---|:---|
| **A1** | `scripts/WebProgressCapture.cs`, `scripts/RefreshWebProgress.cs` | 用例状态与 checks 无操作 owner，组合结果被误读为全部 API 状态 | 采集器和刷新器实现显式 `owner` 与 `category` 归属，引入 `operations` 数组，未到达步骤明确标记 `not-run`。 |
| **A2** | `scripts/RefreshWebProgress.cs` | 多步用例性能将多个 API 耗时相加作为单一 ratio（如 modified-support-bore 2187.57x） | 多步用例在 scenario 级别将 `kind` 设为 `"scenario-aggregate"`，`ratioGmOverPk` 强制设为 `null`；各步骤独立保留各自的合格 ratio 与 coldKind。 |
| **A3** | `scripts/ShortEdgeBlendOracle.cs` | fillet 产物、固定分类探测与后续 Boolean 共享同一个 exit | 显式解耦 primary blend 操作与分类 probe / 后续 lifecycle，各步骤 checks 独立归属，前步成功不被后步倒扣。 |
| **A4** | `scripts/FreeformModelingOracle.cs` | 自由曲面构造与随后的 XT 导入、copy、delete、transform lifecycle 共享退出码 | 引入 `OperationAcceptanceLedger`，将 `parallel-nurbs-path-solid` 与 `three-profile-natural-loft` 的构造 primary 与后续 self/PK lifecycle 独立记录并输出 sidecar `ledger.json`。 |
| **A5** | `scripts/LocalTopologyChangeOracle.cs` | `native-import-*` 导入局部编辑用例将编辑与后续 lifecycle 合并 | 独立记录局部编辑 primary 操作与导入后的 copy/transform/delete lifecycle，分别验收并在 `ledger.json` 中独立呈现。 |
| **A6** | `scripts/GeometryTransformOracle.cs` | body 分支的变换、源体删除、依赖几何采样与 XT 往返共用退出码 | 将实体变换 primary 操作与源体生存期、删除后副本可用性 lifecycle 独立记录，未做 PK 对照的单体几何变换明确标记为 `unverified`。 |
| **A7** | `scripts/ParasolidEvaluationOracle.cs` | 10 个基础体构造与求值已通过，但因后续 offset 依赖删除 5002 导致全盘报错退出 | 在捕获异常与进程真实退出码的同时，在 `finally` 阶段保留并持久化前面 10 个已通过的 primary 操作状态与 checks，offset 失败独立记为 fail，后续未执行步骤记为 unverified/not-run。 |

---

## 5. 候选规则状态声明 (Candidate Rules A8 & A9)

依据任务指示与审计准则，候选规则保持其原始观察来源，**不趁机修改或盲目修改**：
- **A8 (Candidate)**: 针对 chain、support、trivalent、mixed-curved 等后续 Boolean 的不同输入谱系，保留其原始观察，待隔离 same-input 专门 oracle 建立后再行判定。
- **A9 (Candidate)**: 针对 local operation 与 intersection operation 的比较掩码及规范化，保持原有严格掩码不变，不擅自放宽几何容差。
