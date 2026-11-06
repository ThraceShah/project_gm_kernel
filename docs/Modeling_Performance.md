# 完整建模性能与证明计数

基线为 `cd01bdb`，环境 Linux x64、.NET SDK 10.0.302、真实 Parasolid 38.0.150。
本轮没有 C ABI 改动，不运行 NativeAOT 发布。验收继续使用独立 PK、fresh XT、
完整体检查、结构/类型/sense/容差/missing 和物性硬条件；性能不替代正确性。

## 记录的范围

`scripts/ModelingPerformanceOracle.cs` 对相同制造参数运行缓存关闭/开启两种模式，
每个模式在独立 worker 中构造并执行两次。cold 表示该 worker 的第一次 Boolean；
输入构造已执行部分几何代码，并非整个进程从未 JIT。第二次为同进程 JIT/池预热，
但每次都重新构造实体，命令缓存每次从空表开始。两轮都执行同一套严格 PK 对照。

外层 `TIME self` 是完整 API 时间，包含调度、拓扑替换和返回内存。
`PROFILE` 记录规则 Boolean 的 Read/Cuts/Clip/Split/Arrange/Classify/Components
阶段，以及 support pair、SSI cell、标量/方程根 cell、根候选、winding、预算耗尽、
procedural patch Bound 请求、实际 Joint Bound 执行及其耗时。
Components 当前包含装配和发布阶段。根候选包含分割边界的重复发现，不等于最终边数。

cell 数是全操作累计值，不是统一总预算。公共 SSI 保留每 support pair 的
200,000 cell 预算，单次根隔离保留 12,000 cell 预算；平面标量图保留 32,768
局部预算并消费调用者共享预算。缓存命中不降低证明要求或跳过任何拓扑步骤。

## 命令拥有的认证界缓存

规则 Boolean 在输入几何不变的阶段建立临时缓存。key 包含真实实体 tag、pool
generation、sense、程序 root/spine 类别和精确 mapped UV 四端点。不同 clone 可以
miss；不以采样、模型名或近似几何相等提高命中率。只保存成功认证的 XYZ/Su/Sv
九项区间，命中后仍执行原来的 iso、参数射线或辅助扫掠坐标变换。

4096 个槽使用约 0.8 MiB 可选原生临时内存，在命令结束时释放；分配失败直接
回到原计算，不占用求解器原有的固定 scratch 预算。所有 key/value 在下一条命令
重新清空，因此变换、编辑、重载、删除与 tag 重用之后不会沿用旧结果。
只有显式标明 `immutableGeometry:true` 的 scope 可以启用缓存；可能修改几何的
scope 默认只记录 profile。零托管分配与 AOT 兼容性保持不变。

单元测试覆盖同界逐位比较、generation/sense/root/类别/四个端点失配、跨命令清空、
精确 iso wrapper 复用以及可选内存分配失败退回。

## 完整 API A/B 结果

以下 command-cache 实验尚未包含后述 BoundSegment 栈内 jet 复用，因此可以单独归因。
时间是本轮固定输入观测，不作为跨硬件的稳定基准或统一 70% 门槛。

| 输入 | 模式 | cold API | warm API | 实际 Joint Bound cold/warm |
| --- | --- | ---: | ---: | ---: |
| M1 圆角体减 x≥.55 方块 | 关闭 | 10.349s | 11.045s | 6718 / 6718 |
| 同输入 | 开启 | 8.114s | 6.520s | 3523 / 3530 |
| 两个真实圆角体，第二体平移 (.07,.03,.02)，相减 | 关闭 | 421.777s | 467.101s | 393121 / 393121 |
| 同输入 | 开启 | 217.664s | 215.724s | 209599 / 209549 |

重型 Boolean 的完整 API 降时约 48%/54%。两种模式都严格通过 PK 对照，且以下
证明工作量完全一致：49 pair、36,017 SSI cells、236,851 root cells、1880 根候选、
214 次 winding、0 次预算耗尽。九项 bound 请求均为 953,971 次，缓存冷/热分别
命中 183,522/183,572 次。Joint Bound 耗时由 405.799/449.732s 降为
201.622/200.683s，反映了原输入的主要公共热点。

冷次阶段时间如下：

| 阶段 | 关闭 | 开启 |
| --- | ---: | ---: |
| Cuts | 199.925s | 138.358s |
| Clip | 79.372s | 26.883s |
| Arrange | 90.523s | 30.191s |
| Classify | 13.874s | 10.138s |
| Components/装配/发布 | 38.056s | 12.066s |

## 单次 Joint Bound 内部的 spline jet 复用

最后一次区间 Jacobian 已得到固定 UV 盒中的 18 项未播种笛卡尔 spline jets。
后续 nearest-foot、参数导数 residual 和两次截面 frame 复用这些相同的 jets；
Dual 导数种子每次重新组合。key 核对实际 unwrapped UV 盒和 spline basis 的
scalar offset、degree/count/dimension、rational/periodic 属性。它只活在一次 Bound
调用的栈上，不改变自然域外线性延续公式，也不缓存已播种结果。

制造解覆盖 rational/nonrational、不同 Dual 种子、支持 sense/offset 复用、不同 UV
盒或不同 basis 的拒绝；完整 Plane/NURBS 和 NURBS/NURBS rolling Bound A/B
九项上下界逐位一致。真实 M1 NURBS Blend 的同进程热测，2000 次 Joint Bound
由约 1.60–1.63s 降为 1.06–1.09s，约降 33%，上下界 bits 相同。

关闭 command cache，独立严格的“首圆角 → 后部 notch Boolean → 新生曲边再圆角”
两轮 A/B 均通过。第二次完整 FixBlends 从 2.454/1.824s 降为 1.461/1.473s；
中间 Boolean 从 5.078/4.579s 降为 4.117/3.773s。首圆角从 .994/.585s 降为
.960/.531s。不能把这些单边输入的收益外推为所有角点或所有建模操作的性能。

## 复现

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/ModelingPerformanceOracle.cs -c Release -- blend-subtract
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedCurvedBooleanOracle.cs -c Release -- --profile --no-bound-cache --no-spline-reuse --repeat 2 refillet-new-edge
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedCurvedBooleanOracle.cs -c Release -- --profile --no-bound-cache --repeat 2 refillet-new-edge
```

日志在 `temp_docs/modeling-performance/` 和
`temp_docs/mixed-curved-boolean-oracle/profile-*/`；fresh XT 也保存在相应目录。
依赖 SDK 不进入仓库。后续边链/角点 → Boolean 组合输入使用同样方法新增记录，
不以这些既有模型代替新支持域的完整操作验收。
