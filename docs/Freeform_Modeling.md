# 自由曲线、曲面构造与轮廓建模

本模块实现自研 B 样条计算、曲面构造、有限范围内的实体扫掠和放样，以及完整边界匹配缝合。生产代码不调用 Parasolid，也不使用离散三角网格生成模型。真实 Parasolid 38.0.150 仅作为独立的参考生产者和导入检查器。

功能定义对齐用户提供的 Parasolid Functional Description 第 18–19 章 Geometry / B-Curves And B-Surfaces、第 42 章 Sweeping、第 44 章 Lofting 及 sheet sewing 定义。函数签名、选项版本、内存释放结构来自项目使用的 SDK 头文件。原始文档和私有 SDK 不复制进源码。

这里描述的是已经实现的范围。一般 NURBS/NURBS 求交与布尔、任意移动标架扫掠、带导轨放样、任意间隙和局部边匹配缝合仍不在本模块的完成范围内。

## 公开接口

| 接口 | 实现行为 |
| --- | --- |
| `PK_BCURVE_create_spline` / `_2` | 按参数插值有限点列；`_2` 还支持带误差界的点集最小二乘拟合。 |
| `PK_BCURVE_spline_r_f` | 释放返回的曲线描述数组及 fault 数组，曲线实体继续存在。 |
| `PK_BCURVE_make_matched` | 通过精确插结、Bezier 分段和升阶统一多条曲线的次数及节点布局，不改变曲线形状。 |
| `PK_BCURVE_make_bsurf_lofted` | 在匹配后的普通或有理曲线之间构造自然端条件的张量积放样面。 |
| `PK_BCURVE_sweep` | 构造精确的线性平移曲面，保留原曲线参数为 u，平移参数 v 为 `[0,1]`。 |
| `PK_BCURVE_spin` | 将普通或有理 B 曲线围绕给定轴旋转，使用精确有理二次圆弧生成张量积曲面。 |
| `PK_BSURF_make_bcurve_u_isoparam` / `_v_isoparam` | 从张量积控制网精确提取等参数曲线。u 入口固定 u 并返回沿 v 的曲线，v 入口反之。 |
| `PK_BSURF_create_splinewise` | 用线性或自然三次插值构造通过二维点阵的张量积曲面。 |
| `PK_SURF_make_sheet_body` | 将支持范围内的解析面或 B 曲面的正则 UV 矩形构造成一面、四边、四顶点的 sheet body。 |
| `PK_BODY_make_swept_body` | 将平面有界 sheet 沿一条受支持的空间路径平行平移，构造侧面和两个端盖；支持孔环。 |
| `PK_BODY_make_lofted_body` | 在匹配的闭合平面轮廓之间构造经过连续有效性证明的自然放样实体。 |
| `PK_BODY_tracked_sweep_r_f` / `PK_BODY_tracked_loft_r_f` | 释放关联 tracking 返回内存，不删除结果 body。 |
| `PK_BODY_sew_bodies` | 匹配不同 sheet 的完整重合边，合并顶点和边，传播面方向，生成开 sheet 或闭 solid。 |

所有上述入口都通过统一 command dispatcher 执行。创建实体、修改拓扑、几何引用关系和返回内存参与事务；失败不发布临时实体句柄。

## 曲线插值与拟合

插值支持 2–128 个有限位置、1–16 次曲线，且次数小于点数。调用方可以显式给出全部参数，也可以给出部分参数；未给出的参数按已指定参数之间的累计弦长补齐。参数必须严格递增，起终点参数必须与声明的参数区间一致。

无导数约束时，程序构造平均节点向量，组装 B 样条配置矩阵并使用带主元选择的消元求解控制点。三次曲线还支持起点、终点的一阶导数约束，通过三对角系统求解二阶导数并转成分段 Bezier 表示。未指定一端导数时，该端使用自然端条件。

拟合模式采用端点约束的 Householder QR 最小二乘。控制点数从最小次数要求开始增加，直到每一个输入样本在指定参数处的距离误差均不超过 `fit_tol`；必要时退回插值规模。返回实际测得的最大样本距离和相应参数。该误差界针对输入点约束，不是对点间未知连续原曲线的误差证明。

周期插值、指定 B 样条节点约束、内部高阶导数、额外 special/break 点、clamp axes 和 chordal tolerance 暂不支持。没有用忽略约束的曲线替代这些请求。

## 曲面构造

### 精确张量积操作

匹配曲线时，在齐次坐标中完成 Boehm 插结和 Bezier 升阶。普通曲线可通过补权重 1 与有理曲线匹配。输入要求开放、夹持的有效参数区间及正有理权重；不同参数域和周期布局不被静默重映射。

