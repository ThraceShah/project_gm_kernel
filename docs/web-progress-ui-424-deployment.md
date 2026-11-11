# UI424 实际切换记录

此文件先记录已授权执行的固定产物及回退命令，成功后的 HTTP/browser/unit 结果将追加。范围仅静态 `gm-kernel-progress` 同 slug UI；不合 main、不部署 A+B host/proxy、不接入 UV。

- UI：`424e85d6aac32c2afe7cad12cfd04bea4f0037ab`；独立接受 `f1fdc36553e9132c88d78a933e503160950460f2`。
- 冻结 tools：`6efeb0c6f4b45ae12d7ba94f7e4c43b52f1c0717`，已推送；6 个必要源码从固定 Git 对象提取，无凭证或私有依赖。
- release：`../../../../.local/share/gm-kernel-progress-ui/releases/ui-424-tools-6efeb0c`，tools/UI/previous-static 只读，work 独立可写；`release.json` 含完整逐文件 SHA 白名单。
- UI hash：`f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88`。
- exact 旧 bundle tree hash：`d513528c167a8e0c2d47fd35ae955569abb178a8f00594173f4b331c6538ce94`；已复制且通过原 HTTPS 7 个资源逐字节比对，再以 content+metadata 容器提交回退备份。
- 本次候选 610 files / 27,998,665 bytes；保持 index `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`。真实 producer 原 index `bf46829ee02f2d6a48cdad042a52861fd234ddf25b1e24ea7961ad29045d80e3`，baseline equality gate 证明所有 run bytes 与 descriptor identity 相同，复用旧 index 精确字节。

## 执行前留存的精确回退命令

从本项目根运行；先停止该 timer 并用 show 确认 service ActiveState=inactive/failed、SubState=dead/failed、MainPID=0、Job 空且无 TasksCurrent writer。不要杀用户/其它 worker。若 service 尚在 start-post/activating 等过渡态，等待其完成再恢复。

```sh
systemctl --user stop gm-kernel-progress-refresh.timer
systemctl --user show gm-kernel-progress-refresh.service -p ActiveState -p SubState -p MainPID -p Job -p TasksCurrent -p ControlGroup
MSBUILDDISABLENODEREUSE=1 dotnet run --file ../../../../.local/share/gm-kernel-progress-ui/releases/ui-424-tools-6efeb0c/tools/scripts/PublishReviewedWebProgressUi.cs -- --mode rollback --execute --work ../../work --portalctl ../../../../../../../code/my_portal/portalctl.mjs
MSBUILDDISABLENODEREUSE=1 dotnet run --file ../../../../.local/share/gm-kernel-progress-ui/releases/ui-424-tools-6efeb0c/tools/scripts/ConfigureReviewedWebProgressUiHook.cs -- --mode restore --execute --work ../../work --unit-dir ../../../../../../../.config/systemd/user
```

第一脚本核 exact 旧备份 tree hash、恢复同 slug 并校验 HTTPS index/html；第二脚本核完整原 drop-in 备份及无外部编辑后原字节恢复、daemon-reload，仅恢复原 active，不 enable/disable timer。`ROLLED_BACK` 拒绝新 hook 再自动覆盖。出现任何错误保留原 exit/log，不在未知状态继续。

## 当前准备证据

`temp_docs/publishing-integration/live-before.log`、`before-ui-publish-http.json`：原网页 HTML/JS/CSS、index、latest manifest、真实 mesh/log 七条 HTTP200 且 exact bytes。`frozen-prepare.log`：冻结工具独立 runfile 成功；`live-backup.log`：exact old tree 提交成功。首次手填 tools SHA 错误被 Git object check 拒绝，原 exit134/未提交 staging 保留；随后以 git rev-parse 的完整实际 SHA 成功冻结，不能把失败候选归作已发布。
