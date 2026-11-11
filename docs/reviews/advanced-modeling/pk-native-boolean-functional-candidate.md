# PK 原生 Boolean 功能候选

固定计算代码：`4794e60111e1382eafe69880077544fcc135521e`，分支 `feat/pk-native-modeling-delivery`，已提交并推送。该包等待集中独立验收，尚未合入 main、接受为整体完成或上线新看板 run。Blend `60be721` 与性能包由各自 owner 独审，本包没有混入其尚未接受的比较语义变化。

项目原生模型继续为 XT，保留 PK BLEND / ICurve / CHART、native 参数、所有权和容差。实际固定 diff、PK 定义及动态依据分别见 [连续有限根](continuous-native-boolean-roots.md)、[圆柱有限支路](native-cylinder-generator-association.md)、[sheet 所有权](planar-sheet-ownership-fix.md)。没有新持久模型、象限/端点 epsilon 吸附、换输入或历史兼容路径；没有公共 C ABI 变化和 AOT 发布，没有性能长测。

## 已执行结果

日志均相对于 `temp_docs/pk-native-acceptance-audit/`。表中 exit 是原命令退出码；未决物性及失败注入没有记成成功建模。

| 实际范围 | 建模及产品结论 | 原日志 / exit |
| --- | --- | --- |
| NURBS/解析混合、闭交支、空交/包围、多工具、连续/重载、同域及不同有限 trim 的并交差 | 45/45 最终操作双方 API 成功，独立 fresh receive、完整 BODY_check、原严格 AssertEquivalent、拓扑/finite UV/材料通过；前序 subtract 另行比较；40 次 mass comparison 未决 | `native-boundary-native45-v2.*` / **2** |
| oblique cylinder、seam、镜像、有限角域重载、连续最多三次 Boolean | 18/18 最终操作及前序最多两次 subtract 分别通过独立严格/完整检查和材料探针；30 次物性未决 | `cylinder-native-mixed18-v1.*` / **2** |
| shortedge 标准自构造及完整生命周期 | 自构造产品、copy/delete 源、反射/缩放、后续 cut、可编辑 XT 重载和第二次 cut 均通过 strict/full/有限 ICurve/UV/材料；8 次物性未决 | `native-root-standard-lifecycle-v1.*` / **2** |
| 两工具 Boolean，同字节 target/tools 各交 GM 与 PK；正反选边顺序分别执行 | 每轮双方成功/一个实体，fresh/full/strict 通过；各一次物性未决 | `native-root-consumer-forward-v1.*`、`native-root-consumer-reverse-v1.*` / 各 **2** |
| 同场景注入分配失败 | GM 实际 901/failed/零产品，三个原输入 byte-identical 回滚；同输入 PK 实际成功；控制通过，不是 GM 成功建模 | `native-root-consumer-inject-v1.*` / **0** |
| 最终代码的 Boolean 及有限根相关集中回归 | **290/290**，allocation IL guard CLEAN；原生 DLL 与测试 DLL hashes 已保存 | `boolean-final-candidate-regression-v1.*` / **0** |
| 原固定 11c 计算源完整单测 | **1907 通过、2 失败、1909 总项、19m31s**；两项圆柱重载失败没有改写为绿；4794 的修复另由 143 相关项及上述真实 PK 18 项复验 | `native-boundary-full-suite-v1.*` / **1**；`cylinder-native-branch-fixed-v1.*` / **0** |
| sheet → oblique extrusion → delete 源/产品 | 两操作 fresh/full/strict、物性 Verified、所有 geometry/return pool 回零；Blend owner 已复用该独立修复 | `profile-native-owned-v2.*` / **0** |

此处物性未决是共享合同的实际结论，不等于几何失败或物性通过。最大合法 `accuracy=1` 的原生控制仍未能缩窄相关界，详见 [实际输入控制](native-mass-maximum-accuracy.md)。仅 `n_cht_pts` 为原比较 helper 允许的诊断；本包没有新增 sense、类型、missing 或偏差降级。

## 输入、源码与产物绑定

- `boolean-candidate4794.source.sha256`：SHA256 `2a053246b80d12f66cc4751b65ae2eecc97410aa02c53a1a372125cf9c3b608e`，包含当前计算源、实际 oracle、相关单测与架构合同。
- `boolean-candidate4794.artifacts.sha256`：SHA256 `27d65a08779b6da65a6a613b86c4ec5432e2a927fca4131a1943fa71d389a894`，逐文件绑定下列真实输入/中间/最终产品和物性数据。
- `boolean-final-candidate-regression-v1.assemblies.sha256`：最终 290 项实际 DLL hashes；不同脚本的实际 loaded GM assembly 另在各自日志首行，不用同一 hash 冒充所有构建。
- 45 项 loaded GM assembly：`3cd502554fb5ffb09ef477571b0f4c69f8a63f5c8fe24251ccde23ac82e663b3`；18 项：`2a41c9f1644eab59187da3ce754ebc9b2b3229f790f1203e7c5b4733c9db586f`；标准生命周期：`b82dcb0c4eeb26dcb67704514981e843802e9612ebf5d4e8f6ca606c93eda099`；三个 consumer：`091007a4b22a9efb977cb24f927729971ed9a184b56de64cf9a65cd9adf2a0f5`。

