# Projection cc47871 集中独立审查

2026-11-12，q2 Codex/GPT 独立树。**accepted：固定 cc47871 的位置包围优化及实际集成、限定的原生几何／拓扑和生命周期质量，以及唯一 Mac 窗口的描述性观测；未发现新增算法阻断。原合同下全部数值认证仍 blocked，普遍／因果性能收益及整体 PK 目标仍 unproven。** 本报告不签 main 集成、看板 UI 或上线。

## 对象与裁决

| 对象 | 固定身份与结论 |
| --- | --- |
| 独审起点／文档头 | `553b2c6c01d3862411f3b6a03735dc22ee20dc11` |
| 计算及实际 Mac 窗口源 | `cc47871133fb7d536ffa7fd050066c51f4905708` |
| Native／src 树 | `fe3fa0378ef6d9c0dd971de4c993a289cdb8a34b`／`31be8f6c46766bb3b60b0e402bfc043426ed58fb` |
| 比较基线 | `17eedbfd6be891db8c43494cd91a896d261242c4`，已审 Joint185／Native116 |
| 算法及关键集成 | **accepted**：在已准备、次数 0..16 的原有 Bezier patch 合同和实际调用链内 |
| 完整套件原件 | **accepted**：Mac 2022/2022，0 fail/0 skip，原 build0/test0，自然结束 4m44s；原定向 132/132、exit0 |
| 八组质量 | **accepted，限原执行的几何／拓扑及各步生命周期**；七组 raw2、一组 raw0，Unresolved／missing 保留 |
| 唯一 Mac 测量 | **accepted，限固定输入、源、制品和机器的完整 API 描述性观测**；原 runner2、performanceQualified=false 保留 |
| 原合同下全数值 Verified／原 runner 的完整性能资格 | **blocked**：原物性仍有 Unresolved，不能以审计器 0 补签；这不等于模型 Different |
| 普遍／因果收益、单机制贡献、任意模型能力、整体追平 PK | **unproven** |
| 可交整体看板的数据 | **accepted**：实际源、真实模型及派生显示、原三态、所有冷暖样本和 planar 回退；不增加看板发布审查门 |

复用固定 `e812b7a003e1bebf0699afab5e9c212023220e99` 的 `docs/reviews/advanced-modeling/joint-1850534-independent-review.md`，以及其引用的 `2f8aea492d21f39269e514d9bcd3d8e792e3903b` 的 `docs/reviews/advanced-modeling/unified-performance-1654-independent-review.md`。本次不重新微审已接受的 Joint／Krawczyk／CHART 数学；675 稳定盒、1762 多项式、cut950 等未保留实验不在本候选中。

## 实际读取与原件核验

