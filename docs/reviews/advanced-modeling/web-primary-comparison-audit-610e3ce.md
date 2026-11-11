# 看台第4/5问：主比较覆盖与数据时效只读审计

基线 `610e3ce844bf99c85af6069905f9f3deaab08133`，分支 `docs/web-primary-comparison-audit`。本次只交付文档、原始证据摘录和 BCL 读取工具；未改 kernel、acceptance、原 data、web/src、index、门户、live UI 或 timer。以下“当前数据”专指派发指定的只读 bundle，不把工作分支状态推断为线上内核状态。

结论：该 case 确实使用真实 `PK_DEBUG_BODY_compare` 做主要几何/结构比较，并非29项都只是手工计数；也不能说29项都能被 compare 代替。它的原始 local 结果有差异，12条均为官方允许诊断的 chart-point 数量差异，共享 helper 在该白名单和原合同下通过。数据采自610基线的一次历史冷样本，UI424更新不会使它成为最新候选的正确性或性能结果。

## 1. 固定证据入口

所有文件系统路径均相对于项目根目录。外部只读数据根为 `../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/bundle/data`；下文 data 路径相对于该根，run路径为 `runs/20261110T195008Z-610e3ce8`，sample路径为 `cases/support-chain.asymmetric43-base/sample-0`。

| 对象 | 准确入口/字段 |
| --- | --- |
| 主要原始比较 | run下的 `cases/support-chain.asymmetric43-base/sample-0/evidence/compare-0.json` |
| 原始28行 | 同sample的 `capture.json`，`checks`；原始 `evidence` 为null |
| 最终29行 | run的 `manifest.json` → `cases[id=support-chain.asymmetric43-base].checks`；采集器补上oracle-exit并填入capture/log路径 |
| API、参数、时间 | capture的 `steps[0].gm/pk.apiParameters`、`timings`、`sessionPrecision` |
| 原进程结果 | sample的 `oracle.log`、`execution.json`，manifest的 `execution.exitCodes=[0]` |
| 源码 | `scripts/SupportBlendChainOracle.cs`、`scripts/WebProgressCapture.cs`、`scripts/ParasolidBodyCompare.cs`，revision均为610 |
| 本次供UI的投影 | `docs/reviews/advanced-modeling/web-primary-evidence-audit-610e3ce.json`，带原检查值、原证据路径、来源指纹和六组映射 |

源码SHA256与manifest记录逐项一致：SupportBlendChain `9dea35ad1d4b5a85a8198e1797e4723c6df92e10e47b8641f7d9a5f3265f6c6b`；Capture `8b705c1a0e3b79b981785c4759d22858bd3096bc1feb9b544a0000a5b3bafb75`；共享helper `38fdcac891ba99221bb778af8848761daf1b847d74db0bf6de12d714c290635e`。

## 2. 实际调用路径及有效配置

`SupportBlendChainOracle.cs:26–75` 以真实PK构造并imprint原始roof，从同一输入XT导入GM，两边选择跨seam的两条边，半径0.1，独立mark后调用 `PK_BODY_fix_blends` / `KernelRuntime.BodyFixBlends`，tolerance为 `1e-7`、local_check开启。`Fix:163–173` 的 capture Before/After 保存这一操作；capture的真实PK接收GM新XT，与独立PK产物比较。`WebProgressCapture.Compare:172–205` 先做结果数量、双侧BODY_check及拓扑计数，再保存raw compare并调用共享 `AssertEquivalent`。**raw compare文件和形成equivalence pass的helper是两次调用**，当前配置相同；不把raw调用返回0当pass。

同一个oracle稍后还有独立的自身 `Compare:295`，它显式列出七个true flag，另查region语义和物性；普通base路径不会在这里额外调用共享helper（remote-bore/reversed-chart才调用）。原 `SupportBlendChainOracle.Compare` 的 `structuralOk` 只要求global无差异和枚举出的非chart local差异为0，没有像共享helper一样要求local状态与条目一致。截图这份带capture的base结果仍有capture共享helper补充，不能把这一事实推广成所有不带capture的旧oracle都具有相同门。

