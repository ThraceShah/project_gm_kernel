# AGENTS

- 持久性文档统一放入 `docs/`。
- 临时文档统一放入 `temp_docs/`。
- 禁止使用绝对路径。
- 所有文档中的路径都必须相对于项目根目录。
- 所有脚本中的路径都必须相对于脚本文件自身。
- 所有需要编译的代码中的路径都必须相对于其最终编译产物所在位置。
- 所有 C# 代码必须基于 `.NET 10`。
- 所有 C# 代码都必须优先考虑 AOT 兼容性。
- 所有 C# 代码都必须优先考虑零分配。
- 改动验证通过后，必须自动提交并推送到当前分支对应的远程分支，无需再次确认；用户明确要求不提交或不推送时遵循其本次要求。
- 所有脚本统一使用 C# 编写，并使用 `.NET 10` 的 `dotnet run file.cs` 方式运行。
- `scripts/` 下的单文件 C# 脚本如果需要从 C# 调用真实 Parasolid `pskernel`，必须优先通过 `#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj` 引用 PKToy 的 P/Invoke 绑定，并声明 `#:property UsePskernelSharpUsings=true` 复用 `third_party/PKToy/PskernelSharp/Using.cs` 中的全局别名，同时声明 `#:property UseParasolidScriptHost=true` 复用 `scripts/ParasolidScriptHost.cs` 做动态库探查、`P_SCHEMA` 设置、frustrum/memory callback 注册和 session start/stop；禁止在脚本里复制 `PK_*` 类型别名、手写 C# P/Invoke 函数声明、手写 ABI 结构体或重复 Parasolid session 初始化样板。脚本调用当前项目内核时，默认必须通过 `#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj` 直接调用托管 API、`KernelRuntime` 或测试目标对应的项目接口；只有脚本目标明确是验证 C export/ABI 时才允许加载当前项目发布出的 native library 并 P/Invoke（当前为 `scripts/AbiSmoke.cs`）。禁止把 `PskernelSharp` 的 `"pskernel"` 重定向到当前项目 native kernel，以免把 Parasolid oracle 退化成自我对比。例外：生成器模板或专门测试 ABI/export 的脚本。
- 所有 `int` 语义类型必须通过 `global using` 创建类型别名（如 `BodySlot`、`CurveTag`），禁止在 record/struct 中使用裸 `int` 表示不同含义的实体索引或句柄。定义见 `src/ProjectGmKernel.Native/Runtime/KernelTypes.cs`。


## 1. dotnet Process Management

**dotnet CLI 的 MSBuild worker 和 testhost 进程会在后台驻留，导致 CPU 占用 100% 或资源泄漏。必须遵守以下规则：**

- 所有 `dotnet build` / `dotnet test` 命令必须加 `MSBUILDDISABLENODEREUSE=1` 前缀，防止 MSBuild worker 进程驻留后台。
- `dotnet test` 执行完毕后，必须用 `pkill -f testhost.dll 2>/dev/null; true` 清理残留 testhost 进程。
- 推荐写法：
  ```
  MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests && pkill -f testhost.dll 2>/dev/null; true
  ```
- 禁止不带 `MSBUILDDISABLENODEREUSE=1` 直接运行 `dotnet build` 或 `dotnet test`。
- NativeAOT 编译测试按 C ABI 改动范围触发：不涉及 C ABI 改动时，不得运行 NativeAOT 编译或发布来验证，也不得通过验证脚本间接触发。默认使用托管构建、单元测试和 Parasolid oracle 验证，保留 C# 快速编译和迭代的优势。
- C ABI 改动包括 C exports 的名称、数量、函数签名、调用约定、ABI 结构体的尺寸/字段偏移/对齐，以及原生调用边界的内存所有权契约变化。纯算法、建模逻辑和内部实现改动不触发 NativeAOT 编译测试。
- 只有涉及上述 C ABI 改动，或用户明确要求时，才执行 NativeAOT 编译及相应原生 ABI 验证；AOT 兼容性仍须在日常代码设计中保持。


## 2. Parasolid Oracle Tests

**新增或修改会影响 Parasolid 兼容 API、B-rep、几何、`x_t` transmit/receive 的接口时，必须补充 Parasolid oracle 测试。**

测试流程必须覆盖：
- 实现目标 API。
- 在测试中调用我们的 API，用目标参数创建或修改 body。
- 通过我们的 `PK_PART_transmit_b` 导出并保存 text `x_t`。
- 使用真实 Parasolid 加载我们导出的 `x_t`。
- 在真实 Parasolid 中使用同样 API、同样参数创建或修改对应 body。
- 调用 `PK_DEBUG_BODY_compare` 比较 Parasolid 基准 body 和从我们 `x_t` 加载出的 body。
- 比较失败时必须输出可定位的差异信息，例如 `global_result`、`local_result`、diff 类型、相关 face/entity。

如果 `PK_DEBUG_BODY_compare` 的 full local deviation / face matching 检查会把 Parasolid 内部参数化或 transmit 排序差异误判成失败，允许把这些项目降级为诊断输出；但 receive、拓扑计数、region/shell 语义、类型/sense/容差/missing 等结构一致性必须作为通过条件。

### PK_DEBUG_BODY_compare 验收规则

- 优先调用 `scripts/ParasolidBodyCompare.cs` 的 `AssertEquivalent`（由 `UseParasolidScriptHost=true` 引入），比较独立真实 PK 基准和我们导出后由真实 PK 加载的 body。
- 手动调用时必须设置 `max_diffs>0`、`all_tests=false`、`acc_dev_tests=false`、`tol_dev_tests=true`，保留数量、类型、sense、容差、missing 和匹配检查。`max_diffs` 限制有差异的面配对数量，不是几何容差；默认值 0 只比较拓扑数量。
- 调用成功不代表相同；必须同时验收 global/local 结果并输出差异类型及 face/entity。`n_global_diffs` 包含数量相等的记录，`n_face_pairs` 不是成功匹配面总数，禁止以它等于总面数作为通过条件。
- 默认只允许 `n_cht_pts`（内部 chart points）作为诊断。其他降级必须逐项白名单并有独立验证，禁止忽略整个 local/global 结果或同时关闭两种偏差检查来通过几何验收。
- wire 没有面，body compare 不验证曲线形状；必须额外匹配导出后的有限边，检查曲线类型/sense/端点/裁剪范围及双向几何距离，并保留目标 API 的参数、导数等专项测试。采样不能宣称为任意曲线的完整等价证明。
- 比较辅助代码必须有负例：同拓扑但尺寸错误的实体、平移或裁剪错误的 wire 必须失败；等价曲面升阶/插结在容差内应通过。结果用 `PK_DEBUG_BODY_compare_r_f` 释放。


