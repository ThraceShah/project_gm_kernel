# 原生 plane/spline 残差的 Cartesian 区间组织（隔离实验）

本包位于 `perf/native-spline-cartesian-experiment`，起点为已接受的 `6616cd60a55e50a5d8a5ada7274895c44609676b` Native 源码。首次实现为 `241b8f3`，当前完整代码检查点为 `ddd2311`。它尚未独审、集成 main、正式计时或上线，不属于 `03e5a8e` / `97a316` 原 ABBA 包；本页开发时观测不能取得正式加速倍率或完整 PK 性能目标资格。

## 同一原生模型中的等价约束

未知量仍为原 XYZ、原 UV，共五个；保留源 B-surface 控制点/权重/knot、支撑次序与 sense、offset h、CHART t/scale/plane、源实体身份及有限分支。没有生成替代曲面、拟合曲线或改变 XT 定义。设 S(u,v) 为原 spline，A=S_u、B=S_v、n=(A×B)/|A×B|，a=A/|A|、b=B/|B|，s 为原非零 normal sense。原三项 spline 残差为

`F_s = [s*((P-S)·n-h), (P-S)·a, (P-S)·b]`。

新求解残差为 `G_s=P-S-h*n`。在整个 frame 正则邻域，有 `F_s=M(UV)*G_s`，M 的三行为 `s*nᵀ,aᵀ,bᵀ`，且 `det(M)=s*|A×B|/(|A|*|B|)` 非零。因此两套方程有同一精确零点集；plane 与 CHART 两行保持原计算和原位置。这里的恒等式针对原精确几何，未把双精度法向之间的近似正交当作恒等式。

每个 proof box 均使用原向外舍入 spline hull。cross、A、B 的 norm 必须有限且下界严格正，normal 及其方向导数必须有限；这些检查在 h=0 时也不跳过，避免乘零掩盖非法 frame。优化仅用于一个隐式 occurrence、一个 spline 加一个 plane、UV 在原 index3、非周期且内部 knot 至少 C1 的 sheet。C1 保证 G 连续；跨内部 knot 的各侧导数由原 hull 向外合并。其他几何组合/周期或 C0 sheet 使用现有原生方程路径，并未给它们新资格。

中心近似逆为 `R_G=R_F*M(center)`，两种支撑次序都按原 spline row、row3、row4 三列映射。它仅选择预条件器，不作为存在性证明；实际 G 的完整区间 Jacobian 仍必须通过原严格 Krawczyk inclusion 和 contraction<0.9。UV 导数是 `G_u=-S_u-h*n_u`、`G_v=-S_v-h*n_v`，不是冻结 frame。离开零点时原方程满足 `J_F=M*J_G+M_U*G+M_V*G`，不能把原 Jacobian 当成随处只需乘常矩阵。

XYZ 三列的 spline 部分为恒定 identity，plane 为原 axis*sense，CHART 为原 chord。三列一次 frame 验证后直接装配；UV 两列保留完整 IntervalDual 求导。centerF、Jacobian 和参数 RHS 始终选用同一套残差。原 Newton 仍生成候选，原 six expansion/six contraction、舍入和根细分预算、输出状态、reach/closest-foot Hessian 与后续 frame/曲率/加速度代码均保持。capture/require witness 时继续使用原残差，保持共享证书原方程系统及完整五维 witness 合同。

## 针对关键假设的验证

最终相关组 `cartesian-regular-frame-lifecycle` 为 80 项通过、0 失败、raw exit 0。包括 19 项 Cartesian 检查、原 spline cache、CompactBranchCertificate、RollingNetwork、原 XYZ sharing 与实际反射 cache 合同；两项已知 6616/270 suite 失败不在本组内，不能将此称作全套测试通过。本包未重跑整个 suite；原 6616 的 1912/1914、raw1 历史保持原样。

