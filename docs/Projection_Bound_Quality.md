# Projection cc47871完整质量原件

计算固定 `cc47871133fb7d536ffa7fd050066c51f4905708`，Native `fe3fa0378ef6d9c0dd971de4c993a289cdb8a34b`，src `31be8f6c46766bb3b60b0e402bfc043426ed58fb`，基线17eedbf/已独审Joint185/Native116。独立分支 `perf/native-projection-bound-work` 仅按实际投影消费位置的需求减少完整导数求值和同box重复Restrict；原有理位置凸包、保守lower/upper distance、全域分支、预算、原生参数、几何/拓扑及生命周期门保持。实现及输入/工作区借用依据见 `docs/Native_Projection_Position_Bounds.md`。

Mac完整套件2022执行/2022pass/0fail/0skip，build0/test0，4m44s，自然结束。原TRX SHA `25b1a4e03b665d57775e53fe3159d9c639ab8df642f1e490db4a23bd25a58cd1`；三个测试DLL及三个PDB的前后文件hash一致。132项定向原0，原数学/UV/nearest/全局距离负例及新三项位置/控制网逐位和零分配检查保留。无Windows、NativeAOT、C ABI或测试期望修改。

| 当前实际执行 | 原step数 | pass / fail / missing | 原exit |
|---|---:|---|---:|
|curved单操作|1|14 / 0 / 13|2|
|NURBS完整开发对照的候选原执行|6 fresh pair|96 / 0 / 78|2|
|standard shortedge Fix|1|35 / 0 / 17|2|
|rigid + scale.125|1|51 / 0 / 1|0|
|exact mirror8|1|29 / 0 / 23|2|
|shortedge双产品生命周期|5|133 / 0 / 77|2|
|NURBS生命周期|1|16 / 0 / 13|2|
|curved editable reload|1|17 / 0 / 26|2|

八组captureErrors空，无fail。NURBS复用已执行六pair的当前质量证据，没有补跑单fresh凑表；其最终重名产品保留rep5，不能标作其他运行或rep2。七组raw2的物性Unresolved及missing来源/roles不改；rigid原0仍有一项missing。shortedge输入明确PKseed→GMreceive→GMFix；copy删源、镜像/缩放、后续两次Boolean、self-XT可编辑reload和cleanup各原步骤、当前strict/finite/PK receive比较保留。下游与上游操作分别记账，不由driver/test/checker0签数值全Verified。

开发对照只是一次相同current helper、每源六fresh pair的串行B/A观察：基线五暖589.8145/583.4430/534.2955/525.3952/484.3924ms、中位534.2955；候选五暖515.7617/477.6227/440.8453/436.8736/403.6218、中位440.8453，观察−17.490%。冷1107.9154/964.5911另列，所有PK时间及原Raw2保留；该结果不签统计因果/普遍收益，也不代签后续唯一固定五操作窗口或独立接受。

源绑定 `docs/projection-bound/source-binary-binding.json` 核10组PE/portable-PDB链接、3个源文档集合：普通与testhooks Native各335 tracked文档，Tests167，generated/unmapped另列。普通producer实际loaded见证与测试sealed文件/前后清单分层，无新增测试loader或start/end见证。

普通质量Native MVID `c1240871-d64d-4518-8a49-f0d533b267a6`，DLL SHA `d82196e1741424219b69b40688a0b599bc48fce11e69db6a39550d75f05eb03a`；testhooks Native MVID `0fe9e9d6-a403-487b-8d71-34d2a324be52`，SHA `d9d6fe5666ae49efe500fad70d841e70bd8b26d93308834407e4e6512a244ccd`；Tests MVID `04da11ba-7d93-4f39-b6d8-d33225fd649d`，SHA `112b33f3eca994c56f5bfec83a0ef0193c8f38fe44ff2c7e0cda6efdf92a56bc`。制品不同，不因源码相同混签。

质量机器原件在 `docs/projection-bound/quality-receipt.json`，私有原日志/TRX/XT/Capture/DLL/PDB在 `temp_docs/projection-bound/`。对已接受源未加Native探针的采样70文件另按原字节复制到 `accepted-source-sampling-original/`，逐成员SHA核同；采样只提供调用栈/线程墙钟，不签排他CPU。采集器退出与子程序/建模退出分开记录，不向原件补身份。

本包待集中独立算法/集成/测量接受，未合main或上线；整体PK目标仍未完成。已接受185/e139/17eedbf及原两个ZIP保持不可变，不混入并行q2查询/510/物性分支或已拒性能实现。
