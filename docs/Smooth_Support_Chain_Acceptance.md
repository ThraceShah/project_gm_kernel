# 光顺实体圆角链跨有限支撑面分区

本次将已有 subdivision chain 扩展到跨真实 support-face seam 的链。
基线为 `f9ba50e`，入口仍为 `set_blend → fix_blends`，保留真实 procedural Blend。
没有 C ABI 改动；验证使用托管 Release、零分配检查和真实 Parasolid 38.0.150。

## 支持合同

- 选边共享一条基础曲线，有限参数区间相接，内部端点是同一拓扑 vertex。
- 相邻 face 共享同一个 `SurfTag` 和方向。这是同一完整几何定义的分区，
  可直接认证光顺性。不同几何记录之间的 G1 连续性仍需独立认证。
- 半径一致、滚动分支规则。链内部允许每侧一个真实分区 seam，拒绝其它分叉。
- seam 是带 Plane 约束的真实 ICurve，或可由完整正权 BCurve 控制网/解析 Line
  认证的平面曲线。BCurve 平面判断使用向外区间残差，覆盖全部 knot spans。
- 每个活动 seam 与其整条接触曲线具有唯一、完整隔离的交点；该点必须属于
  seam 的真正有限曲线区间。未解析、多根、近乎重合但无法确认同一站位的截线
  明确失败，不发布部分替换。

当前仍要求现有单边构造器可认证的整体四边支持域和简单平面端部。
变半径、不同几何定义的光顺面切换、一般非平面 seam、真正角点和短面消退
不在本次支持域内。

## 构造与恢复

先临时统一同一有向支撑几何上的有限 face 分区，并合并原链的一条基础边。
在该规范化体上只求解一次 rolling branch，沿用原单边完整域、材料方向和
clearance 检查。分区之外的被动 seam 必须通过有限曲线与新 Blend 的完整求交
证明无交，才能按原有限曲线恢复。

两侧活动 seam 分别运输通过各自的接触线。不能把原选边 vertex 投影到 spine
后统一选一个截面：两侧运输站位通常不同。每个站位生成真实圆截线，将同一
Blend 支撑几何分成多个有限 face。原 seam 裁剪原支撑面，所有接触线端点先
进入公共边分裂表，再装配互为反向 fin 的共享 edge。

最终按完整有限域 winding 恢复每个原 face 的身份，通过实际共享边找回每个
Blend patch 的两侧支撑 face。最后一次替换才发布到原体；中途失败由同一建模
事务回滚。临时体、圆截线与未采用的几何均释放。

这条路径允许新 Blend 面数超过选边数。非对称制造输入的两个选边跨两侧 seam，
产生两个不同圆截面和三个 Blend face；中间 patch 的支撑 face 配对发生切换。
这些 face 共用一次求解的真实 Blend 几何，保持相同的切向场。

## 独立 Parasolid 验证

`scripts/SupportBlendChainOracle.cs` 先在真实 PK 中制造 Roof 并作平面 imprint。
两套 modifier 从同一有限输入出发，分别执行相同选边、半径、tolerance 和 v261
更新协议的实体倒圆角；该协议保留 procedural Blend 类型。

制造体有 10 个 face、20 条 edge、12 个 vertex，链内部是实际四价 seam 节点。
结果为 13 face、28 edge、17 vertex，并保留全部 10 个原 face 身份。
验证数据覆盖：

| 支撑及 seam | 原始坐标 | 反射、旋转 .37、统一缩放 1.7、平移 |
| --- | --- | --- |
| Plane/NURBS，x=.43 | 圆角、重载、Boolean | 圆角、重载、Boolean |
| Plane/NURBS，x=.61 | 圆角、重载、Boolean | 圆角、重载、Boolean |
| Plane/NURBS，x+.2y=.43 | 圆角、重载、Boolean | 圆角、重载、Boolean |
| NURBS/NURBS，x=.43 | 圆角、重载、Boolean | 圆角、重载、Boolean |

Boolean 消费者从自研 XT 重载，减去相同方块。反射输入也使用相同变换的工具。
每次修改后的 fresh XT 均由真实 PK 接收并执行完整 BODY_check、独立原生结果的
DEBUG compare、region/shell 语义和 15 项物性比较。类型、sense、容差和 missing
仍为硬条件；内部 chart 数差异继续按既定合同单列诊断。

相关的近乎平行解析 Plane/Plane 支撑面问题由公共有限域分离证明处理
（`KernelRuntime.FinitePlaneSeparation.cs`）。判断使用整个有限边界的区间符号，
没有按角度阈值将不平行曲面当作平行，也没有跳过反射 Boolean。

## 单元验证与证据

最终 `f9ba50e` 基线的全矩阵为 **8/8** 条完整建模链通过，32 次完整 PK
BODY_check 全部为零故障，物性最大归一化差为 **2.71e-9**。相关单元测试
**45/45** 通过；生产构建检查 175 个调度描述符、2337 个核心方法分配 IL 为 CLEAN。

新增 14 项单元测试覆盖：原 face 身份和共享截线、重载后的同一支撑关系、
选边次序置换、九档分配故障注入及失败回滚、半径不一致/链不完整的拒绝、不同支撑记录
须另行认证的拒绝，以及预热后的完整 FixBlends 零托管分配。
与既有 subdivision chain、单边实体圆角、有限 Plane 分离和 contact endpoint
回归一起运行。测试 fixture 位于 `tests/KernelTests/Fixtures/smooth-support-chain.x_t`，
由同一 oracle 的真实 PK imprint 制造；它不参与算法选择。

完整 oracle 与相关测试日志见
`docs/reviews/advanced-modeling/smooth-support-chain-validation.txt`。
脚本的 `TIME FixBlends` 与 `TIME Boolean` 是完整 API elapsed；本轮有其它建模
工作并行运行，这些时间仅为该次验证记录，不是性能 A/B 结论。
本组完整 FixBlends elapsed 为 2.35–13.78s，Boolean 为 9.61–44.04s，逐例记录
均保留在原始日志中。

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/SupportBlendChainOracle.cs -c Release
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release --filter FullyQualifiedName~BlendSupportChainTests
```

原输入、圆角和消费后的 XT 输出保存在 `temp_docs/support-blend-chain-oracle/`。

## 综合主线的有限求解回退回归

在 `c7d81bf` 加 `efe4c4e` 上重新执行了全部八个完整案例，**8/8** 通过，32 次完整 PK BODY_check 均为零故障，最大十五项归一化物性差为 **3.17e-09**。这份快照包含公共近切标量 SSI、稳定残差、自然域端点细化、弦误差扩大、有限 rolling continuation 和全局接触认证；不把较早支持链快照的结果直接视为这些共享改动后的验证。

本次修复了已有 NURBS/Plane ICurve 被第三个近乎平行 Plane 切分时的降维回退：无限平面线的数值失败继续交给原有限四参数区间证明，不能直接排空或提前终止。原失败的 `asymmetric43-reflected` 与其余七例均保留完整严格比较。另有两个独立微倾斜第二闭环制造用例，与相关回归合计 **14/14** 通过。

原始日志见 `docs/reviews/advanced-modeling/support-chain-finite-fallback-validation.txt`。本轮与其它建模进程并行，TIME 行只保留操作记录，不作性能 A/B 结论。
