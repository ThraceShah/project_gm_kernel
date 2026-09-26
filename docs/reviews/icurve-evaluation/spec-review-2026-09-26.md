# ICurve / Blend 求值实现对照规格审查报告

- 审查日期：2026-09-26
- 对照规格：`docs/icurve_design/icurve_blend_evaluation_spec.md`（v2.0，2026-09-12）
- 审查基线：`9970720`（规格基线 `5c645cdf` 之后 79 个提交）
- 测试状态：`MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests` → **493/493 通过**
- 分工：6 路并行审查（§5 chart / §6 terminator / §10–12 blend / §13–15 缓存与数值 / §7–9+16,18,19 计划与编排 / §2–3+21 存储与测试证据），关键发现由主线抽查复核（BlockSchurSolve、JointBlendResidual、XT null-UV、terminator 2×2、XtWriter sense、BlendEnvelopeSolve）。

## 1. 总体评价

已接线的“解析支持面常规 icurve”切片（chart 重建、P4/P2/I3/I2/I1、D0–D2、L0–L3 缓存、共享预算、发布原子性）与规格的数学定义高度一致，多处公式逐项核对无误，`reference_vectors.json` 制造向量被真实消费。GATE-A/B/T/D 的隔离是诚实的：生产路径对 gate 未关闭的构造返回精确拒绝，无静默猜测。

主要问题集中在四类：
1. **若干模块的“成功契约”缺步骤**（envelope/joint 不做 §10.6 恢复即 Success；joint 残差忽略求值状态可假收敛）。
2. **XT 感知数据管道单向**（sense 写死 `+`、null-UV 填零、三种 null 约定冲突），与 §2.4/§3.2 相悖。
3. **oracle 对参数化失明**，恰是 §21.6 明确禁止降级的检查项，导致“参数化正确”这一核心声明缺证据。
4. **一批已实现但未接线的模块**（MultiSeed、ResidualMemo.TryFind、η_k 收紧、line-search、诊断环）让能力清单出现失实行。

无发现会导致当前已接线路径直接发布错误值的 Blocker；但有多处潜在崩溃与假收敛路径，接入更大系统即触发。

## 2. Major 发现

### M1. BlockSchurSolve 栈缓冲区可越界崩溃（潜在）
`src/ProjectGmKernel.Native/Computation/Numerics/BlockSchurSolve.cs:41-48`。契约允许 `nx+nz ≤ 8`（nz 最大 7），但 `hzCopy = stackalloc double[16]`，随后按 `nz*nz` 读写（nz=5 → 25 > 16），直接 IndexOutOfRange（AOT 内核中接近进程中止）。`schur`/`rhs` 切片按 `nx` 尺寸分配却按 `outerCount` 索引，无 `m == nx` 检查。当前唯一调用方 nz=3 不受影响。规格 §12.3、§4.4（工作区按准备尺寸精确给出）。两路审查独立发现。

### M2. 联合残差装配忽略求值状态，可假收敛
`src/ProjectGmKernel.Native/Geometry/Intersection/JointBlendResidual.cs:27-29`。`AssembleResidual` 丢弃 `AnalyticImplicitEvaluation.Evaluate` 的三个返回状态；失败时 `jet = default`（全零）静默写入残差行 0/1/4 且 w=0，`BlendJointLift.TryLiftFromLocalSingular` 的收敛判定（BlendJointLift.cs:92-94）可能接受“全零残差”。同文件 `AssembleJacobian`（:52-56）检查了状态——唯独决定 Success 的残差路径没有。规格 §18.1/§18.2：残差为零必须是真实定义残差。

### M3. Envelope/Joint 求解器不做 §10.6 恢复即发布 Success
`src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEnvelopeSolve.cs:42-62,88-108`、`src/ProjectGmKernel.Native/Geometry/Intersection/BlendJointLift.cs:92-94,209-232`。SolveFourByFour/SolveThreeByThree 仅凭 (E1,E2,φ_A,p) 残差收敛返回 Success；规格 §10.6 明确“成功前必须恢复 spine 参数、接触侧、圆弧 v 和允许区间”（T13 验收“整圈错误根拒绝”）。`BlendImplicitEvaluation.TryValidateArc`（:195-212）存在且单测覆盖，但没有被组合进任何成功路径；接触侧恢复完全不存在。整圈对径片、v 越界的伪装根与真实 blend 点在 API 边界无法区分。（当前未接入 Auto 生产路径，属潜伏。）

