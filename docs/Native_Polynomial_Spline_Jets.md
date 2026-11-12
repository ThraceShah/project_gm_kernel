# 原生非有理B曲面的多项式Jet包围

隔离实现从17eedbf/已接受Joint185/Native116出发，不含cut950、4e5b584或67553ed实验，也不含诊断计时器、缓存/scope扩大。实际AGENTS及两架构已读并与已读接受树逐字核同。

Parasolid官方PK_BSURF_sf_t Documentation Notes明确vertex_dim=3是三维多项式曲面，顶点直接为XYZ；vertex_dim=4是包含XYZW的三维有理曲面。PK_BSURF_create同时规定polynomial维数3、rational维数4。此次按parasolid-api-doc-skill实际读这两页；没有把采样拟合或猜测值当几何依据。

原IntervalSplineBounds对dimension3/rational=false的真实原生source仍走齐次有理商法则。Bezier提取的第四通道虽可能含外舍入误差，但原多项式的权重函数恒等于1，它不是待求的非恒定有理分母。现仅凭该真实source定义，直接对提取XYZ的Bezier多项式做原凸包、导数控制网及外舍入限制，避免对人工第四通道重复求商。没有改变原控制点、knots、次数、sense、XYZ/UV/native gauge或拟合替代面。

有理source继续完整有理路径。周期/多span枚举、原生域和tensor-product线性扩展公式保持。多项式路径仍计算原27项矩形二阶布局，包括扩展需要的三/四阶混合导数；没有把曲面域外当多项式外插。所有inclusion、nearest、finite CHART、tolerance、budget、ownership/rollback及当前Compare/Mass/Capture门保留，未做C ABI/Windows/NativeAOT改动。

初始定向92/92原0，含原有理/非有理、原25矩阵逐位方向对照、C1/periodic资格、degenerate、错误witness、nearest、extension及真实SupportingBlend/RegularSolidBlend消费者。新增独立二项分布矩验证所有27分量及零分配/短输出；另用真实u^4 v^2控制网验证域外(1.125,1.25)的原生tensor线性扩展值2.25、du=6、dv=3、duv=8、duu=dvv=0，不能用多项式外插冒充。原日志/TRX/diff在 `temp_docs/polynomial-jet/`。

后续实际完整API开发对照仅观察−1.797%且波动明显，本实现停止、不进入正式候选。完整原件、两端实际加载源绑定及结算见 `docs/Rejected_Polynomial_Spline_Jets.md`，不追加全套/正式ABBA或微审。质量/数学控制结果保留，Unresolved/Missing/raw exits分层；后续不依赖此实现。
