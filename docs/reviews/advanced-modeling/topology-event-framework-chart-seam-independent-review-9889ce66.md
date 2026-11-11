# 9889ce66 closed SSI 接缝合同独立复审

## 结论与优先方向

**优先 A：修正并认证新建 closed plane / explicit-rolling SSI 的有限参数图生产合同。当前不能采用 B 将 K 包装为普通 native 成功结果。** 先做一次有界的 closed-chart 准备与全接缝资格检查；不能证明覆盖时明确返回 unresolved，不把已知共同支撑根的投影失败直接解释成几何不存在。下面给出实施范围、证书义务与停止条件，不要求一般 CAD 几何的所有 double 端点 bitwise 相同。

固定源码 `9889ce66caf1f1a83515aaa8d2ce5bfbf6a9d7be`，独立分支 `ThraceShah/gm-chart-seam-9889-review`。未追实施者 working diff，未改生产、正式测试、阈值、CSV、公共数学或 main。既有 `3344c539` root→Clip 窄接受及其独立报告 `285e93ca174dd307801ec16dce0f8cad48605e96` 保留；本报告不是重新验收它。协调端随后提供的调用者审计 `8bd0c72` 仅作为固定 9889 的合同交叉核对，未据此替换运行源。

本轮证明的是**现有有限 CHART 方程/ownership 对源 P 的覆盖缺口**，不是证明滚动面与平面的数学交线断裂，也不是证明改 closed refinement 一个条件就能完成 Boolean。冻结 P 的源根已被提供，外部 trim 没有先排除它；精确算术核查排除了“这两个 affine 投影的浮点 enclosure 算窄了”这一解释。支撑匹配、source-root 证明与完整交线分支发现仍各有自己的合同。

## 直接证据与边界

P/S 从实施树 `../gm-topology-contract-sol/temp_docs/contract-sol/frozen-projection/` 只读复制到 `temp_docs/chart-seam-9889-review/frozen/`，哈希分别为：

- P `projection-801-517-8.bin`：`f6dacaa07c99bc467c7915718d8e4f9ff20280362e001377f3c466963c768d79`。
- S `projection-801-208-3.bin`：`eff6fabced8f5c63088226b892b1c9d9e1537ee05b03c8b085e6bc2986f28b09`。

快照包含实际传入 locator 的完整自家 program 和七维 witness。独立检查确认 P/S 的 spine/domain 头和 program 数组序列化字节相同，只是 witness 不同；program 部分 hash 为 `10f33c3501f69f742846601b85dd80f4139e49094248e72b9efe81375ee8e2c9`。raw 布局快照和缓存 DLL 保持 ignored，不作为公共模型格式或厂商材料发布。

没有为这几个重放重建 kernel。读取已有 replay 的 portable PDB，把 **325 个 kernel 源文件和 1 个脚本**的文档 checksum 与本固定 tree 比较，全部匹配；6 个 generated/external 文档未作源码匹配。复制并固定运行 DLL，避免读取随后变化的 actor build。assembly informational version 是构建时旧 HEAD，不能单独标识 dirty 源；因此采用 PDB 文件级源码 checksum，并记录二进制 hash。细节见证据附录。

| 本轮实际运行 | locator 返回 / 进程 exit |
| --- | --- |
| 冻结 P，完整原域 | 67 / **1**，无 station |
| 同 program 的 S | 0 / **0**，`[2.5498703126402273,2.549870312640359]` |
| S rolling witness 加 `.01` | 157 / **0**；脚本将拒绝当负控成功，不能写成投影成功 |
| S 目标域上界减半 | 67 / **0**；同上，确实被有限 trim 排除 |
| 精确 dyadic / 接缝两侧 tube 诊断 | **0**；无 program/witness 修改，无建模操作 |

P 的生产来源在冻结原日志中独立核到：source line **517 generation 2**，input edge/coedge **34/62**，witness **8705**，certificate **5 / PlanePairBlendLineCertificate**；当前 plane515 / rolling176。源 native enclosure `[0.15550974970337608,0.1555097497034054]`，rolling station `[3.1415926535897927,3.1415926535897936]`，spatial error `2.7006503118386178e-14`。它是当前有限输入边及两平面 incidence 的根，不能拿同 tag 的其他 certificate/支持面结果替代。快照自身只冻结 target program+witness，完整 occurrence/generation 来源由同次日志和 live caller 检查提供，不能把 snapshot 当作可跨命令使用的实体证书。

