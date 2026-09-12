---
title: "ICurve / Blend 求值框架"
subtitle: "基于 2025 年 8 月 XT 文档的算法设计与 AI Agent 实施规格"
author: "project_gm_kernel · Engineering Specification"
date: "2026-09-12 · 设计版本 2.0"
lang: zh-CN
---

# 1. 使用范围、证据等级与实施目标

本文用于将 `project_gm_kernel` 的 `icurve`、rolling-ball blend 及其嵌套依赖，落实为可测试、可诊断、支持跨调用缓存的数值求值实现。它不是 Parasolid 内部实现的复刻说明，也不把设计建议冒充为 XT 文件格式的规定。

**代码基线**：`ThraceShah/project_gm_kernel`，提交 `5c645cdfc8a0cd8e3f6aac4c25de839653b4daf6`。本次通过 GitHub 连接读取 `main` 确认了这一提交；实现 agent 开始工作时仍应记录自己的实际 HEAD，并检查差异。[R0]

**文档基线**：用户提供的 `xt.pdf`，封面日期 August 2025，共 138 个 PDF 页面。这里称“2025 版”，不据此宣称它是截至实现日期 Siemens 发布的最后版本。旧文件为 May 2001、V12.0 说明，共 130 个 PDF 页面。2025 版正文页码等于 PDF 页序；旧版正文页码比 PDF 页序小 8。[XT25；XT01]

文档发布日期不等于文件的 schema 版本。2025 版 p.11 的写出说明使用 schema 35102，而仓库部分记录注释参考 sch_37102；导入仍应依据实际 XT 标识和字段映射，不能按封面年份覆盖兼容 profile。[XT25 p.11；R4]

全文采用四种证据等级：

| 标记 | 含义 | agent 的处理方式 |
|---|---|---|
| XT25 / XT01 | 所附文档的明确内容 | 保持原有语义；引用章节和页码 |
| R | 已读取的仓库代码或项目规则 | 以指定提交为基线，不假设所有声明都已接入 |
| DERIVED | 从明确数学定义推导的公式 | 独立做制造解、Jacobian 和导数测试 |
| DESIGN / GATE | 工程选择或待验证兼容问题 | 工程选择可按证据调整；GATE 不得静默猜测 |

除明确的来源陈述外，以下模块划分、缓存布局、阈值、求解器编排、任务顺序均为 **DESIGN**。数学推导不是对 Parasolid 私有算法的描述。

## 1.1 交付目标

实现必须保持 `icurve(t)` 的原生参数定义和特定分支；允许在内部改变未知量、消元方式和跟踪坐标，不允许改变所求的点。对于嵌套过程几何，应优先复用完整的辅助解状态，而非反复从零求内层问题。

正确性包括：位置、参数、分支、方向、有效参数域、所请求导数、缓存生命周期和失败发布语义。有限预算内无法满足这些条件时，应返回可定位状态；不得以“返回最近的点”替代求值成功。

本文覆盖常规 `R` 型恒半径 blended edge、解析面、已具备参数求值能力的 B-surface / offset / swept / spun，以及这些能力组成的有限依赖图。`E` 型 cliff-edge、退化零 range 构造、未知 foreign geometry、未描述的 blend vertex/overlap 不应被自动套入普通管面公式。

## 1.2 不包含的工作

本期不实现完整全局曲面求交拓扑发现、不修改布尔或欧拉操作、不自动修复震荡曲面、不把导入几何重拟合后冒充原几何，也不创建通用反射式算法引擎。内部的“隐式能力”不等于新增 XT `IMPLICIT_SURF` 节点；2025 版 §5.2.5 的 TPMS/lattice 数据不是一般过程曲面的隐式化存储格式。

**本次交付是实施规格与测试数据，不是已经集成的内核代码。没有执行真实 Parasolid oracle，也没有把本文中的接口草案当成已经编译通过的 C#。**

# 2. 对上一版方案的修订结论

## 2.1 已经解除的 chart 疑点

旧版正文 pp.43–44 的余弦点积式没有除以弦长，段内参数文字写成投影到前一 chart 点的切线。2025 版 pp.48–49 明确写出弦长归一化，并改为弦投影；还给出“两个支持面 + 垂直于弦的平面”的求值定义。这两项不应再列为当前 chart 主路径的待定解释。[XT01 pp.43–44；XT25 pp.48–49]

因此，删除上一方案中的“运行时在切线投影/弦投影候选间选择”。常规 chart 区间固定使用 §5 的弦平面规则。Oracle 的作用变成兼容性回归，而不是替这一段文字猜测含义。

## 2.2 terminator 不仅是 Newton 的困难端点

2025 版 p.49 对 branch chart point 与 terminator 之间的参数，明确规定使用**一个支持面和两个平面**，并给出支持面选择规则。它不是普通双面交线求解失败之后可随意启用的优化。[XT25 p.49]

因此，上一方案“所有成功点都必须严格满足两个原始支持面”的验收规则必须按语义区间修改。常规区间检查双面；terminator 区间检查文档指定的三项约束。未选支持面的偏差应保留为诊断，不能擅自添加成第四个等式，或重新投影后改变文档定义的点。

## 2.3 BLEND_BOUND 已提供表达式，但不宜宣布全部歧义消失

旧版 p.60 明确不描述 `BLEND_BOUND` 的具体形状。2025 版 p.63 给出无参数的距离函数组合：

$$
F_B(x)=d_0\big(x+r_1\nabla d_1(x)\big)-r_0.
$$

这使 `BLEND_BOUND` 从“只有边界用途说明”变为可以设计距离函数求值能力的对象。原方案应补充距离函数、梯度、Hessian 及组合求值模块。[XT01 p.60；XT25 p.63]

但是，该页把 $d_0$ 解释为“对应支持面”的距离函数，同时仍使用 `surface[1-boundary]` 指认该面。按通常有向距离解释，二者与接触边界用途存在需要核实的角色对应问题。§11 给出最小平面反例和验证步骤。不能悄悄交换下标，也不能在不同求值点选择“残差更小”的解释。

## 2.4 INTERSECTION_DATA 必须计入 terminator 的额外 UV

2025 版 pp.49–51 明确提供可选 UV 数据：每个 hvec 有 0、2 或 4 个值，总长度为：

$$
N_{\rm values}=(N_{\rm chart}+N_{\rm terminator})\,k,
\qquad k\in\{0,2,4\}.
$$

顺序为：可选 start terminator 的 `hvec[0]`、所有 chart hvec、可选 end terminator 的 `hvec[0]`。terminator 的 branch point 已在 chart 中，不额外重复。[XT25 pp.49–51]

这会直接改变导入索引、UV 初值布局和缓存锚点；不能仅分配 `ChartCount * stride`。

## 2.5 维持不变的架构结论

保留“原始 chart 不可变、准备好的依赖图、多种约束表示、完整辅助状态缓存、显式误差预算、多个求解器与独立验证器”。新版让常规求值规则更明确，也让 terminator 与 `BLEND_BOUND` 成为需要专门实现的语义路径，而不是缩成一套万能 Newton。

# 3. 仓库接入与必须先补齐的数据基础

## 3.1 当前代码事实

指定提交中的 `KernelRuntime.Evaluation.cs` 已有解析、B-curve、B-surface、SP-curve、offset、swept/spun 分支，`ICurve` 与 `BlendSurface` 尚未接入。依赖几何求值仍在 Runtime 的递归分支内访问池，offset 有专门深度限制。[R3]

`GeometryRecords.cs` 已声明 `ICurveData`、 `LimitRecord`、 `BlendedEdgeData`、 `BlendBoundData` 等；这不代表存储、生命周期、读取和求值已经完成。当前 `PoolKind` 没有 ICurve / blend 对应数据池；`SurfaceRecord` 也没有显式 `Sense` 字段，而 `CurveRecord` 有。[R4；R5]

项目架构要求静态分派、计算视图、caller-owned workspace，不使用求值器接口、闭包或全局服务定位器。现有 `AlgorithmStatus` 已包含 `Unsupported`、 `InvalidInput`、 `WorkspaceTooSmall`、 `NotConverged`、 `Singular` 等。[R2；R7]

## 3.2 存储实施项

新增持久数据之前，完整检查 `RecordHeader`、slot generation、pool kind、partition/session 初始化、实体删除、拥有关系、mark/rollback、变长 block 释放及 XT 映射。当前部分过程几何声明没有与已用数据池相同的 header 形状，不能直接传入已有池假定可用。

几何原始数据中至少保存：原始 chart 坐标、base parameter/scale、limits、可选误差字段的存在性、UV stride 与总长度、支持面引用、source sense、实际源 schema 及必要的导入诊断。`?` / null 不得统一替换为零；零可能是合法的坐标、参数或特殊构造值。

在 `SurfaceRecord` 或与其生命周期一致的元数据记录中，保存正确的 surface sense。创建、导入、复制、写出、删除和 rollback 路径应同时覆盖。不能把 face sense 当成 surface sense；解析面参数法向、几何 sense、face 使用方向是不同层次。[XT25 pp.55、88]

## 3.3 BLEND_BOUND 不能伪造公开 surface tag

2025 版节点表将 `BLEND_BOUND` 标为不在 PK 可见，且 p.63 明确其没有参数化。[XT25 pp.63、123]

当前 `ICurveData` 的两个 `SurfTag` 不足以安全表达所有 XT 支持面。建议内部使用带类型的 `SurfaceSupportRef`，区分公开 surface 实体与构造性 `BlendBound` 节点；准备视图统一接收它，公开 API 仍遵守原有类型契约。不向生成的 PK 常量表加入臆造的 `PK_CLASS_blend_bound`，也不提供假的 UV。

## 3.4 建议目录与最小变更边界

以下都是拟新增路径，不表示已经存在。小模块可合并，禁止为了目录完整性创建空实现。

```text
src/ProjectGmKernel.Native/Geometry/Evaluation/
  PreparedGeometryGraph.cs
  PreparedGeometryView.cs
  ICurveView.cs
  OriginalChartParameterMap.cs
  ICurveEvaluation.cs
  BlendEvaluation.cs
  BlendImplicitEvaluation.cs
  SurfaceDistanceEvaluation.cs
  BlendBoundEvaluation.cs

src/ProjectGmKernel.Native/Geometry/Intersection/
  ICurveConstraintPlan.cs
  ICurveSeedSelection.cs
  ICurveCorrection.cs
  ICurveContinuation.cs
  ICurveValidation.cs
  TerminatorEvaluation.cs

src/ProjectGmKernel.Native/Geometry/Caching/
  EvaluationSampleStore.cs
  ParameterCorrespondence.cs
  GeometryCacheVersion.cs

src/ProjectGmKernel.Native/Computation/Numerics/
  SmallLinearSolve.cs
  NewtonStep.cs
  TrustRegionStep.cs
  BlockSchurSolve.cs
  IntervalRootCheck.cs

src/ProjectGmKernel.Native/Runtime/
  KernelRuntime.PrepareEvaluation.cs
  GeometryEvaluationCache.cs
```

XT 字段映射应接入已有 `src/ProjectGmKernel.Xt/` 及运行时导入边界，而不是在数值求值器里解析 XT 字节。已有导出命令与调度模型继续使用，不创建第二套 API 派发。

# 4. 数据契约与准备好的求值依赖图

## 4.1 四类状态严格分开

| 状态 | 内容 | 生命周期 |
|---|---|---|
| SourceGeometry | 原始几何、chart、limits、sense | 模型/版本有效期 |
| PreparedGeometry | 只读视图、能力、依赖和布局 | 借用数据稳定期间 |
| SolveState | 当前及接受的辅助变量、分支、分解 | 单次求解 |
| VerifiedSample | 可复用的解及误差、参数对应关系 | 缓存版本有效期 |

`PreparedGeometry` 不是 `KernelRuntime` 的包装服务。迭代中只调用准备好的静态模块，不重复验证 tag 或查找实体拥有者。视图不应跨越未受控的模型修改或存储移动。

## 4.2 能力而非强制统一 UV 接口

建议用位标记及有限 `switch` 描述以下能力：

```text
ParametricPosition / ParametricD1 / ParametricD2
ImplicitValue / ImplicitGradient / ImplicitHessian
LocalOrientedDistance / DistanceD1 / DistanceD2 / DistanceD3
RecoverParameterWitness
ConservativeBounds / IntervalResidual / IntervalJacobian
ContinuityCells / DomainGuard / SheetGuard
```

