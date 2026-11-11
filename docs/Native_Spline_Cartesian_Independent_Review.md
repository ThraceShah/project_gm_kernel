# Cartesian 残差包集中独审

审查对象为 `6616cd60a55e50a5d8a5ada7274895c44609676b` → `94f60da64836da33eafaea7ce5912c9d5132660e` 的全部 11 文件差异。实际 Native 检查点为 `ddd23111f873a2bf70b239372c9c36aa35c2a38c`，Native 子树 `cf417678e17e5b106f7c4734091c55c9a7b021be`；94f 的同一子树逐字相同。基线 Native 子树为 `5b4c6104c600b0194bb543e2dc11efa89e2f0f1f`。

结论：接受该固定 Native 子树中受 eligibility 和全盒 frame 门控制的 Cartesian 残差优化，以及下文明确列出的实际质量范围。集中源码、数学和成品复核未发现阻断该范围的缺陷；不接受扩大为全套全绿、完整功能验收、正式性能倍率或 PK 性能目标已完成。审查未修改作者 Native 源码、测试或期望，未集成或部署。

已实际读取用户级政策、项目 AGENTS、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、实验说明与 receipt，并按 parasolid-research/parasolid-docs 技能查阅官方定义及实际 MCP 原型。qingyu2 的私有环境准备返回 native ENV_READY、PK38.0/linux-x64，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`；私有依赖不入库。

用户级政策 marker 内文 SHA256 实核为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`，整份用户 AGENTS 的 SHA256 为 `2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`。两者散列对象不同，没有将其当作内容冲突。

原包 `temp_docs/incoming/mac-cartesian-ddd2311-quality.zip` 为 216112 字节，SHA256 `f24b72186f221d91679b80d5f30a232951de7204492c01c396ac954ae8270adb`。`scripts/ReviewCartesianArtifact.cs` 先校验所有 ZIP 路径、重复名、符号链接和目标边界，再解至全新隔离目录：75 文件、展开 1051242 字节；receipt 的 74 个文件大小及 SHA256 全部匹配。原件保留在 `temp_docs/cartesian-independent-review/original/`；receipt 自身是第 75 个文件，不是自哈希声明。

原 `cartesian-regular-frame-native-source.diff` 与固定 6616→ddd 的 Native/tests Git diff 逐字相同；原 `cartesian-regular-frame-runner-output.diff` 与固定 6616→94f 的 runner Git diff 逐字相同。源码树与声明的对应得到确认；这不证明历史进程执行的 DLL 身份，包未携带足以完成该层验证的运行中二进制身份材料，不以其他进程 PDB 补签。

原退出与 TRX 分开读取：columns-ready 编译 raw1；columns-fixed 为 28 项中 25 通过、3 失败、raw1；native-fixture 为 28/28、raw0；lifecycle 相关组为 80/80、raw0。最早 columns 日志没有外部退出文件，不能签 raw0。curved、nurbs、lifecycle、shortedge 及 operations 原外部退出均为 0。这些检查只确认原记录，未重跑 80 项或全套；6616 的 1912/1914、raw1 仍按原质量 receipt 保留。

**模型与算法接受依据。** 实际审查了全部三份 Native 变更、三份测试变更、runner 及四份文档/receipt 变更；并追读原 IntervalResidual、IntervalNormal、IntervalSplineBounds、BezierSurfaceBounds、NestedSplineEvaluation、CHART 映射、nearest/reach 和 runtime 编译、变换路径。内部未知量仍是原五维 XYZ/UV，源 poles、齐次权重、knots、支撑顺序、原 sense、CHART 与实体拥有关系均未被替换。官方 XT Format Reference 的 Intersection/CHART、Blended_edge、Offset_surf 和 Surface sense 定义，以及 PK_BSURF_sf_t、PK_SURF_eval 文档，支持这里采用的原生语义；没有新 frame 实体、XYZ 身份或参数化。MCP 实际查得 PK_SURF_eval、PK_SURF_eval_with_normal、PK_BSURF_create 原型和 PK_SURF_eval 调用链（含参数转换、QSU 求值与导数缩放）；错误尝试 PK_BSURF_eval 返回未找到，未据此推断不存在曲面求值能力。Linux 静态地址未用于其他平台。

