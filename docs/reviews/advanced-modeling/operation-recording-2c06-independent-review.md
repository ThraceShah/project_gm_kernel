# 固定 2c06 操作验收记录增量复审

**结论：记录层多数窄修可有限接受，但总体候选 blocked：新增 first-failed 选择会把较早、已明确归属的验证失败替换成后来的 cleanup child。** 本报告只审记录正确性，不接受全部 A1–A7、175 API、建模交付、性能资格或部署。审查者为独立 GPT/Codex worker；未调用实现者自测来代替独立接受。

候选 `2c06b9da80737f2d501bd5e1886c3ea1494908c7`，对应 `origin/fix/operation-recording-r2`；增量基线为已窄接受的 `0935ec30dd1a5a669a06f3c69bafa473f770fd4f`。依次审 `3296c7eeb0f5c1dd94f9dd4668acb3003b4ad63f`、`27c49ff02bf0e941c89e37da0c5c653c75de173b`、`2c06b9d`，未跟随作者 working diff。旧 shared a2 报告 `ebed51368a51e90ff85ffcdfdbc6ce3f83074064`、bd7 报告 `870f8d1b548dceb3d85c5c827e8cc7215f36f43c`、a0/0935 报告 `8bc447bb5de3c9a28f16bf5635cfa13a47289ad9` 的原结论保留，不重做旧 fuzz。

`src` tree 在候选、0935、先前固定审查的 `5d45dad0b92bd28276f88c1f2cb4fcf35deda6ef` 均为 `7d516f6c16c762581f0ba2d87df4df5e263c6cc4`。题述短名 `5d90b662` 在本地对象中不可解析；没有将其假称已核 SHA，而是核先前报告实际固定的 5d45dad 和本轮确切基线。Host/BodyCompare 对上述固定5d亦无改动；相对0935，Native、Host、BodyCompare、Refresh、公共 Validator 全部无 diff。Evaluation 的已知清理差异不能据此认作新增内核算法缺陷。

## 阻断 F1：有失败 child 不代表父只有传播性失败

位置：`scripts/OperationAcceptanceLedger.cs:201`–202，文件 SHA256 `a780760600aa775daa44bd9725ca771d7363d53333c3edc6e749274a13930f05`。新增逻辑收集所有失败 child 的 `ParentOperationId`，然后无条件从 first-failed 候选中排除这些父。它能在父仅传播 child 异常时选到精确 child，却也排除了已经先记录独立 primary failure 的父。

真实调用衔接：`scripts/ShortEdgeBlendOracle.cs:659`–660 的 CompareFresh 会先把 strict BODY_compare failure 写到 verification integration owner；`:673`–677 的 finally 随后执行两条清理 child。若先比较失败、再 delete 失败，父既有比较 failure 仍在，首因却被改指后来的 delete。该 leaf 文件 SHA256 `082cd5a829092bc96751fcbd6b40ad58716dce8c9629366e4545508cf520bece`。

独立最小入口 `docs/reviews/advanced-modeling/operation-2c06-evidence/FirstFailureProbe.cs`（SHA256 `8ee9dc3e339c90164128a18bdc7b218bdac4e3b49451270f0579aed7d304b48c`）直接编译候选真实 Ledger/Rules，依同一 try/finally 顺序调用真实 RecordCheck/OperationStepScope/EndScenario：先 `verify/strict-compare=fail`，再 `verify/delete-received` 返回 PK22 并抛异常。原 exit **1**，实际 `firstFailedOperationId=verify/delete-received`，预期应保留较早验证 owner；两项 failure 本身均留存，未发生假 pass。产物 `temp_docs/operation-2c06-review/first-failure.json` SHA256 **`db8e77e95fa32e6354b912789662606036d88b477d59b2bcf9a29a389bb38988`**，同字节副本随 evidence 提交。

这是确定性的记录层反例，不是一次真实 PK 几何失败或“双 native failure 已发生”的声明。代码中的真实顺序使该共享入口反例适用于 CompareFresh；本轮未启动完整 ShortEdge lifecycle 来制造几何故障。发现后已用 Orca escalation `relay_3c6beeacfd45` 立即报告协调端；其回执 `relay_1c75a202e563` 确认另派最小followup并要求本轮固定2c06收束，未改作者源或追新working diff。

关闭 gate：区分父的原生/验证 failure 与纯 child 传播终态，或为验证阶段建立独立 owner，并保留实际失败先后。必须独立复验“父较早验证失败 + 后续 cleanup fail”和“父仅传播 child fail”两种情况；不能简单退回总选父，也不能丢任何 check。无需重跑完整 corpus。

## 本轮有限接受与边界

