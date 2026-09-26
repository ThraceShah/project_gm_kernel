# ICurve/Blend 求值实现规格符合性复审（第二轮）

- 日期：2026-09-26（第二轮）
- 审查对象：HEAD `dd73576`，对照规格 `docs/icurve_design/icurve_blend_evaluation_spec.md`（v2.0，1480 行）
- 基线：第一轮报告 `temp_docs/icurve-evaluation/spec-review-2026-09-26.md`（HEAD `9970720`，14 项 Major）；其后 11 个提交（`1560087`..`dd73576`）声称修复 review 1–4 轮问题
- 测试现状：本轮实际运行 `MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests`，**535/535 通过**（allocation IL guard CLEAN，function catalog 83 项验证通过）
- 方法：六路并行子代理按规格章节分工 + 主线对全部 Major 级发现逐条独立复核（读源码/官方 XT 文档原文确认）

---

## 1. 总体评价

上轮 14 项 Major 中 9 项真实修复（M1–M7、M12、M13），修复质量普遍较高且带回归测试；5 项部分修复（M8–M11、M14，见 §2）。已接线的解析主线（chart 递推、五计划、Schur 消元、单边 Jacobi SVD、预算记账、发布原子性、GATE 隔离）经逐条公式核对无误，§23.2 禁止事项逐条审计未发现违例。

本轮确认 **1 项 Blocker**（L3 缓存静态裸指针 arena 无同步，而 `PK_CURVE_eval` 登记为 Concurrent 只读、允许并行进入——并发下数据竞争可发布损坏点；该发现来自同日另一会话的第 15 轮评审 `spec-review-round15-2026-09-26.md`，本轮主线独立核验属实，见 §3）。本轮自发现 10 项 Major，另有 3 项 Major 来自第 15 轮评审交叉核验后并入（合计 13 项），最重的一类是 **XT 与真实 Parasolid 的保真缺陷**：cone 轴向在写/读两侧都未按 §8.3 做恰好一次的反向（代码注释声称已做，实际不存在），属于与 oracle 对比时会直接出错的静默数据错误；另有 chart `parameter_error` 导入即丢弃、body 导入忽略 surface sense、B-surface 静默外插。第二类是**已接线代码的错误值面**（修正器把缩放空间步长直接加到未缩放状态、terminator 切向不乘 sense、非定义面残差混单位且失败时静默为零、圆弧 >π 被静默截断到短弧）。第三类是**测试诚实性**（第十一轮回归测试断言被 `if (Success)` 包裹，回归保护名存实亡）。能力文档仍有 4 行失实。

---

## 2. 上一轮 14 项 Major 修复核验

