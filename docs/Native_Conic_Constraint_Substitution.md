# 原生 conic tube 的七变量等价约束优化

本候选位于 `perf/native-conic-constraint-substitution`，完整 Native 检查点 `74c29e0`、含实际加载身份输出的 runner 检查点 `b47e25e`，Native 子树 `58c1667b0155ec46b53be42b9750c8dbd78a75dd`。父级 G 为已由 `3fdc109babe6eda489d985171e3808f956481be1` 独审、协调者接受的 `94f60da` / Native `ddd2311` / 子树 `cf417678e17e5b106f7c4734091c55c9a7b021be`。本 conic 候选尚未重大数学独审、正式 ABBA、main 集成或上线；开发时间不提供正式倍率，完整 PK 性能目标未完成。旧 6616/03e/97a 原件不改写。

## 原模型和可用范围

固定代码保留原七变量 `P.xyz,Q.xyz,q`。P 为原 implicit ICurve 的位置，Q 为原显式 rolling spine 的最近足点，q 为该 spine 的原生有限参数或原有周期提升。原 conic `C(q)`、CHART gauge、spine/support 身份、sense、半径、trim、源根、coedge 和 B-rep 拥有关系均不修改。没有另造 frame、拟合曲线、线化实际几何、删除未知量或增加模型能力。

优化资格仅按真实计算视图识别：n=7、两个 occurrence，root implicit、唯一 child 的原位置 offset3/parent0、无 UV；root 恰为一个 Blend 与一个 plane/cylinder，child 为该 Blend 自己的 Circle/Ellipse 显式 spine。没有按测试名、tag、角度或固定坐标分派。capture/require witness 时不启用新残差，保持原共享状态/CHART 收缩证书合同。其他结构继续按已有计算能力分派。

## 三次严格等价的残差组织

原方程为 Blend 距离 `s_b*(|P-Q|-r)`、另一原 analytic support 距离、原 affine CHART，以及三行 `D=Q-C(q)=0` 和一行 `E=(P-Q)·C'(q)=0`。

1. 保留 D 三行；在 Blend 距离和 E 中直接使用原精确 `C(q)`。因为 D=0，整个七维系统的零点集不变。E 变为 `(P-C(q))·C'(q)`，其 q 导数包含 `-C'·C'+(P-C)·C''`，未冻结足点或丢失速度项。
2. 对严格正的原半径，把 `|P-C|-r=0` 换成 `|P-C|²-r²=0`；若另一支撑为 cylinder，同样使用它原径向投影的平方半径方程。原长度、速度和 normal 的有限性与严格正性检查仍在；正半径是消除平方额外分支的前提。半径平方、坐标平方均由向外区间运算计算，未用舍入点值冒充精确平方。
3. cylinder 情况在区间求值前代数消去两个方程共有的 P 二次项。令 `a` 为原存储轴、`O` 为原 cylinder origin、`R=P-O`、`V=C(q)-O`、`z=a·R`、`k=2-a·a`，原 radial 为 `K R`、`K=I-aaᵀ`。有 `|KR|²=|R|²-k*z²`，因此 Blend 平方减 cylinder 平方为 `H=-2*R·V+|V|²+k*z²+radius_c²-r²`。新 Blend 行为 `s_b*H`，cylinder 行继续为 `s_c*(|KR|²-radius_c²)`。两行同时为零严格等价于消去前两行同时为零。k 保留原浮点轴分量的精确实数含义及向外包围，没有假定其平方范数恰好等于 1。

三次改写只改变求解组织，未修改公共 native ICurve 的参数化或最终模型。原 finite scalar/root isolator、端点归属与 interval root ordinal 仍决定有限交支，数学扩展域的成功不变成有限实体上的根。

## 实际 Jacobian、预条件和最近足点

七阶 Jacobian 按同一当前残差装配。XYZ 与 child XYZ 六列使用精确解析列；q 列仍由完整 IntervalDual 的原 conic 位置/速度/加速度计算。plane 与原 CHART 保持原公式。cylinder 的梯度为 `2*s_c*K²R`；代码显式再投影一次，未把 `KR` 的单位化方向当作任意存储轴下的精确算术梯度。H 的 P 梯度为 `s_b*(-2V+2k*z*a)`。centerF、Jacobian、最终 t 导数 RHS 一致选择当前残差，不能混用旧 F recorded Jacobian；资格命中时不建立旧方程的 expression record。

