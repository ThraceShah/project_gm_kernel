# 重载圆柱的有限支路关联

完整单测实测发现两项 `MixedAnalyticBooleanTests.ReloadedPartialCylinderCanBeCutAgainAcrossItsAngularBoundary` 失败，原 API 67/not_on_curve，Arrange/BoundaryAssociation。两种 seam 均在同一几何类别复现：box 减去 oblique cylinder，XT 重载，减去平行于轴线的 box，再次重载后继续切割。

固定输入 cylinder：location `(-2.25,0,-2)`、axis `(.6,0,.8)`、radius `.3`、height `7.5`；seam 选用两种原生 reference。实际事件点为 `(-1.1035533905932737,.10000000000000009,0)`，候选为另一条有限原生 Line，区间 `[2.7121320343559643,5.212132034355964]`。原始诊断在 `temp_docs/pk-native-acceptance-audit/cylinder-native-branch-before-v1.*`，两项原 exit 1；临时诊断 callback 已移除。

两个事件同时属于同一 plane/cylinder，不代表属于同一 generator。此前精确双支证明不适用于这个 binary64 轴向，随后把另一条 Line 的反求失败直接当成整个操作失败。修复不归一化或修改该轴，不改参数、容差或拓扑身份；对事件已有精度盒与原生 Line 的叉积作外包围。任一分量严格不含零，就证明整个盒与整条直线分离，排除这一支。反求失败本身仍不能证明无交；没有严格间隙时保留原失败。

计算变化仅 `KernelRuntime.BoundaryAssociations.cs` 的九行。相关 MixedAnalytic 与 FinitePlane 现有正负控制 **143/143，原 exit 0**；包含两个连续重载闭环、分配失败/回滚、零分配，以及距离小于 `1e-8` 的双 generator 不按空间距离合并的原控制。

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests/KernelTests.csproj -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~MixedAnalyticBooleanTests|FullyQualifiedName~FinitePlaneSeparationTests'
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedAnalyticBooleanOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=native-generator-branch-fixed-v1
```

后者实际 **18/18 几何/拓扑/材料对照通过，原 exit 2**，30 次 mass comparison 未决。实际 GM/PK 每一步分别执行，包括前序最多两次 subtract；前序独立 PK live copy 分别严格比较 GM fresh XT，后续不能补签前序。所有最终产品均 fresh receive、完整 BODY_check、共享严格 AssertEquivalent、原材料探针；没有手工修改比较 mask。日志 `cylinder-native-mixed18-v1.*`，输入/中间/最终字节在 `temp_docs/mixed-analytic-boolean-oracle/native-generator-branch-fixed-v1/`。

oracle 改为必须提供新的 `--output-name`，每轮独立保存来源及原生结果，不覆盖原 shared `reload-source.x_t`；统一复用当前 mass bound 合同和 AssertEquivalent，移除其旧的重复比较实现。当前完整 suite 是修复前固定代码的一次执行，包含上述两个真实失败；必须与此窄复验分别登记，不能把全套原始结果改写为全绿。
