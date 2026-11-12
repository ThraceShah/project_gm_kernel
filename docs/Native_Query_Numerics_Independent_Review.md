# Native 查询与物性规范化集中独验

日期：2026-11-12。审查树 `gm-query-numerics-ab597-review`，分支 `review/native-query-numerics-ab597`。本次独验完成，未发现需要作者修复后再验的阻断项。接受下面明确列出的源码、数学合同及有限产品能力；**不签署 510 全点四方相等或一般曲面的物性目标全部通过**。不涉及看板 UI、main 集成、发布或 PK 性能目标。

| 模块 | 独验结论 | 接受边界和保留项 |
| --- | --- | --- |
| Native 一般查询修复 | Accepted，限本轮产品/既有 regular 能力 | 五种圆柱成品及其原生命周期证据；合法 100/200 尺度接触面拓扑修复。未扩大 Boolean 能力，不等于任意 cone/torus B-rep 已全面认证 |
| 510 原始观测及有限精度审计 | Source/evidence accepted；全点相等 Unproven | direct self 495V/15U、import 494V/16U；没有 D/API error。有限边界归属不能替代四方分类相等 |
| 当前物性标准与实现 | Accepted，限正 reference 尺度可认证的实体 | 有单位、整体量依据的规范化和保守包围正确；一般曲面的 `1e-7` 积分资格仍 Unproven |
| 固定源码/二进制/历史来源 | Accepted | 279 制品、698 固定 Git 源文件、12 PE/PDB/source 绑定；72 唯一原物性报告与固定原 ZIP 一致 |
| 本轮阻断项 | None | 未改作者实现、测试期望或原件；现存 U 和缺失历史列保持原状态 |

## 固定范围与实际准备

起点 `17eedbfd6be891db8c43494cd91a896d261242c4`，计算合同 `ab597e8a24b2ef23753507c8ca3e43d12edcc650`，证据工具 `0ef2305f5f75a8f810bb8212219adcc1744b882f`，交付 HEAD `f633b51db703df308cf4ac194e3c2b1661398d4d`。既有 Joint185 / Native116 接受边界不由本轮重新命名或扩张；没有使用其他 moving HEAD。

实际读取用户级 AGENTS、当前 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、起点到计算提交的全部生产/脚本/测试差异，以及后续交付工具和资料。三份项目约束/架构文件在此范围无差异。用户政策正文含尾换行 SHA 为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`；这只证明文件核对，不证明运行时自动加载。

使用已授权统一准备工具对本树实际执行 native ensure，返回 ENV_READY：Linux x64、PK 38.0.150、PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`。真实动态调用经项目 PskernelSharp + ParasolidScriptHost，没有手写 ABI、替换 oracle 或执行 NativeAOT。所有 dotnet 构建/测试及 file.cs 执行设置 `MSBUILDDISABLENODEREUSE=1`；本轮 testhost 已退出，未执行全局 pkill。

## Native 数学与消费者审查

Native 实际差异仅为 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBoundary.cs` 和 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularClassification.cs`。`classification:true` 仅给查询读取 Cone/Torus，其他调用仍用原 reader 能力。公共 BezierSurfaceBounds/BSplineProjection、C exports、结构体布局、XT 定义、参数化、操作预算和建模容差均未改。

ring torus 的所有点在以其原点为心、`R+r` 为半径的球内；对单位方向的正射线，用 `|p-o|+R+r` 覆盖所有正交点。现有 AnalyticLine 对 `R<=r` 明确 Unsupported，不把 horn/spindle torus 算进本次接受。cone 使用与 AnalyticLine 一致的原射线多项式 `a*t²+b*t+c`：`a!=0` 时的 `1+max(|b/a|,|c/a|)` 为 Cauchy 根界；线性退化用 `1+|c/b|`，非零常数无根，恒零多项式返回歧义错误。非有限根界失败，候选根仍经几何残差、单叶 cone、有限 trim、法向 crossing 和 winding 检查；切触、重复 hit 等进入既有最多六方向重试，不能直接当作无交。上界余量不改变实体的成员精度。

这是浮点解析求交与显式失败路径的审查，不是任意病态轴/近退化输入的形式化实数根完备性证明。方向由既有 ProfileNormalize 归一，解析轴由既有 surface preparation 提供；本次接受对象是有效、已封存的五种产品与原生命周期/合法变换。不能从本轮采样推导全部一般 cone/torus 的分类定理。

