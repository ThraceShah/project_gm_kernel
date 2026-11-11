# 04c7ed0 建模性能调查与 OptA 初始化独立审查

日期：2026-11-11。审查者：Codex GPT6.1Sol high，独立于 Pi MiMo 实施者；review-only。

固定对象：`04c7ed00f39c09cb10faad132d77e50a6a669f07`，对照 `610e3ce844bf99c85af6069905f9f3deaab08133`。本报告不是 OptA 实现验收，也不批准 main 合并或部署。

## 结论与接受边界

- **接受已有五个固定 case 的单操作结果资格，接受其有限样本的描述性计时。** 独立逐份读取原 Mac 的 20 份 measure capture，共 45 对操作；每一对都有自己的 strict compare、双侧 full BODY_check、15 项原物性检查和零 API error。不是仅凭进程 exit=0 或脚本的 `qualified=true` 接受。
- **需要修正调查结论与未来基准资格器。** 不能由 n=4/5、固定顺序、一次 warm 进程推出普遍稳态优势；不能证明 cold 差值全部来自 JIT。`paired-benchmark-latest.json` 已被 profile 模式覆盖，不能再当主表原始数据入口。资格器没有机械强制每个 rep 的独立 owner、预期次数与有限正计时。
- **OptA 可以从稳定 610/04c 的初始化分析开始；尚未接受任何优化候选或提速数字。** 建议第一步仅处理 `BoundSegment` 的隐式初始化，保留必要显式 clear，不同时改变 cache 策略。若再优化 `IntervalJacobian.cache.Clear()`，必须另交完整的可读槽/写入支配证明与负控；不接受顺便合入 OptB/OptC。
- **95% 采样与 147 GB 不能当成可删除工作量的证明。** 复核支持优先调查清零路径，但真实 M4 JIT、清零长度和受控 A/B 尚缺；30–50% 或减半的提速预期仅为提案。
- **基线测试状态有平台差异。** 原 Mac 报告为 1857/1858，本审查在 q2 固定源单独复跑所列失败却为 1/1 pass；不能写成跨平台稳定失败，更不能写成全量绿。调查与该收敛问题的修复必须分开。

审查读取了 `AGENTS.md`、`docs/Modeling_Performance_Investigation.md`、`scripts/ModelingPairedBenchmark.cs`、五个 oracle 相对 610 的完整差异、capture/compare helper、相关区间与求解源代码。没有修改生产代码、测试、CSV、阈值或原始 Mac 产物，没有跑全量 25 分钟 benchmark、full suite、AOT 或 R3 生命周期。

## 来源与固定性

当前 04c 的 `src` tree 与 610 完全相同，都是 `7d516f6c16c762581f0ba2d87df4df5e263c6cc4`；`tests` tree 都是 `8693efcd5f0ba1a1413bec94e18c0296a36028ac`。五个 oracle 的增量是重复执行、phase/profile 输出等非默认可选基准插桩，不是新的内核算法。

本审查通过只读 SSH 读取 Mac 原工作树 `gm-modeling-performance` 的 `temp_docs/modeling-performance/`。主表来自不可变路径 `runs/20261111T003330Z-610e3ce/benchmark/report.json`；其 provenance 明确记录 `commit=610e3ce`、`dirtyAtStart=true`，即当时未提交的调查插桩，不能冒称是在干净 04c 上独立重跑的样本。PKToy commit 为 `d820719b6d8510d623972ab9d8efc65edbc312f9`，native tree 与上述固定源一致。

原主机记录：Apple M4、Arm64、macOS 27.2.0、SDK 10.0.400、.NET 10.0.11，PK `38.0.150`、precision `1e-8`，默认 tiering，API 串行调用。load average 前后值也被保留；它不等于完全隔离的空闲主机证明。审查没有在 Mac 同时启动性能负载。

profile/latest 实际来自 `runs/20261111T003957Z-610e3ce/benchmark/report.json`，每 case 两 rep。`latest` 覆盖行为见 `scripts/ModelingPairedBenchmark.cs:115-122`；调查文档第 10 节将它描述成主表入口，需要改为不可变 run 路径或区分 mode。旧产物不应重写。

## 五个 case 的资格与计时

