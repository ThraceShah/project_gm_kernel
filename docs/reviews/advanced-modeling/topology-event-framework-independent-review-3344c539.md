# 3344c539：有限 source-root 消费合同独立复审

## 结论与固定范围

**接受现有已认证有限 source-line root 在 analytic contact Clip 事件中保留原位置这一局部增量；完整两工具 Boolean 仍未通过。** 本轮没有发现实际生产调用链可达的 guard 反例。接受依据是完整调用链的来源/有限域关联、未变的公共数值语义、新单测及反向选边的最小真实 PK 对照；不是把 helper 的几个标志位当作通用几何证明。

| 项目 | 固定值 |
| --- | --- |
| 被审候选 | `3344c539ab919b3f545cde2c3f15e9072440f86e` |
| 比较基线 | `9b2f4c6c989a393fd2cbaa3e66d6ea5b52035017` |
| Oracle 中间提交 | `96a997f`，最终脚本按上述完整候选读取 |
| 独立报告分支 | `ThraceShah/gm-source-root-3344-review` |
| PKToy | `d820719b6d8510d623972ab9d8efc65edbc312f9` |
| 本轮临时证据 | `temp_docs/source-root-3344-review/` |
| 本轮 Boolean 产物 | `temp_docs/contract-sol/review-reverse-3344b/` |

只审固定提交，没有读取实施者后续 Components 修复来替候选加分。既有 `9b2f4c6` default uncut blend 的 primary 接受结论保留。前整体审计 `56825d6`、ec393 独立报告 `272a51e`、既有 R3 独立证据 `8c9fed1` 均为历史记录，本报告不改写其当时结果。未修改生产代码、正式测试、阈值、CSV 或 main。

候选相对基线仅五个文件，473 insertions / 1 deletion：`KernelRuntime.AnalyticBlendContact.cs` 新增局部 helper/test hook 及 using，`KernelRuntime.RegularBooleanCuts.cs` 增加事件位置选择和 trace，`RollingNetworkTests.cs` 增加 39 行测试，以及 Oracle 脚本和消费者合同文档。公共 Curve/Taylor/Interval/Joint/NestedBlendBounds 路径无差异。被拒 ec393 的公共三角函数语义没有进入本候选。

## 合同审查：为何可以保留这个位置

下列源码位置均对应固定 3344；路径相对于项目根目录。

