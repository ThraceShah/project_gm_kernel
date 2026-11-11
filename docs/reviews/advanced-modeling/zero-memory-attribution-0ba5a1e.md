# M4 ZeroMemory 归因：已定位 clear 站点，尚未证得其排他成本

固定分析对象：`0ba5a1e7021b9d8bf0e20bfc2aed1ad20a76c7ba`，Native仍为已接受的 `39c1dc8c037082b040defc7f3a202d6fa86a145b`；对照 `04c7ed00f39c09cb10faad132d77e50a6a669f07`。Codex GPT6.1Sol high，2026-11-11，独立归因研究，不实施内核优化。

**本轮证明到：在一次 M4 TC0/R2R0 FullOpts 的真实代码中，IntervalJacobian 的 bulk zero 调用来自 `cache.Clear()`，长度是 352×cache.Length；调用者传63项，所以每次到达该站点请求清零22176字节。Span.Clear 被折入 caller，下游 ClearWithoutReferences 以尾调用进入 ZeroMemoryInternal，因此 direct stack 不必出现 Span/ClearWithoutReferences 帧。**

**尚未证明：旧默认tier trace中31.4569秒究竟有多少是清零CPU、wrapper/GC等待、调度或采样归因偏置。** 本轮没有取得该旧配置的逐entry次数/时长；不能把这次 FullOpts 映射升级为旧trace的全部时间因果，也不能把显式Span帧0.228秒当全部 cache clear 成本。下一步应是一个保留clear语义的计数/分段计时实验，而不是再做无针对的五分钟 A/B。

## 已有结论继续保留

- 04c五case的45对原primary已被独立接受，v4资格门只接受其有限五case范围；本研究不重做资格器。
- OptA39仅BoundSegment的SkipLocalsInit获源码安全接受，删除的是全部allocation路径合计226288B隐式清零，不是每个early entry都必有该量；RHS、Assemble矩阵、cache.Clear仍在。
- S1的正确性/检查值一致与收益不确定分开：负载2.7→7.7、单warm样本、跨case AB/BA，shortedge −4.7% / solidblend +10.6%不能确证收益或退化。A侧用了旧runner，事后v4 full-run复验只补资格，不能改称same-runner协议；未进入S2。
- 330KB/147GB与“每root-cell 60+次”推导已撤回。root-cell相关高层比值1.011不能证明可去重；OptB/OptC另案，完整操作GM/PK≤1目标不变。

本轮读取了初调查及其勘误、OptA报告、04c/39/v3/v4独立报告与既有M4产物；不以Linux PK反编译地址或q2 x64代码代替M4 JIT。

## 新执行与来源绑定

先只读原M4 `gm-modeling-performance/temp_docs/modeling-performance/`。既有 `opta/jit-base-disasm.txt` / `jit-candidate-disasm.txt` 只过滤BoundSegment，没有Jac本体/可用inline tree，所以不足以完成站点归因。

协调端授权并与private bootstrap串行后，本轮**唯一新M4执行窗口**为原记录 **UTC02:57:03–02:57:07，4秒**，上限30秒内。用现成DLL/no-build，精确运行：

```sh
# 在原成功工作树根目录；dotnet为既有同平台安装，不改依赖
MSBUILDDISABLENODEREUSE=1 DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
DOTNET_JitDisasm='*IntervalJacobian* *IntervalResidual* *CacheSplineContact* *ZeroMemoryInternal* *Clear*' \
DOTNET_JitStdOutFile=temp_docs/zero-attribution-review/jit.txt \
dotnet vstest tests/KernelTests/bin/Release/net10.0/KernelTests.dll \
--TestCaseFilter:FullyQualifiedName=KernelTests.NestedBlendBoundsTests.CertifiedTubeEnclosesPositionsAndFirstDerivativesThroughoutAParameterCell \
--Logger:'console;verbosity=normal'
```

**实际exit0，1/1 pass，test body107ms，vstest total1.9356秒**；这是纯托管区间例，不是完整建模性能样本。结束即释放窗口，没有第二轮Mac运行或BCL微测、没有PK、build、benchmark、全量测试或AOT；没有停其它worker或服务。

实际runtime为Mac Arm64 .NET10.0.11，dotnet为Mach-O arm64。`jit.txt`为vstest父进程输出；目标testhost的Jac/Residual/Zero代码在 `run.log`。早回执曾误称jit.txt未创建，检查后已向协调端更正；两份文件都已只读拉取，不为日志位置重跑。

