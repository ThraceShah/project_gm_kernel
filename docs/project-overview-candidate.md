# 项目整体进度看板 · 完整可审候选（待一次独立审查，未切线上）

## 产品结构

- **默认首页=项目整体进度**：稳定用例目录（463 具名建模用例/20 套件，registry ID 不变）+ 分维状态矩阵（几何拓扑/操作功能 · 物性数值 · 查询/分类 · 性能观测，各维 通过/失败/未决/未验证/不适用）+ 左侧搜索/Blend-Boolean 筛选/只看有问题 + 行展开明细（参数定义、逐维最后验证 commit/tree/host/exit/证据、历史验证列表、真实模型经交付记录进入）。
- **交付记录/历史运行**：单轮快照查看（原 runs 浏览保留），可从 overview 的交付记录进入并返回；刷新/新交付不再重置首页为本轮卡片。
- **边界登记**（510 分歧、5000×5 查询缺口、FaceAttach 未测、curved roles 缺失）与**验证记录**（Mac 2000/1998/2、Linux 1974、物性分量总账、性能目标）单列，**不入建模分母**。

## 数据合同与生产

- `web/data/project-overview.json`（新当前合同，`ProjectOverview` 类型），由 `scripts/BuildProjectOverview.cs` 可重复生成：固定 registry + 固定原件日志逐案提取（registry args=日志 selector 显式映射；MASS_BOUND 逐分量；GEOMETRY_TOPOLOGY_PASS/compare 行）+ 显式映射表（1654 五 op→精确 registry ID）。无 PK 运行；原件只读；旧绿不冒充当前版本（history 分版本标注）。
- 目录统计可复算：summary 与 catalog 逐行一致（校验脚本 CLEAN）；分母=463 具名建模用例；单测/质量分量/性能样本/工具不入分母。

## 来源映射清单（可审）

| 目录子集 | 状态来源 | 版本 |
| --- | --- | --- |
| regular-curved 45 + mixed-analytic 18（63 原子 Boolean 场景） | `../gm-native-unified-delivery/temp_docs/unified-delivery/regular45-final.log`+`mixed18-final.log` 逐案（selector=args[0]） | 4b2405e/133f56ac |
| local-blend 17 | `../gm-blend-functional-delivery/.../complete-catalog.log` 逐案 | 60be7216 |
| short-edge 96/101 | `../gm-pk-native-delivery/.../matrix-96-cases.txt` 名单 + f9 接受范围 | f9c59201 |
| 5 个测量 op（sphere/planar/curved/solidblend/shortedge） | 1654 window operation-summary.json（G17 原值/双 ratio）+ qualityStatusCounts | 1654dbf2/224564d1（当前版本复验） |
| cylinder-* 5 项查询缺口 | `60be/.../axial-material-query.log`（20 点全 5000） | 60be 起未解 |
| 其余 400 项 | 无当前可审计验证记录 → 未验证（保留在目录，不删） | - |

历史成就（4b/60be/f9/1654 等）在行内 history 分版本标注；当前版本复验仅 5 op（含 sphere +11.178% 性能回退标识——性能观察，功能仍 pass）；未复测≠不支持。

## 验证（实际执行）

- 数据级：463 ID 唯一、summary 逐项可复算、缺证据不绿（CLEAN）。
- 浏览器（本地预览，未切线上）16/16：默认 overview、目录/统计、搜索/筛选、63 场景功能+物性+4b 证据与历史、复验标识、sphere 回退=性能观察、边界登记、runs/历史视图、真实 Blend+Boolean 双 viewport 模型、real-uv 入口、返回 overview、刷新不重置、390px 无溢出、无 JS 错误。截图 `temp_docs/cutover-verify/live-overview-desktop.png`/`live-overview-mobile.png`。
- 桌面/390px、加载失败显示缺失不假 fixture（overview-load-error 分支实现）。

## 发布物（待独审后执行）

- UI dist 非data 树 SHA256 `72606f23627e20ab115de97afb23a28ddb3e3f6e018e9bc7200e45f3ce91e36e`；overview JSON SHA256 `bc2c7c7e69d5ba5cf79a1dda94d3ed0c26af9b03ce4c7be14cc39a825137eb98`。
- 发布时按 owned 流程 freeze 新 UI 提交+对应 overview 数据；015 仅起点/历史回滚；producer 钉新 overview/current 数据入口；15 display/real-uv 保留；容量按现 27.5MB+~0.6MB 摘要核算 ≤30MiB。
