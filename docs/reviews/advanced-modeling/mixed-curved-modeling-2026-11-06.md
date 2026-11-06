# M2 混合曲面实体建模验收

状态：M2 的有限、规则横截验收域已完成。失败探索运行和迭代验收来源分开保留。

基线：M1 `ae15ffc`。环境为 Linux x64、.NET SDK 10.0.302、真实 Parasolid
38.0.150。使用托管 Release、单元测试和独立真实 PK；没有 C ABI 改动，未执行
NativeAOT 发布。计划及实现合同见 `docs/Complex_Surface_Solid_Modeling_Plan.md`、
`docs/Mixed_Curved_Boolean_Operations.md`、`docs/Mixed_Analytic_Face_Domains.md`。

## 算法交付

1. 有限 Cylinder 与 Plane/NURBS/Blend 进入同一实体 Boolean 路径；保留真实
   Cylinder 及 Line/Circle/Ellipse。支持面重排不改变原始有序法向叉积决定的
   曲线 sense。周期侧面用精确轴向绕数，不增加人工 seam 边。
2. 圆/椭圆有限边的反求参数提升到实际周期区间，覆盖裁剪、边界分裂和点分类。
   自研 XT 重载后继续两次切削的 partial ellipse 回归保留真实有限弧。
3. Blend/Plane SSI 直接求真实参数域中的标量图，保留边界根、横截面根和
   CHART 误差证据。所有 chart 共享预算；局部角点先合并到全图，再明确拒绝
   真正无关联 segment 的孤立点，不允许未解时返回成功无交。
4. Blend/Plane ICurve 再被第三平面切分时，将两个平面降为精确直线，对 Blend
   隔离三未知根，再按原 ICurve CHART 选择实际分支和有限区间。
5. 布尔新生曲边可以继续进入 M1 圆角：认证与原有 Blend 和有限切削面的全带
   分离，核对原体材料，保持原 Blend，再装配新增 Blend。重叠时完整回滚。
6. 针对实际 SSI 热点，认证完整单跨端夹持 Bezier 后直接提取精确齐次控制网，
   二阶界复用已有一阶齐次界。权重缩放及 subnormal 舍入继续向外包围；
   一般多跨 symbolic de Boor 路径保留，没有采样取代认证。

## 独立混合矩阵

入口：`scripts/MixedCurvedBooleanOracle.cs`。两个内核从相同参数分别创建原体、
工具、M1 圆角和 Boolean。圆角在双方显式使用 v261 选项，保留相同 Blend 类别。
生产算法没有调用真实 PK。

| 输入族 | 案例数 | 必须覆盖的操作或几何事件 |
| --- | ---: | --- |
| M1 圆角体，对称平面切削 | 3 | 并、交、差 |
| M1 圆角体，非对称平面切削 | 3 | 并、交、差，真实非圆截面 ICurve |
| 连续操作 | 3 | x 后再 x、x 后再 y、非对称 x 后穿过接触区的 y 切削 |
| 内部工具、顶面内闭环、窄槽 | 3 | 空腔 shell、面内孔环、Blend 两次有限交段 |
| 两个相交工具 | 3 | 并、交、差，埋入工具边的消除 |
| 真实非平面 NURBS 工具 | 3 | Blend/NURBS 并、交、差，两个有限交段 |
| 另一个独立 M1 圆角体 | 3 | Blend/Blend 并、交、差 |
| 斜圆柱刀具 | 1 | 混合圆角 roof 实体的贯孔，包含 NURBS/Cylinder 交边 |
| 布尔新生边再圆角 | 1 | 原圆角 → 后上角切削 → 新生曲边半径 0.05 圆角 |
| 合计 | 23 | 每例单独保存 fresh 自研 XT 和独立 PK 基准 |

23 个案例均已通过上述硬条件。这是迭代开发中的逐例验收集合，不伪称同一最终
版本的一次完整矩阵运行。最后构建重点重验了受角点合并影响的非对称差/并/交、
连续非对称两刀、两个重载分类消费者，以及真实 Blend/Blend 重型差集；均通过。
原先完整 23 项原生合法性与几何覆盖门槛也使用最终脚本重新运行通过。

逐例日志、构建来源和统计索引持久保存为
`docs/reviews/advanced-modeling/mixed-curved-modeling-2026-11-06-cases.json`。
其中日志里的 `98b2062` 是最终构建时的主工作树父提交；实际构建同时包含
主线集成改动，随后与本报告一起统一提交。23 例最佳成功证据合计 **99 次完整
BODY_check 零故障、23 次严格比较**，15 项物性最大归一残差 **3.882e-9**；
五个关键结果共 **30 个**重载后自研/PK 非边界点分类一致。

对几何覆盖有独立硬断言：NURBS 工具结果确有两个同类 Blend/BSurface 有限交边；
第二个圆角体结果确有 Blend/Blend 交边；窄槽确有两个 Blend/Plane 有限交边；
闭环结果的 BSurface face 有至少两个 loop；空腔的 solid region 有两个 shell；
再次圆角后保留两个真实 Blend 面。这些不是仅根据案例名称推断的覆盖。

“两个有限交段”不等于无限延拓 SSI 的全局连通分支数，不能混淆这两个合同。

