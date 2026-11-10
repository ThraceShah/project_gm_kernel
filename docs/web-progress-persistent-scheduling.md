# Web 进度持久调度（按源变更优先的分批推进）

本文档描述 `feat/web-progress-scheduling` 分支新增的持久调度：把 registry 中已适配（`meshAdapter=true`）的全部 recipe 明确入队，按「源变更影响优先、其余公平轮转」分批推进，每轮真实测量结果在同 geometry/source SHA 下累计保留。不改几何算法/阈值、不改 Web viewer/样式、不重新命名或删 case；registry 只按现有 `source`/`meshAdapter`/`representative` 读取，其他 worker 扩 registry 时新 recipe 自动识别入队。

## 组件与契约

| 组件 | 文件 | 职责 |
| --- | --- | --- |
| 调度核心 | `scripts/WebProgressScheduler.cs` | 持久队列、影响映射、批次规划、结果分类、聚合清单构建、单实例锁、原子写 |
| 驱动 | `scripts/WatchWebProgress.cs` | 每轮计划一批、调 `scripts/RefreshWebProgress.cs` 串行执行、记录确认结果、发布最新聚合 |
| 校验 | `scripts/WebProgressDataValidation.cs` | 兼容放宽：case 证据可引用其它 measurement run（`../<runId>/...`），仍限定在 data 根的 `runs/` 树内且必须存在 |
| 门户打包 | `scripts/PublishWebProgressPortal.cs` | 裁剪只删「最新聚合未引用」的历史 run；最新聚合引用的测量 run 永不静默丢弃；超限显式拒绝并给出容量 |
| 单元测试 | `scripts/VerifyWebProgressScheduler.cs` | 小型可控记录的调度契约测试（35 项，离线、无 PK） |

`scripts/RefreshWebProgress.cs`、`scripts/PrepareWebProgressSite.cs`、registry、Web viewer 与样式均未改动。`scripts/schemaVersion` 1 的既有字段名与状态语义（`pass`/`fail`/`missing`/`unsupported`/`timeout`/`diagnostic`）不变；全部新信息都是**可追加字段**：`case.measurement`、`case.caseProgress`、`case.stale`、`run.accumulation`、`run.provenance.checkVersion`、`run.provenance.freshnessDefinitions`。

## 队列状态（`--state`，默认 `../temp_docs/web-progress/watch-state.json`）

JSON、单写者（`<state>.lock` 文件级排它锁，跨进程已验证拒绝并发实例）、原子写（tmp+rename）。保存：`sourceRevision`、`registryHash`、`registryVersion`、`workingDiffHash`、有序 `pending`（含 `reason`/`attempts`/`retryReason`/`lastRunId`）、`completed`（`runId`/`revision`/`status`/`attempts`/`completedAt`/`batchIndex`/`entryHash`）、`blocked`、`stale`（保留完整旧观测记录）、`batches`（每批 `running`/`complete`/`interrupted`/`contract-failed`/`aggregate-invalid` 与 case 列表）、`publish`（失败重试标记）、`lastRunId`。

- 重启/中断恢复：加载时把仍是 `running` 的批次标记 `interrupted`；只有被确认（写入 `completed`）的观测算完成，未确认项只重跑自己，绝不把未运行推进为通过，也不会重复整批已完成 case。中断批次的 run 目录（partial evidence）永久保留。
- 配方入队单元是 registry 里 `meshAdapter=true` 的 case；未适配注册不入队。新 registry 扩展（其他 adapter worker）自动入队（`reason=new-recipe`）；recipe 定义（source/arguments/parameters/api 等测量相关字段，`EntryHash`）变化使旧观测失效（`reason=recipe-change`）；删除的 recipe 离开 pending。

## 影响映射与失效（保守优先，绝不漏跑默认通过）

每轮对比 `state.sourceRevision..HEAD` 的提交变更 + 工作区/暂存 diff 文件名（`workingDiffHash` 变化才计入；未跟踪文件不入指纹，与旧 watcher 口径一致）：

