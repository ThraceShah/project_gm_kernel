# 原生脊线单参数点的完整证书

这是GM实现及数学依据，不是PK内部迭代次数的已核结论。原生UV、CHART方程、
曲面定义和所有外部几何/拓扑/精度验收保持原合同。

## 适用范围

仅适用于原已支持的两张BSurface或BSurface/Plane原生UV系统，且脊线参数请求
`U0==U1`。V可以仍是区间，必须另通过原frame包围。reach、curvature、anchor及
acceleration请求不走提前结束；非零U区间继续原六轮收紧和全部终验。
不扩大原regular-sheet或支撑/分支适用类别。

## 提前结束的充分条件

初始阶段仍对原方程F(x,t0)=0执行原严格Krawczyk包含及收缩条件，证明原包含盒内
相同分支的根。数值Newton候选、paired UV或CHART位置本身均不是证书。
之后每次将根盒与Krawczyk像相交，仍包含该根，故并非必须固定做满六轮才能
得到合法的更小包含盒。

提前返回须同时完成：

- 对当前根盒重新包围原Jacobian，并用原区间线性解验收原生微分方程；
- 按原offset-foot定义恢复XYZ/UV及导数包围；
- 原nearest检查完整成功；
- 对空间请求，原TubeFrame的D0、沿U及沿V包围全部成功，覆盖实际请求的V区间。

隐式微分仍使用当前根盒中的Fx、Ft，解Fx*x'=-Ft；不将点残差或数值导数冒充
导数包围。支撑参数请求从同一已验根/导数选择原生UV分量。
Krawczyk像与微分RHS使用不同存储；微分求解不能覆盖下一轮收紧所需的包含像。

原最多六次根盒收紧不变。点请求现在可逐轮尝试完整终验，最多六次；这是新增的
有界终验尝试，不能笼统称全部内部工作量上限未变。首次终验未成立会继续收紧，
最终仍不成立则返回实际失败；Newton20次、初始包含搜索及下游单元预算未提高。

## 缓存及精度边界

点证书只适用于其原生参数点及原CHART身份，不证明任何非零参数区间。保存点
结果时不设置NativeDimension父区间资格，故不能由它推导一段曲线。已有非零父
区间仍按原全部流程证明；原shared Cartesian witness路径亦未改变。

更早通过的点包围可能比固定六轮结果宽，但仍须是原定义的真实向外包围。调用者
的有限域、根/材料选择、实体precision、strict compare和物性门限没有放宽。
是否在复杂有理/变换族中降低整链成本必须另用真实操作及固定二进制窗口验收；
局部Jacobian减少或单次wall时间都不能替代该结论。

## 固定源码整链观察

`native-point-fixed-normal-abba-v1/comparison.json`（位于
`temp_docs/native-system-alignment/`）保留98084d9d与f74582c0固定源码的串行ABBA。
candidate Native SHA256为068f4930ce5f8a285e933e2699f29ac3a57996890ca4f51695afa70eaa2b3bcd；
baseline为上一固定包中的c4a083d8b3b5a3f6d7336d261a703cc766090552c719bbf7f2261e1deeb7ef09。
实际PK38、相同runner/helper和五项JIT策略均核验，关闭profiling；原event-arrangement
边界trace在两端相同启用，计时包括其代价。每族rep0排除为cold。

| 场景 | baseline warm GM API ms | candidate ms | 比值 |
|---|---:|---:|---:|
| 四个普通曲面族，每进程四次 | 8179.675 | 7306.641 | .893268 |
| 三个反向有理族、native U[-2,3]、尺度1.7回读再建模，每进程两次 | 10513.971 | 10210.146 | .971103 |

普通Fix为4032.648→3275.304ms，Boolean为4147.027→4031.337ms；有理Fix为
940.710→738.449ms，Boolean为9573.261→9471.697ms。算法主要减少Fix成本，
有理生命周期Boolean仍为主瓶颈；candidate同窗GM/PK API总量比仍约69.9与281.1。
八个子进程共88组模型均完成各操作、fresh XT fullcheck/strict及contact控制。
所有子进程raw2保留物性U，driver raw0仅证明采集完成。此为owner观察，
独立接受和performanceQualified仍false；不是整体性能目标完成。
