# Parasolid body compare 验收修复验证

日期：2026-11-07；基准提交：`616b8cd`。仅修改 oracle、共享比较辅助代码和使用规则，无 C ABI 或内核算法改动。

## 修复内容

- 9 处未启用几何偏差检查的调用统一使用 `scripts/ParasolidBodyCompare.cs`。
- 统一开启局部容差几何和匹配检查，保留类型、sense、容差、missing、region/material/shell 语义验收；仅 chart-point 数量差异默认作为诊断。
- wire 额外逐边匹配曲线类型、sense、精度和端点，并对有限边做双向 65 点距离检查。无顶点的闭合边允许不同参数起点。有限采样不构成任意曲线的完整等价证明。
- 删除 `n_face_pairs == 总面数` 的错误断言。
- XT writer 两边均执行 detach/attach，相同操作构建独立基准，取消整项降级，并增加独立基准曲面的逆参数化/位置检查。

## 验证结果

所有命令从项目根目录运行，使用 `MSBUILDDISABLENODEREUSE=1 dotnet run scripts/<name>.cs`。未运行 NativeAOT 发布。
Primitive/Smoke 在启动真实 PK host 前启动托管会话，运行时额外设置 `P_SCHEMA=bin/parasolid-schema-cache`。

| 检查 | 结果 |
| --- | --- |
| `ParasolidBodyCompareOracle` | 6 个正例、5 个负例全部通过 |
| `ParasolidPrimitiveOracle` | cone/prism/sphere/torus 全部通过 |
| `ParasolidOracleSmoke` | 双向 XT smoke 通过 |
| `BCurveEvaluationOracle` | 数值及 polynomial/rational body roundtrip 通过 |
| `XtGeometryWriterOracle` | 全部 fixtures 通过，不再忽略 body compare 失败 |
| `BCurveAnalyticIntersectionOracle` | 31 个独立正/负合同用例通过；另保留 1 个原有 native wire-fault 边界诊断 |
| `CurvedSheetPartitionOracle polynomial-bilinear-imprint` | 通过，只有允许的 chart-point 诊断 |
| `ParasolidAllSchemaOracle --schema SCH_3701097_37102 --check` | 1 个 schema，direct/managed 比较通过 |
| `ParasolidSchemaCorpusMatrix --case body.solid.block.typical --schema SCH_3701097_37102 --check` | 原始、generated-model、managed 比较通过 |
| `ParasolidXtFixtureOracle`，输入 `bin/parasolid-xt-corpus/BodyBasics/body.solid.block.typical/model.x_t` | 通过 |
| `ParasolidXtCorpusCases/BodyBasics` | 全组通过，覆盖 corpus host 共享调用路径 |
| `ParasolidEvaluationOracle` | 10 个 primitive/rotated XT 比较通过，随后在既有 offset base 删除代码失败（5002）；未修改版本同样失败 |
| `IntersectionOperationOracle` | 65 个通过、22 个因新增线体类型/sense 验收失败；未修改版本把全部 87 个计为通过 |

新回归的正例：相同实体、相同线体、无顶点闭合线体的参数起点变化、相同片体、升阶、插结。
负例：实体高度错误、线体平移、裁剪范围错误、端点相同但曲线内部改变、曲面内部控制点移动。
各托管 body 均经我们的 text XT 导出、保存，再由真实 PK 加载后与独立基准比较。

## 新检查揭示的兼容性差异

以下 22 个交线用例现在失败，而非通过降级放行。它们的 curve class 或 edge sense 不一致；这是现有建模/导出行为的差异，本次没有修改对应内核算法。

类型差异包括 PK intersection curve（3004）与 reimport B-spline（3005）或 line（3001），以及 PK circle（3002）与 reimport intersection curve（3004）。

```text
bilinear-nurbs-hyperbola
bilinear-nurbs-crossing
cubic-ruled-two-components
rational-cubic-ruled-two-components
rational-multiple-spans-four-components
transposed-ruled-two-components
transposed-rational-ruled-two-components
tensor-polynomial-three-isocurves
tensor-rational-three-isocurves
tensor-rational-homogeneous-scale-1e-4
tensor-rational-homogeneous-scale-1e4
tensor-transposed-three-isocurves
tensor-rational-transposed-isocurves
coaxial-sphere-torus-tangent-circle
tensor-quadratic-crossing
general-nurbs-sphere
general-nurbs-sphere-equator
general-nurbs-torus
blend-positive-planes
blend-negative-planes
blend-plane-sphere
blend-sphere-plane
```

本地完整输出及未修改版本的对照输出位于 `temp_docs/body-compare-fix/`；回归导出的 XT 位于 `temp_docs/body-compare-regression/`。