### M4. Terminator 参数化 2×2 回退解错平面方程对（死代码 + 能力失实）
`src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:442-476`。规格 §6.3 要求 Jacobian 两行 `a^T[S_u,S_v]` 与 `e^T[S_u,S_v]`（残差 `a·(S−E)=0`、`e·(S−Q(t))=0`）；代码实际用 `f[0]=(S−Q)·v`（法向为 v，非 e）与 `f[1]=(S−Q)·(ChordRate×v)`（∝a），约束线退化为弦线而非 `Q+μv`。正确残差 `ChordPlaneResidual`（:101）在同文件中闲置。该函数**零调用方**（生产只走 SolveIntervalPoint），XML 注释“witness 失败回退 1D”与代码（返回 Unsupported）不符。能力清单第 28 行“+ parametric 2×2 | Supported”失实。

### M5. Terminator 选面法向回退不可达，且若可达则算错点
`src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:236-242`。规格 §6.2：w 无法定义时改用 branch point 的曲线切向。守卫 `if (!IsFinite(supportNormal))` 永假（`Unit()` 对任何输入都返回有限向量，零输入返回 (0,0,0)），回退分支不可达，退化情形直接落成 `Singular`；即便可达，回退取的是 endpoint 处两面梯度叉积而非 branch point 的曲线切向。§6.2 文档化路径从未执行，无测试。

### M6. XT 导入把 null-UV 填零，三种 null 约定互相冲突
`src/ProjectGmKernel.Native/Runtime/KernelRuntime.IcurveXt.cs:228-253`：XT `Empty` 槽位 `uvValues[i] = 0`；全 null 数组把 `uvType` 改写为 `None`、count 归 0（丢失 §3.2 要求保存的 UV stride/总长度）；部分 null 整体拒绝且无定位诊断。同时 `KernelRuntime.IcurveDecode.cs:118-120` 声明“null 哨兵是 NaN”并拒绝之，`KernelRuntime.PrepareEvaluation.cs:217-223` 把 ±Infinity 当作 null 直接跳过 UV 一致性检查。UV (0,0) 是合法参数，规格 §2.4/§3.2 明确“`?`/null 不得统一替换为零”。XT 边界路径无任何测试（全部 writer 测试用 `UvType.None`）。

### M7. Surface/curve sense 生命周期断链（写死 `+`、导入不读）
- `src/ProjectGmKernel.Native/Runtime/XtWriter.cs:738,761,785,811,835,860,935,959,984,1009,1276,1298,1321,1358`：全部解析面 sense 字段硬编码 `Char('+')`，忽略 `SurfaceRecord.Sense`——负 sense 面写出即被抹掉（INTERSECTION 节点 :1043 正确写了 `curve.Sense`）。
- XT 导入不读 INTERSECTION sense 字段；`KernelRuntime.PrepareEvaluation.cs:44` 硬编码 `curve.Sense = positive`。
- `XtReader.cs` 全文件无 sense 处理。
规格 §3.2 要求 create/import/copy/write/delete/rollback 全路径覆盖 source sense。`IcurveStorageTests.cs:138-154` 的“导入可存负 sense”断言直接戳字段，未经任何真实路径。

### M8. Oracle 对参数化失明，缺失 §21.6 唯一关键比较
`scripts/IcurveEvaluationOracle.cs`：Case A/A2/E 用**我方**求值位置反推 PK 圆的角度再比较（位置检查退化为径向残差）；切向按单位向量比较（抹掉速度）；`CompareD2`（:825-850）在速度不同时退化为参数化不变量；Case F（:372-373）明确按最近采样点比较并注释“参数化不必一致”。全库没有一处 `our eval(t)` 对 `PK_CURVE_eval(same t)` 的同参数比较——而 §21.6 点名“不能把 icurve 参数化不一致……降级，因为参数就是公开求值结果的一部分”。错误的 §5 f_i 递推可以通过现有全部 oracle 用例。

