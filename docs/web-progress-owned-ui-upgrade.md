# Reviewed UI owned hook 升级工具

**2026-11-11 独立有限 review：固定生产工具 `d9fd0605f9beb3e23630c6b54fed3642b2d842da` 为 blocked。** 显式 owned transition 在旧 hook 被外部删除后会降为 first install，真实隔离 CLI 错误退出0并修改 stub 控制状态；复现、正向范围和关闭条件见本文末尾“独立有限 review”。以下原作者报告及173/15证据原文保留，不改写为独立全部通过，也不影响已上线 UI424 或既有 A+B 接受。

本分支仅准备工具与隔离 fixture，不部署任何候选，不修改 live unit、门户或 main。起点 `1dc0afc4a644867b49204bfbc76834b910413177`；当前 live 身份及旧记录路径见 `docs/web-progress-ui-424-deployment.md`。

## 最小信任与备份边界

`ConfigureReviewedWebProgressUiHook.cs --mode install` 遇到既有 `zz-reviewed-ui.conf` 时，只允许显式 owned transition：`--previous-release`、`--previous-release-sha256`、`--previous-state-sha256` 固定旧 release 清单及旧 hook state 原始字节；`--release-sha256` 固定新清单。旧清单逐文件 SHA、只读 tools/UI/previous-static、旧 state 的 UI/review/hook hash、完整原 drop-ins hash、当前实际 hook 和有效 ExecStartPost argv 必须一致。旧记录通过只读 legacy adapter 消费，不补写任何字段，也不把事后核验声称为原始记录。

新 release 的 tools/UI 必须位于固定只读目录，work 独立；producer data 和 portal 路径保持旧 hook 身份，不涉及 host cutover。新 UI commit、review、UI hash 必须和 prepared、release 三方一致。冻结入口接受显式下一轮 UI 身份，仍只从指定 tools Git 对象提取必要源码。

回退包必须是更新前真实 served 的整个 bundle：UI、hashed assets、全部 data 闭包及 tree hash。UI424 与 pre424 的 data index 相同，不能拿旧 release 的 `previous-static` 冒充 UI424。先用 `PublishReviewedWebProgressUi.cs --mode backup --verify-served` 对每个文件 HTTPS 核字节，再以 content+metadata 容器单 rename 提交。owned install 要求这个完整且已核验的备份匹配旧 UI hash 和新 release 的 oldBundleHash；停 timer、writer idle 后再次核当前 served 整包，才原子替换 hook 文件。

完整 drop-ins、effective unit/post 和实际 timer active/enabled 状态同样先 staging+state 单 rename 提交。未知 hook、任何 drop-in/工具编辑、writer 过渡态、job/tasks、缺失备份均拒绝；不 enable/disable timer，发布失败或 published 身份不匹配不 resume。rollback 使用新 work 的更新前 bundle；restore 核 rollback 标记与真实旧包，再恢复 exact 更新前 UI424 hook 和原 timer 偏好。中断 staging/被替换 drop-ins 保留为证据。

## 验证与独立 review

操作入口为 `scripts/FreezeReviewedWebProgressUi.cs`、`scripts/PublishReviewedWebProgressUi.cs`、`scripts/ConfigureReviewedWebProgressUiHook.cs` 的 `--help`。所有路径参数相对于运行脚本目录。私有 Tailnet HTTP 校验仅该 HttpClient 使用 `UseProxy=false`，保留系统 TLS 证书校验，不改全局代理。

验证入口：`MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyReviewedWebProgressUiHook.cs`；`MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyReviewedWebProgressUiBackup.cs`。两者均无真实 systemctl/portal 操作；最终结果见下。没有 Kernel/PK/AOT/build/benchmark 验证。

## UI424 只读事后核验

2026-11-11T06:35:29Z，仅 GET 已有 HTTPS、读取文档指定 release 和 unit 文件，未执行 systemctl、portalctl、配置写入或发布。`temp_docs/owned-live-readonly.json` 记录当前旧 work 的 prepared bundle **610 files** 全部与 HTTPS 逐字节相同；这是新的事后证据，旧 release/state 没有增加字段。

