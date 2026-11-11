# 6616 原生算法优化独立复核

复核对象是 `270e2b6814afe5a48cd0532e89531f6fb80011be` 到
`6616cd60a55e50a5d8a5ada7274895c44609676b` 的整个改动，Native 子树为
`5b4c6104c600b0194bb543e2dc11efa89e2f0f1f`。本分支起点为
`7aea3ee6908f91313452383d41be07e2c329879d`，不修改作者实现或测试期望。

## 取证检查点

已实读本机用户级 AGENTS、项目 AGENTS、两份计算/局部建模架构、完整生产源码差异、
作者说明与质量 receipt。用户级文件整体 SHA256 实测为
`2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`，
managed policy 段（含 begin/end 标记和末尾换行）SHA256 实测为
`1a9db34c465b02dd8a87a6671514e51baabb9d4da96a7780d03d6158932f6e23`；
派发给出的 SHA 为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`，
未签署二者相同。实际读取的规则已遵守，未修改全局配置；不声称运行时自动加载。
私有依赖准备返回 `ENV_READY: profile=native pk=38.0 platform=linux-x64`。

原包 `temp_docs/incoming/mac-algorithm-6616-review.zip` 的 2,399,500 字节、
SHA256 `ca17564e0e5f59186b93841ac8e7ab91a77036d5d3ac0c3b37b7c0f8bfd3ca81`、
205 成员及 19,454,183 展开字节均已独立核实。检查 rooted path、父目录逃逸和符号链接后，
只解至新的 `temp_docs/review-6616/original-mac/`；没有覆写原始证据。
成员长度及 hash 清单另存 `temp_docs/review-6616/archive-manifest.tsv`。

派生的 `docs/native-modeling-algorithm/independent-6616-evidence.json` 核验原 TRX/log hash、
原始退出文件、计数、两项失败消息，并从两个固定 Git 版本重新读取测试源文件核 SHA。
候选结果仍为 1,914/1,912/2/0、原始退出码 1；270 基线只运行那两项，结果 2/0/2、
原始退出码 1。相同测试原字节与失败消息支持这两项为整合基线上已有失败，不能签成全绿，
也不能推定基线完整套件结果。独立检查器退出 0 不覆盖这两个原始退出码。

按追踪快照中 64 字节布局独立逐记录读取真实 key，得到 209,158 请求、22,157 不同 key、
187,001 重复、一个 scope；排除 use 字段和 padding，保留 scope、tag、generation、sense、
root、kind 和四个 double 原始位。三份成功操作 capture 无 captureErrors 或失败检查，
相应原操作 exit 均为 0；归档 XT 保持私有，不提交正文。

本取证检查点已以 `8d6638e` 提交并推送；它本身只接受以上取证事实。

## 整项结论

接受固定 270→6616 两项生产优化的源码、数学保持性及下述质量证据范围，未发现需要
修改作者实现才能解除的新增阻断。此结论保留两项已知基线失败，既不签完整套件全绿，
也不签正式性能目标完成；不是对移动功能分支、后续新模型或任意参数的普遍通过承诺。
本分支只追加独立文档/receipt，没有集成 main、发布或修改测试期望。

### 命令内包围复用

实际复核了 `src/ProjectGmKernel.Native/Runtime/ModelingPerformance.cs`、
`src/ProjectGmKernel.Native/Geometry/Intersection/SurfaceIntersectionPatch.cs`、
`src/ProjectGmKernel.Native/Geometry/Intersection/SurfaceIsoPlaneRoots.cs` 及实际消费者、
`src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedBlendCompilation.cs` 的编译过程。

缓存保存的是原生包围输出，不是新几何。tag、generation、sense、编译 root 和查询类别
均参与 hash 与逐项比较，四端点的 double 原始位逐次混合；最多探查 32 项，满探查窗
只淘汰起始项。每次命中仍验证全部字段，碰撞/淘汰只导致重算。端点比较使用 C# `==`，
不是逐位相等：NaN 不命中，正负零可数值相等；这不改变区间表示的几何域，不应把
追踪文件的“按位不同 key 数量”误说成缓存数值比较的严格定义。

存储的 bound 位于 iso、参数直线及 sweep 包装变换之前；命中后仍执行原包装变换。
支撑参数两侧与空间 spine/surface 使用不同 kind，不能互取输出。短输出在查表前拒绝命中，
仍由原求界报错。真实 compiler 为支持递归生成索引并保留原参数/CHART；同 tag 的 root
数字改变会成为 miss。相同 tag/root 在不可变 scope 内代表同一原几何，不以碰撞或数值
尺度替代几何身份；本结论依赖现有 scope 期间不修改被借用原定义的架构合同。

负项只在隐式 spine、有效根 chart、全编译视图无 BSpline 且无 BCurve 的纯解析路径启用。
这一限制排除了可能随剩余 spline workspace 变化而改变的失败。沿 BoundGeometry、
BoundSegment、EvaluateCore、区间 Jacobian 路径核实：该域采用固定栈工作区、原有迭代
和扩张次数；全部支撑（包括根 DAG 外）参与 ResidualAccuracy 基础尺度。提取 scale 没有
改容差公式，负命中另行匹配此尺度；不同编译视图尺度不能借用原失败。

StoreBound 只接收 Success/NotConverged，其他状态包括 Cancelled、WorkspaceTooSmall、
Unsupported、InvalidInput 均不保存；NotConverged 还要求有限 scale。失败命中不写 output，
也不生成空交或根不存在的证书。SurfaceIsoPlaneRoots 仍对 NotConverged 细分，保留深度、
累计预算、根隔离、有限分支及下游质量检查；新增 RootIsolationUse 只是计数。
没有增加取消能力或做取消延迟测量；既有边界上的检查未被改动，不能签取消端到端能力。

可选表只在第一个合格请求分配，与 CommandScratch 求解器 arena 分开；208 字节×32,768
=6.5 MiB。分配失败令本 scope 后续一直 miss，scope 结束释放，下次 scope 可重新申请。
不申请时没有这笔存储。外层可变操作不启用缓存，内层只读证明使用独立表；析构恢复父
context，工作计数合并而 elapsed 不重复相加。实际读取 ValidateRegularBlendDomain、
ValidateRegularContactPlacement、RegularCurvedBoolean、BuildRollingArrangement 的作用域，
没有将新表提升成 session/模型级共享状态。

独立本机 harness 验证了分配失败后恢复分配器仍不在本命令重试、下一命令能申请、相同
tag 更换 root 必须 miss，以及实际 entry 尺寸 208 字节。作者 Mac 69 项原 TRX/原 exit 0
保留了短输出、碰撞、命令/可变 scope、负项无输出、scale、spline 及生命周期证据。
没有在本机把这些 69 项重新跑成一套，也未模拟未知的生产取消路径。

### plane/spline 五变量 Jacobian

实际复核 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendPlaneSplineJacobian.cs`、
`src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendIntervalSupport.cs`、
`src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs`、SpineParameterMap
及 IntervalDual/IntervalVector 运算。归档新文件快照与固定 Git 源文件 hash 同为
`b308157fd5128aa57511f3b141857c892022e99930ddf99b4be3ccbd20e03e71`。

