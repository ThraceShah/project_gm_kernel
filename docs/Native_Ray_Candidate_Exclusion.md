# 材料射线的保守候选排除

本项沿同一个 PK 整链目标，计算基线为已推送 `1fffe8fe2448ff886557a6f888e4395f58b59d4e`（CCC271+真实 solver UV 数据流），不是统一候选的验收。用户已将研发统一到 q2 集成负责人维护的一套源码；本项有界工作封存后从共同固定源继续，不再基于 CCC/ab 启动下一实验。最新共同入口为 `11bd21a14a234b3cbf0991b5ff8b6f03752fc3a8`，含固定UV及q2有限Cone/Reader/trace工作，本项射线代码未默签为其组成或通过。

## 处理链差异和实际改动

`RegularBoundary.RegularRayWinding` 对非解析面先计算 extent，再只检查沿射线方向的 maximum.Upper<0，随后直接 `IntersectRegularRay`。横向错过整个盒子的射线仍进入曲面求交、有限根、有限面 winding、最近点与CHART/区间证明。现在只在盒子资格及完整 consumer 域已证明时，用外向 slab 排除不能产生原接受候选的闭射线段。相交、相切、不定及无资格候选继续原路径；没有换 ray、增预算、改变接受容差、删除后验或维护历史兼容。

生产改动是 Geometry 的纯资格/slab 方法、原 root residual literal 提内部常量，以及 Runtime ray 非解析分支的调用。`RegularBoundary` 的 Reader、分类参数、Cone/Torus分支和发布等函数未改，供统一源做固定最小patch合流。`RegularSolidBlendOracle.cs` 只在原 measured interval 之后按原 --bench-profile 开关打印 Boolean PROFILE；两侧实际使用相同该 Oracle DLL/PDB。

本次没有增加算法 scratch、持久 cache 或 heap；原零托管分配与失败回滚受影响控制通过。无 C ABI、NativeAOT、Windows、正式旧窗口或旧完整 suite。

## 为什么这个筛选保守

不能把 `RegularSurfaceExtent` 的 Success 当任意 consumer 的有效盒子。本项只证明以下实际 ray consumer：

1. `RegularSurfacePatch` 对 B-surface 枚举实际 knot spans。纯资格检查有限 poles、合法维数/跨度、有限非降 knots 和严格正 rational weights。每个实际 span 内，非负 B-spline basis 与正权使 S 成为齐次点除w所得Cartesian控制点的凸组合；`GeneralSurfaceExtent` 的原外向除法/hull因此覆盖全部原锚点域。非正权、非有限及无法证明者不新增排除。不从有限trim或vertex点样推断自然域。
2. 对 BLEND，原 extent 只界定 SurfaceRecord 的UMin/UMax/VMin/VMax，原 ray consumer 却遍历全部CHART单元。现在显式要求boxed域包含每个实际有限有序CHART参数；V域为同一record的原V域。较小记录窗口不能替完整搜索域。盒子来自同一即时source/generation/program与该域的原BlendExtent，未跨owner/修改复用。
3. 原 `IntersectRegularRay` 的ray并非无舍入的理想直线，而是两个齐次系数构成的degree1区间Bezier：p和外向p+d*upper。slab从同样的endpoint区间构造delta，测试整个系数family及规范参数s∈[0,1]。每轴区间除法给可能s的超集；三个必要范围严格空交才排除。零/±0/次正规、含零除数、溢出、非有限或不定均不能凭NaN造空交；边角/端点相触保留。
4. `ParametricSurfaceIntersection.Isolate` 在扩展参数域作Kraw证明，但此consumer未请求certificates，返回anchor始终由原cell.Center和Clamp保留在原cell，成功还要求centerF每分量Magnitude<1e-9。故盒子按这个原门作外向坐标扩张，保留原近似anchor接受，而不只保留精确数学交。literal改为内部 `RootResidualTolerance`，数值不变；不将它解释为所有consumer的统一精度。扩展域的证明不会把返回anchor域扩大；finite trim随后只能删除原root候选。

这些条件针对当前射线调用，并不证明任意有限face、Cone-pair或其他入口都可使用同一个无参extent。协调已将公共extent资格/完整数学bound统一交Mac实现；q2暂把Cone另一侧BSurface/Blend置unknown，不删除候选。后续在统一源给各consumer显式域/允许外延、来源与精度要求，不能沿用本次ray方法名当通用证书。原extent窗口与全部CHART不一致的其他消费者也须单列核查，不因本次保护ray就签为正确。

## 验证

