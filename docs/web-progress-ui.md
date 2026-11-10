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
   - 硬件与运行配置：记录宿主机 CPU 架构、NativeAOT 发布参数、测试样本数与预热轮数。
6. **自包含与门户兼容性**:
   - 静态构建 (`dist/`) 采用相对资源路径 (`base: './'`)，自包含 Three.js、React 与 Tailwind CSS，杜绝运行时外部 CDN 依赖。
   - 兼容部署至栖点门户 `/apps/gm-kernel-progress/` 或任意二级路径。

---

## 2. 真实数据接入与现状

当前前端已成功整合 Mac 数据 Worker 导出的首包真实数据（来源分支 `origin/web/progress-data`，commit `cc1fe3f`）。

### 2.1 真实数据统计
- **总注册用例数**: 414 项
- **通过 (Pass)**: 4 项（均含完整双内核真实三角网格与逐项几何/拓扑/质量属性对比）
  1. `solid-blend.plane-nurbs`: 真实平面-NURBS 单凸边倒圆角，GM 耗时 1537.67ms vs PK 1.51ms，29 项严格检查全通过。
  2. `short-edge.a1-r0.5-t1e-07-reversefalse-default`: 严格 1e-7 质量门限倒角，29 项检查全通过。
  3. `planar-boolean.overlap-two-axes-subtract`: 平面多轴重叠布尔减运算，27 项检查全通过。
  4. `analytic-boolean.equal-overlap-subtract`: 解析曲面等厚重叠布尔减，27 项检查全通过。
- **诊断测试 (Diagnostic)**: 1 项
  - `regular-curved.block-subtract`: 常规曲面块布尔减。因选择器曾同时命中 block 与 reloaded-block，依契约保留完整 53 项证据但取消比率（`ratioGmOverPk: null`），正确标记为诊断态，严禁标记为绿色通过。
- **数据缺失 (Missing)**: 205 项（等待后续管线导出）
- **内核暂不支持 (Unsupported)**: 204 项（记录架构规划中）

### 2.2 共享数据契约
数据目录统一位于 `web/data/`:
- 根索引: `web/data/index.json`
- 运行清单: `web/data/runs/<id>/manifest.json`
- 几何与证据: `web/data/runs/<id>/cases/<case_id>/sample-0/meshes/`

---

## 3. 构建与发布

### 3.1 本地安装与构建
```bash
cd web
pnpm install
pnpm run build
```
构建脚本会自动将 `web/data/` 目录完整打包至 `web/dist/data/`，生成完全自包含的静态产物，大小约 4.5 MB，共约 60 个文件（远低于门户 32 MB 与 5000 文件上限）。

### 3.2 自动化测试
运行无头 Chromium 浏览器端到端真实数据 Smoke 测试：
```bash
cd web
pnpm run test:smoke
```
自动验证项目：
- 真实数据无缓存加载与 414 用例列表渲染
- 4 Pass、1 Diagnostic、205 Missing、204 Unsupported 状态统计过滤器
- 双视口 3D WebGL 模型渲染与实体角色颜色编码
- 步骤与快照步进控制
- 逐项验收检查项列表（29~53 项）
- 性能面板（含真实 Apple M4 / macOS 宿主环境显示与比率容错）
- 窄屏移动自适应视口切换

测试截图保存于 `temp_docs/`:
- `temp_docs/real_smoke_desktop_workbench.png`: 真实数据桌面工作台
- `temp_docs/real_smoke_desktop_checks.png`: 真实用例逐项验收表
- `temp_docs/real_smoke_desktop_diagnostic.png`: 真实诊断用例（正确保持非绿状态）
- `temp_docs/real_smoke_mobile_narrow.png`: 移动窄屏自适应

### 3.3 栖点门户发布流程 (Tailnet Publish)
```bash
# 1. 检查 slug 冲突（当前 gm-kernel-progress 无冲突）
~/.local/bin/portalctl list

# 2. 发布自包含静态站点
~/.local/bin/portalctl static \
  --slug gm-kernel-progress \
  --name 'GM Kernel vs Parasolid 对比工作台' \
  --description 'GM 几何内核与 Parasolid 真实对比与工程指标追踪工作台' \
  --dir /home/thrace/code/project_gm_kernel/web/dist

# 3. 访问正式 HTTPS 地址
# https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/
```