| ID | 内容 | 结论 | 证据（当前 HEAD） |
|----|------|------|--------------------|
| M1 | BlockSchurSolve 栈越界 | **已修复** | `src/ProjectGmKernel.Native/Computation/Numerics/BlockSchurSolve.cs:31`（`nx+nz>8‖nz<=0‖nx<=0‖m!=nx` 拒绝）、`:35` 工作区校验、`:51,:88` 缓冲扩至 64；唯一生产调用方 `BlendJointLift.cs:171-173` 传入充足。回归 `tests/KernelTests/JointSystemTests.cs:245,265,285` |
| M2 | 联合残差装配丢弃求值状态 | **已修复** | `Geometry/Intersection/JointBlendResidual.cs:28-33,58-62` 三个（四个）状态全部检查后返回；测试 `JointSystemTests.cs:290` |
| M3 | Envelope/Joint 无 §10.6 恢复即 Success | **已修复** | `BlendEnvelopeSolve.cs:37-49`（`BlendArcBounds.TryValidate` 组合进每个 Success 前）、`BlendJointLift.cs:236-270`（恢复梯子 + `TryUnwrapSpineAngle` 候选唯一性）。遗留：无界便捷重载（`BlendJointLift.cs:219-230`）仅供测试 |
| M4 | 2×2 参数化 terminator 解错平面方程对 | **已修复** | `Geometry/Intersection/TerminatorEvaluation.cs:638-705` Jacobian 两行 `aᵀ[S_u,S_v]`、`eᵀ[S_u,S_v]` 与 §6.3 逐项一致；测试 `TerminatorIntervalTests.cs:480`。仍无生产调用方（GATE-T 内部，能力行如实标注） |
| M5 | 选面法向回退不可达/算错点 | **已修复** | `TerminatorEvaluation.cs:239-252` 奇异性由梯度范数判定（`:230-231`），回退 `branchTangent = Unit(∇φ0×∇φ1)` 按弦定向；测试 `:507` |
| M6 | null-UV 填零、三种 null 约定冲突 | **已修复** | 统一 NaN 对哨兵：读 `Runtime/KernelRuntime.IcurveXt.cs:239-257`、解码 `IcurveDecode.cs:120-138`（拒绝单 NaN 与 ±Inf）、prepare `PrepareEvaluation.cs:226-228`、写 `XtWriter.cs:1149`；测试 `IcurveRuntimeEvalTests.cs:218`。遗留：±Inf 在写侧被转 Null（见 MIN-2） |
| M7 | sense 写死 '+'、导入不读 | **已修复（有缺口）** | 写 `XtWriter.cs:723-724` `SenseField(...)` 全站替换；读 `IcurveXt.cs:116-119` → 存储 `IcurveDecode.cs:181-183` → 解析面 `ApplySurfaceSense`（`IcurveXt.cs:364-372`）→ 曲线 `PrepareEvaluation.cs:53`。缺口：无 XT 字节级负 sense 往返测试，oracle 无 sense 用例（能力行 72 因此失实）；body 导入路径仍丢弃 sense（新 N8） |
| M8 | oracle 对参数化失明 | **部分** | Case F 已真同 t 比较（`scripts/IcurveEvaluationOracle.cs:381,409` `PK_CURVE_eval(pkCurve, ourT, 2, …)`，含 raw D1/D2 与切向/法向分解 `:427-446`）；A/A2/E 仍用我方位置 `Math.Atan2` 反推 PK 参数（`:131,198,251`），B/C 仅残差 |
| M9 | oracle 证据链不完整 | **部分** | Case F 状态诚实、含反例负测（`oracle.cs:524-607`）；证据仍只有文本日志（`WriteReport` `:87-92`），无原始 XT 字节/逐查询导数请求记录，且仅成功路径运行 |
| M10 | η_k 机制空转 | **部分** | 收紧分支真实重算（`ICurveCorrection.cs:137-155`，含 `InnerAccuracyInsufficient`）；但 `EvaluateSystem` 从不读 `InnerAccuracyFactor`，解析主线上重算是确定性空操作；§15.1 传播量 `‖F_z H_z⁻¹ H‖` 计算后被弃（`BlendJointLift.cs:188` `_ = innerScale;`）；§15.2 的 η_k clip 与"禁用降维路径"未实现 |
| M11 | 缓存样本无跨域对应关系 | **部分** | `SampleWitness` 结构存在并过 L2/L3 往返（`Geometry/Caching/EvaluationSampleStore.cs:34-58`）；但**生产零生产者零消费者**——所有发布点都传 `SampleWitness.None`（`Runtime/GeometryEvaluationCache.cs:282-285`；`ICurveEvaluation.cs` 7 处），P4/P2 解出的 UV witness 直接丢弃（`ICurveEvaluation.cs:1245,873`） |
| M12 | 多 seed 排序模块未接线 | **已修复** | `ICurveMultiSeed.BuildOrderedSeeds` 接入 `ICurveContinuation.cs:216-222` + `SameLocalBranch`（`:229,503`）；`SubdivideTo` 在生产阶梯上（`ICurveEvaluation.cs:343-350,373-378`）。`PseudoArclengthStep`/`ContinuityCellRules` 仍仅测试用，但能力行已不再声称 |
| M13 | SVD 秩判定用正规方程 | **已修复** | `SmallLinearSolve.cs:219-469` 单边 Hestenes-Jacobi 直接对 A 列旋转，σ 取缩放列范数（`:393-419`），秩 `σ>rankTol·σ_max`（`:437-444`），全程无 AᵀA；极端范围守卫（`:251-263,309-331`）；生产可达（`TrustRegionStep.cs:74-77`）。测试覆盖 1e200/1e-200 与 1e-320（`IcurveTwelfth…:150,173`、`IcurveThirteenth…:119`） |
| M14 | chart 构建诊断被丢弃 | **部分** | `KernelRuntime.Evaluation.cs:120,132-133` 捕获 `LastChartBuildFailure`；但失败分支不进 `ICurveEvalReport` detail（`:135-138`），`ICurveEvalDetail.InvalidChart` 零发射点（`ICurveView.cs:37`），`OriginalChartParameterMap.Build` 无失败 chord/node 索引 |

---

## 3. 本轮新发现

### Blocker（1 项，跨会话交叉核验）

**B1. `GeometryEvaluationCache` 静态裸指针 arena 零同步，与 `PK_CURVE_eval` 的 Concurrent 只读登记冲突（§13.9）**
- `Runtime/GeometryEvaluationCache.cs:56`（`internal static unsafe class`，`CachedCurveSample* arena` 裸指针 + 静态 `clockHand`），全文件无 lock/Interlocked；求值路径（`EvaluateICurveThroughL3`）在"只读"请求中做插入、CLOCK 引用（`:126,144`）与逐出写入（`:184-186`）
- `Runtime/FunctionCatalog.generated.cs:116`（id 19 `PK_CURVE_eval` → `ConcurrencyKind.Concurrent`）；`Runtime/ApiDispatch.cs:268,291`（非独占描述符可同时进入，`SharedReaders++`）——两个并发 `PK_CURVE_eval` 会同时读写同一静态 arena
- 规格原文（§13.9，line 869 附近）：缓存并发与内存语义是显式交付项
- 后果：并发只读调用下精确命中可返回被撕裂/污染的样本（数据竞争未定义行为），不是概率性偏差。**主线已独立核验三处源码**。来源：第 15 轮评审首先发现，本轮确认。

### Major（13 项）