检查覆盖 rational/nonrational、两种支撑顺序、原负 sense、正负 h、非单位 CHART、原线性延伸、错误位置/UV、非正则及有限 frame 门、非周期/C1 eligibility、共享 witness 错 UV 拒绝。独立解析 fixture 为 dyadic shear 的抛物柱面 `S=(u+0.5*v,v,u²)`；验证实际 UV 变化的 normal derivative、离根处 frame 导数项及原 inverse 经 M 映射后的五阶矩阵乘积。直接装配矩阵全部 25 项端点与完整逐方向 evaluator 逐位相同；预热矩阵链实际托管分配为 0。超大 UV 按原线性延伸验收；超大源 pole 的非有限 frame 被拒绝，未错误套用多项式外推。

初次 60 秒工具超时未形成原测试退出文件；随后缺 namespace 的编译 raw1、再后的三个 fixture 断言失败 raw1 均保留。断言修正明确区分了非 dyadic pole 加法产生的真实源几何差异，以及原线性延伸和多项式外推的差别，未放宽生产容差或证明条件。最终 28 项数学/列检查 raw0 后，再完成上述 80 项相关组 raw0；没有用检查器退出码改签这些失败尝试。

## 完整实际操作闭环

Mac ARM64、真实 PK38、当前 Native `ddd2311`；开发测试自然结束后，以下 oracle 串行运行，每项新目录保留实际 GM XT、真实 PK 接收/compare/BODY_check、原结构/有限 section/材料查询和点值物性门。没有新正式 ABBA；下列毫秒为开发 API 观测，含冷启动效应，不与旧 PK 时窗签倍率。

| 独立操作 | 原始退出 | root cells / roots | Jacobian | full residual directions | 直接 XYZ 列 | API ms |
|---|---:|---:|---:|---:|---:|---:|
| curved block subtract | 0 | 1062 / 84 | 2688 | 5376 | 8064 | 691.020 |
| reloaded curved block subtract | 0（同一 oracle） | 4263 / 113 | 8771 | 17542 | 26313 | 459.496 |
| plane/NURBS 独立 fillet | 0 | 150 / 8 | 4648 | 9296 | 13944 | 1091.432 |
| plane/NURBS lifecycle | 0 | 150 / 8 | 4648 | 9296 | 13944 | 1274.451 |
| shortedge 独立 FixBlends 对照 | 0 | 220895 / 3518 | 526133 | 7023 | 0 | 6643.885 |

所有 budget failures=0。前三项 root/SSI/winding 数与初版 `241b8f3` 开发证据一致，新增直接列没有改变其隔离工作量。与 6616 开发固定记录相比，curved Jacobian 18464→2688、reloaded 62358→8771、NURBS 12232→4648；这来自区间相关性减少，不能称为同倍 CPU 减少。shortedge 未命中 G，保留 SSI4956/winding196、求界67137、成功命中96448/失败状态命中90473。

NURBS 独立操作保留九个有限 section 的半径/双接触/切向检查；生命周期单独包含 self XT receive、copy、源删除、mirror-scale、PK full check/compare/点值 mass 与 cleanup，raw0。本包未新执行 fillet 后 Boolean 组合。shortedge 输入仍为真实 PK seed，由 GM receive 后执行本系统 FixBlends，不能宣称该输入由 GM 独立构造。curved 的 tracking/roles 限制仍按原 capture 保留；原点值物性门不能升级为未整合功能源码的新 modulus 三态全部 Verified。

原始日志、TRX、captures 位于 `temp_docs/pk-native-algorithm-work/cartesian-regular-frame-*` 及 `cartesian-frame-curved-capture.json/`。实际 XT 位于 `temp_docs/regular-curved-boolean-oracle/cartesian-regular-frame/`、`temp_docs/regular-solid-blend-oracle/cartesian-regular-frame/`、`temp_docs/regular-solid-blend-oracle/cartesian-regular-frame-lifecycle/`、`temp_docs/short-edge-blend-oracle/cartesian-regular-frame/`。curved runner 的 reloaded 中间 source XT 现也遵循本次输出目录，默认目录位置未变；本次 runner diff 单独保留，未改变 6616 固定测量 runner 原件。

下一集中审查应对实际 Native diff、上述可逆等价、原 closest-foot/witness 及有限分支质量共同判断；这组局部数学测试自身不提供 Parasolid 模型架构接受。下一性能主线继续追踪已定位的 conic tube/有限根开销，不因等待看板发布停止。
