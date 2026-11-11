# 固定 1dc scheduler + host 独立整合 review

**代码接口结论：accepted。** 接受 `1dc0afc4a644867b49204bfbc76834b910413177` 中 A+B 的隔离整合、props 合并、发布闭包与 host generation 接口及本报告验证的增量保护；没有发现该范围的新阻断。**部署结论：blocked / 未执行**，`WebProgressHostedCutover.cs` 仍有下述具体迁移阻断，不能把本结论解释为其 check/deploy/rollback 已接受。

UI424 已于 `2026-11-11T04:47:44Z` 独立上线，仍是静态门户；这不是 A+B 上线。原 A/B 接受保留，不重置旧结论、不重新审完 S/H 历史，也不声明平台计算能力、全 collector 或性能工作完成。

## 固定对象与源码归属

本工作树从固定 1dc 新建 `ThraceShah/gm-publishing-integration-1dc-review`。安全 fetch 后，origin/main 仍为 `610e3ce844bf99c85af6069905f9f3deaab08133`，origin/feat/web-progress-publishing-integration 为完整 1dc。下列远程 review 分支与对象均实际核实，报告不是根据短 SHA 猜测：

| 输入 | accepted head / 报告对象 |
| --- | --- |
| A scheduler | `5ae2cb53573dda56533b3ddb7ec46b5fc878edd0`；origin/ThraceShah/gm-scheduling-5ae-review=`dce5a3b29c0a4b0ccce5952f8b8c057ae873df91`，已读其 `docs/web-progress-scheduling-5ae-independent-review.md` |
| B hosting | `618144f1381cc14d98cc0c9dd582e52fc4b81a09`；origin/ThraceShah/gm-hosting-618-review=`548a1198583daa3b26a37fc1fa5c8b81ae6b2888`，已读其独立报告、evidence 和 closure probe |
| 共同 base | `610e3ce844bf99c85af6069905f9f3deaab08133`；A 相对它 11 个文件，B 15 个文件 |
| 两次 merge | `cbb78082d88655498619d0f6ffd99c2de979f2cf`、`242ac1e35986081b83bca7f2d097b1014356418a`；均保留父历史 |

第一 merge 的 tree 与 A 完全相同：`d1d1d44331c8ec64a52a7e573325a9963b2cf066`。第二 merge 唯一冲突是 `scripts/Directory.Build.props`，没有用另一支的旧实现覆盖 accepted helper。

| 固定 1dc 的路径 | 实际来源 / 本轮判断 |
| --- | --- |
| `scripts/WebProgressScheduler.cs`、`scripts/WatchWebProgress.cs` | 来自 A；后续只增加状态 tmp 链、写根和根重叠保护，未重写已接受的 queue/provenance/退役周期逻辑 |
| `scripts/WebProgressPublishing.cs`、`scripts/WebProgressDataValidation.cs` | 来自 A；补目的既存树/index.tmp 预检、source/dest 分离及 dangling link 拒绝；验证规则没有放宽 |
| `scripts/RefreshWebProgress.cs` | main 原入口加写路径保护；collector 执行和数值门槛未改 |
| `scripts/HostedWebProgressSite.cs` | 来自 B；整合增量为独立 mutation roots、镜像异类状态拒绝、一次 exact index 快照、对象 hash/write 同一 byte[]、forward 提交前完整 generation 验证和 authority schema1 |
| `scripts/ActivateHostedWebProgressSite.cs`、`scripts/PrepareHostedWebProgressSite.cs`、`web/host/` 四个 `.cs` | 与 accepted B **逐对象相同**；serving 的 O_NOFOLLOW、post-open 祖先检查、retained UI/data 路由未回退 |
| `scripts/PublishHostedWebProgressSite.cs` | 新本地串行 post hook；校验闭包，pin index，Prepare/Activate；不含 collector 或 portal 调用 |
| `scripts/Directory.Build.props` | validation OR scheduler 共用 validator/publishing/metadata，一组独立 scheduler include，一组独立 host include |

实际 `dotnet msbuild scripts/Directory.Build.props -getItem:Compile` 同时设置三个 Use 属性，得到恰好 **5 个互异 include**：validator、publishing、metadata、scheduler、host；见 `temp_docs/publishing-1dc-review/props-items.json`。真实各 CLI 又分别用 `dotnet run --no-cache --file` 编译执行；没有靠一个合并 harness 的缓存代替入口验证。