线性扫掠直接构造 `C(u) + v D`。空间平行扫掠直接构造 `C(u) + P(v) - P(0)`；当轮廓或路径有理时，其齐次分子及权重由精确张量积组合得到。旋转曲面把总角度分成不超过 π/2 的圆弧段，使用正权重有理二次表示，不以折线或数值拟合代替圆。

曲线放样支持不同次数、不同内部节点和一般不同形状的截面。两条曲线默认形成一次直纹面，多条曲线形成自然三次插值；参数按截面序号均匀分布在 `[0,1]`。实现可以保留更多等价的分段 Bezier 控制点，控制网和内部节点数量不要求与 PK 的压缩表示相同。

### 点阵插值

`PK_BSURF_create_splinewise` 的位置数组按恒定 v 的行排列：`positions[j * n_u_positions + i]`。每个方向可以选择一次或三次插值。当前支持自然端条件、无 corner twist，以及自动参数或显式参数数组。

自动参数在同一方向的所有行上累加每一段的弦长，再归一化到 `[0,1]`；不是均匀参数，也不是先分别归一化每一行再平均。显式 `u_knot` / `v_knot` 数组给出各点阵行列的严格递增参数，保留其原始范围。每方向最多 32 个样本，生成控制网最多 8192 个控制点。

### 有界 sheet

矩形 sheet 使用 `PK_UVBOX_t` 的 `[u_min, v_min, u_max, v_max]` 排列。四条边由精确等参数曲线生成，曲线 use sense 和 fin 顺序与材料侧方向一致。

支持 plane、cylinder、cone、sphere、torus 和 B-surface 的正则矩形。跨整周期接缝、球极点和检测到的退化边界返回明确错误。B-surface 范围必须落在有效节点参数域内；没有把外域外推当成有界曲面的一部分。一般 B-surface 的整体有效性仍是输入前提，矩形构造入口本身不承担任意控制网的全局自交证明。

## 实体扫掠

当前实现对应 `alignment = PK_BODY_sweep_align_parallel_c`、`simplify = PK_BODY_simplify_no_c`、`topology_form = PK_BODY_topology_minimal_c`、无 twist/scale laws 和默认 fair 选项。

轮廓是一个平面 face 的 sheet body，可以含多个孔环，边界支持有顶点的 line、circle、ellipse 和开放夹持 B-curve。路径是单边开放 wire，可由上述受支持的空间曲线定义。轮廓保持原有方向，沿路径相对起点的位移平移。法向对齐模式仅在直线路径已经平行于轮廓法向时支持。

路径控制点沿轮廓法向的投影必须严格单调。有理路径要求正权重。这个条件保证不同截面位于互不相交的平行平面，构成整个路径区间上的充分有效性判据，而不是少量采样点检查。

多段 path、闭 path、沿弯曲路径旋转截面的移动标架、导轨、扭转和变截面律，以及 wire profile 产生开放 sheet 的 body sweep 暂不支持。曲线级 `BCURVE_sweep` 和矩形 sheet 构造仍可用于开放曲面建模。

## 实体放样

起终轮廓为单个平面、单环 face 的 sheet，中间轮廓为平面闭合 wire。相邻轮廓具有相同边数和顺序，起始顶点确定对应关系。多截面要求显式自然端条件；当前不替调用方选择 unconstrained、clamped 或其他端条件。支持 `simplify_no` 和 minimal topology，不支持 guide wires、显式复杂 vertex mappings、周期端条件、退化端轮廓、拓扑变化和 edge tracking。

存在两种连续有效性证明：

1. **同形缩放族。** 普通或有理边界经过精确匹配后，逐控制点验证截面之间只有正等比例缩放和平移；插值后的尺度 Bezier 系数必须全部为正，平面高度必须单调。该路径支持曲边和非凸但本身有效的截面。
2. **一般凸多边形族。** 对每一个自然三次区间，将全部顶点轨迹转成 Bezier 表示；通过无误差乘加展开验证每个控制层具有共同平面投影，再用向外舍入的六次 Bernstein 系数证明每条边的所有其他顶点始终位于正确半平面。严格凸性与严格单调的平面高度共同证明整个实体不自交。这允许宽度、高度、斜边方向和位置分别变化，不限于同形缩放。

第二种证明是充分条件。边界处接近共线、浮点表示不能证明公共截面平面、或 Bernstein 凸包不能证明严格正号时，程序会拒绝该路径；不会把“没有找到相交”当作有效性证明。没有被上述两种条件覆盖的一般曲边截面族保持显式未实现。

原生 PK 38.0.150 对带内环的 sheet 端轮廓返回 `PK_BODY_loft_bad_profile_type_c`，两截面和三截面、是否提供完整 vertex mappings 均一致。当前公开放样入口保持这个状态协议：全局调用返回正常，但 `status.fault` 为该值，`fault_topol` 指向第一个不合法的轮廓，`body` 为零。带孔截面的空间平行扫掠已经独立验收。

