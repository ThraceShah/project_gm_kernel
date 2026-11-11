# Blend 60be 集中独立验收

日期：2026-11-12。固定候选 `60be7216d646a296d7fd2d0b30b5f336a46a6a66`，开发基线 `089939392bf2d756e22bcdaa97910e12023584c5`，评审分支 `review/pk-native-blend-60be`。本次只增加独立复现与本报告，未修改作者生产代码、比较器、测试期望、原件或容差。

## 结论和接受矩阵

**限定接受下表中的 Blend B-rep 构造和独立生命周期；不接受“全部 Blend 功能及后续消费者无条件通过”的说法。** 新增有限解析方向证明在下述合法流形实体范围内可以作为原始 `curve_sense` 差异的逐项判据；未发现需要否决已验证构造的新增数学或原生模型阻断。五个圆柱产品的 GM 材料查询仍为实际失败，物性未决也仍为未决。

| 能力/操作 | 本次状态与具体接受范围 | 限制、阻断或未证明 |
| --- | --- | --- |
| 有限凸平面倒圆 | 接受目录中的 block 单边、平行分离边、全边，oriented block、prism 全边，oblique 单边的几何/拓扑及各自复制删源、镜像缩放、可编辑 XT 重载、清理 | oblique 单边的构造与生命周期物性均 Unresolved；不推广任意平面多交汇或混合凹凸方案 |
| 有限平面倒角 | 接受 block 单边、平行边、全边、指定面不等距，oblique 指定面不等距，prism 全边及上述独立生命周期；目录物性 Verified | 不推广任意曲面倒角；新独立 prism 全边复现还通过直接 GM 材料查询 |
| 轴向圆柱操作 | 接受单/双 rim 倒圆、单/双 rim 等距倒角、指定不等距倒角的 B-rep、上述独立生命周期与目录物性 | **阻断其直接 GM `BodyContainsVector` 支持声明**：五产品 20 个真实 finite-plane side 点全部 GM 5000，PK 0；失败查询组合原 exit 1，未执行该组合中的后续生命周期 |
| regular Plane/NURBS 常半径带 | 接受新 `plane-nurbs rounding-only --event-arrangement` 代表的结构、有限几何和原生对照 | 原 exit 2，mass Unresolved；其余 NURBS/NURBS、凹/凸等已有类别按固定 f9 证据范围保留，不能据一个新代表签所有参数与生命周期 |
| 光顺链、独立等价支持记录、支撑保孔 | 接受新 asymmetric43 separate-support 代表的构造、重载、复制删源、镜像缩放与清理；旧保孔/链证据按 f9 限定复用 | 新代表三次 mass Unresolved、原 exit 2；未复跑整个类别；后续 Boolean 独立归因 |
| 三条等半径凸带、三价角片和带消退 | 保留固定 f9 七个代表的接受范围，provider 本次未改；当前全边 block/prism 目录另有真实成品证据 | 这是固定证据复用，未新签任意高价角点、任意混合凹凸或所有退化事件 |
| 有限异构 rolling network、正长度短 ICurve | 接受当前单操作代表的自构造、finite ICurve/双侧 UV、native fin box/双向 finite face domain、material ownership 与独立生命周期 | 原 exit 2，六次物性 Unresolved；固定 f9 的 96-case self Fix 矩阵是历史已接受范围，本次未把它重标为 60be 新跑的 96 个生命周期 |
| 非邻接分量、不可行/未认证交互的原子拒绝 | 固定 f9 分离网络、管理回归中的已执行范围可复用；没有新放宽预算或失败条件 | 不推广通用多方事件或全局自交修复 |
| FaceAskOrientedSurf | 接受 raw face 与 surface sense 的组合修正；真实目录同产品、同自然 frame，四种 raw sign、镜像和重载均有原生对照 | null surface 的 orientation 仍按 FD 未定义；未签负 surface 的 query→copy/detach/reattach 组合 |
| 物性合同 | 接受已授权的 15 分量估计/PK 模量外包分类方法；几何独立验收 | upper < 1e-7 才 Verified，lower >= 1e-7 为 Different，其余 Unresolved；目录 exit 2 不转绿 |
| 查询/Boolean/整体交付 | 本报告逐操作结算上述通过项与失败项 | 固定 510 On 分歧、五产品查询、公共 Boolean base 增量、后续组合 Boolean、Mac 性能、任意曲面/变半径/高价角点均未签；没有合 main、部署或发布 |

