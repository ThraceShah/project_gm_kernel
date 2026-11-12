# 通用齐次 Bezier restriction 独立验收

结论：固定交付 `271af03cfefedef12eea19337f1b2b3bca9fe96b` 的 Native 变换、连续外包围、资源边界及关键集成 **accepted（限定范围）**；fixture 修正和原质量执行的分层记录 **accepted**；唯一窗口 **accepted 为描述性观察**。原数值全部认证和完整性能资格仍 **blocked**，普遍统计收益、因果／排他 CPU 收益、整体 PK 性能目标及 ab＋a908 组合 **unproven**。未发现需要修改作者生产实现的新增算法阻断；本报告不授权下一优化、main、UI 或发布。

## 固定源码与实际约束

实际读取了用户级 AGENTS 文件、当前树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、1065→ccc→0cc→54c→271 的真实 diff、调用者和三份作者报告。用户全局 marker 正文 SHA256 实核为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`，仅证明本次实际读取，不声称运行时自动加载。两架构和项目约束相对 1065 字节未变。

| 身份 | 固定值 |
|---|---|
| 已接受基线交付／计算 | `1065e7c8f818ce10f6d3ee3edfe8f8bc33f3d582` ／ `ab0177ed2cfb6fab1b893c81562a0364fc487176` |
| 基线 Native | `90573c1c23eaecebd931c5e08f259a20fedc1930` |
| 候选生产计算 | `cccaa5e2943f8f2515dd01b02c31492378502040` |
| fixture 修正 | `0cc4363d82879392a92dd06a405923812b34695c` |
| 质量／实际窗口 metadata | `54cefeec76f7a61f264140e03c3bd13b695347c6` |
| 候选各步 Native／src | `4f4f92e065fea03bf7384b9a80fa6836326e6322` ／ `5f4b4afea064d457af9d09b3f39d7f420e519db6` |

已实际 `git show` 读取 `80a94062032eca8e257b559dda6d0eb5f8908e38` 的 `docs/Local_Spline_Jet_Independent_Review.md`，复用其接受边界，不重签 e7、Section 或其他先前结果。当前 Native 相对 1065 唯一生产改动是 `src/ProjectGmKernel.Native/Geometry/Bounds/BezierSurfaceBounds.cs` 的 44 行插入；其余原函数未改。测试仅新增 restriction 控制和修正并发 fixture。实际源码中的 projection、完整 spline jet、Jacobian、缓存、拓扑发布及回滚代码均未叠 a908；另一分支 `294cb5ea91ba7384c6fee54aebec406239cb4418` 的限定接受不能当本包的组合验收。移动 query／norm、c10／090／9d／dd／b095 的拒绝实现及父网 assessment 没有通过本 diff 带入。

本树先执行统一私有环境 ensure，实际 raw0／`ENV_READY`，Linux x64／PK38.0，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`。本次独立运行使用 Linux SDK10.0.302／runtime10.0.10，与原 Mac SDK10.0.400／runtime10.0.11 分开。未执行 NativeAOT、Windows 唤醒、Mac fullsuite／family／ABBA 重跑；没有改动作者生产、原测试期望、原报告、ZIP、清单、日志、XT 或测量。未使用全局 pkill，没有需要清理的本次 testhost；进程全部自然结束。

## 连续数学及条件数：accepted

令原齐次 Bezier 多项式为 `H(t)=Σ P_i B_i^p(t)`，其对称多仿射 polar form 为 `F(t₁,…,tₚ)`。原限制后的第 k 个系数唯一等于 `F(a 重复 p−k 次，b 重复 k 次)`；因此定义覆盖零宽、相交的不确定端点和数学外延，不靠有限采样定义几何。

第一路真实 `b≥1/2`。第一次左 split 的系数为 `L_j=F(0^(p−j),b^j)`，表示 `G(s)=H(bs)`。第二次取其 `[a/b,1]` 右网，得到 `G(a/b+(1−a/b)s)=H(a+(b−a)s)`。第二路真实 `1−a≥1/2`。第一次右网表示 `G(s)=H(a+(1−a)s)`，第二次在 `t=(b−a)/(1−a)` 取左网，仍表示 `H(a+(b−a)s)`。由 Bernstein 基的唯一性，两路所有系数均为上述同一个 polar 值。恒等式不要求 a、b 分离或严格有序，也不要求二次 split 的浮点区间完全落在 [0,1]。

