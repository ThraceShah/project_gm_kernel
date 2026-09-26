# ICurve / Blend 求值实现 · 规格符合性综合归并审查报告

- **日期**：2026-09-26
- **审查基线**：Git HEAD `dd73576b584f8a06ba7f9f1396b8751563420179`
- **对照规格**：`docs/icurve_design/icurve_blend_evaluation_spec.md`（v2.0，1481 行）
- **输入来源**：
  1. GLM-5.3 审查报告（`docs/reviews/icurve-evaluation/spec-review-2026-09-26-r2.md`）
  2. DeepSeek-V4.1-Flash 第 15 轮审查报告（`docs/reviews/icurve-evaluation/spec-review-round15-2026-09-26.md`）
- **验证方法**：主线对两份报告的所有 Blocker、Major、文档失实行及关键 Minor 进行 100% 源码定位、公式推导、量纲分析与调用链交叉复核。
- **测试实测现状**：`MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests` → **Passed 535 / Failed 0 / Skipped 0**（Allocation IL guard CLEAN，Function catalog 83 描述符通过）。

---

## 1. 总体评价与合理性复核结论

两份外部 Review 报告具有极高的准确度与深度，对 HEAD `dd73576` 的问题定位吻合度达 95% 以上。经主线全量源码逐行核验与数学复算，**确认两份报告提出的核心问题全部属实，定性基本合理**。

### 1.1 核心性质澄清与分层
归并时对两份报告的发现按严重度与可达性进行了严格分层，避免将“内部未接线模块”与“生产可达崩溃”混为一谈：
1. **Blocker（1 项）**：**L3 缓存静态裸指针 arena 零同步 + `PK_CURVE_eval` 声明为 Concurrent 只读**。这是当前唯一在生产公开 API 上可致内存数据竞争与返回撕裂结果的缺陷。
2. **生产可达数值与求值缺陷（4 项）**：修正器未反乘列缩放 $D_u$、缓存 `ErrorEstimate` 与绝对常量 $10^{-11}$ 比较致大半径下永久不命中、非定义面使用原始隐式值且失败时静默为零、Terminator 容差世界坐标与绝对常数地板。
3. **XT 与真实 Parasolid 数据保真缺陷（4 项）**：Cone 轴向在 XT 读写两侧均未反向（注释声称已反向）、Chart `parameter_error` 导入即丢弃、Legacy Body 导入全量丢弃 Surface Sense、B-Surface 域外静默一阶外插。
4. **未接线/门禁内部模块的数学错误（5 项，接线即错）**：Spun 子午面残差梯度符号完全相反、Terminator 分支切向叉积未乘 Sense、Blend 恢复使用 `Atan2` 截断短弧、Blend 包络圆弧 $v$ 校验硬编码 Frame 与见证 Frame 冲突、Blend Joint Lift 混单位残差未施加 Row Scaling。
5. **测试诚实性与虚假通过缺陷（2 项）**：回归测试断言包裹在 `if (status == Success)` 中导致恒失败仍全绿、区间认证测试名与断言相反。
6. **证据链与能力清单失实（6 项能力失实 + 4 项证据缺口）**：Case F 正 49 边形等弦图表无法验证 $\S5.1$ 余弦比递推、Frame 链式导数无任何生产者、能力矩阵 6 处过度陈述。

---

## 2. Blocker（1 项）

