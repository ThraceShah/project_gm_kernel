# ICurve / Blend Evaluation Capabilities

Generated: 2026-09-26. Verified against `docs/icurve_design/icurve_blend_evaluation_spec.md` (v2.0) and review audit.

## 状态分类标准 (Status Schema)

为避免使用单一的 "Supported" 掩盖模块内部完成度差异，本项目能力清单严格按以下 5 种状态进行细分标注：

1. **生产已接入 + 真实 PK 兼容通过 (Production + Live PK Oracle Passed)**:
   打通完整生产管线（Decode → Prepare → Runtime → PK_CURVE_eval / XT），且在真实 Parasolid 运行时（Oracle Case A–F）完成同参数比较，达到机器精度（$|\Delta| \le 10^{-14} \sim 10^{-15}$）。
2. **生产已接入 + 制造解通过 (Production + Manufactured Solution Passed)**:
   已完整接入生产求值与缓存路径，通过工程单元测试与独立高精度制造解验证。
3. **内部入口可用 + 制造解通过 + 受 Gate 限制 (Internal Entry + Manufactured Solution Passed + Gated)**:
   核心数学与求解算法已完整实现并通过制造解闭环，但因上层语义规范门禁（GATE-A/B/T/D）尚未关闭，生产公共入口精确拒绝或返回特定诊断，严禁伪造根或粗暴绕过。
4. **扩展点 / 内部机制 (Extension Point / Incremental Capability)**:
   数据结构与算法扩展点已预留并具备基础闭环，但在当前纯解析支持面主线上未全量触发，需在对应高阶功能落地时完全激活。
5. **明确未支持 / 严格禁止 (Explicitly Unsupported / Forbidden)**:
   明确排除的几何类型或规格明令禁止的实现方式（如伪弧长作为公开参数、无分支证据的就近吸附）。

---

## 能力状态清单 (Capabilities Matrix)

### 1. 核心求值与图表递推 (Core Evaluation & Chart)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| 原始图表递推与弦平面投影 | §5 / Original chart | **生产已接入 + 真实 PK 兼容通过** | 节点位相同比较；归一化余弦递归计算；弦方向保留；保留并回写 `parameter_error[0..1]`；非均匀弦长图表（Case F2，$f_i / f_{i-1} \ne 1$）与真实 PK `PK_CURVE_ask_interval` / `PK_CURVE_eval` 机器精度一致（$|\Delta T_{\max}| = 1.78\times 10^{-15}$） |
| 解析支持面正则区间求解 (I1/I2/Auto) | §7 / Analytic supports | **生产已接入 + 真实 PK 兼容通过** | Auto: 平面走 I1，其他解析面走 I2；Oracle Case F / F2 真同参比较机器精度通过（D0/D1/曲率 $|\Delta| \le 7.5\times 10^{-14}$），Case A/A2/E 经由几何反算，Case B/C 验证几何残差 |
| 强制求解计划 P4/P2/I3 | §7 / Analytic supports | **生产已接入 + 制造解通过** | 单元测试全覆盖；输出严格基于 $x_0$，无中点误差折中；`BSurfaceEvaluation.EvaluateInDomain` 显式拒绝越界参数避免线性延拓产生伪根 |
| 节点与区间一侧导数 (D0–D2) | §16 / Knot evaluation | **生产已接入 + 制造解通过** | 显式传递 `ChartSide.Left` / `Right`；避免跨节点平均平滑 |
| 诊断型 Auto 计划动态切换 | §7.7 / Stagnation | **生产已接入 + 制造解通过** | 仅在 Auto 模式下触发；强制计划绝不静默切换；记录 `PlanSwitched` |
| 参数延拓 (Continuation) | §17.3 / Native parameter | **生产已接入 + 制造解通过** | 原生参数递增，步长受控，安全拒绝无分辨率步长与分支歧义，多 seed 后备受 SameLocalBranch 严格约束 |
| 局部细分 (Local Subdivision) | §17.5 / Midpoint ladder | **生产已接入 + 制造解通过** | 递归二分与对称探测，中间点步进强制校验同分支连续性，防止跨分支假收敛 |

