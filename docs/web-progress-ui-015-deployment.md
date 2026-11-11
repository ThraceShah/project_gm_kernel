# 固定015 UI-only owned 升级

目标：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。本轮明确获准实际 UI-only 发布，不合 main，不迁移 hosted/proxy，不改变 producer/采集/内核/性能结论。

## 固定身份与准备回执

| 对象 | 固定身份 |
| --- | --- |
| 工具 | `1b935972eff529e13b46cb9b08ad9550e0a6aeb5`；独立接受 `d32e862dc947702d4655912efc916d5c035df04b` |
| UI | `015cd308e147356fecac96bb7354ab1e92c36a03`；独立融合接受 `f9ed821287c7bfb4e23963a10238c9de32a3854c` |
| UI18 hash | `f0593760ed1e22dd73e94f609e9e2353366370b542019a90cb8f486f796a4dd6` |
| 完整625文件 / bytes / hash | `625` / `28214939` / `8dca4b3500652fe0ff5161a8a5943abdcc5a69d0a40b472de17e95b1ca583069` |
| 新 release 清单 SHA256 | `b86ccc134a3aa631387255743de1318d6b8e2868567cb739f36421d273524c2d` |
| 更新前真实 UI424 整包 hash | `ffcf2b2269f8208622da84ded3346f72f63288d63ecfc9e91bb9b2b96676a1b7` |

新不可变 release 为 `../../../../.local/share/gm-kernel-progress-ui/releases/ui-015-tools-1b93597`。7 个工具源码从固定1b Git对象提取，UI源码使用 detached 015；作者 ff5 的 web Git tree 与015完全相同且 web tracked clean。复用独立两轮已验 dist，只复制其18个非data文件并核目标hash；没有重建、工作diff偷换、手工 data overlay 或全零 review pin。15个真实 U/V 资源位于 UI hash 覆盖的 `display/real-uv` namespace。

准备时只读核旧 release 清单/state pin、完整当前 drop-ins、实际 effective ExecStartPost、writer idle、原 timer active/waiting+enabled；610 个当前 served UI424 文件逐个真实 HTTPS200/hash 相同，HTML 为 `a779f1d695dcee897f1cf6726c95a57b8804399b4ea14ecc89b7c2f31b80fe01`。回退源取旧 work/prepared 指向的当前 UI424 整包，**没有使用 pre424 previous-static**。新 previous-static 和 content+metadata 备份同为当前 UI424。

原607历史文件、index `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`、data tree `fbb68a2aacd7eebb910f63a51a23954eec340e9bc3960e000448c699565d7390` 全部保持。producer仍 tracked clean main `610e3ce844bf99c85af6069905f9f3deaab08133`，真实输入为其已验证 publish data root；每轮 frozen UI copy 保留全部 display 文件。625文件/28214939B低于30MiB预算和32MiB硬门，无历史裁剪。

freeze / prepare / verified backup 原 exit 均0。2026-11-11 **08:23:57Z / 16:23:57 SGT**，固定 Configure install 原 exit0：完整 drop-ins + 实际 active/enabled state 已先原子备份，唯一新 fixed UI hook 已加载，timer inactive/dead且仍enabled，service inactive/dead、MainPID0、无Job/ControlGroup、TasksCurrent未设置。此阶段尚不代表实际发布或浏览器接受；后续回执追加。

准备、安装和全部逐文件回执保留于 `temp_docs/ui-015-deployment`。私有 Tailnet HTTP 客户端使用 UseProxy=false，保留正常 TLS；未改全局代理或凭证。

## 事先固定的精确回退命令

以下从本项目根运行，回退目标是**本次更新前 UI424**。先暂停 timer、确认 writer 完全 idle；不杀用户/其它 worker。新 work 的完整备份失败或外部编辑会明确拒绝，不绕过门。每条命令原 exit 单独保留。

```sh
systemctl --user stop gm-kernel-progress-refresh.timer
systemctl --user show gm-kernel-progress-refresh.service -p ActiveState -p SubState -p MainPID -p Job -p TasksCurrent -p ControlGroup
MSBUILDDISABLENODEREUSE=1 dotnet run --file ../../../../.local/share/gm-kernel-progress-ui/releases/ui-015-tools-1b93597/tools/scripts/PublishReviewedWebProgressUi.cs -- --mode rollback --execute --work ../../work --portalctl ../../../../../../../code/my_portal/portalctl.mjs
MSBUILDDISABLENODEREUSE=1 dotnet run --file ../../../../.local/share/gm-kernel-progress-ui/releases/ui-015-tools-1b93597/tools/scripts/ConfigureReviewedWebProgressUiHook.cs -- --mode restore --execute --work ../../work --unit-dir ../../../../../../../.config/systemd/user
```

rollback 全包 HTTPS 校验并写 ROLLED_BACK 后，restore 核实际旧包、完整 drop-ins及effective旧argv，恢复 exact UI424 hook 和原 active bit；不 enable/disable timer。任何 publish/served/关键browser失败均按此回退并重核旧资源及偏好；本任务没有授权循环修工具或网络重试。
