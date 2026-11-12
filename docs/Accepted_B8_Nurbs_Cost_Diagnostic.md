# 固定接受b8源码的NURBS实际调用成本核查

从接受b8/e7/Native6a843独立建立，只外部采样/源码读取，**Native/src无diff**。实际读本树AGENTS/两架构且与接受源字节相同，native私有环境ensure原0/ENV_READY。dd1fd0d和b095均因唯一完整窗口没有目标收益而停止，原2035/2034质量、root工作变化和全部负结果不改，不能再自动叠截面前置机制；这里重新核查实际主调用路线。

一次有限3fresh GM/PK完整NURBS对，cold+rep1/2与当前窗口序号一致，真实接受b8源码普通制品。制品身份来自停止dd窗口的**baseline** archived actual-loaded（不是停止候选）：Native SHA `13eae9e128cf2010ff7aee47cf3ce548eed060001312cd8b006e240e4642bcc6`，逐实际dual Native/Oracle hash在apphost cache选择，并核前后未变；该baseline source manifest336 tracked/PE-PDB/Git在原窗口已核。没有把旧e7窗口不同bytes当本制品。实际collector0、producer **raw2** 分别记录；GM963.817/491.603/443.335ms，三对6API均0，captureErrors和数值U/missing保持。未加Native callback/函数计时器或改建模参数/验收。

外部dotnet-trace以CLR startup suspension attach，dotnet-sampled-thread-time原trace/Speedscope/loaded/命令/日志/退出码/Capture/XT在 `temp_docs/accepted-b8-cost/`。它是受观察执行，**不能用于性能接受、排他CPU或可删成本**；CPU_TIME/UNMANAGED_CODE_TIME为converter合成叶子，非真实native函数，缺栈/内联不可签“没有调用”。无独占机器/温度或原window计时替代。

本次所选FixBlends线程墙钟1888.7517ms，互斥可见栈分类：

| 可见路线 | sampled thread-wall ms | share |
|---|---:|---:|
|Jacobian，无可见spline-bound帧|693.4534|36.71%|
|projection position restriction|494.0371|26.16%|
|other Fix|386.0941|20.44%|
|projection derivative restriction|116.2403|6.15%|
|other joint tube|113.1888|5.99%|
|Jacobian其他spline bound|38.4153|2.03%|
|Jacobian homogeneous jet|36.8039|1.95%|
|other projection|10.5187|.56%|

不能把第一行当“spline之外CPU”；693ms主要最近可见IntervalJacobian及wrapper，内联callee被隐藏。三rep各4179Jacobian/20895directions/8358full残差directions/12537直接XYZ列，record builds/rebinds0。Spline直接列优化已在实际NURBS运行，不得因无record误判优化未启用。每repSSI0/rootcells150/roots8/budget0、3D requests628/evaluations452/hits26保持，冷暖分开。

实际源码 `NestedBlendBounds.IntervalJacobian` 每次有UV occurrence时清局部spline contact cache，再验证/生成相同source/UV的完整18项几何jet，随后Cartesian直接XYZ列和两UV AD residual依次消费，nearest/参数RHS后续仍独立检查。`BoundSegment` 中该cache是单次source/nodes/fixedCharts固定的caller stack span，从EvaluateCore得到的同一occurrence图开始，只数值boxes按原六expansion/六contraction改变，Find内program借用存储不编辑。这个有限纯阶段是可研究的实际scope，不能将整个Fix当不变，也不能在另一个program以相同scalar offset/degree当共同owner。

下一假设：将完整jet cache的初始化移到该实际局部scope入口，在UV盒二进制相同且源身份/元数据仍为该固定occurrence时复用原已成功完整几何jet；**全Jacobian、原Krawczyk/remainder、frame/nearest/reach/witness/UV/CHART和预算仍每次原算**。这不同于已拒4e5共同UV微分/第二jet（其完整开发仅−3.617%）或675稳定contraction/1762多项式jet，实际已读拒报告，不带其源码。当前只是源码/调用证据，尚未实现或验证复用安全/完整收益；先验证真实source稳定/未初始化/失败污染/盒变化控制与完整API，弱即停止，不以采样share或1200旧unchanged次数预签收益。

该诊断不补旧回执，不是新算法接受/main/上线/整体PK达标，不另开微审；原79920整体目标继续从接受b8，暂不混移动query/norm，无Windows/AOT/新suite/ABBA。
