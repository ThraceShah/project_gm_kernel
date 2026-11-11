# 独立审查：真实 U/V 单例闭环 f3

结论：**真实四份资产、面绑定、矩形 trim 与有限显示正例通过；来源 consumer 接受 BLOCKED（P1）**。当前选择的 case/step/kernel/stage 未成为验签上下文，完整且 hash 正确的其他阶段资产能被当作当前阶段绘制。不得据此接受整体 consumer、串行集成后发布或宣称已上线。

本审查在独立 worktree、`ThraceShah/gm-web-real-uv-f3-review` 分支进行。固定代码/数据 `b5858a763b4930f2f671323e74b75a55778130bf`，文档后 final `f3fb9432ae97609f34391cffef819f62a9dd161d`，对比已接受 UI424 `424e85d6aac32c2afe7cad12cfd04bea4f0037ab`；两候选提交之间只有实现者报告变化。该实现者报告是被审查材料，本报告不继承其接受结论。未跟随作者分支后续移动；未改作者 tree、生产实现、原测试期待、live、unit、portal 或 main。

执行顺序为：独立核实完整来源及身份 → 只重载四份既有 XT 验证参数线 → 独立构建与有限浏览器正负例 → 原 UI424 必要回归。没有重新构造模型、重采样 case、运行主几何/AOT/性能/Mac 全套。

## P1：来源身份只在派生资产之间自洽，未绑定当前选择

位置：`web/src/api/dataLoader.ts:140`、`:232`、`:243`、`:255`、`:328`。`fetchMeshData` 只接收目录/路径，缓存键也只有目录/路径；`attachVerifiedDisplay` 核对 item 与 mesh/ISO 的来源字段，却没有当前 case、sample、step、pane kernel 或要求的 stage。stage 只要求属于 before/after，无法证明等于当前选择；缓存命中甚至直接返回，完全不经过本次来源校验。

独立生产构建的 Chromium 反例均只路由替换 `data/display/real-uv/manifest.json`，**没有改动 mesh、ISO、display-manifest 或其 SHA 字节**：

| 输入变动 | 应有结果 | 实测结果 |
|---|---|---|
| `cases[0].steps[0].gm.after = cases[0].steps[0].gm.before`，先加载 Before 再点击 After | GM 拒绝当前选择不匹配的资产 | After 高亮；GM 为 snapshot-2 / before，96 triangles，U5/V5；PK 为 snapshot-1 / after，130 triangles，U5/V5 |
| `cases[0].id = "review.wrong-case"`，`steps[0].id = "operation-999"` | 当前 case/step 与来源不匹配，拒绝 | 两侧均加载原 case / operation-1 的 Before 网格，各96 triangles、U5/V5 |

第二个反例使用新 browser context，从冷加载即被接受；第一个反例还覆盖 path cache 复用。它们是同一个上下文绑定缺口，不是曲线几何错误，也不依赖伪造 SHA。原包所有 hash 正确并不能防止选错完整资产。

可复现探针：`MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewWebRealUvF3Consumer.cs`（先在 `web/` 按锁文件安装及构建）。本固定候选退出 **1**：未改包正例通过，两个拒绝条件失败，三页均无 uncaught error。完整观测在 `docs/reviews/web-real-uv-f3-consumer.json`，截图 `docs/reviews/web-real-uv-f3-wrong-stage.png`。已将精确反例提前报告协调端；未修生产代码或更改测试期待。

修复验收需要把被选中的来源上下文传到加载/绑定边界，并在缓存命中时仍校验；至少覆盖上述两个不改 asset SHA 的反例，以及当前 pane kernel。此处只给出缺失契约，不指定重构方案。

## 来源与真实参数线正例

