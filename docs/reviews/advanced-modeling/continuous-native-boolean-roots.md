# 连续 Boolean 的原生有限根

本次计算改动接续 `a0f68cb9e729b89309d7a203fa205ad9014c0a16`，不修改 Blend owner 的方向查询或比较 helper，不改变 XT 类型、CHART 方程、持久参数化及容差。

## 实际问题及 PK 依据

共同输入先从 NURBS roof 减去 block，再与第二个不同 roof 做并、交、差；分别直接操作及 XT 重载后操作。原先六项在有限面 arrangement 失败。两个原因相继暴露：三支持根只保留近似空间点，丢失共同 native UV 与唯一性域；自然边界 BCurve 与 BSurface 的几何载体相同，但 native 参数不必相同，分别反求产生极短的额外区间。

已实际使用 `parasolid-research` 技能、门户 Parasolid Query MCP 的函数定义与错误码，以及本机匹配 Linux x64 PK 38.0。`NativeFiniteSsiBoundaryOracle.cs` 读取实际失败输入字节，复制其实际支持曲面为 orphan 后求交；GM 生成的 wire 经真实 PK receive、完整 BODY_check。输入 SHA256：

- target：`06bf15b755ce844303bd634076d79702fae93663cfd008b23b4e1da38983b86d`
- tool：`88b1657077c6d4a640cc443c74b048dde59c30d5ad7102e5efe9bdfa98bf753d`

`ssi-native-parameter-v3.log` 实测 GM 原生区间 `0 / .7306956275376494`，fresh PK 区间 `0 / .7306956275376496`。真实 PK 在有限边界的 `SURF_intersect_curve` 根与 `CURVE_parameterise_vector` 均返回其末参数；将整个 native 曲线区间人为扩大则返回 120。此证据不授权 epsilon 吸附或扩大输入范围。静态符号和反编译预览仅作研究线索，没有将地址或伪代码当作定义证明。

## 实现

- `SurfaceTripleRoots` 保留六维 Krawczyk 根盒及唯一性域，严格检查原生有限域，并用同方程的根证书判断重复；去掉原 UV 距离去重。原累计 cell 预算不变。
- ICurve 与非平面 BSurface 的三支持根携带两侧 native UV，通过当前 CHART 方程定位目标有限参数；原有已证明的平面降维保持自身适用类别。
- BCurve/BSurface 根保留完整隔离证书。同系数参数映射继续使用原 native 方程；不同参数但具有两个完全相同恒定 Cartesian 坐标、严格单调第三坐标的 Bezier 边界，反解实际 Bernstein 多项式。根用外包围，唯一性域使用内像，不以区间重叠冒充同根。一般曲线不因此获得新的几何类别。
- 对本次完整 SSI 新建的 open ICurve，在实际自然边界上重新隔离同一参数化方程；原端点 UV 选支、共同唯一性证书证明同根后，使用该 provider 声明的末参数。空间距离仅按原 CHART error 排除候选，不决定身份。已有输入裁剪曲线不套用新 provider 的端点证明。
- 每一步 GM 与 PK 建模分别实际执行；前序产品保留独立 PK live copy 对照，后序失败不倒扣前序。所有新的 before/after XT 写进独立输出目录，不覆盖旧证据文件。

## 验证与状态

`native-boundary-regression-v2.log/.exit`：**301/301，原 exit 0**，allocation IL guard 2969 方法 CLEAN。包含连续/重载六项、同域不同 trim 全 overlay、原有限解析负例、三支持及 offset corner 根，以及不同 native 参数的正反向 BCurve 和 `±1e-13` 偏离边界负例。命令：

```
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests/KernelTests.csproj -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~RegularCurvedBoolean|FullyQualifiedName~FinitePlane|FullyQualifiedName~BCurve|FullyQualifiedName~IntersectionEvent|FullyQualifiedName~Parametric|FullyQualifiedName~SurfaceTripleRoot|FullyQualifiedName~OffsetCornerRoot|FullyQualifiedName~NativeSplineBoundaryRoot'
```

开发运行 `native-boundary-native45-v1` 的六项 continuous/reloaded 均实际 API 0，fresh receive、完整 BODY_check、原严格 AssertEquivalent、有限 UV 与材料门通过；该运行另有 12 项同域回归失败，**整体原 exit 134，不能记为 45 项通过**。随后按 provider/平面能力收窄新定位器，301 项已验证修复。固定计算候选 `11c49613f92dc3317118f91d43332ce19ec5a9f7` 的 `native-boundary-native45-v2.*` 已完成：**45/45 几何/拓扑/材料通过，原 exit 2，40 次物性比较未决**。每个前序 subtract 另有独立结果，不计入这 45 个最终操作。全套单测 `native-boundary-full-suite-v1.*` 的圆柱重载两项失败及窄修复见 [有限支路关联](native-cylinder-generator-association.md)。

```
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RegularCurvedBooleanOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=native-boundary-routing-fixed-v2
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests/KernelTests.csproj -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true
```

所有日志在 `temp_docs/pk-native-acceptance-audit/`；实际新产品在 `temp_docs/regular-curved-boolean-oracle/native-boundary-routing-fixed-v2/`。该 45 项实际 GM assembly SHA256 为 `3cd502554fb5ffb09ef477571b0f4c69f8a63f5c8fe24251ccde23ac82e663b3`，计算源码及脚本 manifest 为 `native-boundary-fixed11c4961.source.sha256`（SHA256 `44dade388049fdfc40826f0854e2c6bfc7505a957fcab66f32b9aa9c676d8184`）；每个独立 file app 另记录其实际 loaded assembly，不能把不同构建元数据产生的 assembly hash 混写为一个。

同一计算增量还实际执行 shortedge 标准自构造、copy/delete 源、反射/缩放、两次后续 cut 与 XT 重载闭环：`native-root-standard-lifecycle-v1.*` 几何/拓扑/有限 ICurve/UV/材料通过，原 exit 2、8 次物性未决。`native-root-consumer-forward-v1.*` 与 `native-root-consumer-reverse-v1.*` 均为同字节 GM/PK 两工具 Boolean 成功，原 exit 2、各一次物性未决；`native-root-consumer-inject-v1.*` 则 GM 实际 901/failed/零产品、三个输入原字节回滚，PK 同输入实际成功，控制原 exit 0。注入控制不算一次成功的 GM 建模操作。

当前共享 mass bound 合同继续独立返回 Verified / Different / Unresolved；物性未决不签署为完整验收通过。该候选尚未独立接受、合入 main 或发布新看板 run。
