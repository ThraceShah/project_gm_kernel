# GM Kernel 进展工作台只读托管服务（web/host）与升级/回滚对接

本文档描述 `gm-kernel-progress` 工作台的持久只读静态托管：一个 .NET 10 minimal ASP.NET Core/Kestrel 产品服务（`web/host/`），用于承载**全量已验证静态数据**（不受栖点门户 32 MiB 静态上传上限约束），以及配套的不可变 UI release 准备/原子激活/回滚脚本、systemd 单元模板与迁移回滚计划。线上 slug、路径与认证范围保持不变：`/apps/gm-kernel-progress/`，门户原有 strip-prefix 与访问控制/MFA 均保留。

本方案不改调度候选实现（`WebProgressScheduler` / `WatchWebProgress` / `WebProgressDataValidation` / `PublishWebProgressPortal`），不写数据库或通用发布后端；data cache 仍由发布链校验后原子切换 `index.json`，host **不直接读取该可变 cache**：激活时把已验证闭包导入站点自有的不可变代次（generation），host 只服务代次快照与对象存储，不当构建 worker。

## 1. 背景与目标

- 持久调度将覆盖 347 个已适配 case；实测证据体量约 0.2–0.9 GB，约 50–55 个轻量 case 后即超过门户 32 MiB 静态上传上限（按解码后字节计，压缩无法绕过）。
- 目标：新增可持久承载全量已验证静态数据的只读 host，容量不再受上传限制；不丢任何 case 几何/证据，不削 case、不改坐标精度、不删被引用 run；完整历史 cache 全部保留。
- UI 构建（vite `emptyOutDir` 会清空 `web/dist`）与服务解耦：host 只服务**受控不可变 UI release**，构建期间线上不受影响，激活原子切换，失败继续上一版本。

## 2. 组件

| 组件 | 文件 | 职责 |
| --- | --- | --- |
| 产品服务 | `web/host/`（`GmProgressHost.csproj`、`Program.cs`、`SiteState.cs`、`SafePaths.cs`、`SafeFiles.cs`） | 只读静态服务：两个 safe public 根、GET/HEAD、MIME/nosniff、缓存策略、流式、全链路径/符号链接防护、代次映射服务、`/healthz` |
| Release 准备 | `scripts/PrepareHostedWebProgressSite.cs` | 构建 UI（或 `--no-build`）→ 校验 dist → 拷贝为不可变 release 快照（排除 `data/`）→ 逐文件 SHA256 清单；内容派生 ID，重复构建幂等，中断自愈 |
| 原子激活 | `scripts/ActivateHostedWebProgressSite.cs` | release 校验 + data 闭包 gate → 导入不可变数据代次 → 单指针 `site.json` 原子切换；`--expect-run` 护栏、`--rollback` 精确回滚、同代跳过 |
| 共享契约 | `scripts/HostedWebProgressSite.cs`（经 `scripts/Directory.Build.props` 的 `UseHostedWebProgressSite=true` 引入） | 闭包 gate、symlink 扫描、release/代次快照与校验、对象存储导入、原子指针写入 |
| 实质验证 | `scripts/VerifyHostedWebProgressSite.cs`、`scripts/VerifyHostedWebProgressGaps.cs` | 端到端验证（HTTP 合同/安全负例/容量流式/激活原子性/重启持久）与 H1–H5 独立回归（一个持续更新 cache 的真实部署形态），日志在 `temp_docs/web-progress-hosting/logs/` |
| 单元模板 | `scripts/systemd/gm-kernel-progress-host.service` | 持久 user systemd 应用单元（%h 路径、资源限制、journal 日志） |

浏览器验证复用 `web/tests/smoke.mjs`（新增 `SMOKE_BASE_URL` / `SMOKE_DATA_DIR` 环境覆盖），继承其全部实质断言（双侧真实几何像素/拾取 ID、失败微值、移动布局、console 无异常），不重复发明。

## 3. 服务契约

### 3.1 两个 safe public 根

1. **UI release**：`<site>/ui-releases/<releaseId>/`（由 Prepare 生成的不可变快照，不含 `data/`），激活指针指定当前 release；保留的旧 release 有受限资产回退（见 3.4）。
2. **数据代次**：激活指针指定 `<site>/generations/<genId>/`（源 cache `index.json` 的**逐字节快照**）与 `<site>/data-store/`（内容寻址对象存储）。`/data/runs/<runId>/...` 经该代次的 `runId→文件哈希` 映射解析到对象存储中的精确副本；可变生成源 cache 不被 host 读取。

