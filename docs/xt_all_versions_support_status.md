# Parasolid part x_t 支持实施状态

## 当前设计

`ProjectGmKernel.Xt` 和 `ProjectGmKernel.Xt.Native` 已从内核主体中独立。公开
managed/C 模型不再使用 `XtGeometryRow`、`XtMeshRow` 或通用 BREP table；它们
直接映射 V30–V38 schema 的 node 和 field。

- 14 个 V30–V37 schema、V38 producer alias 和 V13 embedded base
  （`SCH_1300120_13006`）共 16 个独立 managed namespace 和独立 C/C++ 头文件。
- 3,062 个 schema node 全部生成同名 struct，23,391 个字段全部生成同名成员。
- transmitted node 使用独立连续 typed table；variable field 使用专用 element
  table。
- `transmit=0` 字段保留在类型中并强制为 `Unavailable`。
- pointer 保存原始 node index，并由 descriptor 做 target class 校验。
- ICurve、Blend、B-spline、SPCurve、TRCurve、Swept、Spun、Frame、Mesh 和
  Lattice 均按 schema 原始节点引用图保存，不做几何降级。

V30–V38 与 V13 embedded base 的编译 descriptor 内置在 managed/native 发布物
中，不需要外部 schema；embedded-schema 归档
（`SCH_<版本>_<当前编号>_13006`）因此无需 schema 目录即可解析。其余 V29
以前及未来版本仍可通过调用方目录使用动态 `XtDocument` codec。构建可用
`-p:XtEmbeddedSchemaDir=<目录>` 显式把 schema 原文内嵌进程序集作为补充
（默认关闭；发布物禁止包含 `.sch_txt` 原文，开启期间 `ScanXtArtifacts`
泄漏扫描会失败，仅限内部构建使用）。发布物禁止包含 `.sch_txt` 原文、原始
schema header/terminator、私有 schema 路径、Parasolid API/header、
`pskernel` 或许可材料。

## 当前验收结果

- 私有目录中的 101 个 schema 可 strict `LoadAll`；内置 identity 与同名外部
  schema 的 shape 不一致会返回 `SchemaMismatch`。
- 597 个合法 API corpus case 已通过
  `XT → schema-specific MODEL → 丢弃 XtDocument → XT → Parasolid receive/compare`。
- 15 组 × 328 个适用 case，即 4,920 个 V30–V38 版本矩阵项全部通过。
- C# 与 C 对 3,062 个 node struct、32,700 个 schema/metadata 字段完成
  `sizeof/offsetof` 核对；其中 schema 原字段为 23,391 个。
- NativeAOT header 声明、实现和 Linux 动态库均包含 5,704 个 schema-specific
  typed API；头文件公开短类型/API 名，真实导出符号保留 identity；旧 generic
  BREP/giant-row 符号为零。
- 内置 V13 base 使 embedded-schema 归档在无 schema 目录时完成 base 解析、
  delta 重建与内核绑定回退（Xt 单测覆盖 `ResolveBySchemaNumber(13006)` 与
  `EncodeWithBaseSchema` 归档往返；直连 V13 非嵌入式文件也可输出 typed
  JSON）。
- managed 测试、原内核 82 项回归、NuGet 独立消费、Linux NativeAOT smoke、
  无 schema 目录 build/pack/publish 和原文泄漏扫描通过。
- typed corpus 门禁（`CheckParasolidTypeCoverage -- --strict`）`missingCount=0`：
  原 5 个缺口中 sphere-spun blend、blend 深度 2、ICurve 深度 2 已由成功 fixture
  关闭；loop hole/peripheral 旧 token 经探查确认 v38 恒归一化为 `inner`/`outer`，
  按运行时归一化记录。30 组、330 个 manifest 全部通过真实 Parasolid
  transmit/receive/compare。
- 修复了 embedded-schema transmit（`transmit_version=0`）回归：根因是
  `XtSchemaCatalog` 只识别 `sch_*.sch_txt` 而漏掉 schema 缓存的 `sch_*.s_t` 命名，
  导致 embedded base 回退到最老 schema 3000。修复后 catalog 同时接受两种命名，
  embedded base 恢复固定 schema 13006，identity 中的 modeler 版本跟随
  `document.Schema.ModelerVersion`（如 37102 → `SCH_3701097_37102_13006`），与所
  传输 schema 版本保持一致；此前所有 corpus case 的 managed embedded 再编码被真实
  Parasolid 以 `schema_incompatible (1094)` 拒收。

## Complete 声明边界

V30–V38 的全部当前合法可达 API 语料已通过，但 Frame 独立 producer、Lattice
和 indexed-I/O 仍保留不可达审计。没有合法 Parasolid producer/runtime 时，
生成类型与合成字段 codec 可以完成，但不能伪造 oracle 语料并宣称该语义
`Complete`。审计依据位于：

- `tests/ParasolidXtCorpus/coverage/unreachable/residual-api-audit.json`
- `tests/ParasolidXtCorpus/coverage/unreachable/user-fields-mesh.json`

因此准确结论是：V30–V38 的强类型 schema codec 和全部可达 corpus 已通过；
受 runtime/producer 阻塞的项目单列，不计入已覆盖。

## 主要复现命令

生成器读取调用方私有 schema；构建、打包和 V30–V38 运行不读取它。

```bash
PARASOLID_SCHEMA_DIR=../third_party/parasolid/schema MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/GenerateXtSchema.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyXtGeneratedLayouts.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateXtSchemaModelCorpus.cs -- ../bin/parasolid-xt-corpus
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ParasolidXtFixtureOracle.cs -- <fixture paths...>
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ParasolidSchemaCorpusMatrix.cs -- --schema <identity>
MSBUILDDISABLENODEREUSE=1 dotnet pack src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj -c Release
dotnet run --file scripts/PublishXtToJsonEmbedded.cs
MSBUILDDISABLENODEREUSE=1 dotnet publish src/ProjectGmKernel.Xt.Native/ProjectGmKernel.Xt.Native.csproj -c Release -r linux-x64 -o bin/xt-native/linux-x64
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/XtNativeAbiSmoke.cs
```
