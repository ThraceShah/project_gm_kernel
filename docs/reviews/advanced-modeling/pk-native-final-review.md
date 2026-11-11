# PK 原生建模候选独立整体验收

评审日期：2026-11-11。固定候选 `f9c59201c0b9c5d103dee45124e3f43ca59a0921`，比较基线 `610e3ce844bf99c85af6069905f9f3deaab08133`；评审分支 `review/pk-native-f9c5920`。本报告只评审该候选，不修改实现、原输入、精度或验收阈值。

## 结论

**接受候选在下列已验证操作范围内的计算交付；阻断“整体 M0–M5 已完成”的结论。** 本次未发现六个生产改动文件引入的、需要否决这些已验证操作的新增重大模型或数学问题。这个结论来自实际源码、原件校验和独立运行，不代表任意 BLEND/ICurve、多方事件或全部建模操作均通过。

整体仍有两类原验收红项：横切后的两次原始 `inertia.yz` 点值门槛，以及 self/imported 各 510 个固定探针中的 15/16 个分类差异。原外部退出码均为 134，不能由其他绿色运行代签。未证明这些探针差异是本次增量引入，也未证明它们全是可忽略误差；同样，横切失败不反向撤销独立通过的未切割 Fix 或标准切割。

尚无本候选同实例的 DisplayBody receipt/mesh。本次没有部署、合入 main、建立 Mac 性能结论或接管用户惯量调查树。

## 依据和评审边界

已读实际用户级约束、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、`docs/Finite_Analytic_Rolling_Network.md`、原计划 `docs/reviews/advanced-modeling/plan.md` 的 M0–M5 合同、实施计划和原验收记录中的相关定义/失败链，以及 `docs/reviews/advanced-modeling/pk-native-modeling-delivery.md`。读取了候选相对基线的全部 13 文件 diff；生产改动仅为以下六文件。

| 生产依据 | 本次检查与结论 |
| --- | --- |
| `src/ProjectGmKernel.Native/Geometry/Evaluation/CompactBranchCertificate.cs` | 有限原区间和紧凑区间均按闭盒覆盖，要求区间根存在/唯一及导数沿弦严格为正；不是只测原 CHART 节点。弦误差和角度判定使用向外界；角度报告本身只作诊断。 |
| `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendBounds.cs` | 共享见证包含全部状态分量，附带嵌套 occurrence 的 spine/cell；固定根 CHART 的预条件点取自同单元。未将不相容子单元仅因 XYZ 接近而视为共同方程。 |
| `src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedCurveArc.cs` | 保留原 CHART 的 UV sheet，使用原生弦投影 gauge；证书成功后才绑定紧凑 spine。未证实时保留既有原生完整定义，不发布未证紧凑表示。未新增旧格式/旧 API 兼容轨道。 |
| `src/ProjectGmKernel.Native/Runtime/KernelRuntime.NestedBlendCompilation.cs` | 验证 spine 复制存储原 CHART 到已认证相同的支持上；发布 Prefix 排除临时验证 spine。现有 span/scratch 和有限容量合同不变。 |
| `src/ProjectGmKernel.Native/Runtime/KernelRuntime.AnalyticBlendContact.cs` | 保留已有认证的源直线根时要求 witness、有限 cut station、正确 contact side、等价 support chart、精确支撑/共线条件和源根区间；roundoff 位置检查是附加检查。 |
| `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBooleanCuts.cs` | 只在对应实际 root parameter 事件入口保留源点，继续以原 occurrence、generation、witness 和有限隔离区间登记事件。没有新建位置合并规则，发布仍经过共同 arrangement 和事务。 |

关键前提另外沿调用链核对：`KernelRuntime.Copy.cs::CloneBoundaryCurve` 的 `SameChart` 要求 Identity 和正向 NormalRelation，才把实际 face support 传给压缩；`SpineParameterMap.cs` 的根 gauge 为 `e·(x-P) - (t-t_i)/f_i`；`NestedBlendIntervalSupport.cs::IntervalResidual` 的其余支持、最近点和子 gauge 方程一致；`NestedBlendBounds.cs::CertifyNearestBoxes` 保留嵌套最近支的正则性/方向条件。

