# qualifier v4 定向复审：接受五个已测 case 的 S1 采样门

**固定 v4 可用于已测五个 case 的少量 S1 正式配对样本。** 原4个资格漏判继续拒绝；v3 的换序跨 rep 统计错误已关闭，同 rep 配对与 rep0 cold 身份贯穿 FinishSample/Summarize。接受条件是完整 runner exit=0、`runSummary.overallQualified=true`、全部请求样本及原始产物齐备；不是完整性能目标达标，也不是其它 API/provider 自动取得资格。

已向协调端提前报告上述窄门结论。失败证据保存仍有一个明确限制：missing ms 被正确拒绝，但新 pairsByRep 的 NaN 会使 sample 序列化抛错；这种写出失败/partial report 必须判整轮无效，不能挑选部分统计作为正式结果。协调端已转实施者另作小修，本报告不追其后续 working diff。

## 固定源码与范围

- 核心提交：`e6b17358254c1e1a18f8a02df6e93fb2f5f2c134`；本轮构建/执行 HEAD：`1d341a4eee21ff0bd41156b7023b1508a1dae6d9`，后者仅修旧 report 的 expectedRepetitions→repetitions 回退。
- 对照 v3：`a8cae0455aca64c152ba1c850dc186c3fa8ed1ee`，原复审报告 `588af88affed39e82b0646e6db716b57d08270c2`；此前 OptA 报告 `7c31bf39fc52fc2380213a381535e7c9065e2ffb` 与 04c 报告 `830e7cdf22b646af297b41dc7560d20d7ac7853a` 均保留。
- `scripts/ModelingPairedBenchmark.cs` SHA256：`0e94929b17faf35ac977fe024560fee8703a229a64edb73173d7c2926cbf3d69`；GateVersion 为 `modeling-paired-benchmark-qualification-v4`。
- Native 与 `39c1dc8c037082b040defc7f3a202d6fa86a145b` 无差异，src tree 均为 `f1dc65ce5c7cf2e9ea42e3832e8352147a5c2e1b`。不重复 SkipLocalsInit 源码证明、60 tests、25 boxes、M4 JIT，不合 main、不部署。
- 环境 SDK `10.0.302`；本轮有效验证只构建/执行 BCL JSON 工具，不需要 PK/schema。未在 Mac 写文件或启动采样负载，未运行 full tests / NativeAOT；不将原 S0 负载污染数据当成性能结论。

## 配对到统计的闭合

`FinishSample:214-258` 保留原 GM/PK 行顺序数组作诊断，但按 timing.step 分组、在同 sample 内要求唯一 GM/PK partner 后生成排序的 pairsByRep。Qualify:273-367 仍先验证 step registry、精确预期 API、全 timing 行、rep0…N−1 每侧恰一行、总行数、API error、有限正时间、完整且唯一的 required check ID，required status 必须为 pass。

`Summarize:398-447` 的正式统计只读 pairsByRep，以 rep=0 为 warm process 的 cold，rep>0 为 warm。对不合格样本的 raw Zip（406）只生成拒绝诊断字符串，不进入任何被接受的比率/中位数；未发现其它正式 report 路径重新按原两个数组的位置配对。当前五个明确声明的单操作工作负载中，一个 step 对应一个 rep；这不是任意多 API scenario 的通用解释。

独立临时脚本 `ReplayV4.cs` 从固定 runner 原文生成，只改变脚本目录解析及增加提前返回的 review 入口，并加禁止进入普通 runner 的 guard；从 `static Verdict Qualify` 到文件尾逐字相同。FinishSample 也原文保留。探针读取原文件，不为通过重建负例或改变期望。

| 定向重放 | 独立实际结果 |
| --- | --- |
| 原4反例 | 全部 qualified=false，原字节 hash 不变 |
| 原 v3 换序 capture | qualified=true；cold=GM10/PK1=10，warm=GM100/PK10=10 |
| 全24种 timing行排列 × 正/反2种step排列 | 48种 cold/warm summary JSON逐字相同，0失败 |
| 两 sample 使用相同 rep IDs、第二份 GM 时间加倍 | warm sampleRatios=[10,20]，没有跨sample串配 |
| processExit=9 / timeout=true | 都不合格 |
| requested2 / attempted1，或 attempted2但失败1 | RunOverallQualified=false；完整2/2/0失败为true |
| 原代码自检 | exit0，30条fixture/helper结果均ok：27条sample verdict/order检查，3条aggregate/invariance检查；不是照抄“24项”声明 |

