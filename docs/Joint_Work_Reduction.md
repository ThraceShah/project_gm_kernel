# Joint共同工作量追踪与后续优化

新开发基线为已独审1654/Native224（公开文档头73b06e1）；完整接受见review/pk-unified-performance-1654@2f8aea492d21f39269e514d9bcd3d8e792e3903b。Mac两NotConverged和三态/描述性性能范围不扩大；旧两个ZIP及已测窗口保持冻结。

先仅做一次有界完整三操作诊断（curved/NURBS/shortedge各fresh pair），不把带诊断API时间当性能测量。临时Native回调无分配，所有JSON/反射/栈与I/O在oracle诊断层；首次把记录器放Native触发AllocationBanCheck原1，原日志及源码留temp_docs/joint-work，不绕检查。

每BoundSegment记录source/generation/sense/ownership的全部live几何保守超集与完整原生program各语义字段（无padding字节，所有double保留bits）、SourceBlock/root/实际occurrence和CHART、原生u/v、中心snapshot、全请求/输出容量/workspace/reach预算和实际require数组。capture输出只读长度、不读未初始化值。每scope有唯一ID与真实immutable标记，scope0/可变阶段单列，不跨scope统计复用。成功tube还记录完成原inclusion/nearest/reach/derivative后的boxes和导数，最终frame成功/失败另列，不把frame失败当无根。

首轮虽记录全部live几何source/generation集合和完整program，但临时编译视图没有逐一标明实际owner；其中相同方程组可能属于不同owner，不能把该语义分组冒称同tag命中数。已保留首轮原件，并以e17e6ac补正有界追踪：每个prepared program直接带真实owner/tag/generation/sense/准备种类/block，模块启动实际记录PID/capture/source与Native/Oracle MVID；匿名临时验证program明确不计可复用身份。按不含v的候选层投影统计仍保留完整source/program/spine/CHART/occurrence/中心及请求资源；仅普通无witness/reach/curvature/参数输出的tube才比较跨v需求，不改九输出UV缓存。

上限为每进程300000 BoundSegment，触顶抛出明确诊断异常，不发布不完整追踪为完整。原始definitions/calls JSONL、当前runner所有质量与rawexit、实际DLL/PDB/source identity一起保留。通用当前helper未改，诊断props仅向BooleanOperationOracle显式开启时添加脚本；最终生产改动会移除此临时分支。没有C ABI更动或AOT/Windows运行。

## 实际一次追踪与已选实现

完整三操作均原2/Unresolved，API均0；Native全n/roots/SSI/winding/budget工作量与1654固定候选相同。原995de01诊断源独立保留在diag/unified-joint-native-995de01，回调/反射/追踪props已从生产候选移除。每进程PID文件与当前runner case的关系由原文件创建时间落在串行process-start窗口内绑定，属于文件时间证据，没有补造进程内case身份观察。

curved共5328次BoundSegment（含显示/检查），immutable阶段384次成功tube，完整需求96键/288次重复，tube payload逐位相同，无额外跨v收益。NURBS共664/immutable122，完整需求96键/26重复；仅投影输出surface/v后55键/67重复、21组跨v，payload仍相同。shortedge共2463/immutable2391，成功tube2020；全需求1645键/375重复，普通无特殊需求1566次/1214键/352重复，主要是compact消费者。存储program以外的编译视图源仍严格通过真实owner准备，不把相同内容自动当同一实体。

实现保存已成功完成原inclusion/nearest及解析导数的完整joint boxes/derivatives/occurrence，不保存或投影九输出UV值。prepared Native program显式带真实source tag/generation/sense、存储surface或原生curve准备种类和原program block；完整program由该受控Native输入准备决定，key另含全部program数量、root spine、精确u endpoint bits及workspace容量。只有当前immutable命令可借工作区；新source/generation/方向/准备种类/block/CHART原生定义或命令不得共享。编译curve准备仅用于新建compiler从该实际ICurve及实际支持/CHART读出的视图，verification临时改写program未授权此identity。

只承认普通完整tube请求；所有witness/capture/requireCharts、reach/curvature/acceleration、support参数和anchor输出都执行原路径。命中仍按请求surface/v重新算frame；失败没有条目。工作区128条（参考本次96/55组普通需求），由原command scope可选native存储拥有，分配失败走未优化算法，不取proof scratch、不增算法预算，scope退出释放。它是单一具体joint中间结果，不是新几何、通用结果框架或历史格式层。

最初使用record生成ToString被Native分配检查拒绝，已换普通readonly值类型；最初测试误用不存在的Contains导致编译原1均留原件。修正后44项相关检查实际原0，含完整tube/解析导数、跨v重新frame逐位相同、tag/gen/sense/kind/block隔离、不同native区间/workspace、坏witness仍失败、退化不发布和0托管分配。普通完整操作/lifecycle、两Mac实际一般原因与最终工作量/收益闭环正在继续，不由44项局部绿灯签整体完成。

## owner修正追踪、完整操作与一般原因修复