目标 801 的实际有限域 `[0,2.653063227875798]`，40 个 CHART 点、Help/Help、首末存储位置相等。内部 knot **K=`2.5498703951611956`**，不是 outer limit。P 的完整 XYZ witness 经两侧实际 gauge 投影：

| cell | 合法 native 范围 | 全 source box 的 outward affine 像 |
| --- | --- | --- |
| 37 | `[2.4549933296261197,K]` | `[2.5498703951613213,2.5498703951613275]`，全在 K 后 |
| 38 | `[K,2.653063227875798]` | `[2.549870395161052,2.549870395161059]`，全在 K 前 |

额外用 BigInteger 精确二进制有理数计算 `T + scale * chord·(X-anchor)` 在源 box 上的线性极值，检查原 IntervalNumber 结果包含该精确像。两侧包含检查均通过，且精确符号比较确认上述两次排除。37 的 `像-K` 约为 `+1.262e-13..+1.314e-13`，38 约为 `-1.431e-13..-1.374e-13`；这些显示值是近似十进制，包含和符号判断本身不使用近似值。其它投影与 cell 相交的候选均由受证 position bound 与 source box 分离，完整 scan 没有漏掉另一可接受 cell。

进一步分别在 `[BitDecrement(K),K]` 和 `[K,BitIncrement(K)]` 用原函数取得终端七维 tubes，均 Success：

| 状态 | 左 terminal tube | 右 terminal tube |
| --- | --- | --- |
| X | `[-1.499999999999874,-1.4999999999998672]` | `[-1.5000000000001443,-1.5000000000001377]` |
| rolling U | `[3.141592653589916,3.141592653589929]` | `[3.1415926535896457,3.1415926535896586]` |

两 tube 在 X、中心 X、rolling U 分离，P 又与二者 X/U 分离。这把缺口限定到当前原始 cell 方程的接缝附近；不能把两个不同 gauge 的终端 box 重叠与否直接当作真实支撑曲线的全局连续性定理。anchor38 的 Y 是 `-2.000038001446803`，相对源代表点偏约 `2.694e-12`；Joint `EvaluateSpine(K)` 代表点是 `(-1.5000000000001408,-2.000038001444109,4.99999999855589)`，也不等于 P。这个程序的精确 knot/侧别不能通过偷偷选 K 或原 anchor 来“修好”。

### 一个必须保留的负控：witness Success 不等于目标 gauge 同根

在上述左右同一 terminal 域直接调用 `BoundSharedSpineWitness(... require:P)`，两边都返回 **Success**，尽管最终捕获的 terminal tubes 与 P 在 X/U 分离。源码 `NestedBlendBounds.cs:218` 附近先检查 require witness 位于初始 uniqueness boxes 内，之后继续收缩；源状态还必须满足**该目标方程组，包括 gauge 方程**，唯一性才可推出同根。现有 `LocateCertifiedChart` 先投影、检查完整像位于合法 cell，再使用这个包含检查，正是需要该前提。

这不是当前生产 locator 错误接受 P：它实际正确返回 67。它是对“略过投影，require:P Success 后给 K/flag”的具体反例。B 若要使用旧 bound 函数，必须新增真正的共同分支/误差证明；不能将旧函数 Success 当成已具备的 seam 对应证书。

## 官方 PK 与自家合同的含义

实际阅读 `parasolid-research` 和 `parasolid-docs` 技能，通过 MCP 定点查询 `PK_CURVE_parameterise_vector`、`PK_GEOM_range_vector`、`PK_CURVE_ask_interval`、`PK_EDGE_ask_geometry`、`PK_SESSION_ask_precision`，并核错误 67。使用本机 v380 官方 FD/XT 章节，未把 Linux 静态反编译或地址当数学证明。

