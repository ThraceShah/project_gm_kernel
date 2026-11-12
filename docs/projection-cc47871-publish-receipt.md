# 项目整体进度看板 · 负责人自测发布收据（Projection cc47871 成果接入）

- **交付标题**：负责人自测发布（Projection cc47871 成果接入项目整体进度目录）
- **发布性质**：负责人自测闭环发布（独立 review 已由用户全局明确取消，不再设 review/接受 gate；历史独审收据不冒充新接受）。
- **发布目标 URL**：https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/（默认首页=整体 463 具名目录）

---

## 1. 固定已接受输入与溯源

- **计算与测量窗口源**：
  - Calculation commit: `cc47871133fb7d536ffa7fd050066c51f4905708`
  - Native commit: `fe3fa0378ef6d9c0dd971de4c993a289cdb8a34b`
  - Src commit: `31be8f6c46766bb3b60b0e402bfc043426ed58fb`
  - 交付 docs commit: `553b2c6c01d3862411f3b6a03735dc22ee20dc11`（分支 `perf/native-projection-bound-work`）
- **独审接受依据**：
  - `review/native-projection-cc47871@7b20d7f1046ad5b6f54f8601edd739fa5d54a901:docs/Native_Projection_Independent_Review.md`
  - `docs/projection-independent/{receipt,material-bindings}.json`
- **原始白名单归档与散件**：
  - `mac-projection-cc47871-quality.zip`: 20,724,821 B / SHA256 `c1f1b90ff6f8eed87d23cb960b16e3502eab77211f0d1d13bdce7f14812702b8` / 543 文件
  - `mac-projection-cc47871-controlled-original.zip`: 33,945,233 B / SHA256 `9bfad64ff79211c4fbc6620e3a2678e3f966e8150d5b3dad778e5814c8128919` / 1037 文件
  - 原始报告 report.json SHA256: `6568f1d9d19bbf0df0f9eeb07dc2afac1f5990599535b2f5caa0dbaf40d9e90e`
  - material-binding-current.json SHA256: `3678da6d62f44e00e8e6419af0709b25d73c43f3cfdd50a0a8c7c75e916d0306`
  - 展开目录：`../gm-projection-cc47871-review/temp_docs/independent-projection-cc47871/{original,measurement/original}/`，受控根 `temp_docs/projection-controlled`
- **基线与历史分列**：
  - 基线源: `17eedbfd6be891db8c43494cd91a896d261242c4` / Joint185 Native `116`（`e812b7a003e1bebf0699afab5e9c212023220e99`）
  - 历史 1654 保持原记录分列，不回写覆盖历史证据。

---

## 2. 测量与质量数据事实（严格保留原值）

- **时间戳区分**：
  - 实际测量窗口（execution UTC）：`2026-11-12T04:56:39.7493270Z .. 2026-11-12T04:58:37.0945730Z`
  - 交付/收据时间（delivery UTC）：`2026-11-12T04:58:37Z`
- **受控测量规模与原 exit**：
  - 20 processes, 60 pairs, 120 API measurements（API 失败全部为 0）
  - 进程级退出码：4 planar processes raw exit 0（Verified）；16 other processes raw exit 2（Unresolved）；主 runner raw exit 2
  - `runnerPerformanceQualified`: `false`（性能未达标，不宣称普遍加速）
