# UI424 实际切换记录

**结果：UI424 已在原同 slug 静态门户生效。** 范围仅 `gm-kernel-progress` UI；不合 main、不部署 A+B host/proxy、不接入 UV。

- UI：`424e85d6aac32c2afe7cad12cfd04bea4f0037ab`；独立接受 `f1fdc36553e9132c88d78a933e503160950460f2`。
- 最终冻结 tools：`35498ac5e72eda303883120d8b58cbb6c15fdb33`，已推送；6 个必要源码从固定 Git 对象提取，无凭证或私有依赖。
- 最终 release：`../../../../.local/share/gm-kernel-progress-ui/releases/ui-424-tools-35498ac`，tools/UI/previous-static 只读，work 独立可写；`release.json` 含完整逐文件 SHA 白名单。原6ef release和失败/回滚证据完整保留。
- UI hash：`f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88`。
- exact 旧 bundle tree hash：`d513528c167a8e0c2d47fd35ae955569abb178a8f00594173f4b331c6538ce94`；已复制且通过原 HTTPS 7 个资源逐字节比对，再以 content+metadata 容器提交回退备份。
- 本次候选 610 files / 27,998,665 bytes；保持 index `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`。真实 producer 原 index `bf46829ee02f2d6a48cdad042a52861fd234ddf25b1e24ea7961ad29045d80e3`，baseline equality gate 证明所有 run bytes 与 descriptor identity 相同，复用旧 index 精确字节。

## 执行前留存的精确回退命令

从本项目根运行；先停止该 timer 并用 show 确认 service ActiveState=inactive/failed、SubState=dead/failed、MainPID=0、Job 空且无 TasksCurrent writer。不要杀用户/其它 worker。若 service 尚在 start-post/activating 等过渡态，等待其完成再恢复。

```sh
systemctl --user stop gm-kernel-progress-refresh.timer
systemctl --user show gm-kernel-progress-refresh.service -p ActiveState -p SubState -p MainPID -p Job -p TasksCurrent -p ControlGroup
MSBUILDDISABLENODEREUSE=1 dotnet run --file ../../../../.local/share/gm-kernel-progress-ui/releases/ui-424-tools-35498ac/tools/scripts/PublishReviewedWebProgressUi.cs -- --mode rollback --execute --work ../../work --portalctl ../../../../../../../code/my_portal/portalctl.mjs
MSBUILDDISABLENODEREUSE=1 dotnet run --file ../../../../.local/share/gm-kernel-progress-ui/releases/ui-424-tools-35498ac/tools/scripts/ConfigureReviewedWebProgressUiHook.cs -- --mode restore --execute --work ../../work --unit-dir ../../../../../../../.config/systemd/user
```

第一脚本核 exact 旧备份 tree hash、恢复同 slug 并校验 HTTPS index/html；第二脚本核完整原 drop-in 备份及无外部编辑后原字节恢复、daemon-reload，仅恢复原 active，不 enable/disable timer。`ROLLED_BACK` 拒绝新 hook 再自动覆盖。出现任何错误保留原 exit/log，不在未知状态继续。

## 当前准备证据

`temp_docs/publishing-integration/live-before.log`、`before-ui-publish-http.json`：原网页 HTML/JS/CSS、index、latest manifest、真实 mesh/log 七条 HTTP200 且 exact bytes。`frozen-prepare.log`：冻结工具独立 runfile 成功；`live-backup.log`：exact old tree 提交成功。首次手填 tools SHA 错误被 Git object check 拒绝，原 exit134/未提交 staging 保留；随后以 git rev-parse 的完整实际 SHA 成功冻结，不能把失败候选归作已发布。

## 实际发布、恢复与最终回执

地址：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。唯一允许的传输修复重试开始 **2026-11-11T04:46:12Z**，发布成功状态文件提交 **04:47:44.0142253Z**，timer 恢复 **04:53:45Z**，完整只读产物回执完成 **04:55:14.8362804Z**。

| 最终实际 HTTPS 资源 | SHA256 / 结果 |
| --- | --- |
| index.html | `a779f1d695dcee897f1cf6726c95a57b8804399b4ea14ecc89b7c2f31b80fe01` |
| assets/index-DQUBkM3M.js | `9d469d1e0b2d3b5915dc3bac688088987e93d8e8c16ee63c0cdbaf0e02c5cfcf` |
| assets/index-DJmd_oSm.css | `1b6fa52bed50b05a2bc87a2aff99695ba2d4d790a10dbd2ba782fdb86b9b1d89` |
| data/index.json | `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`，原字节保留 |
| latest manifest | `03c2f12134e152e37cb5bc5f7b9fd3a7e093ad0a30453501b579af3d50f0a66d`，main610 原 run |
| 真实 GM before mesh | `eedd1b3a21178aa8afcce44c10361312e88f758eee0a34472184ab6913d026c2` |
| 原 oracle.log | `d88c873fe055a0391296b8057cb8c3c3b4c1086a17400a9c2e4bcb41a00217dc` |