### B1. L3 缓存静态裸指针 Arena 零同步，与 `PK_CURVE_eval` 的 Concurrent 只读登记冲突（§13.9）
- **位置**：
  - `src/ProjectGmKernel.Native/Runtime/GeometryEvaluationCache.cs:56-62`（`private static CachedCurveSample* arena;`、`private static MemoryPageIndex clockHand;`，全文件无锁、无原子操作、无内存屏障）
  - `src/ProjectGmKernel.Native/Runtime/KernelRuntime.EntryPoints.cs:15`（`Dispatch(ApiId.CurveEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ...)`）
  - `src/ProjectGmKernel.Native/Runtime/ApiDispatch.cs:270-295`（只读非排他调用递增 `SharedReaders++`，允许多线程并发进入）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:227-233`、`:517-522`（`cache.TryFindExact` 命中后执行 `PublishHit`，将 `hit.Position/First/Second` 原样拷入调用方 span，**无任何有限性与几何校验**）
- **违反条款**：§13.9 缓存并发一致性契约；§13.7 样本验证后发布；§18.5 统一原子发布。
- **机理与后果**：
  在 `GeometryEvaluationCache.EvaluateICurveThroughL3` 路径中，若未命中，计算线程调用 `Publish` → `ClaimSlot`（原地修改 `clockHand`）并调用 `Overwrite` 逐字段写入条目字段。写入非原子操作，且先写 Key 字段再写几何数据。
  并发的另一个查询线程在 `TryGetExact` 遍历 arena 时，可在条目写入中途读取：此时 Key 已匹配，但几何数据尚未写毕或属于上一代历史数据。`PublishHit` 不做任何几何核验直接作为结果发布，导致多线程环境下概率性发布撕裂或陈旧点。
- **修复方案**：
  - 短期：若当前版本定位为单线程，将 `PK_CURVE_eval` 的调度属性由 `ConcurrencyKind.Concurrent` 改为 `ConcurrencyKind.Local` 或声明单线程限制。
  - 彻底解决：在 `GeometryEvaluationCache` 中引入无锁槽位版本号（Version-based Lock-free）或读写短锁；`clockHand` 使用 `Interlocked`；条目写入使用 Release 内存屏障，读取使用 Acquire 屏障；`PublishHit` 补充基础有限性检查。

---

## 3. 生产可达的数值与求值缺陷（Major，4 项）

### N1. 修正器未将缩放步长反向映射回真实状态空间（$D_u \cdot p$ 未施加）（§14.1）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Caching/FrozenResidualScale.cs:45-54`（`Apply` 将系统右乘列缩放 $J_s = W_F J D_u$，其中 `_col[j]` 即 $D_u$）
  - `src/ProjectGmKernel.Native/Geometry/Intersection/ICurveCorrection.cs:120-123`（`DoglegStep` 在缩放空间求解得出 $p$）
  - `src/ProjectGmKernel.Native/Geometry/Intersection/ICurveCorrection.cs:161`（`for (BufferOffset i = 0; i < n; i++) trial[i] += step[i];`）
- **违反条款**：§14.1 未知量缩放契约（$\Delta y = D_u p$）。
- **数学复核**：
  缩放后线性系统为 $J_s p \approx -r_s \iff (W_F J D_u) p \approx -W_F r \iff J (D_u p) \approx -r$。
  真实状态变量的下降位移必须是 $\Delta y_i = D_{u, i} \cdot p_i = \text{\_col}[i] \cdot \text{step}[i]$。
  当前代码直接将 $p_i$ 累加到未缩放的 `trial[i]`，导致状态空间更新方向完全偏离求解模型的牛顿方向。列尺度悬殊时（如 P4 计划中 UV 参数量级与弧长量级不均），预测下降与实际下降严重失配，导致步长缩减并假性停滞（`Stagnation`）。
- **修复方案**：
  在 `FrozenResidualScale` 中暴露列逆缩放接口或方法 `UnscaleStep(Span<double> step)`，在更新 `trial` 前执行 `step[i] *= _col[i]`；同步检查信赖域半径与 `minRadius` 在缩放空间的量纲一致性。

