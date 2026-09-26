# ICurve / Blend 求值实现 · 规格符合性审查（第 15 轮）

- 规格：`docs/icurve_design/icurve_blend_evaluation_spec.md`（v2.0，1481 行）
- 审查基线：`dd73576b584f8a06ba7f9f1396b8751563420179`（工作区干净）
- 测试基线：`MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests` → **Passed 535 / Failed 0 / Skipped 0**（原始输出 `temp_docs/icurve-evaluation/_baseline_test_run.txt`）
- 已阅历史：本人第 14 轮报告 `temp_docs/icurve-evaluation/spec-review-2026-09-26.md`（M1–M14）与 `temp_docs/icurve_reviews2/gpt_review_0..4.md`（terminator/SVD/预算/CaseF 四轮）
- 方法：七路并行子代理按规格章节分工（chart/导数、常规计划与解析能力、blend/blendbound/joint、缓存与生命周期、数值与接受、Runtime/XT/oracle/文档、terminator 复核）+ 主线对最高风险结论逐条独立复核（下文中标注「主线已核」的项为主线程亲自读码/推导确认，其余标注来源 lane）。

---

## 1. 总体结论

上一轮 14 项 Major 中，第 14 轮报告里最危险的几条（BlockSchurSolve 栈越界、联合残差丢弃求值状态、SVD 秩判定、CaseF NaN/差分门、UV null 填零、sense 写死、同参数 oracle 比较）**已被后续 10 个提交真实修复**（见 §5，主线逐条复核过 3 条关键项）。

本轮新发现 **1 项 Blocker、10 项 Major、22 项 Minor**。结论按风险排序如下：

1. **唯一 Blocker 在缓存并发**：L3 样本 arena 是 `static` 裸指针结构、零同步，而 `PK_CURVE_eval` 被声明为可并发只读命令；精确命中路径把缓存样本**不加校验**直接写进调用方数组。并发只读调用（调度明确允许并被仓库测试覆盖）下可发布错误点。
2. **计算正确性缺口集中在「已实现但未接线」的模块**：spun 消元梯度符号反、blend 包络 v 截面约定硬编码、§10.3 frame 导数链式项无实现无测试、距离组合入口无距离类型约束。这些今天不进生产，但 T11–T16 一接线即错。
3. **两处生产可达的数值缺陷**：(a) 修正器把缩放空间步长当真实状态增量（缩放只作用于模型）；(b) 缓存精确命中判据的量纲耦合，使大尺度模型（柱/球半径 ≳200）L2/L3 永久不命中。
4. **证据链与文档诚实性**：`docs/icurve_evaluation_capabilities.md` 仍有 7 行失实（回读侧接线、SampleWitness 生产消费、UV null/Sense 的「真实 PK 兼容通过」等），且**§5.1 余弦比递推（`f_i`）至今没有任何真实 PK 证据**——oracle Case F 的 chart 是正 49 边形，余弦比恒为 1。
5. **规格要求但未交付的能力**：§4.3/§4.4 依赖图与资源上限、§19.5 chart 映射跨调用复用、§13.1/§13.2 完整 witness 载荷的生产读写（详见 §7）。

---

## 2. Blocker

### B1【Blocker，并发路径】L3 缓存零同步 + 精确命中直接发布，并发只读命令下可发布错误点

