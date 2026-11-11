# 固定 0d611d 首因修复独立复验

**F1 关闭；有限接受最终候选所声明范围内的记录代码。** 该结论承接上轮对 shared/五 leaf/Capture 的分层窄审，只关闭较早 owned verification failure 被后来 cleanup child 覆盖首因的问题，不接受全部 A1–A7、未知 API 的建模质量、完整真实 PK lifecycle、性能资格或对比平台消费集成。

固定候选 `0d611d174c65b30f66383526e374fd841ded39c5`（`origin/fix/operation-recording-r2`），against `2c06b9da80737f2d501bd5e1886c3ea1494908c7`。本树分支 `ThraceShah/gm-operation-first-failure-0d-review` 从候选建立，带入原 reviewer 提交的原样 docs/repro；`40f6ac3bc6e0df5725bdfef3a1c4e1096250529d` 的 [原2c06 blocked报告](operation-recording-2c06-independent-review.md) 与 `operation-2c06-evidence/` 字节均保留，旧失败没有被改绿或改写为当时已接受。a2、a0/0935 的原接受范围不变。

## 增量与原反例

相对2c06只有 Ledger 首因选择、有限 self-test 和作者说明三文件有改动；Rules、Refresh、公共 Validator、Capture、五 leaf、Host、Native 均无 diff，`src` tree 仍 `7d516f6c16c762581f0ba2d87df4df5e263c6cc4`。候选 Ledger SHA256 **`f1697b6fd1f36998a93802f3e606a097897500122e1721a317363778360a4500`**；Rules SHA256 仍 `9ba16afa7e0bac18c86167aac8cdcf71fab14b7e2861033b20818d9823cf0a66`。本轮未审作者 future working diff，也未改生产或正式 test 期待。

独立 Replay 从 Git 对象取2c06和0d611d的 Ledger/Rules/props，在本树新 ignored snapshot 各运行原 reviewer 的 FirstFailureProbe，**probe 原字节不改**：SHA256 `8ee9dc3e339c90164128a18bdc7b218bdac4e3b49451270f0579aed7d304b48c`。该 probe 通过真实 Ledger/OperationStepScope 入口，先记录 owned strict-compare fail，再 finally 中记录 PK22 cleanup fail；没有 PK session 或真实几何 API 调用。

| 固定源码 | 实际 probe exit | firstFailedOperationId | 原 JSON SHA256 |
| --- | ---: | --- | --- |
| 2c06 | **1**，原失败保留 | `verify/delete-received` | `db8e77e95fa32e6354b912789662606036d88b477d59b2bcf9a29a389bb38988` |
| 0d611d | **0** | `verify` | `6c7c3624223cc30311f6ad7ede61560b2062d7e2f207a8a6d2ae65fa7a1fe81c` |

删除两份 JSON 的 `firstFailedOperationId` 后 DeepEquals=true；两个 failure check、各自 owner、raw PK22/GM null、终态和原 caller `legacyExitCode=134` 全相同。作者声称的 before/after hash 与本轮独立执行精确一致；不是仅阅读其自测接受。本轮 JSON 同字节副本随 `operation-first-failure-0d-evidence/` 提交。

## 五组独立控制与公共磁盘入口

Controls 使用候选真实 Ledger/StepScope，独立建立明确标注 synthetic 的24项 body-result事实；没有用 Rules.Evaluate 返回值制造预期 first owner。五组共40项断言，原exit0：

| 顺序/情境 | 实际首因与保留事实 |
| --- | --- |
| parent真实 owned strict-compare先fail，cleanup后fail | 首因parent；parent/child两项错误均保存，24项先前pass仍在 |
| parent只传播childfail | 首因child；API-return failure check先于父传播的显式terminal check |
| child先fail，先Begin的parent随后独立owned strict-compare fail | 首因仍child；不按Begin ordinal误判，两项错误均在 |
| parent primary完整pass，cleanup childfail | parent primary/status仍pass；scenario fail、caller exit0仍0，首因child，PK22/GM null |
| normal与第二rep | 不创造primary失败首因；两个operation/check owner独立，auxiliary/representation失败不把primary改fail；NaN timing导出null，第二rep无same-input证明仍qualified=false/ratio=null |

此外在同一小控制入口核三个有限边界：显式 EndOperation fail 自动产生有序 primary terminal check，后Begin但先End-fail的对象排首，返回两侧仍null；无任何 failure check 的 raw/legacy terminal状态沿用旧 structural fallback，不新增顺序证据；未知API仅GM0、PK缺失仍unverified、first=null且不合格。没有扩大为类型/历史/时间戳fuzz。

前后原probe两份结果及五组控制分别写入七个新synthetic manifest，启动真正 `scripts/ValidateWebProgress.cs` 文件CLI，**7次原exit均0**；明确校验磁盘roundtrip中的first字段与全部operations不变。public Validator验证操作事实、schema与资格，**本身不校验 firstFailedOperationId 的事件顺序**；首因正确性由上述独立顺序断言与磁盘字段断言确认，不能把CLI0单独冒充首因证明。辅助失败的case用fail展示，保留parent primary pass，不把辅助诊断伪称native几何失败。

