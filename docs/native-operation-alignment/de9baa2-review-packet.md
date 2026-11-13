# 固定原生建模集成复核包

状态：owner 已实现并运行；等待集中 Native/math 独验。整体建模及 PK 性能目标继续执行，
不是任务 DONE，也不是已经合入 main 或独立接受。

固定 source `de9baa27658d7ae3aeaf47e15fdba39c66d630de`，PKToy
`d820719b6d8510d623972ab9d8efc65edbc312f9`。完整实现依据和 PK 官方/静态/动态/
推断边界见 [Native_System_Alignment](../Native_System_Alignment.md)。

本次相对于前一固定包新增或整合的实际能力包括原生有限参数/精度与事件身份、支持 UV、
解析和非解析有限 winding、原生 geometry 定义及所有权、内部 planar 工具 union 的
basic tracking 来源组合与投影新边追踪、正向 primary fin 发布，以及闭合原生
Ellipse Blend 的 finite sheet 建模及可编辑生命周期。复用现有共享 arrangement、
compiler、实体池与 command journal，没有引入替代 XT 模型或历史兼容路径。

## 固定字节与复现前提

包为 `temp_docs/native-system-delivery/checkpoint-de9baa2.zip`，28,340,977 bytes，
SHA256 `cdf744b23d9403b11a0ba1a2349feec80468571ff6f319a0def439c3464ea616`。
内含固定 source archive、四个实际生产托管 DLL/PDB、manifest 以及 941 个明确列出的
源码/证据制品。PE/PDB 身份匹配，Native 350 个、XT 13 个编译源码文档的内容校验
与固定 source archive 一致。生产 Native SHA256 为
`89dcbc7e3ee9a116eae052468958f0d6a6772aca917e6d467688412ad64bd4fa`。

包不包含厂商库、schema、凭证或 NativeAOT 发布。复现应在固定 source 的 Git
worktree，先准备匹配版本的 PKToy 与授权私有环境并实际取得 ENV_READY；源码压缩包
用于内容审查，不能声称它自行提供外部 SDK。原生版本为 PK 38.0.150，Linux x64
使用 .NET 10.0.10；Mac arm64 独立使用 .NET 10.0.11 和同版本 PK。
全部比较通过 [统一启动入口](../Native_Runtime_Policy.md)，不改全局环境。

例如在项目根目录重跑一个新的执行名称：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RunNativeOracle.cs -- CurvedSheetPartitionOracle.cs periodic-ellipse-blend --blend-lifecycle --blend-scale=1.7 --reverse-blend-supports --output-name=review-periodic-new-run
```

manifest 逐执行保留实际 raw exit、观察到的 assembly SHA 及是否匹配包内生产字节。
完整托管测试使用 testhooks，Mac 使用其自己的实际 IL，均没有冒称使用 Linux 包内
字节。原始日志、原 XT 和旧失败不覆盖；详细边界在
[固定证据说明](de9baa2-delivery-evidence.json)。

## 关键结果与复核重点

- 完整 Release 2159/2159、raw 0，allocation guard CLEAN。该测试构建的源码
  随后原样提交到固定 source；不把 testhooks DLL 当成生产 DLL。
- 当前生产字节的 88/88 曲面消费者通过真实 PK fullcheck/strict；周期 Blend 在
  镜像小尺度内偏置、大尺度混合符号及 Mac 逆支持次序下各九种操作通过，分别
  保留 copy/delete、编辑回读、各侧独立基准及实际周期 winding 证据。
- 17/17 Blend/Chamfer 本体与生命周期、后续 Boolean 通过；三族 NURBS 在
  .125/1.7/8 的守卫建模、copy/delete/mirror/reload 再 Boolean 通过。
- 多工具 tracking 的 Mac 固定源对照通过两种事件拓扑。来源按实际构建关系登记，
  17/16 条返回与 PK 一致，各侧 34/32 个有限重叠 witness 及错误来源负例通过。
  derive 不要求完全包含在旧 trim 内，最大采样偏离仍保留；采样不作为连续证明。
- 公共原生 geometry 合同与七类 XT writer raw 0；24 族 SP、48 个有限查询及其
  copy/delete/mirror/reload、D0/D1、有限 wire 对照 raw 0。

集中复核应检查：多方事件身份及 station 合并是否有完整共同见证、投影局部替换的
闭域/材料/有向边条件、union 来源组合与删源前 identity 捕获、回滚工作区和返回内存、
原生主 fin 与自由 geometry 的 owner 初值，以及 signed ellipse spine 的整管适用
证明、原生 sense、SP 有限参数恢复及实际周期 winding。官方定义、静态链、实际动态
分支与数学推断应分别核对，不以 API 成功、checker 退出 0 或面数相同签全部质量。

## 仍在执行的范围

固定整体 API 测量中，16 次 GM Fix/Boolean 合计 30,180.353 ms，对应 16 次真实 PK
为 34.280 ms；四族各两次新模型全保留，进程总测量 32,810.643 ms。几何拓扑通过、
raw 2 的物性 U 单列，尚未达到 PK 性能目标。后续优先处理实际 bound、clip 和
classification 中的重复世界坐标/原生 UV 求解；当前 packet 没有实现或宣称这种
下一步降维，也没有把 CHART chordal_error 的估计当作严格排除界。

510 的当前结论仍为 self 15U、import 16U，different=0、native_class_changed=0。
native7、Capture8 和历史缺失的 self495 列各自保留原来源，未强制四方相等或补造。
物性两侧均是 PK 积分，接受后的量纲归一化与旧原报告分别保留；一般曲面模量导致的
U 没有改名通过。任意高价角点、更一般接触/同域/多参与事件、complete/region
tracking、非 Line union 原边证明、任意 Blend SP UV、多周期/跨接缝及全部 attachment
类别仍按实际边界拒绝或未取得资格，不由本包的有限通过覆盖。