- `src/**`、`third_party/**`、`tests/KernelTests/Fixtures/**`、`download-parasolid.sh`、共享测量宿主（`scripts/ParasolidScriptHost.cs`、`scripts/ParasolidBodyCompare.cs`、`scripts/WebProgressCapture.cs`、`scripts/Directory.Build.props`）→ 全部已适配结果过时并重验（通用 Native/共享 host 变化）。
- `scripts/<oracle>.cs` 命中某 case 的 `source` → 仅该 source 的 case 过时、优先重验。
- `scripts/web-progress-registry.json` → 按 recipe 逐条处理（上面的 recipe-change），不整体失效。
- `docs/**`、`web/**`、根级 `*.md`、`LICENSE` → 不失效任何测量（UI/doc-only 提交不重复全 oracle；每条结果仍以 `measurement.runId`/`measurement.revision`/`source.revision`/`sourceHashes` 精确追溯源码与几何来源，不存在伪标签）。
- 其余任何无法从 registry/sourceCoverage 断言影响的路径（含其他 scripts/工具、tests/ 非 fixtures、未知文件）→ **保守失效全部**。

过时（stale）case 在聚合里保留旧几何/检查/参数/API/日志展示，`status` 回到 `missing`（绝不冒充当前通过）、合同比率字段置空、旧合格比率存入 `stale.previousRatioGmOverPk`，附 `stale` 块（`previousStatus`/`previousRunId`/`previousRevision`/原因）与显式过时 note；重新测量后以新观测替换。

## 批次、预算与重试

- 每轮一个 `RefreshWebProgress` 调用（`--case <批次ids> --run-id <id>`，不带 `--publish-data-to`，由驱动聚合后再发布）：`--case-seconds 300`/`--seconds 1200` 预算、串行 PK、`--samples`/`--configuration` 透传；`--batch-size`（默认 4）控制每批 case 数，预算耗尽未启动的 case 保持 pending（不重复整批）。
- 排序：受影响（`affected`/`recipe-change`）先（representative 优先），再其余 representative，再其余按 suite 公平轮转；已完成 case 永不重复。
- 结果分类：API 真实 `pass`/`fail`/`timeout`（含退出 134）都是**完成的有效观测**（完成并累计，fail 比率为空）；attempted 但无结构化捕获（导出器编译/契约失败）**不推进完成**——保留原始日志与旧有效展示，`attempts++`、`retryReason=exporter-compile-or-contract-failure`，最多 `MaxAttempts=3` 次后 `export-blocked` 暂停排队（不饿死整队）；源变更或 `--force` 后重新排队。批次级契约失败（`validation.json` errors 非空）整批不推进、有限重试；聚合校验失败回滚到原始清单且不推进。
- `--force`：全部已适配 case 转 stale（`reason=forced`）重新排队，历史证据保留。
- 同 SHA 队列未完时下一 timer 继续推进（**不因 fingerprint 相同而永久 skip**）；只有「无事可做」才输出 `Source and registry unchanged; queue complete; ...`。
- 发布失败（`PUBLISH_FAILED`）保留有效 cache，`state.publish.pending` 使下一轮只重试发布、不重跑任何 case。

## 聚合视图口径（每轮发布最新聚合）

每批 run 目录保留两份清单：`collection-manifest.json`（采集器原始批内清单）与 `manifest.json`（聚合清单，viewer 消费）。聚合以原始清单为基底，逐 case 覆盖：

- `caseProgress.classification`：`current`（当前有效性下真实测量：同 SHA 累计或未受影响的精确携带）、`stale`（旧 SHA 真实历史、待重验，绝不当通过）、`not-run`（尚未运行，绝不默认通过）、`export-retry`（有限重试中）、`export-blocked`（重试用尽暂停）、`export-unadapted`（web 导出适配未实现，与真实内核能力无关；`kernelCapability` 保持 null）。
- `measurement`：`runId`/`revision`/`checkVersion`/`host`/`measuredAt`/`batchIndex`/`carriedForward`——每条结果保留其真实测量 run/宿主/提交/检查版本；跨 run 证据引用 `../<runId>/...`，参数/API/网格/检查/性能/日志引用不失真。
- 微小数值（如 `1e-7` 级 normalizedDelta）按 JSON 全精度保留；fail/stale 的 `ratioGmOverPk` 为空，旧合格比率仅存于 `stale` 历史块。不同 SHA 的结果以显式 stale 历史呈现，不与当前测量混同。

## 验证证据（本分支、qingyu2、Linux arm64、PK 38.0.150）

