# 固定 424 相机归属修复：独立定向复验

**结论：接受固定 `424e85d6aac32c2afe7cad12cfd04bea4f0037ab` 的查看体验小切片，可供协调端单独发布。** 原 V3b 持续错误相机已关闭，所测异步切 case、空 case、失败恢复、run/step/stage 归属及同 case 手动视角保持通过；工具、选择和透明显示未见定向回退。该结论不是部署完成，也不包含真实 UV、host 迁移、ledger、导出器或内核验收。

本轮为非实现作者的独立 review，工作树分支 `ThraceShah/gm-ui-424-review` 从上述固定 SHA 起步。只读固定源码和静态数据，没有追作者 working diff，没有修改生产实现、测试期待、历史数据或其他工作树。原 d75 拒收报告仍在 `ThraceShah/gm-operation-inspection-d75-review` 的 `a3d8db052b6d8c01f96092b08bb574111bf3a34c`，文件为 `docs/web-operation-inspection-d75-independent-review.md`。下方原作者记录保留；本独立结论以本节证据为准。

## 独立证据与原复现

- 一次 `pnpm run build`（工作目录 `web`）：exit **0**。TypeScript/Vite 成功，保留已有 bundle-size warning。`web/dist/assets/index-DQUBkM3M.js` SHA256 为 `9d469d1e0b2d3b5915dc3bac688088987e93d8e8c16ee63c0cdbaf0e02c5cfcf`；CSS SHA256 为 `1b6fa52bed50b05a2bc87a2aff99695ba2d4d790a10dbd2ba782fdb86b9b1d89`。
- 先读取/hash 原 `../gm-scheduler-review/temp_docs/op-inspection-d75-review/v3b-repro.mjs`，再原字节复制到 `temp_docs/ui-424-review/v3b-repro.mjs`。两者 SHA256 均为 `4fe0044f29b9cbc7dd036b7aa40d7c8de078e9f220b24ca81200d030f9b3d455`，原文件未修改。
- `REVIEW_URL=http://127.0.0.1:18984/ node temp_docs/ui-424-review/v3b-repro.mjs`：exit **0**、`stale=false`。日志、原 exit、JSON 分别保存在本轮证据目录的 `original-repro.log`、`original-repro.exit`、`v3b-public-repro.json`。脚本内嵌 candidate 仍写 d75，是原标签，不是本轮受测 SHA。
- 原 d75 exit **1**、`stale=true` 仍是有效历史反例；本修复起点 `eb3c5e630518bd9eb105993673e6f54d78204194` 的 sidebar workaround 已使原单方向例 exit 0，不能把 424 宣称为从原例失败到成功。本轮额外验证通用时序和归属机制。
- 独立重跑 `node tests/smoke.mjs --inspection-only`（工作目录 `web`）：exit **0**，38 项全部通过，未跑全量 smoke。日志/exit 在 `temp_docs/ui-424-review/inspection.log`、`inspection.exit`，逐项结果在 `temp_docs/inspection-remediation.json`。
- 另通过独立 Chromium/Playwright 会话执行 **30** 个检查，0 失败、0 uncaught page errors。`temp_docs/ui-424-review/observations.json` 保存相机、双侧 canvas RGBA SHA256、颜色像素数、实际 URL、34 份成功 mesh response 的字节 SHA256/source/body/triangle 和预期 abort；`frame-transitions.json` 保存 RAF 观察，`verdict.json` 保存结论。临时材料按项目约定保留 ignored，不冒称已提交附件。

## V3b 与实际归属

`web/src/App.tsx:176` 的 key 包含 fixture、manifestDir（实际 run 数据目录）、currentCase.id、safeStepIndex 和 before/after stage；`:236` 在受 request ID 保护的完成回调中同时记录请求捕获的 key，`:458` 只把匹配的 mesh 交给子视口。路径来自对应 manifest step 的 gm/pk ref，没有把当前 case 标签贴到旧 data 上。`:207` 的 request ID、AbortController 和 loader signal 共同阻止被替代的有效请求进入视口。

`web/src/components/MeshViewer.tsx:77` 在 case/run 上下文变化时开放首次 fit；`:456` 要求已构建出 face 或 edge 才消费。cameraContext 排除 step/stage，同 case 切快照不会重新 fit。`:311` 的 resetTrigger 只在 counter 增大时执行显式复位，不因 mesh 完成而再次消费旧 reset。

