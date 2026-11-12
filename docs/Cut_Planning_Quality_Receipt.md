# Cut planning950完整质量原件

计算源为 `950e36252d21bbeb641c818784c45b4fb6bf8392`，Native树 `a3a74c043763bfb0ff347f4dc3600d2c18fb1631`，src树 `a3aa72619c4fd3dec43018669415c251f966ef5a`；隔离分支 `perf/native-cut-planning`。基线为已独审Joint185/Native116的17eedbf，接受报告e812b7a。阶段实现及真实输入/借用/修改边界见 `docs/Cut_Planning_Proof_Phases.md`；没有新几何类别、C ABI、Windows或NativeAOT改动。

Mac实际完整套件2023执行/2023pass/0fail/0skip，build0/test0，5m56s，自然结束，无host crash。原TRX SHA为 `ec441a58efb2568667bcd055a1ac46a79116aa6749e2e5cd88c5594a7b2aea26`；测试三DLL/三PDB的前后清单逐文件hash一致。新增4个scope/失效/资源控制连同原消费者的77项定向验证原0；这些不是完整API时间样本。

| 当前质量运行 | pass / fail / missing | 原exit |
|---|---|---:|
|curved|14 / 0 / 13|2|
|NURBS单独Fix|16 / 0 / 13|2|
|standard shortedge Fix|35 / 0 / 17|2|
|rigid + scale.125|51 / 0 / 1|0|
|exact mirror8|29 / 0 / 23|2|
|shortedge双产品生命周期|133 / 0 / 77|2|
|NURBS生命周期|16 / 0 / 13|2|
|curved editable reload|17 / 0 / 26|2|

八组captureErrors均空，原完整/strict/finite检查、真实PK receive、同参数独立PK产品和各步几何/拓扑通过范围保留，七组raw2的物性Unresolved及missing来源/roles不改。shortedge输入仍明确为PKseed→GMreceive→GMFix；GM实际输出的XT/display snapshot与PK独立产品分开，不以显示网格、driver0或test0签数值全Verified。copy删源、镜像/缩放、两次后续Boolean、self-XT可编辑reload和cleanup按原步保留。

本轮单fresh普通三操作的Jac是672/3605/18822；NURBS相对Joint185质量单fresh4179减少574。它们只属于各自执行，后续完整时间窗必须逐进程/rep读取原PROFILE/WORK，不能以此覆写暖rep。原SSI/root cells/roots/winding/budget保留；减少次数不是CPU成本、普遍收益或PK目标证明。

源/制品核实在 `docs/cut-planning/source-binary-binding.json`，质量机器收据在 `docs/cut-planning/quality-receipt.json`，原日志/TRX/XT/Capture/DLL/PDB在 `temp_docs/cut-planning/`。共10组PE/portable PDB链接与3个源文档集合匹配，普通及testhooks Native各335份tracked checksum，Tests166；generated/unmapped另列。八组producer实际loaded见证与封存测试文件/前后清单分别记账，没有新增测试loader或start/end loader见证。

普通质量Native MVID `39448313-0223-4d49-8d81-35e52af0c7d0`，DLL SHA `28d4e504bfb1fb879be30f3e4ca76485411d0a2dece95205e13a54f4257b0fed`。testhooks Native MVID `f375af83-631c-4b36-a022-cd47d4b6ae2e`，SHA `d94d1ef14d0dd7f9d2fe9d0a0bebe0f5b4a954919eb226755ef1e388edb88e7d`；Tests MVID `24812e06-3a08-484d-885d-2ebdb70b04f7`，SHA `6737360a887d5d11c2f36347dc282b9793cc793013b579918924f699ac784515`。各制品不同，即使Native源码相同也不混签。

本轮待集中独立算法/集成/测量接受，无main合入或上线声明。已接受185/e139/17eedbf和两个原包不改，不把新2023结果回写历史2019或1654原失败。整体PK差距仍未完成。
