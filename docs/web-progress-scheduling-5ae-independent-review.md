# 调度 5ae2cb5 独立复审（2026-11-11）

**结论：accepted，可接受此固定调度代码进入后续 main 整合。** 本轮原两项缺口均有独立复验关闭；在限定的调度、数据保真与受影响公共入口范围内，没有发现新的阻断问题。这不授权本 reviewer merge/部署，也不代表 hosting 或整个内核目标完成。

候选 `5ae2cb53573dda56533b3ddb7ec46b5fc878edd0`，对比上一拒收 `05dcdda794f2aaff081e95b2709257b02f43d5de`；安全 fetch 后 main 仍 `610e3ce844bf99c85af6069905f9f3deaab08133`。分支 `ThraceShah/gm-scheduling-5ae-review` 只提交本文。原 A/B 报告、分支与所有 ignored repro 保留；本轮没有审 B 的进行中修复或实现工作树漂移。

## 原两缺口关闭

| 项 | 未改原输入/真实入口 | 原退出码与观测 |
| --- | --- | --- |
| workingPaths 元素错型 | 原 `temp_docs/scheduler-gemini-review/Checks.cs` 原文复制至新目录，生成合法 NewState，仅 `workingPaths=[123]`；真实 Watch `--once --case no-such-case` | **2**，STATE_SCHEMA_INVALID / non-string workingPaths elements；文件前后 SHA256 同为 `75ef863ce43e79f1ce6629811a7f54f780614b85caafeb3c05efc3b95b7376e7`，原文未改；不是通过 Watch 的过滤器静默丢弃错误元素 |
| pure pending 退休期间配方变化 | 同原 Checks：导出失败2次→eligible=false→只改 radius 0.1→0.2→eligible=true | Checks原退出**0**；实际 reason=recipe-change、attempts=0、有当前entryHash，旧 lastRunId/retryReason 保留；首个新配方失败不blocked，等价于新周期attempt1 |

证据：`temp_docs/scheduler-5ae-review/checks.log`、`typed-driver.log`、`workingpaths-before.sha` 与未改的 `workingpaths-typed.json`。

本轮实现先在 LoadState 逐项拒绝错误 workingPaths 元素，再保留 Watch 的防御性筛选；未以 catch-all 或“过滤后继续成功”代替拒绝。Pending 新持久保存 entryHash，active 与 retired pending 的配方身份变化进入同样的新周期，重试历史与真实测量来源不升级成当前通过。

## 旧 pending 迁移与独立组合检查

新增的 reviewer 临时 `temp_docs/scheduler-5ae-review/Audit.cs` 不进入提交，直接调用固定候选 helper，共 **20/20、原exit0**：

- 旧有效 pending 没有 entryHash 时可加载，LoadState 不编辑原文；第一次 Reconcile 绑定当前 recipe hash并保持未变化的attempts2，重复无变化 Reconcile 仍是2，不会无限重置重试。
- 这是一次身份绑定，不声称可以从缺失字段恢复未知历史配方。实际旧版本→本候选有 scheduler 源变更，驱动的保守源失效会先建立新周期；独立验证了这条旧pending升级的失效/绑定顺序。后续 recipe/source 变化可比较已持久身份。
- pending 退休期间分别无变化/source/force/recipe：保持过滤时plan始终为空；恢复后无变化保留attempts2，三种失效情形为attempts0，lastRunId/retryReason均保留，未产生completed/current。
- workingPaths数值元素、quarantined.errors数值元素、retired.pending.attempts字符串、batch.cases数值元素均明确 InvalidDataException 拒绝且输入字节不变；没有吞真实I/O或静默重建坏状态。

源码核验了初次入队、配方更新、MarkChanges、RecordOutcome、force后Reconcile、Restore与quarantine后的pending创建路径：新周期在后续确认前绑定entryHash，旧 valid pending的一次绑定不被当作几何观测；source/force/recipe恢复不会篡改旧revision/checkVersion/host。这里验证的是当前实际消费与生成格式，不宣称支持任意未来schema或人为损坏状态的自动恢复。

## 回归与构建证据

