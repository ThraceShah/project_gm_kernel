# GM Kernel 进展工作台只读托管服务（web/host）与升级/回滚对接

本文档描述 `gm-kernel-progress` 工作台的持久只读静态托管：一个 .NET 10 minimal ASP.NET Core/Kestrel 产品服务（`web/host/`），用于承载**全量已验证静态数据**（不受栖点门户 32 MiB 静态上传上限约束），以及配套的不可变 UI release 准备/原子激活/回滚脚本、systemd 单元模板与迁移回滚计划。线上 slug、路径与认证范围保持不变：`/apps/gm-kernel-progress/`，门户原有 strip-prefix 与访问控制/MFA 均保留。

本方案不改调度候选实现（`WebProgressScheduler` / `WatchWebProgress` / `WebProgressDataValidation` / `PublishWebProgressPortal`），不写数据库或通用发布后端；data cache 仍由发布链校验后原子切换 `index.json`，host 只读它，不当构建 worker。

## 1. 背景与目标

- 持久调度将覆盖 347 个已适配 case；实测证据体量约 0.2–0.9 GB，约 50–55 个轻量 case 后即超过门户 32 MiB 静态上传上限（按解码后字节计，压缩无法绕过）。
- 目标：新增可持久承载全量已验证静态数据的只读 host，容量不再受上传限制；不丢任何 case 几何/证据，不削 case、不改坐标精度、不删被引用 run；完整历史 cache 全部保留。
- UI 构建（vite `emptyOutDir` 会清空 `web/dist`）与服务解耦：host 只服务**受控不可变 UI release**，构建期间线上不受影响，激活原子切换，失败继续上一版本。

## 2. 组件

| 组件 | 文件 | 职责 |
| --- | --- | --- |
| 产品服务 | `web/host/`（`GmProgressHost.csproj`、`Program.cs`、`SiteState.cs`、`SafePaths.cs`） | 只读静态服务：两个 safe public 根、GET/HEAD、MIME/nosniff、缓存策略、流式、路径/符号链接防护、`/healthz` |
| Release 准备 | `scripts/PrepareHostedWebProgressSite.cs` | 构建 UI（或 `--no-build`）→ 校验 dist → 拷贝为不可变 release 快照（排除 `data/`）→ 逐文件 SHA256 清单；内容派生 ID，重复构建幂等 |
| 原子激活 | `scripts/ActivateHostedWebProgressSite.cs` | release 校验 + data 闭包 gate → 单指针 `site.json` tmp+rename 原子切换；`--expect-run` 护栏、`--rollback` 回滚、同 run 跳过 |
| 共享契约 | `scripts/HostedWebProgressSite.cs`（经 `scripts/Directory.Build.props` 的 `UseHostedWebProgressSite=true` 引入） | 闭包 gate、symlink 扫描、release 快照/校验、原子指针写入 |
| 实质验证 | `scripts/VerifyHostedWebProgressSite.cs` | 端到端验证（HTTP 合同/安全负例/容量流式/激活原子性/重启持久），日志在 `temp_docs/web-progress-hosting/logs/` |
| 单元模板 | `scripts/systemd/gm-kernel-progress-host.service` | 持久 user systemd 应用单元（%h 路径、资源限制、journal 日志） |

浏览器验证复用 `web/tests/smoke.mjs`（新增 `SMOKE_BASE_URL` / `SMOKE_DATA_DIR` 环境覆盖），继承其全部实质断言（双侧真实几何像素/拾取 ID、失败微值、移动布局、console 无异常），不重复发明。

## 3. 服务契约

### 3.1 两个 safe public 根

1. **UI release**：`<site>/ui-releases/<releaseId>/`（由 Prepare 生成的不可变快照，不含 `data/`）。
2. **validated data cache**：激活指针记录的绝对路径（如专用 checkout 的 `temp_docs/web-progress-publish/data`），含 `index.json` + `runs/<id>/...` 全量 mesh/evidence（含跨 run `../<runId>/...` 引用）。

除这两根外不服务任何内容：不从仓库根提供目录索引，不暴露 `temp_docs/` 其他日志、Parasolid 库/schema/SDK、任何密钥文件、`.git` 或源码。

### 3.2 路由与方法

| 请求 | 行为 |
| --- | --- |
| `GET`/`HEAD` `/healthz` | `200 ok` + `X-Gm-Ui-Release`/`X-Gm-Data-Run` 响应头；无激活指针时 `503` |
| `GET`/`HEAD` `/data/...` | data 根文件；`/data`、`/data/` 目录请求无索引，无目录 listing |
| `GET`/`HEAD` `/*` | 当前 UI release 文件；`/` → `index.html` |
| 其他方法 | `405` + `Allow: GET, HEAD`（无任何写/上传/API endpoint） |
| 缺失/被拒路径 | `400`（非法路径）/`404`；错误响应同样带 `nosniff` |