自检仍覆盖漏侧、重复 timing owner、孤儿 owner、重复check ID、缺required、required diagnostic、unknown kernel、非正/非有限时间、API error；合法换序的期望仍是通过并得正确统计。合成 checks=pass 只证明 JSON 合同，不冒称这些合成 body 做过真实几何比较。

正常 runner 的 expected samples 由 resolved cases 和冷/热请求数导出（118-169）；每 sample 保存 expectedRepetitions / 实际 pairs 数及失败原因，最后按 attempted / qualified / unqualified 计数，缺样本或任一不合格使 overall=false 并 return4（183）。Summarize 可以保留成功子集供诊断，不能用其局部 qualified=true 覆盖 runSummary 的否决。

## 原输入指纹与两个重验 scope

原4文件从固定 `7c31bf3` 的 `docs/reviews/advanced-modeling/opta-f2-evidence/` 按原字节取出；原换序文件从固定 `588af88` 取出，且与候选新增 test fixture 字节相同：

| 文件 | SHA256 |
| --- | --- |
| no-steps.capture.json | `ac792b98371b194bda183f64db6f5ba23cce858a60fb7d3dfde3a1910e797e60` |
| unparseable-step-ids.capture.json | `9927834289880f065b7c55ad67ee5005442b57ff53530d884404d080ca3b0956` |
| diagnostic-primary.capture.json | `5cd88e26ff020e4c30f35fd961fb34b3253453d51881319886eae4c9cdc6ea48` |
| unknown-timing-row.capture.json | `ebb51edf630f02157791db4e8537fa63485110d4ac51a5975ac14afba84940f1` |
| reordered-timings.capture.json | `133cfb50e758e43267d90bb4ef9205de3164c114eff7e37b1d701a9268a63a55` |

独立 `CliReplay.cs` 调用固定源码构建出的 BCL runner DLL，**49个实际CLI子进程，0个与预期退出码不符**：

- 原20份capture逐份 `--revalidate`：20/20 exit0；原20份 immutable report sample逐份 `--revalidate-report`：20/20 exit0。
- 原4反例经 `--revalidate` 分别 exit5；原换序正例 exit0。
- 从原report派生、另存的 processExit=9、timedOut=true、旧 repetitions错误各 exit6，不能被 capture pass 掩盖；未知 processExit=null 以未处理 NullReferenceException exit134 拒绝，**没有假填0**，但尚非规范化 exit6诊断。

capture-only 入口（58-65）确实仍传入0/false作为过程状态占位，但输出明确写“original process exit/timeout NOT verifiable”，所以仅接受 capture-contract 语义，不把它拿来证明原进程退出。full-run入口（43-52）读取原report的processExit/timedOut、原oracle.log、expectedRepetitions或旧repetitions，不从当前capture计数倒推预期；旧字段回退没有使错误count通过。

这些重验依赖原report/log的可信来源与保留，属于审计重放，不是外部输入的认证或通用schema fuzzer。不得把缺/坏report所致的134当作合格，也不得据capture-only成功恢复未知进程资格。

## 历史45对：实际1521 checks，未重新执行 API

只读 SSH 获取 Mac 原 `gm-modeling-performance` 下 `temp_docs/modeling-performance/runs/20261111T003330Z-610e3ce/benchmark/` 的 report、20 capture与20 oracle.log；在本树 ignored history 中保留相同相对结构。report SHA256仍为 `97fb02559286cc4b1a1aeb0641c547a3e64daeeb6492037eac46984ac3abee51`，与04c及v3审查一致；20个oracle.log SHA256逐份匹配原report，不改历史文件。

独立数原文件得到 **20 capture / 45 pairs / 1521 checks / 0个check.status=diagnostic / 20个log hash匹配**。原cold样本每份1pair，warm进程每份6pair；分别用协议确定的1/6核对旧report.repetitions。Qualify helper的capture-contract与真实process-state两种模式均20/20合格，上述40个CLI重放也都通过；不是1512，也不据声明照抄1521。

