# 6616 原生算法优化独立复核

复核对象是 `270e2b6814afe5a48cd0532e89531f6fb80011be` 到
`6616cd60a55e50a5d8a5ada7274895c44609676b` 的整个改动，Native 子树为
`5b4c6104c600b0194bb543e2dc11efa89e2f0f1f`。本分支起点为
`7aea3ee6908f91313452383d41be07e2c329879d`，不修改作者实现或测试期望。

## 取证检查点

已实读本机用户级 AGENTS、项目 AGENTS、两份计算/局部建模架构、完整生产源码差异、
作者说明与质量 receipt。用户级文件整体 SHA256 实测为
`2b86152fc017cb1cac46838e479cfe314e7bc46254a7b1fdd08bb7207fb82280`，
与派发注明的 managed policy SHA 不同；此处只证明实际读取，不声称运行时自动加载。
私有依赖准备返回 `ENV_READY: profile=native pk=38.0 platform=linux-x64`。

原包 `temp_docs/incoming/mac-algorithm-6616-review.zip` 的 2,399,500 字节、
SHA256 `ca17564e0e5f59186b93841ac8e7ab91a77036d5d3ac0c3b37b7c0f8bfd3ca81`、
205 成员及 19,454,183 展开字节均已独立核实。检查 rooted path、父目录逃逸和符号链接后，
只解至新的 `temp_docs/review-6616/original-mac/`；没有覆写原始证据。
成员长度及 hash 清单另存 `temp_docs/review-6616/archive-manifest.tsv`。

派生的 [独立证据回执](independent-6616-evidence.json) 核验原 TRX/log hash、
原始退出文件、计数、两项失败消息，并从两个固定 Git 版本重新读取测试源文件核 SHA。
候选结果仍为 1,914/1,912/2/0、原始退出码 1；270 基线只运行那两项，结果 2/0/2、
原始退出码 1。相同测试原字节与失败消息支持这两项为整合基线上已有失败，不能签成全绿，
也不能推定基线完整套件结果。独立检查器退出 0 不覆盖这两个原始退出码。

按追踪快照中 64 字节布局独立逐记录读取真实 key，得到 209,158 请求、22,157 不同 key、
187,001 重复、一个 scope；排除 use 字段和 padding，保留 scope、tag、generation、sense、
root、kind 和四个 double 原始位。三份成功操作 capture 无 captureErrors 或失败检查，
相应原操作 exit 均为 0；归档 XT 保持私有，不提交正文。

源码/数学最终结论及必要的本机动态复核将在本页后续追加；本检查点只接受以上取证事实，
不签正式性能倍率、不集成、不发布。
