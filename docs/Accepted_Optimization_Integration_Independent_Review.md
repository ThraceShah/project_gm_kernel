# 已接受组件累计集成 3aff4e2b 独立验收

结论：固定组合的 **Native 契约与关键消费者限定 accepted**，原质量的几何／拓扑／生命周期证据分层 **accepted**，唯一窗口 **accepted 为描述性观察**。全部数值认证 **blocked**（原 Unresolved／missing），完整性能资格 **blocked**（原 producer2／runner2、`performanceQualified=false`）；普遍统计收益、因果／排他 CPU 收益与整体 PK 性能目标 **unproven**。未发现需要作者修改生产实现的新增组合阻断；不接受 ab597 功能分支合流，不代表 main 集成、UI、上线或整体目标完成。

本次是一次固定组合的集中重大集成验收。只新增独立 C# 控制、报告及派生收据；作者生产、测试期望、报告、两 ZIP、清单、XT、日志和窗口全部未改。未从头重审已接受单项、未重跑原 199／完整 suite、八 family 或 Mac 窗口，没有 Windows、AOT、新 capture 五窗、门户工作或下一优化派发。

## 固定身份与实际读取

| 身份 | 固定值 |
|---|---|
| CCC 接受交付 | `271af03cfefedef12eea19337f1b2b3bca9fe96b` |
| CCC 计算／fixture | `cccaa5e2943f8f2515dd01b02c31492378502040` ／ `0cc4363d82879392a92dd06a405923812b34695c` |
| CCC Native | `4f4f92e065fea03bf7384b9a80fa6836326e6322` |
| 干净加入的接受 patch | `a9085ed12bf811cd337bbf2f69964d0cd3957373` |
| 新组合计算 | `3aff4e2b0247bd4f21e74e71d437a33dff1a3e24` |
| 原质量实际执行源 | `b99c218e3592551e9bc73d5c509712871607e8bd` |
| 质量／实际窗口 metadata | `a105eb4854e9f138e409a12bcb041e877f254452` |
| 作者交付起点 | `4026e59c827814e52559ec72d41ff282468e3894` |
| 新组合各步 Native／src | `854d01fec8201646b256acbb8297d3388dd74cdd` ／ `545c317e7c6737b397708b80598e45c73aeb3bf5` |

实际读取用户级 AGENTS 文件、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`，核实后三者与 CCC／作者交付字节相同。用户 marker 正文含尾换行 SHA256 为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`；这是实际文件核查，不声称模型启动时自动加载。

实际读取 CCC271→3aff→b99→a105→4026 diff、三个交付报告、机器收据和调用者。相对 CCC，唯一 Native 改动是 `BSplineProjection.cs` 的 14 插入／1 删除；与 a908 的该生产 patch 稳定 patch-id 同为 `c0620a6d30cb1306aa01f0b783001276ce5c895c`。另加原四 tests 与历史 work 说明，后续仅交付文档／metadata；未带入 20a／c10／090／9d／dd／b095 等拒绝实现或移动 query／norm。没有 C ABI 改动，故不运行 NativeAOT。

固定 `git show` 全文读取并复用：`80a94062032eca8e257b559dda6d0eb5f8908e38:docs/Local_Spline_Jet_Independent_Review.md`、`a17ed4f33ca7ab599ff7f3d0612152cec4688814:docs/Bezier_Restriction_Independent_Review.md`、`294cb5ea91ba7384c6fee54aebec406239cb4418:docs/Projection_Seed_Independent_Review.md`。不重做其 114048 exact、原 187／2039 等覆盖；单项接受不直接充当组合接受。

先执行统一私有环境准备，实际 `ENV_READY`／raw0，PKToy 为 `d820719b6d8510d623972ab9d8efc65edbc312f9`。本次 Linux x64 SDK10.0.302／runtime10.0.10、真实 PK38.0.150，与 Mac 原制品分开；schema／库／fixture 未进入提交。实际使用 parasolid-research、parasolid-docs 与 q2 MCP `PK_SURF_eval`（found:true），并读官方 FD18.3.2.9／106.7，复用固定单项对原生参数、比较及邻近接口的核查；不移用 Linux 地址到 Mac，不以空调用图或服务可达代替实际调用。

