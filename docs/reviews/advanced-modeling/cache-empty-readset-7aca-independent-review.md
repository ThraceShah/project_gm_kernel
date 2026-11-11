# IntervalJacobian 空读集 guard 独立验收（固定7aca）

**结论：accepted，仅此生产 guard 的源码正确性与有限托管负控范围。** 真实生产候选已实现此前接受的最小方向，未发现新增隐藏 cache 读者或错误失效路径。没有测速、CPU收益/DRAM流量结论、Mac执行、AOT验证、完整GM/PK对标或部署；接受不延伸到另一个数学分支或整体内核目标。

候选 `7aca6f0275cdb8c50fd7fb370103b66cf315ff7a`（`origin/perf/cache-empty-readset-guard`），against `0ba5a1e7021b9d8bf0e20bfc2aed1ad20a76c7ba`。前结构方案报告commit `48840509c41fadb1a9283ee4de3e290fcf0cd124`只接受实施方向，本报告才审实际生产改动。review分支 `ThraceShah/gm-cache-empty-7aca-review`；旧报告分支与ignored证据均保留，未跟随作者working diff、混入SharedPlaneGenerated改动或改其他工作树。

## 源码白名单与读集证明

`git diff 0ba5a1e..7aca6f0 -- src`只有 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs` 两小段：`:368` 原完整 `cache.Clear()` 改为 `if(HasAnyUv(nodes))cache.Clear();`，`:382`–386新增纯foreach扫描同一nodes两侧非负UV。无hoist、epoch或持久缓存，无诊断状态/计时/SessionStop dump。其余src/scripts无差异；AllocationBan源码SHA256仍 `057f5adc69730e16fa9866d6e307744fac00839c8c6f1a95483f7cd267ce366a`，没有加例外或移动namespace躲扫描。

保留：`:222` `ReuseSplineContactBounds=false`额外clear、RHS/Assemble清零、变量导数seed/reset、Ready/key/Values顺序、证明/容差/预算/错误状态、caller SkipLocalsInit与C ABI。扫描不按n或root猜readset；任何非负UV（甚至非法的大正数）仍执行原完整span clear，随后原索引/输入行为不被放松或吞错。

记 `R = {node.FirstUv, node.SecondUv | >=0}`，逐源复核如下，不依赖“新栈通常为0”：

| 实际路径 | 读集与生命周期 |
| --- | --- |
| Jac matcher/fill `Bounds:369`–373 | 同一nodes×两侧；uv<0先continue，之后才访问cache[uv] |
| IntervalSpline `NestedBlendIntervalSupport.cs:83`–89 | 唯一另一生产元素读取，cacheIndex>=0且cache非空并matcher通过才取Values；索引来自以下调用者 |
| IntervalNormal `IntervalSupport:43`–44 / SplineUv `JointNestedBlendEvaluation.cs:483`–486 | 只返回当前occurrence两侧UV或−1；负值在cache前Unsupported；analytic/blend分支不读该cache |
| IntervalResidual `IntervalSupport:150`–175 | 同一nodes；其两侧循环 `:166` 也先跳负UV，再传IntervalSpline；uv+1是变量槽，不是cache邻槽 |
| CertifyNearestBoxes `Bounds:224,324`起 | 同一nodes；root/child Normal和两侧UV循环只取R中索引；selected/map/RHS是其他scratch |
| Frame `Bounds:317,320,404`–411 | 两次导数seed均传原nodes/occurrence0与同一cache，不增加键 |
| curvature/acceleration `Bounds:273`–274 | 原nodes occurrence0两次Normal，之后只用所得向量 |
| support U/UV `Bounds:264`→`NestedBlendParameterBounds.cs:54`–74 | 原nodes的child两个Normal，只缩小occurrence选择 |
| witness/anchor/reach | require/capture操作boxes；anchor走Taylor snapshot；reach不接收此cache，没有旁路读 |

EvaluateCore为root/child初始化两侧UV=−1，实际spline分支才写非负UV（同basis可shared）。其`:178`复制活跃nodes/counts；BoundSegment建立唯一活跃span后不再修改这些UV。cache是`:179`的局部stackalloc[63]，未逃逸、未切换program/generation或传给另一组nodes；测试hook有自己的独立cache/clear，不属于本guard。

失败：Jac fill/Residual直接return；expansion调用点`:192`失败continue而不消费未确认Jacobian，refinement`:214`失败退出Bound。下一次有UV调用依然完整clear，failed-fill不会把旧Ready带入retry；无UV时所有上述元素读取不可达，后继也保持R为空。即使未清cache含NaN、假Ready、旧key或上次program的数据，仍不可观察。并非证明其他scratch也可不初始化；以后新增reader/索引来源即需重审。

该闭包与前方案相符；本次还核了实际compiled source，避免把另一数学分支的运行拿来支持这个base。

## 实际程序集、配置与源码绑定

只读复制作者已保存的自家DLL/PDB、日志、TRX和dump到本树独立ignored目录。PE CodeView GUID/stamp与portable PDB ID匹配；**base、guard-testhooks、guard-production各324份非obj Native source document**与固定源码逐SHA256一致（base的Bounds对git归档0ba原文，其余源相同），各排除3份obj生成文档。四份实际所调测试源码的PDB校验和及Test DLL/PDB关联也匹配固定7aca，未发现SharedPlaneGenerated或诊断源混入。

| 自家实际artifact | SHA256 / MVID | PDB编译配置与代码 |
| --- | --- | --- |
| base testhooks Native | `62835236f85a79947781d7809f0aec319f8e2bc4c004b3e39756c0539a368d64` / `8c15a030-686a-4b25-b690-99b52acca69a` | Release / AnyCpu / KERNEL_TEST_HOOKS；Jac IL563B，无HasAnyUv |
| guard testhooks Native | `9a0ebadad0baa0676fa013c277f4ebc619c676464cae8381fdd2e784f2008eb9` / `cc3b42cd-d612-4013-bcb2-92581d57ef13` | 同Release / AnyCpu / KERNEL_TEST_HOOKS；Jac IL571B，helper56B |
| guard普通managed Native | `12e3ad7ff231c50bfb7f6c00701cbfafbc2da19fa9c45a57c288eaf30cf0e356` / `56d27479-d704-4d0a-be99-39c31a15be68` | Release / AnyCpu，无KERNEL_TEST_HOOKS；Jac571B、helper56B |
| 最终KernelTests | `cffe5f08250dca8dcb6eddf380cba78ae3f5e3bd4a7036ac15ceb25e1e0f9f83` | 含第4个section控件，非早期baseline测试程序集 |

三份Native的BoundSegment均IL5608B/InitLocals=false，Jac均InitLocals=true；普通与testhooks的本guard源相同。实测配置是Linux qingyu2 .NET10托管执行，不是Mac M4 JIT或NativeAOT；不从AnyCpu IL推断机器码/性能。测试与普通输出引用的自家XT DLL SHA256都为 `68a350654e7cee5d6321aed9e7f0393fca34b791ed38ea7892baa18d8f379a70`。

guard Bounds源SHA256 `e848b707c64662b086e54e81e549cba05202d4d9b2ac696dfd14d83de76cb4e5`，base为 `a580a030e7a189ce151534e52caabc56a4188cbcee5d365c406d0161162be898`。本轮运行前后都有明确DLL hash/MVID日志，不能只靠HEAD或缓存时间戳。

## 既有证据质量与本轮最小复验

作者原TRX/exit复核为baseline18/18+section1/1、guard20/20+section1/1，不是相同测试集合的19/21性能对照。原allocation日志同testhooks配置2921→2922恰好多一个helper；普通production2880是另一配置，不能和2922混比。普通构建原exit0/function catalog175与CLEAN记录保留。本轮又独立执行原AllocationBanCheck读取已绑定的普通production DLL，**原exit0、2880 methods、CLEAN**，未重建内核、未改扫描范围。

既有base/guard dump各76行，独立逐字节核实相等，SHA256均 **`1e0c99a0ed3e4df8b62321fd7a9894e238c0ef8a33f64b5bb367d14c477bf3bd`**。包含无/有UV成功Bound及其后继、失败/非法输入；不把失败输出未承诺初始化的尾部一致当额外内核保证。旧74KiB栈污染只是烟测，不夸成226KiB frame全部覆盖。120次诊断失败不作为这里的路径覆盖证据。

本轮从自家独立runtime目录**直接串行调用六个既有Fact方法**，真实xunit断言执行、原exit0；不是VSTest discovery，也不冒称新跑全21/19或全suite。显式LoadFrom程序集并记录实际Native位置/hash/MVID，依赖只解析到本runtime目录：

1. GuardScansBothSidesOfEveryOccurrenceAndTreatsAnyNonnegativeIndexAsLive：16个occurrence两侧、0/3/61/int.MaxValue、负值及空span；直接绑定生产谓词，无镜像扫描器。
2. ActualJacobianSkipsOnlyEmptyReadSetsAndInvalidatesTheWholeCacheBeforeFailedFill：实际Jac的无UV毒化cache保留、独立制造dX/dZ/dY矩阵；第16节点SecondUv61失败fill验证整个cache清零，不只Ready。
3. SharedUvFailedFillRetryAndDifferentProgramCannotReuseStaleReadyValues：shared/duplicate UV3、成功→失败→retry，同ScalarOffset/key但payload平移0/7/−3，实际Ready与新hull首分量逐位对照。该控件没有证明18个hull分量全部独立等价；本次正确性另由完整clear与源码闭包保障。
4. SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled：实际成功support U/UV、曲线导数/采样和解析平面关系，不只原deep-chain的NotConverged。
5. OptionalConsumersAreBitwiseStableWithSplineContactReuseDisabled：实际Dump含analytic、spline-supports、curved、deep、singular，后继Frame/curvature/acceleration/reach/witness等，Reuse=false控制保留。spline-supports的真实Bound/Spine、curvature、acceleration、reach有Success，不是只跑一个失败分支。
6. CertifiedTubeEnclosesPositionsAndFirstDerivativesThroughoutAParameterCell：无UV实际Bound与独立点/导数求值的包围检查。

section测试调用的是**本项目托管KernelRuntime**，需要schema环境但未调用厂商PK session。按统一private-env工具ensure本review树native profile，原exit0 / ENV_READY，PKToy pin `d820719b6d8510d623972ab9d8efc65edbc312f9`；只用ignored配置，无SDK/schema/许可证/凭证进入提交。没有触发完整内核构建。

### 新增非finite毒值负控（真实Jac，三份固定DLL）

新增docs内 `NonfinitePoison.cs` 不改生产或正式test：通过现有friend名取得实际类型、反射为真实私有Occurrence/contact/Jac创建强类型delegate，不复制布局或guard。63项cache全部填0xFF，事前确认 **Ready=true且Values首下界为NaN**，然后对两正交平面/线性Y gauge调用实际Jac。expected矩阵独立制造为dX/dZ/dY。

| 实际加载 | 原exit | 22176字节cache与结果 |
| --- | ---: | --- |
| base testhooks628… | 0 | 全span变0，原clear确实执行 |
| guard testhooks9a0… | 0 | 全span仍FF；独立矩阵有效 |
| guard普通production12e… | 0 | 全span仍FF；独立矩阵有效 |

三者打印的九项上下界位模式完全相同，包括signed zero。它验证的是未清非finite/假Ready在此真实无UV调用中不可观察，不依赖new数组为零；全读集证明仍以源码支配关系为准，不用几个控制点代替证明。

初版该probe的deps预载了普通production DLL，尽管请求base，实际加载hash日志显示12e而非628，因而“base应clear”的断言失败exit1；**这是reviewer loader setup错误，未当作候选算法失败或通过**。最终先编译小probe，再仅在其私有输出目录换入固定DLL/PDB、启动全新子进程，强制比较requested/loaded SHA后才验断言；三份实际hash/MVID均正确。原setup-error日志保留。没有改测试数学预期去迁就错误DLL。

## 复现、证据与接受边界

新持久工具均.NET10，路径相对脚本或编译产物；只构建小验证程序，不重建kernel。根目录示例：

```sh
MSBUILDDISABLENODEREUSE=1 P_SCHEMA="$PWD/third_party/parasolid/schema" dotnet run --no-cache docs/reviews/advanced-modeling/cache-empty-7aca-evidence/AuditAndFacts.cs -p:UseSharedCompilation=false -- --facts
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache docs/reviews/advanced-modeling/cache-empty-7aca-evidence/NonfinitePoison.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/AllocationBanCheck.cs -p:UseSharedCompilation=false -- ../temp_docs/cache-empty-7aca-review/production/ProjectGmKernel.Native.dll
```

上述三条本轮实际原exit均0；另一次无`--facts`的最终离线audit原exit0，补完324源与Test DLL/PDB绑定，没有重复数学测试。author最初脚手架CS9203/CS8347编译失败原exit1也保留，未执行测试，不计算法失败。

ignored证据根 `temp_docs/cache-empty-7aca-review/`：original保留作者raw日志/TRX/exit/manifest/dump及自家DLL/PDB；runtime/production为独立运行副本；audit-facts.log/.exit、final-offline-audit.log/.exit、nonfinite.log/.exit、ff-base/guard/production.log、allocation.log/.exit和private-preflight.log/.exit均保留。关键SHA256：audit-facts `a0d55b32616407821256e27382e2a44ba4eec3e60fdcc1afd12fb7f7750d1895`；final-offline-audit `e9f8484eb3b38fce35cf30714049417d7780f6d51a584cd9123bbedfb507b443`；nonfinite `c2d54f40bb0d38a27cd40d8fbaf84dc3b4eb587cdd0e25ad22bc80389c899605`；allocation `9c63a0b08b0790550e84343dd2ff989bc2d6b64d328c1a0e93776b095bc48faf`。

这是“无UV时理论上减少一次完整clear请求”的实际代码验收，不把请求字节合计称DRAM流量，不推算CPU/百分比/净收益，不宣称旧95%具体成因已证。条件扫描成本、实际收益和完整GM/PK目标仍未测；不能从一个无UV代表推所有case无UV。

本轮没有新Mac/真实PK运行、性能A/B、profiler、5case/S2、19/151/full、历史45pair重验、AOT、main整合或live部署，没有改生产源码/正式测试期待/其他工作树。小程序均已退出；直接Fact调用没有起testhost，因此未进行全局testhost清理或影响其他worker。仅提交本报告及两个docs复现工具。后续可由协调端安排此固定guard的整合；新数学分支必须以将来的固定合集另验。