上述 manifest 位于 `temp_docs/pk-native-acceptance-audit/`。产物目录：

```
temp_docs/regular-curved-boolean-oracle/native-boundary-routing-fixed-v2/
temp_docs/mixed-analytic-boolean-oracle/native-generator-branch-fixed-v1/
temp_docs/short-edge-blend-oracle/native-finite-root-lifecycle-v1/
temp_docs/contract-sol/native-finite-root-forward-v1/
temp_docs/contract-sol/native-finite-root-reverse-v1/
temp_docs/contract-sol/native-finite-root-inject-v1/
temp_docs/native-acceptance-contract/maximum-accuracy-actual-block-v1/
```

当前 shortedge 自构造原产品的 SHA256 为 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`，与固定历史 self-probe 产品及已接受 f9 consumer 的 target **同字节**。两个 tool 分别为 `ad885c46aeb0c22a384cc4f3c567cbdc0030ed55c30a57ee99af98d4d186aec2`、`8056a1bf6862831b1d4de1da36af94f6bd5164fcaf019b5f5fc8d51644c40792`；`inputs.json` 和 `boolean-options.bin` 绑定双方相同输入、选项及真实操作。

## 复用证据及明确边界

固定 f9 证据包与独审保持只读：`temp_docs/pk-native-delivery/evidence-final.sha256`，SHA256 `94752804be4ac709fbb4c1104b110b754787121c79a695b6f39723a5a12898c5`，独审 `review/pk-native-f9c5920@4c7646e1085e6fa786b2f335cabc7c086c008e13`。原 96 参数/方向/选边矩阵、支持身份/孔与相关生命周期按固定范围复用，不冒称本次重新执行。

原 self/import 各 510 固定探针的原字节、15/16 差异与退出码保留。当前共享合同的 imported replay `probes-import.*` 原 exit 2、Different 0、Unresolved 16、API error 0；native 自身 roundtrip 的 7 行仍列为独立事实，不能用于豁免 GM 的行。有限距离诊断仍不是严格距离上下界证明。本次没有把历史 self 510 重签成当前新执行或全部相等；当前同字节产品及未修改的 classifier 路径提供复用边界。

Blend owner 的额外 `--managed-material` 对 5 个 cylinder fillet/chamfer 产品返回 5000，属于另行实测的 GM 分类查询不支持，不能改写成 Blend 几何失败或“所有查询通过”。FaceAttach 的负 surface 方向组合目前只是已记录的静态疑点，没有在本包冒称实测失败或擅自增加兼容分支。任意高价/奇异/未证明接触事件继续显式拒绝并回滚，不属于这 63 个已证明最终 Boolean 的支持声明。

## 可复制命令

先使用已授权本机 `gm-private-worktree-env` 准备 native profile，必须得到 ENV_READY（本机 Linux x64 PK 38.0.150、.NET 10.0.302、固定 PKToy）。以下在项目根运行，输出名必须换成新的名字：

```
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RegularCurvedBooleanOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=new-regular
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/MixedAnalyticBooleanOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=new-mixed
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/ShortEdgeBlendOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=new-lifecycle --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RollingBooleanConsumerOracle.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- new-consumer
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests/KernelTests.csproj -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~Boolean|FullyQualifiedName~FinitePlane|FullyQualifiedName~IntersectionEvent|FullyQualifiedName~SurfaceTripleRoot|FullyQualifiedName~OffsetCornerRoot|FullyQualifiedName~NativeSplineBoundaryRoot'
```

consumer 加 `--reverse`、`--inject` 分别复现选边顺序与原子回滚，不混成一次操作。测试原 exit 先保存，再仅清本次 PGID 的 testhost。完整 suite 原命令见 [连续有限根记录](continuous-native-boolean-roots.md)。

## 看板交付清单

1. 绑定固定 computational SHA、source/artifact manifest、原 command/exit、实际输入和每个操作产品；几何、拓扑、物性、分类、回滚和整体状态独立显示。
2. 两个 oracle 的 `STEP_API side=gm/pk` 与 `first-subtract` / `second-subtract` / final 必须分别呈现；不得用后序结果补签缺失的前序产品。新前序 XT 文件绑定 native live-copy 比较结果，不能由预览重建冒充。
3. `mass-properties.csv` 按现行 15 分量/模数/外界/状态显示，exit 2 显示未决；510 历史与当前 imported 合同分别保留，native 自差不消去 GM 差异。
4. 原始 fullsuite 1907/2 与修复后相关 290/143 分别显示，不合成虚构的“本次全套 1909 绿”。注入 901 是回滚控制通过。
5. 独立验收完成后，由看板 owner 以实际接受/整合的最终 source SHA 创建、Capture/Refresh 并发布新的线上 run；本计算 worker 不修改其树、main、门户或历史红 run。线上 source、输入/产物 hashes、操作结论与此交接逐项核对后，才结算整体交付。
