# Reviewed UI owned hook 升级工具

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
