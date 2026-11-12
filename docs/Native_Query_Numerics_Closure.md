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