required check的diagnostic仍不能通过；`n_cht_pts` 是 strict equivalence helper 内部允许的细分诊断，不是把整项required equivalence.status改成diagnostic。原数据全check status为pass，因此目前空的diagnostic白名单没有误杀这批数据；不据此保证其它未来diagnostic/provider。

原provenance仍是610源树与当时未提交的调查插桩、Mac M4 / SDK10.0.400 / runtime10.0.11；本轮只是把此前按owner人工接受、v3机械复核的同一历史数据，再用固定v4 gate重验。没有把旧速度改叫OptA39结果，没有新45次API运行或新Mac性能测量。

## 非伪通过的失败落盘限制

`FinishSample:237` 对 pairsByRep 的毫秒值直接 JsonValue.Create；SafeMs 对缺失/不合法时间返回NaN。Qualify虽然已拒绝，但样本随后序列化仍会抛ArgumentException。独立probe在原合法换序capture的derived copy仅删一条ms，得到：

```text
MISSING_TIME serialize=ArgumentException qualified=false
```

这不能产生错误qualified正式结果，但可能丢失最终runSummary/完整失败报告；自检仅检查对象verdict并未覆盖此序列化阶段。建议单独把非法值序列化为null并保留拒绝原因，不能删掉拒绝条件或放宽finite门。S1使用条件必须包括原始文件、完整report及正常exit；异常、缺产物、partial stats一律不采信。unknown process的exit134同属拒绝诊断待规范化，不把它误报成exit6或成功。

## 命令、日志与实施边界

有效执行命令（均在本树，未发布任何服务）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ModelingPairedBenchmark.cs -- --validate-fixtures --output temp_docs/perf-qualifier-v4-review/selftest
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/perf-qualifier-v4-review/GenerateProbe.cs
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/perf-qualifier-v4-review/ReplayV4.cs -- --review
MSBUILDDISABLENODEREUSE=1 dotnet build scripts/ModelingPairedBenchmark.cs -c Release -o temp_docs/perf-qualifier-v4-review/runner-bin -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/perf-qualifier-v4-review/CliReplay.cs
```

五项最终均exit0；CLI的expected 5/6/134是上文负控子进程真实退出码。独立工具与日志在 ignored `temp_docs/perf-qualifier-v4-review/`。原byte fixtures始终只读，派生变体另存。

**审查工具失误单列：** 初版GenerateProbe的字符串匹配未注入review入口，误进入默认launcher并启动本树的AnalyticBooleanOracle构建子进程；发现后按实际PID精确终止自建进程树，未取得或采信任何新API样本，不归因于候选。保留 `setup-uninjected.log`；修正后加缺注入即throw和禁止普通runner入口guard，再完成上述有效BCL重放。没有远程Mac执行、没有杀其它worker/testhost/publisher；不把这次误启动说成正常JSON验证。

| 证据 | SHA256 |
| --- | --- |
| ReplayV4.cs最终诊断副本 | `166030907b61160d1b406d86a3420ee73269e61ad71ba9a177a3aeff9e00c475` |
| review.log（含逐capture/log hash） | `94e42767268ea003a0e445ca8a8b8cb34ff9feaba5a5db21e71cb7dd14aef23a` |
| cli-replay.log | `05c8c2332f70f20b1683748cae320c83271b96f5cde87b49ff203c6ef80c4271` |
| selftest.log | `234b7c621b7c4e0733d670ec2176b54c5120d67a8bd728bdbfb8ad17bbe9a6de` |
| setup-uninjected.log | `915d94084ceee1ddf6ac1cfd5dbc51a20a8a26e2caca7b06604f83f72077408f` |

S1下一步由实施者在同host/config、同v4 runner、同输入参数与原API边界下做短ABBA，记录cold/warm身份、raw与负载；仍需对全部样本正确性与产物完整性验收。五case之外、任意输入、全provider性能、OptA实际收益、完整建模GM/PK≤1目标均不由本轮接受；无需等待收益完成才交付这份gate审查。
