# 逐操作账本与统一矩阵

解释版本：`operation-layered-v1`。依据用户明确纠正与固定报告 `8c9fed1347bd137bcd6fb8a785db299743e78d90`；本账本不是重新执行R3。基线 main `610e3ce844bf99c85af6069905f9f3deaab08133`；R3 source `9b2f4c6c989a393fd2cbaa3e66d6ea5b52035017`，against `90f4d7840b4d3d8219ba29d1845ccbaff3386f5a`。

## 当前固定9b逐操作事实

| ID / 类别 | 输入、参数和API | 本步证据/状态 | 边界与缺项 |
| --- | --- | --- | --- |
| B0 / 前置构造 | ShortEdge原生/GM default seed，1°、radius .5、mark/fix tolerance `1e-7`，native default update；固定oracle中同制造参数构造 | 作为已测default的fixture来源；原生seed纯PK控制fullcheck0 | 不把前置构造时长计入Fix；不是所有seed/transform独立验收 |
| B1 / default uncut blend | GM `BodyFixBlends`与相应native Fix；GM自家XT由真实PK接收，对独立native产物 | **接受已测default uncut primary范围**：严格compare及fullcheck0；候选产品hash见下表；没有后步157倒扣 | 不扩称所有blend/chamfer、reverse全部变体、self96、main完成。未在本次audit重跑所有物性/有限gate；报告中的两条完整primary生命周期退出0为实现者证据，不能升级成此audit独立实测 |
| B2 / 分类辅助（单独分类API另有本身资格） | reverse=true，`BodyContainsVector`原row289 `(-1.499999667811987,-2.500000004201637,4.50007614662344)` | **辅助兼容失败**：期待5702实际5703；独立log的第66行调用到463行断言 | 不是PK blend几何比较失败；用户批准全510同路径比较仍必须执行，339不豁免；本项已有固定断言不删除 |
| B3 / 内部表示与consumer证书 | reverse=false，`VerifyStationaryShortEnd`精确端点定位，后续 `TestNativePlaneEdge` | **内部端点合同失败**；测试停439行，后续该测试证书断言未到达。独立probe实际证书返回157 | 该函数不调用PK blend compare；一个ulp对blend几何容差可小，对当前精确证书的前提却有实际影响。保留差异，不改成epsilon断言掩盖 |
| I1 / blend→两tool Boolean组合 | GM上步产物+oblique块 `.15,.4,2.5`，meridian块 `.10,.16,1.3`，`BodyBoolean2` subtract；`ForbidSpatialBoundaryReuse=true` | **组合的Boolean步失败157**，Clip3/20，curve208，尚未到ProjectedCellReplacement；本应到达后注入memory901 | 固定测试127行；input target有hash，原两tool XT逐件hash未留；真实PK没有在完全相同target+tools上执行这一Boolean，因此独立same-input Boolean对标 **未验证**，不能称PK肯定能做而GM不能 |
| I2 / 干预诊断 | 仅临时模型单vertex改回 `(-1.5,-2,5)`，不改生产/阈值/ICurve | 第一stationary hook变0/一root；Boolean仍157、后移Clip8/20 curve214；row289仍5703 | **诊断完成不等于操作通过**；只建立第一处局部因果，不是修复方案或统一根因 |
| R1 / 事务回滚 | 同I1的target+两tools，失败前后XT/generation/资源；base相同probe到达注入901 | 独立probe观察原候选与干预模型失败后3输入XT不变；base到注入并回滚 | 原unit test先在127断言失败，后面的回滚断言未执行；不能借probe通过说该unit test通过。候选尚未证明注入阶段的回滚，因为没到注入 |
| S1 / 标准和transverse生命周期 | 9b oracle分别包含copy/transform/delete/receive/第一及第二Boolean；每步在 `CompareFresh`比较 | 实现者两命令退出0，报告严格/full/mass原阈值通过；按每步保存，属于组合中已观测分步 | `ShortEdgeBlendOracle.cs:256-298,341-403`可核查比较时点；不是共同一份输入几何的全部隔离Boolean实验；不能据此豁免I1 |
| P1 / 性能 | B1/I1等 | 本轮 **无单步性能资格结论** | 9b19项耗时及证书耗时不是PK operation ratio；失败单步不得给qualified ratio |

已保存的真实native product：`bd75b7926956c1506e00c172816a0bfc1bb07db1660d74da65aaf1734f219ef1`；未经干预9b target：`7de009139ea2dde494d83859adeb80141871dd61d7d8ca0e4354e0022f5a16dc`。两者各自由真实PK fullcheck零fault并经严格helper compare通过，global21921/local21931/face_pairs0；0配对项不是匹配成功面的总数。输入target/tool缺失hash不可事后伪造；隔离I1需在下次最小检查先保存所有输入、options、导入前后状态。

## RollingNetwork完整19项：实际合同去重

来源为固定9b源码 `tests/KernelTests/RollingNetworkTests.cs` 与独立 `temp_docs/r3-frame-review/rolling19.log`（报告8c9fed1所保存）。以下按参数展开计数共19，16通过/3失败；没有把它们都称为blend独立PK验收。

