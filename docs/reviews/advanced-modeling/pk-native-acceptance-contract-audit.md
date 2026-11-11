# PK 原生验收合同审计

此记录追加当前定义的审计证据，不改变 `f9c59201c0b9c5d103dee45124e3f43ca59a0921` 的固定产物、原始测量和退出码。默认 Boolean 入口的计算修正仍在开发，不属于下面原生对照的已验证范围。

## 物性积分的真实面片细分对照

官方 Function Descriptions 31.1.1 将 `accuracy` 定义为计算工作量/迭代控制，不是百分比或所要求的误差。31.4 与 31.4.1 要求保存积分误差界，并在比较相同或相似实体时使用它；区间相交本身不能证明实体相同，也不能证明固定点值精度。

`scripts/NativeAcceptanceContractOracle.cs` 通过现有 PKToy 绑定和 ParasolidScriptHost 实际调用 Linux x64 PK 38.0.150。脚本读取真实原生 XT，在包围盒中心沿三根坐标轴分别执行 `PK_BODY_imprint_plane`，没有修改 CHART 或坐标。每次从同一输入字节重新 receive；压印只增加边、顶点及有限面的划分。两侧完整 BODY_check 均为零故障，region/shell 数保持 2/2，面数由 10 增至 15；各输出的支持面通过 `PK_GEOM_is_coincident` 与原支持面匹配，oriented-surface sense 保持一致。

