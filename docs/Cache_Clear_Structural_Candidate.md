# 结构优化候选：无 UV 空 read-set 时跳过 cache.Clear（待独立 GPT 把关，未实施）

依据：`perf/zero-clear-diagnostic@661eed9` 的实测计数（原 shortedge 最小代表）
与 `zero-memory-attribution-0ba5a1e.md` 的下一实验建议。**本文件只给候选范围、
证明义务与负控；未经独立 GPT 审核前不删任何 clear、不改 Ready、不加 callee
SkipLocalsInit、不动 rhs/Assemble/导数初始化/证明/容差/预算。**

## 1. 实测动机（计数，负载无关）

- 原 shortedge 单次 GM 操作：`jac-entered = clear-reached = 1,446,213`，
  每次清 22,176B ⇒ **每次操作 ≈32.1 GB 零写入**；clear 累计 503.9 ms（原始
  ticks，含 ~36ms 插桩上界）。
- 分桶：**n=7 uv=False 占 99.4%（1,437,022）**，n=3 uv=False 占 0.6%，
  **uv=True 桶为 0** —— 本 case 全程 cache 读集为空，全部 1.45M 次 clear
  对正确性是纯多余工作（读者集为空）。
- 旧 31.46s/95% 显示归因已被 §4 可否定假设排除为排他 memset CPU；
  本候选不承诺 wall 收益（未测），只承诺**可消除的实际工作量**。

## 2. 全部 cache 读点清查（读集 = 同一 nodes 的 FirstUv/SecondUv≥0 槽）

直接内存访问（`cache[...]`）仅三类，全部索引∈读集：

| 读/写点 | 索引来源 | 说明 |
| --- | --- | --- |
| Bounds:387-388 matcher + CacheSplineContact 写 | `uv=node.FirstUv/SecondUv`，`uv<0` 跳过 | 循环枚举 nodes×sides |
| IntervalSupport:87 IntervalSpline 读（match+Values） | `cacheIndex`，守卫 `>=0&&!IsEmpty` | cacheIndex 只由下列调用者给 |
| `cache.Clear()`（Jac:~372、Bounds:227 条件） | 整段写 | 无读 |

传递消费者（全部传同一 nodes，只经上表访问）：

| 消费者 | cache 到达路径 | 索引 |
| --- | --- | --- |
| IntervalJacobian（nodes） | matcher / IntervalResidual(nodes) | FirstUv/SecondUv |
| IntervalResidual（nodes） | IntervalSpline(cache,uv) + IntervalNormal(nodes,i) | uv=FirstUv/SecondUv |
| IntervalNormal（nodes,occurrence） | SplineUv(nodes[occurrence]) | JointNestedBlendEvaluation:483-486 仅返回 FirstUv/SecondUv 或 −1 |
| CertifyNearestBoxes（nodes） | uv 枚举循环 + IntervalNormal | FirstUv/SecondUv |
| IntervalFrame（nodes,0）/ curvature/acceleration（Bounds:278-279） | IntervalNormal(nodes,0/child) | 同上 |
| BoundSpineSupportSection（nodes,child） | IntervalNormal(nodes[child]) | 同上 |
| witness 路径（capture/require） | 无 cache 读 | 仅 bounds |

⇒ **读集 = 该 BoundSegment 唯一 nodes 列表的 {FirstUv,SecondUv≥0}**；
读集为空 ⇒ 全部读点不可达 ⇒ 跳过 clear 语义保持逐位一致。
（另证实：cache[uv+1] 邻槽从不被读；uv+1 仅是变量槽非 cache 键。）

## 3. 最小生产改动范围（一行条件 + 负控，待审）

- `NestedBlendBounds.cs` IntervalJacobian：`cache.Clear();` →
  `if(HasAnyUv(nodes))cache.Clear();`（HasAnyUv ≤16 步扫描 ≪ 22KB 清零；
  如需可按 BoundSegment 提升一次，但逐调用最简且自含）。
- **不改**：Bounds:227 的 `ReuseSplineContactBounds=false` 调试清零、Ready
  语义、rhs/Assemble clear、导数 seed/reset、全部证明/容差/预算/求值。
- hasUv=true 路径行为**逐字节不变**（保持完整 clear）。

证明义务（供独立 GPT）：§2 清查逐点核对；下游消费者同 nodes 证明；
失败/Unsupported 返回后无后续 cache 读；shared/duplicate UV 与非前缀标量槽
均在读集内；无 epoch/持久化引入。

## 4. 负控（复用既有合同测试 + 新断言）

1. 既有逐位 dump harness 全量：analytic-nested/curved-spine/deep-chain/
   singular 为无 UV 分支、spline-supports 为有 UV 分支，base-vs-guard
   dump **逐字节一致**（含栈污染/反序/早退/cache 历史负控）。
2. 新断言：无 UV 程序下插桩 ClearReached=0；有 UV 程序 clear 计数与 base
   一致（若保留诊断分支计数）。
3. 45pair/S0/S1 历史 capture `--revalidate` 全兼容（无行为变化）。
4. shortedge 实测的 120 个 Jac 失败路径（n=3 桶）输出一致。
5. `ReuseSplineContactBounds=false` 路径不变。

## 5. 重复 Bound 请求（OptB）：已证/未证分离

- **已证**（计数）：224,299 BoundSegment/操作、6.45 Jac/次、expansion
  3.76+refinement 2.69/次、成功率 44.7%。**未证**：不同 BoundSegment 调用
  是否重复同一 (program,surface,spine,box) 键；1.011 bound/rootcell、
  63.48/root 仅是原始比值，不是重复证据；不假设 rootcell 倍率。
- 证明路径：同风格零分配 request-key 普查（键哈希入固定桶计数），纯计数
  负载无关；只有普查证明重复后才能量化可省重评估次数×每次工作量。
  本轮不实施。

## 6. 可消除工作量（不报预计 wall）

- guard：有 UV=0 的操作（shortedge 实测）消除全部 1,45M 次 clear =
  **≈32.1 GB 零写入/操作**；对应实测 clear 累计 503.9ms 原始 ticks
  （含插桩上界 ~36ms）。wall 效果**未测**（排队验证）。
- OptB：待普查，未量化。

## 7. 排队验证（不追负载、不暂停用户进程）

独立 GPT 通过后，向协调端申请**一次独占短窗口**：同 runner/source/DLL/输入
协议（含归因报告的 preflight manifest 表），guard 前后各一次短edge+solidblend
计数/primary 对照 + 逐位 dump；无稳定空窗则不测，报告维持“理论工作量减少、
实际加速未测”。完整对标 PK 目标不变，不凭理论宣称达成。