独立推导为：令 A=Su、B=Sv，n=(A×B)/|A×B|，a=A/|A|、b=B/|B|。因为 n 与 A、B 精确正交，`M=[s*nᵀ;aᵀ;bᵀ]` 作用于 `G=P-S-h*n`，恰得原 `F=[s*((P-S)·n-h),(P-S)·a,(P-S)·b]`。其行列式为 `s*|A×B|/(|A|*|B|)`。这是原精确曲面的恒等式，不是将浮点计算的近似正交假定为精确正交。代码在每个 proof box 上用原向外 hull 检查三种 norm 的有限性和严格正下界，并检查 normal 的值及方向导数有限；h=0 也先过这些门。故整个盒内 M 可逆，F/G 的零点集合相同。s 为负、h 为正或负均不改变这个推导；h 已是原编译视图的 natural-normal offset，不能再乘一次 sense。原 Offset 编译、mirror-scale 的 offset/parity 处理未改。

`CartesianSplineInverse` 对原 spline row、UV row3/row4 对应的三列做 `R_F*M(center)`，两种支撑顺序索引正确。离根 `J_F=M*J_G+[M_U*G,M_V*G]`，因此该矩阵只作为预条件选择，不能认作任意状态的精确逆。实际 UV 列由原 IntervalSpline 的二阶 jets 求完整 `-Su-h*n_u`、`-Sv-h*n_v`，没有漏掉 normal derivative。centerF、区间 Jacobian 和参数 RHS 同时选择 G；plane/CHART 行、原 t/scale/chord 保持。严格 Krawczyk 内包含、按盒宽加权 contraction<0.9、6 次扩张、6 次收缩及外层根预算未改，失败继续失败。未加 epsilon、删除证明或放宽容差。

eligibility 实际仅允许一个隐式 occurrence、n=5、一个 plane 与一个 spline、原 UV index3、非零 sense、非周期且每个内部 knot multiplicity 小于 degree。次数为 p、重数为 m 的原 spline 在 knot 处至少 C^(p-m)，因此此门保证 S 至少 C1。对有效的有限 rational 数据及严格正的分母包围，商函数保持同样连续性。度数、抽取上限、分母与非有限源数据继续由已有 evaluator/hull 返回原失败；这个优化没有自行扩大其能力。

C1 的 S 只保证 G 连续，不能笼统称 G 在 knot 上处处 C1。这里可接受跨 knot 的理由是：有限分片内 S 及其 normal 光滑，全盒 norm 严格非零；G 连续且分片 C1、局部 Lipschitz。原 IntervalSplineBounds 遍历每个相交的正长度 knot span，在接缝两侧（包括 lo=hi 的边界交集）向外取 hull，覆盖各侧导数。线段上的有限分片积分给出所需均值包围；连续的 `x-RG(x)` 和严格内包含给存在性，完整盒上的 contraction 给唯一性。无需假定 knot 上二阶导数相同。原 tensor-product 线性延伸在边界保持一阶连续，分段二阶 jets 也合入原 hull；没有改为多项式外推。证明界或 nearest 无法成立的盒仍拒绝，不从采样生成证书。

G 的 XYZ 部分恒为 identity。直接列先用零方向完整 residual 检查 frame/方程，随后写满三列；plane 的 axis*sense 使用原区间乘法，CHART 的 XYZ 导数仍为原 chord。UV 两列进入完整 IntervalDual evaluator，临时方向在每列结束重置。cache 在任何 UV 活跃时先由原 guard 清理，再按精确 UV box/源 scalar offset、degree/count/dimension、rational/periodic 配置装入；生命周期未越过当前 program。record 路径要求 n>=7 且无 UV，G 要求 n=5 且有 UV，两者不可交叠，不会出现 G center/RHS 配上旧 F recorded Jacobian。失败状态不会发布 bound 输出。

