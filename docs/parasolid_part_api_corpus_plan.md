# Parasolid part API 合法测试语料计划

## 1. 目标与原则

建立长期保留、可重复运行的 Parasolid API 测试模型生成系统，以真实 Parasolid
成功创建并 transmit 的合法数据验收 `docs/xt_all_versions_support_plan.md`。

覆盖定义为：

> 全部可枚举的持久状态生产 API 语义分支、参数等价类和合法边界，加上所有
> 不可达、版本受限及许可受限项的可审计记录。

覆盖来源是 Parasolid API 参数定义、options/result struct、token 常量、API
文档和真实 runtime 行为，不是 x_t schema 节点定义。手写 schema 节点只能
用于 codec 单元测试，不能作为合法 Parasolid 语义语料或目标 1 的验收依据。

本计划只纳入能够创建、修改或影响最终 part x_t 持久状态的 API，以及构造
这些状态所必需的辅助 API。纯查询、会话、线程、内存和 debug API 不作为
语料生产覆盖目标，但可作为比较、检查和基础设施使用。

## 2. 长期保留的清单和生成代码

后续实现必须保留以下代码和数据，不得在语料生成后删除：

- `scripts/GenerateParasolidApiCoverage.cs`：从 header 和 PKToy P/Invoke 绑定
  生成 API、参数、结构体和 enum 清单及 Markdown 视图。
- `scripts/GenerateParasolidXtCorpus.cs`：调用真实 Parasolid 构造模型并生成
  x_t、manifest 和语义摘要。
- `tests/ParasolidXtCorpus/coverage/`：机器清单、人工语义注解、稳定 case ID、
  不可达审计和预期结果。
- `tests/ParasolidXtCorpus/Fixtures/`：精选、可审阅、适合离线回归的 golden
  x_t 和 manifest。

全量语料默认生成到测试项目 `bin/` 下，不提交仓库，由 CI 保存为 artifact。
生成器必须支持确定性运行和 `--check`，连续两次运行必须产生相同 case 集和
规范语义 hash。

两个 `scripts/` 下的 C# 脚本必须使用 `.NET 10` 单文件脚本形式，并按
`AGENTS.md` 要求引用 PKToy 的 P/Invoke、全局别名和 `ParasolidScriptHost`。
禁止复制 `PK_*` 类型、ABI struct、P/Invoke 声明或 session 初始化代码。
脚本中的默认路径必须相对于脚本自身解析。

## 3. API 和类型清单

### 3.1 来源

首版清单以以下内容为权威输入：

- `third_party/parasolid/include/parasolid_kernel.h`。
- `third_party/parasolid/include/parasolid_tokens.h`。
- `third_party/PKToy/PskernelSharp/parasolid.g.cs`。
- `third_party/PKToy/PskernelSharp/Types.g.cs`。
- 可用的对应版本 Parasolid API 文档和真实 runtime。

获得历史版本 header/runtime 后，必须生成版本化 API/type/token 差异，不能
用当前 header 推断历史版本一定具有相同构造语义。

### 3.2 必须提取的定义

清单生成器必须提取并关联：

- 所有函数签名及其版本来源。
- 参数方向、typedef 语义类型、pointer/array/count 关系。
- Options、result 和 standard-form struct 的全部递归字段。
- 每个 enum/token typedef 及其全部公开常量。
- Entity tag 的合法 class/subclass。
- Owner、输入、输出、修改对象和新建对象关系。
- Callback 签名、payload、生命周期和返回语义。
- API 文档中可机器提取的范围、互斥、依赖和默认值。

机器清单必须生成可审阅 Markdown 视图。人工只维护无法可靠自动推断的语义
注解；不得手写并行的 API 全集。

### 3.3 API 分类

每个枚举到的 API 必须且只能属于以下一类：

- `Producer`：创建会进入 part x_t 的持久实体或数据。
- `Mutator`：修改现有实体的可传输状态。
- `RequiredHelper`：构造 Producer/Mutator 合法输入所必需。
- `ExcludedPureQuery`：只读取状态，不产生持久变化。
- `ExcludedRuntimeOnly`：只影响 session、线程、内存、调试或瞬时算法状态。
- `Unreachable`：根据当前版本、许可和公共 API 无法形成合法目标状态。

