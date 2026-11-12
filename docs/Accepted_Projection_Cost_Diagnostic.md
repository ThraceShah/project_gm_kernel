# 接受cc47871的实际剩余成本诊断

已实际读取固定 `7b20d7f1046ad5b6f54f8601edd739fa5d54a901` 完整独审。cc47871/Nativefe3fa、Mac2022/2022与限定八组质量/唯一窗口描述性观测已接受；planar回退、U/missing、原退出与未合main/未上线/整体PK目标保留。新树从固定文档头553b2c6隔离，实际AGENTS及两架构已读/核同，无Native/scripts产品源码diff，未接q2 moving源或已拒实验。

`temp_docs/accepted-source-profile/Profile.cs` 从先前真实窗口loaded.json找缓存apphost，先核Native和Oracle双hash，再直接运行同字节原普通制品，不重建或插Native计时器；CLR startup suspension仅用于外部EventPipe attach。两个六fresh GM/PK pair诊断串行执行，每个直接观察子进程exit2及独立collector0，二者不互签。保留.nettrace、Speedscope、原stdout/stderr、capture/loaded/XT以及命令。采样只作线程墙钟调用链，不当排他CPU/可删预算或新性能窗口。

| 接受源所选Fix采样栈 | NURBS ms / 占比 | shortedge ms / 占比 |
|---|---:|---:|
|Jacobian（全部可见子栈合并）|1456.6195 / 46.31%|143.3379 / 3.56%|
|Projection position Restrict|707.1826 / 22.48%|0 / 0%|
|Projection derivative Restrict|319.6323 / 10.16%|0 / 0%|
|其余Projection|53.3341 / 1.70%|0 / 0%|
|其他BoundSegment|157.2107 / 5.00%|495.7361 / 12.30%|
|其他Fix|451.2389 / 14.35%|3391.7281 / 84.15%|
|总所选采样线程墙钟|3145.2183|4030.8021|

大多数Jacobian采样没有可见样条子帧；内联/符号栈不全不能被解释成样条没执行。shortedge其他Fix的最近可见帧EndBound2108.2569ms，SurfacePlaneIntersection.Find398.1935ms，StartBound106.6167ms；不能以这些label直接签方法排他CPU。两次Sampler没有观察到shortedge的BSplineProjection帧，因而也不能用此栈给上一窗口shortedge−15.223%作投影机制归因。

对EndBound异常大栈，做且仅做一次同源/同字节、无外部sampler、profile on/off的完整API诊断模式对照，每mode六fresh pair；仅现有 `--bench-profile` 开关不同，缓存/预算/几何参数相同。所有producer原exit2、48次实际API均0、captureErrors空，原冷暖/PK/文件留存：

| 操作/现有诊断模式 | 冷GM ms | 五暖GM ms | 暖中位 |
|---|---:|---|---:|
|NURBS on|858.7789|457.6830 /419.7714 /405.9301 /403.2579 /395.8105|405.9301|
|NURBS off|884.7707|464.5462 /455.0651 /409.4967 /407.9804 /399.9314|409.4967|
|shortedge on|1325.0571|707.7130 /590.9486 /526.7132 /522.9931 /471.0212|526.7132|
|shortedge off|1405.4683|731.0102 /505.4049 /536.9941 /488.9721 /485.4487|505.4049|

有限串行模式对照不预签时钟开销为某确定比例，也不签普遍/因果收益；它不支持把52%采样栈解释成删除计时即可消去52%完整API。off没有启用工作计数，不能填入on计数冒称实测。旧所有接受/开发/测量原值保持，不把本模式对照混入性能窗口，不追有利复测。

下一计算核查限定于明确数据流：成功line-search点的原位置三项与同点原受限权重，下一次Newton头又按相同patch/同UV计算。只研究同一Improve调用内传递成功位置证明，仍完整算du/dv、原Newton/UpperDistance/global gap和预算；不跨调用/owner建缓存，不删证明或重启已拒方向。先原九项逐位/状态/零分配与完整API开发收益，再决定是否值得完整交付。