guard 限定一个 occurrence、五变量、隐式 spine、恰好一个 spline、另一个为 plane，且
spline UV 起点为 3。生产 EvaluateCore 的变量布局保证此域 root.Offset=0，XYZ=0..2，
UV=3..4，无嵌套 child。双 spline、非 plane、显式 spine、多 occurrence 不采用特化。
这些 guard 不扩展可支持的几何域。

固定 UV 后，spline foot、su、sv、signed normal 及两个单位切向均不依赖 XYZ。
五行仍为原有两个支持距离、CHART gauge、两个最近脚点正交条件：
`dot(p-o,axis)*planeSense`、`dot(p-foot,normal)-offset*splineSense`、
原 `SpineParameterMap.Interval`、`dot(p-foot,unit(su))`、`dot(p-foot,unit(sv))`。
特化只共享三个位置列的原生 contact hull/normal/tangent，按原支持顺序写前两行，
没有把三列直接填成手推常数而改变 outward rounding 的表达式分组。两个 UV 列保持原
完整 IntervalResidual 方向求导。仍检查 hull、normal、切向和每行的有限性；退化时返回
NotConverged，失败时矩阵不可消费。Krawczyk、收缩、nearest 及 branch 证书未改变。

作者原 20 项 targeted 结果和 exit 0 已核验；新增的矩阵测试实际是有理/非有理×支持顺序
两种、degree=2、单段非周期 knot 的两个盒，再加退化、非 plane guard 与分配检查。
这些测试没有覆盖所有 knot/degree/周期/尺度组合，不能只凭“25 项”声称全域证明。

