# 原生建模算法减量：命令内重复求界

本页记录 `perf/pk-native-algorithm-work-reduction` 的开发检查点。整合起点为 `270e2b6`：已接受的固定功能源码 `f9c59201c0b9c5d103dee45124e3f43ca59a0921` 与首包性能源码合并。当前 Native 源码子树为 `769803239b613bae7c8e7560dd1e22e241464877`。本轮尚未独立接受、集成主分支或上线，未取得新的正式 GM/PK 倍率；首包历史验收与发布记录不变。

## 已确认的瓶颈

整合源码的一次完整 shortedge 操作中，有限 ICurve/plane 查询的 conic BLEND 支持占 202,452/220,895 个根单元。其 100,148 次 tube 求界失败导致继续细分，成功求界后的 102,284 个单元被区间排空，非单调次数为零。原支撑参数坐标查询仅占 1,343 个单元。

随后只在已有不可变输入命令缓存入口记录真实 key：命令作用域、tag、generation、sense、root、类别及参数盒四个 double 的原始位。209,158 条记录只有 22,157 个不同 key，187,001 条为重复请求；所有记录属于同一个只读作用域，500,000 条容量未溢出。此为实际 key 证据，不以空查询比例推算重复率。

原始追踪位于 `temp_docs/short-edge-blend-oracle/integrated-bound-key-trace/bound-key-trace.bin`，SHA256 为 `e373863e5b69b4ef9a223a229a9baecfc70f1da4518b00ad896d914cadc9b36b`。追踪源码快照、diff、读取程序和原始分析保存在 `temp_docs/pk-native-algorithm-work/`；追踪用的进程全局指针已从生产源码撤下。

## 实现与边界

沿用原有命令私有缓存，更新为全位混合哈希、最多 32 项的有界线性探查及 32,768 项容量。达到探查上限可淘汰一项；命中仍逐字段验证完整 key，碰撞只会导致重算。成功项保留原向外区间，不重新构建几何。

新增负缓存只适用于隐式 spine 的纯解析编译视图：没有 BSpline 支撑或 BCurve spine，计算仅使用固定栈工作区和原迭代上限。仅保留 `NotConverged`，命中仍返回这个失败状态、不写输出、不作为空交证明，原细分、根数、预算、有限分支和验证继续执行。`WorkspaceTooSmall`、取消、无效输入及其他状态均不保留。

收敛精度还读取编译视图中所有支撑的数值尺度，包括根 DAG 外的支撑。因此负项额外精确匹配原 `ResidualAccuracy` 的基础尺度；它的提取不改变原精度公式。不同工作区能力或数值尺度不能继承一次失败。成功包围不依赖求解器此次是否容易收敛，仍按原几何 key 使用。

可选存储由原 4,096×200 字节（0.78125 MiB）变为 32,768×208 字节（6.5 MiB），只在第一个可缓存请求时分配，命令结束即释放。分配失败后本命令始终重算。未增加根单元、Newton、区间证书、容差或求解器 scratch 预算；快速操作没有相关请求时不分配此存储。没有 C ABI 改动，未运行 NativeAOT 或 Windows 验证。

## 开发期验证

69 项针对性测试通过，原始退出码 0：缓存完整 key、短输出、可选分配失败、命令与可变作用域隔离、相关 dyadic key 碰撞、负项不发布值、改变编译视图数值尺度后的重算、含 spline 视图的重算、Jacobian record、CompactBranchCertificate 及 RollingNetwork 生命周期测试。见 `temp_docs/pk-native-algorithm-work/command-bound-reuse-lazy.log` 和 `test-results/command-bound-reuse-lazy.trx`。本轮没有重新执行全套测试。

完整 shortedge 使用相同 angle=1、radius=0.5、tolerance=1e-7，仍为独立构造模式；fresh GM XT 被真实 Mac PK38 接收，BODY_compare、BODY_check、有限 ICurve 及质量属性检查通过，操作原始退出码 0。开发诊断工作量如下：

| 实际工作 | 整合起点 | 当前候选 |
|---|---:|---:|
| 求界请求 | 275,525 | 275,525 |
| 实际求界 | 223,355 | 67,137 |
| 成功包围命中 | 30,703 | 96,448 |
| 原失败状态命中 | 0 | 90,473 |
| Jacobian | 1,593,664 | 526,133 |
| 根单元 | 220,895 | 220,895 |
| 根 | 3,518 | 3,518 |
| SSI 单元 | 4,956 | 4,956 |
| winding | 196 | 196 |
| 预算失败 | 0 | 0 |

实际求界减少 156,218 次，恰等于新增成功命中 65,745 次与失败状态命中 90,473 次之和。各用途的求界失败、排空和非单调计数也相同。开发进程 GM API 观测值从 17,074.566 ms 到 6,659.693 ms；两次不是正式固定窗口 ABBA，不据此签发加速倍率，也不与首包旧窗口的 PK 倍率混用。

