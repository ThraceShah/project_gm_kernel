# Development Plan

## 1. Planning Constraints

本计划受以下铁律约束：

- 对外接口必须保持 Parasolid 风格的纯 C 扁平 API。
- 对外 API 与内核实现之间必须存在 session 级命令调度层。
- 默认 API 执行语义必须是串行的，不能把外部线程并发直接暴露为内核并发写入。
- 并发能力必须区分 `Exclusive`、`Concurrent`、`Local` 三类，并受 partition 隔离规则约束。
- 内核主路径不得依赖 GC，设计目标是零分配、可预测延迟。
- 所有运行时能力必须兼容 `.NET 10` `NativeAOT`。
- 任何阶段都不能通过引入托管对象图来换取短期实现速度。
- 对定义性质的层必须一次性做完整，不能长期只做拓扑或几何的一小部分定义。
- 所有里程碑必须产出可执行、可验证、可回归的最小闭环。

## 2. Phase 1: 基础设施与 API 骨架

### 2.1 Goal

跑通从 Parasolid 风格头文件到 C# NativeAOT 导出库的最小调用链，同时建立 API 调度层、session 串行执行语义、错误返回、Tag 分配与基础句柄解析的第一条生命线。

### 2.2 Key Work

- 建立 `.NET 10` NativeAOT 动态库工程骨架。
- 在调试态补充 DNNE 或等价调试桥接方案，但发布路径必须以 NativeAOT 为准。
- 从 `docs/parasolid_inc` 自动解析 typedef、enum、array struct、函数声明。
- 生成第一版 C# 侧 ABI 对齐定义：
  - `PK_*_t` 标量别名
  - `PK_*_array_t` 结构
  - 常量与错误码
  - options / result struct 的 blittable 表达
- 建立 `[UnmanagedCallersOnly]` 导出层与统一错误出口。
- 建立 `ApiEntry -> CommandDescriptor -> SessionCommandQueue -> Dispatcher -> KernelOp` 调度骨架。
- 为 API 元数据补齐并发级别字段：
  - `Exclusive`
  - `Concurrent`
  - `Local`
- 先实现默认串行调度：
  - 多线程并发进入 API
  - session 级顺序入队
  - 单执行器顺序出队
- 预留 partition 锁定与 guard 接口，但 Phase 1 先不开放真正的 local 并发执行。
- 实现 `SessionTable`、`TagAllocator`、`HandleRecord`、`EntityClass` 最小集合。
- 实现第一个最小实体链路：
  - 启动 session
  - 创建一个 point 或 vector 对应实体
  - 返回整数 Tag
  - 再次通过查询 API 解析该 Tag

### 2.3 Deliverables

- NativeAOT 可编译动态库。
- 头文件到 C# ABI 定义的自动生成脚本。
- API 调度层与 session 命令队列原型。
- 最小 session / tag / handle 运行时。
- 一个宿主侧 C 测试或等价 ABI 集成测试。

### 2.4 Exit Criteria

- 宿主进程可以成功加载动态库并调用至少一组 `PK_*` 导出。
- 生成的 ABI 定义在字段顺序、尺寸、对齐上通过自动校验。
- 多线程同时调用同一 session 时，执行结果满足串行入队语义。
- `Tag -> HandleRecord -> Slot` 查询链路可用。
- 主测试路径无额外托管分配，或分配已被压缩到初始化期且可解释。

### 2.5 Anti-Goals

- 不在本阶段实现复杂几何对象图。
- 不手写大批量绑定定义，必须优先建立自动生成能力。
- 不以 CoreCLR 调试成功替代 NativeAOT 验证。
- 不跳过命令调度层，直接让 API 入口调用底层实现。

## 3. Phase 2: 完整定义层与 DOD 内存基础

### 3.1 Goal

一次性冻结拓扑、几何、session 状态和 ABI 相关的完整定义层，同时建立可承载这些定义的 DOD 数据层：连续存储、索引引用、分层 Arena、回滚友好、可做零分配导航。

### 3.2 Key Work

- 一次性定义完整的拓扑 record 族：
  - `BodyRecord`
  - `ShellRecord`
  - `FaceRecord`
  - `LoopRecord`
  - `EdgeRecord`
  - `FinRecord`
  - `VertexRecord`
