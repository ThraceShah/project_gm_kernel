# 局部建模：圆角、倒角、偏置、抽壳

本模块提供自研 B-rep 局部操作。生产代码不加载、不调用 Parasolid；真实 Parasolid 38.0.150 只参与独立验收。算法构造解析曲线、解析曲面与精确拓扑，没有用三角网格代替实体边界。

功能定义对齐 Parasolid Functional Description 第 63 章 Offsetting、第 64 章 Hollowing、第 79–81 章 Edge Blending。接口、选项版本和返回结构以项目使用的 SDK 头文件为准。

## 公开接口与生命周期

| 接口 | 行为 |
| --- | --- |
| `PK_EDGE_set_blend_constant` | 在指定边记录常半径圆角定义，返回实际设置的边。此时不修改实体形状。 |
| `PK_EDGE_set_blend_chamfer` | 记录两个支持面的偏置距离与第一距离对应的面。距离是 face offset range，不是沿原始面的任意倒角宽度。 |
| `PK_BODY_fix_blends` | 将已设置的边转换为圆角或倒角面，返回 blend faces、每个面的 underlying faces、被替换的 edge/vertex 以及 fault。 |
| `PK_BODY_offset` / `_2` | 按面朝实体外部的方向偏置整个 body；`_2` 支持逐面覆盖距离。正值扩张，负值收缩。 |
| `PK_FACE_offset` / `_2` | 仅偏置指定面，重算与相邻面的交线和交点。其他面的偏置距离为零。 |
| `PK_BODY_hollow` / `_2` | 创建原始边界与偏置边界之间的实体材料，`_2` 支持 pierce faces 和非均匀壁厚。 |

抽壳的正距离向外增加壁厚；负距离向内产生空腔。显式 pierce faces 以及逐面覆盖值为零的面会形成开口。被穿透的原面成为有内环的环形面，而不是简单删除原面后留下开壳。

`BODY_offset_2` / `FACE_offset_2` 在已验证的拓扑保持和凸平面消面路径中返回零个 tracking records。抽壳为新内壁返回 `PK_TOPOL_track_create_c` 记录；pierce 面分裂时，为新分出的面返回 `PK_TOPOL_track_derive_c`，关联原 pierce 面。

抽壳无论使用正厚度还是负厚度，原 body 与存续外壳 face 的公开句柄均保持不变，create tracking 指向内壁。正厚度时原 face 移至外侧偏置位置，新内壁位于偏置前的位置；负厚度时原外壳位置不变，新内壁位于内侧偏置位置。球、环面、圆柱、圆台和平面体遵循同一身份规则。拓扑未改变的偏置还保留全部 edge/vertex 句柄。

`BODY_fix_blends` 的 `unders` 是独立返回数组的数组。调用方释放每个 `unders[i].array`，再释放 `unders`、`blends` 和 `topols`。偏置/抽壳的结构通过 `PK_TOPOL_track_r_f` 和 `PK_TOPOL_local_r_f` 释放。

## 支持的几何范围

| 操作 | 可构造的几何 |
| --- | --- |
| 平面实体偏置 | 单个凸实体，所有边为直线、所有面为单环平面；允许任意空间方位和斜棱柱。由偏置支持半空间重建交域，允许原支持面变为冗余并消失，保留存续面及可对应的边、点句柄。 |
| 解析实体偏置 | 完整球、无自交环面、两端平面封盖的圆柱与截锥。偏置保留对应解析曲面类型。 |
| 平面实体抽壳 | 上述凸平面实体；封闭空腔、相邻或分离的多个开口；允许逐面指定不同壁厚。开口面可以形成缺口、分裂成多个面或完全消失，结果必须仍是单个实体。 |
| 解析实体抽壳 | 完整球、无自交环面、圆柱、截锥；圆柱与截锥可穿透一个或两个平面端盖。 |
| 独立直边圆角/倒角 | 凸平面实体中互不接触的多个选中边。圆角面为圆柱；与斜端面相交的边为精确椭圆。倒角支持不同的两个 face offset ranges。 |
| 全边圆角/倒角 | 每个顶点恰好有三个支持面的凸平面实体，所有边使用同种、同尺寸定义，内缩后的支持面布局不改变拓扑。圆角包含全部圆柱边面与球面角点；倒角包含边平面与三角角点平面。 |
| 圆柱端缘圆角/倒角 | 一个或两个完整圆形端缘。圆角构造环面，倒角构造圆锥面，保留无顶点圆边。 |
| 规则自由曲面单边圆角 | Plane/NURBS、NURBS/NURBS，凸/凹单边，两个平面端部，真实 BlendSF 与 BLEND_BOUND 接触 ICurve；M2 还支持与既有有限障碍分离的布尔新生凸曲边再次圆角。详细合同见 `docs/Regular_Freeform_Solid_Fillets.md`、`docs/Mixed_Curved_Boolean_Operations.md`。 |
| 光顺链与已有 Blend 支持 | 同载体拓扑分段链、同一支持面真实分区的切换与接触 seam 传输；有限 Plane/Blend 锐边再次圆角。保留真实 procedural 支撑及各自区间，详见 `docs/Smooth_Support_Chain_Acceptance.md` 和 `docs/Intersection_Local_Modeling_Architecture.md`。 |
| 等半径三价角点 | 实际三边凸 star、Plane/BSurface 三支持的共同偏置根、真实圆柱/Blend bands 和共享球角片；明确的一至三条零 band 及对应支持面消退。详见 `docs/Trivalent_Corner_Blends.md`，不推广为任意高价或混合凸凹角点。 |

