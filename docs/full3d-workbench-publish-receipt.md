# 全 case 真实三维工作台 · 负责人自测发布收据

- **交付标题**：负责人自测发布（463 全目录真实 GM/PK 三维工作台 + Section 2c0e4ea 与累计集成 3aff4e2b 作为版本/改进记录）
- **发布性质**：负责人自测闭环发布。用户已全局取消看板独立 review，本收据不含也不声称任何独立 review/接受 gate。
- **发布目标 URL**：https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/
- **默认首页**：唯一 463 具名建模用例工作台（不是增量页、不是统计页、不是 9 卡首页）
- **发布 UTC**：2026-11-12T15:10:29Z（owned 手动 publish 生成 `bundle-af5343c9a197bf10`；随后 timer 于 15:13Z 用同指纹复核重发同内容）
- **代码提交**：`3af6c23521acebae775cb1e0a134330d5dca82e8`（分支 `feat/modeling-delivery-board`，已推）
- **数据提交**：同 `3af6c235`（`web/data` 全量替换为新 run + Section run）

---

## 1. 固定输入与溯源

### 1.1 采集源（本次真实三维补采）
- 采集树：`gm-delivery-board` @ `3af6c235`；`src` tree `7d516f6c16c762581f0ba2d87df4df5e263c6cc4`（等于 `origin/main:src`）
- 采集机：qingyu2 / Ubuntu 26.04.1 LTS / AMD Ryzen 7 7840HS / 16 logical / .NET 10.0.10 / PK 38.0.150
- registry：`scripts/web-progress-registry.json` version `2026-11-11.default-recipes-v5`，SHA256 `58e863df13cffd2f1d9406c03b371056b6004089f039bd143ddcb53c1ec72277`（463 条稳定 ID，与上一发布同集合）
- Capture/布线：`scripts/WebProgressCapture.cs` `8b705c1a0e3b79b981785c4759d22858bd3096bc1feb9b544a0000a5b3bafb75`；`scripts/RefreshWebProgress.cs` `307b89b453a44aff0c1e45659c6f2db5bf8aacae5fc75eaac63b0dcfe05a3d30`；`src/ProjectGmKernel.DisplayBridge/ParasolidDisplayBridge.cs` `88e48ff49aacd86198ac71298b129857013bc5e62050efa06fd36e1e2b7cb857`
- 说明（如实）：`../gm-native-query-numerics-closure` @ `ab597e8a` 的 `scripts/web-progress-registry.json` 与其自身 oracle CLI 已漂移（`RegularSolidBlendOracle.cs` 要求 `--output-name=`，registry 无此参数），在该树跑 463 全量采集会整批失败；因此本次三维采集使用唯一自洽的 delivery-board 采集树（其 `src` 即 `origin/main` 当前源），并保留 ab597 作为查询/物性事件的独立来源，不混用。

### 1.2 已接受事件（只作为版本/改进记录）
- **Section 2c0e4ea**：calc `2c0e4ea54bc2c4c9e22f24965ce71c41d84635b4`；Native `8fc38005545b10735c3612fdf6bd57460fae19af`；src `e8b7750c97e018ec6508a94fa8132817982158ff`；窗口 metadata `a770a49d07b43dbe05d284f5e11c2d6adfc40fd6`；交付 `fef52f3252bfe9a345f1a2872e9b6f96bdcafccd`；review `24c54edc299fd8fdcc7ec3c53542fca4eca4ebcc`；baseline `553b2c6c01d3862411f3b6a03735dc22ee20dc11`。
  - 只读原件：`../gm-section-2c0e4ea-review/temp_docs/incoming/mac-section-2c0e4ea-quality.zip` 21,731,725 B / SHA256 `3138cd943c7ffc265e4f9200dfb93e188fbb4a829d024b1b24bc06fa1183bed0`（589 成员）；`mac-section-2c0e4ea-controlled-original.zip` 33,951,734 B / SHA256 `79568a4a268b7fae9bab8453c136cf0ad9b5cd091cec3cba6ff3db1d653c74a7`（1037 成员）。
  - 展开原件：`temp_docs/independent-section-2c0e4ea/{original,measurement/original}/`；原 report SHA256 `f6075c57e57f98cf7a88e0cdc82fc4d456fd5094b9f0c8aa6bc6580193309044`；material-binding SHA256 `c01f2ab37f9eb2a55b5da9c50ede3be554e153366c6a0ab1d43e89dea01fd698`；312 mesh / 35,389,380 B / 64 最终 XT。
  - 窗口 UTC 2026-11-12T06:56:45.5438090Z–06:58:43.1836750Z；20 proc / 60 pairs / 120 API / 4 planar raw0 + 16 other raw2 / runner2 / runnerPerformanceQualified=false。