### 2. 数值与代数求解层 (Numerics & Solvers)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| 分块 Schur 补求解 (BlockSchurSolve) | §11 / $nx+nz \le 8$ | **内部入口可用 + 制造解通过** | 工作区已扩展至 64 double；严格尺寸契约；$nz \ge 5$ 绝无越界崩溃 |
| 单边 Hestenes-Jacobi SVD | §14.4 / SmallLinearSolve | **生产已接入 + 制造解通过** | 替代 $A^T A$ Gram 构造，避免小奇异值截断丢秩；引入矩阵 max-norm 尺度归一化与未收敛诊断；极值尺度 $10^{\pm 200}$ 测试通过 |
| 小型线性方程组求解 (LU / QR / Cholesky) | $n \le 6$ | **生产已接入 + 制造解通过** | 行列选主元，无动态内存分配，完全 AOT 兼容；`TrustRegionStep.MaxSmallSystem = 6` 与 `SmallLinearSolve` 对齐 |
| Trust-Region 狗腿步与冻结残差缩放 | §14.1 / §14.3 | **生产已接入 + 制造解通过** | 接受态残差与 Jacobian 尺度冻结，试探步复用，严格单调下降 |
| 局部根区间认证 (IntervalRootCheck / Krawczyk) | §18.3 / §18.4 | **生产已接入 + 制造解通过** | `TryCertifyI3` 实现全链路向外舍入区间算术：覆盖中心残差 $[F](x_0)$、平面/球/柱/锥/环面的精确区间梯度范围 `GradientRange`、圆锥盒 $X$ 全域单叶校验 $R+kz_{\rm lo}\ge 0$ 及环面降相关性展开，明确区分 `CertifiedUniqueRoot` / `CertifiedNoRoot` / `Inconclusive` / `BoundsUnavailable` |
| 嵌套精度自适应 ($\eta_k$ / InnerAccuracyInsufficient) | §15 / Nested budget | **扩展点 / 内部机制** | 已实现重算与半径收缩分支，但在纯解析主线上解析求值无子容差需求，属于等价操作 |

### 3. 缓存与多 Seed 架构 (Caching & Multi-Seed)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| L0/L1/L2/L3 四级几何求值缓存 | §13 / Session & Op | **生产已接入 + 制造解通过** | L0 `ResidualMemo` 在 `ICurveCorrection.Refine` 内接入完整参数态试探步去重；L2/L3 键包含 Tag/Gen/Side/Plan/Epoch；CLOCK 淘汰；Bit-exact 查中；Left/Right 侧向全生命周期一致 |
| 辅助状态载荷 (SampleWitness) | §13.2 / L2/L3 payload | **生产已接入 + 制造解通过** | `SampleWitness` 按值嵌入 `CurveSample`，无动态借用；在 `ICurveEvaluation` 与 `GeometryEvaluationCache.EvaluateICurveThroughL3` 中自动恢复双支持面 UV 见证并在 L2/L3 间持久传递，支持下游 `BlendContactFrame.FromWitness` 零投影恢复接触点 |
| 多 Seed 确定性排序 (ICurveMultiSeed) | §13.6 / T09 | **生产已接入 + 制造解通过** | 已接入 `SubdivideTo` 恢复路径；对参数无分辨率/超预算/分支歧义安全拒绝，候选根经 `SameLocalBranch` 校验同分支后方可采纳 |
| 预测种子 (PredictedOnly Hermite) | §13.4 / L2 seeds | **生产已接入 + 制造解通过** | 仅用作 Corrector 初值，绝不冒充 Exact Hit 发布 |

### 4. 终止区间与特殊曲面 (Terminator & Blends)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| Terminator 1-面 / 2-平面区间构造 | §6 / TerminatorAnchor | **内部入口可用 + 制造解通过 + 受 GATE-T 限制** | 导数在根点求值；正交双平面方程对；以分支点 $B$ 切向（结合曲面 sense 与曲线 sense 定向）一阶预测初始化 $\mu$ 并按局部弦尺度校验分支位移；受 GATE-T 隔离 |
| Terminator 参数化 2×2 求解 | §6.3 / SolveParametricTwoByTwo | **内部入口可用 + 制造解通过 + 受 GATE-T 限制** | 求解弦向与法向两正交平面方程对，残差阈值按局部弦长与曲面尺度自适应缩放；生产入口受 GATE-T 隔离 |
| Terminator 构造性支持面选面 (p.49 规则) | §6.2 / SelectSupportSurface | **内部入口可用 + 制造解通过 + 受 GATE-B 限制** | 已引入类型化 `SurfaceSupportRef`（区分 `PublicSurface` 与 `BlendBoundConstructive`）并接入 `ICurveView` 与 `TerminatorEvaluation.Prepare`；公开创建入口受 GATE-B 隔离 |
| 滚动球 Blend 接触标架与解析链式 Jet | §10.2 / §10.3 / BlendContactFrame | **内部入口可用 + 制造解通过 + 受 GATE-A 限制** | 支持 `previousArc` 连续 $2\pi$ 周期展开及 `TryBuildFrameJet` 全解析一阶/二阶链式求导（无有限差分近似） |
| 滚动球 Blend 4×4 / 3×3 求解器 | §10.4 / BlendEnvelopeSolve | **内部入口可用 + 制造解通过 + 受 GATE-A 限制** | RV-TUBE 制造解收敛；§10.6 `BlendArcBounds` 支持自定义截面参考轴与起止角过滤伪装根，贯穿 `TryRecoverAfterLocalSingular` 恢复梯与 Joint 出口 |
| 扫掠/旋转面局部轴消元 (SweptSpunImplicit) | §9.4 / Swept & Spun | **内部入口可用 + 制造解通过** | 采用误差无损 FMA 补偿点积（`TryFmaWithResidual`）消除大坐标平移下的灾难性抵消，保持全局刚体平移不变性 |
| Blend 联合求解残差装配 (JointBlendResidual) | §11.2 / Joint assembly | **内部入口可用 + 制造解通过** | 已检查支持面求值状态，失败面绝不静默置零，杜绝假收敛 |
| 距离函数组合 (BlendBoundComposition) | §10.2 / Local distance | **内部入口可用 + 制造解通过 + 受 GATE-B 限制** | 提供强制校验 `DistanceGrade.Exact` 的类型化 `OrientedDistanceJet` 重载与全输入有限性守卫；球与圆柱 Hessian 轴向投影及三阶导数通过闭式制造解校验 |