**N1. cone 轴向在 XT 边界从未反向，注释声称已归一化（§8.3）**
- `Runtime/XtWriter.cs:790`（ConeNode 把 PK 约定 `data.AxisX/Y/Z` 原样写入 XT `axis` 字段）；`Runtime/KernelRuntime.IcurveXt.cs:337`（MaterializeCone 经 `FillAxis2` 把 XT `axis` 原样填入 `PK_CONE_sf_s.basis_set.axis`）；body 导入路径同样直通
- 规格原文（line 447）："cone 在 PK 接口的轴与 XT 存储轴反向。因此，准备阶段先明确数据属于 XT 原始约定还是内核已经归一化的 PK 约定；不可在 writer 与 implicit evaluator 各反转一次。"
- Parasolid XT Format Reference 原文（xt_chap.06）："At the PK interface, the cone axis is in the opposite direction to that stored in the XT data."
- 更严重的是 `Geometry/Evaluation/AnalyticImplicitEvaluation.cs:30-32` 注释断言"XT/PK 轴反向已在导入处恰好归一化一次（spec §8.3），本模块不再反转"——该归一化不存在。内核内往返自洽所以现有测试全部发现不了；与真实 Parasolid 互传时每个 cone 的轴向都是反的。**主线已复核：写读两侧源码 + 官方文档原文均确认。**

**N2. terminator 分支切向不乘 support sense，sense 为负的导入全数 `Singular`（§5.1）**
- `Geometry/Intersection/TerminatorEvaluation.cs:174-177`：`tangent = Unit(Cross(jet0.Gradient, jet1.Gradient))`，未应用 `Sense0/Sense1`；chart 路径有乘（`Runtime/KernelRuntime.PrepareEvaluation.cs:250-264` `SenseSign`）
- 规格原文（line 202）："其单位自然切向 T_i，由两个考虑 sense 的支持面法向叉积归一化获得"。注释（`:173`）自称"§5.1 construction"
- 后果：任一支持 sense 为负时 `forwardCosine`/`chordCosine` 检验（`:180-185`）必然失败，GATE-T 内部全部 terminator 查询静默失效（安全失败但功能断裂）。所有现有测试 sense=1，未覆盖。**主线已复核。**

**N3. frame 恢复把 |a|>π 的有向弧静默截断为短弧并返回 Success（§10.2）**
- `Geometry/Evaluation/BlendEvaluation.cs:192`：`arc = Math.Atan2(sinA, cosA)` ∈ (−π,π]，唯一退化守卫是 `|sinA|<1e-9`（`:197`）
- 规格原文（line 541）："以 atan2 恢复并展开 a。不能仅使用 acos 丢掉方向，也不能无证据地总取短弧。"
- 后果：物理弧 3π/2（sinA=−1, cosA=0）返回 a=−π/2 且 Success，v∈[0,1] 映到管的另一张物理面——静默错误几何值。参数路径无 lift/缠绕证据输入；隐式路径的 `TryValidateArc` 有 ±τ lift 但不在该路径上。测试只覆盖 |a|=π/2。**主线已复核。**

**N4. 非定义面偏差发布原始隐式值：单位混杂、失败时静默为零（§6.4 + §23.2）**
- `Geometry/Evaluation/ICurveEvaluation.cs:488-491`：`nonDefining = Math.Abs(otherJet.Value)`；求值失败时保持 `0.0`
- 规格原文（line 330）："未选面残差单独输出 NonDefiningSupportDeviation"；（line 1398）"禁止混用任意隐式值与 signed distance"
- 证据：plane φ 是长度量纲、sphere/cylinder φ=r²−R²（长度²）、torus φ 四次——发布值随面类型换单位且随半径放大；`GeometricDeviation`（`AnalyticImplicitEvaluation.cs:76-112`）存在且常规门在用，此处没用。规格命名的 `NonDefiningSupportDeviation` 全库不存在。测试 `TerminatorIntervalTests.cs:182` 只对单位圆柱断言 `>1e-6`。**主线已复核。**

**N5. chart `parameter_error` 导入即丢弃，再导出为 null（§3.2）**
- `Runtime/KernelRuntime.IcurveXt.cs:156-193`（TryReadChartNode 只读 fields[0..4] 与 hvec，[5]/[6] `parameter_error` 完全忽略）；`IcurveDecodeInput` 无对应成员；`XtWriter.cs:1089-1098`（`ParameterErrorProvided==0` 时写 Null——导入后恒为 0）
- 规格原文（line 101）：几何原始数据至少保存"……可选误差字段的存在性……"
- 后果：真实 Parasolid XT 里带的 chart 参数误差信息被单向销毁。存储字段存在（`GeometryRecords.cs:497-498`）但无导入管线。附带：`prefix != 7`（`:169`）比 schema（`parameter_error` 可选）更严。**主线已复核读写两侧。**

**N6. B-surface 出基本域静默夹紧+一阶外插，持久化数据却声明仅基本域（§9.5）**
- `Geometry/Evaluation/BSurfaceEvaluation.cs:40-85`（域外 (u,v) 夹到 knot 区间并加 `+ derivative·extrapolation` 一阶项）；`Runtime/XtWriter.cs:1208-1239`（SURFACE_DATA 扩展盒写 Null、'B' 标记）；`GeometryRecords.cs:394-440`（`BSurfaceData` 无原始/扩展范围字段）
- 规格原文（lines 512-514）："有效域不能始终等于原 knot 的基本区间……本期未支持的扩展语义必须明确拒绝，不能夹紧到边界。"
- 后果：求值有效域（无界线性外插）与持久化/传输声明的基本域脱节；导入带显式/隐式扩展域的 XT B-surface 时扩展信息整体丢失。

