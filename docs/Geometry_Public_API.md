# 几何公开 API、复制、变换与部件生命周期

更新：2026-11-03。本文描述 A 阶段已经接入生产入口的范围，并区分几何定义与后续实体建模。对应阶段总览见 [Public_Modeling_Stages_AB.md](Public_Modeling_Stages_AB.md)。

## 1. 创建、查询和求值

下表中的名称是公开 C 导出。生成的 SDK 声明或内部数据类型本身不计作实现。

| 几何 | 创建 | 定义查询 | 参数与求值 |
| --- | --- | --- | --- |
| 点 | `PK_POINT_create` | `PK_POINT_ask` | 三维坐标 |
| 直线、圆 | `PK_LINE_create`、`PK_CIRCLE_create` | 对应 `*_ask` | `PK_CURVE_ask_interval`、`PK_CURVE_eval`、`PK_CURVE_eval_with_tangent` |
| 椭圆 | `PK_ELLIPSE_create` | `PK_ELLIPSE_ask` | 同上，两个半轴及坐标系保留 |
| B 曲线 | `PK_BCURVE_create` | `PK_BCURVE_ask` | 多项式及有理曲线，完整控制点、权重、节点和重数 |
| 修剪曲线 | legacy `CRTRCU` | `PK_TRCURVE_ask` | 公开参数从零开始；查询保留基础曲线区间 |
| 曲面参数曲线 | `PK_SPCURVE_create` | `PK_SPCURVE_ask` | 由支持曲面及二维 B 曲线定义，求值组合两者导数 |
| 平面、圆柱、圆锥、球、环面 | 对应 `PK_*_create` | 对应 `PK_*_ask` | `PK_SURF_ask_uvbox`、`PK_SURF_eval` |
| B 曲面 | `PK_BSURF_create` | `PK_BSURF_ask` | 张量积多项式 / 有理曲面及完整节点、控制数据 |
| 偏置曲面 | `PK_OFFSET_create` | `PK_OFFSET_ask` | 基础曲面和偏置距离 |
| 扫掠曲面 | `PK_SWEPT_create` | `PK_SWEPT_ask` | 定义曲线和扫掠方向 |
| 旋转曲面 | `PK_SPUN_create` | `PK_SPUN_ask` | 定义曲线和旋转轴 |