只读源 run：`../gm-operation-recording-r2/temp_docs/operation-receipt-curved-eecab/data/runs/r2-curved-receipt-eecab6d`。实际 case/sample/step 为 `solid-blend.plane-nurbs` / `sample-0` / `operation-1`；clean producer 为 `eecab6d51cae79a687fa459af512adc3e03895f5`，`dirtyAtStart=false`，execution exit=0、timedOut=false。该 producer 的 Native 源码与 `610e3ce` 相同。kernel/stage 来自各 receipt，未用 snapshot 编号推断。

已实际读取 `parasolid-research` 技能，实际调用 MCP `parasolid_get_function_detail` 查询 `PK_FACE_is_uvbox`、`PK_FACE_contains_vectors`、`PK_SURF_eval`，均 found=true。官方原型与固定 PskernelSharp 调用一致；contains options 默认 version=2、is_on_surf=true，UVBOX 四值与动态结果一致。未根据空 callgraph 推断无调用，未复制厂商反编译内容。

统一 `gm-private-env ensure --profile native` 返回 ENV_READY，Linux x64，白名单校验 cache，PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`。使用项目既有 `ParasolidScriptHost` 和真实 PK 38.0.150；没有自写 P/Invoke/session 或把 pskernel 指向 GM。

独立探针：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ReviewWebRealUvF3.cs -- --source-run ../../gm-operation-recording-r2/temp_docs/operation-receipt-curved-eecab/data/runs/r2-curved-receipt-eecab6d
```

该命令退出0，只 receive 四份原始 XT；不运行 exporter、不构造替代 PK body。探针独立核对：完整 mesh/XT/receipt/execution/ISO SHA；public mesh 与 original bytes 完全相同及原 relative receipt link；所有 receipt/mesh/received-XT face 的一一唯一绑定；execution/manifest sourceHashes 与 immutable producer Git blob。两个 producer 源文件的 SHA 实核为：

- `scripts/WebProgressCapture.cs`：`da417ff2733163be641b97416f48014ebaca73e28f628e50203b3fe69735eaa8`。
- `scripts/WebProgressDisplayReceipts.cs`：`76e44ab354d31a9f80fcc28ac2afb2f5fa4f54eee044b8f64b0a94bd939e4204`。
- execution 全文：`afab2ce66467fb9690d84e02a5e7b2d82cd3dbc43f033c84b50a69046b72f279`。

| Receipt | kernel/stage | 唯一绑定面数 | 有线 mesh face / identifier | U/V | 真实求值 XYZ 顶点数 / 最大误差 |
|---|---|---:|---|---|---|
| snapshot-0 | PK before | 6 | pk:face:111 / 36 | 5/5 | 170 / 0 |
| snapshot-1 | PK after | 7 | pk:face:146 / 98 | 5/5 | 170 / 0 |
| snapshot-2 | GM before | 6 | gm:face:38 / 75 | 5/5 | 170 / 0 |
| snapshot-3 | GM after | 7 | gm:face:193 / 115 | 5/5 | 170 / 0 |

完整四组 asset SHA 保存在 `docs/reviews/web-real-uv-f3-evidence.json`。680 个发布顶点逐个与独立 `PK_SURF_eval` 求值比较为0，每个 UV 均经实际 face contains 检查；U 恒 u、V 恒 v。四个实际 face 均通过 `PK_FACE_is_uvbox`，其 trimUvBox 与发布值各坐标差小于1e-12。没有把整个 surface 参数范围当作任意裁剪面，也没有把一般 wireframe 当作 U/V。After 使用独立的 GM115/PK98，而非固化75或跨内核 hardmap。

源 producer 的 GM 分支实际通过 `ParasolidDisplayBridge.Create` 接收 GM 自身导出的 XT；PK 分支保留自身 snapshot，显示网格 tessellator 标签为 `Parasolid PK_TOPOL_facet / PK_GEOM_render_line`，描述的是实际显示求值器。独立审查的 receive 只用于显示复核，没有用独立新建 PK body 顶替 GM 或缺失 before。未发现本增量引入 PK fallback、旧1b源标签、Round6，或把 source tag 本身作为内容 hash。

