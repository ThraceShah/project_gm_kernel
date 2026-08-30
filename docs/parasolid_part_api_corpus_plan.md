# Parasolid part API 合法测试语料计划

> 当前实施版（typed corpus gate）以“可传输数据类型、standard-form 取值和
> manifold 拓扑场景”为完成单位。Producer/Mutator API 仍生成 provenance 报告，
> 但不再要求为产生相同数据的冗余 API 各生成一份 x_t。矩阵、x_t schema 图和
> 精确拒绝合同分别由 `tests/ParasolidXtCorpus/coverage/type-matrix.json`、
> case manifest 和 `scripts/CheckParasolidTypeCoverage.cs` 门禁。

本轮明确排除 FCurve/FSurf、Mesh/PLine/MTopol/Lattice、indexed-I/O、General body、
non-manifold topology、edge blend、three-face blend 以及 `PK_SURF_create_blend`。
这些项目只退出本轮 corpus 门禁，不改变 `docs/xt_all_versions_support_plan.md`
的全格式目标。TRCurve 通过共享 KI `CRTRCU` binding 保留。

## 1. 目标与原则

建立长期保留、可重复运行的 Parasolid API 测试模型生成系统，以真实 Parasolid
成功创建并 transmit 的合法数据验收 `docs/xt_all_versions_support_plan.md`。

覆盖定义为：

> 全部可枚举的持久状态生产 API 语义分支、参数等价类和合法边界，加上所有
> 不可达、版本受限及许可受限项的可审计记录。

覆盖来源是 Parasolid API 参数定义、options/result struct、token 常量、API
文档、真实 runtime 行为以及 transmit 后的 x_t schema 节点图。schema 节点只
作为成功 API case 的结构断言，不能单独伪造合法语料。

本计划只纳入能够创建、修改或影响最终 part x_t 持久状态的 API，以及构造
这些状态所必需的辅助 API。纯查询、会话、线程、内存和 debug API 不作为
语料生产覆盖目标，但可作为比较、检查和基础设施使用。

## 2. 长期保留的代码、清单和产物

后续实现必须保留以下代码和数据，不得在语料生成后删除：

- `scripts/GenerateParasolidApiCoverage.cs`：从 header 和 PKToy P/Invoke 绑定
  生成 API、参数、结构体和 enum 清单及 Markdown 视图。
- `scripts/GenerateParasolidXtCorpus.cs`：发现并调度 case-group 脚本，聚合
  manifest、覆盖率和诊断，提供 `--list`、`--case`、`--group` 和 `--check`。
- `scripts/CheckParasolidXtCorpusCoverage.cs`：将 API 清单中的 Producer/Mutator
  与 canonical case manifest 对齐，生成稳定缺口报告；`--strict` 在仍有未覆盖且
  未具备不可达分支审计的持久 API 时失败。
- `scripts/GenerateParasolidTypeMatrix.cs`：校验 semantic matrix 与生成的
  header/token/type/schema 输入，生成稳定输入哈希报告。
- `scripts/CheckParasolidSchemaDependencies.cs`：逐 manifest 验证 required schema
  节点和抽象依赖边存在于 transmitted XT 图。
- `scripts/CheckParasolidXtGoldenFixtures.cs`：校验精选 golden fixture 的 manifest、
  diagnostics 和模型哈希。
- `scripts/ParasolidXtCorpusHost.cs`：由各 case-group 脚本复用的公共运行框架，
  负责 case contract、输出隔离、transmit、真实 Parasolid 自接收、manifest、
  semantic hash 和诊断；它必须像 `ParasolidScriptHost.cs` 一样由 MSBuild property
  链接，不能被复制到各 case-group 脚本。
- `scripts/ParasolidXtCorpusCases/`：按工作包拆分、可分别执行的 `.NET 10` C#
  单文件脚本。每个脚本只实现被分配的一组相关 entity case，不拥有公共框架。