**N7. chart 切向以隐式梯度为法向，无参数 witness 对齐（§8.3）**
- `Runtime/KernelRuntime.PrepareEvaluation.cs:250-264`：`n = ∇φ·SenseSign`，无自然法向（Su×Sv）对齐或逐类 pin
- 规格原文（line 449）："使用隐式梯度作为法向时，以已知正则参数 witness 对齐自然法向，再应用 source surface sense。不要仅凭'半径为正'假定梯度方向就是该参数面的自然法向。"
- 当前 plane/cylinder/sphere/torus 各类恰好一致（手工核对 `SurfaceEvaluation` 基），cone 在 N1 修复前不可判定。一旦某类不一致，chart 切向翻转 → 整条导入 icurve 参数化手性翻转。

**N8. 常规 body 导入完全忽略 surface sense（§3.2）**
- `Runtime/XtReader.cs:112-224`（MaterializeBody/MaterializeCone/MaterializeSphere/MaterializeTorus 经 `BodyCreateSolid*` 重建，不读面节点 sense 字段，face sense 写死）
- 规格原文（line 103）："在 SurfaceRecord……保存正确的 surface sense。创建、导入、复制、写出、删除和 rollback 路径应同时覆盖。"
- 覆盖现状：创建（默认 +）、icurve 支持导入、写出、删除/rollback 均已覆盖；legacy body 导入是唯一丢 sense 的 importer（M7 的残余缺口）。

**N9. joint lift 无 frozen row scaling，用未缩放混单位残差的绝对 1e-12 判收敛（§12.2 + §14.1）**
- `Geometry/Intersection/BlendJointLift.cs:85-113`（`residual = MaxAbs(f)` 对 `BlendEnvelopeSolve.ResidualTolerance = 1e-12`）；`FrozenResidualScale` 只有 `ICurveCorrection.Refine` 在用
- 规格原文（line 731）："其数值尺度由 frozen row scaling 处理"；（line 883）"平方距离残差可按正的局部长度尺度转成相近量级"
- 六行残差混长度与长度²量纲；大半径/大 spine 时 `‖x−c‖²−r²` 行 O(r²)，1e-12 要么不可达（假 NotConverged）要么对长度行形同虚设。GATE-A 内部模块，接线前必须修。

**N10. 第十一轮回归测试全部断言包在 `if (status == Success)` 里，回归保护名存实亡（§24.2）**
- `tests/KernelTests/IcurveEleventhReviewRegressionTests.cs:49-57`（`Terminator_BranchTracking_MustNotJumpToWrongTorusCircle`）
- 规格原文（line 1414）："'Newton 在几个样本上收敛'……不构成完成"。求解器若回归为恒返 NotConverged，测试依然全绿。同文件其余正确范式见 `IcurveTwelfth…:59`、`IcurveThirteenth…:56`（先 `Assert.Equal(Success, status)`）。**主线已复核。**

**N11. 修正器把缩放空间的步长直接加到未缩放状态上（§14.1，跨会话交叉核验）**
- `Geometry/Caching/FrozenResidualScale.cs:45-54`（`Apply` 把系统变换为 `J_s = W_F·J·D_u`，右乘列缩放 `_col[j]`）；`Geometry/Intersection/ICurveCorrection.cs:120-123`（`DoglegStep` 在缩放空间解出 `step`）；`ICurveCorrection.cs:161`（`trial[i] += step[i]` 直接加进未缩放状态）
- 规格原文（line 883）：unknown scaling 与 row scaling 配套使用，解出的步长必须经 `D_u` 反映射回真实坐标
- 数学：`J_s·p_s = −W_F·r ⟹ J·(D_u·p_s) = −r`，真实 Newton 步是 `_col[j]·step[j]`；代码施加的是 `step[j]`。列缩放偏离 1 时（P4 两侧支持面 UV 尺度悬殊、I2 面内基量级不均）施加方向系统性偏离 Newton 方向，靠真实 ψ 比率/半径收缩兜底 → 收敛退化或假 `Stagnation`。发布点仍过 `IsPublishableRoot`，非错误值缺陷。**主线已复核（Capture/Apply/DoglegStep/施加四处源码）。** 来源：第 15 轮评审首先发现。

**N12. spun 子午面残差的梯度符号相反（§9.4，跨会话交叉核验）**
- `Geometry/Evaluation/SweptSpunImplicit.cs:57`：`gradientWrtPoint = Cross(wc, unitA)`
- 真梯度：`r(x) = ((x−P)×Â)·w_c`，由 `(dx×Â)·w_c = dx·(Â×w_c)` 得 `∇r = Â×w_c = unitA×wc`；代码返回 `wc×unitA = −∇r`。`:58` 注释的推导恰好把方向写反
- 残差本身正确、梯度差一个负号——接线后 Newton 行贡献反向。模块未接线（GATE 内部，自述未接入 prepare），但属确定性错误值。**主线已独立重推导核验。** 来源：第 15 轮评审首先发现。