全边圆角通过支持面向内偏置后形成的中心多面体构造。每个原面沿自己的法向平移中心多边形，每条中心棱生成圆柱面，每个中心顶点生成球面片。面与圆柱之间、圆柱与球面之间共享精确接触边。全边倒角沿用相同的接触拓扑，使用平面替代圆柱、球面片。

偏置和抽壳不会将非凸输入“取凸包”后返回成功。读取支持平面时逐一检验所有原始顶点位于其内部半空间，并检验面环与直边类型。

### 相邻开口与凸平面消面

平面抽壳分别求出原半空间交域和偏置半空间交域，在每个支持平面上组合有向多边形边界。pierce 面的外多边形与反向内多边形进入同一来源组，按精确直线段消去共享边界，并统一拆分 T 接头。内边界接触外边界时形成缺口，不会留下相互接触的两个孔环；剩余部分不连通时拆成多个 face；没有剩余面积的 pierce 面被删除。

结果拓扑由原面来源映射建立。一个原面拆成多片时，保留其中一片的原 face 句柄，其余片进入 derive tracking。同一原边分成多个剩余区间时，其中一个区间保留原 edge 句柄。顶点对应使用完整的原始 incident-face 集合，包含已删除的支持面；不会把丢失一个面的旧顶点误配给仅共享其余两面的新顶点。正厚度与负厚度的原始句柄都对应外壳。

独立 PK 对照覆盖长方体相邻两面开口、共顶点三面开口、三面链式开口引起的面分裂、四面开口和五面开口引起的面消失；同时覆盖任意空间姿态、非均匀壁厚以及原生 XT 导入。若剩余材料分成两个不相连实体，`PK_BODY_hollow_2` 返回 `PK_ERROR_not_unique`，本实现回滚整个命令。

凸平面偏置允许一个支持面在偏置后不再约束交域，例如单面外移消去小倒角面，或整体内缩消去该面。删除的 face 不再占据结果拓扑，存续 face 保留原句柄。此能力以偏置后半空间交域有界、具有正体积且每条边仍可明确确定两个支持面为前提；正好落在退化事件上的零面积面、重合支持面和病态交点仍会显式拒绝。

## 边界与失败

目前不包含任意 NURBS 实体的拓扑偏置/抽壳、非凸平面体偏置、一般变半径圆角、超出规则单边支持域的自由曲面滚动球圆角、局部任意交汇边组的过渡面、任意凹边组合，以及一般混合曲面的自交消除和拓扑重构。这些情况返回显式错误，不返回空结果冒充成功。上述相邻开口和消面扩展仅适用于已校验的凸平面输入，不能推广为任意曲面体的拓扑变化已经完成。

`set_blend` 支持常半径或 face-offset chamfer 的基本选项；高级 setback、cliff、显式 cap、limit、callback、输出 sheet 和扩展 tracking 选项在当前范围外。SDK 也建议不要在设置和固定圆角之间修改模型的拓扑或几何。

数值或几何前置条件包括有限距离、正容差、正圆角半径、正剩余圆柱/圆锥半径、合法环面半径、非零壁厚及未塌缩实体。三平面求交只接受非病态法向组；顶点归并使用浮点舍入量级，不以用户的最大允许误差任意合并小特征。凸平面求解最多 64 个支持平面，构造缓冲区有明确上限，超界返回错误。

调用 `PK_TOPOL_detach_geom` 后保留下来的拓扑可能没有支持面、边曲线或顶点坐标。局部操作在读取几何池前统一检查这些支持，缺失时返回 `PK_ERROR_missing_geom` 并保留输入状态。合法的 tolerant edge 若由 fin 上的曲线提供几何，不会误报几何缺失；只有 fin 几何而没有 nominal curve 的局部重构仍明确返回 `PK_ERROR_not_implemented`。

