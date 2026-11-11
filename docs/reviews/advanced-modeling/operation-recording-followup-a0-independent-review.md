# Operation recording C1/C2 有限增量复审（a0 + 授权追加0935）

**分层结论：a0 的原 C1 历史记录兼容与 C2 ShortEdge owner 缺口在本次有限范围内关闭；a0 本身另有显式错型版本回退缺口，不接受其该版本边界。协调端追加指定的固定0935已关闭这一类型缺口，可有限接受这两项修复与类型补丁。** 不授予整套 A3–A7、其余 leaf 异常归属、receipt/UV、真实输入相同证明或性能资格的整体接受。

- 主候选 `a0c843386b535b2192a0579280e3ccca85b49553`，against `bd7a97919fcba9c607375c7172124ce7cdab2807`；历史解释兼容提交 `eecab6d51cae79a687fa459af512adc3e03895f5`。
- 审计过程中发现错型反例后，协调端明确追加**仅** `0935ec30dd1a5a669a06f3c69bafa473f770fd4f` 的版本类型窄复验，against a0。通过 git archive 固定 BCL 源；工作树仍固定 a0 的独立文档分支，没有跟随作者 working diff。
- 原 bd7 拒收报告 `870f8d1b548dceb3d85c5c827e8cc7215f36f43c` 及其原证据/分支保留，不因后修覆盖；a2 共享层报告 `ebed51368a51e90ff85ffcdfdbc6ce3f83074064` 的有限接受不重审也不扩大。此前 UI/cache/host 证据及分支均保留。

当前分支 `ThraceShah/gm-operation-followup-a0-review`。本轮仅 docs 报告与复现工具，不改生产、正式测试预期、历史数据、main、其他工作树或 live；没有 PK session、内核构建、Mac 任务、测速/profiler、AOT 或昂贵 lifecycle/oracle 重演。

## C1：同一历史字节与旧 index 公共磁盘入口复验

原 run `r2-shared-capture-single-multi` manifest SHA256 **`57117536c174211f85be9a2c20d2560c6925b768491ff50d831340f7dd20dca0`**，本轮开始/结束均相同。复制的是既有冻结材料，不重新生成、补签 pass、删 run 或修改原数据。另一个重复 run manifest 仍为 `33ed04f2c0408e9ca8817503a79bac88774aa33e46ef7d39b86c7a9b616ae1ad`。

使用真实 `ValidateWebProgress.cs` 文件入口；旧解释器由固定 bd7 归档的原公共入口、props、Validator/Ledger/Rules/Publishing/Metadata 编译，不是手写模拟解释器：

| 实际命令对象 / 数据 | 原 exit | cases / meshes / triangles | 结果 |
| --- | ---: | --- | --- |
| bd7 archived public CLI / 原 old-only-data | 1 | 463 / 16 / 13794 | 原同一 Boolean2 operation 的三个 discrepancies |
| 固定 a0 public CLI / 同一 old-only-data | 0 | 463 / 16 / 13794 | errors=[] |
| 固定 a0 public CLI / 原 combined full-data | 0 | 926 / 26 / 15168 | errors=[]，未过滤旧 run |
| 固定0935 archived public CLI / 同一 old-only-data | 0 | 463 / 16 / 13794 | errors=[] |

原三项错误仍精确属于 `analytic-boolean.equal-overlap-subtract / sample-0:op-1` 的 status、primaryStatus、unqualifiedReasons，**不是三个坏 case**。a0 的 `OperationAcceptanceRules.cs:45` 起按每个 operation 的 interpretationVersion 区分支持集；缺字段回到 v1，Boolean2 不被追认，原 unverified/null ratio保留。新 writer 的 `operationSchemaVersion=operation-layered-v2`、每 op `interpretationVersion=operation-rules-v2` 与仍为 `capture-body-result-v1` 的24检查合同各司其职；旧/new oracle `checkVersion` 不被伪造成 revalidation 签名，checkDefinition/source hashes 也不代替解释版本。

