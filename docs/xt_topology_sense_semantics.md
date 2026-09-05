# XT 拓扑-几何方向（sense）的存储与派生机制

日期：2026-09-05。依据 Parasolid v380《XT Format Reference》（下称 XT）与《PK Functional Description》（下称 FD）。本文回答一个问题：fin 与 curve 之间的方向关系记录在哪里，是否与 fin↔edge 的 sense 共用字段。

## 1. 结论

不共用字段。XT 中存储的 sense 有三组，彼此独立；fin 与它自己的 pcurve（容差边的 trimmed SP-curve）之间**没有存储字段**，方向关系是查询时派生的属性，由几何约束保障。

| 方向关系 | 存储位置 | 性质 |
|---|---|---|
| fin ↔ edge | halfedge 节点的 `sense` char（fin 上唯一的 sense） | 存储 |
| edge ↔ curve | curve 节点自己的 `sense` char | 存储 |
| face ↔ surface | `face->sense` 与 `surface->sense` 的组合 | 存储 |
| fin ↔ pcurve（仅容差边） | 无字段 | 查询时派生 |

## 2. 存储的 sense

### 2.1 fin ↔ edge

halfedge（PK 中称 fin）节点只有一个方向字段：`sense`（XT §5.3.9）——'+' 表示 fin 方向与所属 edge 平行，'-' 表示反向。fin 的 forward vertex 与 sense 一致：sense 为 '+' 的 fin 指向 edge 的 end vertex。

### 2.2 edge ↔ curve

edge 上没有方向字段。XT §5.2.9 明确："The edge/curve orientation is stored in the **curve->sense** field"。所有曲线节点（LINE/CIRCLE/ELLIPSE/B_CURVE/TRIMMED_CURVE/SP_CURVE 等）共享公共字段 `sense`（XT §5.2.1 的 ANY_CURVE 公共字段）。curve 的"自然切向"是参数递增方向，`sense` 为 '-' 时取反。

注意：多条 edge 可共享同一 curve（经 `next_on_curve`/`previous_on_curve` 链），此时它们共享同一 `sense`，即共享曲线的 edge 之间方向关系相同；需要相反方向时 Parasolid 使用独立的曲线节点或 trimmed curve。

### 2.3 face ↔ surface

XT §5.2.9：face 法向与 surface 自然法向（dP/du × dP/dv）平行，当且仅当 `face->sense` 与 `surface->sense` 同为 '+' 或同为 '-'。

## 3. fin ↔ pcurve：派生而非存储

fin 的 `curve` 字段仅在容差边（tolerant edge / local precision edge）时非空，指向 trimmed SP-curve（XT §4.3.9）。这个方向关系在 XT 数据中**没有存储位置**：

- halfedge 上只有 fin↔edge 的 `sense`；
- pcurve 节点上的 `sense` 是几何内部的：TRIMMED_CURVE.sense 相对其 basis curve，SP_CURVE.sense 相对其 2D b_curve（XT §5.2.1），与 fin 无关。

派生机制（FD ch.16 §15.7.6）：

> PK_EDGE_ask_oriented_curve 与 PK_FIN_ask_oriented_curve 都返回两个值：底层 curve，以及一个 flag（edge/fin 方向相对 curve 切向是平行还是反平行）。

flag 由查询时推导得出，依据是 XT §4.3.9 的几何一致性约束——pcurve 两端必须落在 fin 对应顶点的 vertex tolerance 内。因此取 fin 的 forward vertex 与 pcurve 端点做几何对应，即可判定 pcurve 参数方向与 fin 走向是否一致。存储层不需要、也没有位置放这个方向。

相关佐证：XT §5.3.4.1 规定 "Fins do not share curves"——每条 fin 的 pcurve 独立存在，正是为了让每条 fin 可以有自己的参数化方向，而不需要额外的 per-fin 方向位。

## 4. 本项目内核的对应实现

| XT 概念 | 内核位置 | 说明 |
|---|---|---|
| halfedge.sense | `FinRecord.Sense`（`src/ProjectGmKernel.Native/Runtime/TopologyRecords.cs`） | fin↔edge，'+'/'-' |
| curve.sense | `CurveRecord.Sense`（`src/ProjectGmKernel.Native/Runtime/GeometryRecords.cs`） | edge↔curve；当前只建精确边，curve 按边方向构造，恒 '+' |
| fin 的 pcurve | `FinRecord.Curve`（`CurveTag`，无方向字段） | 与 XT 一致，正确；当前恒 -1 |

后续支持容差边时：

- 为每条 fin 构造独立的 pcurve 节点，节点带自己的 `sense`（相对 basis B-curve）；
- fin↔pcurve 方向不加字段，按第 3 节机制派生（或等价地，在构造时保证 pcurve 与 fin 同向，并保持端点-顶点对应约束）；
- 写 XT 时不写任何 fin↔pcurve 方向字段。

## 5. 文档出处

连接维护补充：fin 的 `other` 是绕 edge 的有序环，不是空指针终止链；普通双 fin 边的两个 `other` 互相指向。无顶点的 ring edge 也必须维护此环。与此不同，shell 上 back/front face 的 XT 链应当空指针终止，不能直接输出运行时 face-use 环。参见 XT §4.3.10 与 §5.3.9，及 `tests/KernelTests/XtTopologyLinkTests.cs` 的回归检查。

- XT Format Reference：§4.3.8 Loop、§4.3.9 Halfedge、§5.2.1 Curves（公共 `sense` 字段）、§5.2.9 Curve/surface senses、§5.3.4.1 Attaching geometry to topology、§5.3.9 Fin。
- PK Functional Description：ch.16 §15.7.6 Edge/fin orientation flag、§15.8 Representation of exact and tolerant edges。
