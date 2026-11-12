# 原生一维截面包围 e7f0a48 集中独立审查

## 结论与固定范围

**数学、算法与关键集成 accepted；原质量按实际执行范围 accepted；唯一 Mac 窗口作为描述性观测 accepted。没有新增作者算法阻断。** 原合同下“全部数值通过”和完整性能资格仍 blocked，普遍／因果加速及整体 PK 目标仍 unproven。此结论不代表 main 集成、上线或看板审查。

| 审查对象 | 结论 | 边界 |
| --- | --- | --- |
| 连续数学与原生参数语义 | accepted | 同源完整二维证明内的 explicit spine＋原 analytic supports 截线；没有拟合圆或改变 native v |
| 算法、证书、缓存及根／图集成 | accepted | 原完整 cell、正则图、有限根、误差、CHART、nearest 与资源门保留 |
| 原 Mac 质量及产品证据 | accepted | 2029/2029、185 定向、八组实际几何／拓扑／生命周期；不升级原 U |
| 唯一五操作窗口 | accepted | 固定来源、机器、全部冷暖点及两种倍率的描述性数据 |
| 原 1e-7 数值全认证 | blocked | 原 1080 分量有 684 Unresolved；新 norm 不能回填 |
| 原完整性能资格 | blocked | runner raw2、16 个进程 raw2、performanceQualified=false |
| 普遍／因果／单机制排他 CPU 收益、整体 PK 目标 | unproven | 四暖小样本、波动、无排他 CPU／数值温度；没有相关完整证明 |

本树交付起点 `b8c74b95047577e52d1086b48b0b0a88ee3f4388`；计算 `e7f0a4876ef296b56f5f7c57a45f15617eac6918`；完整质量及实际窗口 metadata `ad79d2d4c6b59a81be423de4faf1745ea62cb318`。三者 Native tree 均为 `6a84372805cefdaf5c10cb3e0f3e56f8eb12ffdc`，src tree `cee0c419bd3e42acdc2d95e75a350f03cd8d06fa`。基线固定 `fef52f3252bfe9a345f1a2872e9b6f96bdcafccd`／计算 `2c0e4ea54bc2c4c9e22f24965ce71c41d84635b4`／Native8fc；复用 `24c54edc299fd8fdcc7ec3c53542fca4eca4ebcc` 的 `docs/Section_Root_Independent_Review.md` 及其 7b20/e812 旧数学，不重新审签旧候选。

