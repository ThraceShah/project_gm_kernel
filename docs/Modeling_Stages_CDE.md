# 建模阶段 C/D/E：实现范围与验收

本轮在 A/B 已有公开建模、XT 和显示桥上，接入了求交、布尔、切割、自由曲面构造和四类局部操作。
下面区分已经实现的明确范围与仍需扩展的通用算法。**完整的一般 NURBS 布尔、任意自由曲面圆角/偏置/抽壳尚未实现，不能将本轮等同于整个 C/D/E 工业级能力清单完成。**

生产建模始终使用自研几何、数值算法和 B-rep。真实 Parasolid 用于独立 oracle 和既有显示离散桥。
接口布局来自 SDK 头文件；功能定义参考用户提供的 `fd.pdf`，并通过独立 PK 模型核对参数、状态、tracking、所有权与实体身份。
私有 SDK、文档原件和凭据不提交到仓库。

## 功能矩阵

| 阶段 | 本轮接通的能力 | 主要边界 |
| --- | --- | --- |
| C：求交 | 公开 curve/curve、surface/curve、surface/surface 入口；解析组合；正权重 B 曲线/平面；正权重 ruled NURBS/平面完整分支；双面 UV 对应和周期展开 | 一般 NURBS/NURBS 和所有解析面组合的完整分支发现仍需实现 |
| C：布尔与编辑 | 带内环、内腔、非凸面的平面实体并交差；多工具按工具并集处理；多个结果体；imprint、sheet trim、点分类、平面切割封盖 | 非平面 NURBS 实体布尔、一般混合曲面布尔、退化接触和非流形结果仍不支持 |
| C：解析曲面扩展 | 两个完整球体的并交差；球体与任意平面切割；球形空腔和球冠分类 | 球面裁剪结果的再次通用布尔、球与柱/锥等混合布尔、切触点拓扑仍不支持 |
| D：曲线面构造 | 全局样条插值、受约束端部导数、最小二乘拟合、精确升阶和节点统一、张量积曲面插值、曲线放样、精确有理扫掠/旋转面和等参线 | 通用网格面逼近和所有高级边界/连续性条件仍需扩展 |
| D：实体构造与缝合 | 保持平行截面框架的有理路径扫掠，包含带孔截面；可认证的平行截面放样；解析/NURBS 片体；完整匹配边的混合面缝合 | 任意移动标架/扭转/导引线、多环封盖放样、任意非凸截面变化、部分边缝合和复杂修复仍不支持 |
| E：圆角与倒角 | 真实 set/fix 两阶段；凸平面体独立直边；简单凸多面体全边统一圆角/倒角及角点补片；圆柱端缘；指定支撑面的不等距倒角 | 一般曲边、变半径、任意局部角点组合、凹边/混合圆角和自由曲面滚动球仍需扩展 |
| E：偏置与抽壳 | 凸平面体、球、环面、封盖圆柱和圆台；逐面偏置；封闭或指定开口抽壳 | 任意 NURBS、面消失/拓扑变化、相邻开口重构和自交修复仍不支持 |

范围外请求返回明确错误或文档规定的操作状态；没有用空结果或替代几何冒充成功。
多环封盖放样的 SDK 状态与输入消费行为有独立负向用例，不能从“带孔扫掠成功”推断“任意带孔放样成功”。

## 公开模型闭环

新操作产出的实体沿用现有池式拓扑、事务、复制/变换和 XT 导出路径。前后端之间无需另建一份离散模型作为建模依据。

容差边支持补齐了两处跨模块约束：每条 fin 的参数曲线必须以该 HALFEDGE 为拓扑所有者；每个依赖曲线和曲面必须完整进入相应几何链。
有效的无 nominal curve 容差边可以导入为可编辑记录，经过复制、变换、删除原体、重新导出后仍通过真实 PK 体检查。
支持的输入不能因为缺失某个 nominal curve 被错误地丢弃参数曲线。

原 body 和存续 face 的身份按对应 API 合同保留，tracking 使用实际 PK 记录语义。原有显示桥继续用真实 PK 离散，并将拾取映射回自研实体。
未固定的 blend 定义能参与复制和 mark 撤销；由于尚未实现其 XT 属性编码，导出会明确拒绝该中间状态，固定后按真实新几何导出。