Replay整体exit0、33项断言；其中含原probe预期exit1及作者11份限定JSON/log的前后不变校验。作者原材料只读复制自 `../gm-operation-recording-r2/temp_docs/operation-first-failure/`，`author-inventory.json` 的path相对此目录；未改作者文件、旧JSON或原2c06报告。没有运行作者 self-test 来代替这些独立接受。

## 接受语义的准确边界

`scripts/OperationAcceptanceLedger.cs:204` 从 `AllChecks` 的插入顺序选择第一个 category=primary、status=fail、且owner对应现有failed/timedout operation的检查。RecordCheck先向AllChecks追加，再放入owner数组；RecordReturn的unexpected返回、EndOperation的fail/timeout和未Complete的StepScope.Dispose，均经这条入口产生failure check。因此已记录事件的parent/child先后不再被无条件“排父”或Begin顺序覆盖。auxiliary和representation不参加首因primary选择。

这是**首个已记录、归属有效的 primary failure**，不是所有底层错误真实发生时间的全局排序。多API构造、未插桩调用、直接改Returns/PrimaryChecks、缺owner或缺failure check，不因此补出真实调用时间或未知返回；无匹配事件时保持2c06既有fallback，仅保留结构选择，不能宣传其证明真实先后。上轮的unknown-version理由措辞TODO、validator畸形timing字符串原exit134诊断边界仍在，本修未覆盖、不重做fuzz；也没有新解释版本、checkVersion签名或历史bytes重签。

有限接受的是记录事实的修复与先前已声明范围的记录衔接。Freeform复合构造、Local拒绝、Geometry未知返回、Evaluation采样数值、ShortEdge缺same-input等先前unverified/qualification限制均保留；这里的synthetic pass不能补上它们的真实operation质量、同输入/同参数/同API基准或性能证明。

## 来源、命令与后续 gate

全部新产物位于 `temp_docs/operation-first-failure-0d-review/`，外层log位于 `temp_docs/operation-first-failure-0d-logs/`。Replay归档源码与实际before/after日志/exit、七份公共CLI日志和JSON均保留；`artifact-inventory.json` 的67项path相对此新产物目录，`replay-facts.json` 保存revision/source hashes/实际exit/log hashes。原2c06 JSON不覆盖。

Provenance从实际runfile产物核Portable PDB document SHA256与固定snapshot源码、PE CodeView GUID与PDB ID，四对均匹配，并复制实际DLL/PDB到ignored `pdb-copies/`。清单 `pdb-inventory.json` 的cachePath相对于.NET runfile cache，document path相对项目根：

| 实际新编译产物 | DLL SHA256 | PDB SHA256 |
| --- | --- | --- |
| before原probe | `f3ab0add306a68e791938efe11604a37199a40bea22e34f67e61a14a45a948e2` | `cda04288114b4c3688d9e8d9b3df2e85e1010b8385e116e318af55ace06ae8a3` |
| after原probe | `3de9501718b0c61cc4c4da8f2801a647bb2979897d6d9ec452c3548932777542` | `52d66f136d11e4cdc4ad53f869c7ff8fd1e61687dbd50d2f122f93869167ad29` |
| 独立Controls | `7ae55662824a7f03ae68df102a1fcf905e70e7859892762fef9bf7877606dc50` | `25d189081217a8a25710305b1742aee8ddab42ad33fee4a2c631a41125611df1` |
| 公共Validator | `ee34d83877bbcddd3f146f5b1ea1e4f6777fcd746a43b16fad3bb4307725a2a5` | `7788fe4a30157ff8a6b0fd137803a6a0a2267020ae449e6c20bf07b7cc48c3ce` |

根目录命令如下；Replay拒绝覆盖现存输出，重新复现应选择新review输出目录，保留本次原证据。Provenance也拒绝覆盖其冻结binary：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-first-failure-0d-evidence/Replay.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-first-failure-0d-evidence/Provenance.cs -c Release -p:UseSharedCompilation=false
```

Controls source SHA256 `a82b58b88151bd386457d5b370b65fac5c36bbf43d4a6809004e66b463a0f558`；Replay `e5157aa67f2e226fb18450f2ba6f34b0d58d2d5cd1774bbfe2f4b3e139c5d3ae`。外层Replay log SHA256 `f4a87070383c940cff49a25cd9c764d062189b373202bc1761ece2b0646a775c`，Controls log `16297aa22ee65b7edfa94a3ce13e82eb33391e6d1548da5b58673f5cefeb26c2`。一次运行前环境变量拼写命令exit127仅为reviewer setup错误，未运行dotnet或影响候选判断；未将其算作通过控制。

后续对比平台/UI消费仍须保留sample/occurrence/case身份、同owner primary与scenario auxiliary分层、未知returns/null、前步pass与后步fail、first字段的记录顺序语义，以及24 body-result/26 ShortEdge合同和独立strict/fullBODY/mass证据。构造、same-input、参数配对或API timing缺证据时继续unverified/qualified=false/ratio=null；不能因本F1关闭而补签模型成功或总倍率。完整真实PK lifecycle、未知API质量、资格、receipt/真实双侧U-V消费与集成部署均是另行gate，本轮明确not-run。

本轮只有BCL源码/公开磁盘入口验证；无PK、新oracle、full96/19/151、AOT、性能、Mac或UI展示case，没有动CSV/阈值、main、live、units、portal、proxy或其它工作树。
