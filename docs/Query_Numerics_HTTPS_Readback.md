# ab597 发布实际 HTTPS 读回

首次读回对象为 `20261112T132500Z-queryab597`，发布收据固定提交 `f0da3f9931775df6c8bea674bb2ec2ea421166b1`。这里只读实际 HTTPS、解压/比对证据和原模型来源，没有重新执行建模、PK、积分、suite 或 UI review；原计算/证据 `ab597` / `f633` 及 279 制品保持不变。

## 已核实内容

`scripts/ReadQueryNumericsPublication.cs` 首次读回 `online-readback-v1` 实际取得 21 个 HTTP 200 资源，59 项硬核对完成；工具原 exit **2** 表示下面的发布元数据差异，不能称为上线闭环完成。原脚本字节、响应、逐 URL/长度/SHA 与日志留存 `temp_docs/query-numerics-closure/online-readback-v1/`；机器收据为 `docs/query-numerics-closure-online/readback-v1.json`。

- 实际 index、整目录、events、ab597 manifest、四份证据和 12 个模型 JSON、Projection manifest 均读回成功。整目录 463 个稳定 ID 无重复，五具名圆柱的 query 维度为 pass，来源为正确 ab597；60be 历史 5000 失败仍保留。
- manifest 实际计算 `ab597e8a24b2ef23753507c8ca3e43d12edcc650`、交付 `f633b51db703df308cf4ac194e3c2b1661398d4d`、独验 `3e7ac1b1273c56530bfbd50f0cb6bdffb08e4fd7` 正确。manifest 为 22,199B / SHA `e8bb5c626e9e2154c24e5c53ac6238b8b0551966817d9be2ce63e313a777d742`。
- `results.gz.json` 按 gzip magic 处理，实际 257,328B / SHA `3f29e8dc9965978bbef9e7e0b7db1b509e4b6e119b9ebbd653368e91a29db270`；解压后 **5,247,186B / SHA `aed7c9787a03bbb798f7a7be0f9f3b832400c15956af205a7b55662e4866bd60`，与本地 f633 原 JSON 逐字节相同**。没有按 `.json` 后缀误读，也没有改写原结果。
- 五圆柱原 20 点及输入来源与原结果一致；12 个公开 chamfer mesh/source JSON 与本地实际 Capture 逐 JSON 相同。两个 fillet 没有伪造补网格，manifest 明示容量限制并保留完整原证据。
- 510 summary 与四组原列逐字段相同：direct 15/16U/native 七行，Capture 15/16U/native 八行；旧 self 的 495 条原列缺失和所有当前完整列由无损原结果保留。
- 新 norm、Joint 72 原报告旧新账与本地一致（新 492V/588U，48 项 V→U 等保留）。实际 Projection manifest 仍明确旧合同 396V/684U/0D，没有被 ab597 重判覆盖；顶层明确功能/物性与性能分源，不表示两个分支同 binary。
- 实际 overview 与固定发布提交只差自动刷新的 `generatedAt`，其余 JSON 内容相同；events 与固定提交字节相同。没有把生成时间变化当作内容错源。

## 需同一发布 owner 更正的来源/结果元数据

以下是实际数据内容核对，不是另开 UI review，也不要求重跑计算。完整原数据和模型正确，问题在 manifest 的派生描述/资格字段：

1. 三个 chamfer 条目 `parameters.radius` 及 `apis.*.parameters.radius` 被写成 `.1`。实际 `PK_EDGE_set_blend_chamfer` 没有 radius 参数；单/双 rim 是 range0/range1=`.4/.4`，unequal 是 `.3/.6`，实际 mark radius=null。容差 `1e-5` 与 Capture 一致，不是本次差异。
2. run-detail `query.probes-510-classification` 被标 `status=pass`、`performance.correctnessQualified=true`，但原 direct 15/16U 未取得四方相等资格。overview 的对应记录正确保持 unresolved，两个层级矛盾；应保留证据审计接受与几何分类未决的区别。
3. 上述 510 条目的 source 写成 `scripts/ConeAxisProbe.cs`，实际 producer 是 `scripts/ShortEdgeBlendOracle.cs`（见原执行 argv/PDB/source）。
4. `mass.norm-reassessment-joint` 的 source 写成 `scripts/ReviewNativeQueryMath.cs@ab597`，该文件在所称版本不存在；该独验脚本属于 review 分支。作者历史重判实际入口为 `scripts/ReviewMassCertification.cs@ab597`，应按真实来源绑定。
5. `suite.linux-managed-2025` 填了 PK 侧 `dotnet test` API 与 2025 计数；原执行是一个 GM 托管套件，不存在同次配对的 PK 2025 套件。期望计数可以记录，但不能标作 PK 实测。

