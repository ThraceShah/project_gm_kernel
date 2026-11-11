# 原生 Conic 七变量约束优化集中独审

结论：**接受固定 Native 子树 `58c1667b0155ec46b53be42b9750c8dbd78a75dd` 中受资格门控制的等价约束优化，以及下文明确列出的实际质量范围。** 全包源码、独立数学推导、集中风险检查和原成品复核未发现该范围内的阻断。完整套件两失败、镜像八倍失败、历史执行身份限制继续保留；本结论不签全功能完成、新正式倍率、PK 性能目标、1887 集成、main 或上线。

审查范围为 `94f60da64836da33eafaea7ce5912c9d5132660e` → `f9ffc0985eb1e5ce53f649af66751de7c6c77353` 的全部七文件差异，302 增、8 删，其中三份生产文件。Native 实现检查点为 `74c29e0fc14cc40bc134a012b0eca3fecedc2947`，runner 为 `b47e25ec90f99385f9d2ce86ec88029bb4143e00`；二者与 f9 的 Native 子树相同。比较基线为已由 `3fdc109babe6eda489d985171e3808f956481be1` 有限接受的 G：Native `ddd23111f873a2bf70b239372c9c36aa35c2a38c`、子树 `cf417678e17e5b106f7c4734091c55c9a7b021be`。没有只审末次文档提交，也没有修改作者 Native、测试、runner、期望或原始证据。

已实际阅读本机用户级 AGENTS、本树 AGENTS、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、G 独审原件、`docs/Native_Conic_Constraint_Substitution.md`、交付 receipt 和完整差异，并追读原 point/interval/record 求值、closest、CHART、witness、reach、曲率/加速度、compiler、negative cache 和有限根消费者。读取文件不等于声称运行时自动加载了某层指令；whole 文件与 marker 内文是不同散列对象，没有据此宣布规则冲突。

本次只在 qingyu2 执行。规定的统一私有环境 ensure 实际返回 `ENV_READY: profile=native pk=38.0 platform=linux-x64`，PKToy 为 `d820719b6d8510d623972ab9d8efc65edbc312f9`。按 parasolid-research 和 parasolid-docs 技能实际查询了 MCP 的 `PK_CYL_create`、`PK_BLENDSF_ask`、`PK_CURVE_parameterise_vector`、`PK_DEBUG_BODY_compare` 及错误 906；前三者返回对应原型和调用摘要，compare 只返回 legacy 入口信息，未将其冒称完整 compare 文档或算法证明。实际真实 PK 动态复核见下文。没有把服务可达、空 graph 或 Linux 地址用于代签其他平台执行。

## 模型和重大数学

官方 XT Format Reference v38 的 §5.2.1.2 Circle、§5.2.1.3 Ellipse、§5.2.1.5 Intersection/CHART、§5.2.2.2 Cylinder、§5.2.2.6 Blended_edge 是本次定义依据。Conic 保持原 `C(q)=O+A cos(q)+B sin(q)`；ICurve 保持真实两支撑、sense、CHART 的原参数及 chord/scale、有限 LIMIT、容差与 owner。BLEND 的 u 继承原 spine，v 的两支持和 spine sense 合同保留。没有把 CHART 的折线近似作为实际曲线，或将内部七变量误认为 XT hvec 的另一种持久模型。

资格门位于 `src/ProjectGmKernel.Native/Geometry/Evaluation/NestedBlendConicSubstitution.cs:10`：n=7、恰两个 occurrence、root offset0、child offset3/parent0，均无 UV；root 必须 implicit，恰一个 Blend 加一个 plane/cylinder，child 必须就是该 Blend 自己的 Circle/Ellipse spine。实际 occurrence 构建也已核查，显式 child 不递归制造额外未知量。`NestedBlendBounds.cs:164` 要求 captureWitness 和 requireWitness 都为空。未按 tag、测试名、固定坐标、近邻身份或角度识别模型。

设原未知量为 `(P,Q,q)`，`D=Q-C(q)`，Blend 半径 `r=abs(Range0)`，sense 为 `s_b`；另一支持 sense 为 `s_c`。以下是本次独立推导，不以作者 45 项检查作为证明：