- 位置：
  - `src/ProjectGmKernel.Native/Runtime/GeometryEvaluationCache.cs:56-62`（`internal static unsafe class`，`private static CachedCurveSample* arena`、`static MemoryPageIndex clockHand`，全文件无 `lock`/`Interlocked`/`Volatile`）、`:110-131`（`TryGetExact` 读 `entry.*`）、`:150-176`、`:180-219`（`Publish`/`ClaimSlot`/`Overwrite` 逐字段写条目并写 `clockHand`）
  - `src/ProjectGmKernel.Native/Runtime/KernelRuntime.EntryPoints.cs:15`（`Dispatch(ApiId.CurveEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ...)`）
  - `src/ProjectGmKernel.Native/Runtime/ApiDispatch.cs:270-295`（非 exclusive 命令只要求 `exclusiveWaiting == 0 && WriterCount == 0`，随后 `SharedReaders++` → 多个只读命令可同时在各自线程执行）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:227-233`（命中即 `PublishHit` 并 `return Success`）、`:517-522`（`PublishHit` 把 `hit.Position/First/Second` 原样拷入调用方 span，**无任何几何或有限性校验**）
- 违反条款：§13.9「当前调度仍为串行时，先在已有串行边界下保证版本和发布一致；不要宣称已经实现并行。未来并行采用……**不可变已发布样本、短锁或原子索引发布**」；§13.7「只在验证通过后发布样本」；§18.5「位置、导数……全部达到请求契约后统一发布」。
- 代码行为（主线已核）：`Overwrite` 先写键字段（`Owner/Parameter/Kind/Side`）再写 `Position/First/Second`，`Occupied` 最后写；因此线程 A 改写槽位途中，线程 B 的键比较可能已全部通过，随后读到「新键 + 旧几何」或半写几何，并因 `PublishHit` 不校验而**直接作为答案返回**。
- 与「调度已串行」前提的冲突（主线已核）：本仓库调度**不是**串行——`ConcurrencyKind.Concurrent` 只读命令以 `SharedReaders++` 共享同一 partition，`tests/KernelTests/MemoryConcurrencyTests.cs` 明确测试多线程并发建模/查询。
- 未做：运行期压测复现（概率性竞争，见 §8 实测项 1）。因此本条是「可致错误的竞争」而非「已观测到的错误结果」；若项目短期只支持单线程调用，则应在能力文档与 `ApiDispatch` 上显式降级声明，而不是保留「并发只读」登记。
- 建议：二选一——(a) 把 icurve 求值登记为 `Local`/`Exclusive`（或按实体分区写声明），(b) 给 L3 加「短锁 + 不可变条目 + 原子发布」：先在未发布槽位写好全部字段，再用一次原子写/版本号暴露，读取侧先取版本快照再复制，`clockHand` 用 `Interlocked`。任一方案都不得持缓存锁调用子求值器（§13.9 末句，当前无锁反而满足）。

---

## 3. Major

### M1【Major，生产可达】修正器把「缩放空间步长」当成真实状态增量：`D_u p` 从未施加

- 位置：`src/ProjectGmKernel.Native/Geometry/Intersection/ICurveCorrection.cs:94-95`（`freeze.Capture/Apply`）、`:120-123`（把**已缩放**的 `jacobianMaster`/`residualVector` 交给 `DoglegStep`）、`:161`（`for (i) trial[i] += step[i];`）；`src/ProjectGmKernel.Native/Geometry/Caching/FrozenResidualScale.cs:45-54`（`Apply` 写入 `J_s = W_F J D_u`、`r_s = W_F r`；`_col = D_u` 是 private，且**无任何把步长映射回状态空间的方法**）；`src/ProjectGmKernel.Native/Computation/Numerics/TrustRegionStep.cs:33-81`（`DoglegStep` 完全在所给 J/r 的空间里求解：`rhs[i] = -residual[i]`，出参 `step` 即该空间步长，无反向缩放）
- 违反条款：§14.1「令未知量增量 $\Delta y=D_u p$，缩放残差 $r=W_FF$，Jacobian $J_s=W_FJD_u$……row scaling 与 unknown scaling 在一次 trial 的预测/实际下降比较中固定」。
- 代码行为（主线已核）：模型与预测下降 $\frac12\|r_s\|^2-\frac12\|r_s+J_sp\|^2$ 都按 $\Delta y=D_up$ 计算，但实际 trial 点是 $y+p$。**两侧不是同一个模型**，$\rho$ 因此系统性失真；列尺度越悬殊（正是缩放存在的理由：低质量参数化、量纲不一致的 UV 块）偏离越大，症状是假拒绝 → 半径收缩 → `Stagnation`（`ICurveCorrection.cs:204-214`）。
- 影响判定：因为是「该收敛时报告停滞」而非「发布错误根」（接受仍需真实 $\Psi$ 单调下降 + 每次接受后重跑 `IsPublishableRoot`，`ICurveCorrection.cs:176-193`），故为 Major；但注意 `minRadius = 1e-12 * scale0`（`:97-101`）把状态量纲的量与缩放空间半径混用，修 M1 时需一并换算。
- 可达性：`EvaluateWithPlan/EvaluateWithCache` 的快速 Newton 预算耗尽 → `Refine`；以及 `ICurveContinuation.CorrectAt` → `Refine`。
- 建议：给 `FrozenResidualScale` 增加列缩放回映射（`step[i] *= _col[i]`）后再更新 trial 状态，并把半径/minRadius 统一到同一空间；补一条列尺度非 1 的正向用例（现全部缓存/修正器夹具列尺度≈1，故不可见）。

### M2【Major，潜在（零调用方）】spun 子午面残差梯度整体反号

- 位置：`src/ProjectGmKernel.Native/Geometry/Evaluation/SweptSpunImplicit.cs:55-58`
- 违反条款：§9.4「对 spun，可以用轴向高度一致和径向长度平方一致的两个标量方程消去旋转角……」；§8.3「自然法向是参数偏导叉积……不要仅凭半径为正假定梯度方向」。
- 代码行为（主线已核，含手算反例）：`f(x) = ((x−P)×A)·((C−P)×A) = u·(A×wc)`，故 $\nabla_x f = A\times w_c$；代码返回 `Cross(wc, unitA)`。取 `P=(0,0,0)`、`A=(0,0,1)`、`C=(2,0,1)`、`x=(2,0.5,1)`：`f = 2·x₁`，解析 $\partial f/\partial x_1 = +2$，代码给 `wc×A = (−2,0,0)`。`Wired=false` 时无影响，接线（T11/T13）后 Jacobian 行符号错误会直接把 Newton 引向错误方向。
- 建议：改为 `Cross(unitA, wc)`，并补一条以中心差分为准的梯度测试；同时补 §9.4 要求的「轴上点 / 脚点不唯一 / 错误 profile 分支」守卫（当前只有 `a2 > 1e-30`）。

### M3【Major，GATE-T 内】terminator 参数重建用未按 sense 定向的叉积判「正向」，支持面 sense 一正一负时假拒绝

- 位置：`src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:177-185`（`tangent = Unit(Cross(jet0.Gradient, jet1.Gradient))` 后要求 `Dot(tangent, boundaryChord) > MinCosine`）；对照 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.PrepareEvaluation.cs:250-263`（chart 切线为 `sense₀n₀ × sense₁n₁`）
- 违反条款：§6.2「以递增参数方向定义单位弦 $e$」；§5.1「单位自然切向 $T_i$ 由两个**考虑 sense** 的支持面法向叉积归一化获得」；§6.5「`TryResolveTerminatorParameter` 必须有可复现依据」。
- 代码行为（主线已核）：同一几何量在仓库里有三种处理——`Prepare` 算完叉积后按 chart 方向翻符号、`IsPublishableRoot`/`TryChartTangent` 按 sense 定向、只有本函数把裸叉积符号当硬条件。当两个支持面 sense 之积为 −1（合法配置）时叉积与 chart 增向相反，`forwardCosine < 0` → **该 icurve 的 terminator 参数恒返回 `Singular`**，与几何是否退化为「本端不可延拓」无关。现有夹具两个 sense 都是 `+1`，因此从未暴露。
- 建议：按 sense（或按 `boundaryChord`）定向后取投影，`chordCosine` 保留为真正的回折检查；补一条支持面顺序/sense 互换的正向对照用例。

