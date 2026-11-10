# Project GM Kernel 生产对比与进展工作台 (Web Progress UI)

本文档说明 `project_gm_kernel` 的生产 Web 进展与 GM / Parasolid (PK) 几何内核对比界面的架构、共享数据契约、真实数据接入、使用方式及自动化测试。

## 1. 架构与设计原则

该对比工作台为专业 CAD/CAM 几何建模内核对比分析工具，采用**浅色工程桌面 + 深色 3D 画布**的设计语言，以中文为主、优先保证可读性与信息密度。

### 核心特性
1. **多内核对称视口 (Dual Viewport)**:
   - 左侧: **GM Kernel** 实测结果与几何切片（展示几何生成与 Oracle Tessellation 来源信息）。
   - 右侧: **Parasolid (PK)** 官方基准 Oracle 参照结果。
   - 双视口相机联动（平移、旋转、缩放实时同步），并支持独立解锁或一键视角复位。
2. **严格真实离散快照 (Discrete Snapshots)**:
   - 支持多步骤（前态 Before / 后态 After / 布尔刀具 Tool）离散快照切换与拖拽时间轴、播放/暂停、多倍速控制。
   - 杜绝任何形式的手绘或虚假形变插值，每次快照均基于内核真实生成的 B-rep 离散网格。
3. **拓扑与实体角色色彩编码 (Entity Roles & Colors)**:
   - 选中边 (Selected Edge): 明黄 (`#facc15`)
   - 选中/支撑面 (Selected/Support Face): 蓝 (`#2563eb` / `#3b82f6`)
   - 新圆角/生成面 (New Blend Face): 绿 (`#16a34a`)
   - 其他修改面 (Modified Face): 橙 (`#ea580c`)
   - 删除面 (Deleted Face): 红 (`#dc2626`，仅存在于前态)
   - 未改动面/边 (Unchanged): 中性 CAD 灰色 (`#94a3b8` / `#334155`)
   - 支持射线拾取（Raycasting）：点击面或边显示其 Entity ID、Role、三角网格数量与拓扑属性。
4. **逐项验收与差异矩阵 (Itemized Acceptance Matrix)**:
   - 对比检查项：实测值 (Observed)、基准值 (Expected)、绝对偏差 (absDelta)、相对偏差 (normalizedDelta)、门限容差 (Threshold)、物理单位 (Unit)、状态 (Pass/Fail/Diagnostic/Missing)。
   - 容错规范：缺失数据严格显式为 `null` / `missing`，数值 `0` 不代表缺失，未识别检查项严禁自动判定为通过。
5. **性能度量 (Performance Benchmark)**:
   - 毫秒级耗时：GM Kernel 耗时 vs Parasolid (PK) 耗时。
   - 核心倍率：`ratioGmOverPk`（$GM \div PK$，数字越小代表 GM 越快）；比率为 `null` 时表示无有效倍率，严禁标为 0 倍。
   - 测量口径：按 manifest 真实字段显示 `measurementKind`（如 `single-cold-sample` 单次冷启动）、`samples` / `warmup`、`stableBenchmark` 与 `qualifiedScope`，不泛称“性能认证”或稳态性能结论。
   - 正确性资格：`correctnessQualified`（几何与拓扑强制验收项全部满足门限时才标记为具备正确性资格，避免错误几何的“虚假快速”；仅为正确性资格，非性能认证）。
   - 硬件与运行配置：真实记录宿主机 CPU 架构（如 Apple M4 Arm64 / x86_64）、操作系统、SDK/Runtime 版本与编译发布配置（如 Release Managed / NativeAOT）、测试样本数与预热轮数。
6. **自包含与门户兼容性**:
   - 静态构建 (`web/dist/`) 采用相对资源路径 (`base: './'`)，自包含 Three.js、React 与 Tailwind CSS，生产产物零外部 CDN 依赖，且构建时自动将真实数据包打包至 `web/dist/data/`。
   - 部署至栖点门户 `/apps/gm-kernel-progress/`，完全兼容二级路径与直接 HTTPS 访问。

---