接触边反求的 UV 近邻只能提出 t。新增判断在**原有限 t 域内**求曲线空间点，并以 `1e-8` 验收距离；取消参数端部扩张，真实端点先由 vertex 阶段返回。空间拒绝后，SameSheetSupport 只排序候选；其精确支持定义关系不合并 XYZ 邻近实体，普通反求和完整有限 winding 仍须通过。拓扑返回来自原 source slot；region 由实际 face 的 front/back shell 所属 region 取得。

读取视图为 readonly ref struct；新增热路径为 stackalloc 与原 command scratch，finally 归还，没有模型写入、跨命令缓存或新增 rollback 路径。ReadOnly dispatcher 及失败时 unset enclosure/清零 topology 的外层消费者未改。内部可见性扩张服务于只读证据工具，不是新增公共 C API。

这里的空间阈值沿用当前 classifier 的固定 `1e-8`，与本次真实 PK 默认 session precision 相同；不宣称已实现任意可变 session precision 或更大 tolerant edge/vertex precision 的完整 PK 拓扑选择语义。原 15/16 点的精度分歧也没有因此被豁免。

## 固定点与有限边界证据

`docs/query-numerics-closure/results.json` 的 `fixed510Runs` 是四组各 **510** 行、共 2040 次观测，使用同一 510 点源；不是 501 行或 2040 个独立坐标。独立审计逐 CSV 列核对四 API 值、拓扑、GM_error、状态和原 XYZ 字符串，绑定实际 XT/hash，未让诊断覆盖原分类。

| 当前执行 | V/U/D | PK native 与 roundtrip 不同的行 |
| --- | --- | --- |
| self direct | 495/15/0 | 102、153、204、254、289、390、441 |
| import direct | 494/16/0 | 同上七行 |
| self Capture | 495/15/0 | 上述七行另加 339 |
| import Capture | 494/16/0 | 上述七行另加 339 |

两种 direct 各有 9 个 GM **有限边界**上界见证：50、102、153、204、254、339、390、441、493。审查发现这些证据均可由实际有限 plane 区域见证支撑；它们的有限 edge 距离仍为 Unresolved，不能改写成“9 条边距离已认证”。plane 参数是可求值的具体候选，完整 finite winding 通过后，用 interval 运算包围其实际空间点；这给边界距离上界，无需声称找到 whole-face 全局最近点。其余点保持跨阈值/缺失支持区间，未据不完整 edge 下界宣称整个 face 或 body 分离。

有限 edge 证据使用收到的原参数区间；没有显式范围或无法包围的曲线逐条记入 unresolvedIntervals。优先队列下界只在所有边区间覆盖完整时有意义；不完整时 lower 清零。点区间给上界，平方距离与 session precision 的 interval 平方比较。4096 单元预算没有增加；实际记录的处理单元均未越限。证书的 `faceGlobalMinimumCertified=false`、`fourWayAgreementChanged=false` 保持真实含义。

所有原始分类结束后才进入 native range / finite 诊断和控制点，未发现把精度审计提前预热原分类的改动。两种 direct 共 62 个半/双距离派生控制均相等；这是分开记录的正负控，不是替换原点的资格。

历史 self/import 日志的 15/16 条实际差异和历史 XT/hash 单列于 `historical510`。旧 self 其余 495 条原始四列没有落盘，仍然缺失；本轮重放不补造它们。原 imported 四列 CSV 与此前 imported 当前表另外保留。

## 物性合同、误差及适用前提

已实际使用 parasolid-research 和 MCP 核 `PK_TOPOL_eval_mass_props`、`PK_BODY_contains_vector`、`PK_TOPOL_range_vector`、`PK_SESSION_ask_precision` 原型；保留 deprecated contains_vector 入口。官方 Function Descriptions 31.1.1、31.4/31.4.1、31.12 明确 accuracy 是迭代工作指示，建议存储误差界；惯量是关于重心、按标准 Cartesian frame 表示。MCP token 查询未找到 `PK_mass_bound_modulus_c`，没有把空索引当作定义依据；枚举及默认选项由本机授权头文件和 PKToy 核对。

两边均由真实 PK 积分各自收到的 XT，actual 不是 GM 自有积分器。modulus 输出 amount[2]、mass[2]、cog[4]、inertia[10]、periphery[2] 共 20 double；重心三分量及惯量九分量分别共享一个模量。本轮哨兵及非等轴 block 动态控制确认各输出段和中心惯量 frame：2×4×6、M=48 时对角为 208/160/80；绕 Z 45° 后为 184/184/80、Ixy=Iyx=24，平移后 cog=(5,7,6)，未混入原点惯量。