`ImplicitValue` 不蕴含 `LocalOrientedDistance`。例如 $\|x-c\|^2-r^2$ 有正确零集合，但不是距离函数，不能代入 `BLEND_BOUND` 的距离组合式。

每个能力都同时声明有效域、导数阶数、所需子能力和失败状态。能力检查发生在 plan 准备阶段；失败不等到迭代深处才变成越界读取。

## 4.3 依赖图构造算法

从根几何及请求能力开始，做显式栈 DFS。节点颜色为未访问、访问中、已完成；只遍历当前表达式真正需要的正向依赖。遇到访问中节点时，先检查是否误把拥有关系、边界反向引用当成计算依赖；仅剩真实定义循环时才报告 `DependencyCycle`。

`GEOMETRIC_OWNER` 是反向关系，不进入求值 DFS。`BLEND_BOUND` 的距离表达式只需要 blend 的支持面和 range，不必为了读取这些字段先求 blend 或 spine。对同一 blend 的边界引用也不能机械地展开成循环。[XT25 pp.63、89]

准备阶段输出拓扑顺序、不同求值 occurrence、导数需求、workspace 布局、版本依赖和候选 constraint plans。模型节点可去重，求值 occurrence 不可仅按节点去重：同一个 spine 在两个不同参数位置的求值必须有不同状态。

## 4.4 资源限制

限制以 `MaxPreparedNodes`、 `MaxOccurrences`、 `MaxUnknowns`、 `MaxWorkspaceBytes`、 `MaxBaseEvaluations` 表达；深度仅是资源诊断的一部分。工作区不足返回 `WorkspaceTooSmall`，预算耗尽返回 `NotConverged` 加具体原因，不能统一成 `Unsupported`。

本期普通 2–4 维系统使用小固定栈缓冲；联合系统按准备阶段计算的大小使用显式工作区。不得按每层递归分配最大阶 jet 的大栈数组，导致嵌套时堆叠失控。

# 5. 原始 chart 的确定性重建与参数约束

## 5.1 重建公式

令原始 chart 有 $m$ 个点 $P_0,\ldots,P_{m-1}$。其单位自然切向为 $T_i$，由两个考虑 sense 的支持面法向叉积归一化获得。对非正则点，不进行普通 chart 切向构造。[XT25 pp.46–48]

定义：

$$
C_i=\|P_{i+1}-P_i\|,\qquad e_i=(P_{i+1}-P_i)/C_i,
\quad 0\le i<m-1.
$$

$$
f_0=\mathrm{base\_scale},\qquad
f_i=f_{i-1}\frac{T_i\cdot e_{i-1}}{T_i\cdot e_i},
\quad 1\le i<m-1.
$$

$$
t_0=\mathrm{base\_parameter},\qquad
 t_{i+1}=t_i+C_i f_i.
$$

这里的 $f_i$ 是“参数/长度”。最后一个 chart 点不需要一个常规段的 $f_{m-1}$；不要读取不存在的下一条弦。对 terminator 的扩展参数，另见 §6。

## 5.2 锚点准备算法

先解码并验证所有原始坐标和可选 UV。提供的 UV 是反求初值和位置对应证据：检查其曲面求值与原始 $P_i$ 相容，保留周期展开。无 UV 时，在该支持面的局部域内反求；解析面优先直接恢复，参数面用有界投影，过程面复用子图状态。

需要严格区分“修正辅助 UV”与“移动原始 chart 点”。前者允许；后者改变定义。若原始点与定义曲面严重不一致，标记输入/兼容问题，不静默吸附后重建 chart。

余弦分母小、方向符号不一致、零长弦、非有限数、比例溢出、累计参数不再严格递增时，都输出可定位诊断。不得通过 `Abs(dot)`、删除点或改变 base scale 隐藏问题。使用稳定范数与补偿求和；若 double 的全局参数分辨率不能区分相邻 $t_i$，报告 `ParameterResolutionLost`，不能仅在内部重标参数后仍声称保持原 API。

## 5.3 段内约束

给定 $t_i<t<t_{i+1}$：

$$
\lambda=\frac{t-t_i}{t_{i+1}-t_i},\qquad
 Q(t)=(1-\lambda)P_i+\lambda P_{i+1}.
$$

定义有长度单位的参数平面残差：

$$
p(x,t)=e_i\cdot(x-Q(t))
       =e_i\cdot(x-P_i)-\frac{t-t_i}{f_i}.
$$

其导数为：

$$
p_x=e_i^T,\quad p_t=-1/f_i,\quad
p_{xx}=p_{xt}=p_{tt}=0.
$$

这与 2025 版 p.49 的垂弦平面完全对应。原生参数的逆映射为：

$$
\psi_i(x)=t_i+f_i e_i\cdot(x-P_i).
$$

使用锚定差值 $t-t_i$，减少大 base parameter 下的消减误差。求解器应检查几何长度残差和参数误差，而不是将不同单位直接混入未经缩放的范数。

## 5.4 ChartPoint 是独立查询类型

如果请求参数等于已重建的 chart 参数，位置按文档返回原始 chart 点。不得把附近浮点参数按宽容差吸附到节点，以免产生阶梯状参数化。[XT25 p.49]

求导仍需正确几何 jet 和一侧规则。输入一致性检查在准备阶段完成；不能用“为了降低残差再投影一次”改变精确 chart 命中的位置。缓存中的校正点与原始 chart 锚点必须有不同来源标识。

## 5.5 一阶连续性与不能保证的二阶连续性

对弧长 $s$，$dt/ds=f_i e_i\cdot T$。在内部节点，递推保证左右 $dt/ds$ 相等，因此在正则、同一空间分支的条件下 $dx/dt$ 连续。这是对文档 $C^1$ 说明的直接验证。[DERIVED；XT25 pp.48–49]

若曲线两側足够光滑：

$$
x'(t)=\frac{T}{f_i e_i\cdot T}.
$$

这给出低开销 D1，也可用作一般隐式求导结果的独立对照。D2 通常依赖段内的 $e_i,f_i$，不能从 $C^1$ 推出节点两侧 D2 相同。数据结构必须保留 `DerivativeSide`，禁止在节点处平均左右 D2。

## 5.6 自适应求解分片不改原始 chart

运行时可在一个原始段内增加采样点、划分连续性 cell、构造 Hermite predictor，但所有子段仍使用同一原始 $P_i,e_i,f_i$ 和参数平面。原始 chart 的哈希和参数映射在这些操作前后必须不变。

闭合几何不自动等于可周期求值。是否允许 $t+k\,\mathrm{period}$、seam 端点约定及 scale 闭合，需要明确的导入/兼容证据；不能对所有 `H` limit 曲线无条件执行模运算。

# 6. Terminator 专用语义与算法

## 6.1 查询分类必须先于 solver 选择

将请求分类为 `ChartPoint`、 `RegularChartInterval`、 `StartTerminatorInterval`、 `EndTerminatorInterval`、 `ExactTerminator`、 `OutsideSupportedDomain`。limit 类型 `T` 的 `hvec[0]` 是 terminator，`hvec[1]` 是已在 chart 中出现的分支点。[XT25 pp.48–49]

不得把 terminator 区间作为普通 4×4 Newton 的失败后备，也不得把任意接近端点的点全部按 terminator 处理。

## 6.2 选择曲面与两个平面

按文档默认选择 `surface[0]`；只要以下任一条件成立，选择 `surface[1]`：`surface[0]` 在 terminator 处奇异而 `surface[1]` 不奇异；`surface[0]` 为 `BLEND_BOUND`；`term_use` 为 second。显式 first 并不覆盖前两个例外。[XT25 p.49]

令端点为 $E$、branch point 为 $B$。以递增参数方向定义单位弦 $e$；令 $w$ 为所选曲面在 $E$ 的法向，如果无法定义则使用 branch point 的曲线切向。取：

$$
a=\frac{e\times w}{\|e\times w\|}.
$$

第一平面：$a\cdot(x-E)=0$。它包含 $E,B$ 的弦及方向 $w$。第二平面：$e\cdot(x-Q(t))=0$，其中 $Q$ 是端点/branch 参数之间的弦插值。[DERIVED：对 XT25 p.49 的方程化]

若 $e\times w$ 退化，返回专项诊断；文档没有授权任取一个世界坐标轴替代 $w$。

## 6.3 一维优先实现

令 $v=\operatorname{unit}(a\times e)$。两个平面的交线可写为：

$$
x(\mu,t)=Q(t)+\mu v.
$$

所选曲面有隐式能力时，只需解：

$$
 h(\mu,t)=\phi_A(Q(t)+\mu v)=0,
\qquad h_\mu=\nabla\phi_A\cdot v.
$$

采用带括区间保护的 Newton；已知符号变号时可用 Brent/二分保护。没有变号不代表无根，重根必须转入隔离/奇异处理。根选择依赖本端 branch witness，而不是所有标量根中取最小 $|\mu|$。

若只有参数曲面能力，求两个平面残差对 $(u,v)$ 的 2×2 系统，Jacobian 两行分别是 $a^T[S_u,S_v]$ 与 $e^T[S_u,S_v]$。

## 6.4 特殊区间的验证契约

检查所选面、两个平面、端点分支和参数。未选面残差单独输出 `NonDefiningSupportDeviation`。若项目要求额外模型一致性阈值，必须作为已验证兼容策略声明，不能把它偷偷作为第四个精确等式。

一个制造例说明差别：两个面 $z=0$ 与 $z=y^2-x^3$ 在原点相切，正分支可取 $B=(h^2,h^3,0)$。选择第一面时，文档的两个平面使此小区间的位置沿 $E$–$B$ 弦变化；中点 $x=(h^2/2,h^3/2,0)$ 对未选面产生 $h^6/8$ 的非零残差。这是指定构造的数学结果，不是“双面 Newton 尚未收敛”。[DERIVED]

## 6.5 仍需独立确认的参数与导数细节：GATE-T

新版给出了区间点的构造，但 hvec 的 $t$ 不随 `$h` 传输；所引用段落没有把 terminator 参数值的重建过程完整写成独立公式。因此，`TryResolveTerminatorParameter` 必须有可复现依据。

先用真实 PK 的曲线范围及端点查询记录 $t_E$，作为测试夹具证据；比较向 chart 端点延拓比例、切向匹配等候选重建方式。这些候选是实验对象，不是文本事实。生产路径只接受已验证、可从导入数据重建的规则；不能依赖运行时仍安装 Parasolid。

精确 endpoint 的 D0 可由原始位置给出；有限一侧切向、D1 模长、D2 及最大支持导数阶数要分别验证。不能假定 regular chart 的 $C^1$ 递推自动保证与一面两平面的连接处为 $C^1$，更不能把端点导数全部填零。若只请求 D0 已可正确返回，应与高阶导数失败区分。

# 7. 常规区间的约束计划与降维算法

所有下述计划仅用于 `RegularChartInterval`。先分类语义，再按能力及成本选表示。计划的变更不能改变原始参数平面或分支身份。

## 7.1 P4：参数面 / 参数面，4×4 基线

未知量 $q=(u_0,v_0,u_1,v_1)$，令 $x_0=S_0(u_0,v_0)$、$x_1=S_1(u_1,v_1)$：

$$
F(q,t)=\begin{bmatrix}x_0-x_1\\p(x_0,t)\end{bmatrix}=0.
$$

$$
J=\begin{bmatrix}
 S_{0u}&S_{0v}&-S_{1u}&-S_{1v}\\
 e^T S_{0u}&e^T S_{0v}&0&0
\end{bmatrix}.
$$

曲面差是三行，参数平面是一行。输出位置采用约定的一侧 $x_0$，并记录 $\|x_0-x_1\|$；不要为了“减半误差”擅自用中点替代、破坏平面约束。需要对称方案时，应明确采用单独的等价计划并完整验证。

P4 是一般参数面的基准实现，也是降维路径的交叉测试对象。它不适用于没有参数化的 `BLEND_BOUND`。

## 7.2 P2：参数面 / 隐式面，2×2

$$
F(u,v,t)=\begin{bmatrix}\phi_1(S_0(u,v))\\p(S_0(u,v),t)\end{bmatrix}.
$$

$$
J=\begin{bmatrix}
\nabla\phi_1^T S_{0u}&\nabla\phi_1^T S_{0v}\\
e^T S_{0u}&e^T S_{0v}
\end{bmatrix}.
$$

