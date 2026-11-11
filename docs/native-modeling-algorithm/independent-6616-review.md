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

## 接续：固定正式 ABBA 原包独立审计

本附节接续 `5a4ad5319b77c0944baf598e07dbbce79782a483` 的限定代码/数学结论，
不重做该审查、不改变前文历史截止状态。接受本节固定的一轮 Mac 实测数据及制品绑定，
供协调者交付看板；没有发现阻断这些限定数据使用的问题。接受不扩展为套件全绿、
新物性资格全部 Verified、排他 CPU/热稳定/因果证明或完整 PK 性能目标完成。

### 来源、取证与统计

原包 `temp_docs/incoming/mac-controlled-270-6616-original.zip` 实测 5,644,747 字节，
SHA256 `97a3164698a16f1c543793289f1b863b1a67e20be7127ed5b0da017e0016120a`，
490 个成员、36,430,726 展开字节。C# 检查路径逃逸、rooted path 和 symlink 后解至新目录
`temp_docs/review-6616-controlled/original-mac/`，逐成员长度/hash 重新核验；原包和旧包不改写。
完整成员清单为 `temp_docs/review-6616-controlled/archive-manifest.tsv`。

实际读取并核对固定交付 `03e5a8e8ec9a8a297346e42d1b854f6293c8a083` 的说明、
operation-summary、measurement-receipt 和 material-binding 与包内原字节相同。
基线仍是 `270e2b6814afe5a48cd0532e89531f6fb80011be`，Native 项目子树
`afb8cec5cde9769cb68c470ea8da9591a1804e64`；候选测量包装
`a73bc08dbd39e984dff59dac7bba31a3baf80b4e` 的 Native 项目子树仍为已审
`5b4c6104c600b0194bb543e2dc11efa89e2f0f1f`。实际 Git diff 只有五主 oracle
还原到 270 原字节；五 oracle、Host、BodyCompare、Capture、runner 共九文件的两边
Git blob SHA256 均等于原 source-hashes 记录。原 report 的 NativeTree 字段实际指整个
`src`：基线 `c45cafbbd581642157e963ea55f2c5355fc65211`、候选
`41c8b597c54753ab394bc99a8b2e59bb3d83a756`，不可冒充上述 Native 项目子树。

独立审计器从 20 份实际 capture 重新取得 120 个 API 时间、60 个配对，核实五操作分别
按 baseline/candidate/candidate/baseline 顺序，每进程三个 fresh-input rep；rep0 为冷，
rep1/2 为暖，因此每 source/case 恰为 2 冷、4 暖。实际参数、selector、调用顺序及返回
status 均核验；Boolean 为 GM→PK，两个倒圆为 PK→GM。没有用 report.qualified
替代原检查。重新计算全部冷热 GM/PK 中位数、min/max、配对比值中位数和中位数之比；
原派生表及 JSON 的统计均一致。新增证据还保留原 summary 未列出的 PK min/max。

| 操作 | 基线暖 GM ms | 候选暖 GM ms | 变化 | 基线/候选 | 候选配对 GM/PK 中位数 |
|---|---:|---:|---:|---:|---:|
| sphere subtract | 0.07655 | 0.07810 | +2.02% | 0.9802× | 0.2758× |
| planar subtract | 0.63515 | 0.67470 | +6.23% | 0.9414× | 0.7274× |
| curved block subtract | 938.66770 | 883.69805 | −5.86% | 1.0622× | 444.0818× |
| plane/NURBS fillet | 1569.91415 | 1575.00895 | +0.32% | 0.9968× | 976.4974× |
| shortedge FixBlends | 15858.41445 | 5639.59090 | −64.44% | 2.8120× | 3423.4971× |

这些是同一固定窗口的 API wall 时间。三项暖 GM 变慢、curved 冷 GM 中位数变慢均保留，
不对快操作微差归因，不从一轮实测推广普遍倍率。全部冷数据未删：例如首个 sphere
基线 PK 冷值 12.664 ms，与另一基线冷值 0.4541 ms 同时计入；这也使冷配对比值中位数
27.84484 与两个时间中位数之比 3.60131 相差很大，不能互相替代。候选 shortedge 暖
配对 GM/PK 中位数 3423.49713 与中位数之比 3424.36754 也明确分列。

