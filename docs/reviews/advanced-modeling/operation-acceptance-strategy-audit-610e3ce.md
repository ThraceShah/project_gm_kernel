# 建模验收、交付顺序与效率审计（2026-11-11）

## 结论与适用版本

当前不能把问题统称为“基础计算一直不行”。固定证据分别支持：曾有有限区间证明和周期参数域错误；这些已有范围明确的 R1/R2 修复；当前另有重参数化后的事件/精确约束传递缺口、GM 后续 Boolean 消费失败、分类兼容差异、验收归属混杂和完整操作重复求界成本。不能用其中一个标签代替其他问题的证据，也不能据此宣称整个框架完成。

本审计在独立分支 `ThraceShah/gm-strategy-audit`，启动 HEAD 与当时 `origin/main` 均为 **`610e3ce844bf99c85af6069905f9f3deaab08133`**。不改内核、原测试、阈值、CSV、Web/host/scheduler 实现或 main。本文连同逐操作账本和机器清单，取代旧 M0–M5 文档的**当前执行顺序、归属与完成判定**；历史原始结果不改写。

用户最新硬要求立即适用：每个操作用同模型、同参数、同 API 与真实 PK 独立比较；合法组合测试另列，不因后一步失败倒扣前一步已经直接通过的 primary。只有直接证据证明前一步自己的输出质量合同不成立，才更正它的接受范围。`PK_DEBUG_BODY_compare` 是主要几何/结构比较；full BODY_check、原物性、有限域/拓扑质量检查继续保留，510 是辅助兼容检查。

## 固定证据与本轮新增验证

| 证据 | 固定版本与位置 | 可支持的结论 |
| --- | --- | --- |
| E1 初次独立审查 | 被审 `a1e45006c08491db85f4ef87ced6d236342b15e6`；报告提交 `a3e9a489d17d374bb756fe65e6efb5407f1f896b`，`docs/reviews/advanced-modeling/topology-event-framework-independent-review-a1e4500.md` | 标准 lifecycle 的 fullcheck badloops13826；transverse 两项原惯量 `1.20446e-7/1.12647e-7 > 1e-7`；import-only96 不能证明 self96；不是所有基础几何都失败 |
| E2 R1/R2 分次复核 | `c301b40` 的报告提交 `4e8f0ce14463446c0c9734fb569830407c3fac9e`；`2bdd0f30c2cddca64ac8b1197ac66f39e210894d` 的报告提交 `0a33258db4436c318982220c13b9af432781bc4f`；后续输入校验 `f5642f2fe7c21108d05c605d8419ad93a81e6e32` | R1 标准路径独立通过；R2 从采样改为有限区间证书后，修正空间端点界及向外舍入，限定 provider/有效预算域获接受；不是任意曲线/网络认证 |
| E3 当前 R3 分层独立报告 | 被审 `9b2f4c6c989a393fd2cbaa3e66d6ea5b52035017`，against `90f4d7840b4d3d8219ba29d1845ccbaff3386f5a`；报告提交 **`8c9fed1347bd137bcd6fb8a785db299743e78d90`**，`docs/reviews/advanced-modeling/topology-event-framework-independent-review-9b2f4c6.md` | default uncut blend primary 接受其已测范围；完整 RollingNetwork19 为16/3，按分类/内部端点合同/两tool组合 Boolean157 分开；相同候选输入的独立 PK Boolean 未测 |
| E4 原生自身控制 | E2 报告及其中 `topology-event-framework-2bdd0f3-evidence/` | 无 GM 程序集的纯 PK 对原510坐标有7个自身XT差异；不能全部归因于 GM，也不豁免339；用户已批准改用相同路径逐点比较，原历史失败不重写 |
| E5 整操作历史性能 | main固定本文基线的 `docs/Modeling_Performance.md` 与 `docs/reviews/advanced-modeling/modeling-performance-command-cache.txt` | 重型 Boolean 有953971个bound请求，缓存后仍209599次 Joint Bound，约201.6秒耗于此；有工作量和重复证明证据，不等于当前全部场景的根因 |
| E6 有限源码/数据盘点 | `operation-acceptance-audit-610e3ce.json`；生成器 `scripts/AuditOperationAcceptance.cs` | 固定 git 对象的完整登记清单、词法API/检查点索引、规则影响包络与历史多step证据；不把静态候选当已证bug |
| E7 本轮真实 PK 小控制 | `scripts/BodyCompareAuditControl.cs`；结果见 `operation-acceptance-audit-evidence.md` | 默认max_diffs0漏掉同拓扑错误高度；严格helper拒绝；wire仅compare可过但有限边辅助拒绝平移/错trim；无GM程序集 |
| E8 基础设施边界 | scheduler固定 `5ae2cb53573dda56533b3ddb7ec46b5fc878edd0`，报告 `dce5a3b29c0a4b0ccce5952f8b8c057ae873df91`；host报告 `11617841d0bd910c67ebd5ddd03ef1645be26dbe` | scheduler限定接受不代表host/main集成；host1b4缺index所需run绑定仍可回滚成功，是真实closure遗漏，不是泛化安全反例 |

