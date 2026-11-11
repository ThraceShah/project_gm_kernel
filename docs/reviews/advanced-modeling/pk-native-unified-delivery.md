# PK 原生统一内核交付候选

本候选把已独立接受的 Boolean、Blend、当前 Capture 和累计 record/cache/G/Conic 优化实际合并，并修复统一源中重现的镜像八倍有限接触参数登记问题。计算及公共入口固定在 `4b2405e3945929c5bb93677a68b0ee79e430b2f3`，Native 项目树为 `133f56acb0cf485366cd4f13f457dc3b161655eb`。这是提交并推送的集成候选；集中关键集成独审、看板装配、上线和 main 合并尚未发生。

固定提交整树为 `dcac18749b996dcd9a59b3ae0fb66149aee3f9e2`，整个 `src` 树为 `0c5d23f6f4e3e7aabe5034888f92812c3ecb07ad`。三份固定 diff 为 `temp_docs/unified-delivery/final-versus-base.diff`、`temp_docs/unified-delivery/final-versus-functional.diff`、`temp_docs/unified-delivery/final-versus-performance.diff`；对应共同基点、功能父源和累计性能父源，没有使用移动 ref。

## 来源与实际合并

真正的 merge 为 `b9db9bb`，parents 是功能 `97174392b97e8c5976e56e2890c58314c74c73b5` 和累计性能 `f9ffc0985eb1e5ce53f649af66751de7c6c77353`。两者均为候选祖先。没有从移动 main 推断已集成，也没有导入未接受的 Bezier/Mac 实验或正式测量包装。

| 固定来源 | 接受记录 |
| --- | --- |
| Boolean `4794e60111e1382eafe69880077544fcc135521e` | `75eddad101f0adaaa4550180c73eb1d12fa64fe7` 的 `docs/reviews/advanced-modeling/pk-native-boolean-4794-independent-review.md` |
| Blend `60be7216d646a296d7fd2d0b30b5f336a46a6a66` | `54f1b309d830ac78358ebcba78e1deec8e3c3b8f` 的 `docs/reviews/advanced-modeling/blend-60be-independent-review.md` |
| 当前功能/Capture `1887e30d9050480daf38546f8cdd2df6592bd094` | `docs/reviews/advanced-modeling/pk-native-functional-integration.md`；原功能任务已经结算 |
| 累计性能 Native `58c1667b0155ec46b53be42b9750c8dbd78a75dd` | 6616 接受 `5a4ad5319b77c0944baf598e07dbbce79782a483`；G 接受 `3fdc109babe6eda489d985171e3808f956481be1` 的 `docs/Native_Spline_Cartesian_Independent_Review.md`；Conic 接受 `35e61e5e02e319d8f6b9eaba38f880e62f1bbf0c` 及测量续审 `6a7dfa159c762e90d8180e2f1ed77191b2d52a04` 的 `docs/Native_Conic_Independent_Review.md` |

上述评审路径在各自指定 Git 对象中读取，不声称全部属于当前分支文件。共同基点是 `f9c59201c0b9c5d103dee45124e3f43ca59a0921`。联合 Native 实际为 40 个不同文件（27+14−1），不是 41 个不同文件。两方共有文件 `Geometry/Intersection/SurfaceIntersectionPatch.cs` 的功能 ExactCorner 和 scoped bound cache 均保留；其余累计性能 Native 文件保持来源字节。

冲突实际只有 `scripts/RegularCurvedBooleanOracle.cs` 和 `scripts/RegularSolidBlendOracle.cs`。采用当前证据目录、完整中间产品、当前 helper 和三态质量合同，再接入 repeat/profile。原性能 runner 的旧点值检查和旧输出合同被直接更新；没有旧格式读取、adapter、双轨或 fallback。`217fc` 正式历史测量仍只作历史原件，不能作为统一源的倍率。

## 交界修复与语义

`bda3748` 修改两个内部消费者及两个选边顺序回归。相同原 PK seed 和 GM fresh input 在统一 merge 源真实重现 GM157/fault16059/Clip，而 PK Fix 成功；原退出 134。实际冲突是同一已由有限源根 witness 选定的 event，在目标直线上先登记了浮点反求代表值的单点，随后有界原生根区间不包含该单点。

现登记保留源根已有位置误差包围在目标直线原 gauge 的投影：对 `L(t)=O+D*t`，用向外区间计算 `(P−O)·D/(D·D)`，位置半径经 `|D|` 传播。没有假定存储方向的实数平方范数精确为 1。事件身份仍在此前由原有限源 witness/occurrence 选择，不以这个区间或 XYZ 距离合并。该登记不新增 `NativeLocatorEnds` 资格；不相交登记、不同有限根和排序未证仍拒绝。

