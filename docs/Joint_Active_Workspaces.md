# Joint 求解器活跃工作区与Mac栈溢出

后续最终1654全套已实际完成2000/1998/2/0、原exit1，原host crash未再出现；两NotConverged与Mac固定基线对照保持。完整实体复验及制品见 `docs/Unified_Native_Performance_Package.md`；下文初始864中止原件不覆盖。

统一整包最终全套第一次在a289源实际完成864pass后testhost Stack overflow，原test/driver exit1、TRX outcome Failed。失败栈为RollingNetworkTests.ProjectedRollingCellFailureRestoresTargetAndBothToolsAfterUnion→Boolean→ClipRegularFacePair→TryStationaryNativePlaneEnd→BoundSharedSpineWitness→BoundSegment；不能用末行864pass补签完整2000。

固定未改统一Native133在Mac也于同一测试窄复现stack overflow；candidate与baseline使用同一份测试DLL/PDB、显式同ENV_READY schema及同Fixtures字节。原同字节app比较的第一次缺Fixtures目录，只得到DirectoryNotFound环境失败，已单列未用于算法归因；补齐白名单Fixture后两边原exit1/host crash。原件在 `../gm-pk-performance-delivery/temp_docs/pk-native-algorithm-work/unified-stack-ready-*`。

## 修改依据与不变量

EvaluateCore构造occurrences时原有MaxOccurrences16/MaxVariables63上限检查保留，完成后已知n和实际nodes.Length。BoundSegment也从成功的EvaluateCore counts取得同一已验证维数。此前仍为许多后续矩阵分配63×63。

现在仅按活跃prefix分配：

- EvaluateCore的Taylor variables/residual、double matrix/rhs/residual/pivots；
- point nearest的map、selected、block/rhs/pivots；
- BoundSegment的fixedCharts、state/point Jacobian/inverse/rhs/residual/pivots、boxes/image/centerF、interval Jacobian/remainder、dual variables/equations、UV contact cache与record outputs；
- interval nearest的map、selected与block/rhs。

输入准备期尚不知道n的state/candidate、occurrences、snapshot仍保留原上限容量。记录DAG容量、所有原迭代/预算/容差、原七变量/UV索引与初始化保持；没有SkipLocalsInit、heap代管、新缓存或线程栈/OS资源上限调整。所有运算循环仍是原n/节点范围，未知量、矩阵顺序及proof没有减少。

仅BoundSegment的两double+两IntervalNumber稠密矩阵，n7时由190512B变2352B，是静态空间差异，不能直接当CPU收益。关键验收是原host crash实际变为完整测试通过，而不是文件/诊断数量。

## 已执行与下一步

修订后原栈溢出case与初始化、Conic、spline-cache相关检查共21项原exit0，含上述完整Union失败原子回滚控制。日志/TRX/exit在 `temp_docs/unified-performance/active-joint-workspaces.*`。未改该case期望或拓扑判据。

修订前完整原件为 `temp_docs/unified-performance/unified-bundle-full-*`，普通/test Native身份分开，三DLL/PDB前后清单及私有原字节保留。修订后的最终全套和当前PK实体质量闭环随后执行；其结果不能继承a289前一次开发对照−11.97%或先前scalar/Conic通过状态。

本修复属于已定位求解器工作区/完整性能目标，未接管无关功能；Linux统一1974原0、Mac先前两SupportingBlend NotConverged及baseline对照、所有numericalUnresolved/raw2均按实际执行范围分开保留。