原窗口 UTC `2026-11-11T17:42:47.3159860Z` 至 `17:50:06.6601560Z`，
Apple M4/macOS 27.2 Arm64/.NET 10.0.11/SDK 10.0.400/PK 38.0.150，
PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`。一分钟 load 的原记录范围
3.38–15.37；源码 hash 记录和窗口前后 Native DLL hash 记录一致，但包中不含 DLL，
此处签的是原记录一致性，未在 Mac 重新读取或执行。进程快照不能证明排他 CPU 或热稳定，
也不能证明归档之外绝无其他执行；本包只存在这一轮完整 20 进程 ABBA。

### 原质量门与实际制品

20 个 Process.ExitCode 在原 runner report 均为 0，与原 harness 20 条退出行相符，
无超时；总 harness 原 exit 文件 `controlled-6616-ready.exit` 为 0。没有单独的每进程
exit 文件，因此不把 report 与日志描述为另一组独立进程实测。全部 capture 无 errors，
2,028 项检查均 pass，其中核心 900 项点值物性比较由独审重新计算阈值。
60 份 compare 文件均保留 max_diffs=1024、all_tests=false、acc_dev_tests=false、
tol_dev_tests/数量/类型/sense/容差/missing/matching=true；global 为 no_diffs，
local 为 no_diffs 或只含 n_cht_pts。累计 96 条 chart 诊断，无其他 local 差异。
完整 BODY_check、单 body、region/shell/face/edge/vertex 计数以及 API/blend-fault
实际值也重新核验；face_pairs 仍不被当成成功匹配总数。

原日志中的 curved 125 个独立材料查询、NURBS 每次 9 个有限 section 的半径/接触/切向
检查、shortedge 每次 3 条有限 ICurve 对照均保留并核查各三个 rep；它们仍是固定原 oracle
的有限验收，不升级成任意曲线全域证明。NURBS 与 shortedge 各自按独立操作结算，
本窗口没有执行其下游 Boolean。旧 suite 1914/1912/2/0、原 exit 1 和 270 两项 focused
失败原 exit 1 继续有效，未重跑或改成全绿。

**物性资格仅限固定 270 的原点值门。** 共同 Capture 由真实 PK 分别对 GM fresh XT
和 PK reference 求 15 项物性点值，在 accuracy=.99999999 下按
abs(GM−PK)/(1+abs(PK))<1e-7 判断；这不是 GM 原生物性 API 的模量证明。
原 capture 没有 modulus/uncertainty 字段。shortedge 的失败时模量诊断分支本轮未触发，
日志没有 MASS BOUNDS；不能把这 60 对 point-pass 改名为 4794/60be 新模量三态资格
全部 Verified。实际调用 parasolid-research MCP 的 PK_TOPOL_eval_mass_props 和
PK_DEBUG_BODY_compare detail，核实相关原型/输出；没有把 Linux 静态地址用于 Mac，
本接续未执行任何新 PK/GM 建模或物性操作。

逐文件核实 material-binding 的 312 个实际 mesh、34,964,660 字节，capture 每一条
before/tool/after 引用与 process/step/kernel/source/hash 均有对应；没有额外未绑定 mesh。
全部 mesh 的 source.kernel、commit、freshXtReceive 与实际 producer 对应，原 helper
通过 GM transmit→真实 PK receive 或 PK snapshot 获取显示几何，没有固定 mesh 重建。
原 geometryAuthor 的 312 个值都是 helper 默认的 independent-construction，不能据此
声称所有输入独立构造：shortedge 的 GM 输入实际来自未变更的 PK seed XT，GM 倒圆
成品再由 GM 导出并接收。看板必须沿用这一真实 provenance；默认作者字符串不是该 seed
的构造证明。curved tracking=false，原 semanticCoverage=missing 也保留，不能展示为
全部拓扑修改角色已知。

44 个原 XT 与 report/material-binding 的原进程、sourcePath、hash 及 rep2 逐一相符，
没有补造或公开 XT 正文。它们只属于各进程最后 rep2，且并非每一项都同时保存 GM/PK
两份产品；不能补签 rep0/1 也有独立归档 XT。每个 rep 的 mesh/质量/计时各自存在。
看板若使用这些 XT 作源，须选相同进程的 rep2；其他 rep 可使用自己已绑定的 mesh。

shortedge 所有 12 次操作的根单元 220,895、根 3,518、SSI 4,956、winding 196、
budget failure 0、bound requests 275,525 均相同。作者列出的暖 rep1 求界
223,331→67,137、Jacobian 1,597,561→525,315 与原日志一致；rep2 候选求界实际
67,166，未用 rep1 代替全部样本。60 次 PROFILE/WORK 与原 summary 逐数一致；本轮
公共 oracle 未打印 negative-hit/XYZ-column 数字，不补填开发窗口计数，不把调用下降
直接当成可删除或排他 CPU 成本。

### 交付绑定与未执行范围

可交看板的唯一组合为：计算源码 `6616cd60a55e50a5d8a5ada7274895c44609676b`、
测量包装 `a73bc08dbd39e984dff59dac7bba31a3baf80b4e`、测量交付
`03e5a8e8ec9a8a297346e42d1b854f6293c8a083`，加本附节所在 review 提交。
代码/数学资格仍引用 `5a4ad5319b77c0944baf598e07dbbce79782a483`。
看板源文件使用 03e 的 operation-summary/measurement-receipt/material-binding 原字节，
并绑定原 report SHA256 `fef5e9a8fc33aa9da3d1ab8c932b680f33aa9b0f24214e73e3987c5dfa01e868`、
material-binding SHA256 `df9a43cf55e2cf978d5c009c3920c2315417899d2a0d99f733ad2267b8173d7f`。
不得混入旧 676 窗口、241b 实验或 4794/60be 功能资格。

新增 `docs/native-modeling-algorithm/independent-6616-measurement-evidence.json`
保留全部复算统计、20 个 process 的 60 配对与 capture/log hash、九源文件 hash、
原退出文件、逐 rep shortedge 工作量，以及审计源/490 成员清单 hash。
审计器为 `temp_docs/Review6616ControlledAudit.cs`，只读原件，只写独立派生证据。

| 本接续实际执行/原材料 | 原退出码/状态 | 边界 |
|---|---:|---|
| 独立隔离解包、schema inspection | 各 0 | 只读取证，不执行模型 |
| 审计首次/第二次 | 各 134 | 检查器误要求球体有 edge、随后 JSON int/double 读取问题；原日志保留，无建模 |
| 修正后的完整审计 | 0 | 首次全部复算通过，派生首版另存 temp |
| 增加原 finite 检查后的审计 | 134 | 检查器误把候选 rep1 的 67,137 套到 rep2；原件实际 67,166，未改原件 |
| 按实际逐 rep 核验的最终审计 | 0 | 所有上述来源、质量、统计、制品绑定检查通过 |
| 原 Mac 资格检查/最终预构建 | 各 0 | 不是本机新执行 |
| 原首次 CLI 参数错误 | 134 | controlled-6616.exit；baseline 选项被等号形式误传，未进入计时操作 |
| 原首次候选预构建 | 退出文件缺失 | 包内日志止于 Xt 构建；60 秒工具超时来自交接说明，包本身不能独立补签该工具退出码 |
| 原正式总 harness / 20 子进程 | 0 / 全部 0 | 与前述 134 setup 和旧 suite exit 1 分开 |

本机只运行 .NET10 只读 C# 审计（MSBUILDDISABLENODEREUSE=1），没有新 suite、
288 数学矩阵、建模、ABBA、Mac 登录/执行、Windows、AOT、main 合并或网站发布；
没有启动 testhost 或终止他人进程。正式单窗口观测现已取得，完整性能目标仍未完成，
更广的模量质量资格及后续统一 Capture 修正不由此原窗口代签。

独立取证源码、成员清单和上述审计尝试日志另封为
`temp_docs/review-6616-controlled/independent-measurement-audit.zip`，10 文件、30,441 字节，
SHA256 `740f580f0d8a8097bf59034129c736963b1bd757865e8348273846ac5326884e`。
打包及逐项回读核 hash 的本机进程 exit 0；包不含 XT、厂商库、凭证或反编译正文。
两个原始 Mac zip 终检 hash 均未改变。