对任意从原输入系数区间及端点区间选出的真实值，gate 用完整 outward 区间验 `a,b⊆[0,1]`，并用 `b.Lower≥.5` 或 `(1−a).Lower≥.5` 选非零除数，而非 midpoint。真实比值属于实际 interval 除法结果。每一 de Casteljau 层的 `(1−t)P+tQ` 均沿原 `IntervalNumber` 外向算术，故从输入包含、逐层归纳到左右边界、再到第二网，始终包含这组真实 polar 系数。把相关的 a、b、比值当独立区间会扩大，不能排除真实组合；因此对所有选值成立，不是“新旧宽盒相交”或一组点值证明。比值略超 [0,1] 时源码没有 clamp；线性运算恒等式仍成立。

原 `IntervalNumber` 点值精确捷径、FMA 门及 outward 运算未改。此两路除数落在离零安全区，倒数本身不涉及极小除数溢出。极大齐次量、抵消、subnormal 或高次依赖仍可能使盒更宽／非有限，不能从离零门推导任意条件数或总成功保证；输出不能通过原 finite／正权／消费者证明时仍按失败处理。接受的是正确包含和原失败合同，不是新旧域内端点逐位相同或全输入皆紧。

degree0／1、gate 外及两个除数均未获准的情况进入当前同一定义的一般 blossom。实际 diff 只给原函数改名并在前面分派，循环、顺序、算术逐字不变；这不是旧 API／格式兼容层。实际 production 调用的 degree 来自原最高 16 的提取或其降阶导数网，未扩大私有函数的有效次数合同。

独立入口 `scripts/ReviewBezierRestrictionControls.cs` 通过真实 private 方法及实际 `IntervalNumber` delegate 直接调用普通 Native。参考系数用 BigInteger 精确有理数计算 `∏(1−tᵣ+tᵣ z)` 的 z 系数与原控制系数的点积，没有复制两次 split 或用旧浮点 blossom 当数学 oracle：

- p0／1／2／3／8／16，尺度 2^-1070、2^-500、1、2^500，两个除数选择、0／1、零宽、相邻浮点端点、不确定系数／端点、端点区间重叠、反向参数和一般 conditioning 路径，共 114,048 次精确系数包含控制通过。
- 192 组 degree0／1、一般条件及域外路径与原一般 blossom 端点逐位相同。域内新网没有要求逐位旧网。
- 原生域 `[2,6]×[-3,5]` 上 polynomial／正权 rational 的 10 个全宽／窄宽／零宽／相邻浮点盒，独立用显式 Bernstein power 多项式导数及精确商法则复算完整矩形 27 分量，2,430 次精确分量包含控制通过，同时核对 3／9／18 布局；非正权和非有限位置真实拒绝。

这些有限控制用于发现实现、索引、舍入边界错误；连续结论来自前述恒等式和 interval 归纳，未把点控制扩大为完整连续域证明。

## 源资源、拥有关系及关键消费者：accepted

SplitCurve 在写任何输出前先复制完整 `4(p+1)` 活跃输入到自己的 work；每轮 j 递增，更新 j 只覆盖下一步不再需要的低位，右邻仍为上层值。左网依次写 `r`，右网依次写 `p−r`，端点和每一 pole 均被写一次，无默认初始化 read。第二次调用以同一 output 为 input 安全；独立 432 组完全／前后偏移部分重叠控制逐位同独立输出，输出活跃范围之外原字节不变。这仅接受新 split 分支的重叠性质，未给原 general blossom 追加别名合同。原 surface caller 的 input／result／layer 分离，活跃长度由每轴次数决定，未读上一次长轴留下的尾部。

原 general 插值数 `p(p+1)²/2`，新两 split 合计 `p(p+1)`。每个 split 最大 68 个 IntervalNumber，即 1,088B work；第一个调用返回后第二个调用才分配，两个 work 不同时存活。实际 Linux Release FullOpts JIT 检查显示独立 callee、单个动态 localloc、第一 split 普通 call、第二 split tail jump，general 也 tail jump；固定 wrapper frame 为六次 push＋56B，SplitCurve 为六次 push＋104B，general 为六次 push＋136B。第一 split 的 wrapper＋callee 帧确有额外调用开销；所以“不增 scratch”只指活跃 net/work 上限，不声称总机器栈字节与旧实现完全相同。此观察是本次 Linux JIT，不外推 Mac 或 NativeAOT；实际 disassembly 保存在独验临时日志。p16 热 1,000 次真实 restriction 为 **0 managed bytes**，不外推完整 API 分配量。