原点 Newton 仍生成候选并检查原最近足点。以原中心逆近似 R_F 为起点：D=0 上，代入变换的 `J_U=(I-A)J_F`，A 的非零行只有 Blend/foot，三列为 D 的残差列，其值是原方程对 Q 的梯度 `-s_b*n`、`-C'`。因此 A²=0，近似逆可取 `R_F*(I+A)`。随后对正径向平方的对应列除以 `2*r` / `2*radius_c`，再对消去 cylinder 行的逆变换更新 cylinder 对应列。离根时这只是预条件选择；真实新残差的严格 inclusion 和 contraction<0.9 决定证书，不把该矩阵当作随处精确逆。

最近足点检查未删除。对显式 child，当前 Jacobian 的子块为三行 `Q-C(q)` 和 foot 行；其 q Schur 导数正是 `(P-C)·C''-|C'|²`，为原一维平方距离 Hessian 的负一半。原 `CertifyNearestBoxes` 的负向逆导数门仍拒绝非最近 station。原后续 IntervalNormal/frame、reach、曲率/加速度、support coordinates 和完整 native witness 路径保持。原 6 次扩张、6 次收缩、Newton/line search/根隔离预算、容差及 rollback/ownership 没有提高或放宽；NotConverged、资源或有限域失败不变成空交成功。

## 固定源码的开发证据

原始证据均保留于 `temp_docs/pk-native-algorithm-work/`。三次源码检查点和退出分别独立保留：

| 版本 | 数学/初始化/record/G 检查 | 完整标准 shortedge | root cells | SpatialConic cells | Jacobian | bound evaluations | 开发 API ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| 已接受 G 开发对照 | 80/80 raw0（原 G 范围） | raw0 | 220895 | 202452 | 526133 | 67137 | 6643.885 |
| 原精确 conic 代入 `bd1939d` | 43/43 raw0 | raw0 | 190607 | 172164 | 479508 | 64131 | 5231.773 |
| 正径向平方 `4bcad8c` | 43/43 raw0 | raw0 | 79779 | 61336 | 261835 | 52016 | 3732.211 |
| 公共二次项消去 `74c29e0` | 45/45 raw0 | raw0 | 22371 | 3928 | 16870 | 47246 | 1681.979 |

所有标准结果保留 SSI4956、roots3518、winding196、budget failures0。四份最终 GM XT 都逐字为 SHA256 `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`；fresh GM XT 经真实 PK receive、BODY_check 零 faults、原 compare 仅允许 chart 诊断、三条有限 ICurve 与原点值 mass 门均通过。未把 identical XT 推广为任意模型等价证明。

最后标准运行原 capture 为 `conic-quadratic-elimination-capture/`，原 XT 为 `temp_docs/short-edge-blend-oracle/conic-quadratic-elimination/`。加载的 GM 程序集在进程 start/end 均为 MVID `1d1660ca-fe44-497e-8979-33e4bd443cbc`、2114560B、SHA256 `ff9fd4f4231ad067b1f6653512e2114643fb1510b49cf1edfbeae3ac4e2f2d8b`；实际二进制封存为 `conic-quadratic-elimination-runtime.dll`。早两次没有该进程身份记录，不借后一次 DLL 补签。原 API/WORK/ROOT_WORK 的计数各有本来含义：新的简化残差方向次数为21240，旧 G 原为7023，不能把 recorded 方向计数与新直接列的 arithmetic 混为同一种 CPU 成本。

45 项检查包含 Circle/Ellipse、两种支撑次序、plane 与倾斜 cylinder 的实际投影梯度、退化 conic、原 G 回归、初始化契约和 record。新解析圆管 fixture 还覆盖 plane/cylinder 两种横截支撑的精确位置/速度及原共享 witness 的错误 q 拒绝。矩阵逐方向比较仅为相交包围检查，未冒称端点逐位相同或完整数学证明；实际零托管分配和明确的闭式消费者另行检查。`conic-substitution-cancelled` 文件名指公共二次项代数消去，不表示取消测试；该组45项实际 raw0。

## 变换家族、生命周期与失败归因

