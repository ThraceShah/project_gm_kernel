# Kernel PRD

## 1. Product Positioning

本项目的目标不是再造一个“像 Parasolid 的内核”，而是构建一个 **Parasolid API 级别的 Drop-in Replacement**：

- 对外暴露纯 C 扁平接口。
- 接口命名、参数风格、错误返回、Tag 语义、会话边界严格对标 Parasolid。
- 对外调用方应当能够以接近替换链接库的方式接入，而不感知底层实现语言为 C#。

核心价值主张：

- 使用 `.NET 10` + `NativeAOT` 构建无托管运行时依赖的原生几何内核。
- 以 Data-Oriented Design 重写传统 OOP 几何内核的数据层，优先追求零分配、缓存友好、可预测延迟。
- 以 OCCT 的公开数学与拓扑算法为参考源，沉淀一套可验证、可演进、可自动化辅助翻译的现代 C# 内核实现。

## 2. System Boundary

### 2.1 In Scope

- Parasolid 风格的 `PK_*` 纯 C API 外壳。
- Session / Partition / Mark / Error / Memory / Entity 基础设施。
- 基于 `struct` + Arena + Index 的几何与拓扑数据层。
- 基础解析几何、曲线曲面求值、B-Rep 拓扑导航。
- 基础实体构建、布尔、求交、拓扑派生等核心建模能力。
- 面向 AOT 的代码生成、绑定生成、测试和诊断工具链。

### 2.2 Out of Scope

- 任何面向 UI、交互式建模或可视化渲染的功能。
- STEP / IGES / glTF / STL 等交换格式的读写能力。
- OCAF 风格的文档对象框架、事务对象图和通用应用框架。
- 依赖 GC 行为成立的设计：对象图、委托逃逸、反射驱动分发、运行时动态代码生成。
- 非扁平对外接口：不暴露 C# 类实例、不暴露对象句柄指针、不要求宿主理解托管内存布局。

## 3. Core Design Principles

### 3.1 External Tag vs Internal Struct Index

Parasolid 语义下，Tag 是会话内唯一标识，调用方只看见整数型句柄，不接触内部地址。内核实现必须将该语义与 DOD 存储彻底解耦。

设计原则：

- 外部 `PK_*_t` 保持整数 Tag 语义，不暴露裸指针。
- 内部所有实体仅以 `(pool, slot, generation)` 形式落在连续存储中。
- Tag 解析必须先过 session 级别的句柄表，再落到具体 Arena 槽位。
- 删除实体时只回收槽位，不复用旧 generation，避免悬垂 Tag 被错误命中新实体。
- `PK_ENTITY_null` 及同类空 Tag 保留为稳定哨兵值，不映射到任何有效槽位。

建议的句柄解析模型：

- `TagTable[tag] -> HandleRecord`
- `HandleRecord = { class, arena_id, slot_index, generation, partition, flags }`
- 所有 `PK_*` 入口先校验：
  - Tag 是否非空
  - Tag 是否属于当前 session
  - class 是否匹配目标 API 预期
  - generation 是否与槽位头一致
  - 槽位状态是否为 alive

约束：

- 不允许通过对象引用追踪实体。
- 不允许在对外 API 上暴露内部 index。
- 不允许让 Tag 直接编码为数组下标并永久绑定存储位置；Tag 必须表示稳定的逻辑句柄，而不是脆弱的物理位置。

### 3.2 Arena Memory Architecture

目标不是“少 GC”，而是 **建模主路径零 GC 压力**。

设计原则：

- 所有核心拓扑实体使用定长 `struct`，例如 `BodyRecord`、`FaceRecord`、`EdgeRecord`、`VertexRecord`。
- 所有实体池使用预分配或分段扩展的大块连续内存，避免高频 `new`。
- 所有邻接关系通过整数 index、区间游标或压缩表表达，不使用引用链表。
- 所有短生命周期工作区使用栈上内存、`ref struct`、`Span<T>` 或显式 scratch arena。
- 所有跨 API 返回的数据要么由调用方提供缓冲区，要么由 session 内专用返回内存区统一管理并显式释放。