- MIME：`FileExtensionContentTypeProvider` 成熟映射 + `.log`→`text/plain`；未知扩展名不服务（`ServeUnknownFileTypes=false`）；全部响应带 `X-Content-Type-Options: nosniff`。
- 缓存：`index.json` / `manifest.json` / `collection-manifest.json` / `index.html` → `no-cache`（新激活立即生效）；runs 下 mesh/evidence/日志 → `public, max-age=31536000, immutable`（run 目录不可变）；UI `assets/*`（vite 内容哈希命名）→ immutable；其余 UI 文件短缓存。
- 流式：由 ASP.NET Core 静态文件中间件（内核 send-file）按文件流式输出，不会整包载入内存；实测64 MiB 资源传输期间 host VmRSS ≈ 63 MiB。

### 3.3 安全模型（host 独立防护，不信任“已验证”）

- **严格字符 allowlist**：请求路径仅允许 `[A-Za-z0-9._-/]`，`.`/`..`/空段一律拒绝；百分号（双编码残留）、反斜杠、冒号、控制字符、非 ASCII 全部拒绝。覆盖路径穿越、双编码、非法分隔符。
- **逐路径组件符号链接拒绝**：解析后的路径每个组件（含最终文件）检查 reparse point；激活后植入的符号链接也无法被服务（验证含此负例）。闭包 gate 同时全树扫描符号链接。
- **闭包 gate（激活前强制）**：对 index 注册的**全部** manifest 递归收集引用（steps before/after/tools/afterBodies、checks.evidence、case evidence），包括跨 run `../<runId>/...`，逐条限定在 data 根 `runs/` 树内、必须存在、扩展名仅 `.json`/`.log`；缺引用即拒绝激活（对齐调度发布端“目标 Validate 后再原子 index”的最终合同，不只验证最新 mesh）。验证含受控负例：源数据有效但目标缺跨 run 引用 → 拒绝。
- **并发替换**：run 目录按合同不可变（发布端 staging+rename）、release 不可变、指针原子替换；host 读取期间不存在半成品可见窗口。

## 4. 激活语义与状态分离

- **单指针原子切换**：`<site>/site.json` 同时记录 `uiRelease`/`uiHash`/`dataRoot`/`dataRunId`/`dataSha`，tmp+rename 写入；请求方永远只看到完整旧版或完整新版（验证含 4 读者并发 12 次翻转的原子性断言）。
- **失败保留旧版本**：任何校验失败（release 缺失/校验和不符、data 缺引用/含符号链接、`--expect-run` 不符）在指针写入前终止（退出码 3），旧激活继续服务；重试复用已验证 cache，不重跑几何。
- **同 run 跳过**：release+data 身份与当前指针一致时输出 `ACTIVATE_SKIP_SAME` 秒级跳过。
- **回滚**：`--rollback` 重新激活 `activation-state.json` 中记录的前一激活对（旧 release 与历史 run 均保留）。
- **状态文件三分**：生成状态 `watch-state.json`（调度/watcher）、`publish-state.json`（lastPublishedRun/SHA，portalctl 发布路径）、激活状态 `activation-state.json`（host）互不合并，语义各自独立。
- host 进程无需重启即感知新指针（每请求 stat 指针文件）；重启后仍读取最后激活版本（验证含 SIGKILL 重启断言）。

## 5. systemd 单元、资源限制与日志

模板：`scripts/systemd/gm-kernel-progress-host.service`（user 级，路径用 `%h`）。

- `WorkingDirectory=%h/code/project_gm_kernel-progress-publisher`；`ExecStart=dotnet run --project web/host -- --site %h/.local/share/gm-kernel-progress-host/site --listen 127.0.0.1:8790 --listen 100.97.117.95:8790`。
- **绑定**：仅 qingyu2 的 Tailscale IP（`100.97.117.95`，以 `tailscale ip -4` 实测为准）+ loopback 健康探测；host 拒绝 `0.0.0.0`/`::` 通配绑定；不增公网端口、不改全局 tailscale/nginx/防火墙。端口 8790（以部署时 `ss -tln` 复核空闲为准）。
- **资源限制**：`CPUQuota=100%`、`MemoryHigh=384M`、`MemoryMax=512M`（流式实测 RSS ≈ 63 MiB）、`TasksMax=128`、`Restart=always`、`RestartSec=5s`、`TimeoutStopSec=30s`。
- **日志**：`journalctl --user -u gm-kernel-progress-host`；访问错误/激活指针内容可通过 `/healthz` 响应头核对。
- 验证语法：`systemd-analyze --user verify scripts/systemd/gm-kernel-progress-host.service`（本机通过）。
- host 不持有任何凭证；portalctl 发布 token 仍仅由其既有文件引用（不出现在 source/chat/log）。无需 linger 细节变更：部署机沿用现有用户会话/linger 策略。

## 6. 部署迁移步骤（独立 GPT review 通过后执行；本文不触碰 live）

前置：本分支合入 main 并推送；live 专用 checkout 由既有刷新单元 `git merge --ff-only origin/main` 取得 `web/host/` 与新脚本；旧 timer/portal 在切换前持续工作。