上述七条在发布后及恢复 timer 后均实际 HTTP200、逐字节等于冻结候选，见 `live-after-final.log`、`live-after-resume.log` 和对应 `*-http.json`。`final-live-publish.log` 原 exit0，成功写 published.json；`deployment-receipt.json` 核 **619 个 immutable files 全部 SHA 匹配且只读**。最终发布脚本 SHA `5374e98c541dd86c7ad38d22323d9bf8a0bf66e1935bd811d18fdb614e045f1d`，unit companion SHA `b8619d8a6c2129d5a1dd73d14418468bbd1e918bf5a6c5fe6783a06524e56a49`；完整6源码白名单见 release.json/receipt。

最终 effective ExecStartPost 恰一个命令，固定到 release 内 `tools/scripts/PublishReviewedWebProgressUi.cs`，`--dist ../../ui`、`--data ../../../../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/data`、`--baseline-data ../../previous-static/data`。timer 最终 **active/waiting + enabled**，与安装前原状态相同；没有 enable/disable。源专用 publisher 仍 clean main `610e3ce844bf99c85af6069905f9f3deaab08133`，463 registrations / **17 attempted：14 pass、3 fail**，未运行新几何。

恢复 timer 后 **04:53:50–04:53:52Z** 自动轮次实际完成：Watch 输出 `Source and registry unchanged; retained current static data.`；新 post 输出固定 UI/hash、baselineIndexReused=true 和 `UI_SKIP_SAME includes UI identity`，service 原 status0。新 UI 未被旧 main610 UI 重建覆盖，见 `post-resume-cycle.log`；不是只凭 unit 文本推断。

## 实际桌面/手机验收

自有 Chromium 仅访问真实 HTTPS，结束已 close。**19/19 有效检查通过**：双模型 faces/triangles 与可见像素、全模型/标准视图、透明度及不抢相机、前后态对应真实 served mesh 计数、异步 case 切换重新 fit、FAIL `2.01065554e-7 > 1e-7` 微值/阈值保真；390×844 两个模型、无横向 page overflow、drawer 选择后离开 viewport、GM/PK 单视图扩大、controls 可用；无 uncaught error、必需资源无失败。UV 衍生索引 optional404 及“无等参线”fallback 保留，**UV 未包含**。

两个初始 probe 假设错误原文保留在 `live-browser-424.json/probeNotes`：前后态合法共享 body ID，原“body ID 必须变化”等待 timeout，后改为真实 after 文件 faces/triangles 对照；Playwright isVisible 把 translate 到屏幕外的 drawer 当作 visible，后以实测 x=-308/right=-13 的 viewport 交集验收。没有修改 UI 或门槛来迁就检查，也没有再上传；不把无效 probe 隐藏成首次全过。

截图：`temp_docs/publishing-integration/live-desktop-424.png`、`live-desktop-controls-424.png`、`live-desktop-failure-424.png`、`live-mobile-424.png`，均实际截图并已目视检查。实体拾取本次未另专项复跑；既有 UI 独立 review 结果保留，不把19项宣称为全UI/全平台验收。

## 首次失败与真实回退（未抹去）

首次6ef工具实际 portal 上传成功，但继承代理的默认 .NET HttpClient 校验发生 SSL EOF，原 publish **exit134**，没有 published.json。立即上传 exact 旧 bundle 回退；同一默认 handler 校验也 **exit134**，不能误记门户未回退。随后旧 drop-in 精确 restore **exit0**，原 active/enabled timer 恢复；独立 UseProxy=false 标准证书校验实际核旧 HTML/JS/CSS/index/manifest/mesh/log 全七条 exact200，旧 HTML `ca0e7d8b79e3e21f019385a41dd8ee8cea25bc42a13f8ec304e2847a2b8c5ade`。

协调端仅授权两处固定私有门户 HttpClient 显式 `UseProxy=false` 的最小修复及单次重试；提交354并冻结新只读路径后执行成功。没有关闭证书验证、修改 global 代理/凭证/网络，或反复上传碰运气。原6ef release、所有原 exit、rollback/restore 与实际 old7资源验证日志完整保留在 `temp_docs/publishing-integration/live-*.log`。