本机另用 `temp_docs/Review6616Math.cs` 做 288 个独立制造矩阵组合：degree=1/2/3、
有理/非有理、两种支持顺序和 sense、开放多 knot span/正确重复控制点的周期 U、
尺度 1e-4/1/1e4、内部/跨 knot/外延/周期接缝盒、非零 offset、倾斜 plane、正负非单位
CHART scale、非零 spine 与 chart 存储偏移。288/288 均成功，每个完整 5×5 矩阵的
50 个端点均按 DoubleToInt64Bits 比较一致。此为原方向 evaluator 的保持性检查，
不是 288 个完整建模实体的 PK oracle。未覆盖任意阶数、任意奇异/溢出输入；这些范围
靠 guard、原求值器合同和失败传播审查，不额外宣称通过。

### 原始 Mac 质量与真实成品

原 suite 为 1,914 total、1,912 passed、2 failed、0 skipped，原 exit 1；两项为
NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative 和
SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled，均 Expected Success /
Actual NotConverged。独立 270 基线只执行这两项，原 exit 1，失败消息与固定测试字节相同。
没有用其他移动分支、旧 676 接受记录或检查器退出码覆盖它们。

shortedge（command-bound-reuse）、curved 与 reloaded curved（plane-spline-columns）、
plane/NURBS rounding-only 的原日志、capture、compare mask 与实际 XT 均存在且已核 hash。
三个 capture 合计四个操作，分别 63、52、28 个检查，无 captureErrors/失败检查，原操作
进程退出码均为 0；BODY_compare 保留类型/sense/容差/missing/matching，只有 n_cht_pts
诊断。curved 的 CHART 差异并未把整个 local 检查关闭。NURBS 倒圆独立结算，其下游
Boolean 本轮未执行，不签组合场景。

实际应用 parasolid-research 技能，调用 MCP 的 PK_SURF_intersect_curve、PK_SURF_eval、
PK_DEBUG_BODY_compare detail 及 COI_curve_surface callgraph；原型显示有限 bounds、原 UV
与导数输出，调用链包含分类、全曲线包围和通用求交。没有据静态 Linux callgraph 断言
Mac 动态选中某条内部路径，也没有跨平台使用符号地址。

