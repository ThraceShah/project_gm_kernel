# 计算交付验收快照发布回执（f9c59201 + review 4c7646e）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。本回执记录第一条完整交付（计算线）的实际上线范围、来源与限制；性能线为另一独立 run，另行发布，不与本 run 混签。

## 上线内容

| 项 | 值 |
| --- | --- |
| 线上 URL | `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/`（同 slug） |
| 新 run | `20261111T130936Z-f9c59201`（immutable，`data/runs/20261111T130936Z-f9c59201/`） |
| 建模来源 | `gm-pk-native-delivery` @ `f9c59201c0b9c5d103dee45124e3f43ca59a0921` |
| 接受报告 | `gm-pk-native-final-review` @ `4c7646e1085e6fa786b2f335cabc7c086c008e13`，`docs/reviews/advanced-modeling/pk-native-final-review.md`（SHA256 `829027ad0c125036dbb1b40c8c7ca29f29bfa2859109d9acc519b853107b2685`） |
| 采集/装配来源 | 本树 `feat/modeling-delivery-board` @ `d10c48793c2debfe17f467f76497b2e1b06d87f3`（`scripts/AssembleComputationAcceptance.cs`；仅采集/装配/展示适配） |
| 同实例显示采集 | 隔离 detached 工作树 `gm-board-capture-f9c5920` @ f9c59201（clean），报告标准命令 `--output-name=board-f9c5920-standard --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership`，原 exit 0 |
| 数据版本显示 | 根入口 `数据版本 f9c5920 · 采集 2026-11-11T13:09:36`；逐 case `source` 绑定真实 runner 与 f9c59201；混合来源（建模/接受/采集）在 run.provenance 分列 |
| 发布 UTC | run 生成 2026-11-11T13:09:36Z；实际 publish 2026-11-11T13:16Z 前后（CLI 原 exit 0，见下） |

## 显示范围（准确上线范围）

18 个 case（13 通过 / 4 失败 / 1 诊断），全部与原证据绑定：

- **已过项**：96 self Fix 参数矩阵（8 shard 各 exit 0、`PASS cases=12`、96 名单锁定）、全量回归 1885/1885（原 exit 0）、独立求交 116（原 exit 0，wire 合同范围 + native corrupt-wire 边界单列诊断）、标准 self 生命周期（同实例 5 步双侧 mesh + 275 检查全过 + compare/mass/BODY_check）、同字节双刀 Boolean（输入 sha256 绑定、normalized15=7.701450779942389E-11）、修改支撑面保孔、等价支持链、非邻接带、输入刚体/镜像代表、七角点变体、五个轴向倒圆/倒角、comparator 6 正例+5 负例。
- **原始红项保留**：`short-edge.transverse-two-cuts`（首切 normalized15=1.2044611267910417E-07、再切 1.126468644220207E-07，均 worst=inertia.yz，原门槛 1E-7 不变，原 exit 134；不反向撤销独立通过项）；`probes-510.self-replay`（510 探针 15 差异）与 `probes-510.import-replay`（16 差异）（native_class_changed=0、零 API 错误、原 exit 134）。
- **整体**：`m0-m5.overall-delivery` 显式 **未完成/整体验收仍红**；未测范围记 missing；正式性能验收 missing（Mac 线另行交付）。
- **性能口径**：本 run 全部倍率 null（看板显示“尚未录入基准性能测试数据”）；Linux 诊断耗时（含 1885 的 19.5104 分钟墙钟）只作诊断，不作性能。
- **注入回滚**：`boolean.inject-rollback` 为预期失败试验（GM 901 回滚），记 diagnostic，不计操作通过。

## 发布流程与原退出（owned 流程，工具固定 1b935972，UI 固定 015）

