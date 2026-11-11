# Owned UI transition intent 窄修复

本轮修复独立 review `ctx_9769a077886d` 对工具 `d9fd0605f9beb3e23630c6b54fed3642b2d842da` 的 missing-hook 降级阻断。原最终文档提交 `de87c8b68b9f31e5359919116288b203b95ab01b` 和原失败证据保留，不能用前轮 173 项通过宣称 d9 已接受。本轮不改变其他发布设计或 hosted 迁移，不操作 live/systemd/portal/main。

修复固定 Git SHA：`1b935972eff529e13b46cb9b08ad9550e0a6aeb5`，已推送 `fix/reviewed-ui-owned-upgrade`。独立 reviewer 应固定此对象窄复验原 probe 与受影响 first install/owned restore；本报告的后续文字提交不改变该工具源码对象。

## 原证据及 before

正式独立 review 已固定在 `8d7d90d6fed1a0d6ea16c92a7121ac40dbbaa947`。原 repro 位于 `../gm-publishing-integration-review/docs/owned-ui-d9-review/MissingOwnedProbe.cs`；正式固定字节 SHA256 为 `26d1039ce7322273ae5102a00810bd9a21712e71cf2eaad37dbd984336d2d646`，已从固定 Git 对象只读提取到 `temp_docs/owned-ui-intent-original-MissingOwnedProbe.cs`，未编辑 reviewer 文件。任务开始读取的尚未提交版本 SHA 为 `b16adb7e421d7189fe4c94751c2370291f25403c6340d3b7f9d18e05e8929d46`；reviewer 随后增加了删除前的路径/pin 校验并提交正式版本，这两者不混称。

原 receipt 位于 `../gm-publishing-integration-review/temp_docs/owned-ui-d9-review/missing-owned-7be1d950cee2400388dfec46408d1a37/receipt.json`，SHA256 `09d75f4bac049e6bf433c24322f19c3cc539d57c96d5b9ce081839d71023b78c`。其原 CLI exit0、newHookExists=true、recordedOwnedUpgrade=false、timerActiveAfter=false、counterexample=true；原 install log 明确有 stop timer 和 daemon-reload。它不因修复而改写。

先以同一已确认反例 scenario 的隔离副本重放 d9：`temp_docs/owned-ui-intent-replay/before.log` / `temp_docs/owned-ui-intent-replay/before-receipt.json` 保存 exit0、9 次 stub systemctl 调用、创建 hook/备份、active→inactive。执行源码 SHA256 `3998b8204102dba621a9a5716cfeee6c6f621afda8ae145faff978ccc42ed0b4` 等于固定 d9 的 Configure。副作用后的完整 scenario 留在 `temp_docs/owned-ui-intent-replay/before-scenario-evidence`；不是删除现场再声称原来安全。

## 最小实现

`scripts/ConfigureReviewedWebProgressUiHook.cs` 从任何 previous 身份选项出现判定显式 owned transition intent，不从 `File.Exists(zz-reviewed-ui.conf)` 推断用户意图。三项 previous 路径/清单 pin/state pin 必须完整存在且有效；缺旧 hook 报 `UI_OWNED_TRANSITION_HOOK_MISSING`，partial 身份报 `UI_OWNED_TRANSITION_IDENTITY_REQUIRED`。完整但错误的 pin 仍由既有可信身份校验拒绝，没有降级路径。

install 的 work 创建、hook.lock、LoadState 和其他 systemctl 调用移到只读身份 preflight 之后；因此这些拒绝发生在任何控制、备份或写入前。没有 previous 选项的真正首次安装仍保留；但存在未知同名 hook 时，不能不带 previous 身份覆盖。restore/resume 仍先取各自 work lease 后执行原有流程；没有增加 service 管理器或新 host gate。

## 同输入 after 与有限控制

`temp_docs/ReplayMissingOwnedIntent.cs` 在同一场景路径、相同 CLI 参数/原 pins/准备与回退数据下，仅替换 Configure 执行源码后复放；原 manifest 保留，missing-hook 检查在任何 manifest 访问/写入之前。它是窄反例重放，完整合法新冻结清单由下面的 owned positive 控制独立验证。

