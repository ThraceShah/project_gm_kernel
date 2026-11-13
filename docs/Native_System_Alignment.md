# PK 原生处理链与 GM 对齐

本轮起点 `f7c110934679b47b4b3220f8e13d8831fabe8151`，工作分支
`feat/pk-native-system-alignment`。起点工作区干净；PKToy 固定 `d820719`，
私有环境准备实际返回 ENV_READY（Linux x64、PK 38.0、schema/corpus 就绪）。
此文件持续记录实现与实际验证；不是 M0–M5 已完成声明。

## 当前整体实施计划

用户已将计算正确性与完整操作性能合为一个负责人目标：q2 是唯一研发、集成和
交付 owner；第二研发 worker 已停止，原性能目标一并接续。下文的两人分函数协作
是固定历史过程，已撤销，不再等待另一个研发者或新 scope。Mac 仅可作为同源、
命令式平台验证机器，不再运行 coding agent 或子 worker。

1. 沿真实 Fix/Boolean 的输入定义、候选、SSI/根、有限材料、拓扑发布、失效和求值
   找结构性重复与缺失。原生 XT/UV/精度和共享 arrangement 已取得的成果继续复用；
   不另建模型、Engine 或诊断平台，不用 case 坐标定义算法。
2. 本轮已修的来源、precision/ownership、TR/SP 和 Cone 有限域继续作为完整能力
   消费；跨 native knot 的连续根域及已准入排除消费者已有固定运行，当前攻关是
   变换后的有限端点、原生UV、事件和切割曲线参数的共同传递。原来留给另一 worker 的 extent WIP 由本 owner
   直接核模型、吸收并贯通，不能把9项自测等同于完整 helper。
3. JIT 作为同一个基准入口合同处理：核原配置层级，在同 source/Release IL 的
   主要完整操作上做一次统一开关对照，从整体结果选一套 TieredCompilation，固定
   PGO/QuickJit/R2R与子进程白名单；禁止逐case择优或改全局配置。此前没有显式记录
   的 runtime-default 数据不反签政策、不转成新标准倍率。
4. 正确性与成本在相同完整操作、相同输入/选项上共同验收。稳定统一源码一次集中
   跑受影响建模族、必要全suite、真实PK full check/strict、查询/物性三态和完整
   生命周期，封存实际source/PE/PDB/原始退出与模型，再安排一次重大独立接受。

普通内部问题和失败由同一负责人持续解决，不逐字段派发、反复申请或用小提交结算。
完整PK性能目标尚未达到；当前数百至数千倍的开发耗时差不能由几个局部加速掩盖。

## 真实处理链与关键差异

| 阶段 | PK 依据及证据层级 | 起点 GM 实现与本轮动作（后续执行见下） |
| --- | --- | --- |
| 模型定义、准备 | 官方 FD 15/17/18、XT model/schema：原生拓扑、有限 geometry、ownership、precision 是模型定义 | 沿用 Runtime/XT，不创建替代模型；核查有限边和顶点精度在构建及复制中是否保全 |
| 候选、求交、有限裁剪 | 既有同平台动态证据：Fix 可从终点间追踪；Boolean 可从输入面的有限参数盒界追踪，随后再裁剪；见原 acceptance 的 search-box 控制 | 复用已实现 finite native intervals、paired CHART、标量/向量隔离、共同事件和 SheetArrangement；候选近似不代替最终有限域验收 |
| Blend 支撑、分支、终止 | 静态查询实际命中 fix_blends → BLE_for_all_blended_edges；其下 r12/rax 是间接调用，当前不能由此声称已理解完整 rolling solver | 原解析网络、常半径 regular、三价 corner 分支继续保留其适用域；本轮不以重写 Engine 代替缺失能力 |
| 事件、材料 | QTP_point_in_body 静态链含 region/shell、有限 verts/edges、ordered faces 和 point_in_face_2；后者按名未能解析，仍为未知 | 当前 generation/finite carrier/event 关联已经供 arrangement/classification 消费；继续按真实实体精度与有限归属处理，不把 510 坐标当算法 key |
| 有向 B-rep 发布 | 官方 FD 10 的 tag persistence：split 保留一个来源 tag，shrink 保留 tag，merge 保留一个来源 tag | BuildRegularBooleanProducts 已有 source face → fragment → built face 映射和 face tag 保留，但还直接拒绝 tracking；应消费实际生成关联，而不是从最终 XYZ 重新定位来源 |
| 修改追踪、回滚 | 静态链：boolean → BOO_start_tracking（临时 entity-link）→ BOO_function_2（tag persistence/tracking）→ BOO_end_tracking → PKU_process_boolean_tracking（读取关联、填返回、移除临时属性）；这是静态依赖证据，动态分支待实际验证 | 本轮优先闭合曲面 Boolean 的原生修改来源与发布生命周期，保持 command journal 原子回滚；不把追踪数据变成持久几何定义 |
| copy/delete/transform/reload | 官方 ownership 与 tag 合同；原 acceptance 已有实际消费者证据，但不能覆盖所有 provider | 新增机制用独立 PK 同操作与 fresh GM XT 验证；继续执行删源、变换、editable XT 回读再建模，检查精度和依赖不丢失 |

本轮首项决定性差异是：原生参数/来源已经在计算中获得，起点的 GM 发布接口却尚未完整传递
这些信息，曲面 tracking 被整条拒绝；部分公共构建路径还需核查是否丢失实体精度。
本轮从这些现有共享数据流直接实现，不建立新的调度平台，不碰 Mac 当前数值优化文件。
后续动态行为若否定某项推断，会据证据修正本表。

## 原计划与起点未决类别（当前执行范围见后文）

- M0/M1：共同 finite-use、halfedge/cycle、native UV 和来源关联已经存在；
  通用多参与者/contact/同域证明仍仅覆盖已实现类别，不能把共享数据结构等同于完整能力。
- M2：等定义独立 support、全部保留 loops、远端 bore 已有固定证据；cap/provider
  适用条件和非等定义近似支持不能据此免责。
- M3/M4：异构短边、解析/非解析 regular、三价 corner、双刀、copy/删源/reload
  已有分别绑定源码的成功；任意多方事件、高价角点、变半径与自交修复没有被证明。
- M5：起点最近固定全回归为 2025/2025；原 96 矩阵与各历史检查点只代表其固定源。
  原 handoff 的“完全完成/实体完全精确”等摘要不能覆盖后续原始失败与未决。
- 固定 510 direct：self 495V/15U、import 494V/16U；真实 PK 自回读七行差异。
  附 Capture 的另一次执行观察八行，不能混为同一次。历史 self 其余 495 行原始列缺失。
- 物性：两侧实际是 PK 对 GM fresh XT/reference XT 积分，非 GM 自有积分器。
  当前数学接受的物理 norm 合同 1080 行为 492V/588U；旧报告保持原字节。
  官方 accuracy 是工作量控制，modulus 是误差界，不能用区间重叠签固定精度。
- FaceAttach 尚未测类别、curved tracking 缺口、all-edge local sense 差异及未执行
  provider 均独立登记；不能由 single/parallel 或五类圆柱有限通过推断通用完成。
- 起点的 Mac 已接受性能固定点仅供只读对照；后续固定 patch 接收见集成章节，
  moving 候选不取得验收资格。

## 本轮实施与验收

1. 核真实 PK 曲面 Boolean 的基本追踪行为与内部关联分支，然后从实际 arrangement
   source/cut/publication 映射实现通用机制；运行跨解析/非解析及有限 trim 的控制。
2. 核模型精度、拥有关系及来源 tag 在重建/复制/变换/回读中的保全，修复经实验证明
   的共同缺口；真实负例和 allocation failure 必须保持原输入、池及返回内存。
3. 固定稳定源码，集中运行受影响建模族、真实 PK fresh receive/full check/strict compare、
   查询、物性与生命周期；逐操作分列执行状态及原退出码，再封存源码/制品 manifest。

验证不增加算法预算、不调高容差、不删证明。无 C ABI 改动不执行 NativeAOT。
持久说明放在 docs，原始新运行放在 temp_docs；历史 query/numerics 279 制品保持只读。

## 第一项共同机制：有限来源随归并传递

真实 PK 同输入曲面 Boolean 的基本追踪返回 derive 记录，来源可以是 face/face、
edge/face 或 edge/edge。同域相同实体的 unite/intersect 返回十二条 edge/edge，
部分同域 overlay 返回二十二条；因此仅从最终存活面的两个 owner 反推来源不成立。
初版试验的 45 项几何均通过，却在这些类别漏追踪；原始结果保留，不能算完整追踪通过。

实现保留材料选择前实际 face/coedge 的来源行，有限原边片段继续绑定原 edge。
只有既有 EquivalentRegularEdges 证明归并或共同边合并实际执行时才传递 edge alias。
发布从真实 alias、来源片段和建成 edge slot 输出关系，原 edge 优先于其本体内的
incident faces；不做结果中点反求。临时来源不进入 XT 或 geometry 定义。
tracking=false 不分配来源行；tracking=true 的来源工作区在 SSI/arrangement 完成后
才借用，长度按实际 coedge 数，避免占用求交工作区。早期提前借用导致真实 157 的
失败日志保留在 `temp_docs/native-system-alignment/lineage-{managed,focus}.*`。

实际验证：

- `lineage-verified-managed.log/.exit`：57/57，原 exit 0，覆盖曲面三操作、同域
  不同有限环、同域 copy/reload、既有失败及 allocation rollback；allocation guard CLEAN。
- `tracking-lineage-verified.log/.exit`：45/45 独立建模，GM fresh XT 真实 PK receive、
  完整检查、原 strict comparator、每个产品 125 材料点及已有连续/reload 消费者通过。
  新检查逐条核来源确属调用前输入、原 class、成品 edge 有效且不重复，并与 PK 比较
  来源 owner/class 关系的多重集；这不冒称已证明每个原 face 的几何对应。
  物性另有 47 次 Unresolved，原进程 exit 2，不签整体数值通过。
- `tracking-lineage-domain.*`：12 同域控制的实际记录数与 PK 一致，原 exit 2，
  物性十次 Unresolved；它是上一快照的开发证据。
- bounded GDB 实际命中 start_tracking、ISS__phase2、MAR_explore、end_tracking、
  process_boolean_tracking、fill_TOPOL_track_r。求交栈实际经过 face_face、
  target_topols_x_tool_topols、ISS_intersect_regions、ISS2_pursue_bdry_data。
  `tracking-baseline-gdb-sequential.*` debugger exit 0，inferior 的物性未决另记；
  后两个断点发生在第二次 native Boolean，不能声称六点是同一次调用的全链。
  首次多行 `-ex commands` 没有执行命令块，只停在首断点；该日志不作处理链证据。

本增量仅接受 basic tracking；complete/region tracking 仍明确未实现，不用 basic 返回
冒充其他选项。下一项沿同一来源图处理保留边/顶点的 tag persistence 与生命周期，
再继续精度、依赖和剩余建模类别；本次小提交不表示原总体目标已完成。

## 第二项共同机制：原生有限定义贯穿发布和再建模

实际缺口不是缺少另一个几何拟合器，而是同一 native 定义在链中被拆开：
whole-face copy 分别克隆 support/carrier，未复制 vertex precision 和 fin pcurve；
sheet partition 也没有使依赖曲线绑定到实际发布 support。现在批量克隆使用同一
copy context，保持原有依赖共享，复制实际 edge/vertex precision 与 fin curve，
发布后重建拥有关系。单 incident support 的 ICurve 仅在 SameChart 证明后重绑定。
不支持的无空间 carrier tolerant edge 仍未取得支持资格。

原生 TRCURVE 的有限定义还有更强合同：官方 XT chapter 5 的 edge 说明要求其
vertex 位于对应的 declared curve ends。先前分割只缩短 edge interval，保留完整
TRCURVE 定义，真实 PK BODY_check 报 13818；即使该 vertex 的反求距离为零也不合规。
现在严格子区间生成正确的原生 trimmed 定义，保留 signed basis 参数映射与 sense；
求交、UV 和 ray winding 都沿原 native interval 解析 leaf，再把结果映回调用参数。
ICurve 已保存的 paired UV 随该视图直接使用，不额外做全局 inverse。