### M4【Major，GATE-T 内】terminator 全部数值判据用绝对常数比对未归一化的 ∇φ，并用世界坐标/`1.0` 地板

- 位置（主线已核）：`TerminatorEvaluation.cs:229-234`（选面奇异判据 `GradientNormSq(∇φ) ≤ (1e-12·(1+|E|))²`）、`:310-311`（`|∇φ_A·v| ≤ 1e-12 → Singular`）、`:436`（`gNorm > 1e-14` 门）、`:486`（发布门 `tol = 1e-7·max(ChordLength, 1.0)`）、`:719-725`（`slope` 与 `1e-300` 比较）
- 违反条款：§6.2「`surface[0]` 在 terminator 处奇异而 `surface[1]` 不奇异……**显式 first 并不覆盖前两个例外**」；§6.3「$h_\mu=\nabla\phi_A\cdot v$」；§6.4「若项目要求额外模型一致性阈值，必须作为已验证兼容策略声明」；§21.7「不能直接等同于所有 evaluator 的 Newton 停止门槛……不通过改阈值掩盖 regressions」。
- 代码行为（主线已核）：本切片解析隐式全部是未归一化式（柱/球 `|∇φ| = 2R`），故
  - 选面判据实际等价于 `R ≲ 5e-13·(1+|E|)`：把 `R=1e-8` 的圆柱放在 `|E|≈1e5` 处会被判「在 E 处奇异」，从而**覆盖显式 `first`** 改选 `surface[1]`；同一几何平移到原点附近结论又变（世界坐标耦合）。
  - `denom`/`slope` 是同族带量纲量：同一无量纲问题整体缩放 `s` 后，`|denom|` 随 `s` 线性变化，`s ≲ 5e-7` 时由 `Success` 变 `Singular`。
  - `tol`/`geomScale` 的 `max(…, 1.0)` 使长度 < 1 个世界单位时全部退化为绝对常数（相对精度退化 5–6 个数量级），且该阈值未在任何兼容策略/文档中声明（§6.4）。
- 可达性：GATE-T 隔离（生产入口规则未指定时直接 `CompatibilityGateOpen`），仅显式规则实验入口可达——**这正是本轮应修的理由**：GATE-T 关闭前这套判据就是将来关闭 gate 的依据。
- 建议：全部改成无量纲判据（如 `|∇φ·v|/(|∇φ|·|v|) > ε`，`v` 已是单位向量；奇异判据改为归一化梯度的退化或用局部尺度 `‖H‖·L`），删掉 `(1+|E|)` 与 `1.0` 地板；确需绝对地板时写入 `docs/icurve_evaluation_capabilities.md` 的兼容策略并让测试同时记录阈值。

### M5【Major，证据链】§5.1 余弦比递推 `f_i` 至今没有真实 PK 证据；能力文档却称「真实 PK 兼容通过」

- 位置：`scripts/IcurveEvaluationOracle.cs:282-296`（Case F chart = `angle = Tau·i/(chartCount−1)`，49 点正多边形 → 等弦）+`:363-364`（`chordLen`、`segDeltaT` 按「每段 Δt = C_i·f_0」硬编码）；`tests/KernelTests/IcurveChartTests.cs:17-20`（唯一能区分比值项的 RV-CHART 参考值来自规格公式自算）
- 违反条款：§21.6「对本任务尤其不能把 icurve 参数化不一致作为『仅排序差异』降级，因为参数就是公开求值结果的一部分」；§21.1 Chart 行（非均匀弦递推）；§20.4（`NotRun` 不得并入 `Passed`）。
- 代码行为（主线已核）：正 49 边形的 $T_i\cdot e_{i-1} / T_i\cdot e_i \equiv 1$，即 Case F 只能钉住「Δt = C_i·f_0」这一标度，**无法区分 `f_i` 是否施加了余弦比**。推论：把 `OriginalChartParameterMap.cs:96-110` 的比值项整体删除（令 `f_i ≡ base_scale`），Case A/A2/B/C/E/F 仍全部通过，唯一变红的是规格自算的 RV-CHART 单测。此外全部 oracle 用例的 `baseScale` 来自脚本硬编码猜测（`:676` B 用例 `baseScale: 1`），真实 CHART 里解出的 `bp/bs` 在 Case C 被解析后**丢弃**。
- 建议（成本极低）：(a) Case A/B 直接断言 `TMin/TMax` 与 `PK_CURVE_ask_interval` 一致（这两个 case 已拿到 interval）；(b) Case F 增加一条**非均匀弦** chart 变体再同参数比较；(c) Case C 改用真实 `chartBuf` + 真实 `bp/bs` 解码；(d) 删除或改写 Case B 的 `chart-vs-PK=0` 恒等断言，避免被当成参数化证据。

### M6【Major，潜在（无生产调用方，GATE-A）】blend 包络的圆弧 v 校验硬编码截面 frame，与 §10.2 的 witness frame 不符

