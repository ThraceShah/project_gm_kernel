# PK 原生功能集成与当前 Capture 交付候选

固定计算和 Capture 代码为 **`1887e30d9050480daf38546f8cdd2df6592bd094`**，已推送至 `feat/pk-native-functional-integration`。这是集中核收候选；尚未宣布 main 集成、整体 M0–M5 全通过或新看板上线。性能 owner 的 Mac 方案未混入本分支。

## 固定来源与实际 diff

自动合并提交 `dfc5defe9483009669288f3359a2a97ba9b3bf99` 的两个 parent 为 Boolean 文档头 `aca329607b376032a31283cc0e659fbd54916d97` 和 Blend 固定源 `60be7216d646a296d7fd2d0b30b5f336a46a6a66`，没有冲突或手工解决。Boolean 计算源为 `4794e60111e1382eafe69880077544fcc135521e`，独审 `75eddad101f0adaaa4550180c73eb1d12fa64fe7`；Blend 独审为 `54f1b309d830ac78358ebcba78e1deec8e3c3b8f`。独审结论及支持范围见各自报告，不能由本次合并扩大：

- [Boolean 固定候选](pk-native-boolean-functional-candidate.md)与 [4794 独审](pk-native-boolean-4794-independent-review.md)。
- [Blend 60be 独审](blend-60be-independent-review.md)。

相对于 Boolean parent，Native 运行时只接入已审的 PlanarBlend 有限方向/临时拥有关系与 TopologyQueries 的 FaceAskOrientedSurf 组合；共同的 ProfileFaces sheet 修复保持相同字节。之后 `1887e30` 仅改 11 个 scripts 文件（397 加、52 删），没有 Native 核心、公共 C ABI、模型或几何公式变更。实际 diff 保存在 `temp_docs/pk-native-functional-integration/auto-merge-versus-aca3296.diff`、`merge-resolution.diff`；固定脚本 diff 可用 `git diff dfc5defe9483009669288f3359a2a97ba9b3bf99 1887e30d9050480daf38546f8cdd2df6592bd094 -- scripts` 查看。

当前 Capture 直接接收共享 AssertEquivalent 和 Compare 的**同一次实际调用**结果，删除旧的第二套 raw compare 和仅点值物性积分；没有旧格式兼容分支。比较记录保留原生 mask、返回码、global/local 原始结果、完整 face-pair/local-diff，以及实际执行的条件有限方向证明。物性记录保留 15 分量的 estimate、modulus、normalized lower/upper 和 Verified/Different/Unresolved；不改变原 helper 的分类、预算、阈值、计数或退出码。实际调用异常记录后继续抛出，观察器错误独立登记。

## 当前执行和操作级结论

下表日志和原始 `.exit` 均位于 `temp_docs/pk-native-functional-integration/`，不是检查器代签建模结果。

| 实际执行 | 操作结论 | 原日志 / 原 exit |
| --- | --- | --- |
| block-single-chamfer，自构造→copy/delete 源→镜像缩放→可编辑 XT reload→后续 cut | 前序产品和生命周期分别 fresh/full/strict/材料/物性通过；后续 GM/PK Boolean 各成功，一个产品，资源回零 | `negative-chamfer-v1.*` / 0 |
| oblique-unequal-chamfer 同上 | 实际负 surface 的公共 FaceAsk 与同框架 PK 一致；生命周期及后续 Boolean 分别通过 | `oblique-chamfer-v2.*` / 0 |
| block-single-chamfer 后续 Boolean 分配失败注入 | 前序仍独立通过；GM 实际 901/21653/零产品，target/tool 字节及池计数回滚；同输入 PK 成功。不是 GM 成功 Boolean | `chamfer-rollback-v2.*` / 0 |
| 当前 1887 完整 Capture 的 oblique-unequal-chamfer 操作链 | 自构造、生命周期、负 surface 查询、同字节后续 Boolean、fresh/full/strict/方向/材料及物性 Verified；双方 Boolean 各 error=0/result=21650/count=1，最终资源清理通过 | `captured-oblique-final-v1.*` / 0 |
| 当前 native 反例控制，Capture 关闭/开启各一次 | 真实错误半径和有限 wire trim 均 Rejected，真实材料分类翻转为 Different；两次未决物性计数均 3，60 行 CSV 完全相同；开启后 30 项 Unresolved 分量均未变绿 | `controls-off-v1.*`、`controls-on-v1.*` / 各 0；`off-on-csv-identity.exit` / 0 |
| 集成相关边界测试 | FaceAsk 四种符号组合、sheet/oblique 拥有关系、native 非线性有限根正反例、圆柱接缝 reload：13/13 | `merged-boundary-tests.*` / 0 |
| 当前正常构建分配 IL guard | 2925 方法，CLEAN；没有 AOT 发布 | `final-normal-il.*` / 0 |
| 当前脚本构建 | 8 个受影响 oracle/收据检查脚本全部构建成功 | `current-build-*.exit` / 各 0 |
| 当前 Capture 收据核对 | positive：3 次物性调用/45 行、6 次比较；controls：4 次物性调用/60 行、2 次比较。只是核对收据，不增加建模资格 | `check-positive-v1.*`、`check-controls-v1.*` / 各 0 |

