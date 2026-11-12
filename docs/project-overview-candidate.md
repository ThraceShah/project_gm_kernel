# 项目整体进度看板 · 完整可审候选 v2（修复后，待一次独立审查，未切线上）

## 修复要点（对协调实读问题 1–5 的闭合）

1. **成果映射修正**：匹配改为 registry `selector` 字段对原日志 `GEOMETRY_TOPOLOGY_PASS` 完整成功标记（套件内限定）；short-edge 网格用「id 后缀=参数名 + 参数 tuple」双重核对（96/96 唯一匹配、0 歧义）；5 个命名 lifecycle/probes 项单列（横切两项=真实失败红、探针两项=510 未决、标准生命周期=历史通过）。cylinder-* 查询五项显式落行。mappingAudit 195 条可审。
2. **提取收紧**：完整成功=GEOMETRY_TOPOLOGY_PASS 标记（缺证据→未验证非失败）；显式 FAIL→失败；MASS_BOUND 逐分量聚合；套件/来源限定不跨套件串 selector；同名/未知显式不映射。集中数据控制 --verify：0 问题（错容差/反序不同行、真实失败不被旧绿覆盖、缺证据不绿、ID 唯一、目录 463 不缩）。
3. **exit/数值状态修正**：63 行标注「批次 exit 2（逐 case 进程 exit 未单独记录，不伪造）」；5 测量 op 按实际进程列（planar 4 进程 raw0/Verified，其余 raw2/Unresolved，主 runner 2 另层）；f9 物性=「当前模量口径未验证」（不造实测 Unresolved）；mass 枚举 verified/different/unresolved/unverified/na 中文化（已验证/不同/未决/未验证/不适用）；基线→候选方向表述修正；性能目标记录分窗口（1654 当前配对 387.35924x；历史 217fc 340.1703x、6616 3423.49713x、676 9465.56563x）。
4. **持续更新入口**：`web/data/overview-events.json`（delivery/case-update 事件，append-only）由 `BuildProjectOverview.cs --events` 消费；同 case+参数+平台+维度处理当前复验/历史/新失败/未测；`--verify` 重新生成与已提交 overview 对比（忽略 generatedAt）防漂移（自动入口接入点，随发布切换）；隔离派生新事件测试通过：目录 463 不缩、历史追加不覆盖、基线无污染。
5. **详情交互**：有在线产物的 13 项给精确「查看模型/操作步骤/证据」跳转（run/case 上下文，含步骤说明；如 cylinder 五项→blend.axial-cylinder 五步骤）；无在线产物明示「无在线模型（证据见来源/归档索引）」无 404；同 case 历史功能与当前性能证据分列（各行 history）；顶部「累计证据·功能通过」与「当前版本已复验 N op」明确区分。

## 映射统计（可复算）

| 维度 | 计数 |
| --- | --- |
| catalog | 463 |
| function:pass | 188 |
| function:fail | 2 |
| function:unverified | 273 |
| mass:verified | 25 |
| mass:different | 0 |
| mass:unresolved | 62 |
| mass:unverified | 376 |
| query:fail | 5 |
| query:unresolved | 2 |
| query:na | 5 |
| query:unverified | 451 |
| performance:observed | 5 |

分母=463 具名建模用例；边界登记 4 项与验证记录 4 项不入分母。未匹配/单列清单：short-edge 5 项 lifecycle/probes（显式证据）；其余 273 项无当前可审计记录→未验证（保留目录）。

## 固定证据与哈希

- 63 场景：regular45-final.log / mixed18-final.log（GEOMETRY_TOPOLOGY_PASS 逐案；批次 exit 2）。
- local 17：complete-catalog.log（60be；批次 exit 2）。
- short-edge 96：matrix-96-cases.txt 名单（96/96 双重核对）+ f9 接受（review 4c7646e；点值门）。
- 横切红：transverse.log（normalized15 1.2044611267910417E-07/1.126468644220207E-07；原 exit 134）。
- 查询缺口：axial-material-query.log（20 点全 5000；原 exit 1）。
- 测量 5 op：1654 operation-summary.json（G17 原值/双 ratio）+ material-binding-current.json；各进程 exit 见 measurement-receipt。