## 契约组合：accepted，保持原失败边界

GM 仍以 XT 为原生模型，Parasolid native geometry、UV、owner、precision、类型及拓扑合同不变，没有另造模型或历史兼容层。

CCC 的两次 split 与一般 blossom 限制的是同一原齐次 Bezier 多项式。复用已接受的连续系数包含证明和原外向算术；新网可能比旧网宽，不能要求端点逐位相同，也不能由有限点控制推出连续认证。原位置 Cartesian hull、严格正权／finite、原导数控制网与 native 域缩放、完整 9／18／27 项 jet 消费路径仍在。

a908 只把子盒的 Newton 提议从五个改成中心一个，完整 patch 初始仍五个。每个提议点的 `UpperDistance` 仍取原区间位置上界；原正权 hull、向量 norm≤1 的支持平面下界、`lower >= upper - tolerance` 和全部 pending 域处理完才成功的门不变。累计 200000 cells、128 pending、minwidth1e-13、18 Newton、12 trial 原样。更宽的 bound 与更少的候选可增加细分或触发原失败，不能转成无解或假 success。

全局 distance-gap 不证明唯一 UV、驻点或局部最近点。实际 `NestedSplineEvaluation.Closest` 仍接 `Refine`；stationarity、Hessian 正性、finite、完整 joint nearest、CHART、完整 Jacobian／remainder、严格 Krawczyk inclusion／contraction<.9、reach 和 witness 仍独立执行。公开参数化、距离、曲线适配、圆角 clearance／角点和 nested 编译等实际调用者按原状态映射失败，没有删除下游证明。

ab 的 18 项缓存只保存 `S,Su,Sv,Suu,Suv,Svv` 的 XYZ 包围，不存 dual seed、完整 Jacobian 或 proof。实际 `BoundSegment` 取得固定 program／occurrences／fixedCharts 后新建并清空 stack cache，两处区间 Jacobian 使用同一局部源借用；返回后 cache 消失，下一盒／program 不继承。四 UV 端点 binary64、几何元数据 key、先 Ready=false 再计算及成功发布、standalone 清理、empty read set 隔离仍在。不同投影起点仍重建 snapshot，当前方向逐列重组完整矩阵。recorded 与 conic 准入未因本 patch 扩大，整条 Fix 没有被标 immutable。

资源接受按实际边界：没有新增 production heap／跨 command cache、solver workspace、预算或迭代上限。CCC 不增活跃网／4×17 work 上限，不等于机器总栈字节完全相同；新 split 的 alias 合同不追加给旧 general blossom。热调用的零托管分配观测仅约束所测调用，不能外推整条 API 或 CPU 成本。

## 最少独立交界控制与真实消费者

`scripts/ReviewAcceptedIntegrationControls.cs` 直接引用当前托管 Native；现有 test-hook 用全局 `-p:KernelTestHooks=true` 传给引用项目，未改生产／测试。PK 使用 PskernelSharp＋Using＋ParasolidScriptHost，没有手写 ABI／session 或把 PK oracle 重定向为 GM。

两 restriction 表达 × 两 seed 模式下，对折叠 polynomial 的两个合法等距内点、正 rational 非默认域 `[2,5]×[-3,-1]` 的边界最近点，独立用精确 dyadic 检查已知全局距离平方及 upper−tolerance 闭合；不要求合法 UV 相同。实际成功共八组，热 projection 托管分配均 0；原位置与完整 9／18／27 bound 均成功，27 分量在 native 站位作有限检查。rational 的连续 jet 使用显式可分离有理式及 native 缩放，避免把数值接口的超次数零填充误作连续导数 oracle；1e-12 只作独立点值舍入余量，不改生产容差。

另一个二次双向 bowl 补控制实际进入 descendant Improve，在两表达下均观察到五 seed／中心 seed 搜索次数为 **15／7**，所有四组合仍以原预算关闭同一已知全局 gap，结果见 `final-report-binding.json` 的 `additionalSubdivisionControls`。主入口前两个简单几何本身只需初始五次提议，未冒称它们覆盖了减少子盒候选的分支。

