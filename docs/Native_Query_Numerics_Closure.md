# 原生查询与物性收敛

本分支从 `17eedbfd6be891db8c43494cd91a896d261242c4` 开始；统一 Joint 的既有接受边界不变，不包含 Mac 后续实验或被拒候选。实际读取用户级政策文件、项目 AGENTS、计算及局部建模架构并核固定源码差异；这不声称验证了运行时自动加载政策。私有依赖准备实际返回 ENV_READY，Linux x64 PK 38.0.150、PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`；没有 C ABI 变化或 AOT 编译。

## 圆柱倒圆与倒角成品查询

原通用 regular 分类读取器排除了 Cone/Torus，rotational 专用模型又只接受 Plane/Cylinder，因此合法圆柱成品直接返回 5000。现只对分类读取开放既有解析面能力，复用已有有限 coedge/loop winding；没有扩大 Boolean 的输入类别。射线对 ring torus 使用其包围球限制，对 cone 使用其射线二次多项式的 Cauchy 根界，再由实际有限面 trim 筛选。根、方向及 region/shell 返回逻辑继续走公共 regular 分类。

开发执行（原件在 `temp_docs/query-numerics-closure/`，操作产品在 `temp_docs/local-operation-oracle/`）：

| 执行 | 原始退出码与实际结果 |
| --- | --- |
| `before-rim.log`，指定圆柱单 rim fillet | 1；4 个 finite plane side 点 GM 5000 / PK 0，保留原坐标与结果 |
| `axial-v1.log`，`LocalOperationOracle --axial-arrangement --managed-material --lifecycle` | 0；五类操作、20 个直接 GM 材料点及生命周期后材料查询通过，fresh receive、完整 BODY_check、strict、材料及物性门保留 |
| `axial-tests-v2.log` / `closure-axial-v2.trx` | 0；LocalOperation、RotationalBoolean、FilletInputClassification 共 89/89，包含四种圆柱成品的 owned region/face/edge 与删源后 XT reload 回归 |
| `new-test-isolation.log` | 0；新增四种组合 4/4，独立执行约 1.5 秒；不是另一套完整回归 |

最初测试辅助代码捕获取址局部变量导致编译失败，保留 `axial-tests.log` / `.exit`，修正后才取得上述测试结果。所有构建/测试设 MSBUILDDISABLENODEREUSE=1；仅检查自身 testhost，未全局杀进程。

这是首个源码 checkpoint，尚未签署整个查询/分类/物性任务完成，尚无独立接受、main 集成或上线声明。固定历史 510 原始表和物性原始证据不改写；本轮后续逐点有限精度审计、数学与最终完整交接继续进行。

## 有限接触边的拓扑错误与固定点精度

接触曲线的反求先验收 UV 残差，不能据此认定一个点在有限 edge 的空间精度内。当前分类额外在原 finite t 上求值并验收空间距离，且不再把参数域外的 `1e-10` 扩张当作有限 edge。已由 vertex 阶段处理的真实端点保持其原身份。空间上排除接触边后，其真实支持定义只作为优先候选；通过原反求和完整有限 winding 后才返回 face。候选面克隆经既有完整支持定义关系识别，没有近邻合并、CHART 改写或实体 tag/模型名特判。

- `contact-valid-before.log` / `.trx` 在固定 `4382d23` 分类实现上使用 100/200 倍、PK size-box 内的两例，实际 2 fail，错误返回 edge 而非 face。
- `RegularSolidBlendOracle plane-nurbs rounding-only --event-arrangement --contact-query-controls` 的 `contact-oracle-v2.log` 实际四个有限 face 查询，GM、独立同操作 PK、同 GM fresh XT 的 PK 均为 On/face；两尺度实体均通过完整 BODY_check 与 strict。该完整进程仍因独立物性未决返回 2，没有把查询通过写成整条数值全绿。
- 最初 1000/10000 倍候选触发真实 PK 的 `RTSTBX=13810` size-box violation，保留 `contact-oracle-v1.log`，不作为合法实体接受；初次修复只验距离仍遇到有限 winding 热点，保留中断与 120 秒 hang 记录及实际托管栈，之后才加入上述有完整验收的支持候选顺序。
- `NativeQueryReplay` 对原 Blend owner 保存的五个实际产品与 `axial-material-query.log` 的原 20 个 XYZ 作同字节四方重放，`fixed20-v2.log` 原 0，20/20 一致；`temp_docs/native-query-replay/closure-original-20-v2/results.json` 记录每个输入相对路径/长度/SHA、原 5000/PK0、新四列返回码/分类/拓扑类别及实际加载 Native DLL/PDB/MVID。该重放为固定成品 reload 查询，独立 live 构造/lifecycle 另由上一节执行验收。

固定 510 的 self/import 原始列和历史 15/16 差异继续保留。当前 self 仍有 15 个 On 分歧，native 自差集合仍为 102/153/204/254/289/390/441；没有将这些行整体豁免。`ShortEdgeBlendOracle` 在完成原始全部分类后才运行 finite-distance 与扰动诊断，避免投影缓存污染原始列。新增 JSON 对每个实际分歧保存原 XYZ、两个真实 XT hash、原生 finite 参数上的曲线区间界、实际 vertex/有限 plane 区域的外包围距离、失败区间与明确预算；不把 edge 距离当整个 face 距离。

开发 self 控制 `self-controls-v1.log` 原 2：9/15 行可证明位于实际有限边界的 session precision 内，余 6 行距离界跨阈值，保留 Unresolved。所有 30 个由同一实际 finite edge 最近点候选导出的半距离/双距离控制四方一致；这些派生点不能替换原 510 点。有限边界属于精度球的证明与四个浮点 API 返回相等是不同判断；原始 On 分歧继续明确未签相等。

## 物性：先修合同，再判断可认证范围

实际核官方 Function Descriptions 31.1.1、31.4/31.4.1、31.12 与当前 PKToy 原型/默认选项，并实际通过 Parasolid MCP 调用 `PK_BODY_contains_vector`、`PK_TOPOL_eval_mass_props`、`PK_TOPOL_range_vector`。本轮不因 contains_vector deprecated 而删除入口。`accuracy` 是迭代工作量指示，不是相对误差要求；默认 .99999999999 已高于文档常用区间，已存在 accuracy=1 的实验不能当作界会继续收敛的依据。

当前 helper 的 reference 和 actual 都调用真实 PK 对各自 receive 的成品积分；actual 不是 GM 自有积分器。模量返回布局为 amount[2]、mass[2]、cog[4]、inertia[10]、periphery[2]，共 20 double；中心三分量共用一个模量，惯量九分量共用一个模量。惯量已关于重心，矩阵分量仍以标准 Cartesian frame 表示，没有再次减去原点惯量或偷偷换坐标。

### 当前唯一合同

原 `|a-b|/(1+|a|)` 的常数 1 与有量纲的体积、位置、惯量混加，不具单位一致性；惯量非对角项接近零时尤其不合理。它也按三个独立位置分量判断整体重心，不能给一个坐标旋转一致的误差要求。因此本轮按用户授权修正现标准，不保留旧标准的生产 fallback：

- volume、mass、area 使用各自正的 reference 整体量作尺度。
- 重心差使用 Euclidean norm，除以 reference 回转半径 `Rg=sqrt(trace(I)/(2M))`。
- 重心惯量矩阵差使用 Frobenius norm，除以 reference `trace(I)`。
- 所有 reference 尺度本身也用原模量向外包围。分子由每个分量的外舍入区间产生距离上下界；共同模量按每个分量都可能达到该界处理，不假定误差独立或可以相消。
- 归一化量的上界严格小于 `1e-7` 才 Verified，下界不小于它才 Different，其余 Unresolved。重心三行、惯量九行分别记录其整体量的判定，同时保留每个分量的原 estimate/modulus 和分量差上下界。

这使各量同量纲相除，并在精确数学上对统一长度单位、刚体平移/旋转保持一致；浮点积分及其保守模量仍会随表示变化。`1e-7` 现在表示相应整体物理量的相对认证目标，不是所有分量的混合单位绝对门槛。各观察还分列 estimate-only 归一化区间、reference 模量贡献、actual 模量贡献；estimate-only 接近不能独自签通过。

### 同几何为何不保证当前数值门通过

严格同一几何、相同密度的精确积分当然相同。现有完整 BODY_check 证明的是 PK 的有效性检查通过；strict 使用规定的 topology/type/sense/tolerance/missing/matching 和容差偏差检查，内部 chart points 只作诊断；已证明的有限方向对应只覆盖其明确类别，材料探针只覆盖实际点。它们不是任意曲面的全域零误差、法向图对应或积分误差定理。不能将不同模型或不同阶段的绿灯代入当前积分，也不能因积分界太宽反向撤销已独立通过的操作。

几何误差可能累计也可能相消。若两闭合实体已有全局正常图证明、位移 `|h|<=epsilon` 且未越曲率 reach，体积差可由曲面面积分的 `epsilon*(1+epsilon*|k1|)*(1+epsilon*|k2|)` 控制；这不是任意 Hausdorff 近邻都自动满足的前提。令同密度实体的对称差体积为 D，R 包围它们相对于参考重心的位置，则 `|delta M|<=rho*D`、`|delta c|<=rho*R*D/M_actual`；重心惯量 Frobenius 差可用 `sqrt(2)*rho*R^2*D + sqrt(2)*M_actual*|delta c|^2` 控制。面积还需要切向/法向变化控制；小位置误差本身不能排除高频起伏引起的大面积变化。

这些式子解释权重和尺度敏感性，也明确了还缺什么证明。现有一般 BODY_compare 没有给上述全局 D 或正常图证书，所以不能把操作 tolerance 简单乘 face 数，或将统一门槛抬到最宽 PK 模量就声称已证明合理几何误差。可以为明确应用声明较粗的物性精度目标，但这与从当前几何证据推导误差界不同。本轮先实施有单位/整体量依据的修正，保留尚不能认证的目标精度。

### 固定 1080 分量重判与真实控制

`ReviewMassCertification.cs` 仅只读处理固定历史窗口；逐份核 72 个原 JSON 与固定原 ZIP 内同名条目的长度/SHA，ZIP SHA 为 `5a22e9acae103d1b3b6fcfcb920c7eb13ae526d42fdf5181ac8e78db332436f1`。它不重新执行 PK、不覆盖历史结果、不成为生产旧格式 adapter。`mass-review-final.log` 原 0 只表示重算及原计数断言成功：

| 唯一报告范围 | 原标准 V/U/D | 当前标准 V/U/D |
| --- | --- | --- |
| 主 60 报告，900 分量 | 312/588/0 | 348/552/0 |
| 另 12 个 native-via-managed 回读报告，180 分量 | 84/96/0 | 144/36/0 |
| 全部 72 报告，1080 分量，只计一次 | 396/684/0 | 492/588/0 |

144 个旧 U 转 V，48 个旧 V 转 U（12 volume、12 mass、24 cog.z）；后者说明修正也收紧了原常数 1 对小体积的过度宽容和逐分量重心判断，不能宣传成统一放宽。完整逐分量前后判与 source bytes/hash 见 `temp_docs/mass-certification-review/joint-physical-final/results.json`。例如 sphere 组合的 estimate-only 整体惯量差上界约 `5.43e-10`，而 reference/actual 模量各约 `1.08e-6` / `1.06e-6`，总上界约 `2.14e-6`；未决主要是双方 PK 误差包围，不能据此声称 GM 积分器偏差很大。

`MassCertificationOracle.cs` 读取此前已核合法压印输入 `3047cc3e0d7366fdb655dfae534ad9bc0fe32183443f55cd333923ba891e6a99`，每次从同一原字节重新 receive。identity、.125、8、模型空间内轴置换加平移各测相同 XT 和真实 PK 压印两种情况；完整 BODY_check、oriented support 对应均保留。八个合法数值控制仍 U：identity 同字节的整体最大上界约 `3.34e-7`，压印约 `3.77e-7`，平移/旋转后分别约 `5.09e-7` / `5.73e-7`；没有把相同输入的先验身份或区间重叠变成目标精度认证。三个单位尺度的 2.1×2×2 对 2×2×2 真实尺寸负控均 Different，六个制造的向量 norm/单位控制按预期 Verified/Different，制造控制与实际 PK 报告分开。

早期超出模型空间的平移控制 full BODY_check 失败 10 项，保留原退出码，不用于合法对照。原固定合法 PK 压印已证明旧点值门会拒绝纯细分，本轮既复用其原证据，也保留新的实际 PK 模量。当前明确可认证的正类包括本轮五类圆柱解析成品与其已验证生命周期，以及解析 block 控制；一般曲面/曲面细分后的 PK 宽误差包围仍未取得 `1e-7` 整体物性资格。没有通过增加工作量、改模型或误差球相交伪造全绿。

## 固定计算候选的完整执行与交接

计算候选为 `ab597e8a24b2ef23753507c8ca3e43d12edcc650`，包含查询提交 `4382d23b68997f52b7029d00700c2f783835a3ef`、有限拓扑提交 `8d29582dec6f9592d3f3854530ce1f549128bb66`。其后的证据工具与 JSON 不改变内核或物性合同。没有取入未接受 Mac 性能候选或触碰其 BezierSurfaceBounds/BSplineProjection 公共文件。

`final-suite.log` / `.exit` / `final-suite/closure-complete.trx` 实际 **2025/2025，0 fail/skip，原 exit 0**，包含新增六例；约 6 分 27 秒。函数目录 175 descriptors、分配 IL guard 3072 方法 CLEAN。检查本轮已知 testhost PID 均已退出，没有全局 pkill 或 AOT 编译。用户级政策正文不含 marker 标签、含末尾换行的实际 SHA 与指定 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1` 一致；未声称验证自动加载。