### N2. 缓存精确命中判据 `ErrorEstimate` 量纲耦合，致大半径模型永久不命中（§13.4 / §15.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Caching/EvaluationSampleStore.cs:71, 139`、`src/ProjectGmKernel.Native/Runtime/GeometryEvaluationCache.cs:125`（`entry.ErrorEstimate > maxError` 则跳过）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:392`（`CacheErrorBound = 1e-11` 为绝对常数）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:305, 510`（存入的 `ErrorEstimate` 赋值为 `report.Residual`）
- **违反条款**：§13.4 缓存精度比较必须使用长度量纲；§15.2 按请求容差复用。
- **机理与后果**：
  `report.Residual` 取的是无量纲或平方隐式残差范数（如圆柱/球面的 $r^2 - R^2 \approx 2R \cdot \Delta r$）。
  当模型半径 $R \ge 200$ 时，即使空间误差 $\Delta r \approx 10^{-13}$（机器极限），残差 $2R \cdot \Delta r \approx 4 \times 10^{-11} > 10^{-11}$。
  这导致已收敛的合法点在再次查询时因 `ErrorEstimate > 1e-11` 被永久判定为精度不足，L2/L3 缓存退化为“只写不读”。既有测试全部使用 $R=1$ 夹具，掩盖了该缺陷。
- **修复方案**：
  缓存样本记录欧氏几何偏差（`GeometricDeviation`），或将 `CacheErrorBound` 乘以局部几何尺度 $\max(1.0, R)$；在 `TryFindExact` 中将请求容差作为参数传递。

### N3. 非定义面残差使用原始隐式值、求值失败静默为 0、遗漏预算记账（§6.4 / §23.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:488-491`
- **违反条款**：§6.4 未选面残差输出契约；§23.2 禁止隐式值与有向距离混用；§14.7 基础求值记账。
- **机理与后果**：
  1. `nonDefining` 直接取 `Math.Abs(otherJet.Value)`。平面为长度、圆柱/球为长度平方、环面为 4 次方，量纲完全不统一且无法反映真实几何间隙。
  2. 若 `AnalyticImplicitEvaluation.Evaluate` 失败（如点处于锥的远叶或超出曲面有效域被 Guard 拦截），`nonDefining` 保持初始值 `0.0`，静默伪装成“在面上完全无误差”。
  3. 该调用未计入 `budget.TryConsume(1)` 和 `evaluations++`，存在预算漏记。
- **修复方案**：
  改调 `AnalyticImplicitEvaluation.GeometricDeviation` 获得真实的欧氏距离；失败时赋为 `double.PositiveInfinity` 或实际测得的偏差；接入预算扣除与计数。

### N4. Terminator 发布容差与奇异判据使用绝对常数及世界坐标地板（§6.2 / §6.4）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:229-234`（`GradientNormSq <= (1e-12 * (1 + |E|))^2`）
  - `src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:486`（`tol = 1e-7 * Math.Max(anchor.ChordLength, 1.0)`）
- **机理与后果**：
  1. 奇异判定使用世界坐标 $|E|$ 加权，使得同一几何在平移远离原点时被判定为奇异，进而覆盖用户的显式选面（§6.2 规则：`singular0 && !singular1` 优先于 `first`）。
  2. 发布容差采用 `1.0` 绝对地板，当弦长为微小尺度（如 $10^{-6}$）时，容差为 $10^{-7}$，相当于区间尺度的 10%，相对精度严重退化。
- **修复方案**：
  奇异性改为归一化梯度的投影或局部无量纲判据；容差按特征尺度严格等比例放缩，移除绝对常数 `1.0` 地板。

---

## 4. XT 与真实 Parasolid 数据保真缺陷（Major，4 项）