受控调度单元测试（35 项，无 PK）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyWebProgressScheduler.cs
```

日志与状态快照在 `temp_docs/web-progress-scheduling/logs/`（gitignored 验证产物）；受控验证数据在 `temp_docs/web-progress-scheduling/{generated,data,broken-*,interrupt2-*}`。

| 验证项 | 证据 |
| --- | --- |
| 同 SHA 多批推进 + 累加 | `logs/batch1-4.log`：b001 完成 `analytic-boolean.equal-overlap-subtract`(pass) 与 `local-blend.block-single-fillet`(fail，严格比较失败)，b002 完成 `mixed-analytic.basic-subtract`、`planar-boolean.overlap-two-axes-subtract`(pass)，b003 完成 `short-edge.standard-strict-lifecycle`(**fail, exit 134**)；聚合 `current=5`，各条带真实 `measurement.runId/checkVersion`，跨 run 引用 `../<runId>/cases/...` |
| 真实 fail/134 可记录 | b003：`RESULT short-edge.standard-strict-lifecycle: fail, exits=[134]`，完成并累计，比率为空 |
| fingerprint 相同不永久 skip | b002/b003 在指纹不变时继续推进剩余队列；`logs/skip.log` 仅在队列完成后 skip |
| 坏导出器不推进/不饿死/有限重试/保留证据 | `logs/broken-run*.log` + `broken-state.json`：受控坏 case（编译失败源）attempts 1→2→3 后 `export-blocked`（`retryReason`、`lastRunId`、失败 `oracle.log` 引用保留），同队 good case 正常完成、不被拖死；其后 skip 而非无限重试 |
| recipe 新增自动识别 | `logs/broken-run-recipechange*.log`：registry 追加 `planar-boolean.overlap-two-axes-subtract` 自动入队并完成 |
| recipe 变更失效 | 同上日志 + `logs/broken-state-midrun-7.json`：参数变更后旧 `pass` 观测转 `stale(reason=recipe-change)` 并重验 |
| 过时标记（保守失效） | tracked 工具源变更触发全量保守失效并优先重验（`SOURCE_CHANGED ... paths=4` 波段）；见下文 commit 后的真实 5-case 重验记录 |
| 半途中断恢复 | `logs/interrupt-run5.log`：批次运行中整体终止（等价 KillMode=control-group），批次停在 `running`、零确认、partial run 62 个证据文件保留；`interrupt-run` 恢复轮仅重跑未确认 case 并完成，批次历史 `b000=interrupted / b001=complete` |
| 重启只重跑未确认项 | 同上；单测覆盖 confirmed 保留/未确认不提升为 pass |
| 并发锁 | 单测同进程拒绝；`logs/broken-run1-concurrent.log` 与跨进程 `LockProbe`（holder ACQUIRED / 并发 REFUSED）验证单实例语义 |
| publish 失败重试有效 cache | `logs/` 发布序列：只读目标 `PUBLISH_FAILED ... validated cache kept, retrying without re-running cases`，随后正常目标 `PUBLISHED_DATA` 且无 case 重跑 |
| UI/doc-only 不伪造当前测量 | 单测（docs/web 路径不失效、携带结果 `measurement.revision` 保持真实旧提交）；commit 后真实验证见附录 |
| 未适配不排队 | 队列只含 `meshAdapter=true`；聚合 `export-unadapted=116` 与 `not-run`/`stale`/内核能力结论分离 |
| budget 不造成重复整批 | 未启动 case 保持 pending、已完成不重排（b002/b003 只推进剩余项） |
| 数据契约 | `MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateWebProgress.cs -- ../temp_docs/web-progress-scheduling/data` → `errors: []`（4 runs、1852 case 行、38 meshes）；`--self-test` 9 项仍通过；负例（跨 run 越界/悬空引用被拒）在单测内 |

真实 oracle 执行都在 `systemd-run --user --scope -p CPUQuota=200% -p MemoryMax=12G` 内串行进行，只跑了 2–3 个便宜 recipe + 1 个已知 fail recipe + 受控坏导出 fixture，没有全 347 重跑。

## 容量报告（静态门户 32 MiB 限制）

实测（本分支验证数据）：

- 单 case 证据 0.03–2.6 MB（`analytic-boolean.equal-overlap-subtract` 2.60MB 多步捕获、`local-blend.block-single-fillet` 0.03MB）；17-representative 历史 run 平均约 0.55MB/case。
- 每批 run 固定开销：463 条占位 manifest ≈ 1.1MB/批（`collection-manifest.json` + 聚合 `manifest.json`）。
- 全 347 已适配 case 累计聚合预计 **≈ 0.2–0.9GB 证据 + ~87 批 manifests**（约 0.55–2.7MB/case × 347），远超 32MiB。
- 构建后 `web/dist`（UI + 构建嵌入的 `web/data` 历史 + 4 个验证 run）= 32.3MB；按「最新聚合引用不可丢、其余历史从最旧裁剪」打包后 bundle = **28.2MB**（`PUBLISH_BUNDLE runs=6 bytes=28168616`），历史 run `initial-adapted-a1e4500` 因超出预算被裁（非当前证据，日志明示）。
- 结论：约 50–55 个便宜 case 或 20–30 个重 case 之后，最新聚合及其证据将超过 30MiB 打包预算；此时 `PublishWebProgressPortal` **显式拒绝**（`PUBLISH_REFUSED_BUNDLE_OVERSIZE`，输出实测字节），保持线上旧版本可用，绝不静默丢弃当前 case 几何/证据、不削 case/降精度。

### 给协调端的容量方案（需部署形态变更，review 后安排）

「安全压缩静态」不可行：门户上传限制按**解码后字节**计（32MiB），压缩绕不过；且 viewer 直接取 `data/*.json`，透明预压缩需要门户边缘配合。推荐（现有门户支持的持久形态，无需新后端框架）：

1. **持久静态代理/服务承载 data/**：在 qingyu2 用现有用户级 systemd 持久服务（或 `portalctl service`）跑一个只读静态文件服务，直接服务专用 checkout 的已验证 cache（`temp_docs/web-progress-publish/data`，完整历史、无大小限制），UI 仍以 `portalctl static` 小快照发布或同服务承载；路径仍为 `/apps/gm-kernel-progress/data/...`，viewer 无需改代码。`portalctl proxy --slug gm-kernel-progress --target http://127.0.0.1:<port>` 保留原子发布语义（cache 校验通过才切换索引）。
2. 或门户侧为该 slug 提高静态配额/挂持久卷（由门户 owner 决定）。
3. 过渡期行为：bundle 预算内保留最新聚合 + 其全部被引用测量 run（当前 case 证据永不静默丢），超出即拒绝发布并在 journal 输出容量报告；cache/git 保留全量历史。

### 剩余边界

- 每轮 `RefreshWebProgress`/`WebProgressPublishing` 会对整个 data 根做契约校验（历史行为），随 run 数增长呈 O(n²) 读盘；全量 347 覆盖前建议给校验器加增量模式（未实现，见上）。
- 463 条占位 manifest 的 ~1.1MB/批固定开销未优化；可后续裁剪占位字段（需 schema 兼容评估）。
- 影响映射按 registry `source` + sourceCoverage 保守判定；未来 registry 扩展的 source 自动纳入映射（按路径字符串），不重命名/删 case。
- 聚合清单的 stale/not-run 区分在数据层完整（`caseProgress.classification` + notes）；viewer 不改，因此侧栏计数仍按 schemaVersion 1 状态分桶（stale 计入 missing 桶，tooltip 显示 notes 为准）。

## 部署迁移步骤（review 通过后由协调端安排，本文不触碰 live）

1. 现有 `scripts/systemd/gm-kernel-progress-refresh.service`/`.timer` 命令行**保持兼容**（`--state/--registry/--case/--configuration/--samples/--seconds/--case-seconds/--output/--publish-data-to` 均沿用），首次启用持久调度即从 17 representatives 显式入队开始；随后把 ExecStart 的 `--case representatives` 改为 `--case all`（或删除该参数）即把 347 个已适配 case 全部入队（新参数 `--batch-size` 默认 4，1200s/300s 预算不变）。
2. 队列状态首次生成会重建为「全部已适配入队」；旧 watch-state 不迁移（旧 fingerprint 语义被 `sourceRevision/registryHash/workingDiffHash` 取代）。每个 case 首次观测按 representative 优先 → 公平轮转分批推进，约 87 批（10min timer ≈ 15 小时）覆盖全量。
3. `PublishWebProgressPortal` 的 drop-in 不变；容量超限会显式拒绝并保留线上旧版本（见容量方案）。
4. 验收：连续两轮 timer 日志应显示不同批次推进且聚合 `current` 累加；同 SHA 队列未完时不得出现 `queue complete` skip；杀服务后重跑只补未确认项。
