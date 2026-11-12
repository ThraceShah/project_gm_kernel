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
