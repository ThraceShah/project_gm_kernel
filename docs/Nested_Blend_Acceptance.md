# 三层常半径嵌套 Blend 验收

本页保留 2026-11-05 的阶段结果。后续四项扩展及当前支持边界见 `docs/Blend_Expansion_Acceptance.md`。

本次交付常半径 rolling-ball Blend 的通用数值执行路径。支持面保持真实几何依赖；三次连续构造均生成 `PK_CLASS_blendsf`（4007），没有把嵌套模型拟合或简化为解析面。变半径按本轮要求暂不实现。

## 支持范围

| 项目 | 当前范围 |
| --- | --- |
| 支持面 | 平面、球、柱、锥、ring torus、有理/非有理 B-surface、B-surface 平行偏置、已构造的常半径 Blend |
| 独立构造 | `PK_SURF_create_blend`，等绝对半径、规则闭合滚动分支，支持连续三层构造 |
| 求值 | `PK_SURF_eval` D0、D1、总阶 D2；另提供矩形 `uOrder/vOrder <= 2` 的解析混合导数 |
| 参数 | 保留 procedural ICurve 原始 chart 参数平面；人工 L 界限允许内部最近点沿约束延续 |
| 反求 | 脊线 `PK_CURVE_parameterise_vector`、Blend `PK_SURF_parameterise_vector`、离面拒绝 |
| 生命周期 | 依赖引用、深拷贝、删除、分配失败回滚、mark 撤销、独立程序存储 |
| 相似变换 | 平移、旋转、等比缩放、镜像；镜像交换 Blend 支持面并映射 `v -> 1-v` |
| B-rep / XT | Blend sheet、准确恒 U 圆弧、恒 V SPcurve、editable receive、全新 text XT transmit、真实 PK receive/check/compare |

“通用”指支持面可组合为真实数值依赖图，并不表示全部 Parasolid Blend 子类型或任意退化情形均已覆盖。当前边界：

- 构造只接受规则闭合分支；开放端、terminator、奇点穿越、分支分叉尚未验收。
- 变半径、cliff-edge、不同绝对半径、长弧及全部符号组合不在本次通过范围。
- 负 sense ICurve 的嵌套 XT 输入仍走不支持/opaque 路径；一般 swept/spun 支持面尚未纳入编译器。
- 任意 Blend/Blend 的完整 SSI 分支发现尚未接入公共求交入口。本次接通构造所需的偏置支持面求交、真实脊线及其求值。
- 固定工作区上限：128 个曲面指令、128 条脊线、4096 个 chart 点、65536 个样条标量；一次求值最多 16 个脊线 occurrence / 63 个未知数。构造最多 512 个 chart 点。超限明确失败。
- 最近点检查规则局部极小值：脊线驻点 Schur 条件和样条接触 Hessian；多个相等最近点处的唯一分支选择未作保证。

## 数值路径

`JointNestedBlendEvaluation` 联立求解各层脊线点、脊线参数和 NURBS 接触 UV。显式块 Jacobian 与同一 LU 分解用于 D0 Newton 和高阶 Taylor 系数，避免各层重复调用下一层 Newton。距离、法向、滚动截面均来自真实支持面定义；求值内不使用差分。

程序保存在 session native block 中，求值只使用栈和当前命令工作区，不分配托管对象，不依赖重复点缓存。构造通过接触参数延续和联立投影跟踪闭合脊线。递归 Taylor 路径保留为制造解测试对照。

## 独立模型与正确性

`scripts/NestedBlendOracle.cs` 在本项目和真实 Parasolid 中分别调用相同构造 API：

1. Torus 起始：major=2、minor=0.5，平面法向 `(0,0.6,0.8)`，首层半径 0.1；后两层与 X 法向平面构造，半径依次为 0.1/3、0.1/9。
2. NURBS 起始：二次面 `x=u, y=v, z=0.6u²+1.1v²+0.15uv`，`u,v ∈ [-1,1]`；平面 `z=0.4`，首层半径 0.05，后两层为 0.05/3、0.05/9。

各层通过脊线空间点建立参数对应，比较 12 个真实截面。独立生成的 chart 不要求参数恰好等于 PK。另通过正式 receive 路径编译 PK 定义，以完全相同参数比较求值。

两类独立构造的三层 sheet 都执行：本项目 text XT 导出 → 真实 PK receive → 完整 `PK_BODY_check` 零故障 → `PK_DEBUG_BODY_compare` 结构检查。等比缩放加镜像后重复。全局/局部结果为 `21921/21931`（no_diffs）。数量、类型、sense、missing、容差保持硬门槛。完整 face matching / 参数化偏差不作为通过条件；独立截面和导数另设硬门槛。

NURBS 原生三层定义的一段人工界限附近，PK 自身在 uvbox 内返回 `PK_ERROR_eval_failure (5018)`。性能使用双方稳定求值的规则窗口：原始 U 范围的 10%–30%，V=0.1–0.9。独立构造 sheet 使用其中 10%–15% 的单 chart 区间，避开自身闭合参数接缝。Torus 窗口为原始 U 范围的 35%–65%，V=0.1–0.9。窗口外没有计为验收成功。

### 导数契约