**N13. 缓存命中判据量纲耦合：残差尺度 ErrorEstimate 对绝对 1e-11（§13.4/§15.2，跨会话交叉核验）**
- `Geometry/Caching/EvaluationSampleStore.cs:71`（`ErrorEstimate` 自述 "residual-scaled bound"，非长度量）；`Geometry/Evaluation/ICurveEvaluation.cs:392`（`CacheErrorBound = 1e-11` 绝对常数）与 `:179,227,464`（`TryFindExact(..., maxError, ...)` 直接比较）
- 规格原文（§15.2 line 988）：缓存按已有误差满足新预算复用——前提是误差量纲一致
- sphere/cylinder 残差是 r²−R² 量纲：半径 ≳200 时即便几何上极优的样本其 residual-scaled 估计也超 1e-11，L2/L3 永不命中（缓存静默失效）；与 MIN-7（混单位残差范数）同根。**量纲失配机制主线已核验；半径阈值推导引自第 15 轮报告。**

### Minor

1. **奇数 UV 数读未初始化栈内存**：`IcurveXt.cs:253-257` 奇偶校验循环 `i += 2` 在 `uvCount` 为奇数时读 `uvValues[uvCount]`（调用方 `:104` 传满尺寸 stackalloc，非越界 span 但未初始化）→ 畸形 INTERSECTION_DATA 的接受/拒绝不确定。应先拒奇数。**主线已复核。**
2. **±Inf 写侧静默转 Null**：`XtWriter.cs:1149` 任何非有限值（含 ±Inf）写 Null，解码侧却拒绝 Inf——内部若出现 Inf 会被"省略"而非报错。
3. **terminator 发布容差比常规门松 ~6 个量级且小尺度失效**：`TerminatorEvaluation.cs:486`（`1e-7·max(chord,1.0)`）vs `ICurveEvaluation.cs:719-725`（`1e-13·局部尺度`）；chord=1e-9 时容差是整个区间的 100 倍。
4. **terminator 路径泄漏未记账的基础求值**：非定义面 jet（`ICurveEvaluation.cs:489`）与导数 jet（`TerminatorEvaluation.cs:721,733`）不进共享预算；预算测试（`IcurveEleventh…:119-179`、`IcurveFourteenth…:77-141`）断言的精确 `Used==4` 恰因泄漏成立——测试锁定了泄漏。
5. **terminator 重根无隔离/奇异升级**：`TerminatorEvaluation.cs:585`（无变号即 `NotConverged`），§6.3 line 324 要求"重根必须转入隔离/奇异处理"。
6. **§6.3 参数化 2×2 仍是生产死代码**（`TerminatorEvaluation.cs:638-705`，仅测试调用）：规格的"只有参数面能力时回退 2×2"半接线；类注释与实际相反。
7. **混单位残差范数用于报告与 P4 早接受门**：`ICurveEvaluation.cs:1017-1024,899-907,1270-1287`，§5.3 line 261 要求检查几何长度残差而非混单位未缩放范数（发布正确性由 `IsPublishableRoot` 兜底，但报告值与 P4 预门混单位）。
8. **弦向 e 固定 E→B 方向**：`TerminatorEvaluation.cs:80`，§6.2 line 299 要求"以递增参数方向定义单位弦"；当前因符号不变性无后果，但与文档不符，未来消费者会踩。
9. **terminator 奇异性判定是绝对 epsilon**：`TerminatorEvaluation.cs:229-231`（`≤(1e-12·(1+|E|))²`），小尺度环面（主半径 ~1e-7）会被误判奇异；且按 §6.2 选择规则 `singular0&&!singular1→1` 优先于显式 `first`，假奇异会覆盖调用方的显式选面（第 15 轮报告 M4 指出的叠加后果）。
10. **frame 接受不在横截面内的接触点**：`BlendEvaluation.cs:178-192` 只查 `|‖Q−c‖/r−1|`，不查 `(Q−c)·T≈0`，切向分量导致 cos²+sin²<1 的静默错角。
11. **CaseF/blend FD 校验单一 步长**：`CaseFValidator.cs:90-134`、`scripts/IcurveEvaluationOracle.cs:369`（h=1e-5）、相关测试——§21.2 line 1317 明确"使用多个步长观察误差先降后升；单一有限差分步长过测不足以证明公式正确"。
12. **GATE-B 正交平面字面式反例无回归测试**：§21.1 line 1309 / §11.2 line 668 的反例（d0=x, d1=y → F_B=x−1）没有锁定测试，gate 只靠注释与文档状态守护。
13. **`BlendBoundComposition.Evaluate` 有限性门不全**：`BlendBoundComposition.cs:107` 只查 gradient/hxx/hzz，hxy 等四项 NaN 时返回 Success。
14. **Armijo 线搜索只有未用 helper**：`NewtonStep.cs:70-75` 零生产调用；所有生产 Newton 循环 α=1 全步（下降方向半满足、退化安全进信赖域，但 §14.3 指定的线搜索不存在于任何生产循环）。
15. **`RefineWithPlanSwitch` 死代码且会为每个备选计划新铸 4096 预算**：`ICurveCorrection.cs:224-251` 无任何调用方，违反 §14.7 line 960"每层独立 4096 禁止"；接线前应删除或修复。
16. **P4 不记录发布点的 ‖x0−x1‖**：`ICurveEvaluation.cs:1270-1274,1311-1336`，§7.1 line 361 要求记录。
17. **计划切换原因被覆盖**：`ICurveEvaluation.cs:285-299` 成功后 `detail=PlanSwitched` 覆盖失败原因；§7.6 line 417 要求记录切换原因。
18. **信赖域半径量纲不一致**：`ICurveCorrection.cs:93-101` 初值取未缩放状态范数，狗腿步在列缩放单位下度量；§14.1 的第一基本形式 UV 缩放不存在。
19. **Dogleg 秩亏分支 7–16 维系统会 Unsupported**：`TrustRegionStep.cs:22,69-78` vs `SmallLinearSolve.cs:230`（`n>6` 拒绝）——两个"最大系统"常量不一致（当前计划 n≤4 不可达）。
20. **未绑定 icurve 数据槽的变长块泄漏**：`KernelRuntime.cs:3153-3169`（`FinalReleaseEntity` 只释放 Curve/Surface 的变长载荷，`ICurveData` 槽经 `:3383` 释放但不释放 `HvecBlock/UvValueBlock`）；bind 失败路径（`PrepareEvaluation.cs:22-24`）的 undo 重放正落在此。
21. **遗留 null 哨兵混杂**：`SpunData.Start/End/XAxis` 以零向量为 null 双向传输（`XtWriter.cs:1357-1360`，真实零点被写成 null）；`StartParam/EndParam` 以 0=无界；`ExtendedChartCount` 注释说 −1=absent 但解码存 0 且字段死存储（`GeometryRecords.cs:494`）。
22. **导入溯源字段未闭环**：`ImportFlags` 零写入点；`SourceSchema` 只写不读（`GeometryRecords.cs:506-507`）。
23. **无 XT 字节级负 sense / 非 None UV 往返测试**：`IcurveStorageTests.cs`、`XtGeometryWriterTests.cs:187-303` 只断言 `'+'` 与 `UvType.None`——N1/N5/N8 均因此不可见。
24. **envelope/joint 模块不接预算**：`BlendEnvelopeSolve.cs:61-123,129-193`、`BlendJointLift.cs:59-113` 每迭代最多 6 次隐式求值不记账、上限 16 迭代超 §14.7 起始配置（8 次接受迭代）。
25. **区间认证测试名与断言相反**：`IntervalRootCheckTests.cs:52,65,78,91,105,118` 名称 `_Unique/_Empty/_Undetermined` 实际全部断言 `Unsupported/BoundsUnavailable`——认证被诚实禁用，但测试名描述的是从未交付的行为。
26. **每次求值全量重建 chart**：`KernelRuntime.Evaluation.cs:132` 每次 `PK_CURVE_eval` 重跑 `TryPrepareICurveView`；§19.5 line 1239/§22.2 line 1365 的"一次准备整个 chart 长期复用不可变参数映射"未实现（也无准备/校正成本分离统计）。
27. **L0 残差 memo 只写不读 + key 不完整**：`ResidualMemo.cs:70-72` 的 key 无 sheet/periodic lift/导数需求/内层精度（§13.3 line 816）；生产中 `TryFind` 零调用。
28. **D2 二次全状态预测与周期 lift 解缠绕休眠**：`ParameterCorrespondence.TryHermitePredict` 只用 D1；`UnwrapWithLift` 零生产调用（§13.5 line 831）。
29. **无入口选项对象**：`ICurveEvaluation.Evaluate`（`ICurveEvaluation.cs:37-39`）无精度/预算/缓存策略/诊断级别；`CacheErrorBound=1e-11` 写死（§19.2 line 1223、§15.2 line 988 的按请求预算复用不可实现）。
30. **§22.1 基准只测墙钟时间**：`scripts/IcurveEvalBenchmark.cs:159-264` 无基础求值计数/分解次数/拒绝次数/内存等（`EvaluationCounters` 结构存在但未接线）；§22.4 失败重放环 `FailureReplayRing` 未接线。
31. **§21 矩阵缺口**：terminator 尖点夹具（z=0, z=y²−x³，line 1308）、§21.4 变换语义与 L3 分配失败路径、§21.5 困难曲面（涟漪 B 面、参数尺度失衡、近切/近平行、混合 knot 连续性、宽有理权）全无测试。