| 层 | 独立核实 / 有限接受 | 不授予的结论 |
| --- | --- | --- |
| shared child scope / returns | scope 在调用前建立；返回前异常保留 null，返回后保存真实一侧码；Dispose 失败不吞异常；unexpected return 的 owned failure 不被后续0擦掉；纯传播父可定位 child | F1 未闭；不是所有 API 都有 complete primary 合同 |
| Freeform / Local | 两类 XT lifecycle 有 receive/copy/delete/transform/cleanup child，active lifecycle catch 不回写已完成 construct；未进入的后续 lifecycle 显式 not-run；真实 `_2` 名称、tag/hash 分离、成对参数快照与实际 saved XT 哈希 | Freeform Produce 仍是多 API 构造、无通用完整调用/primary 合同；没有逐构造 API 独立资格；本轮未新跑真实 PK lifecycle |
| v3 expected-return | 仅特定 hollow rejection / missing-delete 合同解释；Local 13/13 原码、empty tracking、GM unchanged XT 与独立 PK pre-call 基准均保存；当前公共磁盘入口接受该保守记录 | rejection 仍 unverified / qualified=false / ratio=null；任意自声明901不可取得建模资格；v1/v2 不继承政策 |
| Geometry | body transform 实际双侧0；source delete 真实 GM0/PK null；source lifecycle 为 integration，异常 catch 指当前阶段，未到达 lifecycle 为 not-run | 单体几何等其余返回未知仍未知；缺 full-wire/构造/same-input 证据，不能全 pass |
| Evaluation | primitive constructors 的真实返回点；offset 创建/正负数值采样/各次删除分 owner；原 positive/negative primary pass 保留；未进入的 dependent branch 可 not-run | sampled numerical pass 不等于 body construction；完整未筛选 Evaluation 本轮 not-run；原清理语义差异待独立调查 |
| ShortEdge / Capture | manual 与实际 Before/After 复用同 context/API/occurrence 对象；保留输入输出 refs；实际哈希源于 saved bytes；26 primary 同 owner；pre-Before auxiliary 属 scenario；重复 case 和 nested scope 不串 peer | 默认例不等于完整 saved-probe/lifecycle；缺 same-input/constructor 证据，operation 和 case ratio 均 null；F1 影响 failure summary |
| Refresh / history | 未改 Refresh；真实公共文件入口验证旧 combined 926 cases 和最终 ShortEdge 463 registry entries；sample 前缀与 check owner 改写保留 | raw entityReferences predecessor ID 的 sample 上下文仍按旧 bd7 remaining item；没有借此签跨样本实体全局身份 |

纯 BCL ScopeProbe 的三组真实子进程都因未捕获控制异常原样 exit134，finally checkpoint 的 `legacyExitCode=null` 与 `inProcessExitCode=0` 分开。返回前/后和 cleanup throw 的 owner、later sibling not-run、较早完整24项 synthetic primary 的 status pass 均保持；没有 source/measurement 资格则仍不合格。另测 NaN/+Infinity/-Infinity 通过真实 RecordTiming 导出 null，不产生 ratio。共 **24 assertions，probe exit0**。这些是记录负控，不是 PK 建模证据。

独立 OwnerProbe 在固定 Capture 全文件 hash 下提取真实 managed identity 字段、case/context scope、Before 前缀、After wrapper、Complete 的返回前缀、AddCheck/Observe。只在 mesh/native/tracking/Compare/Flush 边界设 seam，未重写 owner 字典算法，也未调用任何厂商 ABI。两个 fresh sample 各 **27 assertions，exit0**：两个 preparation checks 不创建 fictitious API；实际 native-only case 后的重复 case 不接错 GM peer；第二 rep 和不同 context 分离；manual/native/GM 对象相同；Before/After 观察异常归辅助错误；After 先记录真实控制返回17，再抛 observer 异常；后续0不能擦掉 failure；嵌套异常恢复。24 primary 的 fixture 明确 synthetic，不能宣称真实 geometry pass。

## 作者产物只读审计，不升级为本轮独立 PK 重跑

作者树相对本项目为 `../gm-operation-recording-r2`。其 `temp_docs/operation-leaf-r2/` 下限定产物、两个旧失败 offset checkpoint、三个 ShortEdge Capture 阶段及 shared-return Boolean 均先读后做独立探针。71份 JSON/XT/log/report 文件 SHA 在 `operation-2c06-evidence/artifact-inventory.json`；清单内 path 相对于作者树，未改其字节，结束时 `--verify-unchanged` exit0。

