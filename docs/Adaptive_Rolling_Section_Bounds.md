# 小角度 section 区间权重的自适应严格余项

统一固定84c当前合同上的开发候选；与scratch及原七变量Conic分块同在 `perf/unified-conic-block`。保持原生great-circle section、q=1-cosθ、v和全部原几何/拓扑/数值质量条件，不改Series点求值、最大64/128项、参数域或根/SSI预算。

## 实际依据

临时诊断源62bf309在统一Native候选上实际执行std shortedge一次。只在GM FixBlends调用前重置线程计数，调用返回后/任何Capture与积分前读数：Weight6414次、445184项，函数内含OS停顿的墙钟91.735079ms；该诊断API1353.179ms，不能把这些elapsed当排他CPU或直接可删成本。原采样的PollGC/CPU_TIME合成叶不承担这种推断。

同次106个困难Boundary根调用的描述全部为procedural surface、原Ellipse spine、两Cylinder supports、固定u0对Plane，其中样例v域[0,.014383326102761093]、原reason Subdivision；没有凭小残差将其放行，未添加tangent/whole-edge推断。正常RootCells22371、roots3518、SSI4956/winding196/budget0保持。

探针原进程2/物性未决保留。探针字符串`ROOTFAIL `包含公共runner的`FAIL `标记，导致本次派生runner quality=Failed；这次诊断显示状态单列，不冒充几何Rejected或正式性能。先前无探针同源候选的当前runner为原2/Unresolved。临时Console/timer/counters仅在独立probe分支，未进入本生产候选。原Capture/log/exit/loaded identity及DLL/PDB在 `temp_docs/unified-performance/root-cost-probe/`，未覆盖历史。

## 同一解析级数及完整余项

原系数递推 `t_k=t_(k-1)*q*(k²-v²)/(k*(2k+1))` 保持。对已保留n项且n≥|v|，后续每一步的值倍率上界为r=q_max/2<1，倍率导数绝对值上界为

`D=|q'|/2+q_max*2|v||v'|/((n+1)*(2n+3))`。

所以完整余项包围仍为

- 值：`|t_n|*r/(1-r)`；
- 导数：`|t'_n|*r/(1-r)+|t_n|*D/(1-r)²`。

全部使用原向外区间运算，包含扩展v域。现在在n=4/8/16/32/64/128且n≥|v|时计算该原证明；只有值和导数的完整尾界均不超过其当前区间两端相邻binary64间隔的较小值时才提前结束。返回值仍明确加入±完整尾界，没有丢掉小尾项。这个停止条件使用浮点分辨率，不改变几何接受阈值。未提前收敛就继续原最大64/128并使用同一尾式，原q及v范围拒绝不变；不追加预算或旧合同回退。

## 已执行检查与待执行范围

原RollingSectionLimitTests的10项通过，覆盖解析q→0、域外section和混合导数。新增11项原raw0：decimal系数/独立q幂矩的值及耦合导数、wide参数和导数盒、q=1.5/1.9独立半角closed-form、原范围/最大预算拒绝及零托管分配。decimal采样仅为数值交叉检查，严格任意盒保证来自上述余项推导及区间运算。

新增检查首次因Reflection同名Weight重载绑定歧义导致9个fixture初始化失败，原10个极限检查通过；原raw1/TRX、初始fixture源码和测试DLL/PDB均保留。修正为明确IntervalDual/outIntervalDual签名后新增11项通过，未修改预期/容差、未重跑旧10项或全suite。日志在 `temp_docs/unified-performance/adaptive-section-*`。

完整实体及当前PKfresh/full/strict/finite/15分量物性和受影响lifecycle随后执行；尚未证明整包净收益或独立接受。本分支两项Mac SupportingBlend失败已在固定统一Native133同字节测试窄对照复现，详见 `docs/Unified_Conic_Block_Development.md`；不推广Linux1974全绿到Mac。原numericalUnresolved/raw2、tracking/roles missing保持，不退回旧point-only门，不继承217fc倍率。
