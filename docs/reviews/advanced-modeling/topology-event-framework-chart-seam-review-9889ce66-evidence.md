# 9889ce66 接缝合同复审：独立证据

固定源 `9889ce66caf1f1a83515aaa8d2ce5bfbf6a9d7be`；结论与可执行 A/B 合同见 `docs/reviews/advanced-modeling/topology-event-framework-chart-seam-independent-review-9889ce66.md`。本轮没有改 kernel/正式tests/数学、没有运行 PK Boolean、Rolling19/151、3344单测、fullsuite/lifecycle/AOT 或性能采样。

## 源、输入与执行身份

从 clean `ThraceShah/gm-source-root-3344-review` 建立 `ThraceShah/gm-chart-seam-9889-review`，HEAD固定上述提交。P/S 从 `../gm-topology-contract-sol/temp_docs/contract-sol/frozen-projection/` 只读复制到本树 `temp_docs/chart-seam-9889-review/frozen/` 并核原 hash。两个文件的 header 与 witness 之后的全部 program 字节相同；program序列化hash `10f33c3501f69f742846601b85dd80f4139e49094248e72b9efe81375ee8e2c9`。

复用已编译的 fixed-source replay，复制至本树 ignored `temp_docs/chart-seam-9889-review/replay-runtime/`；运行前验证其 portable PDB 文档 checksum：kernel 325个src文件、script1文件全部匹配本tree，generated/external6个跳过。没有只凭 assembly informational version 认定源码：该字符串为构建时旧HEAD `89aeac2c31f196077bd0a9d3592f343c8c55c1de`，而 checksum 证明所需dirty source实际等于9889。PE/PDB来自同一个已生成缓存目录，不修改actor build，也不重建kernel。

| 身份 | SHA-256 |
| --- | --- |
| `ProjectGmKernel.Native.dll` | `54d8820fa8b8291656a5156a8dff5b6e300ad8a942f864102d5ac96f8407b513` |
| replay `BooleanOperationOracle.dll` | `b312fa35e9487ef9185852a44466781c334210ae689c0734e28d2700f3521f1a` |
| 固定 `scripts/RollingProjectionReplay.cs` | `8fd04a65cb13c85994833677b8fc251fa160b5c10ee5768cf9d274110a06b90b` |
| P `.bin` | `f6dacaa07c99bc467c7915718d8e4f9ff20280362e001377f3c466963c768d79` |
| S `.bin` | `eff6fabced8f5c63088226b892b1c9d9e1537ee05b03c8b085e6bc2986f28b09` |

portable PDB 校验器使用 `MetadataReaderProvider.FromPortablePdbStream`；读取每个 document.Name 和 Hash，将 `/src/` 或 `/scripts/` 起的项目相对路径映射到本固定tree，对该文件计算同长度的 SHA256/SHA1，比对相等；任何 mismatch 即抛异常。脚本还解析两份 snapshot 的版本、spine、domain，跳过各自 witness 数组后逐字节比较全部剩余 program。执行日志 exit0，输出 `PASS checkedTrackedSourceDocuments=326 skippedGeneratedOrExternal=6; no kernel rebuild`。

## 实际命令与返回

所有路径相对项目根目录。下面 direct-DLL replay 的参数带 `../gm-strategy-audit/`，因为缓存脚本内的 CallerFilePath 仍对应原实施树的 `scripts/`；读取的是本树已核hash的输入，不依赖actor快照后续变化。普通新环境也可按固定 `scripts/RollingProjectionReplay.cs` 文档运行 .NET10 file app；本轮选择缓存执行避免重建kernel。

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/chart-seam-9889-review/VerifyReplay.cs -c Release > temp_docs/chart-seam-9889-review/verify-runtime.log 2>&1

dotnet temp_docs/chart-seam-9889-review/replay-runtime/BooleanOperationOracle.dll ../gm-strategy-audit/temp_docs/chart-seam-9889-review/frozen/projection-801-517-8.bin --details > temp_docs/chart-seam-9889-review/P.log 2>&1

dotnet temp_docs/chart-seam-9889-review/replay-runtime/BooleanOperationOracle.dll ../gm-strategy-audit/temp_docs/chart-seam-9889-review/frozen/projection-801-208-3.bin --details > temp_docs/chart-seam-9889-review/S.log 2>&1