| 独立场景 | 实测结果 |
| --- | --- |
| blend → Boolean，真实 mesh 延迟 750 ms | loading 两侧 bodyId 空、triangle=0；primary/tool 到达后 GM `gm:27`、PK `pk:600`，各 12 faces / 24 edges / 24 triangles。自动 fit 等于显式 Header 复位 |
| Boolean → blend 反向返回 | 两侧恢复各 96 triangles 和初始 blend pose，没有沿用 Boolean 相机 |
| 手动旋转 → 无 steps 的 missing case → 返回 blend | missing 清空两侧；返回首份真实 mesh 才 fit，恢复初始视角 |
| 同 step 延迟 after 900 ms，160 ms 后切回 before | 等待时旧 before 清空；恢复 before 后再等 1200 ms，迟到 after 没有替换 before，两侧仍 96 triangles，canvas hash 保持 |
| 未缓存 Boolean primary 请求 abort | 新页面确认两份实际 GM/PK request 均 abort；两侧 triangle=0、bodyId 空并显示网络错误。移除拦截，经另一 case 返回后重新请求，24 triangles 和初始相机恢复 |
| run 切换，mesh 延迟 700 ms | loading 清空旧几何；从历史 step1 after 手动姿态切回当前 run 同 case，加载当前 run step0 before 实际请求，GM `gm:12`/200 triangles、PK `pk:698`/212 triangles，重新 fit 等于显式 reset |
| 历史 initial-adapted run 的 regular-curved.block-subtract 多 step | step0 before：GM/PK 200/212 triangles；step1 before：132/174；同 step1 after：144/186。延迟加载期间均无上个 step/stage 几何；bodyId、faces、edges、triangles、URL、响应 SHA 对应各自 manifest ref |
| 同 case Header reset → 手动旋转 → after / playback | 手动确实改变相机，快照真实变为 GM 220 / PK 130 triangles，仍保留手动 pose；playback 和多 step 手动 pose 也保持 |

Boolean 自动视角 GM position `4.34,3.55,2.57` / target `0.40,0.40,1.00`，PK `4.19,3.40,2.57` / target `0.25,0.25,1.00`；反向 blend 为 `1.78,1.46,2.14` / `0.50,0.50,0.54`。这些是本数据观察值，不是任意模型的正确坐标。自动 Boolean 双侧 canvas SHA256 为 `1b4da1880936b1df1959317224b65610cd1fc9fffd6418578f225392df4f0c3c`、`daf3596342379c81cec60e6ec54a44d973f971acdf7800745882f195adcd878d`，蓝色像素约 9.05/9.89 万；loading 空状态仍有 grid/axis 少量颜色，未误称完全无像素。

实际打开查看了本轮 `temp_docs/ui-424-review/boolean-auto.png`、`reverse-auto.png`、`history-step1-after.png` 和 `temp_docs/inspection-mobile-320.png`。前两张可见完整模型，未重现原 d75 满屏裁切蓝面；历史多 step 截图保留用户视角，不以再次自动 fit 为通过条件。

## 原查看交互的定向回归

- 工具：Boolean before 两侧 12 faces / 24 edges / 24 triangles → 6 / 12 / 12；双侧实际 canvas hash 都改变，相机不变，重新显示恢复计数。工具来自原请求的实际 tool mesh，不只检查 DOM flag。
- 选择：真实 raycast 选中源 after mesh 的 unchanged `gm:edge:78`；isolate 后 inspector 清空、两侧 face 7→3，仅留 modified2/newBlend1，双侧像素改变。suite 另验证切 case 清空选择。
- 透明：实际截图仍显示灰色透明 unchanged context 和不透明操作面，相关生产逻辑本修复未改。没有扩张为完整透明算法或性能验收。
- 320 手机：suite 的 pane 高 93/92、closed sidebar 完全移出视口，截图已查看。现有 toolbar 仍需横滚，不宣称手机整体优化完毕。
- 旧网格两侧明确显示“无等参线”，suite 的 count=0，无替代曲线；仅验收 unavailable，**不算真实 UV 正向展示通过**。

## 历史数据与有限风险

`git diff 610e3ce844bf99c85af6069905f9f3deaab08133 424e85d6aac32c2afe7cad12cfd04bea4f0037ab --name-only -- web/data` 输出空；两者整个 `web/data` tree 均为 `a1f9069a1edf15ebae103465e99ab69e8cea0758`，覆盖全部历史 data。历史 plane-nurbs 的 `sample-0/meshes` 四份相同 blob：

| 文件 | 与 610 相同 blob |
| --- | --- |
| gm-0-before.json | `4423cccdfe0faa695d5ce5e4896aefa365f9285b` |
| gm-0-after-0.json | `557fa656a056059f1e2796d3b862b704ea29d601` |
| pk-0-before.json | `220437046f07f31d65c296c6f1cb7b32d77fc61e` |
| pk-0-after-0.json | `36e9e14a41f4e1d4c25a26f7066221dfd847a583` |

保留一个明确限制：run-select 切回当前 run 时，RAF 曾观察到约 **20 ms** 的过渡，dropdown 已换值，旧 manifest 的 DIAGNOSTIC 标签、旧 cameraContext 和 `gm:1137`/`pk:1693` 几何仍一致；随后旧 manifest/视口卸载，再出现新 PASS manifest 和空几何，最后载入新 run 数据。切 run 清理在父 useEffect（`web/src/App.tsx:99`），该生命周期沿袭 eb3，424 未改变；本轮不宣称 dropdown 与 manifest 切换逐帧原子。没有观察到旧几何挂在新 manifest 下，也未占用新 run 首次 fit 或持续保留，故列为非阻断过渡限制，不扩大本相机修复。

两侧 Header reset target 不同仍是原共享公平 fit 遗留，上述 GM/PK 数值即为实测；未引入新回退，不扩为全相机重写。无 PK 冒 GM/fallback/ident75/round6/UV 混标的安全收缩沿用已接受范围，本轮未重跑假 GM 导出或升级为真实几何证明。

