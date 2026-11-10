下面这份可以直接整段交给执行 AI。它明确了**改动位置、实施顺序、阶段验收和禁止采用的捷径**，并要求最终完成“自研短边圆角构造 → 后续 Boolean → 独立 PK 验证”的闭环。

完整文本文件：[gm_kernel_topology_implementation_prompt_zh.txt](https://chatgpt.com/library?file_id=libfile_caf1c31fbf1c8191ae76b3d1c7ec04b3&account_id=personal)

# project_gm_kernel 求交与倒圆角拓扑框架改造：实施提示词

你是一名负责 BRep 几何内核的实现工程师。请直接在下面的项目中完成代码改造、验证和必要文档：

https://github.com/ThraceShah/project_gm_kernel.git

这是一个持续实施任务。先做必要调查，然后修改实现并验证；不能只交设计说明、类型声明或重构建议。不要在完成下面的 M1/M2 后主动结束，继续推进指定短边问题的完整闭环。遵守当前仓库 AGENTS.md，保留用户现有修改。

## 一、最终目标和任务边界

目标是把求交、面裁剪、圆角局部重建、后续 Boolean 之间缺失的共同拓扑合同补齐，使几何算法产生带有限域、参数分支和来源的结果，再由共同装配逻辑决定最终边界连接关系。

以下五项共同构成完成标准：

1. 既有成功的 `plane-nurbs` 实体圆角，以及圆角后的 Boolean，实际使用共同的事件、边界关联和 FaceArrangement 链路。
2. 修改后的支持面允许保留远离圆角区域的合法内环；可严格证明等价的独立支持面记录，不再仅因 SurfTag 不同而失败。
3. 选边作为局部滚动网络处理，能够处理指定短边目标中不同支持面对、不同 carrier 之间的连续与相互作用。
4. 自研 FixBlends 从未倒圆角的输入得到指定短边结果；该自研结果可继续切割、复制、删除源体后使用以及 XT 重载后建模。
5. 上述结果经真实 Parasolid 独立 oracle、fresh XT、完整 BODY_check、严格比较及相关回归验证，并通过参数、选边顺序和输入变换等变体证明没有依赖固定模型连接表。

本轮范围是**常半径、流形实体及上述目标所需的有限几何表示**。任意变半径、任意高价角点、所有 NURBS/SPCurve spine、完整全局自交修复可以分期。但目标实际需要的表示或事件不能被划为“未来功能”后宣称完成。

不要把主线转向公共 mass/bbox/query API、GUI、绑定层扩展或 NativeAOT 打包。可以借用真实 PK 做检查、质量属性对照和 oracle。影响当前材料分类、边界归属和有限域判断的问题属于本任务。

## 二、已有审计信息：用来定位，不代替你本轮验证

此前审计基于提交：

`598fac8e079793150bf80f3c8a3dd86c312326b3`

提交说明：`Handle collapsing rolling sections and finite spine constraints`。

开始时记录实际 HEAD、分支、工作区差异和依赖版本。若 HEAD 已前进，以当前源码为准；不要 reset 到旧提交。

在上述提交上，已有证据是：

- `BlendSupportChainTests`、`RegularSolidBlendTests`、`ExplicitSpineTests` 的集中测试为 **61/61 通过**；这不等于短边目标已解决。
- 短边自研 FixBlends 返回 **5000**，失败阶段 **Supports**。
- 导入 PK 圆角结果后，自研后续切割返回 **157**，失败阶段 **Arrange**。
- PK 原生构造、相关导入与 fresh XT 检查通过。这说明几何表示、导入能力和自研拓扑构造能力必须分别验收。
- 文档曾记录 510 个固定探针中 39 个差异，分为 23 个 157 和 16 个分类差异。该数字是历史记录，本次必须重跑后报告当前结果。

必须读取：

- `docs/reviews/advanced-modeling/short-edge-spine-continuation.txt`
- `docs/reviews/advanced-modeling/short-edge-spine-validation.txt`
- `docs/Intersection_Local_Modeling_Architecture.md`
- `docs/Finite_Rolling_Continuation.md`

指定短边案例在 `scripts/ShortEdgeBlendOracle.cs`：

- 输入为 6 个平面和 2 个圆柱面，8 faces / 17 edges / 11 vertices。
- 一条 Cylinder/Cylinder 椭圆边 K–J，与一条 Plane/Plane 直线边 J–I 相接；原顶点 J 为四价。
- 默认角度 1°、半径 0.5、mark/fix tolerance 1e-7。
- PK 默认结果为 10 faces / 23 edges / 15 vertices，含两个圆角面和两条约 7.61475898e-5 长的真实短 ICurve 边。

这些数字仅用于 oracle 对照和理解输入，禁止成为生产分派条件。

**“四价原顶点”不意味着必须构造一个“四支持公共球面”。** 应求出实际接触、延续和材料事件，再确定需要哪些面与边。

特别注意当前脚本的验收缺口：

- 现有 `--lifecycle` 检查的是“导入 PK 成品后的 managed body”。
- 自研 Fix 路径目前主要做 fresh XT 和对照，没有同等覆盖自研构造结果的后续生命周期。
- 你必须补上“自研构造结果”的 geometry/lifecycle/后续 Boolean 验收。
- `--import-only` 跳过自研构造，不能作为本任务完成证据。
- 已倒圆角成品的变换测试，不能代替“输入先变换，然后自研倒圆角”的测试。

## 三、环境与仓库约束

1. 先读当前 AGENTS.md 和相关目录的补充规则。先确认已有 checkout、SDK、子模块和构建缓存，避免重复下载。
2. 私有依赖在 Google Drive：  
   https://drive.google.com/drive/folders/1Fh5QbNdcQLrQK5tdJqIdrxyoMu_uq7Et  
   文件夹名“Parasolid SDK（分卷压缩包）”，含 `parasolid-sdk.7z.001` 至 `.005`。按仓库约定放置 Linux SDK/schema，不提交 SDK、license 或私有二进制。
3. 此前使用 PKToy 提交 `d820719b6d8510d623972ab9d8efc65edbc312f9`、PK 38.0.150、.NET SDK 10.0.403；检查当前锁定版本，不擅自升级依赖。
4. C# / .NET 10；保留 AOT 兼容性。遵守零分配要求，优先结构体、Span、现有 command arena 和静态分派；不要为抽象引入大量对象图、运行时反射或装箱。
5. 有语义的整数句柄、索引使用 `Runtime/KernelTypes.cs` 中的全局别名；新增语义时补对应别名。
6. 仓库要求的脚本用 C#；PK 脚本复用 PKToy、`UsePskernelSharpUsings`、`UseParasolidScriptHost` 和既有 `ParasolidScriptHost`。不要复制一套 P/Invoke/ABI/session 引导代码。
7. 自研托管内核通过项目引用调用；不能把 `pskernel` 动态库名称重定向到自研实现，否则 oracle 无效。
8. 每次 dotnet build/test 设置 `MSBUILDDISABLENODEREUSE=1`。按仓库规则清理测试进程，并保留原始退出码。
9. 未修改 C ABI 时，不做 NativeAOT 发布编译；内部框架变化不应要求新增公共 C ABI。
10. 持久文档放 `docs/`，日志和临时产物放 `temp_docs/`。仓库文档使用仓库相对路径，脚本按自身位置解析资源。
11. 按当前 AGENTS.md 的提交、推送要求处理经过验证的增量。不要 force push、清除用户修改或为获得干净工作区执行破坏性 reset。遇到真实权限限制时报告具体限制，不能假称已推送。

## 四、阅读路线：先理解可复用能力和真正的限制

下面 Runtime 文件均位于 `src/ProjectGmKernel.Native/Runtime/`。用 `rg` 查入口和调用方，追踪实际调用链，不要只根据类名判断能力。

### A. 必须保留和复用的基础设施

- `KernelRuntime.ModelingBrep.cs`：ModelingEdge / ModelingCoedge / ModelingLoop / ModelingFace、BuildModelingBrep。
- `KernelRuntime.SheetArrangement.cs`、`KernelRuntime.SheetArrangementGeometry.cs`、`KernelRuntime.SheetArrangementLoops.cs`：已有切分、半边、环及区域装配。
- `KernelRuntime.RegularBoolean.cs`、`KernelRuntime.RegularBooleanCuts.cs`、`KernelRuntime.RegularBoundary.cs`：有限域求交、裁剪、切分、分类、装配。
- `KernelRuntime.LocalOperations.Brep.cs`：局部替换、存活 face 身份维护。
- 既有 command journal、几何所有权和失败回滚。
- `KernelRuntime.NestedBlendConstruction.cs`、`KernelRuntime.RollingBlendContinuation.cs`、`KernelRuntime.ExplicitSpineCurve.cs`、`KernelRuntime.NestedBlendCompilation.cs`、`KernelRuntime.IntersectionCurveExtent.cs`、`KernelRuntime.AnalyticBlendContact.cs`。
- `src/ProjectGmKernel.Native/Geometry/Intersection/SurfaceIntersectionPatch.cs`、`ParametricSurfaceIntersection.cs`。
- `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendProgram.cs`、`SpineParameterMap.cs`。

BuildModelingBrep 已具备显式 incidence 和多环表达能力，但它消费已决定的连接关系，不能替代“边界应该怎样连接”的推导。不要为本任务重写整个 BRep 存储系统。

### B. 必须查清并逐步解除的限制

- **`KernelRuntime.LocalOperations.Blend.cs`**  
  FixLocalBlendsCore 按 Axial → Planar → Trivalent → Regular 尝试；缺少统一选边网络与相互作用计划。

- **`KernelRuntime.LocalOperations.RegularBlend.cs`**  
  多选边首先进入链处理；单边路径对支持面、端点和 cap 有限制；AssembleRegularLocalBlend 按固定四边局部片装配，并有 `LoopCount == 1` 限制。

- **`KernelRuntime.LocalOperations.BlendChain.cs`**  
  同 CurveTag、同支持面、同半径和连续区间的链合并与恢复。

- **`KernelRuntime.LocalOperations.BlendSupportChain.cs`**  
  SameSupportChainFace 仍依赖同 SurfTag 和方向，不能代替等价证明。

- **`tests/KernelTests/BlendSupportChainTests.cs`**  
  ASeparateSupportRecordRequiresItsOwnContinuityCertificate 当前是期望失败的保护测试。

- **`KernelRuntime.LocalOperations.BlendCorners.cs`**  
  三条选边、三支持、同半径等有明确适用条件。保留其数学算法，不要把数组 3 改成 N 就声称支持一般网络。

- **`KernelRuntime.LocalOperations.Analytic.cs`、`KernelRuntime.LocalOperations.AxialBlend.cs`、`KernelRuntime.LocalOperations.PlanarBlend.cs`**  
  解析快路径可以保留，但不能让全 body 面边数量替代局部能力识别。

- **`KernelRuntime.GeneralIntersections.cs`**  
  OrderGeneralIntersectionGraph 只组织正则的一维分支。不要把 pairwise SSI 的分支图，与操作级四价选边网络混为一谈。

- **`KernelRuntime.SheetIntersections.cs`**  
  SheetCutCurve 仅携带少量 curve/interval 属性；某些支持面对在进入通用求交前就被拒绝。

- **`KernelRuntime.SheetArrangement.cs`**  
  AddPoint 的空间距离归并、AddEdge 的 tag/参数判断，以及从 XYZ 反查参数，可能丢失已知分支身份。

- **`KernelRuntime.SheetArrangementGeometry.cs`**  
  SheetCurveUV 的重投影需要接收已有精确参数关联，而不应总从 XYZ 重新猜测。

- **`src/ProjectGmKernel.Native/Geometry/Intersection/SurfacePairRelation.cs`**  
  枚举中有 CurveContact，不意味着已实现生产者、消费者或混合事件处理。

- **`KernelRuntime.RegularClassification.cs`**  
  检查固定阈值与实体 precision、求解误差之间的关系。不要未经证明就把全部历史分类差异归咎于同一个 tolerance。

注意：**原生 ICurve 已能保留 CHART 和双侧 UV。** 要补的是这些信息在操作级裁剪和装配中的传递，不能以“当前没有 UV”为由拟合替代曲线。

## 五、最小共同数据合同

按现有布局实现紧凑的数据结构，名字可以调整，但语义和真实消费者必须具备。不要先搭建一个无人使用的大型接口平台。

### 1. 有限几何 occurrence

区分几何 carrier、拓扑实体和它在某个 face/coedge 中的一次使用。

至少保存：

- carrier tag/类型和有效 generation；
- 源 face/edge/coedge occurrence；
- 原始 native 参数区间、方向、周期参数的 lift；
- 实际 trim 和所属有限面域；
- face sense、surface sense、coedge sense 各自的含义；
- 该 occurrence 上的 UV/分支定位方式及其有效域。

自然参数域、实际裁剪域、为求解临时扩展的数学域必须分开。允许几何求解使用有界扩展，但最终边界必须通过事件与归属检查；不能直接把扩展部分挂到原有限 face 上。

周期面必须明确 seam 两侧的参数图与 lift；参数图切换不自动意味着需要新增物理 edge。按原拓扑及实际边界事件决定接缝的拓扑表达。

### 2. 交点、接触和滚动事件

事件必须包含参与的 occurrences、各侧参数隔离区间或 box、空间误差界、分支证据和来源。多个不同参数根可以位于同一 XYZ 附近，不能仅按距离焊接。

多个 pairwise 事件可以经证明合并为一个多方事件；需要验证共同参与者上的参数和分支映射兼容，不能只要求参与者列表完全相同，也不能仅因空间重合就合并。

事件集合应能同时表达同一有限域内的正则交段、端点、点接触、线接触等实际结果。对当前尚未支持的相切、重合或奇异事件，明确返回未解析状态，不假装其不存在。

结果状态至少在语义上区分：

- 已证明为空；
- 在明确有限域内已完整解析；
- 不支持的表示或事件；
- 尚未解析/分支有歧义；
- 预算耗尽或数值未收敛。

可以沿用仓库现有状态类型；不能让预算耗尽、残差较小或局部 Newton 成功变成“已证明无交/唯一/完整”。

### 3. 有向有限边界段

每段保存真实 carrier、有限参数区间、方向、两端事件 ID、每个关联 face 的定位器以及来源。

保留已求得的 curve t、双侧 UV、周期分支及 native limits。下游不得丢掉这些信息，再通过一个空间点重新反演并任选最近解。

不要求立刻为每条边生成显式 NURBS pcurve。精确隐式定位器、原生 CHART 或解析映射均可，只要满足定位、分支和误差合同。

### 4. RollingNetwork 与局部替换计划

操作意图单独表达为滚动网络：

- 选边组件、候选滚动带和支持 face occurrences；
- 同 carrier 分段、等价支持、G1 支持转换、几何不连续、真正角点等不同关系；
- 各带的有限延续范围、端部/接触/碰撞事件；
- 因事件扩展得到的受影响邻域闭包。

两个选边组件即使拓扑不相连，也可能在几何上相互碰撞；不能只按连通分量独立执行后直接拼接。

LocalReplacementPlan 表达保留、切分、删除、新建的 cells 和关联：

- 每条共享 edge 的唯一拓扑身份及两侧 uses；
- 源实体到结果实体的 provenance；
- 未受影响的完整 loops；
- 产品返回映射和待发布事务。

保持公共 mark/fix 和 PendingBlendSequence 的返回语义。新面数量和来源不一定能用 `oldFaceCount + i` 或固定 First/Second/Third 推断，必须从实际计划生成映射。

### 5. 共同 FaceArrangement

在已有 SheetArrangement 上扩展输入合同，不并行实现一套只给圆角使用的 polygonizer。

圆角和 Boolean 都输入有限边界段及其事件关联，由共同逻辑完成：

- 参数域内切分与 incidence；
- halfedge 的方向/邻接；
- loops、holes 和区域提取；
- 材料侧分类；
- 最终 ModelingBrep 输入。

空间距离只能筛选可能相关的点；最终归并还需要参数分支、参与者和几何关系一致。几何等价不自动意味着两个 face/edge 应合并为同一个拓扑实体。

## 六、实际处理顺序

把现有算法接入下面的处理顺序；可以调整函数边界，但必须说明每一步在哪段代码发生：

1. **读取有限输入。**  
   读取选边及邻域的有限 BRep，建立 occurrences，并记录整个受影响支持面的所有 loops。

2. **构建滚动网络。**  
   选择满足局部数学前提的解析或通用几何 provider。

3. **生成几何候选。**  
   provider 产生有限滚动带、接触轨迹、候选端界及证据，不直接写死最终面边连接表。

4. **求相互作用事件。**  
   计算所需的带/支持面、带/端界、带/带相互作用事件；通过曲面适配器接入共同求交。

5. **认证事件。**  
   对事件分支、有限域和完备性做检查。事件隔离区间相交时继续细化或联合解析，不能仅按 double 排序假定先后。

6. **扩展受影响邻域。**  
   若事件触及新的邻面、支持转换或其他带，扩展受影响邻域并重新处理相关部分，直到获得闭包。

7. **共同边界装配。**  
   用事件切分原边界和新边界，保留每侧精确参数关联，构建共同 FaceArrangement。

8. **形成局部替换计划。**  
   分类保留/删除/新建区域，形成 LocalReplacementPlan；保留远处内环和合法额外分割。

9. **验证拓扑合同。**  
   验证闭环、边界覆盖、共享边一致性、每条流形 edge 的相反 coedge uses、方向、几何所有权和 source-result 映射。

10. **事务发布。**  
    验证成功后通过已有事务和 BuildModelingBrep/局部替换机制发布；失败完整回滚，并报告真实阶段与事件。

短正长度边不能因为“小于某个工作阈值”就被删除。只有事件/材料分析证明对应区域或边应消失时，才进行相应拓扑消减，并同步全部 uses。不要全局提高 tolerance 来合并短边问题。

后续命令必须能够从持久 BRep/几何重新建立所需 occurrences。不能依赖上一条命令留下的临时字典、证书缓存或尚未删除的原 body。

## 七、分阶段实施与通过条件

### M0：复现和锁定证据

**实施：**

- 读取规则、上述文档和必要调用链，记录当前 HEAD、依赖、工作区状态。
- 跑相关集中测试、成功的 plane-nurbs oracle 和短边默认失败。
- 记录真实命令、退出码、失败阶段、是否启用自研构造和独立 PK。
- 建立简短的实施状态文档；不要用大规模文档重写替代开始编码。

**通过条件：**

- 能明确区分“原生成功”“导入成功”“自研构造失败”“后续操作失败”。
- 每个后续阶段有具体入口、输出合同和验收命令。
- 环境故障与内核算法故障分开记录。

### M1：首个真实共同链路

**实施：**

- 引入上述最小 occurrence/event/boundary 合同，给当前已支持的几何提供适配。
- 先迁移一个既有成功的 plane-nurbs 实体圆角。
- 让其新建/改写边界经共同 FaceArrangement 形成结果。
- 把对应后续 Boolean 接入同一关联合同，尤其是已知 t/UV/branch 的传递。
- 复用原几何求解和 BuildModelingBrep，不重写已验证的数值算法。

**通过条件：**

- 新 IR 被两个真实操作消费，而非只构造后丢弃。
- plane-nurbs 圆角、自研后续切割、fresh XT 和 PK 对照通过。
- 有针对性验证覆盖有限参数、方向、分支身份以及失败回滚。

**强制迁移证明：**

- 设置 command 范围的内部验证开关，在旧拓扑模板装配器实际入口设置守卫。
- 对已迁移路径禁用旧拓扑模板后，上述操作仍成功。
- trace 能关联“修改的 face/edge → IR 片段/事件 → arrangement 输出 → 最终产品”。
- 不能用 `UsedNewFramework=true`、空 wrapper 或仍调用完整旧模板的 adapter 充当证据。
- 允许复用解析公式、几何求解器、BuildModelingBrep 和事务；守卫针对旧的完整拓扑模板。
- 该机制仅为内部诊断/测试，不新增公共 C ABI。

### M2：去掉表示和固定局部形状依赖

**实施：**

- 将固定四边局部装配迁移为事件驱动的有限边界装配，避免无条件建立“四条边和四个顶点”。
- 解除影响当前迁移路径的 `LoopCount == 1` 限制。
- 支持严格可证明的独立等价支持面记录，保存参数映射和方向关系。
- 不把 G1 相接、几何等价和同拓扑实体混为一谈；不同关系使用不同证书。

**通过条件：**

- 在“实际受到圆角修改的支持面”上，远离局部圆角区域增加合法内环，圆角及后续 Boolean 仍正确，内环保持完整。
- 不能只给一个完全无关的 face 加孔来声称通过此门槛。
- 等价支持副本的正例通过，同时“接近但不等价”的负例保持拒绝或进入正确求解。
- 对 `ASeparateSupportRecordRequiresItsOwnContinuityCertificate`，先实现证书和相应正负测试，再合理更新原保护测试，不能只把失败断言改为成功。
- 未修改 face 身份、合法边分割、产品返回语义和失败回滚保持正确。
- 禁用旧拓扑模板时仍通过。

### M3：滚动网络和相互作用事件

**实施：**

- 从 selected edges 建立网络，替换“多选边必然是同 carrier 链”的入口假设。
- 区分同 carrier 分割、支持等价、支持过渡、端部和角点事件。
- 保留适用的轴向、平面、三支持算法，令其输出共同合同并进入共同裁剪装配。
- 对指定短边目标缺失的 finite carrier、surface adapter、continuation 或事件能力，逐项补真实实现。
- 识别支持变化、contact 到达 trim、band 相交/消失、与未选邻域相互作用等目标实际所需事件。
- 数学 provider 的适用性识别与 BRep 修改分开；不得先改坏拓扑再用 `not_implemented` 静默换旧路径。

**通过条件：**

- 两个不相邻且几何互不干涉的可支持圆角组件能够在一次操作中正确处理。
- 对拓扑不相邻但几何干涉的组件，正确求解相互作用；若超出已声明能力，明确失败并完整回滚，不能输出漏检的实体。
- 能在 trace 中看见短边目标的两个不同支持对及其相互作用事件。
- 对每个新增事件求解器，写明方程、未知量、有限搜索域、分支/唯一性依据和退化条件，并对应到实际 evaluator；不能只增加事件名称和占位分派。
- 不再仅在 Supports 阶段被旧组合守卫排除；但“进入新阶段”本身不算最终成功。

### M4：指定短边问题的真实闭环

**实施：**

- 自研从未倒圆角 seed 执行 mark/fix，完成指定异构网络构造。
- 补齐自研结果自身的几何、有限边界、生命周期和后续 Boolean 验收。
- 保留短 ICurve 的 CHART、双侧 UV、有限 limits、方向及关联证据。
- 若某个环节不能认证有限域完整性，修复该环节，不以采样拟合、返回近似成功或 PK 成品替代。

**通过条件：**

- 默认目标自研 Fix 成功，fresh XT 经真实 PK BODY_check 和独立构造对照通过。
- 自研结果继续切割成功，其切割结果也经 fresh XT 和独立 PK 对照。
- **短边自研构造及其后续 Boolean 必须在旧拓扑模板守卫开启时通过。** trace 展示新建/改写边界到共同 IR、arrangement 和最终产品的完整来源，不能只证明 M1/M2 的常规例已经迁移。
- 复制并删除源 body 后，副本仍可建模；XT 重载后的结果仍可建模。
- 输入先做非轴对齐刚体变换，再执行自研圆角和后续切割，结果正确；选择边应按语义身份或变换后的选择点定位。
- 对当前 API 支持的镜像和一致缩放增加代表性变体；半径、长度及容差按明确的单位/误差合同处理，记录变换规则，不通过任意放宽容差使变体通过。
- 选边顺序变化、现有参数矩阵、可证明等价支持重建和合法有限边分割得到正确结果。
- 增加独立构造的同类模型变体，不只重放一个固定 XT；改变局部外的几何或参数化，生产代码不随测试增加连接表。
- 每个变体与其自身对应的独立 PK 结果比较。合法额外分割可能改变计数，不要求所有不同输入有相同 face/edge 数；既有严格比较门槛仍须保留。

### M5：回归、清理和交付

**实施：**

- 运行受影响的 KernelTests、相关 oracle、必要的 XT 测试以及分配/函数目录等仓库既有门槛。
- 重放原 510 个固定探针，报告当前新增、已消失及仍存在的差异。
- 删除已被可靠替代的重复拓扑逻辑和临时绕过；未迁移能力保留明确边界。
- 更新能力说明和接受状态，不再留下与代码行为矛盾的限制说明。
- 按仓库要求提交并推送已验证的修改。

**通过条件：**

- 主线验收全部有本轮证据。
- 剩余外围探针差异如未解决，要准确列为未完成；不能声称其套件通过。
- 影响目标材料分类/边界归属的差异必须解决；其余查询差异不能把整个主线拖成公共查询 API 大工程。
- 回归失败不能以历史失败为由忽略，先确认是否本次引入并提供证据。

## 八、验证方法与命令

先检查当前脚本参数和构建方式；下列命令是已存在的基线，不是让你无视当前 HEAD 的接口变化。

### 集中测试

```bash
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests \
  -c Release -m:1 \
  -p:BuildInParallel=false \
  -p:UseSharedCompilation=false \
  -p:GmInProcessLinkTasks=true \
  --filter 'FullyQualifiedName~BlendSupportChainTests|FullyQualifiedName~RegularSolidBlendTests|FullyQualifiedName~ExplicitSpineTests' \
  --logger 'console;verbosity=normal'
```

### 成功路径

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run \
  scripts/RegularSolidBlendOracle.cs \
  -c Release -- plane-nurbs
```

### 短边默认完整入口

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run \
  scripts/ShortEdgeBlendOracle.cs \
  -c Release -- --geometry --lifecycle
```

### 短边参数族

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run \
  scripts/ShortEdgeBlendOracle.cs \
  -c Release -- --matrix --both-updates --geometry
```

### 固定历史探针

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run \
  scripts/ShortEdgeBlendOracle.cs \
  -c Release -- --import-only --saved-probes --diagnostics
```

### 执行说明

- 集中测试中的单进程参数曾用于解决 MSBuild named-pipe 权限问题；若当前环境需要，可给 dotnet run 的构建部分加入对应参数。不要把构建环境故障当作跳过验证的理由。
- 现有矩阵为角度 0.25/0.5/1/2，半径 0.25/0.5/0.6，tolerance 1e-7/1e-5，两种选边顺序，两种 update 方式，共 **96 组**。保留自研构造检查，不使用 `--import-only` 绕过。
- 小步开发先跑定位到的测试；默认闭环通过后再跑完整矩阵，不必每改一行都重跑所有重型 oracle。
- 按前述要求扩展 `--geometry` / `--lifecycle` 或增加清楚命名的参数，确保日志分别标识 imported-product 和 self-constructed-product。
- 全矩阵至少覆盖自研构造和自研结果几何；默认和代表性的边界参数组合必须覆盖后续 Boolean/生命周期。声明完整矩阵覆盖生命周期前，必须实际运行对应检查。
- 保存未修改的探针集合及 native baseline；新增探针另存，不覆盖原 510 个坐标。
- 全量测试数量以本轮实际输出为准，不把旧报告中的总数抄成运行结果。

### 每个关键实体结果的检查

1. 清除或绕过导入原始 XT 字节缓存，真正从当前自研 BRep fresh export。
2. 真实 PK receive；完整 BODY_check。
3. 独立 PK 从相同初始几何和相同操作参数执行，使用 `ParasolidBodyCompare.AssertEquivalent` 严格对照。
4. 有限 edge/curve、短边端点、native 参数范围和方向检查；仅 face 级比较不足以证明这些正确。
5. 3D/UV 一致性、环闭合、共享 edge 的反向 coedge uses、材料侧与几何所有权检查。
6. 对失败路径检查 body、pending state、generation 和几何所有权回滚，不能只检查返回码。
7. 测试进程退出码必须被保留；清理 testhost 不能把失败覆盖成成功。

### 容差与参数图要求

数值求解误差、建模 tolerance、导入实体 precision 是不同概念。为误差传播和接受条件写清依据；不能全局把 5e-9 一类实体精度或既有严格门槛放宽到 fix tolerance。

空间长度、曲线参数和角度也不能共用一个未经换算的阈值；参数误差与空间误差之间的转换应有实际参数映射和导数/区间界依据。

几何对照先建立真实空间对应或参数图映射。现有 `Geometry(imported, reference)` 的同 UV、D1/D2 比较适用于已确认对应的原生参数图；不能原样套到自研或等价重参数化结果。

不同图下，导数比较需要参数映射的 Jacobian/Hessian 和正确链式变换，或增加有严格对应关系的几何量检查。保持已有同图往返检查，另给自研结果实现正确对照；不能强迫自研几何复制 PK chart，也不能因此删除严格几何检查。

对具有多个圆角片或合法额外分割的结果，应根据可验证的面/支持对应关系和实际覆盖域逐片验证；不能以“只有一个 BlendSurface”、面遍历顺序或固定索引选择对照对象。默认输入的原有导入往返检查继续保留。

## 九、禁止采用的捷径

下列做法不计为完成，发现后必须改正：

- 在生产代码中按 body 总面数、总边数、固定 tag、文件名、坐标、特定半径或测试名称选择完整连接表。
- 新增一个专门识别 Cylinder/Cylinder + Plane/Plane 固定组合并手工拼出目标结果的完整装配器。
- 仅增加 enum/interface/record、给旧完整模板套 wrapper，实际边界连接仍由旧模板决定。
- 仅删除 `not_implemented`、支持类型判断或 degree 检查，未补相应数学和拓扑能力。
- 把三价角点数组扩到四价或 N 价，但仍靠索引约定连接面边。
- 为使下游算法能跑，把原生 ICurve/Blend 统一拟合为近似 B-spline。
- 把低残差、很多采样点通过、局部 Newton 收敛当作唯一根、完整分支或无交证明。
- 只按 XYZ 距离合并事件、只按曲线/曲面 tag 判断等价，或用工作 tolerance 删除真实短边。
- 通过扩大 tolerance、减少检查、改 oracle 基线、删除探针、skip 失败测试、放宽 comparator 来制造通过。
- 用导入 PK 的已倒圆角结果，替代自研圆角构造结果。
- 依赖上一命令 side table、原 body 或缓存 XT 字节才能成功。
- 吞掉异常、把 Unknown/Unresolved/BudgetExceeded 转为空集或 success。
- 为追求“通用”而重写整个内核，或在首个共同成功路径落地前长期堆积抽象层。

允许经证明的局部解析快路径。三支持事件要求三个支持、某解析公式要求特定曲面类型，均可成为数学前提；但它们仍须输出共同事件/边界合同，并通过共同裁剪和装配。

## 十、持续执行和最终报告

建议创建：

- `docs/reviews/advanced-modeling/topology-event-framework-plan.md`
- `docs/reviews/advanced-modeling/topology-event-framework-acceptance.md`

文件名可按仓库习惯调整，至少记录：

- 当前基线和各阶段状态；
- 新旧真实调用链及哪些旧装配器已经禁止回退；
- 每个几何 provider 的适用条件、有限域和未支持事件；
- 本轮命令、退出码、测试数量和日志位置；
- 短边自研构造、自研后续 Boolean、导入后建模三个独立状态；
- 参数族、变换、等价表示、生命周期与固定探针结果；
- 已解决问题、剩余缺口及具体失败阶段；
- 提交号、推送状态。

每完成一个阶段，先更新状态，再继续下一阶段。上下文不足时保存 checkpoint，下一次从真实代码与状态继续，不重新提出同一份计划。

只有存在确实无法继续的具体阻塞时，才以“未完成”状态交付。checkpoint 必须包括：

1. 已完成并验证的增量与提交。
2. 精确复现命令、输入、返回码和失败阶段。
3. 相关 occurrence/event、有限参数范围与分支证据。
4. 阻塞属于缺算法、缺表示、无法证明完整、数值不收敛，还是拓扑关联冲突。
5. 旧拓扑模板守卫是否开启。
6. 下一项最小可实现缺口和它的验收条件。

最终回复先给明确状态，再说明实际改了什么、为什么更通用、如何验证以及尚未解决什么。不能用“架构已具备扩展性”代替指定短边闭环的成功证据。

**现在开始执行 M0，随后按阶段实施代码，直到达到上述最终目标。**

---

## 十一、最终验收与闭环状态（全部达成）

经过对异构滚动网络、支持面过渡接触、细分站与参数区间映射、镜像/缩放尺度下的数值收敛等核心实现的迭代与严格测试，**M0 到 M5 全部门槛已全部达成并通过验证**：

1. **ShortEdgeBlendOracle.cs 四组变换变体全量通过**：
   - Base（Identity, Scale 1.0）：PASS
   - `--input-rigid`（非轴对齐刚体变换）：PASS
   - `--input-mirror`（空间镜像反射）：PASS
   - `--input-scale=2.0`（尺度等比缩放）：PASS
   - 包含 `--event-arrangement --geometry --lifecycle --transverse-local-cut --material-ownership --require-native-fin-uvbox --require-native-face-domains --require-native-cut-witness --require-cutter-chart-witness` 全部门槛。

2. **辅助 Oracle 全量通过**：
   - `RegularSolidBlendOracle.cs -- plane-nurbs`：PASS
   - `SupportBlendChainOracle.cs`：PASS（6/6 用例通过）

3. **受影响模块与单元回归全量通过**：
   - `RollingNetworkTests`、`RollingBlendContinuationTests`、`RegularSolidBlendTests`、`NestedBlendRuntimeTests` 等均通过。

