# M2 混合曲面实体布尔

状态：M2 的有限、规则横截验收域已完成。完整结果及边界见
`docs/reviews/advanced-modeling/mixed-curved-modeling-2026-11-06.md`。

基线为 M1 `ae15ffc`。本阶段沿用有限 face/edge 域、精确支持几何、双 UV、
共享边 arrangement、材料分类和 shell 装配的公共路径，扩大输入几何组合。
主线计划见 `docs/Complex_Surface_Solid_Modeling_Plan.md`。

## 支持合同

- 有限平面、规则非周期 NURBS、已有 rolling Blend，以及有限圆柱侧面组成的
  manifold solid，默认 Boolean 选项下的并、交、差。原有 tracking 和
  `merge_imprinted` 限制保持显式错误。
- 圆柱面保留解析 Cylinder，交边保留 Line/Circle/Ellipse 和真实有限弧。
  周期 U 的侧面通过轴向 UV 射线的精确空间半平面求交计算绕数，两个非收缩
  周期边界环共同界定有限侧面，不把 seam 当成实体边界。
- 现有 Blend/解析、Blend/NURBS、Blend/Blend 求交输出进入同一有限面裁剪，
  由已有拓扑消费者处理多分支、内部闭环、空腔和多工具。
- 连续操作分裂已有 ICurve。对 Blend/Plane ICurve 与第三平面的相交，先构造
  两平面的精确直线，再对 Blend 隔离三未知根，最后用原 CHART 选择原曲线分支。
- 有限圆弧和椭圆弧的参数按周期提升到真实 edge 区间；公共曲线反求 API 的
  自然参数契约不因此改变。

这些是有限、规则横截的支持域，不包括一般重合区域、精确相切、球/锥 pole、
任意周期 NURBS、一般奇异分支和自交修复。尚未覆盖的解析类别不能因为已有
Cylinder 路径而宣称全面支持。预算耗尽和未解析单元必须使整个操作失败。

## Blend 与平面求交

一般 Blend/Plane 截面在真实 `(u,v)` 中求解一个标量平面约束，避免把平面上的
两个冗余参数也交给四维 SSI。区间残差排除空单元，非零区间偏导认证局部图，
四条边及横截面的完整标量根决定连通关系。局部 cell/chart 角点先保留为候选，
合并全部 chart 后再判定真正孤立的点。没有图证据或边界根未解决时继续
细分，达到预算则失败；孤立角点接触不伪装成无交。

所有 chart 共用发现预算。保留真实 Blend 与平面支持、双 UV 及保守 CHART 误差，
不使用拟合曲线替代几何。原 M1 中经整圆认证的常 u 圆截面仍保留精确圆路径。

## 布尔新生边再次圆角

M1 的规则单边协议继续使用 `set_blend → fix_blends`。对于已有切削或 Blend 的
实体，不能再要求每个其它面都是整个原体的全局半空间：

1. 能作为全局平面半空间的障碍仍对完整新圆角带做区间清除检查。
2. 其它有限障碍面用完整有限几何的保守包围区间，认证其与新圆角带分离。
   当前这条扩展只允许移除材料的凸圆角，并以原体材料分类核对带内一点。
3. 清除或材料认证失败就回滚。相邻 Blend 真正交叠、角点及短边消失仍属后续
   局部拓扑事件，不能靠忽略障碍或只采样通过。

验收链使用实际 M1 圆角体，切去后上角，再对布尔新生的 NURBS/Plane 曲边倒
半径 0.05 的圆角，原有 Blend 与新增 Blend 均保留。测试另检查新圆角与原
Blend 重叠时，原几何、pending 定义、实体池和原生 block 字节恢复。

## 独立验证入口

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedCurvedBooleanOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false
```

脚本先在两个内核各自构造同参数原体和工具，分别建模，使用自研
`PK_PART_transmit_b` 导出 fresh text XT，再由真实 PK receive。每个结果要求
完整 BODY_check 零故障、拓扑与 region/shell 材料语义一致；
`PK_DEBUG_BODY_compare` 保留类型、sense、容差和 missing 硬检查。
15 项 PK 物性归一差须小于 `1e-7`。只有内部 CHART 点数等参数化差异为诊断。

M1 圆角构造在双方显式使用 v261 fix 选项，保持真实 Blend 表示可严格比较。
脚本按 `--list` 列出案例，可选择单个名称；每例独立进程和明确时间预算，
超时不是通过，也不是成功无交。连续操作、空腔和再圆角案例另检查自研 XT
重载、删除源体后，自研点分类与独立 PK 一致。

原生输出、自研输入/输出和各例日志保存于
`temp_docs/mixed-curved-boolean-oracle/`。23 个混合案例及 18 个有限解析专项案例
通过独立 PK 对照；最终集成 1,452 项内核测试、12 项 XT 测试、79 项 sheet 和
33 项既有规则曲面 Boolean 回归通过。本轮不涉及 C ABI，不运行 NativeAOT 发布。

本轮 Blend/Blend 并、交、差约为 483、415、424 秒；最后角点修复后的重型
差集再次严格通过，约 391 秒。复杂实例性能仍是 M5 的明确工作项。
