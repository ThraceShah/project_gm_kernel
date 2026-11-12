# PK 原生处理链与 GM 对齐

本轮起点 `f7c110934679b47b4b3220f8e13d8831fabe8151`，工作分支
`feat/pk-native-system-alignment`。起点工作区干净；PKToy 固定 `d820719`，
私有环境准备实际返回 ENV_READY（Linux x64、PK 38.0、schema/corpus 就绪）。
此文件持续记录实现与实际验证；不是 M0–M5 已完成声明。

## 真实处理链与关键差异

| 阶段 | PK 依据及证据层级 | 当前 GM 实现与本轮动作 |
| --- | --- | --- |
| 模型定义、准备 | 官方 FD 15/17/18、XT model/schema：原生拓扑、有限 geometry、ownership、precision 是模型定义 | 沿用 Runtime/XT，不创建替代模型；核查有限边和顶点精度在构建及复制中是否保全 |
| 候选、求交、有限裁剪 | 既有同平台动态证据：Fix 可从终点间追踪；Boolean 可从输入面的有限参数盒界追踪，随后再裁剪；见原 acceptance 的 search-box 控制 | 复用已实现 finite native intervals、paired CHART、标量/向量隔离、共同事件和 SheetArrangement；候选近似不代替最终有限域验收 |
| Blend 支撑、分支、终止 | 静态查询实际命中 fix_blends → BLE_for_all_blended_edges；其下 r12/rax 是间接调用，当前不能由此声称已理解完整 rolling solver | 原解析网络、常半径 regular、三价 corner 分支继续保留其适用域；本轮不以重写 Engine 代替缺失能力 |
| 事件、材料 | QTP_point_in_body 静态链含 region/shell、有限 verts/edges、ordered faces 和 point_in_face_2；后者按名未能解析，仍为未知 | 当前 generation/finite carrier/event 关联已经供 arrangement/classification 消费；继续按真实实体精度与有限归属处理，不把 510 坐标当算法 key |
| 有向 B-rep 发布 | 官方 FD 10 的 tag persistence：split 保留一个来源 tag，shrink 保留 tag，merge 保留一个来源 tag | BuildRegularBooleanProducts 已有 source face → fragment → built face 映射和 face tag 保留，但还直接拒绝 tracking；应消费实际生成关联，而不是从最终 XYZ 重新定位来源 |
| 修改追踪、回滚 | 静态链：boolean → BOO_start_tracking（临时 entity-link）→ BOO_function_2（tag persistence/tracking）→ BOO_end_tracking → PKU_process_boolean_tracking（读取关联、填返回、移除临时属性）；这是静态依赖证据，动态分支待实际验证 | 本轮优先闭合曲面 Boolean 的原生修改来源与发布生命周期，保持 command journal 原子回滚；不把追踪数据变成持久几何定义 |
| copy/delete/transform/reload | 官方 ownership 与 tag 合同；原 acceptance 已有实际消费者证据，但不能覆盖所有 provider | 新增机制用独立 PK 同操作与 fresh GM XT 验证；继续执行删源、变换、editable XT 回读再建模，检查精度和依赖不丢失 |

当前决定性差异是：原生参数/来源已经在计算中获得，GM 发布接口却尚未完整传递
这些信息，曲面 tracking 被整条拒绝；部分公共构建路径还需核查是否丢失实体精度。
本轮从这些现有共享数据流直接实现，不建立新的调度平台，不碰 Mac 当前数值优化文件。
后续动态行为若否定某项推断，会据证据修正本表。

## 原计划与未决类别

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
- Mac 已接受性能固定点仅供只读对照，本树尚未合流；moving 候选不取得验收资格。

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
