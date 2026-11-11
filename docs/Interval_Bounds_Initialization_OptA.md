# OptA：BoundSegment 初始化最小切片（增量报告）

分支 `perf/interval-bounds-initialization`，基线 `perf/modeling-api@04c7ed00f39c09cb10faad132d77e50a6a669f07`
（src 树 `7d516f6c…`、tests 树 `8693efcd…`，与 610e3ce 的内核字节一致）。
历史调查报告 `docs/Modeling_Performance_Investigation.md` 原样保留（附录 A 为口径勘误）。
本文件为本轮 OptA 增量：初始化风险清单、最小方案、逐位 A/B 与负控、JIT 受控对照、
未解决差距。**未合 main、未部署、无 C ABI 变更、未跑 NativeAOT。**

## 1. 范围（已先回报协调端）

- 唯一内核改动：`src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs`
  的 `BoundSegment`（+文件顶部 `using System.Runtime.CompilerServices;` 与契约注释）。
- 合同测试：`tests/KernelTests/NestedBlendBoundsInitializationContractTests.cs`（新增）。
- 配套小提交（reviewer 授权）：`scripts/ModelingPairedBenchmark.cs` 仅修 summary
  `latest` 覆盖口径（只由 measure run 推进，并链接 immutable report），不动 5 oracle、
  不动 `ShortEdgeBlendOracle`（q2 ledger worker 在改）。
- 明确不改：根隔离算法、区间界、容差/预算/证明步骤、缓存 key 语义、classifier、
  `rhs[..n].Clear()`、`Assemble` 内 `matrix.Clear()`、`IntervalJacobian.cache.Clear()`。
- OptB（重复求界合并）/OptC（深嵌套 eval）不在本轮。

## 2. 初始化风险清单（可读槽/写入支配关系）

`BoundSegment` 的 19 个 stackalloc scratch 与读写支配（“活跃前缀”= 调用内真正
使用的 [0..n) / [0..n*n)，n=counts[0]≤63 来自 `EvaluateCore` 打包）：

| scratch（63 基数） | 字节 | 写入点 | 读取点 | 支配证明 |
| --- | ---: | --- | --- | --- |
| `snapshot` TaylorJet[63] | 4544 | EvaluateCore 成功尾 `variables[..n].CopyTo(snapshot)`；anchor 路径 `snapshot[i]=new(...)` i<n | `snapshot[i][0..1]` i<n；`snapshot[nodes[i].Offset+3][0]` i>0 | 成功⇒前缀全写；系数[0]由 Initialize、[1] 由 order 循环写 |
| `occurrences` Occurrence[16] | 448 | EvaluateCore `nodes.CopyTo(nodeSnapshot)` | `nodes[..counts[1]]` 全 7 字段 | 构造 `new(){...}` 显式写全字段 |
| `counts[2]` | 8 | 成功尾 | `counts[0..1]` | 失败提前 return，不读 |
| `point[2]` | 48 | EvaluateCore 输出 | BoundSegment 内无读取 | 纯输出 |
| `fixedCharts[16]` | 64 | 循环 i<nodes.Length（两分支都写） | IntervalResidual/IntervalJacobian/CertifyNearestBoxes 只读 [0..nodes.Length) | 逐 i 支配 |
| `state`[63] | 512 | `state[i]=snapshot[i][0]` i<n | 全部 i<n / [..n) | 前缀支配 |
| `jacobian`[63²] | 31760 | `Assemble`（内部 `matrix.Clear()`+填充 [0..n*n)） | LuFactorize/LuSolveInPlace | **累加语义，Clear 保留**；失败即 return |
| `inverse`[63²] | 31760 | 列循环完成⇒[0..n*n) 全写 | Precondition [..n*n) | 中途失败 return |
| `rhs`[63] | 512 | `rhs[..n].Clear()`+`rhs[column]=1` 每次使用 | 同域 | **累加语义，Clear 保留** |
| `residual`[63] | 512 | Assemble 输出 [0..n) | BoundSegment 内无读取 | 纯输出 |
| `pivots`[63] | 252 | LuFactorize 逐 k 写 `pivots[k]` | LuSolveInPlace 仅在 Success 后 | 逐 k 支配 |
| `boxes`[63] | 1008 | 展开/收缩循环 i<n | [0..n) + 稀疏 `boxes[uv]`、`boxes[uv+1]`、`boxes[parameterIndex]` | 稀疏索引∈[0..n)：打包占用互斥且连续（uv 成对 n+=2、child 4 槽、root XYZ 3 槽），`parameterIndex=child.Offset+3` 必属 child 4 槽 |
| `image`[63] | 1008 | cert 循环 i<n；`image[i]=-equations[i].Derivative` i<n | 收缩循环 i<n；`image[parameterIndex]`；curvature 路径 `image[0..2]`（此时 n≥3） | 同上 |
| `centerF`[63] | 1008 | `centerF[i]=equations[i].Value` i<n（首次 IntervalResidual 成功后） | cert 循环 k<n | 前缀支配 |
| `intervalJacobian`[63²] | 63504 | IntervalJacobian 成功⇒[0..n*n) 全写（逐列） | Precondition/SolveIntervals [..n*n) | 失败⇒continue/return，不读 |
| `remainder`[63²] | 63504 | Precondition 全写 | cert/收缩循环 | 全写支配 |
| `variables` IntervalDual[63] | 2016 | 每个消费前 `variables[i]=new(...)` i<n | 消费者只经 [..n) | 逐前缀支配 |
| `equations` IntervalDual[63] | 2016 | IntervalResidual 成功⇒[0..n)（打包覆盖+末尾 Finite 全读检查） | 成功后读 i<n | 打包覆盖 |
| `splineCache` IntervalSplineContact[63] | 22176 | **IntervalJacobian 入口 `cache.Clear()`（保留）**+CacheSplineContact | IntervalJacobian/IntervalResidual/CertifyNearestBoxes/IntervalFrame/IntervalNormal 只按 nodes 的 FirstUv/SecondUv 读 | 首读必在某次 IntervalJacobian 清零后；**Clear 是承重初始化，保留** |

