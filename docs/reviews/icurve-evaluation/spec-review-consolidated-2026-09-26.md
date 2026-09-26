# ICurve / Blend 求值实现 · 规格符合性综合归并与裁决报告

- **日期**：2026-09-26
- **审查基线**：Git HEAD `025422169ac4be4b39748331929da5926499238d`（原始基线 `dd73576`）
- **对照规格**：`docs/icurve_design/icurve_blend_evaluation_spec.md`（v2.0，1481 行）
- **输入来源**：
  1. GLM-5.3 审查报告（`docs/reviews/icurve-evaluation/spec-review-2026-09-26-r2.md`）
  2. DeepSeek-V4.1-Flash 第 15 轮审查报告（`docs/reviews/icurve-evaluation/spec-review-round15-2026-09-26.md`）
  3. 第三方专家裁决与反例复算意见（Round 16 / 0254221 架构与数学裁决）
- **验证方法**：主线对所有 Blocker、Major、文档失实行及关键 Minor 进行 100% 源码定位、公式推导、量纲分析与控制流交叉复核，并融合第三方裁决中提出的解析反例与架构修正。
- **测试实测现状**：`MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests` → **Passed 535 / Failed 0 / Skipped 0**（Allocation IL guard CLEAN，Function catalog 83 描述符通过）。

---

## 1. 总体评价与关键裁决调整

三方审查意见与主线核验高度共识：**所有核心技术缺陷均有实质代码证据支持，定性全部成立**。
但第三方专家裁决深刻指出了原归并报告中若干“过于轻量化或头痛医头”的修复漏洞，并提供了严格的数学反例。我们对修复策略做出以下根本性调整：

1. **B1（Blocker）**：不能仅凭“将 `PK_CURVE_eval` 改为 `ConcurrencyKind.Local`”或“在 `PublishHit` 加有限性检查”来关闭。`Local` 不等于全局串行（已有分区锁时不同分区仍可并发访问同一全局静态缓存）。首选方案是**为 L3 缓存建立短锁同步协议**，严格禁止持锁执行子求值器。
2. **N1（Major）**：试探步更新必须反向映射 $\Delta y = D_u p$，但**绝不能在原步长数组上直接原地修改 `step[i] *= _col[i]`**。Dogleg 步长范数、信赖域边界碰撞判断以及模型预测下降必须保留在缩放空间计算（使用未反缩放的 $p$），因此必须维护 `scaledStep` 与 `physicalStep` 两个独立向量。
3. **N5（Major）**：Cone 轴向反向是曲面接口约定转换（$A_{\rm kernel} = -A_{\rm XT}$），必须明确界定在 Raw XT 解析与内核规范化曲面记录之间转换一次。对于 Legacy `XtReader.MaterializeCone`，它是调用实体构造器 `BodyCreateSolidCone`（传入的是带估计 height 的放置轴），不能机械套用曲面轴取反，需独立推导端面与截取区间。
4. **Spun（Major）**：原报告仅指出梯度符号反转，但第三方专家推导证明**残差定义本身就错了**：$f = ((x-P)\times\hat A)\cdot((C-P)\times\hat A) = r_{x,\perp}\cdot r_{c,\perp} = 0$ 表示两点在子午面上正交（相差 90°），根本不是共面！不能仅改叉积顺序，必须按照规格 §9.4 实现轴向高度与径向距离平方的双方程消元。
5. **N2 & N13（Major）**：
   - 缓存精度不能简单粗暴地“将阈值乘以 $R$”。必须将 `RawResidualSummary`（调试残差）、`ScaledResidualNorm`（求解器下降判据）与 `EvaluationQuality`（可复用几何质量）三者彻底解耦。
   - 联合求解不仅在大半径下困难，第三方专家给出了**小半径错误成功的确定性反例**（$r = 2^{-24} \approx 6\times 10^{-8}$ 时半径残差 $3r^2 \approx 10^{-14} < 10^{-12}$，在第一次检查时直接以 100% 相对误差错误成功）。必须为联合系统引入基于冻结权重的行缩放矩阵。

---

## 2. Blocker（1 项）

