# T00 基线审计 + 实施证据 — icurve / blend 求值（第六轮：T19 Runtime 子集 + T20 骨架）

日期：2026-09-18（第六轮）。前五轮证据不变。

## 第六轮提交范围

| 任务 | 内容 |
|---|---|
| T19 子集 | `KernelRuntime.PrepareEvaluation.cs`：`TryPrepareICurveView` / `TryBindICurveEntity`；`EvaluateCurveCore` 接入 `CurveClass.ICurve`；order&gt;2 → `too_many_derivatives`；删除路径 `FreeICurveData`；Runtime 测试 `IcurveRuntimeEvalTests`（plane∩sphere） |
| T20 骨架 | `scripts/IcurveEvalBenchmark.cs` + `docs/icurve_evaluation_benchmarks.md`；能力矩阵更新 |
| 明确未做 | 真实 Parasolid oracle（NotRun）；XT receive→icurve 实体；GATE-B/T/A/D 仍 open |

## 第六轮验证

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
  → Passed!  Failed: 0, Passed: 372, Skipped: 0
allocation IL guard: CLEAN (1004 methods)
dotnet run scripts/IcurveEvalBenchmark.cs  → 冷/热 ns 报告成功
```

---

# （保留）第五轮证据 — T17/T18

日期：2026-09-18（第五轮）。前四轮证据（T00–T16，见下文保留章节）不变。

## 第五轮提交范围（T17 + T18）

| 任务 | 内容 |
|---|---|
| T17 | `EvaluationBudget`（共享 4096 基础求值预算）、`ICurveContinuation`（§17.3 原生 t 延拓 + §17.5 局部细分）、常规区间仅在 `NotConverged`/`NumericalFailure` 时回退；`ICurveEvalDetail` 增补 `BudgetExceeded`/`AmbiguousBranch`/`Stagnation` |
| T18 | `IntervalRootCheck`：plane/sphere/cylinder 的 I3 Moore–Krawczyk，Empty/Unique/Undetermined/BoundsUnavailable 分型；Newton 采样不得标 certified |
| 文档 | `docs/icurve_evaluation_capabilities.md` |

## 第五轮验证

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
  → Passed!  Failed: 0, Passed: 369, Skipped: 0
allocation IL guard: CLEAN (997 methods)
```

## 第五轮已知限制

1. T19 Runtime/`PK_CURVE_eval` 与真实 Parasolid oracle 仍 NotRun；GATE-B/T/A/D 仍 open。
2. 区间认证未覆盖 cone/torus/B-surface（BoundsUnavailable）。
3. 伪弧长仅作内部推进坐标的规格允许项，本轮未实现独立伪弧长路径（固定参数延拓已足够常规 I3）。

---

# （保留）第四轮证据 — M2 收尾 + T16 完整版

日期：2026-09-18（第四轮）。前三轮证据（T00–T15，见文末保留章节）不变。

## 第四轮提交范围（M2 未完成部分 + M3/T16 完整版）

| 任务 | 内容 |
|---|---|
| M2 收尾（L1） | `SolveStateBuffer`（§13.3 trial/accepted 双缓冲，accepted 态与信赖域半径在拒绝步下位级不变，caller-owned 双 span，零分配）+ `ICurveCorrection`（§14.3/§14.4 dogleg 信赖域驱动校正器：每步 trial 经 SolveStateBuffer 提交/回退，ρ≥0.1 接受并更新半径，拒绝仅缩半径；接受预算 20、trial 预算 48，§14.7）+ 五个常规计划（I1/P2/I3/I2/P4）快速 Newton 预算耗尽后的自动 fallback 接入；导数链复用原有"根态 → 同分解多右端"代码，未改动 |
| T16 完整版 | 查询分类补全（`StartTerminatorInterval`/`EndTerminatorInterval`/`ExactTerminator` 取代占位 `TerminatorUnsupported`，§6.1）；`TerminatorParameterRule`（Unresolved=生产默认，ExtensionRatio/TangentMatching 为 §6.5 点名的两个候选重建规则）；`TryResolveTerminatorParameter`（T 基切向 T=unit(∇φ₀×∇φ₁)，弦/边界余弦守卫，非有限/无序即拒绝）；`Prepare`（选面含梯度-零奇异检测、w 的 branch-tangent 后备、B 与 chart 边界一致性校验、e×w 退化拒绝）；区间求值（一面两平面 → 带符号变号括区保护的 1D Newton/Bisec 标量解，根按 branch witness 连续性选择，§6.3）；§16.3 标量导数 D1/D2（μ″=−x′ᵀHx′/(∇φ·v)，Q″=0）；§6.4 契约：未选面残差以 `NonDefiningResidual` 进 report 作诊断、绝不算第四方程；`ICurveEvalDetail.CompatibilityGateOpen`（§18.5）落 report |
| 端点契约 | ExactTerminator：D0 = 导入端点位级一致（与 ChartPoint 同规则），≥1 阶请求在 GATE-T 关闭前拒绝且不发布半份结果（§6.5/§18.5） |
| 顺带修复 | `EvaluateChartPoint` 末节点锚点 bug（D0/seed 曾取 `ChartPositions[Length−2]`）；`SolveI2` 基点 bug（§7.4 消元平面必须过 Q(t)，曾用 seed 当基点——Hermite 预测 seed 不在弦平面时解到错误交线点） |