四组合分别保留 degree-zero 过紧容差的 `NumericalFailure`、非正权拒绝；精确 focal 驻点通过 refine 的驻点条件后，原 distance Hessian 仍返回 `Singular`。不把 partial upper 当成功结果。上界与已知全局值的 gap 作精确 dyadic 检查，下界复用已接受合同并核当前真实源码；不是重新证明旧全局 B&B 定理。

从本包真实 sealed NURBS GM 开发成品（rep5）经 GM receive，读取实际 implicit program，两个不同 native 起点各在四组合下消费：完整九项包围 cache-off／on 逐位同；真实 snapshot 的完整 **25 项矩阵**、同 scope 改 UV 盒后与 standalone 全部端点逐位同；不同起点实际 snapshot 不同，每次新建 cache。joint nearest 均成功且 finite，所测热 Bound 均 0 托管分配。以上是当前 Linux hooks 构建，源码无新增 production 分配；不借命中计数归因 Mac API 时间。

真实 PK38.0.150 还对独立构造的相同 B-surface 作有限 range 对照，保留实际公开 GM 成功／失败与 native UV，不以不同等距 UV 判错。三对原产品分别是 curved 单操作 step0、NURBS 开发 step5、shortedge lifecycle step4：真实 GM XT 与独立 PK reference 分别 receive，full BODY_check 均零 fault，当前严格 `AssertEquivalent` 均通过。前两对仅各四条 `n_cht_pts` 诊断，最后一对无 local diff；mask 保留 `max_diffs=1024/all_tests=false/acc_dev_tests=false/tol_dev_tests=true` 及 count/type/sense/tolerance/missing/matching，未用 conditional curve-sense 降级。结果释放，global／local／face／entity 存在派生收据。原质量与此 sealed 消费足够，本次未额外新跑完整建模 producer。

## 原质量、source 与生命周期分层

原关键 **199/199，raw0，17s**；完整 **2043/2043，0fail/0skip，build0/test0，2m18 自然结束**。TRX SHA256 `96012496d7a75f3107c47b148a596a99e9339aa9338232c387cd1fea5feac22b` 实核；原前后三 DLL／三 PDB 六文件逐 hash 同，并与 sealed 文件一致。原 manifest 字段不同导致清单 hash 不同，不代表 binary 变。未给原 test 进程补造 loader 或 start/end 观察。

10 质量 PE／portable-PDB 链、MVID／SHA 及 Git document checksum 实核，普通 Native336、hooks Native336、Tests171 三组 tracked source 分列，generated／unmapped 另列。普通 actual-loaded、测试 sealed／before-after 和窗口身份不能跨 bytes 互补。普通质量实际运行源 b99、计算3aff、窗口a105及交付4026的 Native/src tree 相同，但不以 metadata 短名覆盖 execution／PE source。

| 质量二进制 | MVID | DLL SHA256 |
|---|---|---|
| ordinary Native | `f3bbd545-66c6-4050-b3c9-db6b9f28f891` | `9ffb10635057a332bede715d63ae98d79f9702130d48009baf007b8c33729b3b` |
| hooks Native | `e3f58a66-7205-4a0c-aecc-d18568b678ef` | `7e8ddce5b7b857b6a3b380c353385b31416c927a436654832e8e4947a87d8f3c` |
| Tests | `9b4ab535-1474-4deb-b595-e02f2e05de58` | `df104739f2b9b946b9a241322118e834b42142dfea6638512c3531e646a6a0ed` |

| 原质量组 | 实际范围 | pass / fail / missing | raw |
|---|---|---:|---:|
| curved | 单操作 | 14 / 0 / 13 | 2 |
| NURBS 开发 | 六 fresh pairs | 96 / 0 / 78 | 2 |
| shortedge | 单操作 | 35 / 0 / 17 | 2 |
| rigid＋scale .125 | 原变换组 | 51 / 0 / 1 | 0 |
| mirror8 | 原镜像组 | 29 / 0 / 23 | 2 |
| shortedge lifecycle | Fix 与两路径各两 Boolean，五 step | 133 / 0 / 77 | 2 |
| NURBS lifecycle | 单 step | 16 / 0 / 13 | 2 |
| curved editable reload | 实际 reload step | 17 / 0 / 26 | 2 |

