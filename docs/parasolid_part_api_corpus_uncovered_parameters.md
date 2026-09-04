# Parasolid 类型参数与拓扑缺口（当前实测）

权威清单是 `tests/ParasolidXtCorpus/coverage/type-matrix.json`，当前报告是
`tests/ParasolidXtCorpus/coverage/type-coverage-report.json`。报告来自最近一次全量
聚合生成，而不是 API 数量推断。

## 当前门禁状态

- 30 个 case-group，330 个成功 manifest；全部通过真实 Parasolid 创建、XT transmit/receive
  和组内 `--check`。
- required semantic cells：250；成功覆盖 229；精确拒绝 9；归一化 12。
- `missingLabels` 已清零；`needsRecipe` 已清零。原 5 个缺口的关闭方式：
  - `geometry.blend.pair.sphere-spun`：sphere r=3 + spun line(x=2.2) + 偏移 spine
    (2.0,0.2,-2) 的稳定 seed（`blend.fxf.sphere-spun.rolling-ball`）。
  - `geometry.blend.depth.2`：先做 plane/plane blend，再把其结果 face 与第三个垂直
    plane 嵌套 blend（`blend.fxf.plane-plane-plane.nested.2`，TORUS + SP_CURVE->B_CURVE
    依赖链）。
  - `geometry.icurve.depth.2`：spun(B-curve profile) × warped B-surface 的
    `PK_FACE_intersect_face`（`icurve.spun-bcurve-bsurf.depth2`，INTERSECTION->SPUN_SURF->B_CURVE）。
  - `topology.loop.hole` / `topology.loop.peripheral`：探查
    （`scripts/ProbeXtGapRecipes.cs`）确认 v38 对所有 hole/peripheral 构造
    （boolean 通孔、imprint 内环、周期柱面）只产生 `PK_LOOP_type_inner_c`/`outer_c`/
    `winding_c`，旧 token 5401/5402 永不出现，按运行时归一化记录（与
    `bsurf.sf.form.*` 同一先例）。
- `deferredLabels` 仍有 17 个，表示计划层面的组合矩阵或 callback/运行时能力尚未完全
  关闭；typed strict gap 为 0。

## 已补齐的具体范围

- `PK_BCURVE_sf_t` / `PK_BSURF_sf_t`：标准 form、rational、knot type、periodic/closed、
  self-intersecting、convexity，以及 degree/control boundary（BCurve degree 1/2/4/6，
  BSurf 1/3/4，2×2/3×4/4×4/5×5 和内部 knot）。rational 3D 数据已按
  `vertex_dim=4` 的 `XYZW` 齐次控制点验证：前三个分量是乘以 weight 后的坐标，第四个
  分量是正 weight，不是真实空间维度；周期 smooth-seam 由 `PK_BCURVE_create_splinewise`
  生成并通过 `PK_BCURVE_ask` 确认。
- 经典 supporting surface：Plane、Cylinder、Cone、Sphere、Torus、BSurf、Offset、Swept、Spun。
- SPCurve：9 类 supporting surface × 4 类直接二维 BCurve，共 36 个成功配对；Line/Circle/
  Ellipse 来源另有 23 个成功配对。另有 rational、closed、periodic、seam-crossing 6 个
  变体。4 个 BSurf/球面/环面解析来源仍保留精确 `needs-recipe`。
- ICurve：13 个成功 fixture，覆盖 BSurf 与 Cylinder/Cone/Sphere/Torus/Swept/Spun、
  Cone/Swept、Sphere/Swept，以及反向 face 顺序、seed vector、finite box、
  periodic support、face sense，和深度 2 的依赖型支撑（spun(B-curve) × B-surface）。
  其余解析交线若被内核表示为 Line/Circle/Ellipse，不冒充
  `PK_CLASS_icurve`，记录在 `coverage/unreachable/icurve-surface-matrix.json`。
- Face-face blend：20 个成功 matrix case，覆盖多个 supporting-surface 配对代表（含
  sphere/spun）、rolling-ball、reversed sense、conic、multiple、propagate、relative rho、
  help point，并检查 `BLENDED_EDGE`/`BLEND_BOUND`/`INTERSECTION`。另有嵌套
  blend-on-blend 深度 2 fixture（`blend.fxf.plane-plane-plane.nested.2`）。notch 固定
  seed 无返回 blend body，记录为精确拒绝；width/ratio 的固定 seed 精确返回
  `PK_fxf_fault_inconsistent (17455)`。
- TRCurve：Line/Circle/Ellipse/BCurve 已覆盖 finite、reversed、periodic seam、full period
  或完整参数区间，共 12 个 case；周期 BCurve 的专门 seam 仍未关闭。
- Body/topology：Solid/Sheet/Wire/Minimum/Acorn/Compound；outer/inner/winding/vertex/wire
  loop、fin 1/2/multi、edge valence 0/2、shared/endpoint/apex vertex、through-hole 和双
  通孔。`empty`、`unspecified`、valence-1、inner_sing、zero-loop 有精确拒绝合同。