parameterHash 始终 null/unverified；原 receipt 的 snapshot-0 为 null，其余 recorded 值不全相同。ISO 只把它们保留为 `recordedParameterHash` 诊断，没有补造“已核参数 SHA”。

实现的弦差为 quarter/midpoint 自适应采样，16初始段，显示阈值0.001；保存 double 参数和 XYZ，未宣称解析全局误差界。原 mesh 是近似 float tessellation，0.01 mesh 显示容差与线采样弦差、主几何验收分开。未覆盖平面及非矩形 trim，unsupported/unavailable 分支不替代为边界线；此样例不构成任意 case/all trim 支持证明。

## 独立构建、有限浏览器与原 UI424 回归

在新 tree 执行 `pnpm install --frozen-lockfile`、`pnpm run build` 均成功；production artifact 独立产生。最初 npm ci 因仓库没有 npm lock 退出，最终构建使用现有 pnpm lock。构建保留既有大 chunk warning，managed probe 保留既有 NuGet/binding warnings；没有 NativeAOT compile/publish。

Chromium 使用 ephemeral loopback server，不访问 live。Before 两侧96 triangles；After GM220/PK130，每侧均有一个实际 newBlend、U5/V5，dimmedOpacity=0.25。打开并自看 `docs/reviews/web-real-uv-f3-before.png`、`docs/reviews/web-real-uv-f3-after.png`，两方向线落在可见曲面上；After 使用 Front 后向下观察过渡面，而非要求当前默认相机解决另分支的新截图布局。

实际像素证据：

- Before 在相机/triangleCount 不变时比较开关逐像素差：GM 4465个变化像素，含青3144、紫253；PK 4358个变化像素，含青3035、紫257。蓝色 support 面本身不计作变化线像素。
- After 相机 damping 收敛后：GM 青623→474、紫554→0；PK 青634→490、紫547→0；关闭后 drawn lines=0，恢复后U5/V5。
- 保留初次错误视角下 net-cyan 方向不成立、Before 大片蓝色 support 导致 net-cyan 反向的探索记录于 evidence 的 `exploratoryDiagnostics`；它们没有被写作几何失败或成功。最终 Before 使用相同位置的变化像素，After 使用收敛相机的同色阈值，不改生产测试期待。

真实 WebCrypto 拒绝验证：GM/PK mesh 各附加一个空白字节，保留原 SHA，受影响 pane 变成0 geometry/0 lines；GM/PK ISO 各附加空白字节，保留原 SHA，真实 mesh 保留而 lines=0。另将 GM ISO face identifier 改为-1并更新 ISO asset SHA，仍被面绑定检查拒绝；将 ISO caseId 单独改错并更新 asset SHA，仍被 ISO/item identity 检查拒绝。中断真实 GM After mesh 请求会清空旧 GM geometry，PK After 仍U5/V5。这些成功负例不抵销前述“两个文件自洽但当前选择错了”的反例。

12个唯一 mesh/ISO/summary URL及由summary反向引用的mesh/ISO均返回200；没有 raw XT 依赖。summary 类型明确为 render-only-receipt-summary，携带原 receipt hash，没有冒充原始 receipt。公开包扫描未见 raw XT、vendor binary、私有路径或凭证。

显式 `?display=real-uv` 才进入单例；无 query 时仍选择原 index.latestRunId `20261110T180347Z-90acd4a9`。单例 diagnostic、checks为空、performance与ratio=null、correctnessQualified=false；浏览器性能 tab 显示未录入性能数据，没有继承旧644等其他 snapshot 指标。390px 两 pane 为124/123px高，U5/V5；新 legend/drawer/camera 布局由独立分支处理，本次未据此拒绝或接受该布局。

