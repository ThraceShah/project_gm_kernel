# Owned UI transition intent 窄修复

本轮修复独立 review `ctx_9769a077886d` 对工具 `d9fd0605f9beb3e23630c6b54fed3642b2d842da` 的 missing-hook 降级阻断。原最终文档提交 `de87c8b68b9f31e5359919116288b203b95ab01b` 和原失败证据保留，不能用前轮 173 项通过宣称 d9 已接受。本轮不改变其他发布设计或 hosted 迁移，不操作 live/systemd/portal/main。

修复固定 Git SHA：`1b935972eff529e13b46cb9b08ad9550e0a6aeb5`，已推送 `fix/reviewed-ui-owned-upgrade`。独立 reviewer 应固定此对象窄复验原 probe 与受影响 first install/owned restore；本报告的后续文字提交不改变该工具源码对象。

## 原证据及 before

正式独立 review 已固定在 `8d7d90d6fed1a0d6ea16c92a7121ac40dbbaa947`。原 repro 位于 `../gm-publishing-integration-review/docs/owned-ui-d9-review/MissingOwnedProbe.cs`；正式固定字节 SHA256 为 `26d1039ce7322273ae5102a00810bd9a21712e71cf2eaad37dbd984336d2d646`，已从固定 Git 对象只读提取到 `temp_docs/owned-ui-intent-original-MissingOwnedProbe.cs`，未编辑 reviewer 文件。任务开始读取的尚未提交版本 SHA 为 `b16adb7e421d7189fe4c94751c2370291f25403c6340d3b7f9d18e05e8929d46`；reviewer 随后增加了删除前的路径/pin 校验并提交正式版本，这两者不混称。

原 receipt 位于 `../gm-publishing-integration-review/temp_docs/owned-ui-d9-review/missing-owned-7be1d950cee2400388dfec46408d1a37/receipt.json`，SHA256 `09d75f4bac049e6bf433c24322f19c3cc539d57c96d5b9ce081839d71023b78c`。其原 CLI exit0、newHookExists=true、recordedOwnedUpgrade=false、timerActiveAfter=false、counterexample=true；原 install log 明确有 stop timer 和 daemon-reload。它不因修复而改写。

先以同一已确认反例 scenario 的隔离副本重放 d9：`temp_docs/owned-ui-intent-replay/before.log` / `before-receipt.json` 保存 exit0、9 次 stub systemctl 调用、创建 hook/备份、active→inactive。执行源码 SHA256 `3998b8204102dba621a9a5716cfeee6c6f621afda8ae145faff978ccc42ed0b4` 等于固定 d9 的 Configure。副作用后的完整 scenario 留在 `temp_docs/owned-ui-intent-replay/before-scenario-evidence`；不是删除现场再声称原来安全。

## 最小实现

`scripts/ConfigureReviewedWebProgressUiHook.cs` 从任何 previous 身份选项出现判定显式 owned transition intent，不从 `File.Exists(zz-reviewed-ui.conf)` 推断用户意图。三项 previous 路径/清单 pin/state pin 必须完整存在且有效；缺旧 hook 报 `UI_OWNED_TRANSITION_HOOK_MISSING`，partial 身份报 `UI_OWNED_TRANSITION_IDENTITY_REQUIRED`。完整但错误的 pin 仍由既有可信身份校验拒绝，没有降级路径。

install 的 work 创建、hook.lock、LoadState 和其他 systemctl 调用移到只读身份 preflight 之后；因此这些拒绝发生在任何控制、备份或写入前。没有 previous 选项的真正首次安装仍保留；但存在未知同名 hook 时，不能不带 previous 身份覆盖。restore/resume 仍先取各自 work lease 后执行原有流程；没有增加 service 管理器或新 host gate。

## 同输入 after 与有限控制

`temp_docs/ReplayMissingOwnedIntent.cs` 在同一场景路径、相同 CLI 参数/原 pins/准备与回退数据下，仅替换 Configure 执行源码后复放；原 manifest 保留，missing-hook 检查在任何 manifest 访问/写入之前。它是窄反例重放，完整合法新冻结清单由下面的 owned positive 控制独立验证。

after 的 `temp_docs/owned-ui-intent-replay/after.log` / `after-receipt.json` 保存 CLI **exit134**、`UI_OWNED_TRANSITION_HOOK_MISSING`、**systemctlCalls=0**、hookExists=false、timerActive=true、evidenceUnchanged=true。逐文件 SHA、work 目录集合、stub timer 状态、publication 指针均与执行前相同；无 hook.lock、新备份或 publication 写入。

窄 fixture 入口：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyReviewedWebProgressUiHook.cs -- --transition-only
```

它使用同一 BCL/loopback/PATH stub 基础设施，覆盖缺旧 hook、三项 single pin、三项 missing pin、空/无值/inline 参数、完整但错误的 pin；每项同时检查非零退出和 timer/systemctl/drop-ins/work/backup/publication 无变化。另有无 previous 身份的真实 first install→restore、未知 existing hook 拒绝，以及完整合法 owned prepare→verified backup→install→rollback→restore 控制。默认入口仍保留原完整检查，不把窄检查数冒充全套重跑。

窄 fixture **31/31、外层 exit0**，证据 `temp_docs/owned-ui-intent-reviewed.log` 及 `temp_docs/publishing-integration/ui-hook-9c3d894e409c4e179d9bcf4a9db1d7c7`；其中拒绝 CLI 均为原 exit134，first install/owned positive 和两种 restore 均为原 exit0。before receipt SHA256 `56c7f0ba254c07a8987d86b0843934e30d653c3b3bc6d469b454ce573e0ec53d`，after receipt SHA256 `cdfdfbeadf3a52f476bae9ecc9dfa8ad3fa0b4fa2a3d518827e552836dc720b9`；修复 Configure 源码 SHA256 `929082a9771efc025aadfb19b55c98b4007f7e0623c1c071ccba6fe0e72d2173`。

本轮尚不代表独立 reviewer 接受或实际部署授权。其他独立 22 项 owned 控制、20 项 legacy 只读身份及 3 项固定 Git 冻结结论保持原报告范围，未扩写或重新声称验收；全套 173/后续默认扩展检查没有在本窄修复任务重跑。