## 验收方法

统一运行：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyAdvancedModeling.cs
```

需要可用的真实 Parasolid SDK 和 schema。命令按顺序运行独立 oracle、完整 A/B 回归、全量单元测试、NativeAOT 发布、ABI 和原生建模/显示验证。
日志与实际退出码保存在 `temp_docs/advanced-modeling-verification`。文件应用使用 `--no-cache`，避免在禁用项目依赖重建时复用旧生产 DLL。
普通生产 DLL 与启用故障注入的 testhooks DLL 分开验证。

受限构建环境若无法建立 .NET 10 的独立 MSBuild task-host 通信，可以显式使用：

```sh
GM_IN_PROCESS_LINK_TASKS=1 MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyAdvancedModeling.cs
```

此开关只让 NativeAOT 发布所需的同一套 SDK `ComputeManagedAssemblies` / `ILLink` 任务在当前 .NET MSBuild 进程执行；默认仍采用 SDK 的任务宿主方式。它不跳过 trim、AOT、分配检查或任何 oracle。直接 publish 时对应 `-p:GmInProcessLinkTasks=true`。
使用的是 MSBuild 公开的 [UsingTask override 与进程内任务机制](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-configure-targets-and-tasks)，不修改 SDK 安装文件。

| 验证入口 | 主要验证内容 |
| --- | --- |
| `scripts/IntersectionOperationOracle.cs` | 完整分支、参数、类型、双面 UV、独立求交曲线/线框比较 |
| `scripts/BooleanOperationOracle.cs` | 同参数独立布尔/编辑、结果状态、XT、完整体检查和严格结构比较 |
| `scripts/PlanarTrackingOracle.cs` | tracking 的原始/结果几何、类别、多重性、已有边、miss 状态及负向曲线导入 |
| `scripts/AnalyticBooleanOracle.cs` | 两球布尔、源实体身份、导入后的分类、15 项质量属性 |
| `scripts/AnalyticSectionOracle.cs` | 球冠切割、三个 fence、封盖/圆边方向、质量属性和分类 |
| `scripts/FreeformModelingOracle.cs` | 插值/构造、扫掠/放样、缝合、容差边及导入复制变换生命周期 |
| `scripts/LocalOperationOracle.cs` | 圆角/倒角/偏置/抽壳，完整体检查、质量属性、实体方向和结构 |
| `scripts/NativeAdvancedModelingSmoke.cs` | 实际 NativeAOT C 导出调用和结果的真实 PK 接收/完整体检查 |
| `scripts/VerifyPublicModeling.cs` | 全量 kernel/XT 测试、原有 A/B oracle、显示桥、原生 ABI 与拾取 |

结构验证包含 region/shell/face/edge/vertex 数量、几何类型、方向、容差和缺失实体。
参数化相关的 full matching 按已有项目规则保留诊断。局部操作的部分平面圆角使用等价解析方向表示：保存原始比较差异，先检查物理面法向和每条 fin 的切向，再对两个独立副本执行相同的公开 PK 几何方向规范化，并重新进行严格 sense 比较。
没有关闭方向检查来隐藏材料朝向错误，详见 `docs/Local_Operations.md`。

持久验收结果应与实际运行日志一同保存在 `docs/reviews/advanced-modeling`，以该运行记录中的数量为准。

## 后续通用能力的真实缺口

当前最关键的扩展是一般曲面求交到拓扑构造之间的连续链条：全域分支和奇异点发现、成对参数曲线、任意曲面上的 arrangement、裁剪和材料分类、反复混合布尔以及容差恢复。
这部分完成后，才能自然扩展任意 NURBS 布尔、复杂局部圆角和一般偏置/抽壳，而不能仅靠继续增加少数解析形状分支完成。

本轮的分支发现、通用 B-rep 构造、平面 arrangement、tracking、容差 fin 所有权和独立 oracle，为上述扩展提供了可运行基础。

## 详细文档

- `docs/Intersection_Operations.md`
- `docs/Boolean_Operations.md`
- `docs/Analytic_Boolean_Operations.md`
- `docs/Planar_Tracking_Validation.md`
- `docs/Freeform_Modeling.md`
- `docs/Local_Operations.md`
- `docs/Public_Modeling_Stages_AB.md`