| 身份 | SHA256 |
| --- | --- |
| 旧 `release.json` 原始字节 | `504cc8661339a52e9cb2a40a4ad1f88d573e2b4bc1e706105b77c199033bc660` |
| 旧 `work/hook-backup/state.json` 原始字节 | `b9e403bdc1767796766eea78dc6df5f586aeeef7066551253dd192f0425bbc2b` |
| 当前 owned hook | `029319d829485be8ee6216b46be5ae8a8b28e041b0880a70271406af8b2d7f29` |
| 当前 UI424 整包 tree hash | `ffcf2b2269f8208622da84ded3346f72f63288d63ecfc9e91bb9b2b96676a1b7` |
| UI424 UI hash | `f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88` |

整包来源是旧 release 的 `work/prepared.json` 指定的 `work/bundle-9dc8ccd57888cb4b`，**不是** `previous-static`。下次准备时仍必须重核最新 producer data/served 闭包；上表不能替代切换时校验。旧 frozen tools commit `35498ac5e72eda303883120d8b58cbb6c15fdb33`、UI/review/HTML/index 身份仍按 `docs/web-progress-ui-424-deployment.md`。

## 待独立接受后才执行的入口

下列命令仅为 review runbook，本任务没有执行。`NEW_RELEASE` 是项目根相对的新冻结目录占位符，需替换为 `../../../../.local/share/gm-kernel-progress-ui/releases/` 下实际新 release；它与旧 UI424 release 同级。先从固定 tools Git 对象 freeze，显式传 accepted UI commit/review/dist hash、当前整包 tree hash；准备时从主 Watch 的实际 `temp_docs/web-progress-publish/data` 取数据。`--tools-commit` 不能指向未固定工作树；`--ui-tree` 是独立接受后的干净 UI checkout。

新 release 的通用固定参数如下（参数值相对于 **新 release 的 tools/scripts**）：`--dist ../../ui --data ../../../../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/data --baseline-data ../../previous-static/data --work ../../work --portalctl ../../../../../../../code/my_portal/portalctl.mjs --previous-bundle ../../previous-static --ui-commit <accepted40> --dist-hash <accepted64> --review <accepted40>`。prepare 后先以相同 work/previous-bundle 执行 `--mode backup --verify-served`；install 再附 `--unit-dir ../../../../../../../.config/systemd/user --release-sha256 <new-release-json-sha256> --previous-release ../../../ui-424-tools-35498ac --previous-release-sha256 504cc8661339a52e9cb2a40a4ad1f88d573e2b4bc1e706105b77c199033bc660 --previous-state-sha256 b9e403bdc1767796766eea78dc6df5f586aeeef7066551253dd192f0425bbc2b`。