### M9. Oracle 证据链不完整 + Case F 状态误标
`temp_docs/icurve-evaluation/oracle-latest.txt` 只有聚合误差；§21.6 要求的“原始 XT、逐查询参数与导数请求、真实输出、我方输出、误差、PK build、schema”均缺（XT 字节在内存中即丢弃；全库无 PK build/schema 记录）。Case F 实测 `max|Δpos|=1.205e-2` 超过 1e-4 门槛却被标为 `NotRun:`（正文有“not Pass”不算冒充通过，但实测失败归入 NotRun 是状态误标，且该 1.2e-2 重物化偏差是真实未关闭的兼容偏差）。`scripts/IcurveEvaluationOracle.cs:73-74` 只在成功路径写报告，异常时留下上一轮的 stale 证据。

### M10. η_k / 内层精度机制整体空转（能力清单三行失实）
- `src/ProjectGmKernel.Native/Geometry/Caching/EvaluationBudget.cs:28-43` 的 `InnerAccuracyFactor`/`TightenInner` 只在 `ICurveCorrection.cs:139` 被当启发式下限读取，无任何求解 API 有可收紧的容差参数。
- `BlockSchurSolve.cs:101-112` 计算出 §15.1 的传播量 `‖F_z H_z⁻¹ H‖`，唯一调用方 `BlendJointLift.cs:178` 直接 `_ = innerScale;` 丢弃；§15.1 约束 `‖W_F F_z H_z⁻¹ r_H‖ ≤ η_k‖W_F F‖` 从未强制。
- `ICurveCorrection.cs:137-148` “tighten 后重评”分支实际什么都不重评（`continue` 用同一冻结残差/雅可比再走 dogleg）。
能力清单第 18/19/26 行（“SVD + η_k tighten”“Nested η_k … tighten before radius shrink”“Shared evaluation budget + η_k”）夸大。规格 §15.2 的“从缓存值继续 refinement”也不存在（超误差界的精确样本直接丢弃重解），且 `CacheErrorBound = 1e-11` 为编译期常量，请求级“由松到紧容差”无法表达。

### M11. 缓存样本没有 §13.1 的跨域对应关系
`EvaluationSampleStore.cs:30-60` 的 `CurveSample` 与 `GeometryEvaluationCache.cs:29-46` 的 `CachedCurveSample` 只有位置+jet：无支持面 UV witness、无 blend u/v+arc、无 spine 参数/原始段、无 offset UV/基接触、无嵌套 witness；`WitnessRecord`（§13.2）任何形态都不存在；SampleHeader 的 ModelEpoch/DefinitionRevision/BranchId/PeriodicLift/QualityClass/ConditionEstimate 等字段缺失。P2/P4 校正解出的 witness UV 在发布时被丢弃。§13.1 是“缓存对象是解的对应关系，不只是空间点”，这一交付缺失同时使 §13.5 完整状态预测、§13.6 分支/连续性过滤无法落地。

### M12. 确定性多 seed 排序模块未接线
`ICurveSeedSelection.OrderSeeds` / `ICurveMultiSeed.BuildOrderedSeeds`（4 个同分支 seed、exact→邻近→插值→chart 锚点→UV→局部搜索的确定序、AmbiguousBranch 判定）单测齐全但**零生产调用方**；真实路径（`ICurveEvaluation.cs:232-360`）是即席的单 seed（弦点或单个 Hermite 预测）+ 计划轮换 + 延拓/细分。规格 §19.1 伪代码的“build a small deterministic seed list … for each permitted seed”编排与 §13.6 排序算法未进入求值路径。行为确定、分支安全，但规格的控制流未实现（同理 `ContinuityCellRules`、`PseudoArclengthStep` 也只有单测）。能力清单第 21/22 行有夸大。

### M13. SVD 秩判定用正规方程 AᵀA（§14.2 禁止项）
`src/ProjectGmKernel.Native/Computation/Numerics/SmallLinearSolve.cs:230-291`：`SvdFactorizeSquare` 显式构造 AᵀA 再 Jacobi 旋转。这正是秩亏 dogleg 恢复（`TrustRegionStep.cs:69-77`）和最小范数步走的路径，恰是病态场景；条件数平方效应使 `MachineEpsilon·n·σ_max` 的秩阈值不可靠。规格 §14.2：“不得默认构造 J^T J 处理病态问题”。标准修法是 one-sided Jacobi。

