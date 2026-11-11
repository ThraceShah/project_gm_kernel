# 固定 Boolean 4794 集中独立验收

评审日期：2026-11-12。评审树 `gm-boolean-4794-review`，分支 `review/pk-native-boolean-4794`。入口 HEAD `aca329607b376032a31283cc0e659fbd54916d97` 只增加交接文档；计算源固定为 `4794e60111e1382eafe69880077544fcc135521e`，相对 `f9c59201c0b9c5d103dee45124e3f43ca59a0921` 的 **49 文件、2096 增行、414 删行完整 diff** 为本次对象。没有修改作者实现、测试期望或原件。

## 集中结论

**接受本包在下述 63 个最终 Boolean 场景及具体生命周期中的几何、拓扑、有限事件和安全默认路径交付；接受现行三态数值合同本身。未发现需要否决这些范围的新增数学或模型阻断。仍阻断“全部数值门已通过”“510 分类相等”“任意 Boolean 正确”及“整体 M0–M5 完成”的结论。** 接受合同不等于把合同返回的 Unresolved 改为 Verified。

这是一份有限范围的独立工程验收，不是任意 freeform/奇异/高价交汇的形式化证明。原生 XT、BLEND/ICurve/CHART、参数化、拥有关系和精度语义为前提；没有授权 frame、新持久模型、XYZ 近邻身份、epsilon 吸附、历史兼容或放宽几何门。

历史 `review/pk-native-f9c5920@4c7646e1085e6fa786b2f335cabc7c086c008e13` 保留其明确接受范围：96 构造代表、标准生命周期、共同支持/孔、指定角点和有限消费者，不扩大为完整 local operation catalog。它记录的 transverse 原点值失败和 self/imported 15/16 分类差异及原 exit 134 均保持原状；本报告不重写历史验收规则或补签历史原操作。

## 实际读取、定义和来源

实际读取用户级 `AGENTS.md`、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、相关 `docs/Finite_Analytic_Rolling_Network.md`，完整 diff、`pk-native-boolean-functional-candidate.md` 及其连续根、圆柱支路、数值合同、最大精度、sheet 所有权链接和固定 f9 报告。用户级文件 whole SHA256 为 `2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`；whole 与 marker/内文散列的层级不同，没有当作冲突，也没有声称工具运行时自动加载。

实际使用 parasolid-research/parasolid-docs 技能。本机官方 FD §31.1.1、§31.4/31.4.1 核实 accuracy 是工作量控制，合法范围 [0,1]，modulus 是估计值正负界；XT §6 核实 CHART 的弦投影 gauge、递推 scale，以及 artificial/help/terminator 的不同语义。配置查询工具实际返回 Boolean API、`PK_TOPOL_eval_mass_props` 的原型和调用摘要；门户 MCP 对 `PK_CURVE_parameterise_vector` 实际返回 found 及 `COI_pvec_curve`/`QCU_parameterise_cvec` 调用线索。首个经代理的 curl TLS 失败，随后显式绕过代理的同一 HTTPS MCP 调用成功；没有把服务可达当成原生建模成功，没有以静态 Linux 地址运行其他平台。

原件根为 `../gm-pk-native-delivery/`，下面原日志前缀均为该根下 `temp_docs/pk-native-acceptance-audit/`。独立审计器位于本树 `temp_docs/boolean-4794-review/EvidenceAudit.cs`；`evidence-audit-final.{log,exit}` 实际 **exit 0**：

| manifest | 固定 SHA256 | 实际核对 |
| --- | --- | --- |
| `boolean-candidate4794.source.sha256` | `2a053246b80d12f66cc4751b65ae2eecc97410aa02c53a1a372125cf9c3b608e` | 690/690 与本评审树源码相同 |
| `boolean-candidate4794.artifacts.sha256` | `27d65a08779b6da65a6a613b86c4ec5432e2a927fca4131a1943fa71d389a894` | 484/484 原输入/产物相同 |

固定 f9 的 `evidence-final.sha256` 及其 726 项校验沿用已接受独审，不冒称重跑矩阵。690/484 是文件身份检查，不是成功建模次数。

## 数学、模型与默认路径审查

