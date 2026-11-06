# 同域有限面与实体布尔

## 当前交付

`SurfacePairRelation.CoincidentArea` 表示二维支持几何相同，不能作为无交线的空结果使用。实体布尔对这些支持面的真实有限边界求交、分段和排列，再以两侧材料状态决定保留、反向或删除面。重合单元只由一个输入所有者输出；同域边以完整曲线区间覆盖关系共享，保持真实支持曲面与交线，未作拟合替换。

已验收相同自由曲面实体的并、交、差，以及工具经自研 XT 保存重载后再执行三种操作。并与交保留一个闭合实体，差为空。基础测试同时检查实体删除后的几何依赖释放。输入几何不依赖 tag、测试模型名称或坐标匹配；支持关系由相同样条定义、严格解析同域关系及受认证的平面支持判定取得。

相关入口：

- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.SurfacePairRelations.cs`
- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularCurvePairRelations.cs`
- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBoolean.cs`
- `tests/KernelTests/IntersectionEventBooleanTests.cs`
- `scripts/RegularCurvedBooleanOracle.cs`

## 有限域及失败合同

有限边界排列保留 `merge_imprinted=false` 的印刻边。完整区间包含由曲线求交的 Start/End 事件证明；ICurve 同载体的不同 CHART 区间，仅在支持约束相同且代数交线为唯一单调分支时映射。不能证明的曲线重合、奇异分支或排列不返回部分成功。

源顶点已有局部精度以合并顶点中的最大值传播。新建跨体平面顶点落入另一体边内部时，若输出确实采用跨体连接，其新增事件精度规则尚未完成兼容认证，当前返回 `PK_ERROR_not_implemented` 并回滚。不同裁剪域的三个布尔操作已有对应失败回归，检查输出为空、两个输入的 XT 字节保持一致、曲线及曲面池数量不增加。该限制不会把二维重合域伪装成无交。

此交付不宣称任意重参数化曲面等价、所有不同裁剪域、任意高重数接触或精确切触拓扑均已解决。点接触、曲线接触与面域重合使用不同关系；未接入的事件仍明确失败。

## 验证

Release 托管定向测试 55/55 通过，包含同域成功六例、未解析精度的原子失败三例，以及既有规则曲面与混合解析布尔回归。Release 真实 Parasolid oracle 39/39 通过，包含既有 33 例及新增同域六例。两侧分别独立构造并执行同一布尔 API；自研结果经 text XT 接收、完整 `PK_BODY_check`、拓扑与 region/shell、`PK_DEBUG_BODY_compare` 的类型、sense、容差、missing 等结构条件检验。CHART 点数差异继续只作诊断。

没有 C ABI 改动，因此不触发 NativeAOT 发布。