整合相关生产改动集中在 `c60ad196b324ef1af7204552c6cfd7ac7798dede`；后续 UI-only 工具及冻结发布记录是另一条接受边界。本文不重审或重新执行已发布 UI 工具 354 的操作。

## 按差异选择的独立 BCL 实测

SDK `10.0.302`、runtime `10.0.10`，16 逻辑 CPU；开始时约 21 GiB available、磁盘约 1.2 TiB 可用。BCL file-app 和 managed host 编译实际可用，本轮不需要 PK/private env；没有把缺私有环境当建模失败。所有 dotnet 子进程设置 `MSBUILDDISABLENODEREUSE=1`；没有 dotnet test/testhost、AOT、PK、全 347 collector、86/145 全套或浏览器/性能/Mac/Windows 测试。

持久入口在 `docs/publishing-1dc-review/`，复现命令见其 README。主探针完全通过真实 CLI 操作，不调用被审 Activate/closure/rollback helper；nested Git producer 仅含复制的固定脚本与明确标注 BCL 的 capture，空 PKToy Git 目录仅提供 provenance identity，绝无厂商库、凭证或伪造真实几何结果。

主 fixture：`temp_docs/publishing-1dc-review/f4556c56f3e940a9aebcef482054a5c5/`。producer 固定 commit=`2a168369e473b769f0edcd58890214959e87a58d`，run=`20261111T050651Z-2a168369-b000`。44 项主探针在 source scripts、原 run 全部文件、queue 与 outside sentinel 的前后 hash/bytes 相等断言后结束。

| 实际操作 | 原 exit 与断言 |
| --- | --- |
| Watch→真实 Refresh→受控 capture | capture 原 **7**；manifest failure 与 `2.01065554e-7` 精确保留，未升级成 pass；阈值仍 `1e-7` |
| 目的 index.tmp 链接导致首次发布失败→移除自己的链接→再 Watch | **2 / 0**；失败 pending/reason/time 落盘，恢复清 pending；第二次没有 BATCH 或 collector 重跑，原 run 不变 |
| 公共 Publish CLI | **0**；indexed aggregate 与原 run、跨 run mesh、raw log 进入完整闭包；坏而未 indexed 的 raw manifest 保留为对象，不被当作 indexed 定义递归解析 |
| Prepare / Activate 独立 runfile | **0 / 0**；schema1 authority 的 dataSha 对应源 descriptor commit，generation index 与所有导入文件逐字节经真实 host GET 对照 |
| 后续 cache index 和 raw log 改写 | active HTTP index/log 仍为原字节；恢复 fixture 后继续操作；source cache 没有成为 serving root |
| scheduler queue / authority 误作 mirror | **134 / 134**，分别明确 `HOST_MIRROR_SCHEMA_INVALID` / `HOST_PATH_OVERLAP`；queue 原文不变 |
| 缺跨 run mesh / dangling site 目的链接 | **134 / 134**；前者不改变已发布文件，后者在创建外部目的目录前明确拒绝 |
| 新 UI + 新 generation，镜像写目标为目录 | **0** 并明确 `ACTIVATION_STATE_MIRROR_FAILED`；authority 成功提交，不能记作镜像也成功 |
| 同 pair 重试修复正常 mirror | **0**、`ACTIVATION_STATE_REPAIRED`；authority 全文字节不变，mirror previous 完整一致、count=2，无额外提交 |
| 删除旧目标 metadata 的 raw run 映射后真实 rollback | **3**，定位 `raw: required run mapping missing`；site.json 全文（含 count/previous）、mirror 全文、当前 index/UI HTTP 全部精确不变 |
| 还原自己的目标后合法 rollback | **0**；恢复原 exact index/UI；新旧 hashed assets 仍可 GET |
| 串行本地 hook success→坏源 failure→恢复 | **0 / 3 / 0**；失败保留 authority 原文，恢复 skip-same、不 recommit、不跑 collector |

主探针 **44/44、外层 exit0**，`probe.log`。其中 134 是真实未捕获 InvalidDataException/IOException 的进程退出，不改写为 3。host **pid1554767 / loopback45389** 在 finally 精确 Kill/Wait，exit137；之后该端口无监听。

