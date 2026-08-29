# Parasolid part x_t 全版本支持计划

## 1. 目标

在提供对应 Parasolid schema 的前提下，完整支持该 schema 所描述的
part 文本传输格式。首批范围为 `third_party/parasolid/schema/` 中现有的
101 个 schema；后续新增 schema 必须通过同一注册、差异分析和验收流程接入。

本计划覆盖全部合法 part x_t 内容，包括：

- 普通 schema 和 embedded schema。
- 单 part、多 part、Body、Assembly 和 Instance。
- 全部可传输拓扑和几何。
- 内置及自定义 Attribute。
- User fields。
- Mesh、lattice、frame 和 indexed context。

本计划不覆盖二进制 x_b。不得以能够扫描 token、忽略未知字段或只保存
Body 外形作为“完整支持”。

目标实现采用“规范语义模型 + 无损版本扩展图”：能够安全解释的公共语义
进入可查询、可编辑的规范模型；历史、废弃或版本专属内容保留在无损扩展图
中。任何 receive/transmit 路径都不得静默丢弃合法输入。

## 2. 支持状态与声明规则

每个 schema 独立记录以下五级状态：

1. `SchemaLoaded`：schema 已严格解析并通过自校验。
2. `CodecLossless`：全部 transmitted 字段可保持类型、presence、数组基数、
   引用和数值无损往返。
3. `SemanticMapped`：公共语义已进入规范模型；版本专属内容已进入无损扩展图。
4. `ParasolidVerified`：本项目重编码的文件可被真实 Parasolid 接收，并通过
   对应的语义比较。
5. `Complete`：该 schema 的全部适用 API 语料均通过，且不存在未解释缺口。

状态必须逐级满足。只有达到 `Complete` 才允许对外声明该 schema 完整支持。
缺少对应 runtime、许可、文档或可达构造路径时，必须记录明确的阻塞状态，
不得用“未发现问题”“预计兼容”或单个 smoke case 代替验收。

支持矩阵至少记录：

- 完整 schema identity、schema 文件和文件摘要。
- 对应 modeller/kernel version 和 transmit version。
- 五级状态及最后验证时间。
- 适用、通过、失败、不可达、版本受限和许可受限 case 数量。
- 未解决差异及其责任模块。
- 使用的 Parasolid runtime、平台和许可能力。

## 3. Schema 基础设施

### 3.1 严格解析和生成

将当前单一静态 `XtSchema` 改为按完整 schema identity 选择的只读 registry，
初始注册现有 101 个 schema。运行时不得只根据 schema 名字的数字后缀猜测
兼容性。

schema 生成阶段必须验证：

- Header、terminator、schema identity 和声明的统计信息。
- 节点数量、节点 ID 唯一性、字段数量和字段顺序。
- 节点 transmitted/variable 标志。
- 字段类型、transmitted 标志、pointer class 和 element count。
- 标量、固定数组和变长数组的合法声明。
- schema 引用的 node class 是否存在或属于明确的抽象 class。

任何未识别非空行、字段数不一致、重复 ID 或非法引用都必须使生成失败。
生成结果必须确定性稳定，并提供 `--check` 模式验证仓库内容未过期。

### 3.2 Schema identity 和版本选择

registry 必须保存完整 identity，而不是只保存 schema number。接收时按以下
顺序确定 schema：

1. 解析并验证文件 header。
2. 若存在 embedded schema，验证其 identity 和内容后使用该定义。
3. 否则从内置 registry 精确匹配 schema identity。
4. 找不到匹配项时返回明确的不支持格式错误，不得退回当前 schema 猜读。

发送时严格遵守 `PK_PART_transmit_o_t.transmit_version` 及其他传输选项，选择
对应 schema。默认版本也必须解析为明确的目标 schema，并写入支持矩阵。

## 4. 通用 x_t Codec

Codec 必须覆盖：

- 标量、固定数组、变长数组以及同时包含标量前缀和变长尾部的节点。
- `?`、数值零、空字符串、空数组和 null pointer 的严格区分。
- schema 中出现的全部字段码。
- 节点索引、共享引用、环、前向引用和合法的空引用。
- 普通 schema 与 embedded schema。
- User-field header 和原始 payload。
- Mesh、indexed context 和多 part transmit block。
- 长度前缀字符串、字符字段、Unicode 数据、数字格式和合法换行。

解码结果必须显式保存字段边界、element count、presence 和原始类型，不得只
保存无法恢复字段结构的扁平值数组。编码时的变长长度由对应变长字段决定，
不得使用整个节点值数量推断。

结构无损测试以规范化 node graph 为准：节点顺序、空白和浮点文本表示可以
变化，但字段值、presence、数组维度和引用图必须一致。

## 5. 规范模型与无损扩展图

规范模型必须使用适合 `.NET 10`、NativeAOT 和 DOD 的连续存储、类型化索引
及显式所有权，不依赖反射或托管对象引用图。模型至少包括：

- Part、Body、Assembly、Instance。
- Region、Shell、Face、Loop、Fin、Edge、Vertex。
- Point、curve、surface、transform 和 frame。
- Attribute definition、Attribute、字段定义和值。
- Mesh、lattice 及其公开拓扑和几何数据。
- User fields 和 indexed context 的 owner/payload 关联。

