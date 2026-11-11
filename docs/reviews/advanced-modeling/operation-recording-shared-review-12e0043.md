# 12e0043 共享记录层定向复审

固定候选 **`12e0043a87f612f54b7e7d5d9541e1371ea61adc`**，against **`5d45dad0b92bd28276f88c1f2cb4fcf35deda6ef`**，远端 `fix/operation-recording-r2`。独立审查：Codex GPT6.1Sol high，2026-11-11；只审7文件共享增量，未追后续leaf/receipt或移动工作树。

**结论：共享层仍blocked。** 原 fail复活、默认成功返回、重复ID静默合并、非有限时间导出崩溃、Options/provenance别名及多项浅validator漏验已关闭；但两个共享合同缺口仍在：not-run会遮住已记录的API/primary失败，输入/输出getter借出的嵌套对象仍能改变已完成operation的记录。两项均已交协调端转实施者，review不修改实现。

此结论针对12e；任务结束前追加授权的固定a2修复已另节复验并窄范围接受，见文末，原12e事实及反例不变。

这不是A1–A7整体复审或真实case资格验收。Capture/Refresh适配、leaf异常归属与真实返回、同输入隔离证据、场景实际退出/版本/样本身份仍是明确后续工作；不能据本轮合成正例给175API、旧真实case或整个框架签过。

## 固定范围与执行

读取原独立报告 `90b662317e60685b12ca6abbe9632f47a815f8cb` 的 `operation-acceptance-independent-review-5d45dad.md`、候选 `operation-recording-r2-shared.md` 与完整共享差异：Ledger、OperationAcceptanceRules、WebProgressDataValidation、props、OperationAcceptanceSelfTest、ValidateWebProgress及说明文档。候选/5d的src tree均为 `7d516f6c16c762581f0ba2d87df4df5e263c6cc4`，本轮没有内核/R3/OptA改动。

SDK10.0.302，以下均为BCL/.NET10，串行、`--no-cache`、源码固定；未启动PK、kernel build、全463/19/151、AOT、Mac负载、main或live。原probe源码和历史证据保留；运行新validator自检前已备份旧生成fixture到 ignored `temp_docs/operation-shared-12e-review/prior-validator-fixture/`，不是修改历史run。

| 实际命令（均加MSBUILDDISABLENODEREUSE=1） | 原exit / 结果 |
| --- | --- |
| dotnet run --no-cache --file temp_docs/operation-review-5d/LedgerProbe.cs -c Release -p:UseSharedCompilation=false | **134**，执行到duplicate明确抛错；后半未执行 |
| dotnet run --no-cache --file temp_docs/operation-review-5d/ValidationProbe.cs -c Release -p:UseSharedCompilation=false | **0**，原13变体：11个无效operation变体均拒绝；2个legacy保持无operation |
| dotnet run --no-cache --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- --self-test | **0**，18条PASS |
| dotnet run --no-cache --file scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false | **0**，29条PASS |
| dotnet run --no-cache --file temp_docs/operation-shared-12e-review/IndependentProbe.cs -c Release -p:UseSharedCompilation=false | **0**，独立后半控制及15个实际序列化公共入口变体 |

原probe字节仍与任务固定值一致：Ledger `c76d37a2ad92e5b642bc21b870f1d03dbb5fbda5acf8116445c0a66cfbaf3e85`；Validator `fbdeeb3a8415a7d0c598af337cfaf613461a914c5516c58d0950b8a3a315d622`。候选文档如实记录原Ledger在duplicate终止，这点被独立复现，**没有把半个probe叫作全执行**。

## 已关闭及接受边界

原Ledger未改输入的输出为：`before=fail after=fail qualified=false returns={gm:null,pk:null}`，随后在BeginOperation重复ID处抛InvalidOperationException（Ledger50）。这是明确合同拒绝，134不是某个实际建模API失败。

独立后半probe另文件执行，不冒充原probe继续运行：

