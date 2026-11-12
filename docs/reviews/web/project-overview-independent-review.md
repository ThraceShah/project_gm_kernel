# 项目整体进度看板候选（2bece5d）独立审查报告

- 审查对象：`docs/project-overview-candidate.md`（v2）+ 7 文件 diff
  `5222608e2d074643a1d516c7231ea84cf0933cef → 2bece5dfa952da02a3bf01a8d16f2abc0a31a276`
  （`docs/project-overview-candidate.md`、`scripts/BuildProjectOverview.cs`(374 行)、`web/data/project-overview.json`(761581 B)、`web/src/App.tsx`、`web/src/api/dataLoader.ts`、`web/src/components/OverviewPanel.tsx`、`web/src/types/contract.ts`）。
- 审查树：`gm-project-overview-review` @ `review/project-overview-2bece5d`（clean 创建于候选 SHA）。本树仅新增审计脚本、本报告与派生收据；未改作者树、生产代码、测试期望、任何原数据。
- 审查性质：产品功能/数据事实/统计口径独审。未运行 PK、未新建模、未跑全 suite/ABBA/AOT/Mac/Windows，未触 main/门户/线上（`https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/` 保持原样，未发布 015pin）。所有原进程退出保持原字节引用，本报告的审计 checker 退出码与原退出分列。
- 派发前已核 Pi 专用 profile `AGENTS.md` 中 `orca-user-global-policy-20261111` marker 内文（含尾换行）SHA256=`55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`，内容已实际读取（不冒称自动加载）。两架构文档 `docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md` 与本树 AGENTS 已读，作为口径背景。

## 0. 结论（能否冻结发布）

**不能冻结发布。** 存在 4 类阻断级数据事实错误与 1 类 UI 事实缺口（B1–B5），全部可用本报告附收据定点复现；作者宣称的 `--verify` 0 问题与浏览器 26 检查均为自证/未覆盖这些口径。统计与产品口径本身（分维、463 固定分母、累计 vs 复验分列、缺证据=未验证、历史归档保留）方向正确，修复 B1–B5 并按下文重算后可再做一次窄复验。

汇总表中「作者 v2」列即候选当前值，「证据实际」列为本独审按原始日志/收据重导出的口径：

| 口径 | 作者 v2 | 证据实际（本独审） | 说明 |
| --- | --- | --- | --- |
| function:pass | 188 | **182** | −9 跨套件借证据（B1）+3 首键丢证据（B2） |
| function:fail | 2 | **0～2（口径待定，见 B3）** | 两行红实为物性点值门失败，非几何/功能失败 |
| function:unverified | 273 | **279** | 9 借证据行退回 + 3 行升 pass |
| mass:verified | 25 | 24（若按最终相态可到 29，见 B5） | 1 行系借证据；5 行相态合并误标 |
| mass:unresolved | 62 | 56（最终相态口径 51） | 6 行系借证据 |
| mass:unverified | 376 | 383 | 7 行借证据 mass 清空 |
| query:fail/unresolved/na | 5/2/5 | 5/2/5 ✓ | cylinder 5 行查询缺口、探针 2 行 510 未决、5 行 na 无异议 |
| performance:observed | 5 | 5 ✓ | 数字与 1654 封存 schema 一致 |
| catalog | 463 | 463 ✓ | 目录不缩、ID 唯一 |
| mappingAudit | 195 | 195 ✓（但含 9 条借证据条目） | 89 selector 匹配+96 矩阵+5 named+5 测量 |

96 矩阵双重核对（id 后缀+参数 tuple）经独立复算 **96/96 唯一、0 歧义** ✓；批次/进程原退出（regular45=2、mixed18=2、complete-catalog=2、axial-material-query=1、transverse=134、probes-self=134、probes-import=134、standard=0、1654 主 runner=2）全部与原件一致保留 ✓。

## 1. 阻断项（含受影响行/数据/重现）

### B1 跨套件借证据签绿：9 行 function:pass（含 7 行 mass）来自别的套件日志