源码HEAD=0ba、src tree=`f1dc65ce5c7cf2e9ea42e3832e8352147a5c2e1b`。现成KernelTests DLL SHA256=`e4f7fe095194c5fedd429ddb8b4f783f6e961183efa881065f39c2174ae9491c`；Native DLL SHA256=`76c163c1e687a61df54fd286d23ed8043ee169756f8f80ca4cc8be05fc23b17d`，MVID=`99ab5e07-6f0e-4e1b-b9b9-a39f22ba0843`。独立PE/PDB读取确认相关document checksum与固定源码完全相等：

| document | PDB与源码SHA256 |
| --- | --- |
| NestedBlendBounds.cs | `a580a030e7a189ce151534e52caabc56a4188cbcee5d365c406d0161162be898` |
| NestedBlendIntervalSupport.cs | `fe6cc2acb3cc993d5cba2bf3cf42f5ae62c1ac111797723275edc4fb7b7b60a7` |

因此映射不仅依赖工作树HEAD/时间戳。旧trace仍属于04c调查的默认tier配置，新JIT是上述TC0/R2R0、No PGO；不将二者混成同一执行。

## M4站点映射与清零尺寸

以下native offset均是各方法内部偏移，不是跨进程可复用地址。原raw在 ignored `temp_docs/zero-attribution-0ba/`；不提交库或大段反汇编。

| 站点 | 实际M4代码/IL | 缓冲与频率证据 | 接受边界 |
| --- | --- | --- | --- |
| JointNestedBlendEvaluation.IntervalJacobian | IL563B，InitLocals=true；唯一Span.Clear为IL_0043，PDB源368行；FullOpts1192B / frame464B，fully interruptible | `IG05+0xD4`：w1=352、umull x1=cache.Length×352、x0=cache指针，再间接call；BoundSegment给完整Span[63]，所以22176B | 一次到达该站点一次clear；不是每个Jacobian column一次 |
| Span.Clear / ClearWithoutReferences | Span<Contact>.Clear没有独立compiled body；Jac已包含sizeof×length。下游ClearWithoutReferences272B，在IG06+0x60检查len>0x400，IG07+0x84恢复自身frame后以br尾调用Zero helper | 本调用22176>1024；无需保留SpanHelpers帧即可出现Jac→ZeroMemory栈 | 证明此配置可产生direct形态，不证明旧默认tier里每一段direct都来自同一code version |
| Buffer.ZeroMemoryInternal | FullOpts208B，partially interruptible；IG02+0x24 INIT_PINVOKE_FRAME，IG04+0x80 native调用，IG05+0x84返回转GC mode，条件STOP_FOR_GC | 此wrapper除native memset外还有transition、可能的等待/恢复；native清零部分未做独立CPU计时 | 不能把wrapper采样归因全部算memset；STOP_FOR_GC存在不等于旧运行实际等了多少 |
| Jac小局部隐式初始化 | prologue中3×64B+32B的零store覆盖224B区域；source节点208B随后经MEMCPY写入，另有小spill | 每实际Jac entry有此prologue；这不是22KB cache，也不是已删226KB caller scratch | 未审为可删除，不扩大SkipLocalsInit到callee |
| Jac变量导数初始化/复位 | IG04/IG29与IG26的stp xzr：每项derivative16B；变量value由boxes写入 | 初始n项，成功column后逐项复位；n≤63 | 必需数值构造，不能当冗余缓存清零删掉 |
| IntervalResidual | FullOpts11024B；frame reserve0x1960另有callee-save；prologue零区从fp+0x1150到fp+0x1928，共2008B唯一覆盖范围，有dczva及重叠head/tail stores | Jac每个执行column调用一次；成功完整Jac为n次，另有caller直接Residual调用 | 不是Buffer.ZeroMemory调用；物理写入可能重叠，不把2008B直接当DRAM流量。新证据也说明不能只看“Jac自己无stackalloc”推断所有初始化成本 |
| caller显式clear | BoundSegment222行在Reuse=false时额外清同一cache；RHS/Assemble矩阵、CertifyNearestBoxes rhs/selected仍各有显式clear | 各自执行条件/尺寸由n、count、nodes域决定，不等于Jac entry数 | 不属于已证可删范围，也不从同名stack把它们相加 |

PE读取还看到了另一个类 `IntervalRootCheck::IntervalJacobian`（IL302B）；没有把这个同名方法与JointNestedBlendEvaluation混算。CacheSplineContact本身IL173B、2个调用；IntervalNormal/IntervalSpline的default out与局部hull仍有初始化义务。新单例是analytic支持，未触发CacheSplineContact的JIT body，不能据此说所有spline/callee站点都已动态覆盖。

