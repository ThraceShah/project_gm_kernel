# 建模性能调查：cold/warm 公平基准、热点证据与优化路线

固定版本 `610e3ce844bf99c85af6069905f9f3deaab08133`（上线 main 对应树，工作树
`gm-modeling-performance`，分支 `perf/modeling-api`）。本机 Apple M4（10 核）/ 24 GB /
macOS / .NET SDK 10.0.400 / runtime .NET 10.0.11 / 真实 Parasolid 38.0.150 /
PKToy `d820719`。所有测量串行执行，运行前后记录 `vm.loadavg`
（开始约 2.2/1.8/1.7，结束相近；桌面其它进程未停止，数值仍为本机固定输入观测）。
未运行 NativeAOT 发布（无 C ABI 改动），以下均为托管 JIT 运行时数据，不能外推 AOT 速度。
原始日志、capture、trace 与机器可读 JSON 在 `temp_docs/modeling-performance/`
（gitignored），协议与结论可按第 9 节命令逐条复跑。

## 0. 结论摘要

1. 平台“数百/上千倍”对两个代表 case **是测量口径产物，不是真实吞吐差**：
   analytic/planar Boolean 热态 GM **已与 PK 持平或更快**（warm ratio 0.25×/0.76×），
   平台 43–47× 全部是单次 cold 样本的首调用 JIT/静态初始化。
2. 实体建模（blend/curved boolean/short-edge）的差距**是真实算法工作量差距**：
   warm≈cold、工作量计数逐 rep 一致，ratio 451×/909×/13720×（warm）。
   “基础 surfeval/curveeval 能对标”在本机只对**普通/浅层**求值成立：
   `nurbs-blend.x_t` 吞吐 70–123%、`independent-torus-depth3.x_t` 101–179%（过 70% 门），
   但最深嵌套 fixture `nested-blend-native/depth3.x_t` D0 吞吐仅 **18.6%**
   （self 38.5 µs/点 vs PK 7.2 µs）—— 同一验证基准今天在本机是**红的**，
   逐 eval 固定额外工作（疑为 chart/Newton 逐点重搜，未逐位定位）与建模侧
   “调用次数 × 单次证明成本”同根。
   short-edge 单次圆角 223,331 次 Bound 求值、220,895 root cells，91.4% 时间在
   Bound 区间求值；其中 CPU 采样 95% 落在 `BoundSegment → IntervalJacobian` 的
   **内存清零**（约 330 KB/call × 446k ≈ 147 GB memset）。
3. 平台 2187.57× 是**多 step 求和比**（圆角 + 后续 Boolean 两步合计），
   按 step 分解后 blend ≈1100×、切削 Boolean ≈4200×（本机 cold），
   与独立审计 `56825d6` 的原始分量（Fix 1863.9/1.6 ms、Boolean 3905.9/1.0 ms）同构。
   标签混合不成立“所有百倍都是假”：分解后真实差距仍然很大。
4. 全部 top 热点都在 R3 正在改的受保护文件
   （`NestedBlendBounds.cs`、`SheetArrangement*`/`RollingArrangement`、核心 classifier），
   本阶段**不改内核**；已给出精确到函数/字节量的串行优化提案（第 7 节）。
5. cold 是“同进程输入构造已预热部分代码后的首个 API 调用”，不是完全无 JIT；
   cold−warm 差值用逐 rep 工作量计数一致 + 采样证据归因，不把全差无证据地叫 JIT
   （analytic boolean 14.7 ms→0.05 ms 全是首调用开销；short-edge 16.75 s→16.08 s
   仅 4%，是算法时间）。

## 1. 既有指标/目标准入审计

