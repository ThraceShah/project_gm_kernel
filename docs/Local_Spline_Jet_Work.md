# 单个隐式tube证明内的完整spline jet复用实验

从接受b8/e7/Native6a843隔离，实际读本树AGENTS/两架构并逐字核同接受源、当前源码/空初始diff；native ensure原0/ENV_READY。依据 `diag/accepted-b8-cost@c9316c1` 的真实接受源三fresh NURBS对外部采样和实际代码：4179Jac/8358UV残差方向/12537直接XYZ列每rep已经运行，不能误判旧优化没启用。可见Jac/投影栈是路线，不是排他CPU/可删预算。dd和b095已停止，不带其任何生产实现；4e5共同UV微分、675稳定contraction、1762多项式jet已拒报告实际读，不纳入。原query/norm暂不混。

问题在同一次BoundSegment里：source program/occurrence图/fixedCharts/几何scalar借用存储固定，Krawczyk只改变数值boxes。旧IntervalJacobian对任何UV read set每次清完整cache，使已有同源同UV完整几何jet不能跨相邻Jacobian复用。新局部scope在BoundSegment分配span后**清零一次**，只在这个固定source/occurrence的wrapper调用上声明localSplineScope；其他独立调用仍按原read-set guard清缓存，空read set不接触poison/未初始化值，不能从另一个program数值相同的key借证据。

entry是原完整几何jet，存储与key字段不改模型定义；实际UV四端点改用binary64逐位匹配（包括±0区分），原scalar offset/degree/count/dimension/rational/periodic字段核查保留。slot由同一固定occurrence的UV变量索引定位，几何对象的借用存储在该纯函数scope不变，不能将该许可扩到整个Fix或跨program。未共享原子实体/owner身份，也不以不同program数值相等建立共同拥有关系。

成功cache key匹配才复用原IntervalSplineBounds全部值；所有Jacobian方向、完整残差/frame/normal finite、Krawczyk/remainder/strict contraction、nearest Hessian、reach/witness/CHART/native UV/参数RHS仍每次原算。UV盒或source元数据变化即重算，没有容差匹配/采样/近似/新几何或原定义转换。refresh开始先Ready=false，原bound失败可能已部分写Values，不能让旧key仍Ready；失败不缓存、不当空交、不改原退出或预算。没有新heap/globalcache/solver scratch/配额/ABI/XT/拥有关系。

187/187原0/24s定向；原185完整期望/预算/失败控制保持。两新增控制为失败的periodic大参数refresh不留旧Ready、binary不同零端点不命中；原多个dual seed/sense/offset/曲面/盒变化与full矩阵/nearest等仍检查。实际平面墙完整圆角包围开关有局部命中且所有9项逐位同；曲面墙该单盒无相同UV命中但输出仍逐位同，不冒每个case均命中。普通API实际命中率未由测试counter补造。

首轮187中183pass/4fail原1保留：两个私有反射delegate未增加本次scope参数、制造的非周期[-10,10]外延实际Success而非预想失败、曲面墙单盒未命中。修正当前签名绑定、采用已有真实periodic shift>1e14未解析路径、仅要求确有重复UV的平面控制命中；未改产品容差/验收/数学定义或把原失败洗掉。空read-set保持原无写合同；独立privateJac每次清cache的different-program/poison/失败重试测试继续原期望，不借局部scope跨program。focused与focused-fixed两日志/TRX分开，原Source未提交版本不补造身份。

下一步一次同helpers完整NURBS六对/源，自家实际Fix/transmit→真实PK receive/compare/旧1e-7三态，全冷暖及raw状态保留。若完整收益弱就停，不以sample share、cache次数或局部bound时间签CPU/性能；强才稳定整包质量/唯一窗/一次重大独验。主任务79920未完，无新scope/门户等待或worker_done，无Windows/AOT/旧回执。
