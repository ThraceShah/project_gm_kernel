# 性能交付验收快照发布回执（6761600 + review 80bde347）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。本回执记录第二条完整交付（性能线）的实际上线范围、来源与限制。与计算 run `20261111T130936Z-f9c59201` **分源、不混签**：两者是不同源码的不同 run，不构成同一 kernel 集成。

## 上线内容

| 项 | 值 |
| --- | --- |
| 线上 URL | `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/`（同 slug） |
| 新 run | `20261111T133805Z-perf6761600`（immutable，latestRunId 已切换） |
| 性能来源 | `gm-pk-performance-delivery`：candidate `6761600fef20602342cd9dc81bd4f7e4fb85d939` / src `01e03aa9ed10a8145556872e98fa1677e17a4cb7`（生产 `03931f6`）；controlled baseline `ecfff3aae1dbbe9dd48adbbdf2e8e635da735938` / `e314cc2307c5e77ea6f2b7685ba17836ae4ef5c8` |
| 接受报告 | `gm-pk-performance-final-review` @ `80bde347f293314e2627120e376828e0ed5b6162`，`docs/Pk_Performance_6761600_Independent_Review.md` |
| 采集/装配来源 | 本树 `feat/modeling-delivery-board` @ `a08585ea0930f9a1540510326905dcb509d26b4a`（`scripts/AssemblePerformanceAcceptance.cs`；仅装配/展示，测量全部来自 Mac 原包，Linux 不重测） |
| 原始包 | mac-modeling-performance-6761600-raw.tar.gz `85be8a3e…`、mac-modeling-quality-6761600.tar.gz `f51e1cde…`、mac-modeling-baseline-failures.tar.gz `0c93eb4e…`；raw report.json SHA256 `0b867277…` |
| 数据版本显示 | 根入口 `数据版本 6761600 · 采集 2026-11-11T13:38:05`；逐 case `source.revision=6761600f…`；测量 host=Mac M4（run.host 如实） |

## 显示范围（准确上线范围）

8 个 case（6 通过 / 2 失败），全部值从 Mac 原 report.json 派生并与接受报告表值逐项一致性核对通过：

- **五操作冷/暖全值与样本范围**（20 进程/60 pairs 全部原值在 `evidence/mac-report.json`）：shortedge -37.2420% / 1.5934226541x（22593.31015→14179.10690ms 暖中位），GM/PK 配对中位 **9465.56563x 仍远未达 PK 目标**；planar **+30.334% 回归未归因保留**（区间重叠、zero Jacobians）；sphere -0.40%/0.28479x、curved -0.81%/488.26694x、solidblend -1.00%/1064.27179x（小变化不计实质收益）。每 case 标注合格口径：同 host ABBA、每源 2 冷 + 4 暖、profiling 开启、默认 assembly、rep0 进程冷、rep2-only XT、**不签 f9c59201 event-arrangement 或整体 PK 目标**。
- **工作量收益计数**（独立显示，非 CPU 结论）：residual-directions 约 1.01e7→2.78e4（rep-1 对照）、candidate record-builds/nodes/rebinds/jacobians/directions 与两源保留计数；明示非 99.7% 算术/CPU 删除。
- **质量结算（红保留）**：Mac 全量 suite **1875/1877、2 失败（NotConverged×2）、原外部 exit 1**；同 Mac 基线对照两测项同样失败（既有失败归类，不是新增回归但保持红）；启动 DLL 哈希缺口（missing）保留；12 记录测试 suite 内通过、最大容量测试独立一次通过（exit 0）未并入 1877。Linux 1885 与 Mac 1877 不互签。
- **整体**：`perf.overall-pk-target` 显式 **PK 目标未达成**（curved 488.27x / solidblend 1064.27x / shortedge 9465.57x）+ planar 回归 fail + suite 非全绿 fail；范围排除（一般模型加速、下游组合、f9c59201 接受、PK 达标、全绿 suite、合 main/产品部署）写入 notes。

