思考了 12m 23s

结论

本轮不能整体通过。多项修复已经落实，但新增的多 seed 恢复路径引入了一处普通 ICurve 的错误分支回归；SVD 的尺度处理、Terminator 分支保持等问题也尚未闭环。

本次锁定当前 main：b85cf130dd66284fd8b2d97db8e4a6e2e9933ebc，对照上轮 9970720 检查四个新增提交，并沿用原报告的 17 项编号核对。

本轮完成了源码、调用链和新增测试检查，并执行了 Python binary64/FMA 数值复算，使用 100 位精度参考解对照。没有执行 C# 内核、.NET 测试或 Parasolid oracle，也没有修改仓库。 下面明确区分可复算的错误结果与尚未贯通的功能。


---

1. [P1，新回归] 多 seed 后备绕过延拓的安全拒绝，重新发布错误分支

位置：

Geometry/Intersection/ICurveContinuation.cs → SubdivideTo
Geometry/Intersection/ICurveMultiSeed.cs → BuildOrderedSeeds

这是本轮最需要优先修复的问题，对应原第 16 项的新接线。

当前 SubdivideTo 在 ContinueTo 失败后，不区分失败是否为 ParameterResolutionLost 或分支相关拒绝，直接进入新增候选循环：

ContinueTo 失败
→ 构造候选
→ 对目标参数直接 CorrectAt
→ 任意候选成功便返回 Success

这条成功出口没有经过延拓的 SameLocalBranch，也没有原锚点到候选根的连接验证。候选中统一填写的 branchId: 0 只是标签，不是实际分支证据。

一个普通 ICurve 的复现输入

支持面为平面 x=0，以及主半径 1.5、管半径 1、绕 Z 轴的环面。原 chart 位于 Y 为正的截面圆：

\[
C(\theta)=(0,\;1.5+\cos\theta,\;\sin\theta),
\qquad \theta\in[-2,1].
\]

输入：

P0 = (0, 1.0838531634528576, -0.9092974268256817)
P1 = (0, 2.0403023058681398,  0.8414709848078965)

base_parameter = 4503599627370496       // 2^52
base_scale     = 5.012556521233624
查询 t         = 4503599627370497
重建区间终点   = 4503599627370506

该查询位于原参数区间的 10% 位置。原圆弧上的弦投影单调，最小切向投影为 \(\cos(1.5)>0\)，不存在段内参数折叠。

正常延拓首先选择步长 0.25，但：

2^52 + 0.25 == 2^52

所以 ContinueTo 正确返回 ParameterResolutionLost。当前代码中这一保护仍然存在。

问题发生在新增多 seed 后备中。按当前 I1 与发布检查公式独立复算，弦点候选经过六次快速检查，得到：

项目	结果

正确分支上的位置	(0, 1.6463066710347496, -0.9892392824846421)
当前多 seed 路径接受的位置	(0, -0.5189258985987262, 0.19363266139699442)
位置误差	2.4672693643426746
当前发布容差	约 1.5e-13
最终 Newton 修正量范数	约 7.50e-17


返回点位于 Y 为负的另一条截面圆。它满足两个支持面和参数平面，也通过现有切向符号与环面局部特判，因此 CorrectAt 返回成功。

这个回归确实由本次接线引入：在新增候选循环之前，同一输入的延拓失败后，中点计算也无法产生不同于锚点的可表示参数，原路径会拒绝，而不是发布这条错误分支。

修复意见

不能用“换一个初值直接收敛了”覆盖“无法验证安全跟踪”的失败。

应首先区分失败类别：预算耗尽、参数分辨率不足、分支歧义不能无条件进入普通 seed 重试。其他求解表示可以恢复这些问题，但必须提供新的、有效的分支验证依据。

多 seed 的每个候选也必须携带真实来源和有效域；从锚点来的候选，应经过受限延拓或等价的局部连接验证。不能把所有候选标为同一个 branch ID 后，就省略验证。

回归要求： 本例允许返回正确位置，或保持输出不变并明确拒绝；不能返回负 Y 截面圆上的根。


---

2. [P1，数值模块] 单边 Jacobi 仍直接平方累加，良态矩阵也会被误判为秩零

位置：

Computation/Numerics/SmallLinearSolve.cs → SvdFactorizeSquare

上轮的 \(A^TA\) 构造确实已经移除，原来的小奇异值反例也已修好。但新实现仍直接计算：