- fail和timeout经重算/序列化/后续End仍sticky；unknown scenario不再pass。
- 零、负、NaN、正/负Infinity共5种时间均qualified=false、ratio=null且JSON导出成功，前一个complete operation仍为primary pass，没有丢失前步证据。
- Options与InputProvenance分别对原传入对象和getter返回嵌套对象做修改，内部值仍为1，parameterHash能按同一snapshot重算；ToJsonObject返回值本身也独立。
- 一个明确声明 `capture-body-result-v1` 的完整合成记录可以pass并qualified。其24项required为结果数、双BODY_check、5项拓扑数、strict equivalence、15物性；两侧已知0返回、合法配对参数hash、输入SHA、明确isolated-same-input声明和正有限API时间都提供。
- 在这个完整正例加入owned auxiliary fail / representation fail并EndScenario(1)，仍得到 **primary=pass、aux=fail、rep=fail、scenario=fail、qualified=true**。场景原失败不倒扣已经完成的primary；这个正控应保留，不能为修新反例一律把aux或scenario失败转成primary失败。

公共validator不是独立几何oracle。新Rules与validator共享解释器，故本审查只通过**序列化文件→WebProgressDataValidation.Validate**入口核对事实变体，没有用同一个Evaluate的返回再盲断言自己。完整synthetic正例errors0；逐项缺required、required降diagnostic、错owner、返回901、伪输入hash、未知same-input、未知contract/API、错ratio、状态不一致均有errors。一个非法nested returns数组抛InvalidOperationException，未默默qualified，但尚未规范化为errors数组诊断。

本次接受的是上述已测记录规则，合成pass只证明记录合同；不证明虚构SHA对应真实几何、同输入声明已做隔离实验或任何实际API已通过。具体supported API是Rules45的有限白名单，其它合同/API保持unverified。

## 阻断 S1：not-run优先于已知失败，公共validator也接受

位置：`scripts/OperationAcceptanceRules.cs:29-40,61-62`；`OperationAcceptanceLedger.cs:156-168,188-201,288-290`。

Rules40已经算出explicitFailure，包括非零API返回或primary check fail；但61先选择terminal==not-run，因此将已知失败变成not-run。SetTerminalStatus只保护已经显式End为fail/timeout的状态，不保护已记录的非零返回/失败check。

独立操作序列：建立完整synthetic正例→RecordReturn(gm,901)→EndOperation(not-run)→EndScenario(0)，实际：

```text
NOT_RUN_WITH_ERROR op=not-run primary=not-run scenario=unverified firstFailed=null
returns={"gm":901,"pk":0}
PUBLIC known-error-not-run errors=0 []
PUBLIC not-run-failed-primary errors=0 []
```

第二个公共入口变体直接在完整serialized record中设terminal/status/primaryStatus为not-run，并明确放一项primary status=fail、ratio=null；它也errors0。两个输入均经过磁盘序列化/parse，没有直接向Rules询问期望值。

**影响是失败状态、scenario及首个失败归属错误，并非ratio伪通过**：返回901或失败check本身仍保留在JSON，ratio仍不合格，却被允许解释成“没执行”而非已知失败/矛盾记录。不能仅因没有qualified ratio就称该记录正确。后续修正应让已知失败优先，或拒绝not-run与已执行证据的矛盾；同时保留真实从未到达的not-run和前步primary pass。

持久反例为 [known-error-not-run.json](operation-shared-12e-evidence/known-error-not-run.json) 与 [not-run-failed-primary.json](operation-shared-12e-evidence/not-run-failed-primary.json)，SHA256分别 `faaf4fddfadd8c79b352afb3738778cf3ee4263d72733ef0c75a6066d09fed59` / `6d8604796a78063e7e656492354d0f4fa8fee9f97add2f1d063deb5415b38c07`。

## 阻断 S2：inputs/outputs并未与外部getter隔离

位置：`OperationAcceptanceLedger.cs:293-295` 直接返回live InputHashes / OutputHashes对象；对比Options273-279与provenance284已有DeepClone。

