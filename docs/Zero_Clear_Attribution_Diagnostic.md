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
- **早退/异常**：失败 return 先计数再返回；未处理异常同样触发 ProcessExit
  dump（计数不丢）；本次运行如异常，计数与 exit 一并保留。
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

## 4. 运行结果（待填）

（运行后填写：计数、ticks、CPU-wall 边界、可否定假设、未证明事项。）