### C1-type · a0 新发现：显式99被当缺字段回退，0935已闭

位置：a0 `scripts/OperationAcceptanceRules.cs:45`，`Text(op["interpretationVersion"]) ?? "operation-rules-v1"`。Text 对非 string 返回 null，故明确的数字99也走 legacy支持集；这与“未知版本保守，不得取得资格”不符。

最小 synthetic public-disk 负控：一个 `PK_BODY_boolean`，24个完整归属 primary checks、两侧 returns0、成对 options/hash、synthetic input hashes/provenance、正数 timing，明确 `interpretationVersion:99`，记录 pass / correctnessQualified=true / ratio2。这只是 JSON 规则负控，不是实际几何或测速证据。其 manifest SHA256 **`ba1ac0f08a69b4fdc09c28d2826d740fbf4ec5d7c524de88e9ed88b266fc7d17`**。

| 固定解释器 / 原同一负控字节 | 原 CLI exit | 实际 |
| --- | ---: | --- |
| a0 | **0** | errors=[]，错型版本被当 v1接受 |
| 0935 | **1** | 四项错误：应为unverified、primaryStatus不符、qualification不符、reasons不符 |

该反例发现后即交协调端，未由 reviewer 修实现。0935只改规则的 missing-vs-present 判定与相关 selftest/docs：`ContainsKey` 为 false 才默认 v1；显式 null/object/numeric 不再回退，未知版本不支持合同也不能 qualify。原数字负控在追加复验前后 hash不变。

八项 a0 synthetic 控制：显式v2 Boolean2完整记录0；真正missing+legacy Boolean0；missing+Boolean2保守unverified0；missing+Boolean2伪pass1；unknown string保守unverified0；unknown伪pass1；v2错误显式status1；数字伪pass错误地0。因此 `VersionProbe.cs` **原 exit1（7/8预期，保留失败）**，不是全绿打印。

0935有限11项控制：原numeric1；v2完整0；真正missing legacy0；missing Boolean2保守0/伪pass1；unknown伪pass1；null1；object1；按当前unknown理由记录的unverified0；原历史data0；另保留下述旧unknown理由差异。`VersionRecheck0935.cs` 原 exit0，指的是这份明确包含拒收预期的控制集合。

**兼容边界不隐瞒**：0935给 unknown string也新增 `unknown-or-invalid-interpretation-version` 理由。a0 synthetic unknown-unverified旧三理由 fixture 原来exit0，在0935原字节exit1、仅 `incorrect unqualified reasons`；另建明确带当前四理由的独立副本才exit0且仍unverified/不合格。本轮没有发现真实既有历史包含该unknown版本，不把此保守拒收当新增假pass，也不声称“所有未来 unknown 记录理由均向后兼容”。原副本未被悄悄改写。该边界已反馈协调端并按限定范围收束，未开展更多版本fuzz。

## C2：实际身份/返回/check分支的源码 shim，非镜像字典

源码结论：a0 `WebProgressCapture.cs:31`–37 的 pending key现在取显式 context/API/GM下一occurrence；ShortEdge `:259`明确传 `context:""`，与 `:1688` Fillet的空context scope / 后续 Before相符。Lifecycle `:359` 在方法开头用 scope，包括其后早期实体操作抛异常也会恢复；Fillet同样恢复原 ambient context。查找当前固定 scripts 中 BeginObservedOperation/Context赋值/UseContext调用，未发现另一个依赖旧ambient默认值的 BeginObservedOperation调用者。

本轮不只重复作者三项 seed-dictionary 断言，也不把旧“无精确缓存 binary”exit2冒称通过。新 `OwnerProbe.cs` 对固定 Capture全文件 SHA作硬检查，直接提取并编译：