### 5. 数据管线与 Parasolid 兼容性 (Pipeline & Oracle)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| XT INTERSECTION 实体写入与回读 | §19 / XtWriter | **生产已接入 + 真实 PK 兼容通过** | 写入生产已接入并获真实 PK 接收验证；XT 回读仅在测试层闭环验证；与 PKToy 绑定互相兼容 |
| UV Null 语义与点容差解耦 | §19 / PrepareEvaluation | **生产已接入 + 真实 PK 兼容通过** | 仅成对允许 (NaN, NaN)；面点容差与 ChordalError 解耦 |
| Sense 拓扑方向双向保持 | XT Schema / Records | **生产已接入 + 真实 PK 兼容通过** | 贯穿 Record / Binding / XT Parse / Writer，数据结构支持拓扑方向传递 |
| Cone 轴向 XT/PK 约定 (Cone Axis Parity) | §8.3 / XT25 / SCH_37102 | **生产已接入 + 真实 PK 兼容通过** | 目标 build/schema (v380 / 37102) 上实测 XT 与 PK_CONE_ask 的 axis/ref 轴向不翻转、数值在指定容差 (1e-14) 内保持，当前实现保持既有约定；实测与早期 XT25 文档引文存在冲突，保留为持续跟踪的兼容性专项（见 `docs/reviews/cone_probe_evidence/`） |
| Live Parasolid Receive / Eval 对比 (Case F / F2) | Oracle Case F & F2 | **生产已接入 + 真实 PK 兼容通过** | 我方写出均匀与非均匀弦长（$f_i/f_{i-1}\ne 1$）XT 由真实 Parasolid 加载并在同参数求值，区间上界 `TMax`、同参数 D0/D1 及 D2 主法向与曲率均达机器精度一致（$\le 7.5\times 10^{-14}$）；原生 D2 向量切向差异在 `GATE-D` 中跟踪 |

---

## 门禁状态与暂未开放范围 (Gated & Unsupported)

| 门禁代号 / 项 | 当前状态 | 阻塞条件 / 隔离行为 |
|---|---|---|
| **GATE-T** (Terminator $t_E$ vs PK) | **OPEN** | 生产入口在规则未指定时返回 `CompatibilityGateOpen`，不猜参数 |
| **GATE-B** (BlendBound 角色映射) | **OPEN** | 距离函数组合未接入公开求值；不支持非解析 BlendBound 支持面 |
| **GATE-A** (Blend 弧段极值验证) | **OPEN** | Envelope/Joint 求解器保留在内部，生产 Auto 计划仍保持纯解析支持面 |
| **GATE-D** (高阶导数公开契约) | **OPEN** | `KernelRuntime` 公开入口严格限制 `order <= 2`，高阶直接拒绝 |
| **过程面 / 偏置面 / 扫掠面支持** | **Unsupported** | `PrepareICurveView` 精确返回 `Unsupported` |
| **Spindle / Apple Torus** | **Unsupported** | 当 $a \le b$ 时精确拒绝 |
| **伪弧长作为公开曲线参数** | **Forbidden** | 严格禁止在公开接口暴露内部伪弧长参数 |

---

## 验证套件运行方式 (Verification Commands)

严格遵守 `AGENTS.md` 进程防挂起管理规范：

```sh
# 1. 运行核心单元测试套件（无驻留、自动清理 testhost）
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f '[t]esthost.dll' 2>/dev/null; true

# 2. 运行真实 Parasolid Oracle 对比脚本（验证 Case A–F 及高精度一致性）
P_SCHEMA=third_party/parasolid/schema dotnet run scripts/IcurveEvaluationOracle.cs

# 3. 运行求值基准测试
dotnet run scripts/IcurveEvalBenchmark.cs
```
