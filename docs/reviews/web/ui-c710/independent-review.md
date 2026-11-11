# 固定c710截图整改独立复核

**结论：blocked，尚不建议发布固定候选。** 主要视图、相机、原PK比较与数据版本的整改已具备可核验改善，但实际黄色选中边部分区段拾取成未修改边；同时29条原检查只有5组，与本轮要求的六组不一致。接受下表明确通过的有限范围，不把阻断扩称为几何或PK验收失败。

固定候选 `c710171bfea776a77217a3c417bc73f7906ac4fd`，base `424e85d6aac32c2afe7cad12cfd04bea4f0037ab`；候选web tree为 `d16c3bf7246e8e3013aff86ccdd21e553ef8f0f1`，base为 `cc9022e510f86fc839a6e767499407ad165fdf11`。独立报告分支 `docs/review-web-inspection-c710` 从本审计原 `c9b88af` 派生，保留原PK审计；没有编辑实现者tree、内核、Capture、比较mask、UV来源、验收、历史data、live、unit、portal或main，也未执行PK/新数据采集。

## 输入、隔离与原始exit

由固定git archive获取候选 `web` 和作者报告至 `temp_docs/ui-c710-review`。作者报告是 `docs/web-inspection-layout-camera-candidate.md@c710171`，不是独立结论。依赖只读复用既有node_modules，在独立源码目录执行 `pnpm --dir temp_docs/ui-c710-review/web build`，exit0，只有既有Vite大chunk提示；dist内历史data未用作截图输入。

只读复制 `../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/bundle/data` 到自有 `temp_docs/ui-c710-review/data`；源及副本index SHA256均为 **`117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`**。本地临时HTTP服务的 `/data/` 专门读取该副本，退出时关闭，不部署。run为 `20261110T195008Z-610e3ce8`，截图case为 `support-chain.asymmetric43-base`，1965×1228、默认25%未修改半透明、linked、Before。复核还实际打开After和一个不同case，并检查320/390×844；没有使用旧跟踪数据冒充截图run。

原始执行：

| 命令 | 实际exit / 范围 |
| --- | --- |
| `MSBUILDDISABLENODEREUSE=1 dotnet run docs/reviews/web/ui-c710/Review.cs` | **1**；28项，25pass/3fail，0 pageerror |
| 同命令加 `-- --picking-only`，首次setup | **9**；Node把mode解释成option，未启动浏览器，属于自有工具参数错误 |
| 修正Node参数分隔符后，同picking-only命令 | **1**；8项，6pass/2fail，0 pageerror |

第一次完整探针的两次点击取黄色像素的左上角，且点靠近右端，存在端点/像素边界歧义，因此不能单独据此断言普遍拾取错误。后续唯一一轮专门使用黄色线横向25%、50%、75%的**像素中心**，两侧共6次真实浏览器点击；中段仍有失败，确认下列阻断。原exit、原结果和首次setup错误均保留，未将探针错误称实现失败。

原始日志与JSON在 `temp_docs/ui-c710-review/review.log`、`picking-setup.log`、`picking.log`、`independent/report.json`、`independent/picking-report.json`；可复现C#入口是本目录 `Review.cs`，所有路径相对脚本自身解析。它只构建临时浏览器/HTTP进程，不调用PK，也不写源data。

## 阻断B1：可见黄色描线部分区段选成未修改边

| 侧/黄色线横向位置 | 屏幕像素中心 | 实际inspector | 应有选中边集合 | 判定 |
| --- | --- | --- | --- | --- |
| GM 25% | (545.5,649.5) | `gm:edge:104`，未改动实体 | `gm:edge:106/107` | fail |
| PK 25% | (1369,648.5) | `pk:edge:166`，未改动实体 | `pk:edge:107/148` | fail |
| GM 50% / 75% | (729.5,649.5) / (917.5,648.5) | `gm:edge:106` | 同上 | pass |
| PK 50% / 75% | (1553,648.5) / (1740,649.5) | `pk:edge:107` | 同上 | pass |

点击依据实际canvas黄色像素，不是按估算相机坐标盲点。不是全部LineSegments2失去raycast，也不是只有模型端点有歧义。`MeshViewer.handlePointerDown:496–516` 同时raycast所有实体并取最前的有id命中；普通Line阈值来自 `(pose.far-pose.near)*0.0005`，LineSegments2则使用2.5px描线自身的screen-space逻辑。这给作者一个有限定位入口，但本报告不把该观察直接当作已证唯一根因；也不建议通过关闭深度遮挡或无限增大点击阈值掩盖命中竞争。

LineSegments2继承Mesh的统计风险已独立实际核验：Before两侧均 **10faces / 20edges / 102triangles**，与原mesh faces/edges/indices逐项一致。源码先判断 `userData.type==='edge'` 再判断Mesh，故本case没有把黄色线网格误算成面/三角形；清理路径也覆盖Mesh。错误属于交互拾取，不是这里的计数污染。

## 阻断B2：原29条全部保留，但只有五组