## 实际读取与源码边界

实际读取用户级 `AGENTS.md`、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、有限解析 rolling network 文档、作者交付文档和最终收据。用户级文件实际 SHA256 为 `2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`；这是整个文件的读取身份，不把派发前同步的 managed policy 摘要等同于运行时自动加载证明。

已逐文件读取完整 `0899393→60be721` 的 15 文件 diff（1237 加、68 删），以及相关原源码和拥有关系/消费者。生产增量只在以下三处：

- `KernelRuntime.LocalOperations.PlanarBlend.cs`：接触直线继承实际支持 fin 的原生 gauge；倒角负向接触同时反转直线系数和 station；cap section 的负曲线 sense 同时补偿端点/coedge；平面倒角保存 inward 自然法向、负 surface sense 与正 face sense。未改 primitive、XT 定义或生成后 canonicalize。临时 cap 仅创建自己的 carrier，避免提前绑定随后被替换的 contact carrier 而泄漏。
- `KernelRuntime.ProfileFaces.cs`：wire 早已拥有 curve/point；转 sheet 只维护新 face/surface chain 并增加新 plane 的一个 owner。未再次给原边界登记 owner，未清池或全局重置引用。实际调用前提是已发布、无 face 的 wire；同一修改内有 snapshot/command rollback。
- `KernelRuntime.TopologyQueries.cs`：查询组合两个 sense，原记录、自然 frame、PlaneAsk、参数和 C ABI 不变。已读实际四处测试消费者及生成调度入口，无已额外合成 surface sense 的生产消费者。

另外读取 `f9→089` 的相关依赖差异，特别是继承的父直线方向修正，以及 `ReadRegularBoundaries`、rolling 的 `RequireBoundaryAssociations=true`、共享第三面事件的原有限证明路径。后者去掉未认证后备不等于给 Boolean 整体验收；新 `ReadRegularBoundaryAssociations` 的调用在 Boolean 路径，rolling 保留自己的边界关联入口。固定 f9 的报告 `4c7646e1085e6fa786b2f335cabc7c086c008e13` 已实际从 Git 读取，其 96/1885 等范围及原红项均沿用原限定。不能把 base 的公共 Boolean 研发一起算入本报告的接受范围。

## 原生定义与有限方向证明

实际使用 parasolid-research、parasolid-docs 技能及已连接 Parasolid 查询 MCP：`PK_FACE_ask_oriented_surf`、`PK_DEBUG_BODY_compare` 均实际返回 found；后者索引只给部分内部摘要，未把它视为完整 API 文档。进一步读取本机官方 `online_docs/headers/` 中 `pk_debug_diff_t.html`、`pk_debug_body_compare_o_t.html`、`pk_face_ask_oriented_surf.html`、`pk_edge_ask_geometry.html`、`pk_fin_is_positive.html`、`pk_fin_ask_oriented_curve.html`，以及 XT 5.2.9。没有复制厂商二进制或反编译正文。

核实：21959 表示匹配曲线的 sense 不同；edge query 的 ends/trim 按自然参数递增排列，另给 edge/curve sense；fin positivity 相对 edge，oriented curve 相对 fin，非 tolerant fin 可返回 null curve。XT 的 face/surface 朝向必须由两处 sense 组合。仅这些字段定义本身不能使 raw 21959 自动等价。

本次接受的是条件证明，不是取消 sense 门：

