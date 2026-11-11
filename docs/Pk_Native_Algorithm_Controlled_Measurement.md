# 6616 固定优化包正式 Mac 测量与制品交付

固定 `270e2b6` 到已独立接受的 `6616cd6` Native 优化在同一 Mac 完成一轮五操作 ABBA：20 个原进程、60 对 GM/真实 PK 操作均通过固定质量资格，所有子进程和总 harness 原始退出码均为 0。shortedge 暖样本中位数减少 64.44%，观测加速 2.812 倍；完整 PK 性能目标仍未完成。此页不签 main 已集成或新快照已上线。

## 固定源码和执行范围

基线为 `270e2b6814afe5a48cd0532e89531f6fb80011be`，Native 子树 `afb8cec5cde9769cb68c470ea8da9591a1804e64`；候选为 `6616cd60a55e50a5d8a5ada7274895c44609676b`，Native 子树 `5b4c6104c600b0194bb543e2dc11efa89e2f0f1f`。协调者已按独审 `5a4ad5319b77c0944baf598e07dbbce79782a483` 接受其限定源码、数学和质量范围。

候选测量包装分支为 `perf/pk-native-algorithm-measurement-6616@a73bc08dbd39e984dff59dac7bba31a3baf80b4e`：仅把五个 oracle 固定为基线 270 的原字节，不改变 Native。五个主 oracle 及 ParasolidScriptHost、ParasolidBodyCompare、WebProgressCapture、比较 runner 共九个文件的 hash 两边一致。没有新增历史格式适配或降低检查；实验 `241b8f3`、未接受功能 `4794/60be` 均不在计时源中。

主实现、测量包装与独立测量目录分开保留。原 report 的 `baselineNativeTree/candidateNativeTree` 字段记录的是整个 `src` 子树；本页及 receipt 的 `*NativeSubtree` 明确指 `src/ProjectGmKernel.Native`，不混淆二者。

## 本轮结果

每项操作每个源码有 2 个冷样本（各进程 rep0）和 4 个暖样本（rep1/2）。下表使用全部四个暖样本；GM/PK 为每对比值的中位数，不是两个中位数相除。正变化表示候选更慢。

| 独立完整操作 | 基线 GM ms | 候选 GM ms | GM 变化 | 基线/候选 GM | 候选配对 GM/PK |
|---|---:|---:|---:|---:|---:|
| sphere subtract | 0.0766 | 0.0781 | +2.02% | 0.9802× | 0.2758× |
| planar subtract | 0.6351 | 0.6747 | +6.23% | 0.9414× | 0.7274× |
| curved block subtract | 938.6677 | 883.6980 | −5.86% | 1.0622× | 444.0818× |
| plane/NURBS fillet | 1569.9142 | 1575.0089 | +0.32% | 0.9968× | 976.4974× |
| shortedge FixBlends | 15858.4145 | 5639.5909 | −64.44% | 2.8120× | 3423.4971× |

球面/平面的快速样本及 NURBS 未形成加速结论，回退数值原样保留。curved 的观测改善为有限单窗口结果；主要可复核收益是 shortedge 的重复求界减少。所有冷样本及最小/最大、PK 中位数、配对比值与中位数之比分别保留于 `docs/native-modeling-algorithm/operation-summary.json` 和原 report，未混合冷热或跨主机倍率，也未以新结果改写旧 676 历史。

shortedge 暖 rep1 的原工作量：实际求界 223,331→67,137，Jacobian 1,597,561→525,315，全方向 residual 27,774→7,023。根单元 220,895、根数 3,518、SSI 单元 4,956、winding 196、预算失败 0 均相同。固定公共 oracle 没有打印新 negative-hit/XYZ-column 字段，不能给此轮伪造这些新计数；相应实现与开发证据已在独审范围中核实。

## 窗口与限制

窗口为 UTC `2026-11-11T17:42:47.3159860Z` 至 `2026-11-11T17:50:06.6601560Z`（本地次日 01:42–01:50，UTC+8）。主机 Apple M4、macOS 27.2.0/Arm64，SDK 10.0.400、运行时 .NET 10.0.11、真实 PK 38.0.150，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`。

开发测试、完整 testhost、其他自有 PK oracle 均自然结束后计时；串行预构建，计时进程仍使用同一个 dotnet runner、相同选项与默认 tiering，编译在 API 调用前结束。资格检查自行生成的 Roslyn daemon 根据启动时间和自有命令确认后仅结束该 PID；未清理用户进程。窗口前后两份 Native DLL hash 一致：基线 `f569c162f440610d7a623b74600627d4c05c49f6a3f7961be31b4906909d33a2`、候选 `285166f808d426d0f46f5e4b720c1a909569d9c59cf738ac4499dbecee7451de`。

系统一分钟负载观测 3.38–15.37，没有声称消除全部系统/用户负载、取得排他 CPU 或热稳定资格，也没有等待无限安静窗口或择优重复 ABBA。首次候选预构建工具 60 秒超时，以及首次 CLI 参数格式错误均发生在操作计时前，原失败日志保留；计时 ABBA 只执行上述一轮。

## 质量、真实制品与证据

每个原进程执行三次独立新输入操作，固定资格检查绑定实际 API/step/error/status/body/count/mass 及 fresh XT 的真实 PK 接收、BODY_compare/BODY_check、有限曲线等原 oracle 检查。NURBS 倒圆与 shortedge 单独验收，本轮未新执行下游 Boolean 组合，不将其写为通过或失败。生命周期和完整套件复用已核证据：候选 1,912/1,914、两项整合基线上已有失败、原退出码 1，不能因本轮 60 对通过而改为套件全绿。

原始目录为 `temp_docs/pk-native-algorithm-work/measurement-6616/temp_docs/native-270-6616-controlled/`；原 report 与 20 份 log/capture、全部冷暖数据及输入、源码、host、runtime、PK 身份保留原字节。额外派生物为：

- `docs/native-modeling-algorithm/operation-summary.json`：全部操作统计、60 次实际工作量和原 report hash。
- `docs/native-modeling-algorithm/measurement-receipt.json`：固定身份、窗口、原退出码、加载范围、过程边界与 DLL hash。
- `docs/native-modeling-algorithm/material-binding.json`：312 个实际 mesh、34,964,660 字节，逐文件绑定原进程/step/GM或PK/hash；44 个原 XT 文件绑定各自最终 rep2/hash。

固定 oracle 的 XT 文件名在进程内逐次覆盖，归档原 XT 只属于最终 rep2；每次 rep0/1/2 的实际 mesh、计时和检查分别保留。312 个 mesh 不能替代任意曲线等价证明，rep2 的 XT 不能补签为全部 60 对各有独立 XT 字节。所有原制品均由原 GM/PK 实际输出产生，没有重建模型或用参考系统结果冒充 GM。

此交付等待协调端集中核测量与看板更新。旧 676 快照原样保留；新代数候选封存于独立实验分支，未加入本轮声明，性能总目标继续推进。
