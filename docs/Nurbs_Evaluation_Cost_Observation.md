# 已接受Joint185数学路径的NURBS求值成本观察

隔离诊断分支 `diag/accepted185-nurbs-cost` 从17eedbf/Native116出发，仅加嵌套计时，不含已拒cut950机制。实际执行源 `38d56ea76753ab56391f7194a96563793d70c994`、诊断Native树 `991cbf4485190f2cfe40bc765c33223b419514c4`；不是未改动185二进制，也不是新的性能比较/优化候选。

一次现有plane-nurbs rounding-only三fresh GM/PK pair，当前runner/Compare/Mass/Capture，GM和PK全部API原0；producer/runner原2、79 outstanding，Unresolved/missing保留。每rep原Jac4179、directions20895、residual8358、root cells150/roots8/winding18/budget0，未改数学、证书、容差或参数。栈最大实际3，计时深度上限64，dropped0；不运行额外模型/家族/全suite或ABBA。

计时器只在真实ModelingPerformance捕获scope内生效，按实际返回/异常的嵌套边界记录Stopwatch ticks；普通value struct/ThreadStatic inline storage，不在Native格式化或I/O。字段随原子scope工作合并一次。elapsed是包含被调时间，own是减去已计时直接子调用后的**线程墙钟**，不是排他CPU；没有独立估计探针/JIT/layout/调度影响，不能把own直接说成可删除耗时。未覆盖工作也不伪归给任一分类。

| 分类 | 每rep调用数 | 暖rep1 own ms | 暖rep2 own ms |
|---|---:|---:|---:|
|EvaluateCore point|949|10.541|5.039|
|BoundSegment tube剩余|664|40.062|27.563|
|IntervalFrame剩余|1272|9.887|7.197|
|IntervalJacobian剩余|4179|199.593|165.704|
|NestedSplineEvaluation.Jets|1877|5.469|3.960|
|HomogeneousDerivativeHull|30622|181.296|184.764|
|Interval Weight|0|0|0|
|Numeric spline jets|5825|5.580|4.565|

暖API原574.514/529.106ms，profile elapsed574.441/529.057ms；冷1100.060/1093.266ms单独保留。仅相加已扣子调用的Jac与BezierHull，约占两个暖profile墙钟的66.3%/66.2%；不把包含子调用的Jac239–261ms与Hull再重复相加，不把这一比例签为CPU或预签收益。

这次观察否定了先凭数量猜“求点/角度权重是NURBS主成本”的方向。下一候选先研究原生UV微分与导数包围的实际重复计算：现Cartesian G只直接装XYZ列，两个UV列各重算完整变动frame；Bezier导数hull仍按每次请求构建/限制。必须沿原完整五未知量、XYZ/UV/native CHART、正则性及nearest/inclusion合同实现和验证；不能沿用cut950的已拒阶段复用、拟合几何或放宽证明。

原件 `temp_docs/nurbs-cost/run/`；oracle.log SHA `afc63afea927cf85e5d753755c5a26e344f2f95a0a8e8aa88c8727fb666cab96`，report SHA `b1cdbe5ef881e62b79a5e0854e6ca977ec85580ba64812dfe496dfec999a6f10`。loaded身份、原source清单、DLL/PDB、Capture/XT及所有冷暖原COST行均保留；此报告不增加独审或数值资格。
