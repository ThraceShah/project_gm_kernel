# 独立融合 gate：固定 3c650671

结论：**首个完整产物的静态身份与有限融合 UI 行为通过；整合发布 BLOCKED（P1）**。固定 `1b935972` 工具在下一轮正常 producer 输入下会成功 prepare，却丢失全部15个独立 U/V 资源。首轮手工装配不构成持久发布闭环；本报告不接受当前集成发布，也不代签实际部署或 host/proxy。

审查对象为 `3c650671f30efad266531b0512ccfa600adffe38`，实现者报告 `docs/reviews/web/inspection-reviewed-integration/README.md` 是被审查材料。隔离源码为 `temp_docs/fusion-3c-source/`，工具为 `temp_docs/fusion-tool-1b/`，二者分别 detached 在固定候选与 `1b935972eff529e13b46cb9b08ad9550e0a6aeb5`；未追作者 tree 漂移，未修改作者源码或产物。

## P1：独立显示 namespace 无每轮持久来源

实际执行固定工具的 Freeze 后，再执行其**冻结副本**的 `PublishReviewedWebProgressUi.cs --mode prepare` 两轮，使用同一 frozen UI、同一 work和完全相同的 `--data` 路径/全部CLI参数。仅将隔离producer输入目录的首轮内容移到保留目录，再在原路径装入下一轮原main610数据副本；不修改真实producer或hook：

| 场景 | 输入 data | prepare exit | 产物 files / bytes | U/V资源 | 历史身份 |
|---|---|---:|---|---|---|
| 第一轮 | 607历史文件 + 手工装配15个display资源 | 0 | 625 / 28214664 | 15个全在 | index与607文件原字节保持 |
| 第二轮 | 原main610 producer data，607文件，无display资源 | 0 | 610 / 28047496 | **15个全丢失** | index与607文件仍原字节保持 |

第一轮整包工具 hash 为 `5f43710720d36735d1972666c4f80013fa00b8d3d58fc0a623b4ad2601c3c7e5`，第二轮为 `2aa48412fa6da242a7b987c0bf057068809dacf14f50645b388cb1f55adc0615`。工具没有报闭包或容量错误；新fingerprint会选中新的610文件目录，不会因同一UI或同一index跳过这个差异。

Loopback 实测第二轮全部 `data/display/real-uv/` 的15个URL均404；浏览器打开第二轮的 `?display=real-uv` 显示HTTP404数据加载错误，没有真实样例。失效包括入口index、manifest、display-manifest、四mesh、四ISO和四summary。未悄悄回退假线是正确拒绝行为，但无法修复发布后的入口断裂。

根因与实际入口已定点核实：

- 固定 `scripts/FreezeReviewedWebProgressUi.cs:34` 对所有 `data/` 排除；`:42` 只冻结该UI集合。实际freeze的 `release/ui/` 只有HTML/JS/CSS三个文件，15个display文件均不在UI hash保护/拷贝集合中。
- 固定 `scripts/PublishReviewedWebProgressUi.cs:197` 只拷贝UI集合，`:198` 从本轮 `--data` 拷贝全部非index文件。它不从上一轮bundle或previous-static叠加display资源。`:182` 的 `--baseline-data` 仅在整份非index集合相同时复用原index字节，不是namespace overlay。本反例的previous-static甚至保留全部15资源，仍不能保住第二轮。
- 固定 `scripts/ConfigureReviewedWebProgressUiHook.cs:182` 生成唯一 `dotnet run --file .../PublishReviewedWebProgressUi.cs -- --mode publish --execute`；`:183` 只传 `--dist --data --baseline-data --work --portalctl --previous-bundle`，随后传固定UI/review/hash。owned入口`:207`还要求producer data与旧hook一致，`:216`要求dist指向frozen `release/ui`，未存在独立pinned display overlay参数或合并步骤。
- 已读取工具文档后final `9d6a79e` 的 `docs/web-progress-owned-ui-upgrade.md` 通用参数：`--dist ../../ui`，`--data` 指向producer的 `temp_docs/web-progress-publish/data`，`--baseline-data ../../previous-static/data`。本轮据此隔离替换数据输入做prepare，未读取/操作真实unit，也未执行Configure的任何服务入口。

