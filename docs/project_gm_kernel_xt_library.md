# ProjectGmKernel.Xt 独立库

## 组件

- `src/ProjectGmKernel.Xt/`：`.NET 10` managed class library 和本地 NuGet。
- `src/ProjectGmKernel.Xt.Native/`：NativeAOT C ABI wrapper。
- `src/ProjectGmKernel.Xt.Native/include/ProjectGmKernel.Xt.h`：ABI 1 C header。

两者都不包含 Parasolid schema、派生 descriptor、Parasolid API/header 或
`pskernel` 依赖。调用方负责合法取得 schema，并显式提供目录。

## Managed 使用方式

```csharp
using ProjectGmKernel.Xt;

var catalog = XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();

var document = XtCodec.Read(catalog, sourceBytes);
var model = XtBrepConverter.Decode(document);
XtBrepValidator.Validate(model);

var rebuilt = XtBrepConverter.Encode(catalog, model, targetSchemaIdentity);
var output = XtCodec.Write(catalog, rebuilt);
```

`schemaDirectory` 没有隐式默认值。Catalog 只扫描顶层 `sch_*.sch_txt`，首次
解析后在 catalog 生命周期内只读缓存。切换目录时应创建新 catalog。

`XtDocument` 用于 schema/node graph 无损读写；`XtBrepModel` 是冻结的 DOD
规范模型。第三方内核应把自己的 B-rep 映射到 `XtBrepBuilder` 的 typed table，
填完所有 index、range、owner 和 payload 后调用 `FinalizeModel`。null index 为
`-1`；builder 不会替调用方猜测未填写的语义。

## C ABI 使用方式

调用方在 `PGM_XT_CONTEXT_create` 的 `PGM_XT_context_o_t` 中传入 UTF-8 schema
目录。Context 会复制目录字符串，但其生命周期内目录内容必须保持稳定。

`PGM_XT_BREP_create` 按 counts 一次分配所有 pinned table；finalize 前
`PGM_XT_BREP_get_table_view` 返回可写 view，finalize 后返回只读 view。View 由
B-rep handle 统一释放；XT buffer 和 diagnostic buffer 使用
`PGM_XT_BUFFER_free`。

Handle 是带 generation 校验的 64-bit token，不是托管对象地址。重复 delete、
错误类型 handle、finalize 后再次 finalize，以及非 finalized B-rep 的 encode
都会返回明确错误。

## 支持范围

- 低层 codec：调用方目录中能通过严格解析的兼容 text schema。
- Canonical B-rep 双向转换：V30–V38 当前可达 API 语料；Frame 已有 typed DOD
  表但缺 producer oracle，Lattice/indexed-I/O 仍受当前 runtime/callback host
  阻塞，不能宣称全类型 Complete。
- 不支持：x_b、自动下载 schema、Parasolid session/API 仿真和第三方内核专用
  adapter。

## 打包保护

`scripts/ScanXtArtifacts.cs` 会扫描 NuGet 和 NativeAOT 发布目录。发现
`.sch_txt`、schema identity、schema-specific generated table、descriptor 标记
或私有 schema 路径时立即失败。`scripts/VerifyXtNugetConsumer.cs` 使用临时项目
只引用本地 NuGet，验证依赖图中没有 `ProjectGmKernel.Native`、PKToy 或
`pskernel`。

当前主机已运行验证 `linux-x64`。`win-x64` 和 `osx-arm64` 已配置发布 RID 和
C header，但尚未在对应主机运行验证。
