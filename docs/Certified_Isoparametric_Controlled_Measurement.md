# 已证明cell一维截面候选唯一五操作窗口

计算 `e7f0a4876ef296b56f5f7c57a45f15617eac6918` / Native `6a84372805cefdaf5c10cb3e0f3e56f8eb12ffdc` / src `cee0c419bd3e42acdc2d95e75a350f03cd8d06fa`，完整质量及实际测量metadata源 `ad79d2d4c6b59a81be423de4faf1745ea62cb318`；基线固定接受fef52f3/计算2c0e4ea/Native8fc/review24c54ed。未纳入新查询/新norm、待审移动源或旧拒绝实验，未改旧窗口。设计与完整质量见 `docs/Certified_Isoparametric_Bound_Work.md`、`docs/Certified_Isoparametric_Quality.md`。

UTC **2026-11-12T08:03:04.2462780Z–08:05:02.4967460Z**；Apple M4/macOS27.2 arm64，SDK10.0.400/runtime10.0.11，真实PK38.0.150。十一current helpers/runner逐字同，十个managed-only预构建原0后，唯一串行五操作ABBA。每进程3 fresh GM/PK对，rep0独列冷、rep1/2暖；20进程/60对/120API全部0，16进程raw2/U、4planar raw0/Verified、runner2/performanceQualified=false原样。

| 操作 | 基线暖GM中位ms | 候选暖GM中位ms | 观察变化 | 候选暖min–max ms | 候选paired GM/PK中位 |
|---|---:|---:|---:|---:|---:|
|sphere|.08760|.07970|−9.018%|.0765–.0822|.23972×|
|planar|.77580|.78520|**+1.212%**|.5548–.9950|.74596×|
|curved|44.91595|44.01035|−2.016%|39.7907–45.5108|21.76082×|
|NURBS|504.95495|501.66250|−.652%|470.1583–537.8105|283.79150×|
|shortedge|517.77570|448.23975|**−13.430%**|397.0935–720.1391|197.97848×|

四暖小样本是固定源/输入/机器/制品的描述性观测，不签统计/普遍/因果/单机制或排他CPU。完整保留planar回退、shortedge宽散布与所有冷暖/PK点，不追有利复测或把其他窗较慢基线搬来作本次对照。paired中位与ratio-of-medians分列在机器summary，不乘开发−13.783%或历史倍率，整体PK目标仍未完成。

`docs/isoparam-controlled/operation-summary.json` 逐20进程/60rep保留原PROFILE/WORK/ROOT_WORK行、行号、字段和log/Capture SHA与GM step/API时间，冷与暖各实际，不以rep0填暖。程序未新增普通制品的逐分支执行计数，测试hook命中不冒称全部真实API命中率；已有聚合计数也不能独自证明某次耗时属于新路径。源码同源完整cell proof/覆盖域/explicit analytic admission和所有原Root/有限域/图/CHART/nearest验收门分别审，采样/累计inclusive墙钟或少一个导数遍历不被当成可删除CPU或13.430%贡献。

20实际loaded Native/Oracle与封存DLL/PDB、PE/PDB链接及source.sha256文档checksum逐核；baseline Native335、candidate336 tracked，generated/unmapped单列，见 `docs/isoparam-controlled/source-binary-binding.json`。普通质量、testhooks与测量制品不同，无补测试loader或start/end。此次baseline MVID `a01cccf4-89e7-495f-b217-fbda6c2e9761` / DLL SHA `f467bc2b50265aaec66fbaeea30af3e892e0206265cee7b2907b052c114f492c`；candidate `62018b4e-94ed-4772-ab31-e4c6688e7c5f` / `f840039a1b300dd17ecc1d05469263b4166814638e4b626fd3833ec7f53667db`。

312实际mesh/35,389,380B与64最终XT逐hash/source/step/tag绑定，不是64个rep2产品；同名最终成品按原行为留rep2，公共输入/seed不补rep标签。质量shortedge六pair最终rep5另属开发原执行，不改标本窗rep2。真实GM输出XT经私有PK snapshot显示，GM live/snapshot与独立PK reference分开，不拿PK模型冒GM产物或用显示签质量；shortedge PKseed→GMreceive→GMFix、curved原roles/tracking/source缺失均保留。每步原current strict/finite/真实PK receive/compare和旧1e-7物性三态不降，checker0不洗U。

原host20观察load4.52–6.35，pmset状态与powermetrics原失败保持，无数值温度/排他CPU/恒温资格。无Windows/NativeAOT、额外suite/家族/ABBA、旧格式兼容或用户进程清理。Mac完整2029/2029和185定向只属于当前候选证据，不回填旧来源。

机器收据 `docs/isoparam-controlled/measurement-receipt.json`，私有原根 `temp_docs/isoparam-controlled/`，原report SHA `6cedd40434fa16b26d4badd90a7e984ae6d2a1bcc8fc0d58c831e54304bbe7ea`，material SHA `f31d2f751cdff6c82b8454076f0c61794a7b3757198c4c5bea781c194b3d7f61`。完整算法/关键集成/原质量/本窗口待同次集中独验，未签独立接受/main合入/上线/整体PK完成。Section已接受源及两原包、cc已发布原窗不可变；不给门户流程新增作者等待项，不worker_done。
