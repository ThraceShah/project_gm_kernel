# IntervalJacobian 空读集 guard 候选

基线固定 `0ba5a1e7021b9d8bf0e20bfc2aed1ad20a76c7ba`，分支 `perf/cache-empty-readset-guard`。
执行者为 Codex；协调端明确授权 Linux qingyu2 替代原 Mac/MiMo 要求。
本候选完成有限正确性验证，尚未独立复审、未测速、未部署；CPU 收益及完整 GM/PK 对标均未达成。

## 生产差异白名单

只有 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs`：

```csharp
if(HasAnyUv(nodes))cache.Clear();

private static bool HasAnyUv(ReadOnlySpan<Occurrence> nodes)
{
    foreach(var node in nodes)if(node.FirstUv>=0||node.SecondUv>=0)return true;
    return false;
}
```

谓词位于原 Jac 清零点，每次只扫描本次 nodes；有任意非负 UV（包括未知或非法非负索引）仍执行原完整 clear。
其余 src/scripts 无差异：没有移植 6bb 诊断、状态、计时器、dump、线程逻辑或 namespace 移动。
Ready/key/fill、ReuseSplineContactBounds=false 的额外清零、RHS/Assemble、导数 seed/reset、证明、容差、预算、SkipLocalsInit 和 C ABI 均未改。
`scripts/AllocationBanCheck.cs` 原样，无新增例外。

事前只读固定 `6bb013eddb6cb9795bf56865c9b20434630b5937:docs/Cache_Clear_Structural_Candidate.md`
及 `48840509c41fadb1a9283ee4de3e290fcf0cd124:docs/reviews/advanced-modeling/cache-clear-structural-6bb-independent-review.md`，未 cherry-pick。
后者接受结构方向，不能充当本候选的独立验收。

## 本次源码 readset 核对

以当前生产源码核对，而非依赖候选文档假设：

| 访问 | 原生产来源及守卫 |
| --- | --- |
| Jac prepass 的 matcher/fill | 遍历原 nodes 两侧，uv<0 在 cache[uv] 前 continue |
| IntervalSpline 的 matcher/Values | cacheIndex>=0 且 cache 非空；索引来自调用者的 UV |
| IntervalResidual | 同一 nodes 两侧 UV；IntervalNormal 的 SplineUv 仅返回该 occurrence 的两侧 UV 或 -1，负值提前 Unsupported |
| CertifyNearestBoxes | 同一 nodes 的 root/child Normal 与 UV 枚举；uv+1 仅索引变量，不是 cache 邻槽 |
| 两次 Frame、curvature、acceleration | 同一 nodes/occurrence0 的 IntervalNormal，不增加键 |
| support U/UV section | 同一 nodes 中 child 的两个 IntervalNormal，不增加键 |
| witness、anchor、reach | witness 只处理 boxes；anchor 使用 Taylor snapshot；reach 不接收此 cache |

EvaluateCore 初始化 occurrence 的 FirstUv/SecondUv=-1，仅在实际 spline 分支写 UV，成功后复制活跃 nodes 给 BoundSegment。
BoundSegment 的 stackalloc cache 不逃逸；后续只传递同一活跃 nodes，既不改变其 UV，也不切换 program。
Jac fill/Residual 失败立即返回；expansion 失败继续下一 Jac，refinement 失败退出，所有有 UV 的下一次 Jac 仍完整清零。
因此全负 UV 时两处 cache 元素访问及其后继读取均不可达；未清字节不可观察。
将来若新增 reader 或改变索引来源，需重新核对本闭包。

## 环境与实际验证

在首次构建前运行协调端指定统一工具（这里将路径写成项目根相对形式）：

```sh
../../../../code/orca-worker/workspace/gm-private-worktree-env/gm-private-env.sh ensure --worktree "$PWD" --profile native
```

结果 `ENV_READY: profile=native pk=38.0 platform=linux-x64 corpus=present`；schema/runtime manifest 校验通过，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`。
.NET SDK 10.0.302，运行时 10.0.10，Linux x64；不外推 Mac JIT 或性能。
厂商库/schema 由工具提供于 ignored 位置，未纳入 Git；本轮没有调用真实 PK 建模。