- 位置：`src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEnvelopeSolve.cs:37-49`（`TryValidate` 自行构造 `spineCenter=(R cos s, R sin s, 0)`、`radial=(cos s, sin s, 0)`、`zAxis=(0,0,1)` 后 `TryValidateArc(offset, radial, zAxis, ...)`，即 `v = atan2(offset·Z, offset·radial)/a`）
- 违反条款：§10.6「成功前**必须**恢复 spine 参数、接触侧、圆弧 $v$ 和允许区间」；§10.2「按 spine sense 确定截面起止接触点，设置 $X=(Q_{\rm start}-c)/r$」；§21.1 Tube 行。
- 代码行为（主线已核）：该模块确实只服务「XY 平面圆 spine」这一特例（其 4×4 残差也硬编码 `C(s)=(R cos s, R sin s, 0)`），但即便如此，§10.2 定义的 $Y=\operatorname{unit}(T\times X)$ 对圆 spine 且 $X=$ 径向时等于 **−Z**，而代码用 +Z ⇒ `v` 与文档约定反号；对非对称允许区间（如 `v ∈ [0,0.5]`）会误接受越界根 / 误拒合法根。更根本的是它既不接收也不使用 §10.2 的真实 `(X, Y, a, side)`，接触侧在 `BlendArcBounds` 中**完全没有表示**（只有 `arc/vMin/vMax/sMin/sMax`），`Arc==0`（`Unbounded`）时 `TryValidate` 直接放行 → 仍是「仅凭残差即 Success」的出口。
- 建议：把 `(X,Y,a,side)` 作为显式输入（或准备阶段预换算成 v），删除硬编码约定；`Unbounded` 不应伪装成成功出口。

### M7【Major，潜在】§10.3 frame 导数链式项无实现、无验证

- 位置：`src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEvaluation.cs:15-49`（`BlendFrameInput` 把 `XD1/XD2/YD1/YD2/ArcD1/ArcD2` 全部设为**必填**，无可用阶数标记）、`:82-132`（`Evaluate(order)` 只决定写几个输出，不改变对输入的 D2 需求）、`:166-202`（`TryBuildFrame` 只返回 `x,y,arc`）
- 违反条款：§10.3「frame 的导数由接触 witness、归一化向量和角度的链式求导获得；禁止差分整个嵌套 `EvaluateBlend` 作为默认实现。参数 D2 所需下层阶数由准备图计算，**不能硬编码『外层 D2，所以所有子节点 D2』**」；§16.4（jet 需求闭包）；§21.2（每个 residual 模块两条独立路径）。
- 代码行为（主线已核，含全库检索）：`grep -rn "XD1|XD2|YD1|YD2|ArcD1|ArcD2" src/` 在 `BlendEvaluation.cs` 之外**零命中** ⇒ 没有任何代码计算这些 frame 导数；所有夹具取 `a'=a''=Y'=Y''=0`，于是 `B_u` 的 $v a'V$、`B_uu` 的 $2va'V_1+va''V$、`B_{uv}` 的 $a'V-ava'E$ 三组项**从未被任何测试触及**，而它们正是 §10.3 相对平移不变情形的全部新增内容。公式本身经 lane 独立符号复核与规格一致（非算错，而是**未验证 + 机制缺失**），因此 T12「常规 R blend 参数式、接触 frame 与 jet」不能据此声明完成；能力清单第 4 节也没有 §10.1–§10.3/T12 行。
- 建议：实现 witness→frame 的链式求导（`X=unit(Q_start−c)`、`Y=unit(T×X)`、`a=atan2`）或至少在 `BlendFrameInput` 增加显式可用阶数并在缺阶时拒绝（禁止补零）；补一个 `a'≠0` 的多步长差分夹具。

### M8【Major，潜在（GATE-B 内）】距离组合入口没有距离类型约束，§21.1 的「平方隐式不得被距离组合接受」缺失且非 fail-closed

- 位置：`src/ProjectGmKernel.Native/Geometry/Evaluation/BlendBoundComposition.cs:27-54`（`CompositionInput` 的 d₀/d₁ 全是裸 double）
- 违反条款：§21.1 距离能力行「真 SDF 与平方隐式式对照 | **后者不得被距离组合接受**」；§8.2「`ImplicitJet` 则只声明零集合和导数。**不得以同一个结构体里的未标记 double 混用两者**」。
- 代码行为（主线已核）：`ImplicitJet` 与 `OrientedDistanceJet` 确实是两个类型、解析层内部按曲面类型算真距离（这部分已核实符合），但组合入口接受任意裸数：把 $\|x-c\|^2-r^2$、$2(x-c)$、$2I$ 填进去会被**静默接受**并产出某个数。现有最接近的测试是 §9.3 的 offset 冒牌式对照（证明几何不同），不构成距离路径的 fail-closed 证据；且该模块零生产调用方 ⇒ 今天不产生错误几何。
- 建议：入参改为 `OrientedDistanceJet`（或加 `DistanceGrade` 并拒绝非 Exact），补一条以平方隐式组装的**拒绝**断言。

### M9【Major，生产可达】缓存精确命中判据量纲耦合：大尺度模型 L2/L3 永不命中

- 位置：`src/ProjectGmKernel.Native/Runtime/GeometryEvaluationCache.cs:125`（`entry.ErrorEstimate > maxError` 即跳过）、`src/ProjectGmKernel.Native/Geometry/Caching/EvaluationSampleStore.cs:139`（L2 同规则）、`ICurveEvaluation.cs:86,189`（`ErrorEstimate ← report.Residual ← Norm(residualVector)`，I1 路径是 `|φ_other|`、球/柱是二次式、Refine 路径是**已缩放**残差）；阈值 `ICurveEvaluation.CacheErrorBound = 1e-11`（绝对常量，且请求级容差无表达通道）
- 违反条款：§13.4「精确 key 相同且**样本误差**、导数阶数、侧、版本和语义满足请求时才允许直接返回」；§15.2「缓存应按已有误差满足新预算时复用……不能让『命中了缓存』绕过当前精度检查」；§21.4「由松到紧容差执行」。
- 代码行为：`ErrorEstimate` 不是长度量（无 `GeometricDeviation` 那套局部尺度），却与绝对 `1e-11` 比较。对球/柱 `|φ|≈2R·δ`，发布门允许的偏离是 `1e-13·max(1,R)`、实际收敛后约 `ε·R²` 量级 ⇒ 半径约 **200 以上**（环面因四次式更早）时所有已发布样本都不满足命中条件，L2/L3 退化为「只写不读」。全部缓存测试用 R=1 夹具，因此不可见。
- 建议：把样本误差改为长度量（发布时的 `GeometricDeviation` 或 `‖J⁻¹r‖`），命中判据改为「样本误差 ≤ 请求容差 × 局部尺度」，并把 §15.2 的请求级容差接进 `TryFindExact` 的 `maxError`（§13.2 的 `ErrorEstimate` 与 `ResidualSummary` 本就应是两个字段）。