每 case 原始样本：3 个独立 fresh 进程各 1 对，加一个进程的 6 对；后者 rep0 进入 cold，rep1–5 进入 warm。因此每 case cold n=4，warm n=5，共 9 对，五 case 共 45 对。以下数字重新读取原 measure JSON，**不是本审查重跑性能**；ratio 是逐对 GM/PK 的中位，不是两个中位数相除。

| 固定 case | 主 API / 原执行顺序 | GM cold p50 ms | GM warm p50 ms | PK warm p50 ms | cold / warm ratio | 审查结论 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| sphereBoolean.equal-overlap-subtract | Boolean / GM→PK | 14.66135 | 0.0529 | 0.2080 | 47.481 / 0.247 | 9 对结果 qualified；热态优势只限这组观测 |
| planarBoolean.overlap-two-axes-subtract | Boolean / GM→PK | 31.7180 | 0.3863 | 0.4800 | 42.655 / 0.765 | 9 对结果 qualified；不能据此证明总体持平 |
| regularcurved.block-subtract | Boolean / GM→PK | 1034.48535 | 616.9816 | 1.3754 | 665.155 / 451.138 | 9 对结果 qualified；显著差距足以指导调查 |
| solidblend.plane-nurbs | blend / PK→GM | 1548.76745 | 984.7757 | 1.1040 | 1085.510 / 908.923 | 9 对结果 qualified；仅此独立圆角 |
| shortedge.a1-r0.5-t1e-07 | blend / PK→GM | 16749.5092 | 16082.2447 | 1.1729 | 10848.548 / 13720.180 | 9 对结果 qualified；不包含后续切削/组合 |

### 实际每 rep 的 primary owner

独立审计 20 份 capture：所有记录的 processExit=0、captureErrors=0；每个 step 恰有 GM/PK 两条零 error 计时；每对有 `*-equivalence` pass、两条 `*-body-check` pass、`*-mass-0..14` 全 pass。45 个 owner 都齐全，其余 checks 均为 pass/diagnostic。

`scripts/WebProgressCapture.cs:174-205` 在 GM 新导出 XT 经真实 PK receive 后，验收类型/regions、双侧 full BODY_check、严格 BODY compare，并以 accuracy `.99999999` 验收 15 项物性 `abs(GM-PK)/(1+abs(PK)) < 1e-7`。compare mask 为 max_diffs=1024、all=false、acc_dev=false、tol_dev=true，保留数量、容差、类型、sense、missing、nonmatch，仅 `n_cht_pts` 为诊断。调用共享 `ParasolidBodyCompare.AssertEquivalent` 才形成 equivalence pass，不能把 PK API 调用成功或 `n_face_pairs` 当成等价。

部分 oracle 自带旧 compare 较弱，实际这批带 capture 的 benchmark 由上述额外 strict helper 补齐；这一结论不能推广到未启用 capture 的任意旧 oracle。按 FD 106.7，max_diffs 限制不同 face pair 数而不是几何误差；wire 无 face，本 compare 不能证明其有限曲线形状。本次五个 case 都是实体，未来 wire 必须另附有限边类型/sense/端点/trim/双向几何距离和 API 专项 owner，不能只复用这套布尔资格判断。

真实 PK 核实采用 parasolid-research / parasolid-docs 技能、现有 Host/PskernelSharp 与 FD 106.7。MCP `parasolid_get_function_detail(PK_DEBUG_BODY_compare)` 返回的是 Linux 静态入口资料，不提供本轮 M4 动态证明。本审查另在 q2 调用共享 strict helper：2×2×2 block 等价正例通过；同拓扑 2.001×2×2 负例被拒绝，命令 exit=0（预期拒绝已被断言）。这支持 helper 没有退化成纯拓扑或自我对比，**不替代 Mac 全部几何样本的独立重跑**。

### 选择、输入、时钟与统计的限制