原始结果和自家托管 DLL/PDB 仅保存在 ignored `temp_docs/cache-empty-readset-guard/`。
以下均串行、单 MSBuild worker、非计时正确性执行；未运行 NativeAOT/publish。

基线（生产源码尚未改 guard，使用本次增强的初始化合同）：

```sh
MSBUILDDISABLENODEREUSE=1 NESTED_BLEND_BOUNDS_DUMP="$PWD/temp_docs/cache-empty-readset-guard/base-dump.txt" dotnet test tests/KernelTests -c Release -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false --filter 'FullyQualifiedName~NestedBlendBoundsInitializationContractTests|FullyQualifiedName~NestedBlendBoundsTests|FullyQualifiedName~NestedBlendSplineCacheTests|FullyQualifiedName~SharedUvFailedFillRetryAndDifferentProgramCannotReuseStaleReadyValues' --logger 'trx;LogFileName=base.trx' --results-directory temp_docs/cache-empty-readset-guard
```

exit 0，18/18；allocation IL guard 原样检查 2921 methods，CLEAN。
测试脚手架首次编译曾因返回 ref struct 的 collection expression 产生 CS8347，改为明确 heap array 后通过；该次未执行测试，记录在 `base-compile-attempt.log/.exit`，不当作算法失败或通过。

候选：

```sh
MSBUILDDISABLENODEREUSE=1 NESTED_BLEND_BOUNDS_DUMP="$PWD/temp_docs/cache-empty-readset-guard/guard-dump.txt" dotnet test tests/KernelTests -c Release -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false --filter 'FullyQualifiedName~NestedBlendBoundsInitializationContractTests|FullyQualifiedName~NestedBlendBoundsTests|FullyQualifiedName~NestedBlendSplineCacheTests|FullyQualifiedName~NestedBlendCacheEmptyReadSetTests' --logger 'trx;LogFileName=guard.trx' --results-directory temp_docs/cache-empty-readset-guard
```

exit 0，20/20（当时新文件含三个直接 cache 控件）；2922 methods，CLEAN。
后来仅在测试文件加入第四个控件，调用现有成功 supporting-blend fixture，补上原 deep-chain 只给 NotConverged 的 section 路径：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false --filter 'FullyQualifiedName~SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled' --logger 'trx;LogFileName=guard-section.trx' --results-directory temp_docs/cache-empty-readset-guard
```

exit 0，1/1；同样运行原 allocation guard，CLEAN。
将首次保存的基线自家 Native DLL/PDB 临时复制到测试输出目录（仅 ignored 产物，不改源码），用相同最终测试程序集执行该新增控件：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release --no-build --no-restore -m:1 --filter 'FullyQualifiedName~SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled' --logger 'trx;LogFileName=base-section.trx' --results-directory temp_docs/cache-empty-readset-guard
```

exit 0，1/1，之后已恢复候选 DLL/PDB；加载输入 hash 存于 `base-section-loaded-inputs.sha256`。
基线合计 19 个不同测试、候选合计 21 个不同测试；没有重跑整套或历史 capture。
testhost 清理模式仅匹配含本 worktree 名的 testhost，未杀其他工作树或未知进程。