1. 原 `D=0` 三行原样保留。将原 Blend 距离及 foot 中的 Q 换为精确 C(q)，并保留原 affine CHART。D=0 时每个新旧解逐一对应，且 P、Q、q 都没有删除或重新定义。新 foot 为 `E=(P-C)·C'`，其 q 导数是 `(P-C)·C''-|C'|²`，Q 三列为零；D 的 q 列是 `-C'`。这不是冻结足点或把 q 改为空间投影参数。
2. 对有限且严格正的 r，`ell-r=0` 与 `ell²-r²=0` 等价，因为 ell 为非负长度且 `ell+r>0`。不存在负长度额外根。Range0 为负仍按原合同取绝对值，不是接受负物理半径；零/非有限 r 被拒。Cylinder 原 Radius 另要求有限且严格正，不能让平方掩盖负 Radius。实现仍先验原距离/normal 的有限性，Blend 的 length、conic speed 具有严格正下界；Cylinder 仍先过原 IntervalNormal 的 radial norm/normal 门。有效正半径解上的这些门不丢失原正长度分支；大盒不能证明这些条件时仍失败。
3. Cylinder 令 `R=P-Oc`、`V=C(q)-Oc`、`z=a·R`、`K=I-aaᵀ`。按原存储 a 的精确实数含义，`K²=I-(2-a·a)aaᵀ`，所以 `|KR|²=|R|²-(2-a·a)z²`。令 `k=2-a·a`，直接相减得到 `H=|P-C|²-r²-(|KR|²-rc²)=-2R·V+|V|²+kz²+rc²-r²`。两新支持行是 `s_b H` 与 `s_c(|KR|²-rc²)`。这是可逆行操作 `row_b -= (s_b/s_c) row_c`，两行同时为零的集合未变。不能假定浮点存储轴的实数平方范数恰好为 1。

所有平方和 k 都由原向外区间算术求值，未先取舍入平方当作精确常数；未加 epsilon、拟合 frame 或改变半径/参数/精度。完整盒上的 conic 和原 CHART 残差保持所需光滑性；退化或非有限数据通过已有求值门/新正性门失败。

Jacobian 位于同文件 `:65`。六个 XYZ 列逐项核过：D 对 Q 为 I；foot 对 P 为 C'；Blend 平方对 P 为 `2s_b(P-C)`；Cylinder 为 `2s_c K²R`，代码对 radial 再投影一次，不能写成假单位轴下的 `2s_c KR`；H 对 P 为 `s_b(-2V+2kza)`。其余新父行不再依赖 Q。q 列来自原完整 IntervalDual 的 C、C'、C''，没有省略 foot 的负速度平方项。Plane 的原 axis/sense、CHART 的 chord 和 parameter scale 原样保留。

`NestedBlendBounds.cs:200` 的 centerF、每次实际 Jacobian、`:281` 的 t 导数 RHS 同时选择新残差。`recordCapacity` 在命中时为零，`NestedBlendJacobianExpressions.cs:45` 先分派新 Jacobian，旧表达式图不会混入新 F。全局 n 仍为七，没有将未写的矩阵内存当成零：新矩阵先 Clear，再写六 XYZ 列及完整 q 列。

预条件变换位于 `NestedBlendConicSubstitution.cs:100`。在 D=0 上，设 A 只在 Blend/foot 两行、D 三列有非零项，分别为原 Q 梯度 `-s_b n` 和 `-C'`，则 `J_U=(I-A)J_F`，且 `A²=0`。因此从原近似逆 `R_F` 得到 `R_F(I+A)`；再对平方行对应逆列除以 `2r`、`2rc`，最后 cylinder 逆列加上 Blend 逆列乘 `s_b/s_c`。源码两种支持顺序的行列索引、符号与此一致。离根这只是预条件选择，不是新方程随处精确逆。

实际证书仍由真实新残差决定：严格 Krawczyk 内包含、按盒宽加权 contraction<0.9、六次扩张和六次收缩全在。原 point Newton 20 次、line search 10 次、外层有限根隔离 32768 cells/48 depth 等预算未改。原 point 计算提供种子和预条件，其近似矩阵不能替代区间证明；没有从离根近似逆推断根已成立。

最近点合同未偷换成 scalar station=0。显式 child 的新子块为 `[I,-C';0,E_q]`，`CertifyNearestBoxes` 对参数 RHS 解出的参数分量为 `1/E_q`；要求其上界严格负，正是距离平方 Hessian 严格正的原局部最近分支门。原 point `ValidateNearest` 也保留。任意 ellipse 的某个 station 并不自动证明全局最近点；本优化保留原锚定分支、局部最近和另外的 reach 合同，没有扩大原全局能力。后续 normal/frame、support UV、reach、curvature、projected acceleration 仍用原支撑求值，并消费同一新系统求出的真实 t 导数；没有把平方梯度作为后续 unit normal。