E3 及其他分支报告按上述提交读取；未被本分支复制的报告路径不是本分支已经包含的文件。原始日志摘要和指纹见 E7 附件。本轮没有重跑19/96/347/463、full suite、完整bench或NativeAOT；没有停止其他worker/服务。

实际读取 `parasolid-research`、`parasolid-docs` 技能，并调用 Parasolid MCP 的 `parasolid_get_function_detail(PK_DEBUG_BODY_compare/PK_BODY_check/PK_BODY_boolean_2/PK_SURF_eval/QTP_compare_bodies)`、两个入口的callgraph、错误157查询。MCP 将debug compare标成legacy、没有完整选项说明，故不能把其简短摘要当完整规范；结合本地官方FD106.7、固定绑定构造器及E7验证。Linux符号地址没有跨平台意义，callgraph只是已解析静态直接调用，不是运行时覆盖或性能证明；未从反编译prologue推断某个canonical frame规则。

## 有限范围全量盘点与准确计数

机器清单覆盖固定 main 的全部 **175 dispatch登记、463配方、155个C#脚本文件（134个shebang入口，含基础设施/辅助脚本）、152个KernelTests源码文件、27份review Markdown**。1073个Fact/Theory属性对应1073个词法索引方法；初版1065遗漏了InlineData间注释，已核实并修正词法器，`unindexedTestAttributes`最终为空。InlineData/MemberData与CLI无限组合不展开，这不是1073个动态路径均已人工验收。review目录其他txt/CSV/JSON作为被引用证据，不把它们算作27份Markdown的人工语义审查。每个API/入口都有登记位置、API词法出现顺序、检查点、状态/计时未知范围和关联配方；该顺序不等于某次执行路径，文件里的创建API常只是前置构造。

| 175项按本审计有限分类 | 数量 |
| --- | ---: |
| 构造、几何编辑（包括split/knot/degree等） | 55 |
| Boolean | 2 |
| blend/chamfer标记与执行 | 4 |
| local edit/offset/hollow | 7 |
| 求交/section/imprint | 6 |
| copy/transform | 6 |
| 求值、投影、距离/范围 | 9 |
| 分类 | 1 |
| transmit/receive | 2 |
| 查询、质量、资源/会话管理 | 83 |

这是80个构造/修改等登记加95个求值/分类/传输/辅助登记的枚举分类，**不是80个完整建模能力已通过**。机器清单保留所有名字及分类，query/ask/free不再混作独立建模case。463 registry实际仅有 **blend277 / boolean186** 两个family，347有展示适配，116没有；没有为全部175API建立逐操作验收登记。此前17个代表case不能证明剩下330未运行或116未适配的内核能力。