未新增生产 heap／global cache、扩大 solver scratch、改变持久 geometry／owner 或原生 UV。`Restrict` 每轴仍独立处理同一原控制网；`HomogeneousDerivativeHull` 和 `HomogeneousJetHull` 仍先对原网求导、按原 native knot 跨度缩放，再独立 restriction，包含零宽和极窄盒。没有从裁后网差分除以极窄宽度冒充原导数。Cartesian 转换仍先验正权，完整一／二阶和矩形混合 jet、位置 finite 及上层 finite 门保留。

实际读过 BSplineProjection、NestedBlendIntervalSupport／Bounds／JacobianExpressions、RollingBallReach、OffsetBezierBounds、SurfaceIntersectionPatch 的相关调用。projection 使用保守 Cartesian hull 与 norm≤1 的 support plane 下界，全局距离 gap、200,000 访问上限、128 pending 和窄盒失败均未改；独立实际完整 Closest 调用仍闭合原 1e-9 gap，已知最小距离 .9375／native UV 正确。完整 Jacobian／preconditioned remainder、严格 Krawczyk inclusion、contraction<.9、六次 expansion／六次 contraction、nearest Hessian 正性、reach、frame、CHART、witness、SSI root／预算及 rollback 的原接受门未删减。原 focused195 包含基线187的矩阵、nearest、失败／预算、readset 和消费者控制，其真实制品绑定核实后复用，未重跑整矩阵堆检查。

## Parasolid 原生语义与真实产品控制：accepted

实际读取 parasolid-research、parasolid-docs；官方 FD 18 的曲面参数映射、19 的 B-geometry／knots／原参数区间／重参数化／有限 portion 与 106.7 的比较语义已核。实际 q2 MCP 调用查得 PK_BSURF_create、PK_SURF_eval 和 PK_DEBUG_BODY_compare；同一门户 HTTP MCP 的 PK_SURF_eval 也真实 `found:true`。默认代理下首次 curl raw35／TLS 失败，直接 tailnet 请求 raw0 成功；未把可达性当动态建模证明。误查 PK_BSURF_split 为 not found，随后查询实际 B-surface API，未从空结果推论没有内部调用。没有把 Linux 静态地址用于 Mac，也未公开反编译正文。

通过 PKToy PskernelSharp＋Using＋ParasolidScriptHost 实际启动 **PK38.0.150**。独立建立 polynomial／正权 rational 两片 native B-surface，域 `[2,5]×[-3,1]`、真实非均匀 knot3.25，每组 6 个端点／两侧 span native UV 站位，完整 3×3 点／导数与 GM 一致，最大绝对残差分别 `3.3306690738754696e-16`／`4.440892098500626e-16`。同时检查提取后原矩形 jet 包围；PK 点比较的 1e-11 仅容纳 oracle 点值舍入，精确有理数控制不加 slack，生产容差未改。原 knot/domain 与有限盒仅用于原多项式求界，未改几何或另造参数化再转 XT。

另对原 sealed **GM 产品 XT** 与独立 **PK reference XT** 三对：curved step0、NURBS 开发最终 step5、shortedge lifecycle 最终 step4，逐 source mesh 的 XT hash 取真原件；各自 receive，full BODY_check 共六 body 均0 faults，再执行当前严格 AssertEquivalent，三对通过。前两对各4条 `n_cht_pts` 为唯一诊断，shortedge 无 local diff。保留 max_diffs=1024、all_tests=false、acc_dev_tests=false、tol_dev_tests=true 及数量／类型／sense／容差／missing／matching；调用0没有被当作相同，face-pair 记录数没有被当匹配总数。helper 既有 curve-sense 的逐项有限方向证明分支本次未使用，也未放宽其他 local/global 差异。比较器原有错误尺寸／平移／裁剪 wire 等负例及等价升阶／插结正例源码未改，沿用原已接受 helper 边界，未重签历史负例执行。

本次没有再造 fresh family 成品或进入 Mac 重跑；新的完整主要消费者控制为上述全局 Closest，真实实体集成使用三对原 sealed 成品。原 GM 产品、GM XT 的 PK snapshot 和独立 PK reference 没有混用。

