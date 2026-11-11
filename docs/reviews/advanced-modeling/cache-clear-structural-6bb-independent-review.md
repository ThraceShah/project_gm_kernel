# cache.Clear 空读集结构方案独立审查（固定 6bb）

**结论：有限接受最小生产实现方向；尚无生产 guard 候选通过验收，也没有加速结论。** 逐源码检查未发现“同一 nodes 的 FirstUv/SecondUv 全负”仍读取此 cache 的隐藏路径。可以从固定生产基线另做一次局部 guard：仅在 IntervalJacobian 原清零点检测同一 nodes；有任一非负 UV 索引则保留完整 clear，否则跳过这一次 clear。不要把诊断 state、计时、SessionStop dump 带入生产，也不扩大到 hoist、稀疏 Ready 重置或更多 SkipLocalsInit。

固定分析对象 `perf/zero-clear-diagnostic@6bb013eddb6cb9795bf56865c9b20434630b5937`，基线 `0ba5a1e7021b9d8bf0e20bfc2aed1ad20a76c7ba`；仅 caller SkipLocalsInit 的 Native39 接受范围继续保留。阅读原独立报告 `1d82cc75` 的 `docs/reviews/advanced-modeling/zero-memory-attribution-0ba5a1e.md`，不把本轮 q2 x64 BCL 检查混称为 M4 JIT/原配置性能结果。当前 review 分支 `ThraceShah/gm-cache-clear-structural-review-6bb`，旧 UI/host/scheduler 分支及 ignored 证据保留。

## 最小可执行源码合同

仅将 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs:376`（基线对应 `:368`）的原调用改成：

```csharp
if (HasAnyUv(nodes)) cache.Clear();