| 方法 / 参数项数 | 实际API/检查阶段 | 结果与归属 |
| --- | --- | --- |
| TransportedReferenceMeridian… / 4 | `TryReferenceMeridianContact`正/负例，刚体/镜像/scale1与1.7，局部证明函数 | 4通过；不是独立PK完整建模 |
| HeterogeneousBands… / reverse true,false共2 | BodyFixBlends返回0，拓扑/有限边/支持incidence；分类或精确端点先失败，后transmit/receive未到达 | 2失败，分别B2/B3；本方法没有独立PK primary对照 |
| ShallowRollingPlaneCuts… / 1 | BodyFixBlends+short intervals+managed XT receive | 1通过；有限参数/持久表示合同，不是独立PK Boolean |
| ProjectedRollingCellFailure… / 1 | BodyFixBlends后BodyBoolean2+计划资源故障注入+rollback | 1失败I1；不是一个单纯blend test，也未完成memory负控 |
| RigidlyMovedRollingSeed… / 1 | transform前置、BodyFixBlends+结构/short intervals | 1通过；不是全部刚体参数域PK资格 |
| SubdividedSelectedCarrier… / ellipse false,true共2 | selected subdivision、BodyFixBlends、provenance、XT receive | 2通过；参数/来源/持久consumer |
| IncompatibleFiniteCap… / reverse false,true共2 | BodyFixBlends+subdivision/cap边界/结构 | 2通过；声明provider域局部合同 |
| NetworkAllocationFailure… / budgets0,10,50,200共4 | 故障注入BodyFixBlends；失败检查pending/incidence/generation/resources，成功分支检查结构后返回 | 4通过；事务合同，不是4个独立几何比较或每budget必注入失败 |
| SeparatedNonAdjacent… / 1 | BodyFixBlends、common arrangement trace/结构 | 1通过；局部装配覆盖 |
| InterferingComponents… / 1 | BodyFixBlends应拒绝未解干涉并保持输入 | 1通过；受限provider原子拒绝负控，不是支持该几何 |

## 全操作统一矩阵（每个入口选适用行）

| 操作类别 | 输入/primary | 辅助与内部合同 | 组合单列 |
| --- | --- | --- | --- |
| 构造实体、extrude/spin/sweep/loft/sew | 相同制造参数/选项；GM输出textXT→PK；独立同API PK；compare/fullcheck/region-shell/material与原mass/有限域 | tracking、ownership、原子拒绝、provider边界 | 导入、后续local edit/Boolean |
| blend/fillet/chamfer | 同seed/selected occurrence/mark和Fix options；对应PK操作输出primary，禁止拿后Boolean代验 | finite root/chart/signed phase/endpoint约束；分类独立标辅助 | copy/transform/receive/再次建模与多tool交互 |
| Boolean | 明确target+全部tools的同输入hash、options与body状态；结果集/状态/primary全部检查 | 事件/退化/相切根完整性、tracking、事务；失败行为也对照 | 各内核不同上步产物继续做Boolean、两tool投影/资源注入 |
| section/imprint/求交 | 同有限support/domain、参数options；匹配分支/孤立点及类型/sense/trim；wire需额外有限边/距离/参数检查 | 遍历方向、参数导数、完整根证书；采样不代证明 | 输出接着建wire/sheet、再Boolean |
| local offset/hollow/face编辑 | 同输入及面身份、amount/tolerance；同对应PK API输出primary；canonicalization若使用需独立标变换步骤 | 原子拒绝、消失face/loop、身份tracking与材料sense | copy/delete/reload、后续改模 |
| 曲线/曲面create及knot/degree/reparameter | 同定义数据/参数；metadata/domains/相应求值与几何等价，适用时body/wire primary | 表示变化允许但需参数映射；source dependency生存期 | 用其构造body或intersection，不倒扣单独已过create |
| copy/transform | 同输入及变换；geometry/body对应结果primary；normal/sense/依赖支持 | 共享/独立ownership、source unchanged、删除source后的可用性 | copy后删除/重载再改模 |
| transmit/receive | 同bytes/schema/options，先验可接收性；有限域、结构/sense/容差/ownership；持久root/chart再验证 | 运输前后分类诊断；不同执行路径不混比 | 加载后消费者操作 |
| SURF/CURVE eval、inverse、projection/distance | 同几何/同参数/导数阶数；点/导数/参数/错误码及全局最优性相应合同 | 参数域/周期/退化；body compare只是所属输出几何的适用辅助 | 构造、生命周期错误单列，不用后删除失败抹去已测eval |
| classification | 同点/同路径参考；所有510保留；即时对即时、各自XT接收对各自XT接收 | 7点PK运输诊断与数值条件；339不豁免 | 不取代生产该body的操作primary |
| ask/check/resource/session | 对应返回值、数组、错误码、内存/状态合同 | generation、fault injection、部分执行/未到达可见 | 不用管理操作总exit推断所有几何失败 |

“primary已过但aux/内部合同失败”允许同时出现，数据不能强行压成一个pass/fail。各项原阈值和已有测试保留；集成资格可等待consumer合同修复，但不能反向重写已被独立直接验证的单步结果。
