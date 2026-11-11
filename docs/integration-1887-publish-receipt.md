# 功能集成验收快照发布回执（1887e30d 当前 Capture）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。

## 上线内容

| 项 | 值 |
| --- | --- |
| 新 run | `20261111T190547Z-integrated1887`（immutable，latestRunId 已切换） |
| 集成来源 | `gm-pk-native-delivery` @ `1887e30d9050480daf38546f8cdd2df6592bd094`（文档 `bddcbf77…`，分支 `feat/pk-native-functional-integration`）；自动合并 `dfc5defe…`（parents Boolean 文档 `aca3296…`/Blend `60be7216…` 无冲突）；父独审 Boolean `75eddad1…`、Blend `54f1b309…` 限定范围保留 |
| 采集/装配 | **无新采集**：复用完整同实例 `capture-oblique-final-v1` + 当前产品 `integration-captured-final-v1`（原短 revision `1887e30` 由 `capture-declared-revision-resolved.txt` 外部解析，原件未改）；collector `dd985a6d61d29a6af7b7689443b5cc3aab57328b` |
| 固定清单 | source 537 SHA `45af81fd…` / artifact 68 SHA `edb5c71f…`（均独立核验） |

## 显示范围（8 case）

- **上游 independent-construction**（`integrated.oblique-upstream`，pass）：oblique 不等距倒角自构造+独立生命周期（copy 删源/镜像缩放/reload）；负 surface FaceAsk face=18542/surface=18541 实际 false==PK false；8 次条件有限方向证明；物性 Verified。
- **下游 same-byte-fork**（`integrated.same-byte-boolean`，pass）：同字节 subtract，target `0c3da45b…`/tool `2bafc64c…`、GM 产品 `1d73665e…`/PK `ff6d96d8…`，双方 Boolean 各 error=0/result=21650/1body、fresh/full/finite/FaceAsk/材料/物性 Verified+cleanup 原 0；明确标注为上游 live 成品的同字节 fork，两结论分列互不代签。
- **回滚控制**（diagnostic）：上游 Blend 通过；GM 注入失败 901/21653/0body、原字节与池回滚；native 成功；**控制 raw0 不签 GM Boolean 成功**。
- **合同控制**（diagnostic）：wrongradius/trim Rejected、wrongmaterial Different、30 分量 Unresolved 未变绿；off/on 60 行 CSV 计数各 3 完全相同。
- **边界 13/13、构建/收据**：FaceAsk 四符号/拥有关系/有限根正反例/接缝 reload；IL 2925 CLEAN；8 脚本构建；收据核对（3/45/6、4/60/2）不增资格。
- **父范围复用与红项保留**（missing）：旧 63/17/123/290/96 按父接受范围展示（非 1887 新跑）；510 self/import 15/16、imported 16 未决、native 自差 7、Blend 五产品 BodyContains 5000、不确定物性、原完整单测 1907/2 全保留；**不写 1909/M0–M5 全绿**。
- **当前 Capture 质量格式**：quality-compare-*/quality-mass-*（15 分量 estimate/modulus/normalized 上下界、agreementStatus=Verified/Different/Unresolved 原样）；checks[].evidence 全部指向该等原件；sourceBinding/captureIdentity/loaded DLL/XT SHA 保留；XT 正文不公开。

## 发布流程与原退出

新 release `ui-015-tools-1b93597-integrated1887`（release.json `d15e83a4…`；rollback 基线=本次前 served 整包 `63098690…`，逐文件 HTTPS 核验；previous identity=alg6616f release）。backup/prepare：8 runs/538 case/0 错误；容量 **534 files / 28,060,237B ≤ 30MiB**。ownedUpgrade 安装（previous pins `791af8d7…`/`03f55829…`）；producer 钉 `dd985a6…`。`--mode publish --execute`（dataIndex `40665af8…` 与 prepare 一致）+ 逐文件 HTTPS 核验。浏览器定点 **11/11**（双 step 真实模型、independent-construction/same-byte-fork 标注、四哈希可见、901 不签成功、Rejected/Different/30 Unresolved、父红项、quality 证据 agreementStatus、mesh captureIdentity、无 JS 错误）。自动轮（2026-11-12 03:10:22 CST）UI_PREPARED→UI_SKIP_SAME 无回退。

## 限制（如实）

不签全部 M0–M5/1909 全绿、未执行性能、main 集成或产品部署；回滚控制不签 GM Boolean 成功；旧 510/查询缺口等红项留共同 owner；性能 6616 修正版快照（`20261111T184658Z-alg6616`）与本 run 分源分列。

## 回退

rollback 基线=本次前 served 整包（integrated1887 release `work/previous-snapshot`）：先 `--mode rollback --execute` 再 `--mode restore`；producer/base unit 从 `temp_docs/cutover-backup-5/` 与 dd985a6 原状恢复。