- `tests/ParasolidXtCorpus/coverage/`：机器清单、按工作包拆分的人工语义注解、
  稳定 case ID、不可达审计、覆盖缺口报告和预期结果。
- `tests/ParasolidXtCorpus/Fixtures/`：精选、可审阅、适合离线回归的 golden
  x_t 和 manifest。

每个成功 case 必须输出到独立目录：

```text
<output>/<case-id>/model.x_t
<output>/<case-id>/manifest.json
<output>/<case-id>/diagnostics.json
```

同一 entity 类型的不同参数分支、owner 分支或组合模型必须使用不同 case ID 和
不同 x_t，不能合并为一个难以定位差异的大文件。拒绝分支和不可达项不得生成
合法 `model.x_t`，只保存错误合同或不可达审计。全量语料默认生成到测试项目
`bin/` 下，不提交仓库，由 CI 保存为 artifact。

所有生成脚本必须使用 `.NET 10` 的 `dotnet run file.cs` 形式，并按 `AGENTS.md`
要求引用 PKToy 的 P/Invoke、全局别名和 `ParasolidScriptHost`。禁止复制 `PK_*`
类型、ABI struct、P/Invoke 声明、session 初始化或 transmit/manifest 公共代码。
脚本中的默认路径必须相对于脚本自身解析。

生成器必须支持确定性运行和 `--check`。连续两次运行必须产生相同 case 集、
每个 case 相同的规范语义 hash，以及不依赖发现顺序的聚合 manifest。

## 3. 主 agent 与 subagent 并行实施计划

### 3.1 并行边界

并行的基本单位是 case-group 的代码实现，不是共享框架，也不是一次全量
Parasolid session。一个 case-group 可以负责一个 entity 类型，也可以负责一组
构造依赖紧密的类型；每个成功 case 仍然生成独立 x_t。

不得机械地为 Vertex、Edge、Fin、Loop 等强依赖拓扑类型各派一个 subagent。
entity 专项 case 可以包含构造合法模型所必需的其他 entity，但 manifest 必须
明确本 case 的目标覆盖项和仅作为前置条件的辅助项。

公共 contract 冻结前不启动批量 case 实现。公共 contract 变更只能由主 agent
完成；subagent 如果发现公共能力不足，应报告所需最小接口和复现 case，不得在
自己的文件中复制临时框架绕过限制。

### 3.2 主 agent 职责

主 agent 负责且不得并行分派以下共享工作：

- 实现 API/type/token 清单生成器和人工注解 schema。
- 定义稳定 case ID、case 元数据、输出目录、manifest 和诊断 schema。
- 实现 `ParasolidXtCorpusHost.cs`、case-group 发现和调度器。
- 提供一个最小成功 case 和一个预期拒绝 case 作为模板。
- 冻结 subagent 所需的最小公共接口和文件所有权。
- 审查 subagent 结果，处理公共框架缺口，合并覆盖清单并检测重复 case ID。
- 串行执行首次全量生成、自接收、确定性检查和最终覆盖统计。
- 选择可提交的 golden fixtures，并更新目标 1 的支持状态。

在任何 subagent 开始前，主 agent 必须先证明模板 case 能独立执行、生成一 case
一 x_t、通过真实 Parasolid 自接收，并能由聚合器按 case ID 重跑。

### 3.3 Subagent 交付合同

每个 subagent 必须获得明确的工作包名称、目标 entity/API 清单、允许修改的
文件、禁止修改的共享文件和验收命令。交付必须包括：

- 一个独立 case-group C# 脚本，只修改该工作包拥有的文件。
- 一个独立人工注解文件和一个独立不可达审计文件；推荐所有权形式为
  `scripts/ParasolidXtCorpusCases/<group>.cs`、
  `tests/ParasolidXtCorpus/coverage/cases/<group>.json` 和
  `tests/ParasolidXtCorpus/coverage/unreachable/<group>.json`。
