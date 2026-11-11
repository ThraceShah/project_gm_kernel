# 等参控制网预备求界实验：不进入下一生产包

基于 `0684efd`/`16aca1d` 的工作区候选，实验在原生齐次Bezier span上固定一个参数，预先计算等参区间控制网，保持自由参数、原有限域、正权/区间公式和原求根proof/预算。只允许无offset、无Blend、无sweep/parameter-line的Bezier等参线；不是持久几何或新的参数化。远程PK静态文档核对了constant-u沿整个v域的语义，不将Linux地址用于Mac。

原21项专项/既有SurfaceIso与SurfacePlane测试TRX为21pass/0fail；该后台命令未捕获原退出码，不能把TRX补签成raw0。随后补充degree0/16、双方向有理线性map与原生参数导数、解析二次有理曲面(权比例2^-500/1/2^500、域外/零宽)、offset/sweep/line不适用、root存在证书及0托管分配，最终13个专项检查原始raw0。没有重跑58或fullsuite/AOT。

原curved/reloaded Boolean、NURBS独立fillet、标准shortedge三fresh pairs均原始raw0。shortedge actual loadedstart/end MVID `1c57c00a-2668-413e-a35c-4a3354e357ba`，DLL SHA `098399ac8678608c8fb4ecb105fc656a640dce32da59ba2543ac0d2964babfe4`；原DLL/PDB和源diff保存在临时证据目录。误在正式Native构建前复制的旧9c6DLL/PDB单列为prebuild-old，不作为新实验执行身份；测试的testhooks DLL和测试DLL/PDB另存。

之后与仅缩scratch的16aca（DLL9c6）做一轮开发期同源runner/完整相同托管依赖/同输入对照，串行baseline/candidate/candidate/baseline，各进程6fresh pairs，rep0冷、其余五个全部计入。24pair原进程raw0、captureErrors空、checks全pass。计时结果：

|进程|来源|冷GM ms|全部五暖GM ms|暖中位数ms|
|---|---|---:|---|---:|
|0|16aca|1585.8843|695.2144,519.6080,512.4486,521.8027,476.0363|519.6080|
|1|isoline|1465.9924|716.8868,505.7105,478.9231,482.7237,469.2869|482.7237|
|2|isoline|1544.1818|851.3577,535.3101,557.4856,529.5682,486.0067|535.3101|
|3|16aca|1625.8947|717.9010,563.5706,548.1097,540.6795,519.3937|548.1097|

十暖中位531.2411→517.63935ms（观察−2.56037%），有明显进程内/JIT/系统波动。与增加准备和映射路径的复杂度相比，这个信号不足以支持进入下一生产包；不再追加同类窗口寻找更好数字。数学/操作检查通过和性能未选用分别保留，不签性能接受。cells22371/roots3518/Jac16870/SSI4956/winding196/budget0不变，bound requests仅77061→77053。

本分支作为完整实验源码/测试留存，后续工作从仅保留16aca工作区的固定源继续，不维持回退/双轨兼容。下一个方向是原RestrictCurve在a/b区间逐位相同时重复计算degree+1次完全相同blossom；可保留原算术顺序，计算一次再复制输出。这是源代码内确定重复，非猜测缓存key或删除导数证明。

原始证据均在 `temp_docs/pk-native-algorithm-work/`：`bezier-isoline-*`与`bezier-isoline-prepared-*`、TRX、sealed DLL/PDB、source diff；开发比较为`bezier-iso-dev-*`、app-binding/summary和两固定app目录、`RunBezierIsoDev.cs`/`AnalyzeBezierIsoDev.cs`。没有覆盖217fc/acf7等正式原件。