独立probe在一个已完成complete operation上借出 `op.InputHashes["gm"]` 和output的bodies数组；从返回引用修改target为64个b、body为64个c。再次ToJsonObject记录就变成这些值，公共validator仍errors0、qualified仍true。即使只是读取后误改nested对象，也会修改后续记录；这未满足本轮输入/输出snapshot隔离要求。

**不是要求GM/PK hash机械相等**：不同序列化可能对应合法等价几何。本反例核的是外部getter是否能改写保存的证据身份，修改后的值仍是合法hash格式，所以格式校验不能补偿对象别名。已经导出的ToJsonObject副本独立，不受后来修改影响；缺口是OperationRecord自身的live getter，不宣称历史磁盘bytes被改掉。

建议后续为可写构建期和完成后的snapshot规定明确边界，以显式记录方法写入、只读/深拷贝getter读出；无需借本轮接管leaf适配。若保留mutable builder API，也必须另有封存的历史记录并由validator/consumer读取封存版本，不能只因Options有clone就称inputs全部冻结。

## 后续边界及交付

共享两项阻断修正后只需重放这些固定反例与相邻正控，不需要全schema fuzz。A3–A7、真实case qualification、Capture/Refresh的输入来源/重复身份/实际exit/checkVersion/测量与legacy-aggregate解释仍明确待办；本轮没有将其重新评判或宣称已闭合。原两个legacy总pass/fail都没有捏造operation，但kind仍single-operation，是后续Refresh/解释层事项。

持久reproducer [IndependentProbe.cs](operation-shared-12e-evidence/IndependentProbe.cs) 与同目录props仅引用固定共享源码，BCL运行；先运行上表ValidateWebProgress自检准备磁盘fixture，再执行：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-shared-12e-evidence/IndependentProbe.cs -c Release -p:UseSharedCompilation=false
```

输出保存在ignored `temp_docs/operation-shared-12e-review/`；持久版仅改脚本相对路径解析，不改变变体/期望。原输入与独立变体分开，未修改生产或正式测试。

| 证据 | SHA256 |
| --- | --- |
| 固定Ledger源码 | `a390dfca5ba358e408eac37047fc94c4648b7dffcdf3940dab9417f475b601a0` |
| 固定Rules源码 | `caabbedec45505a45e4ec4447431f4bfe7d6618b3d06a99b629ced2ec41f4a65` |
| 固定WebProgressDataValidation源码 | `c67382502fb77276fab46dd065b4ac148c214a61157c7d5197b3603195d78800` |
| original-ledger.log | `c3a0057585cbec768ab33719e87e1a3b3d346eb64aec0dc9e72bad5504ffba0b` |
| original-validator.log | `05a763e616390e3e70d2382870aebf5862ec85edeff8c4ad017307daca195353` |
| shared-selftest.log | `212ec852ce2a1995a1dbccde2c05cb101aa51c4d52ff57396cd9fe377c63a092` |
| validator-selftest.log | `cae33c533cec10cc216c03dd9fa73734c309d13aa069af9ce8ba32b288589953` |
| independent.log | `bc071ce1108edd83303f1156edcd2b1a064af0c7fab3429576673fe0792c80c8` |
| ignored独立后半probe源码 | `7a079ec600a918037f727b73f3806926eaa8d2f821fb47d3dd484694a30028a0` |
| 持久IndependentProbe.cs（另一次--no-cache验证exit0） | `34bf25a7a9b46c3b27e671742e6820f4c71f4f73e476991d686e3a45be278291` |

固定12e的已关闭部分得到承认，两个共享阻断不得归入“仅leaf TODO”。本轮不接受该共享候选进入后续集成，也不对真实建模几何作新结论。

## 追加授权的固定a2复验：两项共享阻断关闭

在上述12e报告已提交推送 `2ea4b16649f79f0b35657e0efb0b929c5ce68eeb`、任务尚未settled时，协调端明确追加固定 **`a2be6682f0f108bcfc80a0b47a98b37b297c8a35`** 的两项边界复验。原12e结论及两个输入保留，不重写成“12e已过”；也没有审后续移动代码或leaf。

从该固定git对象archive共享BCL文件到 ignored `temp_docs/operation-shared-a2-review/scripts/`，其独立props只引用这些副本；本树生产scripts仍为12e。源码与12e差异限定为not-run矛盾判定、hash getter的深拷贝/显式写入方法、对应自测/validator一条矛盾拒绝及说明。

修复位置：a2 Rules61-64先判explicitFailure，再允许无执行事实的not-run；有执行事实时新增not-run-contradicts-execution原因，validator专门拒绝该矛盾。Ledger EndOperation在已有return/primary/timing时把not-run终止标记为fail；InputHashes/OutputHashes由私有snapshot保存，getter深拷贝，RecordInputHash/RecordOutputHashes走显式clone setter。

**独立实际结果：**

| a2受影响边界 | 实际结果 |
| --- | --- |
| 原known-error-not-run.json，SHA仍faaf4fdd… | 磁盘公共Validate **errors4**：矛盾、状态/primary应fail及原因不一致 |
| 原not-run-failed-primary.json，SHA仍6d860479… | 磁盘公共Validate **errors3**：矛盾、状态与primary应fail |
| 完整正例经显式方法记录input/output后，借出nested getter改为b/c | 内部仍a/a，qualified=true，序列化公共Validate **errors0** |
| complete primary + owned auxiliary/representation fail，EndScenario(1) | primary=pass、aux/rep=fail、scenario=fail，公共Validate **errors0** |
| 已记录gm901后EndOperation(not-run) | op=fail、scenario=fail、firstFailedOperationId=fail |
| 从未执行、无返回/primary/time的not-run | status=not-run，公共Validate **errors0** |

输出hash写入准备使用显式RecordOutputHashes；没有再用已改成copy的getter来“设置”初始数据。输入getter的借出/嵌套变更步骤原样保留。旧leaf里直写getter的方式不会写回私有snapshot，后续适配必须改用显式记录方法；不能因本轮共享接受就直接把旧Capture/leaf叫qualified。

命令（均MSBUILDDISABLENODEREUSE=1、--no-cache，BCL串行）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file temp_docs/operation-shared-a2-review/BoundaryProbe.cs -c Release -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file temp_docs/operation-shared-a2-review/scripts/OperationAcceptanceSelfTest.cs -c Release -p:UseSharedCompilation=false
```