### 共同根和全段推理为何不只是 within-box

原根在站点的完整状态由原系统的区间证书包围。两条根 spine 在同一编译程序中使用相同的两个 support 索引；子 occurrence 的 spine/cell 必须一致。两套方程只允许根的 CHART 平面不同。把原根代入紧凑弦的 gauge，得到它自身的紧凑参数 `p*`；代码包围 `p*`，并把它和发布 station 一起包含在紧凑参数盒内。原完整状态被紧凑唯一性盒包含，并且满足该系统在 `p*` 的共同支持/子方程和由定义成立的 gauge 方程，因此唯一性才允许认定它是 `p*` 的同根。只拥有相近的采样点或仅在盒内而没有这些方程前提，不足以得到该结论。

随后两条有限弧在共同弦 level 上导数严格同向，完整区间的正则性和连续延拓使共同解不能在中途跳到另一分支。这里的结论是**同一正则解析分支及有界端点联系**，不是原/新参数值相同，也不是所有有限端点实数坐标完全相等。发布 station 与 `p*` 处原根之间由收缩后空间盒直径约束在 `1e-10`；低余弦端点负控验证此界使用空间距离，而非用 gauge 差直接充当空间差。

盒预算 262144、深度 40，未知/奇异/非有限/工作区或预算不足均不能通过紧凑证书。原生完整 CHART 保留路径是同一现行 XT 模型的表示选择，不是新增历史版本兼容。前置选站启发式不承担正确性证明。没有 SharedPlane/R3 frame 替换、拟合后转回 XT、全局提高实体 precision 或放宽原 `1e-7` 物性门槛的生产改动。

这属于源码论证和有限风险验证，不是对任意非法 CHART 输入的形式化证明；合法支持及现有 joint interval/evaluator 合同是前提。

## 真实 PK 定义核查

本次实际使用 parasolid-research 及本机已配置 Parasolid 查询 MCP。`PK_SURF_intersect_surf`、`PK_CURVE_parameterise_vector` 和 `PK_DEBUG_BODY_compare` 查询实际返回 found；错误码查询按工具真实参数 `query="157"` 得到 `PK_ERROR_cant_do_intersect`。首次错误地传 `name` 的查询返回泛列表，没有作为特定错误码证据。本次使用配置中的查询通道，没有把 HTTP 门户可达性冒充动态调用，也没有批量反编译。

同时核对本机官方 XT Format Reference 第 6 章的 CHART/LIMIT/BLEND 定义：`f_i=f_(i-1) cos(b_i)/cos(a_i)`、弦投影参数以及 help/terminator/artificial/boundary 的不同语义。help limit 不是任意闭 ICurve 的恒定可加周期。候选没有改写这些原生定义。这里只记录定义和核查方法，不复制厂商文档正文或私有二进制。

原作者非均匀 CHART 的真实 PK eval/inverse 日志和来源已核对；它覆盖 F2/F3 两种尺度的闭合两侧及端点，不扩张为 terminator、高阶或全部 BLEND_BOUND 已通过。

## 原证据核对

原件根目录为 `../gm-pk-native-delivery/`。原索引 `temp_docs/pk-native-delivery/evidence-final.sha256` 的实际 SHA-256 为 `94752804be4ac709fbb4c1104b110b754787121c79a695b6f39723a5a12898c5`，与派发固定值相同。于原树只读执行 `sha256sum -c`，外部退出 0，**726/726 条目全部匹配**，包含日志、原 `.exit`、源快照/diff、输入和输出 XT。索引校验成功只证明所列原件匹配，不补签任何历史建模操作。

本评审临时校验输出为 `temp_docs/pk-native-review/author-manifest-check.{log,exit}`。临时 C# 审计器 `temp_docs/pk-native-review/EvidenceAudit.cs` 读取原件：八个 shard 各外部 exit 0、各 12 个唯一 case；96 个名称与角度 4 × 半径 3 × 容差 2 × 顺序 2 × update 2 的预期集合完全相同。逐 case 存在 self Fix 0、managed full0、有限 ICurve/双侧 UV、mapped geometry 和原 mass 记录，无 FAIL。每 shard runner hash 均为 `af77db29008f9751c2a159e69d26ddc08b7ca6350baffc53d6c5a9cf20e99828`，等于候选脚本。