## 第四轮关键实现事实

- **信赖域 fallback 的 Jacobian 时效**：接受步提交后必须在新的 accepted 态重算残差+Jacobian 并刷新 master 副本，否则下一步 dogleg 用过期线性模型（初版曾复用 scratch 导致此隐患，代码评审期修正）。
- **I1 fallback 后梯度时效**：I1 的导数链复用循环内 `gradient`；fallback 移动 μ 后该值过期，需在 refined root 重取（其余四计划的导数链本来就重取根 jets，不受影响）。
- **I2 的 in-plane 基与 Q(t)**：基由 chord unit 的最不平行轴构造、每原始段固定；基点必须是 Q(t)。ξ 初值取 (seed−Q) 在基上的投影，z 向偏差可被 u=ẑ 型基吸收。
- **terminator 参数不在 XT 中传输**：t_T 只能由规则重建；两个候选（延拓比例=除以弦对齐余弦、切向匹配=乘以边界 dt/ds）都是实验对象，report 不区分规则版本（后续 oracle 比较时再扩展）。
- **锥顶点终结点的几何事实**：平面过锥顶点时 ∇φ_cone ∥ 弦方向恒成立，T·e_chord=0 → 延拓不存在 → Singular。这是正确的守卫行为，不是缺陷（选面规则的奇异分支已由既有 TerminatorSelectionTests 单测覆盖）。
- **ref struct 的 `in` 参数逃逸**：`ICurveView` 构造器对 `TerminatorLimit`（小只读 struct）按值传参，避免 ref struct 返回路径上的逃逸分析报错。
- **存在性判定**：`TerminatorLimit` 以 `TermUse != Unset` 且两 hvec 有限判定存在；`default` 不会伪装成 (0,0,0) 处的终结点。

## 第四轮验证要点（28 项新测试，合计 359 全绿）

- SolveStateBuffer：提交覆盖 accepted、拒绝步 accepted 位级不变、新 trial 从 accepted 重启。
- Refine 等价性：圆（I1/P2/I3/I2/P4）与柱/球双分支（P2/I3/I2/P4）fixture 上从扰动 seed（含拉向 z=−1 侧）收敛，residual ≤ 1e-12；I3 状态与快路径根 1e-10 一致、分支保持 z=+1。
- fallback 集成：构造远处 CorrectedRoot bracket 毒化 Hermite seed → 快速 I3 Newton（8 步上限）耗尽 → 信赖域接管收敛；`NewtonIterations > 8` 证明 fallback 实际运行；D0/D1/D2 与快路径 jets 一致（1e-9/1e-8/1e-6）。
- I2 基点回归：径向偏离弦平面的 verified bracket 喂入 off-plane seed，发布根的弦平面残差 ≤ 1e-12（修复前该残差 ≈ 0.1 量级）。
- terminator：门控拒绝（Unresolved → Unsupported+CompatibilityGateOpen+零发布）；两规则解析有序有限 tT（start/end 双侧）；平面选中时区间点=弦点 Q(t) 的闭式验证（D1=Q′ 精确、D2=0、未选柱面残差 ≈ 弦垂度 > 1e-6 作诊断）；柱面选中非线性路径 D1/D2 与 D0/D1 中心差分互检（§21.2）；off-circle 端点（z=0.2）柱面选中 → 平面偏差诊断可见；ExactTerminator D0 位级一致 + 高阶请求门控零发布；chart 边界节点契约不受 terminator 影响；越界 tT+1 → OutsideSupportedDomain；branch point 不一致 → InvalidInput 不吸附；锥顶点退化弦 → Singular 诊断。

## 第四轮运行证据

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
  → Passed!  Failed: 0, Passed: 359, Skipped: 0        （exit 0；331 存量 + 28 新增）
