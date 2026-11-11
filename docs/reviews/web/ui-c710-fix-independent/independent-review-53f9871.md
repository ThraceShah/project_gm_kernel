# 固定53f9871：原c710两个UI阻断窄复验附节

**结论：accepted，仅固定53f9871本次声明的viewing UI切片。** 原c710在 `docs/review-web-inspection-c710@9ef8ce133bf60cf70f635bd3b016b06a9701e170` 的blocked结论、原输入、失败日志及报告全部保留；本附节没有将原失败改写为通过。结合该原复核已接受的布局、图例、共享相机、overlay键盘及原PK数据/时间说明范围，本次修正解除B1部分可见边拾取和B2六组映射阻断。**不代签独立1eb/e8dd UV分支的串行集成或部署，不自发布。**

## 固定代码、输入和真实受测身份

- 实际受测完整代码：`53f9871e55574ec66a96f12904293515ce40c23c`，产品修复提交 `2e0c8bd`；两者 `web/src` tree均为 `07ded43c9321e7e76a8177865c2852cc4fbd1db3`。
- 与c710相比，生产差异仅 `web/src/components/MeshViewer.tsx` 的拾取和 `web/src/components/ChecksTable.tsx` 的分类。相机、布局、geometry/material、UV/data、Capture/acceptance/mask和内核没有差异。
- 原最终probe `docs/reviews/web/ui-c710/Review.cs` **未修改**，SHA256仍为 `8f05cb5fbdbb06d10e5894c52527be8c384eac2495305535eda6cb42cc63dace`。其输出的 `candidate` 字段仍字面为c710，**本次after JSON中的该字段是旧probe标签，实际执行的是固定53f9871代码**；以本附节及 `docs/reviews/web/ui-c710-fix-independent/actual-source.json` 绑定为准，不能把标签当成测了旧base。
- 在自有 `temp_docs/ui-53f9871-review/web` 用固定git archive取源码、独立构建；只读复用既有node_modules。`pnpm --dir temp_docs/ui-53f9871-review/web build` 实际exit0，仅既有chunk-size提示。没有操作作者tree。
- 输入仍为原审计的数据副本：index SHA256 `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`，run `20261110T195008Z-610e3ce8`，case `support-chain.asymmetric43-base`，1965×1228、Before、默认25%、linked。

为原probe逐字节不变，执行期间只在自有temp目录临时将它既定的 `ui-c710-review` 路径指向固定53源码/数据目录，结束立即恢复原目录；原源码、输入、日志、结果未覆盖。恢复后原c710完整报告JSON hash仍为 `421c57bf91c37ce7cab6e785a5001865f0693efa29fdf4c4b5f2471666a911f2`，原picking JSON仍为 `98b55b44a86bdc9e2409bcad0219f61940ecc3deb6b9627a53642810be4a3a79`。原9ef文档和probe在本轮git diff中均无改动。

## 真实exit与窄复验结果

| 实际执行 | exit / 结果 |
| --- | --- |
| 原最终 `Review.cs`，固定53代码 | **0，28/28**；只运行一次，0 pageerror |
| 同一原probe `-- --picking-only`，固定53代码 | **0，8/8**；只运行一次，0 pageerror |
| 本目录 `NarrowBoundary.cs` | **0，8/8**；两类有限选择边界、逐ID六组及29行显示核对，0 pageerror |

使用命令均为 `.NET 10` 的 `MSBUILDDISABLENODEREUSE=1 dotnet run <file.cs>`。本轮没有重跑作者30+38套件、旧c710全套、PK、新数据采集、NativeAOT或其他内核构建。原始完整输出在 `temp_docs/ui-53f9871-review/{review,picking,narrow-boundary}.log`，真实exit单独保存在同名 `.exit`；提交内保留对应原JSON和日志。提交日志只去除行尾空白，边界probe的两行既有NuGet编译警告将本机绝对路径前缀归一为项目相对路径，未删失败或改变检查内容；完整原日志仍在temp目录，三份JSON逐字节保留。

### B1：两处25%原失败、端点误选与正确控制

| 点击坐标 | 原c710实际失败/控制 | 固定53实际结果 |
| --- | --- | --- |
| GM (545.5,649.5)，25%像素中心 | `gm:edge:104`，unchanged | **`gm:edge:107`，selected** |
| PK (1369,648.5)，25%像素中心 | `pk:edge:166`，unchanged | **`pk:edge:148`，selected** |
| GM (1075,648)，原右端区域 | `gm:edge:115`，unchanged | **`gm:edge:106`，selected** |
| PK (1898.5,648)，原右端区域 | `pk:edge:77`，unchanged | **`pk:edge:107`，selected** |
| GM 50%/75% (729.5,649.5)/(917.5,648.5) | `gm:edge:106`正确控制 | 同一正确selected边 |
| PK 50%/75% (1553,648.5)/(1740,649.5) | `pk:edge:107`正确控制 | 同一正确selected边 |

这是原固定输入上实际canvas点击的结果，不是仅核作者新截图。Before仍两侧10faces/20edges/102triangles；相机/target、可见边长度与黄描线未被此修复更换来绕过原输入。

