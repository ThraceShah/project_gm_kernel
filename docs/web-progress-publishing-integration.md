# Scheduler + hosting 隔离整合

本任务独立工作树为 `../gm-publishing-integration-feature`，分支 `feat/web-progress-publishing-integration`。开始及执行前实际 fetch 的 origin/main 均为 `610e3ce844bf99c85af6069905f9f3deaab08133`；没有合入 main、重写作者历史、force push、接入 ledger/R3/UV 或迁移 hosting/proxy。

| 输入 | 核实的完整 SHA / 范围 |
| --- | --- |
| scheduler accepted | `5ae2cb53573dda56533b3ddb7ec46b5fc878edd0`，独立报告 `dce5a3b29c0a4b0ccce5952f8b8c057ae873df91` |
| hosting accepted | `618144f1381cc14d98cc0c9dd582e52fc4b81a09`，独立报告 `548a1198583daa3b26a37fc1fa5c8b81ae6b2888` |
| merge-base | 两者均为 main610；scheduler 仅 11 个预期调度/发布/文档文件，host 仅 15 个预期脚本/host/文档/smoke 文件 |
| 合并 | `cbb7808`、`242ac1e` 两次保留历史的 merge；唯一冲突为 scripts props。validation/scheduler 共用一份 validator、SafePath、publishing、metadata include；host 独立条件 include |
| 接口修复 | `c60ad196b324ef1af7204552c6cfd7ac7798dede`，后续追加 fixture；不修改内核、C exports 或几何阈值 |

原 accepted 结果及所有原工作树/ignored 证据保留。UI-only 后续授权与发布独立记录在 `docs/web-progress-ui-only-release.md`、`docs/web-progress-ui-424-deployment.md`；UI 发布不代表本 A+B 候选已独立 review 或可迁移部署。

## 接口合同

- `PublishClosure` 生成完整 indexed 与跨 run mesh/evidence 闭包；坏而未 indexed 的 manifest 可作为 raw evidence 保留。source/publish 根不得互相包含，目标既存树、index.tmp 的链接在复制/删写前拒绝。已有 run 只能按同名同 hash 重用，不覆盖旧证据。
- Watch 的 queue/state、source/output、publish 根分离；启动与直接 Refresh 都检查写目标祖先、runs、index.tmp，包含 dangling symlink。失败 pending/reason/time 仍由 scheduler state 持久保存；host 不写 queue 或静态发布状态。
- Activate 捕获一次 exact index，整个 CAS generation 与 dataSha 使用同一快照；对象 hash 和写入使用同一 byte[]，避免 hash 后再次复制源文件。切换 authority 前验证完整 generation 身份/闭包，后续 source cache 写入不泄露到 active。`PublishHostedWebProgressSite.cs` 为串行 post hook，固定已观察 index，无 portal 或 collector。
- `site.json` 为单一权威提交点，新提交含 schemaVersion=1；activation-state 为 schema1 派生镜像。mirror 不得与 site/source 重叠，也不得覆盖 scheduler、legacy watch、portal 等已识别状态；损坏镜像仍可从权威恢复。source、dist、site 的安全根/全部祖先与将被删写的子树必须真实存在或可安全创建，不跟随链接。
- UI release、CAS generation/object 保留；旧 hashed asset、跨 generation run refs 仍 GET 可寻址。坏 rollback 目标在 authority 提交前拒绝，保留 current 全文/镜像/HTTP bytes；有效 rollback 精确恢复旧 index、UI、refs。

## 实际验证（qingyu2 / .NET 10.0.302 / BCL）

所有 build/run 子进程设置 `MSBUILDDISABLENODEREUSE=1`，串行构建。没有 dotnet test/testhost、NativeAOT、真实 PK、347 全量或 145 浏览器全套；因此没有全局 pkill。BCL 环境正常，不需 private/native ensure；任何未来私有依赖缺失应归 ENV_NOT_READY，不归几何失败。