除这两根外不服务任何内容：不从仓库根提供目录索引，不暴露 `temp_docs/` 其他日志、Parasolid 库/schema/SDK、任何密钥文件、`.git` 或源码。

### 3.2 路由与方法

| 请求 | 行为 |
| --- | --- |
| `GET`/`HEAD` `/healthz` | `200 ok` + `X-Gm-Ui-Release`/`X-Gm-Data-Run` 响应头；无激活指针时 `503` |
| `GET`/`HEAD` `/data/index.json` | 当前代次的 index 快照（精确字节） |
| `GET`/`HEAD` `/data/runs/<runId>/<file>` | 代次映射 → 对象存储精确副本；映射外/未知文件 `404` |
| `GET`/`HEAD` `/*` | 当前 UI release 文件；`/` → `index.html`；旧 release 资产按 3.4 规则回退 |
| 其他方法 | `405` + `Allow: GET, HEAD`（无任何写/上传/API endpoint） |
| 缺失/被拒路径 | `400`（非法路径）/`404`；错误响应同样带 `nosniff` |

- MIME：`FileExtensionContentTypeProvider` 成熟映射 + `.log`→`text/plain`（对象按其公开 URL 名取 MIME）；未知扩展名不服务；全部响应带 `X-Content-Type-Options: nosniff`。
- 缓存：`index.json` / `manifest.json` / `collection-manifest.json` / `index.html` → `no-cache`（新激活立即生效）；mesh/evidence/日志对象 → `public, max-age=31536000, immutable`；UI `assets/*` → immutable；其余 UI 文件短缓存。
- 流式：字节自被打开的文件描述符以分块流输出（`SafeFiles`），不整包载入内存；实测 64 MiB 资源完整流式 SHA256 一致，传输期间 host VmRSS ≈ 63 MiB。

### 3.3 安全模型（host 独立防护，不信任"已验证"）

- **严格字符 allowlist**：请求的**原始 target**（服务端正规化前）与解码后路径都只允许 `[A-Za-z0-9._-/]`；百分号（编码点号/双编码残留）、反斜杠、分号、控制字符、`.`/`..`/空段一律拒绝。公开文件名永不需要百分号编码。
- **全链符号链接拒绝**：从 `"/"` 到最终文件的每个路径组件（含配置根自身与全部父组件、中间目录）都不得是 reparse point；替换任一服务根/父目录为外部链接不能改变可服务范围（每请求重新核验，链接替换后立即 `404`/拒绝）。闭包 gate 与激活导入同样全链+子树扫描。
- **检查与打开一致**：叶文件以 `O_NOFOLLOW` 打开（叶部链接交换导致打开失败），打开后再核验一次链，长度/字节均来自该已打开对象；不存在"检查后换对象"的静默窗口。**TOCTOU 政策**：同用户受信部署边界内不承诺对高速对抗式链接/文件交换的绝对免疫；上述逐请求双核验+nofollow 打开把窗口收敛到同用户竞态，超出该边界的对抗不在承诺内。
- **闭包 gate（激活前强制）**：对 index 注册的**全部** manifest 递归收集引用（steps before/after/tools/afterBodies、checks.evidence、case evidence），包括跨 run `../<runId>/...`，逐条限定在 `runs/` 树内、必须存在、扩展名仅 `.json`/`.log`；缺引用即拒绝激活。
- **代次隔离**：源 cache 的后续写入（新 index、改写 manifest、删除文件）不会改变已激活代次所服务的任何字节；可见性变化只发生在显式激活之后。

### 3.4 跨切换兼容（旧 HTML 完成加载）

- **UI 资产**：当前 release 未命中的文件，在**保留 release 的校验清单文件集**内按新旧顺序受限回退（仅清单内文件；`index.html` 与未知路径永不回退，避免陈旧文档/混合内容）。切换前已发出的 HTML 可完成其哈希资产请求；回滚后被切走 release 的资产同理可寻址。
- **数据引用**：保留代次的 run 映射并集内文件持续可寻址（活动代次映射优先）；`/data/index.json` 永不回退（原子指针语义）。
- **retention 规则**：release 与代次/对象默认全部保留（release < 1 MiB/份；对象按内容去重共享）；清理需人工按 `generations/*.meta.json`/`ui-releases/*.meta.json` 裁剪并保留当前+上一代。同 `runId` 内容演化（如聚合 manifest 改写）时：在线请求跟随**当前代次映射**；旧代次字节经回滚精确可观察；`runId` URL 不做多版本歧义回退。