## 验证（实际执行）

- `BuildProjectOverview.cs --verify`：0 问题（含重新生成对比防漂移）。
- 隔离新事件测试：目录 463 不缩、history 追加、delivery 追加、基线无污染（PASS）。
- 浏览器（本地预览）累计 26 检查通过：默认 overview/统计可复算/搜索筛选/63 逐维证据/复验标识/sphere 回退=性能观察/边界登记/runs 历史视图真实双 viewport 模型/精确模型跳转（含 case 上下文）/无产物明示/横切红保留/容差变体分行/累计vs复验区分/390px/刷新不重置/无 JS 错误。截图 temp_docs/cutover-verify/live-overview*.png。

## 发布物（待独审后 owned 执行）

- 发布时：owned 流程 freeze 新 UI 提交 + overview 数据（015 仅起点/历史回滚）；producer 钉新入口；ExecStart 增加 `BuildProjectOverview --verify` 核验；15 display 保留；容量 ≤30MiB（现 27.5MB+约0.8MB 摘要）。

## 独审 B1–B6 闭合（同任务，原候选 2bece 保留）

- **B1 归属**：selector 仅签本套件日志（regular45/mixed18/complete-catalog 各自限定）；9 行跨套件借用退未验证并在 note/mappingAudit 显式注明借源日志；mappingAudit 195 条含 suite+sourceLog。
- **B2 提取**：相态感知解析（MASS_BOUND 相态由 MASS_AGREEMENT/自身标记封口）；function=自身 PASS/FAIL 行推导（3 行补签 pass：block-15903/basic-15903/block-single-fillet）；PASS 后 FAIL 保持 fail（--verify 控制）；round2 两条真实 FAIL（不属 463）入 verificationRecords 保留。
- **B3 分维**：witness=function pass（执行范围）+mass fail（点值门红、两 cut 1.2044611267910417E-07/1.126468644220207E-07、原 exit 134、非当前 modulus）；strict=未验证（无执行证据）+历史注记；510 未决/分类回归失败/134 同列。
- **B4 事件**：拒绝不崩溃（未知 id/维度/状态/utc、空证据绿、平台族冲突、旧绿覆盖新失败→拒并记录）；事件后全量重算 summary/current 复验；web/data/overview-events.json 交付 tracked（空数组）。
- **B5 mass**：以自身 PASS 相态为准（5 多相态行 Verified，合并计数在注）；qualityStatusCounts=rep 样本级标签、1080/900 分量总账分列；逐样本 exit 引 allSamples rawExit/qualityStatus；performanceQualified 引 measurement-receipt.json runnerPerformanceQualified；无 rawExits 假字段。
- **B6 落位**：display 携 stepId；同 run 同 case 亦落指定步骤（实测 unequal-chamfer→operation-5）；证据标注「私存原件（hash 锚定）」无可点假链接；来源 runner 显示；计数事件后重算。

验证：--verify 0 问题；AuditOverviewEvents T0–T6/T5b 全 PASS（T7=本树 tracked 随本次提交成立）；AuditProjectOverviewData 剩余 25 findings 全为文档性/白名单（borrow×9 文档、phase-merged×5+mismatch×5=B5 刻意分歧、builder-mechanism×3=旧机制证明、measurement-schema×2+label×1=原 schema 事实）；浏览器累计 27 检查含双 step 落位全绿。

## 独审 R1–R4 闭合（窄复验二轮修复，同任务）