dotnet temp_docs/chart-seam-9889-review/replay-runtime/BooleanOperationOracle.dll ../gm-strategy-audit/temp_docs/chart-seam-9889-review/frozen/projection-801-208-3.bin --wrong-phase > temp_docs/chart-seam-9889-review/S-wrong-phase.log 2>&1

dotnet temp_docs/chart-seam-9889-review/replay-runtime/BooleanOperationOracle.dll ../gm-strategy-audit/temp_docs/chart-seam-9889-review/frozen/projection-801-208-3.bin --exclude-root > temp_docs/chart-seam-9889-review/S-trim.log 2>&1

MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/chart-seam-9889-review/probe/SeamProbe.cs -c Release > temp_docs/chart-seam-9889-review/seam-probe.log 2>&1
```

退出依次为 **0、1、0、0、0、0**。P返回67且exit1；S返回0；wrong-phase返回157、trim返回67，后两者的exit0仅表示预期拒绝。原脚本将任意非零视为负控拒绝成功，本报告额外核明实际码，未用exit0冒称错误类型皆正确。

本轮replay约45–50ms只是单次诊断耗时，不是性能验收。无真实PK session依赖、无模型构造。现有同机private runtime/schema未更换，不因puremanaged replay唤醒别的平台。

## 独立精确投影与 terminal-tube 探针

临时 `probe/Directory.Build.props` 只 Reference 已核的 `../replay-runtime/ProjectGmKernel.Native.dll` / `ProjectGmKernel.Xt.dll`，没有 project reference 或kernel构建。单文件 `SeamProbe.cs` 的 assembly name为既有friend `BooleanOperationOracle`。它沿用固定replay的同layout reader，只读P，不修改program或witness。

精确投影方法：把每个有限 double 解码为 `N * 2^E`；对每维系数 `scale*chord[j]` 根据精确符号选择源 interval 的下/上端点，累加得到整个 box 的精确线性极值。用 BigInteger 比较两端是否落在原 IntervalNumber 输出内，再与精确 double K 比较。核心 dyadic 运算如下，可接固定replay的 reader 数据复现，不依赖厂商库：

```csharp
global using DyadicExponent=System.Int32;
// using System.Numerics;
readonly record struct Dyadic(BigInteger N,DyadicExponent E)
{
    internal static Dyadic Of(double value)
    {
        if(!double.IsFinite(value))throw new Exception("nonfinite dyadic");
        var bits=BitConverter.DoubleToUInt64Bits(value);
        var exponent=(int)((bits>>52)&2047);
        var fraction=bits&0xfffffffffffff;
        var n=new BigInteger(exponent==0?fraction:fraction|0x10000000000000);
        if((bits>>63)!=0)n=-n;
        return new(n,exponent==0?-1074:exponent-1023-52);
    }
    public static Dyadic operator +(Dyadic a,Dyadic b)
    {
        var e=Math.Min(a.E,b.E);
        return new((a.N<<(a.E-e))+(b.N<<(b.E-e)),e);
    }
    public static Dyadic operator -(Dyadic a,Dyadic b)=>a+new Dyadic(-b.N,b.E);
    public static Dyadic operator *(Dyadic a,Dyadic b)=>new(a.N*b.N,a.E+b.E);
}
```

对cell37/38，令 `lo=hi=Dyadic.Of(frame.Parameter)`；每维 `c=Dyadic.Of(frame.Scale)*Dyadic.Of(chord[j])`，`a=Of(source[j].Lower)-Of(anchor[j])`、`b=Of(source[j].Upper)-Of(anchor[j])`，按 `c.N.Sign` 选择极值累加。验证 `(lo-Of(outward.Lower)).N.Sign>=0`、`(Of(outward.Upper)-hi).N.Sign>=0`，然后验证37的`lo-Of(K)`为正、38的`hi-Of(K)`为负。这些是精确包含/排除判定；日志十进制差值仅便于阅读。

终端tube诊断只调用原 API：

```csharp
var box=new SurfaceParameterBox(lo,hi,0,0);
var status=JointNestedBlendEvaluation.BoundSharedSpineWitness(
    program,spine,box,bounds,capture:captured);
// 分别 lo=BitDecrement(K),hi=K 与 lo=K,hi=BitIncrement(K)
// 逐维打印 captured 和 source witness 的闭区间分离。
status=JointNestedBlendEvaluation.BoundSharedSpineWitness(
    program,spine,box,bounds,require:witness);