## fixture 修正及原质量执行：accepted，全部数值认证 blocked

初次完整执行真实 **2038pass／1fail／0skip，build0／test1，2m59**；TRX SHA `1f363f5772a36f6e5d4a7d4834fdfee34fadc043f92532f183db745f21756b68`。失败是 `IcurveConsolidatedReviewFixTests.Cache_ConcurrentPublishLookupEviction_NeverTearsPayload` line116 的 hits>0 为0；未报告 tear，不能把这一点改写成该次未发生任何竞争或全部通过。

已实际读生产 GeometryEvaluationCache：Publish／TryGet 的 entry copy、写入和 CLOCK 操作均在原短 SpinLock 内，payload按值拥有；与 restriction 无调用依赖。原 fixture 空 cache、Task.Run 无共启，线程池完全允许所有 reader 在任何 publisher 运行前结束；assembly 串行不消除同一测试内部调度。故初次失败可以由 fixture 活性不足产生，未提供候选 restriction 或生产 cache 损坏的直接证据。

0cc 修正仅预填64个合法 g=0 sample、预检查 hit，再以3 writer＋3 reader 的 LongRunning／Barrier 共启；每 worker6000、64key、每 generation200 的 epoch bump、payload 的全部原断言、hits>0、violations==0 均保留。它改善启动条件，仍真实调用竞争 lookup／publish 和周期 stale-epoch overwrite；但 prefill 的 g=0 hit 本身即可满足 hits>0，Barrier 也不证明原 fullsuite 中每次实际读写相交。原件没有 per-phase 数据，不能补签此观察。64key<256 不足以声称这个测试触发容量 eviction；原另一个 churn／clear fixture 独立存在。

最少独立并发控制保留同一6000／64／epoch／payload形状，并额外观察：18,000 publish、90 epoch bump、2,483 hits，其中2,454为非预填 positive generation，2,454落在 writer 活跃生命周期内，0 violations。这是一次新的并发观察，不是原运行的回填或机器指令同时执行证明；短锁源码提供 payload 不撕裂的同步依据。

原窄控制 **2/2／raw0／522ms** 后，新完整 **2039/2039／0fail／0skip，build0／test0，2m58**，TRX SHA `72a4c66989a77a4dd35823a8516230f06ba9349c019154f5f76ada54a1c19aa5`。初次和修正完整套件的前后三 DLL／三 PDB 六文件逐项 hash 同封存原件；初次2 PE/PDB按原ccc源码独立核，修正后10 PE/PDB对应普通Native336、hooksNative336、Tests170三source set。ordinary actual-loaded、测试 sealed 与前后文件身份层级分开，未发明测试 loader／start-end 观察。

普通质量 Native MVID `49b23278-1144-4f7f-8083-fe95d6a6d560`／DLL SHA `29907b7d54411e8dd204f4102e77456bb16674b8dd6542acacc3a9f974015930`；fixedhooks `d745f8d6-c058-4522-a8d3-11871fe58e55`／`c5f25edd233d5629e989de9d631917d9d26be9856135bbe358077921f3a21076`；Tests `97291d08-d316-4be1-82e5-68c4ba8fff0c`／`9d25295bf62c1933ea6485bd70e2ebee13291baa86df984088117ae27091b8f6`。不是用后来raw0、窄2或focused195修补原test1。

| 原实际组 | 实际范围 | pass / fail / missing | raw |
|---|---|---:|---:|
| curved | 单操作 | 14 / 0 / 13 | 2 |
| NURBS 开发 | 六 fresh pairs | 96 / 0 / 78 | 2 |
| shortedge | 单操作 | 35 / 0 / 17 | 2 |
| rigid＋scale .125 | 原变换组 | 51 / 0 / 1 | 0 |
| mirror8 | 原镜像组 | 29 / 0 / 23 | 2 |
| shortedge lifecycle | Fix及两条产品各两次Boolean，五step | 133 / 0 / 77 | 2 |
| NURBS lifecycle | 实际单step | 16 / 0 / 13 | 2 |
| curved editable reload | 实际reload step | 17 / 0 / 26 | 2 |