## 完整边缝合

缝合支持由解析面和 NURBS 面组成的 manifold sheets，以完整重合边建立配对。匹配先检查端点，再将有界曲线精确转成共同的有理 Bezier 布局，利用正权重和控制点凸包给出全区间距离界；不凭离散采样接受不同的曲线。

顶点仅在已配对边的端点处合并。面方向沿配对图传播，合并边的几何由目标 sheet 按边界顺序扩展时已保留的那一侧持有。闭合分量按有向体积定向后形成 solid，开放分量保持 sheet。原目标 body 的公开句柄保留，其余已经缝入的输入 body 被消耗。

当前不做实际间隙修复。匹配精度上限为 `1e-9`；较大的 `max_gap` 不会导致任意拉伸边界。部分重合边、需要 imprint 的 T 接头、重叠面删除、非流形结果、多体 assembly sewing、容差缩减、无顶点闭圆边和 fin-only tolerant 输入不在这条路径的范围内。无法完整匹配的输入保留为 unsewn；歧义和非流形配对返回错误。

输出的 sewn / unsewn 数组使用 `PK_MEMORY_free` 分别释放。释放数组不删除 body。

## 容差、tracking 和所有权

实体扫掠和放样按原生 PK 的 tolerant topology 形式导出。边容差为请求容差 `t`；顶点容差包含 `2e-8` 的分辨率分量，使用防溢出的二范数计算。每个真实 fin 具有独立的 trimmed SP-curve，SP-curve 引用该面的支持面和二维 B-curve。不存在 nominal edge curve 时不填入伪造的空间曲线。

fin 的顶层 trimmed curve 由 HALFEDGE 拥有；SP 曲线、二维曲线和支持面的依赖闭包随 body 一起导出。导入、复制、变换和删除保持这些关系。临时构造曲线在建立 fin 参数曲线后释放。

放样的侧面 tracking 记录关联每一个输入轮廓上对应的 edge，端盖关联输入 face。扫掠侧面记录同时关联 profile edge 与 path edge，端盖记录关联 profile face 与路径的相应端点 vertex。返回记录与数组置于一个返回内存块内，通过相应 `_r_f` 释放。

`PK_TOPOL_detach_geom` 可留下没有几何的合法拓扑。构造和缝合入口在索引任何几何池以前检查所需的顶点坐标、空间边曲线和支持面。缺失几何及尚不支持的 fin-only 编辑输入返回明确错误，不越界读取、猜测坐标或发布半成品。

## 验证

`tests/KernelTests/FreeformModelingTests.cs` 覆盖非均匀插值、导数约束、最小二乘误差、精确曲线匹配、普通/有理张量积、点阵参数语义、等参数曲线、单环及带孔扫掠、非同形凸截面放样、连续塌缩拒绝、native fault 协议、tracking、内存释放、故障注入和脱开几何后的保护。

`scripts/FreeformModelingOracle.cs` 包含独立的 self / native 两条生产路径。每一例都分别调用自己的目标 API，再将自研结果保存为新的文本 XT 并由真实 PK 接收。通过条件包括：

- 两条生产路径的默认完整 `PK_BODY_check`。
- region、shell、face、edge、vertex 五类拓扑数量。
- `PK_DEBUG_BODY_compare` 的类型、sense、容差、missing 和 topology 项。
- 输入点插值或拟合的实际几何误差，以及放样/扫掠 tracking 的来源类型与数量。
- 自研和原生 tolerant XT 的 self receive → copy → 删除原件 → 平移副本 → self transmit → PK receive/check/compare → delete，检查几何池无泄漏。
- 多环放样的失败状态与 fault topology 一致。

本轮独立验收通过 16 个正向建模场景、4 个 tolerant XT 生命周期往返，以及两截面和三截面两组多环放样拒绝协议对照。非同形放样的严格 PK 对照使用宽度和高度分别变化的三个矩形截面；一般凸斜四边形的连续有效性由单元测试检验。不同斜截面组合可能触发 PK 自身不同的拟合和容差分配，不把这些情形的原生几何逐点一致性概括为已经完成。

不同但等价的 B 样条控制网或曲面参数化不要求逐字节相同。默认 BODY_check、拓扑、类型、sense、容差和缺失几何检查不关闭，也不把失败改记为通过。

输出保存在 `temp_docs/freeform-modeling-oracle/`。在引用的 Native DLL 已经更新时，应禁用文件应用缓存，避免读取旧的引用副本：

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/FreeformModelingOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```

并行验收期间若主构建已负责重新生成 Native，可加 `-p:BuildProjectReferences=false` 使用刚生成的程序集。