去重结果为**7条已证报告/验收归属规则，涉及7个源码文件；2条待核实规则，涉及另8个源码文件**。规则不是7个几何算法bug；合法组合本身保留，需纠正结果模型。完整受影响配方ID在JSON的 `rules[].affectedRecipeEnvelope`；上界/共享格式暴露量不能当实际受害数。

| ID | 已证位置及入口 | 应更正的归属 | 配方/范围 |
| --- | --- | --- | --- |
| A1 | `RefreshWebProgress.cs:102-129`、`WebProgressCapture.cs:208-209` | 只有case检查/总status，无独立operation primary owner；将其解释为每个API状态会倒扣前步 | 347适配配方共享schema暴露，实际多step另数 |
| A2 | `RefreshWebProgress.cs:102,125-129` | ratio为捕获API时间之和的case比值，不能标成一次指定操作的性能 | 同347格式暴露；不是347个已错倍率 |
| A3 | `ShortEdgeBlendOracle.cs:256-298,341-403` | self Fix、分类、copy/transform/receive、后续Boolean共用父case失败计数 | 来源包络101配方；各可选flags尚不等于已执行 |
| A4 | `FreeformModelingOracle.cs:49-65,470-496` | 构造primary与随后导入/copy/delete/transform lifecycle均完成才打印该构造case PASS | 精确2个条件场景：`parallel-nurbs-path-solid`、`three-profile-natural-loft`；registry未登记，0不代表无问题 |
| A5 | `LocalTopologyChangeOracle.cs:42-81,270-288` | Imported条件分支将local edit和后续lifecycle结果合一 | 精确2个：`native-import-split-face`、`native-import-face-elimination`；registry0 |
| A6 | `GeometryTransformOracle.cs:254-303` | body分支copy、transform、依赖采样、source deletion、XT组合只有父case结果 | body分支；几何单体分支不据此判错；registry0 |
| A7 | `ParasolidEvaluationOracle.cs:119,676-685`；`docs/ParasolidBodyCompareValidation.md:31` | 10个primitive/rotated primary先通过、后offset依赖删除5002导致总退出失败，应保留前面通过和后面失败 | 历史已证报告归属；没有重跑或声称当前必重现；registry0 |
| A8 | JSON列出的chain/support/trivalent/mixed/rotational/regular-solid六入口 | 独立前序产物各自进入后序，是否误当same-input单步比较需分支核实 | **疑似**，91个配方来源包络；不是91个已证问题 |
| A9 | `LocalOperationOracle.cs:84-90,303`、`IntersectionOperationOracle.cs:347-363` | canonicalization与自定义compare mask需逐类说明合同，不能自动继承所有实体的解释 | **疑似**，17个配方来源包络；wire已有独立有限边检查，non_match=false本身不足以证明wire验收错误 |

三个已跟踪历史manifest共30次attempted case记录，多step记录6条、去重case ID为5个（两版本transverse ID不同）；它们不是当前 main 新运行结果。较新的 `20261110T180347Z-90acd4a9` 17条中，精确3个多step：standard lifecycle、transverse witness lifecycle、modified-support-bore。前两条5个step中可能含未到达占位，不能把5当5个成功API。第三条明确是Fix+Boolean，但仍 `operation=fix-blends`；其case比值 **2187.5741800947862** 是 `sum(GM captured API ms) / sum(PK captured API ms)`，单cold样本、非一次blend倍率，具体原始分量在JSON的 `historicalRuns[].cases[].captureTimings` 与对应capture路径。采集器没有把导出/比较/构造I/O混进这些API计时；这个归属问题不能证明实际整操作性能差距已消失。

本轮直接纠正**3份旧路线/状态文档的当前解释**：plan、acceptance、handoff增加明确取代关系，保留历史正文。待实现的确定分工是上述**7个源码文件**的归属/记录改造，加Web消费者按新字段呈现；A8/A9仍为**2项待核实**，不趁本轮无限扩审或降低既有比较条件。未展开动态路径仍是明示未知，不能据词法方法覆盖率宣称所有配置均已检查。