- Attribute/User field/Assembly：所有已声明字段类型和 body/part/face/edge/loop/vertex owner、
  named/multiple definition、user-field 1/4/8 槽和 body/face owner、多实例/shared part、
  translation/reflection/rotation。group-closing/callback、合法多层 assembly 尚未关闭。
- XT 基础设施：公共 inspector、语义 dependency checker、类型矩阵输入生成器和 8 个
  golden fixture（body、geometry、topology、blend、assembly、attribute、user-field）。

## 仍未关闭的计划维度

以下项目仍在 `type-matrix.json.deferred`，不能由 family-level label 代替：

| 维度 | 当前缺口 |
| --- | --- |
| `bspline.degree-control-boundaries` | 已有多个边界样本，但尚未枚举所有理论上限、退化组合和每个 degree×网格组合。 |
| `bspline.knot-multiplicity-clamped` | 已覆盖 clamped、unclamped、non-uniform、uniform/quasi/piecewise；完整端点/内部 multiplicity 两两矩阵仍缺。 |
| `analytic.geometry.standard-form-boundaries` | 已增加旋转轴、反向轴、最小正半径；所有方向、退化合法边界仍未穷尽。 |
| `derived-surface.profile-matrix` | Offset 正/负、Swept/Spun 线/圆已覆盖；zero offset（5026）和 periodic profile 仍未关闭。 |
| `spcurve.variant-matrix` | 变体已在三个 supporting surface 上验证，但尚未对 36 个 pair cell 全部重复。 |
| `icurve.surface-pairwise-matrix` | 12 个非解析 ICurve 配对已成功；Plane、Cylinder 等剩余解析配对需要可稳定产生 I_CURVE 的 seed。 |
| `icurve.face-seed-variants` | 已有顺序、sense、seed vector、finite box 和周期代表；显式 UV-box 组合在当前 runtime 精确返回 907，尚未形成每个 surface pair 的约束两两矩阵。 |
| `blend.surface-pairwise-matrix` | 已有一组成功配对代表（19 个 matrix case）；仍不是全部经典 surface 的全无序组合。 |
| `blend.option-matrix` | 已覆盖若干 option；trim/walls、range、holdline/limit、shape/xsection 的完整组合尚未关闭。 |
| `trcurve.range-matrix` | 周期解析曲线和有限 BCurve 已覆盖；周期 BCurve seam/full-cycle 仍缺稳定配方。 |
| `curve.cpcurve-producer` | 当前 header/binding 没有可产生独立持久 CPCurve 的公共 producer。 |
| `topology.manifold-cardinality` | 双通孔已补入；nested hole、seam/退化 edge、更多 disconnected shell 仍缺。 |
| `topology.diagnostic-loop-tokens` | `likely_*`、`unclear`、`unset` 等只在诊断/错误路径出现，尚无合法成功 fixture。 |
| `attribute.definition-action-matrix` | multi-field/multiple/named face 已补；group-closing 返回 `PK_ERROR_unsuitable_entity (914)`，callback/no-roll action 仍缺。 |
| `userfield.payload-callback-matrix` | 1/4/8 槽及 body/face owner 已补；callback 成功/错误/顺序需要专用 session callback 配方。 |
| `assembly.depth-transform-matrix` | 多实例和单层 transform 已补；合法多层 nested assembly 在当前 runtime 返回 `PK_ERROR_has_parent (28)`。 |
| `auxiliary.frame-boundary-types` | Frame 没有当前可用的独立公共 producer；Box/UV/Interval 仅作为其它 case 的输入。 |

## 需要用户提供的配方

1. 若必须以 `PK_LOOP_type_hole_c (5401)`/`PK_LOOP_type_peripheral_c (5402)` 的原始 token
   出现（而非 v38 的 `inner`/`outer` 归一化），需要旧版本 runtime 的可 transmit/receive
   公共 API 构造配方；当前已按运行时归一化记录。
2. 若必须关闭全部 ICurve/Blend 组合，请提供能让解析面交线或 derived-surface payload
   保持 `PK_CLASS_icurve`/可解码 XT 的固定 seed；否则这些合法数据仍应保留 `needs-recipe`。
3. CPCurve、Frame、Attribute/User-field callback 若有许可内 producer 或 callback ABI，
   需要对应的 host 配置和最小成功/拒绝合同。

明确排除：FCurve/FSurf、Mesh/PLine/MTopol/Lattice、indexed-I/O、General/non-manifold、
edge blend、three-face blend，以及冗余的 `PK_SURF_create_blend`；这些由
`type-matrix.json.excluded` 记录，不计入本轮门禁。

## 复查命令

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidXtCorpus.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidTypeCoverage.cs
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidTypeCoverage.cs -- --strict
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidSchemaDependencies.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidXtGoldenFixtures.cs -- --check
```