alpha += bp * bp;
beta  += bq * bq;
gamma += bp * bq;

// 最终列范数
sumSq += bkj * bkj;
singularValues[j] = Math.Sqrt(sumSq);

没有尺度保护；最后也没有验证奇异值是否有限，或者是否因为下溢而丢失。函数仍会返回 Success。

不需要病态矩阵即可复现

取：

\[
A=\lambda I_2,\qquad b=(\lambda,\lambda)^T.
\]

它的条件数恒为 1，正确结果始终是：

\[
\sigma_1=\sigma_2=\lambda,\qquad \operatorname{rank}(A)=2,
\qquad x=(1,1)^T.
\]

按当前运算顺序复算：

\(\lambda\)	当前奇异值	当前 rank	随后的最小范数求解结果

\(1\)	(1, 1)	2	(1, 1)
\(10^{-200}\)	(0, 0)	0	(0, 0)
\(10^{200}\)	(Infinity, Infinity)	0	(0, 0)


后两种情况下，输入、真实奇异值和正确解都可以由 double 表示，出问题的是中间平方量。当前 SVD 以及随后使用 rank 的求解器都会返回成功。

另一方面，原反例：

\[
A=\begin{bmatrix}1&1\\0&10^{-10}\end{bmatrix}
\]

本轮复算得到的最小奇异值约为 \(7.071067811865475\times10^{-11}\)，rank 为 2。原 Gram 消减问题已经改善，不能因此把新的单边实现推倒重来；需要补齐尺度与收敛契约。

修复意见

保留单边 Jacobi 路线，补上稳定列范数、缩放后的相关量和旋转计算，并对最终结果做有限性与收敛检查。达到 sweep 上限但未满足正交性要求时，不能无条件成功。

对于整个矩阵量级特别大或小时，可采用适当缩放；列间尺度跨度大时，还需要避免局部点积再次溢出或下溢。LAPACK 的 Jacobi SVD 同样专门处理缩放、数值范围和未收敛状态，这些并不是更换算法名称后自然获得的保证。

回归要求： 加入上述三个尺度的单位阵、不同列尺度、秩亏矩阵、重构与最小范数解测试；不能只保留当前新增的一个近秩亏夹具。当前新增测试主要覆盖的是原来的 \(A^TA\) 消减反例。


---

3. [P1，内部 Terminator 路径残留] witnessMu 仍固定为零，没有实现分支跟踪

位置：

Geometry/Intersection/TerminatorEvaluation.cs → SolveIntervalPoint、BracketedSolve

对应原第 9 项。

本次后备确实从“对称端点比较”改为分别扫描正负方向，但调用仍是：

BracketedSolve(..., bound, 0.0, ref budget, ...);

没有从 branch point 推进过来的参数、根或标量状态。扫描中又优先接受正方向首先发现的变号区间。因此，新增的 witnessMu 参数尚未成为真实 witness。

更重要的是，主 Newton 路径也仍然在目标截面上从 mu=0 开始，未验证从 \(B\) 到目标的连接关系。

主路径就有错误根，不必进入二分

使用第 1 项的同一环面、同一组 \(B=P_0\)、\(E=P_1\)，按当前 BuildPlanes 构造两平面，并指定内部制造参数：

tB = 0
tE = 1
t  = 0.1

当前标量快速求解也会返回同一个负 Y 截面圆上的点，误差仍约为 2.4672693643。这次没有大参数基值，也没有参数分辨率问题。

这是内部一面／两平面定义下的数学错误成功。公开 Terminator 路径仍受 GATE-T 限制，所以不应扩大成公开 API 已经在返回该结果；但该内部算法不能被标为已经保证分支连续性。

修复意见

需要从 \(B\) 的已知根开始跟踪，保存上一接受的参数、根、局部标量状态及有效域。后备 bracket 必须由这个状态限定，不能只是围绕目标截面的零点搜索。

只把 0.0 改成“最后一次 Newton 的 μ”也不够：失败迭代可能已经离开原分支。应保留的是已验证接受态，而不是最后一个试探值。


---

4. [P2，残留] Blend 圆弧验证只加在可选 Envelope 重载，恢复链和 Joint 仍未贯通

位置：

Geometry/Evaluation/BlendEnvelopeSolve.cs
Geometry/Intersection/BlendJointLift.cs

对应原第 3 项。

本次增加了 BlendArcBounds，带 bounds 的 4×4／3×3 重载会调用圆弧验证。这部分是有效进展。