补正追踪e17e6ac刻意关闭新tube复用，完整三操作仍各raw2且原2688/4648/18951 Jac、1034/150/22371 root cells等不变，不用带记录器耗时估性能。已知owner的curved384次成功tube/96完整键/288重复；NURBS96次/完整70键26重复、投影u需求29键67重复（21组跨v）；shortedge1032次/918键114重复。全组payload逐位相同。未授权身份的compact等临时program原记录保留但不进入eligible命中判断；原352 semantic重复不偷换为可缓存source命中。

1f075e9首次完整普通三操作保持27/27/35 outstanding、均raw2，无新硬失败；Jac实际2688→1260、4648→4193、18951→18822，root/SSI/roots/winding/budget保持。这是工作量验证，冷单次API时间不作倍率。该版直接映射128条；针对已证96组在同容量的碰撞，当前改为最多8个槽探查，未增存储或证明预算，后续完整包验收仍待执行。

两项Mac旧失败已按真实消费者排查：原fixture的u求界成功，uv求界raw状态NotConverged集中在最后CHART和其上端点（不是修改测试先验）；点求值成功。BoundSpineSupportSection只允许longitudinal>0来用atan(lateral/longitudinal)，因此无法覆盖严格lateral>0而longitudinal跨0的真实右角端点。现在保留原x>0分支，另在严格y>0时用等价π/2−atan(x/y)，π/2向外包围、导数同原商法则。原sine/nearest/分支/有限检查保留；两者均不具严格符号时仍NotConverged，不以误差或采样放行。没有改预算、容差或原测试期望。

包括原两失败和新增半平面制造导数/原点/负轴失败控制在内，54项集中窄验实际原0；这不补签旧1654的1998/2000 raw1。初次诊断helper遗漏AllowUnsafeBlocks导致编译原1留存；U与UV两个独立probe的原输出分别保留，不能把U成功误写为UV成功。

## 固定完整候选1850534的实际闭环

计算SHA `1850534979379127c8452e85ec2682a86470d96e`；Native树 `1161094e9feff6e92df51f7ac2f16ceba4a42893`，src树 `c53d5d4bc42111fef62cdb08d32bda38f3ff346d`。完整Mac套件实际**2019执行/2019pass/0fail/0skip，build0/test0，约6分钟**；原两SupportingBlend测试期望和已有失败/回滚控制未改。原1654两失败/首次栈溢出、旧raw1与全部历史窗口均不回写。没有Linux/Windows全绿或普遍性能因果声明。

八组实际当前几何闭环source均1850534，捕获无errors/fail：curved/NURBS/std各原2，pass/missing为14/13、16/13、35/17；rigid.125原0且51/1 missing保留；exactmirror8原2且29/23；shortedge双产品生命周期copy/删源/镜像缩放/cut1/可编辑自XT reload/cut2/cleanup原2且133/77；NURBS生命周期原2且16/13；curved中间subtract/reload后Boolean原2且17/26。真实PK fresh receive/full/strict/finite、modulus物性、实际input provenance/roles缺失保持当前三态合同，导出/显示/检查在真实来源上，不用point-only脚本。单操作与下游组合分别验收。

最终普通三操作Jac为**672/4179/18822**，对accepted1654的2688/4648/18951；各根/SSI/roots/winding/budget保持。有限8槽探查后curved恰为96×7次，而NURBS少67×7次；这是实际工作量减少，不是CPU占比或最终倍率。只有cold开发完整操作数据，必要稳定同机窗口尚未执行，不挂旧−11.97%、旧217或185之前的倍率。

八组实际loaded Native MVID `b53b178e-b0ac-46d6-99c9-e48970e75ea3`、DLL SHA `a5289e5c8eed72e42f69e8429688cfb62aa5cf21a73997fc9fa1600a9f9b5347`。完整套件testhooks Native MVID `dda9c0b8-6f0e-4b7a-a98c-9a650a9fce10`、SHA `3284174582bdb03c9cebe4a31bc1c711f786ab9b5ddca351ce2cb01236930766`；Tests MVID `543ae321-61ca-46b1-92a8-d59f9e9c75ad`、SHA `42e1b47850ff66bafba29a0fb08130986d8c8f29f587bc6dd61a8f3597a59b18`。共10个PE/portable-PDB链接逐核335 Native/166 tests tracked文档，generated/unmapped另列；三个测试DLL和三个PDB前后字节相同。普通producer实际loaded见证与测试sealed文件/前后清单见证分列，未补造start/end或测试loader观察。

机器收据为 `docs/joint-work/quality-receipt.json` 与 `docs/joint-work/source-binary-binding.json`。全部原件在 `temp_docs/joint-work/`，补正owner三操作原件104文件经逐hash复制到owner-trace-original并保留原路径来源。核验器原0只签字节/来源一致，不把七个模型原2洗成0，也不签当前包独立接受、main集成或发布。下一步是这一固定完整包的一次集中主要算法/集成接受和必要稳定同机公共当前门测量；PK整体目标仍未完成。
