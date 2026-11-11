# Zero-clear 归因诊断（perf/zero-clear-diagnostic）

目的（唯一问题）：在原 shortedge 代表 GM 单次操作、无 profiler 条件下，实际
`IntervalJacobian` 入口/clear 次数与 clear 累计壁钟能否解释旧 31s 显示归因。
依据独立报告 `origin/ThraceShah/gm-zero-attribution-0ba@1d82cc75` 的
`docs/reviews/advanced-modeling/zero-memory-attribution-0ba5a1e.md` 实施其
“下一最小实验”第 1–2 项；**不删除/修改任何 clear、证明、容差、预算或求值算法**，
不改 R3/公共 math；计数自身影响性能，产物只作归因，不是 qualified GM/PK 性能数字。

分支基线：已 settled 的 `0ba5a1e7021b9d8bf0e20bfc2aed1ad20a76c7ba`（OptA 保留不动），
本分支为独立诊断树 `gm-modeling-perf-diagnostic`，计时证据树 `gm-modeling-performance`
不切换、不丢证据。

## 1. 插桩清单（可审，逐站点）

新增 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBoundsAttribution.cs`
（partial 扩展 + 线程局部计数器 + ProcessExit 一次性 dump）；
`NestedBlendBounds.cs` 仅插入计数/时间戳语句，无任何既有语句改写：

| 站点（0ba 行号） | 插入 | 语义 |
| --- | --- | --- |
| BoundSegment 入口（158） | `BoundEntered++` | 进入计数 |
| EvaluateCore 成功守卫后（161） | `BoundEvaluated++` | 已取得 joint 状态 |
| expansion 循环体（186） | `ExpansionIterations++` | 每次展开尝试 |
| refinement 循环体（213） | `RefinementIterations++` | 每次收缩细化 |
| BoundSegment 4 处 success return（261/267/313/321） | `BoundSucceeded++` | 成功计数；失败=entered−succeeded（派生，不漏计） |
| IntervalJacobian 入口（368 后） | `attribution` 桶索引 + `State.Entered[…]++`、`JacEnteredTotal++` | 按 n 与任意 FirstUv/SecondUv≥0 分桶（n<<1\|uv，桶 0..127） |
| `cache.Clear()`（原 368） | 前后 `Stopwatch.GetTimestamp()`、`State.ClearReached/ClearTicks`、总计数 | clear 累计 ticks/纳秒 |
| IntervalJacobian 2 处失败 return | `State.Failed[…]++`、`JacFailedTotal++` | 失败保留计数 |
| IntervalJacobian 成功 return（380） | `State.Succeeded[…]++`、`JacSucceededTotal++` | 成功计数 |

`HasAnyUv(nodes)` 只读枚举 nodes 的 FirstUv/SecondUv；桶索引钳制在 0..63×2。

## 2. 计数覆盖 / 线程安全 / 早退

- **覆盖**：IntervalJacobian 入口无条件先计数再 clear，故期望不变量
  `ClearReached==JacEntered`；其 3 个 return 点（2 失败+1 成功）全部挂钩，
  `Succeeded+Failed==Entered` 为闭合不变量。BoundSegment 的 4 个成功 return
  全部挂钩，失败由 entered−succeeded 派生（覆盖所有早退/失败路径，不漏计）；
  expansion/refinement 循环体逐次计数，用于解释 J 的来源（≈1+expansion 至
  6 成功前 +6 refinement 至多 12，报告口径同独立报告）。
- **线程安全**：全部计数 `[ThreadStatic]`（内核会话单线程；跨线程也不共享）；
  dump 用 `Interlocked.Exchange` 保证一次性；热路径零分配、零日志。
- **早退/异常（BCL smoke 实测路径矩阵）**：失败 return 先计数再返回；
  正常返回（exit 0）与 `Environment.Exit`（exit 7）→ ProcessExit dump ✓；
  管理未处理异常 → 本 runtime **无 ProcessExit（实测 exit 134）**，由
  `UnhandledException` 同线程同步 dump 兜底（尽力而为，runtime 损坏时不保证）；
  跨线程 `Environment.Exit` → dump ✓ 且聚合全部 owner 线程（实测 owner=1,4
  全量并入，dump-thread=2 与 owner 不同也正确）；`Environment.FailFast`/
  native 崩溃/信号 → **无 dump 保证**（实测 exit 134 无输出）。
- **开销**：空双时间戳对 = 28.07 ns（本机 smoke 实测，5M 次）；桶计数精确性
  5,000,000/5,000,000 验证通过（`scripts/ClearAttributionSmoke.cs`）。每次 clear
  的计时含 2×GetTimestamp+累加（≥28ns 量级），clear 计时**可能包含 GC 暂停/
  调度等待，不是纯 CPU**；dump 同时输出 process CPU/wall 作上界交叉核对。

## 3. 环境与运行记录

- 私有依赖：从既有成功树按同平台已验证来源克隆 `third_party/`（parasolid schema
  + mac-arm64 PK runtime + PKToy@d820719），未改全局配置、未提交厂商库/凭证；
  缺依赖记 ENV_NOT_READY，不计建模失败。
- 运行：原 shortedge 最小代表 `scripts/ShortEdgeBlendOracle.cs --angle=1
  --radius=0.5 --tolerance=1e-07`，无 profiler/capture observer，primary 检查
  与原 exit 语义保留；单次、上限 5 分钟。
- 记录项：source/tree SHA、实际加载 DLL/PDB SHA256、SDK/runtime/tiering、
  准确参数、UTC 窗口、loadavg、process CPU/wall（运行时填写，见 §4）。

## 4. 运行结果（原 shortedge 最小代表，真实 UTC 2026-01-11 03:49:21–03:49:49Z）

编译 exit=0（AllocationBanCheck 原样通过、零例外）；smoke exit=0；
运行 **exit=0**，primary 检查原样通过（FIX self error=0 faces=2
fault=18391=PK_blend_fault_no_fault_c、FIX PK error=0、PASS cases=1）。
墙钟 28s（含构建/会话/验证；GM 自身 FIX 23,340.6ms），load 4.6→4.2。

### 计数（负载无关主交付）

| 指标 | 值 | 校验 |
| --- | ---: | --- |
| jac-entered = clear-reached | **1,446,213** | 闭合 ✓ |
| jac-succeeded + jac-failed | 1,446,093 + 120 | = entered ✓ |
| bound-entered = evaluated | **224,299** | |
| bound-succeeded / failed(派生) | 100,402 / 123,897 | 44.7% 成功 |
| expansion / refinement iters | 843,699 / 602,514 | ≈3.76 / ≈2.69 每 BoundSegment |
| Jac 每 BoundSegment | ≈6.45 | 在静态 1–12 边界内 |
| out-of-range-n / foreign-thread-touches | 0 / 0 | owner=dump=thread 1 |

### 分桶（n 与任意 FirstUv/SecondUv≥0）

| 桶 | entered | clear-ticks | 成功/失败 |
| --- | ---: | ---: | --- |
| **n=7 uv=False** | **1,437,022（99.4%）** | 500,843,578 | 1,437,022 / 0 |
| n=3 uv=False | 9,191（0.6%） | 3,029,297 | 9,071 / 120 |
| **任何 uv=True 桶** | **0** | — | 本次 case 全程 cache 读集为空 |

### clear 累计与旧 31s 的可否定假设

- clear 累计 **503,872,875 ticks ≈ 503.9 ms**（首 clear 26µs 单列；
  每次 ≈348 ns 清 22,176B ≈64 GB/s 有效带宽）；instrumentation 双时间戳
  开销 ≈25 ns/次（smoke）≈36ms/总量，已含在 ticks 内为上界。
- GM 操作墙 23,340.6 ms ⇒ **clear ≈ 2.2%**，显著小于旧 trace 显示归因的
  31.46s/95%。**可否定假设成立**：旧“95% 时间在可删除清零”不成立——
  旧数字是 Speedscope 合并显示区间 + CLR 采样在 QCall/安全点的挂起偏置 +
  ZeroMemory wrapper（STOP_FOR_GC 等待）共同形成的显示归因，不是排他
  memset CPU。本数据不再追加 profiler/OS 采样即可否定该占比解释。
- ticks 为原始诊断（含 GC/调度暂停可能），wall 亦然；按指令不用作成本占比
  或提速结论的进一步换算——唯一用途即上述量级否定。
- 注意：计数扰动 wall（每次 entry 多 ~25ns 时间戳 + 计数），绝对 wall 与
  历史值不可比；**计数与占比关系负载无关**。