### N5. Cone 轴向在 XT 读写两侧均未反向，且存在误导性虚假注释（§8.3）
- **位置**：
  - `src/ProjectGmKernel.Native/Runtime/XtWriter.cs:790`（直接写入 `data.AxisX, data.AxisY, data.AxisZ`）
  - `src/ProjectGmKernel.Native/Runtime/KernelRuntime.IcurveXt.cs:337`（MaterializeCone 直接将 XT 轴填入 `sf.basis_set.axis`）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/AnalyticImplicitEvaluation.cs:30-32`（注释声称“XT/PK 轴反向已在导入处恰好归一化一次，本模块不再反转”）
- **违反条款**：§8.3 Cone 轴向 PK/XT 约定转换；Parasolid XT Format Reference 原文：“At the PK interface, the cone axis is in the opposite direction to that stored in the XT data.”
- **机理与后果**：
  Parasolid 官方规范明确规定 XT 文件中圆锥轴向与 PK 接口方向相反。当前内核写出和读入均未做反转，虽然内核内部往返自洽，但与真实 Parasolid 生成的 XT 互传时，所有圆锥轴向全部反向，半角和母线方向翻转。
- **修复方案**：
  在 `XtWriter` 写入 Cone 时对 Axis 取反；在 `IcurveXt.cs` 及 `XtReader.cs` 读取 Cone 时对 Axis 取反；修正 `AnalyticImplicitEvaluation.cs` 中的虚假注释，并编写真实 Parasolid 往返交叉测试。

### N6. Chart `parameter_error` 导入时被静默丢弃（§3.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Runtime/KernelRuntime.IcurveXt.cs:156-193`（`TryReadChartNode` 仅读取 fields[0..4]，忽略 fields[5..6]）
  - `src/ProjectGmKernel.Native/Runtime/XtWriter.cs:1089-1098`（写出时若 `ParameterErrorProvided == 0` 则写入 Null）
- **机理与后果**：
  真实 Parasolid XT 中 Chart 节点的 fields[5] 和 fields[6] 为 `parameter_error`。当前读取代码不仅未读取该字段，还硬编码 `prefix == 7`。读取后该信息丢失，导致再次写出时沦为 Null，造成单向数据损坏。
- **修复方案**：
  在 `IcurveDecodeInput` 和 `ICurveDataRecord` 中打通 `parameter_error` 的读取与传递，完整保留可选误差字段。

### N7. Legacy Body 导入全量丢弃 Surface Sense（§3.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Runtime/XtReader.cs:112-224`
- **机理与后果**：
  `XtReader.cs` 中的 `MaterializeCone/MaterializeSphere/MaterializeTorus` 通过 `BodyCreateSolid...` 重建实体，未读取 XT 表面节点的 Sense 字段，面 Sense 全部硬编码为正向。导致带负向面的体在导入后拓扑手性与法向断裂。
- **修复方案**：
  在 `XtReader` 构建拓扑后，根据 XT 节点的 Sense 字段调用 `TopologySetSense` 或对生成的 SurfaceRecord 设置正确 Sense。

### N8. B-Surface 域外静默一阶外插，与持久化仅声明基本域脱节（§9.5）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/BSurfaceEvaluation.cs:40-85`（超出 Knot 域时夹紧并累加一阶导数外插）
  - `src/ProjectGmKernel.Native/Runtime/XtWriter.cs:1208-1239`（仅写出基本域）
- **违反条款**：§9.5 有效域契约：“有效域不能始终等于原 knot 的基本区间……本期未支持的扩展语义必须明确拒绝，不能夹紧到边界”。
- **修复方案**：
  未开放扩展域时，严格对超出 Knot 域的 $(u, v)$ 返回 `OutsideSupportedDomain`，禁止静默夹紧外插。

---

## 5. 未接线 / 门禁内部模块的数学错误（Major，5 项）

### N9. Spun 子午面残差的梯度符号完全相反（§9.4）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/SweptSpunImplicit.cs:57-58`
- **数学复核**：
  残差为 $r(x) = ((x - P) \times \hat{A}) \cdot w_c = (x - P) \cdot (\hat{A} \times w_c)$。
  真实梯度为 $\nabla_x r = \hat{A} \times w_c = \text{unitA} \times w_c$。
  当前代码实现为 `gradientWrtPoint = Cross(wc, unitA)`，即 $w_c \times \hat{A} = -\nabla_x r$。
  注释自称“$\nabla_x(w \cdot w_c) = -A \times w_c = w_c \times A$”，推导在叉乘反对称性上出现初等符号颠倒。
- **影响**：当前模块未接入生产（自标未接线），但一旦接线将导致牛顿迭代反向发散。
- **修复方案**：改为 `Cross(unitA, wc)`，并补充中心差分梯度单元测试。