下列命令均带 `MSBUILDDISABLENODEREUSE=1`，所有关键 file-app 均使用 `--no-cache` 强制重建：

```sh
dotnet run --no-cache --file scripts/VerifyWebProgressScheduler.cs
dotnet run --no-cache --file scripts/VerifyWebProgressWatchUpgrades.cs
dotnet run --no-cache --file scripts/ValidateWebProgress.cs -- --self-test
dotnet run --no-cache --file temp_docs/scheduler-5ae-review/Checks.cs
dotnet run --no-cache --file temp_docs/scheduler-5ae-review/Audit.cs
dotnet run --no-cache --file scripts/WatchWebProgress.cs -- --once --state ../temp_docs/scheduler-5ae-review/workingpaths-typed.json --case no-such-case --output ../temp_docs/scheduler-5ae-review/output
```

前五项原退出码均 **0**，数量分别 **145、31、12、原反例诊断、20**；最后一项预期并实际退出 **2**。日志位于 `temp_docs/scheduler-5ae-review/`。源码SHA记录在 `audit.log`：Watch=`795ca9a5e782e74de5a2a9c42c93ec65281c53ae191369ac881a33df7bbc18b1`，Scheduler=`d49e1d8fa43812a691362a9316cbbecfe9c1dbe4424a3014f17c699fee716bc0`。

原闭合项按受影响回归覆盖：

| 项 | 本轮覆盖与边界 |
| --- | --- |
| S1 legacy/损坏拒绝 | legacy备份/安全重建、未知schema/集合/深层记录拒绝由145检查与独立负例覆盖；未将坏state当空队列 |
| S2 staged/revert | 31 driver实际运行nested git与Watch/Refresh，含staged、dirty→clean、rename/delete及恢复 |
| S3/S5 选择/新周期 | 原Checks、20独立检查及145中active/retired/block/stale组合通过；未选择case不执行 |
| S4 quarantine | 145 helper与31 driver继续覆盖坏indexedrun隔离、保留raw evidence、后续推进；未弱化原validator标准 |
| S6 provenance | 145保持真实checkVersion记录/来源/unknown回退、旧revision与微小数值保真；未制造新几何来源 |
| S7 closure/公开入口 | 145包含原公开Publish与完整闭包、新/旧目标、失败保留旧index；本次diff未动Publishing |
| S8 source/dest链 | 本次diff未动SafePath/Portal；145保留链接拒绝，上一固定05dc候选真实Portal父链接退出1及外部完整元数据/内容不变的证据继续适用；未重复无改动大文件打包 |
| S9 有限失败 | 31 driver覆盖非零collector与零退出缺manifest的有限失败；原退出码与blocked分流保留 |
| S10 发布磁盘state | 31 driver继续跨进程验证成功pending=false/下轮skip、失败pending与reason/time持久，未重复oracle |

SDK正常Compile Include依赖追踪已在前轮独立证明，本轮不重新归因缓存；使用新临时脚本路径与强制重建避免前轮fixture产物误用。Managed构建仍有既有trim/AOT分析警告，本轮没有C ABI改动，也未运行NativeAOT；不能把此检查表当AOT发布认证。

## 接受范围与仍待gate

本轮只读review；没有修改生产代码、测试、原fixture/gate、registry/CSV/数值门槛或case状态，没有运行真实PK、347全量、无关kernel、Windows或新agent，没有启动host/新端口，没有操作其他工作树、live publisher、用户unit、portal/timer或main。所有本轮CLI/测试会话均已结束，未清理其他worker或全局testhost。

本接受仅针对**固定5ae调度候选**及上述复验范围。B hosting 上轮 `df701b7` 的 H1b对象完整性/H4b完整镜像历史仍是独立阻断；本轮不审其未来修复。后续仍需合并时保留两个Directory.Build.props include、完整PublishClosure→host generation接口、schema/provenance和独立生成/发布/激活state，完成专用checkout与旧状态迁移演练，再由协调端安排同slug Tailnet代理、健康与回滚部署验收。线上维持既有main610e3ce；不能据调度接受宣布容量host已接受、全case几何通过或内核M0–M5完成。