输入 SHA256 为 `3047cc3e0d7366fdb655dfae534ad9bc0fe32183443f55cd333923ba891e6a99`。脚本 SHA256 为 `15beb5031b59d21bcf04434ca0c2d4407cd2a0db58b75e623eff0f8220c281cc`；已验证日志 SHA256 为 `1cac2ba2153198b40c8cda29fa9e14c01e21cf8e9f06bddf4bb1929c59f6cc2a`。实际命令：

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/NativeAcceptanceContractOracle.cs -c Release -p:UseSharedCompilation=false -- imprint-verified temp_docs/short-edge-blend-oracle/native-transverse/a1-r0.5-t1E-07-reverseFalse-default-transverse-local-cut-reload-before-cut-self-constructed-product-copy-reflected-native.x_t
```

原始日志及外部退出码 `0`：`temp_docs/pk-native-acceptance-audit/native-contract-verified.{log,exit}`。实际输入和三个输出位于 `temp_docs/native-acceptance-contract/imprint-verified/`；该目录的 `artifacts.sha256` SHA256 为 `6c43ecc4c43ff03a2ab85dd6fdb909ce1525819ab6dc9e4eca40087eb8849027`。已存在目录拒绝重写，重跑须指定新输出名。

| 压印轴 | 原固定 normalized15 点值差 | 两个 accuracy |
|---|---:|---|
| 0 | 5.544753303860703e-7 | .99999999 / .99999999999 |
| 1 | 1.3852500837859584e-7 | 同上 |
| 2 | 2.9305446827897e-8 | 同上 |

前两项说明原点值门槛会拒绝真实 PK 自己进行的合法面片细分。这不是 GM 产品的等价证明，不能据此把 GM 的几何、拓扑、裁剪或材料差异改为通过；原生数值误差界仍需独立记录和判定。

## 边界精度对照

同一脚本实际新建 2×2×2 原生 block。session precision 为 1e-8，顶点报告 precision 为 5e-9；沿顶点向实体外平移至 9.9e-9，`PK_BODY_contains_vector` 仍返回 On，1.01e-8 和 2e-8 返回 Outside。因此不能仅凭顶点报告精度就把 GM 边界阈值缩到 session precision 的一半。510 点审计必须保留四方分类，并结合实际有限边界、拥有关系及有效精度判断。

首次编译失败、首次支持面 tag 子集断言失败及其原始退出码仍保留在同一审计目录；它们不属于成功对照。匹配支持面使用原生几何重合查询，不能用压印后 tag 必须不变的错误假设。

## 有误差界的数值判定

`scripts/ParasolidMassProperties.cs` 为原生积分结果保留每个分量的估计与 `PK_mass_bound_modulus_c` 返回的模量。volume/mass/periphery 使用各自模量，中心三分量共享中心模量，惯量九分量共享惯量模量。对每个分量，使用向外舍入计算所有报告区间内数值的归一化差下界与上界，分母仍为 `1+abs(reference)`，阈值仍为 1e-7。

- 上界严格小于阈值：Verified，仅表示在报告误差范围内可验证该数值精度。
- 下界大于或等于阈值：Different。
- 其余：Unresolved，不签署固定精度通过；区间相交不能提升为 Verified。

这不取代严格实体比较、完整 BODY_check、有限裁剪、材料和回滚验收。非法/非有限估计或模量直接失败；不通过缩放姿态、增大阈值或挑选分量消除差异。

`native-contract-bounds.{log,exit}` 实际外部退出 0 验证的是控制实验执行及断言，三个压印数值结果均为 Unresolved，不能将该进程成功说成 15 项数值精度已经通过。新建真实 2.1×2×2 对 2×2×2 block 判 Different；相同精确数值判 Verified；宽误差界包住两个不同点值仍判 Unresolved。命令同上，仅输出名改为 `imprint-bounds-v1`，对应原生输入/输出保存在 `temp_docs/native-acceptance-contract/imprint-bounds-v1/`。此增量尚未切换原 ShortEdge/看板生产合同，后续整套候选统一接入。

## 当前计算修正与合同接入

默认 regular Boolean 现在要求边界关联，不再由 `ForbidSpatialBoundaryReuse` 诊断开关选择身份安全规则。输入顶点按原拓扑身份读取；来源边的有限 native 端点和支持面使用分别登记；第三体交点始终需要证明。普通入口仍拒绝未认证的 cylinder junction，失败恢复原输入与池状态。

原 roof fixture 的相邻面分别发现同一个原生根，旧路径靠 XYZ 邻近归并。当前实现对实际 BCurve/plane 和 BSurface/line 求交保留完整隔离区间；精确 affine NURBS 只有在全部定义极点满足该平面方程时才使用其标量平面方程，持久几何仍为原 BSurface。两个平面与一个 BSurface 的交点从同一有限 plane-pair line 的三维位置及原生 UV 隔离域传递到各 ICurve 的实际 CHART，目标 contraction 同时约束 XYZ 和该 BSurface 的 UV；不按盒子重叠或位置接近宣告同根。

投影替换 fixture 的另一个失败是有限椭圆端点。实际输入给出的方程为 `f(t)=-1.0000000000000013*sin(t)`，trim 为 `[0,pi/2]`。旧 locator 将端点表达为跨过 trim 的 `[-8.590467018205802e-14,+8.590467018205802e-14]`，不能证明它属于哪个有限使用。当前对**已声明的 native t=0** 使用 `sin(0)=0, cos(0)=1` 的精确恒等式；分离的导数证明前半段只有此根，后半段独立完整隔离。此根保留原 POINT/vertex 身份、原 trim 和原坐标。没有将附近参数或 binary64 的 pi/2 吸附到象限。三个新增控制分别保留精确端点、保留 1e-13 的内部根、排除 -1e-13 的外部根。

开发窄验 `default-current-focused.{log,exit}` 为 **38/38，原始 exit 0**，包括默认开关关闭时真实进入投影替换的故障注入及三输入字节/池/分配回滚；allocation IL guard 检查 2939 方法为 CLEAN。真实 PK oracle `regular-native-two-tools` 的 unite/intersect/subtract 三项均通过完整 BODY_check、共享 AssertEquivalent 和材料分类；仅规定的 n_cht_pts 为诊断。其数值结果有两项 Unresolved，故进程原始 exit 2，而不是整体数值通过。

`RollingBooleanConsumerOracle.cs --input-set=<name>` 接受明确的已验证原生 XT 三输入目录。GM 和真实 PK receive **同一组原始字节**；GM 导入后的重新 transmit 字节另用于回滚比较，不替换输入。`default-exact-native-end` 的三输入 SHA 仍为 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`、`ad885c46aeb0c22a384cc4f3c567cbdc0030ed55c30a57ee99af98d4d186aec2`、`8056a1bf6862831b1d4de1da36af94f6bd5164fcaf019b5f5fc8d51644c40792`。两方 Boolean 实际 error=0/result=21650/一个实体，实际进入 projected replacement，fresh GM XT 的完整检查 0、严格比较 0 差异。mass 为 Unresolved，原 exit 2。这是 Boolean 操作的成功证据，不重新签署其固定 f9 输入产品的构造资格。