D0/D1 在同参数下对比真实 PK，门槛为分量误差 `1e-8*(1+|PK分量|)`。总阶 D2 通过两步长 Richardson 差分验证：对真实 PK D1 求导，对比本项目解析 D2，门槛 `1e-6*(1+|参考分量|)`。

PK 原始 D2 与其自身 D1 的数值导数存在差异；NURBS 样例观察到约 `8.4e-4` 最大绝对差异，矩形高阶混合导数还有更大差异。原始 D2 差异是诊断，不能解读为逐分量 PK D2 数值兼容已经通过。性能 D2 统一使用 `triangular=true`，仅包含总阶不超过二阶的六个向量。

## 性能验收

目标为同参数、同导数请求的求值吞吐至少是 PK 的 70%，即 `PK时间 / 本项目时间 >= 0.70`。该目标不扩展为构造、冷启动、公共 SSI 或所有模型的速度保证。

`scripts/NestedBlendBenchmark.cs` 使用 Release、关闭 tiered compilation，真实 `pskernel` P/Invoke 对照本项目托管公共 API。每项预热 2048 点，31 组交替配对计时，每组 512 个变化点；分别顺序和固定种子随机采样，记录 p50/p95、校验和、托管分配。两个模型、两种顺序、D0/D1/D2 共 12 项都设 70% 硬门槛。计时前逐点验证 D0/D1，并以 16 点验证 D2。

环境：Linux x64，AMD Ryzen 7 7840HS，.NET SDK 10.0.302 / runtime 10.0.10，Parasolid 38.0.150。性能结论限定于以上模型和规则窗口。

2026-11-05 最终配对结果（p50，微秒/次）：

| 三层模型 | 请求 | PK 顺序 | 本项目顺序 | 顺序吞吐/PK | 随机吞吐/PK |
| --- | --- | ---: | ---: | ---: | ---: |
| Torus 起始 | D0 | 11.667 | 7.516 | 155.22% | 217.91% |
| Torus 起始 | D1 | 13.883 | 13.303 | 104.36% | 141.35% |
| Torus 起始 | D2 | 36.145 | 18.532 | 195.04% | 223.81% |
| NURBS 起始 | D0 | 27.620 | 12.910 | 213.94% | 247.25% |
| NURBS 起始 | D1 | 30.490 | 21.838 | 139.62% | 161.97% |
| NURBS 起始 | D2 | 79.861 | 34.957 | 228.46% | 246.90% |

12 项全部超过 70%，最小值 104.36%；热路径托管分配全部为零。顺序 D1 的 p95：Torus 本项目 15.384 / PK 15.609 微秒，NURBS 本项目 22.983 / PK 32.992 微秒。完整 p50/p95、采样顺序和校验和见 `temp_docs/nested-blend-verification/NestedBlendBenchmark.log`。

同参数 D0/D1 最大绝对误差：Torus `2.74e-13`，NURBS `4.05e-11`。D2 对原生 D1 Richardson 参考的最大归一化误差分别为 `3.62e-11`、`1.03e-8`，通过 `1e-6` 门槛；原始 PK D2 差异仍按前述契约保留为诊断。

独立构造耗时另记，不计入 70% 目标，也未对构造速度作 PK 比例承诺：Torus 三层约 16.6 / 4.0 / 8.2 毫秒，NURBS 三层约 198.1 / 3.2 / 10.0 毫秒。计入运行环境下的首用/JIT 与构造开销，不是严格构造性能基准。

## 最终验证记录

- 新增 17 个内核用例；全部内核测试 **1323/1323**，XT 测试 **12/12**。
- `VerifyNestedBlend` 的独立构造/求值/反求/相似变换/XT oracle 与配对性能两项均通过。
- 两类独立构造、镜像和 NURBS 正式导入，均完成全新 text XT 的真实 PK receive、完整 BODY_check 零故障与结构 compare no_diffs；新生成 XT 也能再次 editable self receive 并保持空间求值。
- 高级建模的 11 个既有 oracle 通过；修复接入后顶点为空的 ICurve wire 回退范围，并验证普通圆 wire 继续 editable receive。
- `VerifyPublicModeling` 全部 14 项通过，包含上述内核/XT 测试、API/变换/导入/ICurve oracle、NativeAOT 发布、native ABI 和显示回归。
- `NativeAdvancedModelingSmoke` 独立通过，覆盖现有高级建模 C exports 的真实 PK receive/BODY_check。
- 函数目录 174 项；核心 IL 分配检查 CLEAN；`git diff --check` 通过。

原始日志位于 `temp_docs/nested-blend-verification/`、`temp_docs/public-modeling-verification/`、`temp_docs/advanced-modeling-verification/`；高级建模最后的两个子步骤分别由修复后的公共回归和 native smoke 单独复核，不能把早期失败日志当成最终结果。

## 复现

在项目根目录执行，真实 Parasolid SDK 与 schema 使用现有 script host 配置：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/VerifyNestedBlend.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/VerifyAdvancedModeling.cs
```

前者先生成独立模型并执行 native oracle，再运行配对基准；后者运行现有全量建模回归、全部测试、NativeAOT 与 ABI 检查。生成 XT 和原始日志均放在 `temp_docs/`，不依赖已保存的 native 计算结果。