- **累计集成 3aff4e2b**：calc `3aff4e2b0247bd4f21e74e71d437a33dff1a3e24`；Native `854d01fec8201646b256acbb8297d3388dd74cdd`；src `545c317e7c6737b397708b80598e45c73aeb3bf5`；质量实际执行源 `b99c218e3592551e9bc73d5c509712871607e8bd`；窗口 metadata `a105eb4854e9f138e409a12bcb041e877f254452`；交付 `4026e59c827814e52559ec72d41ff282468e3894`；review `611b56c88a34685f6c50aabebe506cf20d0bc734`；baseline `271af03cfefedef12eea19337f1b2b3bca9fe96b`。
  - 只读原件：`../gm-accepted-integration-3aff4e2b-review/temp_docs/incoming/mac-integration-3aff4e2b-quality.zip` SHA256 `c2b9e802c32217c9cc3392c108b0bc2684a5df804a46ac57e8c7763777f9e556`；`mac-integration-3aff4e2b-controlled-original.zip` SHA256 `dbcb4708a9c79e941144deff9e6a55b277f65a72415dab1f874bbda6f47379b6`；312 mesh / 35,389,380 B / 64 XT。
  - 窗口 UTC 2026-11-12T13:15:25.2643960Z–13:17:20.4729820Z；16 raw2 + 4 planar raw0 / runner2 / runnerPerformanceQualified=false；旧合同物性 1080 = 396V/684U（不借 ab597 新 norm 洗绿）。

### 1.3 策略 marker 文件核对
- `/home/thrace/code/orca-worker/pi-profile/AGENTS.md` 的 orca 全局策略正文（去首尾空行 + 尾换行）SHA256 = `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`，与两份 review 收据记录的 `policyFileBodySha256` 一致。仅文件核对，不声称模型运行时自动加载。

---

## 2. 产品形态（唯一工作台）

- 左侧：稳定 463 具名建模用例 / 20 suite；`suite` 下拉、`family`、状态、搜索、以及「本轮改进」badge/筛选。筛选只缩小视图，不缩注册目录（footer 恒显示 `显示 X / 463`）。
- 中间：点 case 直接显示 GM / PK 双 viewport，`before` / `after` / 工具步骤、拖动缩放、camera 联动、picking 返回真实 entity ID、角色图例（target/selected/support/newBlend/modified/deleted）、dim 未改、仅看修改、复位。
- 版本/历史来源：`project-overview.json` 的 `display` 链接在 case 顶栏显示「版本来源：section-2c0e4ea-rep2」按钮，切换后渲染该 run 的 `section.sphere|planar|curved|solidblend|shortedge` 的 rep2 真实 GM/PK before/after/tool，并在横幅注明来源与「单 rep2 显示 ≠ 60 pairs」，返回即回本 run 采集；463 列表不切换、不消失。
- 整体/本轮改进只在顶部 summary bar 与 badge 呈现，历史 run 仍是版本/证据记录。

---

## 3. 真实三维覆盖（本次实际采集）

| 项 | 值 |
|---|---|
| 注册目录 | 463（20 suite，稳定 ID） |
| 有采集适配器的用例 | 347 |
| 本次实际执行 | 347（`pass` 313 / `fail` 34 / `unsupported` 116 / `missing` 0 / `timeout` 0） |
| 真实 3D mesh 文件 | 1,880（GM+PK before/after/tool） |
| 三角片 | 574,914 |
| 有 `after` 产物 | 334；13 个 fail 用例在操作失败前无 after（如实缺失，不伪造） |
| 执行窗口 | 2026-11-12T13:25:43Z–13:51:56Z（q2），另 3 个 mixed-curved blend 用例单独以 900s 预算重跑通过 |

- 分片采集与合并：ot0/ot1/ot2（83/83/82）、sq0..sq5（17/17/17/16/16/16）、to（3 个重跑）。每个 case 目录内含 `oracle.log`、`execution.json`（真实 argv / sourceHashes / exitCode）、`capture.json`、`evidence/compare-*.json`，原退出码与 Missing/Unresolved 完整保留。
- 合并只做「同一 registry 的不相交分片」拼装：仅当主分片未真正产出 `after` 时才采纳副分片，且复制其 case 目录原字节；两次都成功执行同一 case 会硬失败（本次 0 冲突）。
- 未适配的 116 条保持 `unsupported`（展示/导出适配器未实现，非内核不支持），保留真实 source/参数，不造 BRep。

