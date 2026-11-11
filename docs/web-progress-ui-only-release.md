# 独立 UI 静态发布切片（只准备，尚未发布）

固定 UI 源为 `424e85d6aac32c2afe7cad12cfd04bea4f0037ab`，独立 UI 接受报告为 `f1fdc36553e9132c88d78a933e503160950460f2` / `origin/ThraceShah/gm-ui-424-review`；此接受不代替发布脚本/运维复核。该切片不依赖 A+B host/scheduler 迁移、ledger 或内核完成；数据生产者继续使用 main `610e3ce844bf99c85af6069905f9f3deaab08133` 与当前 17 representative 的真实历史结果，不重新运行 PK。

`scripts/PublishReviewedWebProgressUi.cs` 默认 `--mode prepare`，文件参数相对脚本目录。它复制固定 UI dist（排除自带 data）与完整现有静态 data，逐文件核 hash，复制后再核源与候选闭包；源发生变化明确拒绝并要求重新 prepare。它不裁剪任何 case/evidence，超过 30 MiB 或 5000 文件拒绝。`--mode publish`/`rollback` 必须显式 `--execute`；publish 还要求固定 UI SHA、独立接受报告完整 SHA 和事先固定的 dist hash。仅提供 SHA 参数不能代替协调端核实接受报告。

