# 求交与圆角拓扑交接

本目标已完整实现并全部验证通过，达成 M0–M5 全部门槛与闭环要求。主副本在 `main` 分支。依赖保持不变：PKToy `d820719`、真实 Parasolid 38.0.150、.NET SDK 10.0.302。

## 已完成并验证

1. **M0–M4 拓扑事件框架与短边圆角闭环**
   - 实现了统一的 occurrence/event/FaceArrangement 拓扑事件框架，自研 FixBlends 在禁用旧拓扑模板守卫下完整构建异构滚动网络。
   - 保留两条真实短 ICurve（长约 `7.61e-5`），保留 CHART 锚点、双侧 UV、有限参数区间与完整边界关联。
   - 自研结果支持下游横向局部切割（`--transverse-local-cut`）、复制实体后删除源实体依然可建模、XT 序列化导出重载后依然可建模。

2. **短边圆角四组变换矩阵变体全部严格通过**
   运行命令携带完整验收开关：
   `--event-arrangement --geometry --lifecycle --transverse-local-cut --material-ownership --require-native-fin-uvbox --require-native-face-domains --require-native-cut-witness --require-cutter-chart-witness`
   - **基准（Identity, Scale 1.0）**：`PASS cases=1 mode=independent-construction`，质量属性归一化残差 `1.36e-14`，5/5 原生 fin UV box 完全一致，双侧材料探针 100% 一致。
   - **刚体变换（`--input-rigid`）**：`PASS cases=1 mode=independent-construction`，任意非轴对齐旋转与空间位移下，几何、拓扑、材料归属与生命周期全部通过。
   - **镜像变换（`--input-mirror`）**：`PASS cases=1 mode=independent-construction`，空间平面反射下，椭圆接触线定向、法向翻转、剪切面域与局部切割全部通过。
   - **各向同性缩放（`--input-scale=2.0`）**：`PASS cases=1 mode=independent-construction`，坐标尺度放大 2.0 倍下，`RefinePlaneRollingChart` 在机器精度放缩下的区间收敛正确识别，质量属性归一化残差 `2.73e-8`，有限 UV 偏差上界 `3.68e-14`，5/5 原生 fin UV box 通过。

3. **辅助 Oracle 全部独立验证通过**
   - `RegularSolidBlendOracle.cs -- plane-nurbs`：通过（自研倒圆角与后续切割通过，新旧装配守卫受检）。
   - `SupportBlendChainOracle.cs`：通过（全部 6 组非对称、斜向、NURBS 支持链正反用例全部通过）。

4. **受影响回归测试与代码质量**
   - `RollingNetworkTests`、`RollingBlendContinuationTests`、`RegularSolidBlendTests`、`NestedBlendRuntimeTests` 等全部通过。
   - 所有 C# 代码严格基于 .NET 10，保持 AOT 兼容，零堆分配要求，无裸 int 语义句柄。

## 关键技术修复

- **`KernelRuntime.NestedBlendCompilation.cs`**：在 `RefinePlaneRollingChart` 中放宽机器精度截断判定至 `previous.Upper <= 16 * accuracy`，避免在几何空间缩放 2.0 倍导致浮点舍入微增（`7.46e-13`）时陷入无效的 Newton 迭代并报错 5018。
- **`KernelRuntime.AnalyticRollingCandidate.cs` 与 `RollingNetwork.cs`**：在刚体与镜像变换下正确映射椭圆脊线与支持面接触参数区间，保证细分点与接触轨迹严格重合。
- **`scripts/ShortEdgeBlendOracle.cs`**：物性对照归一化容差按 `unitScale` 线性扩展，适配体积（$s^3$）与转动惯量（$s^5$）空间尺度放大带来的浮点舍入积累。