nearest Hessian、reach、后续 frame、曲率/投影加速度的公式和调用门仍在。G 的单 occurrence 条件使 CertifyNearestBoxes 的 child Jacobian 子块路径不适用；根 spline 的正定最近足点 Hessian仍从原 jets 独立检查。reach 继续用原真实支撑、盒和预算。captureWitness 或 requireWitness 任一非空就禁用 G，保留完整原 F 五维见证、原 spine/CHART 系统及 UV 身份；没有只按 XYZ 合并。其余组合、周期/C0 表面和嵌套场景继续走既有方程，属于当前能力分派，并非历史兼容层。

**本次独立执行。** `scripts/ReviewSplineCartesian.cs` 通过项目托管 API、固定 PKToy 绑定和 ParasolidScriptHost 运行，无手写 ABI、类型替身或 pskernel 重定向。只在 qingyu2 执行托管编译及脚本，未运行 dotnet test、80/288/full suite、AOT、Mac/Windows 构建或新 ABBA；未清理其他进程。

| 本次检查 | 实际结果与边界 |
|---|---|
| PK 数值 jets 与实际 G Jacobian | 48 个矩阵：polynomial/变权 rational、两种支撑顺序、sense ±1、h=-0.125/0/+0.125、域内及两个参数方向的原线性延伸；两种 normal derivative 均非零。PK 的 rectangular 布局按 `i+(n_u+1)*j` 读取，独立 cross/normal 导数落入实际全盒区间。属于定点交叉检验，非任意曲面的完整等价证明。 |
| 新直接列 | 同一批矩阵全部 25 项与完整逐方向 G evaluator 的上下端点逐位相同；没有改作者测试期望。 |
| 跨 C1 knot | 原 quadratic `S=(u,v,max(u-0.5,0)^2)`，simple 内部 knot，8 个实际 BoundSpine 盒覆盖正负 h、sense 和支撑顺序；包围解析位置与两侧速度。h=0.75 的焦点/nearest 反例被拒绝。 |
| 原 lifecycle 成品复核 | SHA 绑定的 GM XT 和独立 reference XT 均由本机真实 PK38 receive；两体 full BODY_check 零 faults，AssertEquivalent 严格比较通过，只有 n_cht_pts 诊断。15 项点值 mass 最大归一化残差 `5.391722360075773E-09 < 1e-7`，返回 compare 和 receive 缓冲区均释放。不是重新执行 GM lifecycle 操作。 |
| 早期非 dyadic 机制 | 精确整数缩放独立证明 shear=0.37 的三个已舍入 X poles 二阶差为 `2*2^-54`，shear=0.5 为零，说明前者不等于假定的精确 affine net。不能将这个新例子冒充未封存的历史失败源文件。 |

本次首轮复核脚本编译因遗漏 unsafe 标记退出 1；第二次误将 PK rectangular 输出按项目内部布局读取得到错误导数，退出 134。已按实际官方输出布局纠正复核脚本，第三次数学与成品检查实际退出 0；原失败日志/退出文件保持原字节。后加独立 rounding 机制检查单独运行，退出 0，未改上述生产或已通过数学/成品函数。成功日志 `temp_docs/cartesian-independent-review/reproducer-attempt3.log` 的 SHA256 为 `31a6abc3c39811e8db27b73b191f02a4a20e0e24e7759edef355cb940eae0c27`；attempt1/2/3 的原退出文件分别为 1/134/0。原作者的 columns-fixed 失败与这些复核脚本失败是不同尝试，不混合计数。

显式固定 net10.0、加入独立 rounding 检查后，又对最终交付脚本执行一次完整检查，`reproducer-final.exit` 为 0，日志 SHA256 `57e115f561c10cbb73bfb018f68307a85bc1b53cea25caf7ea857ece7a526c58`，同样得到 48 个矩阵、8 个 C1 盒、反例拒绝和原成品复核通过。最终脚本及全部尝试的原退出/日志散列见 `docs/native-modeling-algorithm/cartesian-independent-review-receipt.json`，不以最终成功覆盖失败尝试。