- 原所有 managed identity字段、BeginObservedOperation、UseContext、SetParameters；
- **原 Before 的 managed前缀**：侧序号、context/API/occurrence、ContextSteps、RequestedOperations、实际OperationRecord创建/复用、ObservationOwner、输入provenance/entity references、Steps创建；
- 原 After wrapper直到Complete调用，及**原 Complete managed前缀**：timing/return/output refs、实际 observed owner、API error check；
- 原 AddCheck/Observe异常归属逻辑与真实 Ledger/Rules文件。

显式 seam：Before在原selection/mesh开始前停止；After的tracking尾段、Complete的native tracking/mesh/immutable snapshot/Compare、Flush均不执行；vendor指针类型仅在被截断签名擦为void*，没有定义或调用P/Invoke/厂商ABI。注入回调模拟该边界异常。24个primary pass是通过原 AddCheck写入的**synthetic recording控制**，不冒充PK检查结果。也没有编译/运行完整 ShortEdge/Lifecycle；完整真实调用路径的 scope归属仍结合源码分析。这是受控执行实际owner分支，不是独立重写一套字典来替实现过关。

两独立进程 `sample-0` / `sample-1`，各 **19项断言，原 exit0**：

| 路径 | 实际观测 |
| --- | --- |
| 先真实prefix的native Fillet→imported Lifecycle Boolean→manual显式空context→GM Fillet prefix | 保存的native OperationRecord与manual、GM Before/After对象 `ReferenceEquals=true`；GM/PK returns均0，2/1 timing落同对象 |
| stale imported ambient负控 | 即使故意恢复旧泄漏情境，Begin显式空context仍取到原native对象，而非新建manual对象 |
| 正常/嵌套/抛异常scope | 原UseContext均恢复精确先前值；Fillet式空scope不永久擦掉caller的context |
| 第二occurrence与不同context | 第二轮新对象、不复用第一rep；相同API不同显式context独立，manual-first reservation也由实际Before两侧消费同对象 |
| manual+observed checks | 实际API error checks与手工auxiliary分类失败归属同一owner；synthetic primary pass保留、HasAuxiliaryFailure=true |
| Before/After边界异常 | 原Observe保留两条注入错误在实际owner；After先记录GM真实控制返回17，再遇边界异常，未把返回/错误转给别的operation |
| 唯一ID与sample | 同进程各occurrence raw IDs唯一、重复显式ID报错；新sample进程重新创建Capture state，不复用上一进程对象 |

“PK侧最后一次operation指针”在中间Lifecycle Boolean后自然改变，**不等于最初Fillet对象**；最终断言使用即时保存的native Fillet对象，不能拿全局latest指针替代所需owner。手工owner→实际GM对象这一证明依赖context/API/occurrence key，不依赖“最近看到哪个API”。

sample层边界：原Capture raw ID为进程内身份；Refresh `:95`–108克隆记录后加 `sample-N:`，改写check owners并设置全局ordinal，保留本地stepRef。该段本轮只核固定源码，不启动Refresh或冒称新的跨sample真实oracle run；原combined index的926 case/operation校验作为既有材料兼容性证据，不升级成新几何证明。重复的 `entityReferences.*.input.predecessorOperationId` 仍需sample上下文，原bd7报告的remaining item未被此scope修复覆盖。

0935相对a0没有Capture/ShortEdge/Ledger/Refresh改动，因此这份C2有限结论同样适用其未改源码；未借机审receipt或其他leaf。

## 原命令、源码与artifact绑定

