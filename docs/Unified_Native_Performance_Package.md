# 统一原生建模性能候选：分块、严格余项与活跃工作区

固定计算候选为 `1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7`，Native树 `224564d1cdc0d0b6cf78c275282454751af2fd60`，整个src树 `c6e8bc553cdfecaebc3c1fac5d30688ba8628e6c`。基于已接受统一产品/当前runner `84c558c10135c117bf8be6629a157e6d230f1b2d`（Native133f56ac），保持功能合并、mirror8有限站域修复、ExactCorner、scoped cache、原生XT/BLEND/ICurve/CHART/有限根/拥有关系与当前Compare/Mass三态合同。

这是已提交推送、完成必要质量闭环的**待独立接受候选**，没有main/发布/Windows/AOT或最终正式倍率。本轮没有导入未选的等参预备c10bf4a或点blossom090848e，没有恢复旧point-only helper/门，也没有改模型、参数化、容差、证明预算或线程栈上限。

## 完整改动及依据

1. Bezier临时区按活跃次数分配，保留原上限16、默认初始化和原算术顺序；来自16aca，单独历史开发观察不继承到本统一源。
2. 已审Conic仍保留全部七变量/原残差。利用实际JG的`[A,0;B,I]`结构准备4×4点预条件器、完整7×7余项和全部七个区间导数；nearest仍验收原`delta·C''-|C'|²`负性。49项余项与通用计算逐位核对，witness禁用及各正branch门保留。见 `docs/Native_Conic_Block_Linear_Systems.md`。
3. Rolling section的同一解析级数在4/8/16/32/64/128项检查原严格值/导数尾界。只有n≥|v|且两尾界已小于当前浮点分辨率才提前结束，仍明确加回完整尾界，最大64/128与原拒绝域不变。不是删除尾项或放宽几何精度。见 `docs/Adaptive_Rolling_Section_Bounds.md`。
4. 已验证n和occurrence数之后的Joint求值/求界/最近脚工作区由固定63维改为活跃维数；未知维数的输入准备容量与原63上限检查保留。此项实际修复Mac统一基线也可复现的栈溢出，不通过增栈掩盖。见 `docs/Joint_Active_Workspaces.md`。

已实际读取本树AGENTS、两架构、统一新修复两消费者及共有Patch/当前runner差异、583原审与b17续审；两独立Mac树均实际native ENV_READY。记录/缓存/G/旧Conic的已审定义复用，未重造一套建模模型。

## 最终质量：分层保留原状态

所有下表实际运行的计算源均为1654，真实PK38.0.150、Mac arm64/.NET10。当前helper执行fresh GM XT→真实PK receive、full BODY_check、严格Compare/有限边方向及15分量含modulus物性。几何/拓扑、数值未决、来源缺失和原退出分列：

| 最终执行 | 结果与范围 | 原exit | Capture pass/fail/missing |
|---|---|---:|---:|
| curved block Boolean | 独立完整单操作几何/拓扑通过，数值未决 |2|14/0/13|
| plane/NURBS fillet | 独立完整单操作几何/拓扑通过，数值未决 |2|16/0/13|
| std shortedge Fix | PKseed→GM receive→GM Fix；PK独立Fix，几何/拓扑通过，数值未决 |2|35/0/17|
| rigid+scale.125 | 原几何/有界物性通过；1个来源/显示缺失照录 |0|51/0/1|
| 准确mirror8 | 真PK/GM Fix各0，fresh/full/strict/finite通过，数值未决 |2|29/0/23|
| shortedge lifecycle | imported-PK产品和GM产生产品两条链，各copy/删源、mirror-scale、cut1、自XT可编辑reload、cut2/cleanup；各步物性未决保留 |2|133/0/77|
| NURBS lifecycle | copy/删源/变换/reload/cleanup与独立原几何检查通过，数值未决 |2|16/0/13|
| curved reloaded block | first-subtract中间产品与reload后最终Boolean分列真实PK检查，数值/来源缺失保留 |2|17/0/26|

八份Capture均captureErrors空，缺失未补造。NURBS和curved从同参数独立构造输入；shortedge不能按默认字符串冒称GM独立输入。curved tracking/roles缺失保持；显示mesh是来自实际GM产品XT的私有PK snapshot，不能继承任意曲面正确性或性能资格。

准确mirror8的PK seed SHA为 `262963ce0b899972272970ace0eff5da8b9161dbf007bd01f20a9d3037a8d1a2`，GM fresh input为 `10b9eaeaf01d1c0e5cc1b1655fa3884844d026b90e25713ae4031139183c0c54`，与统一原修复case字节相同。实际最终产品按本次hash列证，不冒称与q2产品或旧失败相同。