二者 **exit0**；后者固定a2自测 **34条PASS**。BoundaryProbe读取原两文件后实际serialize/parse并调用公共validation，没有调用Evaluate来生成期望。自测磁盘外壳由前面已有fixture复制到a2隔离目录，未运行PK或真实case。持久 [BoundaryProbe.cs](operation-shared-a2-evidence/BoundaryProbe.cs) 的props绑定上述a2 archive目录，路径调整后另一次--no-cache重放exit0；使用前需从固定a2提取相同共享文件，不能将12e helpers冒充a2。

| a2证据 | SHA256 |
| --- | --- |
| 固定Ledger副本 | `776ab7c7597a902559a5c619a48452ded63d414f50c6205bed19516b92ce16ea` |
| 固定Rules副本 | `ccb01be785fc2c7d9c239883e72d285cc86d4b4ace6ed21fb5ad650d0e225137` |
| 固定WebProgressDataValidation副本 | `dce3e785970d9cc919d879eeb7b5326ab32f2c43618a524f479f6f6135e3b86d` |
| boundary.log | `16140f51603ab650ad010835f1e387b0022918c1ec1be3e718d13e29131d3376` |
| selftest.log | `1d542551e7dd127f44aba5098f15b7d42b5f5bc9a2c9355f173a57c735a65bbc` |

**追加结论：接受a2对S1/S2这两个共享阻断的窄范围修复，并保留前述12e已关部分。** 原报告中其余明确待办不被这个结论关闭，未知/非法nested类型的规范化诊断也未扩审；没有给真实case、Capture/Refresh/leaf、全部A1–A7或直接main集成颁发通过。