实际读取用户级策略、树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、fef→e7 全部 diff、三个生产文件及消费者、三份候选报告。约束与两架构相对基线无 diff。实际用户策略 marker 正文含尾换行 SHA256 为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`；这只证明文件，未声称运行时自动加载。

## 数学与原生定义

### 母 cell 的证明内容

新入口不是调用者任意设置的“已证明”布尔量。`SurfaceIntersectionPatch.BoundCell` 先调用原 `Bound(cell, output)`，只有 Success、九项位置／du／dv 区间均有限、且 source 不是 iso／sweep／parameter-line 时，私有构造器才复制**整个相同 source**并附上这个 cell 的覆盖域。后续 iso 复制同一个 program、root、原参数映射与 CHART，再检查其 mapped 二维盒完全包含于该证明域；没有从另一个 source 或另一条方程搬证书。

有限九项本身不等于曲面的全部数学证明。这里可用的前提还包括原 `NestedBlendBounds.BoundGeometry/BoundExplicit` 的完整外舍入合同、已经编译认证的原生显式 spine／支撑定义，以及未改变的 `IntervalNormal`、`IntervalFrame`、`RollingSectionEvaluation`。母 cell 的原 Bound 已执行 spine 区间和原两次 AD frame：中心沿 native u 求导的一次、中心导数清零而 native v seed=1 的一次。analytic normal 的分母／支撑类型／cone 有效半径／torus 有效型及 profile、rolling section 的有限分支和完整尾界均仍经原路径检查，任何未完成不能授权。这些结构性前提，才使九项有限成为完整已认证包围的成功见证。

需要的正则性仍另由 `SurfacePlaneIntersection.Find` 对**母 cell 原九项**组成实际 level set 的 du、dv，并证明至少一个偏导区间不含零。它不是靠新的一维结果证明二维正则。非平面支持的中心 mean-value 收紧仍调用原 patch Bound；原 finite 距离检查、图方向选择与细分都在截线之前。因此省略另一偏导不会省掉原二维图所需的有限性或非零偏导前提；也没有宣称整个参数化处处浸入或没有所有退化。

### 一维包围的连续依据

设原表达式为 `S(u,v)=C(u)+r R(N0(C(u)),N1(C(u)),v)`。新 41 行模块只改变 AD seed 的需求：

- 固定 v：仍以 `SpineParameterMap.Interval(..., (U,1))` 得到原 C 与 native u 导数，v 的导数为零，输出位置和 ∂S/∂u。
- 固定 u：先运行同一个 spine interval，再把中心三项导数清零，v 的 seed 为 1，输出位置和 ∂S/∂v。

两者继续调用原 `IntervalFrame`，没有新插值、圆心拟合或角度重参数化；第三列零仅表示 iso 的第二个非活动参数方向。原小 q Weight 的值尾界与微分尾界、ratio<1 的条件、最大项数及外舍入全部保留；较大 q 的 transverse／norm／arc 分支也未改。ReverseSection 仍在原 frame 交换两接触法向；signed ranges、负角、零角和小角沿同一个定义处理，不能从 Circle 的几何类型推断 v 就是圆角参数。

新循环与原 `BoundGeometry` 相同地沿原 CHART 切分 native u：首末 piece 保留原数学外延，中间 piece 按原 CHART 参数截取，非退化盒不重复零宽 piece，点查询允许端点两侧 hull，所有成功 piece 求 hull。若任何 piece 失败，返回原失败状态；不是删去该 piece。它也支持原 `SpineParameterMap` 已支持的显式有理 spine，仍走原 RationalSpine interval／原 knot 分片与失败合同；未新增任意样条或隐式求解器能力。

固定 u 的位置区间采用 v-seeded frame；旧适配器的位置来自 u-seeded frame。小 q 自适应停项会同时看导数尾界，因此两个正确保守位置区间可以宽度不同。每次仍加入**完整**值及当前自由导数尾界，不能要求所有位置端点逐位相同，亦不能只保留微分尾界。自由导数则和旧适配器最终消费的同一次 frame seed、运算及 CHART hull 一致，关键控制逐位相等。连续包含依据是原区间表达式和完整尾界，有限点观测仅做控制，不被当作连续证明。

### 原生接口核查

实际使用 parasolid-research／parasolid-docs；经 Parasolid MCP 对 `PK_SURF_make_curve_isoparam`、`PK_SURF_eval`、`PK_CURVE_eval` 获取 found:true 的原型与说明。options 初始化宏查询 found:false，未将它当成函数不存在或无内部调用；实际 options 类型由固定 PKToy 绑定核实。官方 Functional Description 的 constant parameter curves 段说明可简化曲线类型、应单列曲线在曲面上的 interval，并可查询自由 surface 参数与 curve 参数的 alignment。没有拿 Linux 静态地址作为 Mac 动态地址，也未公开反编译正文。

封存 cf27be2 研究逐复制清单核 hash：错 UVBOX 的首次 24 次域外 error 与修正后 24 次构造／120 点分开保留，两个 driver0 都不是逐 API 成功证明；首次未提交源码不补造身份。正确 UVBOX 顺序为 lowU、lowV、highU、highV。simple u-iso 为 Circle 的研究仍显示相同数值参数点可相差约 1，强制 SP 才保留本例 native 自由参数。该有限观察没有成为新几何定义或连续证明。

本机真实 PK38.0.150 对本候选 shortedge sealed GM 与独立 PK 产品分别取得两方向 SP 截线，记录实际返回有限 interval，以原自由参数直接比较位置和一阶导数：4 条截线、20 点均 API0，最大位置差约 `1.33e-15`、导数差约 `3.77e-16`。这些是实际 PK 重新 receive 后的观察，不冒称 GM live 运行时导数，更不签任意截面等价。

## 作用域、缓存与消费者

逐构造器核查：初始 Bezier／Blend／spine／support-coordinate view 无证明；offset、sweep、parameter-line 清证明；重复 iso、从 sweep 或 parameter-line 派生 iso 清证明。正常 iso 保留 source 并逐次检查 fixed coordinate 与完整自由区间覆盖。normalOffset 非零、spine、implicit spine、nested Blend support、BSpline support均不能 admission，继续原包围合同。Bezier 即使完整 cell 成功也不能走新 Blend 路径。tag/generation/sense 的 retag 仅复制相同几何，实际 Runtime 在创建原 patch 时就 retag，在证明之前完成，没有在 Find 内改变模型或重标另一个 source。

`NestedBlendProgram` 是操作借用的只读指令／spine／chart／scalar spans；可写 workspace 是独立数值 scratch。实际 `KernelRuntime.BlendIntersections` 在固定 block 上创建 program，并在 Find 返回后才恢复 command scratch 或继续建模；General／Analytic 路径的 Bezier net 也在其 Find 返回后复用。Find 内只写局部 pending、bounds、roots、图输出和诊断，未创建／删除／编辑模型。proof 跟随 ref-struct 局部副本，不进入实体或跨编辑阶段保存；相同借用的不可变前提来自实际生产调用链，不是靠数组“看起来 readonly”推断任意外部修改安全。

一维先验 gate 在 immutable cache 查询**之前**执行。`isoAxis+2` 传入既有 parameterSide，既有编码再加 2，故固定 u／v 实际 kind 为 **5／6**；完整 surface=1、spine=2、support-coordinate=3／4。key 仍有 tag、generation、sense、root、kind 和精确 mapped 二维盒。零填的第三列不会给二维 consumer；只有新一维 Success 才存，新路径失败未进入负缓存，更没有变成空交。局部 Span cache 在 iso 构造时清除。原可选 immutable cache 的分配和 scope、失败即 miss、退出释放及修改边界不变，没有提高预算或借走 solver workspace。

旧 Section memo 不需要只因 key 没有 parent-proof 字段就被判不安全。实际每张表只在同一次 Find 内，原 patch、support 方程、容差、strict、资源和证书需求固定；显式 key 逐位约束 axis、at、first、last 和认证需求。不同父 cell 可能使位置包围宽度或求解工作量不同，但完整成功记录证明的是**同一有限闭区间上同一数学方程的全部根**，并保留原 root／monotone domain／方向证书。当前母 cell 已独立完成其二维证明及 graph regularity；memo 复用并不替代这些证明。成功空集同样先完成全部 pending；失败、部分根、NotConverged不存。

命中后仍用原 patch 运行 `BoundaryNode`，重新检查位置、残差、finite、空间误差及 support 参数；`AddNode/AddSegment`、CHART chord 误差、横断 nr≤1、失败回滚图计数与细分、完整 ValidateGraph 继续。隔离域／finite first,last 相同，原成功根证书的真实性不依赖它来自哪个有效母 cell。没有把不同 conservative enclosure 的端点逐位相同作为逻辑前提，也没有把 memo 内诊断当成新当前导数或重新计算的工作计数。原 scalar32768、depth48、Newton64、32根输出、Find32768／depth40／pending128 与既有资源合同均未改。

全局 nearest、原有限域和原生 UV、artificial limit 拒绝、外层 remap／图验收与 topology 发布继续沿实际 Runtime 消费者执行。nearest／distance 的其他二维 Bound 未改，新 proof 只来自 Find 的完整 cell，不能泛化到未证明的 projection iso。新算法不改变 XT、类型、拥有关系、容差或 C ABI。

## 必要独立执行与原质量

私有环境工具在本树实际 ensure 得到 ENV_READY：Linux x64、PK38.0.150、PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`；存在与可调用分层，后续真实 PK 调用另有成功证据。独立脚本均 .NET10 C#，通过项目 PskernelSharp／Using／Host，调用 GM 的托管项目；没有手写 P/Invoke、ABI 结构体或重复 session 初始化。所有 dotnet 命令禁 node reuse，只清理本次自有 PGID。cleanup 原1／No such process 表示自有组已自然消失，未改写子进程原退出码，也未全局 pkill。