禁止存在未分类 API。新增 header 若出现新 API、struct 字段、enum 常量或实体
class，`--check` 必须失败，直到新增覆盖或不可达审计。

## 4. 逐类型覆盖定义

每个类型必须在机器清单中关联具体构造/修改 API、参数分支、最小模型、组合
模型、适用版本和比较方法。

### 4.1 Part 根和 Body 类型

- Part 根：Body、Assembly、Instance。
- Body 类型：solid、sheet、minimum、wire、general、acorn、unspecified、
  empty、compound。

每种 Body 类型至少包含一个独立最小模型和一个与其他拓扑/几何/Attribute
组合的模型。若某类型不能形成合法可 transmit part，必须进入不可达审计。

### 4.2 Topology

逐项覆盖 Region、Shell、Face、Loop、Fin、Edge 和 Vertex，包括公开 API 可构造
的以下分支：

- Solid region 与 void region。
- 单 shell、多 shell、内外 shell 和空边界合法状态。
- Face 正反 sense、双侧使用和周期 surface seam。
- 外 loop、内 loop、多 loop 和合法退化 loop。
- Edge 正反使用、共享、非流形、退化及带/不带可选 curve。
- Fin 顺序、sense、配对、vertex 关联和周期参数分支。
- Vertex 带 point、无 point、共享及合法退化状态。
- Wire、sheet、solid、general 和 compound 中各类型的所有权差异。

非法拓扑关系只用于验证错误合同，不得保存为合法语料。

### 4.3 Point 与辅助几何

逐项覆盖：

- Point。
- Vector 和 direction。
- Axis1、Axis2 及其他公开 axis standard form。
- Interval 和参数区间。
- Box、UV、UVBox 及其他公开边界结构。
- Transform。
- Frame。

这些辅助类型即使不独立成为 part，也必须通过使用它们的持久状态生产 API
覆盖全部成员和合法语义分支。

### 4.4 Curve

逐项覆盖：

- Line。
- Circle。
- Ellipse。
- ICurve。
- BCurve。
- SPCurve。
- FCurve。
- CPCurve。
- TRCurve。

每种 curve 至少覆盖 standard form 的全部成员、合法 orientation、interval、
open/closed、periodic/non-periodic、regular/degenerate 和 owner 分支。程序化、
foreign 或依赖 callback 的 curve 必须保存其构造代码和 callback 生命周期测试。

### 4.5 Surface

逐项覆盖：

- Plane。
- Cylinder。
- Cone。
- Sphere。
- Torus。
- BSurf。
- BlendSF。
- Offset。
- Swept。
- Spun。
- FSurf。

每种 surface 至少覆盖 standard form 的全部成员、orientation、参数域、周期性、
退化边界、owner 和可选基础 geometry。程序化及 foreign surface 必须覆盖 callback
数据、生命周期及成功/拒绝分支。

### 4.6 Spline 数据

BCurve、BSurf 及相关 spline 构造必须覆盖：

- Degree 和 order 的合法最小、典型和合法上界。
- Control vertices 的最小、典型和边界数量。
- 有权重/无权重、rational/non-rational。
- Open/closed、periodic/non-periodic。
- Knot values、multiplicity、重复 knot 和合法端点条件。
- Clamped/unclamped。
- 每个公开 form token。
- Self-intersection 的每个公开状态。
- 文档允许的退化和低维边界。

各分支必须同时验证 API 查询结果、x_t 往返和真实 Parasolid 接收结果。

### 4.7 Assembly 和 Instance

至少覆盖：

- 空 Assembly。
- 单层和多层 Assembly。
- 一个 Assembly 引用多个 Part。
- 多个 Instance 共享同一 Part。
- 嵌套 Assembly 和重复引用。
- Identity、translation、rotation 和合法组合 transform。
- Instance 更换 Part 和替换 transform 后的持久状态。
- 循环引用、非法类型和非法 transform 的拒绝分支。

合法模型必须保存层级、共享关系、Part identity 和 transform。循环引用等失败
case 只记录错误合同，不生成合法 fixture。

### 4.8 Attribute definition 和 Attribute