**外部不变量（已在源码逐条核实）**：
- I1 打包连续无洞：`EvaluateCore` 以 `n+=2/4` 分配且有 `n+2/n+4>MaxVariables` 检查，
  节点/uv 槽并集恰为 [0..n)；`Offset+3`/`uv`/`uv+1`/`parameterIndex` 全部 <n（非前缀索引只在打包域内）。
- I2 EvaluateCore Success ⇒ snapshot 前缀、occurrences、counts 已写（失败路径不读）。
- I3 Assemble Success ⇒ residual[0..n) 全写（内部同样做 `residual[i]` 全读 Finite 检查，依赖 I1）。
- I4 LuFactorize Success ⇒ pivots[0..n) 全写。
- I5 每个调用点：callee 非 Success ⇒ 输出不被读取（BoundSegment 逐处 return/continue）。
- I6 `splineCache` 任意读取前必有一次 `cache.Clear()`（IntervalJacobian 入口；
  `ReuseSplineContactBounds=false` 路径另有全清零）。

**若证明失效则可能读到未初始化内存的全部字段**（即本清单的读取点全集）：
`snapshot[i][0..1]` 与 `snapshot[nodes[i].Offset+3][0]`；`occurrences` 各字段；
`counts[0..1]`；`state/residual/variables/equations/centerF/boxes/image` 的
[0..n) 槽（Assemble/IntervalResidual 的全读 Finite 检查最敏感，任何打包洞都会
变成非确定 NotConverged）；稀疏索引 `boxes[uv/uv+1/parameterIndex]`、
`image[parameterIndex]`、CertifyNearestBoxes 的 `jacobian[map[i]*n+map[j]]`；
`fixedCharts[..nodes.Length)`；`pivots[0..n)`；四个矩阵 [0..n*n)；
`splineCache[uv]` 的 Ready/box/key 字段（仅在 cache.Clear 保留的前提下合法）。
任何“大概不会被读”的槽都不在此列——不能证明的区域保留清零（本轮即 cache.Clear 全保留）。

## 3. 最小方案（本轮唯一优化）

`BoundSegment` 加 `[SkipLocalsInit]`：仅去除 19 个 scratch 的**隐式零初始化**，
不动任何显式写入/清零；不给 callee、不给 module、不给 `IntervalJacobian`。
按 reviewer 窄门：`rhs.Clear`、`Assemble` 的 `matrix.Clear`（累加语义）与
`IntervalJacobian.cache.Clear`（sparse UV 承重）全部保留；
cache 侧减量需先交“任意 matcher 读取前 prepass 使全部可能 scalar UV 槽失效”的
稀疏语义+负控给 reviewer 核，本轮不做（见 §7）。

## 4. 逐位 A/B 与负控（合同测试）

`tests/KernelTests/NestedBlendBoundsInitializationContractTests.cs`：
5 个程序（analytic-nested 2 级嵌套、spline-supports 稀疏 UV 接触槽、
curved-spine 曲脊、deep-chain 双层嵌套、singular 退化）× 5 个盒
（含退化 u0==u1、跨 chart 盒、边角盒），每盒输出 `Bound` 9 项区间上下界
（hex 位）、`BoundSpineAnchor` 状态+4 接触、`BoundSharedSpineWitness` 状态+9 项
witness、`BoundSpine`、`BoundSpineCurvature` 双侧、`BoundSpineProjectedAcceleration`、
`CertifySplineReach` 状态；另有失败路径（非法盒/非法输出/非法脊/退化奇异）。

- **bitwise A/B**：base dump 与 candidate dump **sha256 相同**
  `c5b09e1387b8dafb4cde1a31fd7ea7525e53b36c95d85dbcd244fbbe6980da21`（70 行，0 差异）。
- **负控（candidate 上全过）**：栈污染（递归 8×8KB 垃圾填充后 dump 相等）、
  反序探测（逐行集合相等）、早退后再正常调用（失败 dump 后相等）、
  重复调用/再入（多次相等）、cache 命中/未命中历史（前缀盒结果不随后继失败探测变化）。
