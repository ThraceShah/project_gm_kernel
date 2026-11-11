# 本轮审计证据与复现

审计基线 `610e3ce844bf99c85af6069905f9f3deaab08133`；只读其他固定源码/日志，写入自己的docs和两个独立C#诊断工具。真实PK控制没有GM引用；没有新增内核/测试预期/阈值、CSV、Web源码、服务或NativeAOT改动。

## 最小真实PK控制

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/BodyCompareAuditControl.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false
```

需要项目既有 `third_party/PKToy` 与真实 `third_party/parasolid`（或Host既有相对环境配置）；不提交任何库/schema/license。此次独立新树初始化固定PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`，runtime只读复用既有依赖。首次构建因新树缺本地依赖而失败，补本地忽略的依赖路径后相同工具退出**0**；首次失败是环境准备，不计成建模失败。没有启动testhost；未清理其他worker进程。

原日志 `temp_docs/strategy-audit/body-compare-control.log` 的关键原文：

```text
default wrong-height: max_diffs=0 global=21921 local=21931
equal block: global=21921 local=21931 face_pairs=0
wrong-height block: global=21921 local=21930 face_pairs=5
  faces=106/280 diff=21952 entities=76/250 counts=0/0 values=0.010000000000000009/0
PASS rejected wrong-height block
wrong trim wire: global=21921 local=21931 face_pairs=0
  wire candidate edges=320/332 endpoint=1 distance=1 ranges=0,1/0,2
PASS rejected wrong trim wire
translated wire: global=21921 local=21931 face_pairs=0
  wire candidate edges=320/344 endpoint=0 distance=1 ranges=0,1/0,1
PASS rejected translated wire
PASS pure-PK comparator scope: equal positive, wrong solid/trim/translation negatives; no GM assembly
```

错误高度是1×1×1与1×1×1.01；线体分别[0,1]、[0,2]及y平移1。无GM程序集由进程内已加载程序集断言检查。默认比较的成功不能当几何相等；严格helper确实检查local并拒绝尺寸变化；wire比较还需有限几何辅助。这不是GM建模操作资格、不是对所有图形的完备证明，也没有尝试用该控制替代原升阶/插结正例集。

## MCP与官方说明核对

实际服务入口是 `https://qingyu1-linux.tail77e06.ts.net/apps/parasolid-query/mcp`，通过已连接Parasolid MCP工具调用，非仅阅读技能。输出摘要：

| 调用 | 观测 |
| --- | --- |
| get_function_detail(PK_DEBUG_BODY_compare) | found，legacy分类，摘要不足以说明所有选项；直接callgraph含QTP_compare_bodies |
| get_function_detail(PK_BODY_check) | received body/options，返回n_faults/faults；文档为checks the given body |
| get_function_detail(PK_BODY_boolean_2) | target、n_tools、tools、options；返回tracking/results；直接callgraph含BOO_function_2及tracking/return处理 |
| lookup_error(157) | PK_ERROR_cant_do_intersect |
| get_function_detail(PK_SURF_eval) | 给定surface、UV和导数阶数，返回point/derivatives；不是完整建模API |
| get_function_detail(QTP_compare_bodies) | internal；摘要含face interior/point-in-face等；不据此推断精确数值算法或时耗 |

本地官方FD **106.7 Comparing bodies**：默认global只比较拓扑数量；max_diffs>0启用匹配face的local比较；wire无face所以无local几何差异；chart-point差异不影响曲线形状。绑定 `third_party/PKToy/PskernelSharp/Types.g.cs` 的compare options默认num/tol/class/sense/missing/nonmatch为true；helper显式max1024/allfalse/accfalse/toldevtrue保留这些默认。官方章节导航见parasolid-docs技能，不复制授权文档全文或反编译代码。

## 指纹与证据来源

以下R3日志来自独立review所保存的 `temp_docs/r3-frame-review/`，对应固定source9b，报告提交8c9fed1；本轮只读，不把它们称本轮新执行：

| 文件 | SHA-256 |
| --- | --- |
| rolling19.log | `a4570a8a35c4f35beda83ffcfd8d541ba27a313416df47e384ccf5f364d0de4a` |
| rolling19.exit | `4355a46b19d348dc2f57c046f8ef63d4538ebb936000f3c9ee954a27460dd865` |
| candidate-probe2.log | `cea0f3ec1334b398585bdee774c315a940c17c596e56e01fe1ef22ee4df8dcfc` |
| vertex-intervention.log | `c83bb8924f9ab225d49c0f8a62664403872bcae8ac334acf7d1592fa673376b3` |
| native-check.log | `c015611bd997c12278aa1a9e3ded4d3261faf460816af1f161184a3c34f9973c` |
| first-carrier-trace.log | `448535a44656a56ff712d93388713877d18198c1990d0eab1fc7b7497c82e8cc` |
| 本轮body-compare-control.log | `8a31e7f7c0e9ecc0483e1d890c840f0cb770f167d5583de739159c86d62575b2` |
| 本轮BodyCompareAuditControl.cs | `5bedca2fb7b1532b7149d2247319f395cc04de372b186535337afac943519e4d` |

## 有限盘点复现

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/AuditOperationAcceptance.cs
```

生成器只读固定git对象610e3ce，在本分支输出 `docs/reviews/advanced-modeling/operation-acceptance-audit-610e3ce.json`；不依赖当前移动HEAD，不运行任何被索引oracle/test/bench。用JsonNode写primitive，避免反射序列化依赖；不做NativeAOT验证。`catalog[].staticReferences`中native是精确符号，managed标准化名字仅标候选，不冒称解析了全部转发/helper。`operationOccurrences`为词法顺序，不能据此得出动态链或误把fixture创建判为被测目标。`checkpoints`列主辅/表示/资源候选标记，实际归属由规则与账本判定。

较新历史17case中的bore性能原始分量（毫秒）：Fix GM1863.865/PK1.6129，Boolean GM3905.8619/PK1.0246。因此case比值是 `(1863.865+3905.8619)/(1.6129+1.0246)=2187.5741800947862`。这只证明原case ratio包含两步；原单步没有独立primary owner，不能据分量另造两个qualified ratio。

所有历史manifest/capture保留原commit、status、checks和timing；本清单只读摘取并绑定来源。还原不了的细分结果应标legacy-aggregate/未验证，禁止追溯捏造pass。没有为了盘点运行463、19、96、完整bench或full suite。

最终生成器连续两次退出0，输出JSON SHA-256相同：`9136c3b9f8814fd88badd70973c31ff7d5aeba7e6882bef5780ff8f44f64fbef`。最终计数175/463/155脚本/152测试文件/1073个属性及方法/27份review Markdown，未分类API和未索引测试属性均为0；这验证枚举稳定性，不升级为全分支语义审查。`git diff --check`通过。
