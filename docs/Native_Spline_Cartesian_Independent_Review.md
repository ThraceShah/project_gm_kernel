# Cartesian 残差包集中独审

审查对象为 `6616cd60a55e50a5d8a5ada7274895c44609676b` → `94f60da64836da33eafaea7ce5912c9d5132660e` 的全部 11 文件差异。实际 Native 检查点为 `ddd23111f873a2bf70b239372c9c36aa35c2a38c`，Native 子树 `cf417678e17e5b106f7c4734091c55c9a7b021be`；94f 的同一子树逐字相同。基线 Native 子树为 `5b4c6104c600b0194bb543e2dc11efa89e2f0f1f`。

本报告的首个提交只确认原件完整性与固定源码对应关系，算法及质量结论尚未签发。审查未修改作者 Native 源码、测试或期望，未集成、部署或重新测量性能。

已实际读取用户级政策、项目 AGENTS、`docs/Computational_Kernel_Architecture.md`、`docs/Intersection_Local_Modeling_Architecture.md`、实验说明与 receipt，并按 parasolid-research/parasolid-docs 技能查阅官方定义及实际 MCP 原型。qingyu2 的私有环境准备返回 native ENV_READY、PK38.0/linux-x64，PKToy 固定 `d820719b6d8510d623972ab9d8efc65edbc312f9`；私有依赖不入库。

原包 `temp_docs/incoming/mac-cartesian-ddd2311-quality.zip` 为 216112 字节，SHA256 `f24b72186f221d91679b80d5f30a232951de7204492c01c396ac954ae8270adb`。`scripts/ReviewCartesianArtifact.cs` 先校验所有 ZIP 路径、重复名、符号链接和目标边界，再解至全新隔离目录：75 文件、展开 1051242 字节；receipt 的 74 个文件大小及 SHA256 全部匹配。原件保留在 `temp_docs/cartesian-independent-review/original/`；receipt 自身是第 75 个文件，不是自哈希声明。

原 `cartesian-regular-frame-native-source.diff` 与固定 6616→ddd 的 Native/tests Git diff 逐字相同；原 `cartesian-regular-frame-runner-output.diff` 与固定 6616→94f 的 runner Git diff 逐字相同。源码树与声明的对应得到确认；这不证明历史进程执行的 DLL 身份，包未携带足以完成该层验证的运行中二进制身份材料，不以其他进程 PDB 补签。

原退出与 TRX 分开读取：columns-ready 编译 raw1；columns-fixed 为 28 项中 25 通过、3 失败、raw1；native-fixture 为 28/28、raw0；lifecycle 相关组为 80/80、raw0。最早 columns 日志没有外部退出文件，不能签 raw0。curved、nurbs、lifecycle、shortedge 及 operations 原外部退出均为 0。这些检查只确认原记录，未重跑 80 项或全套；6616 的 1912/1914、raw1 仍按原质量 receipt 保留。
