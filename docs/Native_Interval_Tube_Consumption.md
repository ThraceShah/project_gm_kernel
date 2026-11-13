# 原生隐式 tube 的子区间消费

本机制正在本轮计算主线验证，不是 PK 内部算法的已核结论，也不代表整体性能目标完成。
官方 XT 的 CHART/支撑/原生参数定义不变。MCP 和动态行进/追踪观察见
`docs/Native_System_Alignment.md`；这里的误差推导属于 GM 自身实现。

## 原问题与边界

已有命令内完整 tube 工作区只消费完全相同的 U 区间。有限根、裁剪与分类不断请求
更小区间，通常再次运行整个存在性、唯一性、nearest 和微分求解。CHART 点及数值
Newton 结果本身不能替代这些证明。单纯减少 CHART 反而可能扩大后续区间工作。

本次仅扩展直接两张非周期 C1 BSurface 或 BSurface/Plane 的已有 native UV 系统。
仍使用同一 offset foot、CHART 方程和原 native 参数单位；周期、嵌套 Blend、显式
spine、shared Cartesian witness、reach/curvature 请求不借此取得新资格。
没有新增模型、XT 字段或持久缓存。

## 可以消费的父证明

父区间 P 上，既有完整求解已证明原方程 F(x,t)=0 的唯一连续根族，完成原六轮
refinement、隐式导数和 nearest 检查。另以向外舍入保存初始包含盒 X 上的证据：

- 原点预条件矩阵 R；它不是缓存的区间 Jacobian。
- 正的 native UV 权重 w，令 D=diag(w)；它只是计算范数，不重定义几何或参数化。
- 对整个 X 与 P 有 c ≥ ||D⁻¹(I−R J)D||∞，且 c<.9。
- 初始包含盒 X、同一 CHART 编号与完整父根/导数包围。

父请求仍执行原证明全部步骤。只有完整成功、存在当前 immutable 工作区、并且
向外舍入的 c 也满足原门槛时才保存额外资格。普通/失败写入清除该资格。

## 子请求的证明

Q 必须包含于 P，并与父证明使用同一 CHART 方程。子请求原有的数值点求解仍只
执行一次，保持20次上限；它向两种证明提供同一个候选 z，不在父证明拒绝后重跑。
其站 t₀ 也必须属于 P，且 z 必须位于 X。

在 t₀ 的真实根 x* 与 z 之间应用预条件均值方程，得到：

`||D⁻¹(x*−z)||∞ ≤ ||D⁻¹ R F(z,t₀)||∞ / (1−c)`。

F(z,t₀) 用原支撑的向外区间求值计算，不能使用浮点残差或建模容差冒充误差界。
由此取得 t₀ 的 UV 包围与 offset foot 的 XYZ 包围。父导数和均值定理再覆盖整个 Q，
并与已有父盒相交。所有相交必须非空、有限；不存在从空盒签成功的分支。

父导数可能跨过驻点，不能一直充当子请求的最终导数。子请求在新的根盒上重新
包围自身 Jacobian，并取一次局部 Krawczyk 像，消除统一加权范数混合 UV 单位造成的
多余宽度。Jacobian 域包含根盒和数值候选的整个凸包；候选仅在父初始盒内并不足以
证明它位于较窄的子盒中。该像只收缩已经证明包含真实根的集合，不冒充新的存在性
证书；仍保留同一次原点预条件矩阵、原参数方程及向外舍入。
随后解原隐式微分方程，再与父导数相交；截面 frame 按请求的 V
重新计算。本次 Jacobian 产生的同盒 spline jet 可在同一次 frame 中消费。
不能因父导数含零便把任意小子区间的单调性长期判为未知。

父证明不满足全部条件或新微分包围不能完成时，原完整证明继续使用已经取得的
同一个数值点；原区间访问/细分、精度、收缩与有限域门槛不变。派生子结果不写成
新的父证明，避免把未证明的更强资格逐层传递。

## 身份、生命周期与原拒绝方向的区别

复用继续受实际 tag、generation、sense、准备类别、program block、完整 program/
workspace 尺寸、spine 与 CHART 限制。不能因为 XYZ 接近而合并来源。父 U 包含
关系不是 CHART 接点两侧方程相同的证明；接点按原选择规则消费自身 gauge。

证据只活在已有 immutable command scope 的 native 内存中，scope结束释放。
ValidateRegularBlendDomain 在进入既有 immutable 范围后取得真实 SurfaceRecord
owner 的准备视图；缓存不越过后续拓扑发布、变换、删除、copy 或 receive。
没有放宽 source/workspace 身份，也没有扩大工作区容量或几何预算。

这不是9d的成功点位置包围携带，也不是675的重复 Jacobian 盒复用：仅有点包围
不具备父资格；父完整证明仍执行六轮，子区间还重新求自己的 Jacobian 与导数。

## 当前验证状态

`native-parent-tube-managed-v5` 64/64 raw0，覆盖真实 regular modeling、错误来源/
generation/sense、失败不保存、shared witness 拒绝、解析平面/双曲面、两侧顺序、
native U[-2,3]、镜像/尺度、无托管分配及不同 CHART 接点导数负例。

开发控制 `native-parent-tube-derivative-control-v1` raw1，2/4失败：父导数跨驻点时
不能证明正侧子区间的斜率。新实现重新求子 Jacobian 后上述控制在完整64项中通过。
早期 whole/profile 是开发源，不能当最终固定源独验。

更完整的 `native-parent-tube-rational-v1` raw134：两个下游再建模 Boolean 在原深度40
仍不能收紧有限根包围；各上游独立 Fix 的原通过保留。`native-parent-tube-full-managed-v1`
raw1，2192/2196，四个真实 fillet reload/cut 构造失败157。原失败生产 Native
46ece2e090fbb75dfe2388616ee2da63d2e29d5305ce641d4da62fb49fe6047a、test程序集和原退出
封存于 `temp_docs/native-system-alignment/native-parent-tube-failed-{production,test}-v1/`。

加入局部像后 `native-parent-tube-managed-v6` 70/70 raw0，恢复原四个失败构造。
随后补齐 Jacobian 凸包前提的生产 Native
4ac5cdd105bef3c175923b16c12ccc7ca2511b7de479b816c2e6895f4e0621f6
执行 `native-parent-tube-rational-v2`，三族反向有理 native U[-2,3]、三尺度九项
copy/delete/reflection/reload/remodel、原 Fix 与后续 Boolean 独立 fullcheck/strict
全部通过，raw2，物性 U 照存。最终 `native-parent-tube-full-managed-v2` 2196/2196
raw0、IL guard3178 CLEAN，testhost自身退出。
现有 b08 固定包保持原字节；以上为当前增量 owner 验证，不是独立数学接受或整个任务完成。
