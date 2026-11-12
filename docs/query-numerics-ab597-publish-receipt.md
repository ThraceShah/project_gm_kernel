# 项目整体进度看板 · 负责人自测发布收据（Native 查询与物性规范化 ab597 成果接入）

- **交付标题**：负责人自测发布（Native 查询与物性规范化 ab597 成果接入项目整体进度目录）
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
- **原始结果与机器入口**：
  - `docs/query-numerics-closure/results.json`: 5,247,186 B / SHA256 `aed7c9787a03bbb798f7a7be0f9f3b832400c15956af205a7b55662e4866bd60`
  - 归档至本看板运行证据：`runs/20261112T132500Z-queryab597/evidence/results.gz.json`（无损 gzip 压缩为 257,328 B，SHA256 `3f29e8dc9965978bbef9e7e0b7db1b509e4b6e119b9ebbd653368e91a29db270`）
  - 交付清单：`source-manifest.json` 与 `artifact-manifest.json`（279 项制品）
- **顶层版本状态**：
  - 顶层标头更新为：`"功能/物性ab597与性能cc47871分源汇总"`（不冒充合一内核，两源分列注明）。

---

## 2. 核心事实更新与数据验收

### A. 五圆柱成品与查询缺口闭环
- **5 具名目录行**：
  1. `local-blend.cylinder-rim-fillet`
  2. `local-blend.cylinder-both-rim-fillet`
  3. `local-blend.cylinder-rim-chamfer`
  4. `local-blend.cylinder-both-rim-chamfer`
  5. `local-blend.cylinder-unequal-chamfer`
- **验收事实**：
  - 直接 GM `BodyContainsVector` 查询在 20 真实点全 0 通过（20/20 四方同 byte，PK 0，rawExit 0）；
  - 完整 `copy-delete-source`、`mirror-scale`、`reload` 均通过；
  - 接触拓扑面修复通过（100/200 尺度）；
  - 5 项成品的当前 `query` 维度均从历史 `fail` 更新为 `pass`，当前版本复验更新为 `reverified`；
  - 历史 60be 20 点全 5000 缺口保留在各行 history 中，真实记录不被篡改；
  - 边界条目 `boundary.bodycontains-axial` 状态置为 `pass`。

### B. 510 Probes 探针
- direct self: 495 Verified / 15 Unresolved / 0 Different，0 API error；
- direct import: 494 Verified / 16 Unresolved / 0 Different，0 API error；
- native 自差 7 行（102, 153, 204, 254, 289, 390, 441）；Capture 另 339=8 行；
- 9 点由有限 plane 区域上界证明精度内；62 半/双距离派生控制相等；15/16U 显著可见；旧 self 其余 495 原四列缺失保留明确 missing；
- 边界条目 `boundary.probes-510` 与验证记录 `query.probes-510` 均如实保持 `unresolved`。

### C. 物性合同验收与 Joint 72 历史报告重判
- **物性合同规范**：`relative-volume-mass-area; cog-Euclidean/radius-of-gyration; inertia-Frobenius/trace; reference-scales-with-outward-bounds`；
- **三态门限**：上界 $< 10^{-7}$ Verified，下界 $\ge 10^{-7}$ Different，其余 Unresolved；
- **圆柱物理量**：五解析圆柱 10 份报告全部为 Verified；一般曲面 1e-7 目标仍未认证；
- **Joint 72 历史报告重判分列**：
  - 主 900 分量：312V / 588U → 348V / 552U；
  - 回读 180 分量：84V / 96U → 144V / 36U；
  - 总 1080 唯一分量：396V / 684U → 492V / 588U / 0D；
  - 净变化：144 项 U → V，48 项 V → U（均为 12 volume / 12 mass / 24 cog.z）；
  - 规范声明：最多五整体物理量，不宣传 1080 独立认证。
  - 登记于验证记录 `mass.contract-ab597`。

### D. 物性数值释疑（FAQ）
- 登记于验证记录 `mass.faq-geometry-vs-mass`：
  1. 严格几何求交/修剪与拓扑重构是局部逼近（$10^{-8} \sim 10^{-5}$），不等于全域刚体积分误差自动受控；
  2. 误差传播需具体几何前提（全域单射法向与曲率控制），现有通用 compare 不自动给出；
  3. 现不少实体 GM 与 PK 估计值差很小（$10^{-10}$），但 PK 自带 modulus 模量误差界较宽（$3 \times 10^{-7}$），严格外包围导致上界超标；
  4. 新标准采用有量纲依据的整体量规范化（体积/Rg/trace），无随意放宽；
  5. U（未决）代表当前积分误差界无法出具高置信度证明，不代表几何损坏，也不代表物性已全解决。

---

## 3. 看板整体目录与状态总账

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

## 4. 静态发布与服务验证

- **容量受控**：
  - 打包文件数：632
  - 静态包总字节数：31,438,252 B（29.982 MiB）
  - 容量上限：31,457,280 B（30.00 MiB）
  - 剩余余量：19,028 B（无损满足 $\le 30\text{ MiB}$ 铁律）
- **服务自测与端点响应（HTTP 200）**：
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/` (855 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/project-overview.json` (822,283 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/overview-events.json` (20,931 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/index.json` (3,260 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T132500Z-queryab597/manifest.json` (22,199 B)
  - `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/data/runs/20261112T132500Z-queryab597/evidence/results.gz.json` (257,328 B)
- **刷新服务**：
  - `systemctl --user start gm-kernel-progress-refresh.service` 执行成功。
  - 数据校验 532 cases, 324 meshes, 99382 triangles, 0 errors。
  - Portal 自动部署与发布验证全部闭环。
