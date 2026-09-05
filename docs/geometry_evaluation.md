# 曲线与曲面求值

本次实现 `PK_CURVE_eval`、`PK_CURVE_eval_with_tangent`、`PK_SURF_eval`。名称按 Parasolid 头文件大小写。

## 支持范围

- 当前运行时实际装载的直线、圆，以及平面、圆柱、圆锥、球和环面。
- 曲线 0–10 阶导数；附切向版本即使只请求位置也返回单位切向。
- 曲面 U/V 各 0–10 阶及混合偏导；矩形和三角布局均支持。三角布局要求两个阶数相等，超过阶数上限返回 `PK_ERROR_too_many_derivatives`。
- 曲面输出按 Parasolid 的 U 快变排列。内部 `SurfaceDerivativeLayout` 的排列不同，由 API 适配一次重排。
- 周期参数不被拓扑边/面的裁剪范围限制；球纬度、圆锥有效半部、apple/lemon torus 的有效 V 范围单独检查。
- 尚未装载到计算池的样条、交线、SP-curve、offset/swept/spun/blend 等几何没有被本次补齐，返回 `PK_ERROR_not_implemented`，不读取未经实现的数据池。

对非有限参数、空输出指针及负的曲线导数阶数，入口返回 `PK_ERROR_bad_parameter`。负的曲线阶数在真实 V38 探查中具有依赖求值状态的表现，不作为本实现的兼容输入范围；本实现采用确定性的输入拒绝。曲面头文件对阶数标注 [NF]，按实测行为将负数视为零，但三角阶数一致性在归零前检查。

## 代码组织

- `src/ProjectGmKernel.Native/Geometry/Evaluation/CurveEvaluation.cs`：直线、圆静态函数，不访问 session。
- `src/ProjectGmKernel.Native/Geometry/Evaluation/AnalyticSurface.cs`：解析曲面按值准备的数据，无实体句柄。
- `src/ProjectGmKernel.Native/Geometry/Evaluation/SurfaceEvaluation.cs`：静态解析公式和偏导，不写拓扑。
- `src/ProjectGmKernel.Native/Runtime/KernelRuntime.Evaluation.cs`：tag/类别检查、准备视图、PK 错误映射和数组排列。
- `src/ProjectGmKernel.Native/KernelExports.cs`、`src/ProjectGmKernel.Native/Runtime/KernelExportCommands.cs`：复用现有调度，不另建算法派发接口。

所有结果先在固定上限栈空间中计算，成功后写入调用者输出。曲线最多 11 个向量，曲面最多 121 个向量；失败不发布部分结果。没有逐点托管数组、接口求值器或闭包。

## 生成器修复

`scripts/GenerateApiLayer.cs` 原先只允许 primitive/pointer 作为导出参数，排除了按值结构体和 callback typedef，导致包括 `PK_SURF_eval` 在内的函数被静默跳过。

现已允许生成的 unmanaged aggregate 和函数指针别名，并将不能生成的签名计入阻断诊断。同时修正头文件来源为 `third_party/parasolid/include/`，通过 ClangSharp 的 ResolveLibrary 钩子绑定实际找到的 libclang，并禁止子进程 MSBuild node reuse。