Block single fillet 的旧 oracle 将 curve_sense 差异降级后在重附着的 canonical copies 上比较，Web 严格比较则正确拒绝。现已删除该归一化路径及 curve/surface sense 白名单。接触直线从所选原生线保留参数方向与 gauge，并从它在各支持面上的真实 fin use 导出 sense；rolling line provider 同样使用来源 fin，而不是固定令第一个 contact 继承 source sense。single 与 parallel 两个真实独立构造结果均可直接通过原始产品的严格比较。`local-single-contract` 为原始 exit 0，物性报告 Verified；`local-parallel-network-fin-fixed` 严格比较 0 差异。额外 all-edge block/oriented 控制仍有原先未关闭的圆弧 sense 差异，不能据 single/parallel 通过签署全部 local operation catalog；本增量不改其 all-edge 构造路径。

当前 ShortEdge、regular curved Boolean、同输入 Boolean 与 local oracle 使用同一个物性报告合同：每个新输出目录的 `mass-properties.csv` 保留 15 分量估计、原生模量、归一化差上下界和 Verified/Different/Unresolved。几何/拓扑硬门失败仍拒绝；若所有硬门通过但存在数值未决，完整执行后打印 GEOMETRY_TOPOLOGY_PASS 并返回 **2**。不保留旧点值门槛作为另一条活动合同，不改历史原始日志或退出码。

固定 510 点的新表逐行保留四方结果，差异集合分别计算，原坐标和历史文件不变。`probes-import` 实际原 exit 2：native-roundtrip 7、GM-native 11、GM-fresh 14、fresh-native 7，四方并集 16 项均为 On 分歧，0 API 错误、0 Inside/Outside 对立，当前标 Unresolved。它们不因 native 自身的 7 项差异而被整批免责。最近有限 FACE/EDGE/VERTEX 的原生 range、重新计算的空间距离与局部 precision 保留在日志；range 的 nominal 结果并非距离误差界，没有证明有限距离界的项仍明确未决。当前表不宣称已经关闭 510 门。

`native-contract-complete.{log,exit}` 的真实控制执行 exit 0：错误尺寸的积分区间判 Different；原生错误球半径与错误有限 wire trim 在 fresh XT receive 后均由未修改的 AssertEquivalent 拒绝；明确分离的真实材料翻转判 Different，On 分歧不签通过，API 错误拒绝。控制目录为 `temp_docs/native-acceptance-contract/native-contract-complete-control-v1/`，保留每个实际 XT 与 manifest。这些是负控与合同执行证据，不能冒充 GM 正向建模成功。

以上为已保存的开发窄验，不是最终固定 SHA 全操作验收。后续统一用 `dotnet run --no-cache`，保留实际加载 GM 程序集 SHA256，并集中记录固定候选的完整生命周期、受影响回归、source/artifact hashes 与原始退出码；看板仍使用固定 f9 历史 run，未来新 run 须明确区分几何通过、数值未决与真实硬失败。

## 默认有限 BCurve/BSurface 根的实际修复

`IsolateFiniteSplineSurface` 对实际 native knot span 提取 Bezier 区间，复用现有 Krawczyk 隔离器，保留源 BCurve 参数区间与目标 UV 共同证书；重复根只有在同一组参数化方程的唯一域证明相同时才合并。未解析或有限域外根不被接纳。内部 BCurve 求值即使请求 D0 仍计算 D1，调用方提供两个向量槽；最初单槽导致的 OutputTooSmall 是真实实现错误，保留开发日志，不归因环境。

实际命令：`MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RegularCurvedBooleanOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- nurbs --output-name=default-native-spline-surface-v2`。`temp_docs/pk-native-acceptance-audit/default-spline-surface-native-v2.log` 中三个独立构造 NURBS/NURBS Boolean 的 fresh receive、strict compare、完整体检查和材料检查通过，mass 三项 Unresolved，原 exit 2。加载 GM assembly SHA 为 `0415c20203b4540a4caa42621a604920944e9309608eb462432f93b7dfb713b5`。同范围 managed 回归 `RegularCurvedBooleanTests` 37/37，原 exit 0，记录在 `default-spline-tests.*`；新增三个默认安全路径的实际两实体 Boolean 生命周期回归。