1. 保留单一 `scripts/ParasolidBodyCompare.cs`，原 `max_diffs=1024`、strict mask、global/local 状态和 raw entity/face/diff 不变。除既有 `n_cht_pts` 外，只能逐条审理 solid 的 21959。非唯一 carrier、非 Line/Circle/Ellipse、不同 precision、无效区间或未证界均拒绝；有条件通过还要求差异 face-pair 数严格小于 1024，不能在截断后宣告证明完整。
2. Line 选定 binary64 仿射映射 `s=alpha*t+beta` 后，以外舍入包围整个源 trim 上的常数及线性残差。alpha/beta 的求取可以是近似候选，因为随后证明的是这组确定系数的完整残差；不是拿近似投影本身当正确性。区间点积另证自然切向的严格号。
3. Conic 用原生 `C+X*cos(t)+Y*sin(t)`。选定 `theta,sigma` 后，两个完整系数残差的 L1 和控制所有参数的位置及导数误差。`min(||X||²,||Y||²)-|X·Y|` 是 Gram 最小特征值下界；只有其严格正且导数残差平方严格更小时，才证明 mapped tangent 始终同向。小半径/病态 frame 不会因绝对位置误差小而自动取得方向证明。
4. Sin/cos 对候选 phase 作逐运算外舍入 Taylor 包围。phase 在 Atan2 范围，代码更保守要求绝对值不超过 4；25 项后首次遗漏 cos/sin 项分别至多 `4^50/50!`、`4^51/51!`，均小于加入的 `1e-25` 余项。分母整数可精确表示。不会把 Math.Sin/Cos 的未给误差候选当成证书。
5. 负 alpha 先反排映射区间；两个端点的站位差包围整段 trim 的端点覆盖差。连续非零仿射映射使源/目标有限段可互相对应，target 全段速度界把两向 clamp 差换成位置界，再加整个 carrier 残差。周期整数 lift 用 `[Math.Tau, BitIncrement(Math.Tau)]` 包住真 `2*pi` 后参与外舍入，不把 binary64 Tau 当精确周期。限制宽度/period lift 可能保守拒绝额外输入，不放行未证输入。
6. 总界须同时不大于实际 edge precision 与 `1e-9` cap。拓扑端点按推导的 edge reversal 匹配，另用各自相等且正的 vertex precision；ring 只在双方均无端点时走无顶点条件。每条边必须各有两个 fin，沿 native BODY_compare 给出的 incident face 对应关系，检查 `fin/edge × edge/curve × map_sign`。独立 fin carrier 不同即拒绝；非 tolerant fin 返回 null 时只使用由 edge 推导的方向，不消费 FD 未定义的 orientation。

以上论证依赖合法 PK 解析 frame、有效流形 solid 和原生比较结果/完整 BODY_check；不是任意损坏 XT 或任意程序曲线的形式化证明。目标是容差内全有限段及两个有向使用等价，不是宣称实数几何完全相等。其他 global/local 差异仍硬拒绝，无新坐标系、替代模型、旧格式兼容或输出重写。

## 原件身份与原执行状态

原件只读根为 `../gm-blend-functional-delivery/`。新独立复现逐条重新校验以下五份收据，全部匹配，源码条目另与固定候选逐字节比较；各索引有重叠，不把 256 个索引条目称为 256 个独立实验。

| 原件索引 | 条目数 | SHA256 |
| --- | ---: | --- |
| `docs/reviews/advanced-modeling/blend-functional-evidence.sha256` | 33 | `9c89bde1cffcc7b40015ea9c562ac1d6ef9122e7ad559cb94e857173957ad25b` |
| `temp_docs/blend-functional/final-api-evidence.sha256` | 107 | `c2cac36671656bfccdcc34fc9ad316b1d8b0e7127ca56e77b30da7af2653e038` |
| `temp_docs/blend-functional/extra-query-evidence.sha256` | 78 | `fb98501b0cb3c7451c0e29884824edc261b2b87fe553496996d0f1142e6962c7` |
| `temp_docs/blend-functional/final-integrated-evidence.sha256` | 24 | `0fb17be8b40f4d78d555f6f3c76aebdfc788f8a13f9c7f83748d340e6291bce8` |
| `temp_docs/blend-functional/shortedge-products.sha256` | 14 | `b055abaea9c88446d0983d911de087dbd406bcf25d647a9bf85bcc813919e56d` |

