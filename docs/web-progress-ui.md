# Project GM Kernel 生产对比与进展工作台 (Web Progress UI)

本文档说明 `project_gm_kernel` 的生产 Web 进展与 GM / Parasolid (PK) 几何内核对比界面的架构、共享数据契约、使用方式及自动化测试。

## 1. 架构与设计原则

该对比工作台为专业 CAD/CAM 几何建模内核对比分析工具，采用**浅色工程桌面 + 深色 3D 画布**的设计语言，以中文为主、优先保证可读性与信息密度。

### 核心特性
1. **多内核对称视口 (Dual Viewport)**:
   - 左侧: **GM Kernel** 实测结果与几何切片（展示几何生成与 Oracle Tessellation 来源信息）。
   - 右侧: **Parasolid (PK)** 官方基准 Oracle 参照结果。
   - 双视口相机联动（平移、旋转、缩放实时同步），并支持独立解锁或一键视角复位。
2. **严格真实离散快照 (Discrete Snapshots)**:
   - 支持多步骤（前态 Before / 后态 After）离散快照切换与拖拽时间轴、播放/暂停、多倍速控制。
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
   - 对比检查项：实测值 (Observed)、基准值 (Expected)、绝对偏差 (absDelta)、相对偏差 (normalizedDelta)、门限容差 (Threshold)、物理单位 (Unit)、状态 (Pass/Fail/Missing)。
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

## 2. 共享数据契约 (Data Contract)

数据位于 `web/data/` 目录，由 Mac 数据 Worker (`ctx_391ece96108a`) 并行导出与更新。前端严格遵循以下契约：

### 2.1 根索引 `web/data/index.json`
```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-11-11T01:30:00Z",
  "latestRunId": "run-20261111-013000",
  "runs": [
    {
      "id": "run-20261111-013000",
      "commit": "a1e4500",
      "generatedAt": "2026-11-11T01:30:00Z",
      "label": "Blend & Boolean baseline",
      "manifest": "runs/run-20261111-013000/manifest.json"
    }
  ]
}
```
*注：`manifest` 路径相对于 `web/data/` 目录。*

### 2.2 运行清单 `web/data/runs/<id>/manifest.json`
```json
{
  "schemaVersion": 1,
  "run": {
    "id": "run-20261111-013000",
    "commit": "a1e4500",
    "generatedAt": "2026-11-11T01:30:00Z",
    "pkVersion": "Parasolid v38.0.165",
    "host": "qingyu2 (Ubuntu 24.04)",
    "configuration": "Release NativeAOT / x86_64",
    "scope": "Blend & Boolean Regression Suite",
    "provenance": "GM Kernel Geometry with PK Tessellation Oracle"
  },
  "cases": [
    {
      "id": "case_blend_constant_radius_box",
      "title": "立方体定半径滚球倒圆角 (R=2.0)",
      "family": "blend",
      "operation": "PK_BODY_fix_blends",
      "status": "pass",
      "parameters": { "radius": 2.0 },
      "apis": {
        "gm": { "name": "GmBlendEngine.FixBlends", "parameters": { "radius": 2.0 } },
        "pk": { "name": "PK_BODY_fix_blends", "parameters": { "radius": 2.0 } }
      },
      "steps": [
        {
          "id": "step_1",
          "title": "步骤 1: 初始实体",
          "gm": { "before": "meshes/box_before.json", "after": null },
          "pk": { "before": "meshes/box_before.json", "after": null }
        },
        {
          "id": "step_2",
          "title": "步骤 2: 圆角曲面生成",
          "gm": { "before": "meshes/box_before.json", "after": "meshes/box_after_gm.json" },
          "pk": { "before": "meshes/box_before.json", "after": "meshes/box_after_pk.json" }
        }
      ],
      "checks": [
        {
          "id": "chk_topol_faces",
          "label": "实体面数量 (Face Count)",
          "status": "pass",
          "gm": 7,
          "pk": 7,
          "absDelta": 0,
          "normalizedDelta": 0,
          "threshold": 0,
          "unit": "count",
          "evidence": "evidence/topol.txt",
          "note": "拓扑面完全一致"
        }
      ],
      "performance": {
        "gmMs": 0.42,
        "pkMs": 0.61,
        "ratioGmOverPk": 0.69,
        "samples": 50,
        "warmup": 5,
        "correctnessQualified": true,
        "host": "qingyu2",
        "configuration": "Release",
        "note": "GM 耗时领先约 31%"
      },
      "evidence": [
        { "label": "拓扑报告", "path": "evidence/topol.txt" }
      ],
      "notes": ["单凸边滚球测试"],
      "source": { "path": "tests/KernelTests/Blend/ConstantRadiusBoxTests.cs", "revision": "a1e4500" }
    }
  ]
}
```
*注：mesh 与 evidence 路径均相对于该 `manifest.json` 所在的目录。*