原始操作记录分别为 `temp_docs/pk-native-algorithm-work/root-shape.log`（SHA256 `641e6dbf52546ad7759555c3e4e92ab480e7de008cb326d25062212da95581b9`）和 `command-bound-reuse-operation.log`（SHA256 `a1d7c89c4273cb69f0e6ce0b650daaf15aac7f7c19602f59a4cd34f2c40f345c`）；同目录保留各自原始退出码、capture 和固定源码 diff。

## 调研与撤下的候选

实际查询了 MCP 的 `PK_SURF_intersect_curve` 原型与 `COI_curve_surface` 调用链，其下存在解析分类、包围和通用求交路径。`scripts/NativeConicPlaneProbe.cs` 通过已有 PskernelSharp/ParasolidScriptHost 在 Mac PK38 中接收真实 GM XT，保留有限边原参数，执行 27 组、216 次原生曲面/曲线调用，原始错误码均为 0。该诊断的 batch wall 包含日志和返回缓冲区释放，不是完整操作基准，也不能证明 PK 动态选择了某个内部算法。

曾试验从原 conic 全参数方程、滚动半径、支撑平面和原 CHART 方程构造必要约束超集。21 项测试和完整操作通过，但只排空 254 次，根单元由 220,895 降至 219,911，减量不足 0.5%。该候选已从生产改动撤下，源码与原始证据保留在 `temp_docs/pk-native-algorithm-work/rejected-global-tube/` 及相邻 `conic-exclusion-*` 文件，不将它描述为主要优化。

真实 GM XT 的只读定义检查还确认，最长有限 ICurve 为椭圆 BLEND 与倾斜圆柱的交线，另两段才与平面相交；定义记录见 `temp_docs/pk-native-algorithm-work/native-root-definitions-ready.log`。不能把 conic 分类中的全部查询都当作 BLEND/plane 支撑组合。

另试验用原生二阶 Taylor 系数选择空间 tube 的初始搜索盒，仍保留原六次扩张、Krawczyk、nearest 与有限根验收。45 项局部测试通过，但完整 shortedge 在 BoundaryIncidence 返回错误 157，根单元 274,568、预算失败 1；该失败操作没有性能成功资格。此候选已完全撤下，生产 Native 恢复本页固定子树，原始源码、diff、日志和退出码保留在 `temp_docs/pk-native-algorithm-work/rejected-quadratic-seed/` 及相邻 `quadratic-tube-seed-*` 文件。没有增加扩张/根预算来保留它，也没有增加双轨回退。

也试验把完整范围求界统一接入已有实体缓存。99 项测试及完整 shortedge 通过，但 32K 表竞争使 Jacobian 从 526,133 增至 663,764；按新增请求上界试验 64K（13 MiB）后为 525,282，仅比检查点少 851 次（约 0.16%）。新增 16,717 次范围请求几乎全为不同参数盒，不值得翻倍缓存内存。这组范围接口/测试改动及扩容已撤下，保持检查点的 32K/6.5 MiB 配置；源 diff、各次日志、捕获、退出码保留为 `temp_docs/pk-native-algorithm-work/extent-command-*` 和 `rejected-extent-reuse.diff`。范围接入扩大了 BoundRequests/BoundEvaluations 的统计覆盖，不能直接混比这两项；内部 Jacobian 计数原本已覆盖该路径。

在恢复检查点 Native 源码后，又串行执行 curved `block-15902`、该脚本同一筛选覆盖的 `reloaded-block-15902`，以及 `plane-nurbs rounding-only`。三个完整独立操作全部通过真实 PK 接收、结构/有限曲线/质量属性检查，各脚本原始退出码均为 0；开发记录为 `temp_docs/pk-native-algorithm-work/curved-command-reuse.log` 与 `nurbs-command-reuse.log`。NURBS 倒圆单独验收，未执行其下游 Boolean。这些冷进程诊断不签发新正式倍率。为保留原有默认输出字节，两个 oracle 增加了 `--output-name` 子目录选项，本次结果另存 `command-bound-reuse`。

共同层 `7b0e5cb8e67dbe31c3cccabeae3f4d20ccb0a540` 当前为未最终接受的开发参考，本检查点未整合它，也未用其标签改写旧 Mac 证据。后续读取固定实际 diff 后按需要复用已有有限根 helper。

剩余开销仍包括大量不同参数盒的通用 tube 证明及完整根隔离。后续继续共享计算热点优化，原生实体模型、参数化、有限分支和证明要求不变；功能语义由 qingyu2 对应负责人交付固定源码后再整合。最终固定源码的质量验证与正式 Mac 测量分时执行，测量中串行同 runner/输入的 GM 与真实 PK，不等待旧看板发布。