| 门 | 原 exit / 结果 / 证据 |
| --- | --- |
| scheduler 原门 | 0，145/145；`temp_docs/publishing-integration/scheduler.log`，发生在首次接口补强阶段，后续纯路径/快照小修由下列真实 driver/E2E 验证，不冒称最后候选重跑145 |
| driver 原门 | 0，31/31；`temp_docs/publishing-integration/driver.log`；后补直接 Refresh 写根保护由最终 E2E 覆盖 |
| validator 最终门 | 0，13 项；`temp_docs/publishing-integration/validator-final.log`；把正常与 binary/symlink 负例的目的根改为 source 的 sibling，保留原语义并新增 overlap 拒绝，避免 overlap 抢先掩盖旧负例 |
| 最终真实 E2E | 0，75/75；`temp_docs/publishing-integration/integration-final.log`，`fixture-0f20b35a89a949f092cb3b3bf19b7714/` |
| 受影响 host 原门一次 | 0，86/86；`temp_docs/publishing-integration/hosting-gaps.log` 及 `hosting-gaps/logs/gaps-all.log` |
| 独立入口 smoke | 0，5/5；Refresh --list=0、Watch 空选择=0、旧 Prepare no-build=0、Portal 缺 data=1、cutover --help=0；未调用 portal，见 `cli-smoke.log` 和各 `cli-*.log` |
| 新 hook 实际入口 | success/failure/recovery 原 exit 0/3/0；Prepare/Activate 在 E2E 独立编译运行；unit companion 和 cutover 编译/help=0，不能当运维模式验收 |
| UI 备份补门 | 0，14/14；`ui-backup.log` / `ui-backup-fc0a3064d12c40c69bdf18a6a17e0b48/`；与 A+B 验收分开 |
| UI unit PATH stub | 0，31/31；`ui-hook-stub-final.log` / `ui-hook-f14aebcd125e46e08afa8cac0610cab2/`；不是实际 systemd 测试 |

最终 E2E 用固定 nested Git source 与显式 BCL capture fixture，真实 Watch→Refresh→失败持久 pending→无 collector 重跑的 PublishClosure retry→Prepare→Activate→独立 host HTTP。capture 原 process exit7、`2.01065554e-7` 微值及真实来源不升级成几何成功；source script 与原 run SHA 映射前后相同。合法跨 run mesh、隔离坏 manifest/raw log 全部逐字节 HTTP 对照；source cache 后续写入不影响 active。mirror 故障提交、same-pair heal、旧 asset/跨代 run refs、坏目标 rollback exit3 的 authority/mirror/current HTTP bytes 不变、正常 rollback exact old index/UI/refs 均实际覆盖。

链接负例真实 exit134（未捕获的明确 InvalidDataException/IOException）：直接 Refresh 的 runs 祖先、Prepare 的 dist 祖先、把 scheduler/authority 当 mirror；拒绝前不写外部目标。不是把 134 改写成 3 或通过。坏 source activation=3，坏 generation rollback=3，正常 rollback=0。

自有 E2E host pid1509330 / loopback33331 在 finally 精确终止并 wait，原 exit137；host gap21865 的自有 process 同样由其 harness 清理。结束后两端口均无监听；没有清理其他 worker/pid/端口。第一次69项版本与早期 fixture compile/格式错误日志保留，后来75项是最终完整 E2E；早期错误不算内核/几何结果。

源码/构建产物 SHA 在 `temp_docs/publishing-integration/final-source-artifact-hashes.json`（入口 smoke 时的源；后续 UI 运维修复以其固定 tools commit/release.json 为准）。E2E 的 `source-hashes.json`、`artifact-hashes.json`、`source-validation.json` 和逐 CLI exit/log 完整保留。构建有既有 trim/AOT 分析及 SDK package NU1903 警告，未宣称 AOT 发布认证。

## 后续 A+B 部署 gate（本任务不执行此迁移）

`scripts/WebProgressHostedCutover.cs` 提供默认只打印计划、read-only check、显式 deploy/rollback 模式，完整参数见 --help。它要求专用 clean publisher 与实际 origin/main 的独立 accepted 全 SHA 一致；既有同 slug 静态 bundle 可原字节回退；用户 unit、loopback/Tailnet 固定端口空闲和既有门户健康；备份完整静态 bundle、drop-ins、host unit、timer active/enabled 与 host 原状态。部署包含 managed host 构建、local activation hook、用户 unit、loopback/Tailnet health、同 `gm-kernel-progress` slug proxy index 字节对照；失败保留备份，显式 rollback 先恢复静态门户再恢复旧服务/单元及偏好。其实际 check/deploy/rollback 均**未执行**，必须由后续独立 reviewer 核其 writer/任务状态与 rollback 外部编辑边界，不能因脚本存在或 help 编译就宣布部署 ready。

live publisher checkout、collector、原 PK/schema、hosting unit/proxy、旧 scheduler 状态迁移均未被 A+B 操作。后续仍需独立整合 review、专用 checkout/旧 queue 迁移演练、具体部署运维 review、同 slug health/真实模型/服务重启和 rollback gate。整合通过不等于整个平台、463 全适配、计算能力或性能目标完成。
