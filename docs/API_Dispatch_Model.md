# API Dispatch Model

## 1. Purpose

本文定义内核的 API 调度层。该层位于对外 `PK_*` 扁平 C API 与内部 `struct + arena + index` 内核实现之间，用来强制落实 Parasolid 风格的执行纪律：

- 外部多线程并发进入 API，不等于内核并发修改。
- 默认执行语义是 session 级串行。
- 并发能力不是“线程安全”一句话，而是按命令类别、partition 隔离状态和算法实现能力精确放行。

本文只定义调度、隔离、分类和状态机，不定义具体几何算法。

## 2. Non-Negotiable Rules

- 所有 `PK_*` 入口必须先进入调度层，禁止旁路直接写 session 状态。
- 默认情况下，同一 session 的命令按入队顺序串行执行。
- 外部 API 并发和内部算法并行必须严格分层。
- 并发判定必须由命令元数据驱动，不能依赖调用点约定。
- `Local` 命令只有在相关 partition 已被正确锁定或隔离时才允许并发。
- 任何放行策略都不能破坏 Tag 生命周期、rollback、guard、cloning 和返回内存语义。

## 3. Scope

### 3.1 This Layer Owns

- API 命令封装
- session 归属判定
- 参数预校验前置
- 命令排队
- 命令并发级别判定
- partition 锁与隔离判定
- 命令执行上下文建立
- 统一错误出口
- 返回内存生命周期挂接

### 3.2 This Layer Does Not Own

- 具体几何/拓扑算法
- 具体 Arena 分配实现细节
- 具体布尔/求交内部并行策略
- 对外 ABI 定义生成

## 4. Design Goals

### 4.1 Semantic Fidelity

调度行为必须尽可能贴近 Parasolid 风格：默认串行、需要时受控并发、partition 级隔离显式化。

### 4.2 Zero-Allocation Main Path

调度层本身不能成为 GC 垃圾制造机。命令记录、队列节点、执行上下文、结果包装必须进入预分配结构或 Arena。

### 4.3 AOT Compatibility

调度路径必须完全兼容 `.NET 10` `NativeAOT`：

- 不依赖反射派发
- 不依赖动态代码生成
- 不依赖逃逸委托链
- 不依赖异常实现正常控制流

### 4.4 Deterministic Behavior

同一输入、同一 session 状态、同一命令序列应得到可重现的执行顺序和错误语义。

## 5. Execution Hierarchy

推荐总链路：

`ApiEntry -> CommandDescriptor -> SessionCommandQueue -> Dispatcher -> KernelOp -> ReturnBridge`

### 5.1 ApiEntry

对外导出函数，例如 `[UnmanagedCallersOnly]` 的 `PK_*` 入口。

职责：

- 读取 ABI 参数
- 绑定 session
- 构造固定布局的 `CommandDescriptor`
- 将命令提交给 session 调度层
- 将内核返回码转换为标准 `PK_ERROR_*`

### 5.2 CommandDescriptor

命令描述符是调度层的最小执行单位。它不是对象图，而是固定布局记录。

建议字段：

- `api_id`
- `session_id`
- `concurrency_kind`
- `access_kind`
- `partition_count`
- `partition_span_ref`
- `entity_count`
- `entity_span_ref`
- `options_ref`
- `returns_ref`
- `flags`
- `caller_thread_id`
- `sequence_no`

说明：

- `concurrency_kind` 描述并发权限。
- `access_kind` 描述读、局部写、全局写、会话控制等访问属性。
- `partition_span_ref` 和 `entity_span_ref` 指向预分配参数区，不做托管堆复制。

### 5.3 SessionCommandQueue

每个 session 一条主命令队列。默认所有命令先进入这条队列。

职责：

- 维护命令的逻辑顺序
- 分配单调递增 `sequence_no`
- 为 dispatcher 提供可预测的取命令视图
- 与 session 生命周期绑定

实现要求：

- 采用固定容量环形队列或分段 Arena 队列
- 满队列时返回明确错误，不允许隐式扩容到托管堆
- 队列节点可重复使用，但必须清理 generation/owner 元数据

### 5.4 Dispatcher

dispatcher 决定命令是否：

- 立即串行执行
- 在满足条件时与其他命令并发执行
- 因锁冲突或前置条件不足而等待
- 被拒绝并返回错误

它是整个模型的裁决中心。

### 5.5 KernelOp

真正的内核算子入口。只接收经过调度层授权的执行上下文。

约束：

- 不允许 KernelOp 重新做 API 级并发放行决策
- 不允许 KernelOp 跨越 dispatcher 直接访问其他 session 状态
- 可在单命令内部进行受控算法并行，但不能修改 session 调度语义

### 5.6 ReturnBridge

把 KernelOp 产生的结果放入 Parasolid 风格返回区：

- 调用方提供缓冲区
- 或 session 级 `Return Arena`

ReturnBridge 负责：

- 返回结构布局对齐
- 失败路径回收
- `PK_*_r_f` 风格释放约定挂接

## 6. Command Classification