| 层 | 证据与判断 |
| --- | --- |
| 来源与 occurrence | `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBooleanCuts.cs:106` 的 `RegularBoundarySurfaceRoots` 校验缓存 provider 的输入边、曲线/曲面及 generation、精确输入 trim 和状态；新根保存 InputEdge/InputCoedge、ContactSurface/CuttingSurface、有限 native enclosure 和 witness。它们在同一同步 Clip 内消费。新 helper 不承担验证任意外来 root 的职责。 |
| 有限 root | `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularCurveIntersections.cs:138` 的 `IsolateFiniteBlendLine` 保留 finite edge 区间与内部 root 证明，验证估计点/空间误差及 native enclosure。`Certified=1, Witness=1` 的 test hook 是构造受信输入的工具，不能替代此生产者合同。 |
| 有限 cut / 同一 branch | `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBooleanCuts.cs:500` 的 `LocateCertifiedCut`、随后 `LocateBoundedStation` 和 `ReconcileCertifiedCutRoot` 先定位当前有限 occurrence 并协调同源重发现/显式同根；无法证明的 enclosure 重叠返回失败。新选择仅匹配 `rootParameters[r]==sourceParameter`，未增加空间距离合并规则。 |
| contact-locus / 唯一侧 | `src/ProjectGmKernel.Native/Runtime/KernelRuntime.AnalyticBlendContact.cs:51` 的既有 `TryAnalyticBlendContactMap` 使用完整显式 line/conic 系数识别 contact，枚举两侧、双侧同时匹配则拒绝；带 phase、scale 与周期提升。它使用原有 `1e-10` 系数识别误差界，不是只凭一个近点匹配，也不是新证明任意实数曲线严格相同。 |
| support 与 chart | 同文件 `:16` 新 helper 根据 section 和 ReverseSection 选择原 support；`src/ProjectGmKernel.Native/Runtime/KernelRuntime.SupportCertificate.cs:35` 的 current chart certificate 要求同定义 support 或精确 signed-permutation 平面 chart，并检查当前 generation。这里使用 chart 的位置等价；normal relation 不用于改变 fin sense。 |
| 原点满足约束 | 新 helper 仅处理显式源直线；要求点在选中的 plane/cylinder 上通过 outward arithmetic 的精确零检查，并要求与源直线叉积三个分量区间幅度均为零。没有新增 residual epsilon，也没有改写点到近邻支撑上。一般旋转后的浮点输入未必能满足这些 exact 条件，保守拒绝不等于已证明任意变换覆盖。 |
| 原点与 cut anchor 关联 | `src/ProjectGmKernel.Native/Runtime/KernelRuntime.RegularBooleanCuts.cs:773` 的既有 `CertifiedRootMatchesCutAnchor`：同 curve 时要求 station 位于 root enclosure；异 curve 时检查 cut 求值点相对 root 的原有算术尺度 roundoff，并将 cut 点反参到源曲线、周期提升后要求落入原 enclosure。该 roundoff 是表示兼容检查；必须连同上述来源、locus、support、有限 branch 使用，不能单独认证根。 |
| 发布与后续约束 | `RegularBooleanCuts.cs:658` 仅在没有原 seed vertex 时选择已通过 helper 的 root.Point；seed vertex 优先级不变。`AddPoint` 的 proof 要求、源/目标 carrier generation、原始参数与 enclosure/witness、后续 tangent/root flags 均保留；未把 root 曲线参数替换成 ellipse 参数。新增 trace 同时输出最终 event point 与 source point。 |

方向与闭合的边界：contact map 继续使用原有 phase/reversal，helper 的 station 必须位于实际 cut.First/Last；原来 closed cut 的无端点分支未变。wrap 的 analytic arc clone 保留 angular chart，并由既有代码对上端 locator 加一个 turn。新 helper 没有把整个周期或另一半周当作同一有限 branch。邻接根的顺序与合并仍由既有有限 locator/reconciliation 负责，不能把“选择第一个 helper 成功的 root”脱离此前关联链单独推广。

因此这个修复保留的是**一个已有有限事件的源约束**。它允许同一物理事件的公共 conic 求值与精确源约束有原有算术误差范围内的表示差异；没有宣称 `Math.PI` 使任意三角表达式精确。既有 sourceLine12 / rolling-surface 点与 contactEllipse124 在 pi 处 1 ULP 的证据解释了该 fixture 的局部断裂，但不证明先前一般 `pi-u` 参数映射缺口就是所有后续失败的根因。

## 新测试的真实覆盖与反控落点

本轮实际运行 `InteriorSourceRootPreservesTheStationaryPlaneConstraintAndItsFiniteBranch`，**1/1 PASS，exit 0，39.8670 s**。它检查至少一个 helper 正例，已发布 event 与源 root 坐标相等；执行原有 stationary 端点及位移 `-1e-8, 0, +1e-8` 检查，并在 managed XT 删除/重载后重复。没有修改原有 418 meridian gauge 断言。

| 正式测试负例 | 实际主要拒绝层；不能扩大成的结论 |
| --- | --- |
| cut trim 排除当前 station | helper 首部参数域；没有测试另一个非法 station 仍落在 trim 内的全部情况 |
| antipodal station | 同一 root/support 不变，最终 anchor 的表示/投影关联拒绝；保留了另一半周反控 |
| source enclosure 整体 +1 | 最终 source 反参与有限 enclosure；不是单纯位置超容差 |
| missing witness | 首部 witness guard；不证明任意非零伪造 witness 有效 |
| wrong support | current support-chart guard；这里改 ContactSurface，未更换 rolling/contact-map 来独立覆盖所有错误曲面组合 |
| point.X +1e-6 | exact support/source-line 条件；不能算作只隔离 anchor 判定的负控 |