### B1边界：不是把selected强制放到所有表面之前

源码逐行核对：产品拾取没有case/tag/屏幕坐标的特判，也没有按 `role==='selected'` 强制挑选。普通Line先以几何范围和投影像素大小求有限world broad phase，Line2继续screen-space broad phase；随后**所有边**统一投影到canvas CSS坐标，以相同3px近邻上限和各自实际stroke半宽判断。Line2的总宽1.5px附加量与narrow phase的每侧0.75px一致。stroke命中优于仅近邻命中；深度相近时按实际renderOrder/像素距离排序。原far clip约500导致的约0.25模型单位宽锥已经移除，没有通过坐标换位或ID白名单让失败点表面通过。

真正写depth且opacity>0的前景面会过滤后方边；不写depth的半透明上下文不会掩掉实际可见描线。下面两类有限控制均保持同一真实mesh，使用标准iso view，不改数据/材料；原三角形独立ray查询核对实际前景，点击仍走产品真实浏览器处理：

| 有限控制类别 | GM / PK实际结果 |
| --- | --- |
| 普通上下文面与透过透明上下文的普通后边仍可选择 | `gm:face:48` / `pk:face:180`为实际前景普通面；另点选到普通 `gm:edge:102` / `pk:edge:181`，其前景三角形为unchanged透明面 |
| 不透明前景必须挡住后方边 | 后边 `gm:edge:104` / `pk:edge:166` 的投影点，实际选择前景 `gm:face:66` / `pk:face:111`（support），没有强制选后边；独立ray最前不透明face与之吻合 |

这些点的输入来源及ray列表在 `docs/reviews/web/ui-c710-fix-independent/narrow-boundary-report.json` 中保留。视角由舍入telemetry重放作有限三角形验证，不能宣称所有视角、DPR、边交叠或透明排序完备；也没有把普通边3px邻域等同任意设备的精确抗锯齿覆盖证明。原9ef真实背面depth-test和orbit/playback控制仍作为原已接受范围保留。

### B2：六组逐ID与29行逐值

额外probe从实际DOM展开的每组读取全部原check ID，逐集合匹配c9b88机器审计，而非只数summary标题。结果如下（UI可按原行插入次序显示，表中按审计语义排序）：

| c9b88组 | 数量 / 实际成员 |
| --- | --- |
| primary | 1：`0-equivalence` |
| overlap-counts | 5：`0-count-0..4` |
| operation-contract | 5：`gm-0-api`、`pk-0-api`、`0-True-blend-fault`、`0-False-blend-fault`、`0-body-count` |
| body-validity | 2：`0-True-body-check`、`0-False-body-check` |
| mass | 15：`0-mass-0..14` |
| scenario | 1：`oracle-exit` |

29个ID唯一、无遗漏、无重复。对每行还核对原status、label、GM/PK值、abs/normalized delta、阈值、单位、note和evidence URL；全部与同manifest一致，按原10位有效数字显示规则比较，没有改结果来凑组六组。不同case有其他原记录时保留“其他”类别，不能把本case六组要求扩成每个case都必须伪造六组。

## 原声明切片整体接受与后续边界

原28项再次确认默认收起验收、overlay不挤画布、Tab/Escape/focus返回、320/390有限视图、同pair pose、After真实绿面、原主PK摘要/flags、缺失compare未记录、不借旧case通过、manifest采集时间/代码/17–463范围/单样本和历史性能提示保持。raw global21921/local21930、facePairs7、12条n_cht_pts诊断及max1024/allfalse/accfalse/toldevtrue语义没有更改，亦未把644.54x当新优化结果。

因此接受**固定53f9871此次声明的viewing UI整体切片**，并结合9ef中其余不变布局/相机/图例/数据呈现已过范围；不是重新签署原c710，也不声称所有模型交互完全无误。UV将从独立1eb/e8dd固定分支串行集成后另测，此接受不代签那次集成、live/unit/portal/main或发布。没有修改实现树、内核、验收、原数据或任何运行服务。

## 证据指纹

| 提交内原结果 | SHA256 |
| --- | --- |
| `docs/reviews/web/ui-c710-fix-independent/after-browser-report.json` | `c7216bfe52fe422b09e33826ebe60ae4b3cfe13ab117d6bd1f38b53202704a56` |
| `docs/reviews/web/ui-c710-fix-independent/after-picking-report.json` | `aed9cd4ca08bc9b4032a450f98c031e2596d8c01d3827819f8dd08e33b8eac0f` |
| `docs/reviews/web/ui-c710-fix-independent/narrow-boundary-report.json` | `6fe9db5305b13989032a513776fe621f84e553c58dfeae4becf2f02406dd9401` |

原probe未改；新边界入口只引用其经hash门控的通用浏览器初始化代码，读取固定53独立preview和原c9b88映射。原作者 `docs/reviews/web/ui-c710-fix/remediation.md@53f9871` 已读作变更说明，以上真实exit和结果为本轮独立复验，没有将作者30+38运行写成本轮执行。
