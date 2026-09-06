# 内核内存机制：实现状态

本文描述当前实现。2026-09-06 的两轮审查、补缺口及验证证据见 `docs/memory_review.md`。

## 所有权与存储

- `SessionMemory` 是 session 资源的底层所有者：64 字节对齐、64 KiB 页、全局最多 2 MiB 空闲页缓存、分配失败注入和统计。进程级 allocator 根及同步对象属于启动基础设施。
- `PagedEntityPool<T>` 限定 `unmanaged`；以二次幂槽位数寻址，记录页不移动。每个 partition 拥有自己的空闲链和当前页；持有调度写权限时，普通槽位分配/回收不争抢全局锁。新页和目录增长有短临界区；旧目录在安全回收点之前保留，避免读者访问已释放内存。
- 空闲链复用已撤销记录的 Tag 字段，不覆盖 Alive、Generation；每次分配完整初始化记录。每页维护存活计数。`TrimAllocator` 在独占且无 mark 时回收空池及存活池中的空页，先剔除对应空闲槽链接，再归还页面；复用页位置时提升 generation。命令内递归 Trim 不回收仍可能被事务引用的记录。
- `StableTable<T>` 按需增加固定大小的稳定地址段，用于分区、线程、scratch 和分区分配器元数据。移除了 256 个分区和 128 个线程的固定上限；锁集合也按需增长，大请求使用非托管临时工作区。索引受 32 位范围和可用内存约束，申请失败返回错误。线程上下文和元数据段随 session 释放。
- `RecordHeader` 保持 16 字节，同时容纳 32 位分区索引；常用 fin/edge 记录保持 64 字节。默认分区的分配器状态内嵌在池中，避免小对象路径多一次目录寻址。
- `BlockAllocator` 使用 16 字节对齐、两级大小索引、物理相邻块双向合并。大于 32 KiB 的载荷独立申请；空页归还给底层页缓存。请求长度、句柄等元数据位于头部，不放入用户可写载荷。释放同时注销句柄。
- `TagMap` 是按存活/回滚保留条目增长的非托管哈希表，不按历史 tag 总数分配数组。tag 在进程中单调增长，session 重启不重置；达到正整数上限返回错误。读取和更新在短临界区内完成，避免表扩容/删除与读者竞争。
- XT/schema 对象仍属于允许托管分配的适配层。`XtAssociationTable` 的文档引用在 mark 删除期间保留，提交删除或 session 停止后清除。

## 命令与回滚

- C 导出与托管 API 都经过同一调度入口。嵌套内核调用复用已经取得的权限。
- 调度在声明的目标分区上进行读写冲突判断；只读查询使用目标实体所属分区，而非调用线程默认分区。session 启停和 mark 操作通过 Exclusive 屏障。
- 事务提交/失败撤销在调度权限释放之前完成。等待分区锁会临时让出权限；若期间 session 被停止或替换，等待调用按 generation 检查返回，不再访问旧日志、线程上下文或临时工作区。
- 每线程独占追加撤销日志，扩容不移动其他线程正在写入的记录；无 mark 的成功命令立即回退自己的日志计数。mark 期间记录全局顺序，goto 在独占点逆序合并回放各线程日志。
- 单独的无 mark PointCreate 和单点删除命令使用原子快速路径：创建发布失败显式归还槽位，删除在修改前完成 tag、所有权和分区锁检查。被 mark 或复合命令包含时仍使用完整撤销日志。
- 槽位分配时就记录撤销信息，包含拓扑、face use、类型化几何数据；不再等 tag 发布才记录。因此未完成构造的对象也能被回收。日志引用稳定记录地址，并核对 generation，避免误释放已经复用的槽位。
- 存量实体记录修改前保存完整前镜像，失败和 goto 回放后释放快照。复合命令即使没有全局 mark，也会保留删除记录至提交；失败会恢复既有实体。Body 回滚恢复原来的分区环位置。
- 分区创建、选择和删除具有命令失败撤销。显式 `PK_PARTITION_delete` 遵循 Parasolid 的永久删除语义：只有命令成功后才清除该分区在所有 mark 中的历史，物理释放对应延迟对象，并更新 mark 保存的当前分区。后续 goto 不会复活显式删除的分区；mark 后新建的分区会被移除，仍存活的 mark 当前分区会恢复。