### N10. Terminator 分支切向叉积未乘以支持面 Sense（§5.1 / §6.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:177`
- **机理与后果**：
  `tangent = Unit(Cross(jet0.Gradient, jet1.Gradient));` 未乘两个支持面的 Sense。
  当支持面 Sense 之积为负时，叉积切向与真实 Chart 切向反号，导致 `forwardCosine < 0`，函数直接判定为 `Singular`。
- **修复方案**：引入 `SenseSign`，令切向为 $(\text{sense}_0 \nabla\phi_0) \times (\text{sense}_1 \nabla\phi_1)$。

### N11. Blend 圆弧角度恢复使用 `Atan2` 截断短弧（§10.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEvaluation.cs:192`（`arc = Math.Atan2(sinA, cosA);`）
- **违反条款**：§10.2“以 atan2 恢复并展开 a。不能仅使用 acos 丢掉方向，也不能无证据地总取短弧”。
- **机理与后果**：
  对于角度大于 $\pi$ 的物理圆弧（如优弧过渡），`Atan2` 强制返回 $(-\pi, \pi]$，直接将圆弧截断反转为劣弧，计算出错误的接触面。
- **修复方案**：结合初始见证点或缠绕数展开角度，禁止在缺乏见证时盲目截断。

### N12. Blend 包络圆弧 $v$ 校验硬编码截面 Frame 与见证 Frame 冲突（§10.2 / §10.6）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEnvelopeSolve.cs:44-48`
- **机理与后果**：
  `TryValidate` 内部硬编码截面为 $X = \text{radial}, Y = (0,0,1)$。
  但在圆 Spine 曲线下，根据 §10.2 定义的自然见证坐标系 $Y = \text{unit}(T \times X)$，实际应为 $(0,0,-1)$。硬编码使得 $v$ 符号颠倒，非对称区间 $[v_{\min}, v_{\max}]$ 将误拒合法根并接受伪装根。此外 `Arc == 0` 时直接放行，破坏了门禁有效性。
- **修复方案**：接收外层传入的真实 Frame，移除内部私自构造的轴向假设。

### N13. Blend Joint Lift 混单位残差直接与绝对 $10^{-12}$ 比较且无 Frozen Row Scaling（§12.2 / §14.1）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Intersection/BlendJointLift.cs:93-95`
- **机理与后果**：
  6×6 联合方程中包含曲面距离方程（长度量纲）与中心点球面方程 $\|x - c\|^2 - r^2$（长度平方量纲）。代码直接取 `MaxAbs(f)` 与绝对常数 $10^{-12}$ 比较。大半径时平方残差使得迭代永远无法达到 $10^{-12}$，造成假不收敛。
- **修复方案**：引入行缩放矩阵，将平方残差除以局部半径尺度转换为长度量纲后再判停。

---

## 6. 测试诚实性与虚假通过缺陷（Major，2 项）

### T1. 回归测试断言全部包裹在 `if (status == Success)` 中（§24.2）
- **位置**：
  - `tests/KernelTests/IcurveEleventhReviewRegressionTests.cs:49-57`（`Terminator_BranchTracking_MustNotJumpToWrongTorusCircle`）
- **机理与后果**：
  测试代码将所有坐标范围与残差断言包裹在 `if (status == AlgorithmStatus.Success)` 块中。如果算法发生严重退化恒返回 `NotConverged`，测试将跳过全部断言并显示“测试通过”。测试失去回归防护能力。
- **修复方案**：在 `if` 前显式加上 `Assert.Equal(AlgorithmStatus.Success, status);`。

### T2. 区间认证测试名与实际断言行为名实相反（§18.4）
- **位置**：
  - `tests/KernelTests/IntervalRootCheckTests.cs:51-127`
- **机理与后果**：
  方法名标为 `_Unique`、`_Empty`，但在断言中全部检查 `Assert.Equal(AlgorithmStatus.Unsupported, status)`。虽然代码诚实拒绝未实现的认证，但测试名称具有误导性，易被外部审计误判为已交付区间认证。
- **修复方案**：测试方法名重命名为 `..._Unsupported_WhenNotImplemented`，明确其为门禁拒绝测试。

---

