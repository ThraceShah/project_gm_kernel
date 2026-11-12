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