## 4. 激活语义、代次与状态分离

- **不可变数据代次（generation）**：激活把已验证闭包导入站点对象存储——逐文件真实拷贝（**绝不硬链到可变源**）按 SHA-256 内容寻址存放（`data-store/objects/`），run 文件集绑定为不可变树记录（`data-store/trees/`），index 快照 + `runId→treeHash` 映射写入 `generations/<genId>/`（内容派生 ID，`gen-<hash16>`）。**文件复用规则**：相同文件跨代次按哈希共享一份对象；已存对象永不被声明为有效而不经校验。**复用完整性校验（H1b）**：每次激活按内容哈希验证全部触及对象（同进程去重，不为性能跳检查）、树记录必须精确匹配其内容键、代次记录必须绑定恰好本次 index 与 run 映射且 index 快照匹配元数据哈希才可 `GENERATION_CACHED` 复用；受控存储故障（对象/树/元数据/index 损坏、对象缺失）从仍有效的源拷贝**原子修复**（`STORE_OBJECT_REPAIRED/RESTORED`、`STORE_TREE_REPAIRED`、`GENERATION_INCOMPLETE_REBUILDING`），损坏内容绝不被当作正确数据服务或永久缓存。逐 GET 不做全量哈希（非目标）；声明的激活/复用完整性在激活时刻成立。**可信更新者边界**：仅 `ActivateHostedWebProgressSite.cs` 单写者写入 store/代次（staging+rename、只增、写前校验）；host 只读；同用户恶意篡改 store 不在承诺内，但下轮激活会检测并修复/拒绝，不默许。
- **单一权威提交点**：`site.json` 是唯一提交点，一次 tmp+rename 原子携带完整恢复记录：`uiRelease/uiHash`、`generation`、`dataRunId/dataSha`、`previous`（上一激活对）与 `activationCount`。**不声称两个文件的 rename 是事务**。
- **派生态镜像**：`activation-state.json` 是提交**之后**尽力写入的派生镜像；写失败显式输出 `ACTIVATION_STATE_MIRROR_FAILED`（激活本身已提交，退出码 0 且报告与实际 active 一致），下轮运行先按指针**重建完整派生态**再输出 `ACTIVATE_SKIP_SAME`——比较**全部契约字段**（active/previous/count，含 activationCount 与 previous 配对，H4b），同 pair 重试也能恢复完整历史/计数；skip 不掩盖修复，repair 不改真实激活计数、不制造新代。进程中断/失败后活跃代次、上一代与计数始终以指针为准且一致；恢复无需重跑任何几何。
- **失败保留旧版本**：任何校验失败（release 缺失/校验和不符、data 缺引用/含符号链接、`--expect-run` 不符、导入失败）都在提交点前终止（退出码 3），旧激活继续服务。
- **同代跳过**：release+generation 身份与指针一致时 `ACTIVATE_SKIP_SAME` 秒级跳过（幂等）。
- **精确回滚**：`--rollback` 从指针的 `previous` 恢复确切的 ui+generation 配对——相同 index 字节、相同 run 文件哈希，绝不重新解析"当前最新"；回滚前**全闭包校验目标代次**（树键+对象哈希+index 快照），受损则显式 `ACTIVATE_REFUSED_GENERATION_CORRUPT` 拒绝并保留可验证的当前激活（不声称损坏字节正确）；旧 release 与旧代次对象均保留，无需重建。
- host 进程无需重启即感知新指针（每请求 stat）；重启后仍读取最后激活版本。

## 5. Release 提交边界（Prepare 中断自愈）

- release 内容与其校验元数据构成**同一完整候选**：元数据原子写入在先、final 目录 rename 在后，final 目录永不会缺元数据出现。
- **缓存命中要求完整候选**：目录存在 + 元数据存在 + 校验通过才 `RELEASE_CACHED`（且非破坏性）；目录缺元数据视为中断残留（未经校验的目录不可能被激活），重试自动 `RELEASE_INCOMPLETE_REBUILDING` 重建，无需人工删缓存；合法已激活 release 不被破坏性覆盖。

## 6. systemd 单元、资源限制与日志

模板：`scripts/systemd/gm-kernel-progress-host.service`（user 级，路径用 `%h`）。