`scripts/BuildProjectOverview.cs` 的 `ReadScenarioLog` 把三个日志写进**同一个全局 `scenarioAudit` 字典（仅按 selector 键控）**，匹配处 `if(selector!=null&&scenarioAudit.TryGetValue(selector,...))` **没有套件限定**；证据路径 `logRel` 事后按消费行的 suite 选，`commit`/`tree` 也随之硬编码。registry 有 **9 个 selector 被 2–3 个不同套件、不同源脚本、不同参数的用例共用**（`disjoint-*` 3 键 × planar/analytic/regular-curved；`two-tools-*` 3 键 × planar/regular-curved；`coincident-*` 3 键仅 planar/analytic，无日志命中）。`regular45-final.log` 实为 `RegularCurvedBooleanOracle.cs` 的 45 用例运行（日志 csproj/PROFILE 可证），其中 `disjoint-15903` 等键属于 regular-curved 场景（tolerance=1e-5、scale=1、tracking=false），而：

| 受影响行（overview 行） | registry source / 参数差异 | 看板现状 | 证据实际 |
| --- | --- | --- | --- |
| planar-boolean.disjoint-unite / -intersect / -subtract | BooleanOperationOracle.cs，defaultTolerance、tracking=true | function:pass，mass=unresolved/unverified/unresolved | 本套件无执行记录 |
| planar-boolean.two-tools-unite / -intersect / -subtract | 同上 | function:pass，mass=verified/unresolved/unresolved | 本套件无执行记录 |
| analytic-boolean.disjoint-unite / -intersect / -subtract | AnalyticBooleanOracle.cs，radiusA=2 radiusB=1 toolOffset=[5,0,0]、tracking=true | function:pass，mass=unresolved/unverified/unresolved | 本套件无执行记录 |

9 行的 `lastVerified.evidence` 全部写着 `../gm-native-unified-delivery/temp_docs/unified-delivery/regular45-final.log`（浏览器展开即可看到「来源 BooleanOperationOracle.cs」与「证据 regular45-final.log」同屏矛盾）。**正确处置**：本套件无证据→未验证（保留目录），或把 planar/analytic 套件自己的审计记录接进来后按行签；不允许借同 selector 给另一 case 签绿。

重现：`dotnet run scripts/AuditProjectOverviewData.cs` → `FINDING cross-suite-borrow` ×9、`over-credited-pass` ×9、`mass-borrowed` ×7（收据 `docs/reviews/web/receipts/overview-data-audit.json`）。

### B2 首键 flush 语义丢失真实通过：3 行被记未验证

`ReadScenarioLog` 的块顺序是 **MASS_BOUND → MASS_AGREEMENT → `GEOMETRY_TOPOLOGY_PASS`（成功标记在块尾）**。解析器在遇到 `GEOMETRY_TOPOLOGY_PASS` 行时先 `Flush()` 再置 `pass=true`，且 `Flush` 仅 `if(!ContainsKey)` 写入——**每个日志的第一个 key 的记录在其自身成功标记之前就被写死（pass=false），随后携带 pass=true 的完整记录被丢弃**。同时状态量在切 key 时不重置，后续 key 的记录会**继承前一个 key 的 pass=true**（合成日志已证明：无任何成功标记的 key 也会被判 pass）。

| 受影响行 | 日志真实证据 | 看板现状 |
| --- | --- | --- |
| regular-curved.block-unite | regular45-final.log 第 49 行 `GEOMETRY_TOPOLOGY_PASS block-15903` | function:unverified |
| mixed-analytic.basic-unite | mixed18-final.log 第 40 行（basic-15903） | function:unverified |
| local-blend.block-single-fillet | complete-catalog.log 第 45、46 行（2 个标记） | function:unverified |

副作用同样危险：真实 FAIL 若出现在同 key 的成功标记之后，会被首写记录**丢弃**（合成日志 `fail-after-green-dropped`：builder 记录 k2 pass=true/fail=false，实际 k2 有 FAIL 行）。候选宣称的「真实失败不被旧绿覆盖」只由 `--verify` 的两个硬编码行检查兜底，**提取器本身不保证**。正确处置：按 key 全量汇聚（自身标记为必要条件）、以 key 自身的阶段记录为准、FAIL 永不被首写记录丢弃。