ResolveLibrary 的用法依据 [ClangSharp 18.1.0 官方源码](https://raw.githubusercontent.com/dotnet/ClangSharp/v18.1.0/sources/ClangSharp.Interop/clang.cs)，避免与该库自身的 DllImportResolver 重复注册。

本次生成覆盖 1190 个头文件函数：45 个手写导出、1145 个生成占位导出。已经手写实现的三个求值入口不会再出现在生成文件中；漏生成问题还通过其余按值 UV API 及 callback API 的 native export 检查验证。

## 验证

- `tests/KernelTests/EvaluationTests.cs`：高阶解析值、单位切向、超出 edge 范围的几何求值、按值 UV 导出调用、矩形/三角排列、输出边界、错误返回、删除句柄、预热后的零托管分配。
- `scripts/ParasolidEvaluationOracle.cs`：真实 Parasolid 数值对照；默认还执行我们的 text XT transmit、真实 receive、相同参数基准 body 的 `PK_DEBUG_BODY_compare`。比较失败输出 global/local、diff 类型及相关 face/entity。
- `scripts/AbiSmoke.cs`：从发布的 native library 实际加载三个入口，验证按值 UV ABI；同时检查曾遗漏的其他结构体/callback 导出是否存在。
- `scripts/VerifyKernel.cs` 已接入完整 evaluation oracle。

数值 oracle 支持 `--numerical-only`，会明确报告未检查 XT，不能当作完整 oracle 的替代。命令为 `dotnet run scripts/ParasolidEvaluationOracle.cs -- --numerical-only`；完整命令不带该选项。

验证结果：116 项 KernelTests 和 6 项 XT 库测试通过；10 组默认/旋转平移坐标系下的基本体均通过数值与 XT receive/body compare。生成器构建和 ABI 检查无阻断项，重复生成文件哈希一致。

### 1e-13 网格对照

门槛为每个标量分量的**绝对差值 ≤ 1e-13**，没有使用相对容差或按模型尺度放宽。

- 每条直线/圆均匀采样 257 点，分别调用 0–10 阶曲线求值和附单位切向求值。
- 每个曲面采样 65×65 参数网格，每点验证矩形 121 个向量与三角 66 个向量，覆盖 U/V 各 0–10 阶及所有相应混合偏导。
- 直线采样区间 [-10,10]，圆采样区间 [-4π,4π]。平面 U/V 区间 [-5,5]；其余面 U 区间 [-2π,2π]；圆柱 V 区间 [-5,5]；圆锥 V 从锥顶参数至 5；球 V 区间 [-π/2,π/2]；环面 V 区间 [-π,π]。
- 包含默认及旋转平移坐标系，另保留稀疏边界、非对称导数阶数和错误返回测试。
- 下面统计包括不同实例，最大误差也包含额外边界测试。曲面 tangent 不另定义，du/dv 已作为一阶偏导比较。

| 类型 | 网格采样点数 | point 最大差值 | 单位 tangent 最大差值 | 导数/偏导最大差值 |
|---|---:|---:|---:|---:|
| Line | 6168 | 0 | 2.220446049250313e-16 | 0 |
| Circle | 2056 | 1.7763568394002505e-15 | 6.661338147750939e-16 | 1.6653345369377348e-15 |
| Plane | 84500 | 1.4210854715202004e-14 | — | 0 |
| Cylinder | 8450 | 7.105427357601002e-15 | — | 0 |
| Cone | 8450 | 1.7763568394002505e-15 | — | 0 |
| Sphere | 8450 | 1.7763568394002505e-15 | — | 8.881784197001252e-16 |
| Torus | 8450 | 1.7763568394002505e-15 | — | 0 |

网格验证是上述定义、实例与采样域上的对照结果，不是所有几何尺度、所有参数上的误差上界证明。

### XT 既有回归修复

block/cylinder/cone 的 receive 922 已修复，原因和对应修正为：

1. Runtime 的 face-use 环被直接导出为环；XT back/front face 链需要各自空指针终止，现在在 shell 链起点截断。
2. Fin.Other 被误设为空指针终止；绕 edge 的 fin 顺序是环，现在在边连接更新时同步维护，也覆盖无顶点 ring edge。
3. block 底面环原本与外法向相反，导致部分共享边两侧 Fin 同向；已反转底面遍历次序。

`tests/KernelTests/XtTopologyLinkTests.cs` 固定检查 face 链终止、前后互逆、fin other 闭环、primary/secondary 方向及归属；默认 oracle 继续要求真实 receive/body compare 通过。

原有检查命令：`P_SCHEMA=third_party/parasolid/schema MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParasolidPrimitiveOracle.cs`。新 oracle 通过 ParasolidScriptHost 准备环境，输出文件保存在 `temp_docs/evaluation-oracle/`。
