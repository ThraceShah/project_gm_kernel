# XT B-rep 实体化与公开拓扑查询

## 导入原则

`PK_PART_receive_b` 对已支持的 BODY 图直接建立运行时 B-rep。它不再根据面数、边数或表面类别猜测 primitive，也不再用单位长方体或全局 Z 轴棱柱替代源几何。

导入过程分为两层：

- `src/ProjectGmKernel.Native/Runtime/XtReader.Brep.cs` 在已有的托管 XT 适配层建立索引，检查节点类型、所有权、环与依赖关系。
- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.XtBrep.cs` 和 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.XtBrepGeometry.cs` 使用会话记录池建立几何与拓扑。核心不创建托管数组、字典或列表。

位置、方向、半径、原生参数化、控制点、权重、结点与重数来自源 XT。共享几何在一次 body 实体化内按源节点索引复用。面方向、前后 shell use、loop 顺序、edge fin ring 与 vertex fan 分别恢复，不能把它们当成同一种邻接顺序。

## 已接通的图与几何

当前实体化器处理采用已识别 BODY 布局的 34–38 版本文档。运行时图包括 region、shell、双侧 face use、多个 loop、fin、edge、vertex，支持平面孔环、无顶点闭合边、单个 vertex 构成的 pole loop，以及由 shell 持有的线框边。直接 body 持有的旧线框子集继续可用。

| 几何类别 | 保留内容 |
| --- | --- |
| Point、Line、Circle、Ellipse | 实际位置、坐标系、半径及源 sense |
| Plane、Cylinder、Cone、Sphere、Torus | 实际坐标系、尺寸及源 sense |
| BCurve、BSurface | 次数、控制点/齐次权重、不同结点、重数、闭合/周期标记、可表示的分类 |
| TRCurve、SPCurve | 原始依赖关系、修剪参数/端点、支持面与参数曲线 |
| Swept、Spun、Offset | 递归定义曲线/曲面、方向/轴/距离、运行时可表示的辅助数据 |

BODY 的 construction surface/curve 链也按实际所有权保留。例如，以 body 边上的 BCurve 创建 Swept，再创建 Offset(Swept)，真实 PK 的 construction 链同时包含 Offset 和 Swept；Swept 被另一几何引用，并不取消其 BODY 归属。导入、深拷贝与 fresh transmit 都保留这两层关系。`PK_PART_ask_geoms` 查询的是这类 part 直接持有的构造几何，并不把所有边界几何也混入结果。

现有经过验证的单条 open ICurve wire 保留专门的导入路径。通用多面 B-rep 的 INTERSECTION 曲线不因此获得支持；这类图继续保留为 opaque。construction point、acorn shell、并非单顶点 pole loop 的无 edge fin、扩展 B-surface 参数域或缩放 procedural 数据的有效图也保留为 opaque，不替换成近似 primitive。

具有非零 `HALF_EDGE.curve` 的 fin 参数曲线图当前也保留为 opaque。运行时虽然具备 fin 几何依赖引用机制，fresh XT writer 尚未验证其专属曲线的 topological owner 和完整依赖链。导入器因此不将这类图报告为可编辑，原始文档仍可转发；不能在导出时把该指针写成零后声称完成无损往返。

NURBS 的 `SCH_knot_type_t` 和 `SCH_curve_form_t` 是一基枚举，不能把 PK 枚举减去起点后直接写成零基值。控制数据和分类必须同时按相应 schema 转换。

## 验证与生命周期

图验证检查源引用确实存在且类型一致，region/shell/face/loop/edge/vertex 的归属关系一致，fin 同时出现在其 loop 和 edge 的实际链中，vertex fan 覆盖所有使用该 vertex 的 fins，以及同一 edge 的正反使用具有相同端点。几何依赖必须无环；非有限标量和负容差不能进入运行时。

几何按依赖顺序建立。TRCurve、SPCurve、Swept、Spun、Offset 的构造器取得依赖引用。body 的拓扑几何引用、构造几何链归属与这些几何依赖引用分别保存。公共接收事务失败时回滚全部新记录和变量长度数据块；在同一成功接收事务中转为 opaque 时，已建立但不再使用的临时几何被显式释放。