- 该组每个成功 case 的稳定 ID、完整输入、覆盖标签和预期摘要。
- 每个合法分支独立生成的 x_t，不用一个组合文件代替多个专项 case。
- ask/check 验证、真实 Parasolid 自接收结果和可定位诊断。
- 预期拒绝 case 的返回码验证，但不为其生成合法 fixture。
- 当前 runtime 无法构造项的审计证据，不把它们计为 covered。
- 仅运行该 group 的验证结果和仍未覆盖的明确列表。

subagent 不得修改聚合器、公共 host、全局 manifest schema、其他工作包脚本或
总覆盖统计。确需公共修改时，由主 agent 先补充并验证公共接口，再让受影响的
subagent 基于冻结后的接口继续。

每个 case-group 脚本必须实现公共 host 规定的最小命令合同：列出稳定 case
元数据、按 case ID 执行、按 group 执行、指定输出目录和执行本组 `--check`。
包含拒绝分支的工作包还必须实现 `--check-rejections`，该命令只验证返回码和
session 完整性，不写入合法 fixture。
聚合器只能通过该合同启动独立 `dotnet run file.cs` 进程和读取结构化结果，不能
通过解析控制台自然语言或源码文本发现 case。

### 3.4 工作包划分

首轮按构造依赖而不是按 schema 节点机械拆分：

| 工作包 | 主要覆盖 | 依赖与边界 |
| --- | --- | --- |
| Body 基础 | Part 根、各 Body 类型、最小模型 | 作为其他组可复用的前置模型，但不建立通用 builder 抽象除非已有两个消费者 |
| Topology 基础 | Region、Shell、Face、Loop、Fin、Edge、Vertex | 同组处理强耦合的合法所有权和 sense |
| Topology 高级 | 共享、seam、退化、非流形、多 shell | 在基础模型可稳定生成后启动 |
| Analytic curve | Line、Circle、Ellipse | 每种类型、参数和 owner 分支独立 case |
| Complex curve | ICurve、BCurve、SPCurve、FCurve、CPCurve、TRCurve | callback 和 spline 分支可在组内继续拆分，但不得共享编辑文件 |
| Analytic surface | Plane、Cylinder、Cone、Sphere、Torus | 每种类型、参数域和 owner 分支独立 case |
| Complex surface | BSurf、BlendSF、Offset、Swept、Spun、FSurf | 依赖基础 curve/surface case 通过后启动 |
| Assembly | Assembly、Instance、层级、共享与 transform | 只覆盖合法 x_t；循环引用进入拒绝测试 |
| Attribute | AttDef、内置和自定义 Attribute | owner 矩阵按稳定 case ID 拆分 |
| Group | Group 创建、成员增删与标签 | 先覆盖 body-owned topology group，再扩展 owner/entity-class 矩阵 |
| User fields | payload、owner、callback 和往返 | 与 Attribute 分开，避免 callback/manifest 文件争用 |
| Mesh/lattice | Mesh、Pline、MTopol、Lattice 及相关 entity | runtime 探测后确认可持久类型 |
| Context | Frame、indexed context、其他候选持久类型 | 未证明可 transmit 的候选先进入审计 |

如果一个工作包仍会使两个 subagent 修改同一文件，主 agent 必须先把它拆成两个
拥有独立脚本和独立注解文件的子工作包；禁止依赖事后解决大范围文本冲突。

### 3.5 分波次执行

并行实施分为以下门禁阶段：

1. 主 agent 建立清单、公共 host、调度器和模板 case。
2. 主 agent 运行模板的 transmit、自接收、聚合和确定性检查，随后冻结 contract。
3. 第一波并行实现 Body 基础、Topology 基础、analytic curve 和 analytic surface。
4. 主 agent 集成第一波，补充确有两个以上消费者的最小公共能力并重新冻结。
5. 第二波并行实现 complex curve、complex surface、Assembly 和 Attribute。
6. 第三波并行实现 User fields、Topology 高级、Mesh/lattice 和 Context。
7. 主 agent 串行完成全量生成、缺口回派、golden 选择和最终完成条件检查。

