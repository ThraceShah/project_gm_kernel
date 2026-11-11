# 统一候选1654与84c的唯一同机五操作窗口

固定Native224候选完成一个完整受控窗口，当前观察为curved −35.77%、NURBS −12.95%、shortedge −4.14%、planar −0.55%，sphere +11.18%（+0.0083ms）。**代码与本窗仍待集中独立接受**，物性/缺失状态不转绿；没有正式继承旧a289的−11.97%或217fc倍率，没有为择优重跑窗口。

计算改动 `1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7`，实际测量metadata/source head `d014f76e41746a08b32e963d24b6a8b02fcaac67`，Native `224564d1cdc0d0b6cf78c275282454751af2fd60`，src `c6e8bc553cdfecaebc3c1fac5d30688ba8628e6c`。固定基线 `84c558c10135c117bf8be6629a157e6d230f1b2d`，Native `133f56acb0cf485366cd4f13f457dc3b161655eb`。两边十一份当前runner/helper文件字节相同，未使用旧point-only脚本。完整算法/当前实体/生命周期/2000全套及Mac基线对照见 `docs/Unified_Native_Performance_Package.md`。

## 协议与原结果

Mac M4/arm64、macOS27.2、.NET10.0.11/SDK10.0.400、真实PK38.0.150。UTC `2026-11-11T23:40:41.3046610Z`–`23:42:45.0173950Z`。先串行十个managed-only预构建且全原0，与本窗建模计时分开；未运行NativeAOT。随后每操作两来源按baseline/candidate/candidate/baseline顺序，各进程三fresh-input GM/PK配对，rep0冷、rep1/2暖；共20进程、60对、120个API时间，全部API0。十个预构建和实际runner的命令、stdout/stderr、源hash、原exit均留存。

当前runner原退出2。planar四进程原0/Verified，其余sphere/curved/NURBS/shortedge共十六进程原2/Unresolved；没有硬失败，也没有将checker/分析器原0当成十六个建模原0。实际Capture的几何/拓扑、modulus物性、Missing来源/角色及原退出按原JSON保存。`performanceQualified=false`保持，独立测量接受尚未完成。

## 全部四暖样本的描述性结果

各source/op的全部四暖GM样本中位；表中变化是本窗中位之比。两个冷样本/source/op另列于机器文件。保留逐进程三对、所有冷暖/min/max和GM/PK逐配对比，不对单个机制归因。

|操作|84c GM中位ms|候选GM中位ms|变化|候选暖min–max ms|候选逐配对GM/PK比中位|
|---|---:|---:|---:|---:|---:|
|sphere|0.07425|0.08255|+11.178%|0.0804–0.1102|0.30856×|
|planar|0.57695|0.57380|−0.546%|0.5435–0.6036|0.71477×|
|curved|132.18265|84.90585|−35.766%|74.8134–95.2172|35.14762×|
|NURBS|691.55280|601.99400|−12.950%|558.0697–631.4847|368.97018×|
|shortedge|670.00785|642.26420|−4.141%|524.1270–795.0665|387.35924×|

小样本且有波动，尤其shortedge区间较宽；以上是观察，不是统计置信或数值全认证。sphere回退保留，无选择性丢样。历史不同窗、不同比较输入或其他平台时间不能相乘或替代本窗。

20次实际loaded身份分别对应两Native：基线MVID `0c4259df-01d7-4515-b42d-e450f0010e8f`、DLL SHA `3118bcfc9b900e5c8a0d5fadef749c7690cea34c889838aee74c1ea800cda42e`；候选MVID `9cdf9668-5571-464f-a941-76e3202db08f`、SHA `7176e85cc67b5afc38fd5cefb079f26b4a1dab289fc1b624a4efb2cf365659a8`。每份归档DLL/PDB/Oracle hash与producer实际loaded观察一致，Native PE/portable PDB链接及333/334份tracked文档checksum、原source.sha256均核对。generated/unmapped文档另列；没有附加start/end loader见证，不能扩张为另一证据层级。d014测量制品与1654质量制品分开，Native源树相同不代表二进制SHA相同。

每进程留UTC、实际身份、uptime/pmset热警告状态；一分钟load为3.82–4.35。数值温度没有普通权限读取，powermetrics原失败留存；pmset是警告状态而非温度。没有独占CPU、温控或其他应用静止的资格声明，不等待无限quiet、不删其他进程。

## 来源及保留物

全部实际312个mesh/35,389,380B逐hash绑定。GM显示mesh来源是本次实际GM产品XT的私有PK snapshot，不能反推任意曲面全部正确或继承性能认证。shortedge是真PK seed→GM receive→GM Fix，不是GM独立构造；其它同参数输入独立构造。roles/tracking缺失按当前Capture原状态保留。

本次64个XT是runner复制的最终文件集合：会覆盖的产品名只保留最后rep2，公共seed/input文件不虚构重复序号或新角色；每rep独立保留实际mesh和检查。第一次派生material-binding.json机械地给所有XT（含输入）标rep2，其字节保留，已由material-binding-current.json纠正，仅派生标签变化，未更动原测量、XT、显示或rawexit。

所有原件在 `temp_docs/unified-native-controlled/`，包括window各进程、十个预构建、主机/每进程状态、全部原exit及原DLL/PDB/Oracle、XT/Capture、三个派生器/初始标签及纠正；没有运行新模型或重建缺失产物。对外机器文件是 `docs/unified-native-controlled/operation-summary.json`、`source-binary-binding.json`、`measurement-receipt.json`，详细mesh/hash清单为原包内material-binding-current.json。分析器成功只签输入和归档字节一致，不替代代码、几何/数值或性能接受。

统一223759看板发布由协调端已经完成，本包没有重复发布、main集成、Windows/AOT验收或PK性能追平声明。