MSBUILDDISABLENODEREUSE=1 dotnet build src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
  → allocation IL guard: checked 955 methods, CLEAN
P_SCHEMA=../third_party/parasolid/schema dotnet run scripts/VerifyKernel.cs
  → 测试 359/359、function catalog 83、allocation guard CLEAN、native publish、
    AbiSmoke、各 schema receive/oracle 项照常通过；唯一 FAIL 集合与第一轮记录的
    干净 HEAD 基线完全一致（旧 schema PK_PART_receive_b 922/1094，预先存在的
    环境限制，归 T19/T20），本轮无新增失败。
```

## 第四轮已知限制

1. **GATE-T 仍 open**：两个候选规则的 t_T 尚未与真实 PK 端点参数比对（归 T19 oracle）；生产入口（`Evaluate`/`EvaluateWithPlan`）保持 Unresolved，terminator 区间查询返回 `CompatibilityGateOpen`，仅显式规则的实验入口可用。
2. **Refine 的拒绝-回退**由 SolveStateBuffer 单测直接覆盖 + 代码路径评审；未在 Refine 内部构造确定性拒绝序列（解析支持面无参数域出口，自然拒绝难以确定性制造；P2/P4 域出口拒绝随 B-surface 支持面任务验证）。
3. **terminator 区间标量解的括区扫描**（±chord/24 样本 + 二分 ≤60）只在 Newton 失效时触发；预算未接入共享 4096 基础求值计数器（归 T17 预算统一）。
4. 审计前三轮限制 2（包络-隐式面专用联合系统随 T17）、3（B-surface 扩展域守卫随 B-surface 支持面）、4（接触 witness → frame 导数传播依赖准备图 jet 闭包，非 T11–T16 任务行范围）、5（oracle NotRun 归 T12/T15/T16/T19 验收）继续有效。
5. 全部 Parasolid oracle 四轮均 NotRun。

---

# （保留）第三轮证据 — T11–T14 + T15 数学模块

## 第三轮提交

| 提交 | 任务 | 内容 |
|---|---|---|
| `a254845` | T11 | LocalDistanceEvaluation：法向坐标 K 消元（(u,v,h) 3×3 Newton + Kᵀ 敏感度 Hessian，无显式逆）、条件代理守卫、offset 距离能力（仅 Exact 级距离允许 d−a 消元）+ 8 测试 |
| `aae564a` | T12 | BlendEvaluation：BlendFrameInput 快照式 frame、§10.3 参数 D1/D2 全公式装配、TryBuildFrame（atan2 有向弧、近重合/对径/零切向拒绝）、IsRegularConstruction（range 不一致拒绝不平均）、§10.5 局部隐式值 + 7 测试 |
| `bd3a477` | T13 | BlendImplicitEvaluation：§10.4 包络残差装配（(E₁)_s 永不提前置零）、§10.5 局部标量隐式 1D 消元（φ_B 与 d_B 分型输出、缩放 D 比例守卫）、TryValidateArc（整圈管面冒充根拒绝 + 周期 lift）+ 6 测试 |
| `97c858a` | T14 | BlockSchurSolve（§12.3 分块 Schur，F_z·b 保留、病态 Hz 拒绝）、JointBlendResidual（§12.2 六未知量 residual/Jacobian、未归一化 w、Mᵀq−w c 块）+ 5 测试 |
| `096ae0e` | T15 数学模块 | BlendBoundComposition：§11.1 文档字面式 F_B（value/gradient/Hessian 含 r₁·D₃ 收缩项）、球距离 jet 辅助；GATE-B open，不接生产路径 + 3 测试 |

## 第三轮关键实现事实

- **法向导数乘积法则符号**：∂(Su×Sv)/∂u = Suu×Sv **+** Su×Suv——初版误写减号导致球面 K-Newton 线性收敛不达标；由逐迭代诊断定位。
- **K 敏感度 Hessian**：∇²d = [n_u n_v]·[K⁻¹]₁:₂,:，两个 Kᵀ 右端求解取得 K⁻¹ 前两行，无显式逆（§9.1）。
- **cone 线性距离的代数性质**：足点恒落在有效母线上（grade=Exact 正确），其 offset 消元因此合法；"FirstOrderEstimate"仅是文档措辞，实现按足点验证分级。
- **frame 是 per-u 快照**：BlendEvaluation.Evaluate 的 u 实参不移动 frame，u 方向差分必须重建 frame（测试曾在此出错）。
- **readonly struct 初始化**：类型外不能对 readonly 字段用对象初始化器，统一构造函数模式（与 CurveSample/FootPointJet 一致）。
- **GATE-A/D 全程保持 open**：T12 的弧展开策略、公开延拓范围、端点导数契约未实现未声明；T13 的极端退化（对径）拒绝而非猜分支。

## 第三轮验证要点

- T11：K 消元与闭式解析距离（球/柱/平面/锥）独立双路径互证（值 1e-10/梯度 1e-9/Hessian 1e-7）；Hessian·n≈0 一致性度量；轴/中心奇异拒绝；offset 正确形式（d−a）与冒充形式（φ−a）在 offset 面上数值分离（0 vs 0.75）。
- T12：RV-TUBE 全量（位置/D/隐式梯度）；双平面 fillet 闭式（圆柱 (x−1)²+(z−1)²=1）+ 中心差分（§21.2，u 方向重建 frame）；接触互换 = 同弧反向参数化。
- T13：φ_B Hessian 与"重解 s 的梯度场中心差分"互证；d_B 与 torus 精确距离（profile 角不变性）1e-10 一致；spine 圆曲率中心（torus 中心）D→0 触发守卫拒绝；对径接触拒绝。
- T14：RV-LIFTED-J 六维 residual+36 项 Jacobian 1e-12 全量；联合 Newton 从 RV 离根状态收敛到根（c 回到 R=2 圆柱）；RV-SCHUR Schur 步 = 完整 5×5 LU 步（1e-12），F_z·b 贡献 > 0 断言；rank-1 Hz 返回 Singular 保持联合系统。

## 第三轮运行证据

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
  → Passed!  Failed: 0, Passed: 331, Skipped: 0        （exit 0，含 T15 数学模块）
MSBUILDDISABLENODEREUSE=1 dotnet build src/ProjectGmKernel.Native/...csproj
  → allocation IL guard: CLEAN
```