额外只补两个小差异检查，未重跑整套：

- `RetainedProbe.cs`：新 successor index 只注册 successor，其 active generation 的 runs 映射**不含旧 aggregate**；真实 Activate exit0 后，current index、旧 aggregate manifest、新 successor manifest、跨 run raw log 四个 GET 全部200且字节相等，**5/5、外层0**。这才是本轮跨代旧引用保留证据，未把“仍在 active 映射中的旧 run”冒充 retained 测试。host **pid1556949 / loopback40831** 精确清理并 wait，exit137，端口无监听。
- 临时 `BoundaryProbe.cs`：queue.tmp 链接被 Watch **2** 拒绝；直接 Refresh 的 runs 祖先链接 **134 / REFRESH_PATH_UNSAFE**；目的已存树内链接被公共 Publish **134** 拒绝。source/data、queue、publish 全树及 outside 文件前后不变，**4/4、外层0**；没有启动 host/collector。脚本及日志保留在 `temp_docs/publishing-1dc-review/`，目的目录为主 fixture 的 `boundary-fdb4a6b862e64cf5856c6312ccb46d60/`。

这些结果覆盖新增写根/删写目的预检；serving NoFollow 源与 B 相同，本轮不引入多租户隔离或每 GET 全 generation hash 新要求。source 快照以单一串行 producer/post hook 为合同，不把本结果扩成同用户恶意文件系统竞争的隔离保证。

## 原作者 gate 的引用边界

已读 `docs/web-progress-publishing-integration.md`，并只读核对作者原日志，**不是本 reviewer 重跑或独立证明全部检查**：

| 原日志（前缀 `../gm-publishing-integration-feature/temp_docs/publishing-integration/`） | 原结果 / SHA256 |
| --- | --- |
| `integration-final.log` | 75/75；`ca7702986d5890061ae73051fe93938329819b43316df58b176f4d8fff350b5b` |
| `hosting-gaps.log` | 86/86；`7510727fbf404f77209db9b1765c2a66b0a2a5cc952b62e38f96cefedd3296f0` |
| `final-source-artifact-hashes.json` | `c05e3290441a77ac5149fb6ed00bdbd5ddf097c67b4b1220ec3f7e9bbaf96e69` |

旧 scheduler/host 接受报告、原 gate、旧工作树及 ignored data 全部保留。没有改实现树、正式 tests/期待、registry、数值门槛、旧 data 或其它 worker 的状态。

## 线上现状的只读核实

开始时只读状态：timer=`active/waiting + enabled`；refresh=`inactive/dead, MainPID0, ExecMainStatus0`；host unit=`not-found/inactive`。资源充足不等于 347 全量已完成。

`2026-11-11T05:13:03Z` 的 `read-only-snapshot.json` 再核 dedicated producer clean main610、frozen tools=`35498ac5e72eda303883120d8b58cbb6c15fdb33`、UI=`424e85d6aac32c2afe7cad12cfd04bea4f0037ab`、UI hash=`f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88`、独立 UI review=`f1fdc36553e9132c88d78a933e503160950460f2`。只发出3个 HTTPS GET：

| 当前静态门户资源 | HTTP / bytes / SHA256 |
| --- | --- |
| HTML | 200 / 855 / `a779f1d695dcee897f1cf6726c95a57b8804399b4ea14ecc89b7c2f31b80fe01` |
| data/index.json | 200 / 854 / `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990` |
| assets/index-DQUBkM3M.js | 200 / 824945 / `9d469d1e0b2d3b5915dc3bac688088987e93d8e8c16ee63c0cdbaf0e02c5cfcf` |

与已发布 UI424 回执一致，没有扩大 live 抓取。463 registrations / 17 attempted（14 pass、3 fail）仍是既有线上记录，本轮没有新增几何观察或全量 collector。未执行 publish、portalctl、unit/drop-in 写入、timer start/stop、proxy/slug、main merge、live rollback 或新部署；离线 parser 不连接服务管理器。

## 后续迁移阻断与可执行交接

以下属于**部署 helper 未接受**，不撤销 A/B 或上述隔离接口接受。两项精确反例已报协调端，协调端确认未来最小修复职责，本轮不修改该 helper 或 live。