## 主要拖慢因素：证据强弱分开

1. **producer/consumer合同未闭合，有直接局部因果证据。** E3中sourceLine12的t4.5与rolling surface(pi,1)给精确 `(-1.5,-2,5)`，contact ellipse124的tπ给 `(-1.5000000000000002,-2,5)`；共享event保留root锚但采用contact坐标。`StationaryBoundaryEndpoint.cs:38`需要精确平面关系，证书入口因此失效。只改临时模型的该vertex可使第一157后移，仍在curve214失败，row289也不变。这个实验只证明第一局部关联，不证明三项同根因，更不支持放大epsilon或全局sin抹零。
2. **重参数化、几何等价、内部可消费性被混判。** `AnalyticRollingCandidate.cs:166-179`把三角系数转主轴，`RegularBooleanCuts.cs:652-665`选位置/锚，`FiniteIntersectionEvents.cs:233-234`只做period lift。signed phase一般缺口真实存在，但当前cap是π−u的固定点，不能据一般反例称它是本fixture157根因。几何容差内等价不要求逐位坐标相同；需要精确代数约束的consumer必须消费producer实际保留的证明，或有等强包围证书。
3. **验收报告的状态粒度错误，有全量有限盘点证据。** A1–A7把不同检查合在case/进程出口；E1的import-only96代替self96和局部完成措辞又造成“已完成→独立拒收→再修”的循环。合法集成测试没有错，错在用总退出码替代每步事实。修改报告模型可立即减少无效返工。
4. **完整操作工作量大，有历史剖面而无当前通用归因。** `SURF_eval`给指定UV的点/导数；Boolean还需有限域候选排除、根隔离/完整性、退化事件、loop/region、所有权与事务。每次eval快，不代表执行次数少，也不代表区间Bound快。E5缓存保持相同证明工作量而省约一半API时间，证明可在不删证明下优化；本轮不抢Mac机器再bench。R2每10站2.5–4秒是新债务但尚未live，不可能解释原live百倍。
5. **协作流程有可修正的具体损耗。** partial14项aborted且余5未跑被叫“1fail”，错误完成摘要使review/修复目标漂移；多轮scheduler/host遗漏schema/闭包不变量导致追补反例。E8最终scheduler接受说明有边界的独立检查可收束；host缺绑定反例属于同一closure合同，不应为省时跳过。现有证据不能量化“换模型造成多少分钟损失”，因此不把全部延迟归因于模型。

## 已生效的统一逐操作验收合同

`operationId` 对一次实际API调用稳定，不用case名推断。每步必须保存输入来源/相同参数、API与options、GM/PK输入hash及前置状态、输出hash、primary/aux结果、计时范围。分类/求值/询问类用对应输出参数/导数/错误码对照，不能强行套body compare；资源/回滚类用返回契约与状态恢复。详表及当前9b账本见 [逐操作账本](operation-acceptance-ledger-610e3ce.md)。

一个已声明provider范围内的实体建模操作达到“可验收”，需要：本步API成功且结果状态/数量正确；独立同操作PK参考；GM结果自家text XT保存后由真正PK receive；适当mask的compare和全部非豁免global/local差异验收；full BODY_check零故障；原body/region/shell/material/sense/容差语义；适用的有限trim/分支完整性/几何检查与原物性阈值。有限采样不是任意曲线完整等价证明，wire要额外有限边匹配。自己的函数返回0不等于输出有效；PK compare API返回0也不等于无差异。

比较mask维持 `max_diffs=1024, all_tests=false, acc_dev_tests=false, tol_dev_tests=true`，num/tol/class/sense/missing/nonmatch开启，仅n_cht_pts默认诊断，释放r_f。max_diffs是有差异face pair上限，不是几何容差；n_face_pairs不是全部成功配对数量。既不把fullcheck当compare替代，也不把compare当GM内部持久root/chart/约束合同的证明。新降级必须逐项独立论证并经用户硬要求约束，不在本任务引入。

