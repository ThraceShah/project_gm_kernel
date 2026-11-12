# 项目整体进度看板 · 负责人自测发布收据（Native 查询与物性规范化 ab597 成果接入与修订）

- **交付标题**：负责人自测发布（Native 查询与物性规范化 ab597 成果接入与元数据修订闭环）
- **发布性质**：负责人自测闭环发布（独立 review 已由用户全局明确取消，不再设 review/接受 gate；自测后直接 owned 发布）。
- **发布目标 URL**：https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/（默认首页=整体 463 具名目录）

---

## 1. 固定已接受输入与溯源

- **计算与证据源**：
  - Calculation / Math commit: `ab597e8a24b2ef23753507c8ca3e43d12edcc650`
  - Evidence commit: `0ef2305f5f75a8f810bb8212219adcc1744b882f`
  - 交付 docs commit: `f633b51db703df308cf4ac194e3c2b1661398d4d`
  - 作者白名单树：`../gm-native-query-numerics-closure`
- **独审接受依据**：
  - `review/native-query-numerics-ab597@3e7ac1b1273c56530bfbd50f0cb6bdffb08e4fd7:docs/Native_Query_Numerics_Independent_Review.md`
  - 独审收据：`../gm-query-numerics-ab597-review/docs/query-numerics-independent/receipt.json`
- **计算侧 HTTPS 读回依据**：
  - 计算 owner（`task_ee94113828ef` / `ctx_2b3eb0357411`）于提交 `c7c8a52f3fe7625bdefc497392ba94db27777944`（`origin/feat/native-query-numerics-closure`）产出：
    - `docs/Query_Numerics_HTTPS_Readback.md`
    - `docs/query-numerics-closure-online/readback-v1.json`
  - 读回确认：21 个 HTTP 200 资源全部成功，59 项硬核对完成，无损原始 `results.gz.json` 与 12 个真实 chamfer mesh 逐字节一致；指出派生 manifest 中的 7 项元数据与描述性差异。
- **原始结果与机器入口**：
  - `docs/query-numerics-closure/results.json`: 5,247,186 B / SHA256 `aed7c9787a03bbb798f7a7be0f9f3b832400c15956af205a7b55662e4866bd60`
  - Gzip 压缩证据：`results.gz.json` 保持 257,328 B / SHA256 `3f29e8dc9965978bbef9e7e0b7db1b509e4b6e119b9ebbd653368e91a29db270`
- **顶层版本状态**：
  - 顶层标头维护为：`"功能/物性ab597与性能cc47871分源汇总"`（两源分列注明，不冒充合一内核）。

---

## 2. 运行修订与错元数据完整只读归档

因当前发布服务包容量上限严苛（$30\text{ MiB} = 31,457,280\text{ B}$），已按用户授权将首次派生错元数据运行安全完整归档，并移出活动 served 集，改由修订运行复用原真实 payload：

### A. 历史错元数据运行（只读归档）
- **归档 Run ID**：`20261112T132500Z-queryab597`
- **归档物理目录**：`/home/thrace/code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/archived-runs/20261112T132500Z-queryab597/`
- **归档文件总数**：19 项文件，总字节数 687,378 B
- **归档 manifest.json**：22,199 B / SHA256 `e8bb5c626e9e2154c24e5c53ac6238b8b0551966817d9be2ce63e313a777d742`
- **归档状态**：原件与索引记录已写入 `archive-manifest.json`，只读固化，可随时逐字节还原；已从 served 目录及 `index.json` 移出，不再消耗 served 配额。

### B. 当前活动修订运行
- **修订 Run ID**：`20261112T072500Z-queryab597`
- **活动 Manifest 端点**：`https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T072500Z-queryab597/manifest.json`
- **修订 manifest.json**：25,730 B / SHA256 `72822fa82b97dbdfcad89a1eeb24ca7d3ef18be2a82881180773450f3bc222df`
- **运行总字节数**：690,229 B
- **复用无损原件**：
  - `evidence/results.gz.json`：257,328 B / SHA256 `3f29e8dc9965978bbef9e7e0b7db1b509e4b6e119b9ebbd653368e91a29db270`（与归档 run 逐字节相同）
  - `meshes/` 12 个真实 chamfer mesh 文件：393,911 B（与归档 run 逐字节相同）

---

## 3. 证实元数据更正事实

依据 `readback-v1.json` 及真实 Capture / 源码，本次发布对派生描述与资格字段完成以下全部更正：