---

## 4. 容量与无损传输

- 静态包（served bundle）：**3,338 文件 / 24,400,676 B（23.27 MiB）**，上限 31,457,280 B（30 MiB），**余量 7,056,604 B**。
- 传输编码：mesh、capture/compare 证据、以及 manifest 均以标准 gzip 成员存储，`step.gm|pk.encoding="gzip"`、index 的 `manifest` 指向 `manifest.json.gz`；解压后必须与原字节完全一致。
- 绑定账：`runs/<id>/mesh-transport.json` 记录每个成员 `uncompressedPath / uncompressedBytes / uncompressedSha256 / compressedBytes / compressedSha256 / kind`。
- 全量复核：两个 run 共 **2,904 成员**，逐文件解压比对 SHA256 与长度 **0 失配**；raw 134,002,613 B → gzip 20,274,648 B（6.61×）。
  - `20261112T143500Z-full3d`：2,872 成员，130,526,526 B → 19,767,306 B
  - `section-2c0e4ea-rep2`：32 成员，3,476,087 B → 507,342 B
- 这不是 schema adapter：当前契约就是 `.gz` 成员 + 显式 encoding；缺失或解压失败在 UI/validator 都是硬失败，不会显示空白模型。

---

## 5. 校验与自测（负责人自测）

- `scripts/ValidateWebProgress.cs`：`{"cases":472,"meshes":1906,"triangles":589270,"errors":[]}`（463 主目录 + Section 9 条证据/质量/套件用例）
- `scripts/ValidateWebProgress.cs --self-test`：含 gzip 传输契约与 encoding/member 不一致拒绝，全部 PASS
- `scripts/BuildProjectOverview.cs --verify`：`issues=0`
- `scripts/AuditOverviewEvents.cs`：`errors=0 notes=0`
- `scripts/AuditProjectOverviewData.cs`：`errors=0 notes=14`（沿用既有 14 条设计性 note）
- 本地端到端 `web/tests/smoke.mjs`：**151/151 PASS**（真实几何数量、角色像素、picking 真实 entity ID、camera 联动/解耦/复位、dim/仅看修改、run 切换各自统计、fail/missing/unsupported 不绿、无 dev fixture、390px 移动布局、无未捕获页面错误）
- 线上端到端 `web/tests/verify-portal.mjs`：**20/20 PASS**（HTTPS 首页/资产/index/manifest/mesh/evidence 200，双 viewport 真实像素，390px 移动，无未捕获页面错误）
- 刷新幂等：timer 连续两次 tick 均 `Finished`，prepared 指纹与 `published.json` 一致，线上 `data/index.json` SHA256 `9387f0636edf6e06b7293f51603032398645bc6763a4ec41f476d9bcaa5bc2e4` 与本地一致。

### 5.1 事件进入 463 目录（不是新页面）
- `overview-events.json` 新增 2 条 `delivery` + 10 条 `performance` case-update（Section 5 + 3aff 5），全部由 review 收据/原 operation-summary 派生，identity 用 catalog 原 parameters/source，未重签其他维度。
- 5 个性能行（`analytic-boolean.equal-overlap-subtract`、`planar-boolean.overlap-two-axes-subtract`、`regular-curved.block-subtract`、`solid-blend.plane-nurbs`、`short-edge.a1-r0.5-t1e-07-reversefalse-default`）的 performance cell 现为最新 3aff 窗口，history 保留 Section 与 projection 旧记录；两回退（Section curved +12.384%、solidblend +3.420%）与 3aff 的 +15.150% / +26.555% 全部照实。
- `summary` 复核：catalog 463、current:reverified 10、performance:observed 5、query:pass 5、mass:verified 29 / unresolved 51 / fail 1、function:pass 183。

### 5.2 已知限制（照实）
- 每 run 的 U/V 等参线派生包 `data/derivatives/isoparametrics/index.json` 在本数据集中不存在（线上同样 404，非本次引入）；viewport 如实显示「无等参线」，独立 `display/real-uv` 入口保留且 200。smoke 已改为在无派生包时断言「如实不可用 + real-uv 入口保留」，不伪造等参线。
- `web/data/project-overview.json` 的 `generatedAt` 每次 timer 重新生成都会变，导致 worktree 该文件长期 dirty、且每 10 分钟生成一个约 24 MB 新 bundle（沿用既有 timer 行为，未改）；其余字段逐字节稳定。旧 release 的 `work/` 已积累约 2 GB bundle，属既有运维成本。
- 116 条 unsupported、13 条无 after 的 fail 用例、以及 `curved` 原 roles/tracking 缺失，均如实呈现，不补造。