capture/require witness 任一非空即走原完整 F 七维证书及同 spine/CHART 约束，未让错误 q 只凭 XYZ 通过。有限交支仍由 `KernelRuntime.IntersectionCurveExtent.cs`、`SurfaceIsoPlaneRoots.cs`、`KernelRuntime.RegularCurveIntersections.cs`、`KernelRuntime.RegularBooleanCuts.cs` 和 `KernelRuntime.FiniteBoundary.cs` 的原 native interval、endpoint、方程、输入 occurrence 和 root ordinal 证明。数学扩展域的 Bound 成功不是有限 edge 的根。未解析/资源失败保留 NotConverged，不能写成 Success+0。

negative cache 原 key 的 tag/generation/sense/root/kind/精确盒和 command scope 未改，失败仍返回原失败状态。`SurfaceIntersectionPatch.AnalyticFailureScale` 继续排除 spline/workspace 依赖，并保留整个 compiler view 对原 Newton accuracy 的 scale；新增 conic 路径是固定栈、固定公式和预算，不引入未入 key 的可变配置。witness API 不借用该空间 bound cache。不存在缓存失败变成空交、跨变换复用证明或用新简化式逃过 proof 的改动。

## 原件、身份和实际质量

`scripts/ReviewNativeConicEvidence.cs` 先核原包 SHA、大小、全部安全路径/重复名/符号链接，再隔离解到 `temp_docs/conic-independent-review/originals/`，不覆盖原件。原包 `temp_docs/incoming/mac-conic-b47-quality.zip`：6810440B，SHA256 `b83b3615bb6a88d21326c7fb629bd2a36e92d36f5c860177426eb9c7d44c81ee`，167 文件、展开 24698347B。receipt 自身为额外一份文件，其余 166 份的大小和 hash 全部实际匹配；封装 receipt 与固定 Git receipt 字节相同。

原 `conic-b47-source.diff` 与固定 94f→b47 的 src/tests/runner 差异逐字相同，两个 runner diff 都与相同固定 Git 对象匹配。原 runner 只加身份输出，b47 未改变 Native 实现。原包没有 PDB；sourceDiff 与 Git 一致、执行文件身份、原成品复核是不同证据层级，不宣称完成无 PDB 的历史 source-to-binary 独立重建。

| 原执行身份 | 实核 PE MVID / bytes / SHA256 | 证据层级 |
|---|---|---|
| 标准最终 conic | `1d1660ca-fe44-497e-8979-33e4bd443cbc` / 2114560 / `ff9fd4f4231ad067b1f6653512e2114643fb1510b49cf1edfbeae3ac4e2f2d8b` | 原进程 start/end 与封存 DLL 一致 |
| family 三进程 | `084efa5a-01a5-41e9-8283-c95d1a7ba4d6` / 2114560 / `fd92cbe6feea3ec566c9c3197e7b4298701179fcf64de1709a584d6074cf64a7` | 三个各自 start/end 均匹配，不能当成标准的同一次构建 |
| G 镜像八倍对照 | `6b0023b5-4641-48eb-8529-8bcb8a39d474` / 2109952 / `42437a911e8b40d7125f7b025a12192cf6a9e64b76f50c60d75c9c23c47f11ba` | 原进程 start/end 与封存 DLL 一致 |

完整 suite 的 KernelTests/Native/XT 三份原 PE MVID、大小、hash 均核对，封存字节与 before/after 记录一致。它们只证明测试前后指定文件的身份，不额外声称 suite 具有上述生产进程同样的 loadedidentity。原 build raw0、test raw1、driver raw1 分开保留；TRX 实为 1944 total、1942 pass、2 fail、0 skip，8m41s。失败为 `NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` 与 `SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled`，均 Expected Success / Actual NotConverged。相对固定历史记录只能说无新增失败，不能把 suite 原1改绿；本次没有重跑 full suite 或 baseline full suite。

原 conic 最终数学/初始化/record/G 组为 45/45、raw0，原最初代入及平方组各 43/43、raw0；它们不是本次独立重新执行。原矩阵测试只检查逐方向区间相交，未证明逐位或全等；原分配断言为零托管分配，不代表整个建模不占内存。早两次标准执行缺进程 loadedidentity，不能借后来 DLL/MVID 补签。