另有两项应按已接受原文澄清的来源描述：Joint 本次是只读标准重判，不能由 `operation/apis=PK_TOPOL_eval_mass_props` 的通用配对模板暗示新执行了 GM/PK 双边积分；FAQ 新增“严格求交/修剪/拓扑重构是 1e-8/1e-5 局部逼近”的普遍断言，独验原文没有给出该算法精度范围，应回到“有效性/容差比较不等于全域零误差和积分预算”的实际结论。

这些差异不撤销 `3e7ac1b` 的 Native/数学接受，也不改变原始模型、2025/2025 或未决数值账。当前仅完成真实内容读回并交发布 owner 自修；在更正资源实际上线并窄读回前，不结算整个任务完成。

## 修订发布窄读回完成

发布 owner 在固定提交 `4e5b97254f29668b8d736f41c92143b1a0783a1e` 自修上述元数据，并发布修订 run **`20261112T072500Z-queryab597`**。本轮不重开 UI review，不重跑计算，也不等待其他性能事件。

`ReadQueryPublicationRevision.cs online-readback-v2` 的真实执行原 exit **0**：**5 个 HTTPS 请求、115 项核对、0 差异**。机器收据为 `docs/query-numerics-closure-online/readback-v2.json`；原响应、日志与 exit 位于 `temp_docs/query-numerics-closure/online-readback-v2*`。首次窄核辅助脚本有一处括号编译错误，原 exit 1 留存为 `online-readback-v2.log/.exit`，没有执行 HTTP；修正后的实际执行日志/exit 为 `online-readback-v2-final.log/.exit`，两者未混写。

- 新 manifest 实际 HTTP 200，25,730B / SHA `72822fa82b97dbdfcad89a1eeb24ca7d3ef18be2a82881180773450f3bc222df`；计算/交付/Native 数学独验仍为固定 `ab597` / `f633` / `3e7ac1b`。
- 两个等距 chamfer `.4/.4`、unequal `.3/.6`、radius=null、两个 fillet radius=.4，以及 GM/PK 两侧 mark/fix 和实际 tolerance 均与原 Capture 一致。
- 510 详情与原两 direct check 为 diagnostic、correctnessQualified=false，15/16U 保持；producer 为实际 ShortEdgeBlendOracle。Joint 为 ReviewMassCertification@ab597 的只读报告重判，无新双边积分语义；套件为 GM 单侧 2025，通过数与原执行一致，PK 不适用且计数为 null。FAQ 无依据的普遍精度区间已删除。
- 463 整目录、五 query pass、功能/物性与性能分源保留；其他有效 run 的索引对象与首次实际读回逐 JSON 相同，只替换已证错元数据 run。
- 新 run 的 gzip 实际 HTTP 200，与首次真实 HTTPS gzip **逐字节相同**（257,328B / SHA `3f29e8dc9965978bbef9e7e0b7db1b509e4b6e119b9ebbd653368e91a29db270`）；再次解压与 f633 原结果 **逐字节相同**（5,247,186B / SHA `aed7c9787a03bbb798f7a7be0f9f3b832400c15956af205a7b55662e4866bd60`）。完整当前列、历史缺失、Joint 新旧账及原输入/质量身份均未改。
- 旧错元数据 manifest 实际 HTTP 404；声明的只读归档全部 **19 文件**逐长度/SHA 核同，原 manifest 仍为 `e8bb5c626e9e2154c24e5c53ac6238b8b0551966817d9be2ce63e313a777d742`。
- 按协调要求没有重复 HTTP 下载 12 个未改模型；逐个核实“首次实际 HTTPS 响应 == 完整只读归档 == 修订发布源文件”字节/哈希链。此项为明确的来源保持证明，不补造第二次 HTTP 模型观察。

**本任务代码、集中独验、数据发布及实际读回交接已闭环。** 首次 readback raw 2 和旧错元数据原件保留，不被本次 raw 0 洗绿。Native/数学接受仍仅限独验报告明示范围；510 四方相等未决、历史 self 其余 495 原列缺失、Joint 重判 588 个未决分量和一般曲面/任意 precision 认证边界均继续保留。没有宣称 main 合并、合一 binary、全部 M0–M5 完成或 PK 性能目标达成。