### 6.1 Primary Concurrency Kinds

所有命令必须预先标注以下三类之一：

- `Exclusive`
- `Concurrent`
- `Local`

### 6.2 Exclusive

语义：

- 不允许与任何其他内核命令并发执行
- 通常涉及 session 级状态、全局拓扑写入、rollback/guard 破坏性变更、partition 合并、memory regime 切换

典型候选：

- `PK_SESSION_*` 启停与全局行为切换
- `PK_PARTITION_merge`
- mark / rollback / guard 影响广泛的命令
- 大范围实体删除、复制、迁移
- 任何尚未证明可安全并发的写命令

放行规则：

- 当前 session 无运行中命令
- 当前命令前方无未完成命令

### 6.3 Concurrent

语义：

- 理论上可与其他 `Concurrent` 命令并发执行
- 典型是只读查询、独立返回计算、纯几何评估

典型候选：

- `ask` / `is` / `eval` 类只读命令
- 不修改 session 拓扑和几何状态的范围、分类、测量查询

放行前提：

- 命令被证明为只读
- 不依赖可变的 session scratch 全局区
- 返回区互不冲突
- 不与运行中的 `Exclusive` 冲突

### 6.4 Local

语义：

- 命令修改局部模型状态
- 只有在相关 partition 已被正确隔离时才允许并发

典型候选：

- local boolean
- local topology repair
- 受限于单 partition 的局部修改型操作

放行前提：

- 命令声明了完整 `partition_span`
- 每个目标 partition 已取得符合规则的锁
- 不与其他命令在相同 partition 或共享跨 partition 资源上冲突
- 不与 session 级 `Exclusive` 命令冲突

## 7. Secondary Access Kinds

除了并发级别，还要标注访问属性，避免“两个命令都叫 Concurrent 但一个偷偷写状态”。

建议值：

- `ReadOnly`
- `LocalWrite`
- `GlobalWrite`
- `SessionControl`
- `MemoryControl`
- `DebugInspect`

说明：

- `ConcurrencyKind` 决定并发资格。
- `AccessKind` 决定冲突域和锁粒度。
- 两者必须同时参与调度判定。

## 8. Session-Level Default Serial Semantics

### 8.1 Default Rule

对同一 session：

- 命令先统一进入主队列
- 如果没有显式满足并发放行条件，则严格按 `sequence_no` 串行执行

这条规则是基线，不是 fallback。

### 8.2 Why This Is Mandatory

原因不是“实现简单”，而是为了稳定以下语义：

- Tag 分配顺序
- rollback / mark 序列
- guard 建立与失效
- session 参数切换
- return memory 生命周期
- 调试、journal、snapshot 的重演能力

### 8.3 Example

即使两个外部线程同时调用：

- `PK_BODY_create_solid_block`
- `PK_ENTITY_delete`

默认也必须表现为：

- 命令 A 入队
- 命令 B 入队
- dispatcher 选择一个确定顺序执行
- 第二个命令在第一个命令完成后看到确定的 session 状态

## 9. Partition Isolation Model

### 9.1 Why Partition Matters

partition 是允许有限并发的最小隔离边界。没有 partition 隔离，`Local` 并发没有可信基础。

### 9.2 Partition State

每个 partition 至少需要以下状态：

- `partition_id`
- `lock_state`
- `lock_owner`
- `guard_state`
- `cloning_state`
- `rollback_generation`
- `active_command_count`
- `flags`

### 9.3 Lock States

建议最小状态：

- `Unlocked`
- `SharedRead`
- `LocalWrite`
- `ExclusiveWrite`
- `GuardTransition`
- `RollbackTransition`

说明：

- `SharedRead` 允许只读查询共享进入
- `LocalWrite` 允许单 partition 局部写
- `ExclusiveWrite` 用于需要排空该 partition 上所有活动命令的情况
- `GuardTransition` 和 `RollbackTransition` 期间禁止 local 并发穿越

### 9.4 Guard and Cloning Interaction

`Local` 并发必须尊重：

- guard 建立/清除
- goto guard
- partition cloning
- partition merge
- body change partition

原则：

- 任何会改变 partition 时间线的命令，一律提升为 `Exclusive`，至少在第一版实现中如此。
- guard / cloning 相关状态改变必须先排空受影响 partition 的活动 local 命令。

## 10. Dispatch Decision Matrix

### 10.1 Exclusive vs Anything

- `Exclusive` 与任何运行中命令冲突
- 必须等待 session 空闲

### 10.2 Concurrent vs Concurrent

可并发，当且仅当：

- 两者都是 `ReadOnly`
- 不共享可变返回区
- 不依赖非线程安全全局 scratch
- session 中不存在运行中 `Exclusive`

### 10.3 Local vs Local

可并发，当且仅当：

- 两者都完整声明目标 partition
- 目标 partition 集合不冲突
- 不共享跨 partition 写资源
- 当前不存在 session 级 `Exclusive`
- 当前不存在影响这些 partition 的 guard / rollback / cloning 转换

### 10.4 Concurrent vs Local

