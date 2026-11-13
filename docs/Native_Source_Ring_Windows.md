# 原生整圆环的操作内有限窗口

官方XT4.3.10定义ring edge的两个vertex均为空；曲线须按modeller resolution落在
其原生所属面的支持面上。没有vertex的整圆参数零点不应被当成实际拓扑端点。
这里使用既有Circle/Ellipse定义、原参数和curve–face归属，不更改持久几何或XT。
以下选窗算法是GM实现；未把它声称为已反编译/动态证实的PK算法。

## 结构差异及失败证据

固定f745的圆柱连续切割在第一次Boolean的Split/BoundaryAssociation返回157。
相同输入/选项下真实PK操作成功。旧oracle先执行GM，失败时未运行参考操作；现先
完成独立PK建模、full check并保存其真实XT，再执行GM，因此负例也有真实参考产物。
原始旧执行保持原字节，新执行分源另存。

跨零点的完整根区间如[-2.49e-14,2.49e-14]无法被塞进[0,2π]的线性窗口。
先在Split后移窗的候选虽越过这个错误，却留下两组相同区间、不同事件身份：完整
求根阶段已没有合格见证，后移窗不能补回。该候选及其raw1保留在
`temp_docs/native-system-alignment/native-source-ring-{window-conic,seed-diagnostic}-v1.*`，
临时trace与patch单独保存，生产代码已移除。没有按近XYZ或重叠盒强并事件。

## 当前准备视图

`PrepareRegularSourceRingWindows`在读取边界后、建立有限关联和求交之前准备窗口。
仅处理两个vertex均为空、完整一周的原生Circle/Ellipse；带vertex的边及部分弧不移窗。

先用真实coedge/loop确认源边已经位于哪些输入face上。这些原生归属是模型定义，
不作为待求交的独立方程重复证明；旋转后不能靠系数偶然舍入为零来重建这层关系。
其他native Plane及逐控制系数证明的精确平面视图，使用原方程
`x*cos(t)+y*sin(t)=h`。恒零方程不形成孤立交根。

每个非恒零平面方程在一周至多两个根。对M个方程生成至多2M+1个候选相位，但
候选本身不证明无根：每一方程在起始相位和末端周期接缝包围上的区间值均须严格
不含零。末端包围同时包含浮点last及精确2π的区间提升，不能遗漏舍入产生的小缝。
不能证明任何候选时保持原窗口及原消费者的实际成功/未决结果，不把失败写成空集。
这是新增有界准备工作，不提高原求根/细分预算。

所有后继coedge使用同一准备窗口，原完整求根器生成相同有限源/方程/序号见证，
再裁剪、分割和选择材料。窗口不生成新vertex、不改变原curve自然域、sense或参数化，
不跨命令缓存；不以一个点证明整个有限区间。此轮接入regular Boolean消费者。

## 横向执行与适用边界

`native-prepared-ring-window-conic-v2`五个独立平面/圆锥曲线场景full/strict及物性
Verified，raw0，包含原失败的两次连续切割。v1因缺少C#using构建失败，不能算建模失败。
`native-prepared-ring-managed-v1`178/178、raw0，含圆/椭圆、旋转尺度、完整根证书、
带vertex/部分弧不移窗以及原有限分类/失败负例。后继原生归属准入与完整集成
`native-prepared-material-integrated-full-release-v1`2219/2219、raw0，IL guard3184 CLEAN。

`native-prepared-ring-lifecycle-{small,unit,large}-v1`中只有周期源环场景接受额外输入
变换，另外四项是原控制，不能声称全部五项都变换。实际周期场景分别以.125/1/8
反射旋转平移，独立GM/PK的target、两个tool均copy/delete-source/transform/XT回读，
GM回读必须可编辑且fresh XT输入逐项full/strict对照独立PK。三次执行均raw0。

后继`native-prepared-ring-staged-lifecycle-*-v1`另外在第一Boolean之后保存双方实际
XT，独立full/strict/物性验收，再删源、可编辑回读，第二Boolean消费该实际回读结果。
small/unit/large均raw0，两个Boolean及物性分别通过。各步分别验收，不借下游checker
补签上游。`native-ring-staged-f745-negative-v1`使用同一个新runner和真实PK38，仅将
产品Native替换为固定f745的068f4930…；PK完整参考及第一步质量实际通过，GM输入
fresh XT严格对照也通过，但GM第一步仍Split/BoundaryAssociation 157、raw1。
这保留了真实旧实现负例，没有将native参考或重建模型替代实际GM产物。

辅助临时标量probe的缺using、临时目录未导入Host及schema970未就绪执行均保留
原退出，未被用作几何/根证书证据。上面的制造解、完整实体操作及实际PK证据单独成立。

非解析周期支持、带真实端点/奇异点的周期参数、通用多方事件和一般曲面物性U，
不由此证明完成。模型名、坐标、固定tag和此验证族均不是生产算法的分派依据。
