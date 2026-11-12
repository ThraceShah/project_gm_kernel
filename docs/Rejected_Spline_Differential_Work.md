# 未保留共同UV微分/总阶二Jet实验

计算 `4e5b584adb10b5305cd48b0d6b28e9d0286c01c0`、Native `58b10fe0d49bfad8c618c47f16e972ac0a3ce724`。93项定向验证原0，保留原25矩阵逐位和Bezier18→27映射对照、degenerate/nearest/witness/extension/零分配。完整开发对照没有证明值得保留的收益，故本实现不进入正式候选，不再扩展或为弱信号重测。

一次串行baseline→candidate开发对照，每源一个fresh进程，各六fresh GM/PK完整API对；基线固定17eedbf/Native116，两源所需七份当前helper逐字核同，先managed预构建后执行。共12pair、24API均0，两进程/两runner各raw2、Unresolved保留，不是正式ABBA或独立性能接受。

| 源 | 冷GM ms | 全部五暖GM ms | 暖GM中位ms |
|---|---:|---|---:|
|17eedbf baseline|1281.3659|615.1463 / 543.6910 / 523.2947 / 489.2063 / 497.2329|523.2947|
|4e5b584 candidate|1125.3024|542.8723 / 525.2777 / 479.6355 / 504.3664 / 465.3268|504.3664|

观察变化−3.617%，波动明显，没有统计/因果/普遍收益或独占机器资格。冷与全部暖、PK时间、原quality/raw结果和文件SHA在 `temp_docs/spline-differential/development-comparison.json`；两份原runner目录为 `temp_docs/spline-differential/dev-baseline/`、`temp_docs/spline-differential/dev-candidate/`，实际loaded DLL/PDB、source清单、原log、Capture/XT均保留。

没有新增完整套件、五操作ABBA或字段微审。此源/方法及质量结果保留，不合main，不覆盖已接受185或cut950历史；后续从17eedbf独立判断更主要的证明计算重复，而非继续用本实验的残差方向次数减少预签收益。