- **五操作暖样本实测与双口径倍率（G17 原值）**：
  1. `sphere` (`analytic-boolean.equal-overlap-subtract`):
     - 基线暖中位 0.08265ms → 候选 0.078ms（-5.62613%）
     - 配对 GM/PK 中位数: `0.260427`；中位数之比: `0.25815`
     - 候选侧 2 rep 样本状态计数: `{"Unresolved":2}`，raw exit 2
  2. `planar` (`planar-boolean.overlap-two-axes-subtract`):
     - 基线暖中位 0.55835ms → 候选 0.99445ms（**+78.1051% 回退，必须保留**）
     - 配对 GM/PK 中位数: `0.875491`；中位数之比: `0.869997`
     - 候选侧 2 rep 样本状态计数: `{"Verified":2}`，raw exit 0
  3. `curved` (`regular-curved.block-subtract`):
     - 基线暖中位 40.50215ms → 候选 38.8493ms（-4.08089%）
     - 配对 GM/PK 中位数: `18.22905`；中位数之比: `18.22884`
     - 候选侧 2 rep 样本状态计数: `{"Unresolved":2}`，raw exit 2
  4. `solidblend` (`solid-blend.plane-nurbs`):
     - 基线暖中位 597.1976ms → 候选 500.76395ms（-16.1477%）
     - 配对 GM/PK 中位数: `253.84728`；中位数之比: `244.25723`
     - 候选侧 2 rep 样本状态计数: `{"Unresolved":2}`，raw exit 2
  5. `shortedge` (`short-edge.a1-r0.5-t1e-07-reversefalse-default`):
     - 基线暖中位 646.96075ms → 候选 548.4714ms（-15.2234%）
     - 配对 GM/PK 中位数: `331.97129`；中位数之比: `332.04468`
     - 候选侧 2 rep 样本状态计数: `{"Unresolved":2}`，raw exit 2
- **物性总账合同**：
  - 72 报告 1080 唯一分量：396 Verified / 684 Unresolved / 0 Different（主 900 分量 312V/588U；额外 180 分量 84V/96U）
  - 保留本窗口旧合同原字节，不代签尚未完成的独立物性标准（ab597）。
- **套件事实**：
  - Mac 2022/2022 raw build0/test0 全部通过（耗时 4m44s，raw exit 0），只属 cc47871；历史 Joint185 2019/2019、1654 1998/2000 原 1 保持分列不回写。
- **八组质量限定**：
  - 7 raw exit 2 / 1 raw exit 0（`active-rigid-small-capture`），全部限定几何/拓扑/生命周期通过，missing 照实记录。

---

## 3. 模型与网格绑定事实

- **选定候选 rep2 显示样本**：
  - `01-sphere-candidate`: `gm-2-before.json`, `gm-2-tool-0.json`, `gm-2-after-0.json`, `pk-2-before.json`, `pk-2-tool-0.json`, `pk-2-after-0.json`
  - `05-planar-candidate`: `gm-2-before.json`, `gm-2-tool-0.json`, `gm-2-after-0.json`, `pk-2-before.json`, `pk-2-tool-0.json`, `pk-2-after-0.json`
  - `09-curved-candidate`: `gm-2-before.json`, `gm-2-tool-0.json`, `gm-2-after-0.json`, `pk-2-before.json`, `pk-2-tool-0.json`, `pk-2-after-0.json`
  - `13-solidblend-candidate`: `gm-2-before.json`, `gm-2-after-0.json`, `pk-2-before.json`, `pk-2-after-0.json`
  - `17-shortedge-candidate`: `gm-2-before.json`, `gm-2-after-0.json`, `pk-2-before.json`, `pk-2-after-0.json`
  - 共计 26 份真实网格文件，2,949,570 字节，全部来自 `temp_docs/projection-controlled/window/{sample}/meshes/` 原件。
- **显示语义约束**：
  - 显示单 rep2 $\ne$ 60 pairs 统计；不虚构 seed/input 的 rep 编号。
  - shortedge 来源：PK seed → GM receive → GM Fix（不称 GM 独立构造）。
  - curved tracking/roles 缺失如实中性标注。
  - 质量开发 NURBS 成品是 rep5，不与正式 rep2 串。
  - 24 份 native-via-managed 额外 GM body 未采主 display，不能绑主 GM mesh；12 份原 sourceBinding 缺失仍缺。

---

## 4. 目录状态与容量审计

- **目录稳定性（固定 463 具名建模用例）**：
  - 463 项具名建模用例 ID 保持不变，未重现 9 卡增量首页。
  - 功能通过为累计历史分列：183 pass / 280 unverified / 0 fail。
  - 当前指定版本（cc47871）复验单独统计：**5 reverified / 458 not-retested**（未测行仍保留历史证据与当前未复验标记，不借此冒签全 463 通过）。
  - 物性统计：29 verified / 51 unresolved / 1 fail / 382 unverified。
  - 性能统计：5 observed / 458 unverified。
  - 查询统计：5 fail / 5 na / 2 unresolved / 451 unverified。