公开CLR10源码与新汇编吻合：非引用Span按sizeof×length进入无引用清零，超过1024走ZeroMemory，CoreCLR的native入口用memset；这是机制核对，实际10.0.11站点/尺寸以上述M4汇编为准。[Span.Clear](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Span.cs#L268)、[无引用清零分支](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/SpanHelpers.ByteMemOps.cs#L325)、[CoreCLR入口](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/classlibnative/bcltype/comutilnative.cpp#L485)。

## 调用频率与成本：哪些已知，哪些不能乘

在成功BoundSegment路径，源码为1…6次expansion Jacobian加6次refinement，因此至少7、至多12次Jac；失败可提前退出。每个成功Jac执行n次IntervalResidual，cache.Clear在column循环外。仅就已到达Jac clear站点的次数J而言，请求字节数为J×22176；本轮**没有测到原shortedge的J**，也没有测DRAM字节/带宽。

旧223331是SurfaceIntersectionPatch高层BoundEvaluations，不是Jac entry或全部allocation路径计数；不能乘7/12或226KB/22176B就宣称旧运行有多少GB流量。新单例pass107ms也不是清零计时；在未测J与实际clear时间前，没有“每次70µs memset”或可省95%的证据。

## 旧trace：形态可解释，CPU因果尚未闭合

独立 `TraceMap.cs` 逐event重建栈、累计正时长并验最终栈归零，不重复相加同名递归帧：

| trace | 正时长显示区间 / 总ms | 含Zero区间 / 归因ms | 最长含Zero区间 |
| --- | --- | --- | --- |
| shortedge | 315 / 33060.550667 | 52 / 31456.884765 | 3576.260000ms |
| solidblend | 541 / 3876.527588 | 77 / 1173.566710 | 55.742248ms |

这些是**Speedscope合并后的显示区间，不是raw采样数或实际helper调用数**。shortedge叶大多为CPU_TIME伪帧，direct Jac→Zero归因31229.327605ms，带Span<Contact>.Clear帧为227.557161ms；它们不能代表逐clear时钟或排他CPU。

新FullOpts的Span内联、SpanHelpers尾调用确实能消掉中间帧；旧defaulttier显式Span帧也与未内联形态相容。但原code version/inline tree没有被保存，**不能宣称已经证明31.229与0.228是同一clear在两个tier的全部分摊**。应将旧“95%时间在可删除清零”更正为“95.149%显示归因栈含ZeroMemory，已定位一个可产生该形态的clear链，排他成本待测”。

CLR sample profiler源码是SuspendEE后抓managed stacks，再RestartEE，并按暂停时GC mode标记managed/external；因此QCall返回处/安全点偏置是一个可检验的解释，不是普通OS逐指令CPU采样的等价物。这解释为什么需要第二时钟或分段计时，却**不证明本例95%是假的、等待的或全部memset**。[CoreCLR采样流程](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/vm/eventing/eventpipe/ep-rt-coreclr.cpp#L101)。

## 下一最小实验及实施范围

下一步唯一应回答的问题：**在原代表GM操作中，实际Jac入口/clear次数和该clear的累计时钟是否足以解释旧31秒归因？** 建议另一实施者在独立ignored源码副本只加诊断，不删除任何clear：

1. 在Bounds364入口计JacEntered；在368的clear前后各读一次Stopwatch，累加ticks与ClearReached；在成功return计JacSucceeded，并按n与“是否有任意FirstUv/SecondUv≥0”分桶。另计BoundSegment entered / evaluated / success及expansion/refinement，准确解释J来源；不拿高层BoundEvaluations代替。
2. 单线程或线程局部计数，结束时一次输出，不在每entry打印/分配；单独量空的双时间戳开销。clear仍原样执行，失败/retry/early-return都计入，异常不丢计数；记录source/assembly hash、runtime/tiering/并发负载。计时包含可能暂停，不能直接叫CPU；同时记录process CPU与总wall作上界交叉核对。
3. 先一个最小代表、无profiler的聚合诊断；若clear累计很小而旧归因很大，再仅对同诊断配置做一次短profile对照或OS级独立采样，检验采样偏置。若累计与wall同量级，才分辨native memset与wrapper/等待；不要一开始再跑全五case或完整A/B。instrumented输出只作归因，不是qualified速度。

具体可交接的最小优化方向，**须由计数/成本确认后另提固定候选**：若多数entry的所有node均无UV，则研究“read-set为空时不清cache”的通用fast guard，混合/有UV仍保持完整clear。这比一开始引入epoch或修改证明结构更小；当前analytic单例说明这种read-set存在，却未量化原shortedge占比，不按case坐标硬编码。

其源码义务清楚：cache直接matcher只在Bounds371-373的非负UV槽；IntervalNormal的spline分支43先经SplineUv校验再读，Residual166枚举同两侧UV；SplineUv在JointNestedBlendEvaluation483-486只返回FirstUv/SecondUv或−1。后续CertifyNearestBoxes、IntervalFrame、support/curvature/acceleration/witness也必须纳入空read-set证明，失败Unsupported不得读旧cache。**本报告不批准实施这个guard，也不批准Ready稀疏重置。**

若走有UV的Ready-only清零，必须保留任意matcher前的全read-set invalidation：两侧、nested非前缀scalar slot、shared/duplicate UV、failed-fill重试和Jac后的消费者全部覆盖；Values/keys写完后才Ready=true，不能跨program/generation持久复用。原RHS/Assemble clear、n个导数seed/reset、out初始化都保留；Residual的2008B局部清零是否冗余需要单独完整写入支配审计，不能从本图直接加SkipLocalsInit。

## 下一次同版本A/B的自动前置清单（本轮仅提出）

执行前生成一份不可变preflight manifest，任一不一致先拒绝，不开始计时：

| 检查 | 自动比对与拒绝条件 |
| --- | --- |
| actual runner | 两侧实际runner文件SHA相同；各自将执行的compiled assembly SHA/MVID与该source/build manifest绑定，核gateVersion与实际入口，不只看HEAD。若路径内嵌常量使assembly不同，先明示并核准差异，不能称同compiled artifact |
| Native base/candidate | 固定提交、src tree、实际已加载DLL/MVID/PDB文档hash相符；差异白名单仅事先接受的属性/文件，拒绝R3或其它moving改动；区分testhooks与默认build |
| workload与输入 | oracle源码hash、exact case/API、全参数、target+全部tools/seed及来源、构造/receive路径、相同配置与私有preflight已知状态对应；不由相同tag推相同输入 |
| 运行协议 | 同host/arch/SDK/runtime/Release/tiering/R2R、每核调用顺序、cold/warm rep身份、请求次数、AB/BA顺序、并发负载阈值事先冻结；日志记实际开始/结束与命令 |
| 输出完整性 | 全部请求/尝试/合格/未执行数与原始capture/log/report齐备，runner exit/overall gate通过；失败与写出异常不抽样删除；每次对应独立primary，组合另列 |

S1 A侧旧runner不是same-runner；事后v4重验不改变这个协议事实。将来可以写一个不含私有数据的独立preflight检查器落实本表，不趁本轮重做通用资格器。

## 复现与证据

本树持久工具都是.NET10 BCL，只读取既有trace或PE/PDB，不执行目标程序集：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run docs/reviews/advanced-modeling/zero-attribution-0ba-evidence/TraceMap.cs
MSBUILDDISABLENODEREUSE=1 dotnet run docs/reviews/advanced-modeling/zero-attribution-0ba-evidence/IlMap.cs
```

二者exit0；默认读ignored evidence根，亦可传相对工具目录的证据根参数。Mac仅新增上述4秒单例；其它均为本轮q2静态读取/重算或明确引用旧结果，没有重新测量完整操作。

| ignored证据（根为temp_docs/zero-attribution-0ba） | SHA256 |
| --- | --- |
| run.log（新M4 testhost+JIT） | `6b52f69de147aeca267d154cb4aa354d8d8a340ac2ed160cc071c70c4f8a2ac3` |
| jit-vstest-parent.txt | `1ef37c3aff9f7a536a4d3d0054196b9180bda5a4022efaa088798f0d6895b0b4` |
| trace-map.log | `f199d86dd3b8904ac4244a65dba29401d68e5a2fc6e3f57c5dada527e45b9e34` |
| il-map.log | `337c6758fc77d876015fe89f3f3a8c35b65ff2821c8c7a41ce126583102f144b` |
| original/jit-base-disasm.txt | `b0ca57401d5654f963cc9c704b359e4dccb083530dbe9e8c0a373762f870d228` |
| original/jit-candidate-disasm.txt | `a55a39e5f0a219bdfb09c9855d6f55ad2c7eed34b5529ec0e4e9de00d9179bbc` |
| 原shortedge trace（旧04c根） | `ed837c54d42604a784fe4a962c195956867376dabd67a4c8e96a0b83f9e634bd` |
| 原solidblend trace（旧04c根） | `dabe21b6c06c6f02ec0f34e69b938f74ef5e1d69565ee9b0ddf6fb074d8fa9bf` |
| 持久工具IlMap.cs | `b0ac3f2e859eec4eb7323f425be95040603e0f04e3dcc054fc0030d712a12965` |
| 持久工具TraceMap.cs | `5bbab353cc8f8f0f88fb189acfc7fe2af0a7f919403d8a6809b2de12bc231763` |

本轮交付的是已绑定源码的M4 clear链、尺寸/静态频率边界、trace重算工具和可否定的下一计数实验。旧归因的排他CPU成本与优化收益仍未证；不以一个已定位站点或删除226KB宣布完整目标完成。