510全坐标保留四列和7点原生诊断，按已授权同路径 `GM即时=PK即时`、`PK接收GM导出=PK接收PK自身导出` 验收；row339不豁免，坐标/精度/物性阈值不变。其失败归分类或导出/接收兼容步，不反向取代blend primary。

组合 `scenarioId` 独立保留原case exit、逐步结果、首个失败API、未到达步骤及端到端时间；组合失败不改写已完成primary。对于后续Boolean差异，必须区分两种输入：共同一份PK几何分别交两内核的独立Boolean；各内核上步产物分别继续操作的integration。要隔离GM产物上的Boolean，则将**同一份GM target+全部tools**交两内核，记录导入前后fullcheck/hash与导入行为；没有这份证据不能称same-inputBoolean差异已证。

## 修订里程碑：现在即可执行

| 阶段/依赖 | 具体交付及文件所有权 | 关闭条件 |
| --- | --- | --- |
| S0 立即；不等修复 | 本审计冻结清单、账本、旧文档取代关系；框架worker独占Capture/Refresh及validation字段，UI worker只消费schema | A1/A2先有operation/result与scenario结果两层；原manifest只标legacy-aggregate，不回填捏造pass/ratio |
| S1 立即并行 | 集成人员准备R1/R2依赖闭包；性能worker整理已有Mac输入/环境/采样协议；各leaf owner按A3–A7分配脚本 | `c301b40 → 2bdd0f3 → f5642f2`逐文件确认完整，包含Bound/compile/copy/chart/test依赖；不得把未接受R3或90f文档中的试验补丁自动混入；不自动等于main已合入 |
| S2 依赖S0；局部修复 | 内核实现者独占frame/contact/event/consumer相关源；review只读固定SHA及原失败工具 | signed-phase、有限端点/root约束保真和适用域写清；先复验原157第一/第二位置、分类289及内部endpoint；每个状态分别报告，不为了绿而先改测试 |
| S3 依赖S2；单步资格 | oracle owner补共同输入Boolean与GM产物输入Boolean的独立case；blend保留已测接受范围 | 同输入hash/options/导入前置质量；本步strict/full/原mass与有限检查；单独报告Boolean，不等待self96才知道该步是否正确 |
| S4 依赖冻结候选 | 集成worker单一工作树、单一测试产物；一次安排受影响managed/native gate+组合矩阵 | 固定SHA上原失败+邻近负控已过，才跑Rolling19/self96/受影响oracles及必要full regression；不以import96替代self96；保留所有未到达/退出码；无C ABI不跑AOT |
| S5 依赖相应单步primary | Mac性能worker独占采样窗口；本任务不新增完整bench | 单步correctness-qualified warm/cold；组合端到端另报；达成对应场景与PK完整操作对标目标后才认性能完成 |

S1代码闭包不能只cherry-pick一个证书文件；c301自身含R1及R2/Bound关联增量，2bdd修正不可漏，f564输入域修正也需保留。以E2源码范围和负控为依据在专用集成分支组合，reviewed不等于自动merged。scheduler5ae可独立准备；host需先拒绝缺绑定回滚，再做二者集成与部署验收。基础设施与几何修复并行，但共享 `scripts/Directory.Build.props` 指定单一集成人员处理两套include，避免多worker覆盖。

最小consumer矩阵：producer→有限参数事件→arrangement/publish→分类；producer→transmit/PK receive；producer→copy/transform→delete source→editable receive→Boolean；Boolean→预期allocation注入→rollback。每行标单步/组合、参数/证明owner及未验证范围。需要的最小负控按变化选择，不每次重跑全部矩阵：