测试从 trace 中选择 helper 能处理的根，`checkedRoots>0` 防止完全空跑，但不是要求所有 CertifiedRoot 都适用此路径。test hook 的 `0..Tau` 域也不是生产中所有实际 trim 的替代证明。

独立补充的最小诊断从同一正式 Oracle 派生，在**临时 carrier 仍存活的 trace 回调内**运行：反向选边顺序，命中四个 line/ellipse 正例，检查 event/source 坐标相等；对 stationary root `(-1.5,-2,5)` 临时反转源 line Axis 并将 enclosure `[l,h]` 变为 `[-h,-l]`，helper 接受；错误顺序 `[-l,-h]` 被拒绝。finally 恢复整份 LineData，发布 target 哈希仍等于候选 `399e580b…`。这是受控 hook 的参数方向反控，**不是独立执行一次反向 provider 的 root isolation**。

两种选边顺序已分别有本轮正式单测和最小诊断的局部正例。任意 rigid/mirror/scale、所有 ReverseSection 组合、general source-direction producer、相邻多重根、高阶网络和全部 wrap/closure 分支没有由本轮小 fixture 全面验证。未发现当前链反例不应写成这些域的普遍证明。

## 真实 PK 对照与仍未完成的操作

实际阅读 `parasolid-research` 技能，并通过 Parasolid MCP 查询 `PK_BODY_boolean_2`、`PK_BODY_check`。前者区分 API 返回与 Boolean result/产物，后者返回 fault 列表；API 调用返回 0 不能替代结果和 fullcheck。未用静态空图推断无调用，未引用跨平台地址或暴露库/认证。运行使用现有 PskernelSharp/ParasolidScriptHost；先做真实 PK session + block/fullcheck0 和 GM SessionStart0 preflight，exit 0。没有手写 ABI 或将真实 PK 指向 GM。

本轮只实际进入一次两工具 Boolean。选边顺序反转、radius `.5`、mark/fix tolerance `1e-7`、fix v31；原两工具尺寸/轴向沿用脚本。v19 subtract 的同一 options 字节传给两侧，独立诊断额外验证 size 168 和 38 个同名字段 offset 相同。native 默认值与原测试 options 源码相符。不是仅凭两个 struct 大小相等猜布局。

| 本轮阶段 | 结果 |
| --- | --- |
| GM Fix / 局部正例 | error 0，count 2，Publish；反向选边下 acceptedRoots=4 |
| 三个 GM XT 输入送真实 PK | 每个 receive 成功、fullcheck faults=0 |
| GM 两工具 subtract | error **157**，result **21653**，bodies 0，stage **Components**，faces **36/-1** |
| ProjectedCellReplacement | entered=False；未执行此处 injected-memory-full 路径 |
| GM 失败事务 | 三个输入重新 transmit 与操作前逐字节相同；未抛 rollback 检查异常 |
| 真实 PK subtract | error **0**，result **21650**，**1 body**；output fullcheck faults=0 |
| 进程 | **exit 134**，最后抛 `GM Boolean contract incomplete`；这是失败实验 |
| GM/PK Boolean 输出 strict/mass | 未执行：没有 GM 输出；不可填绿 |

遗留 sheet 字段是 `RayIntersections / curve547 / Success`，不把它当作 Components 返回点的已证根因。结果说明固定候选已越过本 fixture 原 curve208 Clip 阶段；先前 curve214 的具体失败日志与本次到达 Components 一起支持“旧两处 Clip 不再是这次首先失败处”，不宣称所有 curve214 变体通过。没有追入后续未固定 Components working diff。

**同输入的精确定义**：GM 消费其刚创建的 live target/tools；真实 PK 消费三份 live bodies 操作前导出的原字节，随后使用同 options。它是同一组 GM 序列化几何的对照，真实 PK 侧可能经 receive 改变内部表示；不是声称两引擎都重新 receive 后再做 Boolean，也不是两边各自构造完全独立原生 target 的 Boolean 比较。此路径足以记录本 fixture 的 live-GM/PK-receive 差异，不能独自裁定所有表示合同归属。