### B1. L3 缓存静态裸指针 Arena 零同步，与 `PK_CURVE_eval` 的 Concurrent 只读登记冲突（§13.9）
- **位置**：
  - `src/ProjectGmKernel.Native/Runtime/GeometryEvaluationCache.cs:56-62`（`private static CachedCurveSample* arena;`、`private static MemoryPageIndex clockHand;`）
  - `src/ProjectGmKernel.Native/Runtime/KernelRuntime.EntryPoints.cs:15`（`Dispatch(ApiId.CurveEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ...)`）
  - `src/ProjectGmKernel.Native/Runtime/ApiDispatch.cs:270-295`（多只读调用同时执行 `SharedReaders++`）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:227-233`、`:517-522`（`PublishHit` 将未验证的命中样本直接写入调用方数组）
- **违反条款**：§13.9 缓存并发一致性契约；§13.7 样本验证后发布；§18.5 统一原子发布。
- **严重性裁决**：**维持 Blocker**。
- **并发交错证明（无需弱内存模型假设即可触发）**：
  设槽位原本记录参数 $t_0 \to$ 位置 $P_0$、导数 $J_0$。
  覆盖写入并非原子事务，当写线程覆写此槽位时：
  1. 槽位原本的 `Occupied` 就是 1；
  2. 写线程写入新参数 $t_1$；
  3. 读线程并发介入，看到 `Occupied == 1` 且 `Parameter == t_1` 匹配成功；
  4. 读线程复制尚未更新的旧几何数据 $P_0, J_0$，直接作为参数 $t_1$ 的解发布给用户；
  5. 写线程随后才写完 $P_1, J_1$。
  `PublishHit` 中的有限性检查对合法但属于旧参数的数值完全无效！
- **为什么“改成 Local”不是充分修复**：
  `ApiDispatch` 只有在 `context == null || context->LockCount == 0` 时才把 Local 视为全局排他。在已获取分区锁时，不同分区的 Local 命令仍可并发执行，它们依然会并发访问全 Session 共享的同一个静态 L3 缓存。
- **彻底整改方案**：
  1. **短期基线（短锁同步协议）**：
     - 在 `GeometryEvaluationCache` 内引入细粒度短锁（如 `System.Threading.Lock` 或无堆分配的 `SpinLock`）。
     - 锁保护范围覆盖：查找并复制完整样本、CLOCK 元数据更新、槽位选择与覆写、以及 Attach/Detach/Clear。
     - **严格执行快照复制**：命中时在锁内将样本复制到栈上局部变量后立即释放锁；未命中时释放锁并执行实际几何计算，计算完毕后短暂获取锁写入发布。**严禁持锁调用任何子求值器**（符合 §13.9）。
  2. **长期演进**：基于 CAS 和序列号（Sequence Lock）的槽位原子发布协议。

---

## 3. 生产可达的数值与求值缺陷（Major，4 项）

### N1. 修正器未将缩放步长反向映射回真实状态空间（$D_u \cdot p$ 未施加）（§14.1）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Caching/FrozenResidualScale.cs:45-54`（`Apply` 执行 $J_s = W_F J D_u$，其中 `_col[j]` 即 $D_{u, j}$）
  - `src/ProjectGmKernel.Native/Geometry/Intersection/ICurveCorrection.cs:120-123`（`DoglegStep` 在缩放空间解出 $p$）
  - `src/ProjectGmKernel.Native/Geometry/Intersection/ICurveCorrection.cs:161`（`trial[i] += step[i];`）
- **违反条款**：§14.1 未知量缩放契约（$\Delta y = D_u p$）。
- **解析反例复算（证明线性问题假停滞）**：
  构造线性方程组：
  $$F(y) = \begin{bmatrix} y_1 + 100y_2 - 1 \\ y_1 - 100y_2 - 1 \end{bmatrix}, \quad y_0 = 0$$
  按现有 `Capture` 规则得出：
  $$W_F = 0.01 I, \quad D_u = \operatorname{diag}(100, 1)$$
  $$J_s = \begin{bmatrix} 1 & 1 \\ 1 & -1 \end{bmatrix}, \quad r_s = \begin{bmatrix} -0.01 \\ -0.01 \end{bmatrix}$$
  缩放空间求解得出牛顿步 $p = (0.01, 0)^T$。
  正确物理更新应为 $\Delta y = D_u p = (1, 0)^T$，一步收敛到真根，预测下降与实际下降完全一致（$\rho = 1$）。
  但当前代码直接执行 $y_{\rm trial} = y_0 + p = (0.01, 0)^T$，计算出：
  $$\text{predicted} = 10^{-4}, \quad \Psi_{\rm trial} = 0.99^2 \times 10^{-4} \implies \rho = 0.0199 \ll 0.1$$
  一个本应一步收敛的标准线性系统，由于坐标更新漏乘 $D_u$，被判定为下降严重不足而反复缩步并假停滞（`Stagnation`）。