- 几何比较：同拓扑错尺寸实体必须拒绝；wire平移/错trim必须拒绝；升阶/插结等价正例维持。
- 参数域：signed phase/reversal+一个period lift正例，错误sign/phase且非固定点负例；不能只测当前固定点cap。
- 精确约束：象限语义正例和近象限不应被误认的负例，证明来源/代数关系随事件传递；不全局epsilon抹零。
- 有限证明：原错误端点/错分支/空区间或无效预算应拒绝；已接受R2的全区间包围正例保留。
- 事务：先证明可到注入位置，再检查注入失败与target+全部tools的XT、generation、活跃资源恢复；提前157不能算memory901负控通过。
- 资源/持久：generation变化后旧证书拒用；copy删除源后仍可访问；未支持域原子拒绝。每项映射到实际consumer，禁止只断言“测试标记已开启”。

修复循环先运行最小原失败与一个相关正/负控，保存固定SHA和测试产物hash；本层未关闭，不重复昂贵全套。通过后独立review只审改变的合同和未改原反例；冻结后才一次集成回归，新失败先最小化再决定是否重跑。进程partial/aborted必须写 planned/executed/passed/failed/not-run，各自数量；不得用exit0诊断脚本代替内部Boolean成功。

停止继续同一修复路线/换模触发条件：两轮固定SHA仍重现同一最小反例而没有新证据，或A→B→A回归，或再次把partial/diagnostic当完整完成。届时冻结反例/源/最后已通过点，协调端审查合同与负责人，再决定换实现模型；不是每个新负例都换模型，也不是机械再跑全套。重大算法域缩减、改用户精度/性能目标等互斥取舍才带完整背景交协调端说明，本修订不需要另行授权。

## 性能协议与数据/UI最小交接

复用Mac已有事实与其后续协议，不重复采样。完整操作目标保持对标乃至超越PK；eval旧70%目标不替代它。每个可比case的warm目标为 `GM/PK <= 1`（测量不确定时不称达到），同时保留cold比值与绝对延迟，不用跨场景均值掩盖慢项。当前没有用户批准的cold启动数值SLA，单列事实，不发明宽松门槛。

固定硬件/架构、SDK/PK版本、Release/JIT设置、资源限额与并发负载；同输入和options、相同API计时边界，不计构造/导出/验收I/O。cold细分“新进程首次该API”与“整个进程从未JIT”，二者不可混称。warm同进程预热后反复构造新输入，命令缓存按真实使用边界重置；只复用已证明不变的数据，不删除证明/负控。按Mac协议保存全部原始样本和次序、median/离散程度，样本不足或噪声显著标诊断，不用cold1sample推断稳态百倍。每次有对应输出primary资格；失败保留时长但ratio=null。组合另报各步与端到端wall，不能求和重叠profile段冒充API时间。

最小字段交接（由后续框架实现，历史manifest不改）：`scenarioId/scenarioStatus/firstFailedOperationId/legacyExitCode/checkVersion`；`operationId/ordinal/api/options/parameterHash/inputProvenance/inputHashes/preconditions/outputHashes`；`primaryChecks[]/auxiliaryChecks[]/representationContracts[]`各带owner、证据与status；`operationStatus=pass|fail|not-run|unverified`；`timing={boundary,gmMs,pkMs,coldKind,warmup,samples,correctnessQualified,ratio}`；组合单列 `scenarioElapsedMs`。导入/导出/copy/transform、求值/分类与资源故障也有各自operation ID或明确fixture-precondition角色。可读旧schema但缺细分的结果标`legacy-aggregate`，不能从“总exit失败”还原独立失败，也不能从“总exit成功”捏造每个API都做过PK比较。

前端以operation为主条目，case仅做组合容器；primary、aux、内部表示合同并列，原始过程exit始终可见。先改A1/A2两共享文件及validator，再改A3–A7五leaf文件的归属，最后接UI；A8/A9按具体路径取证后再决定。此顺序避免一次case一个补丁，同时保留真实错误、用户硬目标和已获范围接受的进展。
