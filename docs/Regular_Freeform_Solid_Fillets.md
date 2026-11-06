# 规则自由曲面单边实体圆角

本轮把 `PK_EDGE_set_blend_constant → PK_BODY_fix_blends` 接到现有常半径
rolling Blend 几何、原始 CHART 参数映射和实体 B-rep 替换。生产代码不调用 PK。
阶段计划见 `docs/Complex_Surface_Solid_Modeling_Plan.md`。

## 支持域

以下为 M1 初始支持域。M2 在这些合同上增加了对已有有限切削面及远离新圆角带
的 Blend 障碍的认证处理，使布尔新生曲边可再次圆角；见
`docs/Mixed_Curved_Boolean_Operations.md`。真正相邻 Blend 覆盖和角点仍不在此范围。

- 单个独立常半径边，Plane/NURBS 或 NURBS/NURBS 两个支持面；包括凸边和凹边。
- 两个不同的平面终止面，原边两个端点三价；两个支持面各为四边单环。
- 单个无空腔壳，有限、规则、非周期的滚动分支；可行半径，无自覆盖。
- 其余面必须是平面或可由控制网区间认证的平面 NURBS。原顶点须满足这些
  平面的有向半空间，完整 Blend 带（包括两条接触曲线）须通过非端盖平面的
  全区间清除检查。越界或不能认证时拒绝操作，不能发布中段穿过底面的结果。
- 端部优先识别经过整圆平面认证的精确常 u 圆截面，其余情况进入已有规则 SSI；
  不承诺所有斜终止、退化端部和人工参数界限外延续。

这不是一般自交修复或完整几何检查。多边链、角点覆盖、短边消失、变半径和
含任意非平面障碍的实体留在后续阶段。

## 几何与拓扑合同

滚动符号取决于两侧材料法向和原边 fin 的有向凸凹关系，而不是模型坐标方向。
两条接触边是真实 ICurve：一个原支持面与其 `BLEND_BOUND` 构造面的交线。
运行时保留支持依赖、支持侧别、原始 CHART 锚点与缩放，并用精确 Blend 截面约束
求解每个 CHART 参数对应的脊线站位。D1/D2 使用该映射的链式导数，不用差分。

XT 导出包含真实 `BLEND_BOUND(59)`、几何 owner 环、BODY 曲面链和 Blend 支持闭包。
导入不把无参数化的 BLEND_BOUND 伪装成公开曲面，而是重建同一 contact ICurve。
复制、几何删除、镜像和缩放维护 CHART 缓存与依赖。反射时交换 XT 的交线支持
顺序和 UV/误差角色，BLEND_BOUND 保持正 sense；运行时仍规范化为真实支持面在前。
当前编辑导入支持开放接触交线；未支持的闭合 Help 接触保留原始 opaque 图。

原支持面删除选边邻域，四条相邻边按真实接触点反求参数截短，两个端面替换角部，
四条共享边围成 Blend 面。保留原 body 和幸存 face 句柄，保留可唯一对应的其它
edge/vertex 句柄；原选边被替换。失败由现有 command transaction 恢复所有权、
拓扑、返回内存和 pending blend 定义。

圆弧区间必须按实际截面选支：接近 `2π` 的反求值先归到零，再选短弧。
不能直接取两个未规范化角度的最小值和最大值。凹圆角的端部圆曲线使用负 sense，
同步交换 edge 顶点和 coedge 方向，以保持原来的有向面边界。

## 圆角后的切削

接触 ICurve 可按原始 CHART 反求、与平面或规则曲面求交，并进入已有有限域
arrangement。Blend 与平面有一条经认证的常 u 圆截面时，复用精确圆：先认证整个
u/v 带的平面投影关于 u 严格单调，再验证两条接触曲线同站位、整圆共面。
这样每个 v 最多一个交点，不能漏掉额外交支。不能认证时仍走一般 SSI。

面域绕数中的圆截面也通过中心、半径和整圆平面身份认证后直接映射到 UV；
不是用采样折线决定面内外。全局单一 tube bound 不作为逐 chart 区间认证的前置条件。

M2 已增加非对称平面截面和混合曲面实体布尔的独立验收。`x=.55` 的早期双 NURBS
切削探针曾超过七分钟而未完成，该旧运行仍不记为通过；后续新实现的严格对照
及仍未支持的事件见 `docs/Mixed_Curved_Boolean_Operations.md`。

## PK 对照约定

独立 oracle 为两个内核分别从相同参数创建块体、替换非平面支持面，必要时用相同
布尔创建 L 形真正凹边，然后使用同一选边、半径和 fix 选项。

严格结构对照显式使用 `PK_blend_edge_update_v261_c`：PK 38 的现代默认 fix
在这些输入上会把圆角转换为 BSurface；v261 保留 BlendSF 和 BLEND_BOUND 接触
ICurve。自研同样接受这个明确选项。该对照不代表与 PK 现代默认的内部存储类型
完全一致，也没有关闭类型或 sense 检查。

每例检查 fresh 自研 text XT → PK receive → 默认完整 BODY_check、region/shell
材料语义、face/edge/vertex 数量、类型/sense/容差/missing、PK 物性，以及九个截面
的常半径、实际接触位置、G1 和独立 PK 曲面对应。只有内部 CHART 点数等参数化
差异可以作为诊断。完整体检查不能代替物性检查：本轮大弧选支错误正是由物性捕获。

运行入口：

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RegularSolidBlendOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false
```

支持域回归在 `tests/KernelTests/RegularSolidBlendTests.cs`，标量交点的制造解与
不确定区间拒绝回归在 `tests/KernelTests/SurfaceIsoPlaneRootsTests.cs`。
详细验收结果和未通过边界见
`docs/reviews/advanced-modeling/freeform-solid-fillets-2026-11-06.md`；运行输出保存在
`temp_docs/regular-solid-blend-oracle/`。
