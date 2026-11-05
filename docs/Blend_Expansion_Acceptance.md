# 嵌套 Blend 四项扩展验收

日期：2026-11-06。本轮扩展 `docs/Nested_Blend_Acceptance.md` 的常半径数值路径。四项目标按下面的支持范围分别验收；变半径仍按用户要求暂不实现。

## 四项目标与边界

| 目标 | 已实现并验证 | 剩余边界 |
| --- | --- | --- |
| D2 数学精度与 PK 数值兼容分开验收 | 独立 decimal 制造解；真实 PK D1 的 Richardson 导数；相似变换导数关系；原始 PK D2 差异单独输出 | 不承诺逐分量复现原始 PK D2 的数值偏差 |
| 常见构造与输入类型覆盖 | 开放 NURBS 脊线、四种半径符号组合、支持面交换、三层负 sense、双 Blend 支持面、缩放和平移 | 等绝对半径、规则 rolling-ball 分支；不覆盖变半径、cliff-edge、一般 swept/spun 支持面、奇点穿越与分叉 |
| 公共求交、反求和距离 | Blend/解析面、Blend/NURBS、Blend/Blend 的受限规则交线发现与准确 procedural ICurve；Blend 和此类 ICurve 的向量最短距离；接入几何对最短距离；参数反求 | 求交和距离在已接纳的有限参数域上工作；未解决人工 L 界限外的任意延续、全部域外交线与多个等距分支的选择 |
| 扩展三层性能语料 | 7 个模型 × 顺序/随机 × D0/D1/D2，共 42 个配对硬门槛；包含双 Blend 支持面、负 sense、尺度变化和闭合脊线接缝邻域 | 70% 目标适用于这些规则窗口的热求值吞吐，不是构造、公共 SSI 或全局距离的速度承诺 |

开放构造使用 `x=u, y=v, z=0.3u²+0.1uv`，原始 NURBS 域为 `[-1,1]²`，另一支持面为 `y=0`，两侧绝对半径均为 `0.1`。四种正负组合和支持面交换共八组，双方独立调用构造 API，通过脊线空间位置建立参数对应，逐截面比较，再分别生成 sheet。开放构造在源样条自然边界处终止。

导入的 PK 人工脊线可把接触参数带出源 NURBS 节点域。Newton 不把有效的域外触点夹回节点域；另有距边界 `5e-13` 的收敛回归。数值求值和区间包围均使用相同的张量积线性延伸，未使用端部多项式外推。非有理和有理样条跨双参数边界的 P、D1、D2 包围均有测试。

负 sense 三层样例保留真实依赖图。ICurve 的 sense 控制截面方向，不改变其自然 chart 切向。异号半径构造也保留对应的负 sense。共享的 Blend 脊线和样条接触变量在同一父 occurrence 中去重，避免重复驻点方程。

双 Blend 支持面模型从 torus/plane 首层 Blend 开始，复制并平移 `(0,0.018,0.024)` 作为另一支持面，第二层半径为 `0.01`，第三层与平面构造，半径为 `0.01/3`。两套内核分别构造各层，再比较空间截面和各自生成的 sheet；没有把任一层拟合成解析面。

## 导数验收契约

`tests/KernelTests/NestedBlendPrecisionTests.cs` 使用独立 decimal 闭式解验证球/平面脊线和其滚动截面，覆盖 P、Su、Sv、Suu、Suv、Svv，归一化门槛为 `2e-12`。参考值不调用生产 Taylor、隐式 Jacobian 或差分实现。

一般三层模型同参数 D0/D1 对比真实 PK，门槛为 `1e-8*(1+|PK分量|)`。D2 对真实 PK D1 使用两步长 Richardson 参考，门槛为 `1e-6*(1+|参考分量|)`。参考步长按参数范围取 `1e-5`；较大的 stencil 可跨越 procedural chart 的参数平面切换，不能直接当作单个 chart 内的局部二阶导数。

小尺度模型的 PK D1 差分会放大底层求值噪声。缩放用例因此用未缩放模型的真实 PK D1 加精确相似变换关系作 D2 参考：位置缩放为 `s`、U 缩放为 `k` 时，Suu、Suv、Svv 分别乘以 `s/k²`、`s/k`、`s`。本项目缩放后的 D0/D1 仍直接对比同参数的缩放 PK 模型。

原始 PK D2 仅作诊断。已观测到双 Blend 支持面模型约 `0.025` 的绝对差异；`s=0.001` 时原始 D2 的最大绝对差异可达约 `6.7e7`，而本项目解析 D2 对未缩放 PK D1 及缩放关系的归一化误差小于 `1e-6`。这些观测不能证明 PK 内部具体误差来源，也不表示本项目已经通过逐分量 PK D2 数值兼容验收。

## 公共求交与全局距离

`SurfaceIntersectionPatch` 复用现有 Bezier 规则交线算法，接入真实 Blend 定义。Blend 包围来自联立约束的区间 Jacobian、Krawczyk 收缩、最近点 Schur/Hessian 条件和解析截面区间运算。chart 的 chordal error 估计不作为证明包围。求交期间仅复用同一参数盒的已认证包围；求值基准没有重复点缓存。

包围尚未认证时优先细分相应脊线 U，认证后再进行四维图条件与边界根隔离。输出保留准确的 procedural 支持面和 chart，未用采样折线替代交线。不能认证的奇异、退化或超预算单元使整个操作失败。