- **整改架构设计（双向量隔离）**：
  在 `FrozenResidualScale` 中增加 `MapToPhysicalStep(ReadOnlySpan<double> scaledStep, Span<double> physicalStep, BufferCount n)`：
  - 维护 `scaledStep = p`：专供 $\|p\|$、信赖域边界碰撞判断、模型预测下降等内部代数使用；
  - 维护 `physicalStep = D_u p`：专供 `trial[i] += physicalStep[i]` 几何状态更新。
  - **严禁原地 `step[i] *= _col[i]`**，否则会彻底破坏信赖域半径控制体系。

### N2. 缓存精确命中判据 `ErrorEstimate` 量纲耦合，致大半径模型频繁误 Miss（§13.4 / §15.2）
- **位置**：
  - `src/ProjectGmKernel.Native/Geometry/Caching/EvaluationSampleStore.cs:71, 139`、`src/ProjectGmKernel.Native/Runtime/GeometryEvaluationCache.cs:125`
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:392`（`CacheErrorBound = 1e-11`）
- **机理与澄清**：
  球面/圆柱残差为 $r^2 - R^2 \approx 2R\delta$，其数值随几何半径线性放大；环面四次残差随 $R^3$ 放大。
  不能简单断言“所有 $R \ge 200$ 绝对不命中”（某些节点浮点残差可能为 0），但其确实使得缓存命中率高度依赖于代数方程的形式与空间尺度。
- **整改架构设计**：
  不能仅将阈值乘以 $R$，必须解耦三种语义：
  1. `RawResidualSummary`：保留各方程原始残差，供内部诊断；
  2. `ScaledResidualNorm`：求解器无量纲下降判据；
  3. `EvaluationQuality`：真实的几何欧氏偏差（`GeometricDeviation`）及已验证的导数阶数，以此作为缓存命中决策依据。

### N3. 非定义面残差使用原始隐式值、求值失败静默为 0、漏记预算（§6.4 / §23.2）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Evaluation/ICurveEvaluation.cs:488-491`
- **整改方案**：改调 `AnalyticImplicitEvaluation.GeometricDeviation` 获取真实欧氏间隙；求值失败时记录正无穷大；调用必须计入预算 `budget.TryConsume(1)`。

### N4. Terminator 容差与奇异判据使用绝对常数及世界坐标加权（§6.2 / §6.4）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:229-234, 486`
- **整改方案**：将奇异判定改为归一化梯度的投影或局部无量纲判据；移除容差计算中的 `Math.Max(ChordLength, 1.0)` 世界坐标绝对常数地板。

---

## 4. XT 与真实 Parasolid 数据保真缺陷（Major，4 项）

### N5. Cone 轴向转换缺失与 XT/内核语义边界划分（§8.3）
- **位置**：
  - `src/ProjectGmKernel.Native/Runtime/XtWriter.cs:790`（直接写入未取反的 `Axis`）
  - `src/ProjectGmKernel.Native/Runtime/KernelRuntime.IcurveXt.cs:337`（未取反直接填充 `PK_CONE_sf_s`）
  - `src/ProjectGmKernel.Native/Geometry/Evaluation/AnalyticImplicitEvaluation.cs:30-32`（虚假注释声称已归一化）
- **分层边界规范（避免机械取反造成混乱）**：
  固定内核内部 `ConeData` 遵循 PK 规范（母线方程 $\rho(z) = R + k z$），仅在外部边界做单次转换：
  | 层次 | 轴向约定 | 处理动作 |
  |---|---|---|
  | **原始 XT 节点与解析** | XT 规范（$A_{\rm XT}$） | 原样保留 XT 原始数据，不静默修改 |
  | **XT 解析曲面 $\to$ 内核曲面** | PK 规范（$A_{\rm kernel} = -A_{\rm XT}$） | 在 Materialize 适配器中显式取反一次 |
  | **内核计算、缓存、ConeAsk** | PK 规范（$A_{\rm kernel} = A_{\rm PK}$） | 始终遵循内核既有统一几何定义 |
  | **内核曲面 $\to$ XT 写出** | XT 规范（$A_{\rm XT} = -A_{\rm kernel}$） | 在 `XtWriter` 写出节点时显式取反一次 |
  | **Legacy `XtReader.MaterializeCone`** | 实体构造器规范 | 独立推导底面、顶面截取区间与 `BodyCreateSolidCone` 放置轴，严禁机械取反 |