审计器核对作者 `a7cf09f-sources.sha256` 中全部 **369 个 src 文件**与候选一致，最终四个 runner/comparator hash 也一致。`full.head` 为 `2922ff526908bb9290371d33967e27c1849049b2`；其 `full.diff` 仅是两个 runner 的记录改动，`git diff 2922ff5 f9c5920 -- src tests` 为空。`intersections.head` 为 `74d136b9ed8c3b9894e6dc337942b78d3f23c8b7`，与候选的生产源码及 intersection/comparator 脚本无差异。只读比较对象用于来源核对，未进入或改动用户调查树。

| 原执行证据，均位于原树 `temp_docs/pk-native-delivery/` | 可采用的事实和限制 |
| --- | --- |
| `full.{head,diff,log,exit}` | 1885/1885，外部 0，19.5104 分钟；本次复核来源，不重跑。 |
| `matrix-shard-0` 至 `matrix-shard-7` | 96 个 self 构造/几何 case 完整通过；不等于 96 个均跑生命周期，也不等于每个均启用另行可选 native fin box/face domain 检查。 |
| `matrix.{log,exit}` | 初次串行运行外部 143；不能计入 96 完成数。 |
| `intersections.{head,log,exit}` | 116 独立求交合同，外部 0；另列的 native corrupt-wire 边界未作为兼容通过。 |
| `standard`、`regular-bore`、`support-chain`、`separated-network` | 外部均 0；分别支持标准生命周期、实际修改支撑面保孔、等价支持记录以及非邻接带的独立操作范围。 |
| `input-rigid`、`input-mirror`、`corner`、`analytic-local` | 外部均 0；输入先变换代表、七角点/消退变体、五个选定轴向倒圆/倒角；不扩大为全部本地操作目录。 |
| `comparator-controls` | 外部 0；6 正例及 5 个尺寸/trim/内部/pole 错误负例。 |
| `transverse`、`probes-self`、`probes-import` | 三者原外部均 134，详见红项。 |

临时审计器首次因相对根目录多退一级退出 134，未运行建模；保存在 `evidence-audit-initial.{log,exit}`。修正自身读取路径后的 `evidence-audit.{log,exit}` 外部为 0；增加逐 case 严格比较及 finite 记录检查后的 `evidence-audit-final.{log,exit}` 也为 0。各次均未改原件，没有把前次失败覆盖成成功。

## 少量独立运行

运行前使用现有统一私有工具 ensure `--profile native`，外部 0/ENV_READY；Linux x64，PK 38.0，固定 PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`。没有自行安装或修改全局工具配置。真实调用使用项目现有托管 API、PskernelSharp 和 ParasolidScriptHost；独立 PK 与 GM 库没有重定向。

共同构建参数为 `MSBUILDDISABLENODEREUSE=1`、`-c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true`。每个原外部状态先保存再报告，临时记录前缀均为 `temp_docs/pk-native-review/`。

| 独立执行 | 原外部结果 | 验证范围 |
| --- | --- | --- |
| `focused.{start,log,exit,end}` | 0；28/28 | `CompactBranchCertificateTests` 全部正负控、`InteriorSourceRootPreservesTheStationaryPlaneConstraintAndItsFiniteBranch` 真实 Fix/删除/重载入口、`ProjectedRollingCellFailureRestoresTargetAndBothToolsAfterUnion`。其中 26 个证书 case，另两个为真实源根和回滚测试。测试后只清理自有 PGID 的 testhost。 |
| `consumer.{start,log,exit,end}` | 0 | 新 self Fix 后把同一 target、两 tool 的真实字节送给两边；GM 与 PK 均 error 0/result success/一个 body，投影替换 reached，fresh receive/full0、严格比较和原 mass 通过，normalized15=`7.701450779942389e-11`。 |
| `inject.{start,log,exit,end}` | 0；预期 GM 901/result failed/零 body | 投影替换 reached 后注入发布分配失败，三个输入原字节回滚；PK 同字节正常 success/一个 body/full0。诊断确认实际生产压缩证书成功：2/2/10 站点、4/5/8828 个接受盒，最大空间 link `1.7188375197174616e-13`。 |
| `standard.{start,log,exit,end}` | 0；PASS cases=1 | 独立构造 Fix、copy 后 delete source、反射/一致缩放、首切、editable XT reload、再切及 finite/UV/material/原 mass 全部通过。self Fix、反射副本、首切、再切 normalized15 依次为 `3.965655017106088e-9`、`4.544547029326304e-8`、`2.3886265011558517e-8`、`2.2404188966449608e-8`。 |

具体命令：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~CompactBranchCertificateTests|FullyQualifiedName~InteriorSourceRootPreservesTheStationaryPlaneConstraintAndItsFiniteBranch|FullyQualifiedName~Projected' --logger 'console;verbosity=normal'
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RollingBooleanConsumerOracle.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- review-f9c5920-consumer
PGM_XT_DIAGNOSTICS=1 MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RollingBooleanConsumerOracle.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- review-f9c5920-inject --inject
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=review-f9c5920-standard --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership
```