`b47e25e` 只给 oracle 增加加载程序集身份输出，Native 子树与74c相同。family 的三个进程 start/end 均记录 MVID `084efa5a-01a5-41e9-8283-c95d1a7ba4d6`、SHA256 `fd92cbe6feea3ec566c9c3197e7b4298701179fcf64de1709a584d6074cf64a7`，实际文件封存为 `conic-family-runtime.dll`。不同构建元数据下二进制不同，未将两份 MVID 当成同一次执行。

- 刚体变换、scale0.125、原角1/radius0.5/tolerance1e-7（操作半径/容差随尺度处理）、`--geometry`：raw0，真实 PK fresh XT、有限几何及原质量门通过。其 SSI51198/root cells358969/roots5796/winding194/budget0 单独保留，不套用标准数据。
- 标准 `--lifecycle`：raw0。imported PK product 与 GM 自产 product 两条分别执行复制、原源删除、mirror-scale、PK fresh compare、第一次 Boolean、GM XT reload 后第二次 Boolean及清理。自产 FixBlends raw success，后续两次 Boolean 各自通过；四次 Boolean 的实际根数9/6/13/10和原测量分别保留，不能据组合反签未执行场景。
- mirror、scale8、`--geometry`：候选 FixBlends 返回 PK_ERROR_cant_do_intersect=157、fault16059=`PK_blend_fault_range`、edge92、Network/Clip、SSI Bind/Success188/188，原进程 raw134。原 PK Fix 返回0。该候选没有成功 GM 成品，不生成替代模型补签。

镜像8倍失败仅在固定已审 G 性能基线94f复现：新 detached 工作树 `temp_docs/pk-native-algorithm-work/conic-baseline-g/` 实读其 AGENTS/架构/实际 diff、native ENV_READY；Native 保持 `cf417...`，仅 oracle 使用完全相同的 b47 身份输出版本。原同输入也 raw134，同157/fault16059/edge92/NetworkClip/BindSuccess188/188，SSI49576/root cells95082/roots4712/winding43/budget0全部相同。基线实际 MVID `6b0023b5-4641-48eb-8529-8bcb8a39d474`、SHA256 `42437a911e8b40d7125f7b025a12192cf6a9e64b76f50c60d75c9c23c47f11ba` 亦逐进程 start/end 记录。该证据不说明1887或最新主干在此输入的状态，不撤销其他参数的既有资格，也不把它派成新功能修复微任务。两份失败均不能写成通过。

## 完整套件及身份边界

固定 b47 源码在上述操作自然结束后串行托管构建，build raw0；随后 `dotnet test --no-build` 完整执行 **1944 项 / 1942 通过 / 2 失败 / 0 skipped，raw1，8m41s**。失败仍为 `BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` 与 `NestedBlendCacheEmptyReadSetTests.SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled`，消息均为 Expected Success / Actual NotConverged，与原270/6616历史记录一致。未新增失败，也不把原套件改称全绿。更早基线只复用已固定原证据，没有在本次额外重跑其整套。

构建后的实际 test DLL、它引用的 GM Native 与 XT DLL 在测试前记录 PE MVID、大小和 SHA256，原字节封存；测试结束后再次核 hash 相同。原构建、原 test、外层 driver 的退出与 TRX 分开保留，打包器成功不会改签 test raw1。生产 standard/family 的程序集身份则按前文实际进程 start/end 与各自封存二进制绑定，不能混成同一次构建。

镜像8倍的双方原 PK seed XT 均为 SHA256 `262963ce0b899972272970ace0eff5da8b9161dbf007bd01f20a9d3037a8d1a2`，变换后 GM fresh-input XT 均为 `10b9eaeaf01d1c0e5cc1b1655fa3884844d026b90e25713ae4031139183c0c54`。对应 runner 参数、selector、PK 返回、两个固定源码/实际 DLL 身份及原失败文件一起保留，归因仅限94f/b47。

所有 shortedge 输入仍来自真实 PK seed，由 GM receive 后执行 GM 操作；默认日志 mode 字符串不证明 GM 独立构造。原点值质量门不是新的 GM modulus 三态 Verified。下一正式测量需单列固定 baseline/已审 G/最终 conic 源码、相同 runner 与实际加载身份，开发快值不与6616的正式暖样本直接签倍率。