| 指标/门 | 范围 | 状态 | 缺口 |
| --- | --- | --- | --- |
| `scripts/NestedBlendBenchmark.cs` 吞吐门 `PKtime/GMtime≥0.70` | 仅规则 window D0/D1/D2 求值（2048 warmup、31 批×512 点、p50/p95、GM-PK 轮换、零分配） | **本机复跑：nurbs-blend 70–123%、independent-torus-depth3 101–179% pass；depth3.x_t D0 18.6% FAIL**（self 38481 ns/点 vs PK 7172 ns） | 只规定求值，不是建模；深度嵌套求值未达标；fixture 由 NestedBlend(Extended)Oracle 生成于 temp_docs（不入库） |
| `docs/Modeling_Performance.md` 历史（`cd01bdb` 等） | 重型 Boolean bound-cache A/B（215.7 s warm，JointBound 200.7 s、953,971 请求/209,599 执行）、stack jet 复用 −33%、command cache −21–54% | 历史固定输入观测 | 非当前跑分、非统一 70% 门 |
| 平台 `performance` 字段（`scripts/RefreshWebProgress.cs` → capture 求和取中位） | operation-only，`samples=1 warmup=0`，cold | 在用 | 单 cold 样本、PK 分母 0.4–20 ms 抖动巨大、多 step case 用求和比、无 warm |
| `ModelingPerformance` PROFILE 计数（pairs/ssi/root/roots/winding/bound 请求/执行/命中/bound-ms/stages） | RegularBoolean + FixBlends 两条链 | 本次已接出 5 个代表 oracle（`--bench-profile`） | 平台 case 未带 profile；无“建模操作官方目标” |
| 正式建模目标 | — | **缺** | 建议见第 8 节（分 case 目标，不自设硬门） |

## 2. 计时口径审计（Stopwatch 边界）

- **平台字段来源**：oracle 在 API 调用点包 `Stopwatch`（如
  `RegularSolidBlendOracle.Fillet` / `ShortEdgeBlendOracle.Fillet` / 各 `Produce`），
  `WebProgressCapture.After/BooleanAfter` 收到的 `webElapsed` 即纯 API 区间；
  `RefreshWebProgress` 把 capture `timings` 按 kernel 求和、跨样本取中位。
  Observer（mesh/快照/BODY_check/compare/mass）在计时区间**之外**。
- **stdout `TIME self/PK ... ms=`/`FIX ... ms=` 行含 observer**：打印发生在
  `WebProgressCapture.After` 之后，实测同一调用 capture=3.32 ms 而 TIME 行=13.32 ms
  （PK 侧 observer ≈10 ms）；GM 侧 1631.08→1686.80 ms（observer ≈56 ms）。
  比率一律用 capture/BENCH `api-ms`，不用 stdout 行。
- **BENCH 相位行**（本次新增插桩，`--bench-repeat` 时打印）：
  `prepare-ms`（输入构造+检查）/`api-ms`（两次调用点墙钟，含 mark+选择+observer）/
  `verify-ms`（独立验收：fresh XT、BODY_check、strict compare、物性）/`case-ms`。
  纯 API ms 以 capture 为准。例：plane-nurbs rep0 prepare 18.6 / api 1743.9 /
  verify 53.6 ms；rep1 prepare 0.56 ms（输入构造冷热差 18 ms，其余是首调用开销）。
- **cold 定义**：fresh 进程首个被计 API。输入构造（GM/PK 各自构造或共享 XT receive）
  已执行部分几何代码，所以 cold ≠ 全进程无 JIT。warm 定义：同进程 `--bench-repeat`，
  rep0 记为该进程 cold，rep1..N 计 warm；每 rep **新建未操作输入**，
  命令证明缓存按内核契约每条命令清空，不在已修改 body 上重复执行。
- **独立操作验收**（按用户操作分层要求）：每 case 都是**单操作**对真实 PK 同参数
  独立验收（fresh XT receive + 全量 BODY_check=0 + `PK_DEBUG_BODY_compare`
  max_diffs=1024/all=false/acc_dev=false/tol_dev=true + 计数/type/sense/容差/missing/
  nonmatch + 物性），blend 只对 blend、Boolean 只对 Boolean；组合链另列（第 5 节）。
  任一验收不合格则该样本 ratio 记 unqualified/null（本轮 5 case 全部 qualified）。

## 3. 采样协议（供审计 ctx_799ccc532244 复用）

