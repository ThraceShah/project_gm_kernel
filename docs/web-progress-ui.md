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
   - 核心倍率：`ratioGmOverPk`（$GM \div PK$，数字越小代表 GM 越快）。
   - 资格认证：`correctnessQualified`（几何与拓扑强制验收项全部满足门限时才标记为具备性能认证资格，避免错误几何的“虚假快速”）。
   - 硬件与运行配置：真实记录宿主机 CPU 架构（如 Apple M4 Arm64 / x86_64）、操作系统、SDK/Runtime 版本与编译发布配置（如 Release Managed / NativeAOT）、测试样本数与预热轮数。
6. **自包含与门户兼容性**:
   - 静态构建 (`web/dist/`) 采用相对资源路径 (`base: './'`)，自包含 Three.js、React 与 Tailwind CSS，生产产物零外部 CDN 依赖，且构建时自动将真实数据包打包至 `web/dist/data/`。
   - 部署至栖点门户 `/apps/gm-kernel-progress/`，完全兼容二级路径与直接 HTTPS 访问。

---

## 2. 真实数据接入与现状

当前前端已成功整合真实首包基准数据（来源分支 `origin/web/progress-data`，commit `cc1fe3f`）。

### 2.1 真实数据统计
- **基准 Run ID**: `initial-adapted-a1e4500`
- **对应内核 Commit**: `a1e45006c08491db85f4ef87ced6d236342b15e6`
- **Parasolid Oracle 版本**: `38.0.150`
- **宿主环境**: Apple M4 / macOS 27.2.0 (Arm64), .NET 10.0.11 Release
- **总注册用例数**: 414 项 (Blend 228 项, Boolean 186 项)
- **真实网格与三角片**: 32 个真实几何切片，共 14,992 个三角面片
- **通过 (Pass)**: 4 项（均含完整双内核真实三角网格与逐项几何/拓扑/质量属性严格通过对比）:
  1. `solid-blend.plane-nurbs`: 真实平面-NURBS 单凸边倒圆角，GM 耗时 1537.67ms vs PK 1.51ms，29 项严格检查全通过。
  2. `short-edge.a1-r0.5-t1e-07-reversefalse-default`: 严格 1e-7 容差门限倒角，29 项严格检查全通过。
  3. `planar-boolean.overlap-two-axes-subtract`: 平面多轴重叠布尔减运算，含 Tool 刀具几何，27 项严格检查全通过。
  4. `analytic-boolean.equal-overlap-subtract`: 解析曲面等厚重叠布尔减，含 Tool 刀具几何，27 项严格检查全通过。
- **诊断测试 (Diagnostic)**: 1 项:
  - `regular-curved.block-subtract`: 常规曲面块布尔减。因选择器曾命中变体实体，依契约保留完整 53 项比对证据但 ratioGmOverPk 置空，明确标为诊断态（天蓝色徽章），严禁标绿。
- **数据缺失 (Missing)**: 205 项（等待后续批次数据生成与适配）
- **内核暂不支持 (Unsupported)**: 204 项（明确记录尚未支持的用例范围）

### 2.2 共享数据契约
数据目录统一位于 `web/data/`:
- 根索引: `web/data/index.json`
- 运行清单: `web/data/runs/<id>/manifest.json`
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
运行基于真实无头 Chromium 的端到端测试，全面覆盖 13 项验收点：
```bash
cd web
pnpm run test:smoke
```
自动化测试验证要点：
1. 默认加载 414 项真实用例，无任何 Dev Fixture 污染。
2. 倒角 (228) / 布尔 (186) 分类与状态筛选（Pass 4、Missing 205、Unsupported 204、Diagnostic 1）及全文搜索。
3. 真实通过的 `solid-blend.plane-nurbs` 前态与后态 3D 双视角网格渲染与实体角色色标（新生成圆角面绿色 `#16a34a`，选中边明黄色 `#facc15`，修改面橙色 `#ea580c`）。
4. 3D 视口射线拾取（Raycasting）：点击面/边弹出实体详情、角色标签与三角片数。
5. 布尔运算刀具体（Tool Bodies）在 Before 态下的展示与 After 态下的拓扑重构。
6. Timeline 快照步进、自动播放/暂停、倍速切换（0.5x, 1x, 2x）与滑动条拖动。
7. 双视角相机联动（平移/缩放/旋转同步）与解锁独立控制、一键复位视角。
8. API 调用与参数结构化 JSON 视图。
9. 逐项验收检查矩阵（实测值、基准值、绝对偏差、相对偏差、容差门限与单位）。
10. 性能指标面板（Apple M4 宿主配置、毫秒耗时、倍率与正确性资格认证状态）。
11. 原始证据与日志文件的可点击跳转链接。
12. 严格杜绝伪造通过：Missing、Unsupported 与 Diagnostic 绝不标绿。
13. 快速切换用例/Run 与网络 404 错误防护：旧几何瞬间清除，过期异步响应被代数守卫丢弃。

测试截图保存于 `temp_docs/`:
- `temp_docs/real_smoke_desktop_workbench.png`: 真实数据桌面工作台概览
- `temp_docs/real_smoke_desktop_blend_before.png`: 倒角用例前态（含选中边高亮）
- `temp_docs/real_smoke_desktop_blend_after.png`: 倒角用例后态（含新生成圆角面）
- `temp_docs/real_smoke_desktop_boolean_tools.png`: 布尔减运算（含刀具体展示）
- `temp_docs/real_smoke_desktop_checks.png`: 逐项验收检查表
- `temp_docs/real_smoke_desktop_perf.png`: 性能度量与资格认证
- `temp_docs/real_smoke_desktop_params.png`: API 与参数详情
- `temp_docs/real_smoke_desktop_evidence.png`: 原始证据文件链接列表
- `temp_docs/real_smoke_desktop_diagnostic.png`: 诊断用例展示
- `temp_docs/real_smoke_mobile_narrow.png`: 移动窄屏自适应布局与视口切换
- `temp_docs/real_smoke_live_portal.png`: 真实 HTTPS 门户生产页面验证截图

### 3.3 栖点门户正式发布 (Tailnet Publish)

发布命令（供自动发布 runner 衔接）：
```bash
# 构建生产产物
cd web && pnpm run build

# 发布静态应用至栖点门户
~/.local/bin/portalctl static \
  --slug gm-kernel-progress \
  --name 'GM Kernel vs Parasolid 对比工作台' \
  --description 'GM 几何建模内核与 Parasolid 真实对比与工程指标追踪工作台' \
  --dir web/dist
```

- **正式访问地址**: [https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)
- **门户主页收录**: [https://qingyu1-linux.tail77e06.ts.net/](https://qingyu1-linux.tail77e06.ts.net/) (已收录 `gm-kernel-progress`，状态在线)
