# Parasolid part x_t 全版本支持实施状态

本文记录 `docs/xt_all_versions_support_plan.md` 的可执行验收状态。机器结果以
以下文件为准：

- `tests/ParasolidXtCorpus/coverage/xt-schema-support-report.json`
- `tests/ParasolidXtCorpus/coverage/xt-schema-corpus-matrix-report.json`
- `tests/ParasolidXtCorpus/coverage/xt-schema-applicability.json`
- `tests/ParasolidXtCorpus/coverage/xt-schema-differences.json`

## 已完成

- 101 个 bundled schema 已进入严格 registry；生成器检查 header、terminator、
  统计数量、重复节点、字段类型、pointer class、固定数组和 variable 声明。
- 100 个按 modeller version 排序的相邻 schema 差异已逐节点/字段生成机器清单，
  摘要见 `docs/xt_schema_differences.md`；新增或修改 schema 会使统一 check 失败。
- 普通及 embedded schema、完整 `**PART1/2/3` 文件头、全部物理字段码、
  presence、固定/变长数组、原始字符数组、历史裸符号零、前向及共享引用、
  user fields、mesh/lattice 节点和现代/旧式 multi-part 容器已进入通用 codec。
- receive 后同时保留无损版本扩展图，并建立 part、body、topology、geometry、
  assembly/instance、attribute、mesh/lattice 的连续语义索引。
- 版本降级先执行可表示性检查。固定数组只在被截断尾部均为默认值时收缩；
  版本专属派生缓存和等价默认状态采用显式 adapter 规则。无法证明无损时返回
  `PK_ERROR_wrong_version`，不再静默补零或删除非默认字段。
- `transmit_version=0` 会将历史输入规范化到当前 embedded schema；显式历史
  transmit version 选择对应历史 schema。V1–V3 不再因 schema number 小于
  4000 而被 registry 排除，`10/20/30/40/50/60` 到后续版本均可由同一
  `PK_PART_transmit_b` 路径选择。V7–V38 使用真实 V38 探针固定的兼容表，
  不再错误假设 `transmit_version == schemaNumber / 100`（例如 101→10004、
  91→9008、210–241→20000）；producer maintenance version 若没有独立 public
  transmit token，则在同一 major 内选择最近的可用版本（如 701081→70、
  901101→91）。多 part receive 后可按子集和顺序重建 part block。
- `PK_PART_receive_b` 已按真实 V38 合同实现零基且严格递增的 `part_indices`、
  `part_index`/普通 transmit `identifiers` 错误语义，以及 compound 的默认
  split、keep 和 fail(1096)；compound 无损 codec 回归显式使用 keep。receive
  options 的结构版本、attdef/seek/mixed enum、`key_is_partition` 和
  `make_facet` 错误合同，以及 transmit options 的 1–4 结构版本、format、
  transmit version、indexed context 和 V4 mesh enum 合同，均由真实 V38
  探针固定并进入单元测试。
- Parasolid 38.0.150 已对 77 个 schema 完成最小 solid block 双向 smoke；其中
  64 个 V6+ schema 已进一步通过完整语料 receive 和
  `PK_DEBUG_BODY_compare`。矩阵中的 managed 路径按各 schema 自身的
  `transmit_version` 重发，不再用版本 0 升级到当前 schema。328 个 API 合法模型形成 20,391 个适用的
  case/schema 组合，全部通过；601 个不适用组合均有字段级或版本级原因，
  无 source-unavailable 和实现失败。
- 当前版本完整语料 check、单元测试、schema codegen check、NativeAOT
  publish、ABI smoke 和现有 Parasolid oracle 均进入 `scripts/VerifyKernel.cs`。

## 尚未达到 Complete

按照计划中的声明规则，目前不能把 101 个 schema 全部标记为 `Complete`：

1. 24 个 `SCH_5030` 及更早 schema 缺少对应历史 runtime。当前 38.0.150
   对字段定义完全相同的 `SCH_5030` 返回 `PK_ERROR_corrupt_file`，却接受
   `SCH_5031`；两者的 descriptor hash 已写入支持报告，形成明确 runtime
   下限证据。当前 runtime 同样拒绝以前 schema 作为 embedded base。
2. 13 个 V5.031–V5.059 schema 已通过 solid block 的旧 BODY/SHELL 布局和
   region 双向 adapter，但 sheet、wire、minimum 及部分历史几何仍未通过
   全语料矩阵。缺少对应 V5 runtime 或真实 V5 API 生成种子时，不能把当前
   V38 的 922 结果解释为合法版本限制，也不能手写节点冒充 oracle。
3. `tests/ParasolidXtCorpus/coverage/type-coverage-report.json` 仍报告 5 个 missing
   和 17 个 deferred typed coverage 项。即使 API 分类门禁为
   `strictGapCount=0`，也不能据此宣称目标 2 的 typed matrix 已完成。

因此当前可声明的最高状态是：全部 101 个 schema 达到 `SemanticMapped`；其中
64 个达到完整 `CorpusVerified`，另有 13 个达到 solid-block
`ParasolidVerified`，其余 24 个保持 `AdapterAndRuntimeBlocked`。`Complete`
必须等待 V5 全语料 adapter、对应更早 runtime/合法历史种子以及目标 2 typed
coverage 门禁归零后再更新。

## 复现

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateXtSchema.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateXtSchemaDifferences.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParasolidAllSchemaOracle.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidXtCorpus.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParasolidSchemaCorpusMatrix.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParasolidSchemaCorpusMatrix.cs -- --include-smoke-verified
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyKernel.cs
```
