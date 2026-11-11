# G+Conic 受控测量验收快照发布回执（6616→74c29e0 + 独审 6a7dfa15）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。

## 上线内容

| 项 | 值 |
| --- | --- |
| 新 run | `20261111T210306Z-gconic217fc`（immutable，latest；显示 label=G+Conic 受控测量验收快照） |
| 测量交付 | `217fc6f811e06cbeb936753f77bd65ae4156d8e8`；文档 `Pk_Native_GConic_Controlled_Measurement.md` + 四 JSON 原字节随 run |
| 来源四层分列 | baseline 计算 `6616cd60…`/wrapper `5392a06e…`/Native `5b4c6104…`/src `41c8b597…`；candidate 计算 `74c29e0f…`（含已审 G）/wrapper `34ffe46b…`/Native `58c1667b…`/src `397526ef…`；report/summary 的 NativeTree 字段实为整个 src 树（已注明） |
| 代码/数学 | Conic `35e61e5e…`、G `3fdc109b…`；独审 `6a7dfa15…`（review/native-conic-b47，正式测量附节，独立 evidence SHA `1ac9334b…`） |
| 原包 | `mac-6616-gconic-controlled-original.zip` SHA `acf7b872…`（665 成员/131,859,212B）；report `c5b2cbba…`、material `dd6738a8…`、loaded-identity `820418ac…` 均核验 |
| 采集/装配 | **无新采集**：material-binding 原字节 rep2 显示（每操作第一 candidate 进程=01/05/09/13/17，逐项核对）；collector `8d0d01a6ce1badf0c18da3f44565d39c127d7c9c`（修订 `b6424d8d…`） |

## 显示范围（9 case）

- **五操作（真实模型步骤）**：sphere −11.26%/1.1269x（配对 0.2805x）、planar **+65.66% 回退**（冷 50.08305→59.41610 亦回退）、curved −84.38%/6.4015x（配对 75.6049x）、NURBS −56.56%/2.3019x（配对 438.1989x）、shortedge −87.03%/7.7080x（**配对 340.17025558x vs 中位数之比 334.01404249x 分列**）——每源 2 冷+4 暖、headline warm4、controlled-abba-warm-median、warmup 0、冷 rep0 保留；全部冷/暖中位+min/max+120 原时间在 `evidence/report.json`；离群值（冷 PK 12.033/17.4161ms、shortedge 暖 601.4206–879.774ms）照实，不归因。
- **工作量逐 rep**（报表）：baseline Jacobian 冷 rep0 526,133/暖 rep1 525,315/暖 rep2 525,505 分列；candidate 每 rep 16,870/bound 47,246/rootcells 22,371（对侧 220,895）；bound 暖 rep2 67,166 与 67,137 分列；roots 3,518/SSI 4,956/winding 196/budget 0。
- **质量分层（红保留）**：60 对质量门（fresh/full/strict/finite+900 项 PK 点值 mass、60 compare 原 mask、96 chart 诊断）过；**suite 1944/1942/2/0 raw 1 + 2 known 失败**；**mirror+scale 8 case 94f/b47 各 raw 134、PK 0、157/fault 16059 原红**（不外推 1887）；env 拼写 127=计时前错误、审计 attempt 3×134/旧脚本 143 不混原建模。
- **加载身份**：20 进程 start/end MVID/DLL/PDB/CodeView 一致；PDB 逐文档对 clean wrapper Git 核验 baseline 328/candidate 330 tracked Native 全匹配（各 3 生成文档单列）；九公共 runner 两侧逐字同；不签独立可重现编译。
- **边界照实**：物性仅本窗口点值门（无 modulus，不签 1887 三态/GM 原生 mass）；shortedge 来源纠正=PK seed→GM receive→GM FixBlends→GM XT（默认 geometryAuthor 非独立构造证明，mesh 字节未改）；curved tracking/roles missing 保留；本窗口无下游组合，各步接受范围单列。
- **总体（fail）**：PK 目标未达（shortedge 配对 340.1703x）；planar 暖/冷回退保留；不签普遍加速、G/Conic 孤立拆分、跨窗口相乘、1887 功能/质量资格、下游组合、全家族。**未含 1887/Bezier16aca**。

