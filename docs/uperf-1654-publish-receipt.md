# 统一性能验收快照发布回执（1654dbf2 窗口 + 独审 2f8aea49）

发布对象：[GM Kernel vs Parasolid 对比工作台](https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/)。

## 上线内容

| 项 | 值 |
| --- | --- |
| 新 run | `20261112T002227Z-uperf1654`（immutable，latest） |
| 计算/统一源 | candidate `1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7` / Native `224564d1cdc0d0b6cf78c275282454751af2fd60` / src `c6e8bc553cdfecaebc3c1fac5d30688ba8628e6c`；baseline `84c558c10135c117bf8be6629a157e6d230f1b2d` / Native `133f56acb0cf485366cd4f13f457dc3b161655eb` |
| 窗口/交付/独审 | metadata `d014f76e41746a08b32e963d24b6a8b02fcaac67`；交付文档 `73b06e1004122f6897da10532d6253579a033aa4`（73b 补标签≠原进程执行源）；独审 `2f8aea492d21f39269e514d9bcd3d8e792e3903b`（`unified-performance-1654-independent-review.md`） |
| 原包 | native224 `91ce2f86…`（1042 files）/ review `d57ba230…`（1158 files）；report/quality 73b+d014 分源原字节随 run |
| 采集/装配 | **无新采集**：material-binding-current rep2 原字节选样（01/05/09/13/17 逐项核）；collector `1b6106428a89d1a320f69372f83693987efb19c4` |

## 显示范围（9 case）

- **五操作 rep2 真实模型 + 全部冷暖原值（G17）**：sphere .07425→.08255 **+11.178% 回退（fail 保留，+.0083ms）**、planar .57695→.57380 −.546%、curved 132.18265→84.90585 −35.766%、NURBS 691.55280→601.99400 −12.950%、shortedge 670.00785→642.26420 −4.141%（暖 524.1270..795.0665 波动照实）；**双 ratio 分列**（配对 GM/PK ratioMedian .30856/.71477/35.14762/368.97018/387.35924 vs 中位数之比 .30557/.71609/33.84658/392.44695/388.63863，不互换）；每源 2 冷+4 暖、controlled-abba-warm-median/samples4/total6；20 进程/60 对/120 API 全 0（4 planar raw0/Verified、16 其他 raw2/Unresolved、主 runner 2、driver 0 不替主 exit）。
- **质量分层（missing 不绿）**：主 60 compare 900 分量 **312 Verified/588 Unresolved/0 Different**；额外 shortedge 12 回读控制 180 分量独立列；唯一总账 72 报告 1080 分量 396V/684U/0D（不重复计数）；quality JSON 原样（estimate/modulus/bounds/status/mask/rawdiff/conditionalfiniteproof）；**performanceQualified=false 原字节**，独审仅接受描述性观测。
- **Mac 套件（红保留）**：2000 total/1998 pass/2 fail/0 skip 原 raw 1（7m27s）；**实质栈修复显式显示**（a289 864-pass 后 stackOverflow→修后 21 控 0/2000 不再崩）但 2 supportingBlend NotConverged 仍红（同 Mac baseline 133 窄复现）；Linux 1974 不代签；Fixtures 运行时数据未封存身份限制保留。
- **八组报告类**：rigid 原 0+1 missing、另 7 组原 2/missing 按独审表；mirror8 当前成功 hash≠q2 旧产品不写字节相同。
- **角色/绑定照实**：64 最终文件集角色（20 GM 产品/16 PK reference/4 PK 经 GM 回读再导出/8 GM before/8 PK before/4 PK seed/4 GM received fresh input；NURBS 无额外 PK 产品不造）；旧 material-binding.json rep2 派生缺陷原字节保留（legacy 证据）+current 纠正说明；snapshot380 显示 XT 与 ordinary transmit 不同 hash 不强行相等；shortedge=PK seed→GM receive→GM Fix；curved roles missing 中性。
- **总体（fail）**：sphere 回退/shortedge 波动/离群保留不归因；旧 510/BodyContains5000×5/FaceAttach/curved roles/M0–M5/PK 目标未解决；不签普遍/因果速度，不乘 a289/217fc 窗口。

## 发布流程与历史归档

- 新 release `ui-015-tools-1b93597-uperf1654`（release.json `d95ef56a…`；rollback 基线=本次前 served 整包 `13979b4d…`，逐文件 HTTPS 核验）；ownedUpgrade（previous=unified4b，pins `4dcf467c…`/`57a5f789…`）；producer 钉 `1b610642…`；prepare 实测 **594 files / 27,506,486B ≤ 30MiB（余 3,950,794B）**。
- **预授权归档**（透明、无死链、非被拒结果）：`20261111T142551Z-perf6761600`（35 files/3,081,064B/manifest `5b90d397…`）与 `20261111T184658Z-alg6616`（38 files/3,268,735B/manifest `02b79308…`）共 73 文件/6,349,799B 完整只读冻结于 `retired-data-archive-5/`（manifest `9dc3da79…`，逐文件 path/size/SHA+index 快照，可由 release 归档+核验快照恢复原字节）后移出 served/index；GConic217fc/统一功能4b/main610/Blend60be/integrated1887 继续在线。
- 浏览器 **12/12**（五操作 rep2 模型像素、sphere 回退、双 ratio、312/588、2000 套件+栈修复、binding/mesh 200、unified4b 历史可查、real-uv、390px、无 JS 错误）；自动轮（2026-11-12 08:30:30 CST）UI_PREPARED（dataIndex `4ce1b6f2…`）→UI_SKIP_SAME 无回退。

## 限制（如实）

performanceQualified=false；单窗口描述性观测（UTC 23:40:41–23:42:45，load 3.82–4.35，powermetrics 原1/无数值温度）；两 ratio 口径分列；588/684 分量 Unresolved；2 fail 红；旧红项与 M0–M5/PK 目标未解决；zip/DLL/PDB/XT 不公开（hash 锚定）。

## 回退

rollback 基线=本次前 served 整包（uperf1654 release `work/previous-snapshot`）：先 `--mode rollback --execute` 再 `--mode restore`；两条归档 run 可由 release 只读 archive+核验快照恢复。