真实曲面 UV 仍须独立 capture receipt、双侧实际 snapshot/XT 和来源绑定后的正向像素证据，本轮无该数据。未调用 PK、kernel、AOT、Mac 或全 159/347 测试；未改 ledger、计时、ratio、阈值、host 配置、main 或线上服务。

独立 preview 仅 `127.0.0.1:18984`，suite 自有静态 server `127.0.0.1:8089` 随 suite 退出；独立 Chromium 和 preview 均精准关闭，未清理他人端口/进程。报告只允许协调端据此发布固定 424 查看体验切片；portal/live 发布由协调端另行执行验证。

## 最小复验方法

固定 checkout 424，在 `web` 执行一次 `pnpm run build`，启动 `pnpm exec vite preview --host 127.0.0.1 --port 18984 --strictPort`。原脚本原字节复制后设置 REVIEW_URL 运行；独立页面默认 blend→Boolean，比较自动 fit/显式复位，再反向返回。对真实 `**/meshes/*.json*` 延迟 750 ms，160 ms 时读取视口和 canvas，完成后比对 manifest ref 原文件 SHA、body/计数和姿态。

用新页面避免 meshCache 命中，abort 两份 Boolean primary request，撤销拦截、离开再返回；验证实际请求恢复和首次 fit。stage 竞争用 before→延迟 after→before，再等超过延迟；多 step 用 `initial-adapted-a1e4500` 的 `regular-curved.block-subtract`，先手动旋转再切第二 step 和 after；切回当前 run，验证 step 重置、路径和新 run fit。无需 native 或生成新几何。

---

## 原实现作者记录（独立复验前已存在）

This slice replaces the sidebar-only mesh clearing workaround with selection ownership at render time. Each completed mesh request records its fixture/run/case/step/stage key. App passes its geometry to the viewports only while that key matches the current selection. Camera context includes the run and case, so snapshots and playback within that case retain the manually chosen view. Navigating invalidates the previous fit, including navigation through an empty case; only drawable geometry consumes the destination's initial fit.

The independent d75 review's failure remains recorded in `docs/web-operation-inspection-d75-independent-review.md` on review branch `ThraceShah/gm-operation-inspection-d75-review` at `a3d8db052b6d8c01f96092b08bb574111bf3a34c`. The starting implementation here was `eb3c5e630518bd9eb105993673e6f54d78204194`: its sidebar workaround already passed the original single-direction reproduction (exit 0, stale=false). This change does not claim that the original reproduction still failed at eb3.

## Verification

- `npm run build` in `web/`: exit 0, TypeScript and Vite production build; existing bundle-size warning remains.
- `node tests/smoke.mjs --inspection-only` in `web/`: exit 0, 38 checks passed, zero failures and uncaught page errors. Evidence: `temp_docs/v3b-fix/inspection.log` and `temp_docs/inspection-remediation.json`.
- The unchanged independent reproduction was copied into `temp_docs/v3b-fix/v3b-repro.mjs` to preserve the reviewer's original evidence. Both script hashes are `4fe0044f29b9cbc7dd036b7aa40d7c8de078e9f220b24ca81200d030f9b3d455`. Its fixed-build run exited 0, stale=false; output is `temp_docs/v3b-fix/fixed-original-repro.log`. Its embedded candidate label still names d75 and is not the tested implementation revision.
- With real mesh responses delayed 350 ms, the new case's loading render has empty body IDs and zero triangles in both panes. The subsequent Boolean snapshots have their actual body IDs (`gm:27`, `pk:600`) and 24 triangles per pane, including the tools. Both automatic fits match the corresponding explicit Header reset: GM position `4.34,3.55,2.57`, target `0.40,0.40,1.00`; PK position `4.19,3.40,2.57`, target `0.25,0.25,1.00`. These are observed poses, not general expected coordinates for arbitrary models.
- Reverse navigation restores the actual blend's 96-triangle initial view. Leaving a manually rotated blend through a missing case clears geometry and fits the returning valid model. Aborting two actual destination primary mesh requests clears both panes and displays request errors; retrying via another case fits the recovered Boolean correctly.
- The original Header reset → manual rotation → after snapshot and playback checks pass in both panes. Existing tool, selection, unavailable-isoparametrics and mobile checks also pass.
- Actual screenshots `temp_docs/inspection-new-case-auto-fit.png` and `temp_docs/inspection-reverse-case-fit.png` were opened and checked: both models fit within their panes. The recovered view is saved as `temp_docs/inspection-recovered-case-fit.png`.
- `git diff 610e3ce --name-only -- web/data` is empty. Historical meshes, manifests and index remain byte-identical to the main baseline. No capture, ledger, refresh, exporter, kernel, acceptance thresholds or live services changed in this slice.

This is a UI ownership fix for independent review. It does not claim shared fair framing for unequal GM/PK bounds, completed true U/V receipt integration, new geometry acceptance or deployment. Real curved-snapshot UV export remains a separate follow-up; no receipt or XT is grafted onto historical data.