人工 L 脊线界限不是几何终点。没有显式参数域约束时，在该界限处结束的交线，以及带人工界限的开放 Blend/Blend 分支，返回 `PK_ERROR_cant_do_intersect`，结果数量为零且输出指针为空；单元测试和原生脚本都检查这一点。已知有一组旋转 Blend/Blend 输入，PK 返回额外交线且其自身的参数反求返回 `PK_ERROR_not_on_surface`；本项目不把有限 chart 内的部分交线当作完整成功结果。

当前参数盒沿用项目已有的受限参数域处理方式；PK 的兴趣区域选项可能返回域外延伸曲线，因此这类结果尚无完整数量和裁剪语义一致性保证。Blend 求交的空间 box 选项，以及解析对手面的显式 UVbox，仍明确返回不支持。不能把本轮有限域验收扩展为任意完整曲面 SSI 的覆盖声明。

距离算法复用全局分支定界，舍弃单元需要外舍入包围或分离支持平面的下界。局部数值优化只改进候选，候选上界必须重新认证。分离的球面与 procedural 几何可归约为已认证的球心投影；相交或包容情况仍走一般几何对算法。

公共 oracle 使用开放 NURBS Blend 的非等参平面切割、同一平面的双线性 NURBS 表示，以及绕截面位置旋转 π 并偏移的另一 Blend。检查真实 PK 与本项目的交线数量、端点、17 个空间采样、切向及双方曲面反求。所有输出 wire 都经过本项目全新 text XT 导出、真实 PK receive、完整 BODY_check 和结构 compare。另对离面向量和附近球面检查最短距离。

距离参数 ABI 也通过同一份真实 PK 参数块验证。修复前，`PK_GEOM_range_o_t` 的本项目大小为 232 字节，`param_bound` 和 `opt_level` 分别位于 148、228；真实 PK 为 240 字节及 152、232。生成器现在保留复合数组的 C 对齐，布局测试与 oracle 均检查这一点，距离对照不再分别组装两套参数以绕过布局差异。

公共 SSI 与全局距离仍明显慢于 PK；本轮样例为秒级，未将它们计入 70% 的求值性能目标。

## 三层求值性能

`scripts/NestedBlendBenchmark.cs`：Release、关闭 tiered compilation，31 组交替配对批次，每批 512 个变化点，预热 2048 点；报告 p50/p95、校验和与托管分配。每项先验证 D0/D1 和 D2。通过条件为 `PK时间/本项目时间 >= 0.70` 且托管分配为零。

语料包含原生 torus 三层、原生 NURBS 三层、双 Blend 支持面三层、`0.001` 与 `100` 倍相似变换、三层负 sense，以及独立构造的闭合三层模型。缩放的平移向量为 `s*(0.3,-0.2,0.4)`；较大比例受 PK size box 限制，未把越界模型的 BODY_check 降级为通过。

Torus 及其缩放、负 sense、双 Blend 语料使用原始 U 范围的 35%–65%；NURBS 为 10%–30%；独立闭合模型覆盖完整 U 范围，包含接缝两侧邻域。V 为 0.1–0.9。NURBS 原生窗口外的 PK 自身失败区仍未计入通过范围。

42 项均通过，最小吞吐为 PK 的 **82.92%**，热路径托管分配全部为零。每个模型的六项最小比例为：

| 三层模型 | 最低吞吐/PK |
| --- | ---: |
| 原生 torus 起始 | 102.57% |
| 原生 NURBS 起始 | 140.35% |
| 双 Blend 支持面 | 217.58% |
| 0.001 倍缩放和平移 | 82.92% |
| 100 倍缩放和平移 | 208.10% |
| 三层负 sense | 101.96% |
| 独立闭合三层、完整 U 窗口 | 268.03% |

最大 D2 参考归一化误差为 `9.13e-8`，低于 `1e-6` 门槛。环境沿用前次报告的 Linux x64、Ryzen 7 7840HS、.NET 10 和真实 Parasolid 38.0.150；完整参数、p50/p95 和校验和见 `temp_docs/nested-blend-verification/NestedBlendBenchmark.log`。

## 最终回归

- 新增 18 个内核用例；完整内核测试 **1341/1341**，XT 测试 **12/12**，无跳过。
- `VerifyNestedBlend` 五个步骤全部通过；42 项三层配对性能硬门槛全部通过。
- `VerifyAdvancedModeling` 的 11 个既有建模 oracle、全部公共建模回归及 native advanced smoke 通过；`VerifyPublicModeling` 的 14 项检查全部通过，包含 NativeAOT 发布、C ABI 和显示回归。
- 开放八组、三层负 sense、独立双支持面三层、两种缩放和三个公共求交输出，均通过全新 text XT 的真实 PK receive、完整 BODY_check 零故障和结构 compare no_diffs。
- 生成器重新运行通过，阻塞项和警告项为零；ABI 布局检查通过。核心 IL 分配检查 CLEAN，`git diff --check` 通过。

## 回归与复现

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/VerifyNestedBlend.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/VerifyAdvancedModeling.cs
```

第一条顺序运行基础独立构造、开放/符号/sense 覆盖、双支持面/缩放 oracle、公共求交/距离 oracle 和扩展配对性能。日志位于 `temp_docs/nested-blend-verification/`。第二条包含 11 个既有建模 oracle、完整内核和 XT 测试、公共接口回归、NativeAOT、ABI、显示及 native advanced smoke；日志位于 `temp_docs/advanced-modeling-verification/` 和 `temp_docs/public-modeling-verification/`。