后续参与者收紧同一事件时，当前 arrangement 的**新裁剪直线弧**代表端点随最终登记区间收紧；持久输入边及 native locator 的声明端点保持原合同。没有删短区间、改容差/预算、固定 tag、修改输入模型或重定义参数。修复后同 case 的 Fix 为 GM0/PK0、各两 Blend，fresh XT、full BODY_check、严格比较和有限段检查通过；物性仍有 Unresolved，原退出 2。

Boolean 默认安全关联、有限根/6D 唯一域、BCurve gauge、端点证书和失败回滚保留。Blend 的方向、a0 sheet ownership、FaceAsk 的 face×surface sense 和唯一 helper 的全段有限方向证明保留。G 的 eligibility、原未知量/支撑、frame/nearest/witness 门和 Conic 的原七变量、正 branch、Jacobian/RHS/预条件顺序未改。负缓存仍返回 NotConverged，不转成空交；缓存全 key、ResidualAccuracyScale、命令边界与分配失败重算保留。

## 执行环境与原始证据

仅在 qingyu2/Linux x64 执行。.NET10、真实 PK38.0.150，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`。统一私有准备实际返回 native ENV_READY。已读取用户级 marker 正文并实核 SHA256 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`，也读取本树 AGENTS、两份架构及固定差异/独审原文；不冒称运行时自动加载。

按 parasolid-research 实际调用查询设施，取得 FixBlends、FaceAskOrientedSurf、BlendSfAsk 原型与调用摘要；错误157核为 cant_do_intersect，16059 token 查询未找到，不把空结果当算法证据。真实 PK 动态执行见下表。没有公开厂商库、凭证或反编译正文。

原日志、同期 `.exit`、逐命令 argv/selector/source、输入/产品与 Capture 均私存于 `temp_docs/unified-delivery/` 及各 oracle 的 `temp_docs/` 产品目录。`validation-driver-v1.log` 的 driver 0 仅表示所有子命令已执行并记录，不替子命令的 2 签成功。原失败尝试保留：merge/mirror8 初始失败、一次 locator 候选编译失败、未解决冲突的诊断候选、registration-v1 的后续 Arrange 失败，均不覆盖；未生效的 locator 候选已撤回，最终 Native 仅有上述修复。

| 本轮实际执行 | 几何/拓扑及操作结果 | 原 exit |
| --- | --- | ---: |
| `focused-v1` | 12/12，含镜像八倍正反选边、近双 cylinder 分支及 ExactCorner 等负控 | 0 |
| `regular45-final` | 45 个最终场景，包含 NURBS、same-domain、continuous、reload 与 mixed-disjoint；几何/拓扑全过，40 次 mass 未决 | 2 |
| `mixed18-final` | 18 个最终场景，含 mirror、seam、repeat/reload；几何/拓扑全过，30 次 mass 未决 | 2 |
| `shortedge-lifecycle-final` | 独立 GM Fix 与 PK Fix 分列；imported-product 和 GM 自产产品两条 lifecycle 均 copy/delete 源、反射缩放、第一次 cut、editable XT reload、第二次 cut 成功；8 次 mass 未决 | 2 |
| `mirror8-final` | 与历史输入 hash 完全相同；本源实际 GM/PK Fix 各0，fresh/full/strict/finite/geometry 通过，2 次 mass 未决 | 2 |
| `rigid-small-final` | rigid+scale0.125 的 Fix、几何和当前有界质量通过 | 0 |
| `nurbs-lifecycle-final` | plane/NURBS 独立 fillet，copy/delete/反射缩放/reload/full/strict/cleanup 通过；物性未决保留 | 2 |
| `oblique-final` | 负 surface 查询、生命周期和同字节后续 Boolean 各通过，所有当前质量检查通过 | 0 |
| `rollback-final` | 前序 Blend 独立通过；后续 GM901/failed/零产品及字节/池回滚控制通过，同输入 PK Boolean 成功；不是成功 GM Boolean | 0 |
| `face-query-final` | 四种 face/surface sense 公共查询控制通过 | 0 |
| `direction-controls-final` | 当前 helper 的 10 正/10 负 XT 控制通过，包括 sense/fin、错误半径、有限 wire 等 | 0 |
| `controls-off-final` / `controls-on-final` | 原生错误尺寸 Different、错误半径/trim Rejected、材料负控及三次物性 Unresolved 保留；两次 CSV 60 行逐字节相同 | 各0 |
| `oblique-receipt-final` / `controls-receipt-final` | 同次质量收据与实际 CSV、mask、finite proof/source binding 一致；这是收据检查 | 各0 |
| `normal-il-final` | 普通托管 IL 3003 方法，CLEAN；未执行 AOT | 0 |