每一波只在上一波依赖 case 已通过真实 Parasolid 自接收后开始。某个工作包失败
不阻塞与它无依赖的工作包，但依赖它的后续工作包不得用复制前置模型的方式抢跑。

### 3.6 文件所有权与集成门禁

主 agent 在派发表中为每个 subagent 列出唯一可写文件。多个 subagent 可以读取
全部公共代码，但不能共同拥有任何文件。机器生成文件、聚合 manifest 和总覆盖
报告只能由主 agent 或生成器写入，不能由 subagent 手工编辑。

主 agent 集成每个工作包时至少检查：

- case ID 在全局唯一，命名稳定且不依赖执行顺序。
- 每个成功 case 只产生自己的 x_t、manifest 和 diagnostics。
- 脚本没有复制 P/Invoke、ABI、session 或公共 corpus host 代码。
- 目标覆盖和前置辅助覆盖在 manifest 中区分。
- 组内运行和全局 `--check` 都能发现漏项、重复项和输出漂移。
- 不可达、许可受限、版本受限与实现失败分别统计。

### 3.7 Parasolid 执行并发策略

subagent 可以并行编写和验证互不相交的代码，但在确认 runtime、许可、callback
和 session 隔离能力前，不得默认并行运行多个真实 Parasolid 生成进程。主 agent
先用两个只读模板 case 做独立进程并发探测；探测结果必须记录 runtime 版本、
进程数、返回码和输出确定性。

并发探测未通过或尚未执行时，聚合器必须串行运行 case-group。探测通过后才可
增加显式的受限并发选项，并保持串行模式作为 CI 和故障复现基线。不得在同一
进程内并行共享 Parasolid session。

### 3.8 当前落地状态

当前仓库已落地并通过真实 runtime 验收的工作包为：

- API/type/token 机器清单及 Markdown 视图。
- Body 基础：11 个成功 case、9 个拒绝合同。
- Topology operations：Face/Edge/Loop/Vertex 的 sheet/solid 合法操作共 15 个成功 case，覆盖 make-sheet、imprint、offset、split、precision 和 transform 分支。
- Topology advanced：Euler ring-loop/ring-face、edge split/make-curve、loop make-edge 及 edge precision 共 8 个成功 case；不稳定的删除/attach 分支保留精确错误审计。
- Manifold topology：through-hole inner loop、双通孔、cylinder winding loop、BCurve wire loop、outer/vertex loop 以及 edge valence 0/2 共 9 个成功 case；legacy hole/peripheral token 保留 needs-recipe。
- Analytic geometry：Line、Circle、Ellipse、Plane、Cylinder、Cone、Sphere、Torus
  及 line/circle/ellipse wire-body、option-bearing/reversed wire、旋转轴/反向轴/最小正半径共 16 个成功 case。
- Assembly：空 Assembly、identity instance、levelized/transformed assembly、instance
  transform/replace/reflection/rotation 共 9 个成功 case。
- Attribute：body owner 的 integer/real/string/vector/axis、coordinate/direction/pointer/ustring、
  face/edge/loop/vertex owner 以及 part/body delete，共 15 个成功 case；命名字段的
  real/string/vector/integer/axis 分支在 User fields 组另有独立 case。
- Group：body-owned face group 的 create/add/label/remove 生命周期及 options 变体共 2 个成功 case。
- Complex geometry：BCurve、BSurf、Swept、Spun、SPCurve、curve-spin、curve-sweep、
  line-to-BCurve approximation（含 options 变体）、legacy SP-curve、BSurf knot
  insertion、BCurve knot insertion/removal 和 approximation metadata（含 array 变体）、
  curve-spin options、BCurve spline/fitted/piecewise/splinewise 和 BSurf piecewise/splinewise
  共 26 个成功 case；
  rational/foreign/无直接 producer 的分支已单独审计。