不必为隐式支持面引入两个 UV。收敛后按需求恢复其参数 witness；若只请求位置/空间导数且没有下游 UV 需求，可以延后恢复，但 `SheetGuard` 不能延后到发布之后。

## 7.3 I3：隐式面 / 隐式面，3×3

$$
F(x,t)=\begin{bmatrix}\phi_0(x)\\\phi_1(x)\\p(x,t)\end{bmatrix},\qquad
J=\begin{bmatrix}\nabla\phi_0^T\\\nabla\phi_1^T\\e^T\end{bmatrix}.
$$

正则性同时要求两面交线正则以及参数平面横截交线。仅检查 $\nabla\phi_0\times\nabla\phi_1\ne0$ 不足够，还要检查该切向与 $e$ 的点积。

## 7.4 I2：消去参数平面的二维坐标

构造与 $e$ 垂直的正交基 $U=[u\ v]\in\mathbb R^{3\times2}$，用：

$$
x=Q(t)+U\xi,
\qquad \xi\in\mathbb R^2.
$$

基的构造使用与 $e$ 最不平行的坐标轴，经叉积或 Householder 得到；该选择只是平面内坐标，不改变语义。一个原始 chart 段内固定此基，不在每轮迭代旋转。

$$
\widehat F(\xi,t)=\begin{bmatrix}\phi_0(Q+U\xi)\\\phi_1(Q+U\xi)\end{bmatrix},
\qquad \widehat J=\begin{bmatrix}\nabla\phi_0^TU\\\nabla\phi_1^TU\end{bmatrix}.
$$

恢复参数平面无需第三个方程，仅保留浮点一致性检查。I2 的 Jacobian 比 I3 小，但有几何局部隐式内部求解时，总工作量未必小。第一次实现时保留 I3 对照开关。

## 7.5 I1：一个支持面为平面

若支持平面与参数平面不平行，先求两平面的交线 $x=a+\mu b$，再解另一个支持面的 $\phi(a+\mu b)=0$。平面 / quadric 可得到至多二次标量式；对二次式使用防消减的根公式，例如先计算 $q=-\frac12(b+\operatorname{sign}(b)\sqrt{b^2-4ac})$，再用 $q/a$ 与 $c/q$，处理 $a=0$、$q=0$ 和重根。

判别式的不确定符号要使用误差估计或更高精度确认。两个候选根都需进行原定义、分支和有效域检查。不能因为代数式有两个根就返回两个 `icurve(t)`；已存储的交线定义的是其中一个局部分支。

## 7.6 表示选择的成本模型

准备阶段按有限规则选择；默认排序不是永远“维度最小”，而是综合：基础几何求值次数、所需 jet 阶数、缓存覆盖、内层求解预算、Jacobian 条件数和消元稳定性。

建议初期采用明确规则：解析式无内层求解时优先 I1/I2/P2；参数面一般用 P4；已有良好 spine 参数缓存时可用 blend 局部隐式；内层误差占主导、消元块病态或反复失败时切联合约束。记录 plan ID 与切换原因，用基准结果调整规则。

# 8. 解析隐式能力、距离能力与变换

## 8.1 解析零集合与局部距离必须分型

记 $r=x-c$、$A$ 为单位轴、$z=A\cdot r$、$r_\perp=r-zA$、$\rho=\|r_\perp\|$。以下零集合公式为从解析定义推导的实现建议。[DERIVED；解析定义见 XT25 pp.55–61]

| 曲面 | 低成本隐式式 | 必须保留的限制 |
|---|---|---|
| 平面 | $n\cdot(x-c)$ | 法向方向/sense |
| 球面 | $r\cdot r-R^2$ | $R>0$；方向另处理 |
| 圆柱 | $r_\perp\cdot r_\perp-R^2$ | 轴与参数恢复 |
| 圆锥 | $\rho^2-(R-kz)^2$，XT 轴约定 | 只取有效半锥，$R-kz\ge0$ |
| 环面 | $(r\cdot r+a^2-b^2)^2-4a^2\rho^2$ | 恢复 apple/lemon/doughnut 与实际片 |

球面、圆柱的梯度/Hessian 直接写为解析式；环面多项式可能引入原曲面不包含的分支，不能仅依据零残差接受。

平面、球面、圆柱在相应正则邻域可用有向距离：$n\cdot r$、$\|r\|-R$、$\rho-R$，并乘以与所用 surface sense 对齐的符号。球心、圆柱轴等位置没有唯一法向，不提供正常的距离梯度。

圆锥可在脚点属于有效母线的区域使用 $(\rho-R+kz)/\sqrt{1+k^2}$；环面可在已选 sheet 的正则区域研究 $\sqrt{(\rho-a)^2+z^2}-b$。这两种公式都必须附带脚点/片选择与方向检查，不能宣称在整个三维空间都是目标曲面的光滑 signed distance。

## 8.2 不从任意隐式式估计“真正距离”

$|\phi|/\|\nabla\phi\|$ 是正则邻域内的一阶误差估计，不是严格距离界。`BLEND_BOUND` 的组合需要真正相容的距离函数能力；把任意 $\phi$ 归一化一次后代入，会产生不同几何。

`OrientedDistanceJet` 应包含距离、法向、可用 Hessian、脚点 witness、有效邻域和误差等级；`ImplicitJet` 则只声明零集合和导数。不得以同一个结构体里的未标记 double 混用两者。

## 8.3 XT / PK 坐标和 sense 归一化

2025 版 p.58 明确说明 cone 在 PK 接口的轴与 XT 存储轴反向。因此，准备阶段先明确数据属于 XT 原始约定还是内核已经归一化的 PK 约定；不可在 writer 与 implicit evaluator 各反转一次。[XT25 p.58]

自然法向是参数偏导叉积。使用隐式梯度作为法向时，以已知正则参数 witness 对齐自然法向，再应用 source surface sense。不要仅凭“半径为正”假定梯度方向就是该参数面的自然法向。

## 8.4 仿射变换的两个陷阱

一般隐式方程在 $x=Ay+b$ 下可写 $\widetilde\phi(x)=\phi(A^{-1}(x-b))$，梯度为 $A^{-T}\nabla\phi$，Hessian 为 $A^{-T}H A^{-1}$。但非均匀仿射变换不保持欧氏距离：不能把这个复合函数当作 transformed signed distance，也不能把变形后的圆管当成同半径 rolling-ball blend。

保持已定义参数时，应在原局部坐标求值后变换结果。原参数平面的法向作为协向量变换为 $A^{-T}e$，不是简单取 $\operatorname{unit}(Ae)$。用变换后的点重建一套世界坐标 chart 可能改变参数化。相似变换与反射还要单独验证方向约定。

# 9. 一般参数/过程曲面的局部距离与消元

## 9.1 法向坐标求解

对有定向法向 $n(u,v)$ 的正则曲面，求：

$$
H(u,v,h;x)=S(u,v)+h n(u,v)-x=0.
$$

未知量为 $(u,v,h)$。Jacobian：

$$
K=[S_u+h n_u\quad S_v+h n_v\quad n].
$$

只在固定的连续性片、局部脚点分支和 $K$ 非奇异的管状邻域中，令 $d(x)=h$。这个求解不是全局最近点算法；不同局部脚点可能有不同解。

若求解准确且法向为单位向量，则：

$$
\nabla d(x)=n,
\qquad
\nabla^2d(x)=[n_u\ n_v]\,[K^{-1}]_{1:2,:}.
$$

实现时解线性系统取得前两行敏感度，不显式求逆。检查 Hessian 对称误差与 $H_dn\approx0$，用作调试一致性指标。D2 需要下层相应导数；D3 继续沿定义求导，不以差分跨越 knot/接缝。[DERIVED]

## 9.2 局部有效性与初始化

优先用缓存脚点预测；其次用同一 chart 锚点的 UV，再用曲面 cell 的有界反求。需限制物理步长、周期展开和连续性 cell。若 $K$ 条件数变差、法向反转、存在多脚点或者脚点越过允许片，应停止消元，恢复参数或辅助变量。

局部距离图只能在有证据的邻域内复用。`DistanceValue` 很准确但 `DistanceHessian` 条件差时，应分别报告，不能因为 D0 成功就声称拥有 D2。

## 9.3 Offset 的正确低维表达

有向 distance 已成立时，offset 的零集合可用 $d_{\rm base}(x)-a=0$；必须继承正确 sense、选择同一脚点分支并远离 offset 奇异位置。否则使用原定义 $S_a(u,v)=S(u,v)+a n(u,v)$ 或保留法向脚点的联合约束。[XT25 pp.64–65；DERIVED]

不要把一般解析多项式 $\phi_{\rm base}(x)-a$ 当作 offset；不要无条件将 offset-of-offset 合并为偏移量相加，除非局部法向、片和正则性证明允许该等价变换。

## 9.4 Swept / Spun 的辅助变量消元

对 $x=C(u)+vD$，选择 $D^\perp$ 的二维正交基 $E$，消去 $v$：

$$
E^T(x-C(u))=0,
\qquad v=D\cdot(x-C(u)).
$$

对 spun，可以用轴向高度一致和径向长度平方一致的两个标量方程消去旋转角，再恢复角度及其 periodic lift。轴上点、脚点不唯一和错误 profile 分支要单独处理。

保留文档对 profile/section 类型的约束：2025 版只允许解析曲线或 B-curve，不应为了通用依赖图擅自允许以 icurve 为 swept/spun 的源曲线。[XT25 pp.69–72]

## 9.5 B-surface 的扩展域

有效域不能始终等于原 knot 的基本区间。文档的 `SURFACE_DATA` 描述显式/隐式扩展及原始和扩展范围；隐式扩展允许某些越界求值而不修改 NURBS 数据。[XT25 pp.68–69]

本期未支持的扩展语义必须明确拒绝，不能夹紧到边界。保守包围、正权重凸包和区间界也必须针对实际求值域构造，不能把基本区间内的凸包性质直接用于隐式扩展区。

# 10. Rolling-ball Blend：参数、包络和局部隐式三条路径

## 10.1 支持范围和基本定义

对已经验证的非退化 `R` 型恒半径 blended edge：

$$
B(u,v)=C(u)+r\{X(u)\cos\theta+Y(u)\sin\theta\},
\qquad\theta=v a(u).
$$

$u$ 继承 spine 参数；两个支持面对应的 $v=0/1$ 受 spine sense 影响。range 为带符号 offset，方向包括支持面 sense。`range=[0,0]` 的构造性 blend 不进入普通 $r>0$ 管面路径。[XT25 pp.61–63]

对普通等半径 rolling-ball 数据，应验证可用的半径一致性和接触距离；不要把不一致的 `abs(range[0])`、 `abs(range[1])` 求平均以掩盖输入/类型问题。更一般构造单独分派。

## 10.2 从 spine witness 恢复接触点和 frame

spine 求解应尽量返回 offset 支持面 UV。已知：

$$
c=S_j(u_j,v_j)+r_j n_j(u_j,v_j),
$$

则接触点就是 $Q_j=S_j(u_j,v_j)$。无须再次从球心投影到支持面。这个对应关系是跨域缓存的重要产物。

按 spine sense 确定截面起止接触点，设置 $X=(Q_{\rm start}-c)/r$。利用 spine 切向与有向圆弧约定构造 $Y$，以 `atan2` 恢复并展开 $a$。不能仅使用 `acos` 丢掉方向，也不能无证据地总取短弧。接触点接近重合、对径、spine 切向为零时，转入明确的退化处理。

## 10.3 参数 D1 / D2 的具体公式

令 $c_\theta=\cos\theta$、$s_\theta=\sin\theta$，并定义：

$$
E=Xc_\theta+Ys_\theta,\qquad V=-Xs_\theta+Yc_\theta,
$$

$$
E_1=X'c_\theta+Y's_\theta,\qquad
V_1=-X's_\theta+Y'c_\theta.
$$

则在 $r$ 为常数、当前分片具有相应导数时：

$$
B_u=C'+r(E_1+va'V),\qquad B_v=raV,
$$