原 `web/tests/smoke.mjs --inspection-only` 必要回归最终 **38/38，exit0**：destination camera ownership/延迟/反向/恢复、reset/orbit/播放、真实 picking与selection清理、tool geometry/pixel/camera、旧源不可用、320px pane高度均通过。因8089被其他 tree占用，使用 `scripts/ReviewWebRealUvF3Regression.cs` 只把固定端口改为ephemeral、恢复 stdin的原 `__filename`；所有原断言未变，未停止其他服务。早期端口/启动与该新探针序列化错误均发生在完整断言运行前，最终只执行了一次完整38断言；临时 I/O 子进程尝试只得到局部检查，未冒称完整通过。日志 `temp_docs/real-uv-f3-review/ui424-regression-final.log`。

## 容量与交接边界

| 实测构成 | Bytes |
|---|---:|
| 新 render-only 包 | 167168 |
| build 的 UI-only（排除 dist/data） | 863275 |
| build 的 dist/data（含新包） | 17918485 |
| 其中原有历史 data | 17751317 |
| 整个本地 dist | 18781760 |
| dist/data/fixtures | 0 |

已区分本 tree 的历史数据与实际发布时的现 live 约28MB data。**没有用整个 dist 的18.78MB来判定最终发布通过，也没有把整份 dist当成新增UI负载。** 若 publisher 按 UI-only 排除 dist/data，必须在之后的明确装配里保留新 `data/display/real-uv/` 并合并真实保留 data，不能静默丢掉样例。本次未访问live测量或删原data腾空间；最终串行合并layout后须对最终decoded bundle重新验收≤30MiB预算和≤32MiB硬门。

与424及610分别比较 `web/data/index.json`、`web/data/runs`、`scripts/WebProgressCapture.cs`、`scripts/RefreshWebProgress.cs` 均无差异；原 oracle exit/checkVersion/quality 未改，原建模结论未继承为新样例结果。没有刷新采集、宽范围测试、性能或平台结论。

交接：先修当前选择上下文绑定并复核两个反例，随后由协调端另派串行layout集成、必要回归与最终发布容量验收。本次独立审查完成，候选 consumer 门保持 **BLOCKED**；没有合并或部署交付。

## 追加窄复验：固定 1eb9308 的原 P1 定向接受

本节仅针对 `1eb9308bc400aa7b4cfac44222fc9ec66738a4fb` 的选择上下文修复，结论为 **原 P1 修复定向接受**。前文对旧 f3 的 BLOCKED 及 `67b5a4edbc23582c23d75f4e8ecce9b14265d42a` 的原证据保持有效，未撤销或改写；不把该窄接受扩展为布局、其他几何能力、串行集成或发布接受。

固定源码放在本 review tree 内独立 detached worktree `temp_docs/uv-1eb-source/`，HEAD 实核为上述完整 SHA，未追作者树后续变化。原 review 分支只追加文档和本轮证据；未 cherry-pick 生产源码。固定提交相对 f3 仅改变 `web/src/App.tsx`、`web/src/api/dataLoader.ts`，新增 `web/src/api/realUvDisplay.ts`；`git diff f3fb943 1eb9308 -- web/data scripts src` 为空。故全部原 mesh、ISO、display-manifest、summary、历史资产以及 Capture/内核源字节未变，原曲面/trim/来源动态证明仍指向同一资产。

静态契约复核：

- 独立编译描述符固定 run、case、step、sourceRun/sourceCase/sourceStep/sample/producer，分别固定 GM/PK × before/after 的 meshPath 和 sourceSnapshot；display-manifest SHA 固定为 `0875f9b8208db2d248e3a4fc912b0f5a077d75deaba8b3d1553b840dfb732714`，与原完整文件实核一致。
- App 从当前选择取 `selectedRunId`、`currentCase.id`、`step.id` 和 `subState`，按 pane 的固定 GM/PK 传 selection；这些字段在载入边界与独立描述符比较，未从待验 item 填回 expected。mesh render key 增加 runId，防止选择变更期间沿用旧结果。
- `fetchStepMeshes` 与 `fetchMeshData` 均在 path 解引用/缓存命中之前校验独立选择和阶段路径；缺失映射与该显示源未定义的 tool 映射会拒绝。缓存键包含 fixture、directory、path、runId、caseId、stepId、kernel、stage，无法仅靠相同路径复用其他上下文。descriptor pin、item/source 身份与既有 ISO/mesh 校验共同构成验证链。
- legacy 普通 runs 走原兼容路径，不被要求拥有 display receipt；没有把新显示源严格契约强加到历史网格。

