# 平面建模的 tracking 与导入表示验收

`scripts/PlanarTrackingOracle.cs` 使用独立的自研 API 与 Parasolid API 生产结果，验证 `PK_BODY_boolean_2` 的 tracking 开关、`PK_BODY_imprint_plane_2` 返回结构，以及导入后局部修改的曲线方向处理。此脚本补充几何比较，不能替代一般曲面布尔的实现或验收。

## Tracking 的实际含义

### 平面布尔

启用 `tracking` 后，当前支持的平面实体布尔为结果交线返回 `PK_TOPOL_track_derive_c`。源拓扑可以是 face 或 edge，记录的 `original_classes` 必须对应操作前的类型。布尔会消耗输入，源句柄可能在操作后失效；验收先缓存全部输入 face、edge、vertex 的类型和几何身份，再检查返回记录，避免把已失效源句柄误认为错误。

每个返回 product 必须是当前仍然有效的实体。验收同时比较记录类型、源实体所属输入体、源几何、源类型、产物几何、记录内的数量以及重复记录数量。仅比较 tracking record 总数不能发现源面映射错误。

### 平面 imprint

真实 SDK 的 `PK_BODY_imprint_plane_2` 在有截线时返回一个 `PK_TOPOL_track_create_c` 记录，零个 original，所有返回截线 edges 列入 products。该集合包括切平面上已有的共面边，因此不能只列“新生成的边”。例如：

| 输入与平面 | 记录数 | Product edges |
| --- | ---: | ---: |
| 方块中间的水平切面 | 1 | 4 |
| 经过两个原边的斜切面 | 1 | 4，其中两条已经存在 |
| 平面 sheet 的内部切线 | 1 | 1 |
| 与方块底面重合的切面 | 1 | 4，全部已经存在 |

平面与 body 不相交时，SDK 返回 `PK_ERROR_partial_no_intersect`，tracking 为空且原 body 保留。验收将此情况作为负向 API 合约，不把它计作成功但空结果的建模操作。

## 导入的负向曲线 use

边的正向与附着曲线的参数增大方向是两个独立概念。合法 XT 可以让一条边负向使用其直线；其两个拓扑端点投影到原始直线参数后会得到递减顺序。复制或 trim 需要保存递增的几何参数区间，同时保留 curve use sense、edge 端点和 fin 方向。

专门的回归例通过公开 PK API 创建并 imprint sheet，再用 `PK_EDGE_attach_curves_2` 的 `have_senses=true` 和 `senses[0]=false` 附着反向参数化直线。脚本先断言 `PK_EDGE_ask_geometry` 确实返回负向，并要求完整 `PK_BODY_check` 无故障，随后将真实 PK XT 导入自研内核，在两条路径分别 trim，最后比较结果。测试没有直接修改内部记录或 XT 文本。

## 验收范围与通过条件

33 个案例包含 27 个启用 tracking 的平面布尔、5 个 modern imprint 合约，以及上述导入/trim 回归。布尔案例覆盖并、交、差中的重合、两方向重叠、轴对齐重叠、面接触、分离、旋转工具、封闭空腔、贯通孔和两个工具。

每个正向结果依次满足：

1. 两条独立 API 路径的状态、结果体数量和完整 tracking 几何签名相同。
2. 自研结果重新导出为 text XT，并由真实 PK receive。
3. 两个结果的 region、shell、face、edge、vertex 数量相同。
4. 独立 PK 基准和自研导入结果均通过完整 `PK_BODY_check`。
5. `PK_DEBUG_BODY_compare` 的 topology、class、sense、tolerance 和 missing 项严格通过。此脚本不采用 sense 规范化路径。

原始参考 XT、自研 XT、tracking 差异和比较日志写到 `temp_docs/planar-tracking-oracle/`，不提交私有 SDK 或文档。

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/PlanarTrackingOracle.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
```