最终完整单测的原结果及当前入口复验追加在下节，不能将局部12/12或原历史套件拼成全绿。五圆柱直接 BodyContains5000、510 分类及未决 mass 保持原分层，本任务不扩张无关查询 API。

镜像八倍的关键原始身份：

| 字节 | SHA256 |
| --- | --- |
| PK seed | `262963ce0b899972272970ace0eff5da8b9161dbf007bd01f20a9d3037a8d1a2` |
| GM 变换后 fresh input | `10b9eaeaf01d1c0e5cc1b1655fa3884844d026b90e25713ae4031139183c0c54` |
| 当前成功 GM 产品 | `e17986c1c2e8f442eb7ad79d74a93e434e4155bd83b35db2d273d8202f0de65a` |
| 5539 矩阵实际普通 loaded Native DLL | `9bded01578fa4955993ca5cbd88ee9e11acf923c11c7485d86717db6abc13093` |
| 5539 对应 PDB | `8f56a3afd40a3cee9cca4cc9cfe134744ad4c9d3656afa15d6b51f2d843d560f` |
| 当前 4b2405e 实际普通 loaded Native DLL | `38aadd00300758d23b1aca6a88f7c4ff64d5bf8835ddd56055f0252975864a10` |
| 当前对应 PDB | `64a9f665a3539774fe89700bfdb689b43e3bb0851a073136ea32cd99b299589e` |

已有 SourceIdentity 观察器增加实际 loaded MVID 与私有 DLL/PDB 快照，没有增加 compare 或积分。复用既有 SourceBinding 审计器核得 Native PE/PDB 对应、333 份 tracked Native source checksum 一致；test Native 和 tests 同样核过，生成文件另列。Native test DLL 为 `6a302551a60d0fcf8d320e5a14a7841be9c09824c9d2377cfe6f6217922a893c`；它与普通构建不同，不能混为实际同一制品。

## 最终完整套件与当前 Capture

最终 Native 树的一次完整套件为 **1974/1974，0 failed、0 skipped，原 exit 0，8m44s**。实际使用上述 test Native 和 `KernelTests.dll`（SHA256 `70f4f39b410363017fe95a11cba1d660591af8688392b7cdaab6fffe367e25f8`）；执行前后这三份 DLL/PDB 清单相同。复用审计器核过 test Native 的333份源和 tests 的161份源，generated/other 分开。原 TRX SHA256 `90472933bdcb7245b8da6015cd0b6585ca81fcbf70234690c76fd49547d111ca`。

历史性能套件的两项失败在本次 TRX 均为 Passed：`BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative` 与 `NestedBlendCacheEmptyReadSetTests.SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled`。没有修改这两项测试或原历史记录，也没有单凭历史失败推断统一源结果。这是当前固定 Native 树的实际全套结果，不推广为全部建模/查询能力完成。

5539 后的 `4b2405e` 只改 Capture 来源元数据，Native 树不变。最终公共 runner 在该固定40位源上实际完成五项单操作：planar 原0/质量 Verified；sphere、curved、NURBS fillet、shortedge 各原2/质量 Unresolved。当前 `current-runner-v2` 不能签正式性能，四项未决没有被改成失败的几何，也没有变绿。文件名覆盖只发生在同一进程的重复输出内；本次 repeat=1，Mac 后续重复时原有 API/Capture 每步记录仍独立，XT 文件只属于最后一次。

最新元数据版本另实际复验 `shortedge-lifecycle-current`、`mirror8-current`（各2），以及 oblique、901回滚、Capture off/on 和两项收据核对（各0）。原63矩阵与完整套件仍只记其实际执行版本和相同 Native 树，没有冒充再次重跑。当前 oblique Capture 为2步、123 pass、零 fail/missing、10张真实 mesh；mirror8为1步、29 pass、23 missing、零 fail、4张 mesh；生命周期为5步、133 pass、77 missing、零 fail、28张 mesh。controls 的 raw0 同时保留9 fail和27 missing，代表成功执行负控，不是正例全通过。

当前 off/on 的60行实际质量 CSV 仍逐字相同，SHA256 `74f2bba7042662f1b773aa078b3e2796634089e742db74a23225a40ccb02a975`。同次 receipt 核对分别原0。runner 的只读 audit 对真实 planar Capture 为0；仅在另存的派生副本删去 modulus 或 BODY_check 后，各原2，均未取得 Verified；未改原 Capture 或再执行积分。