- 原九项 A/B 语义由上表 `Bound` 9 项 hex 位覆盖；既有
  `NestedBlendBoundsTests`/`NestedBlendSplineCacheTests` 等原测试保持不动。

## 5. JIT 受控对照（实际删除的初始化）

同过滤触发（`CertifiedTubeEncloses…`）、`DOTNET_TieredCompilation=0`、
`DOTNET_JitDisasm="*BoundSegment*"`、FullOpts 单方法反汇编（base/candidate 同方法签名）：

| 指标 | base | candidate |
| --- | ---: | ---: |
| 零存储站点 `stp xzr` | 54 | 16 |
| 其中成环清零 | 15 个循环 | **0** |
| 循环清零字节/call | **226,288 B** | 0 |
| 单点零存储 | 39（≈624 B） | 16（值存储/`new(x,0)` 类） |

base 15 个清零循环的立即数与 scratch 一一对应：448（occurrences）、512×3
（state/rhs/residual）、1008×3（boxes/image/centerF）、2016×2（variables/equations）、
4544（snapshot）、22176（splineCache）、31760×2（jacobian/inverse）、63504×2
（intervalJacobian/remainder）= 226,288 B（与 reviewer 声明 226,612 B 对齐，差为对齐/小件）。
**结论：本切片确定性删除每 call ≈226 KB 隐式清零**；这些字节在 wall-time 中的
占比由后续受控基准测定，不承诺百分比（trace 显示采样清零主要是 `cache.Clear`，
本切片收益可能小于估算；若实测无收益如实报告）。

## 6. 测试与基线证据

- 受影响 bounds/rolling 单测（NestedBlend*/BlendBound*/RollingBlend*/BezierSurfaceBounds/
  BlendIntersections，60 项）candidate 全过。
- 基线既有失败定位（**Mac 本机观测**）：
  `BlendIntersectionTests.NativeCurveTubeBounds…` 本机有效复验 NotConverged 683 ms
  （04c 干净树）；q2 runtime 10.0.10 同过滤 PASS 1/1 —— 跨 host/runtime 不一致，
  定性 host/runtime/order/config 依赖、未定位；不称已修/全绿、不为 OptA 顺带改。
  证据：`temp_docs/modeling-performance/baseline-test-evidence/`（含一次环境无效的
  970 schema-access 尝试记录，测试体未执行）。

## 7. 未解决差距与下一步

1. **cache.Clear 减量（下一小改，未实施）**：读取集=nodes 的 FirstUv/SecondUv
   枚举（IntervalJacobian/IntervalResidual/CertifyNearestBoxes/IntervalNormal 四处
   同源枚举，SplineUv 亦只返回 FirstUv/SecondUv）；候选语义=在任何 matcher 读取前
   prepass 使“全部可能 scalar UV 槽”（含 shared/duplicate UV、uv+1 邻槽、
   failed-fill 后重试、Jacobian 后的 post 读取者）失效。**按 reviewer 要求，具体
   span/Ready 稀疏语义与负控方案需先交其核准，未核准前保留全清零。**
2. **正式性能前后样本**：待 reviewer 确认窄候选后预约独占窗口；
   计划=shortedge+solidblend 必要样本量（cold×2+warm×5，同 case/同参数/同 API
   资格/fresh input/Release/JIT 一致，raw samples+host load+调用顺序+PK 版本存档），
   sphere/planar 便宜 case 查回归；主表引用 immutable
   `runs/20261111T003330Z-610e3ce/benchmark/report.json` 为 before 基准。
   固定 producer 顺序局限照实声明（GM/PK 轮换在 oracle 层不可改——ShortEdge 由
   他人在改；以多样本抗极小 PK 分母噪声）。
3. 之后一次全量 KernelTests（基线口径：Mac 1857/1858+既有失败单列）；
   R2 证书兼容与 R3 候选（含全局 sin 改动）集成另序，不混入本基线。
4. 目标阶梯（内部观测，非承诺）：实测收益决定是否继续 cache 切片；
   对标乃至超越 PK 的最终目标不变，未测不宣称。

## 8. 复现

```sh
# 合同测试 + 逐位 dump（base/candidate 各一次，diff 为空）
NESTED_BLEND_BOUNDS_DUMP=$PWD/temp_docs/modeling-performance/opta/dump-candidate.txt \
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -p:UseSharedCompilation=false \
  --filter "FullyQualifiedName~NestedBlendBoundsInitializationContractTests"
# JIT 受控对照
DOTNET_JitDisasm="*BoundSegment*" DOTNET_JitStdOutFile=.../jit-candidate-disasm.txt \
DOTNET_TieredCompilation=0 MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release \
  -p:UseSharedCompilation=false --filter "FullyQualifiedName~CertifiedTubeEnclosesPositionsAndFirstDerivatives"
```

产物（`temp_docs/modeling-performance/`，gitignored）：`opta/dump-base.txt`、
`opta/dump-candidate.txt`、`opta/jit-base-disasm.txt`、`opta/jit-candidate-disasm.txt`、
`baseline-test-evidence/`。