### B3 横切两行维度错置 + 一行无执行证据：把物性点值门失败签成功能失败，把一次运行的两个 cut 阶段拆给两个 CLI 场景

原始事实链（全部只读原件）：

- `transverse.log`（SHA256 `c39087c9…`）只有一行 `CASE a1-r0.5-t1E-07-reverseFalse-default-transverse-local-cut-reload-before-cut`，即 `--transverse-local-cut --reload-before-local-cut`（witness 场景）；`inertia-handoff.txt`（`24ea323c…`）给出的复现 argv 为 `--event-arrangement --geometry --lifecycle --material-ownership --require-native-cut-witness --require-cutter-chart-witness --reload-before-local-cut --transverse-local-cut --require-native-fin-uvbox --require-native-face-domains`，与 registry 行 `short-edge.transverse-witness-lifecycle` 的 arguments 完全一致。
- 该日志**只有 2 条 FAIL 行，均为 `FAIL ... mass: mass mismatch`**（第 2111、2568 行），分别对应**同一次运行的首切/末切两个阶段**（`...copy-reflected-cut` 1.2044611267910417E-07、`...reloaded-second-cut` 1.126468644220207E-07，worst 均 `inertia.yz`）；**没有几何/拓扑/有限边/材料 FAIL 行**。`inertia-handoff.txt` 明记「Both self cuts publish and pass strict/full/finite/material; only their raw inertia gates fail」。
- 已接受的 f9 独立评审（`4c7646e1085e6fa786b2f335cabc7c086c008e13`，`docs/reviews/advanced-modeling/pk-native-final-review.md`）「可交看板的状态」表：**「transverse 两次 cut | API/结构/有限边/材料通过，原惯量门槛红 | 不可宣称：整个 transverse 流程通过」**。
- registry 的 `short-edge.transverse-strict-lifecycle`（仅加 `--transverse-local-cut`，无 reload/witness flags）**在 f9 全目录没有任何执行日志**（`grep '^CASE '` 全目录仅 standard/transverse/probes 命中）。

因此候选 `nonGridFacts` 的两行硬编码是三重错误：
1. `transverse-strict-lifecycle` function=fail 的证据是**另一个 CLI 场景**的日志，该场景未执行→应为未验证（历史 exit 134 归属 witness 场景，保留原字节与原契约，不改 raw134）。
2. `transverse-witness-lifecycle` function=fail **违反本次用户口径**（功能/物性分维）：失败的是物性点值门（mass），功能（构造/几何/拓扑/有限边/材料，执行范围内）通过。应为 function=pass（执行范围）+ mass=红（点值门，两阶段分列注明 1.204e-7/1.126e-7），原 exit 134 保留在历史/退出层，**不倒签几何失败**。
3. 两个 cut 阶段的数值误差被配到两个不同 CLI 场景（1.204e-7→strict 行、1.126e-7→witness 行）——同一个证据被拆签。

「探针两项=510 未决」无异议（probes-self/import FAIL 行原文为「saved exact classification regression failed」，exit 134，属分类/查询维）；f9 评审要求「看板必须保留红色」，候选以 query=unresolved（琥珀）+boundary.probes-510=unresolved 表达，与「红」的展示强度差异请作者与协调定夺（本报告不改 f9 合同）。

### B4 事件更新入口不满足「持续正确更新」宣称（5 个子缺口，隔离派生测试实证）

测试：`dotnet run scripts/AuditOverviewEvents.cs`（输出仅落 `temp_docs/independent-overview-audit/events/`，原件只读；收据 `docs/reviews/web/receipts/overview-events-audit.json`）：