对原生 SPCurve，新能力覆盖 BSurface 上的非周期二维 BCurve，其每个有限 knot span
能被正权重齐次 Bernstein 系数证明完全处在一个 surface cell，复合次数不超过 16。
临时齐次代入精确表达 S(U(t)/W(t),V(t)/W(t))，供现有有界 plane root isolation
和 curve inverse 使用；原 t、UV 曲线、support 与 XT 定义不变，没有拟合新的持久
空间曲线。跨未覆盖 cell、非正权重、过高次数等仍明确 Unsupported。该代数实现
不是对 PK 内部算法的同一性主张。

PK 静态查询实际链为 SURF_intersect_curve → MDU_surf_intersect_curve →
ICS_intersect → ICS__general，后者含参数分类、局部求解和结果处理调用。
同平台 bounded GDB 在同一次正例实际命中后三层，调用栈相互吻合。
预设的 ICS__process_results 断点未命中，不能据此猜测完整结果处理分支。
`spcurve-native-gdb.log` 的 inferior 实际正常退出 0，后续 bt/continue 已无进程，
debugger 原 exit 1；两者分开记录。

边与顶点身份现在来自实际 source fragment、canonical alias 和 built slot；
target 优先，split 的首个存活 native fragment 保留旧 tag，tracking 同步换成实际
保留 tag。真实 late allocation failure 在身份已交换后触发，输入 XT、generation、
pool 和返回内存均恢复。没有用坐标排序选择身份。

实际验证与边界：

- `native-finite-chain-managed.log/.exit`：90/90，exit 0，覆盖新 SP 代数、sheet
  partition、curved Boolean、共同事件与 dependent geometry；allocation guard CLEAN。
- `sheet-precision-lifecycle-v10.log/.exit`：exit 0。真实 PK 创建并设置 vertex
  precision 的同字节输入，三种尺度 .125/1.7/8，各执行 copy/delete、finite trim、
  mirror/translation、editable XT reload 后再 imprint；九个操作检查点均真实 PK
  full check 与原 strict compare 通过。前期 5000、13818 及编译失败原日志保留。
- `spcurve-finite-native-v5.log/.exit`：exit 0。八种真实 PK 输入，覆盖 rational/plain
  UV、rational/plain support、反向原生 UV 控制顺序；16 次相同有限请求的正负求交
  均匹配独立 PK，八条 fresh XT wire 均 full check/strict/有限双向采样通过。
  另比较 17 点相同 native t 的 D0/D1；采样不声称连续等价证明。
  端点有限范围按实际 native roundoff 输出，未强求逐 bit 相等。早期 exact-equality
  失败及 native 自身拒绝 descending SP trim 的 120 原记录仍保留。
- `persistence-finite-domain.log/.exit`：六个部分同域控制，几何 strict/full 与逐条
  tracking finite-source witness/错误来源负例通过。实体绑定用真实 transmit identifier，
  不是 midpoint 猜测；有限 witness 也不充当连续证明。原 exit 2，六次物性 U 和
  六次 persistence U 分列。GM 在这些模型保留的旧 vertex、部分旧 edge 比 PK 多，
  原生具体 merge/recreate 历史尚未对齐，不能将总数差异签成通过。
- transverse subtract 的真实基线 GM 保留 0 edge/0 vertex，PK 为 15/8；修复后
  对应保留 15/8，并有实际同源有限范围与删源消费者检查。不能从此扩展成所有类别
  tag policy 与 PK 完全相同。

以上是 owner 自测、当前分支增量；尚未独立接受，未与 Mac 性能固定源合成统一交付。
原 M0–M5 未决类别、588 项物性 U、510 边界分类及未执行 FaceAttach 等不会因此消失。

## 固定数值优化合成

以已接受固定 `3aff4e2b0247bd4f21e74e71d437a33dff1a3e24` 相对 `ab597e8a` 的
八个 Geometry 文件算法差异合成本分支：conditioned homogeneous restriction、
position-only bounds、一次 immutable proof 内的 successful spline jet reuse、
完整 parent-cell proof 的 section bounds、局部完整 section root reuse 和 projection
candidate seed 减算。相关七个测试文件随合同导入，包括 private Jacobian delegate
新增 localSplineScope 的签名；没有保留旧签名兼容层。

没有取回两个 Runtime 文件：它们来自较早功能基线，整文件替换会丢失当前 cone/torus
分类及有限 contact distance 修复。Mac 尚在研发的 UV 和 ray extent 补丁不在组合内。
`integrated-3aff-full.log/.exit` 对合成快照实际完成 **2058/2058**，原 exit 0，
不是沿用任一单分支回归。该轮之后的 trace/reader 修复有各自新验证记录；本轮全量
不能替这些后续改动签名。原 `3ce2fa5c` 的 45 个 curved Boolean 全部严格几何通过，
逐关系有限来源见证及负例通过，mass 47U、partial persistence 6U，原 exit 2。

关键组合消费者仍逐操作实际执行中。初轮17个局部 Blend/Chamfer 的上游及 lifecycle
通过，12个下游 Boolean 通过，5个含 Cone/Torus 的下游在 read 阶段拒绝；不将此记成
上游失败。短边后续消费发现新 PreservedIdentity 诊断没有携 support，既有回调因此
非法读取；此原 exit 134 保留，不能签成建模通过。两处在同任务继续修复与复验，
不因形成统一源码就声称统一端到端能力或性能资格已经成立。

## 一条统一研发与交付主线

共同入口固定为已推送 `0fe797e0accbfa1f73f79fd321cc667ff7e18e50`。q2 负责统一
候选集成和最终交付，实施 native 表示、有限来源/事件、precision/ownership、
tracking/tag、Boolean 发布及真实消费者；Mac 负责整链工作量、候选/SSI/材料求交的
结构改进、同版本 Mac 动态 PK 和平台成本验证。两端产物分别绑定平台与真实 source，
共同交付一套候选源码；不再维护两条各自宣称完成的产品基线。

后续共同文件按函数归属：ReadRegularBoundaries 属 q2；RegularRayWinding 的
非解析 extent 分支属 Mac。交换固定最小 patch，不整文件互替。Mac 当前 ray 工作
未在此接收；不会从历史 CCC/ab 再自动开独立优化分支。关键合成后集中一次重大独验，
日常修复与实测继续，不等待门户。

已接收固定 `1fffe8fe2448ff886557a6f888e4395f58b59d4e` 的 UV 生产/测试 patch，
保留其原文和观测 JSON，见 `Native_Operation_System_Alignment_Work.md`。
联合 solver 实际 UV 与 XYZ 同行，反向和闭合行保持同一来源；只有直接 B-spline
unknown 可进入 knownUv，其余 NaN 沿现合同处理。额外 command scratch 32768B，
限额不变。此 patch 原 Mac 未独验，不因导入文档获得独立接受。

## 有限 Cone 候选域贯穿支持面筛选

Reader 原将 Cone/Torus 仅准入 classification；去掉这一接口内的用途分叉后，共同
有限读取给建模和分类同一原生定义。调用方不再传 classification bool，相关诊断
也更新当前签名，无旧签名兼容。初轮实际放行14/17下游，其中3个 Cone 仍在 Cuts
拒绝，说明 Reader 放行本身不足以取得能力。

这三处由无限支持面候选引起：有限 Cone 与远侧有限刀具面不相交，但支持面 SSI
仍被请求。现在复用 Cylinder 的完整边界齐次区间投影，建立 Cone native axial
范围。高度在 regular chart 无内部极值，另保守包含 apex；正权 carrier 的全部
有限 span 和实体 precision 共同构成证书。由该范围及真实 radial frame 包围整个
有限 Cone，而非只取 vertex/采样 AABB。只有双方完整有限面框及 precision 证明
严格分离，才省去该支持面对；未知、非正权、无法包围的 carrier 继续原路径。
相交的支持面对仍走原 SSI、有限 chart/trim、材料和有向发布，未削弱验收。

PK 静态 face_face 实际依赖有 QCU_box、BOX_boxes_clash、有限 bcurve、find_curves_via_iss
等；这只证明对应计算部件存在，不声称本 Cone 筛选与 PK 内部排序完全相同。
真实同输入的每步结果才是此能力的行为验证。

当前新组合实际结果：

- `finite-uv-integrated-focused.log/.exit`：15/15、exit0，含三轴 Cone 全边界与
  apex 包围、零分配，原 Cylinder、UV 同源行/错误 witness、已交换身份后的回滚。
- `integrated-local-finite.log/.exit`：17/17 上游 Blend/Chamfer、17/17 lifecycle、
  17/17 下游同字节 Boolean 均 full check/原 strict 通过，材料与查询同检；mass4U，
  原 exit2。分别保留12/17、14/17旧阶段失败，不倒签或覆盖。
- `integrated-short-edge-v3.log/.exit`：现场独立构造，完整默认 transverse 流程
  full check/strict/有限面、ICurve、fin UV、material、copy/delete、reload后第二刀通过；
  mass8U，exit2。原新 trace 缺 support 的134及调用签名编译失败1均保留。
  新 trace 从实际 retained edge 的 incident fin/face 读取 support，不填伪造实体。
- `integrated-precision-reattach.log/.exit`：三尺度共12检查点 exit0，新增真实
  FaceAttach detach/reattach 原 support，后续 trim/transform/reload再建模均完整检查。
  此处不声称支持任意换面或自动修复不匹配 pcurve。
- `integrated-regular-rolling-v2.log/.exit`：六类 affine、plane/NURBS、depressed、
  NURBS/NURBS、两类凹支撑的独立 Fix 与后续 Boolean 全通过，原有有限接触与
  plane/NURBS生命周期继续执行；mass另列U，原exit2。首次 shell 环境变量拼写错
  的127是执行环境失败，未执行模型；原日志独立保留。
- 新组合 SP 仍按完整正权/cell准入消费；`integrated-native-sp.log/.exit` 的16查询、
  八条实际 wire full check/strict/有限参数/D0D1通过，exit0。它早于新 UV/Reader
  修复，只证明其实际记录的 assembly，最终统一包需绑定对应后续关键检查。

有限-source 追踪的45项与sheet族79项在 `3ce2fa5c` 实际完成；后者原exit0。
本节均为 owner 实测，尚未集中独立接受，不把2058全量或历史观测时间乘成性能资格。

同域 persistence 另以 `--same-byte-inputs` 作控制：先保留独立建模的既有结果，
该新模式的最终 PK 操作明确读取 GM 调用前归档的真实 target/tool 字节，打印各输入
SHA，不重定向 PK 到 GM，也不冒充独立原始构造。`same-byte-partial.log/.exit` 六项
严格几何、finite-source witness 和错误来源负例仍通过，原exit2、mass6U、persistence6U。
因此原差异不能仅归因两个 primitive creator 的参数化；具体 tag merge/recreate
策略仍未对齐，精确数量合同保持未决，不按 model key 选保留数量。

`11bd21a1` 后核查到新 Cone 排除消费者的另一侧若落 BSurface/Blend，旧
RegularSurfaceExtent 的 Success 本身不足以证明正权和完整 consumer 域。已收紧
该新排除：双方仅 Plane/Cylinder/Cone 完整有限 face 证书可使用，BSurface/Blend
保持 unknown 继续原路径；没有把其框当证据。`finite-extent-qualification.log/.exit`
实际15/15、exit0。此前17项另一侧实际是Plane，保留原源码资格；不回写旧源结果。
公共 BSurface/Blend extent 资格由 Mac 单一 owner 维护，交固定公共 helper 后供
Boolean/ray 两侧消费，q2 不另写一套相似判断。此约束同样需要审视原有其他排除
消费者，不能以新消费者收紧就声称所有既有 extent 调用均有完整证明。