| 字段 | SDK构造默认 | capture/helper有效值 | 含义与边界 |
| --- | --- | --- | --- |
| o_t_version | 1 | 1 | 当前选项结构版本 |
| max_diffs | 0 | 1024 | 0仅global；正值启用匹配面的local分析，上限限制被判不同的面配对数，不是距离容差 |
| all_tests | true | false | 使用各子项选择；并非关闭所有比较 |
| num_tests | true | true | 数量类local比较；global有自己的数量结果 |
| acc_dev_tests | true | false | accurate deviation这组未启用，不能声称“所有精确偏差测试已做” |
| tol_dev_tests | false | true | 以session precision为准的偏差比较；本sample precision `1e-8` |
| tol_tests | true | true | 比较实体容差，与上述几何偏差门是不同检查 |
| class_tests | true | true | 类型比较 |
| sense_tests | true | true | face/surface/curve sense比较 |
| missing_tests | true | true | missing实体比较 |
| non_match_tests | true | true | 保留不匹配实体差异；官方建议保留 |

accurate/tolerant两组用于偏差比较，差异类包括surface、curve和vertex deviation。官方选项说明明确了tolerant以session precision为界；这里没有证据支持进一步宣称其采样策略、极值算法、位级相等、任意曲面完备性或accurate组的某个人造固定阈值。`tol_tests=true`也不能抵消关闭几何偏差组的影响；本配置已有 `tol_dev_tests=true`，没有两组都关闭。