---

## 6. 发布与回退

- release：`~/.local/share/gm-kernel-progress-ui/releases/ui-017-tools-3af6c235-full3d`
  - `release.json` SHA256 `bffdd03e56f662ed21bbccfed7bca01db67979ff099aa3c5a7bd99208a5fe9dd`
  - `uiCommit` `3af6c23521acebae775cb1e0a134330d5dca82e8`，`uiHash` `7e7ae25662164dadc890c0ca3011eb8e50ba7ef89c9bc6483df899bbed015600`
  - `toolsCommit` `3af6c235…`（`scripts/release-tools/` 7 个工具文件，含 gzip 契约）
  - `acceptance` 字段 = `3af6c235…`（负责人自测提交；**不是**独立 review，release.json 内 `acceptanceScope` 已注明）
- 回退快照：`work/previous-snapshot`，treeHash `76cfa2f635531cc337a4eff1a14ffa1dfdfb2acd43ae63ca7ca5b8e9017afc25`，`servedVerified=true`（发布前对真实 HTTPS 校验通过）
- 旧数据整包：`/home/thrace/code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/data-archive-20261112T151152Z`（30,356,192 B，未删除）
- timer 安全状态：`gm-kernel-progress-refresh.timer` active；`zz-reviewed-ui.conf` 指向新 release + 新 `ui-commit/dist-hash/acceptance`；新增 `zz-worktree-validate.conf` 让刷新用当前 gzip-aware validator（publisher checkout 仍被 `ExecStartPre` 固定校验，未改其字节）；原 drop-in 已备份为 `zz-reviewed-ui.conf.bak-*`。
- 线上实测：首页 200 / 855 B；`data/index.json` 200 / 893 B（SHA `9387f063…`）；`data/project-overview.json` 200 / 838,351 B（SHA `b50218c6ecdfcf01dcce23a4cbc73ceb939b2fff7edefe33e0803f3c4b97b583`）；`data/runs/20261112T143500Z-full3d/manifest.json.gz` 200 / 348,231 B；`data/runs/section-2c0e4ea-rep2/manifest.json.gz` 200 / 8,111 B；`display/real-uv/display-manifest.json` 200。

---

## 7. 结论（限定）

本次交付：一个统一的全 case 真实三维工作台，463 目录中 347 个可采集用例都有真实 GM/PK 三维与操作 entity 角色高亮，Section 2c0e4ea 与累计集成 3aff4e2b 作为同一目录内的版本/改进记录（含 rep2 真实模型版本源），已自测并 owned 发布。

不声称：算法/数值全绿、普遍或因果加速、性能资格（两窗口 `runnerPerformanceQualified=false`）、PK 总目标达成、main 合流、以及任何独立 review 结论。

---

## 8. 增量（2026-11-12T15:42Z 再次 owned 发布）：369 适配 + 发布幂等