1. 外层 `--cases` 是 `StartsWith`（`ModelingPairedBenchmark.cs:42-44`），并非 exact ID。默认五个前缀各选一个 case，所以本批未误跑；例如更短的 `s` 可同时选中两个 case，未来应精确 ID 或显式列出解析结果并拒绝歧义。内层 `GM_WEB_SELECT` 经 `WebProgressCapture.Select` 做等值比较；regularcurved 的脚本 substring 筛选没有使本批混入 reloaded case。
2. 重复循环每 rep 构造新的未修改输入，目标 API 数值参数一致，没有拿上次修改后的 body 重做。shortedge 的真实语义是 PK 使用 freshly constructed seed，GM receive 该 seed 的 XT；不是“两边都 receive 同一 XT”。已有操作前的输入检查支持名义几何对应，文档仍应准确描述两条输入路径。
3. ratio 应只来自 capture 的调用点 Stopwatch。它包住公开建模 API，排除构造、mark/select、observer、比对与 I/O。stdout `TIME/FIX` 以及某些外层 `BENCH api-ms` phase 还包含这些工作，不能互换；调查第 2 节“capture/BENCH api-ms”一句与其后解释冲突，应收窄为 capture。
4. `Before` 与输入构造已运行相当一部分 GM/PK 代码；GM-first case 的 observer 还会在计时的 PK API 前做真正的 PK receive/mesh。cold 是“输入准备后的首次目标 API”，不是全进程完全未 JIT；warm 也没有清空整个 session 内存/代码缓存。新输入成立，不等于每 rep 同一微观状态。
5. warm 五个样本同进程，调用顺序按 case 固定，没有轮换或多进程 warm 独立重复。PK 分母约 0.2–1.4 ms，对调度、tiering、缓存与测量扰动敏感；小于 1 的观测只可报本组结果。n=4/5 的 nearest-rank p95 就是最大值，不是可信尾延迟估计；不要用离散度小证明 JIT 因果。
6. PROFILE 计数相同只支持这些计数维度的工作量一致；不能证明所有 cold−warm 差值由 JIT 造成。调查摘要/表中“已达标”“cold 假象”的普遍表述需要限定。
7. 多步求和比另列；前一步圆角不能因为下游 Boolean 失败而自动判错，反之通过圆角也不能替后续 Boolean 取得资格。既有 2187× 组合数据并不自动成为两个独立新 case。

### 未来基准资格器需补的契约

`ModelingPairedBenchmark.cs:155-159` 只机械检查 checks 非空且状态允许、GM/PK 数量相同、timing error=0、进程无 FAIL 且 exit=0；也未直接要求 captureErrors 为空。它未要求预期 API/rep 数、独立 step owner、15 项 mass 完备、有限正计时或固定输入哈希。`Summarize:198-212` 会滤掉失败样本，只要仍有数据就报告 qualified；脚本末尾也返回 0。这不能作为未来通用 hard gate。

应按 case 声明所需 API/步骤/rep 与 owner 集合，拒绝漏 rep/错步骤/缺 compare 或 fullcheck/mass、NaN/Inf/非正计时，显式报告 requested/attempted/qualified 数量，保持 unqualified 原样可追溯。记录实际 fixture/hash、二进制/SDK/配置与样本顺序；跨 case、跨进程不得用相等计时数组长度代替 owner 对应。**本批 45 对是在原始 capture 上补审后取得资格，尚未实现这些机械约束。**

## profile 重算与内存归因

独立重聚合原 evented Speedscope，检查事件栈最终归零；对每个区间按唯一匹配栈帧累计，避免递归重复计数。所有下列百分比都是 inclusive，可相互覆盖，不能相加成排他成本。

| 原 trace | 事件窗口 ms | BoundSegment | IntervalJacobian | ZeroMemoryInternal | 说明 |
| --- | ---: | ---: | ---: | ---: | --- |
| shortedge，两 rep | 33060.551 | 95.370% | 95.149% | 95.149% | 识别到的 Fix API 窗口 32594.668 ms；相对该窗口分别 96.733%、96.509%、96.509% |
| solidblend，三 rep | 3876.528 | 65.660% | 51.657% | 30.274% | 未识别到独立 Fix 栈帧（可能被内联）；这里不能标成已测 API 百分比 |

shortedge 的 ZeroMemory 归因中，`IntervalJacobian → ZeroMemoryInternal` 为 31229.328 ms，显式 `IntervalJacobian → Span<IntervalSplineContact>.Clear → ZeroMemoryInternal` 为 227.557 ms。solidblend 对应为 641.733 / 531.834 ms。它们支持查看 JIT/clear 路径，**不证明 caller 的 BoundSegment prologue 就占了全部这些时间**，也不证明 explicit cache clear 可全部删除。尚无 M4 IR/汇编、实际 store 长度或受控清零 A/B。