### M14. Chart 构建诊断被丢弃，模块自身发布契约为假
`OriginalChartParameterMap.cs:14-17` 注释声称 `ChartBuildFailure`“Published inside the eventual ICurveEvalReport (§18.5)”，但 `KernelRuntime.Evaluation.cs:129`、`KernelRuntime.PrepareEvaluation.cs:49` 均以 `out _` 丢弃 `chartFailure`；`ICurveEvalDetail.InvalidChart` 定义后全库零引用。`Build` 也不返回出错点/弦索引（解码失败有 `failureIndex`）。规格 §5.2 要求各失败模式“输出可定位诊断”。

## 3. Minor / Nit（择要）

- §5.2 “稳定范数与补偿求和”未实现：弦长用朴素 Dot（>1.35e154 溢出被误报 `ZeroLengthChord`），t 链为普通累加（`OriginalChartParameterMap.cs:78,113`）。
- 诊断分类混淆：余弦分母小与符号不一致同报 `MisalignedTangent`；t 溢出误报 `ParameterResolutionLost`（`OriginalChartParameterMap.cs:90-118`）。
- Chart 锚点以 `CorrectedRoot` 入缓存（`ICurveEvaluation.cs:191-193`、`GeometryEvaluationCache.cs:280-283`），§5.4/§13.4 的来源标识规则只靠“发布时 D0 必为锚点”侥幸成立；证明该不变量的测试手工构造了生产路径不会产生的 `ImportedChartAnchor`。
- Terminator 重根/相切根未转隔离/奇异处理（`TerminatorEvaluation.cs:335-391` 无变号即 NotConverged；斜率守卫 `>1e-300` 形同虚设；接受仅凭残差，无 §18.2 条件估计），重根位置误差 ~√tol。
- Terminator 根选择是启发式（`bound = 4.0*ChordLength` + 最近 μ=0 括号），非规格的“本端 branch witness”；多根分歧用例未测。
- `SelectSupportSurface` 的 BLEND_BOUND 例外在唯一生产调用方被 `surface0IsBlendBound: false` 写死（`TerminatorEvaluation.cs:232`）——规则与 24 用例矩阵正确但无法触发。
- Terminator 参数规则 ID 不进缓存 key（`EvaluationSampleStore.cs:30-42`），不同 t_E 规则的样本可互相误用（现有封装每次新缓存，暴露有限）。
- Blend 弧恢复恒取短弧：atan2 结果无展开/证据通道（`BlendEvaluation.cs:186-201`），真弧 3π/2 会以 −π/2 返回 Success；§10.2 “不能无证据地总取短弧”为无条件禁令。
- 局部隐式脚点无分支守卫（`BlendImplicitEvaluation.cs:122-154`）：圆 spine 对径脚点可通过 D 比守卫收敛，产生错误片的 d_B；sense 盲乘，无“确认对应实际圆弧片”。
- Plan 反向迁移缺 §12.4 验证（`BlendJointLift.cs:118-143` joint→elim 直接 atan2，无局部隐函数检查，重置义务推给不存在的调用方）。
- BlendBound 高阶输出无能力门（`BlendBoundComposition.cs:57-110`）：调用方缺 d1-D3 时传 T=0 仍收 Success Hessian，无“非精确”标记（§11.1/§16.4）。
- GATE-B 拒绝路径缺“精确未支持原因”（`KernelRuntime.PrepareEvaluation.cs:95` 裸 `Unsupported`；`UnsupportedBlendConstruction` 定义后零发射）。
- §12.2 “frozen row scaling”只存在于注释（`JointBlendResidual.cs:15`），lift 路径无任何行缩放。
- 线搜索 Newton 未实现，`NewtonStep.ArmijoSatisfied/BacktrackAlpha` 死代码（§14.3）；§14.3 的步前域/连续性 cell 限制也缺。
- L0 memo 只写不读（`ResidualMemo.TryFind` 全库零调用），key 缺 occurrence/sheet/periodic lift/精度类（§13.3）；能力清单第 13 行“Wired into corrector trials”半真。
- `SharedSpineMemo` 未接线且 key 缺 offset（§13.7 禁止合并不同 offset 的子调用）。
- 诊断/计数/FailureReplayRing 未接入任何求值路径（§22.4 的重放字段也缺）；`temp_docs/icurve-evaluation/` 无失败重放产出。
- 取消检查在大循环/子计划入口缺失（§14.7）；`ICurveCorrection.cs:178-179` 接受步后对同一状态重复计费基础求值。
- 便捷重载每次铸造新的 4096 预算（`ICurveEvaluation.cs:527-534,584-589`、`ICurveCorrection.cs:46-54`）——正是 §14.7 禁止的“每层独立 4096”形态（当前调用方为死/测试代码）。
- §14.1 第一基本形式未知量缩放缺失（P2/P4 以原始 UV 步长求解）。
- I1 稳定二次求根公式（`q=−½(b+sign(b)√…)`）未实现，现为通用 8 步 Newton（§7.5 的“例如”措辞留有余地）；仅访问一个根，“两候选根检查”不存在（分支正确性由延拓保证）。
- §19.1 顺序在 I1 情形倒置（`ICurveEvaluation.cs:232-235` 单平面直接进延拓，SolveI1 只在 CorrectAt 内可达）。
- 锥 XT/PK 轴“恰在导入处归一化一次”不存在任何翻转点（`AnalyticImplicitEvaluation.cs:30-32` 注释与 XtWriter/IcurveXt/XtReader/Evaluation 四处逐字传递矛盾）——要么注释为假，要么真实 XT 锥会被物化到错误半锥（§8.3/XT25 p.58）。待与真实 XT 夹具对证。
- 锥 `GeometricDeviation` 用 |ρ−g| 而非真垂距 |ρ−g|/√(1+k²)（保守，导致过严接受，非错误根）。
- B-surface 越界求值无条件线性外推（`BSurfaceEvaluation.cs:37-87`），无 §9.5 要求的“显式拒绝未支持的扩展语义”路径。
- Swept/Spun 消元为半成品（单残差替代 E^T 双方程；spun 无角度恢复/periodic lift），模块自述未接线。
- 局部距离初始化/停止阶梯不完整（无缓存脚点预测、无周期 lift、无多脚点/片逃逸停止；K 病态整调用返回 Singular 而非“D0 可用/D2 不可报”分报，§9.2）。
- 区间认证为诚实 stub（`IntervalRootCheck.TryCertifyI3/TryCertifyP2` 恒 BoundsUnavailable），§18.3 首版 K(X) 压缩算子未实现；内部角点采样用裸 binary64，若直接启用非严格。
- 数值差分交叉验证普遍单步长（`AnalyticImplicitTests.cs:34-87`、Blend 系列测试），§21.2 要求多步长观察误差先降后升。
- Nit：`ExtendedChartCount` 哨兵注释与解码约定矛盾；`ImportFlags` 死字段；`ICurveData` 若干裸 int 计数不符别名惯例；`LevenbergMarquardtStep.cs:6-7` 注释与实现相反；测试文件重复 using/未用变量等编译警告。