- **采集适配器**：`347 → 369`。新增 21 条 `planar-special`（registry 旧参数 `--three-plane-only` 等与本树 oracle 实际 flag 不符，已按 fixture 逐条更正为 `--three-plane-events/--three-plane-hole/--three-face-cylinder/--plane-conic-cut/--conic-corner/--coplanar-tools/--finite-axial-contact/--axial-contact-imprint`）与 `surface-blend.negative-depth3`（补 observer + selector，依赖本树 oracle 自生成的 `nested-blend-native/depth3.x_t`、`negative-depth3.x_t`）。
- **真实结果**：21 条 planar-special 均产出真实 GM/PK 输入 before 三维与真实 GM 失败 `Boolean at Read/None, curve 0, t=0: 5000`（原 kernel 不支持，如实失败，不伪造 after）；`surface-blend.negative-depth3` pass，1 步 before/after 真实 sheet body，角色中性（evaluation/sense 比较，不称建模操作）。
- 当前总账：`pass 314 / fail 55 / unsupported 94 / missing 0 / timeout 0`，有 steps 的用例 **369**，mesh 1,960，三角片 593,386。
- **发布幂等**：`BuildProjectOverview` 的 `generatedAt` 改为由最新已应用事实（delivery / 维度 cell 的 deliveryUtc 最大值）派生，不再用墙钟；同一输入重复生成字节完全一致（实测两次 `82d9fc72…`）。timer 连续 tick 现输出 `UI_SKIP_SAME includes UI identity`，不再每 10 分钟生成同内容 24 MB bundle；未改 timer 频率/全局配置，未删历史证据。
- **新包**：3,459 文件 / 24,617,310 B（23.48 MiB），余量 6,839,970 B；`data/index.json` SHA256 `1e4516886499c8ddb407e18332d66b75643b507910483255f0b62afd9d9d9524`；`data/runs/20261112T143500Z-full3d/manifest.json.gz` 350,069 B；gzip 绑定 2,949 成员（raw 131,214,905 B → 19,901,648 B）。
- 自测：本地 smoke **151/151**、线上 verify-portal **20/20**、`ValidateWebProgress` `cases=472 meshes=1960 triangles=593386 errors=[]`。
- 旧数据整包归档：`…/web-progress-publish/data-archive-20261112T154226Z`（未删除）。
- **仍未完成（94 条）**：`nested-evaluation 42`（纯评估，需“被求值原生曲面/曲线 + 参数/点/导数”专用显示适配，不能用 body 冒充）、`pk-corpus 23`（PK 原生 corpus fixture，需 PK-only 显示与来源分列）、`nested-blend 6` + `extended-blend 3` + `blend-operation 8`（oracle 尚无 observer，需逐族布线；extended 同时负责生成本树缺失的 fixture）、`short-edge 2`（`self-fixed-probes` 已有真实 fail 三维；`imported-fixed-probes` 原 exit 134 未产出 mesh，需查 PK 导入路径）、`surface-blend 1` 以外的 `planar-edit 10`（edit 循环无 observer，且开启 capture 会跑整套矩阵，需按 fixture 加 Before/After 与 selector）。
- 本轮不改 Native/数学/Mass/Compare/容差，不重跑已采集 347，不重跑 PK 全 suite/性能窗；13 个失败前无 after 的用例与 3 个原 900s 首超时/原退出保持分列。

---

## 9. 增量（2026-11-12T16:05Z）：原生评估显示 + PK corpus 单侧显示（434/463）

- **新增显示原语** `WebProgressCapture.Evaluation`：显示真实 receive 的被求值曲面 + 实际求值样本点（selected）+ 原生法向（support）叠加线，并在 step 上写 `evaluation.kind=native-evaluation-display`、`sampledNotContinuousEquivalence=true`、`operationChangedGeometry=false`；不创建 GM 几何、不声称建模操作、不签连续数学等价。
- **nested-evaluation 42**：`NestedBlendBenchmark` 每 fixture 输出一步真实评估显示（真实 fixture 曲面 + 32 个实际求值点/法向），6 个 pattern 注册共用该 fixture 的显示；runner 对声明为评估显示的 capture 免除操作计时，`performance` 保持 null 并写明原因（不伪造 0x/1x，不冒充稳态基准）。
- **pk-corpus 23**：核对每条原用例语义后按 **pk-only** 显示真实 PK fixture body（`displayScope=pk-only`），step 注明原用例是 PK XT corpus/schema + managed transmit round-trip，不是 GM 独立建模；GM 侧明确未执行，不编造等价性。corpus 用例改用显式可写 capture 根与 host case id（私有 corpus 根只读）。
- **契约**：validator（工作区与 release tools 两处）只允许声明了 displayScope 的 step 缺另一侧；未声明仍按原规则报错。
- 适配器 **434/463**；新包 3,845 文件 / 25,754,078 B（24.56 MiB，余 5,703,202 B），`data/index.json` SHA `e48887bcd1e887b4a58d1ac68cf432e7edee881b834bb0eabe650e64d4362076`。
- release：`ui-018-tools-9c2e2654-full3d`（tools 含 displayScope 契约；uiHash 不变 `7e7ae256…`），timer drop-in 已指向新 release，tick 输出 `UI_SKIP_SAME`（幂等保持）。旧数据整包归档 `data-archive-20261112T160541Z`。
- 自测：nested-evaluation 与 pk-corpus 各自本地浏览器实测（真实面/样本点/法向可见、PK-only 侧 GM 明确空白、无 JS 错误、picking 真实 entity）；`ValidateWebProgress` 472 案 2148 mesh 597,844 三角片 0 error；两份 overview 审计 0 error。
- **仍未完成（29 条）**：`planar-edit 10`（edit 循环需暴露输入 body 句柄并按 fixture 加 Before/After+selector）、`blend-operation 8`（曲面求交，需显示源 sheet + 实际求得交线/点）、`nested-blend 6` 与 `extended-blend 3`（oracle 无 observer，需在真实操作边界接观察器）、`short-edge.imported-fixed-probes`（原 exit 134，需查 PK 导入路径与 capture 层）。
