# 内存改造审查与修复记录

审查日期：2026-09-06。首次审查以 `8278845` 为原始基线，以下保留当时的问题与性能证据；本轮补缺口以 `5475aa5` 为基线，结果见本文末尾。修改保留在工作区供审阅。

## 结论

首次待审版本存在内存损坏、撤销失败和并发竞争，原有 140 项测试未能覆盖。首次修复后列出的五类缺口，本轮已补齐对应实现；其中分区删除和回调默认值按真实 Parasolid 的行为修正，而不是沿用最初的语义假设。

原“回退 71–78%”不能作为稳定性能结论：基准仅预热 4 次，随后运行几十次操作，受分层编译/运行配置影响；同时混用了直接托管调用与调度入口。旧 Body 删除也没有回收拓扑和几何，和新删除不等量。它仍提示需要排查，但不能用这个百分比说明并发或非托管内存必然慢几倍。

## 已修复的主要问题

1. **[P0] 实体池并发及元数据破坏。** Treiber 栈只 CAS 指针，没有将 generation 纳入 CAS，因此原“不会 ABA”的注释不成立；并且将 Next 指针写在 RecordHeader 上会覆盖 Alive。目录扩容还立即释放旧目录，读者可能继续使用它。现在采用分区所有的空闲链，稳定目录生命周期，保留 liveness/generation，普通分配用移位与掩码寻址。
2. **[P0] 变长分配器覆盖业务数据。** 大块把请求长度放在用户载荷的第一个字中，写入实际数据后再释放会破坏统计。大小分类可能返回过小块；释放不注销句柄，且相邻 bump 块不合并、空页不归还。现在元数据独立，候选块验证大小，双向合并并归还空页，句柄随释放注销。
3. **[P1] 失败撤销被跳过或丢失。** Cancelled 和 EntityCreated 同为 1；日志只在 tag 发布后记录，漏掉半成品；共享日志扩容/计数回退会与其他线程追加竞争。现在取消值为 0，每线程独立日志，槽位分配即登记，失败按 generation 回收，mark 删除前预留所需日志容量。
4. **[P1] 调度权限过早释放。** 原代码在调度返回后才提交/撤销事务，并继续使用可能已被 session stop 释放的指针；托管入口则完全绕过调度。现已统一入口，事务完成后再释放权限，生命周期授权状态独立于会被释放的 session。
5. **[P1] tag 复活与历史增长。** 新 session 把计数重置为 1，旧 tag 会命中新对象；表按历史 tag 分段保存，循环删除仍持续增长。现改为进程单调 ID 和按存活条目保存的非托管表。
6. **[P1] mark 恢复不完整。** 删除后的 Body 没有重新挂回分区，XT 关联也在 mark 中提前清除。现保留关联，恢复分区归属，并通过真实 Parasolid 的删除/恢复/XT compare 验证基本体。
7. **[P1] 返回内存及回调。** 原默认返回缓冲区每次直接申请底层内存，线程回调只保存但未使用，失败清理在锁内调用回调。现使用块池、配对回调和锁外清理；失败命令回收未交付结果。session stop 原二次方扫描改为线性遍历。
8. **[P1] PK 分区锁协议和死锁。** 待审代码自造 read=1/write=2 常量，但 SDK 实际为 lock_all=26660/lock_one=26661；等待选项也不是 1/2。现使用真实常量，预留返回内存后才发布锁，等待时让出 Exclusive 权限，使持锁线程能够解锁。
9. **[P1] 验收失真。** 名为 ZeroManagedAllocation 的测试允许 700 B/轮；求值输出只预留一个向量却请求位置和一阶导数两个向量，造成栈越界写；测量区间里的 xUnit 断言本身还产生分配。已修正缓冲区和测量边界，并恢复严格 0 B 断言。故障注入现在从冷 session 遍历至成功，核对所有记录池及 tag，而非只看变长载荷统计。
10. **[P2] Debug 常量表的运行时辅助分配。** block 中两处 `stackalloc` 常量表初始化在未优化代码下合计产生约 144 B/次；独立小程序复现单表约 72 B/次。Release 会消除，因此容易误判。现通过简单索引公式直接写入栈缓冲区，Debug 也执行严格零分配验证。

对应代码集中在 `src/ProjectGmKernel.Native/Runtime/`，针对性测试集中在 `tests/KernelTests/MemoryReviewTests.cs`。

## 首次审查的性能证据

统一使用 `.NET 10.0.10`、Release、`DOTNET_TieredCompilation=0`。三个版本运行同一份 `scripts/PerfBaseline.cs`。每项预热 24 个批次，再采样 7 组，每组 32 个批次；计时及分配测量不包括 session 启停。block 批次 8 次、point 批次 512 次，以兼容旧版固定 tag 表。报告中位数，不把几十次调用的单次结果当作稳态性能。