- Mesh/User fields：facet-body 2 个成功 case，body-only、body/face 及 zero-payload user-field
  往返 3 个成功 case，命名 real/string/vector/integer/axis 5 个成功 case；lattice、indexed-context 和 callback/error
  分支的当前 host/runtime 限制已单独审计。
- Sheet body：circle、rectangle、polygon 及 explicit planar loop 共 4 个成功 case。
- Auxiliary geometry：Point construction、geometry copy/transform、part remove、
  temporary entity delete、single-geometry delete、least-squares plane、parallel/perspective/spun body outline
  共 10 个成功 case；Vector/direction/axis 作为 standard-form 输入覆盖并单独审计无 standalone producer 的情况。
- Helical curves：右手单圈与左手渐变多圈 wire-body 共 2 个成功 case。
- Point/Region bodies：point minimum body 1 个成功 case；solid region make-void/make-solid 2 个成功 case，
  region imprint 分支保留错误审计。
- Mutator：body translation、rotation/equal-scale/composed transform、copy topology、
  manifold decomposition、entity copy/entity-copy-2、option-bearing transform、face transform，以及 wire edge reverse/repackage/delete 和 sheet orientation 共 17 个成功 case；solid body orientation、boolean、
  offset、thicken 和 sweep 分支保留审计入口。
- 项目内核的 primitive x_t writer/reader 往返、cone apex 退化拓扑和 8 个 golden fixture；
  schema semantic dependency checker、类型矩阵输入生成器和 golden hash checker 已接入。
- 全局覆盖审计：`CheckParasolidXtCorpusCoverage.cs` 当前报告 289 个持久
  Producer/Mutator、172 个 covered references、152 个仍未由成功 case 引用的 API 分支，
  其中 171 个分支已有不可达/版本/host 限制审计，`strictGapCount=0`；
  `residual-api-audit.json` 为剩余项保留逐 API 的原因、证据和复查条件。result-free 的 `_r_f`、
  partition、application-item 和 transform helper 已正确降级为 RequiredHelper/ExcludedRuntimeOnly，
  不计入持久状态目标。不可达 API 可以与其他参数分支的 covered API 重叠，审计只针对对应语义分支。

上述数量（当前 327 个成功 case、30 个 case-group）与审计清单共同构成当前
Parasolid runtime 的代表性语料门禁：已登记的可达分支拥有独立 x_t，无法在当前 host/runtime
形成合法目标状态的分支拥有 audit-only 记录，不会被误计为 covered。runtime、license、
header 或 host contract 变化时，先重跑审计生成器并将相应条目回派为成功 case。

当前最终验收命令为：

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidApiCoverage.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidXtCorpus.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidXtCorpusCoverage.cs
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidUnreachableAudit.cs
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidXtCorpusCoverage.cs -- --strict
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidTypeMatrix.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidSchemaDependencies.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidXtGoldenFixtures.cs -- --check
```

前两条命令验证机器清单和所有 case-group；覆盖审计先生成稳定报告，再由
`GenerateParasolidUnreachableAudit.cs` 同步逐 API audit-only 条目。最后一条命令是
最终门禁：要求 `strictGapCount=0`、所有 case metadata 对齐且没有缺失 coverage 文件；
`uncoveredApis` 仍可列出已明确审计的分支，但不会被伪装成 covered。

typed 门禁另外运行：

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidTypeCoverage.cs -- --strict
```

当前 v38 runtime 的 typed report 有 3 个 `needs-recipe` 缺口：
`geometry.blend.pair.sphere-spun`、`topology.loop.hole`、`topology.loop.peripheral`；
此外 `geometry.blend.depth.2` 与 `geometry.icurve.depth.2` 尚无成功嵌套 fixture，
因此 typed strict gap 实际为 5。待提供稳定 seed 或能产生旧版 token 的公共 API 配方后
才能将本轮计划标为完全完成。
当前 17 个 deferred 维度仍在
`type-matrix.json` 中单独跟踪，不能以 API strictGapCount=0 代替。

## 4. API 和类型清单

