# 局部完整 spline jet ab0177e 独立验收

结论：**限定 accepted**。数学替换、实际源与借用存储生命周期、Native 关键集成及原记录范围内的几何／拓扑／生命周期接受；唯一正式窗口接受为描述性观察。**全部数值认证 blocked**（原 Unresolved／missing），**正式性能资格 blocked**（原 raw2、`performanceQualified=false`）；普遍或因果加速、排他 CPU 收益与整体 PK 目标 **unproven**。本验收不构成 main 集成、发布或整体建模目标完成。

本次只新增独立 C# 入口、报告和派生收据，作者生产代码、测试期望、原报告／ZIP／日志／测量／XT 未改。未审 UI 或门户，未接管下一优化，未重跑 Mac 全套、八 family 或 ABBA，未使用 Windows。

## 固定身份与依据

| 身份 | 固定值 |
|---|---|
| 计算 | `ab0177ed2cfb6fab1b893c81562a0364fc487176` |
| Native tree | `90573c1c23eaecebd931c5e08f259a20fedc1930` |
| src tree | `232988716489967a9ce581b2bad8fa5756c64a83` |
| 完整质量／窗口 metadata | `85a595edbdd266e9a8b593086bd5535d9e16f375` |
| 作者交付起点 | `1065e7c8f818ce10f6d3ee3edfe8f8bc33f3d582` |
| 接受基线 | `b8c74b95047577e52d1086b48b0b0a88ee3f4388`／计算 `e7f0a4876ef296b56f5f7c57a45f15617eac6918`／Native `6a84372805cefdaf5c10cb3e0f3e56f8eb12ffdc` |

实际读取用户级 AGENTS 文件、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`，逐字核实本树后三者与基线／候选相同。用户规则 marker 正文 SHA256 为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`；仅证明实际文件存在并已读取，不声称模型启动自动加载。计算、metadata、交付三提交的 Native/src tree 实际相同。