**原实际质量接受范围。** 原 capture errors 数组为空；比较 evidence 的 max_diffs=1024、all_tests=false、acc_dev_tests=false、tol_dev_tests=true，数量、类型、sense、容差、missing、匹配检查保留，仅 n_cht_pts 在白名单。原 full BODY_check、region/shell、有限几何与点值质量门不因本次接受而降级。face_pairs=3 是差异面配对数，未当作总匹配面数；global 数量相等的记录未算失败。

| 原操作证据 | 原 raw exit | 保留结果 |
|---|---:|---|
| curved block subtract / reloaded block subtract | 0（同一 oracle） | roots 84/113，root cells 1062/4263，Jacobian 2688/8771，budget failures=0；fresh GM XT、PK 检查/比较/125 点材料分类。curved tracking/roles 明示 missing。 |
| plane/NURBS 独立 fillet | 0 | roots 8，root cells 150，Jacobian 4648，9 个有限 section 的半径/双接触/切向检查；结构、region/shell、PK 点值 mass 门。 |
| plane/NURBS 独立 lifecycle | 0 | self receive、copy、源删除、mirror-scale、PK full check/compare/mass、cleanup 原记录；本次另复核上述真实成品。 |
| shortedge 独立 FixBlends 对照 | 0 | 未命中 G，direct columns=0；root cells 220895/roots3518、SSI4956、winding196、bound evaluations67137、Jacobian526133，budget failures=0，有限 ICurve 保留。 |

curved Jacobian 18464→2688、reload62358→8771、NURBS12232→4648 仅是相对原固定开发记录的工作量变化。691.020/459.496/1091.432ms 与 shortedge6643.885ms 均为原开发 API 观测，未经本包新 ABBA，不能签 CPU 等比例节省或正式倍率。原物性来自 PK 对 GM fresh XT/reference 求值，不是 GM 物性 API 三态全部 Verified。shortedge 实际为 PK seed→GM receive→GM FixBlends；日志中的 mode 字符串与 geometryAuthor 默认值不足以证明 GM 独立构造。本包未执行 fillet 后 Boolean 组合，不能从独立 fillet 或独立 curved Boolean 代签。

早期三失败的修正没有改变生产容差、证明门或 API：最终 fixture 使用 dyadic shear=0.5，避免把舍入后的 poles 当作理想多项式；巨大 UV 按原线性延伸应成功，另用超大源 pole 检查非法 frame。实际源码支持这两个机制，本次 PK 延伸导数交叉检验和精确 pole 差也支持解释。包没有封存产生三次断言失败时的完整测试源快照，不能逐字证明其当时具体 shear 或补签当时通过；这保留为历史身份限制，不以作者解释覆盖 raw1。最终固定源码及独立检查没有发现对应生产缺陷。

**复现与交付状态。** 在当前独立树先执行任务规定的私有环境 ensure，确认 native ENV_READY。最少检查如下；所有输出目录按原脚本相对位置解析，artifact 检查要求新的隔离目标目录：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewCartesianArtifact.cs -p:UseSharedCompilation=false -- temp_docs/cartesian-independent-review/recheck
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewSplineCartesian.cs -c Release -p:KernelTestHooks=true -p:UseSharedCompilation=false -p:BuildInParallel=false
git diff --check
```

本次完成的是固定优化源码/数学与已列实际质量的集中独立接受，未变更 main、上线状态、原固定测量或整体性能目标。原 known2fail 与全套 raw1 继续保留；没有本包 full suite、新下游组合或正式性能验收。审查新增文件只有本报告、两份独立 C# 脚本及派生执行回执；完整提交身份由对应 Git 提交和分支远端 ref 给出，不将本文自写一个尚不存在的提交 SHA 当作证据。
