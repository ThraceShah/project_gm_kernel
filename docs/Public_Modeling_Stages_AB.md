# A、B 阶段：公开建模与轮廓实体化

日期：2026-11-03。验证环境为 Linux x64、.NET SDK 10.0.403、真实 Parasolid 38.0.150。

## 阶段目标与实现边界

本阶段把已有几何求值接入公开创建、查询、复制和变换接口，修正 XT 导入的实际几何与拓扑，新增从闭合轮廓到带孔实体的自研建模路径，并接通 Parasolid 显示桥和原始实体拾取映射。

自研内核负责建模结果及其几何、拓扑、标识、内存与事务。真实 Parasolid 用于两件事：作为独立的兼容性对照；从自研导出的 XT 生成显示三角形与边折线。模型构造不会调用真实 PK 来代替自研算法。

| 阶段 | 可用流程 | 详细说明 |
| --- | --- | --- |
| A：已有几何公开化 | create → ask → 参数域 → evaluate → copy / transform | `docs/Geometry_Public_API.md` |
| A：正确导入 | PK XT → 实际运行时 B-rep → 查询、求值、深复制 → fresh XT | `docs/xt_brep_materialization.md` |
| A：显示与拾取 | 自研 body → XT 与 identifier map → real PK facet / line render → 原自研 face / edge | `docs/Display_Bridge.md` |
| B：闭合轮廓与有界面 | 有界曲线 → wire → 外环与孔环 → 附着平面，或直接创建带孔 polygon sheet | `docs/Profile_Modeling.md` |
| B：轮廓到实体 | 带孔 sheet → 正向或斜向拉伸，或正反方向局部 / 整周旋转 → caps、lateral faces、region / shell | `docs/Profile_Modeling.md` |
| B：结果检查 | 自研结构检查；独立真实 PK 完整 `BODY_check` 与结构对照 | `scripts/ProfileModelingOracle.cs` |

## 公开接口

几何接口补齐 ellipse、trimmed curve、SP curve、B-surface、offset、swept、spun 的相关创建和定义查询，以及 `PK_POINT_ask`、`PK_CURVE_ask_interval`、`PK_SURF_ask_uvbox`。已有解析几何和 NURBS 求值保持独立实现。修剪曲线的公开构造入口采用 SDK 实际提供的 legacy `CRTRCU`，其原生参数从零开始；不虚构一个 SDK 没有的 `PK_TRCURVE_create`。

管理接口包括 `PK_PART_ask_geoms`、`PK_ENTITY_copy`、`PK_ENTITY_copy_2`、`PK_TRANSF_ask`、`PK_GEOM_transform` / `_2`、`PK_BODY_transform` / `_2`。复制保留共享依赖，但新旧实体不共享可变几何 payload。批量 `new` 变换也使用同一个复制映射，显式列出的 basis 与派生几何内的 basis 保持同一身份。变换支持平移、旋转、均匀缩放和反射，并处理反射后的原生参数、offset 方向和面 / 边遍历方向。原地几何批量变换要求显式提供完整依赖及所有者闭包。

轮廓建模入口包括 `PK_CURVE_make_wire_body` / `_2`、`PK_EDGE_make_faces_from_wire`、`PK_BODY_create_sheet_planar`、`PK_BODY_extrude`、`PK_BODY_spin`、`PK_BODY_check`。结果查询补齐 edge vertices、face edges / oriented surface、region shells、body type / config 等公开语义。extrude 的 tracking / local 结果通过 `PK_TOPOL_track_r_f` 和 `PK_TOPOL_local_r_f` 释放。

显示扩展导出 `PGM_BODY_export_display`、`PGM_DISPLAY_validate_pick`、`PGM_DISPLAY_snapshot_free`，并提供相应 C 头文件。它们是本项目扩展，不宣称是 Parasolid 的同名 API。

## 正确性修复

- XT 接收不再按面数或类别猜一个标准 primitive：实际姿态、尺寸、曲线参数、NURBS 控制数据、孔环、face use、dummy halfedge 和 pole loop 均从源图恢复。
- 共享的定义几何、topology-attached geometry 与 body-owned construction geometry 分别管理引用和归属。新增依赖对象不会因未附着到 face / edge 而从复制、变换、导出或删除路径中消失。
- 公开 `PK_BODY_ask_topology` 区分 `want_fins`，恢复真实 SDK 的关系方向，隐藏内部 dummy / pole fins，并对闭合边的相同两端顶点只输出一次关系。
- 尖锥的退化顶点环归属于 cone side face；棱柱侧面的平面坐标系使用单位轴，底面与侧面的 fin 方向保持连续。
- 整周 NURBS 旋转用精确有理圆弧构造周期方向，并验证 clamped seam。带孔整周旋转显式构造独立的内部 void region 与 shell。
- 深复制保留 procedural 的原生范围、方向及辅助字段，重新分配全部变量长度 payload；源 body 删除后副本仍可求值和导出。
- 显示实体映射使用同一次 XT 写出产生的持久 identifier。它不依赖 PK 实体枚举次序或按坐标猜测对应关系。删除 / session restart 后的过期拾取会被拒绝。
- GO 折线回调按 `3 * ngeom` 读取 XYZ 数据，避免圆形路径只保留前三分之一；整圆覆盖和弦高误差有独立检查。

## 最终验收结果

统一入口是 `scripts/VerifyPublicModeling.cs`。它默认顺序运行单元测试、XT 测试、公开几何与变换对照、导入对照、建模对照、双向组合导入和托管显示桥。只有涉及 C ABI 改动或用户明确要求时，才添加 `-- --native-abi`，执行 NativeAOT 发布、ABI smoke 和真实 C 导出显示 / 建模闭环。任意阶段失败都会停止并返回非零退出码，不使用清理命令掩盖失败。下表保留此前包含原生验证的完整基线证据。