| 参考 | 对本选择的约束 |
| --- | --- |
| `PK_CURVE_parameterise_vector(curve,position,&t)` | 参数化输入声明为曲线上的位置；它不是返回最小距离与误差证明的接口。 |
| `PK_GEOM_range_vector(geom,position,options,&result,&range)` | 查询全局最小距离，另有结果和 range；最近位置与原点有严格 native 逆像是不同问题。 |
| `PK_CURVE_ask_interval` / `PK_EDGE_ask_geometry` | curve domain 与某条有限 edge 的 interval/sense 不同；无限曲线返回的 size-box 域也不是有限 edge ownership。 |
| 官方 FD 17.2、17.3，`html/chapters/fd_chap.018.html` | 模型使用 session/local precision；几何近似相容不要求所有浮点端点相同，但 tolerance 不是分支/事件身份，更不能把不同短材料段合并。 |
| 官方 XT 5.2.1.5，`html/chapters/xt_chap.06.html` | 支撑面、limits 和有序 CHART 一起指定交线 branch/gauge；精确 chart 点与 regular 区间有不同求值规则。不能只凭 Help/Help 和重复位置假设任意周期 gauge，也不能替导入原 CHART 重建参数。 |

自家 `docs/icurve_design/icurve_blend_evaluation_spec.md:263` 和 `OriginalChartParameterMap.cs` 保留 chart-point、单侧导数和原参数方程；自适应求解细分不重建原 CHART。新建内部 SSI 可在发布前换一份经过验证的新参数表示，已经导入/对外发布的原 gauge 不能原地悄改。

`parameter` 本来就可选 midpoint/forward representative；现有强合同在于 **Lower/Upper 确实包围同一 source root 的 native preimage**。因此反对 B 不是要求代表 double 等于数学根，而是不能把没有逆像的 `[K,K]` 当作该根的 native enclosure。严格 body compare/fullcheck/mass 验证的是各自几何/拓扑质量范围，不能自动补上这个内部逆映射合同。

本轮没有在 PK 中猜测 `801` 为同 tag 曲线。它是 GM 操作内的临时 carrier，暂无经 topology/support/finite-use 匹配的独立 PK 身份。泛扫 API 或对不相关圆作投影不能回答此 SSI 问题，故不追加这类动态对照。真实 PK 两工具 Boolean 事实复用同次 frozen-operation 日志：PK0 / result21650 / 1body / full0，GM157 / ProductOpenEdge / 三输入回滚 / 未达901；本轮不冒称再次执行该 Boolean。

## A：下一实施合同与顺序

**范围只限新建、尚未发布的 regular closed plane / explicit-rolling SSI carrier。** 已有支撑匹配、显式 circle/ellipse rolling spine、当前有限 face/cut use 与唯一 branch 必须成立。一般 BSurface、多分支/奇异/无显式 rolling station 的情况保留现有不支持或 unresolved，不能借本修复扩大能力声明。