## 2. 真实数据接入与现状

当前前端已整合多批真实基准数据（来源分支 `origin/web/progress-data`，最新 commit `18e67aa`），根索引 `web/data/index.json` 含三份历史 run，UI 可切换并真实展示各自统计；非最新 run 在选择器中显式标注“·历史”，旧 SHA 结果不会被误读为已重验。

持续刷新服务（见 3.4 节）在专用发布 checkout 中生成的 Linux run 会原子替换发布包数据；当前线上门户服务的最新 run 为 `20261110T191615Z-4e60884b`（源码 SHA `4e60884b`，Ubuntu 26.04 / AMD Ryzen 7 7840HS / .NET 10.0.302，14 pass / 3 fail / 330 missing / 116 展示/导出未适配，结论与 Mac run 一致）。仓库 `web/data/` 保留 Mac 历史 run 供追溯。

### 2.1 最新真实数据统计（Run `20261110T180347Z-90acd4a9`）
- **对应内核 Commit**: `90acd4a9d7e766cdaa09e2ea4fb2cf7bf5d2aa45`（固定构建 SHA），17 代表用例约 442 秒跑完
- **Parasolid Oracle 版本**: `38.0.150`
- **宿主环境**: Apple M4 / macOS 27.2.0 (Arm64), .NET 10.0.11 Release（run.host / run.provenance 记录 PKToy commit 与源码哈希）
- **注册用例数**: 463 项（registry `2026-11-11.default-recipes-v5`），其中 347 项具备展示/导出适配器
- **本 run 实际执行**: 17 项（scope.case=representatives，单用例 300s / 总 1200s 预算）
- **真实网格与三角片**: 124 个真实几何切片文件，共 30,922 个三角面片（GM 15,948 / PK 14,974），920 项逐项验收检查、26 个步骤快照
- **通过 (Pass)**: 14 项（含 `solid-blend.plane-nurbs`、`blend-chain.chain-2-base-self-source`、`support-chain.asymmetric43-base`、`trivalent-corner.base-self`、`planar-boolean.overlap-two-axes-subtract`、`mixed-analytic.basic-subtract` 等，均含双内核真实三角网格与逐项严格对比）
- **失败 (Fail)**: 3 项（ratio 严格置空，原进程退出码保留为结果数据）:
  1. `local-blend.block-single-fillet`: 原进程 exit 0，但新增 strict compare 失败（`Type, regions and strict BODY_compare`: web strict fresh XT body geometry/structure/sense/tolerance/matching differs）。
  2. `short-edge.standard-strict-lifecycle`: 原进程 exit 134，`Full BODY_check` 检出 bad_loops faults（note `[{"state":13826,...}]`）。
  3. `short-edge.transverse-witness-lifecycle`: 原进程 exit 134，原惯量 Inertia YZ/ZY 偏差 2.01e-7 超过原阈值 1e-7（accuracy=0.99999999）。
- **数据缺失 (Missing)**: 330 项（本次 run 未选中执行，证据缺失；不推断通过）
- **展示/导出未适配 (Unsupported)**: 116 项（manifest 真实字段：`capabilityScope=web-export-adapter`、`adapterStatus=not-implemented`、`kernelCapability=null`；含义是 web 快照/结构化检查适配器未实现，**不是内核操作不支持**，也未执行/未推断通过；内核能力未评估/未知，不得称为内核缺失）
- **性能测量口径**: `measurementKind=single-cold-sample`（samples=1 / warmup=0 单次冷启动）、`stableBenchmark=false`、`qualifiedScope="Current correctness gates only; no steady-state performance certification."`；UI 显示真实字段，不泛称“性能认证”或稳态性能结论，ratio 为 null 时不标 0 倍。

### 2.2 共享数据契约
数据目录统一位于 `web/data/`:
- 根索引: `web/data/index.json`
- 运行清单: `web/data/runs/<id>/manifest.json`（用例含 `capabilityScope` / `adapterStatus` / `kernelCapability` / `execution` / `performance.measurementKind` 等真实字段）
- 几何网格与切片: `web/data/runs/<id>/cases/<case_id>/sample-0/meshes/`
- 原始验证证据与日志: `web/data/runs/<id>/cases/<case_id>/sample-0/evidence/` 及 `oracle.log`