- `WorkingDirectory=%h/code/project_gm_kernel-progress-publisher`；`ExecStart=dotnet run --project web/host -- --site %h/.local/share/gm-kernel-progress-host/site --listen 127.0.0.1:8790 --listen 100.97.117.95:8790`。
- **绑定**：仅 qingyu2 的 Tailscale IP（`100.97.117.95`，以 `tailscale ip -4` 实测为准）+ loopback 健康探测；host 拒绝 `0.0.0.0`/`::` 通配绑定；不增公网端口、不改全局 tailscale/nginx/防火墙。端口 8790（以部署时 `ss -tln` 复核空闲为准）。
- **资源限制**：`CPUQuota=100%`、`MemoryHigh=384M`、`MemoryMax=512M`（流式实测 RSS ≈ 63 MiB）、`TasksMax=128`、`Restart=always`、`RestartSec=5s`、`TimeoutStopSec=30s`。
- **日志**：`journalctl --user -u gm-kernel-progress-host`；激活结果与镜像修复/失败事件逐行输出，`/healthz` 响应头可核对当前 ui/generation。
- 验证语法：`systemd-analyze --user verify scripts/systemd/gm-kernel-progress-host.service`（本机通过）。
- host 不持有任何凭证；portalctl 发布 token 仍仅由其既有文件引用（不出现在 source/chat/log）。沿用部署机现有用户会话/linger 策略。

## 7. 部署迁移步骤（独立 GPT review 通过后执行；本文不触碰 live）

前置：本分支合入 main 并推送；live 专用 checkout 由既有刷新单元 `git merge --ff-only origin/main` 取得 `web/host/` 与新脚本；旧 timer/portal 在切换前持续工作。站点根需位于无符号链接组件的路径（如 `%h/.local/share/...`）。

```sh
# 1) 专用 checkout 快进到含 web/host 的 main（既有刷新单元的 ExecStartPre 已做；手动等价）
git fetch origin main:refs/remotes/origin/main && git merge --ff-only origin/main

# 2) 构建 UI 并准备不可变 release（替换原 PrepareWebProgressSite 的 dist 语义；无大文件上传）
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/PrepareHostedWebProgressSite.cs -- \
  --web ../web --site $HOME/.local/share/gm-kernel-progress-host/site --builder pnpm

# 3) 首次原子激活（导入不可变数据代次；validated data cache 目录以实际部署路径为准）
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/ActivateHostedWebProgressSite.cs -- \
  --site $HOME/.local/share/gm-kernel-progress-host/site \
  --data ../temp_docs/web-progress-publish/data \
  --state $HOME/.local/share/gm-kernel-progress-host/activation-state.json

# 4) 安装并启动只读 host
systemd-analyze --user verify scripts/systemd/gm-kernel-progress-host.service
systemctl --user daemon-reload
systemctl --user enable --now gm-kernel-progress-host.service
curl -fsS http://127.0.0.1:8790/healthz   # 预期 ok + X-Gm-Ui-Release/X-Gm-Data-Run

# 5) 同 slug 切换门户代理（strip-prefix 默认、访问控制/MFA 不变；仅此步触碰 live）
portalctl proxy --slug gm-kernel-progress \
  --name 'GM Kernel vs Parasolid 对比工作台' \
  --target http://100.97.117.95:8790
# 验证 https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/ 与 /data/index.json
```

**发布 hook 替换**：把既有 portalctl static 发布 hook（部署方 drop-in 里的 `PublishWebProgressPortal.cs` 调用）换为 host-local 原子激活——在 `gm-kernel-progress-refresh.service` 的 drop-in 中以 `PrepareHostedWebProgressSite.cs` + `ActivateHostedWebProgressSite.cs` 取代 `PrepareWebProgressSite.cs` + `portalctl static`：

```ini
# 示例 drop-in（部署方放置到 ~/.config/systemd/user/gm-kernel-progress-refresh.service.d/10-host-activation.conf）
[Service]
ExecStartPost=
ExecStartPost=dotnet run --file scripts/PrepareHostedWebProgressSite.cs -- --web ../web --site %h/.local/share/gm-kernel-progress-host/site --builder pnpm
ExecStartPost=dotnet run --file scripts/ActivateHostedWebProgressSite.cs -- --site %h/.local/share/gm-kernel-progress-host/site --data ../temp_docs/web-progress-publish/data --state %h/.local/share/gm-kernel-progress-host/activation-state.json
```

生成状态（watch-state）与 lastPublishedRun/SHA（publish-state）仍分开；`site.json` 为激活权威记录、`activation-state.json` 为其派生镜像。激活失败下轮重试已验证 cache，不重跑任何用例。

## 8. 回滚计划