1. **先把 producer 资格状态与几何不存在分开。** 新建 carrier 的覆盖证书绑定 program/curve/support generations、finite First/Last、sense、branch 与使用范围；source root 已证在同一共同支撑分支，而目标尚无覆盖证书时，投影失败是 `CoverageUnproven`，不能作为 RootNotOnCurve 丢掉该候选后继续作完整材料划分。这里可以保持公开错误码，但内部失败原因须可定位。真正有排除证书的异 branch / 外 trim 仍正常拒绝。
2. **用共同有限站位组织证明，不用 XYZ 近邻定义接缝。** 本 P 接缝可取显式 rolling U 为局部 continuation coordinate σ，保留同一最近 spine 分支和 phase；在一小段有限 σ window 内，解当前两支撑约束及 σ 约束，证明唯一 joint root、正则性和带方向的单调性。σ 是证明坐标，不是偷偷替换旧 native T。若该 window 有折返/另一 foot/秩亏，缩到已有受支持的正则分片或拒绝；不假定 rolling U 在整个闭环全局单调。
3. **每个原生 cell 有完整 tube 和显式 native map。** 以共同 branch tube 推导新候选 gauge `ψ_i(x)` 的包含与导数界，证明其所声明 native window 的全部 root 存在、唯一、方向一致。每个相邻对，包括实际闭合末/首对，都要有同一物理 branch 的过渡证书：全相关 source/terminal state 被覆盖，有限 continuation window 无空洞，不以空间 box 相交当同根。若采用多 chart 过渡，记录明确 owner 和单值转换；返回现有 station 成功前，仍须得到**实际新 carrier**内同根的 native enclosure，forward evaluator/UV/单侧导数必须使用同一个定义。不能扩旧 window 或把跨 gauge hull 当普通根。
4. **端点/闭合所有权单列。** 新参数域单调、相邻范围完整，实际 finite trim 的两端有证明；重复 Help anchor 不单独证明 cyclic continuation。闭合首末同一物理 root 的 phase/支持 witness/方向须一致，已声明的 finite use 与 generation 保持。出现多个可归属分支、模周期不唯一、跨 trim 不可定、两个不同 root 的 ordering 无法分开，均拒绝发布。
5. **再尝试一次有界 producer 修复。** 可复用 `RefinePlaneRollingChart` 的联合根校正，扩展到该 supported closed 情况，但必须处理 cyclic terminal witness，并以步骤2–4的证书验证重建后的整个有限 carrier。一次候选准备、一次全部相邻/闭合检查，预算耗尽即保留有名 unresolved。禁止“不断加密/迭代直到 P 恰好能投上”。若现有 double-anchor 表示无法在该预算内承载所需过渡定义，提交那一对失败证书，转向明确的新生成 chart 表示或 B 的单独合同改造，不伪称 A 已完成。

`CreateGeneralIntersectionCurve` 当前只对 `!closed` 调用 refinement；其现有 routine 校正内部 seed、保持 terminal、重建 gauge，并检查相邻 chord 的正投影。这能改善 anchor，但**没有证明闭合末首 root 一致、全 branch coverage 或所有 native 过渡**；即使 P 变绿也不能省略上述 gate。导入 CHART 的不可变合同不允许在 consumer 的普通反投影中运行此重建。

### 最小可执行验收包

| 顺序 | 正/负控与必须输出 |
| --- | --- |
| A0，先保持旧输入 | 固定 P 应仍67，S成功，S wrong-phase157、excluded-trim67；本文 dyadic/tube 负控稳定，保证不是放宽旧判断。 |
| A1，新 producer 单个 carrier | 冻结新 program/hash；P 在新实际域取得合法同根 native enclosure，full source state 与 gauge 方程共同成立；S仍成功且与P的界有严格顺序。报告所有39个相邻 cell 和 cyclic接缝的 certificate/未证状态，不以采样数当覆盖。 |
| A2，身份与有限 owner | wrong rolling phase、antipodal/错误 branch、wrong plane/support、stale program/support generation、排除P的actual trim、跨端点无法确定归属均拒绝。负控每次只改变目标前提并打印拒绝层，避免都先被同一个 guard 拦住。 |
| A3，材料段与 gauge | 源线反向及其 enclosure 对应反向、chart 顺序/sense 一致反向至少一例；保留 S 到 P 的正长度开区间并在受证 root 界之间选材料 witness。不能按XYZ/tolerance把两个事件合并。若对 cyclic use 有声明，必须测原始闭合接缝及跨 seam trim；仅本内部 knot 的成功不覆盖它。 |
| A4，发布/操作 | 新 carrier 的 live forward/paired UV 与导出后真实 PK 的相应有限曲线作可靠身份匹配后检查；不要求native参数逐bit相同。再只跑这一 same-input Boolean，保留原版本/option/hash、成功result+固定1body/full0/strict/mass门及失败回滚；是否达到901另列。完整19/151不作为局部迭代循环。 |

A1 是 producer 局部行为修复的必要验收，不等于 A4 Boolean 已完成；A0/A1 的冻结复放使后续验证不必重复昂贵建模。第一批必须保留的短段就是既有 S/P，不允许用模型容差作为删除正长度区间的理由。

## B：可有容差意义，但当前通路不接受

CAD 的既有精度语义允许有界几何对应，不能因此把本例升级为精确 inverse。B 的明确含义应是：**保留当前 carrier 定义，在同一共同物理分支上，为 source root 选择一个带已证非零空间/参数偏差的目标 seam 代表站位。** 这与 A 改善新 producer 使其实际有限图覆盖该 root 不同，不得混写。