八组 hardfail／CaptureErrors 均 0；七 raw2／rigid0 与所有 missing 原样。45 strict／24 mass 原报告核原 mask／结果，360 物性分量按旧1e-7／PK modulus 独算。Fix、copy 删源、反射／缩放、两条真实产品的两次 Boolean、editable reload／cleanup 按实际 step 独立；不以物性 U 或下游失败反判无直接违约证据的前序几何。

继承 fixture0cc，原 payload／hits／6000／64／epoch 门未改；prefill＋Barrier 不证明本次每阶段竞争或 capacity eviction。CCC 首 test1、fixed2039、历史独验 positive-generation 不回填本次2043。CCC 原 acceptedBaseline／costDiagnosticScope 标签勘误按固定3504c4f3读取，原件不改；本组合新 receipt 直接指 CCC271／Native4f／reviewa17、no combined profile，不沿旧 b8／c931 或移用旧 PC／trace。

## 唯一正式窗口：描述 accepted，性能资格 blocked

唯一 UTC **2026-11-12T13:15:25.2643960Z–13:17:20.4729820Z**，M4／macOS27.2 arm64／SDK10.0.400／runtime10.0.11／PK38.0.150。11 helpers／runner 两固定源字节同，10 managed prebuild 原0；串行 ABBA20进程×3 fresh＝60pairs／120API全0。每 source/op 两 rep0 冷、四 rep1/2 暖，**16原raw2／4planar0／runner2／performanceQualified=false** 不变。

| 操作 | CCC 暖 GM 中位ms | 组合暖 GM 中位ms | 变化 | 组合暖 min–max ms | paired GM/PK中位 | GM中位/PK中位 |
|---|---:|---:|---:|---:|---:|---:|
| sphere | .07525 | .08665 | +15.150% | .0787–.1061 | .30192 | .29895 |
| planar | .98505 | .60650 | −38.430% | .6012–.9791 | .73490 | .58116 |
| curved | 42.77295 | 44.91265 | +5.002% | 36.9635–51.9530 | 22.92057 | 22.95268 |
| NURBS | 360.74625 | 268.38920 | −25.602% | 174.5759–521.1778 | 155.96149 | 134.23151 |
| shortedge | 427.20620 | 540.65170 | +26.555% | 351.5903–683.6718 | 297.20463 | 312.88619 |

全部冷暖 GM／PK、min/max、逐对比值及两种倍率从 Capture 独算。60rep PROFILE／WORK 与96 ROOT_WORK 原行号、字段、log／Capture SHA、实际 step/API0逐核，F3 profile 按原舍入精度。没有 rep0 代暖、挑点、跨窗扩样本或相乘单项 gain。所有回退与散布保留；四暖不签普遍统计／因果 CPU，NURBS 仍约134倍、shortedge313倍 PK（中位数之比），整体目标未达。

开发另每 source 六 pairs／24API0／两 raw2。CCC 冷711.3382，五暖323.0306／294.4982／282.1259／270.8615／277.5885；组合冷975.7755，五暖439.5336／173.1452／169.7794／183.4591／199.4341。暖中位282.1259→183.4591／−34.973%，组合冷及首暖更慢，全 PK 点保留；不扩正式四暖样本。开发最终同名成品 rep5 不标为 formal rep2。

20 window actual-loaded Native／Oracle、PE/PDB/source 与原 clean 快照／Git 字节核同，10440 manifest 行、两侧 Native 各336 tracked，generated／unmapped另列。baseline MVID `798ab64f-0f4c-4908-bb16-c121eb1d8c1e`／DLL `9ff8f9cfc765d7cd91e4d953d5cae253c0afb773e4af46d7c1121c800ce8abf9`；candidate `3d6e1841-be60-4b61-8b95-20f64a26a33b`／`094e326994211b7acc239fc2c6f524693ddcebe5d320b2f952b212fa857e4778`。不以另一阶段制品补身份。

