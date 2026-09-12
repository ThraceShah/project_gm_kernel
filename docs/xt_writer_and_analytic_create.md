# 解析几何 create/ask 与 XT writer 扩展

本次实现覆盖两条线：为六种解析几何补齐公开 create/ask 入口，并把 XT writer 的
写出能力补到与求值能力对齐。所有兼容行为均以真实 Parasolid V38（linux-x64
`libpskernel.so`）实测为基准，探针脚本随仓库提交，可复现。

## 公开 create/ask 入口

新增手写导出（不再由生成器产生占位）：

- `PK_LINE_create` / `PK_LINE_ask`
- `PK_CIRCLE_create` / `PK_CIRCLE_ask`
- `PK_PLANE_create` / `PK_PLANE_ask`
- `PK_CONE_create` / `PK_CONE_ask`
- `PK_SPHERE_create` / `PK_SPHERE_ask`
- `PK_TORUS_create` / `PK_TORUS_ask`

实现位于 `src/ProjectGmKernel.Native/Runtime/KernelRuntime.GeometryCreate.cs`；
入口、命令结构与 ApiId 按既有模式接线。至此函数目录共 83 个分发描述符，
生成占位导出降至 1107 个。

### 错误合同（实测 Parasolid V38）

| 输入 | PK 错误 |
|---|---|
| axis / ref_direction 非单位向量 | `PK_ERROR_not_a_unit_vector` |
| ref_direction 与 axis 不正交 | `PK_ERROR_vectors_not_orthogonal` |
| circle/sphere/torus radius ≤ 0 | `PK_ERROR_radius_le_0` |
| cone radius < 0（0 允许） | `PK_ERROR_radius_lt_0` |
| cone semi_angle ∉ (0, π/2) | `PK_ERROR_bad_angle` |
| torus minor > major（apple/lemon） | 接受 |

直线参数区间按 PK 行为取 ±1e4；平面 UV 区间同。探针见
`scripts/ProbeXtGeometryNodes.cs`（另输出各类型几何的参考 x_t）。

## XT writer 补齐

`src/ProjectGmKernel.Native/Runtime/XtWriter.cs` 新增写出：ELLIPSE、
TRIMMED_CURVE、SP_CURVE、B_SURFACE（含 NURBS_SURF、SURFACE_DATA、
BSPLINE_VERTICES、KNOT_SET、KNOT_MULT）、SWEPT_SURF、SPUN_SURF、OFFSET_SURF。
至此 writer 覆盖全部已实现求值的曲线/曲面类别（交线、blend 面、foreign
geometry 仍按 `NotSupportedException` → `PK_ERROR_wrong_version` 拒绝）。

### 依赖几何的传输形态（对齐 Parasolid 规范形式）

依赖几何（trimmed 基曲线、SP curve 的支持面与 2D B-curve、swept 截线、spun
母线、offset 基面）没有拓扑属主，transmit 需要显式处理。以下形态逐一对照
Parasolid 自身 transmit 的文件确认（`scripts/ProbeTrimmedReference.cs`、
`scripts/ProbeSpCurveReference.cs`）：

- 依赖节点仍串入 body 的 boundary 几何链（链尾追加），`owner` 写 body 节点；
  `owner=0` 的链成员会被 PK receive 以 `PK_ERROR_corrupt_file` 拒收。
- 每个无属主依赖节点挂一个 GEOMETRIC_OWNER 节点（XT node 141：
  `{owner: 依赖者, next/previous: 自环, shared_geometry: 依赖节点}`），
  依赖节点自身的 `geometric_owner` 指向该节点。缺失同样导致 receive 922。
- SP curve 的 2D B-curve 是例外：PK 以漂浮节点传输（不进链、`owner=0`、
  `node_id=0`、无 GEOMETRIC_OWNER）。
- SURFACE_DATA 持久字段按 PK 对新建 B-surface 的输出填充：UV 整数边界
  （floor/ceil）、`self_int` 编码为 `PK_self_intersect_* - unset + 1`
  （unset→1, false→2, true→3）、original 范围标记 'B'。`NURBS_CURVE` 的
  `CURVE_DATA.self_int` 同步修正为该编码。

### 语义修正

`PK_OFFSET_create` 产生的 offset 面的 check 状态由 'U' 改为 'V'，与 PK
对新建 offset 的传输一致。

## 验证

- 单元测试：`tests/KernelTests/AnalyticGeometryApiTests.cs`（create/ask
  回读、错误合同、错误类别）、`tests/KernelTests/XtGeometryWriterTests.cs`
  （每类几何挂接到体上传输，断言 schema 节点、依赖指针、链可达性与漂浮
  形态）。213 项 KernelTests 全部通过。
- Parasolid oracle：`scripts/XtGeometryWriterOracle.cs`（已接入
  `scripts/VerifyKernel.cs`）。对每个夹具在两个内核构建同样的体、以同样的
  detach + attach 手术挂接几何、传输文本 x_t、真实 Parasolid receive，并与
  Parasolid 参考体做 `PK_DEBUG_BODY_compare`；随后在两个内核以相同参数求值
  所挂几何（阈值 1e-11）。输出保存在 `temp_docs/xt-geometry-writer-oracle/`。
- 夹具一致性说明：边挂接夹具（ellipse、trimmed、spcurve）以
  `all_tests=0` 的严格 compare 通过。面替换夹具（bsurf、swept、spun、
  offset）中，PK 的 `PK_FACE_replace_surfs` 会按新面重算边曲线（并使 1×1
  双线性 B-surface 触发 `PK_ERROR_face_check_fails`，故用 3×3 次平面
  Bézier），而我们的 attach 只换面几何——两者的边曲线集合必然不同。按
  AGENTS.md 允许的降级规则，这类 compare 差异降级为诊断输出；receive、
  收到体的拓扑计数与我们内核一致（faces/edges/vertices）、收到几何的类别
  与数值求值一致作为通过条件。

## 顺带修复的既有验证门问题

以下问题在本次改动之前的 HEAD 上即已存在，导致 `VerifyKernel` 无法走完，
为验证本次工作做了最小修复：

- `scripts/GenerateXtSchemaDifferences.cs` 缺少 `XtSchemaDefinition` 等
  using 别名，无法编译；补齐别名后重新生成了过期的
  `tests/ParasolidXtCorpus/coverage/xt-schema-differences.json` 与
  `docs/xt_schema_differences.md`（新增 V380 schema 的零差异过渡行）。
- `scripts/ParasolidAllSchemaOracle.cs` 对嵌入 `<built-in>` schema 仍尝试
  读取磁盘源文件计算哈希并崩溃；现以文件名派生的哨兵哈希记录，并在
  `xt-schema-support-report.json` 中反映（102 个 schema：78 verified、
  24 blocked、0 failed）。
- `scripts/ParasolidSchemaCorpusMatrix.cs -- --check` 在 HEAD 上即返回 1：
  提交的矩阵报告本身带 `failedCount=15949`（无生成模型绑定的旧 schema
  case 被计为 Failed），与门禁 `failed == 0` 永远冲突。该状态属于
  xt_all_versions_support 工作流，未在本次修复；当前矩阵报告已重新生成，
  数字与提交状态一致。

## 已知边界

- 交线（icurve）、blend 面、foreign geometry 仍不可求值、不可创建、不可
  传输；cpcurve 求值未实现。
- 接收侧类型化物化仍只覆盖基本体；含依赖几何的 body receive 后为 opaque
  part（文档级往返不受影响）。
