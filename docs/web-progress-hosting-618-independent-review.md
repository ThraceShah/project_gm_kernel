# Hosting 618144f 目标代次 closure 独立复审

**结论：接受本固定候选的 rollback 目标完整 closure / generation 身份修复。** 原 `runs={}` 反例已用未改输入独立关闭；额外黑盒矩阵26/26通过，拒绝均来自真实 rollback CLI，当前权威状态和 HTTP 字节保持不变。未发现该限定范围的新阻断。

被审源 `618144f1381cc14d98cc0c9dd582e52fc4b81a09`，against `1b4efa0fac20515a2b846a5738000fdfa46de587`；review分支 `ThraceShah/gm-hosting-618-review` 从clean树建立。先读旧正式报告 `11617841d0bd910c67ebd5ddd03ef1645be26dbe:docs/web-progress-hosting-1b4-independent-review.md` 与新 `docs/web-progress-hosting-closure-fix.md`，仅审固定差异。旧报告当时的blocked结果保留，本报告关闭其中唯一剩余 H1b-r2，不改写历史为通过。

本轮仅增加报告及独立BCL诊断reproducer，没有修改生产、正式测试、阈值、main、live、portal、units/timer或其它工作树，也没有merge/部署。A scheduler固定 `5ae2cb53573dda56533b3ddb7ec46b5fc878edd0` 的既有接受仍有效，本轮未重审A。这个host局部接受不等于A+B集成、状态迁移、同slug代理健康或部署回滚gate已完成。

## 原两份反例：未改输入、真实 CLI、独立 HTTP

原输入只读复制自 `../gm-scheduler-review/temp_docs/hosting-1b4-review/`，SHA256逐字节吻合：

| 原脚本 | SHA-256 |
| --- | --- |
| `RollbackClosure.cs` | `d21005e28ab11703cd8dd7be990eb9eef571bac2c97ab77ce717afbfc522d7ef` |
| `RollbackValid.cs` | `0434538148fa3e51545e02894ad93922eb437128b5729a98f4db414c0537871c` |

两者依次在本树 `temp_docs/hosting-618-review/` 执行，helper由当前固定source编入、子进程调用真实 `scripts/ActivateHostedWebProgressSite.cs --rollback`，HTTP由新建的真实 `GmProgressHost.dll` 提供。不是只复制新helper调用来推导正确。

| 可观测项 | 两份原诊断本轮结果 |
| --- | --- |
| 诊断外层 exit | **0 / 0**，只表示诊断完成 |
| 完整schema源validator | `RollbackValid` 两次均 `errors=[]` |
| 删除目标metadata中必需runs绑定后helper | **false**；`generation closure: r1: required run mapping missing` |
| 内部真实rollback CLI | **3 / 3**；`ACTIVATE_REFUSED_GENERATION_CORRUPT` |
| HTTP `/data/index.json` | **200，仍r2** |
| HTTP目标r1 manifest/log | **404 / 404**，因为目标未激活；不能再拿此404当当前index引用损坏 |

原1b4的相同输入曾helper true、CLI0、index切到r1而r1 refs404。本候选拒绝在提交点前发生，差异确已关闭。原脚本未断言pointer全文和当前r2所有资源字节，下面独立矩阵补此部分。

## 独立故障矩阵：25次拒绝 + 1次合法回滚

`temp_docs/hosting-618-review/ClosureProbe.cs` 是reviewer新写的黑盒诊断，不调用 `VerifyGenerationClosure`、`Activate` 或 `Rollback` helper。它经真实Prepare/Activate CLI建立target r1+r0（含跨run mesh及quarantine raw log），随后建立仅r2的successor；只用自有loopback host读结果。目标quarantine目录还含损坏且未indexed的manifest，不能因raw log被引用就将该manifest当作indexed定义解析。

每个负例只改reviewer的目标store快照；manifest/tree变化使用真实的新内容键，防止仅因旧checksum不符就冒充语义验证。每次要求真实CLI **exit3 + CORRUPT + 对应具体原因**，并逐字节验证authority `site.json`（含activationCount/previous）与mirror全文不变，以及UI首页、当前r2 index、manifest、log、mesh五条HTTP均200且原字节完全相等。故障后恢复自己的目标快照，源cache不改。

| 负例组 | 实际拒绝层 |
| --- | --- |
| 空/array runs；缺indexed run；缺跨run run；多余未引用run | 必需run查询或最终required集合相等性，非空循环特判 |
| r1绑定r0树；tree为array；treeHash字段与路径键不符 | manifest run id、JSON类型、tree record key |
| 重复tree path；分别删manifest/mesh/log路径 | 严格路径顺序/唯一性；从indexed manifest派生的required file查询 |
| 删除对象；对象内容不匹配其hash | 读取失败明确转closure错误；真实内容SHA校验 |
| manifest run id、commit、run/cases/tools类型、evidence引用类型错误 | 对应身份或JSON类型检查；均用重新hash后的对象/树进入语义层 |
| 跨run mesh引用不存在 | target run tree的实际required file查询 |
| generation metadata id错误；index descriptor指向另一run | target身份绑定，而非单看index checksum |
| 改index合法无关字段并更新metadata index hash；改manifest合法额外字段并更新对象/树映射 | **generation identity mismatch**，证明身份绑定实际index和run→tree映射 |