SDK 的修剪曲线创建函数是返回 `void`、通过输出参数报告错误的 `CRTRCU`。实现保持该 ABI，没有增加不存在的 `PK_TRCURVE_create`。C 导出位于 `src/ProjectGmKernel.Native/KernelExports.cs` 和 `src/ProjectGmKernel.Native/KernelExports.Geometry.cs`；分派、参数验证与运行时查询位于 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.GeometryCreate.cs`、`src/ProjectGmKernel.Native/Runtime/KernelRuntime.GeometryQueries.cs` 及相关 partial 文件。

这里的 offset、swept、spun 是几何曲面的定义接口。它们不等同于偏置实体、抽壳、任意截面扫掠或一般自由曲面建模。由平面轮廓生成有界实体的 extrude / spin 能力及其限制见 [Profile_Modeling.md](Profile_Modeling.md)。

已有 ICurve 可以从受支持的 XT 数据或内部绑定入口建立并求值；本表没有宣称新增通用的曲面求交曲线构造 API。ICurve 的支持范围和导数限制仍以 `docs/icurve_evaluation_capabilities.md` 及独立的 ICurve 验收文档为准。

## 2. 原生参数与 NURBS 数据

### 点到参数与投影基础

`PK_SURF_parameterise_vector`、`PK_CURVE_parameterise_vector` 已接入生产导出及事务分派。
解析几何使用原生参数；B 曲面和 B 曲线通过保守 Bezier 包围及全局距离分支定界定位参数，
Newton 只改善候选上界。ICURVE 使用原始 chart 参数映射并重新求值验证所在分支，修剪曲线映射回公开区间。
公开 parameterise 要求输入点在几何上；离面/离线点返回对应错误，不能把最近点当成成功的参数化结果。
内部最近点基础可用于距离查询，但本次未增加通用公开距离 API。一般求交与双 UV、闭合分支的范围见
[Curved_Surface_Pipeline.md](Curved_Surface_Pipeline.md)。

### 参数域

`PK_CURVE_ask_interval` 和 `PK_SURF_ask_uvbox` 返回与现有几何类别相符的原生参数域。NURBS 使用有效节点域；圆及椭圆使用角参数；Swept 的第一参数来自定义曲线，第二参数表示沿方向的距离；Spun 的第一参数来自定义曲线，第二参数为旋转角。Offset 继承基础曲面的参数域。

解析几何的有限自然范围使用 SDK 的实际约定，包括近似 `±10000` 的无界方向标记；这些数值不是数学上的无穷大，也不能用一个统一的 `[0,1]` 替代。Oracle 对照了直线、圆、椭圆、平面、圆柱、圆锥、球、环面及 dependent geometry 的实际查询结果。

公开 `CRTRCU(basis, a, b, ...)` 要求递增的有效基础区间。得到的曲线在 `[0,b-a]` 上求值，映射为：

```text
trim(t) = basis(a + t)
```

例如基础直线区间为 `[-1.25, 2.75]` 时，`PK_TRCURVE_ask` 仍报告该基础区间，`PK_CURVE_ask_interval` 返回 `[0,4]`。内部 XT 重建保留有向的降序区间，参数映射及奇数阶导数会相应反号；公开 legacy 构造不会因此放宽降序输入。

### 完整定义与内存所有权

BCurve / BSurf 的 `ask` 返回完整控制数组、节点及重数，同时保留维数、有理标志、周期 / 闭合标志和其他已支持元数据。查询不会返回可写的内部 payload 指针。调用方使用 `PK_MEMORY_free` 分别释放返回的数组；失败时不会发布部分初始化的数组组合。

有理数据采用 SDK 的齐次坐标布局。三维有理控制点包含 `x*w, y*w, z*w, w`，而不是未经加权的 Cartesian 坐标加一个权重。SPCurve 的二维基础曲线是参数空间数据，不作为三维部件 construction 成员登记。

BSurf 创建验证有限数据、合法维数、正有理权重、节点单调性和重数、有效参数域及周期条件。控制标量数量在遍历输入缓冲区前做溢出检查。对真实 PK 使用的 clamped-periodic 表示，验证整个有理接缝的闭合与一阶一致性；不能仅因端点 Cartesian 坐标相同就接受周期声明。现有普通 pole-wrap 分支仍保留，支持该表示不意味着任意人为扩展的周期节点组合都能通过真实 PK 的完整曲面合法性检查。

## 3. 深复制

`PK_ENTITY_copy` 和 `PK_ENTITY_copy_2` 的受支持选项执行独立深复制。Body 的拓扑、边界几何、定义依赖和 construction 几何使用同一个复制映射，保持新部件内部原有的共享关系。新旧部件的变量长度控制点、节点、ICurve chart 等 payload 独立；删除源部件后，副本仍可求值、变换及导出。

单次批量 `PK_GEOM_transform_2` 的 `new` 模式也使用共同的复制映射。例如两个 Swept 共用一个 BCurve 时，两个输出 Swept 继续共用同一个新的 BCurve；若输入数组还显式包含该 BCurve，对应输出就是同一个定义依赖。

`PK_ENTITY_copy_2` 目前不支持 destination、user fields、attributes、groups 或 tracking 等高级选项；这些请求明确返回未实现。Opaque XT 不能据此被当成已重建、可编辑的实际 B-rep。部件 construction point 尚未进入这一完整生命周期范围，相关复制和导入会拒绝或保留为 opaque，不会静默遗漏。

## 4. 相似变换与反射

公开入口包括 `PK_TRANSF_ask`、`PK_GEOM_transform`、`PK_GEOM_transform_2`、`PK_BODY_transform` 和 `PK_BODY_transform_2`。矩阵按 SDK 的行存储、列向量约定解释；有效的齐次比例项会先规范化。

生产变换支持平移、旋转、正均匀缩放及反射组成的相似变换。几何中心、方向、半径、控制点、参数范围及容差均需保持有限和有效；失败通过命令事务恢复，不能留下半变换对象。一般剪切、非均匀缩放及透视变换尚未实现。

反射会改变某些原生参数方向，因此兼容性比较同时检查几何位置和参数导数。对于统一尺度 `s>0` 及方向符号 `p=±1`，当前映射为：

| 类别 | 原生参数映射 | 其他处理 |
| --- | --- | --- |
| 直线 | `t' = s*t` | 单位方向变换；依赖直线的 TR 区间同步缩放 |
| 圆、椭圆、B 曲线 | 参数保持 | 变换坐标系或控制点 |
| 平面 | `u' = s*u`，`v' = s*p*v` | 坐标系保持 SDK 的方向约定 |
| 圆柱、圆锥 | `u' = p*u`，`v' = s*v` | 角参数与轴向距离分开处理 |
| 球、环面 | `u' = p*u`，`v' = v` | 半径按 `s` 缩放 |
| B 曲面 | UV 保持 | 齐次控制点变换；反射时 surface sense 翻转 |
| Swept | `u'` 跟随基础曲线，`v' = s*v` | 反射时 surface sense 翻转 |
| Spun | `u'` 跟随基础曲线，`v' = p*v` | 旋转轴与辅助数据一起变换 |
| Offset | 继承基础曲面映射 | 偏置距离依尺度和参数法向方向变化 |

