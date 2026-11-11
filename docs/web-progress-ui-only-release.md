# 独立 UI 静态发布切片（只准备，尚未发布）

固定 UI 源为 `424e85d6aac32c2afe7cad12cfd04bea4f0037ab`，本任务没有将其视作独立 accepted。该切片不依赖 A+B host/scheduler 迁移、ledger 或内核完成；数据生产者继续使用 main `610e3ce844bf99c85af6069905f9f3deaab08133` 与当前 17 representative 的真实历史结果，不重新运行 PK。

`scripts/PublishReviewedWebProgressUi.cs` 默认 `--mode prepare`，文件参数相对脚本目录。它复制固定 UI dist（排除自带 data）与完整现有静态 data，逐文件核 hash，复制后再核源与候选闭包；源发生变化明确拒绝并要求重新 prepare。它不裁剪任何 case/evidence，超过 30 MiB 或 5000 文件拒绝。`--mode publish`/`rollback` 必须显式 `--execute`；publish 还要求固定 UI SHA、独立接受报告完整 SHA 和事先固定的 dist hash。仅提供 SHA 参数不能代替协调端核实接受报告。

实测 prepare 原入口（无 portal/live/unit/main 写入）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file scripts/PublishReviewedWebProgressUi.cs -- --mode prepare --dist ../../gm-web-inspection/web/dist --ui-commit 424e85d6aac32c2afe7cad12cfd04bea4f0037ab --data ../../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/bundle/data --work ../temp_docs/publishing-integration/ui-only-424
```

输出：610 files、27,998,665 bytes；UI hash `f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88`；原 data/index SHA `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`。证据在 `temp_docs/publishing-integration/ui-only-424/prepared.json` 和同目录 `bundle-*`；初次 compile 错误/修复日志保留在 `temp_docs/publishing-integration/`，不能当作发布成功。作者 dist 来自固定 UI 工作树，只做只读复制，独立 reviewer 仍须核定其源码与构建产物对应。

发布前需将 accepted 的固定 UI 源建立专用 detached checkout，构建一次，将 dist 复制为专用不可变 UI release 并核对 accepted dist hash。定时任务不得从旧 main610 checkout 重建 UI：备份 timer 原 active/enabled、完整 effective unit 和原 drop-in 全文/哈希，暂停 timer 并确认 refresh 未运行，reset **整个**旧 `ExecStartPost=` 链后仅保留固定 `PublishReviewedWebProgressUi.cs` hook。脚本本身也固定在经独立 review 的工具 checkout；每次 hook 指向相同 UI commit/dist hash，仅 data 来自原专用 publisher。恢复前查看 effective ExecStartPost，必须只有此新 hook；原本 inactive/disabled 的 timer 不得擅启。上述 unit 切换尚未执行且需独立运维脚本复核，不可据 prepare 通过宣布可部署。

首次 publish 使用独立 UI release work 目录，`--previous-bundle` 指向真实当前同 slug bundle，脚本核其 index.html/data index 与当前 HTTPS 相同后完整备份为 `previous-bundle` 并记录 tree hash。新成功指纹包括 UI commit、UI content hash、完整 data tree hash，故相同 run 也能发布新 UI；portal 或 HTTP 校验失败不写 `published.json`。新包与旧包都使用原 `gm-kernel-progress` slug，凭证由既有 portalctl 自行读取。

验收桌面/手机布局、真实 GM/PK 模型加载、实体拾取、切换 case 后异步 fit、失败微值及全部静态资源无 404；这些浏览器验收本任务未执行。发布后恢复 timer 的原 enabled/active 状态，并执行一次受控 refresh 证明固定 UI hash 仍为新版本，再保持原周期。

回退顺序：暂停 timer、确认 refresh 不运行；以相同 work 和 portalctl 参数执行 `--mode rollback --execute`，脚本核旧 bundle tree hash 并恢复同 slug、核 HTTPS index/html 原字节。随后按备份精确恢复旧 unit/drop-in 全文，daemon-reload、核 effective 链与原 enabled 状态，仅当原本 active 才恢复 timer；`ROLLED_BACK` 标记会拒绝新 UI hook 自动再次覆盖。不要只恢复网页而保留会再次发布新 UI 的 hook。独立脚本/运维复核、固定 UI 接受、实际发布和回退演练仍是剩余 gate。