原生 SP 新能力的生命周期又在 `d5e00ae8` 生产源执行三尺度 × 八参数族：
`sp-full-lifecycle.log/.exit` 原exit0，48次正负请求、24条 wire full check/strict，
以及各自 copy/delete、mirror-scale、editable reload 后再查询、相同native t的D0/D1
均通过。真实库38.0.150，实际 Native SHA 为
`beafac9f20db64127c2a3d1b542f98c0d24627ae8926ab827b5d681e40f1abb3`。
未加算法预算、未生成拟合 carrier；这些点检查仍不冒称连续全域等价证明。

## 固定交付检查点

实际已封存 `temp_docs/native-system-delivery/checkpoint-11bd.zip`，33,594,597B，
SHA256 `e8fb684e5fb78d2ff64cba1f16ab5c5467122087b44cec4b1188cc79646cebda`。
这是完整可运行集成源码 `11bd21a14a234b3cbf0991b5ff8b6f03752fc3a8` 的开发检查点包，
不是最终完整接受。包内 source.zip、实际托管 Native/XT DLL/PDB、原始日志/退出和
真实XT均列逐文件哈希；无厂商 runtime、credential 或大段反编译代码。

Native 为 `022129f481f39011c75548d72e21efbd32e85af0552bd1e4b954678eb5422031`，
五项具名生产执行打印的实际加载 Native hash 都与包相同；PE/PDB GUID 与固定源码
的340个Native、13个XT文档校验匹配，各另3项generated/external未绑定如实列出。
XT是同一实际oracle缓存目录的companion，未增加运行时XT loader observer，不能
把此文件来源升级成独立运行时加载观察。旧阶段及后来源的原始文件只作归档，不自动
继承此生产hash；后续extent资格/SP生命周期明确是不同assembly。

打包输入见 `native-operation-alignment/11bd-delivery-evidence.json`，封存器为
`scripts/SealNativeSystemDelivery.cs`。实际zip完整性检查无错误；第一次sealer原exit0，
随后将JsonArray写入改为显式JsonNode以避免新工具的AOT分析警告，旧包保持原字节。
该包没有包含私有schema/PK动态库，复现仍先按统一私有环境ensure与固定PKToy准备。
公共extent资格、最终稳定源的集中全回归、独立Native/math和整体性能仍在主线继续，
不会在检查点包或小提交处结算原目标。

固定 ray patch `5c8d0427e9600ccef13e9d94d72d19ae0d9bf472` 已按单commit最小
差异合入本候选，没有覆盖Reader当前签名/ConeTorus或重复UV。其资格仅针对实际
ray consumer，不解除Cone另一侧BSurface/Blend的unknown。原Mac 86项与跨类开发
观测仍属其原source，见 `Native_Ray_Candidate_Exclusion.md`。
新统一组合实际 `ray-unified-focused.log/.exit` 103/103、exit0；
`ray-unified-rolling.log/.exit` 六类独立Fix/Boolean及既有生命周期几何均通过，
mass U保留、原exit2。未据两次日志秒数作正式性能比较，也未当作最终全suite或独验。
公共extent资格由Mac从此共同源继续，原目标仍在执行。

## 连续原生 SP 分段域

新增有效C1二维UV knot后，逐span调用严格隔离把内部knot变成了人造请求终点。
`sp-knot-root-independent.log/.exit` 保留 GM157 与独立PK0/1root，原exit134；
该轮两侧已分别执行，先前首GM失败后未执行PK的日志不作PK通过证据。
原生多段linear UV先被PK999拒绝；官方FD95.3.1.1明确不允许这种SP定义，实际
ask的degree1/dimension2/knots/poles记录保留。改用同native t的精确quadratic
定义满足C1，不关闭PK连续性检查，也不把原输入错误算GM建模失败。

现在一次准备全部有限片段的精确齐次复合系数，以只读piecewise计算视图供原
SurfaceIsoPlaneRoots在完整有限t域证明。必须由原始UV和surface knot multiplicity
保证C1，正权/cell资格逐片证明，域连续完整覆盖；位置与一阶导数对跨knot请求
取全部片段外包围。C0/G1不能自动代替该solver的C1前提，未知、切触、无正权或
无法覆盖者不签空交。没有拟合持久曲线、改native t、调容差或增迭代预算。

共享SurfaceIntersectionPatch显式区分此视图的degree/变量/过程性质；不提供假
ExactCorner/控制网，不继承二维cell proof。iso/offset/sweep/parameter-line组合
当前明确Unsupported；SupportRange沿同C1 bounds和均值定理，不落空bezier分支。
单片保留固定stack workspace，多片按真实片数借command arena并在finally归还，
借用期间来源和系数不修改；没有新增heap或扩大arena。

实际 `sp-continuous-managed.log/.exit` 19/19、exit0，含两侧导数包围、跨knot两根
互不合并、缺域和C0负例，以及原section/projection消费者。
`sp-continuous-same-bytes.log/.exit` 24参数/尺度族48正负请求、独立生命周期、
native full check/strict及D0/D1全过，exit0。query的PK端另读取实际GM reload
输入的同字节，独立native transform仍另作生命周期基准，不混淆两个合同。
`sp-exact-failure-replay-v2.log/.exit` 对原157前已归档真实GM输入及原有限请求
参数执行同字节GM/PK重放，两侧0/1root且负区间0root，exit0；该模式仅签query，
不冒充独立构造。原输入SHA `c7f124ca905cdf9b73c925a7322bae068dcb0309de563e0c1f5d49a230bea3c9`。
首replay日志的末行source hash误取了未使用factory输入，实际query输入hash另有
正确记录；保留原日志，v2才纠正该描述，未改模型结果或覆盖原证据。

## 单一计算负责人及显式搜索域

后续执行已统一为本分支单一计算负责人，含正确性、整链性能及统一JIT策略；
前文Mac分工仅保留为历史阶段记录。Mac原coding worker已停止，其平台可作命令式
验证平台，不再与本树并行写生产算法。收到的未完成extent两文件只作候选输入，
经实际读取、准入审查和真实PK对照后才进入本树；没有继承原9项自测的接受资格。

`BSurfaceDomainBounds.Position` 接受明确native UV矩形及是否允许线性外延。
natural内部仅有限正权源能用控制凸包；外域按native两方向线性延续分区，在原
Bezier端点/边上对S、Su、Sv、Suv做区间包围，包含双向外延交叉项。
周期越界保守覆盖整natural周期。无效、未证明权重或无法包围不签Success。
它是临时只读计算能力，不写新的几何定义、native参数或拟合carrier。

`NaturalSurfaceExtent` 的全部四个消费者现在显式检查该状态，避免直接除权后无条件
返回Success。`RegularSurfaceExtent` 明确针对其实际patch枚举域：BSurface全部
native knot cells，Blend全部保存CHART的u及record v；不再用可能较窄的record u
去证明整个搜索。距离操作与ray共用该域。ray的behind-origin排除、有限上端点和
slab排除因此使用同一个完整搜索域证据，而非只给新增slab加资格检查。
这个证据仍不能直接签任意trim face，相关finite-face消费者的native域覆盖继续研发。

实际 `explicit-search-domain-managed-v2.log/.exit` 59/59、exit0，包含共享SSI、
距离/SP及ray资格控制；不是最终全suite。`bsurface-domain-native-v3.log/.exit`
真实38.0.150的plain/rational与两组native UV gauge共16个请求域、784个求值通过，
域含natural、双向外延点、跨四边矩形及内部子域。该测试为求值语义核验，连续包围
来自区间算法，采样不冒称全域证明；完整建模消费者需另以固定源真实XT复验。
前两次原exit134保留：原生浮点求值在natural上界给1.0000000000000002，不能要求
其数值结果严格落入精确函数的凸包上界1。v3单独声明64*2^-52*max(1,abs(component))
的求值舍入比较，未改生产区间/建模容差；相同native参数两端求值另保留1e-11检查。