| 原 run / entry | 原进程事实 | 来源与记录结论 |
| --- | --- | --- |
| `freeform-final` / parallel-nurbs-path-solid | supervisor0，两条必需 XT lifecycle children 已执行 | leaf hash `4d7de881…` 正好是固定3296版本；27c只修后两个 edited-surface API 名称；其 Rules `abee84dc…` 早于最终 `9ba16afa…`。不能称2c06全源一致重跑 |
| `local-import` / native-import-face-elimination，`local-analytic` / sphere-outward-hollow-identities | 原 report/log成功，ledger保守；没有各自 supervisor execution.json | 实际指定例有证据，外部退出仅作者日志，不伪造新的 supervisor0；完整 independent lifecycle 本轮 not-run |
| `local-final-rejection` / separated-walls-in | supervisor0，GM/PK均13 | 13 primary pass；GM before/after/saved own XT hash相等；baseline为独立 pre-call PK，不声称PK rollback。leaf与候选一致，Rules为阶段性abee版本；候选公共磁盘入口对原事实exit0且仍unverified |
| `geometry-body` / `geometry-body-final` / translation-body-block | 前者supervisor0，后者作者直接日志0 | 最终 direct run ledger有transform0/0、GM delete0/PK null；当前PDB叶子及shared源码校验全部匹配候选；前一supervisor的shared源码不同，分开保存 |
| `evaluation-offset-provenance` | **supervisor134**；原异常未吞 | leaf/Rules/Ledger/Host/BodyCompare六项hash匹配候选；first=`op-dep-offset/delete-base-gm`；PK base22（原允许0/22）、GM base5002；GM child PK null；两次采样pass在。原in-process legacy=null/diagnostic0；较早两个134失败run不覆盖 |
| `short-edge-final` standalone | supervisor0 | standalone leaf hash `80469cc1…` 为case scope之前阶段，不冒称候选精确完整重跑；主操作仍unverified/null ratio |
| `r2-short-edge-owner-final` | original oracle0，但重复pre-Before BODY_check引发 capture-internal 重复ID observer error | 原失败manifest `dafb1292…` 保留；26 modeled primary仍pass，不能认作native几何fail，也不删除那两个internal辅助owner |
| `r2-short-edge-scenario-owner` | original oracle0，observer errors空 | scenario准备检查保留；仍是中间Source/Capture版本 |
| `r2-short-edge-case-scope-final` | original oracle0，作者Refresh0/Validate0；本轮独立public Validator0 | 四项关键source hash与候选一致；26 primary同owner，GM/PK0/0；准备检查为scenario，operation与case ratio均null；manifest SHA256 `25cd62488a5c1c8d9cb98eb701b5f747d9273d467c9a642741857e745200c4ed` |

原产物 `run.commit` 有27c等执行时基点且 `dirtyAtStart=true`，本报告使用实际 sourceHashes、PDB documents 与 candidate bytes 比对，不将 run 名或 commit标签冒充编译签名。

PdbAudit 只读枚举当前 runfile cache，核 PE CodeView GUID 与 portable PDB ID，并冻结相关 DLL/PDB 到本树 ignored `temp_docs/operation-2c06-review/pdb-copies/`。完整 hash/document checksum 清单为 `operation-2c06-evidence/pdb-inventory.json`，cachePath 相对于 .NET runfile cache。ShortEdge 实际程序集名是 `BooleanOperationOracle`，未误取同名其它脚本；候选叶子/Capture/Ledger/Rules documents 均匹配。其 DLL SHA256 `65856977da1954612bdfb12d0bc51f4944b4821288c4b62227e66230d6ee4b10`、PDB `27ea64e405341fbbb7a6cd7615ee6c94d9dbcaf85a947aacbd0bb0cf768c8cc0`。Evaluation DLL `bb75d62ca08de30304291544f732e87e5858b8136b7c638af94049c6e02e75f7`、PDB `561c9f42ea395e71539d66c7a03de36590b56fb44d69f4aef24efc5f8b7e76f0`。

缓存快照不是原进程不可变 binary attestation；Freeform/Local PDB仍显示上表阶段性source checksum，本报告没有隐藏这些不匹配。独立 Capture shim DLL/PDB分别为 `2fe6817b3660572046f784487be2512e43b95fba5a4501ed053d7a5743b5721d` / `431cdc6be909790ed6f3e7a08df081f01a13873865e2833938b102ba16713523`，仅证明本轮受控源码入口。

## 公共文件入口、兼容和剩余边界