planar single fillet 接触 line 直接克隆原生父 line 并平移原点，保留方向系数和参数 gauge，不再次归一化。`LocalOperationOracle.cs --output-name=native-parent-gauge-single-v1` 实际 block-single-fillet 严格比较零差异、mass Verified，原 exit 0，见 `local-parent-gauge.*`。

完整固定 `0e67b6b` regular oracle 的 45 项实际为 24 项几何拓扑通过、21 项真实建模失败，原 exit 134，见 `temp_docs/pk-native-acceptance-audit/final-0e67b6b/regular.*`。本修复关闭其中 NURBS 三项；continuous/reloaded 六项与 same-domain 系列十二项尚待修复，不能把本节视作 M0–M5 完成。相同固定源的完整横向 shortedge 默认生命周期实际几何拓扑通过，mass 八项未决、原 exit 2，见该目录 `transverse.*`。two-tools 始终表示 target 加两个 tool，共三个输入。

## 同支持面有限身份的共同层增量

原生 affine BCurve 只在完整 clamped knot 与每个定义系数都证明线性时取得数学直线视图，保留原 native t，不创建另一种持久几何。新增控制中 native 区间 `[0,1]` 对应实际线段 `[0,.5]`；中间系数偏离 1e-13 则不取得该证明。恒定且正的 rational 权重同样按原齐次系数核验。有限原生边之间的关系可以据此证明相交、重合或严格分离；平行且常量水平非零单独记录为 Empty。

操作内事件别名保留双方真实原顶点及来源参数行；只有完整有限载体关系证明后才归并。临时 input edge 的事件索引同步更新，持久输入 B-rep 在发布前不变。ICurve 端点的传递使用同一原生 loop 中相邻 coedge 对该端点的共同拥有关系，以及来源边之间的有限系数对应证明，不以 XYZ 近邻或 CHART 反求后的近似参数替换其原 trim。

隔离器仍要求原收缩条件。在向外舍入的盒跨过有限域时，只有原始 Bezier 齐次系数在原生 corner 精确相等、且该 corner 严格落在已证明的唯一域中，才能把它认作原生边界根。控制中的正负 1e-13 附近情形仍返回 NotConverged，不将它们吸附为该 corner，也不声称已经证明无交。内部 BCurve 的 D0 单位置槽调用使用局部两向量 jet 保持现有 D1/tangent 计算，不改变 native 位置或导数定义。

共享 affine 参数传递明确区分「已证明在此有限 trim 外」和「尚未证明」。后者继续走其适用的提供器或拒绝，前者不会再次进入不适用的反求。传递到目标的站位保留真实方程或真实目标输入边 occurrence；不伪造来源索引。

`default-native-qualified-affine.*` 实际 142/142、原 exit 0；包含同支持面不同裁剪环的六个 Boolean/XT reload 生命周期、相邻分支及有限域负例。进一步 `default-overlay-regression.*` 实际 **189/189，原 exit 0**，覆盖 RegularCurvedBoolean、BCurveEvaluation、FinitePlaneSeparation、IntersectionEventBoolean 与 ParametricSurfaceIntersection；allocation IL guard 2952 方法 CLEAN。本节是共同层增量验证，完整真实 PK 45 项的 `qualified-overlay` 执行尚待汇总，不能由 managed 测试签署完整建模质量或 M0–M5 完成。

`default-boundary-closure.*` 的较早完整执行实际为 33 项几何拓扑通过、12 项建模失败，原 exit 134，加载 GM assembly 为 `4804cab877124e8895bb74f9d9713f5bf83c079e0c73fcd2ddd303318e0b4879`。它已独立关闭最初 21 项中的 NURBS 三项及 same-domain/reloaded 六项；当时 continuous/reloaded 六项与 partial/reloaded 六项仍失败。开发执行与后续源快照分开；`qualified-overlay` 的 source 快照在加载后包含仅测试 hook 的后续改动，不作为最终固定 SHA 验收源绑定，最终候选仍需干净固定提交统一执行。