B 至少要有独立类型分支 `NativePreimage` / `BoundedSeamCorrespondence`，不是在现有输出附诊断 flag。后一类型必须包含 source 的完整 native root/state、occurrence/ordinal、carrier/support/program generation、目标两相邻 cell/finite owner/sense、共同 branch continuation 证书、两侧 terminal tubes 与整个连接 window 的空间/参数误差界。必须证明 source 与两侧都在同一唯一正则 branch，而非只证明各自附近各有一个根；全窗口方向、其它 branch 排除和有限归属也须成立。

本次 terminal tube 仅覆盖 K 邻接一个 double 的小区间；它们与源状态分离，不能直接当作上述连接证书。旧 `require:P` Success 的反控尤其说明现成检查不够。

| 相关 caller / 数据边界 | B 必须新增的能力，不能保持原用法 |
| --- | --- |
| `LocateCertifiedCut`、`FindBlendPlaneJointArc` | 返回 discriminated result；后者将 native enclosure 与 scalar plane-root 唯一域相交，故 bounded代表必须另走受证关联，不能拿K作scalar真根。 |
| `LocateBoundedStation`、`ReconcileCertifiedCutRoot`、`SortBoundaryStations` | 分开 native真根界与空间关联；证明finite owner、物理/参数顺序，重发现按共同源身份；不能将XYZ误差直接填Lower/Upper或将两个不同根的界相交。 |
| `BoundaryParameter`、`AssociateParameter`、source-edge split | 保存mapping-kind、目标关联与非零误差；当前 record 无这些字段。拆分仍依靠同一有限 source occurrence，正长度片段保留。 |
| event point、tangent/UV/curvature、material witnesses | source精确约束不能又被target(K)取代；目标求值与源点的差必须被传播。误差tube不能越过其它root/trim/材料边界；不确定时拒绝，不靠代表点分类。 |
| wrap、端点标志、输出B-rep与下一操作 | 不得把bounded站位变成declared-end singleton；wrap转换要转换完整对应。必须处理生成vertex/edge精度、XT及fresh操作重建证明，不能只写trace；现有新点precision默认零不会自动接住source SpatialError。 |

本操作 `model.Tolerance=min(default_tol,1e-8)=1e-8`，projected阶段另用半分辨率。source误差、target link误差、forward/UV映射误差应有逐项 outward 上界，组合后逐个满足实际消费者的**现有**分辨率/有限排序条件；它们不是 fix/mark `1e-7` 或 mass `1e-7`，也不是 snapshot 的 `ChordalError=0.021997604526821462`。不能改取这些较大值作为 seam 放行门槛。所需误差小于几何阈值仍不自动证明 branch唯一/区间可排序。

出现未证共同branch/最近foot、多分支、tube越过其他root、sense/generation/finiteowner不符、超出现有误差预算或任一caller仍把bounded结果当native真根时，B 必须拒绝或回到 A；不能以成功status隐藏未完成传播。当前代码和快照未提供完整 B 证明，也未完成这些caller改造，因此不推荐本轮把B作为小补丁实施。

## 操作验收状态

本轮直接接受的是冻结缺口诊断、控制和有限 A 路线合同；**没有接受一个尚不存在的 A/B 行为修复**。3344 局部 root/Clip 接受保留，default uncut blend 已接受范围保留。两工具 Boolean 的真实 PK/GM 差异仍为已有同次实验事实，GM ProductOpenEdge157 与三输入回滚保持，901未到；row289、旧418/gauge项、完整Rolling19/self96/lifecycle/性能均未由本轮重跑或关闭。IL2933 CLEAN 是实施者记录，非本轮执行。

原有 gap 文档与 consumer-contract 的失败历史不改绿。本文件给下一实施者的优先次序是 A0→一次A1准备/证书→A2/A3→单个A4操作，不以全套回归猜修复，也不要求用户选择难懂的参数合同。只有改变用户几何精度、目标或公共API契约才需另行升级。

命令、输出及复现细节见 `docs/reviews/advanced-modeling/topology-event-framework-chart-seam-review-9889ce66-evidence.md`。
