# Parasolid part x_t 支持实施状态

本文记录 `docs/xt_all_versions_support_plan.md` 与独立 XT 库的当前验收结论。

## 发布和法律边界

- `ProjectGmKernel.Xt` 是独立 `.NET 10` managed library；
  `ProjectGmKernel.Xt.Native` 是独立 NativeAOT C ABI library。
- 两个发布物都不依赖 `ProjectGmKernel.Native`、PKToy、`pskernel`、Parasolid
  header、session 或许可。
- 使用者必须显式提供自己的 schema 目录。库只扫描目录顶层的
  `sch_*.sch_txt`，不包含默认目录、内置 schema、下载逻辑或 fallback。
- 源码、NuGet 和 NativeAOT artifact 均不包含 `.sch_txt`、schema identity
  registry、schema-specific field table 或派生 descriptor。私有 schema 只用于
  本地集成验收，不进入 Git、包或 CI artifact。
- 支持 text x_t；不支持 x_b。

## 已实现能力

- 外部 `XtSchemaCatalog` 支持索引、lazy strict parse、`LoadAll`、并发只读缓存、
  精确 identity 及受限 producer/schema 兼容解析。
- 无损 `XtDocument` codec 覆盖普通和 embedded schema、presence、固定/变长
  数组、所有现有字段码、字符数组、前向/共享/环引用、user fields、mesh、
  lattice 和 multi-part block。
- `XtBrepModel`/`XtBrepBuilder` 提供 blittable DOD 表、类型化索引、扁平
  offset/count、冻结后只读视图和输出前验证。
- V30–V38 canonical adapter 覆盖 Part/Body、完整 topology、曲线/曲面及 spline
  数据、Assembly/Instance、Attribute、User fields、mesh/lattice、frame 和
  indexed-context 相关持久节点。
- `ProjectGmKernel.Native` 已改为引用 managed XT 核心；session 从调用方的
  `PARASOLID_SCHEMA_DIR` 或 `P_SCHEMA` 创建 catalog，缺失时返回 schema access
  error，不再使用内置 schema。
- NativeAOT C ABI 提供 generation-checked handle、context/document/B-rep 生命周期、
  pinned writable/read-only table view、finalize/validate、独立 buffer 所有权及
  thread-local diagnostic。

## 当前验收结果

- 调用方私有目录中的 101 个 schema 全部通过 strict `LoadAll`，且 schema 工具
  `--check` 不生成 schema-derived source。
- 597 个 API 合法 x_t 模型全部通过：
  `XT → XtBrepModel → 丢弃原 node graph → XT`、managed 再读、真实 Parasolid
  receive，以及适用的 Body/compound child/Assembly/Attribute/User field/mesh
  比较。
- V30–V37 的 14 个独立 schema，加 `SCH_3800150_37102` V38 producer identity，
  共 15 组 × 328 个当前适用 API case = 4,920 个版本矩阵项全部通过，无实现
  失败。
- managed 单元测试和原内核 82 项回归测试通过；Linux x64 NativeAOT publish、
  C ABI smoke、C header/managed row layout static assert、NuGet 独立消费及 schema
  泄漏扫描通过。
- `linux-x64` 已在当前主机实际运行验证；`win-x64` 和 `osx-arm64` 已配置，但未在
  当前环境运行验证。

## V29 及更早版本

调用方提供兼容 schema 时，低层 schema parser 和无损 node codec 可工作；但
V29 及更早版本不在 canonical B-rep 完整双向承诺内，也不得因当前 V38 runtime
接受某个历史文件就标记 `Complete`。历史 runtime、许可或合法 API 构造路径
缺失的项目继续按 `AdapterAndRuntimeBlocked` 记录。

## 尚不能声明 Complete 的项目

仓库现有 API 语料审计仍明确将 Frame producer、Lattice 和 indexed-I/O 记录为
不可达或排除项；它们没有合法的 Parasolid transmit 语料，因此不能被 597/4,920
通过数覆盖：

- Frame 没有稳定的独立公共 producer case；managed DOD 已按实际 FRAME
  geometry/owner/sense/ring 语义实现，但尚缺真实 transmit oracle。
- 当前 runtime 的最小 `PK_LATTICE_create_by_core` 返回
  `PK_ERROR_lattice_geometry (5277)`；Lattice data 的完整 canonical adapter 和
  receive 比较尚无合法输入可验收。
- indexed-I/O 在没有完整 callback/context host 时返回
  `PK_ERROR_not_implemented (5000)`；text x_t codec 不应伪造其语料。

这些项目详见
`tests/ParasolidXtCorpus/coverage/unreachable/residual-api-audit.json` 和
`tests/ParasolidXtCorpus/coverage/unreachable/user-fields-mesh.json`。按照计划的
声明规则，当前结论是“V30–V38 的全部可达语料通过”，而不是全类型
`Complete`。获得支持 Lattice/indexed-I/O 的 runtime/license、合法 seed 或完整
callback host 后，必须重新生成语料并补齐 canonical adapter 才能升级状态。

## 主要复现命令

所有 schema 路径均由调用方通过 `PARASOLID_SCHEMA_DIR` 或 `P_SCHEMA` 提供。

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/GenerateXtSchema.cs -- --check
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ValidateXtBrepCorpus.cs -- ../bin/parasolid-xt-corpus
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ParasolidXtFixtureOracle.cs -- <fixture paths...>
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ParasolidSchemaCorpusMatrix.cs -- --schema <identity>
MSBUILDDISABLENODEREUSE=1 dotnet pack src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj -c Release
MSBUILDDISABLENODEREUSE=1 dotnet publish src/ProjectGmKernel.Xt.Native/ProjectGmKernel.Xt.Native.csproj -c Release -r linux-x64 -o bin/xt-native/linux-x64
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/XtNativeAbiSmoke.cs
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ScanXtArtifacts.cs -- bin/xt-native/linux-x64
```