必要 q2 定向 **81/81、raw0、0 skip**：新增证明 gate、原 explicit spine、Section root memo、surface/iso plane roots、Blend intersection、RollingSection adaptive tails、Blend projection／nearest。构建运行原 allocation IL guard，3081 methods CLEAN；不重跑 Mac 完整套件、185 全组、八 family、矩阵或性能 ABBA。

新增独立解析场控制为 plane／plane 的原 line spine、正角／小角 .001／零角／负角、正反截面、平移／反射／尺度，以及 circle spine＋sphere／plane 的非恒定法向。覆盖固定 u/v、原 CHART 中间点／piece 边界、跨 piece 与原数学外延；解析公式样点同时核位置／自由导数，浮点解析公式的比较留 `2e-15` 计算舍入余量，**不是生产容差或数学证明放宽**。关键自由导数与旧 frame 路径逐位核，新包围热调用零托管分配。无证明、fixed/free 越域、offset、sweep、parameter-line、重复 iso 与不支持源均拒 admission。

完整有限 plane 图在新包围 on/off × Section memo on/off 四种组合均为 **9 节点／8 边、remaining32749**；native V 根为 1/3、U 在 [2,4]，memo 实际7命中。开启新包围时有609／840次控制 hook admission；它们仅属于该制造几何及测试制品，不能外推普通产品命中率。独立 immutable cache 控制实际命中 kind5，二维、generation、sense、tag 错配不能消费该记录；另一轴 kind6 由实际编码及两轴导数控制核实，不增加普通制品计数。