after 的 `temp_docs/owned-ui-intent-replay/after.log` / `temp_docs/owned-ui-intent-replay/after-receipt.json` 保存 CLI **exit134**、`UI_OWNED_TRANSITION_HOOK_MISSING`、**systemctlCalls=0**、hookExists=false、timerActive=true、evidenceUnchanged=true。逐文件 SHA、work 目录集合、stub timer 状态、publication 指针均与执行前相同；无 hook.lock、新备份或 publication 写入。

窄 fixture 入口：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/VerifyReviewedWebProgressUiHook.cs -- --transition-only
```

它使用同一 BCL/loopback/PATH stub 基础设施，覆盖缺旧 hook、三项 single pin、三项 missing pin、空/无值/inline 参数、完整但错误的 pin；每项同时检查非零退出和 timer/systemctl/drop-ins/work/backup/publication 无变化。另有无 previous 身份的真实 first install→restore、未知 existing hook 拒绝，以及完整合法 owned prepare→verified backup→install→rollback→restore 控制。默认入口仍保留原完整检查，不把窄检查数冒充全套重跑。

窄 fixture **31/31、外层 exit0**，证据 `temp_docs/owned-ui-intent-reviewed.log` 及 `temp_docs/publishing-integration/ui-hook-9c3d894e409c4e179d9bcf4a9db1d7c7`；其中拒绝 CLI 均为原 exit134，first install/owned positive 和两种 restore 均为原 exit0。before receipt SHA256 `56c7f0ba254c07a8987d86b0843934e30d653c3b3bc6d469b454ce573e0ec53d`，after receipt SHA256 `cdfdfbeadf3a52f476bae9ecc9dfa8ad3fa0b4fa2a3d518827e552836dc720b9`；修复 Configure 源码 SHA256 `929082a9771efc025aadfb19b55c98b4007f7e0623c1c071ccba6fe0e72d2173`。

本轮尚不代表独立 reviewer 接受或实际部署授权。其他独立 22 项 owned 控制、20 项 legacy 只读身份及 3 项固定 Git 冻结结论保持原报告范围，未扩写或重新声称验收；全套 173/后续默认扩展检查没有在本窄修复任务重跑。

## 独立窄复验接受（固定1b，2026-11-11）

**结论：accepted，原 U1 missing-owned 降级阻断已关闭。** 固定 `1b935972eff529e13b46cb9b08ad9550e0a6aeb5` 的 intent 修复，与原 d9 已独立验证且未受改动的有限范围合并，可作为下一次 **UI-only owned 升级工具** 的接受范围。原 d9 本身仍是 blocked 的历史事实；本轮不改签实际新 UI、main 集成、发布执行或 hosted/proxy cutover。

### 固定源与保留边界

本次独立分支 `ThraceShah/gm-owned-ui-intent-1b-review` 从固定文档对象 `9d6a79e5dd52752ad135736c807ade29dc944353` 建立；开始 fetch 后 origin/fix/reviewed-ui-owned-upgrade 实际为该对象。它与工具对象1b的 scripts无差异。相对 d9，生产仅改变 Configure 的 intent/preflight/lease 位置；另改窄fixture和文档，没有读取或追随作者工作树漂移。

原拒收分支 `8d7d90d6fed1a0d6ea16c92a7121ac40dbbaa947` 和所有旧 ignored evidence 保留；原报告 `docs/web-progress-owned-ui-upgrade.md` 在本分支保留的 Git blob仍为 `4832f5297b2561326f8c0e237624763750727a56`，与8d对象相同。原 `MissingOwnedProbe.cs` **未改一字**，SHA256仍 `26d1039ce7322273ae5102a00810bd9a21712e71cf2eaad37dbd984336d2d646`。两份原d9 receipt同样前后匹配：

| 原路径（前缀 `temp_docs/owned-ui-d9-review/`） | 保留的 SHA256 / 事实 |
| --- | --- |
| `missing-owned-7be1d950cee2400388dfec46408d1a37/receipt.json` | `09d75f4bac049e6bf433c24322f19c3cc539d57c96d5b9ce081839d71023b78c`；原CLI0、创建hook、timer停 |
| `missing-owned-afcccdbee8354d599aa007ee9e788dee/receipt.json` | `870dc55a3a6effa735e30d393a791e2c0a9035a3c4d88725da7e9f8e17579eb0`；补输入身份断言后原CLI0，仍是反例 |

原 probe及repro文件只从8d Git对象原文保留；没有重新生成旧receipt或改写其0为134。新的重放输入/结果各用新目录，旧d9输入的Configure仍为原 `3998b8204102dba621a9a5716cfeee6c6f621afda8ae145faff978ccc42ed0b4`。

### 原probe逐字运行与独立零副作用控制

入口 `docs/owned-ui-intent-1b-review/ReplayOriginal.cs` 的实际命令见同目录README。wrapper只复制新的私有input，替换其中Configure为固定1b原字节并更新**新** manifest pin；原旧工具、数据及原probe不变。新Configure实测SHA256为 `929082a9771efc025aadfb19b55c98b4007f7e0623c1c071ccba6fe0e72d2173`。

原probe逐字执行后的证据为 `temp_docs/owned-ui-d9-review/missing-owned-766d645182b74953b8b83e61e757b83b/`：内部真实install CLI **134**，明确 `UI_OWNED_TRANSITION_HOOK_MISSING`，newHookExists=false、recordedOwnedUpgrade=null、timerActiveAfter=true、counterexample=false；**systemctl调用0**，无hook.lock、新hook-backup或publication写入。原probe仍断言d9的错误成功行为，因此它的外层134来自“expected counterexample not observed”；这是保留原期待的预期结果，不能把这个外层134误判为修复失败或隐去。

随后在相同输入上以相同实际CLI独立抓取执行前后全scenario文件SHA、完整目录集合、work/backup/publication、timer原始状态和stub调用文件：

| 独立控制 | 内部原exit / 原因 / 副作用 |
| --- | --- |
| 完整previous pins、缺owned hook | **134 / UI_OWNED_TRANSITION_HOOK_MISSING**；全部向量相同、0调用 |
| 仅previous release；缺state pin；重复state pin；inline-only state pin；state无value | 各 **134 / UI_OWNED_TRANSITION_IDENTITY_REQUIRED**；各全向量相同、0调用 |

共 **6/6** 独立控制，wrapper最终 **exit0**；日志 `temp_docs/owned-ui-intent-1b-review/replay.log`，结果目录 `temp_docs/owned-ui-intent-1b-review/1060eb45cf7f40029a78e125c667bdc7/`。`missing-owned-zero-effects-effects.json` 保存完整before/after原文，非只看退出码；两侧timer状态SHA均 `a95103289f90ea582ec42ab2631ce76d7552279750e6a9a17a4de0b45d320584`，publication pointer均 `b647ec15412ac35d2d20354d9c9d68c765379dbb4dfacdc63ad7b77bff6bcc1b`，stub调用文件两侧均不存在。目录集合也相等，因此没有把空hook.lock或空backup目录遗漏在文件hash之外。

### 受影响首次安装与恢复回归

另实际执行一次 `MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file scripts/VerifyReviewedWebProgressUiHook.cs -- --transition-only`，**31/31、外层exit0**，见 `temp_docs/owned-ui-intent-1b-review/transition-only.log` 和 `temp_docs/publishing-integration/ui-hook-a98ef551dab54b86ace668c3e36dbc34/`。这是本轮重跑的窄fixture，不冒称独立新写31项或重跑原173全套。

11类intent负例原CLI均134且work/drop-ins/timer/control调用向量不变，覆盖single/missing/empty/no-value/inline和完整错误pin。真正无previous、无owned hook的 first install→restore为 **0/0**，恢复原字节与active偏好；无previous但存在未知同名hook明确拒绝且无写入。完整合法owned prepare→verified backup→install→rollback→restore为 **0/0/0/0/0**，记录ownedUpgrade=true、install停timer，rollback及restore精确恢复原bundle/hook和active偏好。没有执行真实线上publication、systemctl或portal；loopback仅为自己的BCL fixture（端口33253），结束后确认无监听。

### 源码判断与接受范围

- intent由任一previous选项（含inline形式）的出现决定，缺/重复/无value身份和缺hook的拒绝在work创建、lease、LoadState及其它systemctl之前；没有路径名或probe特判。完整但错误的pin继续走既有只读binding拒绝，不再因hook缺失回退first install。
- 原旧release/真实工具/review、完整drop-ins/effective argv、producer/portal路径、新frozen release及current-before-upgrade整包的binding代码仍在owned分支，未被删减或改为宽松legacy推断。真正first install是明确无previous且无existing owned hook。
- restore/resume先从对应work取lease并RequireLoadedTimer，随后仍根据持久 `state.ownedUpgrade` 执行严格backup/publication/恢复校验；省略previous参数不能把已记录的owned恢复降为legacy。lease/timer检查位置的差异已由first及owned restore覆盖；publication-match/resume的原d9有限独立控制继续适用，本轮没有再次声称其新执行。
- Publish、Freeze、Release helper、props、validator/publishing/metadata生产文件与d9 **逐对象相同**。因此原22项owned、20项legacy只读身份、3项固定Git冻结的有限结论维持；仍须下一次freeze显式使用固定1b工具对象，不能从editable tree借代码，也不能拿旧工具d9本身当已接受版本。

本接受允许协调端继续准备下一次UI-only升级：实际UI需另有固定accepted commit/review/dist hash，重新捕获和逐文件校验当时served完整bundle，按writer-idle、独立新release/work、publication-match和浏览器门安排实际操作。它不授权现在安装hook、stop/resume timer、上传或rollback，不代签UI布局集成。原1dc A+B代码接受、当前已上线UI424和未来hosted旧hook冲突/同slug健康回滚部署gate保持各自范围。

### 本轮证据与交付

| 文件（前缀 `temp_docs/owned-ui-intent-1b-review/`） | SHA256 |
| --- | --- |
| `replay.log` | `c19ee6c4b85792c94fe52f8a4af79f8ec5c17a936176db08c818640060b4ed07` |
| `1060eb45cf7f40029a78e125c667bdc7/receipt.json` | `ded36ec17f3271c4208a68bc8db7b63c18dc9a68c6caa5ee0407cf40976de3c1` |
| `1060eb45cf7f40029a78e125c667bdc7/missing-owned-zero-effects-effects.json` | `d82098d972b9d770b6c893b1b603ebefe79172f04f71350cdb72d04235babcd4` |
| `transition-only.log` | `a1d224332c3c8780ac65bbda6ff8282c21dc923594a63cc97d57c6cde256b5ca` |

持久wrapper源码SHA256=`54e649fa1f61207981ba65fbb60c9d68471e9134a222354a21da374d1814b569`。作者已有31日志SHA=`00d7036159f52ee294c24a52a8ec8a4e31131c39642eb3f697f81b3823e304fd`、before/after receipt `56c7f0ba... / cdfdfbea...`仅为只读引用，和本轮独立证据区分。wrapper第一次复制input漏了独立stub.json，原probe在候选CLI前退出134、未产生原probe receipt；原日志保留为 `replay-input-setup-attempt.log`，修正自己的input准备后用新目录完成。没有修改原probe、产品或正式期待来通过。

所有dotnet子进程带MSBUILDDISABLENODEREUSE=1；全BCL，无PK、AOT、347/173大全套、Windows、性能或新worker。没有真实systemctl/live/portal/hostproxy/main修改；既有working trees和gates保留。第一步docs/repro提交 `7f6a55ea276d533072851edb39ef5a43295d5bed` 已推送并核remote SHA，最终接受附节随后推送并核remote HEAD，交接以最终review SHA为准。