1. 新 immutable release：`~/.local/share/gm-kernel-progress-ui/releases/ui-015-tools-1b93597-data-f9c59201`；`release.json` SHA256 `384f8addf4cce2e0e241cb21b0430f8e8b3c602142071d67996e79857a691ff6`；uiHash `f0593760…a4dd6`（18 UI 文件与 15 display/real-uv 资源随 UI 保留）；`previous-static` = 本次更新前真实 015 整包（treeHash `8dca4b3500652fe0ff5161a8a5943abdcc5a69d0a40b472de17e95b1ca583069`，非历史 UI424 备份）。
2. `PublishReviewedWebProgressUi --mode backup --verify-served`：当前 served 625 文件逐文件 HTTPS 核验一致后原子快照，原 exit 0。
3. `--mode prepare`：数据闭包（1407 case/400 mesh）验证 0 错误，容量 **683 files / 31,445,488 B ≤ 30MiB 门（余 11,792 B）**，`dataIndexSha=591fb3dc…610bd`，原 exit 0。
4. `ConfigureReviewedWebProgressUiHook --mode install`（ownedUpgrade，previous-release=015，pin `b86ccc13…`/`2388a513…`）：完整 drop-ins 原子备份、timer 原 active+enabled 状态记录后停表、hook 换指新 release 工具；原 exit 0。
5. 自动入口固定：producer checkout 钉到 `d10c487`（detached clean）；base unit 的 main-tracking ExecStartPre/旧采集 ExecStart 替换为 pin 校验 + `ValidateWebProgress` 快照验证（原配置备份于 `temp_docs/cutover-backup/`，原 unit SHA256 `87fd999e…`）。
6. `--mode publish --execute`：fingerprint `015cd308…:f0593760…:e5226764927f6982165fb797346c5e7a6ca3410e688d9bc55f12f69d9ec282c1`，portalctl 发布 + 逐文件 HTTPS 核验，原 exit 0，`published.json` 已写。
7. 浏览器真实像素验收 **12/12 通过**（自有 Chromium、真实 HTTPS、无全局代理改动）：新 run 默认可见、双侧真实模型三角形与非背景像素、PK compare 摘要、横切红值 1.204461127e-7/1.126468644e-7 显示、倍率未知如实显示、M0–M5 未完成显示、历史 main610 run 原 case（102 triangles）仍可查看、real-uv U5/V5 体验保留、390×844 双 pane 无溢出、无 JS/CSS 失败。截图与 `browser-receipt.json` 在 `temp_docs/cutover-verify/`。
8. `--mode resume` 恢复 timer 原 active+enabled（未 enable/disable）。
9. **实际自动轮**（2026-11-11 21:22:49 CST）：ExecStartPre pin 校验 exit 0、ExecStart 快照验证 exit 0（1407 case/0 错误）、ExecStartPost 输出 `UI_PREPARED …` → `UI_SKIP_SAME includes UI identity`，无回退；线上 latest 仍为本 run，15 display 资源保留。

## 容量与证据完整性（透明记账）

- 内联原 log（脱敏显示派生物，原件字节保留）：transverse/probes-self/probes-import（红）、full（1885）、intersections（116）、regular-bore、corner、support-chain、separated-network、analytic-local、comparator-controls、consumer×3、matrix-96 名单、原证据索引 726 条；同实例采集全部（mesh/capture/compare/oracle.log）。
- **未内联（引用锚定，未删除）**：`standard.log`、`input-rigid.log`、`input-mirror.log` 与 8 个 matrix-shard log，共 8,247,791 bytes；逐文件原路径/大小/SHA256 在 `data/runs/20261111T130936Z-f9c59201/evidence/evidence-manifest.json`，对应结果值/exit/摘录在记录与 `evidence/operations-summaries.json`。原因：固定 30MiB 静态包容量门（本次发布仅余 11,792 B）；若要求全部内联，需为本 run 额外提供约 8.25MB 包容量或改定交付范围。原件保持在 `../gm-pk-native-delivery/temp_docs/pk-native-delivery/` 与 `evidence-final.sha256`（SHA256 `94752804…`）锚定的 726 条原索引内。

## 回退

回退目标 = 本次更新前真实 015 整包（`ui-015-tools-1b93597-data-f9c59201/work/previous-snapshot`，servedVerified）。先 `PublishReviewedWebProgressUi --mode rollback --execute`（新 release 工具），再 `ConfigureReviewedWebProgressUiHook --mode restore --execute`；base unit 与 producer checkout 从 `temp_docs/cutover-backup/` 与 `d10c487` 原状恢复。不擅 enable/disable timer。