每个结果的硬条件：fresh 自研 `PK_PART_transmit_b` text XT 经真实 PK receive；
完整 `BODY_check` 零故障；region/shell、材料归属、face/edge/vertex 数量相同；
`DEBUG_BODY_compare` 保留类型、sense、容差、missing 和 unmatched 检查；
全部 15 项 PK 物性 `abs(a-b)/(1+abs(a)) < 1e-7`。只有 CHART 点数等内部
参数化差异作为诊断，不关闭几何类别或 sense 检查。

连续非对称、空腔和再次圆角还执行自研 XT receive、删除原体、六个明确不在
边界上的点的自研分类，核对独立 PK 的 enclosure 和材料语义。

## 有限解析域专项

`scripts/MixedAnalyticBooleanOracle.cs` 独立 **18/18** 通过：三种 Boolean、
两个 seam、镜像、自研保存重载后部分椭圆弧切割及第三次切割。每例完整 PK
检查、结构比较、物性和 150 个点/拓扑分类均为硬条件。配套 16 个单元测试
覆盖七档故障预算、原生 block 字节恢复、warm 零托管分配和跨周期边上分类。

原来的 almost-coaxial 拒绝回归要求显式 `not_implemented`。新增 reader 曾使其
误入未验收的 skew Cylinder/Cylinder finite-domain 路径并返回 157；已在公共
支持面组合门控修复，不能通过近似旋正输入来制造成功。

## 集成时发现并保留的失败证据

- 单纯延长 45/180/600 秒进程预算的旧 Blend 探针曾超时。它们都是未完成，
  不计通过，也没有被解释为空交。
- 全局孤立角点的拒绝检查最初放在局部 cell 上，导致 `x=.55` 两例约 2.7 秒
  返回 157。最终修复保留所有候选点，合并 cell/chart 后再判断全局孤立点；
  制造解保留真实孤立点拒绝，并增加跨 cell/chart 角点续弧回归。
- 圆柱连续切削曾出现一条解析交边的 sense 差异，作为严格失败修正；有限弧
  跨周期反求也先复现失败再修复，没有把这些差异降为诊断。

## 最终集成回归

| 检查 | 结果 |
| --- | --- |
| Release KernelTests | 1,452 通过，0 失败，0 跳过 |
| XT 独立库测试 | 12/12 |
| 既有 NURBS/Blend sheet 独立 PK | 79/79 |
| 既有规则曲面实体 Boolean 独立 PK | 33/33 |
| 集成后的有限解析专项独立 PK | 18/18 |
| scalar Plane SSI 制造解 | 9/9，含闭环、跨 cell/chart 续弧、孤立点和零预算拒绝 |
| Bezier/区间界定向回归 | 65/65，含新增 10 项等价界及极端缩放回归 |
| 生产 Release 分配 IL guard | 2,283 方法，CLEAN |
| 接口目录 | 174 项，通过 |

角点修复后的最终生产构建另对 `x=.55` 差、并、交和非对称连续两刀执行四项
严格 PK 复验，全部通过；连续两刀包括自研重载、删除原体后的分类。
两个原有连续 x/xy 案例也补齐同样重载消费者验证。新增单元测试还保留与已有
Blend 重叠的第二次圆角拒绝及原几何/native block 字节恢复。

## 后续边界

本轮仍限定有限、规则横截。一般重合区域、精确相切、奇异分叉、周期 NURBS、
一般球/锥/环面有限域、skew Cylinder/Cylinder 和 pole 事件留在 M3；变半径、
相邻 Blend 覆盖、边链、角点和短边消失留在 M4。新生边圆角仍要求 M1 的简单
平面终止面和可认证的障碍清除，不代表任意布尔新边都可圆角。

复杂 Blend 交叠仍明显较慢，后续 M5 应针对完整支持面求交、已有接触边的
重复相交、绕数和面域分类做定向优化。热求值或 Bezier 提取吞吐不能代替
整个实体建模耗时。本轮计时是正确性运行的诊断，不是稳定性能基准。

候选构建的 Blend/Blend 并、交、差分别约 482.9、415.2、424.3 秒；最后角点
修复的生产构建再跑差集约 390.9 秒，完整 PK 检查、严格结构和 15 项物性仍过。
不能用这些单次、不同并发负载下的计时宣称稳定建模性能提升。

## 临时证据位置

- 混合输入、输出、各例日志：`temp_docs/mixed-curved-boolean-oracle/`。
- 初期完整原生输入制造验证：`temp_docs/m2-native-full-input-matrix.log`。
- 集成缺项：`temp_docs/m2-integrated-final-missing.log`。
- 旧候选代表的两项真实回归：`temp_docs/m2-integrated-regression-representatives.log`。
- 角点修复复验：`temp_docs/m2-corner-fixed-regression.log`。
- 最后构建连续消费者与重型差集：`temp_docs/m2-final-corner-build-consumers.log`。
- 最终托管构建：`temp_docs/m2-corner-fixed-production-build.log`。
- 最终内核测试：`temp_docs/m2-final-kernel-tests.log`。
- XT、sheet 和既有 Boolean 回归：`temp_docs/m2-final-xt-tests.log`、
  `temp_docs/m2-final-sheet-oracle.log`、`temp_docs/m2-final-regular-boolean-oracle.log`。
- 集成解析专项：`temp_docs/m2-final-analytic-oracle.log`。

以上是可复现脚本的本次运行证据，不分发私有 PK SDK。
