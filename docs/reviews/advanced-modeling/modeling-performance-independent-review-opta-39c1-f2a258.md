# OptA 39c1 / benchmark qualifier f2 独立复核

日期：2026-11-11；review-only，Codex GPT6.1Sol high。

**结论分开：接受固定39c1的Native方法级源码安全范围；拒收固定f2的计时资格器。** Native接受不包含性能收益、全功能/main集成或新的缓存优化。f2有4个确定漏判，当前不能据它的 `qualified=true` 放行正式少量样本或长计时窗口；cheap诊断smoke可以运行，但每对必须独立核验，且不作性能结论。

## 固定对象与本轮范围

- before：`04c7ed00f39c09cb10faad132d77e50a6a669f07`；OptA：**`39c1dc8c037082b040defc7f3a202d6fa86a145b`**；qualifier v2：**`f2a258b7b5290a5e5160841e1c24668589faf4bf`**。真实对象均核实并fetch，原工作树clean后从f2建立 `ThraceShah/gm-perf-optA-review`；保留audit56825d6/R3报告272a51e及ignored证据。
- 完整before→f2为5文件：两份说明、`scripts/ModelingPairedBenchmark.cs`、唯一Native文件 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs`、新增合同测试。Native差异只有using、注释和BoundSegment上的 `[SkipLocalsInit]`；callee/module、算法、容差、预算、显式rhs/matrix/cache清零均未变。不含被拒ec393全局三角函数改动。
- f2的src tree为 `f1dc65ce5c7cf2e9ea42e3832e8352147a5c2e1b`，tests tree为 `b3a4d974c6b44dec00366a6a17bc811dce816877`；before为 `7d516f6c16c762581f0ba2d87df4df5e263c6cc4` / `8693efcd5f0ba1a1413bec94e18c0296a36028ac`。后续文档提交不改这两棵候选树。
- 复用前审 `830e7cdf22b646af297b41dc7560d20d7ac7853a` 的限定45对资格与描述性计时，不重审/重跑原20capture/45pairs；不追v3或Mac工作diff。只读复制原Mac OptA dump/JIT产物，在q2运行8项相关测试、资格器fixture及一个便宜sphere smoke。无Mac长bench、全60/full suite/19/96/347/NativeAOT/Windows，无生产/正式测试/阈值/main/live改动。
- 实际重读parasolid-research，调用MCP查询 `PK_DEBUG_BODY_compare`、`PK_BODY_check` 原型/返回语义；前者MCP摘要不完整，仍以已审strict helper/FD106.7合同为准。实际cheap smoke通过既有PskernelSharp/ParasolidScriptHost构造真实PK对照；没有手写ABI或重定向真实pskernel，没有提交库/凭据/反编译代码。

## Native：19 scratch的独立写入支配核查

接受前提是既有有效NestedBlendProgram/索引/输出span合同不变；不是为任意损坏内部program输入新增安全承诺。MaxVariables=63、MaxOccurrences=16；动态n和nodes域由EvaluateCore成功结果限定。没有找到可达的read-before-write反例，故不因runner失败倒推Native错误。

| scratch（合计19） | 已核写入→读域、status/optional约束 |
| --- | --- |
| snapshot / occurrences / counts / point（4） | `EvaluateCore`成功尾复制完整variables[..n]、nodes与两个count；本caller surface=-1，随后输出point写完才成功。失败立即返回，不读counts/部分snapshot；point在caller不读。TaylorJet构造器 `this=default`，Initialize完整构造n项，order1解完再copy，因此snapshot所读[0,1]不依赖尾垃圾；Occurrence的7字段由new初始化。 |
| fixedCharts（1） | nodes的每项均赋值，rootChart的continue分支也先写；非root参数取其4槽中的Offset+3。helpers只按nodes[i]读fixedCharts[i]，不枚举未写tail。 |
| state / residual / jacobian（3） | state逐i<n由snapshot填。Assemble先matrix.Clear，显式spine写XYZ及child第四行，隐式写两个support/gauge/child第四行，SplineRows写两UV行。root3槽、child4槽和n+=2的UV域互斥且连续，alias复用同UV不留洞；成功前有限性全读均被相应写入支配。失败不进入LU。 |
| pivots / rhs / inverse（3） | LuFactorize只读n²，Success已逐k<n写pivots且pivotRow<n；LuSolve只读n/n²，即便收到完整span也不读tail。每列rhs[..n].Clear再设单位项，成功后逐row写inverse；中间失败返回，不使用半inverse。 |
| variables / equations / centerF（3） | 每次消费前variables的n项完整构造value/derivative。IntervalResidual按同一pack写XYZ/gauge/child/UV行（显式支路也覆盖），Success才复制n个centerF。Jacobian各column种子使用后复位；失败的半equations不消费。 |
| boxes / image（2） | 每次expansion写满boxes[..n]；certification所有row写image，不短路跳过写入。只有certified后才读image收缩，之后每轮再写满。经IntervalResidual+SolveIntervals成功后image[..n]才作为参数/速度/曲率/加速度消费；Offset+3/uv/uv+1均来自上述已分配域，n≥3保证XYZ。 |
| intervalJacobian / remainder（2） | IntervalJacobian成功逐column/row写n²；失败expansion继续且下一次重新写，refinement失败返回。Precondition逐row/column从显式0/1开始，写满n²；SolveIntervals只读取这一域。 |
| splineCache（1） | 首次centerF的IntervalResidual**未传cache**。随后IntervalJacobian入口全cache.Clear仍在，第一次matcher不会从未初始化Ready取命中；CacheSplineContact成功填18值和全部key后才置Ready。失败不进入caller后继读，重试先全clear；certified后的6次refinement都成功才有post读，Reuse=false另一次Clear也保留。IntervalSpline的empty-cache/Ready短路及fallback的18项写入已核。 |

复核的helper包括 `JointNestedBlendEvaluation.EvaluateCore/Assemble/AssembleExplicit/SpineSupportParameters`、`SmallLinearSolve.LuFactorize/LuSolveInPlace`、`NestedBlendIntervalSupport.IntervalResidual/IntervalNormal/IntervalSpline`、`CacheSplineContact/SplineContactMatches`、`CertifyNearestBoxes/Precondition/SolveIntervals/IntervalFrame` 和 `NestedBlendParameterBounds.BoundSpineSupportSection`，不是只检查本方法正则写入。

optional路径同样已核：captureWitness从完整boxes复制；supportParameter[0]取合法child station，SolveIntervals成功才写[1]，需要四项时BoundSpineSupportSection成功才写[2,3]；reach分支读取已初始化XYZ和UV、预算由caller设定；曲率/加速度只用已解image/normal，失败不发布；anchor重建snapshot[..n]为order0后才定位两侧contacts；几何输出经两次IntervalFrame成功才完成九项。若capture/support/contacts在后续失败前已有部分写入，外层都按非Success拒绝/不合并，**不承诺失败输出具有有效几何意义**。此次attribute没有放宽任何这些caller/callee合同。

## 测试与污染证据：支持什么，不支持什么

q2固定f2独立运行新增初始化合同2项及原SplineCache6项：**8/8、exit0、6.5010秒**，175catalog验证及2921方法allocation IL guard为CLEAN。包含cache on/off、rational/nonrational、不同dual seed/sense、不同box/basis拒绝复用等原检查；不是本轮独立60/60，更不是full suite。

原Mac5程序×5盒的两份dump都70行，SHA-256同为 `c5b09e1387b8dafb4cde1a31fd7ea7525e53b36c95d85dbcd244fbbe6980da21`，已只读拉取并独立核对。它们对有限fixture的状态/数值bits相等；不是所有program/optional路径的完备证明。新增Dump没有调用BoundSpineSupportU/Uv，也没有覆盖所有rational/periodic/别名组合；这些路径本轮接受依据是上面的源码支配，不能借“所有输出”措辞称新测试都运行过。

`PolluteStack(8)`实为depth8…0共9个8KiB递归块，约72KiB，并无目标BoundSegment的地址/范围/写入后值验证；不能称它确定覆盖约226KiB目标frame。重复顺序调用不是实际重入；每次BoundSegment都有调用内cache，跨Dump调用史也不是注入Ready=true/错key/NaN/Inf/有限假命中的证明。新增failure dump还会打印失败后的输出，这些bits不是有效输出合同。**不接受这些扩张的污染/再入声称；也没有据此编造Native已发现错误。** 由于全部潜在读取已由保留的明确写入支配，此局部source-safety接受不依赖“垃圾恰为NaN会拒绝”。不授权删除cache.Clear或扩大SkipLocalsInit到callee/module。

Mac受影响60/60为实现者文档报告；本轮能定位的原目录只有dump、JIT base run日志等，未取得与固定source/config完整绑定的60项原日志，故不升级成独立证据。04c Mac未改源单项NotConverged683ms与q2同source/testtree通过仍是平台/runtime未定位差异；未将它修复或归咎OptA。

## M4 JIT：实际删除量与归因边界

两个原始artifact各仅有一个相同全签名BoundSegment listing，均标Apple ARM64、FullOpts、No PGO，inline统计相同。独立按循环立即数和 `stp xzr,xzr; subs #16; bne self` 核算：