逐项覆盖：

- AttDef identifier、type、actions 和 legal owners。
- 每个合法 owner class。
- 命名字段和未命名字段。
- 零字段、单字段和多字段定义中 API 允许的状态。
- Scalar、array、空数组、presence 和 null。
- 单 owner 多 Attribute、同 definition 链和多个 definition。
- Copy、delete、transform、transmit 等 action 对持久状态的影响。

字段类型至少包括：

- Integer。
- Real。
- String。
- Unicode string。
- Vector。
- Coordinate/point。
- Direction。
- Axis。
- Entity/tag。
- Pointer。
- 当前 header 枚举出的其他 Attribute 字段类型。

内置 Attribute definition 必须从实际 runtime 枚举并逐项记录 identifier、版本、
许可、合法 owner 和字段定义。自定义 Attribute 必须覆盖每种字段类型、字段数、
数组长度、presence、名称、owner 和 action 分支；不把无限的名称和值集合误称
为可穷举范围。

### 4.9 User fields

覆盖：

- 零长度、单字节、多字节和文档允许的边界长度。
- 不同合法 owner。
- 多实体具有独立 payload。
- Callback 成功、主动拒绝、缺失和错误返回。
- Transmit/receive 后 payload、长度、owner 和 callback 调用语义。

只有 callback 成功且 Parasolid transmit/receive 接受的数据可以进入合法语料。

### 4.10 Mesh、lattice、frame 和 indexed context

逐项枚举并覆盖当前公共 API 暴露的持久类型，包括：

- Mesh、Pline。
- MTopol、MFacet、MFin、MVertex、MFin index。
- Lattice、LTopol、LRod、LBall、IJKBox。
- Frame。
- Indexed context 关联的数据。

覆盖实体成员、连接关系、owner、索引、边界、几何和 Attribute。Groups、AppItems
及其他候选类型必须用真实 transmit 实验判断是否进入 part x_t：进入则纳入
持久语料；否则附证据分类为非持久或 runtime-only。

## 5. 参数等价类和边界规则

每个函数参数和 struct 字段递归应用以下规则，并由人工语义注解收窄到该 API
实际合法域。

### 5.1 基础类型

- Logical：`false`、`true`。
- Enum/token：每个文档化合法常量；unknown/invalid 只用于错误合同。
- 整数、数量和索引：合法最小值、典型值、合法上界；零、负数和越界分别按
  参数合同处理。
- Real：合法边界、边界内、典型值、大量级和小量级；NaN、Infinity 和越界
  只作为拒绝测试。
- Length、radius、tolerance、angle 和 parameter 必须分别定义物理合法域，
  不得统一按裸 `double` 处理。

### 5.2 几何值

- Vector/direction：零、非零、单位、非单位、正交、平行和反平行，按 API
  合法域选择。
- Interval：有限、跨零、周期 seam、半无限和无限，仅在 API 合法时生成。
- Transform：identity、translation、rotation、合法组合、反射和缩放；奇异
  或非法类型只验证错误。
- Box/UVBox：点、非零范围、跨零、周期边界和文档化的无界状态。

### 5.3 容器与引用

- String/Unicode：空、ASCII、Unicode、典型长度和合法边界长度；非法编码只
  验证错误。
- Array/count：0、1、多个和合法上界；null/count 关系、重复元素、顺序变化、
  共享引用和别名按 API 合法语义覆盖。
- Entity handle：每个合法 subclass；null、wrong class、deleted 和 cross-partition
  作为错误合同测试。
- Options struct：默认值、每个成员独立非默认值、全部文档化互斥/依赖组合，
  以及会改变持久结果的多成员组合。
- Callback：成功、主动拒绝、边界 payload、错误返回和生命周期分支。

成功分支必须生成合法 x_t 并进入目标 1 验收。非法边界只验证 Parasolid 返回码
及 session 完整性，不得把失败后的模型作为合法 fixture。

## 6. Case 定义和 Manifest

每个 case 使用稳定、可读且不依赖执行顺序的 ID。Case 定义至少包含：