### 4.1 来源

首版清单以以下内容为权威输入：

- `third_party/parasolid/include/parasolid_kernel.h`。
- `third_party/parasolid/include/parasolid_tokens.h`。
- `third_party/PKToy/PskernelSharp/parasolid.g.cs`。
- `third_party/PKToy/PskernelSharp/Types.g.cs`。
- 可用的对应版本 Parasolid API 文档和真实 runtime。

获得历史版本 header/runtime 后，必须生成版本化 API/type/token 差异，不能
用当前 header 推断历史版本一定具有相同构造语义。

### 4.2 必须提取的定义

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

### 4.3 API 分类

每个枚举到的 API 必须且只能属于以下一类：

- `Producer`：创建会进入 part x_t 的持久实体或数据。
- `Mutator`：修改现有实体的可传输状态。
- `RequiredHelper`：构造 Producer/Mutator 合法输入所必需。
- `ExcludedPureQuery`：只读取状态，不产生持久变化。
- `ExcludedRuntimeOnly`：只影响 session、线程、内存、调试或瞬时算法状态。
- `Unreachable`：根据当前版本、许可和公共 API 无法形成合法目标状态。

禁止存在未分类 API。新增 header 若出现新 API、struct 字段、enum 常量或实体
class，`--check` 必须失败，直到新增覆盖或不可达审计。

## 5. 逐类型覆盖定义

每个类型必须在机器清单中关联具体构造/修改 API、参数分支、最小模型、组合
模型、适用版本和比较方法。

### 5.1 Part 根和 Body 类型

- Part 根：Body、Assembly、Instance。
- Body 类型：solid、sheet、minimum、wire、general、acorn、unspecified、
  empty、compound。

每种 Body 类型至少包含一个独立最小模型和一个与其他拓扑/几何/Attribute
组合的模型。若某类型不能形成合法可 transmit part，必须进入不可达审计。

### 5.2 Topology

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

### 5.3 Point 与辅助几何

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

### 5.4 Curve

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

### 5.5 Surface

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

### 5.6 Spline 数据

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

### 5.7 Assembly 和 Instance

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

### 5.8 Attribute definition 和 Attribute

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

### 5.9 User fields

覆盖：

- 零长度、单字节、多字节和文档允许的边界长度。
- 不同合法 owner。
- 多实体具有独立 payload。
- Callback 成功、主动拒绝、缺失和错误返回。
- Transmit/receive 后 payload、长度、owner 和 callback 调用语义。

只有 callback 成功且 Parasolid transmit/receive 接受的数据可以进入合法语料。

### 5.10 Mesh、lattice、frame 和 indexed context

逐项枚举并覆盖当前公共 API 暴露的持久类型，包括：

- Mesh、Pline。
- MTopol、MFacet、MFin、MVertex、MFin index。
- Lattice、LTopol、LRod、LBall、IJKBox。
- Frame。
- Indexed context 关联的数据。

覆盖实体成员、连接关系、owner、索引、边界、几何和 Attribute。Groups、AppItems
及其他候选类型必须用真实 transmit 实验判断是否进入 part x_t：进入则纳入
持久语料；否则附证据分类为非持久或 runtime-only。

## 6. 参数等价类和边界规则

每个函数参数和 struct 字段递归应用以下规则，并由人工语义注解收窄到该 API
实际合法域。

### 6.1 基础类型

- Logical：`false`、`true`。
- Enum/token：每个文档化合法常量；unknown/invalid 只用于错误合同。
- 整数、数量和索引：合法最小值、典型值、合法上界；零、负数和越界分别按
  参数合同处理。
- Real：合法边界、边界内、典型值、大量级和小量级；NaN、Infinity 和越界
  只作为拒绝测试。
- Length、radius、tolerance、angle 和 parameter 必须分别定义物理合法域，
  不得统一按裸 `double` 处理。

### 6.2 几何值

- Vector/direction：零、非零、单位、非单位、正交、平行和反平行，按 API
  合法域选择。
