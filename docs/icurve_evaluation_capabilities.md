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
| 原始图表递推与弦平面投影 | §5 / Original chart | **生产已接入 + 真实 PK 兼容通过** | 节点位相同比较；归一化余弦递归计算；弦方向保留 |
| 解析支持面正则区间求解 (I1/I2/Auto) | §7 / Analytic supports | **生产已接入 + 真实 PK 兼容通过** | Auto: 平面走 I1，其他解析面走 I2；Oracle Case A/A2/B/C/E/F 机器精度通过 |
| 强制求解计划 P4/P2/I3 | §7 / Analytic supports | **生产已接入 + 制造解通过** | 单元测试全覆盖；输出严格基于 $x_0$，无中点误差折中 |
| 节点与区间一侧导数 (D0–D2) | §16 / Knot evaluation | **生产已接入 + 制造解通过** | 显式传递 `ChartSide.Left` / `Right`；避免跨节点平均平滑 |
| 诊断型 Auto 计划动态切换 | §7.7 / Stagnation | **生产已接入 + 制造解通过** | 仅在 Auto 模式下触发；强制计划绝不静默切换；记录 `PlanSwitched` |
| 参数延拓 (Continuation) | §17.3 / Native parameter | **生产已接入 + 制造解通过** | 原生参数递增，步长受控，严格保证同分支连续性 |
| 局部细分 (Local Subdivision) | §17.5 / Midpoint ladder | **生产已接入 + 制造解通过** | 递归二分与对称探测，防止跨分支假收敛 |

### 2. 数值与代数求解层 (Numerics & Solvers)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| 分块 Schur 补求解 (BlockSchurSolve) | §11 / $nx+nz \le 8$ | **内部入口可用 + 制造解通过** | 工作区已扩展至 64 double；严格尺寸契约；$nz \ge 5$ 绝无越界崩溃 |
| 单边 Hestenes-Jacobi SVD | §14.4 / SmallLinearSolve | **生产已接入 + 制造解通过** | 替代 $A^T A$ Gram 构造，避免小奇异值平方截断丢秩；病态测试通过 |
| 小型线性方程组求解 (LU / QR / Cholesky) | $n \le 6$ | **生产已接入 + 制造解通过** | 行列选主元，无动态内存分配，完全 AOT 兼容 |
| Trust-Region 狗腿步与冻结残差缩放 | §14.1 / §14.3 | **生产已接入 + 制造解通过** | 接受态残差与 Jacobian 尺度冻结，试探步复用，严格单调下降 |
| 嵌套精度自适应 ($\eta_k$ / InnerAccuracyInsufficient) | §15 / Nested budget | **扩展点 / 内部机制** | 触发收紧后重算系统模型与残差；纯解析主线上无子容差需求 |

### 3. 缓存与多 Seed 架构 (Caching & Multi-Seed)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| L1/L2/L3 三级几何求值缓存 | §13 / Session & Op | **生产已接入 + 制造解通过** | 键包含 Tag/Gen/Side/Plan/Epoch；CLOCK 淘汰；Bit-exact 查中 |
| 辅助状态载荷 (SampleWitness) | §13.2 / L2/L3 payload | **内部入口可用 + 制造解通过** | `SampleWitness` 按值嵌入 `CurveSample`，无动态借用；UV/Spine 读写测试通过 |
| 多 Seed 确定性排序 (ICurveMultiSeed) | §13.6 / T09 | **生产已接入 + 制造解通过** | 已接入 `SubdivideTo` 恢复路径；跨分支严格隔离；共享单次请求预算 |
| 预测种子 (PredictedOnly Hermite) | §13.4 / L2 seeds | **生产已接入 + 制造解通过** | 仅用作 Corrector 初值，绝不冒充 Exact Hit 发布 |

### 4. 终止区间与特殊曲面 (Terminator & Blends)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| Terminator 1-面 / 2-平面区间构造 | §6 / TerminatorAnchor | **内部入口可用 + 制造解通过 + 受 GATE-T 限制** | 导数在根点求值；正交双平面方程对；分支 witness 追踪；奇点切向回退 |
| Terminator 参数化 2×2 求解 | §6.3 / SolveParametricTwoByTwo | **内部入口可用 + 制造解通过 + 受 GATE-T 限制** | 求解弦向与法向两正交平面方程对，残差 $< 10^{-11}$；生产入口受 GATE-T 隔离 |
| TerminatorBLendBound 选面 (p.49 规则) | §6.2 / SelectSupportSurface | **内部入口可用 + 受 GATE-B 限制** | 当前 `surface0IsBlendBound` 保持 false，待引入类型化 `SurfaceSupportRef` |
| 滚动球 Blend 4×4 / 3×3 求解器 | §10.4 / BlendEnvelopeSolve | **内部入口可用 + 制造解通过 + 受 GATE-A 限制** | RV-TUBE 制造解收敛；$\S10.6$ `BlendArcBounds` 与 `TryValidateArc` 过滤伪装根 |
| Blend 联合求解残差装配 (JointBlendResidual) | §11.2 / Joint assembly | **内部入口可用 + 制造解通过** | 已检查支持面求值状态，失败面绝不静默置零，杜绝假收敛 |
| 距离函数组合 (BlendBoundComposition) | §10.2 / Local distance | **内部入口可用 + 制造解通过 + 受 GATE-B 限制** | 球与圆柱 Hessian 轴向投影及三阶导数已修正并通过闭式制造解校验 |

### 5. 数据管线与 Parasolid 兼容性 (Pipeline & Oracle)

| 能力项 | 范围 / 契约 | 状态 | 说明 |
|---|---|---|---|
| XT INTERSECTION 实体写入与回读 | §19 / XtWriter | **生产已接入 + 真实 PK 兼容通过** | 字段完整保真；与 PKToy 绑定互相兼容 |
| UV Null 语义与点容差解耦 | §19 / PrepareEvaluation | **生产已接入 + 真实 PK 兼容通过** | 仅成对允许 (NaN, NaN)；面点容差与 ChordalError 解耦 |
| Sense 拓扑方向双向保持 | XT Schema / Records | **生产已接入 + 真实 PK 兼容通过** | 贯穿 Record / Binding / XT Parse / Writer，Oracle 验证一致 |
| Live Parasolid Receive / Eval 对比 (Case F) | Oracle Case F | **生产已接入 + 真实 PK 兼容通过** | 我方写出 XT 由真实 Parasolid 加载并在同参数求值，位置与导数差 $< 2\times 10^{-15}$ |

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
