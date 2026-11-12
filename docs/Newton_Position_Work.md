# 单次Newton求解中承接成功位置包围

独立实验基线为已限定接受cc47871/文档553b2c6/Nativefe3fa（独审7b20d7f）。实际AGENTS和两架构已读/核同，初始src/scripts/tests无diff；不混入q2 moving源、675/1762/cut950及其他已拒实验。固定接受源及两原包保持不变。

当前接受源真实未改动Native/Oracle采样记录在同级诊断树的 `docs/Accepted_Projection_Cost_Diagnostic.md`。NURBS所选线程采样墙钟约34.34%处于Projection，不签排他CPU或可删预算。shortedge没有该栈，不能拿本机制解释上一窗shortedge变化。对EndBound大栈另作既有诊断开关完整API对照，不能据此签大计时器成本或删除观察器而称算法收益。

原line-search成功后已用BoundPosition生成目标UV点的保守位置三项；下一Newton头再次用同patch、同UV完整Bound重算这三项。现在在同一Improve/同一seed迭代内承接成功位置及原受限权重hull，仅执行原齐次du/dv hull和原导数商式，完整更新九项。新的weight由同Restrict输出、同遍历顺序Hull各权重，与原Bound的h[3]逐位相同；不改变有理模型、规范、native参数或舍入。

位置/权重仅在UpperDistance接受并将nextUV原值赋给UV后生效，下个Newton头先完整完成导数，再消费九项。每seed重置，失败立即break，最后一次trial的未消费结果不会进入另一seed；无跨call/owner缓存、无旧失败复用。patch只读，调用链是原纯Bezier span算子及UpperDistance，无几何创建/存储改写/回调。BoundFromPosition仅接受本次成功同patch/box证据，允许position与输出前三项直接alias；不授权任意网/源重叠。缓冲区不跨函数生存，无新增native/managed分配或长期scratch。

所有5seed/18Newton/12trial、Lower/UpperDistance、global support-plane/gap、pending128/200000预算及后续Refine/regularity/nearest/CHART保持。位置包围有限不被解释成导数有限或全曲面正则性；旧Bound允许的导数溢出状态不新增放宽/收紧。C ABI与Native模型不改，无AOT/Windows验证。

定向132/132原0（27s）；原位置网/全局最近点/UV离面拒绝/距离/圆角消费者期望保留。新增在原degree0/1/2/4/8/16双维、rational/非rational、点/边界/宽盒/有限数学延伸控制中核：位置+weight承接得到的完整九项对原Bound每端点binary64逐位一致，alias使用通过，资源/非正权显式失败与两原/新路径零分配保留。原日志/TRX在 `temp_docs/newton-position/`。

结论更新：完整NURBS开发对照暖中位观察+15.288%回退，本实现停止、不保留；详情与原证据路径见 `docs/Rejected_Newton_Position_Work.md`。下面保留该实验当时的关键数学/实现假设说明，不表示当前候选接受或后续研发基线。

这是关键数学/实现假设已核的小步源码，未取得完整API收益或独立接受。先做固定源有限完整NURBS开发对照；弱收益及时停止，不靠追加微测试、完整suite或有利重测推进。原数值U/missing/退出与性能资格分层，整体PK目标仍未完成。