### 2.3 几何切片 `web/data/runs/<id>/meshes/<name>.json`
```json
{
  "schemaVersion": 1,
  "bodyId": "body_01",
  "source": {
    "kernel": "GM Kernel",
    "geometryAuthor": "BlendRollingBall::Execute",
    "tessellator": "PK_TOPOL_render_facet",
    "commit": "a1e4500",
    "chordTolerance": 0.0005
  },
  "faces": [
    {
      "id": "face_fillet_surface",
      "positions": [0, 8, 10, 10, 8, 10, 10, 8.58, 9.85, ...],
      "normals": [0, -0.44, 0.89, ...],
      "indices": [0, 1, 2, 0, 2, 3],
      "role": "newBlend"
    }
  ],
  "edges": [
    {
      "id": "edge_fillet_spring",
      "positions": [0, 8, 10, 10, 8, 10],
      "role": "newBlend"
    }
  ]
}
```

---

## 3. 演练数据模式 (Dev Fixture Mode)

根据需求，前端严格隔离演练数据与生产数据：
- **生产默认行为**: 尝试请求 `data/index.json?_t=<timestamp>`。若 Mac Worker 数据尚未部署，UI 呈现友好清晰的“等待生产基线数据”占位态，并提供“加载 DEV FIXTURE 演练体验”按钮，不进行静默假数据伪装。
- **演练模式激活**:
  - URL 参数：访问 `?fixture=1` 或 `?fixture=true`。
  - 界面操作：点击顶部栏右上角的“演练模式 (DEV FIXTURE)”按钮。
  - 演练模式包含：
    1. `case_blend_constant_radius_box` (Pass): 3D 立方体定半径滚球圆角，展示黄色选中边、支撑面、新圆角面。
    2. `case_boolean_cylinder_subtract` (Pass): 基板打孔布尔减，展示圆柱内孔面与拓扑重构。
    3. `case_blend_chamfer_fail` (Fail): 多边汇交斜角角点倒角，展示 G1 连续性超标失败检测。
    4. `case_blend_missing_pk` (Missing): 变半径倒角，展示 PK Oracle 基准缺失时的优雅错误提示。
    5. `case_boolean_unsupported` (Unsupported): 奇异退化相切布尔交，展示内核不支持异常记录。

---

## 4. 构建与测试

### 4.1 安装依赖与构建
```bash
cd web
pnpm install
pnpm run build
```
产物将输出至 `web/dist/`，包含自包含单页 HTML、JS bundle 与 CSS，依赖项零外部 CDN。

### 4.2 本地运行
```bash
cd web
pnpm run dev
# 打开 http://localhost:5173/?fixture=1
```

### 4.3 自动化浏览器 Smoke 测试
运行基于真实无头 Chromium 的端到端测试，覆盖桌面宽屏与移动窄屏视口、无数据占位态、演练模式下全部 3D 双相机视口、步骤切换与验收面板：
```bash
cd web
pnpm run test:smoke
```
测试自动生成并保存截图至 `temp_docs/`:
- `temp_docs/web_smoke_no_data.png`: 真实数据未到达时的等待界面。
- `temp_docs/web_smoke_desktop_workbench.png`: 桌面端双 3D 视口、图例与侧边栏。
- `temp_docs/web_smoke_desktop_inspection.png`: 逐项验收检查与性能指标。
- `temp_docs/web_smoke_mobile_narrow.png`: 移动窄屏下的自适应布局与视口切换。

---

## 5. 后续真实数据整合注意事项 (Integration Notes)

1. **Mac Worker 数据落地**:
   - Mac Worker (`ctx_391ece96108a`) 负责将生成的 Parasolid Oracle 及 GM 实测数据放入 `web/data/index.json`、`web/data/runs/<id>/manifest.json` 及相应 meshes。
   - 本工作台前端完全动态基于此相对结构加载，无需改动任何前端代码即可自动识别新 Run 与用例。
2. **门户正式发布**:
   - 当 Mac Worker 真实数据提交后，通过 `portalctl static --slug gm-kernel-progress --name 'GM Kernel 对比工作台' --dir web/dist` 完成门户正式发布。