## 7. 证据链与能力完整性缺口（Major，4 项）

### E1. §5.1 图表余弦比递推 $f_i$ 缺乏真实 Parasolid 辨识证据
- **事实依据**：
  `scripts/IcurveEvaluationOracle.cs` Case F 构建的图表是正 49 边形，每段弦长完全相等，导致余弦比 $T_i \cdot e_{i-1} / T_i \cdot e_i \equiv 1$。
  即使在代码中完全删去余弦比项，Case F 以及其他所有 Oracle 测试依然能 100% 满分通过。
- **整改要求**：
  在 Oracle 脚本中引入非均匀弦长（长短交替）的 Chart 样本，并在 Case A/B 中补充 `TMin/TMax` 与 `PK_CURVE_ask_interval` 的严格断言。

### E2. §10.3 Frame 导数链式项无实现且全部测试传 0
- **事实依据**：
  全库搜索 `XD1/XD2/YD1/YD2/ArcD1/ArcD2`，除结构体自身定义外无任何计算逻辑。现有测试全部传 `default`（0 值），导致参数导数的高阶耦合项从未在任何测试中被激活。
- **整改要求**：补齐链式导数计算，或在未实现时对非零高阶请求明确返回 `Unsupported`。

### E3. §21.1 距离组合入口接受裸 double 无类型约束
- **事实依据**：
  `BlendBoundComposition.CompositionInput` 全部字段均为裸 double，无法在编译期或运行期拦截平方隐式式等非法输入，非 fail-closed。
- **整改要求**：将入参类型改为 `OrientedDistanceJet` 并增加校验。

### E4. `SampleWitness` 结构在生产端全传 `None`
- **事实依据**：
  所有生产求值路径和发布点均传递 `SampleWitness.None`，UV witness 算后即弃，未形成跨域闭环。
- **整改要求**：在能力文档中将该状态客观降级为“数据结构已定义，端到端管线未接通”。

---

## 8. 能力清单与文档失实修正项（Documentation Integrity）

必须对 `docs/icurve_evaluation_capabilities.md` 中以下过度宣称的行进行修正：

| 行号 / 模块 | 当前过度宣称的描述 | 实际代码客观事实 | 修正方案 |
|---|---|---|---|
| **line 29** | “Case A/A2/B/C/E/F 机器精度通过” | 仅 Case F 完成了真同参 $t$ 比较；A/A2/E 依靠我方位置反算角度，B/C 仅验证几何残差 | 明确拆分标注：Case F 为同参比对，其余为几何与残差验证 |
| **line 51** | `SampleWitness` “求解器生产/消费持续接入” | 生产发布点 100% 传 `SampleWitness.None`，无真实消费者 | 降级标注为“数据载荷就绪，生产消费管线待接通” |
| **line 62** | 滚动球 Blend 4×4/3×3 求解器 | 未披露仅支持“原点居中、XY 平面圆 Spine” | 明确注明仅支持 XY 平面圆 Spine 特例，一般 Spine 受限 |
| **line 70** | “XT INTERSECTION 实体写入与回读: 生产已接入 + 真实 PK 兼容通过” | 回读函数 `TryMaterializeICurveFromXt` 仅在单元测试中调用，`XtReader` 未接线 | 改为“写入生产已接入并获 PK 接收验证；XT 回读仅在测试层闭环” |
| **line 72** | Sense 拓扑方向双向保持 “Oracle 验证一致” | Oracle 脚本中 Sense 覆盖为 0，且无负 Sense XT 往返用例 | 移除“Oracle 验证一致”字样，如实注明为数据结构支持 |
| **line 44** | 嵌套精度自适应 $\eta_k$ | 暗示为有效自适应机制 | 注明“已实现重算分支，但在纯解析主线上为等价操作” |

---

## 9. 关键 Minor 缺陷合并清单（择要）