当前合同的量纲和整体范数成立：volume/mass/area 分别除正 reference 总量；`|Δc|₂/Rg`，`Rg²=trace(I)/(2M)`；`||ΔI||F/trace(I)`。它在精确数学下具有长度单位一致性及刚体平移/旋转一致性。当前阈值仍是 `1e-7`，表达五个整体物理量的相对认证目标；这不是由 BODY_compare 自动推导出的几何误差预算，也不是应用普适精度标准。旧混合单位 `1+|a|` 规则没有生产 fallback。

分量区间的绝对差下界为不相交间隙、上界为最大端点差；平方求和、开方及正尺度相除向外舍入。由每分量盒得到的 Euclidean/Frobenius 最坏界不假设独立误差，允许共享误差全部同时达到模量。reference trace/M/Rg 的区间和分子存在相关性，按独立盒外包围只会保守；不能相消来签 V。近零非对角项也按整个 tensor 的 trace 尺度判断。旋转后 PK 模量盒可能变化，因此实际“可认证/未决”不承诺完全旋转不变。

认证规则为 upper<目标才 V，lower>=目标才 D，其余 U。零/无法证明正的尺度、非有限输入、subnormal 导致正界丢失、trace overflow 在当前 helper 中抛 InvalidOperationException，**不是普通返回 U，也不是 Different 的几何结论**；Compare 的观察层可记 U/error 后继续抛出，调用者可记执行失败。独立负控核实其 fail-closed 行为。本次所有接受的历史/实际产品 reference 尺度均成功建立；退化实体和任意极端浮点范围未获认证资格。norm 中溢出到无穷大的包围也不能作有限目标的 V 证明。

3/9 行分别重复一个整体 cog/inertia 判定。因此 15 行账是分量账，最多五个整体量判定，不是 15 次独立积分认证。

| 固定唯一原报告 | 原 V/U/D | 当前 V/U/D |
| --- | --- | --- |
| 主 60 份，900 分量 | 312/588/0 | 348/552/0 |
| readback 12 份，180 分量 | 84/96/0 | 144/36/0 |
| 全 72 份，只计一次，1080 分量 | 396/684/0 | 492/588/0 |

144 个 U→V；48 个 V→U 为 12 volume、12 mass、24 cog.z。72 份源 JSON 与 33,896,347B / 1038 成员的固定原 ZIP 逐成员 SHA 核对，并从原量重建 20-double 后复算所有当前 bounds/status；没有重写历史或把重判当作重新跑 PK。原 ZIP SHA 为 `5a22e9acae103d1b3b6fcfcb920c7eb13ae526d42fdf5181ac8e78db332436f1`。

八个合法同 XT/真实 PK imprint/单位/rigid 控制仍 U；三个真实尺寸负控 D，六个作者制造 norm 控制单列。identity 同字节 estimate-only 最大上界约 9.71e-16，含模量约 3.34e-7；partition 分别约 2.43e-10 / 3.77e-7，rigid 后含模量约 5.09e-7 / 5.73e-7。这显示关键缺口是 PK 积分包围和目前证据，不能推成 GM 几何已经坏，也不能用同一几何先验或区间相交代签目标精度。五种解析圆柱产品的 10 份当前物性记录均 V。

几何误差确可累积或相消。文档的正常图体积式需要全局一一对应、位移不越 reach 及曲率/面积控制；BODY_check/一般 BODY_compare 没有自动给出这些。对同密度实体，若已知对称差 D 及相对参考重心的半径 R，质量和 COM 上界分别可用 rho*D、rho*R*D/M_actual；关于参考重心的惯量差 Frobenius 范数由 sqrt(2)*rho*R²*D 控制，再加换到 actual COM 的 sqrt(2)*M_actual*|Δc|²。文档公式在这些前提下正确，当前没有对一般曲面实证全域 D/正常图，面积还需控制法向/切向变化。不能仅按 face 数乘 tolerance 或抬阈值来补上这项证明。

## 独立执行及原始结果边界

