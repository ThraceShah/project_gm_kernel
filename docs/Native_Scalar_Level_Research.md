# 原生截面标量方程的连续结构调研

状态：**只读源码/官方接口与真实PK行为调研，尚未修改或实现生产包围算法，也无性能资格。** 独立从固定已接受b8c74b9/e7f0a48/Native6a843建立，实际读本树AGENTS/两架构/当前源码，初始tracked diff空，private native ensure原0/ENV_READY。不带被停止b095的PreparedSection/PreparedNativeSection/span或Find入口准备，不改原e7或b095任何证据。原完整79920目标继续。

当前e7 `AnalyticLevelSet` 从XYZ区间减目标origin，对Cylinder以原(I−aaᵀ)整体线性映射去轴向，再计算length-scaled二次隐式方程及其导数。这个实现已有避免delta−a*(a·delta)重复坐标依赖的修正，不能重新假定它未做。下一假设是进一步直接包围同一个native-v标量方程，减少三维包围再二次组合的依赖膨胀和相关细分，不将一次准备的微小节省作为新目标。

## 精确表达式边界

只考虑已有同源完整二维cell证明覆盖的固定nativeu，原explicit spine/direct analytic supports，且原RollingSection选大q分支、原transverse/sine/arc均成功的情况。小q、零角及不适用者继续原表达式及完整值/微分尾界，不能用除sin(theta)掩盖可去奇点。

原定义是 `S(v)=C+r[x cos(theta*v)+y sin(theta*v)]`，其中x/y/theta必须从同一个原source的IntervalNormal、signed ranges、reverse、transverse归一和Atan2连续表达式取得。v始终是原nativev，theta*v只是计算式；不把PK简化Circle参数当nativev、不拟合圆、不建新几何或在模型中改参数。

对Sphere用M=I，对Cylinder用原AnalyticLevelSet的M=I−aaᵀ；令d=M(C−O)、A=Mx、B=My，目标radius为R，原方程F=(|d+rA cos(t)+rB sin(t)|²−R²)/(2R)，t=theta*v。三角恒等式得到精确五项（不假设浮点axis正好unit、不假设投影幂等，也不把x/y当精确正交单位来删项）：

```
F(v)=c0+c1 cos(t)+c2 sin(t)+c3 cos(2t)+c4 sin(2t)
c0=(d·d−R²+r²(A·A+B·B)/2)/(2R)
c1=r(d·A)/R
c2=r(d·B)/R
c3=r²(A·A−B·B)/(4R)
c4=r²(A·B)/(2R)
F'(v)=theta[-c1 sin(t)+c2 cos(t)−2c3 sin(2t)+2c4 cos(2t)]
```

Plane是相同连续代入后的常数+cos+sin三项：`c0=axis·(C−O)`、`c1=r(axis·x)`、`c2=r(axis·y)`。若实现，所有系数和theta须从原区间表达式向外舍入包围真实常量，保留其不确定性；F与F'用完整有限v盒连续包围，不能从有限采样拟系数或把误差当零。必须证明常量关于v不变，以及真实source在原scope内稳定；相同数值不能作为跨owner分享依据。

这尚不是采用新算法的结论。需先验证公式、区间实作舍入和finite分支，尤其大尺度/镜像、近小q阈值、t超周期及端点/切触/同域；不会为了控制图输出相同而snap根、放宽容差/预算或以点值代根存在证据。若系数不确定性导致未解析仍原失败，不能假无根。Source母cell全9项与原二维非零偏导/图、BoundaryNode、finite/误差、CHART/ValidateGraph、nearest/生命周期必须保留。任何替换的标量包围需是同一原方程的完整连续证书，而非删除原证明；返回空间点仍来自真实原native几何。

## PK接口实际依据

实际用原生parasolid-query MCP精确查 `PK_SURF_intersect_curve` 得found:true、完整原型/说明；名称顺序为SURF_intersect_curve，未把最初找CURVE前缀的4条结果当不存在。静态Linux38地址只作索引，未用于Mac运行；internal_calls_summary为空不能推断无内部调用，预览仅留私有 `temp_docs/query-1.json`，未发布厂商反编译正文。

官方同名函数及options页已全读：有限curve bounds独立于basis；返回交点沿bounded curve排序，curve t与surface UV分别返回；simple/tangent的UV才有效，coincident start/end UV未定义；当前coincident仅报告整段coincidence。可选box只是提速，不保证所有返回点在box内，不能当有限域证明。trimmed curve会忽略给定bounds，仍要求合法interval。真实绑定 `PK_SURF_intersect_curve_o_t` 及现有PskernelSharp函数原型已实际核，脚本不手写ABI或session初始化。

`scripts/NativeScalarLevelStructure.cs` 使用现有PKToy/Using/Host，在接受e7唯一窗口真实GM及独立PK sealed XT上构造三处固定u的simple/SP有限截线，对原body的Plane/Cylinder/Sphere支持面调用真实PK，记录每次原API error/count/curve interval/t/type/XYZ；SP的返回根另以同数值nativev在原BLEND eval核位置。driver0只代表观察完整，不能改签每个API；coincident UV不消费，target曲面参数和nativev不混。源路径与hash绑定实际文件，不固定实体tag识别输入。实际运行状态见私有原JSON/日志/退出码，未执行或失败范围如实保留，不提前签全成功/连续证明或性能。

实际Mac PK38.0.150原执行完成：**108次有限section/surface请求，create/intersection均API0；84个0交点、24个1交点，总24点，12simple＋12tangent**。24次curve eval均0，交点对curve eval最大偏差 `8.881784197001252e-16`；其中12个强制SP交点以返回t直接作原nativev eval均0、位置偏差0。GM输入SHA `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`，PK输入 `b1b0028feca8fd51e0b4b5615d4c34ad81adbee43648dd6c5e320ef9af464655`，各10face，来源是接受e7原唯一窗最终产品；hash实际相同于先前cc产物的事实不更改原来源/rep标签。原driver0与API0分别核，原JSON `temp_docs/pk-native-scalar-intersections.json`，派生summary及脚本/日志单列。没有coincident实例，不签coincidence能力、任意Native参数同值证明、连续几何证明或性能；没有加载GM live冒充PK结果。

下一决定依据是直接标量连续包围能否真实降低主要rootcells/完整API成本，而不是再将b095准备以另一名称带回。未实现生产路径前不申请数学/算法接受，不跑suite/family/ABBA，不改新norm、Windows/AOT或公共模型。