**G1：当前 reviewed UI hook 与 hosted hook 的 effective command 冲突。** 当前用户目录相对本根为 `../../../../.config/systemd/user/`；其 `gm-kernel-progress-refresh.service.d/zz-reviewed-ui.conf` 含空 `ExecStartPost=` 再指定冻结 static UI424 hook。`scripts/WebProgressHostedCutover.cs:154` 新建的 `zz-hosted-activation.conf` 排在它之前，后者再次 reset 会移除 hosted activation，下一轮仍可静态发布覆盖 proxy。

持久 `HookPrecedenceProbe.cs` 将当前 reviewed drop-in **原字节复制**到自己的新 root，加入候选 hosted drop-in；只运行 `systemd-analyze --root=<fixture> --man=no verify`，debug 输出为真实 systemd 离线 parser 的有效配置。原 exit0，最终 **恰一个 ExecStartPost，仍是 `PublishReviewedWebProgressUi.cs --mode publish --execute`**，不是 hosted hook。证据目录 `temp_docs/publishing-1dc-review/unit-25d4a9ad1ff34d439dcea619a1baf91e/`，见 `effective-post.txt`；原 live drop-in hash/bytes 未改。

后续修复责任：备份并 hash 所有原 drop-ins，明确识别本 frozen release 拥有的 `zz-reviewed-ui.conf`；只有原字节身份仍匹配才显式移除或禁用它，任何外部编辑必须拒绝。安装 hosted hook 后验 effective 命令恰一个 hosted activation，无 static portalctl 链。**只把新文件改成更晚的名字不能关闭此 gate。** 修复先在受控 unit fixture 验证，再进入另行授权的 live gate。

**G2：rollback baseline 不能由相同 data index 推导 UI 身份。** `scripts/WebProgressHostedCutover.cs:117` 只比备份 index 与在线 index。当前 frozen release 的 `previous-static/data/index.json` 也是同一个 `117bb8e7...`，但 `previous-static/index.html` SHA=`ca0e7d8b79e3e21f019385a41dd8ee8cea25bc42a13f8ec304e2847a2b8c5ade`，与当前 UI424 HTML `a779f1d6...` 不同。因此 pre424 bundle 会通过其 index equality gate，却恢复错误 UI。

后续修复责任：捕获并验证**迁移前当前 served UI424 的完整 static bundle**，包括 HTML、全部 hashed assets、data 实际闭包与 whole-tree hash；保存 token-independent UI hash、424/review 全 SHA、owned hook/drop-ins 原字节身份及外部 edit 拒绝规则。原 UI 发布的 `previous-static` 仅是 pre424 回退证据，不能直接当 host 迁移前 baseline。host Prepare/Release 必须显式消费已接受 frozen UI424 dist/hash，或另一固定已接受的新 UI；当前 cutover 默认从 publisher 的 `web` 重建，不能确保 UI pin。两项修复必须配合，避免新 unit 与旧 UI pin 互相覆盖。

余下部署顺序仍须独立验收，当前不可直接运行候选 cutover：

1. 在 dedicated clean checkout 准备后续实际 accepted main 全 SHA；先用复制的旧 fingerprint/queue、17 条真实来源记录做状态迁移演练，备份原文、保留原 revision/checkVersion/host/数值与 raw logs，不把旧观测当新 pass。scheduler queue/publish cache/host site/authority mirror 四类状态设独立根，typed state 不互相覆盖；本轮不移动 live producer。
2. 完成 G1/G2 最小修复及独立负控后，核旧 timer active/enabled、所有 drop-ins、旧 host/unit/proxy 状态和当前 UI424 全 bundle。写入前停止 timer 并确认 writer 真正 idle：Active/SubState、MainPID、Job、TasksCurrent 都满足，处理 activating/start-post/queued writer；候选当前单看 is-active 的 precheck 不能替代这个 gate。
3. 后续获授权窗口才安装/激活 host，核独立端口、loopback/Tailnet、同 `gm-kernel-progress` slug 的 HTTP index/UI/真实 mesh/log exact bytes、桌面/手机与服务重启、原 timer 恢复后实际一轮只有 hosted hook。host 保留完整容量供后续347；不要求为本轮验收先跑全 collector。
4. 在受控演练及后续真实窗口验证健康失败回滚：恢复**迁移前当前 UI424** exact bundle、reviewed hook/drop-ins、旧 host/proxy 和 timer active/enabled，保外部编辑拒绝；再次对 UI、hashed assets、data closure/treehash 和恢复后一轮做字节/命令对照。host generation rollback 的本轮成功不能替代同 slug 运维回滚。