| 测试 | 结果 | 事实 |
| --- | --- | --- |
| T1 row 与 summary 同变 | **FAIL** | 事件把 `nested-evaluation.depth3-sequential-D0` 的 function 改为 pass，行变了，顶部 `function:pass` 仍 188（`Count()` 在事件处理之前执行，事件改行不重算计数） |
| T2 固定目录不缩 | PASS | delivery+未知 id 事件后 catalog=463 |
| T3 真实新失败 vs 旧绿顺序 | **FAIL** | 先追加「新失败(fail)」后追加「旧绿(pass, utc 更早)」，行最终=pass——**旧绿覆盖真实新失败**；事件按文件序覆盖，无时间序/单调性防护 |
| T4 错参数/平台不串 | **FAIL** | 事件 schema 无参数/平台/源身份字段，Windows arm64+完全不同参数的事件直接把 planar-boolean.overlap-two-axes-subtract 的 mass 改为 verified（host=Windows arm64 写入 lastVerified） |
| T5 缺证据不绿 | PASS(部分) | 空证据 pass 会被 `--verify` 抓到（exit 3）；但**伪造的非空证据字符串无法识别**（T5b，verify exit 0）——证据真实性只能人工溯源 |
| T6 坏事件不静默签字 | **FAIL** | `dimension:"geometry"`+`status:"great"`+`utc:"not-a-timestamp"` 被静默写入（dimensions 多出非法键 geometry），无拒绝、无未验证降级 |
| T7 更新入口交付 | **FAIL** | 声称的入口 `web/data/overview-events.json` 既未 tracked 也未 gitignore，候选树中不存在；新部署无该入口，`--events` 无文件时静默跳过 |

另外 `currentVersion` 仍为硬编码对象（1654 四个 SHA + 窗口 UTC），事件的 `currentVersion` 字段任意字符串直写；summary 的「当前版本已复验 N op」直接复用 `performance:observed` 计数而非各行 `currentVersion=="reverified"` 的重算（当前恰都是 5，一旦事件扩充即漂移，浏览器已在派生事件副本上实测到计数/行不一致）。

### B5 物性状态/标签口径错误（数据事实层）

- `ReadScenarioLog` 把同 key **多个阶段**（first-subtract / 末段 / disjoint 类双段，段间 `reference`/`reference_modulus` 明显不同）的 MASS_BOUND 分量**累加成一个计数三元组**，note 的「MASS_BOUND 逐分量：Verified X / Unresolved Y」是拼出来的混合账。5 行的合并态与**含自身 PASS 的最终相态 `MASS_AGREEMENT`**相反（均为最终相态 Verified、合并后显示 unresolved）：`regular-curved.disjoint-unite`（regular45-final.log 行 424 vs 447）、`regular-curved.mixed-disjoint-unite`（行 942）、`regular-curved.continuous-subtract`（行 1324）、`regular-curved.reloaded-subtract`（行 1618）、`regular-curved.reloaded-block-intersect`（行 1758）。应逐相态分列或以自身 PASS 相态为准，不得混账。
- 1654 测量 5 行的 mass note「1654 当前质量状态：{"Unresolved":2}（分量级）」：`qualityStatusCounts` 实为**每 op 候选侧 2 个 rep 样本**的状态计数（allSamples 20 条：exit2/Unresolved=16、exit0/Verified=4），不是分量级；真实分量总账是验证记录里的主 900 分量 312V/588U + 180 control = 1080/396V/684U。标签「（分量级）」误导。
- mass 三态判定 `qk.ContainsKey("Unresolved")?"unresolved":"verified"`：缺 `Different` 分支、空计数会误绿（本数据未触发，属口径漏洞）。
- schema 事实：`operation-summary.json` 的 operations[] 只有 `case,baseline,candidate,changePercent,observedSpeedup`——`rawExits` 恒 null（builder 里的 `procExits` 是死代码），`performanceQualified` 字段不存在（“performanceQualified=false”应引 `measurement-receipt.json` 的 `runnerPerformanceQualified:false`）。逐进程退出（planar 4 进程 raw0/Verified、其余 16 进程 raw2/Unresolved、主 runner raw2）与 receipt 的 `qualityStates{exit2-Unresolved:16,exit0-Verified:4}` 一致 ✓，数字口径对，引源需改。

## 2. UI/交互实测（本地独立预览；截图与收据）