| 原操作 | 原 raw exit | 质量接受范围 / 失败边界 |
|---|---:|---|
| 标准独立 FixBlends | 0 | GM 成品真实 PK receive、full BODY_check、strict compare、三条有限 ICurve 和原点值 mass 门通过；roots3518/SSI4956/winding196/budget0 |
| 刚体变换 + scale0.125，geometry | 0 | 固定角1/radius0.5/tol1e-7，半径/容差按 scale 处理；fresh PK 检查、有限几何和质量门通过；roots5796/SSI51198/rootcells358969/winding194/budget0 单列 |
| 标准 lifecycle | 0 | imported PK product 和 GM 自产 product 分别 copy、删除源、mirrorScale、检查、Boolean1、自 XT reload、Boolean2、cleanup；自产 Fix 自身独立通过 |
| imported Boolean1 / Boolean2 | 同一 lifecycle raw0 | 各自成功，roots9/6，fresh XT、full check/compare/finite/mass 原门均保留 |
| GM 自产 Boolean1 / Boolean2 | 同一 lifecycle raw0 | 各自成功，roots13/10，各步质量门保留；不代签未执行组合 |
| mirror + scale8，geometry，b47 | 134 | PK0；GM157 / fault16059 range / edge92 / Network Clip / BindSuccess188/188；无成功 GM 成品 |
| 同输入固定 G 对照 | 134 | 同157/fault/edge/phase/Bind188/188，roots4712/SSI49576/rootcells95082/winding43/budget0 均一致；不是候选通过 |

已按原 family driver 和固定 runner 核参数：`--angle=1 --radius=0.5 --tolerance=1e-07 --bench-profile`，另分别加 `--input-rigid --input-scale=0.125 --geometry`、`--input-mirror --input-scale=8 --geometry` 或 `--lifecycle`；driver 移除 `GM_WEB_SELECT`。baseline 只采用相同 runner 身份改动，Native 为 G。其具体启动环境并非由文件散列提供密码学证明，原 wrapper/receipt、case 标签、原阶段/计数和输入字节相互佐证，不夸大该层级。

镜像双方的原 PK seed 都是 `262963ce0b899972272970ace0eff5da8b9161dbf007bd01f20a9d3037a8d1a2`，变换后 GM fresh input 都是 `10b9eaeaf01d1c0e5cc1b1655fa3884844d026b90e25713ae4031139183c0c54`。失败归因只限 94f/b47 此 case，不声称 1887/main 的结果，不撤销别参数原96矩阵，也未要求接管修复。

原输入确为非原语 PK Seed→GM receive→GM 操作，`ShortEdgeBlendOracle.cs:220` 的数据流清楚。日志 mode 或默认 geometryAuthor 字符串不是 GM 独立构造的证明。原点值 mass 来自 PK 对实体求值，不能成为 GM modulus 三态全部 Verified；curved roles 的既有缺口也未补签。