`scripts/CompareNativeJitPolicy.cs` 准备一次固定源Release IL的ABBA(关/开/开/关)
完整操作比较，四个解析/非解析支撑族各两次fresh模型，独立Fix与Boolean均执行真实
PK、fresh XT strict/full及既有生命周期；raw exit2的物性U仍单列。
PGO/QuickJit/QuickJitForLoops固定1、ReadyToRun固定0，只有TieredCompilation
变化；子进程清空环境后仅恢复明确非凭证白名单。runtimeconfig与环境同值，另以
不计时的GM自有最小方法JIT输出检验FullOpts/Tier0实际分支。选择依据是整组GM API
总时间，不按case或操作选配置；首轮序号仅称该family首次使用，不冒称全部process-cold。
依据为官方[编译运行配置](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation)
及[动态PGO说明](https://devblogs.microsoft.com/dotnet/announcing-net-6/)。
本段只记录已准备的有界执行合同，尚无选择结论，也不回签历史未显式记录的默认环境。

## 原生UV裁剪与CHART方程的共同链

统一运行配置已实际选择并落地，见 [运行策略](Native_Runtime_Policy.md)。此后真实
PK/GM脚本经 `scripts/RunNativeOracle.cs` 启动，固定整组设置，不按case换JIT。

滚动接触原来把已有native UV再次变成两条空间曲线求交。现在直接复用joint solve
已经证明的支持UV及其native-station导数：`BoundSpineSupportUv` 同时支持BSurface
和已有非周期Blend参数视图，原nearest/contraction及预算不变。临时计算视图为
(u,v,0)，只用于UV-ray标量根隔离，不是新的持久几何；offset/iso/sweep组合不准入，
不继承XYZ或标量u缓存条目，不给它二维cell proof。Estimate也按同一参数视图求值。
接触曲线使用实际整个保存station域求根，再按原CHART关联返回自身native t，
不以两个端点的局部反求估计缩掉未知分支。隔离器按证书去重后，结果直接逐根发布，
不再次用XYZ epsilon归并已证明不同的根。原finite endpoint分类合同仍单独保留，
这不等于所有native精度边界或所有trim face已完成。

实际 `native-uv-scalar-v2.log/.exit` 四族各两次fresh模型，独立Fix、Boolean、
真实XT full/strict及既有plane/NURBS生命周期通过，原exit2、mass U另列。
该开发执行的实际Native为
`e1e3da424053ac1ee1b5cf73910f38ed07cccd764a4d33a5c8ed27bcbb272ea7`；
16次GM API合计32113.431ms，相同平台/配置的先前两个TC1进程分别63214.930和
62528.253ms。它是该开发assembly的整操作观测，后续CHART改动仍需在固定最终源
集中测量，不能把约49%的开发差值升级成后续全部assembly或独立性能接受。

扩大到复制/删源/反射旋转/尺度/回读后再建模时，确实暴露了凹NURBS/NURBS后续
Boolean 5018；上游Fix和变换后的strict/full已通过，不连坐改判。5018按实际生成
头文件是eval_failure，早期口头bad_loop描述已纠正。独立PK同operation返回0，
冻结真实输入SHA为
`e12dbabfd4afa65689e3bb415212cfdddb5d779c5be0ffa76b87ab6870c78fed` 和
`12bc647a68422b83e4c75145fc7cf52a9a93468bacea8a2bd5c396737f673ec1`。
逐步定位后实际失败是新生成BSurface/Plane ICURVE的内部witness求值，非先前日志
残留的UV tangent；其原report为P4、segment4、52iterations、residual约5.57e-16、
AmbiguousBranch。原始失败及环境127保持原字节，没有用重跑覆盖。

CHART的原方程是dot(S-anchor,chord)-(t-t0)/scale。旧路径先把S舍入为世界XYZ，
补偿减法也不能恢复在这个舍入中丢掉的值。现在对native齐次控制系数先计算同一
线性泛函，并在两分量精度中减去原native参数步；不移动anchor、不改原t或scale。
P2、P4及最终发布使用同一方程；反支持顺序时P4 Jacobian和D2右端同步选择实际
BSurface列。物理D0误差、UV分支一致性和独立双端continuation门槛保持不变，
没有增预算或放宽旧标准。代码及同输入前后结果支持舍入路径这一根因；这不是对
PK内部使用同样求解器的断言。

`concave-native-uv-stable-chart.log/.exit` 冻结同字节Boolean两侧0/1product，
GM fresh XT full/strict通过，exit0；该脚本仅验几何操作，没有积分，不冒称物性通过。
`native-stable-chart-cross-family-managed.log/.exit` 93/93、exit0，含plain/rational、
双支撑顺序、native gauge、反射旋转三尺度、P2/P4 D0–D2、细薄分支负例及subnormal
CHART合同。`native-uv-stable-chart-remodel.log/.exit` 三族×三尺度的九个完整生命周期
再Boolean及各族直接Fix/Boolean均full/strict通过，原exit2，mass U保留。
该实际生产Native为
`45b1d2e6a29c1c1d55931fa62f0db3b2516f37305f86b73b819c01c7fcc7bc0d`。
全部是owner实测；整体任务、剩余finite-face域资格和最终集中独验仍在执行。

稳定源83649的首次完整回归未完成：原log/exit保留于
`temp_docs/native-system-alignment/83649-full-managed.*`，raw exit1，runner实际记录
1166项完成通过后host aborted。owner对已核实自有testhost PID2950337主动SIGTERM，
不是新模型失败或整suite通过。约十分钟时的一次有界dotnet-stack观察定位在
FilletInputClassificationTests的输入Boolean：SSI已有native UV，但
ChartSupportWitness→NestedBlendSeries.Spine→NestedSpline.Distance仍重新做全局
BSplineProjection。该单次栈是实际调用链，不能解释成各帧排他CPU比例。

这里直接改用既有JointNestedBlendEvaluation.EvaluateSpine消费原native u及保存的
支持UV，保持Blend radial normal定义、NormalSense及误差检查。没有删nearest证书，
没有换参数或创建新的求解器。实际 `native-chart-witness-managed.log/.exit`
22/22、exit0，覆盖原来昂贵的回读/再Cut分类、100/200尺度接触查询、Blend求交及
精度控制。

随后固定源 `8b929da83f9f039ab3bcd5b210bfd492aaa7408c` 的完整托管回归
实际2101/2101、raw exit0、13.6835分钟。四族各两个独立输入的Fix/Boolean以及
三族各三尺度的copy/delete、反射旋转/尺度、XT回读后再Boolean均真实PK
full-check/strict通过；两个执行raw exit2，物性U保持。原生SP的24族48查询及有限
wire生命周期验证raw exit0。四项执行及原始XT绑定的实际Native SHA均为
`9bc6ac8a0281ca246d537bb73ba64da43d2cb3ea26b3018af9f8cb5f5518d9b3`。
托管整suite日志没有额外打印assembly SHA，包中PE/PDB源绑定独立记录，不能用它
补造测试进程的加载观测。

固定策略下该四族16次GM API合计32087.767ms；先前同平台TC1两次分别63214.930和
62528.253ms。它证明这个有界工作负载的实际整操作代价下降，PK差距仍大，未取得
独立性能接受。测试suite耗时包含不同测试与配置，不据历史未知配置作性能归因。
`docs/native-operation-alignment/8b929-delivery-evidence.json` 明列源、执行、raw exit和
未决；封存 `temp_docs/native-system-delivery/checkpoint-8b929.zip` 为26463934字节，
SHA `c7d58d06ec750e55a6e734a4965272cb20633d5623dd6490eefec8279008151f`。
包内源archive与两个GM DLL/PDB逐编译文档核对；JIT ABBA原日志另标1bbf源，
未纳入厂商库或诊断工具目录。这个可复核checkpoint不表示原总体建模目标完成。

### 原生曲面成面与自由定义的拥有关系

官方FD19.6规定非周期sheet参数两端都须在曲面的原生范围内，周期参数起点在范围
内且跨度不超过一周期。FD30.6说明FACE_find_uvbox应覆盖整个有限face，不能把
任意搜索patch的box当成它的证明。静态实际链为SURF_make_sheet_body→
PRI_surf_create_sheet_body→PRI_create_sheet_body，其中前层含GDS_duplicate及
MOD_unshare_geom_from_world，后层含MOD_owner_of_geometry、参数空间分类及多种
sheet构造器。静态存在复制调用不证明每次都会复制；以下分支来自38.0.150动态观察。

普通/正权有理B曲面×unit/shifted原生UV×六种域请求，原实现16个越界请求将
PK的5038 bad_uvbox误报120，八个合法请求总复制并让源在body删除后错误存活。
现在合法独立自由源采用原对象，已有face或自由SP几何owner的源复制，拒绝越界
保留源；源record在新分配前按值保存，既有拥有关系先journal再发布。
`native-adoption-v1`、`native-shared-adoption-v2` 两个实际24请求报告均raw exit0，
合法首/次成面分别独立PK full/strict，删源生命周期一致。已有consumer不再把
被采用的surface当成orphan删除；曲面裁剪、分割、回滚、零heap及成面共79测试
实际通过，raw exit0。首轮测试中的旧orphan计数和主动删除假设已按真实PK纠正，
原失败日志保持，未改成忽略API错误。

Blend观察另揭露下一层差异：独立自由Blend首次成面也采用root并在删body时删除
其原生定义closure；SurfCreateBlend本身引用输入surface，不是无条件复制。
输入已有body时，Blend归入该part的construction定义，随后make_sheet复制root。
自由SP同时引用Blend的support时，PK成面保留原Blend/support身份，并将外部SP
改引复制support，删body后SP及其几何仍可求值；GM旧rolling构造先复制所有
support，掩盖了这个unshare分支。`blend-adoption-v1/v2`、
`blend-shared-adoption-v3`、`blend-owned-support-v1` 为实际观察，不作为GM该完整
依赖合同通过的收据；该共同对象图差异仍在实现中。

后续实现直接令rolling构造引用原支撑，复用现有part归属检查；成面后从现有对象图
取实际boundary defining closure，只有外部自由owner碰到它时才通过同一复制映射
重绑依赖。因此两个SP与Offset保留共享的同一新support，未改变的2D原生参数曲线
不复制；原Blend、support、spine身份属于新body。所有重绑的owner数据、依赖计数
及construction元数据都由既有journal保留。复制给runtime borrower的support继承
明确释放义务，不把原独立借用输入一起删除。没有新模型、历史路径或坐标分派。

真实PK扩大负例又确认一个2D B-curve不能同时成为第二条SP的参数定义，第二次
SPCURVE_create返回914 unsuitable_entity；GM原来接受，现在同样先拒绝，输出0，
原图不变。第二条SP须独立创建其2D native basis，表面仍可共享。原首次重用basis
失败raw134完整保留，没有把它计作unshare几何失败。

启用真实PK delta rollback后（复用MarkOracleStorage），动态确认mark只保留恢复
能力，不应让已删body的定义root继续作为有效tag。旧GM在deferred payload释放前
仍让Blend/support/spine有效。现在先准备删除closure及日志容量，常规拓扑删除后
只retire已无任何活topology/geometry/fin owner的定义；物理payload仍延后释放，
共享分支等到所有父节点失效才retire。runtime ICurve借用的独立输入保留；若原body
留下明确释放义务，则最后借用者删除时失效。mark goto恢复原定义身份和计数，mark
delete物理回收无重复释放。该逻辑只在retained mark下运行，普通整操作不加全池扫描。

`blend-multiple-unshare-mark-v2.log/.exit` raw0实际两侧914负例、两个SP和Offset共用
同一复制support、删body后外部曲线/offset求值、原definition tag失效、goto后原身份
恢复全部一致；`blend-owned-support-contract-v2` raw0验证已有body输入的原part保持。
这两项及三族独立Fix/Boolean full/strict的 `native-owner-graph-rolling` 实际Native为
`9ddb29ffedbc9e8315d4d3129f70b0695339bc6959e1c5a65acbbf6197f1f349`；后者raw2，
mass U不变，误写的depressed-nurbs过滤名没有执行depressed-roof，实际只三族。
`native-owner-unshare-open-blend` 九种上游sheet后裁剪/分割/重复切割full/strict通过，
raw0，生产源早于mark改动，不补签为随后所有assembly的验收。

最终owner局部 `native-owner-final-borrowing` 69/69 raw0，含两种mark结算、外部
SP/Offset共享、借用ICurve的新副本回收及原输入保留、warm成面+删body实际零managed
heap；此前 `native-owner-final-focused` 131/131覆盖分割/part/lifecycle，另存原源。
原完整Mark前后对照、错误5048环境执行及被纠正的旧失败日志均保留。当前仍须固定
新源集中做真实PK、完整集成与原剩余finite-face/分类能力，不据这些局部计数结算整体。

### 原生定义域、有限face与完整闭合实体

官方FD18.3/30.6、FD96.1.4把B曲面的自然边界定义为surface定义的边界；它不等于
求值器允许的线性域外延拓。现将finite face消费者从RegularSurfacePatch的搜索
范围移到独立NativeDefinedSurfaceExtent：原生BSurface域显式禁止非周期延拓，
正权控制包围覆盖该定义；Blend使用原record native u/v及实际CHART覆盖条件，
不再因搜索遍历较宽的CHART而偷换face定义；Sphere/Torus用完整解析包围，Torus
采用abs(major)+abs(minor)，也不把负major的合法范围误套为正major圆环。
这是有效native B-rep的定义包围证明，未把采样当连续面证明。无完整资格时不排除。

Boolean候选对统一使用完整finite face证明，复用既有bounds记录并在当前不可变
输入视图内惰性缓存，每face一次。两侧实际face/edge/vertex precision分别纳入，
以向外舍入的两侧margin之和判分离；未增SSI/root预算。点到support的初筛同样
取得实际source face精度，较宽精度的点不会被固定1e-8的box门提前丢弃；最终
有限winding、几何求值及操作质量门保持独立。这没有证明原510近边界分类已全部
一致。`native-defined-precision-v2` raw0为真实PK设置顶点precision后原XT直读GM，
八个合法plain/rational及native gauge输入的fresh full/strict及精度包围负例通过；
同一执行24域请求/两次成面/删源合同一致，16个bad_uvbox负例保留。

新增异类分离场景暴露原有loopless读取缺口：完整环面合法地没有edge loop，
ReadRegularBoundaries此前返回5000。官方Overview 3及XT §4定义solid为有限体积，
外部无限region必须是void。因此对两个已验证native solid，只在每个face的完整
原生包围连同精度都可得且整body包围严格分离时，直接按no-clash合同保留/删除
原body，复用原平面路径的结算函数；保留原tag、空basic tracking和原operation
result。不重建未变拓扑，不据顶点盒替整个face。盒重叠、接触、未知资格仍进入
原求解链；环面相交建模、apple/lemon分类及任意loopless多面体未因此宣称完成。

完整单face正向ring torus的分类直接使用其原生meridian circle距离，返回真实
face或material/void region，包含对称轴/中孔；条件明确排除自交类型、有限trim和
多region组合。`native-closed-solid-v1` raw0为六个oblique-frame/镜像/三尺度组合，
独立两侧copy/delete、真实变换、各自XT回读后54个inside/hole/outside/on查询，
enclosure和topology class一致，所有fresh full/strict通过。没有把世界坐标样本
用作算法分派。`native-defined-torus-complete-v2` 三种Boolean的geometry/full/strict、
各结果125点查询、basic tracking、原topology保留数及operation result一致，raw2仅
原积分证书U；先前no-loop建模失败raw134及随后NULL空拓扑数组释放的oracle错误
906都另存，未以checker退出0补签。解析原型及Native solid定义的依据来自实际
官方文档；距离公式由原生Torus定义推导，不声称恢复了PK内部完整分类求解器。

NativeClosedSolid与上述Torus Boolean实际Native为
`1345c23b474994b7780c25c8616f8e8d0000c79c68beb98ab05a75a57b24f686`。
此前四族各二次独立Fix/Boolean全部full/strict通过、raw2，实际Native
`20a13c989e01b82538f25a8ffbbd9da3e2d6320407422d07753fb93b83176bf0`，
不作为随后完整闭合体增量的性能资格。局部managed 108/108，以及最后12个native
定义/宽精度/零heap/闭合体/三操作tag与mark合同测试raw0；最后源仍需完整集中回归。

固定a8b409的完整回归实际raw exit1，runner报告aborted、491 passed/24 failed、
2.6506分钟；它不是完整通过。多数rolling失败是ClipRegularFacePair可接收尚未
发布、无原face映射的生成patch，新初筛却索引了可选sourceFaces。现缺少真实
拓扑精度时不作这个排除，生成patch继续原有限裁剪/事件证明。旧闭合Blend测试
主动删除已被原生定义拥有关系销毁的输入也已改为断言输入tag失效。

有界创建/回滚跟踪还定位到第二个实际释放策略：共同ReleaseDefiningGeometry已
按deleteDependentOrphans=false执行创建undo，但FreeICurveData又沿旧import
reclaimer物理销毁零owner支撑；随后construction snapshot恢复其surface header，
早期creation entry再次释放已死payload。这正是一次typed-data Debug.Assert，
而非PK建模结论。现移除该旧拥有关系fallback，payload函数只释放自身存储；
bound实体统一由共同依赖图释放，unbound materializer继续明确清理其自建输入。
临时栈跟踪已全部移除，原trace和失败保持。`native-single-owner-release-managed`
91/91 raw0覆盖三价corner真实拒绝/多深度allocation rollback、闭合continuation、
共享/借用/复制/mark生命周期；rolling生成视图的前一轮41项通过及三项同释放错误
单列。整体仍在修正后的集中回归及真实PK验证中。

7d474完整回归另存raw exit1，runner实际aborted、1013 passed/1 failed、3.5207分钟。
rolling网络和三价失败回滚已经通过；唯一报告失败为回读allocation budget0多留
136字节，随后缺失顶点几何负例在新body包围预检中触发访问错误。这两者均按
实际发生处理，没有继续使用未完成suite签全部通过。包围预检现在确认point/curve
定义就绪，有限边区间计算也在解引用前确认顶点point，缺失定义继续原missing_geom
合同；17个原负例通过，未用不完整几何签no-clash。

跟踪实际分配与释放指针后，136字节准确分解为BSurfCreate在句柄登记失败后留下
的80/48/8字节缓冲区。分配成功不等于已取得稳定handle；旧错误分支只通过handle
释放，遗失未登记的原指针。现该发布前失败分支释放七个原始缓冲区，再释放metadata，
已登记的handle由同一次Free注销。BCurve已有对应正确原指针清理，按事实复用合同，
未改HandleOf语义或让allocator擅自接管调用者指针。推测的typed-pool destructor
试验无效，已完整撤回；分配跟踪及callback已清除。原allocation IL guard未放宽，
曾直接加入字符串诊断的构建被它拒绝（raw exit1），不计成建模执行。
`native-buffer-registration-and-readiness.log/.exit` 72/72 raw0，涵盖全部原25个回读
allocation位置、共享/回滚与缺失几何；随后仍需完整集中回归。

同平台bounded GDB对7d474真实默认短边两个产品调用accuracy=1，实际观察API→
MAS_bodies→solid_occurrences/solid_occurrence_thread→MAS_regions→regions_internal
→MAS_faces；另一实际栈为faces_internal→mas_faces→boundary_curve_method→
MAS__b_curves→integrate_under_b_curve→MAS__over_cylinder→MAS__integrate_vector_dot_dx。
这是本次命中的实际分支，不能把未命中的一般曲面积分方法解释为不存在。
debugger raw exit0，inferior exit2（mass U），两输入及模数保存在
`temp_docs/native-acceptance-contract/mass-bound-native-gdb-7d474/`；原两侧仍都是PK积分。
官方FD31.1明确accuracy是工作量指标，31.4.1要求保留模数。此执行未降低物理norm
目标、未增默认积分工作量，也不把区间重叠视作1e-7等价证明。

Mac使用独立固定7d474 worktree，仅由本owner远程命令执行，没有新的编码worker。
私有依赖ENV_READY后非login SSH的dotnet PATH首次127单列；明确使用既有SDK路径，
实际SDK10.0.400、PK38.0.150、同五项JIT策略，54/54完整ring torus复制删源/镜像尺度/
回读材料查询及fresh full/strict通过（`7d474-mac-closed-solid.log/.exit` raw0）。
Mac实际Native SHA
`c523d16ecdde2d5c5a79672f0c194e8a19a061e8bfa2b2b31aae03b39c7fd2e3`；
它是这个固定源和能力的跨平台收据，不补签后续buffer/readiness修正或整体性能。

### 固定2f997的集中执行与定义域观测

`2f997-full-managed.log/.exit` 完整执行2117项，2116通过/1失败，raw1，13.6458分钟，
没有aborted。唯一失败为旧GeneralConstruction测试在删除成功的自由Blend后仍要求
两个定义支撑存活。新增真实PK自由root删除对照 `2f997-free-root-delete` raw0：
两侧原始两个support及spine均失效，证明此断言与当前native拥有关系冲突。测试现
分别验证失败时原输入存活且无中间泄漏、成功删除时原定义失效；每个budget重新
建立独立输入。`2f997-native-contract-assertions` 25/25 raw0，原full raw1不覆盖，
不把局部复验冒称新的全suite通过。生产代码仍为2f997。

统一JIT下 `2f997-whole-profile` 为四族各两个fresh输入，16次GM Fix/Boolean合计
30481.650ms，全部独立full/strict通过，raw2保留mass U；剖析开启且与全suite并行，
不是独立性能资格。三类非解析实体各三尺度的copy/delete、反射旋转尺度、原生XT
回读及独立变换刀具再Boolean全部full/strict通过（`2f997-lifecycle-remodel` raw2）。
NativeSP另24族/48查询、插入UV结点、copy/delete/mirror/scale/reload、17点D0/D1及
有限wire比较通过（`2f997-native-sp` raw0，连续等价证明范围不扩大）。上述执行实际
Native SHA均为 `585416e1ba9b839d74c5a7a4d274fd9a21b1f7385a61727a21d20ce446e149ef`。

直接510点分别另存 `2f997-self-510` 与 `2f997-import-510`，均raw2，无API错误或
Inside/Outside翻转，分别15U/16U，独立native分类没有改变旧坐标集基线。两次直接
native同XT回读均在102/153/204/254/289/390/441七行有On差异；它们不与历史Capture
八行执行合并。有限数学边界及precision控制JSON均另存，不把四方强制相等，也未
按点坐标改变分类。历史self495缺失列仍缺失。

旧计划中的单个存活三方交汇/投影cell场景在本固定源已实际通过：
`2f997-three-participant` raw2仅mass3U，独立现场Fix后两刀联合Boolean，真实三方
事件、两次反向使用、投影cell材料六点、finiteface/ICurve/finUV、full/strict通过。
这更正该场景的当前执行状态，不把历史19/49/32严格比较失败当作当前失败，也不
扩展为任意多方/接触/高价交汇全部支持。变换族及其他原未决仍按实际执行核实。

新增只读 `NativeSurfaceDomainOracle --inspect-native=<项目相对XT>` 对四个独立
PK产品实际full check后记录33面/5个B曲面定义域、有限face UVbox及周期标志，
输入原字节按SHA另存；`2f997-native-domain-survey` raw0。五个非周期B曲面最大的
UVbox外越为2.506e-15，其余为零或更小舍入量。此项属于动态观测，不是任意surface
域的连续证明，也不据UVbox端点微小越界宣判实际face跨出数学定义。官方自然边界
合同、原生domain包围及实体precision仍各自是候选资格的必要依据。

### 正在实施的原生精度与参数见证链

在固定2f997原三方场景通过后，扩展原oracle使种子和两刀实际使用同一similarity，
并可反转两刀输入顺序；两侧同options，default_tol/blend_radius随单位尺度同变。
调用前两侧真实target/tool XT以及独立PK操作后的完整检查产品分别保留。镜像旋转、
1.7尺度、反刀序的执行实际暴露157，不能因原坐标场景通过而宣称变换族已完成。
当前工作代码仍在该共同模块内研发，没有使用坐标、model name或tag作为算法key。

官方FD17.2.1/17.2.2分别规定原生线性precision的模型一致性及切触附近的接触事件，
并不要求每个有限拓扑接触都为binary64表示下的严格代数零。动态观察到本输入的
端点平面残差约2.4e-14、法向二阶导约67.4，独立PK完成相同联合Boolean并full check0。
候选有限端点provider以完整终端半区间的有符号曲率界、端点D0/D1界证明接触簇和
驻点都归属原native vertex的precision neighbourhood，再严格隔离完整余段；
ScalarRootIsolation.Certified保持false，不把这个原生端点事件冒称精确标量零根。
同carrier不同方程的native事件还要求同实际finite occurrence、同root ordinal、
重叠的已隔离参数盒、其完整并集的连续空间直径及两侧真实surface距离均不超原
linear precision。数学上严格同根的原检查及其2^-56位移负例保持独立，不改其结论。

另一缺口是把解析spine的辅助CHART cell当成最终native域。候选保留每个原搜索cell
和原预算，只对已明确的一条解析native gauge用真实surface定义域验收存在/唯一性
证书。没有扩张最终有限域；真实域外根仍排除。曾试用整个gauge的一次搜索，实际
达到原12000-cell预算且未取得整操作收益，已撤回，未提高预算。新增域外负例和
跨搜索分界的连续根/零heap检查随数学验证保存。

有限winding继续消费同一次joint solve的native UV。非周期Blend的通用参数射线
使用原U/V及导数作一个标量方程；周期conic候选使用实际子spine centre的原生
Gram对偶坐标构造相位方程，并以cos相位的正半平面去除反向根，避免在世界XYZ上
重新做两条曲线的三维交。依赖query的phase视图不读写未含query的command共享
cache，仅使用该方程私有span cache。周期分支仍待专门的数学/真实PK正负族验证，
不能以本输入的非周期Blend成功消费替它补签。

原生端部closure同样存在2ULP域界差：实际两接触的U均为1.5707963267948966，
record上界为1.5707963267948961。候选仅在共享原顶点、两条真实接触及完整连接
U/V strip都能连续包围在一个linear-precision neighbourhood内时采用声明的native
端界。为得到这个证明，零夹角section把权函数的完整零阶和先作代数消去，再保留
所有高阶项、解析导数和原余项界；未采用被拒的PreparedSection/NativeScalarLevel
缓存或改预算。新38项数学检查通过，前一轮11项失败是private Weight改名后旧测试
反射查找失效，已更新为直接验算余项的独立decimal/半角参考，原raw1保持。

本候选 `native-domain-contact-integrated-managed-v1` 190/190 raw0、allocation IL guard
CLEAN；`native-domain-contact-default-v1` 默认独立Fix、完整两刀/copy/delete/反射尺度/
reload、48材料点、finite/nativefin/full/strict全部通过，raw2保留mass8U。
该生产assembly SHA为 `d71d268b499fedd5f9fc3df6124520800473d6c349d4240abb9b831a750dccd2`，
四个实际managed DLL/PDB保存在
`temp_docs/native-system-alignment/experimental-native-periodic-contact-v5-managed-production/`。
其source为614e7ede加 `native-periodic-contact-ray-v5.diff`，不是614e7ede的干净构建。
多数早期失败迭代未逐次保留PE/PDB，不事后重建冒充原二进制。

新增镜像联合Boolean目前已越过端点、native跨CHART根、事件40及finite winding，
仍在后续RootProjection返回157；独立PK成品及原raw134全部保留，没有补签完成。
一次额外携带位置AABB的试验没有改变这个阻断且增加临时存储，已撤回；当前继续
核真实共享支持/CHART见证及有限参数投影，不能把这次试验或候选测试数量算作能力。

### CHART 点是原生定义，公开求值不得用重解覆盖

官方 XT 第 6 章 Intersection curves 的 Chart 部分明确：参数恰等于 CHART 点参数时，
D0 为存储点；段内才由两支持与 CHART 平面定义。源码核查发现普通 ICurve 路径已遵守，
有 compiled programme 的公开 `CurveEval` 却绕过该分类，重解后覆盖了原点。
本次将相同合同用于 compiled implicit ICurve：精确 native 参数直接返回原点，
请求导数时保留原联合求解的单侧导数并恢复存储 D0；相邻参数不吸附。
联合区间求解器的数学方程及预算没有因此改动。

`scripts/NativeChartPointOracle.cs` 对同字节独立 PK/GM 回读的曲线按实际参数和位置
唯一配对，记录精确 CHART 参数和两侧相邻浮点参数。开发执行
`temp_docs/native-system-alignment/native-chart-contract-v1.log/.exit` 为 raw 0，
3 条曲线、10 个点；真实 PK 到存储点约 0–1.8e-15，旧 GM 有约 6e-12 重解偏移。
修正后的 `native-chart-contract-v2.log/.exit` raw 0，同时要求精确参数的 GM D0
逐坐标等于存储点。两次 assembly 分别为 c056db99f63ab9a23d7f065e11b0156c3e50152683959e1912fbabe70e9a3c96
和 a46423653ac00e62ea6b51934276dd27447bfb0b4b7f13f95a1ff97cb1d23899；均为带在研
改动的实际开发程序集，不冒充干净提交的完整交付。

同平台有界 GDB 的 `native-chart-chain-gdb-v1.log/.exit` debugger/inferior 均为 0，
实际栈为 `PK_CURVE_eval → QCU_cvec_at_parm → QCU_deparm_intersection →
QCU_svecs_from_parm → svecs_from_parm → QCU__icurve_eval_chord_plane`。
这是动态观察的该分支；`QCU__icurve_eval_step` 断点未命中，不推断其不存在。
MCP 的优化求值器空调用表也不是没有内部调用的证据。
`native-chart-anchor-managed-v1.log/.exit` 37/37 raw 0；该修正没有签署仍失败的
新增联合 Boolean。周期 native phase 的六个数学包围检查另为
`periodic-support-phase-math-v1.log/.exit` 6/6 raw 0，仍需真实周期 PK 消费者验收。

### 搜索分片与原生有限域分开验收

解析 Line/Circle/Ellipse spine 的辅助 CHART 分片用于候选搜索，不构成新的模型边界。
`ParametricSurfaceIntersection.Isolate` 现在接收明确的原生有限域，最终根包围仍须
整体落入该域；数学扩展域的根仍不自动属于有限输入。Runtime 仅对实际显式解析
spine 的 Blend 提供原 surface record 的 UV 和原边 native t 域。隐式 spine、
一般曲面和原搜索预算保持各自原合同，未改成一个大搜索单元。
新增制造解同时验证内部分片交界可接受、真实有限域外必须拒绝和零托管分配。

在研组合的 `native-finite-system-integrated-managed-v1.log/.exit` 301/301 raw 0；
它不签署其后源码。`native-finite-system-default-v2.log/.exit` 的真实 Native assembly
为 100f847eb5c99a998fe6b1069ed3e1d0f40319d712b92392ac0bf84322632291，默认原生自建
Fix、同模型双工具 Boolean、fresh XT fullcheck/strict compare、有限 ICurve/UV 通过，
raw 2 单独保留 3 项物性区间 Unresolved。该开发组合还含未提交的端点/UV候选，
不是上述单个提交的独立资格或完整交付。新增镜像尺度联合场景仍未通过；其实际
运行已从裁剪推进到分割及 arrangement，不能以阶段推进签署最终 body。

另一个同轴弧半开射线计数试验导致默认 Fix 材料选择失败，原始
`native-finite-system-default-v1.log/.exit` raw 134 保留。该候选已撤回，默认 v2
恢复通过；不能只依靠其制造解或较晚的失败阶段保留一个改变分类的近似规则。

## 原生端点、参数消费者及投影接续的共同实现

本轮接续将已有 arrangement 继续用于发布，没有建立新模型或通用 Engine。处理链中
已落地的结构变化如下，case 名称、坐标、tag 和变换选项均不参与内核算法分派。

| 处理点 | 当前实现与边界 |
|---|---|
| native 有限接触 | 原始顶点、实体 precision、完整有限段曲率与 Taylor 排除界共同认证端点接触簇；精度接触的 `ScalarRootIsolation.Certified` 保持 false，不冒充数学方程的精确零点 |
| CHART 交接 | 共同位置/中心/native-u 七变量见证、两侧收缩与方向、存储点精度球共同处理内部交界；未证明的另一个候选不能因某段已接受就消失；原生端点只能匹配一个已声明的 cut CHART 端点 |
| 事件与有限原边 | 同一有限源的 cylinder/plane 精度事件保留 occurrence、generation、witness；只有声明的 cut 端、原顶点、有限参数包围和独立支撑关系共同证明时才接续端点；已隔离的不同内部根不参加这项归并 |
| 输入边接续 | 生成的直线片段仅在两端事件相同、完整仿射误差界在原生 precision 内且源唯一时复用输入片段；不合并不同原边 occurrence，不以两个近 XYZ 样本代替完整直线证明 |
| 有限 UV 射线 | 非周期 Blend 直接消费共同求解的 native u/v；周期 phase 使用原生 conic centre。圆柱采用实际 native frame 的对偶基，轴向射线不能决定时保持原查询点并认证斜向参数射线；两种情况均复用原标量根隔离及原预算 |
| 塌缩端的参数闭合 | 两条原接触、整个中间 UV 条带及 native 端点共同给出线性精度界，再闭合已有参数图；没有参数 epsilon 或坐标白名单 |
| 薄片接续 | 从原顶点实际相邻的支撑柱面取得接触法向，并用 native 角精度区分切向与横截；投影角点解全部三个平面方程，不要求变换后的点精确满足前两式 |
| 投影的连续证明 | 在 native UV 上对线性平面函数保留导数相关性，取直接界与均值界的交集；固定可逆行变换消除墙方程耦合，所有 homotopy 参数、完整矩形、四面符号、投影单射/方向及边界排除仍须通过 |
| 有限刀具边界 | 世界 AABB 仍做快筛；重叠时可由原生方向的完整线性范围证明与整条边界直线分离，保留原 precision cube。未证明的相交/预算耗尽仍拒绝，不将 AABB 重叠当真实侵入 |

上述精度解释来自官方 FD 17.2/17.2.2/17.3 和 XT CHART 定义；源顶点、支撑及参数
关系来自实际 native 数据与源码；GM 算法改变来自固定 diff。PK 同模型运行证明该
适用族的最终行为对齐，不能据此声称已观测 PK 内部采用相同的行变换或区间算法。
optional `intersection_data` 的 UV 必须匹配 hvecs；其存在不授权用未经认证的 UV
近似替代最终几何证明。

镜像、尺度 1.7、逆刀具顺序的联合场景此前为 raw 134（先 157/5018，后来虽发布但
严格比较为 PK 18/47/31、GM 19/49/32）。这些原退出码与原文件保留。
`temp_docs/native-system-alignment/native-projected-tool-functional-v1.log/.exit` 现在 raw 2：
独立 PK/GM Fix、独立同选项双工具 Boolean、GM fresh XT fullcheck/strict compare、
有限 ICurve/UV、原生有限 face/fin、48 个材料探针和 6 个投影材料探针通过；3 项
物性区间仍为 Unresolved。真实程序集 fe0f5699ccbb87690bbb1eba7d85b905463dbfc2a1f4326cda17ff641de59d42
与 PDB 已原字节冻结在 `temp_docs/native-system-alignment/native-projected-tool-functional-v1-managed-production/`，
生产源为 `3ac81cfc` 加 `temp_docs/native-system-alignment/native-projected-tool-functional-v1-source.diff`；这是开发
源码快照，不冒充干净提交或独立接受。

临时诊断移除后，`native-parameter-clean-v1.log/.exit` 重复上述真实端到端验收，
raw 2、仍为 3 项物性 U，真实程序集为
048613d7a887f32caa56d31757b955c774746ac5f0322a7b9a1363a6ae130ceb。
该二进制/PDB、source head/diff 已另存于同目录同名前缀，未覆盖前次数据。
`native-parameter-clean-managed-v1.log/.exit` 332/332 raw 0，包含原始根/事件负例、
滚动网络、分配/回滚及新增 Native 参数/投影证明。此前新投影测试缺 using 的编译
退出 1 单独保留，不记成建模失败；修正后 `native-projection-parameter-math-v2` 28/28 raw 0。

这次实现是计算主线的一次共同机制进展，尚非原总体目标的完整接受。仍需在稳定
提交上集中完成跨族、尺度/顺序、copy/delete/transform/reload 再建模、原始 510 分源
查询、完整托管回归及同源关键消费者验收；周期 Blend phase 当前只有数学边界检查，
不能继承本次非周期 Blend/真实圆柱消费者的 PK 资格。一般曲面物性 U、高价/非正则
事件和原计划其他未执行类别继续按既有章节登记，不因本次局部几何拓扑通过而消失。
原510/588U、统一固定包、完整最终回归、周期分支、Mac及集中独立接受仍须按实际
新源执行；本节不替随后source或未执行范围取得资格。

### 固定源横向消费者与 imprint 来源修正

固定 `1dae797aa13163c8abafa8f863e0d5d6591f9fd2` 的完整托管运行
`temp_docs/native-system-alignment/1dae797-full-managed.log/.exit` 为 raw 1：
2139/2141 通过，两项 unite 同域有限 overlay 在原生顶点精度计数处失败
（直接及 XT 回读各一项，预期四个、实际两个）。原失败未被后来的 checker 或
几何通过覆盖。根因是新 regular cut 端点接续同时接纳了 coincident-area imprint；
后者携带跨体合并与 precision 来源，不能作为普通 cut end 复用某个输入的身份。
现在两项接续入口都按实际 `KeepImprint` 角色排除这类事件，继续使用已有同域
overlay 来源与发布机制；没有按 Boolean 编号、坐标或 fixture 分派。

修正开发源（`1dae797a` 加该固定 diff）实际 Native assembly 为
6023141eb24473e7a8530849e083693d2521e36b6e5874eb1e8de77d7c154172。
`native-imprint-provenance-managed-v1.log/.exit` 38/38 raw 0，包含上述六种有限
overlay、同域 tracking 与 rolling network。`native-imprint-provenance-pk-v1.log/.exit`
六项真实独立 PK/GM 三操作及回读输入 fullcheck/strict compare、125 点材料分类、
tracking owner/class 与有限来源正例/错误来源负例通过，raw 2 仅保留六次物性 U。
逐侧来源关系数为 unite/intersect 44、subtract 12；有限 witness 明确不是连续证明。
`native-parameter-imprint-eligibility-v1.log/.exit` 镜像、尺度 1.7、逆工具顺序的
独立 Fix/Boolean/投影材料控制也通过，raw 2、三次物性 U。完整回归仍需新源重跑。

此前固定 `1dae797a` 的生命周期与跨平台结果仍单独绑定原源：

- `1dae797-lifecycle-remodel.log/.exit` raw 2：plane-nurbs、nurbs-nurbs、
  concave-nurbs-nurbs，各三尺度 .125/1.7/8 的 copy/delete/mirror/scale、XT
  可编辑回读再建模，共九个生命周期及三组独立 Fix/Boolean 通过；物性另记 U。
  q2 Native assembly 为 74e673d5d403d316a18132c4f7f513da22c94531e7e25faaf1dc8e38990e7fec。
- `1dae797-native-ownership.log/.exit` raw 0 为公共几何 contracts-only：free/owned
  attachment、负例保持目标不变、PART_ask_geoms、嵌套定义、copy/delete/cascade。
  它没有执行 XT writer 子进程，不称为所有 FaceAttach 类别的完整覆盖。
- `1dae797-mac-environment.log/.exit` raw 0 后，Mac arm64 同固定源镜像尺度逆序
  控制 `1dae797-mac-native-parameter.log/.exit` raw 2：独立 Fix/Boolean、fresh XT
  fullcheck/strict、48 材料与六投影探针通过，三次物性 U。真实 Mac assembly 为
  6436530f78c8bf03fa0d14f186a65d3061f93b05697a24b757caa59c24fd5af2。
  该结果不替尚未运行的后继源取得跨平台资格。

这些是 owner 实际运行，尚非独立接受或完整最终交付。

公共几何与 XT writer 的新执行均要求新的 `--output-name`，父子进程使用同一
执行名称和 Release runtime policy，各自输出真实 Native assembly SHA。
原固定目录、原报告及原 XT 不覆盖；旧无名称调用直接拒绝，不维护兼容路径。
`native-imprint-source-geometry-v1.log/.exit` raw 0 已实际完成公共合同及七类
ellipse/TRCurve/SPCurve/BSurface/swept/spun/offset 的 attach、GM transmit、
真实 PK receive/compare 与参数求值。完整产物分别在
`temp_docs/geometry-public-api/native-imprint-source-geometry-v1/` 和
`temp_docs/xt-geometry-writer-oracle/native-imprint-source-geometry-v1/`。
wire 比较器本身不证明有限曲线连续等价；此运行不继承为其他未测 attach 类别资格。

### 9f2a93e 稳定检查点

`9f2a93ebb381d6d85a59188df56fdc091cea7b8e` 已实际完成
`9f2a93e-full-managed.log/.exit`：2141/2141 raw 0。该运行结束后原 testhost 已退出。
q2 Release assembly 6323c0057baf606e9a2a4c8fb48fedd1b076f0c70b7ee67f7b3cf82e1695c0ba
已冻结于 `temp_docs/native-system-alignment/9f2a93e-managed-production/`。
同字节实际执行的 self/import 原510分别为 `9f2a93e-self-510` 与
`9f2a93e-import-510`，均 raw 2：15/16 U、零新增差异、零原生基线变化；
原生自回读七行及 Capture8 的分源边界保持原事实，没有四方强制相等。
Mac 新固定树的 `9f2a93e-mac-native-parameter` raw 2，通过镜像 1.7、逆刀具顺序
的独立 Fix/Boolean、fresh strict/full、有限域及投影材料检查，三项物性 U；
真实 assembly 0c03200a01f368fd03ad250fe98a2f7e334a336995dae23830bff259da9692c9。
第一次 Mac 环境准备因受限 PATH 找不到 sha256sum 而 raw 3，不是模型错误；
同一已有工具补全进程 PATH 后 `9f2a93e-mac-environment-v2` 实际 ENV_READY、raw 0。
上述稳定点不替其后新增 tracking 实现取得完整回归资格。

## 内部刀具 union 的来源组合与新边追踪

重新执行原计划的 tracking 缺口后，普通曲面 basic tracking 已有横向资格，但
rolling 目标的多工具路径仍在 tracking=true 时跳过有限刀具 union。真实 PK 同参数
返回 16 条记录，GM 的 `native-multitool-tracking-baseline-v2` 在第三面事件 raw 134、
API 157。这个差异不能用普通 Roof 的 tracking 通过代替。

现在内部 union 把实际 planar arrangement 构建的每个 face fragment 直接绑定到
原 face；保留 face handle 的交换同步更新这些临时关系。union 边由实际 incident
face 来源组合；同体两面之间只在原拓扑邻接、唯一原边及完整有限 Line 对应界同时
通过时绑定原 edge。BCurve 等尚未具有这个完整原边证明的类别不冒充已支持。
元数据仅在当前命令工作区，未写入几何定义或 XT。union 完成后只保留实际来源行，
释放未使用容量，再运行原曲面求解器，不增加几何预算。

最终发布沿原 finite fragment/canonical edge 到真实 built edge 的关系组合来源，
保留内部 union 产生、随后仍存活或被分割的跨工具边。投影接续创建的新有限边在
创建时登记实际两侧保留 face 的来源；不能从清理前图中猜到一条尚未存在的边。
基础追踪只把跨原始输入 owner 的 derive 关系发布，原有限 edge 优先于同体面，
原 class/tag 在删源前捕获。返回仍为单块原生返回内存，外层 command journal
负责整个 union、裁剪、身份交换和发布的失败恢复。

官方 FD 10.2.3/10.2.7 定义 tag persistence 与低层 split/merge/change 的来源语义；
MCP 静态 `BOO_start_tracking → BOO_set_entitylnk_attr` 继续到实际临时属性写入。
它支持“操作内来源关系”的解释，不证明 GM 使用 PK 相同的数据结构。此前有界
GDB 的完整命中范围见第一项机制；这次新证据为同模型动态操作和真实返回记录，
不是新的全链 GDB 证明。

开发源 `9f2a93e` 加固定 diff，真实 assembly
9c22da004b3ca0d910c3fc59494fccc98ba4d76802d34efd1341ea7b66c5f0f5，
原字节、source head/diff 冻结于 `temp_docs/native-system-alignment/native-tool-union-origins-v1-*`。
`native-tool-union-origins-managed-v1` 99/99 raw 0、allocation IL guard CLEAN；
`native-tool-union-publish-rollback-v1` 四种真实失败注入 raw 0，覆盖追踪开/关、
投影创建后及身份交换后预算 0/1，全部恢复原目标、两工具、generation、XT、池和返回内存。
这不是四次完整回归，亦未单独观测其中哪一次恰好失败在组合返回块分配。

`native-tool-union-finite-tracking-v2` 和 `native-tool-union-finite-tracking-mirror-v1`
均 raw 2：默认及镜像 1.7/逆工具顺序，两种不同的正则三参与事件均完成独立
PK/GM Fix 与同选项 Boolean、fresh fullcheck/strict、有限 ICurve/face/fin、材料探针。
两种 Boolean 的记录数各为双方 17/16，来源 owner/class 多重集相等；各侧通过
34/32 个实际来源关系的有限重叠采样及同 owner/class 错误来源负例。来源绑定来自
每侧实际 transmit identifier/class 与同字节 PK 回读，不是固定 tag 或世界中点猜测。
采样不作为连续等价证明；四项物性 U 单列。

新来源检查曾额外要求“产品完全包含在旧有限来源面中”。默认投影边使 GM 最大
采样距离约 8.2738718e-8，该新增假设 raw 134；对真实 PK 自己返回的记录应用
同一要求也 raw 134、约 8.2738667e-8（`native-tool-union-tracking-native-containment-v1`）。
FD 的 derive 不是旧 trim 的子集合同，故最终检查要求实际有限重叠 witness，
并同时保留整个采样最大距离诊断。实体几何/拓扑 strict/full 门未变；这不是把旧
几何失败改名通过。过大来源容量的候选、15/16 漏新边的候选及脚本启动 typo、
缺 using 的构建退出也分别保留，不由后一次成功补签。

同一开发程序集的 `native-tool-union-finite-tracking-small-v1` 又实际通过刚体变换、
尺度 .125、反选边顺序的两种联合 Boolean，记录仍为双方 17/16、有限来源关系
34/32，strict/full/有限域/材料均通过，raw 2、四项物性 U。它与默认及镜像 1.7
分别保存，不把其中一个默认变体作为全部适用性的证明。后继固定提交的完整回归、
统一制品与独立接受仍在执行；complete/region tracking、非 Line 原边证明、周期
Blend phase 的真实 PK 消费者以及原总体未决类别未因此取得资格。

## 原生主 fin 的内存发布合同

固定 `553253c6` 的 Release 全量为 2144/2144、raw 0；其原生 SP 生命周期
24 族、三尺度、参数/导数/有限 wire 对照 raw 0。Mac 同固定源的镜像 1.7、
逆工具顺序及两种 tracking Boolean 也完成 strict/full、有限来源重叠负例，
raw 2、四项物性 U。四族、各两个新模型的生产 whole-operation 实测共 32 条
API 行，八组独立 Fix/Boolean 几何拓扑通过、raw 2；非解析重叠求解的秒级
成本仍远高于 PK 毫秒级，不能称已达性能目标。这些结果只绑定固定 553 源。

同源扩大到直接构造的 local-operation 生命周期后，17 项中有一项四条平行
边倒圆返回 5295。新执行先运行独立 PK，同参数实际返回四个 Blend，随后 GM
失败于材料选择；原脚本先 GM 后 PK 的失败行没有执行 PK，不补签该旧行。
相同 GM 输入经自身 XT 回读后却通过。自然 Line 的 TMin/TMax 差异曾作为
候选根因，实际改成相同自然域仍失败，已撤回该候选；逆选边顺序也未修复。

决定性区别是直接构造第一条边的 `FirstFinEdge` 为负向，回读后为正向。
官方 FD 34 checking 的 `PK_EDGE_state_fin_not_pos_c` 明确要求第一 fin 正向；
XT EDGE.halfedge 保存此主 fin。既有 writer 在导出时主动选正向 fin，因而
掩盖了内存构造违反同一原生合同的问题。这不是坐标或输入 family 的定义。

新实现只在尚未分配 tag 的新边发布前，沿既有环找到正向 fin，将首尾指针
旋转到该处；不改环次序、sense、曲线参数或已发布实体，也不分配托管对象。
没有正向使用的单用 sheet/dummy-fin 表示不借此次修复宣称闭环。
新增 direct/reload 与正反选择顺序四种网络测试，同时检查倒圆后与 copy/delete
的正向主 fin 及池回收；受影响托管集 86/86、raw 0，allocation guard CLEAN。

`native-primary-fin-publication-v1` raw 0 实际通过原失败四边倒圆、fresh XT
strict/full、copy/delete、镜像尺度、GM XT 回读及下游 Boolean。
`native-primary-fin-local-lifecycle-v1` raw 2：17/17 独立原生操作与生命周期、
再 Boolean 几何拓扑通过，四项物性 U 分列。原 553 的失败 raw 1、两个未生效
假设及新结果分别保留，不用新通过覆盖旧退出码。

`native-primary-fin-full-managed-v1` 完整 Release 回归 2148/2148、raw 0，
allocation guard 3154 方法 CLEAN，所属 testhost 已退出。
`native-primary-fin-multitool-v1` raw 2 再通过镜像 1.7、逆工具顺序的两种
多工具 Boolean、有限 face/fin/ICurve、材料及来源错误负例，各侧记录 17/16、
来源关系 34/32；四项物性 U 保持原分类。此生产 Native assembly 为
985da52a6294bc76d5a0e5718036421dc611072a2ab94e12960a797d319d0dd8。

闭合显式 Blend 的实际新探针 `native-periodic-blend-applicability-v4` raw 1
还揭示另一个独立能力缺口：同圆柱/斜平面、同范围与两点，PK 返回 BLENDSF，
原生脊线 Ellipse、自然范围 0..2π，GM 返回 5000/未实现。此探针没有建有限
sheet 或进行 body compare，不能给既有 periodic phase 数学测试补消费者资格。

## 闭合解析脊线进入原生 Blend、有限裁剪及回读链

本节是上述 applicability probe 之后的实际实现。官方 FD 18.2.2/18.3.2.6
把 Blend 定义为两个支持、带符号的等幅半径及原生 spine/extent，U 沿原曲线、
V 在接触截弧上。MCP 静态链从 `CNS_blended_edge` 的 offset、`general_case`
继续到 `ISS_intersect_between_pvecs`、`ISS__seek_intersections` 的解析类别分派，
`ISS__plane_cylinder` 下含实际 ellipse 构建。Linux PK 38.0.150 的有界 GDB
`native-periodic-blend-gdb-v3` 实际命中这条 plane/cylinder 分支及
`MAK_blended_edge`；没有命中本次设置的 curvature-trim/sloping-cylinder
断点，不声称这些分支已动态覆盖。debugger raw 0 与各次 inferior 自身退出分列。

GM 已有 offset SSI 的精确 Ellipse，只是公开 Blend 创建入口仅接受 Line/Circle，
随后拒绝 Ellipse。现在直接将同一原生 Ellipse 绑定到既有 Blend compiler；两侧
offset 的完整系数认证继续执行，不重新采样/拟合一条 ICurve。新入口还要求半径
严格小于椭圆完整法管的最小曲率半径 `b²/a` 的向外区间下界，未证明或可能奇异
的整管保持显式拒绝。此充分条件是 GM 的已证明适用边界，不冒称 PK 的全部范围。

spine 的原生 sense 按有序接触径向量的叉积确定，包含两侧 offset 的符号。
新 mixed-sign 对照最初 strictcompare 失败；同原生 U 的五个截弧采样实际表明
V 顺序反向，而非可忽略的 chart 数量差异。补上半径符号后，GM 的原生定义、
求值与 XT 使用同一方向；没有改比较器去白名单这些错误。

实际有限 sheet 消费者又暴露两个共用缺口。其一，旧 `TryAllocateSurfaces`
给自由圆柱留下 `OwnerFace=0`，当 body 的第一个 face 正好占槽 0，writer 会
把本应仅作定义依赖的圆柱误写成该 face 的支持。单几何求值正确不证明拥有关系。
`native-periodic-body-check-gdb-v2` 在真实 PK `TCH__body → num_faces_of_surface`
处命中错误；原生参考 body 的 `SOL/get_tol` 则只是另一条诊断，未混作 GM 错误。
分配入口现明确初始化“无 face owner”，没有在导出阶段修补或另造所有权。

其二，SPCurve 的有限 XT edge 恢复要调用原曲线反求，原实现只支持 BSurface
上的 SPCurve。Blend 上精确原生 UV 定义 `(u=t,v=constant)` 因而丢失有限参数，
回读仍可传输却无法再建模。现在复用父 Blend 的原生反求，并通过原 SP 曲线 D0
在原生线性精度内验收；不把父面反求本身当成曲线上点证明。任意非等参 UV 曲线、
多周期覆盖及跨接缝有限域还没有因此取得资格。

扩展既有 `CurvedSheetPartitionOracle`，让两侧独立执行同参数 API 创建，再分别
make-sheet、imprint、trim keep/remove、section front/back/both、第二平面及重复
操作。每步 GM fresh XT 由真实 PK fullcheck，标准 strict helper 仅将 chart
数量作默认诊断。复制删源、镜像尺度以及各侧自行 XT 回读后再操作另列；GM receive
还明确要求可编辑，不能靠 opaque 重传取得生命周期通过。新追踪仅报告实际接受的
周期 winding 根，携带原 curve/surface generation、有限参数及原生 UV；它不改求解。

开发源为 `4dca5f9a` 加本轮固定 diff。生产 Native assembly
a946cafda903f64a6529605e6dbb11ee3f3f5403e1c6e32c673154737778cd8d 的实际结果：

- `native-periodic-integrated-sheet-suite-v1` raw 0：旧解析/非解析、rational、
  多 span、闭环及新周期 Blend 共 88 项全部完成 fullcheck/strict，实际接受
  87 个周期 winding 根，未仅凭新默认模型判框架完成。
- `native-periodic-blend-mixed-large-v3` raw 0：混合符号、镜像尺度 8、复制删源、
  可编辑 XT 回读后，九种操作通过，实际周期 winding 根 96。
- 更早同一机制的镜像 1.7/逆支持次序九项，以及内偏置/镜像尺度 .125 九项各自
  raw 0，日志分别为 `native-periodic-blend-phase-mirror-v1` 和
  `native-periodic-blend-inward-small-v1`；它们绑定各自实际程序集，不替后继固定
  字节补签。前者实际周期根 114，后者早于新增分支追踪。

`native-periodic-definition-managed-v2` 29/29 raw 0 包含复制定义、整周期接触、
有限 SP 回读反求和离曲线负例及分配失败回滚。后继
`native-periodic-integrated-full-managed-v1` 已实际完成当前混合符号扩展后的完整
Release 回归 2159/2159、raw 0，allocation guard CLEAN，所属 testhost 已退出。
固定包和跨平台关键消费者仍需集中完成；上述 sheet 新能力不等于任意闭合 seam solid、任意高价事件、
complete/region tracking 或 PK 性能目标全部完成。全部失败开发执行和脚本构建
错误的 raw exit 独立保留，未被 checker 成功或后次通过覆盖。

## 在研：原生双支撑 UV 消元（整体任务继续）

两个不同的非周期 C1 BSurface offset 支撑，其 rolling 中心严格等于
`S0(u0,v0)+h0*n0`。普通 D0/D1 包围现在直接解四个原生 UV 未知量：
两 offset 点的三维相等，加原 CHART station 方程；不再把由第一个支撑
完全定义的 XYZ 作为另外三个未知量。此机制不修改原生参数、持久 XT、
半径/normal sense、CHART 定义或支持求值器，不包含已拒候选的 scalar
rolling 公式、分支缓存或 jet 重写。Plane 混合、嵌套 Blend、显式 spine、
需要共享 witness/曲率/reach/支撑 UV 的请求仍使用原有认证系统。

四维系统使用原残差精度、Newton/线搜索上限和六次扩张/收缩；保留严格
Krawczyk 包含、收缩率 <0.9 和原 nearest-foot Hessian 证明。通过后将
原生 UV 及其导数严格提升回原七维状态，供既有 Frame/Tube 消费。
CHART secant 的偏离仅用于初始盒宽提示，不能当曲线误差界；不能收缩
时仍显式 NotConverged。相同几何但不同寻根状态不增加持久实体。

实际执行保存在 `temp_docs/native-system-alignment/`：

- `native-uv-elimination-math-v1.log/.exit` 原始 1：两个驻点曲率例未收缩；
  修正初始盒提示后的 `native-uv-elimination-math-v2` 24/24、raw 0，含
  镜像/尺度、支撑顺序、已知 D0/D1 包围及重复支撑不允许唯一管证书。
- `native-uv-elimination-full-managed-v1` 全套 2164/2164、raw 0；分配
  IL guard 3160 方法 CLEAN。此轮执行源尚未加入后续 plane residual 试验。
- `native-uv-elimination-whole-v3` 生产 JIT 政策、四族各两次 fresh，真实
  PK 各自独立 Fix 与 Boolean、GM fresh XT fullcheck/strict 均通过；raw 2
  保留物性 U。八组 GM API 总和 12306.791 ms，固定 de9 对照 30180.353 ms；
  仍远慢于 PK，不作性能达标声明。凹双曲面 warm Fix/Boolean 分别
  959.266/1535.269 ms，根单元从 de9 的 1438/3282 降到 270/914，最终
  根数保持 10/38。原 v1 Plane 降维较慢，已撤回该分支，v2/v3只处理双 BSurface。
- 新增有理支撑、反向 U 和 `[-2,3]` 原生域的跨族消费者还在执行。
  `native-uv-elimination-rational-gauge-v1` raw 134 保留：接触探针旧
  `x=u` 假设已识别并修正，plane/nurbs 单独倒圆由后续
  `native-uv-rational-contact-v2` 实际通过；变换/回读后下游 Boolean
  仍有真实 SSI/有限面消费者缺口，不能因上游通过而反签该下游。

这是算法结构与成本的实改，仍非 M0–M5、剩余分类或完整性能目标完成。
没有新增独立 review worker；整体稳定交付再集中接受。

## 在研：有理支撑的求交误差与原生接触端点传递

新执行把有理权重、反向 U、非单位 native knot、镜像旋转、三尺度、复制删源及
editable XT 回读后的 Boolean 连起来；不是仅在新四维系统内自验。

1. 原齐次 Bezier net 先组合平面线性泛函，再作有理投影，得到同一曲面的
   outward residual 包围。只在原 XYZ 点包围残差不能满足请求时与其取交；
   空交显式失败。没有改变 `2e-14` 点残差门限、root budget 或几何容差。
   原先平移后约 `2e-13` 的独立 XYZ 区间宽度不能据此认定模型错误。
   这不是已拒 NativeScalarLevel 的 Blend/V 公式或 root-solver 改写。
2. 接触线/平面已在 Blend 原生 U 隔离出唯一根，但映射回 ICurve chord gauge
   后原 U 根盒、单调域被丢弃，后续退回 XYZ 反求。现在在操作私有
   `RegularBoundaryRoot` 保留该原始证据，失败的 provider 清空其接触证据。
   同一个 Blend、同切割平面、同固定 native V 且在同一单调根域的有限
   provider 端点，必须把根到存储端点的完整 native U 条带及两侧实际空间
   包围全部放进 `1e-8` 线性精度邻域，才关联到该有限端点。它是精度合同内
   的有限代表，不声称该代表恰是平面数学零点；不按参数 epsilon、坐标或
   单点距离合并。新增 trace 记录原 U 根盒、V、有限 station、完整直径和 witness。
3. 多个 support pair 提供同一 canonical cut occurrence 时只插入一次有向对，
   保留 KeepImprint 标记。环提取前仅剥离具有反向成对用途的悬挂树：其边界
   1-chain 为零，不能围成面域。已有闭环、没有反向用途的源边不被剥离，
   也不因此删除模型定义中的实体。原来提环后的零链规则保持相同含义。

以上都是 GM 实现结论。官方 XT/FD 对原生参数、有限拓扑及 precision 的定义是
前提；此前同平台 PK CNS/ISS 动态链仍是实际所选分支证据。新静态查询的
`PK_SURF_parameterise_vector` 经 COI/QSU；QSU 的空 callgraph 在实际反汇编
中有 tail jump 和间接 switch，不能解释为没有调用。PK 是否采用本轮四未知量
消元及如何在内部关联这些有限端点仍未知，不作已复刻 PK 内部算法的声明。

执行日志仍在 `temp_docs/native-system-alignment/`，原字节及退出码保留：

- `native-uv-plane-composition-managed-v1` 27/27 raw 0；
  `native-uv-plane-spur-managed-v1` 为测试命名空间编译错 raw 1，修正后 v2
  48/48 raw 0，不计为建模失败。
- `native-uv-plane-spur-full-managed-v1` 2171/2171 raw 0；端点证据集成后的
  `native-uv-contact-integrated-full-managed-v1` 2172/2172 raw 0，IL guard
  3166 CLEAN。其后仅增加 failed-provider 清空保护及相应负例；
  `native-contact-end-negative-managed-v4` 16/16 raw 0，包含错误 UV 根、
  无认证、远离实际空间邻域及裁剪后失败 provider 不可遗留证据。此前 v1
  是测试调用签名编译错，v2 是 owned/free geometry 不同 body 的 API 拒绝，
  v3 正确复制为 free geometry 后 8/8 raw 0，均未反签失败操作。
- `native-uv-contact-final-rational-v1` 是脚本 fixed-buffer unsafe context
  编译错；v2 实际执行三族 Fix、各自 Boolean、九个 lifecycle/remodel：
  有理权重、U 反向、native U `[-2,3]`、尺度 `.125/1.7/8`，全部 GM fresh XT
  receive/fullcheck/strict、材料及上下游操作分别通过，raw 2 保留物性 U。
- `native-uv-contact-final-sheet-v1` 全部 88/88 raw 0，实际 periodic winding
  87；不是给尚未执行的曲面或事件类别授予资格。
- 原 `native-uv-rational-*` 定位执行中的 raw 134/157/5018/5295 保留。中间
  spurs 集成只通过五个 lifecycle，两个后续失败不被删除；保留原生 U
  根证据后凸双 NURBS 的第六项通过；用完整 native 条带精度证明后凹支撑
  三尺度也通过。最后以统一 v2 执行重新确认九项，而非拼接中间绿灯。
- 扩大验证的 `native-uv-contact-native-gauge-v1`：有理正向 U、native 域
  `[-.125,.375]`、切割 `x=.43`、生命周期尺度1.7，仍暴露凸双 NURBS 回读
  Boolean 和凹双 NURBS Fix 终止失败。PK 同输入操作已执行；这些新缺口
  继续由本 owner 处理，不把前述九项通过当通用能力或整体目标完成。

新的 whole-operation 性能与固定包尚待当前主线稳定集成；旧 de9 包保持原字节。
