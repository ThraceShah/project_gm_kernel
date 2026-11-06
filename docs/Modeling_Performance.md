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

`FixLocalBlendsCore` 的外层 profile 记录整条圆角操作，构造后的只读 domain、
contact 证明分别建立局部 scope；这些 scope 的工作计数汇总回外层，缓存随各自
证明结束释放。子阶段 elapsed 不重复相加到完整 API 时间，父阶段时钟已包含它。
不能把最后一个证明阶段的 `Last` 误标为整条圆角耗时。`BlendChainOracle.cs`
可直接输出完整 FixBlends/Boolean 时间和这两类操作的 profile，不再需要临时插桩。
Bound 计数指经过插桩的 procedural patch 包围请求/执行，不代替构造、求值和
global-reach 内部全部数值工作量；完整 API elapsed 仍包含这些工作。

接触分离还先用成功的子 tube 包围并合，认证整个 contact chart 的位置及一阶
导数父包围，再用只读精确参数盒缓存复用。中心或子域查询不会踢掉这个父证据，
iso/offset 等改变几何的 wrapper 会使它失效。叶子、失败尝试及后续配对仍消费
同一个 32,768-cell 预算。该步骤减少对远离候选面的重复隐式细分，不能用 CHART
采样或未认证误差代替完整 hull。

配对还可尝试由导数和包围中心估计的分离方向。只有两个完整参数盒的保守投影
区间严格分开才接受；方向的估计本身不作排空。投影 clearance 用方向 L1 范数
作保守换算，保留请求的笛卡尔间隙。等参 wrapper 的投影也使用完整导数区间的
mean-value 界。近接、交叠、深度或预算未解仍失败。

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

## 新边链结果的保存、重载和继续切削

另以 M4 的 `chain-3-base-pk-source` 进行整链实验：两套内核从同一个真实 PK
Roof 有限 B-rep 独立分出三段光顺边链，半径 .1 分别倒圆角；自研结果保存并重载，
随后两套内核分别执行相同方块切削。输入是 degree 4×2 的非平面 Roof，分割位置
为 x=.21/.68。两次修改之后都使用原 chain oracle 的 fresh XT、完整 BODY_check、
严格结构 compare、region/shell 语义及物性检查；最后检查自研几何所有权全部释放。
本组没有放宽几何类型、sense 或其它结构条件。

基线为 `3075169` 加当时未提交的 M4 边链实现快照；四个新增/变化的实现文件
SHA-256 保留在原始日志开头，可与最终 M4 提交核对。性能插桩只在 oracle 外侧记录
完整 `BodyFixBlends`/`BodyBoolean` 的 elapsed 和进程 CPU 时间，没有改变构造参数。
每模式独立进程、相同会话内执行两轮全新建模，cold/warm 定义同前。

正式 command-cache A/B 在三个建模 agent 都暂停 build/test/oracle 的短窗口内
串行执行。两模式均开启栈内 jet 复用，只有 Boolean 命令缓存开关不同；四条完整
链均严格通过。桌面其它进程未停止，因此这些数值仍是本机固定输入的观测。

| 完整 API | 缓存关闭 cold/warm | 缓存开启 cold/warm |
| --- | ---: | ---: |
| 三段边链 FixBlends elapsed | 1.571 / 1.050s | 1.627 / 1.090s |
| 重载后 Boolean elapsed | 11.405 / 11.034s | 8.761 / 8.575s |
| 重载后 Boolean 进程 CPU | 11.759 / 11.172s | 9.151 / 8.714s |
| Boolean 实际 Joint Bound | 9129 / 9129 | 4642 / 4677 |

Boolean 完整 API elapsed 降低约 23%/22%，CPU 降低约 22%/22%；FixBlends
不使用命令缓存，其时间相近。两模式的 54 pair、144 SSI cells、4787 root cells、
67 根候选、189 winding、0 预算耗尽和 14,378 次 Bound 请求完全相同。
缓存命中 4487/4452 次；Bound elapsed 从 5.801/5.685s 降至 3.245/3.210s。
各次前后模型的完整 BODY_check 都为 0，切削后物性最大归一化差为 1.28e-9。