// 同一 partial 类中的私有、零分配谓词；不能只查 root/node[0]。
private static bool HasAnyUv(ReadOnlySpan<Occurrence> nodes)
{
    foreach (var node in nodes)
        if (node.FirstUv >= 0 || node.SecondUv >= 0) return true;
    return false;
}
```

这是提供给实现者的合同，**本 reviewer 没有修改生产源码**。6bb 的诊断文件已有同名谓词；从无诊断的生产基线另加时应只有一个定义，不引入诊断依赖。所有非负值都使 guard 为 true，不把合法 UV 缩成某前缀/某一侧/根节点；非法 program 原有的错误行为也不能被额外过滤掉。

保持不变：`NestedBlendBounds.cs:227` 的 `ReuseSplineContactBounds=false` 清零；`:176` RHS、Assemble matrix、所有导数 seed/reset、输出/局部初始化、Ready/key/Values 写入顺序；求解、证明、容差、预算、成功/失败状态与所有原检查。不要删除 cache allocation、把清零改成 n 项、只清 Ready、跨 program 保存状态，或改成一次 Bound 只清一次。条件扫描本身有成本；本次不测也不承诺净收益。

**首个实现不 hoist。** 从源码看 BoundSegment 的 nodes 生成后不再修改，理论上可缓存谓词 bool；但这会增加参数/传递一致性义务，当前逐 Jac 局部检查已自含。尤其不能把“hoist 布尔判断”混成“有 UV 时把 clear 提到 Bound 入口”：boxes 每轮变化且失败重试，原完整失效屏障必须保持逐 Jac 执行。

## 源码空读集证明：全部直接访问与后继消费者

全仓检查 `IntervalSplineContact`、`SplineContactMatches`、`CacheSplineContact`、`IntervalSpline(` 的引用，不只照抄候选文档表格。cache 类型为私有值类型；生产 cache 在 BoundSegment `:182` 为局部 stackalloc[MaxVariables=63]，只向以下私有同步调用转发，不持久化/返回/保存到 program/global。测试 hook `NestedBlendIntervalSupport.cs:98` 使用另一份独立 slot 并保留自己的 clear，不是这个 guard 的目标。

记 `R(nodes) = {node.FirstUv, node.SecondUv | 索引 >= 0}`。所有 cache 元素寻址都在此集合中；`uv+1` 是变量/参数槽，不是 cache 邻槽键：

| 实际源码 | 访问与前置守卫 |
| --- | --- |
| `NestedBlendBounds.cs:384`–388 | 遍历同一 nodes 的两侧；`uv<0` 立即 continue，之后才 `SplineContactMatches(cache[uv],...)` / `ref cache[uv]` fill |
| `NestedBlendIntervalSupport.cs:83`–89 | 唯一其他生产元素读点：`cacheIndex>=0 && !cache.IsEmpty && SplineContactMatches(...)` 才取 `.Values`。数值计算读的是匹配 hull；未命中另算局部 hull |
| `NestedBlendIntervalSupport.cs:23`–34 | matcher 先检查 Ready；fill 成功才写 key 并最终 Ready=true，失败直接返回，不能靠旧 Ready 跨请求复用 |
| `JointNestedBlendEvaluation.cs:483`–486 | IntervalNormal 所用 SplineUv 只返回该 occurrence 的 FirstUv/SecondUv 或 −1，无法制造第三个 cache 索引 |

后继调用逐点核验：

- **Jac 本体/Residual**：Jac 的列循环 `Bounds:392` 仍传原 nodes。`IntervalResidual:150`–175 倒序遍历同一 nodes；两侧 UV 循环 `:166` 跳过负索引；`IntervalNormal:43` 遇负 SplineUv 返回 Unsupported 后才能到 `:44` 的 IntervalSpline。analytic 和 blend 分支不读 cache。即使 surface 与该 occurrence 不匹配，也只能 Unsupported，不能借别的 program/index 读旧 cache。
- **NearestBoxes**：`Bounds:229` 传同一 nodes/cache；`:348`–349 的 Normal 只选同一数组中的 root，`:356`–362 的 UV 循环同样先排除负索引。selected/map/RHS 是另外的 scratch；本方案不改其 clear。
- **Frame**：`Bounds:322`、`:325` 到 `:414`–421 的两个 Normal，仍为同一 nodes occurrence0、同一 cache。两次不同导数 seed 不改变 R。
- **curvature/acceleration**：`Bounds:278`–279 的两个 Normal 仍为原 nodes occurrence0；其后只操作已产生的 interval vectors。可选分支不会另取 cache 键。
- **support U/UV section**：`Bounds:269` → `NestedBlendParameterBounds.cs:54`–74；两次 Normal 使用同一 nodes 的 child 索引，只缩小 occurrence 选择，不增加 R。supportParameter 的返回/输出不携带 cache。
- **witness/reach/anchor**：`Bounds:208`–213、`:230`–234 的 require/capture 只读写 boxes；reach `:242`–258 使用 boxes/独立 RollingBallReach，不接收此 cache；anchor `:309`–315 使用 Taylor snapshot 的 SpineSupportParameters，不转发 cache。BoundExplicit 的独立路径不由本 guard 改动。

**同 nodes/生命周期**：`EvaluateCore:83`、`:114` 为每个 occurrence 初始化两侧 UV=−1；`:94`–108 在实际 spline 分支建立 UV（同 basis 可共享）；`:178` 将活跃 nodes/counts 复制给 BoundSegment，后者 `:163` 建立唯一活跃 span，后面只有读取。代码没有把这个 cache 传给另一个 nodes/program/generation。

**失败**：Jac 的 fill/Residual 失败分别在 `Bounds:388`、`:392` 返回。expansion 的调用点 `:196` 只在成功后使用 Jacobian，否则 continue；refinement `:219` 失败立即退出 Bound。一个有 UV 的新 Jac 无论上轮成功还是 failed-fill，都先完整 clear。无 UV 时两处元素访问均不可达，后继的相同 R 也为空；已有垃圾/旧 program 同 ScalarOffset/旧 Ready 因而不可观察。失败离开 Bound 后局部 cache 生命周期结束，未找到跨程序逃逸点。

因此证明的是“**此局部 cache 的未清字节无人读取**”，不是“所有 scratch 不必初始化”或“所有 case 无 UV”。任何以后新增不经上述 UV 守卫的 reader 都会使本证明失效，必须重新审计。

## 一次既有诊断：计数闭合，范围与因果须修正

协调端只读复制了既有 M4 证据，本轮没有访问 Mac 发起任何新执行。成功 `run2.log` 原 SHA256 `afada1746a0178e5c72b0cb7319c68e7f1ece0b0273b04f29533b69f0ac90c80`；原记录 UTC **2026-11-11 03:49:21–03:49:49Z**，原 `RUN_EXIT=0` / `PASS cases=1 mode=independent-construction`，case=`a1-r0.5-t1E-07-reverseFalse-default`。早先 `run.log` 是 allocation guard 拒绝的编译失败，不能算第二份成功诊断或建模失败。候选文档 §4 的“2026-01-11”与原日志不符。

| 原始统计 | 独立复算 |
| --- | --- |
| Jac entered / clear reached | 1,446,213 / 1,446,213 |
| Jac succeeded / failed | 1,446,093 / 120，和等于 entered |
| expansion / refinement | 843,699 / 602,514，和等于 Jac entered；每个 loop body 一次 Jac 与源码一致 |
| n=3、uv=False | entered9,191 = success9,071 + fail120；ticks3,029,297 |
| n=7、uv=False | entered1,437,022 = success1,437,022 + fail0；ticks500,843,578 |
| 两桶总 ticks | 503,872,875，等于 total；频率1,000,000,000，所以约503.873ms原始跨 clear 时钟 |
| owner / dump / foreign / out-of-range | 1 / 1 / 0 / 0（仅记录条件，非无竞态的通用线程证明） |
| Bound entered / evaluated | 224,299 / 224,299 |
| recorded Bound succeeded / derived remainder | 100,402 / 123,897，算术闭合，但不能直接认作真实成功/失败分类，见 C2 |

此 session 只出现无 UV 桶；结合上面的源码后继证明，可以说**这批记录到的 Jac 请求适用空读集方向**。不能推广所有 workload，更不能把120次失败计数说成120种失败路径覆盖。

按已知63×352布局计算 `1,446,213 × 22,176 = 32,071,219,488 bytes`，约32.1十进制GB，**只能叫请求清零字节合计**。不是实测 DRAM 流量、cache traffic、已证物理零写入或带宽；不据此估计速度。当前 proposal 的“32.1GB零写入/实际工作量”应收窄到这一口径。

### C1 · 当前不是文档/旧 smoke 所称的 TLS 聚合与退出保证

`NestedBlendBoundsAttribution.cs:46` 为一个全局静态 AttributionState；`:52`–58 是非原子首 owner 认领/拒绝其他线程，不是 ThreadStatic。并发首次触达可竞争，foreign 增量也非原子，所以 foreign=0 不能独立证明任意并发执行无丢失；本日志的解释依赖既有单计算线程会话约束。

实际 dump 仅由 `KernelRuntime.Memory.cs:267` 在 SessionStopCore 入口调用，甚至早于 session 有效性检查；源码没有 ProcessExit、UnhandledException 注册，也没有 dump-once 或 session reset。重复 Stop 会重复输出，下一 session 也会继续累加。正常 oracle 的 `finally`（`ShortEdgeBlendOracle.cs:302`）能覆盖普通成功与会走该 finally 的异常；Environment.Exit、FailFast、native 崩溃、信号或跳过该 finally 没有这里宣称的退出 dump 保证，输出本身也未被隔离为不可失败操作。

`scripts/ClearAttributionSmoke.cs:79` 起测的是**另一套** ThreadStatic ThreadState/List/lock/ProcessExit 模型，不能用其跨线程聚合/exit134兜底结果验收固定6bb的真实实现。本轮17项 BCL probe 直接编译固定 attribution 源，已实证 foreign thread 被拒、连续 dump 输出两次且不 reset；不需要重跑旧5M次开销测速或故意崩溃进程。

### C2 · BoundSucceeded 漏了可选成功出口；全 session 也不是 FIX 独占

`NestedBlendBounds.cs:296` 的 acceleration、`:305` 的 curvature 都可能返回 Success，却没有 BoundSucceeded++。所以“4个成功出口已全覆盖”“失败=entered−succeeded不会漏计”不成立；100,402 至多为已挂钩出口的成功数，123,897 包含未挂钩成功的可能，44.7%不能无条件称总成功率。602,514/6=100,419 与 recorded100,402 差17，**单靠差值无法区分后续失败还是漏钩成功**，本报告不臆造其分布。Jac 三个正常返回点则均已挂钩；未预期异常仍可能导致 entered 与返回计数不闭合，不能泛称异常自动无损。

计数从进程首次触达累计到 SessionStop，未在 FIX 开始/结束取 delta。原 runner 的 session 同时包住准备/receive、native 产品经 GM reexport、GM Fillet、GM transmit、比较与验证、清理等阶段（`ShortEdgeBlendOracle.cs:165`–302）。只要任何阶段触达 Bound/Jac 都被计入，未测各阶段份额。

原 `FIX self ms=23340.638` 是 Fillet 内部窗口，`gm-call-ms=23346.655` 又是调用外层窗口，均不是上述计数的相同边界。清零计时只包 `cache.Clear` 前后时间戳，不包所有桶计数/扫描/输出开销；可能含调度/GC等待，不是排他 CPU。不能将503.873ms除23,340.638ms写成合格成本占比或负载无关事实，也不把一次空时间戳均值外推成严格可减去的开销上界。

### C3 · 旧95%具体成因没有被此不同配置诊断严格证明

503.873ms 与旧 trace 显示31秒差异很大，使“95%可删清零CPU”仍无充分证据，值得优先做小结构变更；但本次无 profiler、插桩配置、scope、运行/负载都与旧默认tier trace不同，也没有同配置 OS 采样或 code-version 证据。不能据此断言旧95%已被严格排除，或已证明其全部来自 GC wait/安全点采样bias/合并显示区间的某种组合。旧原报告1d82的未证边界继续保留。本轮按用户要求不补 profiler、计时对照或 Mac 运行来追此因果。

### C4 · 事后 DLL/PDB 匹配已核，成功运行当时的加载绑定仍缺

只读收到的当前自家 managed Native DLL SHA256 `5bcc851859df4b14bb72c6a02ae89d5ad030b3b1aaddf1a6f643521122972110`，PDB `04404bfab328a5df94872ee6302bea4f680b8dd17856c5566058a34866229f91`，MVID `ba0fe65f-c9d1-4af4-860d-3e0a629d67ba`。离线 PE CodeView GUID/stamp 与 portable PDB ID 相符；六份相关 source document 的 SHA256 与固定6bb吻合：

| 文档（相对 src/ProjectGmKernel.Native） | SHA256 |
| --- | --- |
| Geometry/Evaluation/NestedBlendBounds.cs | `9d1b0b818da6fb5ac0fa90e8fc1d169eed2617f57be45cfb8ebdbc95c21e4cbe` |
| Geometry/Evaluation/NestedBlendBoundsAttribution.cs | `7eb2f433b3a171dd819951292c21c52c93ab89db8a6cbea060c827b19c9d4fd7` |
| Geometry/Evaluation/NestedBlendIntervalSupport.cs | `fe6cc2acb3cc993d5cba2bf3cf42f5ae62c1ac111797723275edc4fb7b7b60a7` |
| Geometry/Evaluation/NestedBlendParameterBounds.cs | `42d98672455a322d8ce0747c27bb9c2d8b569e394c5ecda4a3c59440dc48d055` |
| Geometry/Evaluation/JointNestedBlendEvaluation.cs | `46b43a8091e335e11fdff65678b19055d8148bd9f5763b8335c75ff75bdbc62f` |
| Runtime/KernelRuntime.Memory.cs | `9c5e424f9f2e92f21c85af57a950068539d12f05fb29ed5a201ce7c6b48930ef` |

PE还确认 BoundSegment IL5768B/InitLocals=false，Jac IL1030B/InitLocals=true。这里读取的是 managed IL，不是新M4或x64 native反汇编。661eed9→6bb 在 src/scripts 无差异，后者新增的是 proposal 文档。

但 `preflight.txt` 是03:40:44、HEAD c084a809的旧失败尝试，Native hash `d3e99280831844140d99921809d0309ced451f404c371542f679eb7db09fd4d9`，不等于事后5bcc；其中M4/.NET10.0.11/SDK10.0.400/default tiering与命令是早期环境记录，**不能冒充成功03:49 run2的不可变 preflight**。run2没有实际加载 DLL/PDB/MVID manifest。本轮只能接受原日志计数和事后相关源码绑定，不能补造 at-time loading 证据或声称整个产物所有源码均已审。build2.log只有warning，没有独立原exit字段；原run2明确记录exit0，二者应分开表达。

## 后续固定生产候选的必要负控（本轮不替实现）

1. **无/有 UV 两支**：无 UV 的真实 analytic/nested 程序，局部 Jac clear 执行次数应0；任一侧/任一晚节点有 UV，原完整63项 clear 次数不变。第0节点无 UV、最后 child 第二侧有 UV、稀疏/非前缀索引都必须走 true；不要只测 representative 全无 UV。
2. **共享/duplicate 与失败 fill**：同 basis 双侧共享 UV、不同节点复用索引、failed-fill 后再试、成功 cache 历史后失败再换 source/program（相同 ScalarOffset但不同数据）应与基线状态/已定义输出一致。不能把 Ready-only 或跨 program reuse 混入本次改动。
3. **Jac 后消费者**：NearestBoxes、两次 Frame、curvature、acceleration、supportU/UV child section、capture/require witness及失败分流，用实际托管数值路径检验；至少既有有 spline 支持例和 analytic/deep chain/奇异例。不是一段新 helper 模拟数学就算完成。
4. **状态历史/branch**：栈污染、反序调用、UV→无UV→另一UV、同 nodes expansion/refinement失败/重入、Reuse=false 后续额外 clear 都应保留语义。计数仅针对 Jac 目标站点；不能把 debug额外 clear 当 guard 回归，也不能为达“全零次”删掉它。
5. **精确比较边界**：比较真实返回状态及已定义输出前缀（区间边界、导数、matrix/equation等合同区域）；不要要求未使用 cache、padding或未承诺初始化的失败尾部字节相等——无UV新cache保留毒值正是预期。有UV完整 clear应有毒值/旧 Ready负控，不只检最终pass打印。

历史45pair JSON revalidate只证明旧记录/资格器兼容，不能证明新native数学或速度；120次失败也不能代替上述多类路径。未来固定 guard SHA另审；如必要可以做小托管正确性验证，不因此授权 before/after性能对照、5case/S2/19/151/full/PKbench/AOT。

## 本轮实际执行与可复现工具

两条均 q2 **.NET10 BCL/托管小程序**，原 exit **0**：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache docs/reviews/advanced-modeling/cache-clear-6bb-evidence/ContractProbe.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache docs/reviews/advanced-modeling/cache-clear-6bb-evidence/ArtifactAudit.cs
```

ContractProbe 对 attribution 源SHA作硬校验，临时项目直接包含固定原文件（没有挪 namespace/改变正式 allocation扫描范围），只补一个两字段 Occurrence scaffold，17项检查实际谓词、full-span poison合同、owner拒绝、bucket边界、重复dump/不reset。**不是实际 Jac/消费者数值测试或已实施生产 guard 的测试**。ArtifactAudit 只读取 PE/PDB/文本，不加载执行目标DLL，复算上表并显式输出 `PREFLIGHT_HAS_CURRENT_DLL=False`，不隐藏来源缺口。两者都不测时间/吞吐，也没有构建完整内核或AOT。

日志位于 ignored `temp_docs/cache-clear-6bb-review/`：contract-probe.log/.exit、artifact-audit.log/.exit；协调端只读复制的原证据保存在其 `original/` 子目录，仅自家DLL/PDB及日志，不含厂商库/凭证。其余原始SHA：preflight `bdc601896471fade2307bbed434149d6be39a5bc71512added4c5cdb3ad57e7e`；首失败run `f2214b07008c1dd058efbe05b93314a79d9cef76e030a69787da49501663ea23`；build2 `90ae72f324801fa882257705958346973663de468c95d3e43a3fdb5e712e9e25`。

本轮只提交本文与两个 docs 内复现工具；未改 core、生产 guard、测试预期、历史数据、正式 allocation规则、其他工作树/main/live，未部署。没有在Mac新增任何运行、没有性能测速、PK或大内核构建，自己的小托管子进程均已结束。可交给独立实现者做最小生产候选；该候选正确性与实际收益仍分别待验。