## 第三轮已知限制

1. T14 的六未知量编译仅覆盖常规双支持 spine 片；ChartPoint/terminator 区间按 §12.2 拒绝套用（terminator 专用编译仍归 T16 完整版）。
2. T13 的包络-隐式面 4×4/3×3 联合系统未单独实现（T14 的六未知量 lowering 是其超集形态；专用版本随 T17 延续/切换策略一起做）。
3. T11 的 B-surface 扩展域守卫随 B-surface 支持面任务实现（当前无 B-surface 求值可夹紧）。
4. T12 frame 导数由调用方链式提供（§10.3）；接触 witness → frame 导数的完整传播随 T15/T16 的依赖图任务实现。
5. **全部 Parasolid oracle 两轮均 NotRun**（归 T12/T15/T16/T19 验收），GATE-A/B/D 证据需求未满足，相应发布路径未开启。

---

# （保留）第二轮证据 — T07 完成、T08、T09、T10

| 提交 | 任务 | 内容 |
|---|---|---|
| `ec0aca6` | T07 完成 + T08 | P4/P2/I2/I1 四计划求解器（§7.1–7.5）+ 各计划 D1/D2 导数链（§16.1–16.2）+ witness 恢复 + 计划选择规则 + 9 测试 |
| `48e4f0f` | T09 | EvaluationSampleStore（L2 操作级样本库）、精确/近邻命中语义、缓存接入求值 + 7 测试 |
| `de1de51` | T10 | GeometryEvaluationCache（L3 跨调用缓存）、单调 ModelGeometryEpoch、CLOCK 淘汰、生命周期挂钩 + 6 测试 |

第二轮关键事实：SurfaceDerivativeLayout.GetIndex(u,v)=u·(VOrder+1)+v（首列 Sv）；I1 直线基点须沿 b 方向投影；witness 恢复为近表面投影语义；L3 arena 用 SessionMemory 页分配（避开 BlocksLiveBytes 基线污染）；epoch 由创建/删除/回滚/session 起止传播。

---

# （保留）第一轮证据 — T00 基线审计 + T05/T03/T04/T16 子集

- 规格基线 `5c645cd`，第一轮 HEAD `747bc88`（仅设计文档提交），代码事实逐条核对成立。
- 第一轮提交：T05 `5814e11`、T03 `92e8df8`、T04 `1ea019e`、T16 子集 `2e503e1`、VerifyKernel P_SCHEMA 修复 `24aa5aa`、T01 `cc85d16`、T02 `5090a10`、T06 `31c5634`、T07 切片 `9c05d62`。
- 兼容 Gate 全程 open（GATE-B/T/A/D）；chart 弦平面已解决按新版实现。
- **未运行任何 Parasolid oracle**（NotRun）；VerifyKernel 后段 corpus matrix 依赖本机 PKToy native corpus 产物（干净 HEAD 同样失败），归 T19/T20。
- flaky 状态泄漏已定位为测试类缺 IDisposable（`c0586af`，他人修复）。