真实 PK 重新 receive 三对原封存产品：curved 单操作、NURBS 单操作、shortedge lifecycle step4，各实际 GM/PK body 均 full BODY_check 零 fault，当前 strict compare 通过，保留 global/local、face/entity 与仅允许的 n_cht_pts 诊断。`max_diffs>0 / all_tests=false / acc_dev_tests=false / tol_dev_tests=true` 未放宽，释放原 compare 结果；face_pairs 不是成功匹配面总数。

另一次当前普通托管源 **fresh NURBS 完整消费者**实际执行创建／Fix／transmit／真实 PK receive／compare：GM/PK API0、16pass／0fail／13missing、captureErrors空，原 runner2。它属于 Linux 执行，不进入 Mac 计时，也不把测试 hook 制品冒充普通 producer。

| Mac 原质量组 | pass / fail / missing | 原 exit／范围 |
| --- | --- | --- |
| curved 单操作 | 14 / 0 / 13 | 2，step1 |
| NURBS 单操作 | 16 / 0 / 13 | 2，step1 |
| standard shortedge 开发 | 210 / 0 / 102 | 2，原六 fresh pairs，最终成品 rep5 |
| rigid＋scale .125 | 51 / 0 / 1 | 0，仍有 missing |
| mirror8 | 29 / 0 / 23 | 2 |
| shortedge lifecycle | 133 / 0 / 77 | 2，原五步分别保留 |
| NURBS lifecycle | 16 / 0 / 13 | 2 |
| curved editable reload | 17 / 0 / 26 | 2 |

八组 captureErrors 均空，55 份原 strict 与29份 mass 报告逐核，七 raw2 原样。Fix、copy 删源、反射／缩放、两次 Boolean、自己 XT reload／cleanup按实际步骤分列；下游失败不能反判已独立通过的上游，除非有直接违约证据，本次未发现此证据。

