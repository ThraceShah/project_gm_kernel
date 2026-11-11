# 6616 → 已审 G+Conic 的同机受控完整操作测量

已接受的 G 与 Conic 优化在同一 Mac 完成一轮五操作 ABBA：20 个原进程、60 对 GM/真实 PK 操作全部通过固定原质量资格，原 harness 和20份子进程 exit 均为0。暖样本中位数观察到 curved 减少84.38%、NURBS独立fillet减少56.56%、shortedge减少87.03%；planar增加65.66%，原样保留。本页只签本固定窗口结果，不将不同历史窗口相乘，不把 G 与 Conic 的单独贡献拆成未经测量的倍率；完整 PK 性能目标仍未完成。

## 固定身份与公共 runner

| 身份 | baseline | candidate |
|---|---|---|
| 计算源码 | `6616cd60a55e50a5d8a5ada7274895c44609676b` | `74c29e0fc14cc40bc134a012b0eca3fecedc2947`，含已审G |
| Native 项目子树 | `5b4c6104c600b0194bb543e2dc11efa89e2f0f1f` | `58c1667b0155ec46b53be42b9750c8dbd78a75dd` |
| 测量包装源码 | `5392a06ea07267f093461876d84fc575849f5ea5` | `34ffe46b7010b44c221fae24b2371597e46ede36` |
| 整个 src 子树 | `41c8b597c54753ab394bc99a8b2e59bb3d83a756` | `397526ef55c3b0376f3e2fc353375ba7ca18395b` |

G 的限定接受为 `3fdc109babe6eda489d985171e3808f956481be1`，Conic 为 `35e61e5e02e319d8f6b9eaba38f880e62f1bbf0c`，协调者已接受相同固定 Native 子树。包装只统一 scripts，没有改变任何 Native 文件：五主 oracle 从原固定 a73/270 runner 恢复相同字节，沿用原检查与 API 边界；共同加入不运行建模的 identity-only 预构建入口、观察器加载身份及每进程原退出文件。五oracle、Host、BodyCompare、Capture、比较runner共九文件在两边 hash 逐字相同，原 hash 清单和两侧实际 Git diff 单独保留。

新观察器仅在原 capture 启用时，于启动和进程退出时记录实际加载的 GM Assembly MVID、DLL/PDB SHA256、CodeView和portable-PDB身份，启动时封存该DLL与PDB。十个 identity-only 预构建均通过，比较资格正例及六个缺失/失败负例均通过。20个正式进程的 start/end 匹配封存 PE MVID/DLL hash、PE CodeView与portable PDB id，并分别核过328/330份受Git跟踪的Native源文件校验和；生成或未映射文档另列，不冒称独立可重现编译或将它们归为已核源文件。窗口前后两边项目输出DLL/PDB也保持原hash。

## 全部暖样本结果

每项操作每个source为2个冷样本（各进程rep0）和4个暖样本（rep1/2）。headline取全部四个暖样本；每对GM/PK比值的中位数与两个时间中位数之比分别保存。正变化表示候选更慢。

| 独立完整操作 | baseline GM ms | candidate GM ms | GM变化 | baseline/candidate | candidate配对GM/PK中位数 |
|---|---:|---:|---:|---:|---:|
| sphere subtract | 0.0871 | 0.0772 | −11.26% | 1.1269× | 0.2805× |
| planar subtract | 0.5850 | 0.9691 | +65.66% | 0.6037× | 0.8265× |
| curved block subtract | 879.3874 | 137.3725 | −84.38% | 6.4015× | 75.6049× |
| plane/NURBS fillet | 1530.2238 | 664.7763 | −56.56% | 2.3019× | 438.1989× |
| shortedge FixBlends | 5610.2323 | 727.8500 | −87.03% | 7.7080× | 340.1703× |

球面/平面为亚毫秒样本，实际波动与回退没有丢弃；不把微小差异或planar回退归因于某段代码。全部冷值、GM与PK的min/max、中位数、每对ratio、ratio of medians及120个原时间都在新summary与原report中保留，未择优排除或拼接窗口。

shortedge原暖rep1的工作量为：root cells220895→22371、Jacobian525315→16870、bound evaluations67137→47246；roots3518、SSI4956、winding196、budget failures0保持。完整方向/record/简化残差的算术不同，不能把这些计数比例直接当排他CPU节省；每个rep的实际计数原样保存，不用rep1替代其余样本。

## 窗口与质量边界

UTC窗口为 `2026-11-11T20:14:18.5205340Z` 至 `20:18:00.1129440Z`（UTC+8本地11月12日04:14–04:18）。Apple M4、macOS27.2/Arm64、.NET10.0.11、SDK10.0.400，真实PK38.0.150，PKToy仍固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`。managed JIT/default tiering，两边同runner、input/argv、检查与每项GM/PK固定顺序，一次只运行一个oracle。原一分钟系统负载4.00–5.43。

开发、完整testhost、PK oracles与预构建分时，自有相关进程自然结束后开始正式窗口；未杀用户进程、追求无限安静或宣称排他CPU/热稳定。唯一一轮20进程ABBA结束后才运行派生统计/身份校验。此前只读thread-wall trace不在本窗口内，不用GC/ThreadPoll或inclusive占比解释净收益。私有准备时一次工具路径拼写错误127发生在任何计时/建模之前，修正后两树均ENV_READY；它不是操作失败或第二次ABBA。

60对均保留fresh GM XT的真实PK receive、原BODY_check/strict compare、数量/region/shell/type/sense/tolerance/missing/matching、原有限几何及15项点值mass门，只有原n_cht_pts诊断可降级。qualified不扩大为GM modulus三态全部Verified。shortedge是PK seed→GM receive→GM FixBlends；curved tracking/roles missing仍是实际范围。五项按独立操作结算，此窗口未执行下游组合。

既有质量继续分列：candidate fullsuite1944/1942/2/0、原raw1及两项known失败保留；G/Conic各自已验lifecycle/独立后续Boolean步骤复用原实际范围。mirror+scale8 case在94f/b47各raw134、PK0、相同157/fault16059/NetworkClip，不能因本窗口标准操作通过而写成全家族通过，也不外推1887/main。新窗口没有重跑fullsuite、Windows/AOT或整合功能1887。

## 原制品与派生物

原20进程目录为 `temp_docs/pk-native-algorithm-work/measurement-6616-gconic-candidate/temp_docs/native-6616-gconic-controlled/`；每个目录保存原log、process.exit、capture、实际各rep mesh/check/timing、实际加载DLL/PDB及start/end身份。绑定的312个实际mesh共34,973,060字节，44个原XT只属于各进程最终rep2；固定oracle逐rep覆盖同名XT，不能补签rep0/1各有独立归档XT，也不是每项都同时保存GM/PK两份产品。发布若选择XT必须绑定对应进程rep2；其余rep可使用自己的原mesh。

本轮新派生物均位于 `docs/native-modeling-gconic/`：`operation-summary.json`、`material-binding.json`、`loaded-identity.json`、`measurement-receipt.json`。它们绑定原report、全部进程/制品/加载身份及源码，未改03e/97a、旧6616或1887历史。原始退出、模型检查、派生检查器成功与独审/集成/上线状态分开记录。

本交付等待协调端集中核算及新快照；未声明main已合并或新结果已上线。下一热点仍按完整性能目标推进，不由这一次局部结果签PK目标完成。