可复核最小探针为 `scripts/ReviewInspectionFusionRetention.cs`：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewInspectionFusionRetention.cs
```

前置条件是上述两个固定detached tree、候选的独立pnpm锁文件构建，以及任务给定的只读main610历史data和bundle源存在。探针所有项目路径均相对自身解析，复制到全新 `temp_docs/fusion-retention-repro/`，既有证据不覆盖。外层退出 **1** 表示保留门失败；Freeze与两轮prepare均原exit0，最终日志 `temp_docs/fusion-retention-same-path.log`。最初使用两个输入路径的等价复现保存在 `temp_docs/fusion-retention-separate-argv-evidence/` 及 `temp_docs/fusion-retention.log`，未删除；随后仅加强harness为同一data路径，要求15资源保留的断言不变，结果和整包hash相同。结构化原结果纳入本目录 `evidence.json`。

探针显式使用全零review pin作为私有测试标记，并把首包当作本地previous-static fixture，只验证freeze/copy/prepare契约；没有冒称首包已独立接受或已核当前served rollback。未执行publish、backup网络校验、Configure、portalctl、systemctl、timer或其它live操作。源bundle/data逐文件前后再hash未变。

最小可复核补救是让独立显示资源成为固定UI自身的一部分：例如放入**非producer `data/`** 的独立URL namespace，由UI hash覆盖并由每轮UICopy带出，再统一调整consumer与evidence URL；保持现有mesh/ISO/summary内容和原producer/index字节。这样可以沿用现有1b工具，而不把显示资源伪装成main610新采集数据。另一方案须是另行固定并审查的正式pinned overlay机制；本固定工具中没有这个机制，不能用一次手工拼包代替它。本任务未自行实现补救或引入新工具机制。

## 合并与首包身份

`5a60104f358b21fcb2c978ba9d250407178ce61e` 是真实双父merge：U/V `e8dd252b2a1baa848c2c59ad9c9b43dd9dc845ba`、layout `53f9871e55574ec66a96f12904293515ce40c23c`。生产web tree与 `6a433222da0775dcb5524d7751d94e8f096fe797` 完全相同。U/V的api/types/全部data与e8dd逐字节相同；cameraUtils、ViewportContainer、EntityLegend、ChecksTable、ComparisonView与53f逐字节相同。交叉App/Header/Viewer/geometryBuilder差异保留两方选择context、geometry ownership、共享camera/drawer、selected LineSegments2与真实U/V顶点颜色。

从固定源码独立 `pnpm install --frozen-lockfile`、`pnpm run build` 均exit0。三个UI文件逐个hash与作者首包一致，工具口径UI hash实核为 **`e0429cf28007de740018ebfc1898a630c1535be0bcdd668e4ed38b415a811ee4`**。只保留原Vite大chunk和现有.NET依赖/平台分析warning，没有NativeAOT编译。

首包按实现者inventory的625个路径逐一从loopback fetch：**625个HTTP200、逐文件size及SHA256全部相同，总28214664B**。UI3文件905462B、历史607文件27142034B、新display15文件167168B，真实完整包低于30MiB预算和32MiB硬门，没有prune或删除历史证据。原root index仍为 `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990`；历史607文件在两轮中均逐个保持，不更改时间、run或参数，也不向旧run塞U/V。两次正式prepare的数据闭包validator均无error；公开包文本/扩展名复核未见raw XT、vendor库、私有路径或凭证。

本报告没有把整个dist当成UI-only：冻结UI明确只有905462B，独立display不在该UI集合正是上述阻断的原因。容量通过不能替代资源持久性。

## 有限融合观察

只做17项有限正向检查及2项阻断现象观测，不重跑作者57/30/14/34全套，也不重跑PK、surface导出、Capture、几何、性能或平台套件。结果在本目录 `evidence.json`；其中验证“全部404”和“显示404页面”的true表示反例被证实，**不是发布门通过**。

| 检查 | 本轮实际结果 |
|---|---|
| 用户截图case `support-chain.asymmetric43-base` Before | 双侧各102 triangles、dimmedOpacity=0.25、黄像素1499/1502；selected feature48/48可见，共享pose/target一致 |
| 有限真实拾取 | 各pane黄线25%像素处点击，实际选中原mesh内的 gm:edge:107 / pk:edge:148，未仅验证按钮flag |
| shared legend / evidence drawer | legend唯一且在canvas外；drawer打开前后canvas bbox逐字相同；原compare global21921/local21930/facePairs7及mask/12项表示诊断保留 |
| 原main610来源 | run `20261110T195008Z-610e3ce8`、commit `610e3ce844bf99c85af6069905f9f3deaab08133`、采集时间 `2026-11-10T19:50:08.4502366+00:00` 保持；samplesRequested=1 |
| 历史单冷倍率 | 644.54x只在性能details中出现，明确single-cold-sample、warmup0、非稳态/非本轮优化结论；未成为新UI结果 |
| 独立U/V四snapshot | Before双侧96 triangles；After GM220/PK130；每pane U5/V5，receipt stage和GM snapshot2/3、PK snapshot0/1匹配；共享相机保留 |
| 真正U/V像素 | After开关改变GM6648/PK6613像素，其中青344/303、紫730/705；pose不变，关闭lines0，恢复U5/V5 |
| 390px融合空间 | 双pane205/204px，U5/V5；共享legend和drawer overlay不缩canvas |
| source/context拒绝 | 实际App warm Before后gm.after误指Before时GM清空、PK正常After；错case/step两侧清空；独立编译实际loader热缓存后错stage/case/step/kernel均拒绝，合法请求仍available |
| legacy与缺来源 | 截图legacy原mesh继续绘制、U/V=0；未从别的snapshot借线；有限主页面无uncaught errors |

暖缓存边界探针是固定source另行esbuild为production DEV=false的临时browser模块，不向产品源码或dist添加调试接口。实际App反例、真实像素和drawer操作均使用与候选hash一致的首包。

已打开自看 `temp_docs/fusion-gate/legacy-before.png`、`uv-after.png`、`next-uv-missing.png`、`uv-data-heading.png`。这保留已接受layout查看范围与 `39cbf947` 的U/V P1修复范围；原f3/c710拒收不撤销，不扩大为全部case/trim/几何能力结论。

另有**非阻断文案遗漏**：`web/src/components/InspectionPanel.tsx:165` 的data drawer共享h3在U/V模式仍写“已发布基线 · 真实数据来源”，与实现者报告“不会称为已发布基线”的文字不完全一致。下面的包生成时间/原来源代码/scope及无性能声明均明确，本轮未把它重新判为U/V来源/几何错误；建议后续限定该标题的render-only措辞。截图与实际字符串已保留，未修改产品。

## 交接

唯一阻断集成发布的发现是P1独立namespace持久来源缺失，已在报告完成前发送协调端。先固定最小补救，再以**同一fresh producer data第二轮**证明15资源仍可寻址且hash正确，并保留607历史/index原字节，然后才对新固定候选作窄融合复验。父分支的查看/来源接受保持分层有效；本轮审查完成且当前整合发布保持 **BLOCKED**，未改作者tree、历史、内核、Capture、live、unit、portal或main。

## 追加：固定015 namespace窄复验及合体范围接受

以上3c拒收已先独立提交并推送 **`ceeb898c2dfe0929f47f9e605aba3b2d7fdfb2b3`**，原报告、两轮失败现场、原探针均保留。本节是对新固定生产 **`015cd308e147356fecac96bb7354ab1e92c36a03`** 的明确追加，不撤销3c反例。结论为：**015修复通过，结合两parent接受和上述3c有限融合检查，可接受固定015的合体UI/有限显示及发布输入范围**；实际owned hook安装、backup/served核验、部署和host/proxy仍是另外的执行门，本轮没有执行。

独立detached源码为 `temp_docs/fusion-015-source/`，不跟随作者后续docs或browser working diff。固定diff只包含15份资源从 `web/data/display/real-uv/` 到 `web/public/display/real-uv/` 的100%字节相同移动，以及三个文件的URL路由更改：`dataLoader.ts`、`realUvDisplay.ts`、`InspectionPanel.tsx`。新 `progressAssetUrl` 在app base下把可信显示namespace映射到 `display/real-uv/`，普通历史输入仍映射到 `data/`；缓存键含实际asset URL及原完整selection上下文。原descriptor来源/阶段/面映射、display-manifest内容SHA、mesh/ISO SHA、trim gate和未知parameterHash契约未改。未发现native、Capture、几何、布局/相机/拾取或607历史字节变化。

新的独立复现为 `scripts/ReviewInspectionNamespace015.cs`，命令：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewInspectionNamespace015.cs
```

