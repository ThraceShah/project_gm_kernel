# 全局样条投影按位置需求计算保守包围

从已接受17eedbf/Joint185/Native116隔离实现，不包含cut950、4e5b584、67553ed、1762f12等未保留实验或诊断计时器。实际AGENTS及两架构已读并核与已读接受基线相同，初始源码无diff。

前几项计算改动未取得值得保留的完整收益后，停止按插入式计时比例猜CPU贡献。对未改动接受源的真实Native/Oracle DLL先按原loaded hash选定apphost，再做一次六fresh pair EventPipe采样；采样所选FixBlends线程墙钟3526.6891ms，其中全局BSplineProjection栈1496.9297ms（42.45%）、IntervalJacobian1414.5231ms（40.11%）。它们是采样线程墙钟，不是排他CPU、可删除耗时或预签收益；CPU_TIME/UNMANAGED_CODE_TIME是转换器合成叶子，不冒充真实方法。原采集、Capture、loaded身份和.trace在接受基线树的 `temp_docs/unmodified-nurbs-profile/`，无Native计时探针或模型改动。

真实调用链为CreateGeneralRollingBlend→RollingBlendSample→NestedSplineEvaluation.Distance→Closest→BSplineProjection。原全局分支下界仅消费位置三项，线搜索候选的UpperDistance也仅消费三项，却都调用位置+du+dv九项包围。现BoundPosition按原Restrict、严格正权和Cartesian控制点凸包、原外舍入顺序产生完全相同前三项；需要du/dv的实际Gauss-Newton步骤继续原完整Bound。

Closest将第一次位置包围已生成的caller-owned受限控制网保留到原support-plane下界，避免同一个box重复Restrict。中间Improve只写best，点求值使用自己的stack workspace，不能覆盖这份net。所有lower/upper距离证明、五seed/18迭代/12线搜索、200000累计单元、pending128/分割和全局gap停止门保持；不使用未认证点值替代UpperDistance，不放宽nearest/容差或几何/拓扑/物性门。

初始定向132/132、原exit0。新位置三项及两种调用形式对原完整九项逐位一致，保留net对独立Restrict逐位一致，覆盖U/V次数0/1/2/4/8/16、rational/非rational、点/边界/宽盒及轻度Bezier数学延伸；短输出/不足workspace/非正权失败与零分配通过。原全局最近点、原UV反求/离面拒绝、几何距离/Bezier pair以及SupportingBlend/RegularSolidBlend消费者保持原测试期望。

原日志/TRX/源码diff在 `temp_docs/projection-bound/`。当前仅为已验证源码进展，完整API收益、产品/生命周期和集中独立接受待；下一步先做固定源码有限开发对照，原Unresolved/Missing/raw退出码分开保留。不做无C ABI变更的NativeAOT/Windows或微审阶段。