// requireSuccess 不作为 source 满足 target gauge 的证明。
```

实际关键输出：

```text
EXACT_DYADIC cell=37 intervalCoversExactBox=True whollyAboveK=True whollyBelowK=False
EXACT_DYADIC cell=38 intervalCoversExactBox=True whollyAboveK=False whollyBelowK=True
LEFT domain=[2.549870395161195,2.5498703951611956] status=Success
LEFT state=0 [-1.499999999999874,-1.4999999999998672] sourceSeparated=True
LEFT state=6 [3.141592653589916,3.141592653589929] sourceSeparated=True
LEFT requireSourceWitness=Success (alone does not prove gauge equation)
RIGHT domain=[2.5498703951611956,2.549870395161196] status=Success
RIGHT state=0 [-1.5000000000001443,-1.5000000000001377] sourceSeparated=True
RIGHT state=6 [3.1415926535896457,3.1415926535896586] sourceSeparated=True
RIGHT requireSourceWitness=Success (alone does not prove gauge equation)
PAIR state=0 separated=True
PAIR state=3 separated=True
PAIR state=6 separated=True
EXACT_K evaluation=Success p=(-1.5000000000001408,-2.000038001444109,4.99999999855589) storedAnchor=(-1.5,-2.000038001446803,4.99999999855589)
No program/witness mutation; no production branch changed
```

探针第一次编译因 local function捕获ref-struct program而失败，exit1，执行零次投影；保留 `seam-probe-compile.log`。改为static helper显式传入program后如上exit0。没有把编译失败当kernel失败，也没有针对生产代码作修复。本轮出现现有ILCompiler 10.0.10 NU1903警告，没有AOT编译。

## 哈希与复用证据的区别

以下文件相对于 `temp_docs/chart-seam-9889-review/`：

| 文件 | SHA-256 |
| --- | --- |
| `verify-runtime.log` | `4159eaad5af70076c52d2664bb6a809cff8a85c384978a5be8dbe40449dcbfb7` |
| `VerifyReplay.cs` | `0c5cdf8ed80244485dbf206811bb23bc4bcb77d4517ad893f52312bd15123645` |
| `P.log` | `6eb609aa104575c6601897c9596ff78510d7776f88577730d03dc1dff1dfab13` |
| `S.log` | `90e444b942d07934d0732ebaf985a21e032818690911b007457f6110c3b4e891` |
| `S-wrong-phase.log` | `55fab5e89f39b89e1e6448ad87316af995fe9c8773d551c8c793e7dd92523526` |
| `S-trim.log` | `6b34b96608c65633e6648a9cef76432b0bf7ca9fd14c50b5fdb830325412af71` |
| `probe/SeamProbe.cs` | `1c61544f557c8ca6f5ec7e51fe33ba0770af8b0117997530b05916c46dba75c4` |
| `seam-probe.log` | `bbbb5594750ca2a2ab17c3c15f1291cda8d20a661daa6b7ed5171096146d80ea` |
| `seam-probe-compile.log` | `1434e3d4367461264969281ee8a305fe47416e011774bcd2c536c5485f965469` |

只读复用日志，不宣称本轮重新运行其建模：

| 原实施树相对项目路径 | SHA-256 / 内容 |
| --- | --- |
| `../gm-topology-contract-sol/temp_docs/contract-sol/frozen-projection.log` | `3f03e3811706a18bbfd31dde907223aeccb3f2a3ab15b2a8ac0c46a85714e494`；第446–448行source517/2、34/62、certificate5/witness8705 → snapshot P → RootNotOnCurve；尾部GM157/PK0/1body/full0 |
| `../gm-topology-contract-sol/temp_docs/contract-sol/finite-root-projection.log` | `22ffdeddb1986cdf8a96f7b6474e95f11432b7f4a78ee0d78dae308963a39b64`；source有限证明及对应投影拒绝 |

本轮read-only源码检查包括 `KernelRuntime.NativeCutLocator.cs` 的入口支撑/完整witness/gauge先验、`NestedBlendBounds.cs` 的require位置、`OriginalChartParameterMap.cs`、`KernelRuntime.GeneralIntersections.cs` 的closed分支、`KernelRuntime.NestedBlendCompilation.cs` 的refinement、finite station/association/材料caller。对`8bd0c72`调用者审计以`git show`只读核对；不跟新工作差异。运行未创建testhost，没有杀其它agent进程。私有runtime/schema保持原状。