在固定 worktree 内独立执行锁文件安装及 production build，TypeScript/Vite exit0，仍只有原大 chunk warning。原 `scripts/ReviewWebRealUvF3Consumer.cs` 在原 tree 和执行副本中的 SHA 均为 **`4278d8ef82e0fd565c6554c1cb6eb715140c0f14867074ff45d471555bd5ebb4`**，逐字节未改；只复制到固定 worktree 的 scripts 目录运行，没有改任何断言或生产代码。执行 `MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/uv-1eb-source/scripts/ReviewWebRealUvF3Consumer.cs` 返回 **0**：未改包正例、错 case/step 拒绝、warm Before 后把 gm.after 指到 gm.before 的拒绝均通过，三页均无 uncaught error；旧反例在新候选中清空错误 pane 的 geometry/lines，未悄悄使用旧网格。

有限浏览器复核共12项通过，原探针3场景另列于 `docs/reviews/web-real-uv-1eb-evidence.json`：

| 针对性检查 | 实测 |
|---|---|
| 正常四份 snapshot | Before GM/PK 各96 triangles、U5/V5；After GM220/PK130、U5/V5；标题仍为目标 case，provenance 分别为 GM snapshot-2/3、PK snapshot-0/1，与所选 Before/After 一致 |
| Before 同位置真实变化像素 | GM 青3144、紫253；PK 青3035、紫257；开关时相机与96 triangles不变 |
| After 同位置真实变化像素 | GM 青228、紫552；PK 青145、紫547；相机收敛且220/130 triangles不变，恢复后U5/V5 |
| GM Before 被指向真实 GM After | Before GM拒绝、PK正常；再选合法 After，双侧220/130与U5/V5恢复 |
| GM/PK Before 路径对调 | 错 pane均0 geometry/0 lines；合法 After 仍正常，未全局禁用正确快照 |
| display-manifest 仅追加一个空白字节 | JSON语义及其内mesh/ISO SHA未变，两侧仍因独立descriptor全文SHA不匹配拒绝 |
| 原路径已 warm 后改 context | case、step、stage、kernel、run 任一错均 data=null并有明确error；warm GM After作为Before亦拒绝；合法Before/After及拒绝后的合法Before仍available |
| 缺失映射 | `fetchStepMeshes` 的 undefined ref在该严格显示源返回data=null及mapping error |
| legacy17无receipt | 原默认run `20261110T180347Z-90acd4a9` 的目标case原mesh确无displayReceipt，两侧各96 triangles正常绘制、lines=0且显示“无等参线” |

warm-cache 边界复核使用固定原始 `dataLoader.ts` 另行 esbuild bundle 成 browser ESM，`import.meta.env.DEV=false`，由临时 `/__review_loader.mjs` 提供，不向产品dist写探针、暴露调试接口或替换生产代码。先合法请求使实际模块缓存变热，再逐项以相同path调用导出的真实加载API并改变selection，最后验证合法调用仍成功；不能将这些拒绝归因于冷缓存或错误item自证。App映射、真实像素与旧反例则使用独立production build验证。

`temp_docs/uv-1eb-review/before-valid.png`、`temp_docs/uv-1eb-review/after-valid.png` 已打开自看，双向线落在可见曲面上；截图SHA及原探针结果保存在新增evidence文档中。未重跑 PK/surface 导出、采集、全38 browser、AOT、性能或其他平台测试；未修改布局、拾取、历史数据、live/unit/portal/main或发布。余下串行layout集成、必要集成回归与最终30MiB/32MiB装配发布门仍由协调端另派，不能用本节接受代替。