- 每 case：cold = 3 个独立 fresh 进程 + warm 进程 rep0（共 4 个 cold 样本）；
  warm = 单进程 rep1..5（5 样本）；串行、cheap case 在前（sphere→planar→curved→
  solidblend→shortedge）。
- GM/PK 同进程同线程同参数同精度：sphere/planar/curved 为 GM 先、PK 后
  （各自按相同数值参数独立构造）；solidblend/shortedge 为 PK 先、GM 后
  （short-edge 两侧接收同一 PK 种子 XT）。调用顺序随 case 记录在 JSON
  `operation.callOrder`，不做跨机相除。
- 计时口径：capture `timings`（operation-only）；相位行与 process wall 另记。
- 资格：进程 exit=0、无 `FAIL `、capture checks 全 pass/diagnostic、
  gm/pk 成对且 error=0。比率=逐 rep GM÷PK 的中位 + 中位比（两个都报）。
- 离散度报 min/p50/p95/max 并标注小样本（n=4/5 时 p95≈max）。
- 每样本存 oracle.log（sha256）、capture.json、loadavg 前后值；
  `scripts/ModelingPairedBenchmark.cs` 输出机器可读
  `temp_docs/modeling-performance/paired-benchmark-latest.json`。

## 4. cold/warm 公平 ratio 表（本机 610e3ce，全部 qualified）

operation-only，毫秒；ratio = GM÷PK（逐 rep 中位）；n=4 cold / 5 warm。

| case（单操作） | GM cold p50 | GM warm p50 | PK warm p50 | cold ratio | warm ratio | 判定 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| sphereBoolean.equal-overlap（两球相减） | 14.7 | **0.051** | 0.208 | 47× | **0.25×** | 热态快于 PK；平台 43–47× 为 cold 假象 |
| planarBoolean.overlap-two-axes | 31.7 | **0.386** | 0.480 | 43× | **0.76×** | 热态与 PK 持平 |
| regularcurved.block-subtract | 1034.5 | 617.0 | 1.375 | 665× | 451× | 真实差距（Arrange/Components） |
| solidblend.plane-nurbs（圆角） | 1548.8 | 984.8 | 1.104 | 1086× | 909× | 真实差距（Bound 求值 52%） |
| shortedge.a1-r0.5（短边圆角） | 16749.5 | 16082.2 | 1.173 | 10849× | 13720× | 真实差距（Bound 91%） |

离散度（warm GM min–max）：sphere 0.048–0.056、planar 0.367–0.410、
curved 614.7–620.8、solidblend 983.6–1015.7、shortedge 16041–16139 ms；
PK warm p50 0.208/0.480/1.375/1.104/1.173 ms。cold GM 离散度 ≤6%（JIT 稳定）；
PK cold 分母抖动大（solidblend 1.42–1.47 ms；平台历史同 case 曾 1.5–18.4 ms，
单样本 ratio 因此在 85–1018× 间摆动——平台 ratio 必须带样本数与冷热标注）。

组合/集成场景（平台 case，单列，不并入上表）：
`blend-chain.chain-2-base-self-source`（810×）、`support-chain.asymmetric43-base`
（580×）、`trivalent-corner.base-self`（685×）等为多操作链 cold 单样本，
链内每步独立验收已 pass；链路端到端比率不代表单步，后续由 Capture/Refresh
逐操作归属 worker 补步骤级展示。失败样本
（`short-edge.standard-strict-lifecycle` 44.3 s、`transverse-witness-lifecycle`
72.4 s，GM fail）按规则 ratio=null，时间仅诊断。

## 5. 多 step 求和比归属（2187.57× 实例）

`solid-blend.plane-nurbs-modified-support-bore` 含两个被计 step
（`PK_BODY_fix_blends` + `PK_BODY_boolean`）。平台 `gmMs/pkMs` 为 step 求和，
ratio 2187.57 = 求和比。本机 `--bench-repeat=2` 逐步分解（capture 纯 API，
checks 108/108 pass）：