当前正例实际记录了 8 次条件有限方向证明。负 surface 原始 face=18542/surface=18541 的实际公共查询为 false，与 PK false 一致；镜像、copy/delete 和重载后再次实际检查。没有据静态 FaceAttach 疑点补造失败或修改重附着 API。

早期编译错误、错误 IL guard 参数和检查器并发构建的复制重试保留原日志，不计入建模成功。当前没有重跑旧 63/17/123/290/96 的完整矩阵，按各独审固定范围复用，也没有重签原完整单测为全绿。

## 输入、执行源和产物

当前正例的产品目录为 `temp_docs/local-operation-oracle/integration-captured-final-v1/`，Capture 为 `temp_docs/pk-native-functional-integration/capture-oblique-final-v1/`。目标来自当前 GM 自构造且独立验证后的实际 live 产品，经 copy/delete 源、镜像缩放和 reload；没有导入 PK 成品代替自构造。后续 Boolean 将同字节 target/tool 分别交双方，保存真实版本 19、subtract=15902、tolerance=1e-7 的原生选项字节。

| 实际字节 | SHA256 |
| --- | --- |
| Boolean target | `0c3da45bd617f1222c1e8a809bb0a6bec8058fa901c5b99a82e6bf2431400ce3` |
| Boolean tool | `2bafc64cf83ebda0d48aef75ad7b08b24c28d151d7f0cc61c1b2e32d8c2ff89a` |
| GM Boolean 产品 | `1d73665e25f3bfccccbe6c87b16da0232baa59a37fe81a580fbb8303e97eba53` |
| PK Boolean 产品 | `ff6d96d89d1ab68582317c9c4786aec588346d164dcd70584e8fe3c10c14debc` |
| 实际 loaded GM DLL | `fff3758e998c65bbfeaa78c5bdd84cc94c7379f330c90034355857c25aaa6d17` |
| 实际 loaded LocalOperationOracle DLL | `390d1e9a6a2149e8a4a8fcf39a6bc1ca2d808c36e6f9a062bd59d010a0198af8` |

本次实际 live GM target tag=371、true PK 同字节接收 tag=1140 只是当前执行日志，不是静态验收标识。未来 replay 必须取其真实新标识。

固定清单：

- `temp_docs/pk-native-functional-integration/final-source.sha256`，537 项，清单 SHA256 `45af81fd2cc6572d9a25784a868580c85cd821e0c53c0ef4fbbc6a21f639054a`。
- `temp_docs/pk-native-functional-integration/final-artifacts.sha256`，68 项，清单 SHA256 `edb5c71f66a657b3fabcd995d6af92493928383705696f62f5b4f99a1624d9df`。
- `final-capture-kernel.identity.sha256` 保存实际 DLL/PDB；`final-receipt-check.log` 保存两个清单核对均为 0。清单覆盖固定执行源码与真实产品/Capture；本交接文档后增不改其字节。
- WebProgressCapture 源 SHA256 `33157f3d7dd79e433c566cc3c735ebe27ab03d4d22f6fb8a21d0132979ab9b08`，BodyCompare `2ef5f2ff029fd3a6f800ca11f51308b387ea53e5af3557513a283d5d9d192987`，MassProperties `1afc2dd45201d1244228c1a8b4658c8ffa35b3f988a9f12b46d62a228930279d`。