---

## 3. 构建与发布

### 3.1 本地安装与构建
```bash
cd web
pnpm install
pnpm run build
```
构建产物输出至 `web/dist/`，Vite 插件会自动将 `web/data/` 目录递归复制至 `web/dist/data/`。生产产物大小约 8.5 MB（数据包约 4.2 MB），包含约 60 个文件，远低于栖点门户 32 MB 与 5000 文件的发布限制。

### 3.2 自动化测试
运行基于真实无头 Chromium 的端到端测试（浏览器路径通过 `SMOKE_BROWSER_PATH` 配置或自动发现，不绑定特定 Chromium 版本），所有期望统计均从 `web/data/index.json` 与对应 run 的 `manifest.json` 实时读取，不硬编码用例数，数据批次更新不会使测试失效：
```bash
cd web
pnpm run test:smoke   # 本地 dist 真实断言
pnpm run test:portal  # 真实 HTTPS 门户发布验证
```
自动化测试的实质断言（非“canvas 存在”式检查）：
1. **真实双侧网格渲染**：左右视口的绘制统计（面/边/三角片数与角色分布）必须等于源 mesh JSON 的真实几何，且 WebGL 画布内模型像素超门限。
2. **before→after 真实变化**：前态黄色选边（`#facc15`）可见、后态新增绿色 blend 面（区域化绿色像素去噪后显著增长）可见，快照像素签名真实改变。
3. **可证拾取**：射线拾取返回的实体 ID 必须存在于真实 mesh JSON 的 face/edge ID 集合中，并显示真实角色标签与三角片数。
4. **双相机联动/独立/复位**：拖拽与缩放时另一侧跟随；解锁后不跟随；复位后几何保持（模型像素仍在）。
5. **播放真实切快照**：播放期间绘制角色分布真实变化、时间轴计数推进；暂停后画面冻结（非仅按钮状态）。
6. **快速切 case/run 不旧数据**：最终 bodyId/面数/三角数等于最终用例的源 mesh；run 快速往返后回到最新清单统计。
7. **404 错误无旧 geometry**：mesh 请求失败时旧网格清除（bodyId 清空）并显示错误提示。
8. **状态诚实性**：缺失/未适配/诊断/失败绝不标绿，比率为空（N/A，不标 0 倍）；最新 3 个真实 FAIL 的具体差异（bad_loops state 13826、原惯量 2.01e-7 超阈值 1e-7、strict BODY_compare 差异）、原阈值、API 参数（`PK_BODY_fix_blends` 等）与原进程退出码（exitCodes=[134]/[0]）均可见且证据文件可访问。
9. **性能口径诚实**：显示真实 `measurementKind=single-cold-sample`、samples/warmup、`qualifiedScope`，不宣称性能认证/稳态结论。
10. **产物卫生**：dist 无外部 CDN 依赖（仅允许 w3.org 命名空间与 react.dev 文档链接）、无 DEV FIXTURE 数据。
11. **布局验收**：桌面（1440×900）侧栏 320px、双视口并排不重叠、header 不横向溢出；窄屏（390×844）侧栏改抽屉、主画布宽≥250px、header 换行不溢出、图例默认折叠不遮模型、抽屉选 case 后关闭、GM/PK 切视图与 before/after、参数/差异/性能均可查看。
12. **run 切换真实统计**：三份历史 run 各自的真实统计独立展示，历史 run 在选择器标注“·历史”。

