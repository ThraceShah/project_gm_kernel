# 统一 PK 原生内核集中关键集成独审

日期：2026-11-12。**限定接受固定候选的计算集成、新 mirror8 修复、所列实体操作和当前真实看板交接证据；发现一个阻断当前 runner 自动质量签字的缺陷 B1。Mac 可取固定源执行和留存原始观测，不能凭当前 runner 的 Verified／audit 0 完成质量接受或签正式倍率。** 独审任务完成不等于 runner 缺陷修复、main 集成或上线。

## 固定对象与接受矩阵

| 对象 | 固定身份 |
| --- | --- |
| 交付文档头 | `410748d3b615296d1aa1c0e77df194eb7833b74d` |
| 计算／Capture／runner | `4b2405e3945929c5bb93677a68b0ee79e430b2f3` |
| Native 树 | `133f56acb0cf485366cd4f13f457dc3b161655eb` |
| 整个 src 树 | `0c5d23f6f4e3e7aabe5034888f92812c3ecb07ad` |
| 实际 merge | `b9db9bb03239c65c40b53d3b47f2a1dc41421112` |
| 功能父源 | `97174392b97e8c5976e56e2890c58314c74c73b5` |
| 累计性能父源 | `f9ffc0985eb1e5ce53f649af66751de7c6c77353` |
| 共同基点 | `f9c59201c0b9c5d103dee45124e3f43ca59a0921` |
| 新修复 | `bda37480472ddb6dfac4ca553299092436cb0bbd` |
| 独立执行树 | `e8162fcfc993ba71c54f95fb5608f1be34ac94ae`，只新增独审脚本；上述 Native/src 及 oracle/helper 字节未变 |

| 能力／交接 | 结论 | 可交付范围与明确边界 |
| --- | --- | --- |
| 两父源计算集成 | **accepted** | 27+14−1=40 个不同 Native 文件；37 文件逐 Git blob 与所属父源相同，另审共有 Patch 和新修复两文件。没有新增模型、预算、容差或实验导入。 |
| mirror8 交界修复 | **accepted** | 同字节 seed／GM fresh input，真实 GM/PK Fix 均成功，几何、拓扑、有限边、fresh XT/full/strict 通过；物性 Unresolved，原 exit 2。 |
| 45+18 最终 Boolean、shortedge 双来源生命周期、NURBS 生命周期 | **accepted，限定几何／拓扑** | 复核原始完整记录和身份，保留各步原 exit 2、mass 未决；不把 63 个最终场景与前序 API、lifecycle 或单测相加。 |
| oblique 指定不等距倒角→生命周期→同字节 Boolean | **accepted** | 独立重做闭环原 exit 0，Capture 2 步、123 pass、零 fail/missing；上游构造、生命周期、下游 Boolean 分别有证据。 |
| 回滚、方向、face/surface 四 sign | **accepted，控制范围** | 原 901/failed/零产品的控制成功不改写为 GM Boolean 成功；10 正／10 负方向及四 sign 不推广全部查询。 |
| 当前 Capture／单一 Compare、Mass observer | **accepted，观察与来源合同** | 同次结果、15 分量、模量、上下界、raw mask/diff/有限证明保留；off/on 的 60 CSV 行同字节。Missing 和 Unresolved 不变绿。 |
| 当前看板候选 handoff-current-v2 | **accepted，可交装配 owner** | ZIP 156 文件、56 真实 mesh、7 组 Capture，逐 mesh 反查私有 XT hash 和实际 sourceBodyTag。按下表显示原 exit、负控、missing，保留旧 run；未执行装配／上线。 |
| runner 编排／参数／计时留证 | **accepted，开发观测范围** | 单源或显式同合同两源 ABBA、fresh-input repeat、selector、同期 raw exit 与工作量留存；`performanceQualified=false` 正确。未独立重跑 ABBA。 |
| runner `QualityReasons`／`--audit` 自动质量门 | **blocked：B1** | 缺失或与原报告矛盾的证据仍 audit 0，同函数参与主 runner 的 Verified 判断；需修当前合同并窄复验后才能自动签字。 |
| Mac 固定源领取 | **accepted，执行资格** | 取上述 4b 全 SHA；平台私有依赖先 ENV_READY。可以构建／运行／收集原件，质量自动接受受 B1 阻断；没有本次 Mac 测量，不能套用 217fc 历史倍率。 |
| 全部数值门、任意建模、全部查询、PK 性能目标 | **unproven／已有边界保留** | mass 未决、curved tracking/roles missing、510 分类差异、五圆柱直接 BodyContains5000、未测 FaceAttach 不补签。 |