源代码和 q2 的 unmanaged size 复核：IntervalNumber=16 B、IntervalDual=32 B、TaylorJet=72 B、Occurrence=28 B、IntervalSplineContact=352 B、KernelVector3=24 B。`BoundSegment:148-171` 所声明 stackalloc 合计 226612 B，其中四个 63² 矩阵为 190512 B，不是 158 KB；63 项 spline cache 为 22176 B。这个数不含 callee frame/JIT spill/alignment，**不是 M4 实测栈帧或内存流量**。成功路径至少一次 expansion 加六次 refinement，即至少七次 IntervalJacobian/cache.Clear；最多六次 expansion 加六次 refinement，不能统一假设六次。

其余需要修正的口径：

- 14711.7 ms / 223331 = 65.874 µs/记录的 bound evaluation，是该高层计时的平均值。把另一轮 inclusive 采样推导的“memset 70 µs/call”当同次运行的排他成本，已大于此平均总成本，不能这样分解。
- 446662 = 2×223331，与两 rep 的高层计数相符，但 `BoundEvaluations` 在 `SurfaceIntersectionPatch.BoundCore` 计数；一个调用可能拆域或走 explicit 分支，不能直接当作经过 JIT prologue 的 BoundSegment entry 数。
- `330000×446662 = 147398460000 B` 是假设乘法，不是测得 147 GB store 或内存带宽；不得用采样 wall time 反向证明其准确。
- 223331 / 220895 root-cells = **1.011**，不是每 root-cell 60+ 次；223331 / 3518 recorded roots 才是 63.482。这不能单独证明同一 `(cell,box)` 被重复求界，也不能授权 OptB 去重。
- bound 请求/执行/命中来自不同路径，不能默认 requests=evaluations+hits；stage 父时间包含子时间，Scope 合并计数也不意味着独立 CPU 成本。相邻表中的原采样与 PROFILE 不应拼成一个精确排他模型。

## OptA：最小可审范围与初始化证明义务

建议第一候选只在 `NestedBlendBounds.cs:143` 的 `BoundSegment` 考察方法级 `[SkipLocalsInit]`。不要扩大到模块、EvaluateCore、CertifyNearestBoxes 或其它 callee；不要把此局部审查当作它们的证明。IntervalJacobian 本身没有大 stackalloc；其显式 cache clear 是另一项需单独证明的优化。JIT 内联与实际清零去留必须在目标 M4 上验证，不能只凭源码尺寸许诺速度。

下表是固定源的读写依赖清单；它支持一个受限实现方向，**不代表未提交候选已经通过污染负控**。

| 缓冲区 / 源码位置 | 首次读取前的必要写入或支配条件 | 优化时不可破坏的约束 |
| --- | --- | --- |
| snapshot、occurrences、counts、point；Bounds:148-152 / EvaluateCore:178 | 只有 EvaluateCore Success 后读取，成功时复制变量/节点 active prefix 并写 counts；TaylorJet 构造完整初始化系数 | 不可读取失败后的 partial snapshot；n/nodes 外尾部不可参与计算 |
| fixedCharts；Bounds:153-158 | 每个 active occurrence 都赋值，包括 rootChart 的 continue 分支 | 非 root 的 offset+3 必须仍指已写 snapshot；不能按 node 数假定 scalar 前缀 |
| state、residual、jacobian；Bounds:160-162 / JointNestedBlendEvaluation:287 | state 填 n 项；Assemble 成功写 residual，并先 matrix.Clear 后用 += 填 Jacobian | **保留 Assemble matrix.Clear**，加法填充依赖零值；失败必须在后续读前退出 |
| pivots、rhs、inverse；Bounds:162-166 | LU 成功写 pivots；每列 rhs[..n].Clear 再设单位向量，解完复制整列 inverse | **保留 rhs clear**；LU failure/奇异不能继续；helper 即便收大 Span 也只能读 n/n² |
| variables、equations、centerF；Bounds:172-174,359-370 | active variables 显式构造(value,derivative)；IntervalResidual 成功覆盖 active residual 行，复制后才读 | 需覆盖 occurrence 的空间、child station、两侧 spline UV 全部方程；column seed 必须复位 |
| boxes、image；Bounds:179-213 | 每次 expansion 写全 boxes；成功行循环写全 image，认证成立后才 refinement | 失败/早退的 partial image 不可被接受；不能依靠随机 NaN 来确保拒绝，有限脏值也可能伪造证书 |
| intervalJacobian、remainder；Bounds:184-211,356-377 | 每个成功 column 写所有 row；仅 Success 后 Precondition 写全 n² remainder | Failed Jacobian 路径不能读半矩阵；Precondition 的每格以显式 0/1 开始 |
| splineCache；Bounds:171,360-365 / IntervalSupport:23-34 | 当前每次 Jacobian 先全 clear，Ready 短路支配其余键/Values 读取；18 个区间及全部键写好后才 Ready=true | 仅删 caller 隐式初始化时仍保留 cache.Clear；若优化它，须证明所有可读 slot 的 Ready 均先置 false |
| 后半段 optional 输出；Bounds:214-311 | 最近点/正定、支持参数、曲率、加速度、witness、anchor contacts 等走各自成功路径 | 不能只验证九项主 bounds 而遗漏 optional 分支；失败输出不可发布 |