1. **Chamfer 参数与 API 语义更正（消除虚构 radius=0.1）**：
   - `local-blend.cylinder-rim-chamfer`：`range0 = 0.4`, `range1 = 0.4`, `radius = null`, `tolerance = 1e-05`, `markApi = PK_EDGE_set_blend_chamfer`, `fixApi = PK_BODY_fix_blends`；
   - `local-blend.cylinder-both-rim-chamfer`：`range0 = 0.4`, `range1 = 0.4`, `radius = null`, `tolerance = 1e-05`, `markApi = PK_EDGE_set_blend_chamfer`, `fixApi = PK_BODY_fix_blends`；
   - `local-blend.cylinder-unequal-chamfer`：`range0 = 0.3`, `range1 = 0.6`, `radius = null`, `tolerance = 1e-05`, `markApi = PK_EDGE_set_blend_chamfer`, `fixApi = PK_BODY_fix_blends`；
   - 两个 fillet 成品保持 `radius = 0.4`, `markApi = PK_EDGE_set_blend_constant`；
   - fillet 与 chamfer 的 mark 参数与 fix 阶段清晰分列，不把 mark 参数冒充 `fix_blends` 签名。
2. **510 探针分类（`query.probes-510-classification`）资格与状态纠正**：
   - Case 级 `status` 置为 `diagnostic`（非 `pass`）；
   - `performance.correctnessQualified` 置为 `false`；
   - Check 级标明 direct 15/16U 保持四方 On 分类未决（Unresolved），不伪填全过，亦不把 U 冒充 Different；与 overview 宏观记录保持一致。
3. **510 探针 Source 绑定纠正**：
   - `source.path` 绑定至真实计算/执行代码 `scripts/ShortEdgeBlendOracle.cs`，纠正原错误的 `scripts/ConeAxisProbe.cs`。
4. **Joint 物性只读重判（`mass.norm-reassessment-joint`）Source 与语义纠正**：
   - `source.path` 绑定至作者树真实入口 `scripts/ReviewMassCertification.cs@ab597`，纠正原引用的不存在脚本 `ReviewNativeQueryMath.cs`；
   - `operation` 更正为 `reassess-sealed-mass-reports`，API 明确为 `read-only sealed reports reassessment` 与 `sealed baseline reports readback`，杜绝任何新执行双边 GM/PK 积分的暗示；新 492V/588U 与旧 396V/684U 分列不改写历史。
5. **Linux 托管套件（`suite.linux-managed-2025`）PK 侧未执行纠正**：
   - 明确为 GM 托管单测套件单侧实测（`passed: 2025, failed: 0`）；
   - 删除伪造的 PK 侧 `dotnet test` API 与 2025 实测，PK 侧置为 `not-applicable`（parameters 为 null）；套件不计入 463 建模分母。
6. **物性释疑（FAQ）删除无来源断言**：
   - 彻底删除“严格求交/修剪/拓扑重构是 1e-8~1e-5 局部逼近”的普遍断言；
   - 严格按 3e7 独验结论表述：几何比较通过与 `BODY_check` 有效性仅证明实体在容差意义下自洽，不等于全域零误差和满足刚体积分误差预算；局部误差向全域体积分传播需要特定几何与法向约束；GM 与 PK 估计值极接近，但 PK 自身附带 modulus 模量界较宽导致外包围上界超标；新标准采用有量纲整体量归一；U 代表高确定性证明尚缺，不代表几何损坏。

---

## 4. 看板整体目录与状态总账

- **目录规模**：固定 463 具名建模用例（0 遗漏、0 重复，保持 UI016 分维状态大盘，绝不回到 9 卡增量首页）。
- **总账统计（分母 463）**：
  - `boundary`: pass=1, unresolved=1, untested=1, unverified=1
  - `family`: blend=277, boolean=186
  - `currentVersion`: not-retested=453, reverified=10（Projection 5 op + Cylinder Query 5 op）
  - `function`: pass=183, unverified=280
  - `mass`: verified=29, fail=1, unresolved=51, unverified=382
  - `performance`: observed=5, unverified=458
  - `query`: pass=5（五圆柱已修复）, na=5, unresolved=2, unverified=451

---

## 5. 静态发布与服务验证

- **容量受控**：
  - 打包文件数：632
  - 静态包总字节数：31,442,594 B（29.986 MiB）
  - 容量上限：31,457,280 B（30.00 MiB）
  - 剩余余量：14,686 B（严格满足 $\le 30\text{ MiB}$ 铁律）
- **数据与事件审计**：
  - `scripts/AuditOverviewEvents.cs`：19/19 全部 PASS（errors=0, notes=0）
  - `scripts/AuditProjectOverviewData.cs`：errors=0, notes=14
  - `scripts/BuildProjectOverview.cs --verify`：issues=0
  - `scripts/ValidateWebProgress.cs`：532 cases, 324 meshes, 99382 triangles, errors=[]
- **线上 HTTP 端点响应验证**：
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/` (HTTP 200, 855 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/project-overview.json` (HTTP 200, 823,016 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/index.json` (HTTP 200, 3,290 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T072500Z-queryab597/manifest.json` (HTTP 200, 25,730 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T072500Z-queryab597/evidence/results.gz.json` (HTTP 200, 257,328 B)
  - 原运行 `runs/20261112T132500Z-queryab597/manifest.json` 返回 HTTP 404（已干净移至归档）。
- **后台服务**：
  - `systemctl --user start gm-kernel-progress-refresh.service` 执行完成。