DiskProbe 不调用 Evaluate 来制造期望结果，使用先前独立review冻结的 synthetic complete v1 fixture，逐份修改事实并启动真正 `scripts/ValidateWebProgress.cs` 文件入口。16项中 **15项匹配预期，整体原exit1保留**：旧926 cases /26 meshes /15168 triangles errors空；最终ShortEdge463 /4 /612 errors空；旧observer失败run可作为失败数据验证；真实Local v3拒绝记录仍unverified；legacy missing和v2 complete支持；unknown/numeric伪pass、v3 body伪pass、自声明901伪资格、v2套用新拒绝政策、schema/checkVersion伪升级、缺结构primary/错误owner均非零拒收。自声明901保守unverified可通过schema校验，绝不取得建模资格。

唯一未匹配项是畸形 JSON 类型 `timing.gmMs="NaN"` 配伪ratio：预期CLI结构化拒收exit1，实际 **exit134**，`scripts/WebProgressDataValidation.cs:148` 的 `GetValue<double>()` 抛错。该文件在0935与候选 SHA均为 `d600bef75107ba2f947aa9a201507dc92bfe6113d2214363ae1d56b8094f8979`，属于既有 public validator 错型诊断边界，未假签资格，不当成新增native算法fail或本轮F1的变体；无进一步类型fuzz。原log为 `temp_docs/operation-2c06-review/disk/nonfinite-numeric-string-forged-ratio.log`。与真实nonfinite double经writer转null的通过控制明确区分。

旧真实manifest `57117536c174211f85be9a2c20d2560c6925b768491ff50d831340f7dd20dca0` 在原树和本轮副本均不变；没有删旧run、补签pass、改旧bytes或用oracle checkVersion签新解释。unknown-version synthetic理由措辞的旧TODO仍保留：始终unverified、实际历史未发现，不声称全部unknown bytes兼容。receipt、正确曲面eec原run与四receipt已有作者audit0的背景未升级为真实双侧U/V消费者交付；本轮未重跑展示case。

## 复现、验证与下一平台 gate

在项目根运行，均.NET10/BCL，无PK session、内核build、NativeAOT或发布：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/ArtifactAudit.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/OwnerProbe.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/ScopeProbe.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/FirstFailureProbe.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/DiskProbe.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/PdbAudit.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-2c06-evidence/ArtifactAudit.cs -p:UseSharedCompilation=false -- --verify-unchanged
```

DiskProbe复制到新目录时拒绝覆盖；重复复现应先选独立新review输出目录，不能清理或改作者产物。保留原first-failure exit1、DiskProbe exit1与一次环境变量拼写setup错误exit127，不把它们改绿；原scope18断言版本与最终24版本日志分别保留，原PDB审计快照亦不覆盖。ArtifactAudit最终 **63 assertions /71 files /exit0**；OwnerProbe **27×2 /exit0**；ScopeProbe **24 /exit0**。没有运行实现者的 OperationAcceptanceSelfTest 来代替这些独立检查。

关键原log SHA256：artifact audit `ef7d643115d621da852f02d48450512600a6b2ca1c55238c08ac57532b611581`；owner probe `9b0ec067bdd681709267f27585b9b6de3295dbce3b4388a17aac65ac524f86f0`；scope probe `e88ce6a1e8388a89e6c2fb4c16ea307ac536117833007ab2af091f7c6d897c9f`；disk probe `f3215707226fc5415960b75f979da8be82e2c85f90918622e24409dea5088711`；first-failure `63a8cd7158f7502308aa144c8033e8298e6cd44988fea942522bc2d2fef22172`。FirstFailureProbe编译DLL/PDB为 `b462a2cdbdd8a8b30a92dd842b099d13b1ba18d3be802d44710a107f898d7900` / `006d2305b223798854d495c647c2b8df95a23854deecd83daf5bed1f6a653e93`；PDB文档checksum与候选Ledger/Rules、实际probe source均匹配，完整probe自身hash见随附PDB清单。

下一对比平台消费集成的具体 gate：先关闭F1，再按真实source/interpretation版本导入有限run；保持scenario auxiliary/observer failure与native primary独立；sample/occurrence/case身份和未知raw return必须保留，不能合并第二rep；保留24 body-result/26 ShortEdge合同及strict compare/fullBODY/mass证据和失败check。缺构造合同、same-input snapshot、配对参数或真实API timing时继续unverified/qualified=false/null ratio；组合任一后步失败不得抹已过前步。若要接受完整leaf/PK lifecycle或新增建模资格，另行按同输入/参数/API真实PK独立验收，并先完成统一privateenv prepare/check与API核对；本轮这些运行明确 **not-run**。

本树仅docs/repro，不改作者代码、CSV、阈值、历史bytes/core，不跑19/96/151/corpus/347/AOT/性能/Mac，不碰live、units、portal、proxy、main或其它树。线上UI424、producer610/17与尚未部署的候选保持原状态；报告不授权部署或宣称真实U/V已交付。