实际审阅基线到计算提交的完整 diff：三个生产文件 `NestedBlendBounds.cs`、`NestedBlendIntervalSupport.cs`、`NestedBlendJacobianExpressions.cs`，两个测试文件及工作说明；并读调用者、program 编译／存储、区间 spline、recorded／Cartesian／conic 路径与后续消费者。无 C ABI 改动，因此只做托管构建，未运行 NativeAOT。环境准备先实际返回 `ENV_READY`，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`，本地真实 PK 为 **38.0.150 / Linux x64**。schema／私有库未提交。

固定读取 `59322ef758c4b3ffb4e740506cc2412191b59c8a` 的 `docs/Certified_Isoparametric_Independent_Review.md`，复用其中 e812／24c／7b20 已接受边界，不重签。此次三文件 diff 未带入 dd／b095／4e5／675／1762 被拒实现；未混入移动 query／norm。GM 原生 XT 及 Parasolid 的几何、拓扑、参数化、拥有关系与容差仍是前提，没有新增模型或历史兼容层。

## 数学与源稳定性：accepted

本次替换的是固定几何源与固定 UV 盒下，原 `IntervalSplineBounds` 成功计算出的同一完整几何 jet，按 XYZ 三分量保存 `S, Su, Sv, Suu, Suv, Svv`，共 18 个区间。它没有缓存 dual seed、完整残差、整张 Jacobian 或一次 Krawczyk 证明。`IntervalSplineVector` 每次以当前 u/v 导数重组位置与一阶导数的 dual；相同源、相同需求可以替换该确定性计算的结果，后续证明仍必须完整执行。

`NestedBlendProgram.Spline` 实际使用 `ScalarOffset`、两方向 degree/count、dimension、rational、periodic，从借用 scalars 切出控制点和两条 knot 序列；参数起止来自这些 knots。缓存键保留这些字段及 UV 四端点 binary64 位模式，四端点以 `DoubleToInt64Bits` 比较，故 +0 与 −0 不合并。`IntervalSplineBounds` 的周期提升、Bezier 提取、二阶包围和非周期线性外延均未改。其余 instruction 字段不参与原几何 jet：`NormalSense`、`Offset` 在 `IntervalNormal` 中随后作用；spine／CHART、section 反向与半径在各自残差／frame 中随后作用。它们没有被错误地冻结进 jet。

**键本身不是跨 program 的 owner 证明。** 接受依据是实际调用链限定的局部生命周期：

- `BoundSegment` 从 `EvaluateCore` 拿到一次 occurrence snapshot，固定 nodes 和 fixedCharts 后分配 `stackalloc IntervalSplineContact[n]` 并清零一次。两处真实 `IntervalJacobianWithRecord` 调用都在这同一函数中，分别位于原 expansion 和 contraction 循环；wrapper 的普通分支才传 `localSplineScope=true`。没有第三个生产调用者扩张该许可。
- nodes 的 UV 索引在 `EvaluateCore` 中分配，随后不改。不同 occurrence 分配不同变量段；同一 occurrence 两支持只有既有 `SplineBasis` 相同才共享 UV。编译器的 basis 关系来自已准备的控制点／knots 与元数据，offset 支持复制基础 instruction。即使同一 basis 有不同 ScalarOffset，也会因键不同重算；相同 ScalarOffset 下 offset／sense 后算。这里没有以另一 program 的数值元数据相等签共同 owner。
- 持久 program 从 block 内连续的 surfaces／spines／charts／scalars 只读借用；编译阶段的 program 从已 Take 的 compiler 区间借用，workspace 是剩余区间。`BoundSegment` 与被调用的区间计算不编辑／释放／扩容这些源，不调用模型写操作或外部回调；其写入的是局部数值工作区及既有 tube 工作区。编译器后续追加／修改发生在返回之后，下一次 `BoundSegment` 取得新 stack cache。实际读取 `ApiDispatch.cs` 的串行／partition reader-writer 排斥；`RegularBlend` 实际先调用域验证，再做 end extension／组装／释放源。readonly span 本身不证明宿主永久不变，本次接受也不跨该调用返回。
- `BoundGeometry` 跨 CHART cell 时逐次创建新的 `BoundSegment`。公开求值／反求／Fix 内其他阶段均不能保留该 cache。整条 Fix 会修改模型，未被标成 immutable。

`CacheSplineContact` 一开始先 `Ready=false`，失败后不发布 key，完整成功后才写 key 并置 Ready。原 bound 失败可能部分覆盖 Values，此时原 key 也不可再命中。Ready 仅表示这个有限计算状态，不是无限期有效或对 NaN／Inf 输入的额外许可；生产入口和原 finite／正性检查保持，区间消费者仍检查结果有限。

普通独立 private Jacobian 的默认 scope=false，仍按实际 UV read set 清整个 cache；关闭复用同样清理。空 read set 不读写 poisoned cache。recorded 准入排除 spline UV，conic substitution 也排除 spline UV；二者没有借用新 scope 改证明。Cartesian spline 和 plane-spline 直接 XYZ 列保留全部方向，普通 UV 残差逐方向重算。没有新增 production heap／全局缓存、扩大 solver scratch、预算、迭代次数或调整容差；新增计数仅在测试 hook 编译条件内。

原完整 Jacobian、预条件与完整 remainder、严格 inclusion 和 contraction<.9、六 expansion／六 contraction、nearest Hessian 正性、frame finite、reach、witness、native UV、CHART 及参数 RHS／区间求解均保留。nearest、参数支持及 frame 的额外读取也经同一源／盒匹配；失败仍返回原非成功状态，未变成无交或成功发布。拓扑发布、command journal 回滚与资源门代码未改，本次接受不以缓存命中删除证明。

## 独立动态控制与关键集成：accepted

`scripts/ReviewLocalJetControls.cs` 直接绑定当前 Native 私有方法及其真实类型，没有复制 ABI 布局或用自己的缓存代替待审路径。在 polynomial／rational 两组中：

- 在同一局部 span 连续调用真实 Jacobian；只将既有 entry 的 Values 制成 poison，保持成功 key 不变，后一次 local 调用确实消费前次 jet 并拒绝。这是故意无效的制造控制，之后丢弃／恢复该 entry，不作为合法输入。
- 合法相同盒、UV 改变、XYZ 改变、参数区间改变时，与清缓存的 standalone 路径对照完整 **25** 项矩阵端点逐位一致；±0 拒命中；成功→真实 periodic 大参数失败 refresh→重试，不保留旧 Ready。
- 相同 numeric metadata、不同 scalar payload 的另一个 program 使用新 scope，结果与 standalone 一致；standalone 自身也清掉传入的旧 span。没有把 local=true 跨 source edit 调用当合法使用。
- empty read set 下，local=true 的真实 Jacobian 保持整块 poison 原字节不动。

实际封存的 NURBS **GM 产品**由 GM receive 后取出真实 implicit blend program，复用开关对完整九项位置／导数包围逐位相同；热 `Bound` 操作实际测得 **0 托管分配**。这只约束所测热操作，不外推整个 API 的分配量或普通 producer 命中率。作者 hook 的命中观察也不能单凭计数区分同一 Jacobian 内共享 UV 与跨 Jacobian 命中，不能换算完整 API 的 CPU 节省。

原 fixed187/187 复用，未重跑以堆叠相同覆盖；其中空 readset／不同 program／shared UV／失败重试、完整矩阵、位置／导数、nearest、witness、预算和其他消费者保持原控制。`NestedBlendBoundsInitializationContractTests.OptionalConsumersAreBitwiseStableWithSplineContactReuseDisabled` 的完整 dump 比较保持；本次 test diff 是当前私有签名绑定、两项新增控制与命中观察，没有放松基线数学／质量期望。原首轮 **183pass／4fail／raw1** 原样：两反射 delegate 漏参数，非周期外延实际成功，曲面墙单盒没有重复 UV；fixed 使用真实 periodic shift>1e14 失败路径并仅对有命中的平面控制要求命中。初始未提交源码缺少固定身份，不能补造其逐行来源；只接受现有固定 diff、原失败日志和后来封存制品各自证明的范围。

实际使用 parasolid-research／parasolid-docs 技能与 q2 MCP：`PK_SURF_eval`、`PK_BSURF_create` 接口查得，`PK_DEBUG_BODY_compare` 查得 legacy 条目；误查 `PK_BSURF_eval` 返回 not found，随后改查实际 `PK_SURF_eval`，不以空结果断言无调用。官方 FD 18.3.2.6、19 章及 106.7 核对原生参数与比较语义；没有把 Linux 地址用于 Mac。只交付方法和观察，不附厂商二进制或大段反编译正文。

真实 PK 38.0.150 对独立建立的二次 polynomial／rational B-surface，在域内、零端点、双向外延各四个 native UV 站位检查完整 3×3 位置／导数；最大绝对残差分别 `1.7763568394002505e-15`、`1.0685896612017132e-15`。六组 jet 分量同时作原包围点控制，控制中的 `1e-12` 仅用于 PK 点值舍入余量，未改生产容差，不是连续域认证替代品。

另从 sealed GM／独立 PK reference 各自真实 XT，取 curved 单操作 step0、NURBS 开发最终 step5、shortedge lifecycle 最终 step4 三对，分别 receive、full BODY_check（0 faults）及当前严格 `AssertEquivalent`，全部通过；前两对各四条 `n_cht_pts` 仅诊断，最后一对无 local diff。保留 global/local、face/entity 与原 mask；不是调用成功就当相同，没有把 facePair 数当匹配总面数。辅助函数原有 curve-sense 条件降级必须附有限解析方向证明，不能一概忽略；本次三对与 fresh NURBS 实际只出现 `n_cht_pts`，未使用其他降级。

本地再执行一次当前源码 **fresh NURBS 完整消费者**：GM 和独立 PK 按相同参数创建／Fix，GM transmit→真实 PK receive／compare，实际 **16pass／0fail／13missing，captureErrors=[]，producer raw2**。这是 Linux 当前构建，排除在 Mac 时间窗口之外。真实 GM live、从 GM XT 得到的 PK snapshot、独立 PK reference 身份分别保留，没有用 PK 重建替代 GM 产品。该入口只清理自己创建的 PGID，未做全局 pkill；结束时该组已自然消失，定向 kill 返回1／No such process 记录保留。

## 原完整质量与生命周期

Mac 原 **2031/2031／0fail／0skip，build0／test0，4m21s**；TRX SHA256 `cfdadda6e51d242291e2d63d2c03e0d64dc45f94ecc821511388de1d68863a0f` 实核。前后三 DLL／三 PDB 六文件逐项 hash 不变且与 sealed 文件相同；清单字段数不同不影响逐文件身份。fixed187/187／raw0／24s 与首轮 raw1 各自保留。

| 原质量组 | 实际执行范围 | pass / fail / missing | raw |
|---|---|---:|---:|
| curved | 单操作 | 14 / 0 / 13 | 2 |
| NURBS 开发 | 六 fresh pairs | 96 / 0 / 78 | 2 |
| shortedge | 单操作 | 35 / 0 / 17 | 2 |
| rigid＋scale .125 | 原变换组 | 51 / 0 / 1 | 0 |
| mirror8 | 原镜像组 | 29 / 0 / 23 | 2 |
| shortedge lifecycle | Fix 及两条产品路径各两次 Boolean，五 step | 133 / 0 / 77 | 2 |
| NURBS lifecycle | 实际单 step | 16 / 0 / 13 | 2 |
| curved editable reload | 自 XT reload 后实际 step | 17 / 0 / 26 | 2 |

八组 CaptureErrors 均空、七 raw2 和一 rigid raw0 保留；raw0 不会消除该组的 missing。实际 Fix、copy 删源、反射／缩放、imported 与 self-constructed product 下游 Boolean、reload／cleanup 按原 step／check 分开验收。下游 Boolean 与上游 Fix 属独立操作；本次没有前序输出违约的直接证据，物性 Unresolved 不反判已通过的几何检查，也不把局部通过泛化为所有组合已认证。

逐核 **45 strict／24 mass** 原质量报告，质量中另按原1e-7公式复算360个物性分量；没有把同一报告的 group 重复状态当独立资格。10 组质量 PE／portable PDB 链接、MVID／hash 和 Git source checksum 实核：普通／testhooks Native 各336 tracked、Tests169，三组源集合分开，generated／unmapped 单列。ordinary actual-loaded 与测试 sealed／前后文件属于不同证据层，不补造测试 loader 或 start/end 观察。

普通质量 Native MVID `53ef0988-1c37-479d-a0f1-70e89262ed7f`，DLL SHA `e57411ff880667669b7253697e60c7cf1daf2a8074e8c39fbd02e1e0d133a135`；testhooks `54d27e24-e038-47b4-a84a-d40559f4759a`／`e786b57648ac24854cdd18c216e1c362bc1ce77fb249116d3714422e438bc726`；Tests MVID `b35d4ebf-8258-449d-bca6-dbce082b3f51`。不能用不同 bytes 的质量、testhooks 或窗口制品互补缺失身份。

## 唯一窗口：accepted 为描述，性能资格 blocked

UTC **2026-11-12T10:43:07.8867710Z–10:45:04.7464770Z**；M4／macOS27.2 arm64／SDK10.0.400／runtime10.0.11／PK38.0.150。11 helpers／runner 两固定来源字节同，10 managed-only 预构建 raw0 后唯一串行五操作 ABBA；20进程×3 fresh pairs＝60对／120 API 全0。每 source/op 两个 rep0 冷、四个 rep1/2 暖；**16进程raw2／4planar raw0／runner2／performanceQualified=false** 保持。

| 操作 | 基线暖GM中位ms | 候选暖GM中位ms | 变化 | 候选暖min–max ms | paired GM/PK中位 | GM中位／PK中位 |
|---|---:|---:|---:|---:|---:|---:|
| sphere | .07605 | .08030 | +5.588% | .0730–.1422 | .27950 | .29084 |
| planar | .58225 | .79345 | **+36.273%** | .6072–1.0308 | .75912 | .76710 |
| curved | 45.15985 | 48.03740 | +6.372% | 45.8429–54.7100 | 24.45289 | 24.45025 |
| NURBS | 514.23385 | 448.15595 | **−12.850%** | 397.5527–549.4333 | 286.07237 | 273.13259 |
| shortedge | 580.02890 | 580.37960 | +.060% | 387.5506–711.6635 | 250.72970 | 250.16362 |

全部冷暖 GM／PK、逐对比值和两种中位倍率从 Capture 独算；60rep PROFILE／WORK 与96行 ROOT_WORK 的原行、行号、字段、log／Capture SHA、实际 step/API0逐核。F3 profile 只按原舍入精度比较。不取 rep0 当暖，不择优删除离群／回退、不乘历史倍率；四暖小样本不支持普遍、统计或因果收益。局部命中不能签12.850%的机制贡献，目标 NURBS 相对真实 PK 仍约273倍（中位数之比），整体 PK 性能目标未实现。

开发 NURBS 是另一次 B后A、每源六 pairs：五暖中位 **473.7497→394.8797ms／−16.648%**，两 raw2、冷点／全部五暖／PK 点均在收据；候选首暖492.7909也未删。不能用开发扩充正式窗口资格；NURBS 原质量复用其六对96pass／78missing，最终同名成品 **rep5**，不冒称正式窗口 rep2。

20份实际 loaded Native／Oracle、封存PE/PDB／source manifest逐Git核：两端 Native336/336 tracked、10440行 manifest，generated／unmapped另列。baseline MVID `0053c89a-0112-4d9d-a40d-493a193a508e`／DLL SHA `13eae9e128cf2010ff7aee47cf3ce548eed060001312cd8b006e240e4642bcc6`；candidate `46c24546-1486-4914-9df5-bb9d8687be7b`／`5728e5eae5f16e9e9b313942df5b716d03ad2e7b764ea0399128ca2d79e6ff73`。

主60份物性报告900分量＝**312Verified／588Unresolved**；额外12报告180＝**84Verified／96Unresolved**；总1080＝**396V／684U／0Different**，均按原1e-7／PK modulus独立复算。144份 strict 保持原 mask 与结果。ab597新norm未经该次执行，未回填；审计器0不补签producer2，故不能声称数值全认证或正式性能接受。

20个host原观察 load4.99–6.19，无数字温度／恒温／排他 CPU 资格；原 pmset 观察与 powermetrics 失败保留。另51份接受b8外部trace逐hash原字节复制已核，collector0／producer2分开；1888.7517ms为采样线程墙钟，含内联／合成leaf限制，仅供路线诊断，不是候选计时或可删CPU成本。

312实际mesh／35,389,380B、64最终XT逐hash/source/step/tag核实，详见 `docs/localjet-independent/material-bindings.json` 和收据。GM snapshot与PK reference不混；公共seed/input不是rep2、开发最终rep5与正式最终rep2区分。curved原roles/tracking/source缺失保持，204份proof有source binding、12份缺source保留；其中24份PK产品→GMreceive控制没有对应显示mesh，不能补造。显示绑定不增加几何／数值或性能资格。

原 report SHA256 `c13e698e5c5507e3668866945911b9c6fe56fbeeba93a036b3752534fb8fc8cf`，material SHA256 `e2893893de201f670805b0b9961b9563c288f8dadeb8bd878a6e38fbc7ff5aa2`，均核同。

## 可交付范围与原件

两包安全隔离解包，拒绝绝对／越界／反斜线／驱动器／重复／symlink路径，逐成员长度及hash重新计算；并逐核530项质量原清单和1036项测量原清单。quality **20,579,749B／531成员／展开78,700,033B**，SHA `ec50959c2ec008d5eb2f120d4b13cc8854e812d7c68f61262827be1451df0484`；controlled **33,988,439B／1037成员／展开137,703,906B**，SHA `f0d743e7a0f05147938d9ea8ddd12e03b0451df427c6de56615166493b3ae55c`。原包和原清单不改，派生物独立保存。

独立入口为 `scripts/ReviewLocalJetEvidence.cs`（extract／quality／receipt）、`scripts/ReviewLocalJetMeasurement.cs`（recheck／bindings）、`scripts/ReviewLocalJetControls.cs`、`scripts/ReviewLocalJetRuntime.cs`（一次consumer）。公开机器收据 `docs/localjet-independent/receipt.json`；私有原件、逐成员清单和执行日志位于 `temp_docs/independent-localjet-ab0177e/`。入口使用.NET10，路径相对脚本定位，PK经PskernelSharp＋Using＋ParasolidScriptHost，GM直接托管调用。所有dotnet执行禁用node reuse；未运行测试全局清理或改变全局配置。

独验自身首个 controls 运行原 **134** 保留：独立检查把 PK rectangular 输出（u fastest）误按内部 layout（v fastest）比较，已只修正独立脚本的索引，未改作者实现／期望。第二次 controls原0；extract、quality、measurement、bindings、aggregate各原0；fresh consumer原2独立登记。首失败前已执行的检查不能凭后来成功补造整次成功。

交付是固定候选的有限 Native 独立接受及可复核原质量／唯一窗口描述；没有发现需要修作者实现的具体新算法阻断。既有数值blocked、正式性能blocked和整体目标unproven保持，后续若继续优化或处理原U，须另按实际执行建立新证据。当前分支仅保存审查产物，不合main、不发布。