| 实际源码机制 | 论证、限制和结论 |
| --- | --- |
| `KernelRuntime.RegularBoolean.cs`、`RegularBoundary.cs`、`RegularPointContacts.cs`、`ThirdFaceEvents.cs` | 普通入口显式 `RequireBoundaryAssociations=true`；原顶点按拓扑身份读入，无跨 body 的距离合并。input finite occurrence、端点参数和 support use 登记后进入共同 arrangement。第三体 pass 无条件使用隔离/拥有关系证明，原点反求/空间 AddPoint 回退已删除。`ForbidSpatialBoundaryReuse=false` 的独立负控与发布故障注入实际通过。这里接受的是身份安全规则，不把材料分类用的几何距离检查说成身份证明。 |
| `SurfaceTripleRoots.cs` | 方程为三支持的六维共同 native UV。全有限参数积从初始 pending cell 覆盖；每个叶子以区间排除，或扩展盒中的 Krawczyk 严格内含与收缩 <1 接受，并检查 enclosure 留在原有限域。根盒及三个唯一域一起保留。相交根盒只进入候选判断，必须有一方唯一域包含另一方共同六维根盒才能去重；否则细分/失败。100000 累计 cell、深度 64、pending/output 容量保持有限失败合同，没有提高预算。 |
| `ParametricSurfaceIntersection.cs`、`SurfaceIntersectionPatch.cs` | 有限边界例外只认原 Bezier 齐次 corner 精确同点且严格落在唯一域内；沿用收缩条件。不是用扩展域根或近 corner 空间点替代有限根。±1e-13 控制仍 NotConverged，不误签存在/无交。 |
| `NativeSplineBoundaryContact.cs`、`RegularCurveIntersections.cs`、`FiniteIntersectionEvents.cs` | BCurve 与 BSurface 保留原 knot span 和共同证书。相同原系数/恒正权重仅在逐系数严格等式成立时用 native affine 映射；不同参数的直线载体仅适用于两个完全相同恒定 Cartesian 坐标、第三坐标全域严格单调的 clamped 非有理 Bezier。实际 Bernstein 坐标反解必须唯一隔离；根取外像，唯一域取内像，不能以根盒重叠或同载体直接认同根。拒绝一般 rational/多 span/非单调类别是能力限制，不新增模型或转换路径。 |
| `NativeTripleSurfaceRoots.cs`、`NativeCutLocator.cs` | 三支持根携带共同 native UV，传入现有 CHART 方程；共享支持身份、完整 gauge 区间覆盖与目标 contraction 包含完整 witness 是定位前提。不同支或相邻图不满足既有连续性条件即失败。平面降维/BSurface locator 的能力 guard 保留，未把一般 locator 误当平面证明。 |
| `NativeSurfacePairEnds.cs`、`SheetIntersections.cs` | 末参数特例仅对本次 provider 新建、open、Both UV、非 blend contact、artificial limit、cut 端等于原曲线自然端的 `NativeSurfacePair`。重新隔离同支持 native boundary 方程；raw UV 只选唯一候选支，原根和新根证书还必须证明同根，CHART error 只筛除候选。已有输入 trim、闭曲线、其他 limit 不取得该资格。不能仅凭近端点或 raw UV 落盒宣称同根。 |
| `CoincidentLineBoundary.cs`、`SurfacePairRelations.cs`、`SheetArrangement.cs`、`ThreePlaneCutEvents.cs` | 同域有限边先证明实际载体/参数关系；未证且未严格分离的类别显式不支持。相邻 coedge 的端点传递依赖实际 loop、共同 native vertex 和来源边有限关系。alias 仅在本次操作中，归并同步临时 input edges、cut ends、参数行、切触/接触行及精度最大值；仍保留原顶点记录，不改持久输入来预先焊接。generation/occurrence 检查防止跨实体或跨有限使用借证。 |
| `BoundaryAssociations.cs` 的 4794 九行 | plane/cylinder 的共同支持不足以选择 generator。用整个事件精度盒减去原 line origin，与原 direction 做向外叉积；某分量严格不含零才排除整条无限 line，因此也排除其有限区间。inverse 67 本身没有成为无交证明；无严格间隙仍走原验证/失败。没有更改 binary64 轴或“修正”输入。 |
| `StationaryBoundaryEndpoint.cs`、`RegularBooleanCuts.cs` | 对声明 native t=0 使用 sin/cos 精确恒等式，分离导数或二阶导数证明相应半段唯一，剩余半段独立隔离。实际顶点拥有关系、sense 与有限 trim 保留；近内部根、外部根不能吸附到端点。 |
| `Evaluation.cs`、`LocalOperations.PlanarBlend.cs`、`AnalyticRollingCandidate.cs`、`ProfileFaces.cs` | D0 单槽用局部两向量 jet 承担已有 D1；不改 native 数学。接触 line 从真实 source fin use 确定 sense/gauge；删除比较器中的 canonical reattach 和 sense 白名单是收紧门。single/parallel 的原实际独立证据可用，不能扩张到 all-edge local catalog。sheet 引用计数修复复用 Blend owner 固定 `54f1b309d830ac78358ebcba78e1deec8e3c3b8f` 独审；本次另核其 Boolean 交界及源差异，不替 Blend60be 签集成。 |

