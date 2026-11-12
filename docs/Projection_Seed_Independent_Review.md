# 投影候选搜索 a9085ed 集中独立验收

## 结论与交付边界

**Native 改动、连续距离证明的保持和关键消费者集成 accepted；原质量按真实执行及缺项边界 accepted；唯一 Mac 窗口作为描述性观测 accepted。没有新增作者算法阻断。** 本结论允许交付该独立候选及其证据，不表示任意输入必收敛、参数唯一、全部数值通过或整体 PK 目标完成。

| 对象 | 状态 | 限定 |
| --- | --- | --- |
| 上界候选缩减、原连续上下界及完整遍历 | accepted | 原合法 witness、positive rational hull、有限域和距离 gap 合同保持；失败仍失败 |
| Native 参数、关键消费者、几何／拓扑／生命周期 | accepted | 原成功范围与原认证门；不增加任意退化、多谷或边界驻点支持能力 |
| 原 Mac 2033 测试、八质量组和产品证据 | accepted | 接受真实证据及其 pass／missing／raw exit；不是全部检查通过 |
| 唯一五操作窗口 | accepted | 固定来源、全部冷暖点、两种比值的描述性数据 |
| 原 1e-7 物性全部认证 | blocked | 正式窗口 1080 分量仍为 396 Verified／684 Unresolved／0 Different |
| 原完整性能资格 | blocked | 16 个 process raw2、runner2、performanceQualified=false |
| 普遍／因果／单机制排他 CPU 收益、整体 PK 性能目标 | unproven | 四暖小样本、有回退及散布，无排他 CPU／数值温度资格；NURBS 仍约 221 倍 PK |
| 与 ab0177e 合流、main、UI／门户／上线 | 未验收／未执行 | a908 未叠 ab；没有合 main、发布或接下一优化 |

集中未决只有上述原数值与性能资格、普遍性及更大目标；不要求作者改 seed 算法、改期望或重跑 Mac suite／family／ABBA。新增解析控制暴露的原预算失败另列如下，不能用已取得的局部上界冒充完成。

## 固定来源与规则

交付起点 `ea5a27b38de6abf766200a58dccae65596d9e996`，计算 `a9085ed12bf811cd337bbf2f69964d0cd3957373`，质量／窗口 metadata `1e59c305c6e28c84ff8bd1090bcf9a33169a261d`。实际核同 Native `101e5ff835ac883884367d90979f9bba8bd7ea23`、src `e981af6722fc2cefb282a4ef32d105b35b60b537`，不把 metadata SHA 当另一次计算实现。

基线固定 `b8c74b95047577e52d1086b48b0b0a88ee3f4388`／计算 `e7f0a4876ef296b56f5f7c57a45f15617eac6918`／Native `6a84372805cefdaf5c10cb3e0f3e56f8eb12ffdc`。实际以 `git show 59322ef758c4b3ffb4e740506cc2412191b59c8a:docs/Certified_Isoparametric_Independent_Review.md` 读取已接受边界，复用 e7、Section 与此前 Projection 的原包围证明，不重审已接受算法或把 ab 的限定接受当成组合验收。

实际读取用户级规则文件、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`，核 b8→a908→ea5 全文件清单与实际差异，读取三份作者报告及其机器材料并逐源核 PDB／Git。用户规则 marker 正文含尾换行 SHA256 实际为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`；这证明读到的文件，不声称启动时自动加载。GM 原生 XT、Parasolid 的拓扑、几何、拥有关系、容差及参数化均为前提。

实际生产差异只有 `src/ProjectGmKernel.Native/Geometry/Projection/BSplineProjection.cs`：初始完整 patch 的 Improve 仍五 seed，后续子盒只用原中心 seed；初始盒可先由合法旧 best 的下界证明直接剪掉，此时无需调用 Improve。新增测试文件四项，不改原 185 项期望。Newton 位置 carry、Bezier isoline／point、dd／b095、移动 query／norm、ab 局部 jet 均未带入。没有新模型、UV 重参数、ABI、cache、heap 或预算。

