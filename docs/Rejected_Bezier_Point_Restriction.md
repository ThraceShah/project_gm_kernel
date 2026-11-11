# 等端点 blossom 复用实验：不进入下一生产包

本实验从0684efd/16aca仅缩scratch源出发，在RestrictCurve的a/b区间上下端点均逐位相同、degree>0时，将原degree+1次完全相同blossom计算一次，再逐位复制。独立导数网限制、原初始化/算术及proof/预算不变。

8个新增原始通用blossom逐位对照（degree0..16、16×16、点/等参/域外及普通盒、带区间不确定的齐次控制网）和5个已有独立曲面位置/导数检查共13项原raw0。完整curved/reloaded Boolean、NURBS独立fillet和std shortedge3fresh pairs也原raw0，原PKfresh/拓扑/有限几何/物性门保持。Native loaded start/end MVID1b1b807d-ca3f-48fc-9068-da98a09e77c0，DLL SHA975cd35a9d45179e584207e78fa7cf11e85f5435c3a4cf30e9b15d1d8da55454，原DLL/PDB另存。

对同一封存compiled runner/完整相同托管依赖和同std输入（angle1/radius.5/tol1e-7），替换正常引用的16aca9c6DLL和新975DLL，两fresh进程/source、每进程六fresh pairs，依序baseline/candidate/candidate/baseline：

|进程|来源|冷GM ms|全部五暖GM ms|暖中位数ms|
|---|---|---:|---|---:|
|0|16aca|1529.0223|682.7923,509.5359,503.6327,497.6937,512.7855|509.5359|
|1|point|1332.0540|702.6599,536.0677,499.1569,528.8941,483.2224|528.8941|
|2|point|1631.9706|809.8983,573.0518,510.0592,536.0235,526.0555|536.0235|
|3|16aca|1588.9260|687.9066,541.2960,561.0427,568.2830,493.1550|561.0427|

24pairs原raw0、captureErrors空、checks全pass。十暖中位527.04075→532.4588ms（观察+1.028%），两轮变化方向不同。独立NURBS单次冷API1386.534ms亦不能证明跨进程回退或收益。源内算术重复减少没有转化为此开发对照中的可重复整模净收益，故不再追加窗口寻找好数字，不纳入下一生产包。数学通过和性能不选分别记录。cells22371/roots3518/SSI4956/winding196/Jac16870/bounds47246/budget0保持。

本分支仅完整保存实验源码/测试/不选理由。后续从0684efd继续，16aca的单独观察收益不与本实验或正式217fc跨窗相乘。原217fc等正式证据不改，不另启58/fullsuite/AOT/五操作正式窗口/字段微审。

原证据在 `temp_docs/pk-native-algorithm-work/` 的 `bezier-point-*`、`bezier-point-restriction-*`、TRX、fixed app/binding、DLL/PDB，以及 `RunBezierPoint*.cs`、`AnalyzeBezierPointDev.cs`。全部冷热样本、先前尝试均保留。下一步实际盘点解析组合的root work，主攻求界/根隔离而不继续积累这类静态减少。