无损版本扩展图保存：

- 原 schema identity。
- 无法安全规范化的节点和字段。
- 原始字段类型、presence、数组基数和值。
- 节点引用及其与规范实体的映射。
- 历史、废弃和版本专属判别信息。

修改规范实体后重新发送时，adapter 必须明确合并规范模型和扩展图。发生
冲突时不得静默选择一方；必须采用文档化的确定性规则或返回不可表示错误。

## 6. 版本 Adapter 与降级

每个 schema adapter 负责规范模型、无损扩展图和该 schema node graph 之间
的转换。相邻版本优先共享声明式差异规则，但不得假设节点 ID 相同就具有完全
相同语义。

向旧 schema 发送前必须执行可表示性检查，覆盖：

- 目标版本是否存在对应实体或字段。
- enum/token 是否在目标版本合法。
- 数值范围、数组长度和字符串能力是否兼容。
- Attribute definition、owner 和字段类型是否可表示。
- Assembly、mesh、lattice、user fields 和 indexed context 是否受支持。

不能无损表示时返回明确错误并输出诊断，不得删除实体、截断数组、替换 enum、
清空 Attribute 或降低几何类型后继续成功返回。

不新增非 Parasolid 风格公共 ABI。优先完整实现现有 `PK_PART_receive_b`、
`PK_PART_transmit_b` 及其 options、错误码和内存所有权语义。

## 7. 实施顺序

1. 修正当前 codec 的固定数组、变长长度、presence 和 token 边界问题。
2. 建立 101-schema registry、严格生成检查和 schema identity 映射。
3. 实现无损 node graph 及 decode/encode 结构往返。
4. 实现 embedded schema、user fields、mesh 和 indexed context。
5. 建立规范 part 模型及无损版本扩展图。
6. 依次完成 topology、geometry、Assembly/Instance、Attribute、mesh/lattice
   的语义 adapter。
7. 接入 `docs/parasolid_part_api_corpus_plan.md` 定义的语料矩阵，逐 schema
   推进支持状态。
8. 将支持矩阵纳入持续验证；发现新 schema 时自动生成差异和待覆盖项。

每一步都必须形成能够被真实 Parasolid 验证的闭环，不得先批量声明支持、
再把验证推迟到所有实现结束之后。

## 8. 验收流程

对每个目标 schema 和每个适用的合法语料执行：

1. 使用真实 Parasolid API 构造基准模型。
2. 使用目标 `transmit_version` 生成原始 x_t。
3. 本项目 receive，检查 node graph、规范模型和扩展图。
4. 本项目按同一 schema transmit。
5. 真实 Parasolid receive 本项目输出。
6. 比较接收结果和步骤 1 的基准模型。
7. 将接收结果再次 transmit，并验证规范语义 hash 稳定。

不要求文本字节、节点编号或节点排序完全相同。以下比较为强制要求：

### 8.1 Body

使用 `PK_DEBUG_BODY_compare`。Receive 成功、拓扑数量、region/shell 语义、
类型、sense、容差和 missing 类差异必须作为通过条件。若 full local deviation
或 face matching 将内部参数化、节点编号或 transmit 排序差异误判为失败，
可降为诊断，但必须保存 `global_result`、`local_result`、diff 类型及相关实体。

### 8.2 Assembly 和 Instance

递归比较：

- Part 集合和层级。
- Instance 数量、引用目标和顺序语义。
- Transform。
- 共享 Part 和共享引用。
- 空 Assembly 与多层嵌套。

### 8.3 Attribute

比较 definition identifier、type、actions、legal owners、字段名称、字段类型、
字段数量、presence、数组长度、值、owner 和 Attribute 链接。内置属性还必须
记录 runtime/version/license；自定义属性必须比较完整 definition 和 payload。

### 8.4 User fields

通过真实 callback 比较原始 payload、长度、owner、调用次数和关联顺序。
不得只检查 user-field size 非零。

### 8.5 Mesh、lattice 和 indexed context

使用对应 ask/check API 比较公开类型、连接关系、owner、几何数据、索引数据、
边界和 Attribute。无法公开查询的 transmitted 数据必须由无损 node graph
比较补足并记录限制。

任一适用 case 失败，该 schema 不得标记 `Complete`。

## 9. 持续验证与新增版本

新增 schema 时必须：

1. 严格解析新 schema 并生成与最近版本的节点/字段差异。
2. 更新 registry 和 transmit-version 映射。
3. 将新增或变化内容映射到规范模型或无损扩展图。
4. 更新 API 语料适用矩阵。
5. 在对应真实 Parasolid runtime 可用时完成双向 oracle 验证。
6. 更新支持矩阵；未完成项保持明确的非 `Complete` 状态。

历史废弃状态若无法通过当前公共 API 构造，不得手写 x_t 后宣称语义验收
通过。可以使用合成节点做 codec 单元测试，但必须将真实 oracle 状态记录为
未完成，等待对应历史 runtime、合法种子文件或可证明的公共构造路径。