### Nit

- 陈旧文档注释与实现矛盾（回归风险）：`SmallLinearSolve.cs:212-213`（自称"AᵀA 对角化"，实际单边 Jacobi）、`LevenbergMarquardtStep.cs:7-8`（自称正规方程增广，实际堆叠 [J;√λI]）
- `JointBlendResidual.cs:93` 死代码 `columnA = Scale(gradientA, 0)`；`BlendJointLift.cs:22-37` `Occurrence` 无生产用途
- `BlendEnvelopeSolve.cs:7-17` 连续两个 `<summary>`；`BlendImplicitTests.cs:1,3` 重复 using
- 能力文档缓存行称"键包含 Plan"，实际 Plan 只存储不参与匹配
- chart 节点包括端点在内全部要求单位切向（`OriginalChartParameterMap.cs:65-71`），比 §5.1"非正则点不构造切向"更严（安全拒绝）
- `TryMaterializeICurveFromXt` 对 `parameter_error` 只出现 1 次的 chart 拒绝（`IcurveXt.cs:169` prefix≠7），比 schema 更严
- `BlendImplicitEvaluation.cs:117-133` 假设单位/正交 axis & refDir 未验证（当前由 `CircleData` 不变量保证）
- §5.6 chart 哈希不变性无机制（结构上满足，无检查）