- **容量审计（严格 $\le 30$ MiB 门）**：
  - 门限要求：$\le 30\text{ MiB}$（$31,457,280$ 字节）；portalctl 32 MiB 硬限。
  - 内容复用优化：历史 `20261111T174315Z-blend60be` 中 `blend.axial-query-gap` 与 `blend.axial-cylinder` 在同 run 内包含完全相同的 20 份网格文件（1,297,799 字节），通过 manifest 引用同一网格路径并移除冗余目录，回收 1.238 MiB 空间，且完全通过 `ValidateWebProgress.cs` 校验。
  - 最终发布包（work candidate bundle）：
    - **文件数**：**613 文件**（$\le 5000$ 限制）
    - **总容量**：**30,738,282 字节（29.314 MiB）**（$\le 30$ MiB 限制，余量 718,998 字节）
    - 新增 run ID：`20261112T045837Z-projcc47871`（cases=9, runBytes=3,637,272）

---

## 5. 自测与线上服务验证

- **自测闭环结果**：
  - `BuildProjectOverview.cs`: 0 issues，catalog=463, deliveries=8, mappingAudit=210。
  - `AuditOverviewEvents.cs`: **19/19 PASS**（T0–T8 + T4h/T4i/T4j），`errors=0, notes=0`。
  - `AuditProjectOverviewData.cs`: `errors=0, notes=14`。
  - `ValidateWebProgress.cs`: `cases=523, meshes=312, triangles=96870, errors=[]`（0 errors）。
  - `PublishReviewedWebProgressUi.cs`: prepare 成功通过容量门（613 文件 / 30,738,282 B），publish --execute 完成发布。
- **自动化服务轮次（systemd `gm-kernel-progress-refresh.service`）**：
  - 第一轮（自动再生与发布）：
    - `ExecStart` 成功执行 `BuildProjectOverview.cs --events` → `BuildProjectOverview.cs --verify` → `ValidateWebProgress.cs`。
    - `ExecStartPost` 成功通过 `PublishReviewedWebProgressUi.cs` 发布至 Tailscale 门户，退出码 0。
  - 第二轮（验证幂等与无回退）：
    - 触发后自动完成再生与验证，并由 `PublishReviewedWebProgressUi.cs` 确认 `baselineIndexReused=False`，成功完成校验，退出码 0。
- **线上 HTTPS 端点实测（全部 HTTP/2 200 OK）**：
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/` (200 OK)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/project-overview.json` (200 OK, 814,847 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/overview-events.json` (200 OK, 16,211 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/index.json` (200 OK, 2,824 B, latestRunId=`20261112T045837Z-projcc47871`)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T045837Z-projcc47871/manifest.json` (200 OK, 65,279 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T045837Z-projcc47871/cases/projection.sphere/sample-0/01-sphere-candidate/meshes/gm-2-before.json` (200 OK, 416,959 B)

---

## 6. 剩余未完成目标与历史边界保留（不因本次交付冒充整体完成）

1. **五圆柱 BodyContainsVector 5000 查询缺口**：历史与当前均未解决，保持 `fail` 标注。
2. **FaceAttach**：负 surface query/copy/reattach 组合未测，保持 `untested` 标注。
3. **510 分类边界**：self 15 / import 16 / native 7 差异未决，保持 `unresolved`。
4. **物性分量未决**：684 个 Unresolved 分量照实记录，不冒签通过。
5. **主体性能目标未达**：
   - cc47871 shortedge 配对比 331.97x（比中位数之比 332.04x）
   - solidblend 配对比 253.85x（比中位数之比 244.26x）
   - planar 出现 +78.105% 明显回退
   - `runnerPerformanceQualified: false` 原样保留，不把单次描述性观测当成整体性能目标达成。