### M10【Major，文档诚实性】`docs/icurve_evaluation_capabilities.md` 仍有 7 行失实

详见 §6 的核对表。最严重的是第 70 行「XT INTERSECTION 实体写入与回读 = 生产已接入 + 真实 PK 兼容通过」：回读侧 `KernelRuntime.TryMaterializeICurveFromXt` 在 `src/` 内**零调用方**（仅 `tests/KernelTests/XtGeometryWriterTests.cs`），XT 接收主路径 `Runtime/XtReader.cs` 对 `INTERSECTION`/`CHART` **零命中**，而 oracle 自己写着 `NotRun: XT full INTERSECTION entity hydrate`。按 §20.4「`NotRun` 不能合并为 `Passed`」，该行必须拆分标注。

---

## 4. Minor（择要，按可操作性排序）

| # | 结论 | 文件:行号 | 违反条款 |
|---|---|---|---|
| m1 | 迭代耗尽时回传的 `residual` 是装配前（陈旧）值；`usedJoint` 在联合路径失败时残留 true | `BlendEnvelopeSolve.cs:121,191`；`BlendJointLift.cs:111,256` | §22.4 / §18.1（失败须可定位） |
| m2 | 3×3 计划丢弃 `planeNormal`（死参数），无 §7.4 要求的参数平面浮点一致性检查；装配失败折叠为 `Unsupported`（4×4 却透传精确状态） | `BlendEnvelopeSolve.cs:144-150,169-171,232-263` | §7.4 / §10.4 |
| m3 | 「收敛但被 §10.6 arc 判据拒绝」与「迭代未收敛」共用 `NotConverged`，测试固化了该混淆 | `BlendEnvelopeSolve.cs:107-108,177-178`；`BlendJointLift.cs:264-267` | §18.1（停止原因 vs 几何接受分离）/ §18.5 |
| m4 | dogleg 三处退化出口都返回 `Singular` → 一律映射 `ParameterizationSingular`；「stationary least squares」无表达位 | `TrustRegionStep.cs:60,87,116`；`ICurveCorrection.cs:124-135` | §18.1 / §14.5 |
| m5 | P2/P4 在 `order=0` 也做 (2,2) 支持面求值并计费（I1/I2/I3 已按阶门控） | `ICurveEvaluation.cs:930-936,1312-1323` | §16.4 / §19.4 / §22.2 |
| m6 | 几何/分支 guard 六个 `return false` 全塌缩，`detail` 保持 `None`（sheet/nappe/前向误差不可分） | `ICurveEvaluation.cs:586-649` | §14.6 / §17.1 / §19.1 |
| m7 | 未选面诊断在被拒（cone 远叶 / torus 镜像片）时静默为 `0.0`（语义=「恰好在面上」）；未选面求值与区间 D1/D2 求值未计入共享预算 | `ICurveEvaluation.cs:487-491`；`TerminatorEvaluation.cs:719-735` | §6.4 / §14.7 |
| m8 | 延拓步长纯比例调度：不看预测误差、无曲率/`x''` 界 | `ICurveContinuation.cs:70-80,146,158-164` | §17.2 / §17.3 |
| m9 | §19.3 的 checkpoint 契约未实现：`CommandScratch` 只有 `Take`/盲目 `Return`（不校验栈顶），无 `Checkpoint/Restore` | `CommandScratch.cs:34-55,69-74` | §19.3 |
| m10 | I1 无防消减二次根公式、无「两候选根都检查」；且 Auto 选中 I1 时实际走 continuation，报告仍写 `Plan = I1`（plan ID 与算法族不一致） | `ICurveEvaluation.cs:764-805`；`:234-237` | §7.5 / §7.6 |
| m11 | §7.6 四条建议规则只落实一条半（无 P4/blend 局部隐式/联合提升的自动规则）；`Select` 注释称 I2 比 I3 便宜，**项目自身基准显示 I2 慢约 7%** | `ICurveConstraintPlan.cs:58-104`；`temp_docs/icurve-evaluation/benchmark-latest.txt` | §7.6 |
| m12 | 预测样本（`PredictedOnly`）在失败请求后残留、占 L2 容量；`_ = cache.TryInsert(...)` 丢弃失败（满时已验证结果静默不入表） | `ICurveEvaluation.cs:259-261` 及 6 处 `_ =`；`EvaluationSampleStore.cs:225` | §13.7 |
| m13 | bracket 不筛 `MaxOrder`/`Plan`：D0-only 样本（`First=0`）参与 Hermite 预测 ⇒ 退化为 smoothstep，而规格要求「无可靠导数时用端点插值」 | `EvaluationSampleStore.cs:151-175`；`ICurveEvaluation.cs:244-262` | §13.5 / §13.3 |
| m14 | §13.5 周期展开未接线（`UnwrapWithLift` 生产调用方 0），Hermite 注释假定「已展开」但无校验 | `ParameterCorrespondence.cs:22-32`；`ICurveEvaluation.cs:248-254` | §13.5 |
| m15 | 区间认证测试名/注释声称 Unique/Empty，实际全部断言 `Unsupported`；`TryInvert3` 显式构造 3×3 逆矩阵（与 §14.2 禁令字面冲突，虽不可达） | `tests/KernelTests/IntervalRootCheckTests.cs:9-13,51-127`；`IntervalRootCheck.cs:144-164` | §18.4 / §14.2 |
| m16 | `reference_vectors.json` 的三阶收缩项含 `r₁` 因子，而 `CompositionInput.T` 不含（`r₁` 在 `Evaluate` 内乘）→ 若按名喂入会得 `r₁²T`；该字段无消费者 | `docs/icurve_design/reference_vectors.json`；`BlendBoundComposition.cs:101-106` | §11.1（一致性） |
| m17 | 死代码/失效守卫：`BlendEvaluation.cs:187-189` 永假分支；`BlendBoundComposition.cs:34` 下三角字段不读（静默对称化）；`JointBlendResidual.cs:93` `Scale(gradientA, 0)` 无用变量；`PseudoArclengthStep.cs:21-53` `n` 无上限 `stackalloc[256]`；`LevenbergMarquardtStep` 零调用方且 `predictedReduction` 无有限性检查 | 同左 | §23.2（禁止假成功）/ 可维护性 |
| m18 | 弱/恒等断言：`IcurveChartTests.cs:108-135` 两侧传同一 `tangent`（无法发现侧别误用）；`GeometryCacheTests.cs:188,196` `published` 只自增不断言；`IcurveNinthReviewRegressionTests.cs:118` `Assert.InRange(Used,0,Max)` 近乎无约束 | 同左 | §21.1 参数连续性行 / §21.4 |
| m19 | `report.Residual` 注释写「final max \|F\| in scaled units」，实际是未缩放 2-范数，且被当长度量与 `1e-11` 比较 | `ICurveView.cs:63` | §13.2 / §18.5（契约准确性） |
| m20 | chart 切向判据 `normSq > 1e-30` 非尺度不变：`R=1e-8` 双柱面任何交角都判 `Singular`；溢出时诊断为 `NonFiniteInput`（原因错位） | `KernelRuntime.PrepareEvaluation.cs:261,140-144` | §5.1 / §21.7 |
| m21 | XT 边界丢弃 §3.2 要求的「可选误差字段存在性」与 `ExtendedChartCount`（守卫成死分支）；导入失败仍是布尔折叠、无定位诊断；`ImportFlags` 无写入方 | `KernelRuntime.IcurveXt.cs:105-149,156-193`；`GeometryRecords.cs:507` | §3.2 / §5.2 |
| m22 | oracle 证据粒度：报告无 PK build/schema、无原始 XT 落盘、异常时不写失败报告（旧 PASS 文件原地留存）；`benchmark-latest.txt` 停留在 `HEAD=a025013` 且缺 CPU/构建模式/内存峰值 | `scripts/IcurveEvaluationOracle.cs:85-92`；`temp_docs/icurve-evaluation/benchmark-latest.txt:2` | §21.6 / §20.4 / §22.1 |