- **补充验收**：清理虚假注释，增加非零轴向截面、原点偏移及倾斜轴的真实 Parasolid XT 双向交叉往返测试。

### N6. Chart `parameter_error` 导入丢弃（§3.2）
- **位置**：`src/ProjectGmKernel.Native/Runtime/KernelRuntime.IcurveXt.cs:156-193`
- **整改方案**：打通 `parameter_error` 在 `IcurveDecodeInput` 与 `ICurveDataRecord` 中的流转通道，完整支持可选误差字段往返。

### N7. Legacy Body 导入全量丢弃 Surface Sense（§3.2）
- **位置**：`src/ProjectGmKernel.Native/Runtime/XtReader.cs:112-224`
- **整改方案**：在解析面节点时提取 Sense 字段，并在生成实体面时赋予正确的拓扑 Sense 属性。

### N8. B-Surface 域外静默一阶外插（§9.5）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Evaluation/BSurfaceEvaluation.cs:40-85`
- **整改方案**：严格限制在有效 Knot 基本域内求值；超出范围时返回 `OutsideSupportedDomain`，禁止静默线性外插。

---

## 5. 未接线 / 门禁内部模块的数学错误（Major，5 项）

### N9. Spun 子午面残差几何定义错误与梯度符号反转（§9.4）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Evaluation/SweptSpunImplicit.cs:55-58`
- **几何与梯度双重推导**：
  1. **几何残差错误**：
     $$f(x) = ((x - P) \times \hat A) \cdot ((C - P) \times \hat A) = r_{x, \perp} \cdot r_{c, \perp}$$
     $f(x) = 0$ 对应的是两个径向矢量**垂直（点积为 0）**！例如当 $x = C$ 时两点明明在同一子午面上，残差却等于 $\|C_\perp\|^2 > 0$；而当 $x$ 与 $C$ 相差 90° 时，残差反而等于 0。残差本身并未表达“在同一子午面”的几何约束！
  2. **梯度符号错误**：
     即使对于该错误残差，$f = (x - P) \cdot (\hat A \times w_c)$ 的真梯度也是 $\hat A \times w_c$，而代码返回 `Cross(wc, unitA)`，符号相反。
- **彻底整改方案**：
  放弃这个单平面残差，直接落地规格 §9.4 的双方程消元系统：
  $$h_1(x, u) = \hat A \cdot (x - P) - \hat A \cdot (C(u) - P) = 0 \quad (\text{轴向高度一致})$$
  $$h_2(x, u) = \|P_\perp (x - P)\|^2 - \|P_\perp (C(u) - P)\|^2 = 0 \quad (\text{径向距离平方一致})$$
  解析求出对 $x$ 和 $u$ 的偏导数，并补充轴上点与旋转角周期展开处理。

### N10. Terminator 分支切向叉积未乘以支持面 Sense（§5.1 / §6.2）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Intersection/TerminatorEvaluation.cs:177`
- **整改方案**：切向构造引入面 Sense 符号：$T = \operatorname{Unit}((\text{sense}_0 \nabla\phi_0) \times (\text{sense}_1 \nabla\phi_1))$。

### N11. Blend 圆弧角度恢复使用 `Atan2` 截断短弧（§10.2）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEvaluation.cs:192`
- **整改方案**：角度展开必须结合前序见证点或缠绕数，禁止在无见证时盲目截断在 $(-\pi, \pi]$。

### N12. Blend 包络圆弧 $v$ 校验硬编码 Frame 与见证 Frame 冲突（§10.2 / §10.6）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Evaluation/BlendEnvelopeSolve.cs:44-48`
- **整改方案**：接收外层传入的规范化见证坐标系；移除内部假设的 $Z = (0, 0, 1)$。

### N13. Blend Joint Lift 混单位残差无行缩放及小半径假成功漏洞（§12.2 / §14.1）
- **位置**：`src/ProjectGmKernel.Native/Geometry/Intersection/BlendJointLift.cs:93-95`
- **小半径错误成功解析反例**：
  设构造测试夹具：
  $A(c): c_z = 0, \quad D(c): |c|^2 - 1 = 0, \quad S(x): x_z = 0, \quad p(x, t): x_y = 0$
  圆柱半径 $r = 2^{-24} \approx 5.96 \times 10^{-8}$，中心点 $c = (1, 0, 0)$，试探点 $x = (1 + 2r, 0, 0)$。
  此时除球面半径方程外，其余五项残差全为 0。半径方程残差为：
  $$\|x - c\|^2 - r^2 = (2r)^2 - r^2 = 3r^2 = 3 \times 2^{-48} \approx 1.066 \times 10^{-14} < 10^{-12}$$
  由于未做尺度归一化，算法在第一次迭代即判定残差 $< 10^{-12}$ 并直接返回 `Success`！但实际距离为 $2r$（相对半径误差高达 100%）。
