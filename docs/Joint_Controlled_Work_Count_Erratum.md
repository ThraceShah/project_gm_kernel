# Joint185正式窗口工作量逐rep勘误

本勘误纠正 `e139092f1aff3daf32f90d3f8c2975fd544784c4` 的 `docs/Joint_Controlled_Measurement.md` 中这句泛化：

> 本窗重复实际工作计数与质量阶段一致：curved Jac2688→672，NURBS4648→4179，shortedge18951→18822；各原root/SSI/roots/winding/budget保持。

**正式窗口的Jac与方向计数须按进程和rep分别读取，不能用rep0覆盖暖rep。** `672/4179/18822`仍是185质量阶段单fresh三操作原执行的实际计数，也分别是正式窗相关候选进程的rep0；它们不代表正式窗的所有重复。原e139文档和已交原包保留，本补充覆盖该句对正式窗全部rep的概括，不改原日志、原report、XT、计时或执行身份。

下表列原日志中的 `rep0 / rep1 / rep2`；rep0冷，rep1和rep2暖。同一行列出的两个进程各自都有这三个实测值，并非只取其一代表另一进程。

| 原进程目录 | Jac（rep0 / rep1 / rep2） | directions（rep0 / rep1 / rep2） |
|---|---|---|
|08-curved-baseline、11-curved-baseline|2688 / 2688 / 2688|13440 / 13440 / 13440|
|09-curved-candidate、10-curved-candidate|672 / 686 / 707|3360 / 3430 / 3535|
|12-solidblend-baseline、15-solidblend-baseline|4648 / 4648 / 4648|23240 / 23240 / 23240|
|13-solidblend-candidate、14-solidblend-candidate|4179 / 4179 / 4179|20895 / 20895 / 20895|
|16-shortedge-baseline、19-shortedge-baseline|18951 / 18951 / 18951|123277 / 123277 / 123277|
|17-shortedge-candidate、18-shortedge-candidate|18822 / 18814 / 18814|122570 / 122514 / 122514|

sphere和planar八个进程的三个rep均为Jac0、directions0。所有20进程/60rep的完整 `PROFILE`、`WORK`、`ROOT_WORK` 计数，包括residual-directions、record-builds/nodes/rebinds、direct-spline-point-columns，以及各rep的root/SSI/roots/winding/budget等原字段，已逐项另存 `docs/joint-controlled/work-by-repetition.json`。每条保留原日志行号/原行；rep顺序与原Capture的GM step和API时间核对，没有从rep0填充其他rep或重建缺失计数。计数变化的成因在此不作推断，也不将计数当CPU成本。

机器收据逐进程引用原 `oracle.log` 的SHA256和原Capture SHA，统一绑定原report SHA `f129fb858bdffb7283c152ea8dc8e6d7145aa7eaf983008a1f7e018a51f52b80`，并再次核实原窗口ZIP SHA `5a22e9acae103d1b3b6fcfcb920c7eb13ae526d42fdf5181ac8e78db332436f1`。新收据自身SHA为 `b4d9d9c65b62b3cb0cce7401e5834020738d543f40779e1804a1df29df361bf3`。

实际测量metadata/source仍是候选 `5d5bf8ebdfd639c935ac9a1e27ef9e682814dcc3`、基线 `73b06e1004122f6897da10532d6253579a033aa4`；计算仍为1850534、Native1161094e。原16进程exit2/4进程exit0、runner2、performanceQualified=false及全部三态/时间/冷暖/minmax/planar回退保持。本元数据补充不宣称重测或增加算法、时间及统计接受范围，整体看板应消费逐rep原计数。

本次仅从现有原件派生，读取器与原退出记录位于 `temp_docs/joint-work-errata/`。未运行模型、Mac性能窗口、家族或完整套件，未改计算、测试、原执行SHA或两个已交ZIP；也不为这项口径纠正开启新的独审阶段。