实测 prepare 原入口（无 portal/live/unit/main 写入）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file scripts/PublishReviewedWebProgressUi.cs -- --mode prepare --dist ../../gm-web-inspection/web/dist --ui-commit 424e85d6aac32c2afe7cad12cfd04bea4f0037ab --data ../../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/bundle/data --work ../temp_docs/publishing-integration/ui-only-424
```

输出：610 files、27,998,665 bytes；UI hash `f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88`；原 data/index SHA `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`。证据在 `temp_docs/publishing-integration/ui-only-424/prepared.json` 和同目录 `bundle-*`；初次 compile 错误/修复日志保留在 `temp_docs/publishing-integration/`，不能当作发布成功。作者 dist 来自固定 UI 工作树，只做只读复制，独立 reviewer 仍须核定其源码与构建产物对应。

发布前需将 accepted 的固定 UI 源建立专用 detached checkout，构建一次，将 dist 复制为专用不可变 UI release 并核对 accepted dist hash。定时任务不得从旧 main610 checkout 重建 UI。`scripts/ConfigureReviewedWebProgressUiHook.cs --mode install --execute`（帮助列出必要参数）备份 timer 原 active/enabled、完整 effective unit 和原 drop-in 全文/哈希，暂停 timer 并确认 refresh 未运行，reset **整个**旧 `ExecStartPost=` 链后仅保留固定 UI hook；检查 effective ExecStartPost 恰有一个命令。安装后 timer 保持停止，等待首次 publish 与浏览器验收，再显式 `--mode resume --execute`。

脚本本身固定在经独立 review 的工具 checkout；每次 hook 指向相同 UI commit/dist hash，仅 data 来自原专用 publisher。该 companion 从不 enable/disable timer，只恢复记录的 active 状态；若 enabled 状态被用户改变则拒绝擅自启动。resume 要求 published.json 的 UI commit/hash/review 与安装记录一致。上述 unit 模式尚未执行且需独立运维脚本复核，不可据 prepare/compile 通过宣布可部署。

协调端已 review 固定 `1c492a84ce642289359556070d290cad4167b205` 的备份容器及两份发布脚本，随后要求补齐 writer 过渡态 gate 和真实 producer 数据路径后，在本任务内授权 UI-only 实际同 slug 更新；此授权不包含 A+B host/proxy 迁移或 main 合并。后补 gate 由 `scripts/VerifyReviewedWebProgressUiHook.cs` 的隔离 PATH/C# systemctl stub **31/31** 验证：inactive/enabled、inactive/disabled、active/enabled 的 install→匹配 published→resume→restore，activating/deactivating/reloading（MainPID0 但仍在旧 post）、pending job、外部 drop-in 修改拒绝；不调用真实 systemd。检查明确读取 ActiveState/SubState/MainPID/Job/TasksCurrent/ControlGroup，停 timer 后仅允许 inactive/failed 且无 writer/tasks/job。源 drop-in 和备份 hash 必须一致后才写 hook；无关 systemd enablement 链接不属于写集合。

实际只读核查：live 主 `WatchWebProgress.cs` 以 `--output ../temp_docs/web-progress-publish/generated --publish-data-to ../temp_docs/web-progress-publish/data` 更新完整 validated 根。旧两个 post 仅做 Prepare UI/dist 与 Portal 上传。新 hook 的 `--data` 必须指向这个持续更新的 `data`，而不是固定 `bundle/data`；reset 不会移除数据生产步骤。源 index SHA 为 `bf46829ee02f2d6a48cdad042a52861fd234ddf25b1e24ea7961ad29045d80e3`，与已服务 `117bb...` 仅发布 generatedAt/属性顺序不同，所有 run 文件相同。

`--baseline-data` 指向冻结的旧静态 data。只有全部非 index 文件 hash 及去除 generatedAt 的 index 身份完全一致时，才沿用旧 index 的精确字节，避免 UI-only 改动无谓变更当前数据；任何新 run/对象/索引身份变化都会采用真实新 producer 快照并记录新 SHA。source 原始 index/tree SHA 与 baselineIndexReused 一并进入 prepared.json，不伪称源字节相同。本任务 producer 根 prepare 已实测 baselineIndexReused=true，仍为 610 files / 27,998,665 bytes / data index `117bb...`。

`scripts/FreezeReviewedWebProgressUi.cs` 只从固定 tools Git 对象提取 6 个必要源码文件，复制 accepted UI hash 和 exact 旧 bundle，生成逐文件 SHA 白名单与 release.json；tools/UI/previous-static 设为只读，发布 work/state 独立。timer 使用冻结 release 的脚本和 UI，不能指向仍会编辑的工作树或作者 web/dist。冻结、实际发布和浏览器结果另记在最终整合报告中。

首次 publish 使用独立 UI release work 目录，`--previous-bundle` 指向真实当前同 slug bundle，脚本核其 index.html/data index 与当前 HTTPS 相同后备份。旧 bundle 全树 hash 为 `d513528c167a8e0c2d47fd35ae955569abb178a8f00594173f4b331c6538ce94`（610 files / 27,976,183 bytes；463 registrations / 17 attempted）。完整旧文件和 metadata 先写入独立 staging container，逐字节核验后以一次目录 rename 提交到 `previous-snapshot/{bundle,metadata.json}`；不得仅以目录存在当备份成功。中断、缺 metadata、损坏快照会保留 `.incomplete-*` 或 staging 证据并从确切旧源重建；已有 metadata 指定旧 hash 时，错误的新源不能替换旧历史。有效 legacy `previous-bundle` + `previous.json` 只读复用。

`--mode backup --previous-bundle <relative> --work <relative>` 是纯本地备份恢复入口，不调用 portal；`scripts/VerifyReviewedWebProgressUiBackup.cs` 用小型 BCL fixture 经真实 CLI 检查中断、缺 metadata、损坏、错误修复源与正常重试。publish/skip 都要求可校验的完整回退备份；新成功指纹包括 UI commit、UI content hash、完整 data tree hash，故相同 run 也能发布新 UI；portal 或 HTTP 校验失败不写 `published.json`。新旧包都使用原 `gm-kernel-progress` slug，凭证由既有 portalctl 自行读取。

验收桌面/手机布局、真实 GM/PK 模型加载、实体拾取、切换 case 后异步 fit、失败微值及全部静态资源无 404；这些浏览器验收本任务未执行。发布后恢复 timer 的原 enabled/active 状态，并执行一次受控 refresh 证明固定 UI hash 仍为新版本，再保持原周期。

回退顺序：暂停 timer、确认 refresh 不运行；以相同 work 和 portalctl 参数执行 `PublishReviewedWebProgressUi.cs --mode rollback --execute`，脚本核旧 bundle tree hash 并恢复同 slug、核 HTTPS index/html 原字节。随后以相同 work/unit-dir 执行 `ConfigureReviewedWebProgressUiHook.cs --mode restore --execute`，按备份精确恢复旧 drop-in 全文，daemon-reload、核 effective 链与原 enabled 状态，仅当原本 active 才恢复 timer；若用户改过 drop-in，拒绝覆盖用户编辑。`ROLLED_BACK` 标记拒绝新 UI hook 自动再次覆盖。不要只恢复网页而保留会再次发布新 UI 的 hook。独立脚本/运维复核、实际发布和回退演练仍是剩余 gate。