纯算法/内部数据变化未触及 C export、ABI 结构体或跨原生边界所有权；本轮没有 AOT 编译。局部 Span、scratch 的失败返回和 finally 回收没有引入新的分配路径或公共模型；最终产品仍经过共同构建检查和 command rollback。不能由这些静态观察替代实际故障注入，见下面独立运行。

## 原执行矩阵与证据可证层级

| 原执行 | 可接受事实 | 原 exit 与不能宣称 |
| --- | --- | --- |
| `native-boundary-native45-v2` | 15 类 × 并/交/差，共45最终操作；双方54个 API step均0，包括9前序 subtract；54份最终/前序严格、fresh/full及所列材料检查记录 | **2**；40次 mass comparison 未决。执行为11c计算源；4794相对11c的生产差异只有上述严格排除九行，45证据按未受影响范围复用，不称在4794重跑45。 |
| `cylinder-native-mixed18-v1` | 6类 × 并/交/差，共18最终；双方各30 API step均0，含12前序操作，repeat类最多两次前序subtract；30次几何比较记录 | **2**；30次 mass comparison 未决。独立补验两种 seam 的连续重载回归。 |
| `native-root-standard-lifecycle-v1` | 标准自构造、copy/delete源、mirror/scale、cut、editable XT reload、第二次cut；每步 strict/full、有限 ICurve/UV及材料代表检查 | **2**；8次 mass comparison 未决，不扩张为所有输入/所有额外 face-domain gate。 |
| `native-root-consumer-forward-v1`、`native-root-consumer-reverse-v1` | 同字节target+2tool，正反选边分别GM/PK成功、一个body、fresh/full/strict | 各 **2**；各一次物性未决，不是两个工具分别构造后换输入。 |
| `native-root-consumer-inject-v1` | GM901/failed/0body，三输入byte-identical rollback；同输入PK成功 | **0** 是控制通过，不是GM成功建模。 |
| `native-boundary-full-suite-v1` | 固定11c完整单测1907pass/2fail/1909，19m31s | **1**；两项圆柱重载失败原样保留。 |
| `cylinder-native-branch-fixed-v1`、`boolean-final-candidate-regression-v1` | 修复后143/143，以及最终290/290，allocation IL guard CLEAN | 各 **0**；不能拼成“最终1909全绿”。 |
| `profile-native-owned-v2` | sheet、oblique extrusion分别fresh/full/strict、mass Verified；全部相关pool回零 | **0**；按固定独审复用，不是本轮重跑。 |

63 是45+18个**最终场景**，不是上述全部API调用、前序步骤、lifecycle、测试用例相加。BODY_check和strict比较覆盖有限产品定义；regular/mixed材料是固定代表点，不能把这些采样说成整个材料场或严格finite distance的完整证明。