---

## 4. 能力清单失实/需修正行（docs/icurve_evaluation_capabilities.md）

| 行 | 现状 | 问题 |
|----|------|------|
| line 29（oracle） | "生产已接入 + 真实 PK 兼容通过 … Case A/A2/B/C/E/F 机器精度通过" | schema 定义为"完成同参数比较"，实际仅 Case F 同 t；A/A2/E 位置反推参数 + 单位切向，B/C 仅残差（M8 残余） |
| line 51（SampleWitness） | "求解器生产/消费持续接入" | 生产零生产者零消费者，全部 `SampleWitness.None`（M11 残余） |
| line 62（envelope/joint） | 未披露 | 实现只支持原点居中、XY 平面圆 spine（`BlendEnvelopeSolve.cs:206`），旧遗漏延续 |
| line 72（sense） | "Oracle 验证一致" | oracle 无 sense 用例；且无负 sense XT 往返测试 |
| §5 XT 行 | "写入与回读" | 写侧生产；`TryMaterializeICurveFromXt` 仅测试调用（`XtGeometryWriterTests.cs:292,379,463,540`），未接入常规 body 接收路径 |
| η_k 行 | "触发收紧后重算系统模型与残差" | 重算在解析主线上不可能改变任何值（M10 残余），措辞暗示有效机制 |

另：上一轮失实行（memo/η_k 定位/伪弧长/连续性 cell/回放/共享 spine/2×2）已如实修正；GATE 表与 `compatibility_gates.json` 一致且诚实。

---

## 5. 规格要求但整体缺失的能力（择要）

1. §4.3 依赖图（显式栈 DFS、occurrence 模型、拓扑输出、`DependencyCycle`）与 §4.4 其余限额（`MaxPreparedNodes/MaxOccurrences/MaxUnknowns/MaxWorkspaceBytes`）——现为手写双解析支持固定准备
2. §8.3 cone 轴恰好一次归一化（N1）与梯度-自然法向 witness 对齐（N7）
3. §9.1/§9.2 一般参数/过程曲面的法向坐标足点求解（现仅解析）；§9.4 swept/spun 的 `v=D·(x−C(u))` 恢复与接线（`SweptSpunImplicit` 自述未接线）；§9.5 扩展域存储与域绑定拒绝（N6）
4. §10.2 spine witness → 支持面 UV 对应存储（`SharedSpineMemo` 只存 spine 点）；§10.3 witness → frame jets 的链式求导管线（`BlendFrameInput` 无生产构造点，参数 D1/D2 公式不可达）
5. §11.3 GATE-B 最小 oracle 实验与 `BlendBoundRoleMap` 产物；§11.5 接触边界一维求解与 y-辅助联合形式
6. §14.3 生产循环内 Armijo 线搜索；§14.5 LM 生产调度；§14.6 短窗口可观测量（步余弦/最小奇异值）
7. §15.1/§15.2 敏感度传播与 η_k clip 规则、按请求容差
8. §16.4 准备期 jet 需求闭包/逐类能力表/workspace 布局；§16.5 D3+ 截断 Taylor 扩展
9. §18.2 `NumericallyVerified`/`CertifiedLocalRoot` 可请求分级；§18.3 保守区间认证首版（机制在、入口全拒）
10. §19.5/§22.2 准备 chart 持久复用；§22.1 全量观测计数器；§22.4 内核侧失败重放
11. §21.5 困难曲面夹具整行；GATE-T 的真实 PK t_E 夹具证据尚不存在（gate 诚实开放）

---

## 6. 已验证符合的重点（抽样）