## 几何所有权

- 曲线和曲面可在同一 body 中共享；引用取得、分离、最后一个引用释放和计数回滚均有实际路径。点仅允许一个 vertex 父对象，与 Parasolid 的 `PK_ERROR_has_parent` 行为一致。
- `PK_EDGE_attach_curves`、`PK_FACE_attach_surfs`、`PK_VERTEX_attach_points` 及 face/edge/vertex 的 `PK_TOPOL_detach_geom` 先完成参数、分区与所有权验证，再保存涉及记录的前镜像。跨分区附着被拒绝；直接删除仍被拓扑引用的几何也被拒绝。
- 共享曲线的 edge 环、共享曲面的 face 环按 XT 的闭合环规则输出；单个所有者的环链接为 null。共享、分离和回滚的导出结果由真实 Parasolid receive/compare 验证。

## 返回缓冲区

- 小结果由非托管块池提供，每个结果在 `ReturnAllocator` 独立登记，可通过 `PK_MEMORY_free` 释放。
- 回调分配的内存保存配对释放函数，线程回调实际参与结果分配；成功和失败清理路径都在注册表锁外调用回调。
- 失败命令释放尚未交付的返回缓冲区。查询结果不会因后续查询或 mark goto 失效，但其 tag 可能不再有效。
- session stop 对返回表做线性遍历，释放未归还结果；不再对每个结果从表头重新扫描。
- 全局及线程内存回调的注册/查询均接通；全局回调可在 session 启动前注册。任一回调为空时，整对回调恢复默认值，与真实 Parasolid 的查询结果一致。已返回块保留分配时配对的释放函数。
- 回调中的线程状态查询报告外层保护状态；递归停止正在执行的 session 被拒绝，避免提前释放外层调用的数据。

## 线程协议

- chain type、应用线程 ID、link length 和 remaining 分别存储。并发链及独占链跨调用保留相应权限，达到 link 边界或 stop 时让出；查询返回真实 SDK 的 `chain_none`、`chain_concurrent`、`chain_exclusive` 值。
- 线程自省查询可以在另一线程持有独占链时执行。普通 Local 操作在没有分区锁时按独占方式执行；已隔离分区上的建模可以并行。
- 分区锁使用 SDK 的 `lock_all/lock_one`、`wait_yes/wait_no`，并检查 `not_at_pmark`。修改会使分区离开 checkpoint，goto 恢复 checkpoint 状态。
- `PK_FUNCTION_find` 和执行分类查询由已实现导出的生成目录支持；每次构建检查目录与实际 dispatch 声明一致。当前支持单 session、单个活动全局 mark、标准分区和 `local_level=none`，不实现完整的多 mark/pmark 图或其他尚未实现的建模 API。

## 验证与限制

- `tests/KernelTests/MemoryAcceptanceTests.cs` 严格断言被测操作托管分配为 0；测量区间不包含 xUnit 断言或初始化。
- `tests/KernelTests/MemoryReviewTests.cs`、`tests/KernelTests/GeometryOwnershipTests.cs` 和 `tests/KernelTests/ThreadProtocolTests.cs` 覆盖字段前镜像、失败撤销、永久分区删除、共享几何、动态表与锁集合、等待时停止 session、回调及部分空页回收。
- `scripts/PerfBaseline.cs` 使用统一 Release 配置、多批次预热及 7 组采样，分别报告托管入口与 C 导出入口，包含实际托管分配量。
- `scripts/AllocationBanCheck.cs` 在每次内核构建后检查实际 IL，识别 `newarr`、装箱、引用构造和已知分配调用；启动及 XT 边界按精确符号列出。导数布局的异常构造已消除，门禁为 CLEAN；该检查不等同于整个 BCL 调用图的分配证明。
- 真实 Parasolid 的本地库未实现 `PK_THREAD_ask_function_run`，因此分类查询以生成目录检查和实际调度测试验收；oracle 明确报告这一验证限制。线程 ID、链、回调默认值、分区锁及几何生命周期具有直接 oracle 对照。
