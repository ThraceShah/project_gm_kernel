# PK 整链结构对齐：首个原生 contact 数据流改动

原任务继续。当前研究/计算基线固定为 CCC `271af03cfefedef12eea19337f1b2b3bca9fe96b`，不是累计性能组合4026，也不含 q2 的 d511 Runtime 改动。实际读本树 AGENTS、两架构与干净起点；私有环境实际 ENV_READY。本轮无 C ABI 改动、NativeAOT、Windows、门户或新的任务范围。

累计组合的独验 `611b56c88a34685f6c50aabebe506cf20d0bc734:docs/Accepted_Optimization_Integration_Independent_Review.md` 已 fetch 并完整读取：3aff/4026 的限定 Native/消费者/原几何拓扑生命周期接受与描述性窗口接受成立，数值与完整性能仍 blocked。本研究不倒填该接受或来源。q2 固定 `d5112b6a845a67bed5300f97ffef9d2c59ca373b` 的实际 AGENTS、说明与生产 diff 已读；其 basic tracking、edge aliases 和来源发布不在本改动内，不 cherry-pick 移动源。

## 已核查的整链及未知处

|阶段|PK 实际证据|本树 GM 数据流及剩余问题|
|---|---|---|
|选择/候选|Mac 两个原夹具实际命中 fix_blends、BLE_for_all_blended_edges、BLE_fix|FixLocalBlendsCore 按真实 marked edge、邻接及 provider 条件选择网络/regular 路径；不由符号名推全部筛选算法|
|原生几何/数据|FD18.2.2、18.3.2.6 的支撑、半径、spine/extent 定义；保留已核动态 native 参数约束|CreateGeneralRollingBlend 实际 clone/offset/compile，联合追踪有真实 B-spline UV；旧追踪仅保存 XYZ，随后丢弃 UV 而逆求|
|SSI/追踪|Mac plane/NURBS 的栈实际为 BLE__fix_prepare→get_all_bdries→CNS_blended_edge→ISS_intersect_at_pvec→ISS__phase2/MAR_explore；NURBS/NURBS 的 MAR 栈同属 Fix|GM 使用原联合 Project、CHART、有限终止与整段证明；本次传递实际 solver UV，不另造曲线或参数化|
|有限根/trim/material|MCP 对 ISS_intersect_at_pvec 实际返回27个直接依赖；CNS_blended_edge、BLE__get_blended_edge 空图仍未知。原生 Boolean 入口实际命中，但本次单次断点不足以覆盖其内部 SSI/材料全链|四条有限 contact/end carrier 经事件、Trim、ArrangeFiniteReplacementFace；Boolean 经 SSI/arrangement/material。RegularRayWinding 的曲面候选只用投影上界，横向 ray/AABB 筛选缺口待单独实现/核实|
|拓扑发布|Mac 实际命中 BLE__fix_modify，未据此推测其全部修改/共享实现|AssembleFiniteRegularBlend 读取并安排全部原 faces，CloneRegularBoundaryBody、ReplaceLocalBody；是否为主要成本尚未证明。q2 正负责真实来源、tracking/tag/precision，避免双写|
|原生求值/nearest/reuse|原 PK 夹具在 Fix 后实际评估/导出有限边；内部求值复用策略未由本次断点证明|持久 compiled program、实际 native source/generation/sense 与借用仍保留；联合最近点/站立/Hessian、完整 interval Jacobian/Krawczyk/CHART/finite 门继续执行|
|失效/生命周期|本次 PK Fix 后 Boolean、BODY_check、原生 XT 真实执行；不能由此覆盖全部失效分支|新数据仅一次构造的 scratch/stack，返回即释放；跨类 oracle 实际执行 fresh receive/独立 PK compare，plane/NURBS 还执行既有 copy/删源/mirror-scale/reload/cleanup|

Mac LLDB 栈、命令、实际 image/库 SHA 与退出在 `temp_docs/system-chain/pk-dynamic-stacks-{plane-nurbs,nurbs-nurbs}/`。两个 debugger0、两个 inferior0 分开。NURBS/NURBS 首个 ISS__phase2 断点在输入 BODY_check，不签成 Fix 的 phase2；其 MAR 栈才有实际 Fix 调用者。未命中、一次性断点已删除、间接调用和空图均不能证明不存在。调试器内的 API 时间不是性能测量；未使用 Linux 地址调试 Mac。

## 实际通用改动及复用资格