输出名不能复用，复现须另取新名。新 XT 仅在本树 `temp_docs/contract-sol/review-f9c5920-{consumer,inject}/` 和 `temp_docs/short-edge-blend-oracle/review-f9c5920-standard/`，没有覆盖作者目录。

### 本轮审计收据

`docs/reviews/advanced-modeling/pk-native-final-review.sha256` 固定本次 67 个条目：32 个新输入/输出/选项产物、三个 runner/comparator、结算时两个托管构建产物，以及原日志/exit/UTC、审计器和源码快照记录。实际 `sha256sum -c` 为外部 0、67/67 匹配；收据内路径均相对项目根。原日志及 XT 仍保存在本机忽略目录，未将私有依赖上传。`source.head` 固定候选，`source-working.diff` 为空；两 DLL hash 是运行结算时的构建目录快照，未冒充各进程的动态加载模块测量。

| 运行 | UTC 开始 → 结束 | 原 log SHA-256 |
| --- | --- | --- |
| focused | 12:06:37 → 12:09:38 | `da6ef2692f7c700854b42616c19e0dc355717a9e7c066846e1618577bd5f8758` |
| consumer | 12:09:11 → 12:10:59 | `d840f2900ac902f57002d33c66bbc814271e9041ceecd2670584fa8292d8580e` |
| inject | 12:11:22 → 12:13:06 | `82ea84cfc276cfa5cc8cdc9a507948425305dacd5f656e916f53a29570f45aa4` |
| standard | 12:10:09 → 12:13:44 | `38b30120d7c7912ef273c0ff80596b267f784fb603ec6574b80ff133eb90fb8b` |

上述日期均为 2026-11-11，时间包含构建，不是排他 CPU 时间。运行有重叠，不能据此做性能排名。自有进程已结束，没有残留本任务 testhost；未清理其他任务进程。

### 严格 mask、有限边和生命周期的实际范围

实体使用现有 `scripts/ParasolidBodyCompare.cs::AssertEquivalent`：`max_diffs=1024`、`all_tests=false`、`acc_dev_tests=false`、`tol_dev_tests=true`、`non_match_tests=true`，验收 global/local，仅 `n_cht_pts` 可诊断。另验 body type、region material 和 shell 数；不用 face_pairs 等于面总数作条件。每次结果调用 `PK_DEBUG_BODY_compare_r_f` 释放。BODY_check 使用 PKToy 默认全部几何/拓扑检查，`max_faults=100`，未关闭几何项。wire 另做有限边匹配、类型/sense/端点/trim 和双向距离采样；采样不宣称任意曲线的完整等价证明。

116 runner 的实际 mask 是 `max_diffs=64`、`non_match_tests=false`，并显式开数量/类型/sense/容差/missing/tol deviation、关 accuracy deviation；另外调用 `AssertWireGeometry`。这份 wire 证据不能被表述成与上述 solid 完全相同的 mask 或完整实体比较。