推荐分层：

- `Persistent Arena`：会话级长期存活的拓扑、几何、属性、映射表。
- `Transaction Arena`：单算子执行期临时数据，例如求交候选、分类结果、边界循环。
- `Return Arena`：对齐 Parasolid 风格的输出数组与结果结构生命周期。
- `Rollback Delta Store`：支持 mark / pmark 的增量变更记录，禁止隐式对象快照。

禁止事项：

- 禁止在热点路径分配托管数组后依赖 GC 回收。
- 禁止 `Dictionary<object, object>`、装箱、LINQ、反射遍历、异常驱动正常流程。
- 禁止以 class 层层包装 struct 池，重新引入对象图和间接寻址。

## 4. OCC Translation Strategy

OCCT 只作为算法参考源，不作为架构模板。

翻译原则：

- 保留数学不变量、容差传播逻辑、拓扑前后置条件。
- 丢弃 `Handle` 智能指针、RTTI 宏、异常风格资源控制和分散式小对象分配模式。
- 将核心计算重写为适合 `Span<T>`、SIMD、连续内存访问和显式 scratch buffer 的形式。
- 每次只翻译一个最小可验证算法单元，并建立输入输出对拍。

优先翻译对象：

- 基础向量与矩阵运算
- 解析曲线曲面求值
- Bézier / B-Spline 基础求值
- 简单求交与分类逻辑

## 5. Risk Assessment

### 5.1 Floating-Point Tolerance Drift

风险：

- OCC 算法大量依赖容差、区间裁剪、退化判定和近似相等。
- 机械照搬分支条件，极易在 C# 重写后出现边界翻转、分类不稳定、布尔裂面。

应对：

- 建立统一容差策略层，区分几何容差、拓扑容差、求解器容差、比较容差。
- 保留原算法中的判定顺序，不随意合并条件或“简化”数值分支。
- 为每个翻译单元建立极小、极大、近退化、共线、共面样例集。
- 所有容差常量必须集中管理，禁止散落魔法数字。

### 5.2 Borrowing and Mutability Conflicts

风险：

- OCC 代码默认可通过对象引用在多层调用中共享并修改状态。
- C# DOD 重写后，`ref` 生命周期、`Span<T>` 借用范围、槽位扩容失效会形成新的可变性冲突。

应对：

- 将“读取视图”和“可写句柄”严格分离，避免同一算子同时持有多个失效中的 `ref`。
- Arena 扩容采用显式阶段边界，禁止在持有旧 `ref` 时触发重定位。
- 算子内部先收集 index，再进入批量写回阶段，避免边遍历边改拓扑导致悬挂引用。
- 对关键池采用 generation 校验和调试断言，尽早暴露非法借用。

### 5.3 AOT Compatibility Regressions

风险：

- 反射、运行时代码生成、泛型膨胀不可控、委托封送和异常路径都可能破坏 NativeAOT 目标。

应对：

- 对外入口统一采用 `[UnmanagedCallersOnly]`。
- 热路径只使用 AOT 可静态分析的代码形态。
- 所有绑定生成、元数据生成、头文件解析工具与内核运行时隔离。
- 每个阶段都必须产出 NativeAOT 集成验证，不接受“先在 CoreCLR 跑通，之后再 AOT 修复”的流程。

## 6. Success Criteria

- 宿主可按 Parasolid 风格启动 session、调用 `PK_*` 接口、获取整数 Tag、基于 Tag 再次查询实体。
- 主建模路径无可观测 GC 分配。
- 基础实体创建、查询、删除、回滚在语义上满足 Parasolid 风格预期。
- 从 `docs/occt` 选取的首批算法样本可以稳定翻译为 `.NET 10` AOT 兼容、零分配导向的 C# 实现。