## 4. 能力清单失实行（docs/icurve_evaluation_capabilities.md）

| 行 | 声明 | 实际 |
|---|---|---|
| 13 | L0 residual memo “Wired into corrector trials” | 只写不读，TryFind 零调用 |
| 18/19/26 | “SVD + η_k tighten” / “Nested η_k …” / “Shared budget + η_k” | η_k 机制空转，innerScale 被丢弃 |
| 21 | Internal pseudo-arclength augment | 仅单测，无求值路径调用 |
| 22 | Near-tangent → Singular（continuity-cell） | 仅单测；近相切实际落 NotConverged/Stagnation |
| 28 | “+ parametric 2×2” Supported | 死代码且方程错误 |
| 29 | R-blend envelope/joint “Internal + Schur lift” | 未披露实现只支持原点圆 spine（XY 平面） |
| 27 | Failure replay + counters | 模块未接入任何求值 API |
| 16 | Shared-spine memo “Multi-parent keys” | 未接线且 key 缺 offset |

第 46 行（L2 witness “partial”）自我披露充分，属实。GATE 四行全部属实且诚实。

## 5. 规格要求但整体缺失的能力

1. §13.1/§13.2 witness 对应关系存储（WitnessRecord、跨域 witness 链）。
2. §16.4 jet 需求闭包 / PreparedGeometryGraph（§3.4 的 PreparedGeometryGraph.cs、PreparedGeometryView.cs、ICurveValidation.cs、GeometryCacheVersion.cs 均不存在）。
3. §12.2 求解后 c 的段/参数回映、spine 分支验证与缓存登记；§12.5 循环依赖检测。
4. §4.2 能力位标记声明层（ImplicitValue≠LocalOrientedDistance 目前靠结构体分型维持，无声明层）。
5. §18.3 区间认证首版（K(X)、向外舍入、压缩判据）。
6. §14.3 线搜索与步前域/cell 限制；§14.6 震荡短窗诊断与“近反向步→收缩域”“UV 大位移小→换坐标”等缓解。
7. §9.2 局部距离完整初始化/停止阶梯；§9.4 spun 角度恢复；§9.5 B-surface 扩展域拒绝路径。
8. §13.6 接线的确定性 seed 循环（MultiSeed 现为死模块）。
9. §5.2 无 UV 锚点反求（解析面直恢/参数面有界投影/过程面子图复用）与周期展开保存。
10. §21.5 困难曲面测试：波纹 B-surface、混合 knot 连续性、大权域 rational、近邻平行/交叉分支。
11. §21.4 变换语义测试（均匀缩放 λ 的弦长/scale 规则、支持面换序/反射/sense）。
12. §22.1/T20 基准行：随机扫描、同 spine 多父、困难尾部、内存峰值、CPU/构建模式记录（文档已声明未达标，属实）。