- Producer/Mutator API 名称和版本。
- 前置模型及依赖 case。
- 完整输入参数和 options。
- 所覆盖的参数等价类、enum 分支和实体类型。
- 预期成功值或明确错误码。
- 输出/修改实体及其 class、owner 和数量。
- 适用 transmit version/schema。
- 语义比较器和强制/诊断差异规则。
- 所需 runtime、平台、许可和 callback 能力。
- 清理和 session 隔离要求。

每个成功 fixture manifest 保存：

- 稳定 case ID。
- 构造 API 和完整参数。
- Parasolid kernel/schema 版本。
- Transmit options。
- 适用 schema 列表。
- Entity class/count。
- Attribute、user-field、mesh 和 indexed-context 摘要。
- 预期比较策略。
- Canonical semantic hash。
- Generator 版本和输入清单摘要。

不得使用 x_t 文件字节 hash 代替 semantic hash，因为合法 transmit 可能改变
节点编号、排序、空白和浮点文本形式。

## 7. 不可达项审计

每个不可达、版本受限或许可受限项必须记录：

- API、type、参数分支和稳定 case ID。
- Parasolid kernel/header 版本。
- 所需前置状态。
- 尝试过的合法公共构造路径。
- 实际返回码和诊断。
- 文档、header 或 runtime 行为依据。
- 缺失的许可、模块、历史 runtime 或种子数据。
- 负责人、最后审计时间和重新审计触发条件。

不可达项不能计入 covered。获得新 header、runtime、许可或发现新 Producer API
时，相关审计必须自动失效并重新评估。

历史废弃状态若无法由当前公共 API 构造，不得通过手写 x_t 将其伪装成合法
Parasolid 语料。可以保留针对该 schema 的合成 codec 测试，但目标 1 的
`ParasolidVerified` 和 `Complete` 状态必须保持未完成。

## 8. Oracle 流程

每个成功 case 执行：

1. 启动真实 Parasolid session。
2. 仅通过公共 Parasolid API 构造或修改模型。
3. 使用 ask/check API 验证构造结果。
4. 按适用版本 transmit 为 x_t。
5. 由同一真实 Parasolid runtime 自接收并复查，证明 fixture 合法。
6. 将 x_t 交给本项目完成 receive/transmit。
7. 由真实 Parasolid 接收本项目输出。
8. 按 `docs/xt_all_versions_support_plan.md` 的类型化规则比较基准和结果。
9. 生成 manifest、semantic hash 和差异诊断。

真实 Parasolid 不可用时允许报告 skip，但该 case 不得计为通过。Oracle 脚本
必须输出足以定位 API、case、版本、实体和差异类型的信息。

## 9. Golden 样本和全量语料

提交到 `tests/ParasolidXtCorpus/Fixtures/` 的精选样本必须覆盖：

- 每个主要 Part/Topology/Geometry entity class 的最小代表。
- Assembly/Instance 专项模型。
- 内置和自定义 Attribute 专项模型。
- User fields 专项模型。
- Mesh/lattice/indexed context 专项模型。
- 至少一个多特性组合模型。
- 能代表主要 schema 代际变化的小型历史样本。

全量版本乘以全量 case 的语料由生成器写入测试项目 `bin/`，CI 上传 artifact。
Artifact 必须携带完整 manifest、支持矩阵、失败诊断和生成环境信息，使任何
失败都能按 case ID 重建。

## 10. 完成条件

目标 2 只有同时满足以下条件才完成：

- 持久状态生产 API 100% 已分类。
- 递归参数类型、struct 字段和 enum 常量 100% 已分类。
- 每个合法可达语义分支至少有一个成功 case。
- 每个边界等价类已有成功语料或明确的错误合同。
- 每个实体类型至少有一个独立最小模型及一个组合模型。
- Assembly、Attribute、user fields、mesh/lattice 和 indexed context 均有专项模型。
- 所有成功 case 均能生成 manifest 和 x_t，并通过真实 Parasolid 自接收。
- 生成器连续两次运行产生相同 case 集和 semantic hash。
- 精选 golden 样本可离线回归，全量语料可由 CI 重建。
- 不可达、许可受限和版本受限项目单独统计，且不计入 covered。
- 目标 1 的 schema 只有在其全部适用 case 通过后才能标记 `Complete`。