| 静态产物 | base | candidate |
| --- | ---: | ---: |
| stp xzr站点 | 54 | 16 |
| 上述清零loop | 15 | 0 |
| 到达全部这些allocation路径的loop store量 | 226288B | 0B |
| 方法code bytes | 14448 | 14412 |

15项长度为4544、448、512、31760、31760、512、512、1008、1008、1008、63504、63504、2016、2016、22176。该观察支持窄切片确实消除了这些隐式清零，**不是所有BoundSegment entry都必删除226288B**：EvaluateCore/LU等早退可能只到达前面的allocation。剩余零存储/显式rhs.Clear、Assemble.matrix.Clear、IntervalJacobian.cache.Clear及callee工作仍执行。

`TieredCompilation=0`与触发过滤来自实施记录，artifact本身直接可验证的是同签名/FullOpts/架构/loop；未发现记录JIT当次DLL hash+完整环境的机器manifest，不能称对该缺失闭包做了独立重建。源码diff、立即数及方法形态相互一致，但这不是本review新跑Mac JIT或bench。保存源/产物hash供后续固定窗口核对。

此前ZeroMemory95%是inclusive采样，且主要路径在IntervalJacobian；这里不把它全算已删除，也不声称30–50%收益、带宽或所有调用226KB。最终完整操作目标仍为 **GM/PK≤1**；内部阶段600/7000等观察门不能替代它。此轮没有新的性能达标结论。