预览组装：本树 `npm run build`（tsc+vite）产物 + 只读复制 publisher 静态包 run 数据（`gm-delivery-board/web/dist/data/{index.json,runs}`，6 个有效 run，latest=20261112T002227Z-uperf1654；`project-overview.json` 与本树候选**逐字节同哈希** `dd9c729b…`）+ display；端口 8813/8814/8815 自选。浏览器：playwright-core + 本机 chromium，独立执行 `dotnet run scripts/AuditOverviewBrowser.cs`。

17 项检查、15 通过、2 个预期失败（即 B4/B6 的实证）；收据 `docs/reviews/web/receipts/overview-browser-audit.json`，截图在 `temp_docs/independent-overview-audit/browser/`（overview-default / overview-cross-suite-row / model-jump-cylinder / event-summary-mismatch / overview-mobile-390 / overview-load-failure.png）。

通过：默认 overview+计数、463 行渲染、查询缺口行保留 fail+5000 证据、证据为本地路径文字（非可点链接，见 B6 口径）、跨套件行同屏显示来源/证据矛盾、搜索（'cylinder'→9 行）/family 筛选（boolean→186 行）/只看有问题（71 行）、模型跳转到正确 run/case（blend.axial-cylinder 双视口 >0 三角形）、返回 overview、刷新回 overview、390px 全量渲染、缺数据显示显式失败屏（无假数据、无 404 链接）、全程无未捕获 JS 错误。

**B6（UI 事实缺口）**：
1. **无 step 定位**：`web/src/types/contract.ts` 的 `display` 只有 `{runId,caseId,note}`，`App.tsx` 切 manifest 后 `setCurrentStepIndex(0)`。实测点 `local-blend.cylinder-unequal-chamfer` 的「查看模型」落在 blend.axial-cylinder 的**第一步 cylinder-rim-fillet**，不是 unequal-chamfer 自己的 operation-5（截图 model-jump-cylinder.png）。5 个 cylinder 产品互相不可分辨，note 写「步骤区分」未实现。manifest 里其实有 step id（operation-1..5 一一对应五个产品；blend.planar-chamfer 的 operation-1=prism-all-chamfer、operation-2=oblique-unequal-chamfer，prism 两行落点正确纯属 step0 恰好对位）。修复：display 增 stepId/operation 选择器并在 App 定位。
2. **同 run 内换 case 不重置步序**：`selectedRunId` 不变时 manifest effect 不重跑，`currentStepIndex` 保留上一个 case 的值（代码级确认，浏览器未单独截图）。
3. **证据可达性口径**：绝大多数「证据」是相对文件系统路径文字（`../gm-.../temp_docs/...`），对门户用户不是可点击原件；收据/hash 证明「原件私存」，但产品文案须分清「有 hash 的私存原件」与「可点击查看」，不能让用户误以为能点开。
4. 顶部「查询失败 1+边界」取 `boundary:fail`=1，而 query:fail=5（cylinder 5 行）；「当前版本已复验 N」直接复用 `performance:observed` 计数。两者都应从行重算或改标签（与 B4 计数问题同根）。

## 3. 已核实为正确/可保留的部分（不阻断）

- 分维结构（功能/物性/查询/性能）、`currentVersion` 复验与累计证据分列、463 固定分母+边界/验证记录不入分母、缺证据=未验证非失败、历史分列不顶替、目录不缩、容差变体分行、无在线产物明示不造 404——设计口径符合用户目标。
- 96 矩阵 id 后缀+参数 tuple 双重核对独立复算 96/96；5 个 named lifecycle/probes 项单列结构正确（映射内容见 B3）。
- 测量 5 行的数值（changePercent、warmGmMedianMs/warmPkMedianMs、配对 ratioMedian 387.35924284395196→文中标 387.359、中位数之比 388.639）与封存 `operation-summary.json` 一致；planar +0.55%（sphere 观测变化 11.18%≈+.0083ms 量级）确实只进性能维不进功能维 ✓。
- 63 场景的 `GEOMETRY_TOPOLOGY_PASS` 逐案标记真实存在（regular45 45 键+all、mixed18 18 键+all、complete 17 键）；批次 exit 2 的说明（mass Unresolved 编码、逐 case 未单录不伪造）与 `.exit` 原件一致。
- 边界登记（510 未决/faceattach 未测/bodycontains 缺口 fail/curved-roles 中性）与验证记录（Mac 2000/1998/2 原 1、Linux 1974 原 0 不互代、分量总账 900/180/1080、性能目标分窗口）与已接受背景一致。
- `--verify` 本身可复现（本机独立执行 `VERIFY issues=0`）——但它只做自比对+少量硬编码不变量，不能作为数据事实正确性的接受证据（B1–B5 均可零告警通过）。