| 门禁 | 当前证据 | 检查内容 |
| --- | --- | --- |
| 内核单元测试 | 910 / 910 通过，0 跳过 | 新旧 API、几何、拓扑、内存、分配失败、事务及生命周期回归 |
| XT 单元测试 | 12 / 12 通过，0 跳过 | 独立 XT 读写库回归 |
| 公开几何契约 | 全部通过，含 14 项 periodic 正负控、4 项归属对照与 7 类 writer 验收 | 原生定义和参数域、完整数组、几何归属、零起点 TR 参数与 PK 规范化后的对照 |
| 几何复制与变换 | 83 / 83 通过 | 原生 D0–D2、批量依赖共享、反射方向、construction geometry、源实体不变、真实 PK 完整 body 检查 |
| B 建模 | 33 / 33 通过 | 独立 API producer、fresh XT → PK、五类拓扑计数、完整 `BODY_check`、类型 / sense / tolerance / missing、extrude tracking |
| 独立 PK 导入 | 13 / 13 通过 | 六类有姿态 primitive、孔环、B-surface / swept / spun sheet、尖锥、辅助曲面与共享依赖链 |
| 自研 B 输出再导入 | 33 / 33 通过 | 实际实体化、两种拓扑查询、深复制、删除原体后求值与 fresh export、池与 payload 回基线 |
| PK B 对照输出导入 | 33 / 33 通过 | 同上，包含真实 PK 的 clamped-periodic NURBS 整周旋转 |
| 托管显示桥 | 已通过 | 8 类流程、PK identifier、viewer cell 映射、每个显示面的 ray pick、线框边拾取、删除及 session 生命周期 |
| NativeAOT 与 C ABI | 已通过 | Linux x64 发布、1510 个生产方法的零分配 IL 守卫、124 个实现分派描述符、实际 C 导出 ABI smoke |
| 原生建模与显示闭环 | 已通过 | C API 创建 block；带孔斜向拉伸、深复制、删除原件、变换；带孔整周旋转；真实 PK 完整检查、显示、原实体拾取及结果释放 |

2026-11-03 的最终统一运行完成全部 **14 个步骤**，每步退出码均为 0，入口最终返回 0。持久验收记录为 `docs/reviews/public-modeling/stages-ab-validation-2026-11-03.txt`。脚本原始输出位于 `temp_docs/public-modeling-verification/`、`temp_docs/profile-modeling-oracle/`、`temp_docs/brep-import-oracle/`、`temp_docs/display-bridge-oracle/`。

原生显示检查的三个结果分别包含 26、50、32 个可追溯到自研实体的绑定；两个带孔实体分别经真实 PK 生成 32、556 个显示三角形。验证同时检查每个显示面的拾取归属及删除后的过期标识拒绝，不以“成功生成网格”代替实体映射验收。

本轮也修正了验收脚本的原生启动环境：CoreCLR 内修改环境变量不会自动更新独立 NativeAOT 运行时的环境缓存。统一入口会在启动子进程时传入规范化的 schema 目录，两个独立原生检查脚本也可自行完成必要的一次启动准备。生产 `PK_SESSION_start` 的 schema 契约保持不变。

## 复现

准备 .NET 10、适合当前平台的 NativeAOT 工具链，以及项目已配置的真实 Parasolid SDK。SDK 与授权材料不进入 Git。

入口优先采用 `PARASOLID_SCHEMA_DIR`，其次采用 `P_SCHEMA`；未设置时使用项目的 `third_party/parasolid/schema`。显式相对路径按脚本位置解析。

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/VerifyPublicModeling.cs
```

只运行轮廓建模对照：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ProfileModelingOracle.cs
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/BrepImportOracle.cs -- ../temp_docs/profile-modeling-oracle
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/BrepImportOracle.cs -- ../temp_docs/profile-modeling-oracle --reference
```

目录参数相对于脚本文件解析。运行真实 SDK 对照脚本时，`PskernelSharp` 的 `pskernel` 始终指向真实 Parasolid；自研托管入口由项目引用调用，只有明确的 ABI 门禁加载自研 native library。

## 尚未覆盖的范围

本阶段具有明确的可用子集，不能等同于所有 Parasolid 建模 API 已完成。

- 一般剪切与非均匀仿射缩放、高级 copy attributes / groups / tracking / destination 选项尚未实现。
- 轮廓必须为一个平面 face 的外环和孔环；NURBS / 椭圆轮廓的可接受范围由保守的控制凸包与单调性检查确定。任意曲线排列、自交分割、多区域自动识别尚未实现。
- 拉伸限于距离边界，包括斜向拉伸；到 face / body 的限界拉伸与多结果建模尚未实现。
- 旋转轴必须在轮廓平面内，且整个轮廓严格位于轴的一侧。碰轴或穿轴需要奇异拓扑分割，尚未实现。
- 通用 INTERSECTION 多面 B-rep、tolerant fin pcurve 及其他未识别 XT 图保留为 opaque；保存原文件不意味着它们可编辑或可绑定自研 face / edge。
- 自研 `PK_BODY_check` 提供明确的结构检查子集；完整几何合法性是独立真实 PK 的验收门禁，不会把未实现的检查静默当作成功。
- C 阶段的求交分支、imprint / split / trim / 分类 / 布尔结果构造，D 阶段的一般自由曲面建模，E 阶段的圆角 / 倒角 / 偏置实体 / 抽壳 / 非流形仍是后续工作。
- 已存在的 parametric ICurve 原始 D2 与 PK 参数约定差异仍单独记录于 `docs/reviews/icurve-evaluation/parasolid-sdk-validation-2026-11-02.md`；本阶段没有放宽该门禁或把它声明为通过。
