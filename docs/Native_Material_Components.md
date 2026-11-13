# 有限源边连接的材料分类

当前最新集成已通过2219项Release回归。下文保留各次执行的真实状态；周期源环
缺口已由后继准备视图解决，见`docs/Native_Source_Ring_Windows.md`，不是删去原失败。

本轮改变Boolean处理链对已构建拓扑的消费，不增加持久模型或通用Engine。
既有原生支持面/有限源边、交曲线、事件身份及有向face arrangement继续是定义。

## PK依据的层级

- 官方FD55.4.4及55.4.14定义region selection和材料侧，不能把sheet当solid默认
  处理，也不能据本实现声称支持所有material-side选项。
- MCP静态链显示`BOO__classify_adjacent_faces`依赖面排序、in/out分类及分类表更新，
  `classify_in_out_faces.isra.0`具有`set_alternating_class`调用；后者不等于已核运行。
- 同平台PK38真实NURBS/NURBS fillet后subtract动态命中
  `BOO__all_halfedges_in_chain → resolve_face_classes → BOO__classify_adjacent_faces`，
  并命中其`QTP_faces_order`、`classify_in_out_faces.isra.0`和
  `BOO__update_class_table`。记录为
  `temp_docs/native-system-alignment/native-material-processing-dynamic-v1.log`。
  GDB raw0，原被测操作进程raw2为物性U；不是GM验收或性能窗口。
- 未命中的alternating/general-classify/point-in-body断点不能证明这些路径不存在，
  也不证明PK所有Boolean使用本轮GM的连通分量规则。以下充分条件是GM算法依据。

## GM充分条件

在完整裁剪/分割/arrangement之后，只考虑原输入edge的真实有限片段：

1. 片段没有任何切割占用标记；按既有canonical edge身份查找，不按坐标猜共享。
2. 恰有两个不同面片的相反coedge使用，且来源是同一输入body。
3. 本次支持面关系全部为已解析Empty/RegularCurves；同域、接触、轴向接触以及
   未解析关系不启用传播。没有跨参与者传播，也不跨新增交线。

两个面片通过该片段的开内部邻接。完整有限交线已进入arrangement，故跨此邻接
不穿越其他body边界；其他body的内外材料值相同。连通分量取最小面片索引为种子，
种子继续执行原完整`RegularClassify`及原内部witness失败处理。其他成员使用同一
材料选择/反向结果。输入owner一致且全部是原外向solid边界，故布尔两侧谓词相同。
hasClash/targetClash由种子更新；不能将几何求值失败转成空集或outside。

多个shell、孔环、disconnected片段和仅共顶点不会自行连接。第三个边使用、同向
使用或来源无法绑定使该邻接不成立，不从异常拓扑导出材料证明。
每个分量仍有真实几何查询；这是拓扑传播，不是删除分类证据或跨命令缓存。

## 切割占用身份

旧代码在复用原生有限直线或InheritedSourceEdge时直接采用源边片段，但没有总是
设置CutEdges位；AddEdge复用已有边亦同。现在这些路径保留几何/拓扑身份，并标注
该边已被cut占用。即使本face已经有半边使用，也必须先标注，再跳过重复插入。
否则材料传播会把实际交线当成普通源边。标记只在本command生存，bit2的keep-imprint
语义不变；没有按长度/距离合并短边。

实现使用stack span和原既有工作区，不申请托管heap；没有新全局缓存。真实几何、
拓扑、操作状态、回读生命周期与物性仍分开验证，验收结果随后按真实运行追加。

## 当前真实执行

`temp_docs/native-system-alignment/`下保留以下原始记录：

- `native-material-propagation-managed-v1`101/101、raw0；新增邻接规则正负例
  `native-material-components-rules-v1`2/2、raw0，含切割占用、跨owner、非源边、
  同向使用、第三个使用和禁用传播控制。规则例仅验证拓扑条件，不代替实体几何。
- `native-material-propagation-whole-v1`四族×2独立Fix/Boolean/fresh XT fullcheck/
  strict及contact控制均通过，raw2物性U。warm Boolean winding
  161/155/213/205→113/109/144/139；此前口头第四族写206，以原日志205为准。
  这是减少真实几何查询的证据，wall7067.106ms不是固定窗口性能接受。
- `native-material-propagation-rational-v1`三个反向有理native U[-2,3]族，含九次
  .125/1.7/8反射旋转、copy/delete-source、XT回读再Boolean，几何拓扑通过，raw2。
- `native-material-propagation-short-edge-v1`自建短边Fix、两次下游Boolean、复制
  删源及回读再建模、有限fin UV box检查通过，raw2保留八项物性U。
- `native-material-propagation-booleans-v1`43个独立普通建模场景raw0。
- 额外守卫曲面同域多刀具`native-material-propagation-coplanar-v1`三项均在
  Arrange/BoundaryAssociation返回157、raw1。相同runner加载固定f745 Native
  的`native-material-coplanar-fixed-f745-repro-v1`逐项同样失败，raw1；它早于本次
  材料传播，仍是当前真实缺口，不能引用旧acceptance历史通过覆盖。本任务继续排查。
- `native-material-propagation-allocation-guard-v1`实际生产Release IL检查3122方法，
  raw0/CLEAN；未运行C ABI/AOT。当前完整托管回归仍在运行，不提前签发完成。

后继有限关联修复：三项coplanar失败均由不属于当前有限片段的事件触发，例如已知
共同无限支持线上的参数3对应有限[0,2]，逆序为-3对应[-2,0]。现在对真实实体precision
包围E和精确仿射原生曲线L(t)=o+dt计算区间
`dot(E-o,d)/dot(d,d)`；其整体严格在[first,last]外才返回“本片段无该事件”。
这是有限域排除，不给点逆解以根证明资格，不放宽端点边界。跨端点的完整区间仍未决。
该规则支持原生Line和已有逐系数证明的仿射BCurve视图，不重定义持久曲线。

`native-finite-affine-association-coplanar-v1`实际三项全部full/strict/物性Verified、raw0；
`native-material-finite-contact-v1`五个轴向有限接触imprint同样raw0。
`native-material-finite-conic-v1`四个平面/圆锥曲线有限裁剪通过；第五个周期接缝场景
在种子第一次Boolean的Split/BoundaryAssociation返回157，raw1，固定f745复现相同失败
（`native-material-conic-fixed-f745-repro-v1`）。不将其下游尚未执行阶段写成通过。

`native-material-finite-integrated-full-release-v1`完整2216/2216、raw0、IL guard3183
CLEAN，覆盖本轮材料传播和有限仿射排除源码。首次完整回归误用Debug，owner在
1895项通过、10m18s时主动终止已核自有testhost PID3325193，原raw1/host aborted
保留为未完成执行，不能说整suite通过或新建模失败。一次有界stack定位于
ContactBooleanConsumerTests的winding/Bezier求交，只证明当时调用链，不作排他成本归因。