- 一次性定义完整的几何 record 族：
  - `PointRecord`
  - `VectorRecord`
  - `AxisRecord`
  - `TransformRecord`
  - `CurveRecord`
  - `SurfaceRecord`
  - `LineRecord`
  - `CircleRecord`
  - `EllipseRecord`
  - `PlaneRecord`
  - `ConeRecord`
  - `CylinderRecord`
  - `SphereRecord`
  - `TorusRecord`
  - `BSplineCurveRecord`
  - `BSplineSurfaceRecord`
- 定义完整的 class / token / entity category / ownership / adjacency 模型。
- 定义完整的 session / partition / mark / pmark / guard / cloning 状态模型。
- 为每类 record 建立独立实体池与槽位头：
  - alive bit
  - generation
  - class
  - partition
  - rollback stamp
- 实现 `Persistent Arena`、`Transaction Arena`、`Return Arena`、`Rollback Delta Store`。
- 实现局部游标分配器和会话级大块分配器。
- 设计紧凑邻接表达：
  - parent index
  - sibling / next ring index
  - packed range into side tables
- 建立基础拓扑完整性检查器，覆盖 parent-child、一致性、空槽、悬挂引用。
- 建立基础几何完整性检查器，覆盖 class 对应关系、参数布局、区间合法性、变换一致性。
- 实现最小删除与 generation 防悬挂策略。

### 3.3 Deliverables

- 一套完整定义的拓扑与几何数据字典。
- 一套不依赖对象引用的 B-Rep 与几何基础数据层。
- Arena / pool / rollback 原型实现。
- 基础拓扑构建和导航测试。
- 基础几何定义装载与查询测试。
- 分配统计与调试断言工具。

### 3.4 Exit Criteria

- 拓扑类型与几何类型的定义层已经完整冻结，而不是只覆盖当前 demo 用例。
- 可以构建最小 body-shell-face-loop-edge-vertex 拓扑链。
- 可以承载基础解析几何与自由曲线曲面记录，即使暂未实现全部算子。
- 可以在不分配托管对象的前提下完成基本导航与删除。
- 删除旧实体后，旧 Tag 不会误解析到新实体。
- Arena 扩容、回滚、临时工作区回收具备可重复测试。

### 3.5 Anti-Goals

- 不允许为了图省事改用 class 双向引用结构。
- 不允许将回滚实现为托管对象快照。
- 不允许只为 point / block / cylinder 临时定义一小撮类型后继续往后推进。

## 4. Phase 3: 核心算子翻译实验

### 4.1 Goal

从 `docs/occt` 中选取最基础、最可验证的数学模块，建立一条“OCCT 参考实现 -> AI 辅助翻译 -> 高性能 C# 重写 -> 对拍验证”的标准作业流程。

### 4.2 Candidate Scope

首批候选按风险从低到高排序：

- `gp` / `math` 基础向量矩阵运算
- `Geom` / `Geom2d` 的简单解析曲线曲面求值
- `BSplCLib` / `BSplSLib` 的基础 Bézier / B-Spline 求值
- `IntAna` / `Extrema` 的简单求交或极值模块

### 4.3 Key Work

- 为每个候选算法建立“原始来源 -> 依赖 package -> 输入输出合同 -> 容差策略”卡片。
- 拆分 OCC 实现中的：
  - 纯数学核心
  - 容差判定
  - 临时对象与缓存
  - 拓扑副作用
- 形成标准翻译模板：
  - 保留数学流程
  - 显式 scratch buffer
  - 明确输入输出 ownership
  - 明确 SIMD / `Span<T>` 可用点
- 建立对拍测试：
  - 固定样例
  - 随机样例
  - 退化样例
  - 容差边界样例
- 对首批翻译结果做 allocation profiling 和 AOT 编译验证。
- 为内部算法并发建立实验边界：
  - 只允许单命令内部并行
  - 不改变 session 级 API 串行语义
  - 优先选取布尔、求交等天然重算子做后续并发入口预研

### 4.4 Deliverables

- 第一批核心算法翻译样本。
- 翻译规范与代码审查清单。
- 数值对拍与容差回归测试集。
- 一份“哪些 OCC 写法必须禁止直接映射到 C#”的经验清单。

### 4.5 Exit Criteria

- 至少一个基础几何算法完成从 OCC 到 C# 的无损语义迁移。
- 翻译后的实现可在 NativeAOT 下编译并通过对拍。
- 算法主路径没有不可接受的托管分配。
- 团队形成可复用的 AI 辅助翻译 workflow，而不是一次性手工移植。

