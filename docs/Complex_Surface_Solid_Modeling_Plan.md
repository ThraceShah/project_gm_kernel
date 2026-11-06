# 下一阶段：复杂曲面实体建模

基线：`1b071bb`。主线目标是把已有 Blend、SSI 和有限域 B-rep 能力接成
“自由曲面实体选边圆角 → 含 Blend 的实体布尔 → 新生边再次圆角”。
物性、包围盒、完整体检查和显示继续使用真实 Parasolid，不作为公开查询 API
补齐任务捆绑进主线。验证使用托管内核；没有 C ABI 变化时不运行 NativeAOT。

## 阶段与退出条件

| 阶段 | 实现范围 | 必须通过的验证 |
| --- | --- | --- |
| M1 规则单边实体圆角 | 常半径、Plane/NURBS 及 NURBS/NURBS、明确的简单端部；先完成一个闭合实体，再扩大凸凹支持域 | 相同输入、边和半径分别执行 self/PK；self fresh text XT 经 PK receive；完整 BODY_check；拓扑、region/shell、类型/sense/容差/missing 比较；半径、接触、切向连续；结果继续切削 |
| M2 混合曲面实体布尔 | 有限解析域、Blend/NURBS、Blend/Blend、已有 ICurve 再分割；使用 M1 实际输出 | 并交差、内部闭环、多分支、空腔、多工具、两次连续操作；斜圆柱切孔；新生边圆角 |
| M3 求交事件 | 按 M1/M2 的具体阻断推进近切、seam/pole、重合区域、精确相切与分叉、必要延续 | 制造解和独立 PK 对照；完整分支、双 UV、端点、闭合性；预算耗尽或未解决单元明确失败 |
| M4 边链与角点 | 光顺边链、支撑切换、等半径三边角点、相邻 Blend、限定消退事件、Blend 上再次圆角 | 每类独立案例，继续 Boolean/再次圆角，失败定位到支撑、分支或角点 |
| M5 组合案例和性能 | 记录 SSI、约束根、候选面、边界再分割、分类的耗时及预算；定向优化 | 选定复杂案例完成整条能力链，保留可复现的支持域和未支持事件 |

## 第一轮执行

状态：M1 已完成下述规则单边支持矩阵，XT 有限边区间短修已合入。
五组实体圆角以及各自后续切削均通过独立真实 PK 对照；生命周期另覆盖
XT 重载、复制、删除源体、镜像和缩放。验收记录见
`docs/reviews/advanced-modeling/freeform-solid-fillets-2026-11-06.md`，
实现合同见 `docs/Regular_Freeform_Solid_Fillets.md`。

M2 也已完成其有限、规则横截验收域：实际 M1 Blend 实体的并交差、
Blend/NURBS 与 Blend/Blend 有限交段、内部闭环、空腔、多工具、连续切削、
有限斜圆柱贯孔，以及布尔新生曲边再次圆角。23 个混合案例和 18 个有限解析
专项案例独立 PK 对照通过；最终集成内核 1,452 项和 XT 12 项全部通过。
精确边界、迭代验收来源和性能限制见 `docs/Mixed_Curved_Boolean_Operations.md`
及 `docs/reviews/advanced-modeling/mixed-curved-modeling-2026-11-06.md`。

1. 主工作树接通 `set_blend → fix_blends`：选择滚动符号与分支，生成真实接触曲线，
   裁剪支撑面和端面，装配共享 edge/fin/loop，保留原 body 与幸存 face 句柄。
   首个可验收支持域为一个规则独立边、两个简单平面终止面，无角点覆盖和自重叠。
   以非平面 NURBS 输入作为正向回归，超范围输入必须回滚。
2. 用实际圆角体进行切削；出现公共裁剪/分类阻断时修复共享机制，不新增按模型名识别的专用路径。
3. 独立短期 worktree 修复 XT 有限边参数丢失：恢复实际裁剪区间，验证重载后点分类和再次切削。
4. 运行针对性回归、完整托管测试、真实 PK oracle、接口目录及核心分配检查。
   保存日志与 fresh XT 于 `temp_docs/`，把支持域和实际结果写入 `docs/`，验证通过后提交推送。