## 证据索引与保留的前置失败

日志前缀 `temp_docs/publishing-1dc-review/`。外层最终 exit 均0；各 CLI 原 exit 见主表及 fixture 中逐操作日志，HTTP 的 status/length/SHA 全记录在主 fixture 的 `http.json` 与 `retained-150a4c1650ee4f8899d3b8cc25a2a22e/http.json`。没有重跑作者75/86来冒充独立 review。

| 文件 | SHA256 |
| --- | --- |
| `probe.log`（44项） | `2d43e234bf2c026487f6b392d492c66ed8fdca501834a65a4f14abf71d4cbe0b` |
| `retained.log`（5项） | `6d6d97454910ec22d779a7cda40a3a237594ee61218047d87b0810ca03a713ba` |
| `boundary.log`（4项） | `6f2a545025d0a9b9f9604fd33fd7a850b3b3fe95138448d18f895a2a582369a6` |
| `BoundaryProbe.cs`（实际执行的临时源） | `4b230129f5e66177d7f98c45cfcf2b7f38a95ec76e04b5812306384165795289` |
| `hook-precedence.log` | `82bffb1436570dae8e3c7b7a2663a1f9778bca691477c784911474665102b6a3` |
| `read-only-snapshot.json` | `e17d5636c1441646b948fd89c00515a050cc04e012c567059e3e1d0cfd81b928` |
| `final-source-artifact.sha256`（完整源码/构建 hash 清单） | `b54ce24125122dc5735a5c97d4c069d3d2a1b3aa8a0f7bc28563635c6e8594b1` |

关键实测源码：props=`ce1038aa1c8719472762cbfd1bea346d36c301627fcaa109bbd503d38bb2469a`；Watch=`b2412b11e01d9cad9e2d8e73f075b041e615b2d014e8abeb2ac4ebdd9f0b9750`；Publishing=`73b049a6155e9dd912cfaa1770a3142440d689acdd47ce97b88e55c67e921b04`；Hosted helper=`7774b3718204102ec643121eafad13a3539405dcf8017c9926af2478a8cb627d`；本地 hook=`93d73f2557c1a87abf0d76d9f9cd8725b8b768c1c715cc66c504473f97a7aee9`；host DLL=`00815518ca2b707c6a0e7b3f0399e5f0655584c6772630578162c62de186893a`。

持久主 probe hash=`5ce076df2631dba4107f9f250e1c0ea034d02c5c61aa428fd050c4cf320a85f7`；retained=`f794683f820f0ad4a1ed0e125aa4e1a31a863fa2491e0ce413fc7972230400d5`；offline hook=`1b8aa09b0b0ea5e4635bbc2a86a6bff11b75e970066c0a60e348931591854ca0`。三份都以实际持久位置执行，非事后修改期待的副本。

reviewer 自己的前置错误未掩盖：主 probe 首次 CS4007 编译失败（exit1）；随后一次诊断在恢复 cache 时误写 source index 而非原 exact published bytes，导致预期“不变”断言失败（exit134），自有 host pid1553390/43393 已 finally 清理，未发现产品反例；修正 fixture 的恢复为原 byte[] 后在全新目录得到44/44。offline parser 第一次实际 verify 已exit0，但提取器误认 ASCII 箭头，诊断exit134；改为提取真实 `ExecStartPost:` 段后新 root 成功。snapshot 临时脚本也保留一份编译失败日志。所有原日志保留，不归因内核/几何或抹成首次全过。

SDK 原 NU1903/trim/AOT 分析警告保留；不宣称本轮完成 AOT 或跨平台认证。全部本轮进程已结束，只清理自己持有的 host process；没有 fuser-k、全局 pkill、其它 worker/端口清理。review 分支仅新增 docs/repro，第一小步 `ead9f90280503e89a834dcd87049c9b372a6ea4a` 已推送并核 remote SHA；最终报告随其后提交同分支，协调端按最终 remote HEAD 取审。
