# 本轮计算与性能成果接收、发布

负责人持有两条完整交付，按完成事件分别发布到 [现有看板](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。本文件只是交接与执行约定；当前没有收到本轮固定候选、接受结论或作者完整产物，不代表任何一条已完成或已上线。

## 一次交接所需输入

协调者分别转交下面两条来源的完整交付包与明确接受结论。作者提供现有真实产物，不需要另建交接平台；未执行项写明未执行，缺失项写明缺失。

| 来源 | 任务 | 最小输入 |
| --- | --- | --- |
| q2 计算 | `task_3315264d0556` / `ctx_b6fbbccd3586`，树 `../gm-pk-native-delivery` | 完整修复原 M0–M5 的固定代码 SHA、接受范围与证据位置；实际执行产物、操作及组合结果、原始日志和退出码；代表操作的真实显示产物，或允许按固定接受版本采集的准确命令与输入 |
| Mac 性能 | `task_79920db63952`，树名 `gm-pk-performance-delivery` | 固定代码 SHA、接受范围与证据位置；同一 Mac 上 GM/PK 的真实 runner、模型和参数、配置与测量范围、预热及每个 raw sample、采集时间、实际退出码及正确性资格；工作量计数与实际耗时结论分别说明 |

两条都需给出：

1. **源码身份**：完整 SHA、实际源码树/dirty diff 与哈希、runner/输入/参数哈希及相关依赖版本；若运行时存在未提交改动，原样说明，不能用 commit/tag 名代替实际 source。接受说明固定到这一份候选，不以“分支最新”替代。
2. **原始执行**：命令 argv、工作目录（项目相对路径）、host/架构、SDK/runtime、PK 与绑定版本、环境准备结果、UTC 开始/结束、每个进程原退出码、超时和未运行范围；原日志和 raw 文件位置、大小与 SHA256。日志脱敏另存派生物，原件保留。
3. **逐操作验收**：primary 结果与组合整体分开；每一步 API/error/result、参数/容差、输出实体、receive、完整 BODY_check、有效 mask 的 BODY_compare global/local/diff、必要物性或 finite 边证据。后续 Boolean 失败只归属实际失败步骤，不能反签上游 blend 失败或通过。
4. **同次显示来源**：真实 GM/PK before/after/tool mesh 与 DisplayBody receipt，绑定同一次实例的 mesh/XT hash、阶段、输入和源代码；XT 原文、厂商库及凭证不公开。没有 GM 产物时明确缺失，不能由 PK 重建代替。
5. **性能口径**：工作量收益可以独立报告；倍率必须来自同 host、同模型/参数、同 runner/范围的合格 GM/PK 样本。Mac raw 字节不改，不以 Linux 重测覆盖；缺失或不合格倍率保持 null，旧冷样本不充作本轮优化证据。

若作者已产出完整当前 `index.json` / `runs/<id>/manifest.json` 包，直接提供包与 SHA256；否则提供上述原产物，由本负责人仅做必要的当前记录装配/采集适配。双方以实际交付的当前记录格式为准，不新增旧 schema 兼容。收到后一次核对缺口；不在等待期间轮询作者终端或重复跑模型。

## 已读代码与部署基线

准备分支为 `feat/modeling-delivery-board`，起点 `ff5fecf913a5f86340ecb2399b303b55cbee124e`。已读实际全局规则、本树 AGENTS、架构、Capture/Refresh/validator/publisher、UI contract/Header/Performance/compare/loader 与 owned 工具源码；本树起始 diff 为空。

部署依据为 `../gm-reviewed-ui-upgrade/docs/web-progress-ui-015-deployment.md` 及其 receipt，工具固定 `1b935972eff529e13b46cb9b08ad9550e0a6aeb5`（接受 `d32e862dc947702d4655912efc916d5c035df04b`），UI 固定 `015cd308e147356fecac96bb7354ab1e92c36a03`（接受 `f9ed821287c7bfb4e23963a10238c9de32a3854c`）。不使用老 UI424 作为本次回滚目标。

2026-11-11 本机只读预查：

- 当前 owned release 为 `../../../../.local/share/gm-kernel-progress-ui/releases/ui-015-tools-1b93597`；release 清单 SHA256 为 `b86ccc134a3aa631387255743de1318d6b8e2868567cb739f36421d273524c2d`，hook state SHA256 为 `2388a51366ae2186e2f763f3198096a9c18726c547cb42a09dbd29af8edb7276`。
- prepared 记录为 625 文件 / 28,214,939 bytes；18 个 UI 文件 hash 为 `f0593760ed1e22dd73e94f609e9e2353366370b542019a90cb8f486f796a4dd6`，其中 15 个资源在 `display/real-uv/`。这些是本机状态及既有发布回执，尚非本轮重新逐文件线上验收。
- timer 为 active/enabled；读取时 service inactive/dead。实际完整 drop-ins 为 `publish.conf` 和 `zz-reviewed-ui.conf`，effective ExecStartPost 只有固定015 owned publisher；未变更 timer、unit、release 或门户。
- producer 是 `../../../../code/project_gm_kernel-progress-publisher` 的 clean main610；原 ExecStartPre 仍 fetch/fast-forward main，ExecStart 仍运行 `WatchWebProgress.cs --case representatives`。因此新成果静态上传本身不足以防止下一轮回退。
- 30 MiB 预算仅余 3,242,341 bytes，32 MiB 硬限仅余 5,339,493 bytes。每次新包先核容量，不以裁删原始证据或引用通过容量门；不足时保留候选并报告具体所需空间和交付范围。

本树 `scripts/WebProgressCapture.cs` / `scripts/RefreshWebProgress.cs` 尚是旧 case 汇总路径，未携带 operation-layered 记录与同次 DisplayBody receipt；`web/src/types/contract.ts` 也未声明操作记录。已有 operation-layered 实现在其它固定候选中，不能未经接受就移入并冒称本轮完整结果。拿到实际接受的完整来源后，复用其有效记录实现，只修改必要的当前格式展示/验证，不新造数学模型或 provider。

## 每次事件的执行通路

1. **固定输入**：保存协调者接受结论和包哈希；从接受版本及实际原产物生成独立 immutable run。当前 run 只声称自己的完成范围，另一条未完成时保持未完成；历史 run 原字节保留。多源版本在每操作/样本来源分别标注，根入口不冒称全部来自一个代码版本。
2. **必要采集**：优先直接接收作者包；确需补 q2 显示时，先按接受版本准备匹配 private runtime 并实际取得 ENV_READY，只采必要代表。沿用 `RefreshWebProgress.cs` / Capture / 已接受操作记录实现与原参数，不跑全347、不触 Mac 正式计时、不做 NativeAOT。
3. **装配与验证**：沿用 `ValidateWebProgress.cs`、`WebProgressDataValidation.cs` 和 `WebProgressPublishing.cs`；补足当前操作记录和同次 receipt 的必要验证后再装配。检查原 exit、mask、未知/null、mesh/receipt/source 绑定及全部引用；验证器退出0不能替代建模进程成功。现 validator 只验证旧展示结构闭包，不能独自认证新操作成果。
4. **固定自动来源**：在第一次发布前，将实际自动入口的 main-tracking pre-steps、采集 source、data root 和 owned UI hook 一并固定到已接受实现与已完成数据快照；保留 frozen015 UI-owned display 目录。若计算与性能版本需要合并，使用最小整合分支、明确两个来源，经协调者确认一次有意义的整体验证；不合 main，不拿不同源码的结果互签。不能只改 post hook 而保留会覆盖新 run 的旧采集入口。
5. **备份与切换**：发布当次重新读取 current release/state/hash、完整 drop-ins/effective argv 和 timer enabled/active 原状态；暂停 timer 后等 writer idle，不 kill 用户进程。完整当前 served 包及 timer/unit 状态原子备份，沿用固定1b owned 流程，新 work 与回退包指向本次更新前的真实015整包；不复用015 release 内那个历史 UI424 backup。
6. **同 slug 发布**：沿用 fixed `PublishReviewedWebProgressUi.cs` 的 prepare / backup / publish 路径及正常 TLS、direct HTTP 策略。先核数据闭包、完整容量和 immutable UI hash，再原子发布 `gm-kernel-progress`；不迁 host/proxy，不修改全局代理或凭证。源码改动验证后先 commit+push 当前工作分支，发布 receipt 后续单独保存。
7. **线上闭环**：真实 HTTPS 读取 HTML/JS/CSS/index/manifest/新 mesh/log 与 receipt；浏览器检查真实模型像素、PK 原摘要/raw 差异、未知倍率、源版本、主模型、独立 UV 与手机交互。随后通过实际同一自动入口执行一轮，核新 run 不回退、15 display 资源保留、原 timer 状态恢复；不以HTTP200或unit文本替代此轮实际结果。失败回滚本次备份的整包及完整配置并核实，不擅自 enable/disable timer。
8. **事件交付**：用户可见且自动轮核验通过后，立即向协调者报告“数据已发布”及准确 URL、run/source、UTC 时间、提交/推送/接受/上线状态与限制；不等待另一条。两条都完成才发最终 worker_done，中间无事件即结束 turn 等消息。

## 当前待交接

**已交付（2026-11-11，两条完整）**：

1. 计算线：run `20261111T130936Z-f9c59201`（f9c59201+review4c7646e）已上线并经协调者独立核验接受；红项/原 exit 照实、M0–M5 未完成。回执 `docs/computation-acceptance-publish-receipt.md`。
2. 性能线：run `20261111T133805Z-perf6761600`（6761600+review80bde347）已上线（latest）；shortedge -37.2420%/1.59342x/9465.56563x、planar +30.334% 保留、1875/1877+2 既有失败+exit 1、work 计数全部显示；与计算 run 分源不混签。容量方案 A（协调者批准）：两个重复中间 main610 快照已归档至 release `retired-data-archive/`（404 文件/18,094,468B 哈希一致冻结）后移出 served 根。回执 `docs/performance-acceptance-publish-receipt.md`。
3. 性能显示补齐：run `20261111T142551Z-perf6761600`（latest）为性能成品：五操作各 1 step 真实 GM/PK before/after/tool 模型（85be 原包 rep-2 字节副本，与 rep2 XT 对应），`evidence/display-binding.json` 全绑定，step 标题明示显示单样本≠60 pairs 统计；三报表 case 明示无模型；浏览器 9/9 真实模型像素验收。
5. 6616 测量线：run `20261111T183127Z-alg6616`（270e2b6→6616cd6 + 独审 1806fd58）已上线（latest）：五操作 rep2 真实模型（material-binding 原字节、shortedge 来源纠正=PK seed XT）、冷暖全值双 ratio 语义分列、回退/离群照实、点值门物性边界（无模量三态）、1912/1914 红保留、不签功能集成。owned release 切至 `ui-015-tools-1b93597-alg6616`（回退基线=本次前 served 整包），producer 钉 e80ae2b；浏览器 13/13、自动轮无回退。回执 `docs/measurement-6616-publish-receipt.md`。

待未来事件：功能整合（dfc5defe/1887e30d 当前 Capture 合同，功能 owner 单管）验证通过后按当前格式接 run；性能总目标继续推进。
4. Blend 线：run `20261111T174315Z-blend60be`（60be7216+独审 54f1b309）已上线（latest）：六显示 case 同实例补采模型、查询缺口红（20 点全 5000）、mass/510/FaceAttach 未决照实、不签 Boolean/性能/总目标。owned release 切至 `ui-015-tools-1b93597-blend60be`（回退基线=本次前 served 整包），producer 钉 86e1716；浏览器 11/11、自动轮无回退。回执 `docs/blend-60be-acceptance-publish-receipt.md`。Boolean 4794 已独审接受但集成固定产物未到，待交接。

owned 发布/release `ui-015-tools-1b93597-data-f9c59201` 与自动入口钉定（producer d10c487+快照验证）不变；两轮自动轮均 UI_SKIP_SAME 无回退；浏览器验收 12/12+11/11 真实像素通过。