审查期间一组对照数据（ops/s，约值）：

| 入口 / 用例 | 改造前 | 待审实现 | 修复后 |
| --- | ---: | ---: | ---: |
| 托管 / block 创建 | 201,117 | 158,145 | 203,933 |
| 托管 / block 创建＋删除 | 206,465 | 126,498 | 171,680 |
| 托管 / point 创建＋删除 | 7,482,037 | 8,479,331 | 8,067,911 |
| 托管 / mark 创建＋block＋goto | 167,949 | 126,799 | 169,069 |
| C 导出 / block 创建 | 202,697 | 216,942 | 203,455 |
| C 导出 / block 创建＋删除 | 199,109 | 155,764 | 170,168 |
| C 导出 / point 创建＋删除 | 3,914,197 | 2,977,110 | 8,095,606 |
| C 导出 / mark 创建＋block＋goto | 167,362 | 148,868 | 168,245 |

全部被测区间为 **0 B/op 托管分配**。托管和 C 导出的 block 创建、point 周期和 mark 周期均达到或优于改造前。这不等于所有性能指标都通过：

- 完整 Body 删除现在确实回收子实体，不能与旧的“只删 Body 槽位”视为相同工作；原始创建＋删除数字仍如实列出。
- 托管 point 入口现在也执行调度。其单记录发布是原子操作：发布失败显式归还槽位，普通独立命令不需要撤销日志；mark 或复合命令中仍完整登记日志。这条快速路径有专门的分配失败测试，不通过关闭错误恢复换性能。
- 该基准覆盖有限基本操作和批次，并非大模型、全并发规模的最终验收，也不是 NativeAOT 吞吐测量。

快照和原始输出保存在临时目录 `temp_docs/memory-review/`：`original-results.txt`、`pre-fix-results.txt`、`final-verified-results.txt`；更新代码后可直接重新运行基准。不要把调试/短预热结果与本表混比。

## 首次审查的验证

- Debug 与 Release 均为 **156/156 测试通过**，包括严格 0 B 分配断言；不是保留 700 B 容忍阈值后的通过。
- 原 140 项测试之外新增针对性回归，覆盖失败撤销、冷分配失败、mark 删除失败、跨 session tag、物理块合并、池元数据、两个分区在同一个 Barrier 上同时持有写权限、并行日志扩容与失败隔离、等待锁及回调重入。
- `scripts/ParasolidEvaluationOracle.cs -- --memory-review`：我方和真实 Parasolid 执行同参数基本体创建、mark 删除/恢复及临时实体创建/删除；我方 text XT 导出后由真实 Parasolid receive 并执行 `PK_DEBUG_BODY_compare`，覆盖 5 种基本体及各自旋转坐标系。
- NativeAOT 发布与 `scripts/AbiSmoke.cs` 均通过，真实共享库在 `src/ProjectGmKernel.Native/bin/Release/net10.0/linux-x64/publish/`。B 样条 oracle 的 10 组数值案例及普通/有理曲线附着 Body 的 XT compare 通过。测试数量与最终运行结果以 `temp_docs/memory-review/` 的验收输出为准。
- 分配检查从源码正则改为 IL 检查，识别真实数组分配、装箱和引用构造。消除了 IntegrityChecker 的首次调用委托分配；不通过扩张整文件白名单掩盖剩余异常构造。

## 本轮五类缺口的关闭记录

| 原审查项 | 当前实现 | 对应验证 |
| --- | --- | --- |
| 分区生命周期和存量字段回滚 | 实体记录前镜像、快照释放、精确 body 环恢复；无全局 mark 的复合命令也能撤销删除；分区删除成功后才清理历史 | 字段/删除失败测试、分区生命周期 oracle |
| PK 线程协议 | 分离应用线程 ID、链类型、长度、remaining；保留链权限；补充函数目录、分类查询、全局回调和 checkpoint 前置检查 | 并发链/独占链调度测试、线程 oracle、构建时目录一致性检查 |
| 共享几何所有权 | 曲线/曲面的引用取得、分离、计数回滚与最后引用释放；点禁止多个父对象；跨分区附着拒绝 | 共享曲线和共享曲面的 XT receive/compare、B 样条重新附着、分配失败及跨分区测试 |
| 空页和元数据容量 | 每页存活计数与空页回收、页位置复用时提升 generation；稳定地址分段表和动态锁集合 | 部分空池回收、300 个分区、140 个新线程、32 个锁，以及等待时停止 session |
| IL 分配门禁 | 导数布局改为无异常分配的校验接口；每次构建检查实际 IL，未扩大核心目录豁免 | Debug/Release 构建均为 CLEAN，严格 0 B 运行断言 |