## f2 qualifier：原17自测通过，4个独立负控失败

原脚本 `--validate-fixtures` exit0；它直接调用Qualify、两个summary/overall helper检查，不是实际启动oracle失败→FinishSample→run report→outer exit4的完整跨进程负控。仅原17通过不充分。

在ignored副本只给ValidateFixtures追加4个Check，Qualify/fixture构造/summary代码保持固定f2原文：

| 原ValidCapture(2rep)上的唯一变动 | f2实际结果 | 应有结果及原因 |
| --- | --- | --- |
| 删除steps | qualified=True | 拒绝；没有expectedAPI/owner实体，`ModelingPairedBenchmark.cs:255`的TryGetValue失败反而不报告缺title |
| 两个step id改成not-an-operation | qualified=True | 拒绝；steps字典为空，仍沿用timing step与check ID获通过 |
| 所有required primary状态改diagnostic | qualified=True | 拒绝；required仅验证ID存在，统一pass或diagnostic使strict/fullcheck/mass都可缺成功证明 |
| 追加step999/kernel=other/ms=-1/error901计时行 | qualified=True | 拒绝；筛GM/PK后只验证过滤行，额外错误行被忽略 |

独立probe最终 **exit3、4 fixture verdicts wrong**。持久原输入在 `docs/reviews/advanced-modeling/opta-f2-evidence/`，副本与原脚本hash/命令见证据附件。它们是资格器反例，不是4个Native几何失败，也不证明原45pairs已不合格；原45pairs之前是逐owner人工审过的事实。

f2中明确改善的内容保留：exact ID/alias与重复case拒绝，正常present owner的错误API标题拒绝，预期rep数/双方unique step匹配，缺mass/body/fault拒绝，有限正计时及sample外层退出/timeout/captureErrors检查；measure/profile分开的latest、immutable run引用，capture-only时钟与shortedge真实seed路径说明。部分有效子集留diagnosticOnly，整体有不合格/少sample时非0的设计方向正确。它们不能抵消上述共同关系未闭合与required状态漏判；本轮不追v3扩审。

## cheap S0与下一gate

只在q2做sphereBoolean、cold1+warm1的便宜smoke，两个oracle进程共3对操作，runner原exit0。不靠其qualified字段，另外逐capture核实3个owner都为预期Boolean、各恰1GM/1PK零error正有限计时、26/26 required唯一且全pass、captureErrors0；真实PK strict compare、双侧fullcheck、5counts、15mass成立。**只接受这3对输出的诊断正确性，不接受其计时为OptA收益/正式性能结论；sphere也不能代替重型BoundSegment路径。** 不拿q2/Mac时间相除。

下一步仅需固定新qualifier：所有timing row合法且完整join到唯一step/预期API与每rep，required primary必须pass，诊断项逐类白名单；用本次4个未改输入复验并保留原17，再定向审查实际run整体非0和subset诊断。然后在Mac预约有限正式样本窗口，每对独立primary、fresh input、相同API/options与JIT配置，保留固定producer顺序/小PK分母/cold定义局限和实际二进制来源。测后再独立审收益，不因Native source-safety提前宣布性能改善或main已接受。