$$
\begin{aligned}
B_{uu}&=C''+r\{X''c_\theta+Y''s_\theta+2va'V_1
               +va''V-(va')^2E\},\\
B_{uv}&=r\{aV_1+a'V-av a'E\},\\
B_{vv}&=-ra^2E.
\end{aligned}
$$

frame 的导数由接触 witness、归一化向量和角度的链式求导获得；禁止差分整个嵌套 `EvaluateBlend` 作为默认实现。参数 D2 所需下层阶数由准备图计算，不能硬编码“外层 D2，所以所有子节点 D2”。[DERIVED]

## 10.4 包络约束

对恒半径管面，令 $q=x-C(s)$，$s$ 是 spine 原生参数而非弧长：

$$
E_1=q\cdot q-r^2=0,\qquad E_2=q\cdot C'(s)=0.
$$

其一阶导数：

$$
(E_1)_x=2q^T,\quad(E_1)_s=-2q\cdot C',
$$

$$
(E_2)_x=C'^T,\quad(E_2)_s=q\cdot C''-\|C'\|^2.
$$

注意在未收敛迭代点不能因为目标根满足 $E_2=0$，就提前把 $(E_1)_s$ 永久置零。

blend 与一个隐式面的 icurve 可求 $(x,s)$ 的 4×4 系统：$E_1,E_2,\phi_A,p$。在参数平面内表达 $x$ 后，还可变为 $(\xi_1,\xi_2,s)$ 的 3×3 系统。无需完整 frame 的导数，但仍需要 spine 的相应 jet。[DERIVED]

## 10.5 局部标量隐式

固定 $x$，在缓存指定的 spine 分支附近解 $q\cdot C'(s)=0$。定义：

$$
D=\|C'\|^2-q\cdot C''.
$$

当 $D\ne0$、局部脚点可唯一延拓时：

$$
\frac{\partial s}{\partial x}=\frac{C'^T}{D},\qquad
\phi_B(x)=\|x-C(s(x))\|^2-r^2,
$$

$$
\nabla\phi_B=2q,\qquad
\nabla^2\phi_B=2\left(I-\frac{C'C'^T}{D}\right).
$$

若还需要局部距离，令 $\rho=\|q\|>0$、$n=q/\rho$，则：

$$
d_B=\rho-r,\quad \nabla d_B=n,\quad
\nabla^2d_B=\frac{I-C'C'^T/D-nn^T}{\rho}.
$$

应用该 blend 的方向因子，并确认它对应实际圆弧片。$\phi_B$ 的好处是梯度/Hessian 低成本；$d_B$ 才是局部 distance 能力。两者不混用。[DERIVED]

## 10.6 失效与切换

以 $D$ 相对 $\|C'\|^2+\|q\|\|C''\|$ 的比例和误差估计判定消元可靠性，不用固定的未缩放常数。$D$ 小时，切包络辅助变量或联合系统；这避免不稳定除法，但不能保证消除真实的焦点/包络奇异。

包络方程描述的是整条管面候选。成功前必须恢复 spine 参数、接触侧、圆弧 $v$ 和允许区间。`v∈[0,1]` 是文档所述边界圆弧的常见片选择，但公开曲面求值允许的延拓范围需按实际契约验证，不得把 owning face 的裁剪域自动当作几何求值域。

近 terminator 的 spine 若使用 §6 的一面两平面定义，必须据该定义求 $C,C',C''$；不能改回“两个支持面真正交线”的 tube 后仍称等价。

# 11. BLEND_BOUND：新增距离组合能力与角色验证门槛

## 11.1 文档表达式的数学实现

原样保留文档命名，令：

$$
y=x+r_1\nabla d_1(x),\quad
F_B(x)=d_0(y)-r_0,\quad
A=I+r_1 H_1(x).
$$

对常量 $r_0,r_1$，有：

$$
\nabla F_B=A^T\nabla d_0(y),
$$

$$
\nabla^2F_B=A^T H_0(y)A+
 r_1\sum_{k=1}^{3}[\nabla d_0(y)]_k\,
 \nabla^2(\partial_k d_1)(x).
$$

最后一项是 $d_1$ 的三阶导数张量收缩。要得到 `BlendBound` D1，通常需要 $d_1$ D2；要得到 D2，通常需要 $d_1$ D3。不能把该项省略后仍标为精确 Hessian。[DERIVED；定义见 XT25 p.63]

大张量不必全部存储；实现按方向的三阶收缩即可。缺少 D3 时仍可使用只要求 Jacobian 的位置求根，但高阶输出要受能力检查约束。

## 11.2 新文档的角色一致性检查：GATE-B

文档把 $d_0$ 对应到 `surface[1-boundary]`，把 $d_1$ 叫作另一个支持面。按通常有向距离，取两个正交平面：

$$
d_0(x,y,z)=x,\quad d_1(x,y,z)=y,\quad r_0=r_1=1.
$$

文档组合式变成 $F_B=x-1$。它与 $d_0=0$ 无交线，而与 $d_1=0$ 相交在 $(1,0,z)$；它的法向还与 $d_0=0$ 的法向平行，而非正交。对应 $d_0=0$ 的普通 rolling-ball 接触点为 $(0,1,z)$，代入文档组合式得到 $-1$。[DERIVED]

这不是 chart 的旧疑点，也不是本文对 Siemens 实现的实测结论。它说明：**公式中两个距离函数的角色、boundary 索引说明、signed range/sense 约定之间，仍需真实数据校准。** 数学上可推导的交换角色式不得冒充文档勘误。

## 11.3 最小 oracle 实验

用两个正交平面构造 radius 为 1 的常规 blend，分别读取两个 `BLEND_BOUND` 的 boundary 值、实际 icurve 支持面顺序、range、sense 和 chart 点。对 chart 点分别评价文档字面式和物理接触构造；记录哪一个组合与实际数据对应。

再覆盖一个非直角双平面、plane/cylinder、正负 range、支持面换序、负 surface sense、负 spine sense。不能用对称特例通过就宣布映射通用。输出唯一、版本明确的 `BlendBoundRoleMap`，关联测试夹具和实际 PK build。

GATE-B 未关闭前可以实现并测试距离组合的纯数学模块；不得把未经确认的角色映射接入生产。遇到相关根几何时返回精确的未支持原因，不影响不含该构造的普通 icurve 开发。

## 11.4 不带参数化的能力路径

不要发明 `BlendBound(u,v)`。其支持组合走 I3/I2 或 P2，并通过隐式梯度提供求交法向。需要进一步的距离函数时，`F_B` 本身也不能自动当作 signed distance；它只是文档给出的组合函数，需要新的局部法向坐标/投影步骤才能建立 distance 能力。

## 11.5 接触边界专用优化与联合形式

角色映射确认后，对于“对应支持面 + BLEND_BOUND”的边界 icurve，可从 spine witness 直接获得 $Q_j(s)$，解一维方程 $p(Q_j(s),t)=0$。这避免从 blend 相切处做一般双面横截求交。必须在完整区间内对照文档组合式与 PK 结果，尤其是参数和导数；不能仅用两端位置一致作为等价证明。

若距离函数本身嵌套求解昂贵，可保留辅助点 $y$：

$$
y-x-r_1\nabla d_1(x)=0,\qquad d_0(y)-r_0=0.
$$

外层再加另一个支持面和平面约束，得到一个显式联合系统。它将距离调用的依赖显露出来，便于共享脚点和误差预算，但不会凭空消除 D2/D3 的真实导数需求。

# 12. 联合约束、分块消元与嵌套求值

## 12.1 何时不再套用“Newton 调 Newton”

若一次外层残差/Jacobian 反复触发相同子 icurve 的独立非线性求解，或内层误差已经妨碍外层接受步，则提升内层变量为共同未知量。提升按实际求值 occurrence 执行；共享几何但位于不同参数位置的两次求值不能被错误合并。

候选计划需记录变量范围、残差范围、依赖稀疏结构、可消元块、所需导数和 branch guards。不要构造一般字符串表达式解释器；有限节点类型与静态 residual/Jacobian 模块即可。

## 12.2 六未知量的具体例子

在常规、正则 spine 片上，spine 点 $c$ 满足两个隐式支持：$A(c)=0,D(c)=0$。令 $w(c)=\nabla A(c)\times\nabla D(c)$，外层点 $x$ 属于 radius 为 $r$ 的 blend，并与隐式面 $S$ 相交。求 $(x,c)\in\mathbb R^6$：

$$
\begin{cases}
A(c)=0,\\
D(c)=0,\\
\|x-c\|^2-r^2=0,\\
(x-c)\cdot w(c)=0,\\
\phi_S(x)=0,\\
p(x,t)=0.
\end{cases}
$$

方向导数可直接计算：

$$
\delta w=(H_A\,\delta c)\times\nabla D+
          \nabla A\times(H_D\,\delta c),
$$

$$
\delta\{(x-c)\cdot w\}=(\delta x-\delta c)\cdot w+
                         (x-c)\cdot\delta w.
$$

对 $w$ 做安全尺度处理，但不能在未处理尺度导数时改变实际函数。使用未归一化 $w$ 可免去归一化导数；其数值尺度由 frozen row scaling 处理。[DERIVED]

这个系统消除了逐次 `C.Evaluate(s)` 的独立 Newton。得到 $c$ 后必须恢复其原始 chart 段与参数，验证它属于选定 spine 分支，再登记到 spine 缓存。不得用外层 $t$ 给内层样本命名。

仅当 spine 的该片确实由两支持面交线定义时才使用上述 lowering。ChartPoint 和 terminator 区间必须编译它们自己的真实求值法则，不能套用此六方程系统。

## 12.3 Schur 消元公式

对于外层 $F(x,z)=0$、内层 $H(x,z)=0$，Newton 方程为：

$$
\begin{bmatrix}F_x&F_z\\H_x&H_z\end{bmatrix}
\begin{bmatrix}\Delta x\\\Delta z\end{bmatrix}
=-\begin{bmatrix}F\\H\end{bmatrix}.
$$

$H_z$ 稳定可解时，先解 $H_z W=H_x$、$H_z b=H$，再求：

$$
(F_x-F_zW)\Delta x=-F+F_zb,
\qquad \Delta z=-b-W\Delta x.
$$

右端中的 $F_zb$ 在内层未完全收敛时不能省略。显式矩阵求逆不进入实现。若 $H_z$ 病态但完整系统仍可解，保留该块，用全系统 QR/SVD 或重新选消元顺序。

## 12.4 计划切换和状态迁移

从消元计划切联合计划时，使用已有的所有脚点、spine 参数、UV 和辅助坐标填充变量。反向切换必须验证被消元块具有可用局部隐函数。切换后重新计算真实 residual/Jacobian，重置不相容的线性模型和 trust radius；不可沿用旧维数的分解。

计划切换只发生在接受态或明确的失败恢复边界。一个 trial 步中不能根据残差临时改变方程定义，否则实际/预测下降比没有意义。

## 12.5 有限嵌套不等于真正循环

`C2 → B1 → C1 → B0 → C0` 可作为有限依赖图准备。若同一几何定义确实自我依赖，应报告循环或缺少构造语义；不能把所有循环一律通过“联立”接受为新几何。只有有独立明确定义、自由度和分支信息足够的约束提升才合法。

# 13. 跨域、跨阶段、跨调用缓存

## 13.1 缓存对象是解的对应关系，不只是空间点

一个可复用样本至少包含根几何参数和相关子几何的状态：

```text
outer icurve t
  <-> support0/1 parameter witness
  <-> blend u/v and arc selection
  <-> spine icurve parameter and original segment
  <-> offset support UV and base contact point
  <-> lower-level procedural witnesses
```

反向使用这些对应关系时仍需验证语义。比如 terminator 一面两平面解，对未选支持面的 UV 不能登记成“该点精确在面上”的证据；外层联合系统中恢复的子点，只有满足子几何自己的查询法则，才能进入子几何的精确缓存。

## 13.2 建议样本布局

```text
SampleHeader
  GeometryIdentity, ModelEpoch, DefinitionRevision
  ParameterizationRevision, FrameRevision
  QueryKind, OriginalSegmentId, BranchId
  ParameterBits, PeriodicLift, DerivativeSide
  AvailableJetOrder, QualityClass, SourceKind
  WitnessOffset, WitnessCount
  ErrorEstimate, ResidualSummary, ConditionEstimate
  NeighborhoodDescription, LastUsedStamp

WitnessRecord
  GeometryIdentity, EvaluationOccurrence
  WitnessRole: Defining / Diagnostic / SeedOnly
  ParameterValues, ParameterChartId, PeriodicLifts
  Position, AvailableJetOrder
  NestedStateOffset, NestedStateCount
  ResidualAndError, ValidityRegion
```

变长 witness 放在 flat arena / 分页块，不在每个采样点创建一个对象树。只保存未来有价值的对应关系；不要把整棵 prepared graph、所有高阶 jet、完整迭代历史复制到每个样本。

## 13.3 四层缓存

| 层 | 保存内容 | 使用规则 |
|---|---|---|
| L0：一次 residual 的 memo | 同节点、同参数、同分支的 jet | 随 trial generation 变化；不跨不同坐标误用 |
| L1：一次求解状态 | 接受态辅助变量、当前分解、trust radius | trial 与 accepted 双缓冲；拒绝步可回退 |
| L2：操作级 atlas | 成功样本、局部预测模型、参数对应 | 批量/连续调用复用，可显式传入 |
| L3：几何版本缓存 | 经过验证的样本、准备计划与锚点 | 跨命令保存，独立内存，受模型版本控制 |

L0 的 key 不只是 NodeId，应含求值 occurrence/参数值、sheet、periodic lift、导数需求和内层精度等级。同一位置请求更高阶导数时可增量计算，但不能把低精度内层解冒充高精度 memo 命中。

## 13.4 精确命中和近邻命中

精确 key 相同且样本误差、导数阶数、侧、版本和语义满足请求时才允许直接返回。一个误差较大的样本对更严格查询仍可作 seed，但不是精确结果。

近邻命中默认仅提供预测。量化参数的 hash/grid 可加速寻找候选，不得把同一桶中所有参数映射到同一个精确空间点。无误差界的插值不能跳过最后的校正。

原始 chart 节点有专门的 D0 返回规则，不能被后写入的校正样本覆盖。样本应区分 `ImportedChartAnchor`、 `CorrectedRoot`、 `PredictedOnly`。

## 13.5 完整状态预测

若接受状态 $y$ 包含所有外/内层未知量：

$$
y_{\rm pred}=y_k+y'_k\Delta t+\tfrac12y''_k\Delta t^2.
$$

没有可靠 D2 时使用线性预测；没有可靠导数时使用同片内已验证端点的插值。周期参数先展开再插值，之后保留 lift，不在中间将 $2\pi-\epsilon$ 和 $\epsilon$ 直接平均到 $\pi$。

对两个同片样本 $t_a,t_b$，可用 Hermite 预测：

$$
y(t)=h_{00}y_a+h_{10}\Delta t\,y'_a+
     h_{01}y_b+h_{11}\Delta t\,y'_b,
$$

其中 $s=(t-t_a)/(t_b-t_a)$，$h_{00}=2s^3-3s^2+1$、$h_{10}=s^3-2s^2+s$、$h_{01}=-2s^3+3s^2$、$h_{11}=s^3-s^2$。它仍是 seed。不得跨原始语义切换、未处理接缝、branch 或低连续性边界直接使用。

## 13.6 初值排序算法

先过滤语义、版本、分支和连续性片；再按确定性顺序取：满足精度的精确样本、同片邻近样本预测、两侧样本插值、原始 chart 锚点及导入 UV、局部有界反求/细分候选。

候选评分可以综合缩放预测残差、空间位移、子层已有 witness 比例和预计求值成本，但评分只排序，不替代 branch guard。对称分支或距离相近时不得用随机 tie-break；使用稳定 source index，并在无法区分的情况下报告歧义。

推荐维护按原生参数排序的分页样本表和每原始段的短候选索引。几何最近点树只做候选发现；空间接近不意味着相同 `t`、相同分支或相同 periodic lift。

## 13.7 跨父节点复用与缓存发布

一个 spine 被多个 blend 使用时，其几何版本缓存可以共享；blend 的截面/arc/接触 side 状态属于各自父定义，不可混淆。依赖引用中的 range 和 sense 是父约束的一部分，不能把使用不同 offset 的两个子调用合成同一状态。

只在验证通过后发布样本。trial 步使用单独状态，不覆盖 accepted state；父求解失败时，其 trial witness 不进入“已验证”缓存。独立验证通过的子结果可以在明确的事务边界发布，但第一版宜统一在顶层成功后发布，减少状态复杂度。

失败缓存只记录“某 seed/plan 在某预算下失败”的性能信息，不能永久标记这个参数无根，不能让一次失败成为未来更好初值的拦截条件。

## 13.8 版本、rollback 与 ABA

初期使用保守而安全的单调 `ModelGeometryEpoch`：几何创建/修改/删除、影响定义的变换、rollback、session reset 均按契约使缓存失效。缓存自身写入不增加模型 epoch。slot generation 与模型 epoch 同时检查，防止旧 slot 复用后命中另一个实体。

rollback 时不要把缓存 epoch 回退为历史数值；单调增加并使借用结果失效。后续性能需要时改用定义 revision + 依赖传播：子支持面、spine、chart、range、sense 改动均使父缓存失效。哈希可加速比较，但不得仅依赖可能冲突的哈希来保证身份。

## 13.9 内存与并发

L3 不使用 `CommandScratch` 或 API return arena。前者每命令释放，后者受对外返回值生命周期约束。现有代码的 `AvailableSpanOfDoubles()` 只能用于不与父层存活数据重叠的叶计算；父子求解同时持有数据时必须真正划分工作区。[R6]

采用 session-owned 有上限的 native cache arena，按块分配、按 geometry/segment 进行 CLOCK/LRU 淘汰。锚点准备数据与动态样本采用不同预算。禁止跨层持有缓存锁后再调用子求值器。

当前调度仍为串行时，先在已有串行边界下保证版本和发布一致；不要宣称已经实现并行。未来并行采用几何 read lease、线程/worker 私有 L1/L2、不可变已发布样本、短锁或原子索引发布；query 结束前再验证 lease/epoch。禁止将 TLS scratch 等同于可跨 worker 迁移的操作状态。

# 14. 数值求解器的具体实现和切换规则

本章的阈值与预算是起始工程配置，不是 XT 规定，也不是全局收敛保证。普通 root problem 的目标为所有定义残差同时为零；最小二乘只是求步工具，不是允许丢弃几何约束。

## 14.1 缩放

在当前接受态 $y_k$，令未知量增量 $\Delta y=D_u p$，缩放残差 $r=W_FF$，Jacobian $J_s=W_FJD_u$。$D_u$ 的 UV 块可依据第一基本形式 $[S_u,S_v]^T[S_u,S_v]$ 确定物理步长尺度；奇异参数块不要强行白化。

距离/位置残差按局部长度处理；平方距离残差可按正的局部长度尺度转成相近量级。row scaling 与 unknown scaling 在一次 trial 的预测/实际下降比较中固定，只在接受步后重建。若缩放因子依赖 $y$ 而被当作真正函数的一部分，就必须包含其导数；推荐不要这样实现。

机器精度使用 binary64 的 $\epsilon=2^{-52}$（或明确定义的 unit roundoff $2^{-53}$）。**C# `double.Epsilon` 是最小正次正规数，不能当作这里的机器精度。** [N4]

## 14.2 小型线性代数

2–4 维良态方阵可先采用带主元 LU；检查线性回代残差和数值枢轴。条件可疑时切列主元 Householder QR；秩判定和最小范数恢复使用小型 SVD。对较大联合问题先用块结构与 QR，性能证据充分后才增加稀疏/迭代解法。

不得默认构造 $J^TJ$ 处理病态问题；正规方程会放大条件数影响。LM 优先通过增广 least-squares 的 QR 求解，而非在所有情况下对正规方程做 Cholesky。[N2：相关线性求解选择；DESIGN]

用分解做多右端求解，禁止显式逆矩阵。分解可以在同一收敛状态的 D1/D2 中复用；跨参数的旧分解仅能作为近似校正模型或预条件，不能当作新点的精确 Jacobian。

## 14.3 带线搜索的 Newton

求 $J_sp=-r$。若方向不是下降方向或线性系统不可靠，直接转信赖域。否则从允许的最大 $\alpha\le1$ 开始，检查：

$$
\Psi(y_k+\alpha D_up)\le
\Psi(y_k)+c_1\alpha\,g^Tp,
\qquad \Psi=\tfrac12\|r\|^2,\quad g=J_s^Tr.
$$

先按真实域、连续性 cell 与允许的空间移动限制步长，不能对越界的 trial 逐坐标夹紧后仍沿用原模型。到达可穿越接缝时先完成 cell transition，再重建局部模型；到达真实边界则由分支/端点语义处理。

接受步后更新完整 nested state；拒绝步恢复全部状态。对内层误差影响较大的函数，要求 trial 和 base 都达到足以判定下降的精度。

## 14.4 Dogleg

令 $g=J_s^Tr$，Cauchy 步为：

$$
p_C=-\frac{g^Tg}{\|J_sg\|^2}g.
$$

Newton/Gauss-Newton 步 $p_N$ 用 QR 求得。$\|p_N\|\le\Delta$ 时选 $p_N$；$\|p_C\|\ge\Delta$ 时沿 $-g$ 到边界；其余情况求 $\beta\in[0,1]$ 使 $\|p_C+\beta(p_N-p_C)\|=\Delta$。分母接近零或秩不足时不要产生 NaN，进入相应恢复分支。

实际/预测下降比：

$$
\rho=\frac{\Psi(y_k)-\Psi(y_{\rm trial})}
           {\frac12\|r\|^2-\frac12\|r+J_sp\|^2}.
$$

分母非正则重建模型或缩步。可用起始策略：$\rho<0.25$ 缩半径，$\rho>0.75$ 且步触边时放大，$\rho>0.1$ 才考虑接受。仍必须过几何与分支 guard。Ceres 官方资料说明了 Dogleg/LM 的信赖域结构；这里的具体调度由项目控制。[N2]

## 14.5 Levenberg–Marquardt

解：

$$
\min_p\left\|
\begin{bmatrix}J_s\\\sqrt\lambda I\end{bmatrix}p-
\begin{bmatrix}-r\\0\end{bmatrix}\right\|_2.
$$

用与 trust-region 相容的下降比调整 $\lambda$。LM 可在 Jacobian 近秩亏时给出稳定步，但不能把 $J_s^Tr\approx0$ 或步很小当作交点存在的证据。残差未达验收要求时返回 stagnation，而不是 success。[N1；N2]

## 14.6 震荡、停滞和错误分支识别

维护短窗口内的 residual norm、step cosine、下降比、最小奇异值估计、内层误差、plan ID 和 guard 失败类型。建议触发规则如下：

| 现象 | 首选措施 | 不能采用的掩盖方式 |
|---|---|---|
| 连续两次拒绝 full Newton 步 | 转 trust region、缩步 | 无限制增加 Newton 次数 |
| 相邻步方向近反向且 residual 无改善 | 收缩局部域/换同分支 seed | 随机跳到远处 |
| 残差降到内层误差量级后抖动 | 收紧内层、重算 Jacobian | 降低外层正确性要求 |
| UV 很大但空间位移很小 | 换局部坐标或隐式表示 | 对 UV 无条件限幅 |
| 消元块病态 | 恢复辅助未知量 | 对小分母加任意 epsilon |
| 小残差但 branch guard 失败 | 拒绝、重新延拓 | 把数值收敛当分支正确 |
| 小步但大残差 | 标记停滞，切方案/细分 | success |

Broyden/旧 Jacobian 是可选后期优化，不进入第一版正确性路径。任何异常后清除近似线性模型，最终导数前重建真实 Jacobian。

## 14.7 一次请求共用预算

建议初始配置：单 seed 快速 Newton 最多 8 次接受迭代；信赖域最多 20 次接受迭代并单独限制 trial；最多 4 个同分支 seed；continuation 最多 64 个接受步；所有嵌套基础几何求值共同计入一个预算，例如 4096 次。区间细分另设节点与时间预算。

这些值必须通过基准校准。每一层都独立拥有“4096 次”会导致指数式预算膨胀，禁止这样实现。取消请求在大循环和子计划入口检查，不在每个标量加法检查。

# 15. 嵌套精度、敏感度和误差预算

## 15.1 内层误差对外层的影响

内层 $H(x,z)=0$ 仅解到残差 $r_H$ 时，局部估计：

$$
\delta z\approx-H_z^{-1}r_H,\qquad
\delta F\approx-F_zH_z^{-1}r_H.
$$

因此，应约束：

$$
\|W_F F_zH_z^{-1}r_H\|
\le \eta_k\|W_FF\|.
$$

远离根时可粗解，接近根时减小 $\eta_k$；最终验证时再收紧。它是对 inexact Newton 思路的嵌套几何应用，不是宣称 KINSOL 使用这里完全相同的过程面调度。[DERIVED；N1]

## 15.2 可实施的调度

先估计当前子块误差传播系数，预算分配采用残差行贡献而非按嵌套层数平均。无敏感度估计时使用保守内层容差，并禁用需要更高精度的降维路径。

可使用 $\eta_k=\operatorname{clip}(c(\|F_k\|/\|F_{k-1}\|)^\gamma,\eta_{\min},\eta_{\max})$ 作为初始规则，但对模型变化、near-singularity 和 first iteration 单独设置。导数求解的容差往往比位置求值更严格，必须作为独立请求传递。

缓存应按已有误差满足新预算时复用；否则从缓存值继续 refinement，不重新初始化。不能让“命中了缓存”绕过当前精度检查。

## 15.3 下降判定中的函数噪声

若基础几何/内层求值带来的 $\Psi$ 误差区间覆盖了当前预测下降量，则当前 $\rho$ 不可靠。先提高 base/trial 的评价精度，再作接受判断；不要用噪声随机驱动 trust radius 收缩和放大。

外层残差、内层残差、导数误差和 source 数据容差分别记录。输入几何本身的不一致不能通过更小 Newton tolerance 消除。

## 15.4 最终误差等级

给出位置误差估计时，应包含外层校正、内层传播、chart/输入一致性和舍入影响：

$$
\epsilon_{\rm out}\lesssim\epsilon_{\rm corrector}
 +\sum_jK_j\epsilon_j+\epsilon_{\rm arithmetic}.
$$

这是局部数值估计，除非 $K_j$ 和所有误差均为保守界，否则不能标为认证误差。要求严格误差界的请求走 §18 的认证流程。

# 16. 导数算法与能力传播

## 16.1 对定义方程求导

对最终、已选择语义的方程 $F(y(t),t)=0$：

$$
J y'=-F_t,
$$

$$
J y''=-\{F_{tt}+2F_{yt}y'+F_{yy}[y',y']\}.
$$

重用根处同一 Jacobian 分解求多个右端。不得对 Newton 的控制流、线搜索次数、cache hit 分支做自动微分；它们不是几何定义。[DERIVED]

## 16.2 双隐式常规区间的专用 D1/D2

对 I3：

$$
Jx'=\begin{bmatrix}0\\0\\1/f_i\end{bmatrix},
\qquad
Jx''=-\begin{bmatrix}x'^TH_0x'\\x'^TH_1x'\\0\end{bmatrix}.
$$

同时使用 §5.5 的切向公式对照 D1。对于 I2，$Q'(t)=e_i/f_i$ 进入 $F_t$，不能因为只求二维变量就漏掉基点随参数变化的贡献。P4/P2 的二阶右端由曲面二阶链式项组成，优先实现方向二阶收缩，避免为少量导数构造完整大张量。

## 16.3 Terminator 的标量导数

在 §6 的固定平面构造中，$x=Q(t)+\mu(t)v$，$Q''=0$。若 $\nabla\phi_A\cdot v\ne0$：

$$
\mu'=-\frac{\nabla\phi_A\cdot Q'}{\nabla\phi_A\cdot v},\qquad
x'=Q'+\mu'v,
$$

$$
\mu''=-\frac{x'^TH_Ax'}{\nabla\phi_A\cdot v},\qquad
x''=\mu''v.
$$

这求的是文档的单面构造，不是原双面交线导数。端点或连接处不满足正则条件时，按一侧与阶数能力返回，不做任意正则化。[DERIVED]

## 16.4 Jet 需求反向传播

根请求 D0、D1、D2 分别生成子图需求。例如：包络 Jacobian 通常需要 spine D2；`BlendBound` 梯度需要一个距离 Hessian；`BlendBound` Hessian 可能需要距离 D3；参数 blend D2 的 frame 链式求导可能进一步提高子阶数。

在准备阶段计算闭包，检查连续性和最大可用阶数，输出 workspace 布局。如果子能力不够，选择导数需求更低的等价 plan，或者拒绝不支持阶数。不能调用较低阶后把高阶补零。

## 16.5 高阶扩展

首阶段完成内部 D0–D2 及各路径能力表；公开 `PK_CURVE_eval` 对 icurve 的最大阶数以真实目标版本确认，不能直接继承 line/circle 的 0–10 阶行为。

将来需要高阶时，可用截断 Taylor jet：令 $y(h)=y_0+\sum_{k\ge1}a_kh^k$。每阶先把未知 $a_k$ 设为零，计算 $F$ 的第 $k$ 阶已知系数 $b_k$，再解 $J a_k=-b_k$；输出导数为 $k!a_k$。这个递推依赖该分片足够光滑，并应使用相同的定义方程和分支。[DERIVED]

有限差分只作为诊断/测试和明确标识的后备；跨 chart knot、NURBS knot、接缝、terminator 时必须一侧化。不能给有限差分结果标上“解析 D2”。

# 17. 震荡曲面、低质量参数化与延拓框架

## 17.1 先分辨困难类型

参数退化、真实几何相切、真实高频振荡、导数不连续、错误输入、错误分支、内层函数噪声是不同问题。诊断中至少分别记录：曲面度量条件数、交线法向夹角、参数平面横截性、spine 消元 $D$、当前 knot/cell、内层误差和 source check flags。

文档对附着拓扑的 B-surface 要求 G1，并不意味着导入的任意几何都具有 C2，也不意味着数值求值器可以自动修复不合格面。[XT25 p.65]

## 17.2 连续性 cell 与步长

在原始 chart 段内部，按支持面的 knot span、连续性等级、参数接缝、过程面局部距离图的有效邻域分片。曲率估计可以用来限制预测步长，例如让 $\frac12\|x''\|\Delta t^2$ 小于本次预测误差预算；估计不可靠时缩步或做区间界。

cell 之间用明确的参数图转换、periodic lift 和 derivative side 连接。到达一个低连续性边界不能直接让 Newton 用两侧不一致的 Hessian；降到 D1 求步或切到适用的一侧。

## 17.3 参数 continuation

对当前 $F(y,t)=0$，从可靠样本 $(y_k,t_k)$ 解 $Jy_t=-F_t$，预测到 $t_k+\Delta t$，用目标参数约束校正。成功后评估预测误差和迭代次数调整 $\Delta t$，失败则退回 accepted state 并减半。

跟踪方向由目标 $t$ 与原生方向确定。步长不能跨过未处理的原始 chart 节点、endpoint 语义或接缝事件。到目标前最后一步仍精确设置请求的 $t$，不能用附近的接受点代替。

## 17.4 伪弧长 continuation

当固定参数局部校正条件差时，可对物理交线约束 $G(y)=0$ 求其一维零空间切向 $v$，解增广系统：

$$
G(y)=0,\qquad v_k^TD_m(y-y_{\rm pred})=0.
$$

$D_m$ 表示适当的参数/空间度量。原参数 $t=\psi_i(x)$ 在每个接受点回算，跟踪到目标截面附近，再用原参数平面完成最终校正。

伪弧长只改变内部推进坐标。如果原生参数映射本身发生折叠或同一局部范围存在多个解，必须报告参数化/分支问题，不得重新定义 `icurve(t)`。terminator 区间继续使用其专门约束，不能用全局物理交线绕开文档定义。

## 17.5 局部细分与多根隔离

失败后只在原 branch 的局部邻域细分。解析面或有保守界的样条块先做包围排除；对剩余候选用区间残差/导数界、局部 root isolation 和分支延拓筛选。多个候选根无法用现有证据区分时，返回 `AmbiguousBranch` 诊断。

强振荡面可能在很小空间范围内包含多个根。采样更密和选择最近根不是唯一性证明。`chordal_error` 是估计，可辅助 seed 半径，不得直接当成严格无漏包围盒。[XT25 p.47]

## 17.6 近似模型仅作为加速器

局部 Hermite、低阶多项式和样条近似可提供 seed、曲率估计、预条件或区间候选，但最终回原几何校正与验证。未经用户明确的几何修复操作，不得平滑控制点、降次数、抑制高频或放松某条残差来换取收敛。

# 18. 接受条件、认证和错误发布

## 18.1 求解器停止与几何接受分离

`CorrectorStopReason` 只说明迭代为何停止，`ValidateEvaluation` 才决定结果是否可发布。小 residual、小 step、stationary least squares、预算用尽分别记录。

| 查询类型 | 定义约束 | 额外检查 |
|---|---|---|
| ChartPoint | 原始 chart 位置 | 输入一致性、请求阶数和一侧导数 |
| RegularChartInterval | 两支持面 + 原始参数平面 | 分支、sheet、参数域、nested witnesses |
| TerminatorInterval | 所选面 + 两指定平面 | 端点分支、已确认的端点参数规则 |
| ExactTerminator | 原始端点位置/已确认端点契约 | 一侧切向与导数能力 |

这些语义类别必须进入 cache key、diagnostic 和测试用例，避免不同定义的近邻样本互相覆盖。

## 18.2 NumericalVerified 与 Certified 分开

`NumericallyVerified` 表示按照残差、条件数、局部误差估计、分支证据和参数域进行了检查，不是数学证明。`CertifiedLocalRoot` 还要求对明确邻域的严格包围、存在性/唯一性验证。`SeedOnly` 从不直接作为正常精确输出。

高条件数时，即使 residual 很小也可能有较大的位置前向误差。使用 $J^{-1}$ 对残差的作用或分解中的条件估计判断；不明确时提升精度、缩小区域或拒绝指定精度。

## 18.3 区间认证的可实施首版

对有严格区间 Jacobian 的系统，在盒 $X$、中心 $x_0$、点预条件矩阵 $Y$ 上构造：

$$
K(X)=x_0-YF(x_0)+(I-Y[J](X))(X-x_0).
$$

所有步骤采用向外舍入。如果 $K(X)\cap X=\varnothing$，可排除该盒内根；若 $K(X)\subset\operatorname{int}X$ 且经保守范数检查得到相应压缩条件，可认证该盒中唯一根。第一版采用同时检查包含和压缩的保守实现，不追求覆盖所有可认证情况。[N3；DERIVED/标准区间方法的工程化]

这个认证只针对对应方程与盒，不自动证明它就是整个交线的正确分支；仍需与 branch anchor/延拓证据连接。Jacobian 奇异的重根可能让验证不确定，不能据此判无根。

## 18.4 区间能力必须真实

首期优先支持解析代数 residual 和有正分母界的受控 NURBS cell。对过程面，内层结果、导数和分支都要有保守界，才能参与认证。仅有普通浮点采样的 evaluator 标为 `BoundsUnavailable`。

不得通过对 `Math.Sin` 的普通结果调用一次 `BitIncrement/BitDecrement` 就声称严格区间三角函数；必须有经验证的误差与取值范围处理。也不能把随机扰动、多初值或更高精度单点 Newton 称为 certified。

需要认证的请求如果能力不足，应明确返回未满足认证等级，而不是降为普通成功。需要的只是工程数值精度时，则可以按清楚标记的 NumericalVerified 契约工作。

## 18.5 错误模型与输出隔离

沿用 `AlgorithmStatus`，在 `ICurveEvalReport` 内加入细分原因，例如：`InvalidChart`、 `ParameterResolutionLost`、 `ParameterizationSingular`、 `AmbiguousBranch`、 `InnerAccuracyInsufficient`、 `UnsupportedBlendConstruction`、 `CompatibilityGateOpen`、 `BudgetExceeded`。不要把这些名字未经验证就映射成新的公开 PK 错误常量。

位置、导数、切向及相关 API 数组先写内部临时区；全部达到请求契约后统一发布。D0 成功但用户同时请求 D2 失败时，整个该 API 请求不发布半份结果。需要部分结果的内部算法另用明确的返回结构。[R3；R7]

# 19. 顶层编排、接口草案与工作区

## 19.1 求值流程伪代码

以下是控制流规格，不是已存在的函数名列表。agent 应使用项目实际类型并保持静态分派。

```text
EvaluateICurve(root, t, requestedOrder, options, workspace, cache):
    validate finite parameter, order, output capacity
    acquire stable geometry read view and version token
    prepare required dependency capabilities and original chart map
    classify query semantics before choosing any solver

    if query is ChartPoint or ExactTerminator:
        execute its position/derivative contract
        validate requested quality
        publish only on full success
        return

    lookup exact sample with matching semantics/version/side
    if its quality and order satisfy request:
        validate version token and publish
        return

    build a small deterministic seed list on the same branch
    choose constraint plan using available capabilities and state

    for each permitted seed:
        restore complete accepted nested state from that seed
        run bounded Newton / trust-region corrector
        if numerical stop and semantic validation pass:
            refine nested states to final accuracy budget
            rebuild true root Jacobian and compute requested jets
            validate the complete result again
            validate geometry version token
            publish output and verified cache records
            return Success
        else:
            preserve diagnostics, discard incompatible trial state
            switch plan only for the diagnosed failure cause

    try bounded continuation from a verified branch anchor
    then, if supported and budget remains, local subdivision/certification
    otherwise return detailed failure without partial output
```

GATE-B、GATE-T 影响的是相应能力路径，不应阻塞完全不依赖它们的任务。后备策略无权在同一请求中改变 `QueryKind`。

## 19.2 建议的 C# 入口形状

以下为接口草案，参数类型需由 agent 实施；裸索引使用项目要求的语义别名，不能原样理解为已编译代码。[R1]

```csharp
internal static class ICurveEvaluation
{
    internal static AlgorithmStatus Evaluate(
        in ICurveView curve,
        double parameter,
        DerivativeOrder order,
        in ICurveEvalOptions options,
        ref EvaluationWorkspace workspace,
        ref EvaluationCacheAccess cache,
        Span<KernelVector3> derivatives,
        out ICurveEvalReport report);
}
```

建议 `ICurveEvalOptions` 仅包含本问题族所需的精度、质量等级、预算、导数侧、缓存策略和诊断等级。不要把任意 kernel 设置都装进一个巨型 `KernelContext`。

`ICurveEvalReport` 建议保存：状态细分、query/plan ID、branch ID、source segment、位置/参数/内层 residual、误差等级、条件数指标、基础求值次数、校正/trial 次数、缓存命中类型和 fallback 轨迹。诊断轨迹写 caller-owned ring buffer，默认不分配字符串。

## 19.3 WorkspaceFrame 契约

采用单一 owner 的 arena cursor。进入子调用先存 checkpoint，再分配对子调用及返回后仍存活数据足够的独立片；退出在 `finally` 恢复 checkpoint。父 residual、Jacobian、accepted/trial states 不得被子 scratch 覆盖。

工作区大小由 plan 的变量/残差数量、jet closure 和同时存活区间计算。以测试验证：所有可观察 outputs 之外的哨兵未变；拒绝 trial 后 accepted state 一致；workspace 恰好不足时无半写与越界。禁止返回指向 scratch 的 cache witness。

## 19.4 AOT 与零分配边界

准备阶段可在受控 native arena 分配长期数据；热路径不能创建托管数组、闭包、接口对象、LINQ 迭代器或装箱 enum。静态模块配合有限 `switch`，公共线性代数以 span 接收数值，不引入逐点委托 residual callback。

统一入口不意味着每个几何都返回 121 项 jet。增加真实需要的 D0/D1/D2 快路径，在已知 plan 中直接调用相应静态能力；仅在请求高阶时分配/计算较大布局。

## 19.5 冷启动与懒准备

$t_i$ 的比例递推依赖前缀 chart 切向，冷启动不能假称已有 O(log m) 随机查找。初次准备至少计算到目标所需的前缀，批量/频繁随机查询则建议一次准备整个 chart 的 $t_i,f_i$；之后长期复用不可变参数映射与锚点 witness。

准备成本与单点校正成本分别统计。可用 UV 数据主要节省锚点反求；它不能替代正则性、方向和原始坐标一致性检查。

# 20. AI Agent 实施任务与依赖关系

## 20.1 执行规则

每个任务单独提交或形成可独立审查的变更集：先给失败测试，再实现最小代码，再运行指定验证，最后更新证据。共享接口由一个集成 agent 维护；数值组件、chart/导入、过程几何与缓存可在稳定接口后分工，不并行修改同一核心记录布局。

遵守仓库 `AGENTS.md`：永久文档放 `docs/`，临时证据放 `temp_docs/`；代码使用 .NET 10、AOT、语义别名；脚本使用单文件 C#。真实 Parasolid oracle 通过既有 PKToy / `ParasolidScriptHost`，不能将其库名重定向到自己的 kernel，也不能把自我对比当 oracle。[R1]

以下任务 ID 与交付包的 `implementation_tasks.json` 一致。未关闭的语义 gate 只阻断依赖该 gate 的发布，不允许用跳过测试制造完整支持的结论。

## 20.2 任务总表

| ID | 任务与主要产物 | 依赖 / 验收重点 |
|---|---|---|
| T00 | 基线审计、实际 schema/API 能力清单、兼容 gate 记录 | 记录 HEAD、PDF 哈希、实际 PK build；不改几何 |
| T01 | 持久记录、source sense、构造面引用、池生命周期 | T00；删除/slot 复用/rollback/变长 block 测试 |
| T02 | INTERSECTION_DATA 解码与原始锚点保存 | T01；四种 UV type、0/1/2 terminator、null 与错长 |
| T03 | 解析隐式/局部距离 jet 和 sheet guards | T00；值、梯度、Hessian、方向和半锥/环面反例 |
| T04 | 原始 chart 重建、查找和参数平面 | T02、T03；非均匀弦、C1、原始节点不动 |
| T05 | 小型 LU/QR/SVD 与 Newton/TR 步 | T00；病态矩阵、秩亏、线性回代与小步非成功 |
| T06 | 分支状态、候选 seed、UV 展开及 cell | T04；接近双分支、seam、确定性 tie-break |
| T07 | P4/P2/I3/I2/I1 常规 icurve 求值 | T03–T06；等价路径、制造根、失败隔离 |
| T08 | D1/D2、jet closure、导数侧 | T07；隐式导数、chart 节点两侧、真实阶数能力 |
| T09 | L0/L1/L2、完整 witness 预测和 trial 回退 | T07、T08；同节点不同参数、精度升级、拒绝步 |
| T10 | L3、版本传播、淘汰与跨调用生命周期 | T01、T09；reset/rollback/ABA/共享子几何 |
| T11 | 一般参数面局部距离与 offset 能力 | T03、T05、T09；脚点分支、K 条件数、敏感度 |
| T12 | 常规 R blend 参数式、接触 frame 与 jet | T08、T11；正负 sense/range、arc、退化拒绝 |
| T13 | blend 包络与局部隐式计划 | T12；4×4/3×3、D 指标、整圈错误根拒绝 |
| T14 | 约束提升、块 Jacobian 和 Schur | T09、T13；六未知量、非零内层残差、plan 迁移 |
| T15 | BLEND_BOUND 距离组合与边界专用计划 | T11、T12、GATE-B；角色、D3 收缩和零 UV |
| T16 | terminator 专用分类、一维/二维求解 | T04、T05、GATE-T；选面、两平面、非定义面诊断 |
| T17 | 震荡诊断、continuation、局部细分 | T07、T09、T14；预算共享、接缝/多根/噪声 |
| T18 | 有界代数/样条 cell 的局部认证 | T03、T17；向外舍入、无根/唯一/不确定分开 |
| T19 | Runtime/PK 接入、XT roundtrip 与真实 oracle | T07–T16；已声明能力逐类验证；公开错误契约 |
| T20 | 性能基准、内存预算、回归文档与发布矩阵 | T10、T17–T19；相同正确性门槛下比较成本 |

## 20.3 建议里程碑

**M1：无过程嵌套的正确常规求值。** T00–T08 交付解析/参数支持面下的常规 icurve，保留显式未支持能力；不能宣称包含 terminator/BlendBound 全覆盖。

**M2：完整状态与跨调用缓存。** T09–T10 交付冷/热、正序/逆序/随机访问一致的结果与生命周期测试。性能优化不得放松 M1 的验收。

**M3：嵌套过程几何。** T11–T16 交付常规 rolling-ball、联合约束及已关闭 gate 的构造性路径。GATE-B/T 的证据不可由数值自洽测试代替。

**M4：困难区域和发布。** T17–T20 完成可靠后备、认证子集、oracle、ABI、基准和明确能力矩阵。

## 20.4 单任务完成证据

每项至少提供：改变文件列表、实现的能力/未支持范围、对应单元测试、实际运行命令与退出码、失败夹具和诊断位置、热路径分配结果、适用时的 oracle build 与输出文件。`Skipped`、 `NotAvailable`、 `NotRun` 不能合并为 `Passed`。

若实际 HEAD 与本文不同，agent 先判断每项是否已有实现并加测试，不通过重复造一套类来“满足文档文件名”。

# 21. 验收测试矩阵与制造夹具

## 21.1 数学层测试

| 类别 | 必须覆盖的测试 | 判定标准 |
|---|---|---|
| Chart | 非均匀弦长/曲率、非零 base parameter、scale | 递推与平面公式一致；节点位置保持原值 |
| 参数连续性 | chart 内部节点左右 D1/D2 | D1 在适用条件下连续；D2 不被强制抹平 |
| 常规计划 | 同输入 P4/P2/I3/I2/I1 | 相同原生参数和分支下位置/导数相容 |
| Analytic guards | 双锥错误半部、apple/lemon、轴/极点 | 零隐式残差不足以绕过 guard |
| 距离能力 | 真 SDF 与平方隐式式对照 | 后者不得被距离组合接受 |
| Tube | 圆 spine + 常半径截面 | 包络、梯度、Hessian 和局部距离公式 |
| 联合系统 | 同一几何 nested/augmented/Schur | 相同根；非零内层 residual 下的 Newton 步一致 |
| Terminator | $z=0,z=y^2-x^3$ 的一面构造 | 按定义接受；未选面偏差作诊断 |
| BlendBound | 正交平面字面式反例 | 保留 GATE-B，不自动交换下标 |

交付包的 `reference_vectors.json` 给出若干可直接转换为单元测试的数据与期望值。它们只验证数学关系，不代表这些制造数据已经通过真实 XT receive。

## 21.2 Jacobian / Hessian 测试方法

每个 residual 模块使用两个独立路径：实现的解析/链式导数，以及在单一光滑分片内部的数值差分或可用的复数步对照。复数步不能跨 `Abs`、归一化分支、clamp、 `atan2` 分支或含不兼容内层求解的代码。

使用多个步长观察误差先降后升；单一有限差分步长过测不足以证明公式正确。检查方向一阶与二阶收缩，比只检查完整矩阵的少数元素更能发现 chain-rule 遗漏。

§10 包络的 $(E_1)_s$ 测试必须包含不在解上的状态；§11 Hessian 测试必须有 $d_1$ 三阶导数非零的曲面，不能只用两个平面使遗漏项自动为零。

## 21.3 UV 与限界数据测试

对每种 `uv_type`，覆盖无 terminator、仅 start、仅 end、两端 terminator；验证额外 UV 顺序、chart 第一组偏移、总长度检查。包含单个 UV pair 为 null、仅一个分量 null、未知 enum、数组截断、超长、branch point 重复及周期展开。

极点/参数奇异处可能有多个 UV 对应同一点：不能因 UV 差很大就判位置不一致，也不能把一个 UV 的 jet 直接用于另一个参数图。构造性 `BLEND_BOUND` 无 UV 的事实必须由能力层表达。

## 21.4 缓存测试

相同查询集合分别冷缓存、正序、逆序、固定种子随机序、混合导数阶数、由松到紧容差执行。结果应在相同质量门槛内一致，分支、query kind 和 derivative side 必须相同；不要求所有平台逐 bit 一致。

对共享 spine 的多个父 blend 交替求值；对同一个节点在同轮的两个不同参数点求值；对缓存淘汰、分配失败、模型修改、slot 复用、session reset、mark goto/delete 覆盖。检查精确样本不会被 seed 覆盖，trial 拒绝不会污染已接受的 correspondence。

变换测试需保持语义：均匀缩放 $\lambda$ 并保持同一参数时，chart 弦长乘 $\lambda$、scale 除以 $\lambda$。支持面交换、反射和 sense 改动应连同映射规则一起测试，不能误用“交换支持面但同一 t 的导数不变”作为不成立的断言。

## 21.5 困难曲面测试

制造不同频率与振幅的波纹 B-surface、极不均匀参数尺度、近相切支持面、spine 近焦点、非常接近的平行/交叉分支、多个 knot 连续性等级、权重跨度较大的 rational surface，以及局部输入不一致。

验收关注：不返回错误根、不越界、不挂死、预算可预测、可定位失败、提升精度/细分后能恢复的案例确实恢复。难例未收敛可以是正确状态；把它误报 success 是阻断缺陷。

## 21.6 Oracle 与 ABI 验证

沿用项目的真实 Parasolid 工作流：目标 API/几何构造、项目 XT 写出、真实 receive、相同基准建模及 `PK_DEBUG_BODY_compare`，保留结构和语义必须通过的检查。对本任务尤其不能把 icurve 参数化不一致作为“仅排序差异”降级，因为参数就是公开求值结果的一部分。[R1]

使用真实头文件与已安装绑定发现可用 API，不凭本文创造 `PK_ICURVE_create` 等未核实入口。若本期没有公开创建 icurve 的路径，可先用真实 XT 夹具与已有内部导入/构造接口测试计算模块；公开 API 能力与完整 roundtrip 仍单列未完成项。

`BlendBound` 不公开 UV；cone XT/PK 轴、负 spine sense 的 v 端映射、chart knot 处高阶导数、terminator 参数和值均列专门 oracle 用例。保存原始 XT、query 参数与 derivative request、真实输出、我方输出、误差、PK build 和 schema。

## 21.7 数值门槛与模型分辨率

文档默认线性分辨率为 $10^{-8}$、角分辨率为 $10^{-11}$；它们是模型/格式相关尺度，不能直接等同于所有 evaluator 的 Newton 停止门槛。[XT25 p.26]

对良态、单位尺度制造解可设严格的绝对/相对门槛；对一般模型依据请求容差、局部尺度、条件数和实际 oracle 行为定义。测试必须同时记录容差，不通过改阈值掩盖 regressions。要求超出可支持精度时报告能力/数值限制，而非悄悄放宽。

# 22. 性能、内存与可观测性

## 22.1 基准不能只数 Newton 次数

每条路径统计准备耗时、基础面/曲线 D0/D1/D2/D3 次数、嵌套求解次数、残差/Jacobian 次数、分解与线性回代次数、trial 拒绝数、continuation/细分节点、缓存命中类型以及最大临时/长期内存。

分别测冷启动、同点重复、顺序扫描、随机查询、同一 spine 多父共享、逐次提高导数/精度和困难尾部。记录 P50/P95/P99、样本数、固定随机种子、CPU、RID、构建模式、实际 HEAD 与能力配置。

## 22.2 起始优化优先级

先减少昂贵基础几何和嵌套求解，再优化小矩阵算术。优先实现：准备视图跨调用复用；原始 chart map 持久复用；完整状态预测；接触 witness 复用；D0/D1/D2 按需 jet；根处同一分解求多个导数右端；代数/局部隐式消元；按几何和原始段分组的批量查询。

批量内部可重排求值顺序提高 locality，但最终输出须恢复请求顺序。串行根依赖不适合在每个 Newton 步里临时启动任务；真正独立查询的并行化以后续 Runtime 隔离能力为前提。

## 22.3 发布门槛

热路径预热后不得产生逐点托管分配；长期缓存有配置上限并验证淘汰有效；scratch 峰值必须与准备预算相容；所有失败路径无泄漏和越界。具体速度提升目标在基准得到后设置，不能在尚未实现时承诺倍数。

比较 nested 与 augmented、P4 与 I2 时使用相同查询、相同质量等级和同样的错误根拒绝标准。更快但跳到了别的 branch，不算性能改进。

## 22.4 诊断级别

默认只记录计数器和最终 report；调试级别使用 caller-owned 短轨迹；失败重放级别在请求结束后把数据写到 `temp_docs/icurve-evaluation/`，避免在每轮 residual 内做文件 I/O。

重放记录至少含：source identities、参数、语义类型、原始段、初始完整状态、plan、scaled/unscaled residual、版本、预算、失败原因和适用时的最后有效 cell。不要只输出 `Newton failed`。

# 23. 兼容性 Gate、来源优先级与禁止事项

## 23.1 Gate 清单

| Gate | 已明确部分 | 待确认部分 | 受影响发布路径 |
|---|---|---|---|
| GATE-B | p.63 给出距离组合、无参数化、索引文字 | 距离角色与 boundary/support 对应，sense/range 映射 | BLEND_BOUND 与边界专用优化 |
| GATE-T | p.49 的一面两平面、选面规则 | terminator 参数重建、退化平面、端点导数契约 | terminator interval / 高阶 endpoint |
| GATE-A | blend u 继承 spine、v 端映射 | 具体 arc 展开/极端退化及公开延拓范围 | blend 参数恢复与 sheet guards |
| GATE-D | 正则定义方程可求导 | 目标 PK icurve/blend 最大阶数与 knot 侧行为 | 公开高阶 API |

普通 chart 的归一化余弦与弦投影**不再是 gate**。可以直接依据新版实现，不应重新开启“切线或弦任选”的歧义。

若新证据与本文的工程推断冲突，保留原始证据、更新 gate/规格与测试。若真实 PK 行为与说明文字不同，则明确区分文档语义与所支持版本兼容策略，不能声称两者从来没有差别。

## 23.2 必须避免的实现错误

禁止重新采样原始 chart 后重建原生参数；禁止近邻缓存直接当精确结果；禁止只用空间距离识别分支；禁止把整个管面候选当成正确 blend 圆弧；禁止对 terminator 一面构造强加另一面等式；禁止混用任意隐式值与 signed distance。

禁止直接在每轮迭代中查 Runtime tag/拥有链；禁止父子 scratch 重叠；禁止仅用 slot index 做长期 cache identity；禁止以 least-squares stationary 或步很小作为成功；禁止把高阶导数补零；禁止通过加任意 epsilon 隐藏真实奇异。

禁止改动 `AGENTS.md` 来绕过 oracle、路径或零分配规则；禁止提交原始专有 PDF、真实 Parasolid 二进制/头文件或本机许可证作为这份方案的附件。来源记录用文档版本、章节、哈希和合法获取位置即可。

# 24. Agent 启动指令与完成定义

## 24.1 可直接交给实施 agent 的任务说明

> 在遵守仓库 AGENTS.md 的前提下，按本文和 implementation_tasks.json 实施。首先读取实际 HEAD 的计算框架、Runtime 求值、GeometryRecords、池/mark/所有权以及 XT 映射代码，提交基线差异和能力清单。按 T00–T20 的依赖顺序完成最小可测试增量。以 2025 版文档明确的 chart 弦平面规则为准；terminator 使用专门语义；BLEND_BOUND 的角色映射在 GATE-B 关闭前不得静默选择。所有快路径与后备保持原始参数、分支和方向。每项交付包含测试、运行证据和剩余限制，未运行的 oracle 不得报告通过。先完成正确性与状态生命周期，再用相同正确性门槛下的基准优化。

## 24.2 总完成定义

完成意味着：声明支持的每一种输入和导数阶数都有制造测试、真实兼容证据或明确限定的内部契约；常规/特殊参数区间语义正确；缓存冷/热和访问顺序不改变分支；nested/augmented 的等价性有测试；失败无部分发布与内存污染；困难案例有预算、后备和可重放报告；性能数据可复现。

“实现了若干类”“Newton 在几个样本上收敛”“通过自己的结果与自己的结果对比”不构成完成。局部认证只对已声明的能力子集负责，不能把它扩写成任意过程几何的全局保证。

## 24.3 命令与证据位置

常规入口继续使用项目已有验证脚本。涉及 build/test 时保留 `MSBUILDDISABLENODEREUSE=1`；清理残留测试进程不能吞掉原始 test 失败退出码。建议验证脚本自己保存测试结果与退出码，再执行符合仓库规则的清理。[R1]

```text
dotnet run scripts/VerifyKernel.cs
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests
```

新增 oracle/benchmark 脚本为相对于项目规则组织的 C# 单文件脚本；真实内核绑定和 session 初始化复用 PKToy / ParasolidScriptHost，不重复 P/Invoke 定义。构建 native 时使用实际 host RID，不把某台机器的 RID 写死成全平台结论。

建议永久交付路径：`docs/icurve_blend_evaluation_spec.md`、 `docs/icurve_evaluation_capabilities.md`、 `docs/icurve_evaluation_benchmarks.md`。临时实际结果路径：`temp_docs/icurve-evaluation/`。本文交付包中的 JSON 可作为任务工具输入，复制到仓库时按项目文档布局安排。

# 附录 A. 来源与可追溯信息

**[XT25]** Siemens, *Parasolid XT Format Reference*, August 2025，用户附件 `xt.pdf`，138 页。核对了 p.48、p.49、p.63 的页面图像以及相关章节文字。主要依据：§5.2.1.5 pp.46–51；§5.2.2.6–8 pp.61–65；§5.2.9–10 pp.88–89；§6 p.123。SHA-256：

```text
5b20ac101d76087de42c98e0b167202c7f6135bab8d25cfbb7cb72e8a983560e
```

**[XT01]** *Parasolid XT Format*, May 2001，用户附件 `Parasolid XT Format.pdf`，130 个 PDF 页面，正文明确描述 V12.0。对照正文 pp.43–44、60–61。SHA-256：

```text
adf6c5305475f480f91966594b5d1851b84a7f0f20a2520d698550efc2cb7c14
```

**[R0]** GitHub `main` 读取结果，2026-09-12 核对；本文固定提交：

```text
5c645cdfc8a0cd8e3f6aac4c25de839653b4daf6
```

**[R1]** `AGENTS.md`：.NET 10、AOT、零分配、路径和脚本规则、Parasolid oracle。**[R2]** `docs/Computational_Kernel_Architecture.md`：静态分派、计算视图、工作区、算法族编排。**[R3]** `src/ProjectGmKernel.Native/Runtime/KernelRuntime.Evaluation.cs`：当前求值分支、scratch、发布与错误映射。**[R4]** `src/ProjectGmKernel.Native/Runtime/GeometryRecords.cs`：ICurve、limits、blend 声明与当前几何记录。**[R5]** `src/ProjectGmKernel.Native/Runtime/EntityPools.cs`：当前 pool kind。**[R6]** `src/ProjectGmKernel.Native/Runtime/CommandScratch.cs`：每命令/每线程工作区。**[R7]** `src/ProjectGmKernel.Native/Computation/AlgorithmStatus.cs`：内部状态。以上 R 来源都对应固定提交，而非任意未来 main。

**[N1]** SUNDIALS / KINSOL 官方文档，*Mathematical Considerations*：Newton、inexact/modified Newton、解与残差缩放、停止条件。外部参考用于数值机制，不定义 XT 参数语义。访问核对：2026-09-12。

```text
https://sundials.readthedocs.io/en/v7.7.0/kinsol/Mathematics_link.html
```

**[N2]** Ceres Solver 官方文档，*Solving Non-linear Least Squares*：LM、Dogleg、信赖域和线性求解方案。本文不要求链接 Ceres，也不采用其停止条件代替几何验证。访问核对：2026-09-12。

```text
https://ceres-solver.readthedocs.io/latest/nnls_solving.html
```

**[N3]** R. E. Moore, *A Test for Existence of Solutions to Nonlinear Systems*, SIAM Journal on Numerical Analysis, 1977, DOI `10.1137/0714040`；以及 S. M. Rump 的区间验证研究说明。用于区间存在性/包含验证的背景，不据此声称所有过程几何已具备 interval 能力。

```text
https://epubs.siam.org/doi/10.1137/0714040
https://www.tuhh.de/ti3/rump/Research_Rump/topics.shtml
```

**[N4]** Microsoft Learn, *Double.Epsilon Field / System.Double.Epsilon*：该常量不是 machine epsilon。访问核对：2026-09-12。

```text
https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-double-epsilon
```

# 附录 B. 文档修订摘要

v2.0 相对上一轮方案：常规 chart 从待确认候选改为确定的弦投影；新增明确的 ChartPoint / Regular / Terminator 语义分类；补全一面两平面和 1D 降维；补全 INTERSECTION_DATA 的 terminator 布局；为 BLEND_BOUND 增加距离组合和高阶导数，但保留角色映射 gate；把“所有点验证双面”改为按语义验证；补充构造性支持面引用、source sense、池生命周期、完整状态缓存、联合求解、具体信赖域步骤、误差传播、任务 DAG 和测试矩阵。

本稿没有把未运行的 oracle、性能基准或 C# 编译标为通过。随包的数学测试数据只能作为实施起点，不替代项目集成验证。