1. **host/激活层**：`dotnet run --file scripts/ActivateHostedWebProgressSite.cs -- ... --rollback`——从指针 `previous` 精确恢复上一 ui+generation 配对（同 index 字节/同 run 哈希；镜像损坏也可用）。或显式 `--release <旧id>` 指定旧 release；旧代次默认保留。
2. **门户层**：如需退回静态快照形态，用既有 `PublishWebProgressPortal.cs`（其 bundle 预算逻辑保留）或最近一次 `portalctl static` bundle 重新发布同一 slug；随后 `systemctl --user stop gm-kernel-progress-host` 可选。
3. **单元层**：`systemctl --user disable --now gm-kernel-progress-host`；旧刷新 timer/发布链从未停止，随时可整体回退。
4. 数据层无需回滚：源 cache 只由发布链管理；站点对象存储/代次只增，被激活引用的 run 永不丢弃。

## 9. 容量与已知边界

- 全量 347 case 的证据 cache（0.2–0.9 GB 量级）由对象存储承载，无 32 MiB 限制；导入只在激活时发生、按文件哈希跨代去重，同 SHA 队列推进不重复拷贝。
- 校验 tail：发布链对整个 data 根的契约校验随 run 数呈 O(n²) 读盘（调度文档已记录）；host 侧闭包 gate 只做引用闭包/包含性/符号链接检查（实测 4 run、89 MiB、3502 引用的 gate+导入+激活 < 1 s），不做全量 JSON schema 重校验；未做增量模式扩展。
- 463 条占位 manifest 的每批固定开销（~1.1 MB/批）未优化，属调度侧议题。
- release/代次/对象默认全部保留（见 3.4 retention 规则）；清理为人工操作。

## 10. 验证证据（本分支，qingyu2）

- `scripts/VerifyHostedWebProgressGaps.cs`（H1–H5 + H1b/H4b 独立黑盒回归，真实 CLI+host+HTTP，单一持续更新 cache 模拟 publisher 原子换 index）：**47/47 通过**（`temp_docs/web-progress-hosting/logs/gaps-all-after-H1b-H4b.log`）。修复前逐项先失败实证：`gaps-before-fixes.log`（H1–H5：未激活/被拒/被改写数据可见、根/父目录替换为外链即服务根外标记、旧 HTML 资产切换后 404、state 写失败 exit 134 且 skip 不修复、Prepare 中断留永久坏缓存）、`gaps-H1b-before.log`（损坏对象 HTTP 200 CONTROLLED_OBJECT_STORAGE_FAULT）、`gaps-H4b-before.log`（mirror count/previous 不恢复）；reviewer 原版 `Integrity.cs` 不改输入复跑对照：`temp_docs/hosting-rereview/integrity-before-fix.log`（fail）→ `integrity-after-H4b.log`（count=3/3、previous 完全一致、CAS 修复后 HTTP 200 为原始字节且 key hash 匹配）。
- `scripts/VerifyHostedWebProgressSite.cs`：**53/53 通过**（`verify-hosting-log.txt`）：真实已验证 cache 快照（3 run/28 MiB/3495 引用）HTTP 逐字节一致；多 run 数据集 608 文件/89 MiB 全量流式 GET 校验和一致；64 MiB（>32 MiB）受控资源流式 SHA256 一致、传输期间 VmRSS ≈ 63 MiB（服务容量测试，非图形 case 声明）；23 项穿越/双编码/repo/token 负例全拒；GET/HEAD only、无目录 listing、MIME+nosniff、缓存策略、4 读者 × 12 次指针翻转无撕裂、SIGKILL 重启持久、源 cache 全程只读。
- `web/tests/smoke.mjs`（继承断言，指向临时 host）：**145/145 通过**（`temp_docs/webui_v2_smoke_log.txt`、截图 `temp_docs/webui_v2_*.png`）：真实双视图模型、拾取实体 ID、失败微值（Inertia YZ 2.01065554e-7 > 1e-7 等）、移动布局可用，console 无异常。

## 11. 运行依赖

- 部署机（qingyu2）：.NET 10 SDK（`dotnet run` 构建/运行 host 与脚本）；node/pnpm（仅 UI 构建期）；systemd 用户实例（沿用现有 linger 策略）；Tailscale IP 存在；站点根路径无符号链接组件。
- 门户机（qingyu1）：`portalctl proxy` 同 slug 指向 qingyu2 Tailnet origin；无新增组件。
- 不需要：数据库、上传接口、公网端口、全局 tailscale/nginx/防火墙变更、新凭证。