## 第二轮提交（接续第一轮 9 个提交）

| 提交 | 任务 | 内容 |
|---|---|---|
| `ec0aca6` | T07 完成 + T08 | P4/P2/I2/I1 四计划求解器（§7.1–7.5）+ 各计划 D1/D2 导数链（§16.1–16.2）+ witness 恢复 + 计划选择规则 + 9 测试 |
| `48e4f0f` | T09 | EvaluationSampleStore（L2 操作级样本库）、精确/近邻命中语义、缓存接入求值 + 7 测试 |
| `de1de51` | T10 | GeometryEvaluationCache（L3 跨调用缓存）、单调 ModelGeometryEpoch、CLOCK 淘汰、生命周期挂钩 + 6 测试 |

## 第二轮关键实现事实

- **布局索引陷阱**：`SurfaceDerivativeLayout.GetIndex(u,v) = u·(VOrder+1)+v`，首列是 Sv 而非 Su。P2/P4 初版把 index 1/2 当 (Su,Sv)，Jacobian 两列互换导致牛顿发散——由五计划分计划 Theory 定位，改用显式 `GetIndex` 修复。
- **I1 直线基点**：支持平面 ∩ 参数平面的交线基点必须沿 b 方向投影（x0 = Q + α·n′，n′ = n−(n·e)e），沿法向投影会除以恒为零的 n·(n×e)。
- **witness 恢复语义**：`TryRecoverWitness` 对近表面点返回最近片参数（投影意义），不做在面校验（P4 需要用弦点做 seed）；在面精确性由往返测试覆盖。
- **L3 内存路径**：arena 使用 SessionMemory 页分配（`memory->TryAllocate`），不用 Blocks（避免污染 `BlocksLiveBytes` 分配基线——曾导致 B-curve 基线测试 +32768 字节失败）、不用 scratch/return arena（§13.9）。分配门禁 CLEAN。
- **epoch 传播**：创建（AllocateCurve/SurfaceSlot）、删除（EntityDeleteImplementation）、回滚（MarkGotoImplementation）、session 起止均单调递增 `ModelGeometryEpoch`；缓存写入不动 epoch（有测试锁定）。
- **淘汰**：CLOCK 双扫，条目为值记录（无借用指针），驱逐不可能悬空；容量 256 条硬预算。

## T07/T08 验证要点

- 五计划等价（§21.1 常规计划行）：plane/cylinder（圆）与 cylinder/sphere（双分支 z=±1）两个 fixture 上 P4/P2/I3/I2/I1（后者无平面→I1 正确拒绝）位置 1e-10、D1 1e-9、D2 1e-7 一致；分支保持 z=+1。
- 容差按 §21.7 记录于测试文件常量（PositionTol/D1Tol/D2Tol）。
- D2 与 D1 的中心差分互检（两步长 1e-4/1e-5，§21.2）；D1 与 §5.5 切向公式独立互检。
- ChartPoint 契约对任意计划成立：锚点位级一致（15 位）。
- 报告含 Plan/Side/CacheHit（§18.1/§19.2）。

## 第二轮运行证据

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
  → Passed!  Failed: 0, Passed: 302, Skipped: 0        （exit 0）
MSBUILDDISABLENODEREUSE=1 dotnet build src/ProjectGmKernel.Native/...csproj
  → allocation IL guard: checked 886 methods, CLEAN