```sh
# 1) 专用 checkout 快进到含 web/host 的 main（既有刷新单元的 ExecStartPre 已做；手动等价）
git fetch origin main:refs/remotes/origin/main && git merge --ff-only origin/main

# 2) 构建 UI 并准备不可变 release（替换原 PrepareWebProgressSite 的 dist 语义；无大文件上传）
MSBUILDDISABLENODEREUSE=1 dotnet run --file scripts/PrepareHostedWebProgressSite.cs -- \
  --web ../web --site $HOME/.local/share/gm-kernel-progress-host/site --builder pnpm

# 3) 首次原子激活（validated data cache 目录以实际部署路径为准）
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

生成状态与 lastPublishedRun/SHA 仍分开；激活失败下轮重试已验证 cache，不重跑任何用例。

## 7. 回滚计划

1. **host/激活层**：`dotnet run --file scripts/ActivateHostedWebProgressSite.cs -- ... --rollback`（回到上一激活对）；或显式 `--release <旧id>` 指定旧 release（`ui-releases/` 全部保留、数据历史 run 全部保留）。
2. **门户层**：如需退回静态快照形态，用既有 `PublishWebProgressPortal.cs`（其 bundle 预算逻辑保留）或最近一次 `portalctl static` bundle 重新发布同一 slug；随后 `systemctl --user stop gm-kernel-progress-host` 可选。
3. **单元层**：`systemctl --user disable --now gm-kernel-progress-host`；旧刷新 timer/发布链从未停止，随时可整体回退。
4. 数据层无需回滚：validated cache 只追加、`index.json` 原子替换由发布链管理，被聚合引用的 run 永不丢弃。

## 8. 容量与已知边界

- 全量 347 case 的证据 cache（0.2–0.9 GB 量级）由该 host 直接服务，无 32 MiB 限制；UI release 快照 < 1 MiB。
- 校验 tail：发布链对整个 data 根的契约校验随 run 数呈 O(n²) 读盘（调度文档已记录）。host 侧闭包 gate 只做引用闭包/包含性/符号链接检查（实测 4 run、89 MiB、3502 引用的完整 gate + 激活 < 0.4 s），不做全量 JSON schema 重校验；未做增量模式扩展。
- 463 条占位 manifest 的每批固定开销（~1.1 MB/批）未优化，属调度侧议题。
- release 不自动清理（每份 < 1 MiB）；如需清理，人工删除旧 `ui-releases/<id>*`（保留当前指针指向的与上一份，回滚材料即够）。

## 9. 验证证据（本分支，qingyu2）

- `scripts/VerifyHostedWebProgressSite.cs`：**53/53 通过**（日志与逐条命令/退出码：`temp_docs/web-progress-hosting/logs/verify-hosting-log.txt`、`verify-commands.txt`）。要点：
  - 真实已验证 cache（live publisher cache 快照，3 run/28 MiB/3495 引用）→ manifest → 双侧 gm/pk mesh/evidence HTTP 内容逐字节一致；多 run 数据集 608 文件/89 MiB 全量流式 GET 校验和一致。
  - 容量受控资源 64 MiB（>32 MiB）流式 GET：Content-Length/校验和一致，传输期间 host VmRSS ≈ 63 MiB（服务容量测试，非图形 case 声明）。
  - 安全负例 23 项（穿越/双编码/非法分隔/绝对路径/repo/SDK/token/.git）全拒；激活后植入符号链接404；GET/HEAD only（405+Allow）、无目录 listing、MIME+nosniff、index/manifest no-cache、mesh/asset immutable。
  - 原子性：release 校验和、失败保留旧根、data 校验失败不切 index、同 run 跳过、失败重试不重跑、4 读者 × 12 次指针翻转无撕裂、SIGKILL 重启后仍服务最后激活版本、源 cache 全程只读（树指纹不变）。
  - 跨 run 引用按浏览器相对路径解析落于 `runs/` 树内且内容一致；受控“缺跨 run 引用”被激活 gate 拒绝（对齐 S7 修复合同）。
- `web/tests/smoke.mjs`（继承断言，指向临时 host）：**145/145 通过**，截图 `temp_docs/webui_v2_*.png`、日志 `temp_docs/webui_v2_smoke_log.txt`：真实双视图模型（非仅 canvas）、拾取实体 ID、失败微值（Inertia YZ 2.01065554e-7 > 1e-7 等）与移动布局均可用，console 无异常。

## 10. 运行依赖

- 部署机（qingyu2）：.NET 10 SDK（`dotnet run` 构建/运行 host 与脚本）；node/pnpm（仅 UI 构建期）；systemd 用户实例（沿用现有 linger 策略）；Tailscale IP 存在。
- 门户机（qingyu1）：`portalctl proxy` 同 slug 指向 qingyu2 Tailnet origin；无新增组件。
- 不需要：数据库、上传接口、公网端口、全局 tailscale/nginx/防火墙变更、新凭证。