---

## 5. 前几轮修复的闭环复核（本轮实测/读码确认）

| 上轮项 | 状态 | 证据 |
|---|---|---|
| BlockSchurSolve 栈越界（M1） | **已闭环（主线已核）** | `BlockSchurSolve.cs:31-37` 强制 `nx+nz≤8`、`m==nx`、`schurWorkspace.Length ≥ nx²+nx+nx·nz+nz`；`nx+nz≤8` 时该需求上界恰为 64（`nx=7,nz=1`），调用方 64 元缓冲自洽；`JointSystemTests.cs:245-262` 有 `nz=5` 回归 |
| 联合残差丢弃求值状态（M2） | **已闭环（主线已核）** | `JointBlendResidual.cs:60-63` 对三个支持面求值状态逐一早退，失败面不再静默置零 |
| Schur 右端 `F_z b` 省略 / `H_z` 病态 | **已闭环** | `BlockSchurSolve.cs:79-124` 保留 `F_zb` 并真算 `innerResidualScale`；`H_z` 奇异返回 `Singular` 交回全系统 |
| SVD 用 `AᵀA` 判秩（M13） | **已闭环** | `TrustRegionStep.cs:69-77` 秩亏走 `SvdFactorizeSquare`/`SvdMinNormSolve`；`SmallLinearSolve` 单边 Jacobi + 尺度归一化 |
| XT null-UV 填零（M6） | **机制已闭环 / 证据未闭环** | `KernelRuntime.IcurveXt.cs:239-257`（`Empty → NaN`、null 成对）、`IcurveDecode.cs:120-138`（单分量 null 定位 `NullUvValue`）、`XtWriter.cs:1143-1149`（非有限写回 `Null()`）；但 oracle 全用例 `UvType = None`（`IcurveEvaluationOracle.cs:1020`）⇒ 真实 PK 空 UV 组合未验证（见 §6 第 71 行） |
| sense 写死 `+`（M7） | **机制已闭环 / 证据未闭环** | 写 `XtWriter.cs:723` 等、导 `KernelRuntime.IcurveXt.cs:364-372`、绑 `PrepareEvaluation.cs:53`；oracle 脚本对 `Sense` **零命中** ⇒ 负 sense 无真实 PK 用例（§6 第 72 行） |
| 同参数 oracle 比较（M8） | **部分闭环（主线已核）** | Case F 确已同参数比较（`IcurveEvaluationOracle.cs:380-381` → `oracle-latest.txt` `max|Δpos|=1.113E-015`）；但其余 case 仍以我方位置反推 PK 参数，且 §5.1 比值项无证据（M5） |
| CaseFValidator NaN/差分门（上轮 gpt#3） | **已闭环** | `git show dd73576`（共享 `TryComputeFiniteDifferenceD2`/`ValidateFiniteDifferenceD2` + 多例负向覆盖）；`oracle-latest.txt:31-33` 有 NaN/扰动拒绝日志 |
| terminator 分支跟踪/步长（上轮 gpt#1/#2） | **部分闭环** | 已改 λ 归一化连续跟踪 + 相对步长（`TerminatorEvaluation.cs:328-481`）；但新引入的判据仍是绝对常数/世界坐标地板（M4），且 `BracketedSolve` 还留着一处世界坐标残差接受（`TerminatorEvaluation.cs:614-615`） |