先在固定015源码tree按pnpm锁文件独立构建（exit0）。探针实际调用**相同固定1b Freeze**，再调用新release中的frozen Publish prepare；源码从固定Git对象提取，未改工具机制。两次均从原main610的607文件新复制输入，**均无display overlay**、同一data参数路径。首轮work整体移入保留目录，第二轮在原work路径独立重新装配，避免把复用已有bundle误称为独立第二轮。两个独立产物均625 files / **28214939B**，逐文件保持原607历史/index和原15份显示内容。新freeze真实包含18个非producer UI文件，显示15文件位于 `release/ui/display/real-uv/`，由UI hash覆盖；producer data只有原607文件。

| 固定015身份 | 实核值 |
|---|---|
| 工具SHA | `1b935972eff529e13b46cb9b08ad9550e0a6aeb5` |
| UI hash（固定1b口径，18文件） | **`f0593760ed1e22dd73e94f609e9e2353366370b542019a90cb8f486f796a4dd6`** |
| 第一轮及独立第二轮整包hash | **`8dca4b3500652fe0ff5161a8a5943abdcc5a69d0a40b472de17e95b1ca583069`** |
| 607文件producer data hash | `fbb68a2aacd7eebb910f63a51a23954eec340e9bc3960e000448c699565d7390` |
| 原root index SHA（两轮均不变） | `117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990` |
| UI18 bytes / producer607 bytes / full625 bytes | 1072905 / 27142034 / 28214939 |