当前实际普通 Native MVID 为 `d8c6b453-63bd-4138-841e-136977b4aa6f`，DLL/PDB见上表；当前 shortedge oracle MVID/快照也单独保留，DLL SHA256 `58605fb572436b3afb1b00a723d3072bb8cf613e23aec05a27b305d35a9fc493`，PDB `95d5d1439bdddac15741fd04a28920a470891bcaa553441d2ef0a7eb97a32ba5`。扩充既有审计器的 scripts 路径识别后，PE/PDB及6份实际 oracle/helper source checksum 一致。不同构建生成元数据的制品身份分列，没有只凭源码树相同声称DLL相同。

公开交接候选为 `temp_docs/unified-delivery/handoff-current-v2.zip`，SHA256 **`5197d647fbc47a15f3338e83f1c4063227fddcd4dfff92186c0274aa510495bf`**，156个文件。包含7组真实 Capture/质量/mesh、原exit、命令与来源清单；56张 mesh 均逐份核其私有源 XT 存在、hash相同及真实live/snapshot body身份。XT正文和DLL/PDB不在包中；控制组没有mesh，不补造。结构收据为 `docs/reviews/advanced-modeling/pk-native-unified-delivery.receipt.json`，原 source manifest SHA256 `9487f841413932e35e7a08511177c6cfe8734b4488c4c5a48a3eea4b96d8697b`。原日志公开副本只按报告中的本树根及用户目录脱敏规则派生，原字节保持私存。

最终 `temp_docs/unified-delivery/final-source.sha256` 的817条和 `temp_docs/unified-delivery/final-artifacts.sha256` 的2415条均已逐项校验为0；后者包含678份本树自有 oracle 输入/产品文件，SHA256 `c9ffe114e99c20e4c49f82c38ab0bcb4c0dde86415db917c31b2938166e7f987`。公开包的 `temp_docs/unified-delivery/handoff-current-v2/files.sha256` 也逐项校验为0。这些0仅证明字节身份，不改变任一建模原exit或质量三态。

## 当前公共 runner 与交接

`scripts/NativeModelingWorkComparison.cs` 已直接采用当前 v2 合同。默认单源；显式 `--baseline` 才对两个固定 clean tree 做 ABBA，两个源必须使用相同当前 oracle/helper 字节。输出目录不可存在；每子进程记录实际 argv、40位 source/capture commit、同期原 exit、source manifest、当前 Capture、loaded identity、真实产品与工作量。`--repeat` 控制每进程 fresh-input 次数，rep0 冷、后续暖；原 API timer 未改。旧 runner 参数/旧数据不兼容，未增加历史读取。

在根目录先按授权的统一私有准备流程取得 ENV_READY，然后使用新输出名：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/NativeModelingWorkComparison.cs -c Release -p:UseSharedCompilation=false -- --output ../temp_docs/new-unified-run --repeat 3
```

Mac owner 可在自己的后续同机窗口使用这个入口，不需要本任务访问 Mac。Linux 这里只记录工作量和质量；runner 始终保留 `performanceQualified=false`，没有从旧分离窗口继承倍率。Jacobian requested directions、实际 residual directions、record build/rebind、direct columns、bounds/cache 和 root cells 分别记录，不能互作等价 CPU 单位。

`--audit` 只核已有当前 Capture，保留缺失/失败与 agreementStatus；15 分量必须有 modulus/上下界且 Verified 才能取得该数值层资格。Capture 继续仅观察原 AssertEquivalent/Compare，同次 raw mask、diff、finite proof 与 checks.evidence 保留。shortedge 的当前来源明确为 PK seed→GM receive→GM Fix，带实际 seed/fresh-input hash；没有伪造独立 GM 输入。curved 未取得的 tracking/role 仍 missing。

看板最小交接包只含当前真实 Capture、mesh JSON、quality JSON、hash/command/source/原 exit 收据；XT、DLL/PDB 正文私存。mesh 的 sourceBodyTag、DisplayBody snapshot、XT hash 与真实产品绑定，未用 PK 重建物替换 GM 产品。发布 owner 应保留已有 run，新候选集中独审后再装配；Unresolved、原 API 错误、observer 错误和缺失资格独立展示，不把检查器0改写为建模成功。

本轮不签任意 Boolean/局部建模、全部 M0–M5、所有查询或 PK 性能目标完成；没有 main、门户、Windows 或 Mac 构建/测量。只清理自己确证的 PID/PGID，不使用全局 pkill。