---

## 6. 能力文档核对（逐行）

| 行 | 声明 | 证据（调用方 / 脚本） | 判定 |
|---|---|---|---|
| 28 | 原始图表递推 …… **真实 PK 兼容通过** | 生产确有（`PrepareEvaluation.cs:147`），但 oracle 无判别性证据（M5） | **夸大**（改「制造解通过；真实 PK 参数化证据缺失」） |
| 30 | 强制计划 P4/P2/I3 **生产已接入** | `src/` 内 `EvaluateWithPlan` 零调用方；仅作 Auto 备选可达 | **夸大**（改「Auto 备选可达；显式强制仅测试/脚本入口」） |
| 31 | 节点一侧导数 …… 显式传递 `ChartSide.Left` | 生产入口恒用默认 `Right`；`Left` 仅 GATE-T 路径与测试 | **部分夸大**（标注生产只用 Right） |
| 40-43 | BlockSchur / SVD / LU-QR / dogleg | 主线已核（见 §5） | **属实** |
| 51 | `SampleWitness` …… **求解器生产/消费持续接入** | `new SampleWitness` 全库仅 `tests/KernelTests/GeometryCacheTests.cs:233`；生产构造全走无 witness 重载，读取侧从不读 `Witness` | **夸大**（改「载荷 + roundtrip 单测通过；生产未接入」） |
| 62 | Terminator 区间构造 …… **杜绝跨分支跳跃** | 实现是空间位移/法向夹角/中点偏差启发式（`TerminatorEvaluation.cs:426-452`），无分支身份证据 | **夸大措辞**（§23.2 禁止「只用空间距离识别分支」当充分条件） |
| 63 | Terminator 参数化 2×2 求解 | `SolveParametricTwoByTwo` 仅测试调用（`TerminatorIntervalTests.cs:492`），生产 `EvaluateTerminator` 恒走 `SolveIntervalPoint` | **部分夸大**（标「未接入的实验函数」） |
| 70 | XT INTERSECTION 实体**写入与回读** …… 真实 PK 兼容通过 | 写侧属实；回读侧 `TryMaterializeICurveFromXt` 仅测试调用、`XtReader.cs` 无 INTERSECTION/CHART，oracle 自述 `NotRun` | **夸大**（拆成「写侧通过 / 读侧未接线 / 完整 hydrate = NotRun」） |
| 71 | UV Null 语义 …… 真实 PK 兼容通过 | 机制属实；oracle 全用例 `UvType=None` ⇒ 无真实 PK 空 UV 证据 | **夸大**（改「制造/单元测试通过」） |
| 72 | Sense 双向保持 …… **Oracle 验证一致** | 机制属实；oracle 脚本对 sense 零命中 | **夸大**（删「Oracle 验证一致」） |
| 73 | Case F live PK receive/eval | `oracle-latest.txt:26-34` 同参数 D0/D1/rawD2 一致且**主动披露** raw D2 切向差 1.456E-002 | **属实（披露充分）** |
| — | §10.1–§10.3 / T12 参数式 blend | 第 4 节无对应行 | **缺失声明**（且 M7 表明能力确实未完成） |
| — | 第 63 行范围列「§11.2 / Joint assembly」、第 64 行「§10.2 / Local distance」 | 应为 §12.2 与 §11.1 | **章节引用错误** |

---

## 7. 规格要求但仍未交付的能力（本轮复核确认）

1. **§4.3/§4.4 依赖图与资源上限**：`MaxPreparedNodes`/`MaxOccurrences`/`MaxUnknowns`/`MaxWorkspaceBytes`/`DependencyCycle` 全库零命中；`PreparedGeometryGraph/PreparedGeometryView` 不存在（当前解析面切片无可遍历依赖，故非缺陷暴露，但 §4.3 的整体交付未完成，`GeometricOwner` 反向边规则也未实现）。
2. **§19.5 / §22.2 chart 映射跨调用复用**：每次 `PK_CURVE_eval` 都重建整张 chart（逐点一致性检查 + 2 次切向隐式求值 ⇒ Θ(m) 基础求值/次），无 session 级 prepared-chart 缓存；准备成本与单点校正成本**都没有分别统计**（`EvaluationCounters`/`EvaluationDiagnostics` 零生产调用方）。
3. **§13.1/§13.2 完整 witness 生产读写**：见第 51 行判定；`SampleWitness` 缺 `WitnessRole`/`EvaluationOccurrence`/`ParameterChartId`/`PeriodicLifts`/`NestedStateOffset`/`ValidityRegion`/`QualityClass`/`DefinitionRevision` 等字段 ⇒ §13.5 完整状态预测、§13.6 子层 witness 评分、§13.7 spine/原始段复用均无数据基础。
4. **§9 局部距离/offset 能力层**（`SurfaceDistanceEvaluation`/`LocalDistanceEvaluation`）：实现与公式已核实符合（含 offset 只接受 `DistanceGrade.Exact`），但零生产调用方（T11 未接线）。
5. **§6.3 参数曲面 2×2 路径**、**§12 联合提升**（`BlendJointLift`/`JointBlendResidual` 仅内部互引）、**§12.4 计划迁移**（`TryMigratePlanState` 无调用方）、**§17.4 伪弧长**（仅单测）、**§18.3 区间认证**（诚实 fail-closed 的桩）。
6. **§21.6 专项 oracle 用例缺口**：cone XT/PK 轴约定、负 spine sense 的 v 端映射、chart knot 处高阶导数、terminator 参数/值（GATE-T）均无真实 PK 用例；cone 轴约定的静态推导（XT 存储轴与 PK 轴反向）与代码「两侧都原样传递」冲突，需以真实 receive 夹具定案——**本轮不升为 Major，因既有 `ParasolidEvaluationOracle` 的 cone 用例长期通过，存在文档 Note 与实际行为不一致的合理解释**，但其结论必须落纸（§8 实测项 6）。