install 停 timer 后若旧 writer 尚未 idle，保留完整备份和原 exit，等待旧轮次自然结束后先评估证据；不要删 backup 再盲重试，也不要杀用户/其它 worker。失败后以新 work 回退；有有效更新前快照时，即使 publish 未写成功记录，也应执行整包 rollback 再 restore。精确顺序如下，第一次命令还会在新 work 留 `ROLLED_BACK` 阻止自动覆盖：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file NEW_RELEASE/tools/scripts/PublishReviewedWebProgressUi.cs -- --mode rollback --execute --work ../../work --portalctl ../../../../../../../code/my_portal/portalctl.mjs
MSBUILDDISABLENODEREUSE=1 dotnet run --file NEW_RELEASE/tools/scripts/ConfigureReviewedWebProgressUiHook.cs -- --mode restore --execute --work ../../work --unit-dir ../../../../../../../.config/systemd/user
```

rollback 前必须暂停该 timer、核 writer inactive/failed、dead/failed、MainPID=0、无 job/tasks；发布脚本不替代 writer 管理。两条 CLI 原 exit 必须分别留存，不用后一个成功掩盖前一个失败。只有显式 publish 成功、完整 HTTPS 校验与独立浏览器接受后，才运行同一新 release 的 Configure `--mode resume --execute`；工具再次核 publication fingerprint、served 全包、当前 hook/effective argv、完整回退备份及原 enabled 偏好。此次范围不含 zz-hosted 排序或 hostproxy 部署。

## 最终固定工具与结果

工具固定 Git SHA：`d9fd0605f9beb3e23630c6b54fed3642b2d842da`，分支 `fix/reviewed-ui-owned-upgrade`，已推送。冻结下一轮时传此值作为 `--tools-commit`；文档追加不改变该工具对象。入口仍为三份同名脚本，`--help` 可只读核参数。本轮没有创建真实下一 UI release、执行未接受 UI 或改动当前 live。

| 验证 | 原 exit / 结果 | 证据 |
| --- | --- | --- |
| 完整 hook/upgrade fixture | 0；173/173 | `temp_docs/owned-upgrade-reviewed-rerun.log` |
| 备份 fixture | 0；15/15 | `temp_docs/owned-backup-reviewed.log` |
| 固定最终 Git 工具 freeze→prepare | 0 / 0；7 源码逐字节等于固定 Git 对象，无 published.json | `temp_docs/fixed-tools-smoke.log`、`temp_docs/fixed-tools-freeze.log`、`temp_docs/fixed-tools-prepare.log` |
| live 只读整包身份 | 0；610 files exact HTTPS | `temp_docs/owned-live-readonly.log`、`temp_docs/owned-live-readonly.json` |

173 项包含原 31 项、原检查数断言、active/enabled、inactive/disabled、inactive/enabled 的 owned upgrade→publicationmatch→resume→rollback→restore，坏 publication、完整备份才 skip/rollback/resume、外部编辑/未知 record/坏 pin/工具修改/有效 argv 修改/queued job/activating/tasks、source symlink/overlap、缺 metadata/asset/错误 UI 同 index、切换前及 HTTPS 校验期间被外部修改、完整 hook 备份和中断 staging 保留，以及真实冻结 CLI 的固定源码/独立 prepare/覆盖与 source drift 拒绝。fixture 的 UI424/下一 UI 是合成标记数据，旧六工具来自真实 `35498ac` Git 对象；真实 live UI 身份另由上面的只读核验支持，不把 fixture 宣称为实际发布。

系统调用仅子进程有限 PATH 下的 BCL stub；publication 仅 loopback HTTP 与 stub portal。只有临时工具副本替换 HTTP origin，生产脚本没有 endpoint override/fixture 后门。固定最终工具的额外冻结冒烟为 `temp_docs/publishing-integration/ui-hook-c73ab32f25d440baae724facd78b082b/frozen-final-tools`，清单 SHA `4cf969ca23cdc0d9dfc4e0e7124df94fb1410724844b0c6db034f35c16789cf9`；它只是合成 UI 的只读测试产物，不能用作实际 UI 发布候选。

预期拒绝 CLI 原 exit134 保留在每条 case log；故意让 portal stub 返回的原 exit23 保留在 `temp_docs/publishing-integration/ui-hook-c73ab32f25d440baae724facd78b082b/active-publish-fail.log` 等（外层 CLI exit134），没有成功 publication 记录。完整 case 日志在 `temp_docs/publishing-integration/ui-hook-c73ab32f25d440baae724facd78b082b`；备份日志在 `temp_docs/publishing-integration/ui-backup-9c400dde9e094db39f8c9233e571457a`。首次扩展 harness 的 stub 编译曾 exit1、外层 exit134（JsonNode.Remove 使用错误），修正后完整重跑；失败原日志仍在 `temp_docs/owned-upgrade-reviewed.log` 及 `temp_docs/publishing-integration/ui-hook-8a554ffd2ab64dc284603a6eafdfe9f3/stub-build.log`，未伪记首轮全过。

`--verify-served` 必须在新 work 的首次 backup 使用；已有未核验/legacy metadata 明确拒绝原地补 `servedVerified`，要求新 work，不发网络请求也不改旧记录。后续已核验备份可以只读重核。新发布前仍需独立接受实际 UI、核新 manifest pin、按上述 writer-idle/runbook 执行；本任务不代替这个实际发布决策。

## 独立有限 review（固定 d9，2026-11-11）

**结论：blocked，尚不能授权用本固定工具执行下一次 owned UI-only 升级。** 唯一独立新阻断为 U1：明确携带 previous-release pins 的 owned upgrade，在旧 owned hook 缺失时静默切成 first install。其余已检查的 pin、整包备份、拒绝、恢复和固定 Git 冻结路径有下列正向证据；这些通过不能抵消 U1。没有扩成 hosted/proxy 迁移 review，也没有撤销原 1dc 整合代码或当前 UI424 的接受。

### 固定对象与证据来源

review 在独立 `gm-publishing-integration-review` 目录的新分支 `ThraceShah/gm-owned-ui-d9-review` 完成。起点为文档对象 `de87c8b68b9f31e5359919116288b203b95ab01b`，fetch 后 origin/fix/reviewed-ui-owned-upgrade 实际指向该对象；它和生产工具对象 `d9fd0605f9beb3e23630c6b54fed3642b2d842da` 的 scripts **无差异**。没有追后续修复工作树，也没有改生产、正式 fixture、旧 data 或 live。

作者证据只读引用，不重复全套173/15：以下路径前缀为 `../gm-reviewed-ui-upgrade/temp_docs/`。

| 已读原证据 | 原结果 / SHA256 |
| --- | --- |
| `owned-upgrade-reviewed-rerun.log` | 173/173，原exit0；`048174e8d9213af890f96260f2b6beaad7fa8e6ad0a7ffbfad589d7370813fb0` |
| `owned-backup-reviewed.log` | 15/15，原exit0；`dd14e83839946d239bf839b0f198d9deb9b4215d275853124ff01c76c5361a38` |
| `fixed-tools-smoke.log` | 固定d9 freeze/prepare=0/0，7源码匹配、无publication；`60286ba5fc1e243c7773c14b78f0d087924efa2338d489fa93f7c0ac5172e109` |
| `owned-live-readonly.json` | 作者610文件逐条HTTPS验证；`e1500a56b8c0bdea43a75499abbf462d3a1a353a6b70c5dc17e75d49f7fb9626` |

独立 repro 在 `docs/owned-ui-d9-review/`，README 给出实际运行命令。除明确将 fixture helper 的私有 HTTPS origin 替成 loopback 外，运行代码取自固定 Git 对象；生产没有增加 endpoint override。旧六工具直接取真实354对象。所有操作脚本为.NET10 C#，dotnet build/run 子进程设置 `MSBUILDDISABLENODEREUSE=1`；仅一次私有 BCL stub 编译及小 fixture CLI，无173/15重跑、PK、AOT、collector、浏览器、性能或平台计算测试。

### U1：外部删除 owned hook 后忽略显式升级边界

`scripts/ConfigureReviewedWebProgressUiHook.cs:161` 用 `File.Exists(dropins/zz-reviewed-ui.conf)` 决定 `ownedUpgrade`；全部 previous release/state pins、实际旧工具身份、完整 drop-ins/effective argv、新 frozen release pin、current-before-upgrade verified bundle 的严格验证都在后续 `if (ownedUpgrade)` 内。调用者即使明确给出 `--previous-release`、`--previous-release-sha256`、`--previous-state-sha256`，旧文件缺失也会绕过该分支，进入原首次安装路径。

`MissingOwnedProbe.cs` 复制一份已经由独立正向测试通过的私有 fixture，重新固定 relocation 所需的 hook/state pin；实际独立检查两代 frozen 文件集合/逐文件hash、旧新 producer/portal/unit 路径一致，再**仅删除原 owned hook**。三个 previous 参数和真实匹配的 pin 仍全部提供。systemctl stub 在 unlink 后保留原 loaded effective argv，模拟文件外部删除但未 daemon-reload 的状态；新 hook 写入后按实际文件返回新 argv，没有把文件缺失模拟成 stub 自己崩溃。该反例不需要 HTTP 或真正服务管理器。

最终证据：`temp_docs/owned-ui-d9-review/missing-owned-afcccdbee8354d599aa007ee9e788dee/`，`receipt.json` 实际为：

```json
{"cliExit":0,"setupPinsAndPathIdentityValid":true,"newHookExists":true,"recordedOwnedUpgrade":false,"timerActiveAfter":false,"counterexample":true}
```

真实 CLI 输出 `UI_HOOK_INSTALLED_TIMER_STOPPED`；stub 调用序列含 `stop gm-kernel-progress-refresh.timer`、writer show、`daemon-reload` 和新 ExecStartPost show。完整 `hook-backup/state.json` 已创建并错误记录 `ownedUpgrade=false`，原来缺失的 hook 被重建。预期是把原 owned hook 的外部删除视为所有权丢失并拒绝；因此这不是良性 help/no-op，也不是旧 pin 不匹配导致的正确拒绝。最初确认目录 `missing-owned-7be1d950cee2400388dfec46408d1a37/` 同样实际CLI0；补充输入一致性断言后的最终反例没有改变产品代码。

**最小关闭职责：** 明确区分显式 owned transition 和真正首次安装；任一 previous-owned 参数或明确升级模式出现时，旧 hook 缺失必须在 backup/control/hook 修改前拒绝，不得根据文件是否存在隐式改模式。保留真正首次安装的既有兼容性。用上述原反例重新验证内部 CLI 非零、缺失 hook 状态及全部其它 drop-ins/timer 偏好保持不变、无 stop/reload/新 hook-backup；外来/已编辑 hook、其它 drop-in、原正常 owned upgrade 与恢复继续成立。不要通过改正式期待、去掉 previous 参数、预先补回 hook 或修改 live 来关闭本项。

### 独立有限通过范围

| 控制 | 真实结果与边界 |
| --- | --- |
| `OwnedProbe.cs` | **22/22、外层0**；fixture `temp_docs/owned-ui-d9-review/ee5bdd5ec81f4be4baca876eb3af711f/` |
| 受控拒绝 | 错 previous pin、外来 sibling drop-in、有效 argv 不符、缺 backup metadata、同index错误UI且**重新计算正确tree metadata**，真实CLI均134；全drop-ins字节不变，无stop/start/reload、无新hook-backup。lease/log等私有诊断文件不被冒称绝对零文件I/O |
| writer / 部分中断 | queued job真实134，旧hook不变、完整backup已原子提交、stub timer停；abandoned staging保留。失败整代保留为 `next-queued-evidence`，另用 pristine fixture 开始独立成功场景；不是运维删除backup重试教程 |
| 上传与校验失败 | portal stub原 **23**，外层CLI **134**，明确保留 `original exit=23`、无published；portal0但served JS错误同样CLI134、无成功记录；恢复正确整包后publish CLI0 |
| resume / rollback / restore | 无publication及错误review均134且不resume；匹配后resume0恢复真实原active/enabled。缺rollback metadata为134且不写ROLLED_BACK；完整rollback0恢复 **UI424** 整包，非同index的pre424。外部restore edit拒绝且原字节保留，随后正确restore0恢复全部drop-ins与原active；没有enable/disable调用 |
| 原旧记录保真 | 旧release、六个真实旧工具、state全树字节未改；没有补写ownedUpgrade/previousBundleHash/servedVerified到原legacy记录 |
| `FreezeProbe.cs` | **3/3、外层0**；真实freeze→prepare **0/0**，7个源码逐字节等于固定d9 Git对象。故意修改私有editable输入的Publish工具也没有进入冻结产物；dirty UI source另以CLI134在创建release前拒绝，无published记录 |

freeze fixture 为 `temp_docs/owned-ui-d9-review/freeze-4b566c491e1b432dbadc7dd3e7c3e023/`，synthetic UI commit=`8a80f0f033e1e80ce5a7f5361627f7de54257bc9`、release manifest hash=`c356de492cff6e05d1824b9c15571b3941209303163a68ec5bb7b81680e5e2cf`。它仅是 BCL 测试数据，不能用作实际新 UI 发布候选。

writer tuple 的完整 Active/SubState/MainPID/Job/TasksCurrent/ControlGroup 拒绝条件已逐项读源码，原173中的 running/transitional/tasks/empty-group 等记录继续作为作者证据；本轮独立只选 queued 及恢复路径，不把未重跑的全部tuple组合声称为新独立全过。备份内容和metadata同目录staging后rename；hook完整备份也先state+drop-ins同目录rename，writer idle后核旧整包并重核drop-ins/effective argv，再单文件rename；上述正常路径的设计与实测相符。`UseProxy=false` 仅位于私有门户HTTP helper，未设置证书绕过、降TLS或全局代理。

主 loopback **38865** 在 finally 关闭；前置诊断 loopback38845也已关闭。未使用fuser/pkill或操作其它worker。缺hook反例仅调用独立编译的BCL systemctl stub，不使用loopback或真实portal；所有CLI均已结束。

### 当前 UI424 的独立只读核实

`LegacyReadProbe.cs` **20/20、exit0**，`2026-11-11T07:08:26Z` 收据为 `temp_docs/owned-ui-d9-review/legacy-87b3380f453d463f9fe128acc7508bf3/receipt.json`。实际旧 manifest pin=`504cc8661339a52e9cb2a40a4ad1f88d573e2b4bc1e706105b77c199033bc660`、旧 state pin=`b9e403bdc1767796766eea78dc6df5f586aeeef7066551253dd192f0425bbc2b`；所有manifest文件匹配、tools/UI/previous-static只读，6旧源码逐个等于真实354 Git对象。

完整当前 drop-ins 恰是原备份加旧 owned hook：`zz-reviewed-ui.conf`=`029319d829485be8ee6216b46be5ae8a8b28e041b0880a70271406af8b2d7f29`，`publish.conf`=`8b2e7864e694e50b82fe39378065043de8261fc9bea0e3bb8632b18baf119898`。真实历史保存的 `effective-post-after.txt` 单个 argv 与当前原字节hook精确相等；本轮**没有新查询 live manager**，不能把历史dump冒称新systemctl运行。实际切换时的fresh effective check仍是后续操作必要门。

旧work指定的当前prepared bundle为610文件，tree=`ffcf2b2269f8208622da84ded3346f72f63288d63ecfc9e91bb9b2b96676a1b7`，其UI hash与固定424/review匹配。只发3次HTTPS GET，均200且逐字节SHA匹配：HTML=`a779f1d695dcee897f1cf6726c95a57b8804399b4ea14ecc89b7c2f31b80fe01`（855 bytes），index=`117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`（854），JS=`9d469d1e0b2d3b5915dc3bac688088987e93d8e8c16ee63c0cdbaf0e02c5cfcf`（824945）。本轮没有再抓全站610；作者610逐GET证据仅按上表引用。

### 证据哈希、前置错误与交接

生产源SHA256：Configure=`3998b8204102dba621a9a5716cfeee6c6f621afda8ae145faff978ccc42ed0b4`；Publish=`2d5efee97dacc6e1f2dca4bea087957ed97aff1cd0ca4de44608b6bfce6d675e`；Release helper=`45066e44d3b497bdfdea9f605bce3fa7d68f6db7f1a53a710d84bd31203b060e`；Freeze=`c6c00b10c19ec9d27a429338b46b51164acd7d870d49202ed279492517ad0a96`。固定源码完整hash及各CLI原exit/loopback HTTP length/hash均留在fixture；没有凭证或厂商库。

| 本轮文件（前缀 `temp_docs/owned-ui-d9-review/`） | SHA256 |
| --- | --- |
| `probe.log` | `634d70e14dcabbc5d4738e4853a0ad79ae04b9af290ecc4377c759f1d413ecdf` |
| `legacy.log` | `bfe711ef4f2fc7fba836113acf9fe0f64b017d4a5023fa529eb19344348908b5` |
| `freeze.log` | `4377159ec592bf2fba4ef08af396b8358f0f6b86dd2f6be8cb682e3ec379c394` |
| `missing-owned.log` | `a820e4ebe76e4db024a7039548488fa54c2f16144ec91bce6cd488495c12e7ba` |
| `missing-owned-afcccdbee8354d599aa007ee9e788dee/receipt.json` | `870dc55a3a6effa735e30d393a791e2c0a9035a3c4d88725da7e9f8e17579eb0` |
| `ee5bdd5ec81f4be4baca876eb3af711f/http.json` | `ec9ed5287bdf2d3e0597025dbda79b32dc06a0171da9864a32931a7c8933928e` |

持久repro SHA：Owned=`b10ab58b03fe235c5824d003d3958a083d5f91f94fc557b919137f3409210745`；LegacyRead=`2b92d77aab7cbbe9c445e8b400cfb5a201b4ecc16e894f98f2b883d2a411d185`；Freeze=`0951cee87bfa3515406dff1633bead19e0a7d118575e58c38cbae8c2771dba87`；MissingOwned=`26d1039ce7322273ae5102a00810bd9a21712e71cf2eaad37dbd984336d2d646`。反例诊断外层0代表成功复现缺口，**内部install0是错误行为**，不可当作工具验收通过。

reviewer前置错误完整保留：Owned初次在负例恢复时因File.Copy保留只读mode导致自己恢复写失败（exit134），finally已关闭自己的listener；改为只对自己的backup文件恢复写权限后新fixture22/22。MissingOwned初次漏Source helper编译exit1；下一次stub在unlink后自己FileNotFound导致外层134，未记为产品正确拒绝；补充真实loaded-command语义后独立CLI0稳定复现，再加完整输入断言仍CLI0。没有改产品或期待来抹平差异，NU1903/平台分析警告保留，不宣称AOT/Windows认证。

本报告与repro为docs-only小步提交；第一步 `41662b094e0e7f01c2d2ae7516189a392c768cf4` 已推送并核remote SHA，最终报告随后推同分支。已向协调端立即报告U1；后续只需最小实现修复与独立复验U1及受影响正常/拒绝路径，实际下一UI的截图布局/accepted commit/review/dist hash、fresh served整包、writer idle、真正发布与浏览器门另行安排。当前不执行新UI发布、unit install/stop/resume、portal/slug、host/proxy或main修改；未来hosted旧hook冲突和整包运维rollback gate仍另案保留。