## 发布流程与元数据修订

1. 新 release `ui-015-tools-1b93597-gconic217fc`（release.json `a8897989…`；rollback 基线=事件前 served integrated1887 整包 `ec45e24b…`，HTTPS 逐文件核验）；容量 **570 files / 31,363,448B ≤ 30MiB（余 93,832B）**。
2. ownedUpgrade 安装（previous=integrated1887，pins `d15e83a4…`/`7757dd5c…`）；producer 钉 `8d0d01a6…`。
3. **元数据修订**：首发 `20261111T210025Z-gconic217fc` 携带复制的 6616 label/残留 cross-ref（同一测量、无新数据错误）；按现行 immutable 规则以修订 run `20261111T210306Z-gconic217fc` 取代（label/绑定/注释修正，测量原件未变）。缺陷 run **完整归档**至 release `retired-data-archive/`（36 文件哈希一致、manifest SHA `3bb3b992…`、冻结只读）后因 30MiB 门（首发后仅余 93,607B）移出 served 根；未删任何原始样本。
4. 浏览器 **14/14**：五操作 rep2 模型像素、双 ratio 分列（340.17025558/334.01404249）、planar 回退、质量红、PK seed 来源纠正、mesh 四层绑定、120 时间 report 可达、1887 历史可查、real-uv、390px、无 JS 错误。
5. **自动轮**（2026-11-12 05:10:17 CST）UI_PREPARED（dataIndex `72a473fb…`）→UI_SKIP_SAME 无回退。

## 限制（如实）

单窗口一轮 ABBA（UTC 20:14:18–20:18:00，Apple M4，load 4.00–5.43）；不签排他 CPU/热稳定/普遍倍率/PK 目标；44 XT 仅各进程末 rep2 且未必双侧（缺则标缺）；大原包/DLL/PDB/XT/私有路径不公开（本地 hash 锚定）；原 suite/mirror 红与既有未决继续留共同 owner。

## 回退

rollback 基线=事件前 integrated1887 整包（gconic217fc release `work/previous-snapshot`）：先 `--mode rollback --execute` 再 `--mode restore`；producer/base unit 从 `temp_docs/cutover-backup-6/` 与 8d0d01a 原状恢复；缺陷 run 可由 `retired-data-archive/` 原字节恢复。

## 派生标签元数据修订（第二版，2026-11-12）

协调 HTTPS 核出 210306 manifest 仍有 54 处复制残留：operation 文本 `controlled measurement (270 vs 6616)`、`parameters.acceptanceReview` 误指 `../gm-performance-6616-review/.../independent-6616-review.md`、四 evidence.label 写 `03e`、report SHA 写 `fef5e9a8…`，以及错窗口物性归属。一次修正全部为真实链：operation `controlled measurement (6616 vs G+Conic 74c29e0)`、review `../gm-conic-substitution-review/docs/Native_Conic_Independent_Review.md @ 6a7dfa15…`、`217fc 原 …`、report `c5b2cbba…`、物性=本窗口 900 项 PK 点值门；合法历史描述“270 公共点值检查”未盲改。数字/状态/原件字节未动。

- 修订 run：`20261111T211605Z-gconic217fc`（collector `81b1083b1ef128b7e36f2c6881cb890f8facb82a`）；输出 JSON 全文遍历确认无残留（4 处 `03e` 命中均为 SHA 子串假阳性）。
- 210306 缺陷 run **完整归档** `retired-data-archive-2/`（36 文件哈希一致、manifest SHA `9f89570e…`、冻结只读）后按授权移出 served/index（同 210025 方式），其余历史未动。
- 26 个 rep2 mesh 与 4 份证据 JSON 经 HTTPS/本地双侧 hash 全同原件（26/26、4/4）。
- owned 同 release `ui-015-tools-1b93597-gconic217fc` 发布：容量 570 files / 31,364,000B ≤ 30MiB；producer 钉 `81b1083b…`。
- 自动轮（2026-11-12 05:20:45 CST）UI_PREPARED（dataIndex `3c4694d4…`）→UI_SKIP_SAME 无回退。
