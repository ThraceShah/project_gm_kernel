# Projection cc47871唯一同机五操作窗口

固定候选计算/实际测量源 `cc47871133fb7d536ffa7fd050066c51f4905708`，Native `fe3fa0378ef6d9c0dd971de4c993a289cdb8a34b`，src `31be8f6c46766bb3b60b0e402bfc043426ed58fb`；基线固定 `17eedbfd6be891db8c43494cd91a896d261242c4` / 已接受Joint185 Native116。十一份当前runner/helper逐字相同，先十个managed-only预构建原0，随后一次串行ABBA；不改输入、容差、预算、质量门或模型来源。完整质量2022/2022及八组原执行见 `docs/Projection_Bound_Quality.md`。

UTC2026-11-12T04:56:39.7493270Z–04:58:37.0945730Z；Mac M4/macOS27.2/arm64、SDK10.0.400/runtime10.0.11、真实PK38.0.150。五操作各baseline/candidate/candidate/baseline，每进程三fresh GM/PK对，rep0冷、rep1/2暖。20进程、60对、120次API全部原0。planar四进程raw0/Verified，其他十六进程raw2/Unresolved，runner2、performanceQualified=false保持，无硬失败；独立几何、数值、来源及性能接受分层。

| 操作 | 基线暖GM中位ms | 候选暖GM中位ms | 观察变化 | 候选暖min–max ms | 候选paired GM/PK中位 |
|---|---:|---:|---:|---:|---:|
|sphere|.08265|.07800|−5.626%|.0757–.0878|.26043×|
|planar|.55835|.99445|+78.105%|.9455–1.0684|.87549×|
|curved|40.50215|38.84930|−4.081%|38.7392–40.0157|18.22905×|
|NURBS|597.19760|500.76395|−16.148%|446.9253–621.0768|253.84728×|
|shortedge|646.96075|548.47140|−15.223%|525.3929–733.4724|331.97129×|

完整保留planar回退及NURBS/shortedge波动；没有以绝对值小抹掉回退，没有为不利结果重跑。每source/op四暖、两冷全部原值、逐配对比和ratio-of-medians分别在机器summary；小样本只作固定输入/源/机器的描述，不签普遍、因果或单机制倍率，不乘此前窗口或开发对照倍率。整体PK目标仍远未完成。

逐进程/rep的原PROFILE/WORK/ROOT_WORK、原行/行号、对应Capture GM step/API时间及日志SHA在 `docs/projection-controlled/operation-summary.json`。不以rep0或此前质量单fresh工作量覆盖暖rep，不把inclusive/采样墙钟或减少的函数调用当排他CPU、可删除耗时。开发B/A与此次固定窗口为不同执行/口径，分别保留。

20份原source.sha256与实际loaded Native/Oracle DLL/PDB逐hash核同，Native两边各335 tracked文档checksum和PE/PDB链接核实，generated/unmapped另列。源绑定在 `docs/projection-controlled/source-binary-binding.json`。基线MVID `fc27d3ef-eb64-4d02-a5f6-4fcb3a96a1fd`、DLL SHA `d45d733bf505b55c8af9b25c8e06fa8bdd8510822935ed61f9928529bc1d63c8`；候选MVID `c1240871-d64d-4518-8a49-f0d533b267a6`、SHA `d82196e1741424219b69b40688a0b599bc48fce11e69db6a39550d75f05eb03a`。候选普通质量和此窗口Native恰为相同字节，testhooks另一个制品；仍逐实际执行记账，不增加start/end loader或向历史补身份。

312个实际mesh/35,389,380B、64最终XT文件逐hash绑定，原集合是最终文件而非64个rep2产品；同名产品覆盖仅留rep2，公共input/seed不附虚构rep编号/新角色。GM mesh来自本次真实GM产物的私有PK snapshot，与GM live tag/PK独立产品分列。shortedge保留PKseed→GMreceive→GMFix来源，curved roles缺失不补造，所有rep原比较和15物性检查保留，不从显示或checker0补签质量。

20份实际进程观察load3.72–4.30，pmset及powermetrics原失败保留，无数值温度或独占CPU/恒温资格；无Windows/NativeAOT、额外全suite/家族/ABBA、用户进程清理或无限quiet等待。原根 `temp_docs/projection-controlled/`，原report SHA `6568f1d9d19bbf0df0f9eeb07dc2afac1f5990599535b2f5caa0dbaf40d9e90e`，material-binding SHA `3678da6d62f44e00e8e6419af0709b25d73c43f3cfdd50a0a8c7c75e916d0306`。机器收据 `docs/projection-controlled/measurement-receipt.json`。

本完整包待同一次集中算法/集成/测量独审，无main合入或上线；并行q2查询/510/物性源未接入。已接受185及两原包与已拒实验原件保持不变，不发送worker_done宣称整体PK目标完成。