## 实际读取与证据身份

实际读取本机用户级 `AGENTS.md`（whole SHA256 `2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`）、本树 AGENTS、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、交付报告／receipt、固定父差异及相关实际消费者。whole 与派发的政策正文摘要属于不同散列对象，不声称运行时自动加载。按实际固定 Git 阅读 Boolean `75eddad101f0adaaa4550180c73eb1d12fa64fe7`、Blend `54f1b309d830ac78358ebcba78e1deec8e3c3b8f`、6616 `5a4ad5319b77c0944baf598e07dbbce79782a483`、G `3fdc109babe6eda489d985171e3808f956481be1`、Conic `35e61e5e02e319d8f6b9eaba38f880e62f1bbf0c` 及续审 `6a7dfa159c762e90d8180e2f1ed77191b2d52a04` 的接受依据和限制，复用其已审数学，不把历史实验再派成研发阶段。

仅在 qingyu2/Linux x64 执行。实际使用 `parasolid-research`、`parasolid-docs` 技能；统一私有准备再次返回 native ENV_READY，PK38.0.150、PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`、SDK10.0.302。查询 MCP 实际取得 `PK_DEBUG_BODY_compare` legacy 入口摘要和 157=`cant_do_intersect`；`PK_LINE_sf_t` 不是函数，查询未找到，没有据空结果推断算法。官方 XT §5.2.1.1 的 `R(t)=P+tD`、FD §31.1.1／31.4 的 accuracy 工作量与 modulus 正负界，与本次采用的原生定义一致。MCP 摘要不冒称完整 compare 文档或算法证明；没有把 Linux 地址用于 Mac。

作者证据根为 `../gm-native-unified-delivery/`，以下原件前缀为其 `temp_docs/unified-delivery/`。独审脚本 `scripts/PkNativeUnifiedIndependentReview.cs` 只读原件，派生物在本树 `temp_docs/unified-review-4b2405e/`。没有修改作者代码、测试期望或原证据，没有 vendor／XT／DLL/PDB 正文入 Git。

| 原件 | 实际独立核验 |
| --- | --- |
| `final-source.sha256` | 817/817 同原件及本树当前计算源码；manifest SHA `9487f841413932e35e7a08511177c6cfe8734b4488c4c5a48a3eea4b96d8697b` |
| `final-artifacts.sha256` | 2415/2415 全部逐字节；manifest SHA `c9ffe114e99c20e4c49f82c38ab0bcb4c0dde86415db917c31b2938166e7f987`，包括原输入／产品 |
| 三份 fixed diff | 重新以固定 SHA 对 `src scripts tests` 生成，分别同原文件 SHA：base `123bdd80eeb562d6a1e7efc5f5f6976cf6dc8654be91a346d4d945be370e70a9`、功能父 `93e8a8bc5e3c9d2b5c71b1959069ab251163f59a84ee758485172c18d8d115d2`、性能父 `e3c84768889daeace4397baa03eaabe003fe4b57542fac2f588e66f6aa0ac81f` |
| handoff ZIP／成员 | SHA `5197d647fbc47a15f3338e83f1c4063227fddcd4dfff92186c0274aa510495bf`；156 成员与目录原字节相同，files manifest 核 155 条（自身为第156文件）；无 XT／DLL/PDB／厂商库／schema |
| 完整 TRX | SHA `90472933bdcb7245b8da6015cd0b6585ca81fcbf70234690c76fd49547d111ca`；1974/1974、failed0、skipped0，原日志与 `.exit` 为0；不重新运行1974项 |

实际解析 PE CodeView 与 portable PDB GUID、PDB 的逐文档 SHA256，333 Native／161 tests／6 oracle-helper 源均与本树相同；obj/generated/other 分列，不冒签。普通 loaded Native 是 DLL `38aadd00300758d23b1aca6a88f7c4ff64d5bf8835ddd56055f0252975864a10`、PDB `64a9f665a3539774fe89700bfdb689b43e3bb0851a073136ea32cd99b299589e`、MVID `d8c6b453-63bd-4138-841e-136977b4aa6f`；测试 Native 是 DLL `6a302551a60d0fcf8d320e5a14a7841be9c09824c9d2377cfe6f6217922a893c`，测试 DLL `70f4f39b410363017fe95a11cba1d660591af8688392b7cdaab6fffe367e25f8`。两种制品分开，不能由相同 Native 树签为相同二进制。

历史两项 `NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative`、`SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled` 的源在性能父与4b间逐 Git blob 未变，当前 TRX 均 Passed；没有用历史 NotConverged 否定当前结果，也没有覆写历史失败。TRX、原 exit、前后文件清单、PE/PDB/source 交叉支持该次套件；不冒称取得测试进程内的额外加载见证或密码学执行证明。

## 集成交界与新修复

Git ancestor 和真实 merge parents 均已核实；`--remerge-diff` 实际显示冲突为 `RegularCurvedBooleanOracle.cs`、`RegularSolidBlendOracle.cs`。前者保留当前有限域／中间产品／reload／失败路径，后者将 repeat 放在 fresh input 构造外层；当前质量 helper 与不可覆盖目录保留，repeat/profile 接入。没有恢复旧270点值 Capture、旧目录读取或双合同兼容。五操作的 API timestamp 在原调用周围取值，Capture 的 Before 在计时前、After 消费已冻结 elapsed；其他日志的整段墙钟不能冒充 API 时间。重复产品文件只保留最后一次，Capture 每步记录独立；当前 repeat1 原件不冒称 repeat3 实测。

共有 `SurfaceIntersectionPatch.cs` 保留功能 `ExactCorner` 的资格排除、原齐次 corner，并沿性能父保留 scoped cache 的状态、kind、tag/generation/sense/root、原精确参数盒及 failureScale。负项仍返回 NotConverged，未变空交；支持参数路径／参数线／iso 映射继续各走实际变换。其他13份性能 Native文件同父源原字节，G／Conic 的 eligibility、witness 禁用、nearest、全盒 frame、centerF/Jacobian/RHS／预条件顺序和预算未新增改动。功能侧除新修复两文件及共有 Patch 外均同功能父；没有导入16aca Bezier／moving实验。

新修复集中核 `ClipRegularFacePair`→`AddPoint`→`AssociateParameter`→`KnownBoundaryParameter`→`ArrangeFaceCutGraph`、`LocateBoundedStation`／`SortBoundaryStations`：

1. endpoint 先由有限来源、carrier generation、原方程／root witness／occurrence 选择。新登记在选择之后执行，不以 XYZ 距离或投影区间重叠创造身份。多个同 sourceParameter 的认证根逐个参与交集，空交拒绝；不同根／不同 occurrence 仍通过原身份与排序检查。
2. 对已知源位置球 `P±e`，原 gauge 站位为 `(P−O)·D/(D·D)`，误差分子半径为 `e‖D‖`。代码用向外区间点积、范数与除法，正速度下界先验；没有将存储 double 方向的实数平方范数假定为1。投影盒是站位包围，**不新增存在／唯一根／NativeLocatorEnds资格**。根数、有限域证书仍由此前根隔离路径决定。
3. 后来参与者的站域收紧保留每条 witness，读者取交集。只对本次派生、open、Line、无 NativeLocatorEnds 的局部 cut 副本 clamp 代表端点；未写持久 input edges，原 native locator 仍由 HasCutEnd 检验。收紧后 `Last<=First` 整操作157拒绝，不删掉该弧后继续成功。
4. 排序仍要求不同认证事件闭盒严格分离；未证顺序拒绝。证据型路径的正长度阈值为0，不能用旧1e-10候选距离或1e-12片段阈值吞短弧；材料 witness 取已证 open gap。代表点在合法站域内不等于宽包围自身已经证明唯一根。

独立执行14项集中控制覆盖正反选边 mirror8、`2e-13` 间距短弧保留／重叠盒拒绝、后续收紧与不合法声明端点、同 witness 不得指向别事件、不同 branch/occurrence 不归并，以及旧候选 epsilon 内两个根。14/14、raw0。没有为每个字段重复建 fixture，也不把这些控制当任意多方退化事件的证明。

## 原操作、独立复验与看板显示

下表 checker 0 是本次只读证据／结果核验成功，**不是替原操作改 exit**。完整记录及各 log/exit/command SHA 另存同目录的独审 receipt。

| 原记录／新独立执行 | 实际结果 | 原操作 exit | 本次 checker exit |
| --- | --- | ---: | ---: |
| `regular45-final`／`mixed18-final` | 最终45+18几何／拓扑通过；分别40／30次 mass 未决；5539实际执行，4b同 Native | 各2 | 0 |
| shortedge imported／自产 lifecycle，NURBS lifecycle | copy删源、mirror-scale、cut1、editable reload、cut2／cleanup 原限定步骤通过；mass 未决保留 | 各2 | 0 |
| `rigid-small-final` | rigid+scale0.125 及当前有界质量通过 | 0 | 0 |
| `oblique-final`／`rollback-final`／四sign／方向控制 | 原限定控制通过；rollback中 GM Boolean 实际901，零产品，PK同输入成功 | 各0 | 0 |
| 原 Capture off/on controls | 60 CSV行 SHA同为 `74f2bba7042662f1b773aa078b3e2796634089e742db74a23225a40ccb02a975`；负控未决和失败保留 | 各0 | 0 |
| 原完整套件 | 1974/1974；test Native源333／tests161核实 | 0 | 0 |
| 原 `current-runner-v2` | planar0/Verified；sphere、curved、NURBS fillet、shortedge各2/Unresolved；非正式倍率 | 总2 | 0，仅接受这些实际原件 |
| **新独立 mirror8** | 真PK0/GM0、各两Blend，fresh/full/strict/finite通过；mass未决2次；Capture29pass23missing | **2** | 0 |
| **新独立 oblique 闭环** | 上游独立构造／生命周期与下游 same-byte-fork 分开；2步骤、123pass、无fail/missing | **0** | 0 |
| **新集中身份／短弧控制** | 14/14，无失败／跳过 | **0** | 0 |

mirror8 独立运行实际 seed SHA `262963ce0b899972272970ace0eff5da8b9161dbf007bd01f20a9d3037a8d1a2`、GM fresh input `10b9eaeaf01d1c0e5cc1b1655fa3884844d026b90e25713ae4031139183c0c54`、成功 GM 产品 `e17986c1c2e8f442eb7ad79d74a93e434e4155bd83b35db2d273d8202f0de65a` 均与原件相同。新构建 loaded Native DLL `7fefecab0305cc19979c53b0850d7da7ec67e8e06d55f3ddf469fe041c32dd92`、PDB `0d06642ab91f73bf746eeaa07686840cca9e4777da0f0403c39a39e886e54d33`、MVID `063edb20-1270-49a4-b806-7177058603e4`，PE/PDB及333源另核；与作者DLL不同，按实际构建列证。

看板现包：oblique `123/0/0`、mirror8 `29/0/23`、lifecycle `133/0/77`、controls `27/9/27`、runner curved `14/0/13`、NURBS `16/0/13`、shortedge `35/0/17`（次序 pass/fail/missing）。controls 原0表示负控执行成功，9fail不能被藏成正例绿；mirror／lifecycle缺失不能全绿。DisplayBridge 实际从GM CaptureDisplay的XT/bindings生成私有PK snapshot，再facet；GM live tag、snapshot tag、XT hash分开，PKsnapshot未冒充GM原实体。mesh是显示近似，不继承几何证明或性能资格。当前具名证据可交看板装配，无需先解决全部未证明模型能力；本任务未触门户。

## B1：当前 runner 可接受不完整或矛盾的质量证据

定位 `scripts/NativeModelingWorkComparison.cs:111`–119，主运行在第72行附近也消费同一个 `QualityReasons`。evidence 字段只有存在时才检查文件存在；mass只查数量、两个modulus、normalizedUpper与字符串Verified，未检查估计／下界；compare只要求某个check ID存在，未核链接报告的结果。实际Capture producer本次未发现把这些状态写错，**缺陷在 runner 的接受门**。

由真实 `current-runner-v2/01-planar-candidate` 隔离复制派生，保留原文件不变，调用当前原样runner `--audit <派生目录> --repeat 1`。没有再建模、Compare或积分；派生副本是破坏性负控，不是真实新运行。

| 副本唯一变化 | 当前 audit原exit | 结论 |
| --- | ---: | --- |
| 原样正控 | 0 | 正控正确 |
| 删除 actualModulus | 2 | 正确拒绝数值资格 |
| 删除 BODY_check checks | 2 | 正确记录missing |
| 删除 compare check | 2 | 正确记录missing |
| 删除 mass的两estimate与normalizedLower | **0** | 丢失当前合同必要数据仍称无outstanding |
| 删除quality checks的evidence链接，并删除整个evidence目录 | **0** | **核心阻断：实际比较／物性证据全缺仍接受** |
| 原链接comparison报告status改为Rejected，summary保持原pass | **0** | **核心阻断：报告拒绝与summary矛盾未被发现** |

三份假阳性日志都为 `capture quality outstanding=0`。这证明审计器0不能作为质量合格；不证明原planar几何失败，也不否定逐原件已独立核实的看板包。主runner另外保留原exit、FAIL日志检查，但不能补回此函数遗漏的证据约束。

最小解除条件：在当前格式中强制每次所需compare／mass证据链接及实际报告存在，校验summary与同次报告／来源一致；15分量按身份验收必要estimate/modulus/上下界和Verified语义，缺失保持missing，矛盾／Rejected保持失败。复用这里的正控和现有负控，加上述三负控窄复验即可；不要求重跑63/1974或积分，不新增旧格式兼容，也不要求改内核数学。未在独审树修作者runner，当前B1保持blocked。

## 可复核交付与未执行范围

本次只新增独审脚本、本文和派生收据。执行命令（先取得native ENV_READY，独立新树／新输出目录）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/PkNativeUnifiedIndependentReview.cs -c Release -p:UseSharedCompilation=false -- evidence
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/PkNativeUnifiedIndependentReview.cs -c Release -p:UseSharedCompilation=false -- execute
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/PkNativeUnifiedIndependentReview.cs -c Release -p:UseSharedCompilation=false -- negative
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/PkNativeUnifiedIndependentReview.cs -c Release -p:UseSharedCompilation=false -- results
git diff --check
```

