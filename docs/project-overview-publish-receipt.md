# 项目整体进度看板 · 负责人自测发布收据（独审由用户取消）

- 候选：3968948b60b9412ae1d046b5328d1618f7aa3a21（含 e8756517 授权记录）
- 发布性质：负责人自测发布，独立 review 由用户明确取消（见 docs/modeling-delivery-board-handoff.md）；历史独审收据不冒称本次接受。
- 自测（不重跑已过控制，列既有实证）：
  - 数据控制：AuditOverviewEvents 19/19 PASS（T0–T8+T4h/T4i/T4j），errors=0/notes=0；AuditProjectOverviewData errors=0（14 notes=事实注记）；BuildProjectOverview --verify 0 问题；目录 463 稳定、映射 195 条、无绿无证据。
  - 关键交互：步骤落位 5/5（首击 unequal-chamfer→operation-5、reload 仍落、跨 run rep2-display、同 run rim-fillet→operation-1）；overview 统计/筛选/证据/分维/模型跳转浏览器累计 27 检查通过（temp_docs/cutover-verify/browser-*-receipt.json）。
  - 构建：web tsc --noEmit+vite build 成功；overview JSON 哈希见 release.json files 清单。
- 状态/身份检查与失败事实保留未放宽；数据格式不变、无历史兼容。
- 发布明细（URL/哈希/自动轮）由本收据后续补记段给出。

## 发布明细（owned 流程，2026-01 UTC 时间戳见下）

- URL：https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/（默认首页=项目整体进度目录）
- 新 release：`~/.local/share/gm-kernel-progress-ui/releases/ui-016-tools-1b93597-overview`（tools 1b93597 不变）；release.json sha256=c3cdaaa80a1d298a8d9980d092faf0d992de70e7168e6f581fbbaca4e8dd2972；uiHash=ab2f234054493f1dec7dc7c4e8b73f57feea991fba0e4dbbedc25d053eed10ee（非 data 树）；回退基线 previous-static=发布前 served 整包（treeHash 4085b75cae49fbc7c0090e082d7b6b1ff831fd58560729364777a999a66d7d3a，backup --verify-served 核验一致）。
- UI/source/producer：UI 源=3968948b60b9412ae1d046b5328d1618f7aa3a21（ui-commit 钉）；收据/自测身份（review 位）=4b3e37d4c681c8dbaf2ceb152012f2ba95491281；producer 钉 1b6106428a89d1a320f69372f83693987efb19c4 未变；容量 28,323,027B（<30MiB 门；portalctl 32MiB 硬限）。
- 流程：prepare（UI_PREPARED 596 文件）→ backup --verify-served（treeHash 与 previous-static 一致）→ ConfigureReviewedWebProgressUiHook install（owned upgrade，前代 release/state 钉齐）→ 显式发布轮 → 线上验收 → hook resume（timer active）→ 自动轮 `UI_SKIP_SAME includes UI identity`（无重复发布、无回退）。
- HTTPS 核验：index/data/project-overview.json/data/overview-events.json 全 200；线上 JS assets/index-BxSiSvou.js 哈希 6536846c1571357c015d3eb6137984b139484e04f38169e9363957815fb056da 与本地 dist 一致。
- 真实浏览器（线上）：6/6（默认整体目录+463 计数、分维统计、首击 unequal-chamfer→operation-5 真实落位、返回目录、390px、无 JS 错误）；收据 temp_docs/cutover-verify/browser-live-receipt.json。本地预验收累计 27+5 检查（统计可复算/筛选/证据/模型/落位/刷新不重置等）。
- 15 display/real-uv 资源：随数据闭包保留（runs 明细未动）；历史原件/归档/回退快照保留。
- 剩余问题（如实）：510 self15/import16/native7 分类未决、五圆柱 BodyContainsVector 5000 查询缺口、FaceAttach 未测、curved roles 缺、mass Unresolved 62 行、Mac 套件 2 NotConverged、M0–M5/PK 性能目标未达、sphere +11.178% 性能回退观察保留。
