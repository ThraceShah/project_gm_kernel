# Hosting 目标代次闭包修复记录（2026-11-11）

实现分支 `fix/web-progress-hosting-gemini`，基准 `1b4efa0fac20515a2b846a5738000fdfa46de587`。本轮由 GPT 接手已有未提交补丁，保留接手时差异在 `temp_docs/web-progress-hosting/inherited-closure.patch`，未使用 Gemini 身份报告结果。这里只记录实现者证据，固定候选仍需独立 GPT reviewer 接受；没有合 main 或部署，线上继续原有版本。

## 原输入与基准复现

先读原报告 `717eac0`、`eca364d` 与新报告 `11617841d0bd910c67ebd5ddd03ef1645be26dbe`，只读复制原 `RollbackClosure.cs`、`RollbackValid.cs` 到 `temp_docs/hosting-closure-sol/`。两份原输入 SHA256 分别为：

- `RollbackClosure.cs`：`d21005e28ab11703cd8dd7be990eb9eef571bac2c97ab77ce717afbfc522d7ef`。
- `RollbackValid.cs`：`0434538148fa3e51545e02894ad93922eb437128b5729a98f4db414c0537871c`。

用基准 helper 与真实 Activate CLI 先复现：两份诊断原退出均为 **0**，helper 返回 true/errors 空，内部真实 rollback CLI **0**、`ROLLBACK_ACTIVATED`，HTTP index **200/r1**、r1 manifest/e.log **404**。`RollbackValid` 的两份源先经过原 validator，均 `errors=[]`。证据为 `temp_docs/hosting-closure-sol/baseline-rollback-closure.log`、`baseline-rollback-valid.log`；输入原文未改。

## 修复合同

`scripts/HostedWebProgressSite.cs` 的 `VerifyGenerationClosure` 验证目标 metadata 的 schema、id、必需 runs object，以及每个树记录的 schema、内容键、严格排序且无重复的路径映射和对象真实哈希；同一对象哈希在一次验证内去重。所有读取保留全链 symlink 拒绝。

从真实 index 快照读取必需 indexed runs，逐个解析对应树中的 manifest 对象，检查 run id/commit 绑定与所需 JSON 类型。遍历 before/after/tools/afterBodies、check evidence、case evidence，检查每个逻辑引用对应的 run/tree/file；跨 run 引用同样必须闭合。最终 run 集合等于 indexed 与 referenced run 的并集；index 实际哈希、metadata sourceLatestRunId 与由 index+映射派生的 generation id 均须匹配。

仅 indexed manifests 定义引用。未 indexed 的隔离 run 可以通过安全 raw log 被引用，目录中另有损坏 raw manifest 不会被提升为有效 indexed run。空 mapping 只有在实际 index 无任何 indexed/latest run 要求时才可能通过；有要求时必拒。缺 metadata/index 及错误 JSON 类型均转为清楚的 `ACTIVATE_REFUSED_GENERATION_CORRUPT`、CLI exit **3**，提交点前终止。

正常激活的 H1b source-correct CAS/tree/meta/index 修复和缓存验证路径、H4b 单一权威提交点与完整镜像恢复未改；host 流式、opened FD/no-follow、旧资产与旧 run 回退路径也未改。逐 GET 不增加全代哈希。

## 验证结果

所有 dotnet 命令设置 `MSBUILDDISABLENODEREUSE=1`，file-app 外层使用 `--no-cache`；仅 managed .NET 10，无 PK/NativeAOT、Web145、347 或 kernel 测试。临时证据均在 `temp_docs/hosting-closure-sol/`，下表日志路径相对于该目录。

| 验证 | 实际退出/结果 | 日志 |
| --- | --- | --- |
| 原 RollbackClosure / RollbackValid | 诊断 0/0；helper false，内部真实 rollback CLI 3/3，错误指出 r1 required mapping missing，index 继续 200/r2 | `final-rollback-closure.log`、`final-rollback-valid.log` |
| 原 gaps 47 + 首批 H1c 37 | 0，84/84 | `gaps-all/logs/gaps-all.log` |
| 最终 H1c（增加 index hash、tree hash 身份负控） | 0，39/39 | `gaps-h1c-final/logs/gaps-H1c.log` |
| host 53，旧 host 完整真实数据源 | 0，53/53 | `verify-original-data/logs/verify-hosting-log.txt` |
| host 53，本树较早快照 | 0，53/53 | `verify/logs/verify-hosting-log.txt` |
| 原 Probe | 0；未激活/被拒源不露出，精确 r1 回滚、延迟旧 asset 200、外部 marker 404、raw 编码负例 400 | `final-probe.log` |
| 原 Integrity | 0；CAS 修复后 HTTP 200 原始源字节/hash，source unchanged；双镜像故障后 count=3/3、previous 一致、STATE_REPAIRED | `final-integrity.log` |
| 原 PrepareInterrupted / 真实 Prepare 重试 | 0/0；INCOMPLETE_REBUILDING 后完整恢复 | `final-prepare-interrupted.log`、`final-prepare-retry.log` |

最终脚本共有原 47 与 H1c 39，即 86 个检查，以上分段日志分别保留实际执行计数。H1c 每个拒绝用例均断言 exit3/明确错误、pointer 全文（含 previous/count）及 mirror 全文不变，且当前 r2 index、manifest、log 仍可 HTTP 访问；末尾真实合法多 run 代次回滚恢复精确 index 字节和跨 run raw log。负例用真实内容键保存改变的 manifest/tree，分别断言具体缺引用/类型/身份错误，避免仅因陈旧 checksum 被拒就冒充闭包覆盖。

完整源沿用旧实现工作树 `../gm-progress-hosting/temp_docs/web-progress-hosting/fixtures/data-real` 的只读副本：3 run/3495 引用；容量集合 608 文件/89 MiB，64 MiB GET 完整 SHA 与 Content-Length 一致，观测 maxVmRSS **64 MiB**。本树较早快照则为 2139 引用、375 文件/80 MiB、maxVmRSS 65 MiB，两者分开记录。第二监听仅为 `127.0.0.2`，不宣称 Tailnet/跨机部署验证。RSS 是该资源规模的观测，激活导入仍逐文件读取，不作为一般常量内存证明。

自有 loopback 端口为 18893/18894/18896（原诊断）、18901（gaps）、18902（host）；只终止自己的子进程，无全局杀进程或 worker 操作。源码与实际 host DLL 哈希记录在 `source-artifact.sha`。完整回归保留旧 H1b cache/repair 与 H4b mirror 故障链，未降低原门槛或改历史输入。

## 剩余项

独立 reviewer 须按推送的固定 SHA 复审并决定是否接受。A scheduler 与 B host 的 main 整合、真实 Tailnet 同 slug 代理、持久 user unit、部署与回滚演练由协调端另行安排；本轮没有操作旧 feature 分支、其他工作树、live publisher、portal proxy 或用户 unit。
