# Blend 功能交付验收快照发布回执（60be7216 + 独审 54f1b309）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。

## 上线内容

| 项 | 值 |
| --- | --- |
| 线上 URL | `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/`（同 slug） |
| 新 run | `20261111T174315Z-blend60be`（immutable，latestRunId 已切换） |
| 候选来源 | `gm-blend-functional-delivery` @ `60be7216d646a296d7fd2d0b30b5f336a46a6a66`（分支 `feat/pk-native-blend-functional`，开发基线 `089939392bf2`） |
| 独审来源 | `gm-blend-60be-review` @ `54f1b309d830ac78358ebcba78e1deec8e3c3b8f`（分支 `review/pk-native-blend-60be`），报告 `docs/reviews/advanced-modeling/blend-60be-independent-review.md`（SHA256 `fc974d24…`） |
| 采集/装配 | 隔离树 `gm-board-blend-60be`（detached clean @ 60be7216）同实例显示补采 7 组（exit 0/0/0/1/2/2/2）；collector 本树 `86e1716c823ff89ca26f7f98116e0ebae2986c2b` |
| 数据版本显示 | 根入口 `数据版本 60be7216`；逐 case source=60be7216；显示绑定 `evidence/display-binding.json` |

## 显示范围（准确上线范围，15 case）

- **接受矩阵行（有真实模型步骤）**：有限凸平面倒圆目录、有限平面倒角目录（含 oblique 不等距，本次补采 exit 0）、轴向圆柱五操作（5 步全模型）、regular Plane/NURBS 带代表、独立等价支持记录链代表、短边单操作生命周期代表——各 case 挂同实例补采 GM/PK before/after/tool 模型（真实 DisplayBody，未重建、未以 PK 代 GM），完整生命周期（copy 删源/镜像缩放/可编辑 XT 重载/清理）与同产品 plane 方向查询通过。短边 self 产品 SHA256 `37ac232e…b0ba` 与作者接受成品逐字节一致。
- **红项保留**：`blend.axial-query-gap`——五圆柱产品直接 GM `BodyContainsVector` 20 真实点全 5000、PK 0、作者+本次复现原 exit 1；明示为独立查询 API 缺口，不否定上游 B-rep 构造。
- **未决照实**：mass 合同（upper<1e-7 Verified / lower≥1e-7 Different / 其余 Unresolved）与未决清单（catalog 2、regular 1、chain 3、shortedge 6、oblique-fillet 2，各 exit 2 保留）；510 四向 On 分歧未决；FaceAttach 负 surface reattach 仅静态线索未测。15 分量条件证明不以数量替功能。
- **合同/诊断类（明示无模型不造模型）**：FaceAskOrientedSurf 两 sense 合成修正（四 sign fixture true/false/false/true、123/123 回归）；21959 条件方向判据（唯一有限 Line/Circle/Ellipse 全段完整证明才条件通过，其余硬门、raw mask 保留；direction-controls 10 正/10 负）；独审语义 8 对成品 raw 0 + prism 全边 chamfer 全生命周期 + 128 材料查询 raw 0；f9 固定证据只读复用（标明历史接受不冒充本轮新跑）。
- **总体**：`blend.overall-status` 显式限定接受 + 不签 Boolean/Mac 性能/任意 freeform/变半径/高 valence/整体 M0–M5/总目标；性能倍率全 null（本轮不发布开发倍率）。

## 发布流程与原退出（owned 流程，工具固定 1b935972，UI 固定 015）

1. 新 immutable release `ui-015-tools-1b93597-blend60be`：release.json SHA256 `5f1078b0d8155e34cdee906abf78ee4b767f3069fc458ab046deda8a9f87918f`；previous-static = **本次更新前真实 served 整包**（treeHash `d8f0fbb601cdeeca8d5233e46ecfc56ae0536b59a44a756bfd3926341ee72ba0`，经逐文件 HTTPS 核验快照；非 UI424、非旧基线）。
2. `--mode backup --verify-served` / `--mode prepare`：数据闭包 0 错误（5 runs/512 case/242 mesh）；容量 **428 files / 20,910,131 B ≤ 30MiB**，原 exit 0。
3. `ConfigureReviewedWebProgressUiHook --mode install`（ownedUpgrade，previous-release=data-f9c59201，pin `384f8add…`/`4f0e40bc…`）：drop-ins/units/timer 原状态原子备份（`temp_docs/cutover-backup-2/`），hook 换指新 release；原 exit 0。
4. 自动入口更新固定：producer checkout 钉 `86e1716c823ff89ca26f7f98116e0ebae2986c2b`，base unit pin 校验 SHA 同步更新（备份可回退）；`ValidateWebProgress` 入口实测 0 错误。
5. `--mode publish --execute`：fingerprint `015cd308…:f0593760…:baf99b93…`（与 prepare 一致），portalctl 发布 + 逐文件 HTTPS 核验，原 exit 0。
6. 浏览器真实验收 **11/11**：六显示 case 双侧真实模型像素、黄色像素点击选中真实边、查询缺口红（5000）可见、mass Unresolved 不转绿、非签声明可见、real-uv 旧能力回归、390px 手机 drawer+模型无溢出、无 JS/CSS 失败。截图与 `browser-blend-receipt.json` 在 `temp_docs/cutover-verify/`。
7. `--mode resume` 恢复 timer 原 active+enabled；**实际自动轮**（2026-11-12 01:50:06 CST）UI_PREPARED→`UI_SKIP_SAME`，无回退。

## 限制（如实）

- 显示样本=7 组代表性同实例补采，不等于全类别证据范围（全类别=作者 complete-catalog 17/17 与五份收据索引 33/107/78/24/14 条，原件只读保留于 `../gm-blend-functional-delivery/` 与 `../gm-blend-60be-review/`）。
- 未签：Boolean（4794 独审已接受但集成固定产物未到，另行交接）、Mac 性能（6616 未正式测量，无开发倍率）、任意 freeform/变半径/高 valence、整体 M0–M5/总目标；510 边界与五查询缺口留共同 owner。
- XT 正文/私有依赖不公开；原 exit 2/1 均照实显示，未以任何绿色运行代签。

## 回退

回退基线=本次更新前真实 served 整包（新 release `work/previous-snapshot`，servedVerified）。先 `PublishReviewedWebProgressUi --mode rollback --execute`（blend60be release 工具），再 `ConfigureReviewedWebProgressUiHook --mode restore --execute`；producer/base unit 从 `temp_docs/cutover-backup-2/` 与 86e1716 原状恢复。