此前未协调负载的三组实验也全部严格通过，并保留完整日志。其中首次 cache-on
受到同期编译和其它 oracle 运行影响，Boolean elapsed 为 22.553/11.621s，未用作
正式加速依据。补充 cache-on CPU 复测为 elapsed 8.777/8.457s、CPU 9.163/8.591s。
另在 command cache 关闭时，栈内 jet 复用关闭/开启的 Boolean 为
15.690/14.966s 和 11.348/11.479s，FixBlends 为 1.802/1.314s 和 1.611/1.046s；
这些未协调负载数据仅作补充，不代替上表的串行实验。

原始证据为 `docs/reviews/advanced-modeling/modeling-performance-chain-cache.txt`，
包含六组全部 strict 日志及阶段计数；只删除了构建器输出中包含本机绝对路径的
包告警，验证输出完整保留。插桩补丁为
`docs/reviews/advanced-modeling/chain-performance-instrumentation.patch`；通过
`patch -o scripts/BlendChainPerformanceOracle.cs scripts/BlendChainOracle.cs < docs/reviews/advanced-modeling/chain-performance-instrumentation.patch`
生成独立临时 harness，再执行下面的命令；原 oracle 保留。
该补丁固定两轮，接受 `--no-bound-cache` 和 `--no-spline-reuse`，其余为原案例名。

## 复现

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/ModelingPerformanceOracle.cs -c Release -- blend-subtract
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedCurvedBooleanOracle.cs -c Release -- --profile --no-bound-cache --no-spline-reuse --repeat 2 refillet-new-edge
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedCurvedBooleanOracle.cs -c Release -- --profile --no-bound-cache --repeat 2 refillet-new-edge
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/BlendChainPerformanceOracle.cs -c Release -- chain-3-base-pk-source --no-bound-cache
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/BlendChainPerformanceOracle.cs -c Release -- chain-3-base-pk-source
```

日志在 `temp_docs/modeling-performance/` 和
`temp_docs/mixed-curved-boolean-oracle/profile-*/`；fresh XT 也保存在相应目录。
依赖 SDK 不进入仓库。

## 最终集成源码的完整链复测

源码 `88cf31b`，同一 `chain-3-base-pk-source`，固定两轮新建输入，先串行关闭缓存，
再串行开启；其余建模 agent、测试和 oracle 均已停止。两模式都保留 spline jet
复用、完整接触/间隙证明和相同 PK 硬门。所有四条链均通过首次圆角、fresh XT
对照、重载后切削、再次 fresh XT 对照及所有权清理。

| 完整 API / 工作量 | 缓存关闭 cold/warm | 缓存开启 cold/warm |
| --- | ---: | ---: |
| 三段链 FixBlends | 2.109 / 1.633s | 2.112 / 1.594s |
| 重载后 Boolean | 12.678 / 12.343s | 10.057 / 9.720s |
| Boolean procedural Bound 执行 | 9858 / 9858 | 5055 / 5044 |
| Boolean bound 请求 | 16499 / 16499 | 16499 / 16499 |

本机完整 Boolean 降时约 20.7%/21.2%；本组 FixBlends 没有明显冷次收益。
两模式的 54 pair、150 SSI cells、5601 root cells、77 根候选、179 winding、
0 budget failures 完全相同。相较历史快照，M3 求交和接触证明工作已经变化，
不能直接用历史调用计数作为当前版本的分母。

原始记录在 `temp_docs/m345-performance-final-uncached.log` 与
`temp_docs/m345-performance-final-cached.log`，筛选记录及源文件指纹收录于
`docs/reviews/advanced-modeling/m3-m4-m5-validation.txt`。复现无需临时补丁：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/BlendChainOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false -- chain-3-base-pk-source --profile --repeat 2 --no-bound-cache
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/BlendChainOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false -- chain-3-base-pk-source --profile --repeat 2
```

新的角点 → 重载 → Sphere/Blend 切削 → 第二刀 → 新生边圆角，在
`scripts/TrivalentBlendCornerOracle.cs -- base --consumer --refillet` 中由两种输入
来源分别严格通过；这组正确性运行并行过其它 oracle，未将其 wall time 充作
独占性能测量。新增完整 contact 证明也使嵌套再次圆角仍有数十秒输入，这项
成本与重型 Boolean 的分钟级输入都保留为后续优化对象。
