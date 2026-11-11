# 6616 受控测量验收快照发布回执（270e2b6→6616cd6 + 独审 1806fd58）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。

## 上线内容

| 项 | 值 |
| --- | --- |
| 线上 URL | `https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/` |
| 新 run | `20261111T183127Z-alg6616`（immutable，latestRunId 已切换） |
| 测量组合 | baseline `270e2b6814afe5a48cd0532e89531f6fb80011be`（src 树 `c45cafbb…`，Native 子树 `afb8cec5…`）→ 计算 `6616cd60a55e50a5d8a5ada7274895c44609676b`（src 树 `41c8b597…`，Native 子树 `5b4c6104…`）；测量包装 `a73bc08d…`（report.candidateSha=该树）、测量交付 `03e5a8e8…`、代码/数学接受 `5a4ad531…` |
| 独审 | `gm-performance-6616-review` @ `1806fd58d8aab59942912eabadcd4324e30fdfb0`（`review/pk-algorithm-6616`，报告含正式测量附节） |
| 原包 | `mac-controlled-270-6616-original.zip` SHA `97a31646…`（490 成员/36,430,726B）；raw report SHA `fef5e9a8…`、material-binding SHA `df9a43cf…` |
| 显示采集 | **无新 Capture**：rep-2 显示直接取 material-binding 原字节（312 mesh 绑定逐项核验；每操作第一 candidate 进程 rep2 = 01/05/09/13/17）；collector `e80ae2bff5bca4df4ac02f8a56fcb2403823ef77` |

## 显示范围（9 case）

- **五操作（有真实模型步骤）**：sphere/planar/curved/solidblend/shortedge——各挂 rep2 GM/PK before/after/tool 真实模型（step 标题明示单 rep2 显示≠60 pairs 统计；**shortedge 标题纠正来源：输入=未改 PK seed XT、GM 执行倒圆/导出**；curved tracking=false/角色未知保留）。全部冷/暖值（每源 2 冷+4 暖）含 GM/PK 中位、min/max、**配对 ratio 中位数与中位数之比分列不互换**：sphere +2.02%/0.2758×、planar +6.23%/0.7274×、curved −5.86%/444.0818×、NURBS +0.32%/976.4974×、shortedge −64.4379%/2.81198x（候选配对 GM/PK 仍 3423.49713x）。三项暖回退、curved 冷回退、冷离群值全部保留不归因。
- **工作量计数（报表）**：暖 rep1 Bound 223,331→67,137 / Jac 1,597,561→525,315 / residual 27,774→7,023；**rep2 候选 Bound 67,166 分列**；根单元 220,895/根 3,518/SSI 4,956/winding 196/budget 0 不变；negative-hit/XYZ 未打印不补造。
- **质量/套件（红保留）**：2028 检查/60 compare 原 mask/96 chart 诊断；套件 **1914/1912/2/0、原 exit 1** + 270 focused 两失败 exit 1 照实；正式 harness/20 子进程原 0（report/log 记录，非独立 exit 文件）；setup 首 CLI 134、首预构建 exit 缺失+60s 超时分开；checker 0 不补签。
- **物性资格边界（缺失照实）**：仅 270 公共 Capture 15 项点值门（accuracy=.99999999，abs/(1+abs)<1e-7）；**无 modulus 字段，不称 60be/4794 模量三态全 Verified**；shortedge MASS BOUNDS 分支未触发。不加历史 adapter、不改写原统计。
- **总体（fail）**：PK 总目标未完成（3423.49713x）；不签排他 CPU/热稳定/普遍倍率/功能集成（4794/60be/G241b 未含）。report/报表 case 显式无模型。

## 发布流程与原退出（owned 流程，工具固定 1b935972，UI 固定 015）

1. 新 release `ui-015-tools-1b93597-alg6616`（release.json `163bfe8e…`；previous-static=本次前 served 整包 `c0196fd1…`，逐文件 HTTPS 核验；previous identity=blend60be release）。
2. backup --verify-served / prepare：数据闭包 0 错误（6 runs/521 case/268 mesh）；容量 **463 files / 24,110,473 B ≤ 30MiB**。
3. ownedUpgrade 安装（previous pins `5f1078b0…`/`c11eff78…`）；producer 钉 `e80ae2b…`+base unit pin 同步；自动入口实测。
4. `--mode publish --execute`（fingerprint `247fda68…` 与 prepare 一致）+ 逐文件 HTTPS 核验，原 exit 0。
5. 浏览器真实验收 **13/13**：五操作 rep2 模型像素、64.4379%/2.8119795/3423.4971307 原值、双 ratio 语义分列、PK seed XT 来源纠正可见、planar 回归、1914/1912 红、点值门/无模量三态、非签声明、real-uv、390px drawer、无 JS 错误。截图与 `browser-meas-receipt.json` 在 `temp_docs/cutover-verify/`。
6. resume 恢复 timer；**切换后自动轮**（2026-11-12 02:40:11 CST）UI_PREPARED（247fda68…）→UI_SKIP_SAME 无回退。

## 限制（如实）

单窗口一轮 ABBA（2026-11-11T17:42:47–17:50:06Z，Apple M4，load 3.38–15.37）；不签排他 CPU/热稳定/普遍倍率；rep2 XT 仅各进程末 rep 且未必双侧（curved/solidblend 单侧，缺失照实）；XT 正文不公开；未含 4794/60be/G241b 功能集成（未来按 1887 之后验证通过的当前格式另行接 run）。

## 回退

回退基线=本次前 served 整包（alg6616 release `work/previous-snapshot`，servedVerified）：先 `--mode rollback --execute` 再 `ConfigureReviewedWebProgressUiHook --mode restore --execute`；producer/base unit 从 `temp_docs/cutover-backup-3/` 与 e80ae2b 原状恢复。

## 元数据纠错修正版（2026-11-12）

协调独立 HTTPS 核出实际错误：五操作 performance 字段为候选 4 暖中位数，但 measurementKind 误标 `fresh-process-cold-median` 且 samples=6。修正为 `controlled-abba-warm-median`、headline samples=4（warmup 保持 0；scope 明写每源总 6=2 冷+4 暖全保留、冷 rep0 未丢弃）；全部 60 pairs/120 时间/冷暖 min-max/双 ratio/原件不变。

- 修正 run：`20261111T184658Z-alg6616`（latest，label 标注元数据修正版；collector `982ba595f838a5c004e7d81ae794fe358ea0f22b`）；**非新测量**，provenance.metadataCorrection 绑定原 run `20261111T183127Z-alg6616`。
- 纠错前派生 manifest（SHA `41c7aecb93a27a9286255cbbfdbebb6a40475aa79fbaf450134a166a89a98193`）/validation/receipt 以原字节随修正 run 保全（evidence/pre-correction-*.json）；纠错前 run 原样保留在历史。
- 发布：owned release `ui-015-tools-1b93597-alg6616f`（release.json `791af8d7…`；rollback 基线=纠错前 served 整包 `eea84599…`，HTTPS 逐文件核验）；容量 501 files/27,379,659B ≤30MiB；producer 钉 982ba59。
- 定点核验 9/9（controlled-abba-warm-median、samples=4、n=4/n=2 冷暖明细、3423.4971307 原值、关键 mesh 200、pre-correction 证据在档、无 JS 错误；isoparametrics 派生索引 404 为历来可选缺省，非回归）。
- 自动轮（2026-11-12 02:51:08 CST）UI_PREPARED（dataIndex 7d7ab71a…）→UI_SKIP_SAME 无回退。
