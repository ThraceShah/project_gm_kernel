# 不保留Cut planning950性能方向

本实验不进入下一正式优化候选：完整NURBS暖中位收益只有2.602%，四暖样本波动较大，同时curved观察回退22.897%。它没有证明值得保留的完整建模收益。2023/2023质量通过和NURBS工作量减少仍是原事实，但不代替时间收益；没有为弱信号重跑窗口、扩大缓存/生命周期或追加微审。下一开发仍从已接受Joint185/Native116、17eedbf出发，不沿用本实验或恢复其依赖方案。

固定计算/测量源为 `950e36252d21bbeb641c818784c45b4fb6bf8392`、Native `a3a74c043763bfb0ff347f4dc3600d2c18fb1631`；基线固定17eedbf/Native116。实验阶段、修改边界和完整质量原件见 `docs/Cut_Planning_Proof_Phases.md`、`docs/Cut_Planning_Quality_Receipt.md`。这些文件描述已执行实验，不代表独立算法接受或正式集成。

唯一窗口UTC2026-11-12T03:01:47.4182710Z–03:03:45.1383680Z；Mac M4/macOS27.2/arm64、SDK10.0.400/runtime10.0.11、真实PK38.0.150。先十次managed-only预构建原0，十一份当前runner/helper逐字相同。五操作串行ABBA，每进程3fresh GM/PK对，rep0冷、rep1/2暖；20进程、60对、120次API均原0。planar四进程raw0，其他十六进程raw2，runner2、performanceQualified=false保持。无NativeAOT、Windows或新预算。

| 操作 | 基线暖GM中位ms | 实验暖GM中位ms | 观察变化 | 实验暖min–max ms | 实验paired GM/PK中位 |
|---|---:|---:|---:|---:|---:|
|sphere|.08355|.08535|+2.154%|.0715–.1235|.31506×|
|planar|.62010|.54715|−11.764%|.5237–.5641|.69701×|
|curved|37.35170|45.90420|+22.897%|37.5411–54.1528|22.86683×|
|NURBS|566.82260|552.07205|−2.602%|518.0432–641.7021|350.20775×|
|shortedge|643.69070|630.54875|−2.042%|523.5537–722.9650|373.67397×|

每source/op的全部四暖、两个冷、min/max、逐配对比和每进程/rep的原PROFILE/WORK/ROOT_WORK字段、原行/行号及日志SHA保留于 `docs/cut-controlled/operation-summary.json` 和 `temp_docs/cut-controlled/`。不以rep0覆盖暖rep，不以Jac减少推断CPU成本，也不把表中变化解释为单机制因果或与上一窗口倍率相乘。是否存在JIT/layout或机器负载影响尚未判定，不以猜测抹掉本次不利结果。

20个实际loaded身份、原source.sha256清单与Native/Oracle DLL/PDB核同，Native两边各335 tracked源checksum及PE/PDB链接核实；实际加载、封存文件与后来派生检查分层。机器绑定见 `docs/cut-controlled/source-binary-binding.json`。312实际mesh/35,389,380B、64最终XT文件逐hash绑定；产品覆盖保留rep2，公共input/seed不虚构rep编号或来源角色。原raw2/Unresolved/missing没有洗绿。

逐进程load4.62–5.81；保留pmset与powermetrics原失败，无数值温度或独占CPU/恒温资格。原report SHA为 `baa16e77c42d3e3ec271536b28d5c8816a7d75c5e1644efa08050bb3eea45c56`，material-binding SHA为 `538d268f424b2426c0195a6473d9d047c909ad32a0978311ea21b3d0ebe72bc9`。

原套件2023/2023和八组质量保持，已接受185/e139/17eedbf与两个原包保持原字节。该实验未请求新的独审阶段、未合main或发布；整体PK目标仍未完成。下一方向先针对被接受源的完整NURBS执行定位真正主成本，而非继续扩大纯阶段复用或重复这一弱信号。