Offset 的符号不能只按矩阵行列式统一翻转：基础面的 UV 映射也参与法向方向。求值、定义查询和 XT 导出需要表达相同的实际曲面。Body 反射还调整 loop / fin 的方向和顶点关联，保持真实 PK 可接收的有向拓扑。

`PK_GEOM_transform` 产生新几何，源几何保持不变。`PK_GEOM_transform_2` 的原地 `modify` 模式要求输入显式覆盖完整的定义依赖及所有几何所有者闭包；缺少共享依赖或外部所有者时返回 SDK 对应错误。已附着的边界几何通过 body 变换处理，不能由一个孤立的几何变换悄悄移动整个部件的一部分。同一次合法批次中的共享依赖只变换一次。

## 5. 引用、部件归属与 construction 几何

TRCurve、SPCurve、Offset、Swept、Spun 和已绑定 ICurve 保持对定义几何的存活引用。几何依赖计数与 face / edge 的拓扑所有者计数分别管理，直接删除仍被几何引用的对象返回 `PK_ERROR_still_referenced`。普通解码后绑定的 ICurve 只借用输入支持面；删除曲线会释放存活引用，原来独立存在的输入支持面仍然保留。由 XT 导入或深复制创建的 ICurve 拥有其支持面的释放责任，其他上述依赖几何也会在最后一个拥有者删除后释放没有其他引用的定义几何。如果拥有者或最后一个 face 先于借用者删除，支持面会记录待释放状态，直到最后一个借用引用释放且没有拓扑所有者时才被回收；mark / goto 同时恢复拓扑引用计数和原有待释放状态。

一个定义几何连通分量只能归属一个部件。新依赖对象使用已经归属 body 的定义几何时，继承该部件归属。将最初游离但共享依赖的多个根附着到部件时，必须满足 SDK 的完整共享关系要求；跨部件归属冲突不能通过隐藏复制或改变另一个部件解决。

`PK_PART_ask_geoms` 返回部件的 construction 几何，而不是所有边和面的几何列表。分类依据是有向定义关系：从附着到拓扑的曲线、曲面及 fin pcurve 向下可达的定义几何属于边界定义闭包；同一部件内闭包之外的三维几何属于 construction。

具体例子：

- 一个 attached Swept 及其定义曲线属于边界定义闭包，均不进入 construction 列表。
- 在 wire 的 edge-owned 曲线上新建 Swept，Swept 成为 construction 成员，定义曲线仍由 edge 所有。
- 在这个 Swept 上再建 Offset，Offset 与未附着的中间 Swept 都保留为 construction 成员。
- ICurve 附着到 edge 后，它的两个支持曲面属于边界定义闭包，不会被错误写入 XT 的 construction surface 链。
- 将 construction 根附着到 face / edge 后，根及其定义闭包退出 construction 链；剩余反向所有者继续作为 construction 成员。

Construction 使用独立的 body / previous / next 字段，不复用边界几何链。创建、附着、脱离、单独删除、body 删除及 mark undo 会同步维护列表与引用。删除顶层 Offset 时，没有其他所有者的中间 Swept 会一并删除；已经由 edge 所有的定义曲线继续存在。复制、body 变换、XT 写出与重建都遍历这些成员，避免“创建成功但之后不可查询或泄漏”的路径。