但当前调用链仍存在：

TryRecoverAfterLocalSingular
  → 不带 bounds 的 SolveFourByFour
      → BlendArcBounds.Unbounded
  → 若失败，再进入 Joint
      → 仍只依据代数残差返回 Success

TryRecoverAfterLocalSingular 没有 bounds 参数，Joint 的成功出口也没有 spine 范围、接触侧和圆弧验证。

此外，当前 bounds 验证器使用圆 spine 的径向和 Z 轴作为截面基；这可以用于明确限定的圆管制造例，但还不是从真实 Blend 接触点、sense/range 恢复的通用 frame。

修复意见

纯数学候选求解允许使用 Unbounded，但应明确它返回的是候选方程根。用于恢复“实际 Blend 分片”的入口必须携带验证所需的 bounds/frame/witness，并在 Envelope 和 Joint 的所有成功出口执行同一套语义验证。

否则，“直接调用带 bounds 重载的测试通过”并不能证明实际恢复链不会绕过它。


---

5. [P2，验收缺口] Case F 已比较同参数 D0/D1，但仍没有比较原生 D2

位置：

scripts/IcurveEvaluationOracle.cs → RunOurWriterLivePkReceive

对应原第 11 项。

这里应先确认修好的部分：双方现在确实使用同一个 ourT 求值，D0 和未单位化的 D1也直接比较；误差超限会抛出失败，不再降成 NotRun。

但是 D2 仍然比较：

PrincipalNormalUnitDelta(...)
CurvatureOurs(...) - CurvaturePk(...)

没有比较：

Distance(&ours[2], &reference[2])

随后日志却写成 true same-t D0/D1/D2 compare。

仍然存在明确盲区