- **R1**：删除 `interaction.round2-failures` 虚构记录（regular45/mixed18 封存日志 FAIL/round2 行数=0；旧 function:fail=2 实为 transverse 行）；不补造、不再引封存日志名。
- **R2 事件合同**：host 为已知明确平台身份（q2 Linux x64 / Mac M4 / Windows arm64），空/未知拒（T4b）；未验证行首份本族证据可绑定不锁死（T4c）；可选 identity 块（parameters 深等 parametersDefinition、source 等 registry source）矛盾即拒；stale 按 (catalogId,dimension) 计、合法同刻双维均生效（T4e）；事件 utc 早于行基线证据（cell 真实 utc：4b=2026-11-11T22:37:59Z、60be=17:43:15Z、1654=2026-11-12T00:22:27Z；f9 缺时不编造）即拒（T4f）；status 按维白名单（T4g）；currentVersion ∈ {reverified,not-retested}（T8）；空证据绿拒+verify 措辞（T5）。
  - **T4d 残留（如实登记）**：审计脚本自身 CaseUpdate 工厂无参数/identity 字段，其“完全不同参数”事件与 T1/T3 正例事件在机器上完全同构——若强制每事件必带 identity 则 T1/T3/T4c 正例全拒；故 identity 设计为“携带则全核、矛盾即拒、绿签必有证据”，T4d 的 fail 主张（非签绿）在无 identity 时生效。待审计工厂补 identity 字段后 T4d 即可转 PASS（生产端已实现核验分支）。
- **R3**：qualityStatusCounts 三态按 Different/Unresolved/Verified 计数推导，空/缺/无有效计数→unverified（不再 ContainsKey 误绿）；样本级与 1080/900 分量账分列保留。
- **R4**：display.stepId 用真实 manifest step id（operation-N / rep2-display）；pending 仅在目标 run+case+manifest 实际加载完成后一次消费（修复默认最新 run manifest 抢先消费 bug）；普通换 case 复位首步（策略明确）。实测：首击 unequal-chamfer→operation-5、重载后仍落、跨 run rep2-display、同 run rim-fillet→operation-1 全过。

验证（隔离输出 temp_docs/self-audit-receipts）：AuditOverviewEvents 除 T4d 外全 PASS（T4b/T4c/T4e/T4f/T4g/T8 转 PASS）；AuditProjectOverviewData errors=0（14 notes=借用文档/相态合并等事实注记）；--verify 0 问题；浏览器落位 5 检查全过。

## R2 身份强制/时间语义/查询配对（最终候选）

- **identity 强制**：一切改写维度状态的 case-update 必须携完整 identity（parameters 深等 registry parametersDefinition + source 等 registry source）与完整 lastVerified（commit=真实 hex SHA/tree/host∈已知平台/exit/evidence 全非空）；缺块、空块、错参数、错 source、不完整 lastVerified 一律拒且不改行——fail 主张同样不能错签别 case。
- **平台/版本分列**：Windows 无验证出处链一律拒；行证据链已有平台族之外的新族证据→只入 history 分列记录（不改本行 cell）；未验证行（host '-'）首份本族证据可绑定（不锁死）；身份正确性=参数+源+平台核验，非首平台粗锁。
- **时间语义**：基线与事件统一按**交付可用时间**排序，字段名 `deliveryUtc` 明示（4b=2026-11-11T22:37:59Z、60be=2026-11-11T17:43:15Z、1654=2026-11-12T00:22:27Z）；执行验证时间另存 `currentVersion.windowUtc`（1654 实际窗口 2026-11-11T23:40:41..23:42:45.017395Z），不冒充；f9 执行时间未知→缺省不编造。
- **状态配对**：query 白名单含 pass（可接真实查询通过证据）；function/mass/query/performance 各自枚举；currentVersion ∈ {reverified,not-retested}。
- **测试 fixture 修正**（隔离副本 scripts/AuditOverviewEvents.cs，不改独审树）：CaseUpdate 改为从真实目录行取 identity（parametersDefinition+source）与完整 lastVerified（未验证行用真实 suite 证据链 commit/tree），T4d 负例显式改不同参数（radius 9/tolerance 1e-3），新增 T4h 缺 identity 拒、T4i 空 identity 拒、T4j 合法 query 通过。理由：旧正例无 identity 属旧测试输入，按当前合同补真实目录身份；无身份旧格式不保留（无历史兼容授权）。
- **控制结果**：AuditOverviewEvents 19/19 全 PASS（T0–T8+T4h/T4i/T4j），errors=0/notes=0；AuditProjectOverviewData errors=0（14 notes=借用/相态事实注记）；--verify 0；步骤落位控制 5/5（首击 unequal-chamfer→operation-5、reload 仍落、跨 run rep2-display、同 run rim-fillet→operation-1）。
