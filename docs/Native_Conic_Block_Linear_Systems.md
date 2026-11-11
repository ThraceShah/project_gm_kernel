# 原七变量 Conic 分块线性计算（开发候选）

本候选基于0684efd/16aca工作区分支，只优化已审G+Conic中严格限定的n7/two-occurrence Circle/Ellipse结构。原P.xyz、Q.xyz、q、七个残差、原生C(q)/CHART/支撑/sense和有限分支保持。没有新几何或参数化、容差/预算放宽、SkipLocalsInit、缓存扩展、历史兼容或失败回退。

## 数学与实现

原JG按S=(0,1,2,6)、D=(3,4,5)重排行/列，精确为`[A,0;B,I]`，B只有q列非零。点预条件器取当前G点Jac各区间中点，用4×4小LU产生近似R_A，再构造`R=[R_A,0;-B_mid*R_A,I]`。原七维严格包含、contraction<.9、6/6 expansion/tightening、所有positive/finite/CHART/nearest/witness门继续验收；R不是证书。n4为线性工作维数，小LU沿用该维数既有数值合同，几何容差和根预算未变。

centerF复用同次G点Jac生成的Value，删除不再使用的旧F→G逆变换。额外点Jac计入真实统计。`I-RJG`三个Q列严格为零，其余保留原非零项及原k顺序，与通用Precondition49项逐位比较。原全7维收缩与包含循环仍消费完整矩阵。

导数区间用`A*x_S=b_S`、`x_D=b_D-B*x_S`，输出全部七项，仍以原向外区间和既有SolveIntervals求解。原explicit-child nearest子块`[I,-C';0,phi_q]`对(0,0,0,1)的末项为1/phi_q；有限phi_q的upper<0等价原负性门，phi_q仍包含完整`delta·C''-|C'|²`。其他结构及capture/requireWitness走原方程，没有删除其证明。

## 已执行质量与身份

19项专项/相关检查原raw0：块逆/七导数包围、49个余项逐位、nearest与原子块求解一致及正/含零/奇异负例、零托管分配，原Circle/Ellipse/支撑顺序/tilted-cylinder/native derivative/witness与初始化合同。

当前旧性能源上的完整curved/reloaded Boolean、NURBS独立fillet和standard shortedge三fresh pairs，各原raw0。对应Capture分别52/28/189检查全部pass，captureErrors空；检查器0不替代这些原exit。curved roots84/113、Jac2688/8771；NURBS roots8/cells150/Jac4648；shortedge roots3518/cells22371/SSI4956/winding196/budget0/bounds47246保持。shortedge Jac16870→18951、方向108710→123277，增加2081个点Jac，不能伪称Jac减少。

实际loaded start/end Native MVID `0af769b4-c4e4-4744-a1be-4491692c463a`，2115584 bytes，SHA `0451c112474e22cf60ecfcbe5507673ba0da85c3d00c78bffa3bbba0f1170015`。普通DLL/PDB及testhooks DLL/PDB、原日志/exit/TRX/输入/XT/Capture分别保存于 `temp_docs/pk-native-algorithm-work/conic-block-*`，driver为`RunConicBlock.cs`，数学/实际root分布见`ConicBlockRationale.md`。开发API ms curved582.872/reloaded251.206/NURBS1138.612、shortedge1461.010/718.799/551.559不构成配对性能结论。未为此重跑58/fullsuite/AOT/五操作正式ABBA或发独审。

尚未证明分块实现的完整净收益，当前仅为隔离候选。通用Bezier等参预备c10bf4a及等端点blossom090848e均已根据开发比较排除，不在本源。

## 统一基线与下一步

已实际读410748d统一报告，并核f9ffc09到4b2405e在Geometry/Evaluation、Geometry/Bounds无差异；统一Native133f56ac现已通过独审583d917。其完整1974/1974/raw0和mirror8几何成功属于该统一源，不补签本旧分离性能源，旧失败原件不改。当前runner的B1证据门修复由原owner负责，修复前不凭audit0签新性能质量、不复制旧点值门绕过。当前数值Unresolved/raw2保持。

后续在固定统一计算源准备本候选与16aca，只沿用当前oracle/helper、SourceIdentity和三态质量；不混入旧点值wrapper，不继承217fc倍率。217fc修订上线211605已结算，原件固定。继续实际根/求界/预条件成本及完整质量/净收益闭环，尚未达到整个PK性能目标。