| 本树实际执行 | 原始退出码 | 验收内容 |
| --- | ---: | --- |
| ReviewNativeQueryEvidence evidence-v3 | 0 | 46,915 项核对：279 path/size/SHA、698 固定 Git 源、12 PE/PDB/source、原 exit/TRX、全部当前 CSV/XYZ、72 原 ZIP 成员及旧新账 |
| ReviewNativeQueryMath math-native-final2 | 0 | 72 份新判定逐字段重算；18 个制造 norm/单位/共享误差控制及 5 类硬失败控制；真实 PK frame；错误半径/finite trim 拒绝；原五产品 240 个离阈值 GM/独立 PK 查询及 strict compare |
| 原六个新增测试定向执行 | 0 | 6/6，0 fail/skip；函数目录 175、分配 guard 3072 CLEAN |
| NativeQueryReplay independent-fixed20 | 0 | 原五产品/原 20 XYZ，同字节四方 20/20；真实 receive/full check/strict 保留 |

数学 receipt 的 checks=5681 是记录 mapped-library 单例检查前的计数；最终日志为 5682，差一为最后的库清点断言，不是遗漏一个数学控制。实际映射 PK 库 91,444,112B，SHA `fc9ba50b40d7e9ee05e02f28c2a29e6ab75b9c91e6b6ba5ea588cf54149b9c52`，与作者封存映射一致。

保留独验工具自身的开发失败：第一次 evidence 程序把原 probe CSV 的 XYZ 列偏移读错，exit 134；修正审计脚本后 v2/v3 成功。扩充数学负控后一次错误地重复删除已随 wire 生命周期消失的 carrier，exit 134 / PK_ERROR_not_a_tag；修正本树审计清理代码后 final2 成功。没有据这些审计工具失败更改作者实现或测试期望，也没有将失败原始执行补签成功。

作者原完整 2025/2025、原 exit 0 的 log/TRX 和 3072 CLEAN 已只读核对，本树未重跑全套、63/17/510/family/ABBA。五 producer 各 kernel/oracle 十份，加套件两份，共 12 绑定；每 kernel 335 tracked 文档、每 oracle 六文档，套件 tests 166。实际 PE MVID、CodeView/portable PDB GUID、每 tracked checksum 与固定 Git 核对；套件只接受封存输出身份，不补造运行时 loader 观察。

原 Capture 285 pass/0 missing 的 axial 可独立接受；self/import/contact 原 exit 2 仍是带未决条件的执行。negative 原 exit 0 及 Capture 15 fail 是成功捕获错误尺寸/半径/trim 的反例，不是正向产品通过，也不反签成建模失败。没有改原 exit、原观测、原 XT、程序集或历史账。

## 可交付数据入口

当前计算 source 名为 `ab597e8a24b2ef23753507c8ca3e43d12edcc650`；norm 名为 `relative-volume-mass-area; cog-Euclidean/radius-of-gyration; inertia-Frobenius/trace; reference-scales-with-outward-bounds`。可按以下已有机器格式读取，毋需让旧格式成为生产 adapter：

- `docs/query-numerics-closure/results.json`：`fixedOriginal20`、`contactQueries`、`fixed510Runs[].classifications` 提供实际完整查询表；`finiteBoundaryEvidence` 只提供其明示证书；`historical510` 分列历史差异及缺失。
- 同文件 `executions[].actualStrictCompareReports`、`actualMassReports`、`loadedIdentity` 绑定操作模型、质量、XT hash 和实际生产者；原退出码在 `executions[].rawExit`。模型和私有 PE/PDB 的实际路径、长度、SHA 在 `docs/query-numerics-closure/artifact-manifest.json`，相对来源树为 `../gm-native-query-numerics-closure`；越级路径只白名单解析到 `../gm-pk-native-delivery`。
- 同文件 `jointMassStandardsReassessment.reports` 的 `oldProperties/current` 是 72 份完整旧新分量账，`currentNativeMassControls` 区分真实 PK 和制造控制。原 Joint 报告/ZIP 只读来源树为 `../gm-joint-1850534-review`。
- `docs/query-numerics-independent/receipt.json` 绑定本树最终审计 receipt、脚本、日志/TRX 和固定交付文件 hash；本树运行原件在 `temp_docs/native-query-independent`，原 20 点新重放在 `temp_docs/native-query-replay/independent-fixed20`。只复制必要原产品/原 log，未批量搬运私有库或无关原件。

本 review 的提交只包含独验文档、两份 C# 审计脚本和小型派生 receipt。结论是本次 Native/数学关键独验结束，已接受模块和未证明目标分别保留；没有 main、上线、其他平台性能或全部物性目标完成声明。