缓存专项风险不能由“只清前 nodes.Length 项”解决：索引是 `FirstUv/SecondUv` scalar offset，可能稀疏、非前缀、跨 nested occurrence，且两侧可能共享 UV slot。任何缩小 clear 的实现，必须先枚举**所有**可能触达的两侧 slot 并使 Ready=false，再允许第一次 matcher；边走边清可能改掉共享 slot 的复用语义。填充失败可能已改写部分 Values，旧 Ready=true 的键绝不能在失败重试后继续有效。

当前 key 含 ScalarOffset、degree/count/dimension/rational/periodic 与 U/V 四端点，却不含 program identity/generation；其安全性依赖 cache 仅在当前调用/program 内使用，不能把它搬成跨命令静态/线程缓存。`ReuseSplineContactBounds=false` 的 clear（Bounds:214）是有意义负控，不能删除或使其失效。异常、重入、不同 program 共用 offset、generation 改变，都不能从旧栈内容取得命中。

候选至少应提供以下证据，再由新固定 SHA 独立复审：

1. 源码级 active slot 写入支配证明，包含所有 status/early-return 分支及调用 helper 的读域；无需把未读尾部为了形式而全部清零，但要证明它确实未读。
2. deterministic stack/cache 污染，兼有有限脏数、NaN/Inf、Ready/key 假命中；单纯 noinline 调用一个污染函数不保证覆盖目标 frame，必要的临时 hook 只能留在独立 harness。覆盖最大→最小 n、失败→成功、重复调用、异常/重入、cache hit/miss/失败填充、相同 offset 不同 program/generation。
3. analytic/no-UV、plane/BSpline、BSpline/BSpline、rational/periodic、两侧别名、nested 非前缀 offset 等正负例；九项区间上下界语义 double bits 与状态 A/B，并覆盖 optional curvature/support/witness/anchor/acceleration。不要比较 padding；有限 fixture 的逐位一致不能代替上述写入证明。
4. 不修改 epsilon、Krawczyk/最近点/闭合证明、预算/depth、根区间数量及验收门。保持拒绝路径、zero-allocation、生命周期/rollback 语义。OptB 重复求界结构与 OptC 深嵌套求值另案，不混入初始化 patch。
5. M4 目标配置下 JIT/IR 或汇编确认实际消失的清零路径与长度，再做约定串行窗口的受控、轮换顺序 A/B。不要与 Pi 的采样或 R3 热负载并行，也不要把观察到的 speedup 推广到所有 provider。

稳定 610/04c 的 OptA 不必无限等待 R3 的所有组合问题；但后续整合 R1/R2 时要串行复核新证书和缓存读写假设。R3 相关文件不在本轮修改范围，已通过的独立圆角与未通过的组合分别保留状态。

## depth3 门与既有失败

`scripts/NestedBlendBenchmark.cs:21-78` 对 exact `--fixture=` 选定的 XT 做 paired surface evaluation；512 calls/batch、31 batches、warmup 2048，sequential/shuffled 与各导数阶分别计量，allocated=0 且 throughput≥70% 是该专门脚本的门。它不是完整建模 API 的 70% 目标。