唯一生产文件为 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedBlendConstruction.cs`。原联合 Project 已求得 point 与 B-spline 两侧 native UV，旧代码在存 trace 时只保 XYZ；反向 trace 前又 RollingBlendSample(initial)，ChartSpine 未传 knownUv，再经 DistanceFromSeed/Distance 找触点。现在将真实 UV 与 point 同行保存、反序拼接并传入原 ChartSpine/ChartSupportWitness，反向起点恢复原初始 Project 的 contacts。

初始、每个成功前进点与 ProjectBoundary 的 witness 来自同一次成功 joint solve；闭合终点复制真正初始 point 的 UV，而非近闭合最后一步临时 contacts。已有联合残差、stationarity/局部 Hessian/nearest 分支检查仍执行。ChartSupportWitness 随后针对实际 offset source tag/sense/native UV 作原 forward error 和 normal 检查；其自身不证明 nearest。原先 ChartSupportDistance 在 seeded 成功时也使用局部 refine，并非每个 CHART 点都带独立全域最近点证书。本次保留后续有限 trace、整段 interval/CHART/nearest/终止/发布失败门，不从 knownUv 接口存在推得复用资格，也不删除公开距离/未知触点路径的全域投影。

只将实际作为 solver unknown 的直接 B-spline UV 传为 known。analytic 或 nested BLEND 支撑一侧填 NaN 供既有非 known 路径消费，不把初始化0或另种 section 参数冒充 native UV。未改 B-surface 系数/knots/正权、BLEND/ICurve/CHART 定义、native-v、拥有关系、容差、预算或持久格式；没有拟合几何、全局 cache、数值相同即同 owner 的推断。

存在直接 B-spline 支撑时增加两个 `4×512` double 的 command scratch，合计32768B；512点、2048访问和全部 solver 限额不变，未增加 arena 配额。UV 在借用 program 数值 workspace 前预留，避免 span 重叠；外层 checkpoint 统一归还。额外初始 contacts 为4 doubles stack。非 B-spline trace 不借 UV workspace；原热单边零托管分配检查通过。此资源说明不等于任意平台栈/arena余量证明。

## 针对机制的验证与原失败

`tests/KernelTests/RollingBlendContinuationTests.cs` 延用三种 bulge/radius 的自然域外延/双向 trace：实际每行 spline UV 必须通过原 witness 检查，错误 UV 与错误 offset 层必须失败。另增精确二次碗/平面的闭合 trace，检查首尾 XYZ/UV 同行及 Help limits、删源释放。

- 首轮受影响集合：25 pass、3 fail、原 test1。三处均为新增检查在数值 workspace 借用结束后运行，实际 WorkspaceTooSmall；原 TRX/日志/source diff 保留。不是生产失败被重签成功。
- 测试改为在真实 command 范围使用既有 Ensure/Release 规则后，只窄复验三组：3/3、raw0、697ms；生产未为此增加 workspace 配额或改实现。
- 闭合控制另1/1、raw0、487ms。以上不是一次29项或完整 suite 执行。
- 未重跑旧完整 suite、八 family、正式 ABBA/五类窗口。原25项中含实际零分配、失败回滚、contact/有限拓扑与生命周期控制；其原始通过保留。

原始证据在 `temp_docs/system-chain/{affected-tests,affected-tests-fixed,closed-witness}*`。首轮失败的 test assembly 未及时单独封存，缺失如实保留；未重建冒充原二进制。后续测试制品分别封存。首失败 source diff、TRX、原退出仍在。

另保留调试准备错误：第一次 MSBuild property query0只返回目标路径；显式 build 的一次工具60秒超时没有原始最终退出；后续完整 Build0。首动态 driver134 在 model 前因 apphost 缺失退出，下一 LLDB1 在 run-args 解析前退出；随后两个 native 执行成功但重复-o只保留continue，仅有命中证据。最后改自动继续+单条 backtrace 才取得上述栈；不回填早期记录。

## 跨类完整操作观察

两个 source 各一个进程，四类各两次完整操作。先基线后候选，非正式窗口；各 case repetition0 不统称冷，所有点及真实 PK 时间在 `docs/native-operation-alignment/uv-cross-class-observations.json`。下表仅各类第二次执行，无统计/因果/CPU资格。

|类别|CCC Fix ms|传 UV Fix ms|CCC Boolean ms|传 UV Boolean ms|
|---|---:|---:|---:|---:|
|plane/NURBS|438.368|315.135|243.285|245.364|
|depressed roof|139.310|117.556|252.320|241.157|
|NURBS/NURBS|1816.040|1731.614|6520.387|6661.220|
|concave NURBS/NURBS|7337.254|7328.803|23401.888|23576.786|

两侧均 PASS all8、无 FAIL，Fix 与后续 Boolean 的64条显式计时 API 返回均0；各自 producer2 的物性 Unresolved 保留，driver0不等于数值通过。每次按原 oracle 在真实 PK 独立建模、GM fresh XT receive、BODY_check/原 strict compare/9 section 接触与切向检查；section 有限点不升级为连续证明。plane/NURBS 另有既有生命周期消费者。未启用 Capture，因此没有本轮 Capture loader/mesh/proof 显示证据，不能借历史 Capture 补齐。

实际源/二进制执行绑定见 `temp_docs/system-chain/cross-class-executions.json`。CCC Native DLL SHA256 `3c281fa6ddad43ed5943630687b911edf7dbe6dea6262ac173142e90f064008a`，传 UV 为 `27277c0ea3d7a95d0efd54b16b001b1bb1b88aff7de4623e2b0fb44a6d833fc6`。baseline managed binaries 在 source 编辑前封存；两个 oracle DLL/PDB 随实际引用构建，helpers 源未改，不要求跨引用 DLL 逐位同。

结果只说明解决了一类直接 B-spline contact 重求。plane/NURBS 有明显开发观察差别，但 NURBS/NURBS、凹类特别是 Boolean 的巨大差距仍在；不能把此改动作为整个百倍差距的解释或目标完成。生产/测试改动尚未独立接受，无 main 集成、上线或完整数值/性能资格。原79920继续从整条候选→SSI/根→材料→发布→求值链消除结构性工作，不自动进入下一微热点→完整套件→旧窗口循环。