- Interval：有限、跨零、周期 seam、半无限和无限，仅在 API 合法时生成。
- Transform：identity、translation、rotation、合法组合、反射和缩放；奇异
  或非法类型只验证错误。
- Box/UVBox：点、非零范围、跨零、周期边界和文档化的无界状态。

### 6.3 容器与引用

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

## 7. Case 定义和 Manifest

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

## 8. 不可达项审计

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

## 9. Oracle 流程

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

支持本项目实现的 case 必须额外提供 managed-kernel transmit 回调。host 将其
输出保存为同一 case 目录下的 `managed-model.x_t`，交给真实 Parasolid receive
并用同一基准 body 做结构比较；managed 输出失败时 case 不能标记为完整通过。
公共 managed bridge 由主 agent 维护，case-group 不得复制 transmit/session 代码。

真实 Parasolid 不可用时允许报告 skip，但该 case 不得计为通过。Oracle 脚本
必须输出足以定位 API、case、版本、实体和差异类型的信息。

## 10. Golden 样本和全量语料

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

## 11. 完成条件

本语料计划只有同时满足以下条件才完成：

- 持久状态生产 API 100% 已分类。
- 递归参数类型、struct 字段和 enum 常量 100% 已分类。
- 每个合法可达语义分支至少有一个成功 case。
- 每个边界等价类已有成功语料或明确的错误合同。
- 每个当前 runtime 可达的主要实体类型至少有一个独立最小模型及一个组合模型；
  仅能通过复杂前置条件、不可达或版本/许可受限的实体类型必须有对应 audit-only 记录。
- Assembly、Attribute、user fields、mesh 均有专项成功模型；lattice/indexed context 和 callback
  受 runtime/host 限制的分支有专项 audit-only 模型与复查条件。
- 所有成功 case 均能生成 manifest 和 x_t，并通过真实 Parasolid 自接收。
- 生成器连续两次运行产生相同 case 集和 semantic hash。
- `CheckParasolidXtCorpusCoverage.cs --strict` 通过，`strictGapCount=0`；
  `uncoveredApis` 中的分支必须全部在 `coverage/unreachable/` 下有逐 API 审计，
  因而不把不可达项错误地计为 covered。
- 精选 golden 样本可离线回归，全量语料可由 CI 重建。
- 不可达、许可受限、版本受限和 host-contract 受限项目单独统计，且不计入 covered。
- 目标 1 的 schema 只有在其全部适用 case 通过后才能标记 `Complete`。

## 12. Typed corpus gate 实施说明（当前版本）

本版本将旧的 API residual audit 降级为 provenance 信息，并新增以下共享基础设施：

- `scripts/ParasolidXtSchemaInspector.cs` 复用生产 `XtText`/`XtSchema` 解码器，
  对每个成功 transmit 输出稳定的 `schemaNodes` 和指针依赖边；`BLEND_BOUND`、
  `BLENDED_EDGE`、`SP_CURVE` 等隐式节点只在父 case 中断言，不重复保存 x_t。
- `CorpusCaseSpec`/`CorpusAssemblyCaseSpec` 支持 `typeCoverage`、typed ask、
  schema 节点和依赖合同。semantic hash 同时包含实体计数、schema graph 和
  typed labels，连续生成可直接用 `--check` 检测漂移。
- `tests/ParasolidXtCorpus/coverage/type-matrix.json` 是当前 header/token/schema
  版本的类型矩阵；`required` 项必须有成功 label，或在 `rejected`/`normalized`
  中有精确 runtime 证据。`excluded` 项单独统计，不进入 strict gap。
- `scripts/CheckParasolidTypeCoverage.cs` 只检查 canonical case-group manifests，
  生成 `type-coverage-report.json`，不再把 generic residual audit 当作类型覆盖；
  `deferredLabels` 单独列出计划要求但尚未展开的 pairwise、边界、oracle 和 golden
  fixture 维度。