令参考速度为 \(v=x'\)，加速度为 \(a=x''\)。将错误加速度构造为：

\[
\widetilde a=a+c\,v,\qquad c\ne0.
\]

由于：

\[
v\times\widetilde a=v\times a,
\]

曲率不变；加速度的法向分量也不变，所以单位主法向比较同样无法发现错误。

因此，D2 的切向分量可以错误，而当前 Case F 的 D2 检查仍通过。

修复意见

增加原始 D2 向量的绝对／相对误差检查；曲率和主法向可以继续作为辅助诊断。优先采样严格位于原始 chart 段内部的参数，节点处另按已确认的单侧契约比较。

建议加入一个 oracle 检查器的负向测试：只对我方 D2 添加非零的 c * D1，该检查必须失败。

在完成前，能力清单应写成“同参数 D0/D1，加几何曲率／法向比较”，不能将其等同于完整原生 D2 兼容验证。


---

6. [P2，接口／缓存] ChartSide 尚未贯穿 I1 普通区间路径

位置：

Geometry/Evaluation/ICurveEvaluation.cs → EvaluateRegularInterval、EvaluateI1ByContinuation

对应原第 10 项。

节点路径已正确使用请求的 side 定位导数段，并独立查找原始节点位置索引，这部分可以保留。问题在于普通区间选中 I1 后，没有把 side 传给 EvaluateI1ByContinuation；后者仍将报告和缓存样本固定写成 ChartSide.Right。

最简单的触发序列：

普通单位圆，普通区间中点
请求：ChartSide.Left

第一次：求值成功，但 report.Side == Right，缓存也写入 Right
第二次：仍按 Left 查缓存，无法命中刚才的结果

L3 入口同样先按请求的 Left 查询，再根据返回报告中的 Right 发布，因此也无法使这类请求重新变热。

这不是说普通光滑区间的左右导数数值不同，也不是说已修好的节点左导数又错了。 问题是请求、报告和缓存 key 没有遵守同一套语义。

修复意见

将 side 继续传入 I1 专用路径，并统一该路径的报告与缓存发布。其他仍写死 Right 的预测、失败报告位置也应一并核对。

另一种设计是明确把普通区间 side 归一化，但必须在查找、求解和发布之前统一处理，不能只在某个分支中悄悄改成 Right。


---

7. 两项“增加了结构，但还未完成交付”的残留

7.1 Witness：存储已完成，求解器的生产与消费尚未接入

SampleWitness 已按值加入 L2/L3，Overwrite 和 ToL2Sample 也确实保存并恢复它，不再有借用 scratch 的问题。

但正常求值仍使用不带 witness 的 CurveSample 构造函数；该构造函数明确填入 SampleWitness.None。P2/P4 求解出的 UV 尚未作为结果返回，也未用于下一次求解初始化。L3 正常发布同样重新构造不带 witness 的样本。

新增测试是手工构造 SampleWitness 后直接写缓存，证明的是载荷 roundtrip，不是求解器保存、恢复和使用 witness 的完整链路。

修复方向： 让已验证求解结果携带对应计划的辅助状态；成功发布时写入缓存，下一次求解时按版本、分支、cell、精度和参数展开规则恢复。当前可标为“存储载荷可用”，不应关闭原第 15 项的完整状态复用要求。

7.2 诊断：绑定重载已有出口，公开求值仍丢弃原因

新增 TryBindICurveEntity(..., out ChartBuildFailure) 是有效修复，但 Runtime 的 EvaluateICurve 仍然：

TryPrepareICurveView(..., out _)

也没有把具体失败索引传到外层报告。

修复方向： 保留新增重载，继续把准备失败原因、源实体和 chart/UV 索引贯穿到内部诊断出口。无需臆造新的 PK 错误码，但不能让公开调用后的诊断仍只有笼统求值失败。


---

上轮 17 项的闭环情况

下表中的“已修”均指本轮源码级核对，不表示本轮执行过相应 .NET／oracle 测试。

原编号	本次判断

1. Schur 工作区与尺寸契约	原问题已修。 扩大内块复制区，检查矩阵／向量长度及 m == nx，最终步暂存后发布。
2. 联合残差丢弃求值状态	原问题已修。 三次支持面求值状态均传播，Joint 先检查装配状态，MaxAbs 不再忽略非有限值。
3. Blend 候选接受验证	部分完成。 带 bounds 的 Envelope 重载可检查；恢复链与 Joint 尚未贯通。
4. Terminator 2×2 平面方程	原公式已修。 残差及 Jacobian 使用 PlaneNormal 和 ChordUnit。
5. ChordalError 放大贴面容差	原耦合已移除。 当前单独使用点面一致性容差。
6. UV null 语义	核心路径已修。 成对缺测、单分量拒绝及写回 null 已接入；完整 roundtrip 本轮未运行。
7. Sense 保真	本次涉及的导入、绑定和写出链已补齐。 全生命周期兼容结果仍需实际测试证据。
8. Terminator 法向回退	原不可达与取点错误已修。 显式处理选定面退化，并在 branch point 求切向。
9. Terminator 分支保持	未完成。 witnessMu 固定为零，主路径也未进行连接验证。
10. 导数侧贯穿	节点路径已修，普通 I1 路径仍有遗漏。
11. 原生参数 oracle	D0/D1 已明显改善，D2 仍未完成原始向量比较。
12. 稳定 SVD	原 Gram 问题已改善，但尺度处理仍可错误成功。
13. Chart 诊断传播	部分完成。 新增绑定出口，公开求值仍丢弃原因。
14. \(\eta_k\) 精度调度	已增加实际重评调用；仍是扩展点。 当前解析求值没有对应的子容差调度，不能当作完整嵌套精度闭环。
15. 完整 witness 缓存	载荷和缓存传输已完成，求解器生产／消费未完成。
16. 多 seed 接线	已接入，但引入本轮第 1 项回归，不能验收通过。
17. BlendBound 选面占位	当前限制已明确说明。 解析支持面范围内保持 false 合理，无需为了接线而绕过 GATE-B。


能力矩阵采用分层状态是改进，但其中“严格保证同分支”“完整同参数 D2 兼容”等措辞仍强于当前代码能支持的结论，应随上述残留一起修正。

回归材料与修复顺序

已整理本轮反例及正向控制：

下载 C# 回归测试草案

测试覆盖多 seed 错误分支、Terminator 同类错误根、SVD 大小尺度、原小奇异值修复控制，以及 Left 普通区间缓存一致性。该 C# 文件尚未编译或运行。

已执行的独立复算见 Python 复算脚本 和 数值证据记录。其中只模拟明确说明的相关路径，不是完整内核替代实现。

建议先修多 seed 绕过安全拒绝和 SVD 错误成功，再完成 Terminator／Blend 的分支接受链。 Oracle D2、side、诊断和 witness 则按独立契约逐项关闭。不要继续以“新增了参数、字段或一次函数调用”作为完成标准；这次剩余问题主要发生在这些新部件之间的连接和成功出口上。