原完整 TRX SHA256 `db8768aa7dbf3a7cb22b8b84cf05c25015a447f75438ba83b1d25d057af92b48`：2029执行／2029pass／0fail／0skip，原 build0／test0，自然5m37。前后三 DLL＋三 PDB 每个文件 hash 相同，并与封存文件一致；两张清单字段数量不同不妨碍逐文件身份核实。原 focused185／raw0／24s复用；首次缺 IntervalDual derivative 参数编译1，未运行测试，原失败独立保留。

10 组质量 PE／portable PDB 的链接、MVID、hash 与固定 Git 源文档 checksum 逐核：普通／testhooks Native各336 tracked、Tests169，三组源集合；generated／unmapped单列。Mac PE只解析元数据，未加载冒充q2。普通质量 MVID `2188a30e-4c84-45b0-9e37-92250f26f185`／DLL SHA `9468a7ba91b82a975be8b748e3cf5ce7ad1dd9ac430fb234fd69b2ad0d1ac389`；testhooks `46b93efb-b8b6-4d8b-a01c-9bea4260694c`／`dfb6f258b82bd970db84564441e174812302d7adce184ea0cf6aeea6b558443d`；Tests `dad07d57-e24e-489d-b281-79289bad72e2`。producer actual-loaded、套件 sealed 文件和前后清单分别论证，不补造测试 loader 或 start/end 见证。

## 唯一 Mac 窗口与材料边界

UTC **2026-11-12T08:03:04.2462780Z–08:05:02.4967460Z**；Apple M4／macOS27.2 arm64／SDK10.0.400／runtime10.0.11／PK38.0.150。十一份 helpers／runner 两来源字节相同，十个 managed-only 预构建 raw0；五操作串行 ABBA×每进程3 fresh GM/PK pairs＝20进程／60对／120 API 全部0。每 source/op 两个 rep0 冷、四个 rep1/2 暖；16 raw2、4 planar raw0、runner2／performanceQualified=false保留。

| 操作 | 基线暖 GM 中位 ms | 候选暖 GM 中位 ms | 变化 | 候选暖 min–max ms | paired GM/PK 中位 | GM 中位／PK 中位 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| sphere | .08760 | .07970 | −9.018% | .0765–.0822 | .23972 | .23894 |
| planar | .77580 | .78520 | **+1.212%** | .5548–.9950 | .74596 | .77234 |
| curved | 44.91595 | 44.01035 | −2.016% | 39.7907–45.5108 | 21.76082 | 21.74586 |
| NURBS | 504.95495 | 501.66250 | −.652% | 470.1583–537.8105 | 283.79150 | 279.22105 |
| shortedge | 517.77570 | 448.23975 | **−13.430%** | 397.0935–720.1391 | 197.97848 | 198.10388 |

所有 GM/PK 冷暖点、配对倍率与 ratio-of-medians由原 Capture 独算，完整值在 `docs/isoparam-independent/receipt.json`，没有选取暖最优点或删掉 planar 回退。60rep 的 PROFILE／WORK 与96行 ROOT_WORK逐原行／行号／字段、日志／Capture SHA和实际 capture step/API0核实；F3 profile时间只按其舍入精度比较。原聚合 SSI／根／winding／budget 工作量可与基线比较，但不能反推新分支的实际命中率、排他 CPU 或删除一遍导数对13.430%的贡献。普通制品没有新增这些分支计数，本审查未补造。

开发 shortedge 六 pairs 的暖中位506.9494→437.0756（−13.783%）属于另一原执行，首暖候选更慢、冷点与PK点都保留；不能用它替代正式窗口、改成 rep2 或给正式13.430%增加统计资格。正式数据不乘历史倍率，不签普遍／因果收益。原20个 host 观察load4.52–6.35、pmset状态与 powermetrics 原失败保留；无数值温度或恒温／排他 CPU资格。

