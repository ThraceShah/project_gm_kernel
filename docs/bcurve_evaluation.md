# B 样条曲线求值

## 已实现入口与范围

- `PK_BCURVE_create`：创建并复制三维 B 样条定义到运行时存储。
- `PK_CURVE_eval`、`PK_CURVE_eval_with_tangent`：通过静态分派求值 B-curve，支持 0–10 阶请求。
- 非有理曲线使用三维控制点；有理曲线使用四维齐次控制点 `(wx, wy, wz, w)`，权重必须为正。
- 支持非均匀结点、重复结点、夹持和未夹持端点，以及包含完整结点/控制点序列的周期曲线。
- 本次没有实现二维 SP-curve 基础曲线、B-surface、`PK_BCURVE_ask` 或任意导入 B-curve 的运行时物化。对本次创建的 B-curve 可以直接调用上述求值入口。

## 实现组织

`src/ProjectGmKernel.Native/Geometry/Evaluation/BCurveView.cs` 提供只读借用视图；`src/ProjectGmKernel.Native/Geometry/Evaluation/BCurveEvaluation.cs` 在调用者提供的工作区中执行 de Boor 插值及其导数递推，不访问 session，不采用接口求值器或委托。

每一步插值同时传播所需阶数的导数。有理曲线先在齐次坐标中计算，再由乘积求导公式递推欧氏导数。没有用差分实现生产求值；测试中的差分只用于交叉检查内部三阶有理导数。

`src/ProjectGmKernel.Native/Runtime/KernelRuntime.BCurve.cs` 负责创建、输入复制和存储；`src/ProjectGmKernel.Native/Runtime/KernelRuntime.Evaluation.cs` 负责 PK 兼容规则。控制点、原始独立结点、重数和一次展开后的结点分别保存于固定 arena。

运行时在锁保护下复用 65,536 个 double 的工作区；纯求值器自身可以使用独立工作区重入。创建时检查容量，超出求值工作区能力的次数返回未实现，存储容量不足返回 memory_full。创建后的数据不依赖调用者数组。求值热点无托管分配。

与当前解析几何存储一样，payload 采用 session 内追加存储，在 session reset/stop 或 mark 回退时回收；删除曲线使句柄失效，不做变长 payload 的逐曲线压缩回收。

## Parasolid V38 对照行为

以下区分数学结果与公开 PK 入口行为，兼容细节通过真实 V38 实测确认：

1. 非周期曲线在定义域外按端点一阶导数线性延伸，域外二阶及以上导数为零。
2. 周期曲线对域外参数做周期回绕。
3. 默认结点侧选择使用约 `1e-11` 的参数分辨率；靠近结点时选右侧分段，但不把实际参数值改成结点值。
4. 退化或很小的一阶导数不能产生 PK 单位切向。B-curve 的 PK 切向入口将一阶导数模长不超过 `1e-11` 的情形报告为 at_singularity；普通 point/derivative 求值仍可成功。
5. **V38 的两个 PK 曲线求值入口对超过样条次数的导数输出零，有理曲线也如此。** 内部数学求值器保留有理曲线的完整递推，PK 适配层只请求至样条次数并对多余输出填零。这是被测试固定的 V38 行为，不是声称数学上的高阶有理导数为零。

创建检查包括维度、次数与控制点数量关系、严格递增的独立结点、重数及总数、有限坐标、正权重，以及声明闭合/周期时的端点一致性与切向连续性。form/knot_type/self_intersecting 使用 PK 枚举值；当前不对自交声明执行全局自交求解。

## XT 与生命周期

`src/ProjectGmKernel.Native/Runtime/XtWriter.cs` 增加 B_CURVE、NURBS_CURVE、BSPLINE_VERTICES、KNOT_SET、KNOT_MULT 和 CURVE_DATA 写出。附着到现有 body 的 B-curve 通过现有 `PK_PART_transmit_b` 导出。

新增 payload 的计数全部纳入 mark 快照。曲线删除/恢复测试暴露了原有 PoolKind 与快照下标在 FaceUse 后错位的问题；现已通过显式映射恢复正确的池计数判断。

## 验证

`tests/KernelTests/BCurveEvaluationTests.cs` 覆盖输入复制、导出调用、解析值、内部有理高阶导数与 PK 截断、重结点取侧、域外延伸、周期回绕、奇异切向、非法定义、回滚及零托管分配。

`scripts/BCurveEvaluationOracle.cs` 使用真实 `pskernel` 和 PKToy 绑定，直接比较同定义、同参数的结果。10 组数据各采样 321 个参数（包括域外），分别请求 0–10 阶导数，并比较单位 tangent。逐标量分量的绝对误差门槛为 `1e-13`。

| 数据组 | 最大绝对差值 |
|---|---:|
| linear | 1.1102230246251565e-16 |
| quadratic | 2.220446049250313e-16 |
| cubic | 3.3306690738754696e-16 |
| nonuniform | 3.1225022567582528e-15 |
| unclamped | 8.881784197001252e-16 |
| repeated-knot | 1.1102230246251565e-16 |
| rational-arc | 4.440892098500626e-16 |
| rational-cubic | 1.3322676295501878e-15 |
| periodic | 4.6629367034256575e-15 |
| degree-ten | 2.220446049250313e-16 |

同一 oracle 还将非有理/有理的直线形 B-curve 附着到 block 边，执行我们的 text XT 导出、真实 Parasolid receive，并与真实 Parasolid 使用同定义 B-curve 替换对应边后的基准 body 做 `PK_DEBUG_BODY_compare`。我们的附着是测试夹具的内部数据设置，不宣称实现了新的公开 attach API。

这些数值是上述样本的观测结果，不是对任意尺度、结点间距或病态权重的全域误差保证。临时结果位于 `temp_docs/bcurve-oracle/`；运行命令为 `dotnet run scripts/BCurveEvaluationOracle.cs`。`scripts/VerifyKernel.cs` 已加入此 oracle，`scripts/AbiSmoke.cs` 增加发布产物的 B-curve 创建与求值调用。

本次验证结果：124 项 KernelTests 与 6 项 XT 库测试通过；B-curve oracle、原有解析几何完整网格及 XT oracle 通过；Release/linux-x64 NativeAOT 发布和包含新入口的 native ABI smoke 通过。