execute驱动退出0只表示已执行并记录子命令，不覆盖mirror8原2。evidence和results最终checker各0；results初次134是独审脚本错误要求mirror全部历史input比较都有GM绑定，已按其23missing实际合同分层，修正为只对正例完整oblique要求非空后checker0；原失败日志保留。首次外层日志重定向因temp_docs尚未建立退出1、未启动检查器，非模型失败。没有以最后成功覆盖任何原操作退出。

没有重跑45/18/full1974/全家族／正式ABBA，没有AOT或CABI修改，没有Mac／Windows／main／门户操作，没有全局pkill；本次进程树结束后未见所属testhost残留，无须终止其他进程。旧217fc受控窗口只保留其既有接受，不签本统一源倍率。第一笔审计脚本提交 `e8162fcfc993ba71c54f95fb5608f1be34ac94ae` 已推送；完整最终提交和远端SHA由交付消息给出，本文不预写尚不存在的自身SHA。

## B1 窄复核续审：84c558c（2026-11-12）

**本节是当前结论：B1 已在固定修复源 `84c558c10135c117bf8be6629a157e6d230f1b2d` 的当前单操作 runner 合同内解除，接受其自动质量门；上述原4b缺陷、原退出和接受边界作为历史证据保留。** 原完整独审提交为 `583d917890a9f654dbd4a1f4e96e5e5f261f68eb`。本轮只审 B1，没有重启计算集成独审或建模阶段。