## 并行和共享合同

主工作树负责圆角与集成；短期 XT 修复使用独立 worktree。求交与 Boolean 只有出现
具体独立阻断时才拆出额外 worktree；外围查询不阻塞主线。
有限参数域与方向沿用 `ModelingEdge`/`ModelingCoedge`，真实交线沿用 ICurve 双 UV
和已有 Blend 支撑依赖。公共构造器由集成主线负责，子任务避免并发改动。

## M3、M4、M5 的共同实现

这三项共用 `docs/Intersection_Local_Modeling_Architecture.md` 的有限域、事件、
方向、所有权和原子发布合同。生产代码没有按测试名称、固定坐标或实体 tag
选择分支。算法先认证几何关系，再由真实邻接图选择局部拓扑计划。

- M3：同域支持的真实有限边界覆盖、外接孤立点的顶点环、跨原生 seam/pole 的
  有限球面域，以及带有双 UV 的规则近切闭环。区域重合、孤立点、已证明无交
  和未解析事件分别处理；预算不足不返回部分成功。
- M4：同载体分段光顺链、真实支持面分区切换、三价等半径凸角点、限定的一至
  三条 band 消退、Sphere/Blend 切削及已有 Blend 支持上的再次实体圆角。
  角点先求共同滚动球，再共同装配；不以顺序单边圆角代替角点求解。
- M5：完整操作的阶段/求根/绕数计数、只读阶段的命令局部认证界缓存、单次
  joint bound 中的未播种样条 jet 复用，以及新输出的保存重载和继续建模。
  A/B 必须执行相同正确性硬条件，优化不能跳过证明或修改失败语义。

专项合同和证据见 `docs/Coincident_Finite_Domain_Boolean.md`、
`docs/Intersection_Point_Contact_Consumers.md`、`docs/Smooth_Support_Chain_Acceptance.md`、
`docs/Trivalent_Corner_Blends.md`、`docs/Finite_Rolling_Continuation.md` 和
`docs/Modeling_Performance.md`。以下仍是扩展工作，并非已完成的一般算法。

本轮三个阶段的上述可认证支持域已完成联合验收：内核 1,603 项、XT 12 项全部通过，
复杂角点/选边顺序 84 例及两种输入来源的整条组合建模链通过独立 PK；最终源码的
完整链缓存 A/B 保持相同硬门，测得 Boolean 降时约 21%。完整证据、预算和失败边界见
`docs/reviews/advanced-modeling/m3-m4-m5-2026-11-06.md`。

## 后续支持域扩展

1. 一般精确相切曲线、分叉、自交、非仿射同域参数关系、点连接并集和内部相切
   空腔仍需独立事件及消费者计划。有限球面当前要求可认证的开放半球图，不能
   推广为任意球面/锥面/环面区域。
2. 真正几何支持面切换、曲面终止、不同半径和任意高价角点仍需新证书；不能将
   同一支持面被拓扑分区的链当成这些问题已经解决。
3. 小面消退已实现明确的等半径零 band 事件，任意短边修复、局部覆盖和混合
   凸凹角点没有因此自动获得支持。未证明的间隙或 continuation 明确失败。
4. 重型 Blend/Blend Boolean 仍有分钟级输入。继续按完整操作的实际 profile
   优化，保留历史快照与新版本复测，不能用热求值吞吐替代建模耗时。

## 边界

本轮不承诺变半径、任意角点、自交修复、全部相切或重合，也不把独立 Blend sheet
视为实体圆角完成。PK 对照只允许将参数化和 face matching 差异记为诊断；结构、
完整体检查和物性不能降级为可选项。完成记录必须区分 M1 首例与整个 M1 支持矩阵。
