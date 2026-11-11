# Reviewed UI owned hook 升级工具

本分支仅准备工具与隔离 fixture，不部署任何候选，不修改 live unit、门户或 main。起点 `1dc0afc4a644867b49204bfbc76834b910413177`；当前 live 身份及旧记录路径见 `docs/web-progress-ui-424-deployment.md`。

## 最小信任与备份边界

`ConfigureReviewedWebProgressUiHook.cs --mode install` 遇到既有 `zz-reviewed-ui.conf` 时，只允许显式 owned transition：`--previous-release`、`--previous-release-sha256`、`--previous-state-sha256` 固定旧 release 清单及旧 hook state 原始字节；`--release-sha256` 固定新清单。旧清单逐文件 SHA、只读 tools/UI/previous-static、旧 state 的 UI/review/hook hash、完整原 drop-ins hash、当前实际 hook 和有效 ExecStartPost argv 必须一致。旧记录通过只读 legacy adapter 消费，不补写任何字段，也不把事后核验声称为原始记录。

新 release 的 tools/UI 必须位于固定只读目录，work 独立；producer data 和 portal 路径保持旧 hook 身份，不涉及 host cutover。新 UI commit、review、UI hash 必须和 prepared、release 三方一致。冻结入口接受显式下一轮 UI 身份，仍只从指定 tools Git 对象提取必要源码。

回退包必须是更新前真实 served 的整个 bundle：UI、hashed assets、全部 data 闭包及 tree hash。UI424 与 pre424 的 data index 相同，不能拿旧 release 的 `previous-static` 冒充 UI424。先用 `PublishReviewedWebProgressUi.cs --mode backup --verify-served` 对每个文件 HTTPS 核字节，再以 content+metadata 容器单 rename 提交。owned install 要求这个完整且已核验的备份匹配旧 UI hash 和新 release 的 oldBundleHash；停 timer、writer idle 后再次核当前 served 整包，才原子替换 hook 文件。

完整 drop-ins、effective unit/post 和实际 timer active/enabled 状态同样先 staging+state 单 rename 提交。未知 hook、任何 drop-in/工具编辑、writer 过渡态、job/tasks、缺失备份均拒绝；不 enable/disable timer，发布失败或 published 身份不匹配不 resume。rollback 使用新 work 的更新前 bundle；restore 核 rollback 标记与真实旧包，再恢复 exact 更新前 UI424 hook 和原 timer 偏好。中断 staging/被替换 drop-ins 保留为证据。

## 验证与独立 review

操作入口为 `scripts/FreezeReviewedWebProgressUi.cs`、`scripts/PublishReviewedWebProgressUi.cs`、`scripts/ConfigureReviewedWebProgressUiHook.cs` 的 `--help`。所有路径参数相对于运行脚本目录。私有 Tailnet HTTP 校验仅该 HttpClient 使用 `UseProxy=false`，保留系统 TLS 证书校验，不改全局代理。

基础验证：`MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyReviewedWebProgressUiHook.cs` 原 31 项通过；`MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyReviewedWebProgressUiBackup.cs` 原 14 项通过。两者均无真实 systemctl/portal 操作；升级完整链 fixture 和最终固定 SHA 将在后续验证记录追加。没有 Kernel/PK/AOT/build/benchmark 验证。