实际重读用户级 AGENTS（whole SHA256 仍为 `2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`）、本树约束、原 B1、固定 diff 和当前实际 producer。相对 `410748d3b615296d1aa1c0e77df194eb7833b74d`，`84c558c` 只有 runner 修改及新增 `scripts/NativeModelingQualityGateChecks.cs`；交付文档追加及 B1 receipt 位于文档头 `8c2d6da82f4c83c4803fab408e424f4b6ca5f295`。Native 树仍是 `133f56acb0cf485366cd4f13f457dc3b161655eb`，整个 src 树仍是 `0c5d23f6f4e3e7aabe5034888f92812c3ecb07ad`。没有改作者树或把修复 runner 合入本独审树的生产入口；独立脚本从精确 Git 对象抽取 runner 到隔离临时目录执行。

源码核查结论：主运行和 `--audit` 共用修复后的 `QualityReasons`；compare/mass 摘要必须有实际报告，摘要、报告、sourceBinding、loaded identity 和已有命令来源互相一致。每步要求其 snapshot 比较与 GM/PK mesh 链接对应，再按 GM live／PK reference 实体匹配独立比较和物性，二者 native pair 和 XT hashes 相同。已按实际 `WebProgressCapture.Compare` 核实保留 PK copy 与显示 snapshot 是不同实体，没有要求其 tag 相等。当前单操作来源合同未被扩张为任意 lifecycle 审计器。