1. **奇数 UV 读越界（内存安全隐患）**：`KernelRuntime.IcurveXt.cs:253-257` 奇偶循环 `i += 2` 在 `uvCount` 为奇数时越界访问 `uvValues[uvCount]`。应在循环前直接校验并拒绝奇数。
2. **$\pm\infty$ 写侧静默转 Null**：`XtWriter.cs:1149` 将任何非有限值转为 Null 写入，导致计算产生的非有限异常在序列化时被伪装成缺失字段。
3. **Terminator 导数漏记预算**：`TerminatorEvaluation.cs:721, 733` 的导数隐式求值未调用 `budget.TryConsume(1)`。
4. **Dogleg 秩亏分支维度常量不一致**：`TrustRegionStep.cs:22` 允许最大维度 16，而 `SmallLinearSolve.cs:230` 限制 $n \le 6$，维度 7–16 会抛出 `Unsupported`。
5. **BlendBound 复合有限性检查不全**：`BlendBoundComposition.cs:107` 仅检查 `hxx` 与 `hzz`，忽略 `hxy, hxz, hyy, hyz` 的有限性。
6. **未绑定 ICurve 数据槽的变长内存泄漏**：`KernelRuntime.cs:3153` 在释放未绑定实体的 ICurve 槽位时未释放关联的 `HvecBlock`。
7. **L0 残差 Memo 键不完整且只写不读**：`ResidualMemo.cs` 生产环境中 `TryFind` 零调用。
8. **Oracle 失败报告被覆盖**：`IcurveEvaluationOracle.cs` 发生异常退出时不重写报告文件，旧的 PASS 报告原地残留。

---

## 10. 推荐修复分期与实施顺序

建议按以下 5 个阶段实施闭环修复，确保每一步均有测试保护且不破坏架构：

### 第一阶段：并发与内存安全（阻塞级）
1. 修复 **B1**：为 `GeometryEvaluationCache` 引入槽位版本快照与读写同步机制，或在调度层显式降级并发属性。
2. 修复 **Minor 1**：在 `IcurveXt.cs` 校验 UV 计数奇数并提前拒绝。

### 第二阶段：生产数值与求解器修正（核心正确性）
1. 修复 **N1**：在 `ICurveCorrection.cs` 中对试探步反乘列缩放 $D_u$（`step[i] *= _col[i]`）。
2. 修复 **N2**：将缓存精确命中判据的 `CacheErrorBound` 乘以几何尺度，避免大半径模型永久穿透。
3. 修复 **N3**：将非定义面残差改为 `GeometricDeviation`，失败时拒绝，并接入预算记账。
4. 修复 **N4**：重构 Terminator 的奇异性与容差判据，移除世界坐标加权与绝对常数地板。

### 第三阶段：Parasolid XT 数据保真与 Sense 闭环
1. 修复 **N5**：在 `XtWriter` 与 `IcurveXt` 中实现圆锥轴向的恰好一次反转，删除虚假注释，补齐真实 Parasolid 往返用例。
2. 修复 **N6**：打通 Chart `parameter_error` 的读取与写出管线。
3. 修复 **N7**：在 `XtReader` 中恢复面节点的 Sense 属性解析。
4. 修复 **N8**：严格禁止 B-Surface 域外静默外插。

### 第四阶段：门禁内部模块数学公式重构
1. 修复 **N9**：纠正 `SweptSpunImplicit.cs` 的子午面残差梯度方向。
2. 修复 **N10**：Terminator 分支切向叉积引入支持面 Sense 定向。
3. 修复 **N11 & N12**：重构 Blend 圆弧优弧角度恢复与见证 Frame 坐标系绑定。
4. 修复 **N13**：为 Blend Joint Lift 引入行缩放矩阵。

### 第五阶段：测试集诚实性与能力文档校准
1. 修复 **T1**：去除 `IcurveEleventhReviewRegressionTests.cs` 中的 `if (status == Success)` 保护罩。
2. 修复 **T2**：重命名区间认证测试方法。
3. 补充 **E1**：在 Oracle 中增加非均匀弦长 Chart 用例，验证 $\S5.1$ 余弦比递推。
4. 修正 **Section 8** 中的 6 处能力文档过度陈述，确保矩阵与生产事实 100% 吻合。