---

## 8. 需实测验证项（主线建议按此顺序执行）

1. **B1 并发复现**（最高优先，唯一 Blocker）：在多线程链上并发 `PK_CURVE_eval`（两线程、同段不同 `t`、循环 10^5–10^6 次），每次校验返回点同时落在两个支持面上且与请求参数一致；同时打印 `SessionData.Partitions[p].SharedReaders` 以证明并发确实发生。命令前缀：`MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests --filter FullyQualifiedName~IcurveConcurrencyStress && pkill -f '[t]esthost.dll' 2>/dev/null; true`。
2. **M1 列缩放反例**：`F=(0.5,0.5)`、`J=[[1,1e-9],[0,1e-9]]` 经 `Capture+Apply` 后断言 `‖trial−accepted‖` 是否等于 `‖D_u p‖`（当前等于 `‖p‖`）；端到端对照 `ICurveCorrection.Refine` 当前应报 `NotConverged/Stagnation`，改 `trial[i] += col[i]*step[i]` 后应一步归零。
3. **M9 缓存尺度反例**：把既有 R=1 夹具（`IcurveReviewRegressionTests.cs:158-166` 的 `CircleView(1)`）整体放大 `s∈{1,1e2,1e3,1e4}`，观察第二次求值的 `report.CacheHit`：预期 `s≥1e3` 起 `None`、`TryGetExact=false`（若 `s=1e4` 仍命中，请回传 `report.Residual` 实测值修正量纲推断）。
4. **M5 参数化证据**：跑 `P_SCHEMA=third_party/parasolid/schema dotnet run scripts/IcurveEvaluationOracle.cs`，在 Case A/B 加 `TMin/TMax == PK_CURVE_ask_interval` 断言、Case F 加非均匀弦变体；并做一次「删除 `OriginalChartParameterMap.cs:96-110` 比值项」的对照实验，预期只有 RV-CHART 单测变红。
5. **M4 terminator 尺度不变性**：把 `TerminatorIntervalTests` 的制造圆柱夹具整体乘 `s=1e-6`，比较相对精度是否与 `s=1` 同量级（预期退化 5–6 个数量级）；另构造 `R=1e-8`、`|E|≈1e5` 的选面用例，验证 `term_first` 是否被假奇异覆盖（预期 `SelectedSurface` 由 0 变 1）。
6. **cone XT/PK 轴约定**：用真实 PK 建 cone（半径 2、半锥角 0.25）→ `PK_PART_transmit_b` → 逐字段比较 `CONE.pvec/axis/radius` 与 `temp_docs/evaluation-oracle/cone.x_t`（当前 `location (0,0,0)/axis (0,0,1)/radius 2`）。接受则把「两侧均不翻转」固化为已验证兼容策略并写入文档；拒绝则按 §8.3 在 import/export 边界各翻转一次。
7. **零分配补强**：对 **icurve tag** 加一条热路径 `GC.GetAllocatedBytesForCurrentThread()` 循环用例（预热后为 0），否则 §22.3 在该切片只有构建期 IL 守卫这一条证据链。

---

## 9. 建议修复顺序

1. **B1 缓存并发**：先决定「降级为不可并发」还是「加锁 + 原子发布」。在决定前，任何并发使用都不得声称 L3 可信。
2. **M1 列缩放步长**（生产可收敛性）+ **M9 缓存量纲**（把 §15.2 请求级容差接进命中判据，顺带修 m19/m22 的字段语义）。
3. **M4/M3 terminator 判据尺度不变性与 sense 定向**：这是 GATE-T 将来关闭的判据基础，先修判据再谈关闭。
4. **M5 oracle 参数化证据**（成本最低、收益最大：Case A/B 的 interval 断言 + Case F 非均匀弦变体）。
5. **M6/M7/M2/M8 未接线模块的正确性**：在 T11–T16 接线前修，并补 §21.2 要求的多步长 + 方向收缩测试（现在计划级 Jacobian 完全没有独立校验路径）。
6. **M10 + §6 表格**：能力文档与真实接线状态对齐（这是本仓库历史上重复被审查命中的漂移点）。
7. **Minor 批处理**：m1/m3/m4（失败状态可定位）、m5/m12/m13（按需 jet 与缓存污点）、m7（预算漏计）、其余按 §4 表逐条清理。

---

## 10. 审查方法与边界

- 七路并行子代理按规格章节分工独立审查；主线对下列结论做了独立复核（不是转发）：B1 的全部四处证据链、M1（`FrozenResidualScale`/`DoglegStep`/`ICurveCorrection` 三方对读）、M2（手算反例）、M3/M4（`TryResolveTerminatorParameter`/`PrepareEvaluation.TryChartTangent` 对读 + 阈值公式代入）、M5（Case F chart 构造 + 唯一判别性测试定位）、M6（`TryValidate` 硬编码 frame）、M7（全库检索 frame 导数生产者为零）、M8（`CompositionInput` 字段类型）、M9（命中判据与残差量纲）、M10 与 §6 表格逐行（含 `TryMaterializeICurveFromXt` 调用方检索、`XtReader` 零命中、oracle 自述 `NotRun`），以及 §5 的三条闭环复核。
- 测试基线为**实跑**：535/535 通过（未跑的 oracle/benchmark 一律标 `NotRun`，未并入 `Passed`）。
- 本轮未做：真实 Parasolid oracle 运行、基准重跑、并发压测复现——这些均列入 §8 实测项。