| 稳定候选执行 | 原 exit | 实际状态 |
| --- | ---: | --- |
| final-axial | 0 | 五类独立构造与 copy/删源/镜像缩放/reload，所有额外直接材料查询通过；Capture 285 pass/0 missing |
| final-fixed20 | 0 | 原五产品/原 20 XYZ，同字节四方查询 20/20；真实独立产品 strict/full 保留 |
| final-self-direct / final-import-direct | 各 2 | canonical 510 四方分别 495V/15U、494V/16U，无 Different/API error；native 自差仍为原七行 |
| final-self（附 Capture 的完整受影响生命周期） | 2 | copy/删源/镜像缩放/editable self reload、有限面/fin/材料检查通过；109 pass、19 missing，六次物性 U，15 个原点 On 分歧 |
| final-import（附 Capture） | 2 | 20 pass、4 missing，一次物性 U，16 个原点 On 分歧 |
| final-contact | 2 | 四个合法范围内有限 face 查询三方 On/face 一致，独立/full/strict 通过；18 pass、15 missing，物性 U |
| final-negative | 0 | 负控执行成功：错误尺寸 Different、错误半径与有限 wire trim Rejected；Capture 的 15 fail 为预期反例，不能算正向建模失败或成功产品 |
| final-native-mass | 2 | 八个合法 PK 数值控制 U，三个真实尺寸负控 Different，六个人工数学 norm/单位控制按预期 |
| mass-review-final | 0 | 只读核固定 72 原报告并按当前合同重算，非重新执行 PK，更非 1080 项全部通过 |