## 发布流程与原退出（owned 流程，工具固定 1b935972，UI 固定 015，同 release `ui-015-tools-1b93597-data-f9c59201`）

1. **容量方案 A（协调者已批准，relay_791894409d7b）执行前完整归档**：两个中间重复 main610 快照（`20261110T191615Z-4e60884b`、`20261110T193022Z-c5a1c582`，同一 17-case 数据 14过/3败 的连续自动再生成）404 文件 / 18,094,468 bytes 全部字节一致归档到 `~/.local/share/gm-kernel-progress-ui/releases/ui-015-tools-1b93597-data-f9c59201/retired-data-archive/`（含 `archive-manifest.json`（SHA256 `f3258e77…`）+ `archive-sha256.txt` + 归档前 index，整体冻结只读）；原件同时保留在 `work/previous-snapshot`（核验过的发布前整包）。板上保留最终 main610 run `20261110T195008Z-610e3ce8` 可查。
2. 暂停 timer、确认 writer idle 后执行数据根切换：移出两快照、`PublishWebProgress` 发布性能 run；`ValidateWebProgress` 0 错误（489 case/152 mesh/3 runs）。
3. `PublishReviewedWebProgressUi --mode publish --execute`（同一 fixed 工具/release/hook 参数）：UI_PREPARED 287 files / **13,494,421 B ≤ 30MiB 门**，portalctl 发布 + 逐文件 HTTPS 核验，fingerprint `015cd308…:f0593760…:10920347df93cd202a07cd35da9272113a270e13657f9923f1b98b87b1ac6695`，原 exit 0。
4. 浏览器真实验收 **11/11 通过**（真实 HTTPS、自有 Chromium）：latest=性能 run、数据版本 6761600fef206、shortedge 37.2420/1.59342/9465.56563 全可见、合格样本中位与口径显示、planar +30.334% 保留、suite 红（1875/1877/NotConverged）显示、PK 目标未达显示、**计算 run 历史与红值仍可查**、real-uv 体验保留、390px 无溢出、无 JS/CSS 失败。截图与 `browser-perf-receipt.json` 在 `temp_docs/cutover-verify/`。
5. timer 恢复原 active+enabled（未 enable/disable）。
6. **实际自动轮**（2026-11-11 22:10:13 CST）：ExecStartPre pin 校验 exit 0、ExecStart 快照验证 exit 0（489 case/0 错误）、ExecStartPost 输出 `UI_PREPARED files=287 bytes=13494421` → `UI_SKIP_SAME includes UI identity`，无回退；线上抽查 index/性能 run manifest/mac-report/suite-receipt/计算 run 回执/历史 main610 manifest/display-manifest 全部字节一致或 200。

## 限制（如实）

- 合格倍率为同 host 配对中位数（接受口径）与中位数之比两种语义并存显示；两者均真实，字段级倍率=中位数之比，接受值 9465.56563x 在标题/检查项。
- 小样本（每源 2 冷 + 4 暖）；profiling 开启（合格计时含 profile 开销）；load average 不证明排他 CPU；planar 回归原因未归因；suite 2 失败为同 Mac 基线既有失败（保持红）；启动 DLL 哈希缺口保留。
- complete-suite.trx（2.75MB）未内联（哈希 `fce633b0…` 锚定于 run-receipt，原件在私有 incoming 目录）；XT 正文与原归档不公开；rep2-only XT 未签给其它 reps。
- 本 run 是测量结果展示，不代表候选已合 main/集成/部署；与计算 run 不构成同一 kernel 集成。

## 回退

回退目标仍为本次更新前真实 015 整包（`work/previous-snapshot`，含移出前数据根原字节）。回退命令同计算回执；若回退，性能 run 与移出快照恢复由 `retired-data-archive/` + previous-snapshot 原字节支持，均有哈希清单。