各历史进程自己的日志开头真实 loaded GM assembly SHA256 分别为：45项 `3cd502554fb5ffb09ef477571b0f4c69f8a63f5c8fe24251ccde23ac82e663b3`，18项 `2a41c9f1644eab59187da3ce754ebc9b2b3229f790f1203e7c5b4733c9db586f`，lifecycle `b82dcb0c4eeb26dcb67704514981e843802e9612ebf5d4e8f6ca606c93eda099`，三consumer各自记录 `091007a4b22a9efb977cb24f927729971ed9a184b56de64cf9a65cd9adf2a0f5`。历史材料可证到该进程自报loaded DLL哈希、固定源快照与原件一致；没有各历史进程的独立PDB载入收据，不能用最后构建目录的DLL/PDB替它们逐进程补签。290的两DLL收据也只按其实际层级保留。本轮额外核对自身PE/PDB/source及真实进程模块，见独立运行收据。

## 当前数值合同与未决范围

`ParasolidMassProperties.cs` 的15分量索引与PK输出布局一致：amount/mass各一个modulus、中心三个共用中心modulus、惯量九个共用惯量modulus、periphery一个。估计加减界、分子差及 `1+abs(reference)` 的界都向外；下界除最大分母，上界除最小分母，保守包含所有报告值的归一差。**upper <1e-7 才Verified，lower >=1e-7 才Different，其余Unresolved**；非法/非有限输入拒绝。区间重叠不会签Verified；不同几何仍由未放宽的strict/full/finite/material门阻断。

作者477eec8/f169helper的真实同字节PK imprint控制已读：原支持/方向、region/shell保持，合法细分前两轴触发旧点值门；说明旧测试会误拒，不证明GM等价或PK错误。`native-contract-complete`原exit0包含wrong-size积分Different、wrong-radius实体和wrong-finite-trim wire经fresh receive后由原helper拒绝，以及真实分离材料翻转Different。最大合法accuracy=1的实际pair仍exit2，area外上界约2.40091e-7；不提高阈值、去掉分量或换输入。

`probes-import`新合同执行原exit **2**：510个原坐标逐行核对未变，四列保留，Different0、Unresolved16、APIerror0；native自身roundtrip7、GM/native11、GM/fresh14、fresh/native7分列，GM独有项没有用native7豁免。finite range nominal距离不是严格distance上下界，仍不能签边界分类相等。历史self15/import16与本轮import16不是同一次execution。当前self产品SHA256 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba` 与固定self/consumer target同字节，只允许标注复用；本轮没有重新执行self510。

shared `ParasolidBodyCompare.cs` 在本完整diff中未修改，SHA256 `38fdcac891ba99221bb778af8848761daf1b847d74db0bf6de12d714c290635e`。max_diffs=1024、all_tests=false、acc_dev_tests=false、tol_dev_tests=true、non_match_tests=true；验global/local，仅n_cht_pts诊断，结果r_f释放。没有混入Blend60be条件sense/FaceAsk或性能6616口径；wire额外有限边匹配仍只在其声明范围成立。

## 本轮独立执行

先通过统一私有环境ensure，实际ENV_READY，Linux x64 PK38.0、.NET10.0.302、PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`；没有提交私有依赖。新日志均在 `temp_docs/boolean-4794-review/`。

`focused.{log,exit}`：原exit **0，24/24**。覆盖六维单根/断开双根、Bernstein非线性native映射及±1e-13偏离、精确corner及近corner未决、精确conic端点/内外根、逐系数affine否定、盒重叠无共同方程、极近双generator与有限trim、两种圆柱seam重载、默认不支持junction原子拒绝，以及默认开关关闭时真实projected publication失败后的三输入/池/分配回滚。该最后一个用例约1m57s，原测试总时间1.9750min，不作性能对标。

本轮 `SourceBinding.cs` 对自有构建PE/PDB GUID匹配、PDB的SHA256 source document逐项核验：Native328个源码、tests154个源码均一致，另有generated/other 3/4项单列，不冒称其为人工源码。Native DLL=`815dc2b22a777f1777673ff9878194471330a23c7c43ae993b9033ba947acc08`，PDB=`f2a2e370d7343f6b3837b49f24143be8139a68ada4355b31965307d0a2429992`；test DLL=`15bfb0a3ddb68d6d2ebed185124268b6901365c6e59412471b13bbc84aa6b005`。这是本轮自身收据，不能跨进程替历史构建代签。