三份本包标准 GM XT（bd1939d/4bcad8c/74c）均实核为 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`。G 的第四份原件另从 `../gm-spline-cartesian-review/temp_docs/cartesian-independent-review/original/temp_docs/short-edge-blend-oracle/cartesian-regular-frame/` 只读核同名 self XT，hash 也相同，并与 G 固定 receipt 相符；没有改动该树。字节相同只说明这些固定产物，不能泛化到任意模型。

rootcells220895→22371、SpatialConic202452→3928、Jacobian526133→16870、bound67137→47246 是相对固定 G 原记录的工作量变化。新旧 directions/record/direct arithmetic 的含义不全相同，不能按 inclusive sampling 或计数直接签排他 CPU 节省。1681.979ms、lifecycle warm806.323ms 都是开发观测，不与旧6616正式5.64s签新倍率；没有新 ABBA。

## 本次独立执行与可重复性

`scripts/ReviewNativeConic.cs` 直接引用项目托管 API、PKToy 绑定和 ParasolidScriptHost；没有手写 P/Invoke、ABI struct、别名替身或把 pskernel 重定向给本项目。使用现有 test hook 取得实际新 Jacobian，独立参考由未消去的平方范数多项式和 Complex 步长求导构成，不调用作者 Dual/H/K²R 或 directional matrix 的结果。合成 computation view 只用于算术风险检查，不冒充已构造的合法 B-rep。

| 本次执行 | 实际结果与限制 |
|---|---|
| 16 个 Jacobian / 112 个独立导数方向 | Circle/Ellipse、plane/cylinder、两支持顺序、Blend sense±1；P 与 Q 故意不一致，q 非零；七列均核。stored axis=(0.6,0.8,1e-6)，其平方范数不精确为1，8个 cylinder 情况还要求错误 unit-axis 公式落在实际区间之外。最大独立数值导数落在区间外距离为9.408841978100768e-18，检查阈值2e-13。此阈值仅属于复数数值交叉检验，未进入生产容差或证书。 |
| branch/nearest/witness 风险 | 0/负/NaN/Inf cylinder Radius 均拒绝；负 Blend Range0 按原 abs 合同成功。一个原 CHART 的真实 BoundSpine 包围解析位置和速度；错误 q 的原七维 witness 被拒；相同空间位置上的远端 circle station 返回 NotConverged。far case 经过原 point nearest 门拒绝，未声称它绕过该门专门触发新 interval nearest；新子块的严格负 Schur 合同另已独立推导和源码核对。 |
| 原成品真实 PK38 复核 | 直接载入原 PK native XT（`b1b0028feca8fd51e0b4b5615d4c34ad81adbee43648dd6c5e320ef9af464655`），与原 GM self XT（37ac…）比较。两体 full BODY_check 零 faults；AssertEquivalent global21921/local21931/face_pairs0，通过数量/region/shell/type/sense/tolerance/missing/matching和几何门，仅既有 n_cht_pts 可诊断。三条有限 ICurve 按端点匹配，16段原 PK arc-length 包围通过，双向各17采样的最大距离3.3180942870238203e-12；15项点值mass最大归一化差3.965693779194955e-9<1e-7。采样不是任意曲线完整等价证明；这是复核原成品，没有重新执行 GM 建模或测速。 |

本次没有运行 dotnet test、45/80/fullsuite/96、AOT、Mac/Windows 构建或正式计时；只有串行 .NET10 托管编译/脚本，所有命令继承 `MSBUILDDISABLENODEREUSE=1`。结束后未见本次 owned PGID/testhost 残留，没有清理其他任务进程。作者代码仍是固定 Native 子树。

新增复核自身的失败也保留：artifact 首次 raw134 是反射 JSON 输出被 AOT 友好配置禁用（工具原输出未另存日志），移除该输出后 artifact audit raw0；增加 receipt 对照时一次 raw134 是复核误加了原件不存在的末尾换行，已改为实际 byte hash 对照，最终 raw0。risk/product attempt1/2 为脚本编译 raw1（global using 顺序、fixed-buffer property 访问），attempt3 raw134 是对 PK 零 faults 的 null 数组调用 free，错误906已由 MCP核为 null_arg_address；修正本脚本后 attempt4 raw0。最终再使用未经 GM 重导出的原 PK native XT 作 reference，完整执行 raw0。没有将这些脚本错误记成建模失败，也没有以最后成功覆盖失败尝试。

最终成品日志 `temp_docs/conic-independent-review/risk-product-final.log` SHA256 为 `c1d12434bcf0e10e26f6476223011ce62a6e2d200e08cfafdaa4040d48a9758f`；最终原件审计日志 `temp_docs/conic-independent-review/evidence-audit-final2.log` 为 `02f95dea853fd23e89579d1fe046dc550de5c835a6a8ee3d8e21a1ed9addb1d3`。工具返回的 raw exit 和各尝试 hash 另列于 `docs/native-modeling-algorithm/conic-independent-review-receipt.json`；本次未在启动当场另写 `.exit` 文件，不伪造为原进程同期原件。

最小重复命令（先按统一私有环境流程取得 native ENV_READY）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewNativeConicEvidence.cs -p:UseSharedCompilation=false -p:BuildInParallel=false
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewNativeConic.cs -c Release -p:KernelTestHooks=true -p:UseSharedCompilation=false -p:BuildInParallel=false
git diff --check
```

原交付 receipt 保持原字节，其中尚未独审的字段是历史提交状态；本报告和新增独审 receipt 记录本次接受，不篡改作者原回执。交付只包含报告、两份必要 C# 复核和派生回执；二进制、XT、原包、凭证与厂商内容不入 Git。首个有效原件审计提交已推送为 `c8aab2b2fee5934d6bc9c892aa9ee7028a62ca3f`；完整最终提交和远端 ref 由任务交付消息给出，不在报告中预写尚不存在的自身 SHA。