## 4. 修复清单（交作者在同一任务修，随后窄复验）

1. **B1**：selector 匹配加套件+源+完整参数/precision 对应（或改为唯一键）；9 行退回未验证或接本套件证据；mappingAudit 逐条注明来源日志与套件。
2. **B2**：提取器按 key 汇聚（自身标记为必要条件）、相态分段、FAIL 不可丢；对 3 行补签 pass；补「首/末键、无标记 key、PASS 后 FAIL」的解析器回归（本树合成用例可复用）。
3. **B3**：transverse 两行按功能/物性分维重排（witness=function 执行范围 pass+mass 红、两 cut 阶段分列数值；strict=未验证+历史注记），保留原 exit 134/历史契约文字，不改 raw、不倒签几何失败；probes 红/未决展示强度与 f9 合同对齐。
4. **B4**：事件处理后重算 summary（或渲染层按行计数）；事件 schema 增加并校验 dimension/status/utc 枚举与格式、参数/平台/源身份（不匹配→拒绝或标未验证）；按 utc/序号单调，旧绿不得覆盖新失败；`web/data/overview-events.json` 作为交付物 tracked（空数组）或明确由 producer 侧生成并在发布清单登记；`currentVersion` 与「当前版本已复验」从行重算。
5. **B5**：mass 分相态分列/以自身 PASS 相态为准；`qualityStatusCounts` 标签改为样本级或引真实分量总账；三态判定补 `Different` 与空集=未验证；引源改 `measurement-receipt.json` 的 runnerPerformanceQualified/qualityStates。
6. **B6**：display 增 stepId 并在 App 落到具体步骤；同 run 换 case 重置/保留步序策略明确；证据行区分「私存原件（hash）」与可点击链接；顶部计数从行重算。

窄复验范围（修复后一次做完）：本树两脚本重跑（`AuditProjectOverviewData.cs`、`AuditOverviewEvents.cs` 归零或转白名单）、浏览器脚本 17 项全绿（含 step 定位与事件计数同变）、上述 9+3+2+5 行逐行核对。不按字段另开任务。

## 5. 证据固定（SHA256）与审计产物

原件（只读引用，未改动）：

| 文件 | SHA256 |
| --- | --- |
| `../gm-native-unified-delivery/temp_docs/unified-delivery/regular45-final.log` | `9cd6ba940cae8ce3b694c47a031e81f1a3986de720b88f6f8824ca6b6adcb7a5` |
| `../gm-native-unified-delivery/temp_docs/unified-delivery/mixed18-final.log` | `ad0aa1174a8549fb3dff0ebccb035841978e8c494d631a95941baadab45e537a` |
| `../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/complete-catalog.log` | `2b2d07c4167f96b48f1a7cac4fd25b8cdea95b6b29a3e9aef2c3618f45be8b45` |
| `../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log` | `898878a22c3812a66b331fdc68e8d153ceb7dbde6e1ab8c02da1008ef9ce321c` |
| `../gm-pk-native-delivery/temp_docs/pk-native-delivery/transverse.log` | `c39087c9c37c07f48c1eb26468ea9b2664909cce913e4ea8544ed3820b668a97` |
| `../gm-pk-native-delivery/temp_docs/pk-native-delivery/standard.log` | `22d0a47224ddeffc5a3017f3d8960db8b6b4b5cb5aa4a797938340bd824dc213` |
| `../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-self.log` | `841972d27a06d847fdc7dac18d81ae569dd46b96ea0541ad311b1d72c0448447` |
| `../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-import.log` | `75629a8c31443c35ac9b8d5750474c3ca6b3c83afefdd9489d9d6e163e32c4ee` |
| `../gm-pk-native-delivery/temp_docs/pk-native-delivery/matrix-96-cases.txt` | `d2360e82eeea6a0febf46a27dd8427781c64e7a71ffe28401cb43a87ae732f26` |
| `../gm-pk-native-delivery/temp_docs/pk-native-delivery/inertia-handoff.txt` | `24ea323c958e1a674a9658f2b0ebea969439acbd4ef5546012b8499b97e5ada0` |
| `scripts/web-progress-registry.json` | `58e863df13cffd2f1d9406c03b371056b6004089f039bd143ddcb53c1ec72277` |
| `scripts/BuildProjectOverview.cs`（被审对象） | `24cc13b5bce5ab54cb4b612960cffe9f1fb42c303fe6096dc4190ba6a30ae48c` |
| `web/data/project-overview.json`（被审对象） | `dd9c729bdae4c082b4b7990d074254ebd35b2cb90db42ef3db824ea5ab6887de` |
| 白名单复制 `temp_docs/incoming/.../operation-summary.json` | `48b84e9c777e6423d0a3783e7dba9ca0ad2afee776aa691622b476d68aa6d557` |
| 白名单复制 `temp_docs/incoming/.../measurement-receipt.json` | `07aa477e7c6f80199416ead954f8c00a944567122bbd54f94264843d2581d6b8` |
| 白名单复制 `temp_docs/incoming/.../material-binding-current.json` | `c5ba332c398283ed2313f69a9a465cc6a2d45bcbf74d0c7015f8701e1f04d951` |