物性15个具名分量与各自摘要一一匹配，estimate、modulus、上下界、阈值和三态均核验；修复中的 outward 公式逐项同现有 `ParasolidMassProperties.Classify`，仅复算已记录数值的内部一致性，没有再积分。比较继续消费原已接受 helper 的最终裁决，核对严格 mask、raw differences 及其诊断／有限证明记录，没有另造几何判断。缺失返回2；Rejected、API 错误及来源／摘要矛盾返回1；真实 Unresolved 保持2。没有改 producer、数学、容差、预算或历史格式。

独立验证入口是 `scripts/PkNativeUnifiedB1Review.cs`，没有直接运行作者的验证 driver。复用旧独审7组隔离副本、作者 v3 的7组新增派生副本及5组真实 Capture，独立规定预期值，对固定 runner 逐组运行；每组非 DLL/PDB 输入文件的字节与文件数在执行前后核验未变。19次结果如下；`rawAuditExit` 是本次审计子进程的原退出，**不是建模原退出**。

| 复用输入／隔离控制 | 数量 | 本次 rawAuditExit | 本次控制 checker |
| --- | ---: | ---: | ---: |
| intact、original-planar 正控 | 2 | 各0 | 0 |
| no-modulus、no-body-check、no-compare | 3 | 各2 | 0 |
| 原假阳性 no-estimates-lower、no-evidence-links | 2 | 各2 | 0 |
| 原假阳性 rejected-compare-evidence | 1 | 1 | 0 |
| wrong-step、source-contradiction、false-verified-bounds、native-api-error、command-source-contradiction | 5 | 各1 | 0 |
| duplicate-component、report-no-estimate | 2 | 各2 | 0 |
| 原真实 sphere、curved、NURBS fillet、shortedge | 4 | 各2 | 0 |