分区删除的语义需要特别区分：Parasolid 的显式 `PK_PARTITION_delete` 会删除该分区在各个 mark 中的历史，之后 goto 不会复活它；mark 保存的当前分区也会相应更新。依据是 v380《PK Functional Description》的 “Deleting partitions” 和 “Session marks” 小节，且已实际调用本地 Parasolid 验证。命令执行失败时仍会恢复删除；永久清理只发生在命令成功提交后。

共享所有者的 XT 链必须闭合，单个所有者使用 null 链接。本轮真实 receive 测试定位并修复了共享 edge 链的线性输出问题。点的附着规则不同：再次附着已有父对象的点返回 `PK_ERROR_has_parent`。任一内存回调为空时，整对回调恢复默认值；这两项均有直接 oracle 对照。

## 本轮验收证据

- `tests/KernelTests`：Debug 和 Release 均为 **179/179**，包括非托管元数据增长、失败撤销、回调保护状态、大小锁集合等待期间停止 session，以及命令内 Trim 不破坏待回滚记录。
- 构建自动检查 **71 个已实现导出**的函数目录及 **655 个核心方法**的 IL；结果为 CLEAN。内部几何工具不再构造参数异常来表示输入失败。
- `scripts/ParasolidEvaluationOracle.cs -- --memory-review`：分区语义、共享曲线、共享曲面、各自删除/恢复，以及 5 类基本体的默认/旋转坐标系，均经我方 text XT → 真实 Parasolid receive → `PK_DEBUG_BODY_compare` 验证。共享测试还保存真实 Parasolid 自身的 transmit/receive 结果供定位。
- `scripts/ParasolidThreadOracle.cs`：线程 ID、可空的未用输出、两类链的 0/1/3 链长、remaining、保护标记、回调默认值及点父对象限制均有直接对照。
- 本地 Parasolid 库的 `PK_THREAD_ask_function_run` 返回 `not_implemented`，部分较新函数名不在其查询目录中。因此此维度不计作 oracle 通过，采用生成目录检查与真实调度行为测试验收，脚本明确输出该限制。
- 同一 IL 检查器对 `5475aa5` 基线仍检出 3 处异常对象构造并返回失败；当前代码返回 CLEAN，确认门禁没有通过放宽豁免来清零。
- 最终版本的 NativeAOT `linux-x64` 发布和 `scripts/AbiSmoke.cs` 通过；B 样条的 10 组数值案例及普通/有理曲线重新附着后的 XT receive/compare 通过。对应输出为 `temp_docs/memory-review/gap-aot-publish.txt`、`temp_docs/memory-review/gap-abi-smoke.txt` 和 `temp_docs/memory-review/bcurve-gap-oracle.txt`。

## 本轮等量性能对照

基线为 `5475aa5`，该版本已完整删除 body 子对象，所以本表的创建/删除是等量工作。前后运行同一份 `scripts/PerfBaseline.cs`，使用 .NET 10.0.10、Release、关闭分层编译、固定 CPU 2、24 批预热及 7 组中位数；session 启停不计时。单位为 ns/op，越低越好。

| 入口 / 用例 | 修改前 | 修改后 |
| --- | ---: | ---: |
| 托管 / block 创建 | 5,056.3 | 4,700.9 |
| 托管 / block 创建＋删除 | 5,976.5 | 5,744.8 |
| 托管 / point 创建＋删除 | 125.2 | 124.1 |
| 托管 / mark＋block＋goto | 6,201.1 | 5,581.2 |
| C 导出 / block 创建 | 5,140.7 | 4,729.5 |
| C 导出 / block 创建＋删除 | 6,055.0 | 5,625.1 |
| C 导出 / point 创建＋删除 | 124.7 | 130.0 |
| C 导出 / mark＋block＋goto | 6,210.8 | 5,610.2 |

全部测量区间为 **0 B/op**。最大耗时增长约 4.3%，未超过 5% 目标。优化保留所有权、分区锁和失败撤销检查，主要减少重复的 tag 查询、上下文查询和默认分区元数据寻址，并保持常用记录的紧凑布局。原始数据为 `temp_docs/memory-review/gap-baseline-pinned.txt` 与 `temp_docs/memory-review/gap-final-scalar.txt`；中间含调度抖动的测量也保留在同目录，不能把单次最大值当作稳定吞吐结论。

原始输出及同机基线快照保存在 `temp_docs/memory-review/`。实现范围是当前已实现的 API、单 session、单个活动全局 mark、标准分区及 `local_level=none`；不是完整 Parasolid 建模 API 或完整多 pmark 图的实现。当前架构详见 `docs/memory_architecture.md`。