实际UI组依次为API return2、辅助/拓扑/故障/exit9、BODY_check2、主比较1、物性15；总29未丢行。用户本轮要求六组，原审计映射应为主比较1、重叠拓扑计数5、操作返回/fault/body-set5、双BODY_check2、物性15、原场景出口1。当前把exit及多种义务揉在9条辅助组，又把API return2单列，未落实六组映射；报告不把5组说成6组。

修复应只按原check ID重新分组，完整保留原值、阈值、失败/其他状态、证据链接，不改任何验收结论或为凑组六组复制行。后续有限复验为六组行数1/5/5/2/15/1且总29，并验证原failure不被隐藏成pass。

## 已接受的截图整改范围

| 用户目标 | 本轮实际证据与结论 |
| --- | --- |
| 共享小字单行图例在3D外 | desktop只有一份，位于canvas上方；320/390保持自身单行横向滚动且文档无横向溢出；通过 |
| 验收默认收起、展开遮盖3D | default无dialog；打开后canvas仍823×984级大小，overlay从main顶部覆盖canvas，宽1645、高1171；展开29行后内部scrollTop确实可变化；Tab留在dialog，Escape关闭并返回原调用summary；通过 |
| 查看修改可看到真实feature | Before两侧48/48采样无遮挡，投影长度均约675.37px，实际黄色线清晰；After点击查看修改后两侧48/48、投影面积约57172.9/57273.1px²，实际宽绿面约59411/59397像素可见；视觉通过，拾取另由B1阻断 |
| 双侧公平相机 | Before与After实际pose/target两侧相同；本轮点击交互后切换snapshot不抢镜；作者固定610输入报告另有linked orbit、manual orbit跨step/playback、unlink/relink、424 delayed/aborted/recovery及run切换通过，明确作为复用作者证据；不冒称本轮独立重跑57项 |
| 主PK摘要与原flags | 实际读取manifest的compare路径；截图global21921/local21930/facePairs7及12条n_cht_pts诊断，max1024/allfalse/accfalse/toldevtrue及其余true完整可展开；原local未说零差异；不同case的实际raw结果也核对，缺失证据显示不可用而不借前case通过；通过 |
| 原数据时间/代码/样本 | 数据面板取manifest run.generatedAt、commit、scope和configuration；包含17executed/463registered/samplesRequested1，未混入index.generatedAt；644.54x只在历史性能detail并标非本轮优化结论；通过 |

比较与时效语义沿用 `c9b88af` 原审计：7是返回配对记录数，不是已成功匹配面总数；12chart诊断不等于raw无差异；29是原记录数而非能力数。此UI展示不执行新的PK验收、不改变生产mask。

## 视觉改善与测量边界

本轮打开并检查独立截图 `temp_docs/ui-c710-review/independent/desktop-before.png`、`desktop-after.png`、`320/390-closed.png` 和 `320/390-overlay.png`。Before黄色边处于两pane中部，After宽绿色倒圆带有真实填充面积；主画布没有被旧大图例或常驻验收表挤占。手机截图在本轮缺失compare路由负控之后拍摄，显示不可用是故意的缺失证据状态，不能拿它冒称正常raw已读；正常raw在本轮desktop另有直接核验。

复用作者同610数据的baseline/candidate原产物，而非重建整套baseline：`../../../../orca/workspaces/project_gm_kernel/gm-web-focus-layout/temp_docs/layout-camera/{baseline-report.json,candidate-report.json}`。作者tree当前HEAD确为c710；candidate报告57项无失败/无pageerror。baseline报告记录Before投影长度334.4px、48/48本就无遮挡，candidate约675.4px，**Before不是由“看不到”变成“全无遮挡”**；黄色像素196/197→1499/1502还包含描线从约1px到2.5px，不把全部黄色增量归相机。After baseline采样可见20/48及17/48，candidate48/48，同时作者绿面填充与本轮独立截图支持改善；投影/无遮挡采样使用候选cameraUtils，baseline又由rounded camera telemetry重放，因此是有限视角启发式证据，不是独立几何等价/最优相机证明。

作者额外38项ownership/tool suite使用较旧跟踪run `20261110T180347Z-90acd4a9`，只作历史辅助证据，**不替代本轮指定610截图数据**。本报告未重跑该全套，也未从其绿色结果推断B1拾取必正确。

## 后续最小动作与未验证项

修复B1的可见线命中竞争，先仅复验上述两处失败像素中心及四处正确控制，确保仍depth-test、未改相机公平性或mask；修复B2后只核六组映射及29原行。没有证据要求重新采集PK数据、跑全部case、重算bench或更新历史时间。固定c710仍blocked，以上通过项保持有限接受；报告不自发布，也不批准新的未审SHA。

未证明所有模型/相机/设备像素比下的遮挡与拾取完整性，未做完整历史suite、实时线上部署核验、内核/UV/PK新运行或稳定性能测量。相机数据的数值一致检查使用组件舍入后的telemetry并结合共享pose源码，不能冒称独立测得所有浮点camera matrix逐位相同。