版本/平台分列（不互代）：63 场景/local17 = q2 Linux x64、Native `133f56ac`/`60be7216`（4b2405e/60be7216）；96 矩阵/横切/510 探针/标准生命周期 = q2 Linux x64、f9c5920（接受评审 `4c7646e1085e6fa786b2f335cabc7c086c008e13`）；1654 测量 5 op = Mac M4 / macOS 27.2、Native `224564d1cdc0d0b6cf78c275282454751af2fd60`、measurement commit `d014f76e41746a08b32e963d24b6a8b02fcaac67`、交付 `73b06e10…`、独审接受 `2f8aea49…`。

本树新增审计产物（派生，不覆盖任何原件）：

| 产物 | 作用 | SHA256 |
| --- | --- | --- |
| `scripts/AuditProjectOverviewData.cs` | 相态正确解析 vs builder 语义复刻、跨套件/欠记/合并账/矩阵/测量 schema 核对 | `3625fb7818189e843c61f656d9fa021c1a91f984f4b00ed9f3c7926281ac1872` |
| `scripts/AuditOverviewEvents.cs` | T0–T7 隔离派生事件行为测试（真实 producer 子进程、temp 输出） | `6b1b87c10d76ecbdc650778cab1eb6e6e373ad2c98610be61bfb11e7f1be3e28` |
| `scripts/AuditOverviewBrowser.cs` | 17 项浏览器实测（8813 正常/8814 缺数据/8815 派生事件副本） | `8d45583aed41fa771c52ec7774fb1b351d34c0ff848a2eede904b41d94a3d164` |
| `docs/reviews/web/receipts/overview-data-audit.json` | 41 条 findings + 89 行逐行对照 | `ed6adf75868321f6469bc714e30621d4b4d298b29c51d1c014431bc5cf063c31` |
| `docs/reviews/web/receipts/overview-events-audit.json` | 9 测试结果（4 FAIL/1 PASS 摘要见 B4） | `7b73774c112940881f5900bf49bd80138b506c2ac01477474fcd5ee988706595` |
| `docs/reviews/web/receipts/overview-browser-audit.json` | 浏览器 17 检查明细 | 见提交后 git 记录 |

浏览器截图（临时派生）：`temp_docs/independent-overview-audit/browser/*.png`。隔离派生测试与合成日志：`temp_docs/independent-overview-audit/`（含 builder 子进程各 temp 输出）。审计脚本退出码仅代表审计结果，与原建模进程退出（134/2/1/0）分列，不补签任何原操作成功。本机仅清理本审查确证的 3 个预览服务 PID（8813/8814/8815），未动其他任务进程。