候选未切割 blend 的 strict/full/mass 资格另有固定实施者证据：对旧 9b target，normalized15 `5.036881582931127e-15`；对冻结纯 PK target，双方 full0、strict compare 通过、normalized15 `2.1627000794607434e-10`。本轮只读并核对日志及两个 native-reference 文件哈希相同；**没有声称本轮新建一次 native Fix 或重跑这些 primary 命令**。参考是先前纯 PK 默认/radius-.5 产物，非 GM 自比。本轮 fresh 反向选边生成的 candidate target 与该固定候选 primary 使用的 target 哈希相同。

## Oracle 审查发现：当前失败证据有效，未来成功门槛要补

`scripts/RollingBooleanConsumerOracle.cs:18` 拒绝覆盖已有实验目录，记录 target+两个工具及 option bytes/hash，三输入先 receive/fullcheck；真实 PK 调用、tracking/result free、GM 失败回滚均实际执行。PK 输出 XT 保存，可外部计算 hash；目前 manifest 不含结果文件 hash，本报告补录本轮结果 hash。

**需要另行修正的成功资格漏洞**：脚本仅要求两侧 API error=0 和输出数量相等，未显式要求两侧 Boolean result 为成功，也未要求这个已知应有一个实体的 fixture 返回 1 body。如果两边 API 均 0、result 为失败且 bodies 均 0，长度比较可通过，strict/full/mass 循环空跑，最终 Require 也可通过。该路径由源码条件直接推出，未伪造真实 PK 会如此返回；本轮观察到 PK success/1body 与 GM157，所以不影响本轮失败对照。

下一个成功提交的最小验收修订应明确：两侧 error/result 均成功、本 fixture `n_bodies==1`、所有产物非空且 full0、GM 输出真实 PK receive 后 strict compare 和原 normalized15 mass 门槛；保留独立失败-result/零-body 的资格器负例。输出及返回码在清理前记录，结果 XT/hash 加入 manifest。`--inject` 必须明确是 reached callback + 901 + 三输入回滚的独立失败事务测试，不可替正常 Boolean success。review-only 本轮不代替实施者修改脚本。

## 分层验收与交接

| 操作 / gate | 本次状态与后续 |
| --- | --- |
| 原 9b default uncut blend primary | 保留此前独立接受；下游消费者失败不能倒推它失败 |
| 3344 有限 source-root / Clip 位置保留 | **接受窄增量**：实际 provider 链 + 本 fixture stationary/reload + 两种选边局部覆盖；不扩张到所有 analytic roots |
| 新单测 | 本轮独立 1/1 exit0；反控的真实落点如上 |
| 候选对旧 9b / 冻结纯 PK 的 primary | 复用固定 strict/full/mass 日志；本轮 fresh target 哈希核对相同 |
| 151/151、IL2932 CLEAN | 实施者证据，本轮未重跑；不得记作本轮实测 |
| 隔离两工具 Boolean | **未通过**：本轮真实 PK 成功、GM Components157；需先定位固定返回并修复，随后补 Oracle 成功 gate 再单独验收 |
| injected-memory-full at ProjectedCellReplacement | 未到达，901 合同未验收；现有 rollback157 不等于此 gate |
| row289 分类 on/out | 独立未关闭项，本轮未重跑 |
| 旧 418 meridian 表示断言 | 独立 frame/gauge 项，本补丁没改；不以放宽到 mod-pi 通过，也不据此否定本局部 root 合同 |
| self96、完整 Rolling19、生命周期、AOT、整套与性能 | 本轮未跑，不声称通过；本任务无 ABI 改动，不跑 AOT |

本阶段可以固定局部合同与报告，不必等待完整 Boolean 才保存成果。后续实施者只需接 Components 返回点、Oracle 成功资格门槛和仍独立开放的分类/表示项；先做对应最小实验，再按操作的 primary/consumer gate 集成，不能用成功子段替代整个 test 或全部建模里程碑。

可复核命令、摘要和哈希见 `docs/reviews/advanced-modeling/topology-event-framework-independent-review-3344c539-evidence.md`。