最终 `complete-catalog.log` 实有 17 构造和 17 生命周期通过记录、34 次同产品 plane query，并打印 `17 passed, 0 failed, mass_unresolved=2`；原 exit **2**。日志 SHA256 `2b2d07c4167f96b48f1a7cac4fd25b8cdea95b6b29a3e9aef2c3618f45be8b45`，动态加载 GM assembly 为 `0e49ecb43e4ead37925dcef63b8d6c3e4e6355854ebddd3166e33b56ea58dfe6`。完整 cleanup 断言在每个 case 最后执行；原 exit-1 的方向/seed cleanup runs 仍保留，不用最终执行改写它们。

`direction-controls-v5` 原 exit 0，10 正/10 负；四 raw sign query fixture 原 exit 0，true/false/false/true，fixture 不冒充四个合法成品。123/123 最终相关回归、46/46 ownership 回归、140/140 较早开发回归及 normal IL2897 CLEAN 均只按各自固定源与时间复用；140 不被重标为最终源码全回归。

`axial-material-query.log` SHA256 `898878a22c3812a66b331fdc68e8d153ceb7dbde6e1ab8c02da1008ef9ce321c`，原 exit **1**，逐行保留 20 个点的 GM 5000/PK 0 及各自分类。默认 Blend 目录不选择 `--managed-material`，不会消除这个已知失败；只能说独立 B-rep 生命周期通过、附加查询操作失败。

`regular-contract-v1`、`chain-contract-v1`、`shortedge-single-v1` 均原 exit **2**，分别有 1、3、6 次 mass Unresolved。短边 self 产品仍为 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`，变换/重载为 `57eecc0294734c805d3ded1ab034fc4c78dec6da6d2300a6ce85a0e553981ad2`。它们是真实 GM 产品，不以两次真实 PK gauge 控制替代；原 PK 控制 exit 1 与新证明控制 exit 0 分别保存。

对复用证据，重新读取并校验 f9 总索引的摘要 `94752804be4ac709fbb4c1104b110b754787121c79a695b6f39723a5a12898c5`，四个 `regular-bore/support-chain/corner/separated-network` 原 exit 均 0，实际日志摘要与作者及固定独审报告一致。本次没有重做已独审的 726 原件全量检查或 96/1885 执行。

## 少量独立执行

统一私有环境 ensure 实际返回 **ENV_READY**：Linux x64、PK 38.0、固定 PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`，schema/corpus 就绪；私有依赖未入 Git。先只读审查，收到协调端重型窗口结束消息后保持串行。本次没有 `dotnet test`/testhost，也未启动 NativeAOT、Windows、Mac 或其他 fullsuite。

独立 runner `scripts/BlendFunctionalReviewOracle.cs` 使用现有 PKToy/Host 及直接托管 GM API；只在本树另存派生 fixture。它通过冷路径私有方法 delegate 定点检查真实 finite proof，随后仍调用完整 `AssertEquivalent`；没有替代比较器。

| 新执行，均在 `temp_docs/blend-60be-review/` | 原外部结果与意义 |
| --- | --- |
| `semantic-v1.{log,exit}` | exit 1；复现代码误用无构造函数的 KernelVector3，编译失败，未运行建模；原源码快照保留 |
| `semantic-v2.{log,exit}` | exit 134；8 对真实成品已完成检查，随后首个直接记录变异仍走导入 XT 缓存，raw sense=0，runner 自身拒绝“没有进入证明”的控制；不是生产候选失败，也不算成功负控；原源码与原输出保留 |
| `semantic-v3.{log,exit}` | **exit 0**；显式丢弃变异 fixture 自己的 XT 缓存并要求新字节不同；五收据全匹配，8 对实际 GM/PK 成品完整 BODY_check 零、当前比较通过，4 正/2 负独立证明控制通过，fixture geometry/returnBlocks 全清理 |
| `prism-chamfer.{log,exit}` | **exit 0**；新独立 GM/PK prism 全边 chamfer 构造、复制删源、反射缩放、editable XT reload、fresh PK 全检查/严格比较、32 plane query 前后、128 个有限面两侧直接材料查询及 cleanup 全通过；两次 mass Verified |