- **§5.1 chart 递推公式逐项一致**（f 递推、t 递推、弦单位、无 Abs/无删点/无 base-scale 修正），对照外部参考向量 RV-CHART（`IcurveChartTests.cs:53-63`）；§5.5 单侧 D2 区分实测
- **§6.1–6.4 分类先于求解、两平面构造、一维优先、第四方程不施加、无第四次方程的验证契约**（含 `BeyondChart_WithoutRule_IsGatedWithoutOutput` 无部分输出的 gate 语义）
- **§7 五计划维数与 Jacobian 布局**（P4 4×4 一侧输出、P2 2×2、I3 3×3 + 横转性门、I2 固定基、I1 线上标量解）与 Auto 切换纪律（只在完整失败后切换、重算不携带状态）
- **§12.2 六未知系统 Jacobian 代数正确**（δw 公式、Mᵀq−w 块）；**§12.3 Schur 公式**（S=F_x−F_z W、恢复步用未收敛 b、病态内块回退全系统）；**§12.5 无真循环**（深度 8 细分 + 64 步上限）
- **§14.2 小线性代数**：主元 LU（n·ε·max|A|，非 double.Epsilon）、列主元 QR、无任何正规方程；**单边 Jacobi SVD** 含极端动态范围与次正规守卫；§14.4 狗腿三分支与 0.25/0.75/0.1 半径策略
- **§14.7 预算**：生产路径单请求共享预算横跨首选计划、全部备选、延拓与细分；五计划逐一"跑才记账"（`IcurveP4ChargeOrderTests` 行为验证）
- **§10.3/§10.4/§10.5/§11.1 数学逐项精确**：B 的 D1/D2 每项无缺无符号翻转（独立重推导核对）；D 投影隐式 Hessian；BLEND_BOUND 组合 Hessian 的 D3 收缩为强制输入（结构上不可丢）
- **§13.8 版本/ABA**：`ModelGeometryEpoch` 在 create/delete/rollback/session 边界单调推进，缓存写不推进，tag+generation 双检查
- **§18.1 停止与接受分离**（`IsPublishableRoot` 独立门含前向误差代理）；**§18.5 全有或全无发布**（失败 D2 不发布半结果；detail 词表齐全且不发明 PK 常量）
- **§23.2 禁止事项逐条审计无违例**：无 chart 重采样、邻缓存不当精确值、分支身份不按空间距离、terminator 不施加第二面、隐式值与符号距离不分型混用、迭代内无 Runtime 查询、无 parent/child scratch 重叠、停滞/小步不发布为成功、高阶不补零、奇异不加 epsilon 掩盖
- **§17.6 近似模型仅作加速器**：预测只做种子必再校正；GATE-A/B/T/D 四门全 `open` 且与代码行为一致

---

## 7. 测试与证据现状

- 本轮实跑：`MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests` → **535/535 通过**（0 失败 0 跳过，3s）
- oracle 证据：`temp_docs/icurve-evaluation/oracle-latest.txt` 为最近一次运行产物（Case F 同 t：max|Δpos|≈1.1e-15，raw D2≈3.7e-15，切向 D2 差 1.456e-2 已披露跟踪）；本轮未重跑真实 PK oracle，其结论沿用既有产物
- 测试诚实性问题集中三处：N10（断言包裹）、MIN-25（名实相反）、MIN-4（预算测试锁定泄漏）

## 8. 建议处理优先级

1. **B1 缓存并发**：给 L3 arena 加同步（或把 Concurrent 只读请求限制为纯读路径/线程本地化），补并发回归测试——在其它修复前这是唯一可能产生未定义行为的项
2. **已接线求解器错误面**：N11 D_u 反映射（含信赖域半径量纲统一，MIN-18 一并修）→ N13 缓存命中量纲 → N4 非定义面残差
3. **XT 真实保真**：N1 cone 轴（写读两侧 + body 路径，修注释，补 Parasolid 往返 oracle 用例）→ N5 parameter_error → N8 body sense → MIN-23 负 sense/UV 往返测试 → MIN-2 ±Inf 策略
4. **其余错误值面**：N2 terminator sense → N6 B-surface 域拒绝 → N7 witness 对齐 → N3 弧展开、N12 spun 符号、N9 joint row scaling（GATE 内部模块，接线前必修）
5. **测试诚实性**：N10 → MIN-25 测试改名 → MIN-4 预算测试改含泄漏修复
6. **上轮遗留 partial**：M8（A/A2/E 同 t 比较）+ M9（证据链）→ M10/M11（真机制或能力行降级）→ M14（诊断入 report + `InvalidChart` 发射）
7. **缺失能力按规格里程碑**：§4.3 依赖图 → §18.3 区间认证 → §10.3 frame-jet 管线 → §16.4 闭包 → §22 观测
8. **死代码清理**：MIN-15 `RefineWithPlanSwitch`、Nit 组（陈旧注释/死变量/重复 summary）

另：第 15 轮报告（`spec-review-round15-2026-09-26.md`）中本轮未独立复核的 M5（§5.1 余弦比递推无真实 PK 证据）、M6（envelope 截面 frame 硬编码，与本报告能力行 line 62 同源）、M8（距离组合入口无距离类型约束）建议一并纳入修复清单；其 M3/M4 与本报告 N2/MIN-9 重叠。

## 9. 审查方法说明

六路并行只读子代理分工：A=上轮 M1–M14 修复核验 + 能力行；B=§5/§6；C=§7/§12/§14/§10.6/§18.1–18.2；D=§10/§11/§16/§21.2；E=§3/§4/§8/§9/§13.8；F=§13/§15/§17–§23 + 能力文档全表审计。主线对全部新 Major 及关键 Minor 逐条读源码复核（含 Parasolid XT 官方文档原文比对），修正了一处子代理误报（`TryMaterializeICurveFromXt` 并非零调用方，为仅测试调用）。汇总阶段发现同日另一会话的第 15 轮评审（`spec-review-round15-2026-09-26.md`，同 HEAD），其 Blocker 与三项 Major 本报告六路未覆盖——已逐条主线独立核验（B1 缓存并发三处源码、N11 缩放步长四处源码、N12 梯度符号独立重推导、N13 量纲失配机制）后并入；其 M5/M6/M8 未独立复核，仅在 §8 引用。测试套件本轮实际运行。所有发现均给出 file:line 与规格条款；未运行的验证（真实 PK oracle 重跑）明确标注沿用既有产物。