ICurve 绑定同样取得支持面的存活引用，但保留 `OwnsSupportSurfaces` 的所有权区别：内部解码后绑定的曲线借用调用者的面，删除曲线不会删除这些独立输入；导入和深拷贝的支持面由新图拥有。拥有者先于其他借用者删除时，支持面保留待释放标记，等最后一个引用离开再清理。最后一个 face 拓扑拥有者先删除时也使用这一规则；mark/goto 同时恢复拥有者计数与原标记。失败创建的回滚只撤销新增存活引用，不取得调用者输入的释放权。

真实 PK 的 sheet XT 在边界边上还保存 dummy fin。它不属于 face loop，但必须在运行时导入和删除时保留正确的 edge ring。删除 body 时，遍历必须越过已退休的 face fin，才能释放同一 edge ring 上仍存活的 dummy fin；删除前的 undo 容量预算也必须包括它们。

## 公开查询语义

公开入口补齐了 `PK_EDGE_ask_vertices`、`PK_FACE_ask_edges`、`PK_FACE_ask_oriented_surf`、`PK_REGION_ask_shells`、`PK_BODY_ask_type` 与 `PK_BODY_ask_config`。opaque body 的 type/config 查询读取原始文档的分类元数据；返回 body 类型不意味着它已经成为可编辑的运行时模型。

`PK_BODY_ask_topology` 遵守 `want_fins`，并依据真实 SDK 的关系方向输出：

- `BODY → REGION` 的 positive sense 表示 solid region，negative sense 表示 void region；`REGION → SHELL` 无方向。
- 包含 fins 时输出 `LOOP → FIN` 及带方向的 `FIN → EDGE`；不包含 fins 时输出带方向的 `LOOP → EDGE`。
- 输出 `EDGE → VERTEX`，以及线框的 `SHELL → EDGE`。一条闭合边的两个端点是同一个 vertex 时，仅输出一条顶点关系；`PK_EDGE_ask_vertices` 仍在两个端点槽位中返回该 vertex。
- dummy fins 不出现在公开拓扑列表中；`PK_EDGE_ask_fins` 同样只返回属于 loop 的 fins。
- pole loop 的内部 null-edge halfedge 也不作为公开 fin 返回。两种 `want_fins` 模式都直接输出 `LOOP → VERTEX`；`PK_LOOP_ask_fins` 对这种 loop 返回空列表。XT 中该 halfedge 保留 loop/vertex、自环 next/previous、null other/edge 与未设置的 sense。

`scripts/ProbeTopologyContracts.cs` 对真实 SDK 的实测结果为：

| 模型 | `want_fins` | 拓扑实体数 | 关系数 | 每条边的公开 fin 数 |
| --- | --- | ---: | ---: | ---: |
| 长方体 | false | 37 | 70 | 2 |
| 长方体 | true | 61 | 94 | 2 |
| 圆形 sheet | false | 6 | 6 | 1 |
| 圆形 sheet | true | 7 | 7 | 1 |
| 单条开线框边 | false / true | 6 | 5 | 0 |
| 尖锥 | false | 12 | 14 | 2 |
| 尖锥 | true | 14 | 16 | 2 |

运行时棱柱生成器的 loop 也按实际相邻边的公共顶点设置 fin forward vertex。底面使用反向 winding，避免旧实现根据 edge 的第一次/第二次使用猜测端点而形成不连通 loop。侧面 PLANE 的 normal 使用对应边中点的单位径向方向，避免把边长写入单位坐标系并导致自己的接收构造器拒绝。

尖锥的 pole loop 挂在 cone side face 上；原先挂到顶部 cap plane 的归属已修正。复制通过公开 `LOOP → VERTEX` 关系重建内部 pole halfedge，删除经 face/loop 释放该 halfedge。

## 缓存与验证入口

未修改的导入 body 可以继续使用收到的 XT 文档转发。几何 detach/attach 成功后丢弃旧文档关联，后续 transmit 必须遍历实际运行时图。mark/goto 恢复后的 runtime 图可以重新导出，不能用旧缓存掩盖修改或回滚结果。opaque 图不允许通过这些入口假装完成实体编辑。

`tests/KernelTests/XtBrepImportTests.cs` 覆盖姿态与尺寸、孔环、闭合线框、图损坏、分配故障回滚、拓扑查询、缓存失效与 mark/goto。

