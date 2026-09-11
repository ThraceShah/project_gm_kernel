# ProjectGmKernel.Xt 独立库

## 组件与发布边界

- `src/ProjectGmKernel.Xt/`：`.NET 10` managed class library 和本地 NuGet。
- `src/ProjectGmKernel.Xt.Native/`：NativeAOT C ABI wrapper。
- `src/ProjectGmKernel.Xt.Native/include/ProjectGmKernel.Xt.h`：ABI 2 公共入口。
- `src/ProjectGmKernel.Xt.Native/include/ProjectGmKernel.Xt.SCH_*.h`：每个 schema
  identity 一份独立头文件，提供短名称的逐节点 C 类型和 typed table API。

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

字段成员自 ABI 2 起为强类型裸值：`d/n/w/t/q` 为 `long`/`int64_t`，`u` 为
`ulong`/`uint64_t`，`f` 为 `double`，`c`/`l` 为 `byte`/`uint8_t`，`v`/`h`/`i`/`b`
直接使用向量/区间/包围盒结构体。null 用固定哨兵表示（`XtSchemaField` 常量、
C 侧 `PGM_XT_NULL_*` 宏）：`p` 为 `-1`，整数为 `INT64_MIN`，无符号为
`UINT64_MAX`，实数为 NaN，`c`/`l` 为 `0xFF`，复合结构体为全 NaN。decode 遇到
与哨兵相同的文件值会抛 `XtFormatException`，因此哨兵不会与文件数据混淆。
`transmit=0` 字段仍存在于 struct 中以保持布局，但成员值不维护、不应读取。
`p` 字段保存原始 x_t node index；目标节点类在本 schema 内时生成按目标命名
的强类型引用结构体（如 `PARTITIONRef` / `PGM_XT_PARTITION_ref_t`，含
`Index`/`index`），否则退化为裸 `int32_t`。finalize 根据编译 descriptor 校验
引用。过程几何不求值、不 NURBS 化，ICurve、Blend、B-spline 等保持原始节点
引用图。

构造模型时使用 schema namespace内的 `COUNTS` 和 `MODEL_BUILDER`。Builder
一次分配 pinned tables；写入各 node/variable-field span 后调用 `FinalizeModel`。
Finalize 之后只允许并发读取。

### 枚举字段常量

部分枚举类字段（schema 类型 `u` 或 `c`）带有全部取值的伴生枚举，成员本身
保持数值类型，可直接与枚举比较或赋值。数值取值来自 Parasolid XT Format
Reference 的 "Schema Definitions" 章节（`SCH_*` 枚举，即 x_t 文件真实值域，
与 PK API 头文件中的 `PK_*_t` 数值**不同**）；字符取值来自 XT 拓扑文档
（`sense` 的 `'+'`/`'-'`，`REGION.type` 的 `'S'`/`'V'`）。覆盖范围按文档有
明确记载的字段划定，例如：

```csharp
(Schema.BODY__body_type)model.BODY[0].body_type   // solid_body=1, wire_body=2, ...
Schema.REGION__type.solid                          // (byte)'S'
```

```c
enum PGM_XT_BODY_body_type_e {
    PGM_XT_BODY_body_type_solid_body = 1,
    /* ... */
};
row.body_type == PGM_XT_BODY_body_type_solid_body;
```

C 侧常量名为 `PGM_XT_<节点>_<字段>_<值名>`（枚举标签 `_e` 结尾），C# 侧
枚举名为 `<节点>__<字段>`，值名保留文档 snake_case。生成映射文档
（`docs/xt_schema_generated_mapping.md`）的 Values 列逐字段记录枚举来源；
无文档依据的字段不臆测取值，保持无常量。文档值表若有缺口，以 corpus 实测
值为证据补录（如 `curve_form` 的 `helical_arc = 8`，见于螺旋 b-curve，表内
注释标明依据）。corpus 校验脚本（`scripts/ValidateXtSchemaModelCorpus.cs`）
对 `tests/ParasolidXtCorpus/Fixtures` 和 `bin/parasolid-xt-corpus` 全部生成
模型持续断言生成枚举与真实 x_t 数据一致。

动态版本使用：

```csharp
var catalog = XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();
var document = XtCodec.Read(catalog, sourceBytes);
```

外部目录只扫描顶层 `sch_*.sch_txt`。若目录包含与内置 identity 同名的 schema，
其 node/field shape 必须与编译 descriptor 完全一致，否则返回 `SchemaMismatch`。

## JSON 导出工具

`src/ProjectGmKernel.Xt.JsonTool` 是 NativeAOT 控制台程序，把 x_t 转为结构化
JSON，支持全部 15 个内置 V30–V38 绑定：