## 6. 已验证符合的重点（抽样）

- §5.1 递推公式逐项正确（含 f 下标范围、末点无 f_{m-1}），RV-CHART 参考向量独立复算一致（~1e-16）。
- §5.3 参数平面残差/FMA 锚定差值/逆映射 ψ_i 精确；§5.5 D1 切向公式与 D2 侧别不平均，节点左右 D1 连续性有测试。
- §5.4 chart 节点位精确返回原始锚点，无宽容差吸附；chart 重建无 Abs(dot) 掩盖、无删点、无改 base scale。
- §6.1 分类先于求解器选择，六种 QueryKind 齐全；limit-T 的 hvec 语义与 §2.4 UV 布局（含 (N_chart+N_term)·k 长度、不重复 branch point、顺序偏移）解码侧完全正确且有 12 用例矩阵。
- §6.2 选面规则（含“显式 first 不覆盖两个例外”）与 24 用例矩阵一致；两平面构造、退化 e×w 拒绝（无世界轴替代）正确。
- §16.3 terminator 导数在解析根处求值（d67c3e1 修正后）与规格公式一致，柱面闭式 + 中心差分双路验证。
- GATE-T/GATE-B 隔离真实：生产入口 Unresolved → `Unsupported`+`CompatibilityGateOpen`，零发布；BlendBound 无生产消费者、无下标交换、无“小残差择优”解释。
- §7.1–7.4 P4/P2/I3/I2 的 F/J 逐项与规格矩阵一致；I3 横截性含 e·切向检查；I2 基固定每段、Q′=e/f 进 F_t；P4 单侧 x0 输出、无中点替代。
- §8.1 五类解析式与守卫（双锥半侧、apple/lemon 镜像片拒绝）正确；§8.2 ImplicitJet/OrientedDistanceJet 分型，|φ|/‖∇φ‖ 不作距离；§8.4 仿射梯度/Hessian 与均匀缩放距离门正确。
- §9.1 法向坐标消元、∇²d 经线性解（无显式求逆）；§9.3 offset 只认 Exact 级距离，φ−a 冒充被测试拒绝。
- §10.1–10.5 公式逐项正确（含 B_uu/B_uv/B_vv 全部 θ 项、(E1)_s 无条件装配且离根测试、φ_B 与 d_B 分字段不混用）；§10.6 D 比判据为相对量、无 epsilon 加分母。
- §11.1 三阶收缩项作为必需输入不可省略；sphere/cylinder D3 非零三阶导数测试满足 §21.2 特例要求。
- §12.2 六方程与 δw 方向导数精确（RV-LIFTED-J 钉住）；§12.3 Schur 保留 F_z b、H_z 奇异保全系统、无显式逆。
- §13.4 近邻命中仅预测、PredictedOnly 永不精确命中、位精确 key；§13.8 单调 epoch + slot generation 防 ABA 有测试；§13.9 L3 会话级 arena、CLOCK 淘汰、不占 CommandScratch。
- §14.4 dogleg 公式、ρ 判据、阈值；§14.5 LM 增广 QR 非正规方程；§14.1 机器精度用 2^-52 命名常量、无 double.Epsilon 误用；冻结缩放遵守 trial 内冻结。
- §14.7 共享预算接线正确（首选计划/轮换/延拓/细分共用一预算，近邻检查与 P4 求值按实计费——最近四个提交），耗尽 → `BudgetExceeded` 非 `Unsupported`；起始配置 8/20/4/64 齐全。
- §18.5 原子发布（D0 成功但 D2 失败 → 整请求零发布）有测试；八个 detail code 齐全；§18.2 前向误差门 ‖J⁻¹r‖ 参与验收。
- §18.4 区间认证诚实拒绝，无 BitIncrement 伪区间三角、无随机扰动冒充认证。
- §19.3/19.4 workspace checkpoint/finally、哨兵测试、零分配 IL guard（1137 方法 CLEAN）、静态分派。
- §24.3/AGENTS.md：oracle 走 PKToy/ParasolidScriptHost 无 P/Invoke 复制、pskernel 未重定向到自有内核；脚本路径相对；测试命令规范。