所有新运行基于 .NET10，使用 `MSBUILDDISABLENODEREUSE=1`；脚本路径参数相对脚本自身。核心公共入口命令（根目录运行）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file temp_docs/operation-followup-a0-review/bd7/scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../../old-only-data
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-followup-a0-review/old-only-data
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../temp_docs/operation-followup-a0-review/full-data
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file temp_docs/operation-followup-a0-review/0935/scripts/ValidateWebProgress.cs -c Release -p:UseSharedCompilation=false -- ../../version-controls/numeric-version-forged-pass
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-followup-a0-evidence/VersionProbe.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-followup-a0-evidence/OwnerProbe.cs -p:UseSharedCompilation=false
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file docs/reviews/advanced-modeling/operation-followup-a0-evidence/VersionRecheck0935.cs -p:UseSharedCompilation=false
```

另运行当前 `scripts/OperationAcceptanceSelfTest.cs`，原exit0，37个PASS；是记录合同synthetic测试，不是PK/geometry资格。没有重跑共享层整体或昂贵套件。VersionProbe的批量公共CLI由首次 `--no-cache`的固定公共源码运行后，在同一未变源码下用正常SDK依赖追踪，未借旧Native缓存冒充新代码。0935 snapshot由固定SHA git archive生成，**没有改作者工作树**。

| 固定源码 | SHA256 |
| --- | --- |
| a0 Capture | `e212cbf7cd2167f2cde9d2aadbaaff69656d887d2c9536568efe433e17e1c592` |
| a0 ShortEdge | `adbbb74ba34098a3e14aa531bc736875eb5948c895cac7004284b8bc03715122` |
| a0 Ledger | `d8564e58b5b55853b43c20b2a1f0d922b910306a69571f6a5deb1b662645e4a5` |
| a0 Rules | `10e08f8255b93d5c36121fb39d3e76eb6e588a0dbde8347d31ae9fe86a841add` |
| 0935 Rules | `6f5310c03d2a8ff3bc8e1e37e6e776e5c2ad4075c9cbd5a809c9154eed1aaf6c` |
| 原Validator（上述三版本未改） | `d600bef75107ba2f947aa9a201507dc92bfe6113d2214363ae1d56b8094f8979` |

OwnerProbe生成shim SHA256 `14e7b2e446d463781bfc5576c6c4b611b3644c6a9d501a0f2c25ec732ea25bf3`，其实际managed Probe.dll `74d847cd260cfe816d0f31d529275caec3166a3e797e048c8255a77a7696c81f`、PDB `a45fdccc5aec40d45b86ee14d7584c100eb19ac43ab08897aa250e36cabe24fa`；这只是**本轮shim产物**，不是完整oracle/Native程序集的 attestation。提取片段hash也逐项记在owner-probe.log。

全部本轮原始材料位于 ignored `temp_docs/operation-followup-a0-review/`；bd7/0935 archived脚本、old-only/full副本、synthetic manifests、各公共CLI日志/exit及summary均保留。关键log SHA256：old-before `d676226b6f6231140f8af56d3096f60cb8dcfafeafe38f4cbf0b5c60b6bda50e`；old-after `14c511760266d43fdd5c820fbe6d3a585945c737b4d29dc1a1675bada4223cd5`；full-after `f95e42aa64ffee4c9e14db30a01789bbfeab0bfe6d27b38faec9ceeeb76371b0`；VersionProbe `068ad9e120eddfe96acf50d6059c575804d7a51bdccc639c375d69098544ffcb`；OwnerProbe `0e5539d1595668995430ecc40daf6eb7cd7c22daf81a31f4055a0ea47cebd591`；0935recheck `7e7289874d7cb57975dda8c7b7ff6ca94bd98f705b20931dee0816890bdb0605`。

复现工具搭建中出现过 JsonSerializer反射禁用、错误地拿latest-PK pointer代替保存Fillet对象、构造JSON时重复挂同一node的三次setup错误（exit134），均留有setup-error日志，修正只涉及reviewer自己的新harness；不是候选PK失败或通过证据。正式生产/既有test预期未改。最终OwnerProbe两进程exit0；VersionProbe a0故意保留错型失败exit1；0935窄复验exit0。所有自起小进程已结束，未杀其他worker/live进程。

剩余工作仍是独立leaf API/异常域闭环、真实单/多操作资格、实际receipt与双侧UV消费、后续集成部署；本报告不合main、不部署、不把安全记录修复说成这些目标已完成。
