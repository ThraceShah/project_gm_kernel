# 求交与圆角拓扑交接

本目标未完成，按用户要求停止实现，交给下一模型。实施主副本是 `main` 所在项目目录，最新实现 `95e4f4a` 已推送到 `origin/main`。`intersection-baseline` 仅供历史对照。依赖保持不变：PKToy `d820719`、真实 Parasolid 38.0.150、.NET SDK 10.0.302。原始验收门槛和固定探针均保留。

## 已完成并验证

- `plane-nurbs` 自研圆角和后续 Boolean 在旧装配守卫下实际走共同有限边界、事件和 FaceArrangement；实际受影响的支持面可保留远处内环，独立等价支持面有正反证书用例。
- 默认短边从未倒圆角 seed 自研构造，保留两条真实短 ICurve；已验证严格 fresh XT 对照、完整 BODY_check、有限面域、后续切割、复制后删源、XT 重载后继续建模及 48 个材料检查。上述检查通过，默认生命周期的原始物性点值仍有四项失败，整条命令退出 134。已验证代表性输入变换和独立模型变体；未宣称所有变体的生命周期均通过。
- `.997` 额外分界已解决；保住 `.003`、`.61` 合法分割及两条短 ICurve。固定探针 row 289 的自研 5018 已消除。
- `95e4f4a` 修复了真实的联合切割构造差异。PK 联合减法先产生三角滚动片的两端，随后在 imprint 清理中去掉分界并重接三平面；PK 逐刀减则保留该片。自研现在对完整局部有限域证明投影和连续性后，经共同 arrangement 重接，未提高实体精度。两种选边顺序下，单交点和双交点切割均通过独立严格比较、完整 BODY_check、有限边界和切割后的原始物性门槛；单交点另有 12 个材料检查一致。相关托管集中测试 65/65、普通独立 Boolean 43/43、带孔 `plane-nurbs` oracle 通过。

## 未完成

- **默认圆角为何仍触发原始惯量差异。** 创建时确有区别：PK 在最终两端点之间推进并按角度等试步检查保存锚点；自研先创建较宽候选交线，再由裁剪确定有限区间。默认 PK / 自研三条 CHART 分别为 10/2/2 和 101/20/20；动态跟踪同一 `.5°` seed 的两种选边顺序，PK 长交线分别从两端推进并保存 11/6 个锚点。已检验默认三条有限交线全长与独立 PK 的偏差上界不超过 `1.44e-11`；只把自研 CHART 截到最终端点未消除失败。这些证据没有证明锚点差异就是原始点值失败的原因，也没有找到默认实体的真实材料错误。没有改验收门槛。若继续查构造差异，优先做能隔离影响的有限交线对照：固定自研支持面、拓扑和最终端点，只重建内部参数图；先验证完整有限几何，再看原始点值。该实验尚未实施。
- **固定 510 点和总回归。** 在 `95e4f4a` 上两路均已完整重跑：自研 15 处、导入 16 处，与前次差异行完全一致，零 API 错误，原生基线未变，两条命令均退出 134。已确认部分点在 PK 自身 XT 往返后也变更边界分类，其他点不能据此一概解释。旧快照 `b62f7a8` 的 96 组均自研构造及几何通过，44 组全过、52 组仅原始惯量失败；不能当成当前快照或全矩阵生命周期通过。外围 SSI oracle 的已知失败也留在验收记录。
- **最新实现全量回归未完成。** `126a571` 最近完整托管回归为 1857/1857、退出 0；包含 `95e4f4a` 实现的候选全量按交接要求中止，已报告 1243 个通过，运行器退出 0，但日志明确标为中止，不能算全量通过。

从 `main` 先按 [执行约束](topology-event-framework-execution.md) 的命令补跑全量。默认最小独立复现：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ShortEdgeBlendOracle.cs -c Release -- \
  --event-arrangement --geometry --lifecycle --transverse-local-cut \
  --material-ownership --require-native-fin-uvbox --require-native-face-domains \
  --require-native-cut-witness --require-cutter-chart-witness
```

详细证据见 [验收记录](topology-event-framework-acceptance.md)。最新本地日志在 `temp_docs/topology-event-framework/`：`m5-projected-sliver-full-candidate`、`m5-projected-sliver-material-{forward,reverse}`、`m5-95e4f4a-immutable-probes-{self,imported}`、`m5-native-fix-step-selection-{default,forward,reverse}`。换机器时用对应命令重建本地日志，不把旧结果当作新证据。