`scripts/BrepImportOracle.cs` 从真实 PK 创建六种有姿态的解析实体、带孔 sheet、B-surface sheet、Swept sheet、Spun sheet、有姿态的尖锥，以及两个含构造几何的线框，再通过公共 receive 进入本内核。它拒绝 opaque 结果，比较原生参数下的点/曲线/曲面求值、公开拓扑关系及 body type/config；移除原始文档关联后 fresh transmit，再让真实 PK receive 并执行结构、类型、方向、容差和 missing 检查。随后经公共 `PK_ENTITY_copy` 深拷贝并删除原体，重复副本求值、拓扑查询、fresh transmit 和真实 PK 比较，验证副本不依赖原体的几何。构造几何案例还从双方的公开 `PK_PART_ask_geoms` 递归检查定义依赖，比较显式 part membership、几何 class、几何 owner 数和参数求值，防止只保留可见边而丢掉附属几何。每例删除后，实体池、tag 数和几何 payload 的 live bytes/blocks 必须回到基线。运行结果记录在 `temp_docs/brep-import-oracle/oracle-latest.txt`；程序遇到任意失败返回非零退出码。

传入一个包含 `*-managed.x_t` 文件的建模 Oracle 输出目录时，同一脚本会改为逐一验证这些文件：目录参数相对于脚本路径解析，先由真实 PK 接收作为比较基线，再直接将原始文件交给本内核的公共 receive，重复同样的实体化、求值、拓扑、独立复制、fresh transmit 与生命周期门禁。这个组合运行的报告单独写入 `temp_docs/brep-import-oracle/modeling-integration/oracle-latest.txt`，不覆盖默认案例的报告。

在目录参数之后加 `--reference`，则读取 `*-reference.x_t`，用于同样验证真实 PK 建模结果（包括使用 clamped periodic B-surface 的完整旋转体），报告写入 `temp_docs/brep-import-oracle/modeling-reference-integration/oracle-latest.txt`。

## 已验证结果（2026-11-03）

在 Parasolid 38.0.150、XT transmission 371 上，以下三组使用包含 construction 生命周期与 boundary 定义闭包分类、批量复制共享关系、反射 offset 读写符号以及 ICurve 支持面引用修复的同一版运行时执行，全部返回退出码 0：

| 输入来源 | 案例数 | 日志中的边界几何参数样本最大绝对差 |
| --- | ---: | ---: |
| 独立真实 PK 生产器，含尖锥与两例 construction 图 | 13 / 13 | `1.4210854715202004e-14` |
| 本内核公开建模输出，经真实 PK 接收建立比较基线 | 33 / 33 | `3.552713678800501e-15` |
| 对应的真实 PK 原生建模输出 | 33 / 33 | `7.105427357601002e-15` |

共 79 例都完成 runtime 实体化、两种 `want_fins` 查询、fresh XT → PK、公开独立复制后删除原体并重新验证。参考文件包括原生 clamped periodic NURBS 完整旋转体。region material、shell/face-use incidence 和 PK 结构检查均通过，全部实体池、tag 与 payload live bytes/blocks 回到基线。

两例 construction 图另外比较公开 `PK_PART_ask_geoms` 的显式成员、递归定义依赖、几何 class、geometric owner 数和原生参数 jets；其中共享图同时包含 Offset、它的 Swept 支持面、Spun、TRCurve，以及仍附在 edge 上的同一 BCurve。导入后、副本删除原体后和两次真实 PK fresh receive 都通过该检查。该结果证明的是上述明确的模型与检查集合；通用 INTERSECTION B-rep、fin 专属参数曲线、布尔运算和未实现的拓扑类型不在本门禁覆盖范围内。

本轮对应日志是 `temp_docs/brep-import-oracle/closure-13-final.log`、`temp_docs/brep-import-oracle/closure-modeled-33-final.log` 和 `temp_docs/brep-import-oracle/closure-reference-33-final.log`。原有 ICurve 独立对照另在 `temp_docs/phase-ab-icurve-final-2026-11-03.log` 完成 A、A2、B、C、D、E、F、F2、F3 回归；日志中原先明确标记的终点、高阶导数及 blend 门禁仍保留 `NotRun`，不计为通过。