每个公开修改都在现有 command transaction 中执行。失败撤销临时几何、拓扑、句柄映射和 pending blend 定义的本次更改；mark rollback 还能恢复固定圆角以前的未固定定义。复制 body 时，未固定定义会复制到新边，第一支持面映射到新 body 的对应面。

未固定圆角定义的 XT 属性编码尚未实现。为避免保存时静默丢失定义，包含 pending blend 的 body 在 `PK_PART_transmit_b` 返回 `PK_ERROR_not_implemented` 且不提供输出块；完成 `PK_BODY_fix_blends` 后可以正常导出结果几何。

## 验收

`tests/KernelTests/LocalOperationTests.cs` 覆盖偏置方向、逐面偏置、句柄保持、封闭和开口抽壳、tracking、边/角点/端缘曲面类型、返回内存、复制、mark undo、故障注入与拒绝无效输入。

`tests/KernelTests/LocalTopologyChangeTests.cs` 增加相邻开口、分裂面和消面、完整 face/edge/vertex 身份、正厚度原面位置与朝向、四类解析体的内壁 tracking、失败注入与 mark 回滚、零壁厚开口以及断开结果的原子失败。

`scripts/LocalOperationOracle.cs` 中的两条独立生产路径分别调用自研 API 与真实 PK API，并使用相同的输入参数。每例把自研结果写为新的文本 XT，再交给真实 PK receive。验收检查：

- 返回的圆角 underlying-face 数量或抽壳 tracking 数量。
- body 的 region、shell、face、edge、vertex 数量。
- 独立 PK 基准与自研导入结果的完整 `PK_BODY_check`。
- 体积、面积、质心与惯性张量。`PK_TOPOL_eval_mass_props` 的 accuracy 是积分工作量控制，越接近 1 越精细；验收使用 `0.999999999`，不把很小的值误作误差容限。
- 按世界坐标建立每个面的双射，对应面内部点和每条边界上的材料侧法向相同；逐 fin 对应并比较三个采样点处的有向切线，拒绝反向或缺失边界。
- `PK_DEBUG_BODY_compare` 的 topology、class、sense、tolerance 和 missing 项。

部分圆角和倒角使用等价但不同的解析参数方向：曲线轴/方向与其 use sense 同时反向，或平面轴与面 use sense 同时反向。原始 XT、原始严格比较结果及每个 sense diff 均保留。只有原始差异全部属于解析 surface/curve use sense，且以上世界坐标材料侧与边界方向检查已经通过时，才对两份独立结果的副本执行完全相同的公开 PK 规范化：复制实体，脱开现有解析几何，按实际几何重新创建并附着解析面/曲线，显式保留材料侧法向，曲线方向与原 edge 方向一致。

规范化前后再次比较全部面法向和 fin 切线，两个规范化副本分别执行完整 `PK_BODY_check`，随后再次执行严格 `PK_DEBUG_BODY_compare` 和质量属性比较。`sense_tests` 始终开启；任何拓扑、几何类型、面 sense、容差或 missing 差异都不能进入该放行路径。该处理只用于 oracle 中证明表示等价，不参与生产建模，也不修改原始导出文件。轴向偏置和圆柱端缘操作已对齐 SDK 的确定性曲线 use sense，无需此规范化。

原始 SDK 文档、私有库和 oracle 输入 SDK 不随代码提交。运行输出位于 `temp_docs/local-operation-oracle/`。

`scripts/LocalTopologyChangeOracle.cs` 独立验证这次拓扑扩展：22 个正向操作、2 个 `PK_ERROR_not_unique` 负向操作，另有 2 组 self receive → copy → 删除原体 → transform → transmit → PK receive 生命周期。所有正向结果使用原始几何直接通过完整 `PK_BODY_check`、严格 `PK_DEBUG_BODY_compare` 和 15 项质量属性比较，未使用方向规范化放行。

该 oracle 同时比较 tracking 的原面来源与类型、create/derive 分类、新面数量及材料侧方向；并按原始 incident-face 集合核对存续 face/edge/vertex 的身份。原生错误路径有时留下部分修改，因此两个负向场景核对原生错误码，并把自研事务回滚后的 XT 与调用前独立 PK 副本比较，不把原生报错后的中间模型当作成功结果。报告位于 `temp_docs/local-topology-change-oracle/`，归档见 `docs/reviews/advanced-modeling/local-topology-oracle-2026-11-03.txt`。

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/LocalOperationOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/LocalTopologyChangeOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```