标准独立生命周期与固定矩阵的 geometry/finite UV 是实际检查，最强 native fin UV box/face domain 验证按原代表运行单列；本次标准命令没有加这两个选项。没有重跑整 96/1885、PK 全 347、Windows、无 C ABI 改动的 NativeAOT 或 Mac 性能环境。

## 仍红及不能替代的证据

1. 原 `transverse.log` 两次 self cut 在 accuracy `0.99999999999` 下 normalized15 分别为 `1.2044611267910417e-7` 和 `1.126468644220207e-7`，均 worst=`inertia.yz`；原 `1e-7` 门槛不变。两次实际 API 已 Publish，strict/full/finite/native witness/material 通过，完整命令仍失败。这只支持“可构造并通过这些检查”，不支持完整 transverse 验收绿色。
2. 原 self/imported 510 重放各为 15/16 差异，零 API 错误、`native_class_changed=0`，原外部 134。看板必须保留红色，不能沿用旧交接中的“原生基线也全改变”解释。
3. 同字节 consumer 的目标 hash 为 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`，两把刀为 `ad885c46aeb0c22a384cc4f3c567cbdc0030ed55c30a57ee99af98d4d186aec2`、`8056a1bf6862831b1d4de1da36af94f6bd5164fcaf019b5f5fc8d51644c40792`。横切从独立构造 Fix 后的不同目标继续：self 反射目标 `57eecc0294734c805d3ded1ab034fc4c78dec6da6d2300a6ce85a0e553981ad2`，独立 native 反射目标 `3047cc3e0d7366fdb655dfae534ad9bc0fe32183443f55cd333923ba891e6a99`。输入身份不同，不能互签。
4. `native-rechart.log` 的 CHART 10→73 是改变 XT 定义/gauge/实际有限域后的诊断，不是相同字节的重复测量。finite 积分收敛和 modulus overlap 不证明真值，也不能证明 PK bug；本报告不采纳替代验收、改坐标或换输入的方案。
5. `topology-event-framework-handoff.md` 和原 `plan.md` 后附总结仍保留旧完成性叙述，其中 sample-only `NestedBranchStationMap` 和 imported-only 矩阵不是本轮依据。以固定候选的本报告及已核原件作为此次状态依据；本次没有重写历史文档或原测量。

## 可交看板的状态

| 操作/能力 | 当前可展示的真实结论 | 不可宣称 |
| --- | --- | --- |
| 当前 self 异构短边 Fix；96 参数矩阵 | 真实自建并导出，PK receive/full/严格比较、有限 ICurve/UV、mapped geometry 和原 mass 在执行范围通过 | 任意输入、全部生命周期、所有额外 face domain gate 全覆盖 |
| 标准 self 生命周期 | 本次独立外部 exit0；真实自建、复制删源、反射缩放、切割、可编辑重载再切全链通过 | 用它替代 transverse 失败 |
| 同字节双刀 Boolean | 本次独立成功，保留 target/tool/options/输出 hash；注入失败另列 | 等同于从独立 native/self 构造结果开始的横切 |
| transverse 两次 cut | API/结构/有限边/材料通过，原惯量门槛红 | 整个 transverse 流程通过 |
| 固定 self/imported 510 | 分别 15/16 差异，红 | 510 全通过或 native baseline 已改变 |
| 保孔/等价支持链/非邻接组件/七角点代表/五轴向操作 | 作者固定日志与源码身份已核；按各自执行范围展示 | 所有局部操作、全部退化/多方事件完成 |
| 独立求交 116 | wire 合同范围通过，附实际 mask 和原生限制 | solid 同 mask 或任意曲线严格全等证明 |
| 回归 1885 | 来源/源码一致的作者实际完成记录，外部 0 | 本评审重跑 1885 |
| DisplayBody/mesh、正式性能 | 本候选无新同实例显示 receipt；未做正式性能验收 | 610 的旧显示是本轮成果，或已超过 PK |
| M0–M5 总交付 | **未完成，整体验收仍红**；保留上游各独立通过项 | 已全部接受、合 main 或已上线 |

本评审只形成文档交付。发布与新显示采样由看板负责人在接受版本处理；用户惯量调查继续由用户指定 agent 负责。