最终完整Mac套件为**2000执行、1998pass、2fail、0skip，原exit1，7m27s**。两失败是`NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative`及`SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled`，均line155 Success→NotConverged。未改测试预期。已用同测试DLL/PDB、同schema在未改统一Native133 Mac基线窄复现相同两失败；这不是候选新增回退证据，也不能将Linux1974原0推广为Mac全绿。

第一次a289全套在864pass后testhost Stack overflow/原1，与未改Native133在同case窄复现；含Fixtures修正后的真实对照原件保留。活跃工作区修复后原case及21项相关控制原0，最终完整2000不再中止。第一次864不是完整通过，原日志/exit/TRX不覆盖。最终三DLL及三PDB前后SHA相同。

## 实际制品与源码

八次最终运行由当前producer记录实际loaded Native：MVID `7b78b001-a705-4e34-9e8f-ec9c439fe3ed`，DLL SHA `b7fd887d3367d7e8830dc45751f5ca1237e1bb237e5e4532b6cd5f4337209180`；每次归档的PE/PDB链接与334份tracked Native文档checksum一致。没有额外start/end observer，不能扩张这个见证层级。

完整测试Native MVID `eef8225b-e9cf-4448-9a6e-a5d0da430674`、DLL SHA `6f7821235b9a6c1fb93cdcddaa60547de05d5bc298b46bec0f28061f20d3eb01`；测试DLL MVID `98ce53b8-663a-47ca-bb24-5d547ba34bff`、SHA `d56311d11553107aac2aaced2398088d829bfd748e66545e5146d24a97fee03b`。PE/PDB分别核334 Native/164 tests源；generated/unmapped文档另列。测试制品是sealed文件及前后清单证据，没有额外测试进程loader见证，不与普通Native混为同一binary。

结构/原exit收据为 `docs/unified-performance/quality-receipt.json`，实际PE/PDB/source绑定为 `docs/unified-performance/source-binary-binding.json`；核验器成功只签字节/来源一致，不改建模原2或test原1。

## 现有开发观察与最终窗口边界

**尚无1654完整正式性能倍率。** 栈修复之前a289与固定84c只作一次std开发对照，同当前runner/helper，串行baseline/candidate/candidate/baseline，每进程6fresh-input pairs，rep0冷、其余五暖全部保留。24对API均0、四原进程各2/Unresolved且当前门保持：

|进程|来源|冷GM ms|五暖GM ms|暖中位数ms|
|---|---|---:|---|---:|
|0|84c|1447.6907|624.0417,608.0812,615.7199,598.6509,597.9440|608.0812|
|1|a289|1452.6970|782.1845,568.9873,578.0012,529.2547,510.3781|568.9873|
|2|a289|1426.0764|609.0623,548.0574,482.3396,513.4381,518.8765|518.8765|
|3|84c|1925.1567|829.2430,661.7981,627.9804,600.4774,579.1598|627.9804|

10暖/source中位611.90055→538.6561ms，观察−11.970%，两个进程中位方向都较低。不能把它挂到1654、分拆给各机制、签数值全Qualified或与217fc旧窗相乘。std root22371/roots3518/SSI4956/winding196/budget0保持，新点Jac计入18951，统计方向数不当CPU单位。冷样本与JIT/系统波动原样保留，不签独占CPU/温控。

下一步仅对这个稳定完整候选集中主要算法/集成交界独审，再按固定84c baseline/共同当前runner和质量三态作必要一次同机五操作正式窗口。物性Unresolved不改成几何失败，也不退旧point-only；新窗口性能资格由分层审查决定，不因runner的performanceQualified=false永远禁止记录，但也不能自行变绿。

## 原始证据

`temp_docs/unified-performance/` 保留final active-quality、active-* family、两次完整套件及前后制品、初始/修正测试、baseline-comparisons、probe62bf、a289开发对照、所有command/source/exit/XT/Capture与scripts。`SourceIdentity`的原source与绑定不重标。探针`ROOTFAIL `碰到runner的`FAIL `字符串启发式，故其派生quality=Failed单列，不作为几何Rejected；探针未进入最终Native。

原同字节Mac基线对照第一次缺schema得到970，stack对照第一次缺Fixtures得到DirectoryNotFound，均按环境未就绪保留，不签成算法结果。修正fixture反射重载绑定的初次raw1及源码/测试DLL/PDB也保留。历史217fc/acf7/5392/34ffe和两未选分支原件不改。