625文件/28214939B低于5000文件、30MiB预算及32MiB硬门，无历史prune、参数/时间重标或新采集run。Prepare与外层probe均exit0，记录 `temp_docs/fusion-015-independent-retention.log`、`temp_docs/fusion-015-retention/retention-report.json`。先前使用同一work复用同fingerprint的检查现场另存 `temp_docs/fusion-015-cached-round-evidence/`，没有用它代替上述独立装配证明；3c的两个输入方案及日志仍原样保留。review pin仍为显式全零私有fixture标记，不是实际部署认可记录。

真实browser仅追加10项定向检查，证据 `namespace-015-evidence.json`。服务**仅**提供 `/apps/gm-kernel-progress/` 子路径，prefix外404，未用根路径fallback掩盖错误。四UV快照仍为Before96/96、After220/130，每pane U5/V5且正确snapshot/stage；截图 `temp_docs/fusion-015-browser/uv-after-subpath.png` 已打开自看。新namespace的mesh/ISO/summary引用全部200且与原已签字节相同，index/manifest/descriptor请求也实际经过该app前缀；旧case仍102/102 triangles、UV unavailable、run610原采集时间。没有把这轮当作新的surface/几何验收或性能结果。

负控明确匹配新URL并记录route触发次数，避免“旧pattern没有拦截却当成reject”：冷加载把GM Before指向真实After、warm Before后把GM After指向Before、错case/step，各实际拦截manifest **1次**；受影响pane清空，未影响的PK仍96或130 triangles。display-manifest附加空白字节实际拦截 **2次**，两pane均因独立pin拒绝。warm反例先实测Before两侧96、U5/V5再点击After，证明cache已热。没有改原f3 probe、断言或生产代码。初次请求分类把带query的document导航误算asset，产生一条harness false；已保留于 `harnessDiagnostics`，仅对同一批已记录request按pathname重分类，未重跑/放宽产品条件，实际所有asset路径均正确。有限主页面无uncaught error。

因此原3c发布阻断在**固定015**中闭合：新资源属于freeze/UI copy的持久namespace，不再依赖本轮producer或手工overlay，也没有引入未审工具机制。前文已记录的非阻断共享标题文案遗漏保留；两parent的有限查看/来源范围和此轮namespace结果共同支持上述固定015合体范围接受，不能扩大为任意trim、全部case或实际已部署。未重跑作者14/57等全套、PK导出/建模、采集或AOT；未触及live/unit/systemctl/portal/main或作者tree。