20份实际 loaded Native／Oracle、封存 PE/PDB、原 source manifest 全部核实：baseline Native335、candidate336 tracked，10430行源 manifest逐 Git 字节 checksum核查；generated／unmapped单列。baseline MVID `a01cccf4-89e7-495f-b217-fbda6c2e9761`／SHA `f467bc2b50265aaec66fbaeea30af3e892e0206265cee7b2907b052c114f492c`；candidate `62018b4e-94ed-4772-ab31-e4c6688e7c5f`／`f840039a1b300dd17ecc1d05469263b4166814638e4b626fd3833ec7f53667db`。它们与普通质量／testhooks制品分开，metadata 与计算源 Native tree 相同另由实际 Git tree 核实。

原72份 mass 报告每份15分量独立按**原1e-7**公式复算：主60报告900＝312Verified／588Unresolved，额外12报告180＝84Verified／96Unresolved，总1080＝396V／684U／0D。144份 strict报告保留原 mask；报告数量、状态出现次数与分量数量不混算。审计器0不洗 producer2，不借已接受 ab597新 norm回填原数据；用户可以研究或调整标准，但本审查没有签“全数值通过”。

312 实际 mesh／35,389,380B及64最终 XT逐hash、source／step／tag绑定；完整绑定在 `docs/isoparam-independent/material-bindings.json`。真实GM输出经私有 PK snapshot显示，snapshot不等于独立PK重建。204份 proof有原source绑定，24份 PK-product→GMreceive 控制无对应显示 mesh，12份原source binding缺失保留；curved原roles／tracking缺失不补造。shortedge的输入是PKseed→GMreceive→GMFix，公共seed／input不标rep2；同名最终产品按原覆盖行为归rep2，质量开发六pair仍rep5。显示和材料绑定不授予产品额外几何或性能资格。

原 report SHA256 `6cedd40434fa16b26d4badd90a7e984ae6d2a1bcc8fc0d58c831e54304bbe7ea`，material SHA256 `f31d2f751cdff6c82b8454076f0c61794a7b3757198c4c5bea781c194b3d7f61`，独立核同。

## 原件、派生物与交付

两包安全隔离展开并逐成员重新哈希：quality **18,760,675B／529成员／展开71,158,542B**，SHA `335a733b28a6875f9522a786cfb4f03af2cfdbd31bf15ccbafe5885d21d5ed0f`；controlled **33,975,610B／1037成员／展开137,674,519B**，SHA `bb03d55d39335688c01003c3ec94a7dd61c5c809fb34803265c79675db0f6197`。拒绝越界、重复成员、反斜线／驱动器路径与 symlink，未修改原包、原报告、原测量或厂商文件。

独立入口：`scripts/ReviewIsoparametricEvidence.cs`、`scripts/ReviewIsoparametricMeasurement.cs`、`scripts/ReviewIsoparametricControls.cs`、`scripts/ReviewIsoparametricRuntime.cs`；派生收据在 `docs/isoparam-independent/`，私有原件及全部执行日志在 `temp_docs/independent-isoparam-e7f0a48/` 与 `temp_docs/isoparam-review-*.log`。没有厂商库、凭证或反编译正文进入提交。

独验自身失败尝试均保留：首 extraction helper 等号笔误编译1；首 quality helper误猜 loaded字段名原134；首 controls 在 Host设置schema之前启动托管session原134；aggregate首次局部变量重名编译1、第二次误要求前后清单所有字段相同原134。分别修正独验脚本后，真实候选控制、原件审计和最终 aggregate成功；这些失败不是作者建模失败，也未用成功审计器补签原raw2。没有改变作者生产代码、测试期望、预算或原数值规则。

无 C ABI 变化，未运行 NativeAOT；未做 Windows／Mac 构建测速、fullsuite／八family／新 ABBA、门户／UI审查或下一优化。当前任务完成于限定独立接受；保留的数值 blocked、性能资格 blocked和更大目标 unproven如上，不接管后续实现。