共**25**个负例；每个CLI3、具体原因匹配、pointer/count/previous/mirror/5HTTPbytes不变。最后恢复完整目标，仅将metadata.runs JSON属性顺序反转：真实rollback **CLI0 / ROLLBACK_ACTIVATED**，index恢复目标原始精确字节，两indexed run的六个文件及跨run raw log全部200且字节正确。authority generation为原target、count3、previous为r2；mirror的count/previous一致。说明合法多run、属性顺序和隔离坏manifest没有被误杀。矩阵整体 **26/26、exit0、source unchanged=True**。

持久reproducer在 `docs/hosting-618-review/ClosureProbe.cs`，与实测临时脚本仅输出目录及说明不同，实质fixture/故障/CLI/HTTP断言相同；不会覆盖旧证据。无需为此重复整套86检查。

## 源码合同与拒绝归属

固定差异仅 `scripts/HostedWebProgressSite.cs` 的closure验证/逻辑引用解析与Rollback入口、验证脚本和文档。host serving源码、Prepare/Activate CLI源码和原H1b/H4b实现没有改动。

| source位置（固定618） | 独立判断 |
| --- | --- |
| `scripts/HostedWebProgressSite.cs:501`，`VerifyGenerationClosure` | 先验证metadata object/schema/id/runs，逐run验证tree schema、内容键、files数组及每条二元记录。relative path/hash必须合法，路径严格ordinal递增，重复项不能用dictionary覆盖后假通过。每个CAS对象一次真实hash、同一次验证按hash去重。 |
| 同函数index/manifest部分 | 实际读取目标index快照并验证hash；从index descriptor要求其run的manifest路径，读取对应CAS manifest，要求run id/commit一致。遍历gm/pk before/after/tools/afterBodies、check evidence、case evidence；按manifest目录解析cross-run路径，要求每个run/tree/file真实闭合。 |
| 同函数集合/身份尾部 | required为indexed和referenced run并集，必须与metadata映射集合相等；latest必须registered且与metadata一致。generation id由实际index SHA及按原生成规则排序的run→tree映射派生，不以metadata自报id/hash代替内容联系。 |
| `TryResolveLogicalReference` | 在逻辑runs空间解析相对路径，拒绝超出runs、非法字符/绝对路径/反斜线/冒号；manifest子目录及跨run映射有明确归属，不从source cache重新找文件补全。 |
| `Rollback`调用及catch边界 | 验证在authority提交前；closure的JSON/type/format/argument/overflow及文件/权限错误转成false+定位原因，调用者输出CORRUPT并由CLI退出3。缺metadata/index不再混报NO_PREVIOUS。有效目标才进行pointer原子提交与原mirror流程。 |

closure不是UI完整schema/几何正确性验收的替代：它负责已indexed定义所要求的资源和身份，不重新计算mesh或PK结果；未indexed坏manifest只有作为对象的存储hash义务，未被提升成需要递归解析的索引定义。拒绝发生于目标存储故障语义；没有扩成同用户恶意FS隔离、每GET全代重hash或一般常量内存保证。

## H1b forward repair/cache 与 H4b history未回退

只读复制原 `Integrity.cs` 再执行，外层exit0；其内部真实CLI初次activate、镜像目录故障下forward、仍坏镜像下rollback、正常同pair重试均exit0，正确报告mirror失败及 `ACTIVATION_STATE_REPAIRED`。authority和mirror count均3、完整previous一致，没有额外提交次数。reviewer另作只读postcheck，核active pair全部六个字段、previous、count3逐项一致。

相同诊断受控损坏CAS对象后，源仍正确；同输入activate输出 `STORE_OBJECT_REPAIRED`、缓存generation复用，HTTP **200 / ORIGINAL_SOURCE_EVIDENCE**，source unchanged=True，served SHA与对象键相符。postcheck再次核source/CAS字节一致，exit0。与源码中未改的每activation对象hash去重、tree/map及缓存index校验流程相符。未给GET增加hash，不因rollback拒绝修改现有服务状态。

## 复用与未重跑的证据

实施者固定日志已只读核计数：`gaps-all` **84/84**（原47+当时H1c37），`gaps-h1c-final` **39/39**，`verify-original-data` **53/53**。最终脚本含86检查，但不存在本轮独立“一次86全过”的声明。608文件/89MiB、64MiB完整hash/RSS64MiB、原Probe/PrepareInterrupted等是实施者/旧报告证据，本轮没有重复这些全量检查、浏览器145或AOT；host源码未变且本轮独立HTTP/CAS/history针对差异补证。

本轮全BCL/.NET10 managed，无PK、347/19/151、Mac或kernel运行。原诊断端口18896依次使用，Integrity18894；均先确认未监听，原脚本通过自己持有的Process handle清理。独立矩阵host **pid1455408 / 127.0.0.1:42373**，finally精确Kill该子树并WaitForExit，记录exit137；结束后ss确认三个端口均无监听，无fuser-k或全局杀进程。

结果允许协调端继续安排A+B整合与部署gate，**本轮没有merge、迁移watch状态、修改持久服务或部署**。证据哈希、实际命令和harness前置错误见 `docs/web-progress-hosting-618-review-evidence.md`。