- **整改方案**：
  为联合系统施加正的冻结行缩放权重矩阵 $W_F$（球面行除以 $2r$，各面方程除以梯度范数），将所有方程转化为无量纲误差后再进行收敛判定。

---

## 6. 测试诚实性与虚假通过缺陷（Major，2 项）

### T1. 回归测试断言全部包裹在 `if (status == Success)` 中（§24.2）
- **位置**：`tests/KernelTests/IcurveEleventhReviewRegressionTests.cs:49-57`
- **整改方案**：在 `if` 前增加强断言 `Assert.Equal(AlgorithmStatus.Success, status);`，杜绝算法恒失败时测试虚假全绿。

### T2. 区间认证测试名与实际断言行为名实相反（§18.4）
- **位置**：`tests/KernelTests/IntervalRootCheckTests.cs:51-127`
- **整改方案**：将方法名重命名为 `..._Unsupported_WhenNotImplemented`，如实反映门禁未开放状态。

---

## 7. 证据链与能力完整性缺口（Major，4 项）

### E1. §5.1 图表余弦比递推 $f_i$ 缺乏非均匀真实 Parasolid 证据
- **事实依据**：Case F 的 Chart 是正 49 边形等弦图表，每段余弦比恒为 1。代码删去余弦比项仍能全绿通过。
- **整改要求**：在 Oracle 脚本中引入非均匀弦长 Chart 样本进行真同参比对。

### E2. §10.3 Frame 导数链式项无实现且全部测试传 0
- **事实依据**：全库搜索 `XD1/YD1/ArcD1`，除声明外无任何计算逻辑，测试全部传 0。
- **整改要求**：补齐链式导数计算，或在未实现时对非零高阶请求明确返回 `Unsupported`。

### E3. §21.1 距离组合入口接受裸 double 无类型约束
- **事实依据**：`CompositionInput` 无类型守护，无法在编译期或运行期拦截平方隐式式。
- **整改要求**：将入参类型改为 `OrientedDistanceJet` 并增加校验。

### E4. `SampleWitness` 结构在生产端全传 `None`
- **事实依据**：所有生产求值路径均传递 `SampleWitness.None`，UV witness 算后即弃。
- **整改要求**：在能力文档中如实降级。

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

## 10. 推荐分期实施与验收顺序

严格按照专家裁决建议拆分为独立可验证的提交，逐步闭环：

| 阶段 | 核心任务 | 关键实现要点 | 核心验收证据 |
|---|---|---|---|
| **Phase 1** | **B1 并发数据竞争消除** | 引入缓存细粒度短锁协议，严格锁定查找/复制/淘汰/写入，严禁持锁求值 | 构造人工线程屏障交错测试、并发读写与淘汰用例 |
| **Phase 2** | **N1 缩放与物理双步长架构** | `FrozenResidualScale` 分离 `scaledStep` 与 `physicalStep`，保留 $p$ 控制信赖域 | 构造上述线性系统反例（断言 $\rho \approx 1$）、非均匀参数化测试 |
| **Phase 3** | **N5 XT Cone 轴向转换分层** | 明确 XT 读写边界单次反向；审计 Legacy `XtReader.MaterializeCone` | 倾斜轴、非零原点圆锥的真实 PK 双向交叉往返测试 |
| **Phase 4** | **Spun 几何消元重构** | 落地 §9.4 高度与半径平方双方程消元系统，解析推导对 $x, u$ 的 Jacobian | 真实旋转生成点测试、解析/数值差分一致性测试 |
| **Phase 5** | **N2 & N13 质量与尺度归一化** | 缓存解耦三类指标；Joint Lift 引入冻结行缩放矩阵 | 极小半径反例（$r=2^{-24}$ 拒绝假成功）、多尺度收敛测试 |
| **Phase 6** | **测试集防线加固与文档校准** | 移除 `if (Success)` 保护罩；校正能力清单 6 处陈述 | 单元测试套件全部通过，能力文档与源码事实 100% 对齐 |