**依据链**：实际调用了parasolid-research的MCP `get_function_detail(PK_DEBUG_BODY_compare)`，返回 `found=true/type=LEGACY_KERNEL_ROUTINE` 和generic说明，**未给C原型**，因此没有把它用作签名证明。签名改由v38 SDK `third_party/parasolid/include/parasolid_debug.h` 与固定PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9` 的 `PskernelSharp/parasolid.g.cs:8025` 相互核对；都是两个body tag、options指针和results指针，返回error code。默认值由SDK的 `PK_DEBUG_BODY_compare_o_m` 和 `PskernelSharp/Types.g.cs:21255–21285` 的显式无参构造器核对，再实际构造和调用验证。C# `new T {…}` 调用该构造器，省略的true项不会变成false；不要换成零初始化 `default(T)` 冒充相同配置。

官方FD **106.7 Comparing bodies** 的实际HTML文件名是 `../../../../code/Parasolid_Installer_v380_rv1000/online_docs/chapters/fd_chap.107.html`，不是文件名106；同目录上级的 `headers/pk_debug_body_compare_o_t.html`、`pk_debug_body_compare_r_t.html`、`pk_debug_global_diffs_r_t.html` 和 `pk_debug_diff_t.html` 支持上述解释。Linux静态地址不用于跨平台原型或动态覆盖证明；未提交厂商库、schema、反编译文本或大段官方正文。

## 3. 这7对/12条/6项到底表示什么

| 原始字段 | 值 | 应显示的解释 |
| --- | --- | --- |
| globalResult | 21921 / `PK_DEBUG_global_res_no_diffs_c` | global无差异 |
| localResult | 21930 / `PK_DEBUG_local_res_diffs_c` | raw local有差异，不能改成raw无差异 |
| facePairs | 7 | 返回的不同面配对记录数；不是所有已匹配面数，更不能说其余6面未比较 |
| localDiffs.length | 12 | 全为21949 / `PK_DEBUG_diff_n_cht_pts_c`，按唯一白名单诊断 |
| 非白名单local条目 | 0 | 这是对已返回条目的计数，不是独立能力数量 |
| globalDiffs.length | 6 | 数量比较记录数，不是6项失败；本文件每项masters/similars相等 |

六条global记录为shells `1/1`、faces `13/13`、accurate edges `28/28`、tolerant edges `0/0`、accurate vertices `17/17`、tolerant vertices `0/0`。它们保留相等记录；按global结果和逐条语义解释，不能按数组长度报错。独立ask计数为regions `2/2`、shells `2/2`、faces `13/13`、edges `28/28`、vertices `17/17`。**raw compare shell字段为1，而BODY_ask_shells为2，保留各自原值和查询名称**；官方简短说明不足以在本审计确定内部筛选口径，不用未经核实的void/material猜测覆盖原计数。因此“重复覆盖”允许UI归组折叠，不意味着任意计数字段可互换或删除。

FD106.7明确chart points不改变曲线形状，故仅21949诊断有直接依据。该结果并没有把surface/curve/vertex偏差、类型、sense、容差、missing或匹配差异一并豁免。共享helper还独立检查body type、regions数，以及每个region的material flag与shell数形成的multiset；这不是完整拓扑同构证明。

FD解释：local分析直到找到 `max_diffs` 个不同面配对或所有面配对分析完。返回7小于1024，而且两body各13面，所以本case**没有触达此截断上限**；不能因此声称raw文件列举了全部13个成功匹配面。未来若返回配对数触达max，UI应标“可能截断/完整性未确认”，尤其不能仅因返回条目恰好全在诊断白名单就宣称没有后续阻断差异。当前helper没有独立的cap命中防护；这是未来大模型合同的完整性风险，**本次没有证明当前13面case漏判**，也没有构造昂贵大模型来臆证。需要验证该风险时应先用有限结果合同fixture覆盖“前段chart、后段非白名单、截断”状态，不改生产白名单/阈值来求绿。

## 4. 29行映射：主比较置顶，原检查按义务保留

| UI组 / 原行数 | 原check ID | 与主比较的关系、保留原因 |
| --- | --- | --- |
| 主比较 / 1 | `0-equivalence` | 主卡打开 `evidence/compare-0.json`，显示API、有效mask、global/local语义、12诊断及唯一白名单；原equivalence门还包括type/region语义 |
| 拓扑计数 / 5 | `0-count-0..4` | shell/face/edge/vertex与compare数量覆盖重叠，regions与helper重叠；可折叠，原查询口径和所有原值保留 |
| 操作返回与结果集 / 5 | `gm-0-api`、`pk-0-api`、两条 `*-blend-fault`、`0-body-count` | error=0、fault=18391、结果集1/1是操作合同；单个body compare不说明API返回、错误行为或结果集对应关系 |
| 实体有效性 / 2 | `0-True-body-check`、`0-False-body-check` | 双侧full BODY_check各0fault；相似的两个body仍可能同样无效，不能用compare替代 |
| 独立物性 / 15 | `0-mass-0..14` | volume/mass/centroid/inertia/area原数值；`abs(GM-PK)/(1+abs(PK)) < 1e-7`、accuracy `.99999999`继续原样保存，compare不返回这些15值 |
| 原场景出口 / 1 | `oracle-exit` | 原进程退出0保留；该oracle后续还有reload/cut及其自身比较，capture中只有Fix一步；出口不能替代逐操作owner |

合计capture28加原oracle-exit共29。它是**29条原始检查记录**，不是29个独立功能或29项几何能力。部分物性值受对称性重复、计数也有交叉，均不足以证明能够删除它们的原验收合同。不要把“全部capture/check证据都指capture文件”当成更直观的主比较入口；在呈现层增加raw compare链接、原check ID定位和源码链接即可，原source/evidence仍保持可访问。

另有不在本case29行内、必须按适用操作保留的合同：rollback/资源/错误路径验证的是状态恢复；wire没有面，raw BODY_compare不检测局部线形，须由共享helper有限边类型、sense、精度、端点、trim和双向65点距离辅助，并保留参数/导数/有限域API语义检查。采样不能证明任意曲线完全等价。有限root/chart、分支完整性、consumer身份或持久性、分类兼容也不能由body输出在容差内相似自动推出。

建议主卡文字：**“真实PK主比较：global无差异；local 12条chart-point诊断；原严格门通过（仅n_cht_pts诊断）”**。其下并列原始global/local枚举、7返回差异面配对、mask和session precision。`Before`只是当前查看网格；验收发生在Fix输出新XT上，应标“检查对象：该操作After/fresh XT”，不把Before预览当正在被比较的输出。默认可折叠辅助组；任何原fail/missing/timeout/unsupported始终展开并显示原值/阈值/证据，不改原失败。

## 5. 配置判断与有限真实PK控制

本次没有发现把local全关掉或将同拓扑错误尺寸接受为等价的当前配置缺陷。现有配置对“session precision范围内的几何与结构兼容、chart点表示可不同”有官方依据和有限负控；不据此宣称所有flag选择在所有API/模型上均合理。推荐今后**显式写全当前有效值**以固定构造依赖，并对cap命中/未知结果单独报告完整性；这是建议，不是本轮生产mask变更。没有依据把 `all_tests=true` 视为无条件更正确：它会改变子项选择并启用accurate偏差组，需另立目标合同及表示变化正例验证。也没有理由关闭tol_dev、non_match或扩大诊断白名单。

### 可消除的真正重复调用：仅capture内同一次操作的这一对

已经逐源码确认，不只是两个flag列表相似：`WebProgressCapture.Compare` 将 `AfterBodies[(true,ordinal)][0]` 保存到同一局部变量 `received`，将 `AfterBodies[(false,ordinal)][0]` 保存到同一局部变量 `reference`。先用这两个变量调用raw compare；写JSON、释放raw result后，原样传给 `AssertEquivalent(reference,received,...)`；后者只先询问type及region/material/shell信息，再用同一对tag调用compare。两处都用同一固定PskernelSharp显式构造器，显式覆盖字段相同，故完整有效mask相同。两次调用之间没有body修改、session precision设置或进入下一次操作。第二次调用发生在同一个post-operation/fresh-XT阶段。因此**此处是实际可合并的重复几何比较工作**；不能据此推算节省多少毫秒，也不能把两次比较耗时计入现有APIratio。

下一最小代码任务可只让Capture复用现有raw result：把共享helper现有的“type/regions检查、raw结果global/local/白名单判定、适用wire辅助”拆出可消费借用结果的同一校验路径；Capture仍先调用一次PK并按原格式写raw JSON，再把仍有效的结果传给这条完整路径，按原异常处理生成原 `0-equivalence` 行。原独立 `AssertEquivalent` 入口仍可自行做一次比较并调用相同判定，不让其它oracle调用者丢门。两条路径的mask必须机械保持一致；若将来不同，拒绝把某一mask结果冒充另一合同。只在当前函数作用域借用结果，不缓存tag、body或result跨操作，也不返回 `r_f` 后悬空的native指针。

由一个明确owner在 `finally` 释放 `PK_DEBUG_BODY_compare_r_f` 一次；证据序列化与结果判定均在结果有效期内完成，释放失败也保留为错误。共享判定继续逐条处理全部global/local结果和仅n_cht_pts白名单，保留当前成功/失败条件、日志的face/entity/diff信息；type/regions/适用wire检查不能因复用raw结果而跳过。BODY_check、原15物性、API/fault/body-set及原场景出口完全保留。若type/regions或判定失败，已写出的raw证据仍须存在；不能把检查前移后导致失败case再无比较证据。结构化输出可直接由同一借用result同时产生raw JSON和判定摘要，无需再从展示文字反向解析结果或引入通用全局缓存。

验收该后续小改时，只需证明同一pair/阶段的PK调用数由2变1、原raw字段与检查行一致、相等正例/同拓扑错尺寸负例/已有chart诊断样本仍按原规则判定，并对API错误和释放生命周期作有限检查；已有wire负控和物性等原门继续原样适用。**这只是只读建议，本轮没有改Capture/helper或执行假定实现的验收。** `SupportBlendChainOracle` 自身的compare在capture之后，后面又有reload/cut后的另一份body；阶段、tag或输入不同，不能按“看见同名API”一概删除。对其是否复用必须另核身份、输入、mask和所需语义，不能跨原模型生命周期缓存本次结果。

先在本worktree执行统一privateenv ensure，收到 `ENV_READY profile=native pk=38.0 platform=linux-x64` 后才动态；没有改全局配置或credentials。临时控制使用既有PskernelSharp、Host及共享helper，不手写P/Invoke、不加载GM程序集。额外environment-only调用 `PK_SESSION_ask_kernel_version` 实际返回 `38.0.150`，与历史manifest所用版本一致，记录于 `docs/reviews/advanced-modeling/web-primary-evidence/compare-environment.log`。

| 控制 | 新执行/复用证据 | 观测 |
| --- | --- | --- |
| 默认值/有效mask | 本轮真实构造 | version1；默认max0/alltrue/accuratetrue/tolerantfalse；helper max1024/allfalse/accuratefalse/toleranttrue，其余true |
| 相等block正例 | 本轮，1×1×1对独立1×1×1 | global21921/local21931，返回0条面配对，helper通过；再次证明0不意味着没比较 |
| 同拓扑错高度负例 | 本轮，1×1×1对1×1×1.01 | 默认max0漏掉几何差异；当前helper global21921/local21930、5返回配对，surface/curve/vertex deviation被拒绝 |
| wire错trim/平移 | 复用56825d6，不重跑 | raw无local差异，共享有限wire辅助分别拒绝终点/起点错误 |
| 另一独立block负控 | 复用830e7cd对04c7ed0的独立审查，不重跑 | 2×2×2正例通过、2.001×2×2拒绝；不等同Mac全样本重跑 |

本轮新控制摘录在 `docs/reviews/advanced-modeling/web-primary-evidence/compare-control.log`；自有临时源码保存在同目录 `BodyComparePresentationAudit.cs.txt`（复现时放到 `scripts/BodyComparePresentationAudit.cs`，项目路径按该位置解释），不纳入生产脚本。第一次尝试只因重定向目录尚不存在而未启动dotnet；修复目录后控制退出0；随后仅修正诊断布尔值打印后重复同一廉价控制退出0，没有增加模型种类。额外environment-only查询不构造模型。编译产生既有NuGet/绑定警告，没有执行AOT发布、全量test/19/96/347/bench或性能样本；无需清理未启动的testhost，未终止其他worker进程。

既有证据准确来源：`56825d6` 的 `docs/reviews/advanced-modeling/operation-acceptance-audit-evidence.md`、`scripts/BodyCompareAuditControl.cs`（原控制日志hash `8a31e7f7c0e9ecc0483e1d890c840f0cb770f167d5583de739159c86d62575b2`）；`830e7cd` 的 `docs/reviews/advanced-modeling/modeling-performance-independent-review-04c7ed0.md`（原strict-mask日志hash `57b7341a8f5ecce692f0a1381f2b40075e2b6f2974ea55ff446e9132052bdf36`）。这些是已有报告证据，未把它们改称本轮新执行。

## 6. 数据新旧清单与准确时间

| 层 | 固定事实 | 可说/不能推导 |
| --- | --- | --- |
| UI | 派发给定UI424已经上线；本任务未操作live | UI发布独立于采集，本轮未重新核验部署SHA |
| bundle最新run | `20261110T195008Z-610e3ce8`；commit610；clean source | 该bundle中的最新run，不是候选工作分支的新采集 |
| run.generatedAt | `2026-11-10T19:50:08.4502366Z` | UTC+08/SGT为11月11日03:50:08.4502366；是采集开始时间 |
| run.finishedAt | `2026-11-10T19:59:58.6901827Z` | UTC+08为03:59:58；采集总590.1497805秒 |
| index.generatedAt | `2026-11-10T20:00:06.6856928Z` | UTC+08为04:00:06；index写入时间，不能冒充新采集 |
| scope | registered463、attempted17 | 原分布pass14/fail3/missing330/unsupported116；不是463项均已执行或通过 |
| 环境 | PK38.0.150、Release managed、.NET10.0.10、Linux X64、TieredCompilation=0、CPUQuota200%、serial PK | 原manifest声明；不是无配额稳态测量 |
| 截图case | GM2881.9895ms / PK4.4714ms = 644.538511428188x，n=1、warmup0 | 历史单冷样本，API-only；没有包含导出/比较/mesh/I/O，不能据此认定最新优化速度 |
| 数学候选 | a5ab85d，独立review7c1606e | 接受固定39个有限cell、唯一足点及39连接的数学层；不接受owner/live generation/P/S inverse/runtime发布或Boolean已完成；未采进本run |
| guard候选 | 7aca6f0，独立review957ce708 | 固定空读集guard源码正确性/有限控制接受；未测速，无CPU收益、Mac执行、完整建模或部署结论；未采进本run |

上述候选范围直接核对固定review文档 `topology-event-framework-shared-plane-coverage-independent-review-a5.md@7c1606e`、`cache-empty-readset-7aca-independent-review.md@957ce708`；它们与610的已发布基线分栏。没有据当前日志缺省猜测“正在修哪一步/已到百分之几”。数据卡宜同时显示“采集版本/采集开始与结束/样本与范围”和“UI版本”，时间栏只反映各自事件。

## 7. 最小数据刷新与发布计划（本轮未执行）

1. **只有UI说明改变时零采集。** UI worker消费本投影或等价只读映射，突出主要比较与采集版本；保留旧run及原失败，不改历史generatedAt。不能只为了更新时间重复所有未变case。
2. **guard若需要发布新操作证据，先固定包含已审7aca的实际集成SHA。** 选择一个真实触及空读集路径的已有建模case，例如已登记 `support-chain.asymmetric43-base`，先由源码/小trace确认它命中该路径，避免仅因名称猜测覆盖；使用该SHA的同输入、同API、相同选项和现行比较合同采集单操作Fix输出。若没有命中，选择确实命中的最小已登记操作，不为这张截图强造收益。正确性采集与提速实验分开，单样本只做描述性数据。
3. **数学层暂不采成Boolean pass。** 39cell/foot材料可作为独立诊断附件，保留原exit1及分段覆盖语义；先满足review所列owner/generation/P/S inverse与实际可执行consumer合同，再以具体同target+全部tools输入的Boolean建立单操作证据。不能将冻结数学日志转成web operation pass，不能为显示“最新”先重跑整个case集合。
4. **新run独立存储。** 采集owner可用 `scripts/RefreshWebProgress.cs` 的精确 `--case <case-id>` 选择、`--samples 1`、独立 `--run-id/--output`，仅对冻结SHA的真实操作执行；不在本docs分支采集。该脚本仍会运行所选oracle的原后续流程，故运行前确认所选入口/预算；需要纯隔离操作时先由指定capture owner提供相应入口，不能把未计时后步冒充新的operation owner。保存实际源码/输入/选项/比较mask/PK版本与时间范围；变更比较合同的case才需要对应负控与等价表示正例。
5. **验证后再发布。** 数据owner按既有validation和bundle闭包验证run→manifest→capture/compare/mesh/source指纹；发布只添加新run并明确切换数据指针，UI记录自己的版本。旧case未重跑时保留其旧commit/时间并列展示，不能复制到新run假装同SHA新测。若只有该操作的新数据，就只标该操作更新；另行受控A/B证明速度后才发布优化倍率。compare控制耗时始终不进入APIratio。

## 8. 本投影的可复核性与未验证范围

`MSBUILDDISABLENODEREUSE=1 dotnet run scripts/AuditWebPrimaryEvidence.cs` 仅BCL读取固定run，不运行oracle或改data；输出含index/manifest/capture/compare SHA256、原29行六组唯一归属、mask及timestamp/code/scope。它断言原28/29数量、所有local差异仍为21949、每条原check恰入一组。连续运行输出应相同；若外部index或原文件改变，指纹会体现，不能将新投影悄称旧快照。

最终BCL生成连续两次退出0，JSON SHA256相同：`96d3d5788ac5a6f5eb612f9ced8a197df73b6f72f4d58866b32e85af7b75d734`；`git diff --check`通过。新控制摘录hash为 `a9d7e1342cf9e2d732c73468921fbc156983f04fc870470344b2b4fb6bc94534`，environment摘录为 `720bc628549c738597bcabc8a282b5a2dbdb49d675b5892bb98c2b314d584173`，最终临时源码存档为 `3c87692ea2f17aa14f810d810bedfa100c688fc8d6a37c972f990f0da93ac15d`。存档相较第一次成功控制只增加版本查询和无模型的environment-only退出分支；几何控制、mask和共享helper不变。原始编译输出位于 `temp_docs/web-primary-compare-control.log`、`temp_docs/web-primary-compare-environment.log`，文档摘录仅去掉含本机绝对路径的编译警告；未把警告伪作几何失败。

未做：所有API/模型的mask完备证明、accurate组与tolerant组的系统比较、大模型cap实际漏判复现、任意wire曲线完整等价证明、最新候选建模操作验收、稳定性能采样、live部署核验或数据发布。本报告没有发现需要放宽白名单/阈值的依据，也不更改既有生产验收。