私有环境准备实际执行 native ensure，返回 raw0／`ENV_READY`，schema、同平台 PK 38.0 运行库与 corpus 就绪。PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`，动态核实 PK 38.0.150。未提交私有依赖。无 C ABI 变化，未运行 NativeAOT；未唤 Windows、未到 Mac 补跑。

## 连续数学与失败语义

对当前有限盒 B，原 restricted homogeneous Bezier net 包围真实提取／限制的控制数据；`BoundPosition` 逐控制点要求权重区间下端严格正，逐 Cartesian 比值要求有限。实际曲面因 Bernstein 系数非负且和为一，位于真实 Cartesian 控制点的凸包；原区间 hull 因而覆盖 **整个** B。其依据是原提取、blossom restriction 和外舍入，不是控制点或曲面采样。

对任意合法 native 参数 witness w，原 `Bound(patch,{w},values)` 的前三项包含真实 S(w)，`UpperDistance` 对三个坐标的差、平方、和、sqrt 向外包围，给 U≥‖S(w)−q‖≥d*。完整九项位置／du／dv 仍在原每次 Newton 点执行，trial 仍走原 `BoundPosition`，18 次 Newton／12 次 trial 未改。导数与驻点仅用于提议下一个 w；导数不可用、错误驻点或 clamp 停顿都不能产生删域证明。有效位置上界不依赖导数给出的方向正确。

`ProjectionPoint.Point` 是位置区间的 midpoint，**不等同于已证明位于曲面上的精确 S(U,V)**；上界证书来自整个位置区间而非 midpoint 的距离。midpoint 只影响 Newton 提议和支持平面方向的效率。`Improve` 不能修改传入 patch 的只读 poles；其九项和 trial 包围使用自己的栈工作区。Closest 为当前盒持有的 restricted net 不会被覆盖；外层下一 patch 的提取在前一 Closest 返回之后才复用外层 scratch。

两个下界都保留：

1. `BoxDistance` 是 q 到整个 Cartesian interval box 的距离，平方和下端、sqrt 向下收敛，因此 Lbox≤min(B) distance。
2. 对最终规范化的任意方向 d、‖d‖≤1，令 M 为所有 restricted Cartesian pole 对 `(P−q)·d` 的向外上包围，则每个 S∈B 满足 `−M≤(q−S)·d≤‖q−S‖`。所以 `max(Lbox,−M)` 仍保守。d 来自当前 best.Point、另一个 patch、区间 midpoint 或坏驻点均不改变此不等式。

规范化继续用原区间平方／和、sqrt 的上界并再 BitIncrement，缩放系数用原 BitDecrement reciprocal；其外舍入余量用于覆盖归一化和最终浮点乘法舍入，不是把浮点 unit 向量假定成精确单位长度。长度非有限／零时不使用该方向，原 AABB 下界仍在；方向计算不可用也不能授权删域。以上使用原已接受 interval／norm 合同，候选缩减没有修改这些运算。新增 exact dyadic 控制直接调用原私有 `BoxDistance`／`UpperDistance`，独立验证一个三维盒的精确最小平方 3/4、最大平方 59/16；它是舍入控制，不替代连续论证。

pending 初始覆盖整个 patch；每次二分两盒共享同一中点，覆盖不丢域。只有 `lower >= best.DistanceUpper−tolerance` 的原距离 gap 或 `bestUpper<=tolerance`、并使用 d*≥0 的快捷证明可免继续细分。best 只下降，先前以较大 best 剪掉的盒仍满足最终 gap。全部 pending 完成才 Success；200000 累计 cells、128 pending、最小归一 width 1e-13 及全部失败 status 不变。有限浮点比较及区间舍入沿用原合同，没有新声称任意容差的精确实数等号结果。

因此子盒少 seed 只能改变上界提议与搜索次序效率，不能让未证明盒消失；可能增加细分或触发原失败。局部最小、切触、端点、多谷、近乎平坦都不因命中一个 seed 就获得全局完成资格。

初始识别直接比较复制自 patch.Domain 的四个端点，不作 epsilon 判断或归一化改域。每个 knot span 独立保留自己的 native domain，下一 span 可持有上一个 span 的合法 best witness。零次数方向不参与宽度选择，原一维 curve 适配继续使用正宽 dummy 方向；次数为零不意味着把 native domain 改成零宽。真正零宽 domain 被原 Restrict 拒绝；两方向零次数而 gap 未闭合则原 NumericalFailure。反复重复初始 seed 或不能继续有效二分只会遇到原失败门，不是成功证据。

## 实际消费者与参数资格

实际逐调用核 `KernelRuntime.Projection.cs`、`KernelRuntime.GeometryDistance.cs`、`KernelRuntime.Intersections.cs`、`KernelRuntime.LocalOperations.BlendCornerValidation.cs`、`NestedSplineEvaluation.cs` 及其 series／joint／bound 消费者：

- `ClosestBSurface` 与 NestedSpline 的外层均从 Infinity best 开始，跳过零宽 knot span，对所有有效 spans 调完整 Closest，任何非 Success 立即传播；best 跨 patch 只带合法已评估 witness。
- `SurfParameteriseVector` 在完整 closest 成功后仍要求 `DistanceUpper<=1e-8`；curve 适配保留原有限区间及点匹配。SSI 的原 25-seed 快速 inverse 未改，后备仍要求成功及原几何距离门；它不认证交支或把求不到参数当无交。
- `GeomRangeVector` 成功时沿用原 midpoint foot 并重新计算点值距离；这个公开点值不是上述区间 U，不能把两者标成完全相等或把它当独立严格下界。native UV 没有被 snap、重参数化或由新估计代替。
- `NestedSplineEvaluation.Closest` 仍以原 1e-4 global gap 提供起点，然后 16 次原 Refine；NumericJets 使用原 native 参数与完整二阶导数。DistanceAtFoot 仍检查正定 Hessian／determinant，再求隐式 UV 导数及 finite normal／distance；联合方程仍有完整 Jacobian、ValidateNearest、原 CHART 域、Krawczyk 严格 inclusion／contraction、区间 nearest Hessian 与 witness 包含门。
- Corner 的非支撑面距离仍用原 global upper／gap；支撑面的唯一足点资格来自原强凸性／reach／stationarity 证明，不能由一个距离 witness 替代。局部拓扑、finite、发布、command scratch 及 journal／失败恢复不在 diff 内，原生命周期证据和一次 fresh 完整消费者继续覆盖它们。

**距离 gap 不证明 UV 误差、驻点、全局唯一参数或同一等距分支。** 多个等距最小点可返回不同合法 UV；本审查没有要求位同或给全局唯一参数签字。Refine 的 stationarity／点态 Hessian 也不单独证明任意曲面的全局唯一性；需要该资格的建模路径仍必须通过自己的有限域与区间证明。候选对这些后续证明只提供起点，不提供替代证书。

## 必要独立执行

四个独立入口为 `scripts/ReviewProjectionSeedEvidence.cs`、`scripts/ReviewProjectionSeedMeasurement.cs`、`scripts/ReviewProjectionSeedControls.cs`、`scripts/ReviewProjectionSeedRuntime.cs`。全部 .NET 10 C#；GM 直接引用托管项目，真实 PK 用 PKToy＋Using＋ParasolidScriptHost，无手写 ABI／PInvoke／session 或 oracle 重定向。所有 dotnet 禁 node reuse；子进程记录自有 PGID 并只清该组，未使用项目旧全局 pkill。

Linux 定向 **38/38、raw0、0 skip**，包含原 projection、Bezier bounds、geometry distance、blend projection 与新增 seed 控制；构建 allocation IL guard CLEAN。复用原 Mac 189／2033 和已接受根／CHART 等范围，不重新开整套算法审计。

制造控制固定 native U=[2,5]、V=[−3,−1]，old5 与 new1 均调用真实 `ClosestBSurface`，不复制生产求解器。对有限矩形平面、可解析的正有理权重平面、knot=3.5 的同面分片，覆盖 interior／edge／corner，并作正交反射置换与平移。成功候选的 U 和 U−tol 平方用 BigInteger exact dyadic 与解析 d*² 比较；每种路径预热后的实际完整 closest 热调用 0 managed allocation。共 **20 个成功场景×两路径**；不把控制 hook 或 0alloc 签成普通 API 搜索率／CPU 资格。

多等距谷底 `S(t,s)=((t−1/2)²,s,0)`、query `(1/16,3/8,1/4)`：t=1/4、3/4 都有全局 d*=1/4。两路径、两种姿态完整闭合；nested Refine／Hessian 也通过。它包含折叠退化处，纯距离支持不代表该退化面可以通过任意建模 regularity 门。

另设正则抛物面 `S(t,s)=(x,s,x²)`、x=t−1/2。query `(0,3/8,9/16)` 的距离平方为 `(x²−1/16)²+5/16`，有两个已知 interior minima；query z=1/2 的平方为 `x⁴+1/4`，是退化 Hessian 的平坦极小。对两姿态，**old5/new1 在 1e-7 的完整二维搜索都 NotConverged**，虽其 partial upper 已接近解析最小，也不当成结果。原 1e-4 的 nested 起点可完整成功，Refine 恢复该极小；平坦例 Distance 必须 Singular，两路径实际都拒绝，非平坦双极小例则保留原局部 Hessian 成功。这同时验证“距离近似”“驻点”“可微 nearest 足点”三种资格不同。

原 minimum-width 失败用 degree0 常值 patch 和 1e-30 请求实际触发 NumericalFailure；零宽 native domain、非正权重、非法 tolerance 均拒绝，两 seed 模式一致。没有提高生产预算或容差来洗上述失败；200000／128 的源码门保持，未专门扩大资源做压力矩阵。

参考语义实际加载 parasolid-research／parasolid-docs。经 q2 Parasolid query MCP 查 `PK_SURF_parameterise_vector`、`PK_SURF_eval`、`PK_GEOM_range_vector`，均 found:true，并查 error157=PK_ERROR_cant_do_intersect；官方 FD chapter32 说明 native geometry／finite topology 的距离区别，以及 range tolerance 不等于返回参数／点的几何精度。还明确读取 opt_level 的限制：默认 performance 复杂时可得局部极小，故动态控制使用 accuracy，并以解析全局解独立校验，未把 PK 当万能全局证明。已安装章节可读，章节里的 headers 链接在本机缺失，改读实际章节及 MCP 原型；不冒称缺失页已读。

真实 PK 动态在同参数正有理 finite edge minimum、折叠多谷、抛物双谷上分别核对：前两者 GM 两路径均成功且对应原 native finite 参数；抛物双谷 PK 返回 sqrt(5/16)，GM 两路径都原 error157，输出结果未伪造。Linux 静态符号地址没有用于其他平台，未由空调用图推断内部无调用。

三对原封存产品重新由真实 PK receive：curved 单操作、NURBS 开发最终 **rep5**、shortedge lifecycle **step4**。GM 产物与独立 PK reference 各自 full BODY_check 零 fault，严格 `AssertEquivalent` 均通过；保留 global/local、face/entity 与 n_cht_pts 诊断，未将 face_pairs 当匹配面总数。

另一次当前普通托管源 **fresh NURBS 完整消费者**：真实创建／Fix／GM transmit／PK receive／严格 compare，GM／PK API0、16 pass／0 fail／13 missing、captureErrors空、原 runner2。它是 Linux 执行，排除在 Mac 正式窗口外；不把 hooks 制品冒充 ordinary producer。

## 原质量、身份与唯一窗口

原完整 TRX SHA256 `27663a21ad9f4a7e6255195b627a9d05e793207c0866a6575cc4dcd846e5e576`，独立解析 **2033/2033、0 fail／0 skip、原 build0／test0、自然 2m49**。前后三 DLL＋三 PDB 每文件 hash 与 sealed 文件相同，清单字段数不同不判文件不同。原定向189／raw0／24s、新四项及原185期望复用；这些时长不是性能资格。

| 原质量组 | pass／fail／missing | 原 exit／实际范围 |
| --- | --- | --- |
| curved 单操作 | 14／0／13 | 2，step1 |
| NURBS 开发 | 96／0／78 | 2，六 fresh pairs，最终同名 rep5 |
| shortedge 单操作 | 35／0／17 | 2，step1 |
| rigid＋scale .125 | 51／0／1 | 0，仍缺一项 |
| mirror8 | 29／0／23 | 2 |
| shortedge lifecycle | 133／0／77 | 2，五实际步骤 |
| NURBS lifecycle | 16／0／13 | 2 |
| curved editable reload | 17／0／26 | 2 |

八组 CaptureErrors 均0；Fix、copy 删源、反射／缩放、两次 Boolean、可编辑自 XT reload／cleanup按实际步骤保留，后续操作不反判已独立通过的前序。原质量45份 strict／24份 mass，与窗口144份 strict／72份 mass 分开；独查189份 strict 的结构 mask、global/local 和差异条目，仅356条 n_cht_pts diagnostic，**本包无启用其他 conditional finite 降级的条目**，没有全关偏差门。原 mass U 和七 raw2 没有被检查器0补签。

10 质量 PE／portable PDB 逐链接、MVID、hash 和 fixed Git checksum 核实；ordinary Native336、testhooks Native336、Tests170，三个 source 集合，generated／unmapped 单列。ordinary actual-loaded 与测试 sealed／前后清单各自论证，不补 test loader 或 start/end。ordinary MVID `6a992446-9083-4400-9f6b-6b0b91f476f8`／DLL `29b4d3f243a6a5df37629d732f2096ff9b75d43dc46da479b9fc79cfd7637a20`；hooks `a86ba398-3bd3-4cd8-96dc-2143cc251046`／`8a0f1afebfb4051e3e87a711592764d2cc1f61b268d44b6324743de5bd655837`；Tests `ef9baabe-a84f-4504-8f19-25a9e901c496`／`015df3add9a5114f0bcb6bff62a171fa4c4949364f251cbc50540cd1a20fb6cf`。Mac PE只解析元数据，没有加载冒充 Linux 执行。

唯一正式 UTC **2026-11-12T11:12:20.7951730Z–11:14:18.6309000Z**，M4／macOS27.2 arm64／SDK10.0.400／runtime10.0.11／PK38.0.150。十一 helper／runner 两树逐字同，十 managed-only prebuild 原0，串行五操作 ABBA／20 process×3 fresh GM-PK＝60 pairs／120 API0。每 source／op 为两 rep0 cold、四 rep1/2 warm；16 raw2／4 planar raw0／runner2／performanceQualified=false 保留。

| 操作 | b8 暖 GM 中位 ms | a908 暖 GM 中位 ms | 观察变化 | a908 暖 min–max ms | paired GM/PK 中位 | GM中位／PK中位 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| sphere | .09165 | .07415 | −19.094% | .0720–.1084 | .27684 | .27023 |
| planar | .74575 | .57300 | −23.165% | .5497–1.0075 | .73932 | .56936 |
| curved | 48.16765 | 49.63675 | +3.050% | 37.5140–54.5167 | 26.31834 | 26.32621 |
| NURBS | 486.25330 | 348.09230 | −28.413% | 304.3591–408.8690 | 222.39081 | 220.97591 |
| shortedge | 569.51715 | 610.75165 | +7.240% | 445.2589–768.7713 | 364.79717 | 366.11416 |

全部 cold／warm GM／PK、min／max、逐对倍率及 ratio-of-medians 从原 Capture 独算，分别保留在 `docs/projection-seed-independent/receipt.json`；不替换、合并、挑点或乘历史倍率。60 rep 的 PROFILE／WORK 及96行 ROOT_WORK逐原行／行号／字段与 log／Capture hash、实际 step／API0核实；F3 profile api-ms 只按原舍入精度比较。NURBS 原4179 Jac／20895 direction／12537直接XYZ、root150／8／winding18保持；这些外层数不含 projection 内部 Newton，不能推断没省工作，也不能签 CPU 因果贡献。

开发六对另保留：baseline 冷1070.1797、五暖中位431.9899；candidate 冷1027.7697、五暖中位324.5235，−24.877%，全部 GM／PK点另存。它不是正式 rep2，不给正式四暖增加样本资格。20 host load4.46–5.94；原 pmset 观察与 powermetrics 失败保留，没有数字温度、恒温或排他 CPU 证明。c931 的52份旧外部诊断逐 hash 核同，只用于路线，线程墙钟不当 CPU，也不进入候选计时。

20 window actual-loaded Native／Oracle、sealed PE/PDB 和 source manifest逐核，10440行 Git 字节 checksum，baseline／candidate Native各336 tracked。baseline MVID `0053c89a-0112-4d9d-a40d-493a193a508e`／DLL `13eae9e128cf2010ff7aee47cf3ce548eed060001312cd8b006e240e4642bcc6`；candidate `06d1f98c-869f-405d-9c36-23f5e2c0e7ad`／`9835b5a8b3d8a926a56c18b79eb6aee4f1cbc5011898c27058660168b5bbc2ac`。不与普通质量或 hooks bytes 互补身份缺口。

主900分量＝312V／588U，额外180＝84V／96U，按原1e-7／PK modulus公式独立重算，合1080＝396V／684U／0D。没有借 ab597 的新 norm 回填。

312实际 mesh／35,389,380B 与64最终 XT逐 source／step／tag／hash绑定，见 `docs/projection-seed-independent/material-bindings.json` 和收据。GM live、GM XT 经 PK snapshot、独立 PK reference分别保留；204份 proof有 source 绑定，24份 PK-product→GMreceive 控制无对应显示 mesh，12份缺 source 保持缺失。curved 原角色／provenance缺口不造；公共 seed／input不贴 rep2，64文件不等64个 rep2产品。NURBS 质量 rep5 与正式最终 rep2 分开。

原 report SHA256 `08e736d3887045ef18b2a24f2b8e071a0c4336fde45d6c998d80c3e094eae1ec`，material `87bc58b9d6e1f8e96434da61bc440cb6a5e4bd5e02ef76404c99defcc0325913`，独核一致。显示／来源绑定不额外授予几何、物性或性能资格。

## 原件保护、独验失误与可复核交付

两 ZIP 拒绝绝对路径、`.`／`..`、反斜杠、驱动器、重复成员、symlink及越界后隔离展开，并逐原清单成员 length／SHA核实：quality **20,526,861B／524成员／展开78,395,422B**，SHA `d8e1895ca86a1f29737e67e1fe2d568876452de1950ba6b23d2122d998666a09`；controlled **33,987,355B／1049成员／展开137,713,130B**，SHA `5b0adf8f1d8b76ed5e9ac80883de7efd5890417260885bf107fb9b4dfe20808a`。原件在 `temp_docs/incoming/`，展开及全部独验日志在 `temp_docs/independent-projection-seed-a9085ed/`；没有修改原包／原清单／原报告／测量／XT／日志。

作者两个 Info 字段误读 raw134、首 Pack 引旧工作文档 raw134、另存 fixed raw0，均保留并与 producer 分开；失败 Pack 在原 manifest／ZIP生成前。原汇总的环境变量拼写和工具 JSON截断 parse失败属于派生／取证过程，不视为新建模成功或失败，更不借此重跑正式窗。

独验自身原失败日志亦保留：controls-attempt1 raw134源于错误地要求抛物例必在原预算成功；attempt2 raw134源于错误地要求对应公开 GM range 必 raw0；改为真实记录原失败后 attempt3 raw0。receipt-attempt1 raw134误猜开发基线目录为 baseline 后缀，按真实原目录修正后 attempt2 raw0。上述均非作者新增失败，也未改生产容差／预算／期望。

独验私有 box 控制初版把最近 X距离写成 .75，检查偏弱；原 lower／upper 数值与收据保持原字节，另存 `bounds-verified.json`，按真实最近 X=.5 的精确 3/4 重验 raw0，最终源码再核 raw0。公开补充收据说明此修正，不用后来通过覆盖早先证据。脚本中的采样／解析点值控制均不冒充连续证明。

第一笔独验提交 `59d1731683a65d862969bb9646bbde4b73f2e5bf` 已实际推送并核远端同 SHA；最终报告及补充收据随后单独提交。最终分支为 `review/native-projection-seed-a9085ed`，提交／推送状态以最终交接的 full SHA 为准。本审查没有独立更改作者生产／测试期望、合 main 或上线。