实际读取用户级 AGENTS、本树 `AGENTS.md`、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`，以及 17eed→cc47871 全部四文件 diff 和实际消费者。两架构及 AGENTS 无 diff；生产只改 `BezierSurfaceBounds.cs` 和 `BSplineProjection.cs`，新增 `BezierPositionBoundsTests.cs`，原测试期望未改，没有 C ABI 变化。

用户全局 marker 正文含尾换行的 SHA256 实际核为 `55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1`；连同两条 marker 的散列为 `1a9db34c465b02dd8a87a6671514e51baabb9d4da96a7780d03d6158932f6e23`，二者不是同一对象。这是文件核验，不冒称已验证模型运行时自动加载。统一私有环境 ensure 实际返回 ENV_READY：Linux x64、PK38.0、PKToy `d820719b6d8510d623972ab9d8efc65edbc312f9`；随后真实 PK 动态确认版本 38.0.150。

| 原 ZIP（均在 `temp_docs/incoming/`） | 独立核验 |
| --- | --- |
| `mac-projection-cc47871-quality.zip` | 20,724,821B；SHA256 `c1f1b90ff6f8eed87d23cb960b16e3502eab77211f0d1d13bdce7f14812702b8`；543 成员／展开 79,641,496B |
| `mac-projection-cc47871-controlled-original.zip` | 33,945,233B；SHA256 `9bfad64ff79211c4fbc6620e3a2678e3f966e8150d5b3dad778e5814c8128919`；1037 成员／展开 137,327,178B |

两包均独立检查相对路径、无越界／重复／symlink，逐成员 hash 核隔离展开文件。质量展开在 `temp_docs/independent-projection-cc47871/original/`，测量在 `temp_docs/independent-projection-cc47871/measurement/original/`，原件未修改。包内四份质量公开文件、五份测量公开文件逐字核固定文档头；包内 `package-versus-accepted.diff` 对固定 `src/scripts/tests` diff 逐字相同，SHA256 `3902134d459299dea28eb6cd5a8f10e5ec5836a6765839bf8ab8232496499fd0`。70 份接受源采样复制件逐长度／hash 核原清单；采样线程墙钟不等于排他 CPU 或可删除耗时。

质量包 10 组 PE/portable-PDB 链接、MVID、文档 checksum 对固定源核实：普通 Native 与 testhooks Native 各 335 tracked 文档，Tests167；三个 source manifest 共1563行逐 Git 核。三个测试 DLL 和三个 PDB 前后清单与封存字节相符。20 个测量进程的 Native、Oracle 共40组 PE/PDB、10,420行源清单逐一核实；两边 Native 都335 tracked，Oracle 各6个 tracked 脚本，generated／unmapped 单列。

| 制品 | DLL SHA256／MVID |
| --- | --- |
| Mac 普通质量及窗口候选 Native（同字节） | `d82196e1741424219b69b40688a0b599bc48fce11e69db6a39550d75f05eb03a`／`c1240871-d64d-4518-8a49-f0d533b267a6` |
| Mac testhooks Native | `d9d6fe5666ae49efe500fad70d841e70bd8b26d93308834407e4e6512a244ccd`／`0fe9e9d6-a403-487b-8d71-34d2a324be52` |
| Mac Tests | `112b33f3eca994c56f5bfec83a0ef0193c8f38fe44ff2c7e0cda6efdf92a56bc`／`04da11ba-7d93-4f39-b6d8-d33225fd649d` |
| Mac 窗口 baseline Native | `d45d733bf505b55c8af9b25c8e06fa8bdd8510822935ed61f9928529bc1d63c8`／`fc27d3ef-eb64-4d02-a5f6-4fcb3a96a1fd` |
| q2 本次真实控制及 fresh NURBS Native | `2b66dd63598552cc5cfe0149e775559ee35352b4607ce394a724125d4e060493`／`9e1f632c-0dc2-4c5a-980e-a0c30ee38c21` |

普通 producer 的实际 loaded 声明与封存字节，和测试 sealed 文件／前后清单是不同见证层；未补造测试进程 loader 或 start/end 身份。Mac DLL/PDB 只作只读元数据解析，没有加载它们冒充 q2 执行。

## 算法与集成判断

`BoundPosition` 两 overload 使用原 `Restrict`。旧 `Bound` 的权重 hull 下界，正是同序遍历所有受限控制点权重的 minimum；新代码的 `weightLower` 与之相同。严格正权检查后，两者对每个 Cartesian 坐标都按同一顺序执行 `poles[d]/poles[3]`、随后逐点 interval division 和 Hull。省去的齐次坐标 hull、hu/hv 和导数商式没有参与这三个位置输出；原外舍入运算与顺序未变。

保守性来自同一个数学定义：Restrict 用 blossom 将原齐次 Bezier 多项式限制／重参数到 box。对新盒的局部参数 [0,1]²，Bernstein 权非负且和为1；严格正的受限权重使有理面成为 Cartesian 控制点的正归一化组合。interval 包含实际受限控制点，逐 Cartesian 商的 hull 因而包含面。零宽盒、边界和允许的有限数学延伸沿用原 Restrict；延伸后若不能证明受限权重严格正，返回失败，没有将外部参数转成另一模型或新 gauge。

`Closest` 三处效果分别是 box 的位置下界、同 box 受限网复用，以及 line-search 候选的 UpperDistance。BoxDistance 仍对三坐标 interval 求向下距离；support-plane 仍以向上 norm 再向内缩放，确保方向 norm≤1，并用全部有效 Cartesian 控制点投影的上端给出距离下界。新代码只复用刚刚计算出的同一网，不替换原证明或剪枝准则。

`Improve` 有独立的九项 `values` 及 Bound/BoundPosition 栈工作区，仅通过 `ref best` 写回候选；不能覆盖外层 `poles`。外层 poles 与 patch 的只读源、bounds、pending 都是不同栈存储。support-plane 只遍历 `4*(p+1)*(q+1)` 活动前缀，不读尾部；BoundPosition 失败立即退出 Closest，line-search 失败立即 break，不消费失败的部分输出。本次不授权 input/output/workspace 任意重叠。

line-search 只读取刚更新的前三项计算 UpperDistance；下一次真正 Newton 迭代先完整 `Bound` 重写九项，再读 du/dv，不读取上次 trial 的旧导数。完整 `Bound` 原本也没有以导数全有限证明 regularity；因此按位置调用不删除已有 regularity 门。独立极陡 patch 控制显示，位置有限、原导数计算溢出时，原 Bound 仍 Success 且导数非有限：该行为被如实记录，没有把位置成功解释成曲面正则性认证。

200000 累计单元、pending128、5 seeds／18 Newton iterations／12 trials、分割、原 gap／tolerance 停止门、nearest、finite 和 CHART 都未改。`NestedSplineEvaluation.Closest` 后的 Refine／NumericJets、DistanceAtFoot 与实际九项 Newton 路径保持；公开 UV／曲线反求、GeometryDistance、局部距离／角点清隙等仍走原成功／失败合同。没有新缓存、模型、实体拥有关系或跨调用状态。

独立控制覆盖 rational／非 rational 的每对 U/V 次数0..16，共578对、2312盒：两新调用形式与原前三项逐位相同，保留网与独立 Restrict 逐位相同，活动前缀和输出尾哨兵保持。另用与生产 interval 算法无关的 BigInteger 整数 Bernstein 和精确 double 有理比较，检查62,424个坐标包含关系；这些有限样例补充上述数学论证，不冒充任意曲面的全等证明。短输出／不足 workspace、反向盒／退化原域、零／负权、坐标商溢出、原导数溢出分层、平面内部／边／角三个已知全局最近点通过；两 overload 预热重复100次实测0托管分配。

## 原始质量与本机验证

| Mac 当前组 | 原 pass／fail／missing | 原 exit 与范围 |
| --- | --- | --- |
| curved 单操作 | 14／0／13 | 2，原 roles 缺失与数值 U 保留 |
| NURBS 候选开发原执行 | 96／0／78 | 2，六 fresh pair；不是重新补跑一个 pair |
| standard shortedge Fix | 35／0／17 | 2，PKseed→GMreceive→GMFix |
| rigid＋scale.125 | 51／0／1 | 0，仍有一项 missing |
| exact mirror8 | 29／0／23 | 2 |
| shortedge 双产品 lifecycle | 133／0／77 | 2，各步独立记账 |
| NURBS lifecycle | 16／0／13 | 2 |
| curved editable reload | 17／0／26 | 2，原 subtract／reload 后 Boolean 分列 |

八组 captureErrors 全空、fail0。逐原 Capture、45份 strict compare、24份 mass 及实际 exit 核验；质量收据的 `massStatusOccurrences` 含报告级和分量级出现次数，不当作唯一物性分量总数。NURBS 六 pair 的最终同名成品是 **rep5**，不能放入正式三 pair 窗口并标 rep2。copy 后删源、镜像缩放、两次 Boolean、自 XT 可编辑 reload 与 cleanup 按原步骤保留；下游失败不反向抹掉已独立通过的上游 Blend。

实际使用 `parasolid-research` 技能，经 Parasolid MCP 定点查询 `PK_BSURF_create`、`PK_SURF_eval`、`PK_SURF_parameterise_vector`、`PK_PART_receive_b` 与 `PK_DEBUG_BODY_compare`，核标准形、原生 UV／导数请求、内存 receive 原型及当前比较 helper；未公开反编译正文，未将 Linux 静态地址用于 Mac。

q2 必要定向测试29/29、raw0，包含新位置界、全局最近点／UV离面拒绝、距离消费者及两个 SupportingBlend 控制。只清理本次 setsid 的自有 PGID；测试结束时该组已自然消失，kill 返回1／No such process，不影响 test 原0；未执行全局 pkill。

本机真实 PK38.0.150 对三对实际封存产品重新 receive、full BODY_check 和 `AssertEquivalent`：curved 单操作、NURBS 开发 rep5、shortedge lifecycle step4，均通过当前 strict mask，只有既定 n_cht_pts 诊断，原 face/entity 差异明细保留。没有把 face_pairs 当成功面总数。另实际运行一次当前普通托管源的 fresh NURBS 完整 API／transmit／真实 PK compare 消费者：原 runner2／16 pass／13 missing／captureErrors空；这只补充当前 Linux 执行与质量状态，不是 Mac 时间样本。

## 唯一 Mac 窗口

UTC **2026-11-12T04:56:39.7493270Z–04:58:37.0945730Z**，M4／macOS27.2 arm64、SDK10.0.400／runtime10.0.11、PK38.0.150。十一份 helper/runner 在两源逐字相同；十个 managed 预构建原0。核实原 runner 对完整 API 调用直接计时，显示、对照、积分在计时外；没有删掉建模内部的证明步骤。

五操作各串行 ABBA，共20进程×3 fresh pairs＝60对／120次 API，全部 API error0。每 source/op 有2个独列冷点（rep0）和4个暖点（rep1/2）；四个 planar 进程原0，另外16个原2，主 runner2、performanceQualified=false。分析器、打包器或审计器0不代签建模。

| 操作 | 基线暖 GM 中位 ms | 候选暖 GM 中位 ms | 变化 | 候选暖 min–max ms | paired GM/PK 中位 | GM中位／PK中位 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| sphere | .08265 | .07800 | −5.626% | .0757–.0878 | .26043 | .25815 |
| planar | .55835 | .99445 | **+78.105%** | .9455–1.0684 | .87549 | .87000 |
| curved | 40.50215 | 38.84930 | −4.081% | 38.7392–40.0157 | 18.22905 | 18.22884 |
| NURBS | 597.19760 | 500.76395 | −16.148% | 446.9253–621.0768 | 253.84728 | 244.25723 |
| shortedge | 646.96075 | 548.47140 | −15.223% | 525.3929–733.4724 | 331.97129 | 332.04468 |

全部冷暖 GM／PK 点、min/max、逐配对 ratio、中位数及 ratio-of-medians 从原 Capture 独立复算，完整值在 `docs/projection-independent/receipt.json`。保留 planar 明显回退，不按绝对时间小删除、不归因、不为不利结果重跑；保留 NURBS／shortedge 波动。与前窗或开发 B/A 的倍率不相乘，四暖小样本不签普遍／统计因果或单机制收益。

逐20进程、60 rep 的 PROFILE／WORK、96条 ROOT_WORK 原行、行号、字段及 Capture/API 时间独立复核，全部对应原 log/hash；没有用 rep0 覆盖暖 rep。相同原 SSI／root／winding／budget 字段保持；inclusive 或采样墙钟、调用次数不能充当排他 CPU 或可删工作。

原72份唯一物性报告共1080分量：60主报告900分量为312 Verified／588 Unresolved，12份 native-via-managed 回读控制180分量为84 Verified／96 Unresolved，合计396 Verified／684 Unresolved／0 Different。按原估计值和 modulus 重新计算外包围 normalized lower/upper 及三态，原1e-7合同事实保持；不按15个 check 对同报告重复计数。本 review 不决定门槛永远不能调整，也不代签另一计算任务尚在研究的新标准或传播模型。

20份原观察 load3.72–4.30；powermetrics 原失败、pmset 状态及无数值温度的边界保留，没有独占 CPU 或恒温资格。未登录 Mac 构建／重测，未运行 Windows、NativeAOT、额外完整套件、八 family 或新 ABBA。

## 可交整体看板的数据边界

312个实际 mesh／35,389,380B 及64个最终 XT 逐 hash 绑定。原数据入口为 `temp_docs/independent-projection-cc47871/measurement/original/temp_docs/projection-controlled/`，原报告 SHA256 `6568f1d9d19bbf0df0f9eeb07dc2afac1f5990599535b2f5caa0dbaf40d9e90e`，`material-binding-current.json` SHA256 `3678da6d62f44e00e8e6419af0709b25d73c43f3cfdd50a0a8c7c75e916d0306`。本机来源复核另存 `docs/projection-independent/material-bindings.json`。

64个最终文件的逐 path／source／hash／角色在独审 receipt：20 GM成品、16 PK reference成品、4 PK成品经GM回读导出、8 GM before输入、8 PK before输入、4 PKseed、4 GM received fresh输入。它们不是64份 rep2独立产品；同名成品按原 producer 覆盖只留 rep2，公共输入／seed 不虚构 rep 编号。NURBS 的 PK独立产品仍在逐 rep Capture，不补造额外 products 文件。

GM live tag、其私有真实 PK snapshot tag、独立 PK reference 产品分开；GM网格由本次真实 GM XT 的 PK snapshot 派生。显示 XT 与普通 transmit 可有不同版本／序列化，不强求字节相同；网格近似不继承几何、数值或性能资格。保留 shortedge 的 PKseed来源、curved 原 role/tracking缺失，以及没有 sourceBinding 的实际报告；不以猜测数据或固定 tag 补缺失。

另逐312 mesh核 captureIdentity、source live tag、snapshot tag和实际 source XT hash；60份 same-instance-snapshot-pair 证明按原 step 的 gmMesh／pkMesh 链接，144份 GM-fresh-vs-native 证明核其实际 pair/tag与身份。后者含24份 shortedge “native via managed”额外控制：它们来自 PK成品经另一个 GM body 回读再导出，原 producer 没有为该 body 记录 display step，不能误绑成主 GM产品 mesh；另外12份 proof 原 sourceBinding 缺失仍保留。

看板可使用上述实际 source／operation／step 和原三态、完整冷暖值、planar 回退；本报告没有审 UI、发布网站或设定新的上线批准流程。并行 q2 查询修复4382d23、510、物性移动源尚未合入此候选，不在本签收范围；整体 PK目标尚未完成。

## 可复核交付与退出

新增仅三份 C#/.NET10 审计脚本、本报告及派生 JSON；路径按脚本自身解析，真实 PK 复用固定 PskernelSharp／Using／ParasolidScriptHost，没有手写 P/Invoke、session样板或将 psKernel重定向为 GM。生产与测试期望、原包与原报告未改，无历史兼容层。

`scripts/ReviewProjectionEvidence.cs` 核包／质量、运行必要定向与生成汇总；`scripts/ReviewProjectionMeasurement.cs` 核唯一窗口／原数值合同／模型绑定；`scripts/ReviewProjectionControls.cs` 做独立数学控制及三份代表性 sealed产品验证。均使用 `MSBUILDDISABLENODEREUSE=1`，不触发 NativeAOT发布。

复核输出和逐成员完整清单留 `temp_docs/independent-projection-cc47871/`；原审计 log 留 `temp_docs/projection-review-*.log`。evidence两次、measurement两次、数学／PK控制、最终汇总与最终来源绑定核验成功执行原0；当前 fresh消费者原2单列。首次汇总器因 C#局部变量重名编译失败原1，修正后成功，失败日志 `temp_docs/projection-review-receipt-01.log` 保留。来源核验前两次对额外 native-via-managed 控制错误要求存在主 display mesh，均原134；根据实际 ShortEdgeBlendOracle 回读路径分开后核验原0，三次日志均留 `temp_docs/projection-review-material-bindings-*.log`。这些是审计器问题，不是模型失败，也未改原件补签。

第一笔审计脚本提交 `46f4928b173d1668b3eb803a56c6595e62961138` 已推到 `review/native-projection-cc47871`。最终完整报告及收据的提交／远端 SHA 由完成消息给出；本次交付不代表已合 main或上线，没有需要作者分字段逐轮重审的新增阻断。
