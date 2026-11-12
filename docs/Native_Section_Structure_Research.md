# Native BLEND有限等参数线与参数对应研究

本树固定从已接受553b2c6/cc47871/Nativefe3fa隔离；实际AGENTS与两架构已读/核同，生产src无diff，不接待审Section或q2新合同/拒绝实验。目的不是拟合/替换BLEND，而是先查真实截面结构及参数，避免直接把圆参数充当原生UV。

已读parasolid-api-doc-skill的非deprecated `PK_SURF_make_curve_isoparam`、options、PK_UVBOX、SURF_eval、CURVE_parameterise_vector等官方参考。preferred simple可简化为解析/implicit/Bcurve，否则SP；preferred SP保留SP；want_interval必须true，构造曲线可能延伸超出surface，返回部分interval必须单列。curve依原surface拥有关系，不能把无限basis当有限源。第一导数按PK_SURF_eval的矩形布局，仅请求一个方向的一阶，未把BLEND的近似高阶导数当精确证据。

实际Mac真实PK38.0.150经固定PKToy PskernelSharp/Using/ParasolidScriptHost，未加载GM为pskernel或手写ABI/session。输入为接受cc唯一窗的真实GM最终self XT与PK独立产品XT：

- GM SHA `37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba`。
- PK SHA `b1b0028feca8fd51e0b4b5615d4c34ad81adbee43648dd6c5e320ef9af464655`。

两body各10face（6Plane/3Cylinder/1BLEND），逐真实face/surface动态查询，不用固定实体tag猜映射。每BLEND两个方向、三个固定参数、simple/SP各一，共24次构造；每曲线五个原face参数范围中的点，同时记录curve返回有限区间、surface/face UV、原API退出、反求曲线参数、位置与一阶链式关系，均属于本次PK接收产品，不冒充GM运行时参数观测。

修正后的24次构造及120个点/一阶反求均API0。固定u的simple返回Circle(class3002)，SP返回SPcurve(class3006)；固定v两偏好均SP。强制SP路径自由参数与原surface自由参数相同，五点相同数值eval位置误差至约1.85e-15，一阶比例接近1。固定u简化为Circle后，同一数值直接代入curve参数，GM产品最大点位差约1、PK约.912；反求匹配几何点约1.02e-15内，但d(circle_t)/d(native_v)随本次选定u约.552至1.430。圆几何与原生v参数不是同一对象，不得据此改变BLEND定义或直接替换求根参数。三个u、五点及导数只作有限观察，不签任意截面的全等或参数公式证明；更未测算法性能。

原 `temp_docs/native-sections/pk-isoparam.json` 是首轮探针把UVBOX误当区间序列而24次域外构造失败的记录，driver原0只表示记录完成，不签调用成功；不可引用成PK能力失败。已核官方UVBOX顺序为[lowU,lowV,highU,highV]，修正索引后独立 `pk-isoparam-corrected.json` / summary及两组stdout/stderr/exit保留，未覆盖首轮。首轮探针当时未提交，无原源码snapshot，不补造其提交/字节证据。

源码 `RollingSectionEvaluation` 定义为原great-circle插值，原生v是插值参数；小q由带完整尾界的解析Weight证明，大q使用原transverse/arc函数。后续任何利用截面结构的方案必须保留这些定义、reverse/sense/finite branch与所有证明；不能仅根据PK返回Circle就更改v或删除q近零/反向/奇异检查。

下一实现优先考虑按实际一维需求计算native isoparametric bounds：现有iso适配器将完整position/du/dv结果中的一组导数丢弃，明确可消费的输出只有原位置与自由参数导数。先限定原显式spine、原analytic supports，保持原cell/global图与所有其他regularity/nearest/有限域检查；不自行给隐式/nested系统删去Jacobian或证明。验证完整输出/失败界与真实整API收益后才决定是否保留，不靠此PK采样给新算法签数学或收益。

Section2c0e4ea/a770/fef及两原包保持不可变且独验中。cc旧窗口/新物性合同不变，未主线合入、未签当前研究算法接受或整体PK目标。