19/19符合预期，独立外层 checker 原0；未发现本次 B1 合同内尚存阻断。本轮动态审计均为repeat=1，更多重复的逐步匹配只作源码核查，不冒称实测。最后四项的原建模退出仍各2，planar仍原0；没有用审计0补签建模，也没有把缺失或未决签成几何错误。原 v1 错分缺compare、v2中间版本及原4b三假阳性记录均未覆盖。本次 runner SHA256 是 `78c98d884640d185c07337d4d34ec02bdb14f9d5fa3876cfef6c19831b75f6a5`，与固定 Git 字节相同；逐次 argv、Capture hash、log/exit SHA、输入未变文件数见 `docs/reviews/advanced-modeling/pk-native-unified-84c558c-b1-independent-review.receipt.json`。完整日志私存于 `temp_docs/unified-review-b1-84c558c/`。

| 当前交付对象 | 续审结论 | 实际可交付范围 |
| --- | --- | --- |
| 原计算集成及几何／拓扑接受 | **accepted，原范围不变** | 继续引用原4b完整独审；本次没有重新运行或冒签新二进制。 |
| 当前单操作 runner 自动质量门 | **accepted，B1解除** | 固定84c；Verified／Failed／Unresolved按当前报告合同验收，缺失不绿，支持现有单操作 runner 的每步匹配。 |
| 当前看板 handoff | **accepted，可交装配** | 原156文件／56mesh ZIP 仍为 `5197d647fbc47a15f3338e83f1c4063227fddcd4dfff92186c0274aa510495bf`，本次核前后同hash；追加84c修复来源与本续审即可，保留原missing／负控／exit显示。 |
| Mac 统一基线领取 | **accepted，可取84c全SHA** | 私有环境先ENV_READY；原Capture身份仍属4b，禁止改称84c的新运行。 |
| 新Mac倍率／正式性能窗口及全部模型能力 | **unproven，原边界不变** | 未运行Mac或ABBA，`performanceQualified=false`继续有效；真实mass未决、curved缺失及原查询边界仍保留。 |

可复核命令（新输出目录；仅需要普通.NET10，不加载私有PK库）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/PkNativeUnifiedB1Review.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/unified-review-b1-84c558c
git diff --check
```

仅在 qingyu2 执行。没有运行旧 evidence／execute、mirror8／oblique／14项控制、817／2415整包复验、1974单测、PK调用、Compare、积分、ABBA或AOT；没有触Mac／Windows／main／门户，也未终止其他进程。续审完成和推送不等于装配、上线或main集成，最终完整提交及远端SHA在交付消息中给出。
