# Planar sheet 所有权修复

完整 oblique 种子序列在调用 Blend 前就留下 4 curve / 4 point。进一步最短控制表明，仅 `BodyCreateSheetPlanar → EntityDelete` 也失败：`profile-owned-before.*` 三项均失败，原 exit 1；其第一个 pool 断言直接记录 4 curve 残留，原完整序列的 point 残留另见 Blend owner 保留的原日志。问题不能归到 Blend 或单独归到 Extrude。

wire 已经通过构造发布登记曲线与顶点点的所有权。`EdgeMakeFacesFromWireCore` 改成 sheet 后又对全部边界调用 `RebuildBoundaryGeometryLinks`，使原引用重复计数；随后的 identity basis transform 也会把这些误计的引用当成共享几何。修复仅维护新 face 的 surface chain 和新建 plane 的一个所有权引用，保留原曲线及点的引用计数，不清池、不全局重置 owner。

`ProfileModelingTests` 与 `GeometryOwnershipTests` 实际 **42/42，原 exit 0**，包括 sheet 单独清理、oblique extrusion 两种删除顺序、已有共享曲线与回滚、分配失败恢复；曲线、曲面、拓扑、点与 return block 计数恢复。allocation IL guard 2952 方法 CLEAN。命令：

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~ProfileModelingTests|FullyQualifiedName~GeometryOwnershipTests'
```

独立真实 PK oracle 使用原 oblique 输入：四角 `(-2,-3,0),(2,-3,0),(2,3,0),(-2,3,0)`，路径 `(.35,0,sqrt(1-.35*.35))`，距离 8。双方分别创建 sheet 与 extrusion；删掉各自原 sheet 后才检查 extrusion。两个操作分别 fresh receive、完整 `BODY_check`、当前共享 `AssertEquivalent` 均零差异，物性均 Verified；每轮删除产品后的 `(body,curve,surface,point,returnBlocks)` 均从全零回到全零。没有修改比较 helper、输入方向、模型或容差。

```
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/PlanarSheetOwnershipOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=sheet-and-oblique-fixed-v2
```

原 exit **0**，日志 `temp_docs/pk-native-acceptance-audit/profile-native-owned-v2.log`；实际 GM assembly SHA256 为 `60a9bf93e15af03e80a681f1bd5fafd0df792b9f2b573c9d3eaf4354216a96d2`。输入/产品/15 分量积分界保存在 `temp_docs/planar-sheet-ownership/sheet-and-oblique-fixed-v2/`，source 与 artifact hashes 分别在 `profile-owned.source.sha256`、`profile-owned.artifacts.sha256`。初始编译失败记录保留，不计成建模失败。

该修复不依赖尚未提交的 continuous Boolean 三曲面证书研发；验证时已将那些改动独立保存。计算改动只有 `KernelRuntime.ProfileFaces.cs` 的所有权登记，附带上述最少回归及 oracle，可独立交 Blend owner 整合。它没有签署仍失败的六项连续 Boolean，也不代替 Blend 完整功能的集中独立验收。