测试截图保存于 `temp_docs/`（每次运行重新拍摄；`real_smoke_*` 系列为修复前 grid-only 缺陷的历史证据，不作为成功依据）：
- `temp_docs/webui_v2_desktop_workbench.png`: 真实数据桌面工作台概览（双侧真实模型）
- `temp_docs/webui_v2_desktop_blend_before.png`: 倒角用例前态（黄色选边高亮可见）
- `temp_docs/webui_v2_desktop_blend_after.png`: 倒角用例后态（绿色新 blend 面与橙色修改面可见）
- `temp_docs/webui_v2_desktop_boolean_tools.png`: 布尔减运算（含刀具体展示）
- `temp_docs/webui_v2_desktop_fail_compare.png`: strict compare 失败的具体差异
- `temp_docs/webui_v2_desktop_fail_bodycheck.png`: bad_loops state 13826 失败详情
- `temp_docs/webui_v2_desktop_fail_numeric.png`: 原惯量数值/阈值失败详情（2.01e-7 vs 1e-7）
- `temp_docs/webui_v2_desktop_perf.png`: 性能口径（single-cold-sample / 非性能认证）
- `temp_docs/webui_v2_mobile_main.png` / `webui_v2_mobile_drawer.png` / `webui_v2_mobile_inspection.png` / `webui_v2_mobile_fail.png`: 390×844 移动布局、抽屉选 case、参数查看与失败数据
- `temp_docs/webui_v2_live_desktop.png` / `webui_v2_live_fail.png` / `webui_v2_live_mobile.png`: 真实 HTTPS 门户验证截图
- `temp_docs/webui_v2_smoke_log.txt` / `temp_docs/webui_v2_live_log.txt`: 测试断言日志

### 3.3 栖点门户正式发布 (Tailnet Publish)

发布命令（供自动发布 runner 衔接；`portalctl` 通过 PATH 解析）：
```bash
# 构建生产产物
cd web && pnpm run build

# 发布静态应用至栖点门户（同一 slug 覆盖更新，保留权限）
portalctl static \
  --slug gm-kernel-progress \
  --name 'GM Kernel vs Parasolid 对比工作台' \
  --description 'GM 几何建模内核与 Parasolid 真实对比与工程指标追踪工作台' \
  --dir web/dist
```

- **正式访问地址**: [https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)
- **门户主页收录**: [https://qingyu1-linux.tail77e06.ts.net/](https://qingyu1-linux.tail77e06.ts.net/) (已收录 `gm-kernel-progress`，状态在线)

### 3.4 持续刷新与发布（gm-kernel-progress-refresh）

用户级 systemd timer/service `gm-kernel-progress-refresh`（unit 模板来自 `scripts/systemd/`，部署方 drop-in 追加发布步骤）每 10 分钟检查并在专用 checkout 生成/发布，不在任何编辑/工作树中运行定时 git 变更：

- **专用 checkout**: `code/project_gm_kernel-progress-publisher`（独立 clone，只 `git merge --ff-only origin/main`，脏树则拒绝；PKToy submodule 固定 pin，PK 库复用 ignored `third_party/parasolid`）。资源限制：CPU 200% / MemoryMax 12G / KillMode=control-group / TimeoutStartSec=25min，单用例 300s、总预算 1200s，串行 PK 进程。
- **生成状态与发布状态分离**：watcher 状态（`watch-state.json`）仅在导出成功后推进，坏 run 不推进 state/index；发布状态（`publish-state.json`，由 `scripts/PublishWebProgressPortal.cs` 维护）独立记录 lastPublishedRunId/SHA，仅 portalctl 成功后写入；发布失败保留旧门户与已验证缓存，下轮直接重试发布而不重跑任何用例；同 SHA 重跑打印 `Source and registry unchanged` / `SITE_UNCHANGED` / `PUBLISH_SKIP_SAME_RUN`（秒级跳过）。
- **原子发布**：PrepareWebProgressSite 校验后将 validated 缓存装入 `web/dist/data`，portalctl static 原子快照发布同一 slug；仅发布已验证包，脚本失败/超时保留既有门户版本。
- **已知限制（诚实声明）**：timer 当前只执行 `--case representatives` 的 17 个代表用例（463 注册中的子集），其余 330 个 missing 用例**没有**自动入队/分批调度（registry 已含 cases/sourceCoverage 映射可供将来按新 main 变更优先排队，但队列调度器尚未实现）；结果按实际 scope 标注，不得以 17 代表用例冒充全项目验收；旧 SHA 结果在 UI 中显式标为历史。
