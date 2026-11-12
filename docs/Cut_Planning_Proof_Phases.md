# 原生切割规划的只读求证阶段

从已接受Joint185的Native116和17eedbf出发，仅减少构造期间真实重复的求证工作。185/e139/17eedbf、6c75质量包和5a22窗口包保持原样。完整接受依据是e812b7a003e1bebf0699afab5e9c212023220e99的 `docs/reviews/advanced-modeling/joint-1850534-independent-review.md`；其有限接受、Mac2019/2019、原16exit2/4exit0/runner2与逐rep工作量限制沿用。

现有e17原owner追踪中，NURBS构造阶段542次成功tube包含376次contact-plane根查找和104次整带range求证。完整语义键359/183重复只是方向依据，不能授权整个Fix不可变，也不是排他CPU成本。新增阶段依据下列实际调用链，而非把同数值或同数组长度当source身份。

| 阶段 | 固定输入和输出 | 修改边界 |
|---|---|---|
| IntersectBlendContactPlane | 实际ICurve的contact map、Blend及支持；原有限站位、每个原CHART的根、原ICurve gauge映射和排序 | 只写caller-owned数值根；EvaluateBlendContact、BlendContactInterval和AppendBlendContactRoots不创建/删除实体，Find/Bound只写计算scratch及可选工作区 |
| TryBlendContactPlaneEvents | 同一实际contact/Blend；严格根隔离、完整有限gauge单调认证、原CHART seam双投影和空间误差 | 只写数值根/隔离区间/误差；原512 gauge预算和strict separation不变 |
| TryCertifyBlendPlaneIsocut | 由同一真实SurfaceRecord.DataIndex读取程序，按该owner准备一次；全v整带value、du单调性、两个contact存在性和最终残差 | 仅返回empty或经原检查的共同u；借用view及局部阶段已结束才进入NestedBlendIsoparam创建circle和FiniteSectionChart发布 |

GetNestedBlendProgram(data,owner)沿用已审真实tag/generation/sense/kind/block、program/CHART/root和工作区合同。只有原完整n维inclusion/nearest/导数成功tube可复用，v-frame继续重算；特殊witness/有限证书需求保留原路径。不修改几何、native参数化、容差、预算、错误映射或测试期望。

ModelingPerformance.Scope增加显式reuseCurrent选项：已在不可变父阶段时沿用父工作区并不重复合并/释放；其他情况下为这个已核实纯调用建立短阶段，继承真实RootUse，结束时合并工作一次并释放自己的可选工作区，恢复mutable父阶段。它不允许跨创建、变换、receive、回滚或命令使用借用view。Scope不存入模型，没有新的共享持久状态、证明scratch配额或公开C ABI。

最初定向验证77/77、0fail、原exit0，包含原完整RegularSolidBlend/BlendSheetPartition/BlendContactEndpoint/BlendIntersection/NestedBlendCacheEmptyReadSet消费者、JointTube与ModelingPerformance检查。新增控制覆盖父workspace保持、退出后的原生证据失效、RootUse继承且只计一次、缓存关闭/可选分配失败仍逐位得到原包围界。原日志、TRX和源码diff在 `temp_docs/cut-planning/`。

当前仅为已验证小步源码进展；完整产品质量、实际完整建模收益及下一集中交付仍待执行和独立接受。后续使用当前runner/Compare/Mass/Capture及真实PK，原Unresolved/Missing/raw2分别保留，不用局部工作计数预签速度或PK目标。