本机真实 Linux PK38.0.150 又接收原 Mac GM shortedge 产品与归档独立 PK reference，
输入 SHA256 分别 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`、
`b1b0028feca8fd51e0b4b5615d4c34ad81adbee43648dd6c5e320ef9af464655`。
两体完整 BODY_check 为零故障，AssertEquivalent 使用 max_diffs=1024、all_tests=false、
acc_dev_tests=false、tol_dev_tests=true，结果 global=21921/no_diffs、local=21931/no_diffs；
没有用 face_pairs=0 当作匹配数量证明。3 条实际有限 ICurve 的 27 个原参数 D1 样点有限，
15 项物性最大归一残差 3.9657217010385984e-9，小于原门槛 1e-7，进程原 exit 0。
该样点检查不宣称任意曲线全域等价；原 Mac 有限曲线对照仍是该操作的原证据。
这次复核没有重建模型冒充原产物，也没有新执行 GM 倒圆。

### 计时、撤下实验与执行边界

原 shortedge 工作量相同：根单元 220,895、根 3,518、SSI 4,956、winding 196、预算失败 0；
实际求界少 156,218，等于新增成功命中 65,745 加失败命中 90,473。它支持重复计算减少，
不等同排他 CPU 节省。开发观测 17.075→6.660 秒不是固定窗口 ABBA。
curved/reloaded/NURBS 的全方向调用从每 Jacobian 5 次变 2 次，但三个位置列仍计算；
不能签 CPU 减少 60%，也不能从 curved 微小时间差归因。新正式成套 GM/PK 计时、
倍率、完整 PK 性能目标均未完成，需协调者另排测量窗口和看板交付。

Taylor 初盒完整操作原失败 157/budget1 保留；全局 hull 与 extent64K 候选仅为归档撤下
实验，固定生产 diff 没有这些实现。不要求重跑它们，没有增加预算、兼容分支或放宽证明。
本次只运行 managed .NET10 harness 和真实 PK 原成品只读复核，无 C ABI 改动、无 AOT
编译/发布、无 fullsuite/ABBA/Mac 执行/Windows 启动，未清理任何别人的进程。

## 本机执行记录与复现

本机新增原始材料在 `temp_docs/review-6616/`，独立 harness 源码在 `temp_docs/Review6616*.cs`。
运行命令均带 MSBUILDDISABLENODEREUSE=1，数学 harness 另传 KernelTestHooks=true、
UseSharedCompilation=false、BuildInParallel=false；这是 managed build，不是 AOT publish。
PK harness 按项目 scripts/Directory.Build.props 复用已有 PskernelSharp 全局 aliases、
ParasolidScriptHost 与 ParasolidBodyCompare，无手写 PK ABI 或初始化副本。
为应用该目录 props，执行时将 `temp_docs/Review6616ArchivedProduct.cs` 原字节临时复制到
`scripts/Review6616ArchivedProduct.cs`，运行后删除执行副本；两份 SHA 相同，未提交脚本。

| 实际执行 | 原退出码 | 结论 |
|---|---:|---|
| 隔离解包 harness | 0 | 原包尺寸、hash、成员、路径检查通过 |
| 独立 evidence audit | 0 | 原 TRX/exit/hash/messages/test bytes/key/capture 核验通过 |
| 数学 harness 初次编译 | 1 | harness 未传全局 hooks，且少一个 using；未执行数学/建模 |
| 修正编译后的数学 harness | 134 | 288 矩阵已通过，随后 GM session setup 失败，未记录原错误码 |
| 配置本进程 schema 路径后的数学 harness | 0 | 288 矩阵及 allocation/root/scope 检查全部通过 |
| PK harness 初始临时目录编译尝试 | 1 | 未导入 scripts props，未执行 PK 操作；最终按上述目录执行 |
| PK 原成品复核 | 0 | 原产品接收、完整检查、严格比较、有限 D1 样点及物性通过 |

首次 session setup 失败不登记为建模操作失败；本进程设定脚本相对 schema 路径后成功。
原 setup 失败日志 `temp_docs/review-6616/math-review-run.log` 保留，不以最后成功覆盖。
本次没有启动 testhost；所有自有 .NET harness 已自然结束，不运行全局 pkill。

独立复核源码、上述原始日志及成员清单另包为
`temp_docs/review-6616/independent-review-evidence.zip`，10 个文件、17,894 字节，SHA256
`b90d5eead7bcc3683dec20b6457275f8d1d3f54a440f6fac38cd7c20cd4ec19e`。
此包不含厂商库、凭证或 XT 正文；原 Mac 包终检 SHA 未改变。
