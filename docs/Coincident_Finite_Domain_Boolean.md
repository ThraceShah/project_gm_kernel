# 同域有限面与实体布尔

## 当前交付

`SurfacePairRelation.CoincidentArea` 表示二维支持几何相同，不能作为无交线的空结果使用。实体布尔对这些支持面的真实有限边界求交、分段和排列，再以两侧材料状态决定保留、反向或删除面。重合单元只由一个输入所有者输出；同域边以完整曲线区间覆盖关系共享，保持真实支持曲面与交线，未作拟合替换。

已验收相同自由曲面实体的并、交、差，以及同一自由曲面支持上的不同裁剪域覆盖；工具可先经自研 XT 保存重载。相同完整实体的并与交保留一个闭合实体，差为空；不同裁剪域则真实分裂边界、分类和组装，不按几何同域直接返回原体。基础测试同时检查点分类和实体删除后的几何依赖释放。输入几何不依赖 tag、测试模型名称或坐标匹配；支持关系由相同样条定义、严格解析同域关系及受认证的平面支持判定取得。

相关入口：

- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.SurfacePairRelations.cs`
- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularCurvePairRelations.cs`
- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBoolean.cs`
- `tests/KernelTests/IntersectionEventBooleanTests.cs`
- `scripts/RegularCurvedBooleanOracle.cs`

## 有限域及失败合同

有限边界排列保留 `merge_imprinted=false` 的印刻边。完整区间包含由曲线求交的 Start/End 事件证明；ICurve 同载体的不同 CHART 区间，仅在支持约束相同且代数交线为唯一单调分支时映射。不能证明的曲线重合、奇异分支或排列不返回部分成功。

源顶点已有局部精度以合并顶点中的最大值传播。原顶点落入另一体边内部、所有原始 incident 支持面均为平面、且保留单元实际采用跨体连接或更换来源时，应用独立的 PK38 平面 imprint 合并精度元数据策略：`RegularPlanarImprintMinimumPrecision=1e-7`，仍保留更大的既有源精度。它不是几何拟合容差或 `10*session_precision`；数值求交与几何残差的验收没有放宽。

此交付不宣称任意重参数化曲面等价、所有不同裁剪域、任意高重数接触或精确切触拓扑均已解决。点接触、曲线接触与面域重合使用不同关系；未接入的事件仍明确失败。

## 精度元数据的独立制造证据

`scripts/RegularCurvedBooleanOracle.cs --precision-evidence` 只在真实 PK 中运行 48 组制造实验，分别改变并读回会话线性精度 σ、改变最终布尔 `default_tol`，并改变两个输入的统一尺度及平移。两个输入先经过普通横截切削，检查这些输入顶点仍准确，再执行真正的同域有限域叠加。

| 变化量 | 实测范围 |
|---|---|
| 会话线性精度 σ，调用后读回 | `1e-9`, `1e-8`, `1e-7`, `1e-6` |
| 最终布尔 `default_tol` | `1e-9`, `1e-8`, `1e-5`, `1e-3` |
| 几何缩放及等比例平移 | `.125`, `1`, `8` |

48/48 均通过完整 PK BODY_check；两个新平面 imprint 焊接点的 `PK_VERTEX_ask_precision` 为 `max(σ/2,1e-7)`。普通横截输入的全部顶点、输出中两个真正 NURBS junction 以及所有 incident 边仍报告 accurate 基准 `σ/2`，因此不能把新增顶点的最小值归因于源 tolerant edge、请求容差或与 σ 等比的规则。σ/2 是 accurate 实体的查询基准，不是一个需要写进每个顶点的显式 tolerance。自研当前默认 σ 未改动；新增策略仅写该特定拓扑事件的局部元数据。

这是 PK38 的独立兼容策略及可重跑的经验合同，不冒充官方给出的通用数值误差定理。测试中的坐标用于识别制造解预期的事件和反例；运行时按 incident 支持关系和实际单元来源判断，不含这些坐标。

## 验证

Release 托管同域测试 12/12，覆盖完整域和不同裁剪域的三种布尔、两种序列化状态，以及新 imprint 元数据和仍准确的 NURBS junction 反例；连同既有规则/解析/点接触回归，定向 69/69 通过。Release 真实 Parasolid oracle 45/45 通过，包含既有 33 例及新增同域 12 例。两侧分别独立构造并执行同一布尔 API；自研结果经 text XT 接收、完整 `PK_BODY_check`、拓扑与 region/shell、`PK_DEBUG_BODY_compare` 的类型、sense、容差、missing 等结构条件检验。CHART 点数差异继续只作诊断。

没有 C ABI 改动，因此不触发 NativeAOT 发布。