`RayBoxExclusionTests` 包含11个实际测试case：横向无交、闭边角与端点、起点内外/反向、原残差附近、外向endpoint、2^500平移尺度、±0/次正规/非有限/溢出、错误权重/knots、BLEND完整CHART覆盖及缺失域。它们与既有曲面Boolean、共同事件、regular blend的失败回滚/生命周期/零分配一起执行，实际 **86/86、0fail/0skip、test0、22s**。源diff、TRX、原退出与测试/普通制品分别在 `temp_docs/system-chain/`，不是一次旧完整回归。

两侧各一个进程，四类各两次实际Fix与随后Boolean，plane/NURBS另执行原生命周期消费者；独立PK按同参数建模，GM fresh XT在真实PK receive/full check/原strict compare及section专项通过。两侧均PASSall8、无FAIL、各32条显式计时API0；producer均2，原物性U保留。driver0不改签producer2。未启用Capture，不补造loader/mesh/proof显示绑定。section有限点不冒充连续证明。

以下只是每类第二次执行的开发观察，不是中位数或正式窗口；后续case的rep0不能统称进程冷。全部GM/PK点、逐rep PROFILE和stage在 `docs/native-operation-alignment/ray-cross-class-observations.json`。

|类别|UV源 Fix ms|加筛选 Fix ms|UV源 Boolean ms|加筛选 Boolean ms|
|---|---:|---:|---:|---:|
|plane/NURBS|403.349|276.421|252.560|255.390|
|depressed roof|115.822|110.329|225.115|201.135|
|NURBS/NURBS|1704.221|1576.295|6571.577|4688.415|
|concave NURBS/NURBS|7299.207|6634.100|23473.669|17425.905|

|Boolean逐rep实际工作/阶段|UV源|加筛选|
|---|---:|---:|
|plane/NURBS root-cells|1347|1132|
|depressed roof root-cells|1331|1100|
|NURBS/NURBS root-cells|2835|1796|
|concave NURBS/NURBS root-cells|6140|3270|
|NURBS/NURBS bound-evaluations|4307|3491|
|concave NURBS/NURBS bound-evaluations|22352|19712|
|NURBS/NURBS rep1 Classify elapsed ms|2059.951|664.608|
|concave NURBS/NURBS rep1 Classify elapsed ms|5377.435|1645.632|

每类两rep的上述work计数分别相同，原逐rep均保留，不合为新独立证书。ssi-cells字段为0不表示未求交；计数按原插桩含义。stage/Bound等包含关系不作排他CPU、不相加归因、工作减少不直接换算普遍API增益。plane/NURBS Boolean的回退也保留。凹类仍约17.4秒，主要剩余SSI/区间边界工作尚未解决，不能签整体PK性能目标完成。

普通Native DLL：固定UV `27277c0ea3d7a95d0efd54b16b001b1bb1b88aff7de4623e2b0fb44a6d833fc6`，加筛选 `014e92c3ac5c4075bbb3cca65644b28dfa5d56d9edf127b2a374c02e946bf582`。两侧同Oracle DLL SHA256 `3a71d0dbb19db721077edde8ee9a49ea826c2b46082c84a0b5088432c25d97c8`，PDB亦同；原入口/pdb/Native hash和source diff绑定在 `temp_docs/system-chain/ray-cross-class-executions.json`。基线Native先封存，后与共同诊断Oracle放入新隔离目录，没有改写旧执行目录。

## PK 同平台整链补证

持续具名断点的两个Mac原夹具实际完成Fix→Boolean→BODY_check/XT，debugger0与inferior0分别保留。Boolean栈实际为PK_BODY_boolean→BOO_function→BOO__target_x_tool/imprint→target_topols_x_tool_topols→BOO__face_face→find_curves_via_iss→ISS_intersect_regions→ISS__phase2/MAR；与输入/结果BODY_check栈分开。Fix仍实际经过CNS_blended_edge/ISS_intersect_at_pvec/MAR及fix_modify。本次追踪默认tracking=false，不拿BOO_function_2未命中推无此分支，不据栈声称PK采用同一个slab实现或已理解全部间接调用。

原命令、实际库image/SHA、输入代码及输出XT、完整stdout/stderr/退出在 `temp_docs/system-chain/pk-dynamic-full-stacks-{plane-nurbs,nurbs-nurbs}/`。使用Mac库按名断点，未移用Linux地址；断点调试耗时不作为API性能。MCP实际查询的静态依赖与截取的不完整反编译仅保留私有temp资料，不进公开文档/门户。

当前改动为已验证可交接的固定进展，尚未独立数学/Native接受或与统一候选新组合验收，无main/上线或完整数值/性能资格。原79920继续，同一源码主线由q2集成交付，Mac继续公共extent与候选/SSI/材料的完整结构工作；不因本次提交结束或另建平行产品。