原调查汇总的 depth3 D0 18.6% 可作为待核的红门记录，不能因本轮整体建模调查而抹掉，也不能直接给所有建模操作设 70% 门。本次可用 evidence 中未找到对应完整原始 benchmark log/配置和运行时 fixture hash 配对，故不独立确认 18.6% 数字的同 fixture/同配置资格。只读时当前三个 fixture SHA256 为：

- `temp_docs/nested-blend-native/depth3.x_t`：`31c415714b7f7811818a2ce21801e579051cb1e9c965ef57cb2ad92ce491fe8b`
- `temp_docs/nested-blend-native/independent-torus-depth3.x_t`：`1e1ab1a31854d9065a9146d0ecae1aa018c8b421f8f479f7fa64efdbbc9452bc`
- `temp_docs/nurbs-blend.x_t`：`b701dc6c36040769677295b2ca9a18f25d45fafab513e311fd51fc4465ac7200`

这些是读取时文件哈希，不是原运行时已记录的哈希。后续复核应保存原配置/完整输出再归因，不在本轮扩大跑 OptC。

原 Mac full suite 1857/1858、`BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` 的 NotConverged 属原报告记录。04c 与 610 src/tests tree 相同，说明调查没有改动其算法/测试；但本审查 q2 SDK 10.0.302 / runtime 10.0.10 同名测试单独运行 **PASS 1/1，exit=0**。因此当前证据应写“Mac 曾报告失败，q2 此配置未复现，需保存 Mac 未修改配置单例控制”，不能写成所有主机稳定既有失败。协调者已将这个最小控制交给 Pi；本报告不等待重跑全套，也不把收敛修复混到 OptA。

## 实际执行与证据清单

本地受控探针/输出均在 ignored `temp_docs/performance-review-04c/`，采用 .NET 10；脚本用其自身路径解析输入，真实 PK 只通过 PskernelSharp + ParasolidScriptHost。以下为实际完成的命令；前两项只读归档/布局，不是 Mac benchmark rerun：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/performance-review-04c/AuditCaptures.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/performance-review-04c/InspectStacks.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file temp_docs/performance-review-04c/CheckStrictMask.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative' --logger 'console;verbosity=normal'
```

最终四项均 exit=0：日志分别 `audit-captures.log`、`inspect-stacks-final.log`、`strict-mask.log`、`baseline-negative.log`。InspectStacks 首次 setup 因临时脚本缺少 ScriptPath 未编译，保留 `inspect-stacks-setup.log/.exit=1`；修正探针后成功，未改被审源码。原始捕获与 trace 是从 Mac 读取的历史证据，不是本审查产出的 45 次新运行。没有启动 NativeAOT，没有终止其它 worker/live publisher 的进程。

| ignored 本地证据文件 | SHA256 |
| --- | --- |
| measure-report.json | `97fb02559286cc4b1a1aeb0641c547a3e64daeeb6492037eac46984ac3abee51` |
| trace-shortedge.speedscope.json | `ed837c54d42604a784fe4a962c195956867376dabd67a4c8e96a0b83f9e634bd` |
| trace-solidblend.speedscope.json | `dabe21b6c06c6f02ec0f34e69b938f74ef5e1d69565ee9b0ddf6fb074d8fa9bf` |
| audit-captures.log | `e0c25adc9e3e9ca2ded45b9a3670b2a7afcad366d69085523d11145f91679c1f` |
| inspect-stacks-final.log | `5044cbdcf62104af10a3028474bcf77126ca1e43c347726825d6cdde9aa71c47` |
| baseline-negative.log | `72d5f001d3a7daebb4f1547915c51a340cf920a89ae827c1913ea48e1cd04fa3` |
| strict-mask.log | `57b7341a8f5ecce692f0a1381f2b40075e2b6f2974ea55ff446e9132052bdf36` |

待办属于后续固定候选：修正报告措辞/基准资格器，补齐 M4 初始化归因和污染负控，保留 Mac 既有失败的精确控制，再审 OptA 新 SHA。没有任何生产优化在本轮获准通过，也没有据本轮五个 case 接受整个建模目标。