## 6. 验证入口与证据

以下对照使用真实 Parasolid 的独立 producer，不把 `PskernelSharp` 重定向到本项目。脚本通过本项目的托管运行时 / C 导出创建或修改对象，另以相同输入调用真实 PK；对 body 再执行自研 text XT 写出、fresh PK 接收及结构比较。完整验收总表以 `docs/Public_Modeling_Stages_AB.md` 的最终版本为准。

| 验证 | 文件 | 重点 |
| --- | --- | --- |
| 公开几何契约 | `scripts/GeometryPublicApiOracle.cs` | C 导出、完整 NURBS 数组、自然参数域、TR 参数映射、周期正负控制、引用与 construction 生命周期 |
| 单类几何 body / XT | `scripts/XtGeometryWriterOracle.cs` | 七类几何的独立创建、导出、真实 PK 接收与 body 对照；由完整公开 API Oracle 调用 |
| 复制与变换 | `scripts/GeometryTransformOracle.cs` | 原生 D0–D2、共享定义依赖、反射、body 全检查、construction 复制 / 变换后 fresh XT |
| 通用导入 | `scripts/BrepImportOracle.cs` | 实际图重建、construction membership、class / owner / jets、删除源后的独立副本和池回基线 |
| 单元回归 | `tests/KernelTests/GeometryPublicApiTests.cs`、`GeometryTransformTests.cs`、`ConstructionGeometryTests.cs` | 数值负控、失败不变性、allocation fault、mark undo、payload 独立及完整释放 |

原始结果位于 `temp_docs/geometry-public-api/oracle-latest.txt`、`temp_docs/geometry-transform-oracle/report.txt` 和 `temp_docs/brep-import-oracle/`。完整最终门禁由 `scripts/VerifyPublicModeling.cs` 顺序运行，并在任意失败时返回非零退出码。

2026-11-03 在统一生产程序集和真实 Parasolid 38.0.150 上的分项实测：完整 `GeometryPublicApiOracle` 通过，包含 U/V 两个方向的 14 个 clamped-periodic 正负控制、四类部件附着契约、construction 生命周期，以及它串接的七类 `XtGeometryWriterOracle`；`GeometryTransformOracle` 为 **83 通过、0 失败**。两个进程均返回 0。变换集合覆盖新几何批次的共享依赖，以及两类 construction wire 在四种相似变换下的复制、fresh XT 和真实 PK 对照。

Clamped seam 检查先在公共局部坐标系内形成齐次控制系数，再使用局部运算误差及单列的输入 ULP 传播范围；整体平移 `1e6` 不会把单列 `1e-7` 的切向缺陷掩盖掉。U/V 的这些负控制返回与真实 PK 相同的 `PK_ERROR_periodic_not_smooth`，未扰动的平移曲面仍被接受。

旧 writer Oracle 的 TRCurve-of-Line 夹具会被真实 PK 规范化为 Line。数值对照显式把零起点的 TR 参数映回基础 Line 参数，例如基础区间 `[-100,100]` 下比较 `trim(100.5)` 与 `line(0.5)`，并检查 D0–D2。该参数映射保留严格的同一点、同导数比较；它不是放宽数值容差。

复现公开几何契约与其 body / XT 组合门禁：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/GeometryPublicApiOracle.cs
```

只运行创建、查询及生命周期对照可传 `--contracts-only`。它不会替代 body 写出 / 接收验收。已有构建的受控诊断可传 `-p:BuildProjectReferences=false -p:UseSharedCompilation=false`，统一验收则负责实际生产程序集的构建。

## 7. 明确不包含的能力

本阶段不提供一般曲面求交的分支构造、imprint / split / trim 拓扑构造、通用布尔、任意放样 / 扫掠、实体圆角 / 倒角、实体偏置或抽壳。几何类具备求值、创建和定义查询，并不能推导出这些高级建模能力已完成。

原始 parametric ICurve 的严格完整 D2 与 PK 约定差异仍是独立的未通过门禁，见 [parasolid-sdk-validation-2026-11-02.md](reviews/icurve-evaluation/parasolid-sdk-validation-2026-11-02.md)。本阶段没有改变该导数定义，也没有降低该对照要求。