主900物性分量312V／588U，另180为84V／96U，合 **1080＝396V／684U／0Different**，旧1e-7／PK modulus 独算，144 strict 保留原 mask／结果。group 重复分量不是新独立证书；ab597 新 norm 未进入该执行，未回填。checker0 不洗 producer2，故数值全认证及完整 performanceQualified 仍 blocked。

20host load3.94–4.41，无数字温度／恒温／排他 CPU 证据。实际 pmset raw0 只说未记录热／性能告警，powermetrics raw1（需 superuser）原样，不把 pmset 当温度；本组合没有新 profile，不移用另 PID 的旧方法地址或 inclusive wall 作 CPU 成本。

312mesh／35,389,380B、64最终XT逐 source／step／tag／hash 核同，GM live、GM XT经PK snapshot、独立PK reference 分开。公共seed/input不贴rep2，64文件不等64个rep2产品；curved原 roles/source/provenance缺口不造。204proof有source binding、12缺source保留；24条PK产品经GMreceive控制无对应显示mesh，不能补造。显示绑定不增加正确性或性能资格。

原 report SHA256 `cb958f38a7c24d1890c9b76e64ddf4a9f4ab4aad4b0bd77b151d446bfd5999b2`，material `a621f72a70bf040bb9e61bdf03fa0a22f20cea56f9770806d2da6beaa6ebdca3`。

## 原件、独验失败与交付边界

两 ZIP 拒绝绝对路径、`.`／`..`、反斜杠、驱动器、重复、symlink及越界后隔离展开，逐成员长度／hash核实；原 manifest 分别468／1043项逐项核。quality **18,552,422B／469成员／展开69,933,277B**，SHA `c2b9e802c32217c9cc3392c108b0bc2684a5df804a46ac57e8c7763777f9e556`；controlled **34,012,773B／1044成员／展开137,748,725B**，SHA `dbcb4708a9c79e941144deff9e6a55b277f65a72415dab1f874bbda6f47379b6`。原件在 `temp_docs/incoming/`，本次展开／日志／私有收据在 `temp_docs/independent-accepted-integration-3aff4e2b/`。

独验自身失败不洗绿：quality首轮raw134误把计算SHA要求用于实际b99产品；controls首／第三次build1是引用项目hooks属性未传，误启的第二个并行build只向自身PGID发TERM／raw143，未执行建模；第四／第五次raw134是独立点oracle错误比较超degree的数值零填充与连续有理导数，按显式有理式修正后第六次raw0。首补充控制raw134因单向bowl在Improve前已可裁剪，改双向bowl才覆盖真实descendant。未改作者实现、预算、容差或期望；失败中间脚本没有逐版封存源码，不补造来源。

extract、quality第二次、measurement、bindings、controls第六次、aggregate、最终真实subdivision检查各raw0，均与原producer状态分开。所有dotnet禁用node reuse；本次不运行testhost，也不全局pkill。脚本的诊断分配与反射不作为生产AOT／零分配修改；生产路径仍以源码和热调用边界判断。

独立入口：`scripts/ReviewAcceptedIntegrationEvidence.cs`（extract／quality／receipt／final）、`scripts/ReviewAcceptedIntegrationMeasurement.cs`（recheck／bindings）、`scripts/ReviewAcceptedIntegrationControls.cs`（主控制及`--subdivision`）。公开派生物在 `docs/accepted-integration-independent/`：`receipt.json`、`material-bindings.json`、`final-report-binding.json`，最后一份绑定当前入口／报告hash、独验原退出与补控制。

首笔验证进展 `7d4b1bdcb1f22e57d4949ac87ac46de71b6f610d` 已推送并核远端同SHA；最终报告／控制／收据随后提交，最终 full SHA 以交接与远端复核为准。交付分支 `review/native-accepted-integration-3aff4e2b`；不合 main、不上线。此固定组合审查结束，不自动串接下一轮微优化→suite→窗口，也不让两条转向PK整链结构工作的研发主线等待本审查或门户。
