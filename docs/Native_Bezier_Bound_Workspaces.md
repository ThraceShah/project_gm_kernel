# Bezier scratch 与后续求界优化

`16aca1d` 只将 Bezier 求界中五处临时空间按活跃次数分配，保留原最大次数16、默认初始化、原区间运算/循环顺序、proof和失败条件。JetHull 复制活跃齐次控制网前缀。没有跳过初始化、改模型、扩大缓存或证明预算。

## 开发期完整操作对照

固定已审 G+Conic 的原始托管DLL（b47 family，Native subtree `58c1667b0155ec46b53be42b9750c8dbd78a75dd`，SHA `fd92cbe6feea3ec566c9c3197e7b4298701179fcf64de1709a584d6074cf64a7`）与 `16aca1d` DLL（SHA `9c6bf48cb80d67d4583d327f0bf5d50483a525f9ff5a3076d902954af00b6a83`），共用同一已编译ShortEdge runner和完整托管依赖。两边只替换正常托管引用的GM DLL，没有重定向PK绑定。每进程开始/结束打印实际加载DLL SHA/MVID，binding清单记录共同runner及依赖原字节。

Mac串行顺序 baseline/candidate/candidate/baseline；每进程六个fresh PKseed/GM receive pair，rep0单列为冷样本，其余五个全部保留。输入为angle1/radius0.5/tolerance1e-7，完整FixBlends边界和原几何/拓扑/真实PKfresh检查保持。没有重新跑58项、fullsuite、五操作正式窗口或AOT。

| 进程 | 来源 | 冷样本GM ms | 五个暖样本GM ms | 暖中位数ms |
|---|---|---:|---|---:|
|0|baseline|1582.8658|757.2050,605.7630,580.2368,573.0288,554.4850|580.2368|
|1|candidate|1525.6577|727.2677,546.8391,516.2555,570.9146,547.4152|547.4152|
|2|candidate|1628.4407|694.4362,520.4817,517.2697,549.2877,497.4875|520.4817|
|3|baseline|1405.5236|656.1076,600.2573,628.8417,623.2067,594.4724|623.2067|

各来源十个暖样本中位数603.01015→547.12715ms（观察−9.26734%）；两轮各自候选中位数也较低。24对完整操作原进程raw0、captureErrors空、所有capture checks pass。cells22371/roots3518/SSI4956/winding196/budget0/Jac16870/bounds47246不变，各进程最终XT均SHA `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`。

这是足以支持暂保候选的开发期信号，非正式加速倍率、独立接受或PK对齐；只有一个输入/操作，未量化稳定CPU/温控资格，重复中有明显JIT/系统波动。后续与实际主导求界开销形成有效整包后集中验收。不得与6616→G+Conic正式窗口混算或乘旧倍率。

首轮最小依赖复制漏了Viewer.IContract，四个原进程虽然raw0，但captureErrors/快照检查失败，未用于上表。该轮与分析器raw134原样保留；补齐同runner托管依赖后使用全新`bezier-dev-ready-*`目录运行上表一次，没有择优重跑。它是开发环境未就绪，不能签成建模成功或失败。

证据在 `temp_docs/pk-native-algorithm-work/`：`bezier-dev-ready-{0-baseline,1-candidate,2-candidate,3-baseline}.{stdout,stderr}.log/.exit`、同名capture目录、app-binding、summary、`RunBezierDevReady.cs`和`AnalyzeBezierDevReady.cs`。最终XT在 `temp_docs/short-edge-blend-oracle/bezier-dev-ready-*/`。共同runner、基线和候选实际运行托管字节分别保留于两个`bezier-dev-ready-app-*`目录；原先质量检查点与失败尝试没有覆盖。

原217fc/acf7/5392/34ffe以及历史正式发布原件固定。整体性能任务继续；现有套件两失败、mirror×8原失败、PK seed/rolesmissing及point-value资格边界仍适用。