## 7. 测试与证据现状

- 493/493 通过；`IcurveFirst…TenthReviewRegressionTests` 十轮回滚保护齐全。
- §21 矩阵覆盖良好段：Chart、参数连续性、五计划等价、解析守卫、Tube、联合/Schur、terminator 制造例、缓存冷/热/乱序/ABA。
- §21 矩阵缺口段：BlendBound 正交平面字面反例（RV-BBOUND-ROLE 在 reference_vectors.json 有数据但零消费）、§21.3 UV 位置级测试（start-only/end-only 行偏移、单 null 保留）、§21.4 变换语义、§21.5 困难曲面多数行、§21.6 全部专项 oracle 用例、§21.2 多步长差分。
- 证据文件：T00 基线审计详实；oracle-latest.txt/benchmark-latest.txt 存在但（见 M8/M9）不满足 §20.4/§21.6 的证据粒度。

## 8. 建议处理优先级

1. **假收敛与崩溃面**（M1 BlockSchurSolve 缓冲、M2 残差状态、M3 §10.6 恢复）——先修，因为一旦接入更大系统即产生静默错误值。
2. **数据保真**（M6 null-UV、M7 sense 生命周期）——XT roundtrip 是对外契约，且阻碍 oracle 对证。
3. **证据有效性**（M8/M9 oracle 同参数比较 + 证据链 + Case F 状态修正）——关闭“参数化正确”声明的唯一途径；与 M7 的 sense 修复、锥轴约定（Minor 中 §8.3 项）联动做真实 XT 夹具对证。
4. **契约与文档对齐**（M4/M5 terminator 死代码修复或删除、M10 η_k 接线或降级声明、M12 seed 循环接线或改写能力行、M14 诊断发布）。
5. 之后按 §20.3 里程碑补齐缺失能力清单（§5 第节），并在每轮保持 `docs/icurve_evaluation_capabilities.md` 与真实接线状态同步。

## 9. 审查方法说明

- 六路并行只读审查，各路对照指定章节逐条核对公式与语义，并交叉核验能力清单相关行。
- 主线对 6 处 Major 证据做了独立抽查复核（BlockSchurSolve 缓冲尺寸、JointBlendResidual 状态处理、innerScale 丢弃、ResidualMemo 调用图、XT Empty 填零、terminator 2×2 方程对、XtWriter sense 硬编码、BlendEnvelopeSolve 成功路径），均与报告一致。
- 测试由主线全量运行（493/493），blend/terminator 子集由对应审查路各自运行验证。