### 4.6 Anti-Goals

- 不在本阶段直接冲击大体量布尔内核。
- 不允许跳过对拍，直接凭肉眼认为“逻辑差不多”。
- 不允许把 OCC 的 Handle / RTTI / 异常式控制流原样搬入 C#。

## 5. Phase 4: 基础拓扑构建与测试工具闭环

### 5.1 Goal

把 API 骨架、DOD 数据层和首批几何算子接起来，形成第一个可用的建模闭环：通过 Parasolid 风格接口创建基础实体、回读验证、可视化检查。

### 5.2 Key Work

- 实现基础创建型接口：
  - point
  - vector
  - line / plane 的最小几何构造
  - block
  - cylinder
- 将构造算子接入：
  - Tag 分配
  - API 调度层投递
  - arena 写入
  - 拓扑连通
  - 查询接口
- 为并发级别补齐首版命令注册表：
  - 哪些命令必须 `Exclusive`
  - 哪些查询命令可标为 `Concurrent`
  - 哪些 local 算子未来要求 partition 锁才能放行
- 补齐最基础 enquiry API：
  - ask class
  - ask owner / parent
  - ask geometry payload
  - ask body contents
- 建立最小验证脚手架：
  - C 侧或跨语言 ABI 测试
  - 文本拓扑 dump
  - 可视化导出或调试显示桥接
- 建立端到端回归用例：创建块体、创建圆柱、查询面边顶点数量、删除、回滚、重复创建。

### 5.3 Deliverables

- 可通过 `PK_*` 风格接口创建基础几何和 body。
- 一套最小可视化或几何检查工具链。
- 端到端冒烟测试和回归测试集。
- 第一版开发者诊断手册，覆盖 session、tag、topology dump、allocation trace。

### 5.4 Exit Criteria

- 宿主可以通过纯 C 风格调用创建 block 和 cylinder。
- 宿主多线程调用查询型 API 时，默认调度语义和命令分类结果可验证。
- 生成实体的 Tag、拓扑结构和几何查询结果稳定一致。
- 至少一条端到端用例覆盖创建、查询、删除、回滚。
- 整条建模链路在目标约束下保持 AOT 可发布、接口扁平、主路径零分配导向。

### 5.5 Anti-Goals

- 不把“能显示出来”误判为“拓扑语义正确”。
- 不引入重量级 GUI 作为本阶段前置条件。
- 不为了演示效果提前接入交换格式读写模块。

## 6. Cross-Phase Engineering Rules

- 每个 phase 都必须有 NativeAOT 发布验证。
- 每个 phase 都必须有 allocation 基线，禁止分配回归无声进入主分支。
- 每个 phase 都必须保留 Parasolid 风格命名与 ABI 约束，不允许内部便利性污染外部接口。
- 每个 phase 都必须尊重 API 调度层，不允许旁路命令队列直接写 session 状态。
- 每个 phase 都必须沉淀最小失败样例，特别是容差、退化、非法 Tag、回滚破坏案例。
- 每个 phase 都必须明确区分外部 API 并发和内部算法并发，禁止混用概念。
- 所有自动生成物必须可重复生成，禁止手工修改后失去再生能力。

## 7. Recommended Execution Order

1. 先完成 Phase 1，把 ABI、session、tag、最小实体链路跑通。
2. 再做 Phase 2，一次性冻结拓扑/几何/状态定义层，避免后续算子建立在残缺模型上。
3. 之后进入 Phase 3，用最小算法单元验证 AI 翻译流程和数值纪律。
4. 最后完成 Phase 4，把基础建模、查询和验证工具闭成第一圈。

## 8. Definition of Done

满足以下条件，才能认为第一阶段内核工程真正起飞：

- 外部调用者已经可以把它当成 Parasolid 风格库来链接和调用。
- API 调度层已经建立，默认 session 级串行语义成立。
- 内部核心数据层已经确定为 `struct + arena + index`，没有回退到对象图。
- 拓扑与几何定义层已经完整成型，而不是只围绕少数演示实体打补丁。
- 首批 OCC 算法翻译已证明该路线可行，且未破坏 AOT 与零分配目标。
- 基础实体构建、查询、回滚和调试闭环已经形成，可支撑下一阶段持续扩展。