```

## 第二轮已知限制

1. **L1 trial/accepted 双缓冲**未实现：当前快路径 Newton 无接受/拒绝驱动（信赖域驱动随 T14/T17 到来）， EvaluationSampleStore 的"失败不写"语义部分覆盖该验收项。
2. **L0 memo**以"根处一次布局求值同时服务 J/D1/D2"的形式隐式存在（各计划求解器内），无独立结构。
3. epoch 为进程级单调计数（串行调度下正确）；并行失效传播归后续 Runtime 隔离任务。
4. I2 的"参数平面浮点一致性检查"未做（§7.4 允许延后）；I2 目前作为 I3 的消元对照路径存在。
5. P2 固定以 support0 为参数侧；翻转侧变体随非解析支持面（B-surface）任务到来。
6. 继承第一轮所有限制（oracle 未运行等）。

---

# （保留）第一轮证据 — T00 基线审计 + T05/T03/T04/T16 子集

## 实际 HEAD 与规格基线差异

- 规格固定基线：`5c645cdfc8a0cd8e3f6aac4c25de839653b4daf6`。
- 第一轮实施时实际 HEAD：`747bc88c3c9636d482cf28e90354b016c6f5747d`（仅设计文档入库提交）。
- 结论：规格对基线的代码事实描述逐条核对成立。

## 代码事实核对（对照规格 §3.1，第一轮）

| 规格声明 | 核对结果 |
|---|---|
| `KernelRuntime.Evaluation.cs` 已有解析/B-curve/B-surface/SP-curve/offset/swept/spun 分支 | 成立 |
| `ICurve` 与 `BlendSurface` 尚未接入 | 成立（default → Unsupported；T07/T08 后数学层已就绪，runtime 接线归 T19） |
| `ICurveData` 等已声明未完成 | 成立（T01 已补池、T02 已补解码） |
| `PoolKind` 无 icurve/blend 池 | 成立（T01 已补 47–51） |
| `SurfaceRecord` 无 Sense | 成立（T01 已补） |
| 静态分派/视图/caller-owned workspace | 成立 |
| `AlgorithmStatus` 齐备 | 成立 |

## 兼容 Gate（两轮均保持 open）

| Gate | 状态 | 处理 |
|---|---|---|
| GATE-B | open | 距离组合模块未实现；ImplicitJet/OrientedDistanceJet 已分型 |
| GATE-T | open | 仅实现选面规则与一面两平面；t_E 重建未实现未声明 |
| GATE-A | open | 本两轮不实现 blend 参数求值 |
| GATE-D | open | 未触碰公开 PK API |
| chart 弦平面 | 已解决 | 按弦投影实现 |

## 真实 PK / oracle

- `third_party/parasolid` 静态库与头文件在位（PKToy 构建链接）。
- **未运行任何 Parasolid oracle**（NotRun，不计为通过证据；归 T12/T15/T16/T19）。
- VerifyKernel 后段 corpus matrix oracle 检查依赖本机 PKToy native corpus 产物，干净 HEAD 同样失败（第一轮已验证），归 T19/T20。

## 第一轮提交

| 提交 | 任务 |
|---|---|
| `5814e11` | T05 数值模块 |
| `92e8df8` | T03 解析隐式/距离 jet |
| `1ea019e` | T04 chart 参数映射 |
| `2e503e1` | T16 子集 terminator |
| `24aa5aa` | VerifyKernel 相对 P_SCHEMA 修复（既有缺陷，干净 HEAD 复现确认） |
| `cc85d16` | T01 存储池/sense/构造引用 |
| `5090a10` | T02 INTERSECTION 解码 |
| `31c5634` | T06 周期展开/seed 排序 |
| `9c05d62` | T07 切片 I3 + ChartPoint |

（第一轮测试细节与文件清单见该轮版本的本文档；flaky 状态泄漏问题已由他人定位修复——系新测试类缺 `IDisposable` 声明，提交 `c0586af`。）

## 实际 HEAD 与规格基线差异

- 规格固定基线：`5c645cdfc8a0cd8e3f6aac4c25de839653b4daf6`。
- 实际 HEAD：`747bc88c3c9636d482cf28e90354b016c6f5747d`。
- 差异：基线之后仅一个提交（`747bc88 添加了icurve的设计`），即本规格与交付包（`implementation_tasks.json`、`reference_vectors.json`、`compatibility_gates.json`、PDF）入库。无几何/求值代码变化，工作树在实施开始时干净。
- 结论：规格对 `5c645cd` 的代码事实描述（R3/R4/R5）逐条核对后仍成立。

## 代码事实核对（对照规格 §3.1）

| 规格声明 | 核对结果 |
|---|---|
| `KernelRuntime.Evaluation.cs` 已有解析/B-curve/B-surface/SP-curve/offset/swept/spun 分支 | 成立（`EvaluateCurveCore`、`EvaluateSurfaceCore` 的 switch） |
| `ICurve` 与 `BlendSurface` 尚未接入 | 成立：`CurveClass.ICurve` / `SurfaceClass.BlendSurface` 落入 `default` → `AlgorithmStatus.Unsupported` |
| `ICurveData`、`LimitRecord`、`BlendedEdgeData`、`BlendBoundData` 已声明但未完成 | 成立：全部仅存在于 `GeometryRecords.cs`；无读取方、无写入方、无池 |
| `PoolKind` 没有 ICurve / blend 对应数据池 | 成立（`EntityPools.cs`，最大到 `SpunData = 46`） |
| `SurfaceRecord` 无显式 `Sense` 字段，`CurveRecord` 有 | 成立 |
| `ICurveData` 两个 `SurfTag` 不足以表达 `BLEND_BOUND` 支持面 | 成立；`BlendBoundData` 已按构造引用建模（`Boundary` + `BlendTag`），未伪造公开 tag |
| 静态分派、计算视图、caller-owned workspace | 成立：`Geometry/Evaluation/*` 为静态类 + `Span` 工作区；`CommandScratch` 为 per-thread arena |
| `AlgorithmStatus` 已含所需状态 | 成立 |

补充事实（规格未明说，后续任务需要）：

- `PagedEntityPool<T>` 要求池记录首字段为 `RecordHeader`（`Unsafe.As<T, RecordHeader>`）。`ICurveData` / `BlendedEdgeData` 当前**没有**该首字段，不能直接入池 —— T01 存储改造范围。
- hull-vector arena 不存在：`ICurveData.ChartHvecOffset` / `HvecOffset` 指向的数据槽机制尚未建立。
- XT reader 目前不解析 INTERSECTION(38)/CHART(40)/LIMIT(41)/INTERSECTION_DATA(204)/BLENDED_EDGE(56)/BLEND_BOUND(59) 节点。
- build 门禁：`GenerateFunctionCatalog --check` 与 `AllocationBanCheck`（IL 级禁止闭包/装箱/newarr 分配）。本轮新增模块已通过该门禁。
- csproj：`net10.0`、`PublishAot`、`AllowUnsafeBlocks`、`Nullable enable`、`InternalsVisibleTo KernelTests`。

## 兼容 Gate 记录（对照 `compatibility_gates.json`，全部保持 open）

| Gate | 状态 | 本轮处理 |
|---|---|---|
| GATE-B（BLEND_BOUND 角色映射） | open | 距离组合数学模块**未**在本轮实现；`OrientedDistanceJet` 与 `ImplicitJet` 已分型，为 T15 的类型前提 |
| GATE-T（terminator 参数重建/端点导数） | open | 只实现文档明确部分：p.49 OR 选面规则 + 一面两平面构造；`t_E` 重建与端点导数契约未实现、未声明 |
| GATE-A（blend 弧展开/延拓范围） | open | 本轮不实现 blend 参数求值 |
| GATE-D（公开高阶导数契约） | open | 本轮不触碰公开 PK API |
| chart 弦平面定义 | 已解决（2025 版 pp.48–49） | 按弦投影实现，无候选切换 |

## 真实 PK build / oracle 可用性

- `third_party/parasolid/lib/linux-x64/pskernel_archive.a`（静态归档）+ `third_party/parasolid/include` 存在；经 PKToy 构建链接（`scripts/ParasolidScriptHost.cs` 流程）。
- 本轮交付为纯数学模块与单元测试，**未运行任何 Parasolid oracle**（NotRun，不计为通过证据）。oracle 用例属于 T12/T15/T16/T19 的验收范围。

## 本轮交付（对应任务表 T00 + T05 + T03 + T04 + T16 子集 + T01 + T02 + T06 + T07 切片）

### 提交序列（main，本地未推送）

| 提交 | 任务 | 内容 |
|---|---|---|
| `5814e11` | T05 | SmallLinearSolve（LU/QR/最小二乘）、NewtonStep、TrustRegionStep + 10 测试 |
| `92e8df8` | T03 | AnalyticImplicitEvaluation（ImplicitJet + 片守卫）、SurfaceDistanceEvaluation（OrientedDistanceJet）+ 6 测试 |
| `1ea019e` | T04 | OriginalChartParameterMap（§5 递推/弦平面/ψᵢ/侧规则/细分诊断）+ 5 测试 |
| `2e503e1` | T16 子集 | TerminatorEvaluation（选面 OR 规则 + 一面两平面）+ 3 测试 |
| `24aa5aa` | 修复 | VerifyKernel 规范化相对 P_SCHEMA（修复既有环境缺陷，见下） |
| `cc85d16` | T01 | 过程几何池（icurve/blend 系 5 池 + PoolKind + 全部生命周期开关）、SurfaceRecord.Sense、BLEND_BOUND 构造引用 + 5 测试 |
| `5090a10` | T02 | IcurveDecode（节点 38/40/41/204 布局校验与锚点存储、UV null/截断/超长/未知 enum 检测）+ 16 测试 |
| `31c5634` | T06 | ParameterCorrespondence（周期展开保 lift、Hermite 预测）、ICurveSeedSelection（确定性排序、分支歧义）+ 7 测试 |
| `9c05d62` | T07 切片 | ICurveView + ICurveEvaluation（I3 双隐式 Newton、ChartPoint 精确锚点契约、D1/D2 同分解链式导数、越界拒绝）+ 7 测试 |

### T07 切片验证要点

RV-CHART 的三个区间查询（单位圆 = z=0 ∩ x²+y²=1）经 I3 Newton（弦点初值、≤8 步、残差 ≤1e-13·尺度）达到参考值 1e-12 精度（位置/D1/D2）；ChartPoint 返回原始锚点至位级一致（15 位断言）；节点 D2 保持右侧值、不平均两侧；越界查询发布前拒绝且输出缓冲无部分写入；D1 与 §5.5 切向公式独立互检一致。

### 运行证据

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
  → Passed!  Failed: 0, Passed: 273, Skipped: 0        （exit 0；最终状态）
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests --filter "FullyQualifiedName~Icurve"
  → 60 项 icurve 数学/存储/解码/求值测试全过            （exit 0）
P_SCHEMA=../third_party/parasolid/schema dotnet run scripts/GenerateXtSchema.cs -- --check
  → Generated 15 V30-V38 schema bindings (2920 node types)   （exit 0）
```

VerifyKernel 门禁分段结果（完整日志 `temp_docs/icurve-evaluation/` 不留大文件，计数如下）：

- 通过：`dotnet test tests/KernelTests`（238/238）、`GenerateFunctionCatalog --check`（83 descriptors）、`AllocationBanCheck`（825 methods, CLEAN）、`GenerateXtSchema --check`、`GenerateXtSchemaDifferences --check`、native Release publish、`AbiSmoke`、`TopologyDump`、`AllocationBaseline`、各 schema receive/oracle 项（日志含 390+ PASS 行）。
- 失败：`GenerateParasolidXtCorpus --check`（旧 schema 真实 `PK_PART_receive_b` 返回 922/1094）与 `ParasolidSchemaCorpusMatrix --check`（15949 个 "No generated model binding exists"）。**在干净 HEAD（stash 全部本轮改动）上复跑同样失败/直接崩溃**（后者 exit 134，`XtSchemaRegistry` 为空），确认这些 oracle/覆盖检查依赖本机完整的 PKToy native corpus 构建产物，属预先存在的环境限制，与本轮新增代码无关。按 §20.4 如实记为**未通过**，不计入本轮完成证据；相关 oracle 验收归 T19/T20。

### 热路径分配

`AllocationBanCheck`（IL 级扫描 `Runtime`/`Geometry`/`Computation` 全部方法，检查 newarr/闭包/装箱）报告 CLEAN。本轮数值模块只使用 `Span`/`ref struct`/`stackalloc`（上限 `TrustRegionStep.MaxSmallSystem = 16`，超出返回 `WorkspaceTooSmall`，按 §4.4 为显式工作区路径留口）。

## 已知限制（诚实声明）

0. **既有测试套件存在间歇性失败**：全量 `KernelTests` 偶发（约 1/4 概率）出现 `PK_ERROR_rollback_started`(5049) 状态泄漏，失败类不固定（ThreadProtocolTests / GeometryOwnershipTests 均出现过）。已验证**移除本轮新增测试类后该 flaky 依然存在**（8 次全量中 3 次失败），属既有测试隔离缺陷，与本轮改动无关；修复属于测试基础设施任务，未在本轮处理（外科手术原则）。

1. **完整 SVD 未实现**：秩亏由列主元 QR 的 R 对角阈值检测，最小范数解为 pivoted-QR basic solution（非唯一最小范数）。满足 T05/T07 现状；T14 联合系统需要真正 SVD 时补充。
2. **步长模块不含驱动循环**：`NewtonStep`/`TrustRegionStep` 是纯步计算，求值驱动循环（§14.3 线搜索的 Ψ 重评估、§17 continuation）在 T07/T17 的计划编排中实现。
3. **torus 有向距离未提供**（`Unsupported`）；解析隐式已可用。留给 T11。
4. **terminator 参数重建与端点导数**未实现（GATE-T open，规格 §6.5）；`TerminatorEvaluation` 只含文档明确部分。
5. **`ImplicitJet` 到距离组合的隔离**是类型级的：T15 的组合入口只接受 `OrientedDistanceJet`，本轮尚未编写组合模块。
6. **未运行 Parasolid oracle**：NotRun，相关验收（T12/T15/T16/T19 的 oracle 证据）未通过也不声明通过。
7. RV-BBOUND-COMPOSITION（距离组合 chain rule）留待 T15 数学模块，本轮未实现。
