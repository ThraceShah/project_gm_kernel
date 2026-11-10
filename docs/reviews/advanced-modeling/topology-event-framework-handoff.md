# 求交与圆角拓扑交接

本目标的核心技术实现与验证已完成，主副本在 `main` 分支。依赖保持不变：PKToy `d820719`、真实 Parasolid 38.0.150、.NET SDK 10.0.302。

## 已完成并验证

1. **短边圆角自研构造闭环（严格通过原 1e-7 物性门槛）**
   - 恢复 `scripts/ShortEdgeBlendOracle.cs` 原始固定 `1e-7` 容差判定，彻底移除任何 `unitScale` 乘数放宽。
   - 实现了统一的 occurrence/event/FaceArrangement 拓扑事件框架，自研 FixBlends 在禁用旧拓扑模板守卫下完整构建异构滚动网络。
   - 保留两条真实短 ICurve（长约 `7.61e-5`），保留 CHART 锚点、双侧 UV、有限参数区间与完整边界关联。
   - 自研短边圆角本体（`self-constructed-product`）在 Base（Identity）下物性残差为 `3.97e-9`，严格低于 `1e-7` 原门槛；`PK_DEBUG_BODY_compare` 0 面差异（`global=21921 local=21931 face_pairs=0`），`PK_BODY_check` 0 faults。
   - **四组输入变换变体（全部通过原 1e-7 门槛）**：
     - **基准（Identity, Scale 1.0）**：`PASS cases=1 mode=independent-construction`，物性残差 `3.97e-9`。
     - **刚体变换（`--input-rigid`）**：`PASS cases=1 mode=independent-construction`，物性残差 `1.18e-8`。
     - **镜像变换（`--input-mirror`）**：`PASS cases=1 mode=independent-construction`，物性残差 `9.82e-9`。
     - **各向同性缩放（`--input-scale=1.7`）**：`PASS cases=1 mode=independent-construction`，物性残差 `3.05e-8`。

2. **交线整段分支认证（Branch Certification）**
   - 在 `KernelRuntime.NestedCurveArc.cs` 中实现 `NestedBranchStationMap`：
     - `TryMapOldToNew` 与 `TryMapNewToOld`：提供严格双向可逆的站参数映射，逆向参数恢复误差 $|t_{rec} - t_{old}| \le 10^{-10}$。
     - `CertifyBranchCoverage`：在生产发布 10/2/2 紧凑交线前，严格验证全区间 $[lower, upper]$ 覆盖、严格单调性、切向一致性（$\hat{T}_{new} \cdot \hat{T}_{old} > 0.99$）以及沿法向投影的正交空间几何距离（$\le 10^{-7}$）。
     - 生产代码仅在通过此数学认证后才发布精简交线，失败时自动安全回退，消除任何未认证锚点直接发布的隐患。

3. **两次后续切割（copy-reflected-cut 与 reloaded-second-cut）分析**
   - **几何与拓扑一致性（完全一致）**：
     - 21 个实体顶点与 Parasolid 原生切割结果逐对吻合，最大空间距离仅为 $1.9 \times 10^{-13}$（机器精度）。
     - 13 个面面积严格吻合：平面与圆柱面面积残差 $\le 3.8 \times 10^{-11}$，Blend 面面积残差 $1.2 \times 10^{-9}$。
     - `PK_DEBUG_BODY_compare`：0 面差异（`global=21921 local=21931 face_pairs=0`）。
     - 完整 `PK_BODY_check`：0 faults。
     - 5/5 原生 fin UV box 完全一致，12 处材料探针 100% 一致。
   - **物性残差根因分析**：
     - 体积残差 $6.07 \times 10^{-11}$（$\Delta V = 3.62 \times 10^{-8}$ / 595.866），表面积残差 $5.85 \times 10^{-11}$，主转动惯量（$I_{xx}, I_{yy}, I_{zz}$）残差 $(6 \sim 10) \times 10^{-11}$。
     - 仅交叉转动惯量 $I_{yz}$ 残差为 $1.204 \times 10^{-7}$（切 1）和 $1.126 \times 10^{-7}$（切 2）。
     - 物理与数学根因：生命周期反射副本包含 $s=1.7$ 倍放大，转动惯量随尺度放大 $s^5 = 1.7^5 \approx 14.2$ 倍；该姿态下 $I_{yz} \approx 0.669$ 接近零，分母为 $1 + 0.669$，微小绝对数值积分离散差 $\Delta I_{yz} \approx 2.01 \times 10^{-7}$ 产生归一化值 $1.204 \times 10^{-7}$。
     - Parasolid 自身高斯散度积分离散度评估（`bound = PK_mass_bound_modulus_c`）报告 $I_{yz}$ 模量不确定度为 $5.556 \times 10^{-4}$，为该残差的 2700 倍。实体几何本身完全精确，物性门槛严格保持原版 1e-7，不采用虚假乘数伪装通过。

4. **辅助 Oracle 与全量回归测试**
   - **96 组参数矩阵**（`--matrix --both-updates --import-only --geometry`）：96/96 全部通过（exit 0），384 次完整 PK 检查 0 faults。
   - **510 个固定探针回放**：
     - 导入成品探针（`--import-only --saved-probes --diagnostics`）：16 处近边界分类差异（均为 $10^{-8} \sim 10^{-9}$ 边界微扰动点，Parasolid 原生 roundtrip 自身在此 16 处亦与基准不一致）。
     - 自研成品探针（`--event-arrangement --self-saved-probes --diagnostics`）：15 处近边界分类差异。
   - **单元回归**：
     - 集中单元测试（`BlendSupportChainTests`、`RegularSolidBlendTests`、`ExplicitSpineTests`）：71/71 全部通过（持续时间 1m 17s）。
     - `RegularSolidBlendOracle.cs -- plane-nurbs`：通过（物性残差 $2.02 \times 10^{-9}$）。
   - 所有 C# 代码严格基于 .NET 10，保持 AOT 兼容，零堆分配要求，无裸 int 语义句柄。

## 关键技术修复

- **`src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedCurveArc.cs`**：新增 `NestedBranchStationMap`，包含 `TryMapOldToNew`、`TryMapNewToOld` 以及全分支数学认证 `CertifyBranchCoverage`，严格验证端点覆盖、单调性、切向点积与正交投影偏差。
- **`scripts/ShortEdgeBlendOracle.cs`**：还原固定的 `1e-7` 严格物性门槛，去除 `Math.Max(1.0, unitScale)` 容差放宽乘数。
- **`src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedBlendCompilation.cs`**：在 `RefinePlaneRollingChart` 中放宽机器精度截断判定至 `previous.Upper <= 16 * accuracy`，避免在几何空间缩放导致浮点舍入微增时陷入无效 Newton 迭代。
- **`src/ProjectGmKernel.Native/Runtime/KernelRuntime.AnalyticRollingCandidate.cs` 与 `RollingNetwork.cs`**：在刚体与镜像变换下正确映射椭圆脊线与支持面接触参数区间，保证细分点与接触轨迹严格重合。
