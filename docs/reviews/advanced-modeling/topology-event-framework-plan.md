# Topology event framework implementation

> 2026-11-11 当前执行规则修订：以 [逐操作审计与修订路线](operation-acceptance-strategy-audit-610e3ce.md) 和 [验收账本](operation-acceptance-ledger-610e3ce.md) 的 S0–S5 顺序及操作分层为准。下面 M0–M5 保留为历史需求，不能据历史完成措辞推断当前全通过；已测单步 primary、分类/内部合同和后续组合分别报告，后步失败不倒扣先前独立通过的操作。self96、原阈值、完整结构/有限域与组合要求继续保留，import-only 不替代 self；R1/R2 接受也不等于代码已合 main。

## Baseline

- Initial HEAD: `598fac8e079793150bf80f3c8a3dd86c312326b3`, branch `main`, upstream `origin/main`, clean worktree.
- PKToy: `d820719b6d8510d623972ab9d8efc65edbc312f9`.
- Installed .NET SDK: `10.0.302`, runtime `10.0.10`; no dependency upgrade.
- Existing independent PK library and schema are present in `third_party/parasolid/`.
- No public C ABI change is planned; validation uses managed compilation, tests and independent PK, without NativeAOT publication.

## Execution and acceptance

1. M0: reproduce focused tests, plane-nurbs solid fillet and subsequent Boolean, and the default short-edge failure. Preserve commands, exit codes and logs in `temp_docs/topology-event-framework/`.
2. M1: feed finite directed boundaries and endpoint associations into the existing SheetArrangement halfedge/cycle machinery from both regular filleting and Boolean. Guard the legacy full topology assembler at its actual entrance. Require fresh XT, independent comparison and rollback tests with the guard enabled.
3. M2: retain all affected support loops and certify separate equivalent support records. Test actual affected holes, equivalent and near-equivalent records, identities and rollback.
4. M3: build the selected rolling network and solve finite interactions; retain provider applicability conditions and reject unresolved events atomically.
5. M4: self-construct the heterogeneous short-edge fillet, then cut, copy/delete source, reload and model. Verify independent PK, geometry and preconstruction transforms and parameter variants.
6. M5: regressions, immutable 510-probe replay, capability updates, commit and push verified increments.

After an explicit goal resume, follow the recovery order, research rules and issue table in `docs/reviews/advanced-modeling/topology-event-framework-execution.md`. Use its **当前恢复检查点** for the exact full regression command and first remaining probe hypothesis; the earlier Shutdown checkpoint records the historical baseline. The original M0–M5 gates remain in force.

The current results and remaining gates are maintained in `docs/reviews/advanced-modeling/topology-event-framework-acceptance.md`. The analytic rolling equations, finite station proofs and persistent chart handling are in `docs/Finite_Analytic_Rolling_Network.md`.
For the latest pause and model handoff, start with [求交与圆角拓扑交接](topology-event-framework-handoff.md).

## Investigated baseline call chain

`FixLocalBlendsCore` tries axial, planar, trivalent and regular providers. `BuildRegularLocalBlend` sends multiple edges to `BuildRegularBlendChain`; single regular edges produce contact and cap curves before `AssembleRegularLocalBlend` decides a fixed four-edge boundary. That assembler also rejects multiple loops.

`RegularCurvedBoolean` reads finite boundaries, prepares and clips support intersections, splits source edges, calls `ArrangeSheetFace`, classifies cells and publishes through the existing B-rep builder. `ArrangeSheetFace` supplies the reusable halfedge successor, cycle and hole machinery. `BuildModelingBrep` validates explicit incidence and builds storage, but does not choose the boundary connections.

The initial migration will keep geometric formulas and trim solving, replace final template loop construction with finite directed segments and common cycle extraction, and preserve known parameter associations rather than reconstruct them from XYZ. This first provider remains subject to its existing regular endpoint hypotheses; it does not establish heterogeneous network support.
