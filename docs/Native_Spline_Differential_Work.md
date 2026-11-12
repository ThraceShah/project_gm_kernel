# 原生样条共同微分工作

从已接受17eedbf/Joint185/Native116的隔离树推进，不包含cut950已拒方案、诊断38计时器、新scope或扩大缓存。实际项目AGENTS及两架构已读并核与已读接受基线相同；当前src/scripts起点无diff。方向依据 `diag/accepted185-nurbs-cost` 的 `docs/Nurbs_Evaluation_Cost_Observation.md`：Jacobian与Bezier导数Hull约占两个诊断暖API线程墙钟的三分之二，这不是排他CPU或预签收益。

Cartesian G仍为原完整五未知量P.xyz、原生UV，原plane/CHART方程、正则性、nearest、完整outward inclusion/收缩/导数求解、budget和容差保留。新增私有SplineDifferential仅把U、V两方向的共同标量Value运算做一次，各方向的加减乘除、平方和平方根导数仍按原IntervalDual表达式逐项外舍入；没有新几何定义、UV gauge、近似导数或采样证书。XYZ仿射列和两个UV列一次装入同一25元素矩阵，plane/CHART的原IntervalDual结果仍用于UV常量方向。资格范围仍是原非周期C1 plane/spline单occurrence，不扩到其他程序。

Bezier BoundSecond现在只求实际请求的总阶≤2的18分量，保持原矩形公式的商法则运算顺序。IntervalSplineBounds原生域分支使用这18分量；线性扩展分支保留原27分量的矩形二阶布局，其三/四阶混合导数仍参与精确tensor-product线性扩展。未恢复已拒isoline准备或point blossom方案，未改变导数网、参数域、拥有关系或证明预算。

初始定向93/93、原exit0。原SplineCartesianRowTests的25元素逐位方向对照、rational/两种顺序/extension/变frame导数/正则性负例/nearest/witness及零分配保持原期望。新增BezierSecondJetTests对U/V次数0、1、2、4、8、16交叉、rational/非rational、点/边界/区间/轻度Bezier数学延伸盒，18个分量逐位对原27分量映射；短输出、非正权失败和零分配同时通过。也执行原Bounds/SplineCache/SupportingBlend/完整RegularSolidBlend消费者。日志/TRX/初始diff在 `temp_docs/spline-differential/`。

后续一次完整API开发对照仅观察−3.617%且波动明显，本实现不进入正式候选，结算见 `docs/Rejected_Spline_Differential_Work.md`。93项已通过数学/状态控制及原12pair证据保持，不追加全套、ABBA或字段微审；后续从已接受17eedbf独立推进，不沿用本实验或以工作量代签完整收益。