另执行普通生产托管构建：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet build src/ProjectGmKernel.Native -c Release -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false
```

exit 0，175 个 function catalog descriptors 验证通过；生产 allocation IL guard 检查 2880 methods，CLEAN。
仅有既存 NuGet ILCompiler 包告警，未运行 AOT 编译；构建记录为 `temp_docs/cache-empty-readset-guard/production-build.log/.exit`。

## 数学、行为与负控边界

- 初始化合同的 analytic-nested、spline-supports、curved-spine、deep-chain、singular 各 5 个盒；实际 Bound 九项上下界/导数、spine、anchor、witness、双侧 curvature、acceleration、reach，以及新增 support U/UV 失败结果和非法输入结果，共 76 行。
  `cmp base-dump.txt guard-dump.txt` exit 0，逐字节相同，SHA256 均 `1e0c99a0ed3e4df8b62321fd7a9894e238c0ef8a33f64b5bb367d14c477bf3bd`。
- 反序、重复调用、早退后正常调用、命中/未命中历史保持原合同；新增 Reuse=false 对整个 Dump 的逐位比较，并用 collection 隔离全局开关。
  原约 74KiB 栈污染仍仅为烟测，没有 226KiB frame 覆盖证明。
- 新控件通过反射为真实私有 Occurrence/contact/Jac 绑定强类型 delegate，不复制布局、guard 或清零实现，也未向生产添加 test hook。
  对全部 16 个 occurrence 的两侧测试 0、3、61、int.MaxValue 和负值；空 span/全负返回 false，任何非负返回 true。
- 对真实 analytic Jac 输入完整 63 项毒化 cache：缓存字节保持原样且矩阵包住独立制造的 dX/dZ/dY。
  同一个 cache 接着用于第 16 节点 SecondUv=61 的失败 fill，验证全 span（含未使用槽）全部清零；不是只查 Ready。
  失败通过 periodic 1e15 参数触发现有拒绝分支，未拿原诊断的 120 次失败冒充路径覆盖。
- shared/duplicate UV=3 的实际 Jac 成功、失败 fill、retry；连续使用相同 ScalarOffset/key 而 scalar payload 平移为 0/7/-3 的不同 program。
  检查真实 Ready 与重新求得的 hull 第一分量逐位一致，失败后全 span 为零；此控件没有宣称比较全部 18 个缓存分量。
- 既有 bounds 控件将区间与独立点/导数 evaluator 对照；既有 spline cache 控件覆盖 rational/non-rational、不同 seeds/sense/key。
  supporting-blend 成功 fixture 在 Reuse=false 下检查 U/UV、导数与实际曲线采样及解析平面关系；此新增成功 section 控件是独立数学验收，不声称其成功输出另有完整逐位 dump。
- 不要求无 UV 的未使用 cache 字节与基线相等；保留毒值正是 guard 预期。
  dump 的测试 caller 保留默认局部初始化，也会记录失败输出；相同的失败尾部字节不作为 kernel 承诺初始化该区域的证明。

## 证据范围

旧 Mac attribution 计数属于全 session，不能当作 FIX 独占窗口；合计请求清零字节不是实际 DRAM 流量。
旧 preflight 属于先前失败尝试，其 HEAD/DLL 与 run2 不匹配；本轮不补造成功运行当时的绑定证据。
没有 before/after benchmark、profiler、5case/S1/S2、19/151/full 建模验证、45 历史 capture 重验或负载等待。
未动其它工作树、main、live、portal，未部署；未来 shared-seam 表示另分支处理。

## 验收记录与 artifact hash

源文件和自家 managed artifact 的完整 SHA256 见 `base-manifest.sha256`、`guard-source.sha256`、`guard-artifacts.sha256`，均在上述 ignored 证据目录；下表保存核心值供固定候选复核。

| 对象 | SHA256 |
| --- | --- |
| base NestedBlendBounds.cs | `a580a030e7a189ce151534e52caabc56a4188cbcee5d365c406d0161162be898` |
| guard NestedBlendBounds.cs | `e848b707c64662b086e54e81e549cba05202d4d9b2ac696dfd14d83de76cb4e5` |
| 原 AllocationBanCheck.cs | `057f5adc69730e16fa9866d6e307744fac00839c8c6f1a95483f7cd267ce366a` |
| base testhooks Native DLL | `62835236f85a79947781d7809f0aec319f8e2bc4c004b3e39756c0539a368d64` |
| base testhooks Native PDB | `74b277a64208991253dbfdc2b80d25b43a5188c04d755105cff0084f3c66671d` |
| guard testhooks Native DLL | `9a0ebadad0baa0676fa013c277f4ebc619c676464cae8381fdd2e784f2008eb9` |
| guard testhooks Native PDB | `f0a887feb3d743b3b519200e4118ba6d438ef85cc0962fb77786d19f2d978e12` |
| final KernelTests DLL（含新增 section 控件） | `cffe5f08250dca8dcb6eddf380cba78ae3f5e3bd4a7036ac15ceb25e1e0f9f83` |
| guard production managed Native DLL | `12e3ad7ff231c50bfb7f6c00701cbfafbc2da19fa9c45a57c288eaf30cf0e356` |
| guard production managed Native PDB | `ea48b3a697c6370322bc481b4f0cef84d5a30000558c5b5e6d4fcb30b6cb7166` |

测试 artifact 为 managed testhooks 版本，额外生产构建为普通 managed 版本，均不冒充 NativeAOT 库或旧 Mac 加载产物。