| step | GM cold / warm | PK cold / warm | cold ratio | warm ratio |
| --- | ---: | ---: | ---: | ---: |
| `PK_BODY_fix_blends`（圆角） | 1645.9 / 1039.9 ms | 1.48 / 1.12 ms | 1112× | 926× |
| `PK_BODY_boolean`（后切削） | 3405.7 / 3331.5 ms | 0.83 / 0.78 ms | 4129× | 4238× |

与正式审计 `56825d6` 原始分量（Fix 1863.865/1.6129、Boolean 3905.8619/1.0246，
总 2187.574）同构：**标签混合属实，但每步真实差距仍为 10³–10⁴ 量级**；
这些 cold 分量缺独立 primary owner，只作诊断标签，不追造 qualified 单步 ratio
（qualified 单步数据见第 4 节自己的独立 case）。

## 6. 热点证据（工作量计数 + CPU wall 采样）

PROFILE 计数（warm rep，`--bench-profile`；括号为占 warm API 比例）：

| case | pairs | ssi-cells | root-cells | roots | winding | bound 请求/执行/命中 | bound-ms | stages |
| --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- |
| sphere/planar Boolean | 0 | 0 | 0 | 0 | 0 | 0/0/0 | 0 | 全 0（走解析捷径） |
| regularcurved | 36 | 7 | 1092 | 84 | 155 | 4684/0/0 | 0 | **Arrange 453.7(72%)**、Components 158.5(25%)、Split 6.7、Clip 3.0 |
| solidblend 圆角 | 0 | 0 | 478 | 8 | 18 | 1502/1130/54 | 524.5（52%） | （FixBlends 无 boolean stage） |
| shortedge 圆角 | 0 | 4956 | **220,895** | **3518** | 196 | 275,525/223,331/30,727 | **14,711.7（91.4%）** | 同上 |

逐 rep 计数完全一致（证明工作量恒定，cold−warm 差值不是工作量）。
单次 Bound 成本：shortedge 65.9 µs/eval、solidblend 464 µs/eval；
命中率 11%/4.8%（该 case 命中条件严格，key 含精确 UV 四端点/generation/sense）。

CPU wall 采样（dotnet-trace `dotnet-sampled-thread-time`，100 Hz，事件栈自底聚合）：