八组 CaptureErrors／fail 均0，七raw2／rigid raw0 的 missing 均保留；copy／删源／反射缩放／imported与自建产品下游 Boolean／editable reload／cleanup 按原 step 分列。下游失败或物性 Unresolved 不反判没有直接违约证据的前序 Fix，也不代表完整组合已数值认证。质量原45 strict／24 mass报告核原mask；360物性分量按原1e-7／PK modulus独算，不能拿group重复分量当独立证书。

质量NURBS复用开发六对，最终同名成品是rep5；正式窗口最终rep2与之分开。原质量包work中首失败阶段的“尚未全通过”残句未重打包，以后段和机器receipt明确记录的fixed2039分层解释。另发现原 `quality-receipt.json` 的 acceptedBaseline 仍写b8/e7，costDiagnosticScope仍写旧b8/c9316c1；实际本次开发／窗口和复制源是ab/1065。该标签残留不被接受为源身份依据，本独验以实际Git、PE/PDB、source清单和original capture核对；原件保持不改，派生收据明确当前基线，不能让消费者仅凭旧标签归因。

## 唯一实际窗口：accepted 为描述，完整性能资格 blocked

实际 UTC **2026-11-12T12:16:04.3265350Z–12:17:58.8091060Z**，M4／macOS27.2／SDK10.0.400／runtime10.0.11／PK38.0.150。11 helpers／runner两固定源字节同，10 managed预构建raw0；唯一串行ABBA20进程×3 fresh pairs＝60对／120 API 全0。每 source/op 两个rep0冷、四个rep1/2暖，**16 producer raw2／4 planar raw0／runner2／performanceQualified=false** 保留。

| 操作 | ab暖GM中位ms | ccc暖GM中位ms | 变化 | ccc暖min–max ms | paired GM/PK中位 | GM中位／PK中位 |
|---|---:|---:|---:|---:|---:|---:|
| sphere | .09585 | .07715 | −19.510% | .0740–.0777 | .28447 | .28760 |
| planar | .60880 | .76870 | +26.265% | .6007–1.0072 | .76122 | .69701 |
| curved | 45.50075 | 48.83760 | +7.334% | 37.6758–50.0874 | 25.07962 | 25.05713 |
| NURBS | 450.53995 | 337.37470 | −25.118% | 231.9371–486.8714 | 212.70038 | 212.90171 |
| shortedge | 536.43630 | 592.62485 | +10.474% | 445.0634–736.2309 | 346.20355 | 344.59942 |

全部冷暖GM／PK、两种倍率、min/max及逐对值从原Capture独算；60rep PROFILE／WORK与96 ROOT_WORK原行、行号、字段、log／Capture SHA及对应step/API0逐核。F3 profile只按其原舍入精度比较，不把rep0当暖、不挑点或跨窗乘gain。四暖小样本不支持普遍统计、因果或排他CPU资格。外层4179Jac／20895directions／8358residual／12537XYZ、root150/8、winding18、requests628／eval452／hit26不计restriction内部算术，不能据不变说没省，也不能签25.118%的单机制贡献。

开发是独立的每源六对／24API0／两raw2：ab冷949.3467、五暖中位441.167；ccc冷1069.7489、首暖488.3386、五暖中位243.3549，观察−44.838%。候选冷与首暖更慢保留，不补充正式统计。20host load3.57–4.26，无数字温度／恒温／排他CPU；原pmset观察和powermetrics失败仍在。56copy只代表当时真实快照，含后增review／readiness，不声称全都已存在a867时；采样线程wall不变成排他CPU或候选计时。

20份 actual-loaded Native／Oracle、PE/PDB/source manifest 10440行核Git原字节，两侧Native各336 tracked，generated／unmapped另列。窗口 baseline MVID `d86e9878-e126-43e8-8f9e-283fc4ed1351`／DLL SHA `a1d038c1f42ba19615dfba4b359aa1bf4247a74bdd17101e28a4478b70244a63`；candidate `b2c007f1-f248-4049-8a75-aebe7d832056`／`75e63c18b22207f2943e86669048ccaa894ef375b83fb4011399e9506aa16101`。质量、测试和窗口制品不互补身份。

主60报告900物性分量312V／588U，另12报告180分量84V／96U，合 **1080＝396V／684U／0Different**，按原1e-7／PK modulus重算；原144 strict报告保留实际mask和结果。未借ab597新norm回填；checker0不洗producer2，全部数值和正式performanceQualified仍blocked。NURBS相对PK仍约213倍（中位数之比），完整PK性能目标未达。