8 对成品来自最终目录的 block-all-fillet、prism-all-chamfer、oblique-single-fillet、cylinder-unequal-chamfer，各取原成品与镜像重载成品；该 runner 只是读取这些真实产品，不冒充又执行了八次 Blend。每对原字节 hash 记录在新日志。

新增控制专门弥补原大尺寸错误负例不足：

- 从真实 oblique GM 产品提取有限 ellipse，分别补偿 raw sense/两 fin、反转 natural axis、再加 π phase；三例各有两条实际 21959、两条 proof 成功、其他 local diff 0，正例完整 BODY_check 零。实际负映射经过不同周期 seam，map 为 `-t+2*pi` 与 `pi-t+2*pi`，全段界分别约 `6.26e-15`、`1.99e-14`。
- 同一有限 ellipse 的拓扑端点移 `5e-9`，保留 sense 补偿：native raw 仅两条 21959，无其他 local diff；两条 proof 均 false，完整 helper 拒绝。
- 实际 GM 圆柱一个 ring 做 sense/fin 补偿后，半径增 `1e-11` 的有界正例通过（全段界约 `2.002e-11`、BODY_check 零）。半径增 **5e-9** 时，native raw 仍仅两条 21959（小于会话 precision），两条 proof 均 false，完整 helper 拒绝。这直接验证新白名单没有把 native 容差内但超过自身上限的错误混过。

本次没有新测大量随机病态输入；对任意大坐标、超过允许周期范围、极小/退化 conic 的全覆盖能力不作承诺。它们必须取得同一完整界，未取得就拒绝。

复现命令（新目录名不可复用）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/BlendFunctionalReviewOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=semantic-new
GM_WEB_SELECT=prism-all-chamfer MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/LocalOperationOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=review-60be-prism-new --blend-arrangement --lifecycle --managed-material
```

第二条在本树实际输出目录是 `temp_docs/local-operation-oracle/review-60be-prism-chamfer/`；第一条的变异 fixture 在 `temp_docs/blend-60be-review/semantic-v3/`。两个新执行实际加载 GM assembly 均为 `8126ba254d9ec02183070edb6ba8c2ca8b828e8e7098c8d39bddfcc677b39a70`；`semantic-v3.log`、`prism-chamfer.log` SHA256 分别为 `23df946b7682aba5b68a2387fe5792ab05fd5ea9f9f50cb16b3bedc2828170f8`、`22a000e02d57a79c269ab4ce762142bf4f392ff0457dec8e94959ef10104bdfa`。本报告收据 `docs/reviews/advanced-modeling/blend-60be-independent-review.sha256` 绑定源码、原退出码、日志、fixture、当前构造产品及引用原件身份。

## 未执行范围与后续归属

没有为 FaceAttachSurfaces 的静态线索补签实测失败或将它加入本次必需前置；接受的 copy/delete/transform/reload 不调用该 reattach 组合。五圆柱查询及固定 510 边界合同继续交共同 owner，不能据下游 unsupported 单独撤销已通过的上游 B-rep，也不能把它们宣传为查询支持。base Boolean 未独审范围和移动 Boolean 分支修复均不在此接受矩阵内；本次没有接管或验证那些实现。

本评审收据 53/53 条目实际校验匹配，checker 原 exit 0，记录在 `temp_docs/blend-60be-review/review-receipt-check.{log,exit}`；这是身份检查，不给任何失败操作补签成功。

无 C ABI 变更，不触发 AOT；没有额外全局配置修改、门户发布、合 main、Mac 性能、显示 receipt 或同实例 mesh 资格。交付状态只为本 review 分支的独立报告及复现实验；固定 f9 历史红项与新质量层次同时保留。