```
dotnet publish src/ProjectGmKernel.Xt.JsonTool -c Release -r linux-x64 -o bin/xt-json-tool/linux-x64
bin/xt-json-tool/linux-x64/XtToJson <model.x_t> [-o <output.json>] [--compact] [--schema-dir <directory>]
```

不传 `-o` 时输出到 `<input>.json`；默认缩进，`--compact` 紧凑。JSON 顶层为
`schema`（绑定 identity）、`version_text`、`user_field_size` 与各传输节点表
（只含非空表），行内是 `_xt_index`/`_xt_order`（变量节点加
`_xt_variable_length`）和传输字段：null 哨兵输出 `null`，`c` 字段输出单字符
字符串，`l` 输出 true/false，向量/区间/包围盒输出具名对象，变长与定长字段
输出数组；非传输字段省略。带生成枚举的标量字段额外输出 `<字段>_name`
兄弟键（如 `"body_type": 1, "body_type_name": "solid_body"`；未知值时为
`null`，null 值时不输出该键）。序列化器由 `GenerateXtSchema.cs` 生成到每个
schema namespace 的 `JSON` 类（`XtGeneratedModelJson` 统一分发），直读强类型
行、零反射，AOT 安全。带内嵌 schema 的文件（如 corpus 的
`managed-embedded.x_t`）需 `--schema-dir` 或 `PARASOLID_SCHEMA_DIR`/`P_SCHEMA`
提供支撑 schema；工具会按形状兼容规则选择绑定并在 stderr 提示。

## C ABI 使用方式

`PGM_XT_CONTEXT_create` 的 schema 目录可为 null；此时 V30–V38 仍可使用。通用
API 只管理 context、document、buffer 和 diagnostics。每个翻译单元选择一个
schema 头文件：

```c
#include "ProjectGmKernel.Xt.SCH_3701097_37102.h"

PGM_XT_INTERSECTION_t *intersections;
PGM_XT_BLENDED_EDGE_t *blends;
PGM_XT_DOCUMENT_to_MODEL(...);
PGM_XT_INTERSECTION_get_read_view(...);
PGM_XT_CHART_hvec_get_read_view(...);
PGM_XT_MODEL_to_DOCUMENT(...);
```

15 份头文件可以重复使用同一套 `PGM_XT_EDGE_t`、`PGM_XT_BODY_t` 等短名称。
同一份源码可由不同 target/条件宏选择不同头文件后分别编译。一个翻译单元同时
包含两个 schema 头文件会得到明确编译错误，因为短 typedef 无法同时表达两个
layout。

动态库中的真实链接符号仍带完整 identity，以免不同版本发生符号冲突；头文件
把短函数名映射到对应的真实符号。这个细节不影响调用方源码。C++ 关键字字段
使用尾随 `_`，例如 C 的 `.new` 在 C++ 中为 `.new_`；其他字段保持 schema 名称。

不存在 generic geometry row、mesh row、table kind 或 schema-neutral BREP table
API。C struct 和 managed struct 逐字段对应；固定数组内联，变长字段使用专用
range 和 element view。判断 null 使用 `PGM_XT_INDEX_IS_NULL`、
`PGM_XT_INTEGER_IS_NULL`、`PGM_XT_REAL_IS_NULL`、`PGM_XT_VECTOR_IS_NULL`、
`PGM_XT_BOX_IS_NULL` 等宏（managed 侧比较 `XtSchemaField` 常量）。

Handle 是带 generation 校验的 64-bit token，不是托管对象地址。Table view 由
model handle 统一释放；XT 输出 buffer 使用 `PGM_XT_BUFFER_free`。

## 支持范围与验证

- V30–V38：内置编译 descriptor 和强类型双向 schema model。
- V29 以前和未来版本：调用方 schema 驱动的动态无损 `XtDocument` codec。
- 不支持：x_b、自动下载 schema、Parasolid session/API 仿真。

生成映射见 `docs/xt_schema_generated_mapping.md`。它逐 node、逐 field 记录 managed
成员、C 成员和 codec 分支。元数据也直接公开为 `_xt_index`、`_xt_order`，仅
variable node 具有 `_xt_variable_length`。当前 Linux x64 已运行 managed、NativeAOT、15 份独立 C header layout 和代表性 C++ header 编译、
5,427 个导出符号、597 个 corpus case 和 4,920 个版本矩阵项验证；win-x64 和
osx-arm64 仅提供发布配置，尚未在对应主机运行。

`scripts/ScanXtArtifacts.cs` 禁止 `.sch_txt`、schema 原始 header/terminator 和
私有 schema 路径进入 NuGet/native artifact，但允许生成类型、字段名和编译
descriptor。