目前已接入并通过真实 Parasolid 自接收的新增工作包包括：

- `spline-standard-forms`：BCurve/BSurf rational、form、knot、periodic/closed、
  self-intersection、convexity 取值；三维 rational B-spline 使用 `vertex_dim=4` 的
  齐次 `XYZW`（第四分量为 weight，前三个坐标已乘 weight），并以
  `PK_BCURVE_create_splinewise` 覆盖 smooth-seam；
- `surface-dependencies`、`surface-pair-matrix`、`spcurve-variants`：9 类 supporting
  surface × 4 类直接二维 BCurve 的 36 个 SPCurve 配对、23 个解析来源配对，以及
  rational/closed/periodic/seam 变体；
- `icurve-surface-matrix`：12 个成功 ICurve 配对及顺序、sense、seed-vector、finite box、
  周期支持面变体；显式 UV-box 组合在当前 runtime 精确返回 907，解析交线被表示为
  Line/Circle/Ellipse 的组合保留 needs-recipe；
- `blend-matrix`：19 个 face-face blend 配对/选项 case，含 BLENDED_EDGE、BLEND_BOUND、
  INTERSECTION；sphere/spun 和 blend-on-blend 深度 2 的固定 seed 不稳定，notch 无返回
  blend body，均有精确审计；
- `trcurves`：共享 `third_party/PKToy/PskernelSharp/KernelInterface.cs` 中的 KI
  `CRTRCU`，覆盖 Line/Circle/Ellipse/BCurve 的有限、反向、周期 seam 和 full-cycle，共 12 个 case；
- `body-types`、`manifold-topology`：Acorn/minimum/compound、outer/inner/winding/
  vertex/wire loop、edge valence 0/2、shared/isolated vertex，并保留 legacy
  hole/peripheral、inner-sing/valence-1 等精确审计；compound 使用多 part 专用 verifier。
- `attributes`：coordinate、direction、axis、pointer、ustring 以及 body/face/
  edge/loop/vertex owner；entity field 的当前 runtime 拒绝码单独记录；
- `user-fields-mesh`、`user-field-single`、`user-field-eight`、`assembly`：zero/nonzero
  payload、1/4/8 槽 body/face owner、multiple shared instances 和
  translation/reflection/rotation；当前 nested/empty、callback 的限制记录为 rejection/audit。

上述新增组已经把可稳定构造的矩阵单元接入门禁；仍未关闭的 pairwise、callback、
Frame/CPCurve 和运行时限制维度由 `deferredLabels` 单独报告，不能从 API
strictGapCount 推断为计划全部完成。

面向用户的门禁命令为：

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GenerateParasolidXtCorpus.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/CheckParasolidTypeCoverage.cs -- --strict
```

旧的 `CheckParasolidXtCorpusCoverage.cs` 仍保留 API provenance 报告和兼容性统计，
但其 `strictGapCount` 不代表 typed matrix 完成度；最终 typed 门禁以
`type-coverage-report.json` 的 `missingCount` 和精确 rejection/normalization 统计为准。

当前 v38 runtime 的 typed report 有三个 `needs-recipe`：
`geometry.blend.pair.sphere-spun`、`topology.loop.hole`（5401）和
`topology.loop.peripheral`（5402）。通过-hole、周期圆柱和 BCurve imprint 已分别
关闭 `inner`、`winding`、`wire`；这些缺口若没有能产生并可往返的公共 API 配方，必须
保持缺口，不能以审计或手写 XT 节点冒充成功。blend/ICurve 深度 2 同样等待稳定嵌套
seed。收到配方后再把 typed strict gap 收敛到 0。

此外，报告当前有 17 个 `deferredLabels`，并且 `planGapCount=20`（另有 3 个
needs-recipe）；这些 deferred 仍包括未穷尽的 ICurve/Blend/BCurve 组合、Attribute/
User-field/Assembly callback、CPCurve、Frame 和诊断 token。这些项目不会被
family-level strict label 冒充为已完成。