原始 Capture 的声明 revision 是短值 `1887e30`，外部收据 `capture-declared-revision-resolved.txt` 将其解析到上述唯一完整 SHA；没有改写原 Capture。新发布 replay 应按下面完整 40 位 SHA 声明。

## 复现与同实例入口

先用已授权 `gm-private-worktree-env` 准备本树 native profile，并确认 ENV_READY。当前环境为 Linux x64、PK 38.0.150、.NET 10.0.302；环境命令、原 exit 见 `environment.*`。下面在项目根运行，目录名换成新名，不覆盖本次证据：

```sh
GM_WEB_COMMIT=1887e30d9050480daf38546f8cdd2df6592bd094 GM_WEB_CAPTURE_COMMIT=1887e30d9050480daf38546f8cdd2df6592bd094 GM_WEB_CAPTURE="$PWD/temp_docs/new-integrated-capture" GM_WEB_SELECT=oblique-unequal-chamfer MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/LocalOperationOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- --output-name=new-integrated-product --blend-arrangement --lifecycle --managed-material --then-boolean
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/CaptureQualityContractCheck.cs -c Release -- temp_docs/new-integrated-capture temp_docs/local-operation-oracle/new-integrated-product positive
```

真实反例和未决控制（本次使用了固定输入 `temp_docs/native-acceptance-contract/native-contract-complete-control-v1/input.x_t`）：

```sh
GM_WEB_COMMIT=1887e30d9050480daf38546f8cdd2df6592bd094 GM_WEB_CAPTURE_COMMIT=1887e30d9050480daf38546f8cdd2df6592bd094 GM_WEB_CAPTURE="$PWD/temp_docs/new-integrated-controls-capture" MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/NativeAcceptanceContractOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- new-integrated-controls temp_docs/native-acceptance-contract/native-contract-complete-control-v1/input.x_t
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/CaptureQualityContractCheck.cs -c Release -- temp_docs/new-integrated-controls-capture temp_docs/native-acceptance-contract/new-integrated-controls controls
```

同实例入口是 `LocalOperationOracle.VerifyFollowingBoolean`：在实际 lifecycle 后以 live managed target 调用 `WebProgressCapture.Before`，随后由真实 BodyBoolean2 返回的产品调用 BooleanAfter。step 0 标为 independent-construction，step 1 标为 same-byte-fork，包含实际 target/tool hashes；生命周期质量单独绑定，未伪造新建模步骤。每张显示 mesh 绑定原 live body、display snapshot body、实际 content-addressed XT 与 SHA；归档中间产品明确标为 archived，不冒称当前 live 实例。

## 看板 owner 交接及结算条件

1. 从固定完整计算/Capture SHA 创建新的集成 run；独立核收前不得标为 accepted。保留旧 Blend/Boolean run 及原始红项，不能覆盖或回填旧原始数据。
2. 按 `checks[].evidence` 读取本次 `quality-compare-*.json` / `quality-mass-*.json`；不查旧文件名、不回退到点值对比。读取每份报告的 captureIdentity/sourceBinding，保留源文件、实际 loaded DLL、实际 XT hashes。
3. 基础展示 status 为 pass/fail/missing/diagnostic，领域 `agreementStatus` 原样保留。Unresolved 或缺少 modulus 不得变绿；真实 native API 错误与 observer 错误独立展示，不以命令 exit 0 消除反例的 Rejected/Different。
4. 几何、拓扑、BODY_check、有限方向证明、物性、材料、回滚和性能各自保留结论；证明报告保留 mask、原始 diff 和有限映射/范围/容差/两个 directed fin/full bound。不可只截一个表面绿灯。
5. 新 run 的实际模型/参数、step 0 自构造来源、step 1 同字节来源与 DisplayBody 实例逐项核对；不得用 PK 重建显示物替代 GM 产品。新采集 command/source/artifact/原 exit 一起存档。
6. 保留旧 510 self/import 的 15/16 差异、当前 imported 未决 16、native 自差 7 的独立事实；保留 Blend 5 个直接 BodyContains5000、不确定物性及原完整单测 1907/2。不得发布“全部 M0–M5/1909 全绿”或未执行的性能资格。
7. 协调者核收当前集成候选后，由看板 owner Capture/Refresh/发布，再将新线上 run URL、发布 SHA、source identity 与检查结果交回共同核对；这些完成后才结算本任务。当前报告不构成上线证明。
