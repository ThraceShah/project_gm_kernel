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

`BODY_offset_2` / `FACE_offset_2` 在当前保持拓扑类型的支持范围内返回零个 tracking records。抽壳为每个未穿透原面返回一个 `PK_TOPOL_track_create_c` 记录，关联新生成的偏置面。原 body 与存续 face 的公开句柄保持不变；拓扑未改变的偏置还保留全部 edge/vertex 句柄。

`BODY_fix_blends` 的 `unders` 是独立返回数组的数组。调用方释放每个 `unders[i].array`，再释放 `unders`、`blends` 和 `topols`。偏置/抽壳的结构通过 `PK_TOPOL_track_r_f` 和 `PK_TOPOL_local_r_f` 释放。

## 支持的几何范围

| 操作 | 可构造的几何 |
| --- | --- |
| 平面实体偏置 | 单个凸实体，所有边为直线、所有面为单环平面；允许任意空间方位和斜棱柱。保留各支持面并求三平面精确交点。 |
| 解析实体偏置 | 完整球、无自交环面、两端平面封盖的圆柱与截锥。偏置保留对应解析曲面类型。 |
| 平面实体抽壳 | 上述凸平面实体；封闭空腔或互不接触的多个穿透面；允许逐面指定不同壁厚。 |
| 解析实体抽壳 | 完整球、无自交环面、圆柱、截锥；圆柱与截锥可穿透一个或两个平面端盖。 |
| 独立直边圆角/倒角 | 凸平面实体中互不接触的多个选中边。圆角面为圆柱；与斜端面相交的边为精确椭圆。倒角支持不同的两个 face offset ranges。 |
| 全边圆角/倒角 | 每个顶点恰好有三个支持面的凸平面实体，所有边使用同种、同尺寸定义，内缩后的支持面布局不改变拓扑。圆角包含全部圆柱边面与球面角点；倒角包含边平面与三角角点平面。 |
| 圆柱端缘圆角/倒角 | 一个或两个完整圆形端缘。圆角构造环面，倒角构造圆锥面，保留无顶点圆边。 |

全边圆角通过支持面向内偏置后形成的中心多面体构造。每个原面沿自己的法向平移中心多边形，每条中心棱生成圆柱面，每个中心顶点生成球面片。面与圆柱之间、圆柱与球面之间共享精确接触边。全边倒角沿用相同的接触拓扑，使用平面替代圆柱、球面片。

偏置和抽壳不会将非凸输入“取凸包”后返回成功。读取支持平面时逐一检验所有原始顶点位于其内部半空间，并检验面环与直边类型。

## 边界与失败

目前不包含任意 NURBS 实体的拓扑偏置/抽壳、一般变半径圆角、自由曲面滚动球圆角、局部任意交汇边组的过渡面、凹边混合圆角、邻接穿透面产生的平面布尔重整，以及偏置导致支持面消失时的拓扑重构。这些情况返回显式错误，不返回空结果冒充成功。

`set_blend` 支持常半径或 face-offset chamfer 的基本选项；高级 setback、cliff、显式 cap、limit、callback、输出 sheet 和扩展 tracking 选项在当前范围外。SDK 也建议不要在设置和固定圆角之间修改模型的拓扑或几何。

数值或几何前置条件包括有限距离、正容差、正圆角半径、正剩余圆柱/圆锥半径、合法环面半径、非零壁厚及未塌缩实体。三平面求交只接受非病态法向组；顶点归并使用浮点舍入量级，不以用户的最大允许误差任意合并小特征。凸平面求解最多 64 个支持平面，构造缓冲区有明确上限，超界返回错误。

调用 `PK_TOPOL_detach_geom` 后保留下来的拓扑可能没有支持面、边曲线或顶点坐标。局部操作在读取几何池前统一检查这些支持，缺失时返回 `PK_ERROR_missing_geom` 并保留输入状态。合法的 tolerant edge 若由 fin 上的曲线提供几何，不会误报几何缺失；只有 fin 几何而没有 nominal curve 的局部重构仍明确返回 `PK_ERROR_not_implemented`。

每个公开修改都在现有 command transaction 中执行。失败撤销临时几何、拓扑、句柄映射和 pending blend 定义的本次更改；mark rollback 还能恢复固定圆角以前的未固定定义。复制 body 时，未固定定义会复制到新边，第一支持面映射到新 body 的对应面。

未固定圆角定义的 XT 属性编码尚未实现。为避免保存时静默丢失定义，包含 pending blend 的 body 在 `PK_PART_transmit_b` 返回 `PK_ERROR_not_implemented` 且不提供输出块；完成 `PK_BODY_fix_blends` 后可以正常导出结果几何。

## 验收

`tests/KernelTests/LocalOperationTests.cs` 覆盖偏置方向、逐面偏置、句柄保持、封闭和开口抽壳、tracking、边/角点/端缘曲面类型、返回内存、复制、mark undo、故障注入与拒绝无效输入。

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

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/LocalOperationOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```