可并发，当且仅当：

- `Concurrent` 命令是真正只读
- 它读取的对象不跨越正在被 local 改写的 partition
- 或者系统有明确的一致性快照机制

第一版建议：

- 保守处理
- 只允许读取与 local 改写 partition 无交集的数据

## 11. Scheduler State Machine

### 11.1 Command Lifecycle

- `Created`
- `Queued`
- `Waiting`
- `Dispatchable`
- `Running`
- `Completing`
- `Completed`
- `Failed`
- `Cancelled`

### 11.2 State Transitions

- `Created -> Queued`
- `Queued -> Waiting`
- `Waiting -> Dispatchable`
- `Dispatchable -> Running`
- `Running -> Completing`
- `Completing -> Completed`
- `Running -> Failed`
- `Queued|Waiting -> Cancelled`

### 11.3 Transition Rules

- 只有 dispatcher 可以把命令推进到 `Running`
- 只有 KernelOp 返回后才能进入 `Completing`
- `Failed` 必须先完成锁释放、返回区处理、错误码固化

## 12. Error Model

调度层要处理的错误不只是算法错误，还包括执行模型错误。

至少要覆盖：

- 无效 session
- 队列已满
- 命令分类缺失
- partition 声明不完整
- 锁冲突
- guard / rollback 状态冲突
- 非法跨 partition local 写
- 调度取消
- 内部执行器故障

原则：

- 参数错误尽量在入队前发现
- 调度冲突错误在 dispatcher 阶段返回
- 算法错误由 KernelOp 返回
- 不使用异常作为常规失败通道

## 13. Memory Model

### 13.1 Queue Memory

- 命令槽位固定容量
- 参数引用指向预分配参数区或调用方缓冲区镜像区
- 不因高并发排队而产生托管堆抖动

### 13.2 Execution Context

每个运行中命令需要一个固定布局 `ExecutionContext`：

- `session_ref`
- `partition_locks`
- `return_arena_ref`
- `scratch_arena_ref`
- `error_sink`
- `telemetry_ref`

### 13.3 Return Memory

只允许两种返回路径：

- 调用方提供输出缓冲区
- session `Return Arena` 分配并由明确 API 释放

禁止：

- 临时 `new[]`
- 临时 `List<T>`
- 临时 boxing 容器

## 14. Internal Algorithm Parallelism

### 14.1 Separation Rule

命令内部允许并行，不代表 API 级也允许并发。

例如：

- 一个 `Exclusive` 布尔命令进入执行后
- 它内部可以并行做候选求交、面分类、分块计算
- 但从调度层视角，它仍然是一个独占命令

### 14.2 Requirements

- 内部并行只能使用命令私有 scratch / transaction arena
- 不可并行写 session 共享元数据，除非已在命令内部建立更细粒度同步且经过证明
- 不可改变外部可观测的 Tag / rollback / return 语义

## 15. Journaling and Diagnostics

调度层必须成为诊断采样点。

至少记录：

- `sequence_no`
- `api_id`
- `session_id`
- `concurrency_kind`
- `access_kind`
- `partition_span`
- 入队时间
- 开始执行时间
- 结束时间
- 返回码
- 分配统计

建议支持：

- session 命令时间线导出
- 锁等待统计
- 命令分类审计
- “为什么这个命令没被并发放行”的拒绝原因

## 16. Version 1 Policy

为了先把正确性立住，第一版建议采用保守策略：

- 默认所有写命令都视为 `Exclusive`
- 默认只有严格只读查询才可能标为 `Concurrent`
- `Local` 分类先落元数据和锁框架，不急于大规模开放
- guard / rollback / cloning / merge 一律按 `Exclusive` 处理
- 先保证串行正确性、可回放性、零分配，再逐步解锁并发

## 17. Suggested Data Structures

建议记录：

- `SessionDispatchState`
- `CommandDescriptor`
- `CommandQueueSlot`
- `PartitionLockRecord`
- `ExecutionContext`
- `DispatchDecision`
- `CommandTelemetry`

建议枚举：

- `ConcurrencyKind`
- `AccessKind`
- `CommandState`
- `PartitionLockState`
- `DispatchRejectReason`

## 18. Acceptance Criteria

本模型落地后，至少应满足：

- 多线程同时进入同一 session 的 API，默认得到串行一致的执行结果
- 同一 session 的只读查询命令可在证明安全后并发放行
- `Local` 命令没有 partition 隔离就绝不放行
- 任何命令都不能绕过调度层直接进入内核写路径
- 调度层主路径不产生不可接受的 GC 压力
- 调度决策、等待原因、返回状态可被诊断和回放

## 19. Open Decisions

以下内容需要在实现前进一步冻结：

- `Concurrent` 查询是否允许读取正在被 local 修改的 partition 快照
- partition 锁是否采用纯逻辑锁还是带线程所有者信息
- 同一外部线程的重入调用是否允许直接短路
- 队列满载时返回错误还是支持调用方阻塞等待
- journal / snapshot 是否在调度层直接挂接，还是由更低层统一采集
