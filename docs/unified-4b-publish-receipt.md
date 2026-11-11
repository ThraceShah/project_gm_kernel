# 统一内核验收快照发布回执（4b2405e + runner 84c558c + 独审 583d917/B17）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。

## 上线内容

| 项 | 值 |
| --- | --- |
| 新 run | `20261111T223759Z-unified4b`（immutable，latest） |
| 计算/统一源 | `4b2405e3945929c5bb93677a68b0ee79e430b2f3`（真 merge `b9db9bb`：功能 `97174392…`+累计性能 `f9ffc09…`；整树 `dcac1874…`/src `0c5d23f6…`/Native `133f56ac…`） |
| runner 质量门 | `84c558c10135c117bf8be6629a157e6d230f1b2d`（仅脚本，Native 不变）；文档 `8c2d6da8…` |
| 独审 | `583d9178…` + B1 续审 `b17f1aa4…`（review/pk-native-unified-4b2405e，`pk-native-unified-4b2405e-independent-review.md`） |
| 原包 | `handoff-current-v2.zip` SHA `5197d647…`（156 files/56 mesh/2,457,925B mesh；files.sha256 逐项核 0）；collector `62c15329cf46ea56f4eba60f561a1d589199fd9b` |

## 显示范围（10 case）

- **7 捕获组 56 模型全展示**（GM/PK before/after/tool 真实 step）：oblique 负 surface 倒角→生命周期→同字节 Boolean（2 步/10 mesh/123 全过 raw0）、**mirror8 实质修复**（GM157/fault16059 已修：GM/PK 0、strict/full/finite 过；2 mass 未决 raw2，历史输入 hash 相同）、shortedge 双来源 lifecycle（5 步/28 mesh/133 过 77 未决 raw2）、controls 反例（27 过 9 失败 27 未决 raw0=成功负控含回滚 901，不冒正例）、curved/NURBS/shortedge 单操作（6/4/4 mesh，未决照实）。checks 状态原样（missing 不绿），sourceBinding/角色缺失如实。
- **完整套件**：Native full suite **1974/1974、0 failed、原 exit 0**（TRX `90472933…`）；历史已知 2 项本 TRX 实际 **Passed**（未改测试/历史记录）；333+161 源校验一致。
- **63 场景清单**（报告/独审引用）：regular45+mixed18 几何/拓扑全过、70 次 mass 未决、原 exit 2；rollback/face-query/direction-controls/controls-off-on 分列原 0。
- **总体（fail）**：M0–M5/PK 目标未完成；510/self15-import16/native7、BodyContains5000×5、FaceAttach 未测、curved roles 缺失继续限定；**Linux 非正式性能、倍率全 null、217fc 旧倍率不绑定**；GConic/6616 历史 run 独立保留。
- 上游 independent-construction 与下游 same-byte-fork 分列；shortedge 来源=PK seed→GM receive→GM Fix（不称 GM 独立构造）。

## 容量与历史归档（协调裁决）

- 容量账：prepare 实测 **629 files / 30,389,201B ≤ 30MiB（余 1,068,079B）**。
- 按协调容量裁决归档三条（完整只读冻结、逐文件 path/size/SHA+树清单、可恢复；原件/manifest 未改；非被拒结果，历史接受/红项原样）：
  - `20261111T130936Z-f9c59201`（58 files/3,230,189B，manifest `f5504e54…`）→ `retired-data-archive-4/`（manifest `7bdf6be3…`）
  - `20261111T183127Z-alg6616`、`20261111T133805Z-perf6761600`（43 files/3,343,428B）→ `retired-data-archive-3/`（manifest `1aea4f24…`）
  - 三条均同时可从核验过的发布前 served 整包恢复；统一 run 的 manifest/receipt 带历史归档说明（runId/原 manifest SHA/文件数/字节/归档身份），无死模型链接。
- 保留在线：main610 历史、676 五模型、正确 6616 修正版、Blend60be、integrated1887、GConic 修正版。

## 发布流程

新 release `ui-015-tools-1b93597-unified4b`（release.json `4dcf467c…`；rollback 基线=本次前 served 整包 `e60f7339…`，逐文件 HTTPS 核验）；ownedUpgrade 安装（previous=gconic217fc，pins `a8897989…`/`c0444e42…`）；producer 钉 `62c15329…`；`--mode publish --execute`（dataIndex `027f6ff8…`）+ 逐文件 HTTPS 核验；浏览器 **11/11**（六组模型像素、mirror8 修复可见、负控/缺失保留、1974+Passed、quality 链接、mesh 200、GConic 历史可查、real-uv、390px、无 JS 错误）；自动轮（2026-11-12 06:41:12 CST）UI_PREPARED→UI_SKIP_SAME 无回退。

## 限制（如实）

510/BodyContains5000/FaceAttach/curved roles/mass Unresolved/M0–M5/PK 目标未完成；无正式性能（217fc 旧倍率不绑定统一内核）；XT/DLL/PDB 不公开（hash 锚定）；1974 全过不证明限定项解决。

## 回退

rollback 基线=本次前 served 整包（unified4b release `work/previous-snapshot`）：先 `--mode rollback --execute` 再 `--mode restore`；三条归档 run 可由 release 只读 archive + 核验快照恢复原字节。