- shortedge（两 rep 33.1 s）：`BodyFixBlends → FixLocalBlendsCore →
  BuildInteractingRollingBlends → BuildRollingArrangement(95.9%) →
  JointNestedBlendEvaluation.BoundSegment(95.4%) → IntervalJacobian(95.1%) →
  `Buffer.ZeroMemoryInternal`(95.1%)；消费侧 `SurfaceIsoPlaneRoots.Find`(88.9%)、
  `IntersectFiniteIntersectionCurvePlane`(74.7%)、`SheetLoopWinding`(55.7%)。
- solidblend（三 rep 3.9 s）：BoundSegment 65.7% → IntervalJacobian 51.7%；
  `ZeroMemoryInternal` 30.3%、`BezierSurfaceBounds.Restrict/Bound/RestrictCurve`
  24–29%、`IntersectSheetCurvePlane`/`SurfaceIsoPlaneRoots.Find` ≈25%。

**定量分解（shortedge warm 16.08 s）**：
Bound 区间求值 14.71 s（223,331 次 × 65.9 µs，91.4%）
+ root/winding/拓扑等 ≈1.37 s（8.6%）。其中 memset ≈70 µs/call：
`BoundSegment` 每调用 stackalloc 合计约 210 KB（`MaxVariables=63`，
63×63 double/IntervalNumber 矩阵 4 块 = 158 KB，TaylorJet[63]≈4.5 KB 等）
+ `IntervalJacobian.cache.Clear()` 63×~340 B×≈6 次 ≈126 KB，
合计 ≈330 KB/call × 446k 次（两 rep）≈ 147 GB 清零 —— 与采样 31.5 s 自洽。
top3：① Bound/IntervalJacobian 的清零与区间算术（blend 类 52–91%）；
② Arrange/Components（curved boolean 97%）；③ 消费侧 root 隔离/绕数重复请求
（220k root cells ↔ 223k bound 执行 ≈ 每 root 60+ 次重求）。

## 7. PK 参照与动静交叉验证

- qingyu2 反编译 MCP 从本 Mac **可访问**（`https://qingyu1-linux.tail77e06.ts.net/
  apps/parasolid-query/mcp`，initialize/tools/call 均 200）；技能
  `parasolid-research.SKILL.md` 已读。Mac ARM 不能直接用 Linux 反编译地址/GDB
  动态层（`libpsk_runtime.so`/`psk_gdb_debug.py` 为 Linux 侧，q2 可辅助）。
- `parasolid_get_function_detail PK_BODY_fix_blends`：0x279c30、21,926 B、
  签名/选项结构与 PskernelSharp 绑定一致；`get_internal_callgraph`（depth 2）
  显示其调用 `BLE_for_all_blended_edges`（0x9d4810）等 53 项 —— 与协调端
  静态结论一致。`BLE_for_all_blended_edges` 调用图仅见 ASS_/LIS_/DS_ 直接调用
  （寄存器间接调用未解析；空/稀疏图 ≠ 无调用，推断均标注为推断）。
- 动态侧交叉验证：本机真实 PK 每次 `PK_BODY_fix_blends` 1.0–1.8 ms、
  `PK_BODY_boolean_2` 0.2–1.6 ms（warm），与 GM 计数对照 → PK 同样走
  edge-mark→fix 流程但无“区间证明式”bound 请求量级；GM 的 223k bound/220k root
  cells 是证明工作量差异而非 API 面差异（推断，未在 PK 二进制内逐步定位）。

## 8. 可并行 / 必须串行

**本阶段已并行落地（非内核）**：5 个 oracle 的 `--bench-repeat/--bench-profile`
可选插桩（默认行为逐字节不变）、`scripts/ModelingPairedBenchmark.cs`、本报告与
JSON 产物、计时口径修正（明确 stdout 行含 observer，比率一律用 capture）。

**必须串行（全部 top 热点在受保护文件）**：
`src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs`
（=R3 “NestedBlendBounds”，含 `BoundSegment`/`IntervalJacobian`）、
`KernelRuntime.SheetArrangement*`/`RollingArrangement`（Arrange）、核心 classifier。
按规则先报范围与交叉风险，由协调端决定顺序；以下为精确提案：

- **优化 A（memset 消除，预计 blend 类 ≥30–50% 降幅）**：`NestedBlendBounds.cs`
  内 `BoundSegment`/`IntervalJacobian` 加 `[SkipLocalsInit]`，并把
  `cache.Clear()` 改为只清 nodes 引用的 UV 槽或 generation 戳；
  **注意 `IntervalSplineContact.Ready` 当前依赖零初始化**，必须显式初始化全部可命中槽，
  否则会错误命中缓存产出错误区间 —— 必须做九项区间逐位 A/B
  （历史 harness：Plane/NURBS、NURBS/NURBS rolling Bound 逐位一致），
  再全量 KernelTests + 各 oracle 硬门。
- **优化 B（探索，动证明结构，需证明 review）**：root 隔离/绕数对同一
  (cell,box) 的重复 Bound 请求合并（220k root cells↔223k 求值），
  或 winding/root 间增量复用区间；不减任何证明步骤、不放容差。
- **优化 C（深度嵌套求值，新证据）**：`nested-blend-native/depth3.x_t` D0 求值
  38.5 µs/点（torus-depth3 仅 10.8 µs），逐 eval 固定额外工作 ≈5×；
  疑为 `JointNestedBlendEvaluation.EvaluateCore` 逐点重做 chart 选择/Newton
  起步（未逐位定位，需采样/单测确认）；`JointNestedBlendEvaluation.cs`
  不在 R3 文件清单内但属核心文件，按规则先报范围由协调端定序；
  目标：depth3 D0 达到 ≥70% 门（先修红再谈超越）。
- **串行顺序**：R3 候选冻结+独立 review → 从固定 SHA 开 perf 分支 →
  A/B 多样本 + main 几何硬门（BODY_check=0、strict compare、物性）→
  不同模型 review → 合入；不得在已知正确性回归的版本宣称完成。

## 9. 可测目标建议（基于本轮数据，不自设硬门）

| case | 现状 warm GM/PK | 分阶段可测目标（提案） | 主瓶颈 → 手段 |
| --- | ---: | --- | --- |
| sphere/planar Boolean | 0.25×/0.76× | 保持 ≤1× | 已达标；防止回归即可 |
| 基础/浅层求值（nurbs-blend、torus-depth3） | 吞吐 70–179% | 保持 ≥70% 门 | 已达标 |
| 深嵌套求值（depth3 D0） | 吞吐 18.6% | **先回 ≥70% 门（修复红门）** | 逐 eval 重搜 → C |
| regularcurved | 451× | 优化 A 后复测；Arrange 增量证明复用后以 2× 阶梯收敛 | Arrange 72%+Components 25% → B |
| solidblend 圆角 | 909× | 优化 A 后 bound 52% 部分预期减半，先到 ≤600× 再定下一阶 | bound 52%（其中 memset 30%）→ A |
| shortedge 圆角 | 13720× | 优化 A 先到 ≤7000×（memset≈55% API）；B 后以 ≤2000× 为下一阶 | bound 91% → A、B |

UI/平台建议：`performance` 同时展示 cold/warm 与样本数（当前 `samples=1 warmup=0`
的 ratio 不得当稳态吞吐）；多 step case 按 step 展示 gm/pk 与 ratio
（与 56825d6 原始分量一致），组合链单独标注；保留历史原结果与 checkVersion，
不改已发布数据。目标“对 PK 持平乃至超越”（GM/PK≤1）适用于每个独立操作：
解析/平面 Boolean 已达成；实体建模按上表阶梯推进，未达成前不宣称。

## 10. 复现与产物

```sh
# 全部 5 case：cold×3 + warm×5（空格分隔参数；~25 分钟）
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ModelingPairedBenchmark.cs -c Release -p:UseSharedCompilation=false -- --cases sphereBoolean,planarBoolean,regularcurved,solidblend,shortedge --cold 3 --warm 5
# 热点计数（每 case 1 进程 × 2 rep）
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ModelingPairedBenchmark.cs -c Release -p:UseSharedCompilation=false -- --mode profile --cold 1 --warm 1
# 多 step 归属
MSBUILDDISABLENODEREUSE=1 GM_WEB_CAPTURE=$PWD/temp_docs/modeling-performance/multistep GM_WEB_ROOT=$PWD dotnet run --file scripts/RegularSolidBlendOracle.cs -c Release -p:UseSharedCompilation=false -- plane-nurbs --event-arrangement --remote-bore --require-cut-section --bench-repeat=2
# CPU 采样（工具装在 temp_tools/，不改全局配置）
dotnet tool install --tool-path temp_tools dotnet-trace
MSBUILDDISABLENODEREUSE=1 dotnet build scripts/ShortEdgeBlendOracle.cs -c Release -o temp_docs/modeling-performance/tracebin/shortedge
MSBUILDDISABLENODEREUSE=1 temp_tools/dotnet-trace collect --profile dotnet-sampled-thread-time --duration 00:00:01:40 --format Speedscope -o temp_docs/modeling-performance/trace-shortedge -- dotnet temp_docs/modeling-performance/tracebin/shortedge/BooleanOperationOracle.dll --angle=1 --radius=0.5 --tolerance=1e-07 --bench-repeat=2
# 求值吞吐门（先生成 temp fixture，再逐 fixture 跑；depth3 会抛 gate failed）
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/NestedBlendOracle.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/NestedBlendBenchmark.cs -c Release -p:UseSharedCompilation=false -- --fixture=nurbs-blend.x_t
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/NestedBlendBenchmark.cs -c Release -p:UseSharedCompilation=false -- --fixture=nested-blend-native/independent-torus-depth3.x_t
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/NestedBlendBenchmark.cs -c Release -p:UseSharedCompilation=false -- --fixture=nested-blend-native/depth3.x_t
```

产物（均相对仓库根，`temp_docs/` gitignored）：
`temp_docs/modeling-performance/paired-benchmark-latest.json`（主表机器可读，
含逐样本 gmMs/pkMs/ratios/资格/loadavg/log sha256/pkVersion/commit/nativeTreeHash）、
`runs/<runId>/benchmark/report.json` 与 `cases/*/oracle.log|capture.json`、
`profile-run.log`、`multistep.log`+`multistep/capture.json`、
`trace-shortedge(.speedscope.json)`、`trace-solidblend(.speedscope.json)`、
`smoke/`（早期复核样本）。

## 11. 版本对照与基线状态

- 本轮按“先测上线 610”只测 `610e3ce`。R1/R2 分支（含未上线的 10-station 证书
  2.5–4 s/8.8–10.9k boxes）不解释当前平台倍数，仅记为未来 candidate 额外成本；
  R3 candidate `9b2f4c6` RollingNetwork 16/19（含 Boolean 157）未整体验收，
  不作 qualified 速度基线，后续对比须逐 case 正确性标签、新旧分报。
- 基线单测：`tests/KernelTests` 1857/1858 pass；既有失败
  `BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative`
  （Assert Expected Success / Actual **NotConverged**，BlendIntersectionTests.cs:155）
  在 610e3ce 独立复跑稳定失败，属基线既有问题（与 bounds 收敛域相关，未修，
  与本调查改动无关；建议归并到 bounds 优化线一并处理）。

## 附录 A（勘误与口径更正，2026-01-11，不修改原文/原 trace/原样本）

独立 review 与协调端重算后，以下口径在本文正文写得不准，以本附录为准：

1. **Bound 成本口径**：14711.7 ms/223,331 = **65.874 µs/次是 Bound 评估的总成本**
   （BoundTicks 包含其内部全部工作，含区间清零），不能再叠加“memset ≈70 µs/call”
   —— 二者是同一时间的两种分解，正文第 6 节的加法式并列有重复计算之嫌。
2. **bound/rootcell 比**：223,331/220,895 = **1.011 次 bound/记录 root cell**，
   正文“每 root 60+ 次重求”是把 root（3,518 个）与 root cell（220,895 个）混用；
   ≈63.48 是**每记录 root 的 bound 评估数**。另 `BoundEvaluations` 是高层
   BoundCore 计数，一次可能展开多个 `BoundSegment`，故“evals→call”换算只是上界。
3. **清零量口径**：reviewer x64 实测 sizeof：interval 16/dual 32/jet 72/contact 352；
   `BoundSegment` 声明 stackalloc 合计 **226,612 B**（本机 ARM64 JIT 反汇编零初始化
   循环合计 226,288 B+单点 624 B），`cache.Clear()` 22,176 B/次；
   成功路径 `IntervalJacobian` **≥7 次**（1 expansion + 6 refine）而非 6 次。
   “≈330KB/call、≈147GB”仅为估算口径，**未实测验证 traffic**；
   trace 中 ZeroMemoryInternal 归属（31.4569/33.0606 s=95.15% inclusive，
   直接父帧 IntervalJacobian ≈31.23 s）证明采样到的清零主要是 `cache.Clear`，
   不能把 95% 解释为全部可去除，也不承诺 30–50% 收益。
4. **depth3 求值红门**：本机复跑（`nurbs-blend`/`independent-torus-depth3` pass、
   `depth3.x_t` D0 18.6%）为 **Mac 平台观测**；当时原始 per-fixture 日志与
   hash-at-run 未完整存档，q2 未独立证实；定性保持“reported platform observation”，
   不作跨 host 结论。
5. **基线单测失败定性**：补充证据（q2 .NET SDK 10.0.302/runtime 10.0.10 同过滤
   **PASS 1/1**；本机 SDK 10.0.400/runtime 10.0.11 有效复验 **NotConverged 683ms**）
   后，原“基线 1857/1858、既有失败”限定为 **Mac 本机已观测**，失败为
   host/runtime/order/config 依赖、根因未定位；不称已修、不称全绿。
   证据在 `temp_docs/modeling-performance/baseline-test-evidence/`。