`consumer.{log,exit}`：原exit **2**。本轮实际self Fix得到2条blend，再将同一组target+2tool原字节交给GM和真实PK；双方error0/result21650/1body，进入projected replacement并Publish。两侧完整BODY_check faults0、fresh receive的strict global/local无差异（face_pairs=0不作为面匹配总数）。该进程自己的GM_ASSEMBLY记录为上述815dc2b…；运行中的PID2034575实际模块maps另核GM DLL同哈希及独立libpskernel，未重定向oracle。PE/PDB/source收据与其loaded DLL相同字节。实际target仍37ac232…、两tool仍ad885c46…/8056a1bf…；GM/PK输出分别a3867d304c7baa2aeb0b41915ba5d7722ff225b994e23914e656b2a1536f26a5 / cab363bf1fe3cd14ab178c6c0cff201a67704c2fae3ab99af269d643d3f2fcdf，与作者这次对应输出同字节。一个mass comparison仍Unresolved，没有补签self510。

`contract-controls.{log,exit}`：原exit **0**，是负控/合同实验执行成功。真实wrong-radius实体及wrong-finite-trim wire经fresh receive后均由未改strict helper拒绝；真实wrong-size mass为Different，明确分离的材料Inside/Outside翻转为Different，On分歧为Unresolved，模拟API错误被拒。原生3047cc3…输入分别沿三轴真实imprint，支持/方向和region/shell保持、faces10→15；两轴仍重现旧点值门误拒，三次数值比较均Unresolved。该控制raw0不签“物性全Verified”，也不冒充GM正向操作。新产品位于 `temp_docs/native-acceptance-contract/review4794-contract-controls/`。

可复制命令（必须另换输出名）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests/KernelTests.csproj -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~SurfaceTripleRootTests|FullyQualifiedName~NativeSplineBoundaryRootTests|FullyQualifiedName~OnlyAnExactlyProvenNativeCorner|FullyQualifiedName~AnExactNativeConicEnd|FullyQualifiedName~NativeSplineAffineView|FullyQualifiedName~OverlappingRootEnclosuresWithoutCommonEquation|FullyQualifiedName~SharedCylinderPlaneSupportsDoNotIdentifyTwoCloseBranches|FullyQualifiedName~UncertifiedCylinderJunctionLeavesEveryInput|FullyQualifiedName~ProjectedRollingCellFailureRestores|FullyQualifiedName~ReloadedPartialCylinderCanBeCutAgainAcrossItsAngularBoundary' --logger 'console;verbosity=normal'
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RollingBooleanConsumerOracle.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- review4794-consumer
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/NativeAcceptanceContractOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- review4794-contract-controls ../gm-pk-native-delivery/temp_docs/native-acceptance-contract/native-contract-complete-control-v1/input.x_t
```

单测执行后仅核对并清理本次PGID2023655内testhost，已无残留；没有清理其他任务进程。两次file-app均实际结束。审计/构建/数学/建模/负控的exit分别保存，不能互签。

本轮固定收据为 `docs/reviews/advanced-modeling/pk-native-boolean-4794-independent-review.sha256`，绑定审计器、完整reviewed diff、源码HEAD/空工作diff、各新原日志/exit、PE/PDB核对记录、消费者实际输入/选项/产品和真实负控产物；私有运行库不进入Git。收据校验只确认这些原字节，没有改变各原execution状态。

## 交付边界

没有重跑fullsuite/96/290全套，没有性能长测、Mac/Windows或NativeAOT、main整合、门户发布和新看板run。五圆柱Blend产品GM BodyContainsVector=5000保留为下游查询API失败；不倒扣其独立上游Blend，也不签“所有查询通过”。FaceAttach负surface reattach仍只是静态疑点，不扩张为Boolean前置任务。

下一实际交付由集成/看板owner绑定接受的最终source、inputs/products/option hashes和原command/exit，分别展示每步API、几何/拓扑、mass三态、分类未决和注入控制；不能用重建预览代替真实产品。上线核对和Blend/FaceAsk/性能包集成不在本次接受范围。
