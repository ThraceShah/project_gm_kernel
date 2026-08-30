# ProjectGmKernel.Xt 独立库

## 组件与发布边界

- `src/ProjectGmKernel.Xt/`：`.NET 10` managed class library 和本地 NuGet。
- `src/ProjectGmKernel.Xt.Native/`：NativeAOT C ABI wrapper。
- `src/ProjectGmKernel.Xt.Native/include/ProjectGmKernel.Xt.h`：ABI 1 公共入口。
- `src/ProjectGmKernel.Xt.Native/include/ProjectGmKernel.Xt.Schema.generated.h`：
  V30–V38 的逐 schema、逐节点 C 类型和 typed table API。

发布物不包含 `.sch_txt` 原文、Parasolid header、API、kernel、session 或许可。
V30–V38 的生成类型和编译 descriptor 是本项目代码的一部分，因此这组版本
不需要运行时 schema 目录。V29 以前及未来版本只提供外部 schema 驱动的动态
`XtDocument` codec；调用方负责合法取得 schema。

## Managed 使用方式

```csharp
using ProjectGmKernel.Xt;
using Schema = ProjectGmKernel.Xt.Schema.SCH_3701097_37102;

var catalog = XtSchemaCatalog.OpenBuiltIn();
var document = XtCodec.Read(catalog, sourceBytes);
var model = Schema.CODEC.Decode(document);

ReadOnlySpan<Schema.INTERSECTION> intersections = model.INTERSECTION;
ReadOnlySpan<Schema.BLENDED_EDGE> blends = model.BLENDED_EDGE;

var rebuilt = Schema.CODEC.Encode(model);
var output = XtCodec.Write(catalog, rebuilt);
```

每个支持的 identity 位于独立 namespace，例如
`ProjectGmKernel.Xt.Schema.SCH_3701097_37102`。每个 schema node 都有同名大写
struct；字段保持 schema 的 snake_case 名称。每个 transmitted node 有独立连续
table，每个变长字段有独立 element table。

字段使用显式 `Unavailable`、`Null`、`Value` 状态。`transmit=0` 字段存在于
struct 中，但 decode 后只能为 `Unavailable`，builder 不能发送它。`p` 字段保存
原始 x_t node index；finalize 根据编译 descriptor 校验引用。过程几何不求值、
不 NURBS 化，ICurve、Blend、B-spline 等保持原始节点引用图。

构造模型时使用 schema namespace内的 `COUNTS` 和 `MODEL_BUILDER`。Builder
一次分配 pinned tables；写入各 node/variable-field span 后调用 `FinalizeModel`。
Finalize 之后只允许并发读取。

动态版本使用：

```csharp
var catalog = XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();
var document = XtCodec.Read(catalog, sourceBytes);
```

外部目录只扫描顶层 `sch_*.sch_txt`。若目录包含与内置 identity 同名的 schema，
其 node/field shape 必须与编译 descriptor 完全一致，否则返回 `SchemaMismatch`。

## C ABI 使用方式

`PGM_XT_CONTEXT_create` 的 schema 目录可为 null；此时 V30–V38 仍可使用。通用
API 只管理 context、document、buffer 和 diagnostics。模型 API 按 schema identity
和 node 名称强类型导出，例如：

```c
PGM_XT_DOCUMENT_to_SCH_3701097_37102_MODEL(...);
PGM_XT_SCH_3701097_37102_INTERSECTION_get_read_view(...);
PGM_XT_SCH_3701097_37102_BLENDED_EDGE_get_read_view(...);
PGM_XT_SCH_3701097_37102_CHART_hvec_get_read_view(...);
PGM_XT_SCH_3701097_37102_MODEL_to_DOCUMENT(...);
```

不存在 generic geometry row、mesh row、table kind 或 schema-neutral BREP table
API。C struct 和 managed struct 逐字段对应；固定数组内联，变长字段使用专用
range 和 element view。

Handle 是带 generation 校验的 64-bit token，不是托管对象地址。Table view 由
model handle 统一释放；XT 输出 buffer 使用 `PGM_XT_BUFFER_free`。

## 支持范围与验证

- V30–V38：内置编译 descriptor 和强类型双向 schema model。
- V29 以前和未来版本：调用方 schema 驱动的动态无损 `XtDocument` codec。
- 不支持：x_b、自动下载 schema、Parasolid session/API 仿真。

生成映射见 `docs/xt_schema_generated_mapping.md`。它逐 node、逐 field 记录 managed
成员、C 成员和 codec 分支。元数据也直接公开为 `_xt_index`、`_xt_order`，仅
variable node 具有 `_xt_variable_length`。当前 Linux x64 已运行 managed、NativeAOT、C layout、
5,427 个导出符号、597 个 corpus case 和 4,920 个版本矩阵项验证；win-x64 和
osx-arm64 仅提供发布配置，尚未在对应主机运行。

`scripts/ScanXtArtifacts.cs` 禁止 `.sch_txt`、schema 原始 header/terminator 和
私有 schema 路径进入 NuGet/native artifact，但允许生成类型、字段名和编译
descriptor。