附 Capture 的两个 510 执行观察到 native 自差另含 row339，共八行；direct 执行仍为原七行。原 U 总数分别仍 15/16，四列和输入 XT/hash 原样记录，没有用展示执行覆盖 direct，也没有据此猜测 PK 内部原因。所有 62 个 direct 派生精度控制四方一致；两种 direct 各有 9 个实际有限边界归属证明，其余仍按各行原包围与失败区间保留未决。

历史 `probes-self.log` / `probes-import.log` 原件及其 15/16 条差异的四列/XYZ、对应历史 XT 路径/长度/hash 已只读绑定。旧 self 日志没有持久保存其余 495 点的原始四列，交付中明确为缺失，不由“它们当时相等”的摘要或本轮重放补造；原 imported 四列表与此前 current imported 全 510 表均单独绑定并保留。

交接的当前机器格式为 `docs/query-numerics-closure/results.json`，含逐操作、四组完整逐点当前列、历史实际差异列、逐分量标准前后判、真实 PK 与制造控制分列、全部原退出码、12 组 PE/PDB/source 绑定。`source-manifest.json` 与 `artifact-manifest.json` 分别绑定源码和 279 个实际制品；XT 与 DLL/PDB 本体仅在私有 `temp_docs/`，不提交厂商库。十个 producer 实际封存的 kernel/oracle PE-PDB 与每份 335 个 tracked Native 文档、六个 tracked oracle 文档逐 checksum 匹配；套件为封存输出 PE/PDB/source 核验，没有补造额外运行时 loader 观察。`final-fixed20` 还记录实际映射的 PK 库：Linux x64，91,444,112B，SHA `fc9ba50b40d7e9ee05e02f28c2a29e6ab75b9c91e6b6ba5ea588cf54149b9c52`。

复放方式由每条 execution 的 `executedInvocation` 记录；运行前仍需统一私有环境 native ensure，输出名改为新名以保留原件。历史标准重判脚本固定核原 ZIP SHA，不作为生产旧格式兼容路径；收集器仅汇总已有执行，不代签其退出码或领域结论。

线上只读初查：直接 Tailscale HTTPS 读取 `data/index.json` 原 200，SHA `4ce1b6f22e487c8db26656626681ece04939f73919ab5677c69357b1efd2c046`，整个目录仍为六个旧 run，latest=`20261112T002227Z-uperf1654`，未包含本候选。因此当前不能宣称已独立接受、已集成或已上线。默认网络代理首次 TLS EOF 的失败与直连成功分别留存；只对该 HttpClient 禁用外部代理，TLS 验证保持，未改全局配置。后续需交协调端集中关键独验/看板 owner 自测发布，再按实际新目录与资源 hash 读回核对，不能拿旧 run 卡片充当本轮上线。