312mesh／35,389,380B与64最终XT逐source／step／tag／hash核实；含公共seed/input，64不等64个rep2成品。204proof有source binding、12缺source，24条PK产品经GM重收再导出控制没有对应显示mesh，缺失保持。GM真产物、GM XT的PKsnapshot及独立PKreference分别绑定，不增加几何或性能资格。report SHA `c9a838b50dbe9c6234f7873e5f831fd6766754f5296b1942b738e796b4db538b`；material SHA `e64ed139b9e18c4f8dd8b281c91ea34c1c4b82f27e6307f4ef7a48b743c1b744`。

首Prebuild错误引用e7目录、声明ab SHA，在build/model前被守卫拒绝raw134；Fixed才完成10build0。首RunWindow读旧prebuild退出，在window目录／host／model前停，只有异常log、没有原rawexit文件，不能补造134；Fixed才产生唯一20process窗口。首cost-copy错旧路径raw134，Fixed核已有部分hash不覆盖再补真实ab文档56files／raw0；这些都不是模型或性能重跑。

## 可交付边界与执行收据

| 项目 | 状态及边界 |
|---|---|
| 连续数学／outward／条件数分派／别名／资源 | accepted；同一原齐次多项式，工作网上限不变，非全输入紧界或总成功保证 |
| 关键Native消费者／真实PK三对产品 | accepted；原门、预算和生命周期保留，有限实际范围 |
| fixture修正／原质量记录 | accepted；首test1仍真，原fullsuite无per-phase观察，metadata旧标签不作身份依据 |
| 唯一窗口 | accepted为描述；全部回退、散布、raw2和U保留 |
| 全数值认证／完整performanceQualified | blocked；原missing、684U与producer2未解决 |
| 普遍统计／因果排他CPU／整体PK目标／ab+a908组合 | unproven；本次未建立该资格 |
| main、UI／门户、上线、下一优化 | 未执行，不在本次交付内 |

两ZIP安全隔离展开，拒绝absolute／dotdot／驱动器／反斜线／重复／symlink／越界成员，逐成员和原清单长度及hash独验：quality **25,308,629B／566成员／展开98,147,963B**，SHA `4fa26c33cff4fbd130c4a598b2fa35b7c9fcf9965e4ed5979c717fb8f4820542`，原清单565项；controlled **34,012,458B／1049成员／展开137,745,912B**，SHA `f4b86e3473b79ed13ee6732d047172a1fc8af806d9dae93bb6c787a1b3491645`，原清单1048项。原包及原清单未改。

独立入口为 `scripts/ReviewBezierRestrictionEvidence.cs`（extract／quality／receipt／executions）、`scripts/ReviewBezierRestrictionMeasurement.cs`（recheck／bindings）、`scripts/ReviewBezierRestrictionControls.cs`（精确polar、完整native jets、PK、并发与stack入口）。公开派生物在 `docs/bezier-restriction-independent/`，私有原件／展开／逐成员hash／运行日志在 `temp_docs/independent-bezier-restriction-cccaa5e2/`；extract首日志在 `temp_docs/restriction-independent-extract-01.log`。脚本为.NET10、相对脚本定位，GM直接托管，PK无手写P/Invoke／ABI／session，也未将PK重定向GM。

独验自身 measurement01／02 真实raw134：先误把正式窗口基线身份当计算ab而非真实1065，后把原failed／fixed prebuild driver也计作10build；仅修独立检查器，第三次raw0。aggregate01真实raw134：同样把开发基线身份当ab，改按真实1065后aggregate02 raw0。日志原样保留；首次未提交脚本没有单独sealed快照，不能补其逐行身份。extract、quality、bindings、controls、Debug与Release stack均各raw0；最终移除测量检查器的重复解包入口，仅对安全展开的原件只读复验，第四次raw0且派生receipt逐字同，没有producer重跑。成功检查不补签任何原producer或此前独验失败。未运行新的完整suite／family／压力矩阵，没有厂商库、凭证或大段反编译进入公开交付。

审查证据已先提交并推送 `d0e5d14e18fee5c4c07dfc604a95af4f949b0727`，随后报告和绑定另存本review分支。最终远端full SHA由交付消息核对；本报告的有限独立接受与已提交／已推送分开，不代表已合main、已上线或原大目标全部完成。
