# M1 规则自由曲面实体圆角验收

日期：2026-11-06。基线：`1b071bb`；同时合入 XT 有限边区间修复 `edb3afb`。
环境：Linux x64、.NET SDK 10.0.302、真实 Parasolid 38.0.150。
本轮没有 C ABI 变化，使用托管 Debug/Release 验证，没有运行 NativeAOT 发布。

阶段计划：`docs/Complex_Surface_Solid_Modeling_Plan.md`。
实现范围：`docs/Regular_Freeform_Solid_Fillets.md`。

## 交付结果

已有 rolling Blend 几何现已进入 `set_blend → fix_blends` 实体操作：根据材料方向
选择凸凹滚动符号，生成两条真实接触 ICurve 和两个端部圆弧，截短相邻边，裁剪
支持面及端面，装配共享 edge/fin/loop，保留 Blend 面。圆角结果可以继续参与
一次穿过 Blend 的实体相减。生产实现没有调用真实 PK。

首轮限定单条独立边、常半径、两个简单平面端部、四边单环支持面和规则非周期
滚动分支。除两个支持面外，其余面要求平面或可认证的平面 NURBS；完整圆角带
必须通过非端盖平面的区间清除检查。不能认证或预算耗尽时明确失败并回滚。
这不构成任意自由曲面实体、自覆盖修复、角点或边链的全面支持。

## 独立 PK 矩阵

脚本：`scripts/RegularSolidBlendOracle.cs`。两个内核分别从相同参数构造原体、
附着实际非平面 NURBS、选择同一几何边并以相同半径圆角。凹边使用各自内核
先相减形成的 L 形原体。NURBS 为 degree 4×2 控制网，不是仅把平面改成样条类别。

| 案例 | 半径 | 圆角后 F/E/V | 后续 x=.5 切削 F/E/V | 结果 |
| --- | ---: | --- | --- | --- |
| 凸 Plane/NURBS，隆起顶面 | 0.10 | 7/15/10 | 7/15/10 | 两步通过 |
| 凸 Plane/NURBS，下凹顶面 | 0.15 | 7/15/10 | 7/15/10 | 两步通过 |
| 凸 NURBS/NURBS | 0.10 | 7/15/10 | 7/15/10 | 两步通过 |
| 凹 Plane/NURBS | 0.10 | 9/21/14 | 9/21/14 | 两步通过 |
| 凹 NURBS/NURBS | 0.10 | 9/21/14 | 9/21/14 | 两步通过 |

每个圆角体和切削体均执行以下硬条件：

- 自研 fresh text XT，经真实 PK receive 为单个 solid；自研输出和 PK 基准分别
  运行默认完整 `PK_BODY_check`，均为零故障。
- face/edge/vertex、2 region/2 shell、材料区域及 shell 归属一致。
- `PK_DEBUG_BODY_compare` 保留数量、几何类型、sense、容差和 missing 检查。
  `global_result=21921`；只有内部 CHART 点数差异作为诊断，未关闭类型或 sense。
- 使用 PK 对两个体计算 15 项物性；各项 `abs(a-b)/(1+abs(a)) < 1e-7`。
  十个结果中的最大实际残差约 `2.59e-9`。
- 圆角几何在九个截面检查常半径、两侧真实接触位置、G1 及独立 PK 几何对应。

两边显式使用 `PK_blend_edge_update_v261_c`，保留 BlendSF、BLEND_BOUND 和接触
ICurve，才能严格比较同类表示。PK 38 的现代默认 fix 会把这些圆角转换为
BSurface；本轮不声称与现代默认的内部存储类别一致。完整局部偏差和自动 face
matching 仍不作为内部参数化完全相同的承诺。

## 生命周期与 XT 短修

独立生命周期覆盖自研 XT receive、确认非 opaque 及曲边有限区间、深复制、
删除收到的源体、镜像加二倍缩放和平移、点分类、fresh XT 再交 PK 完整检查、
严格比较及物性。结果零故障，结构一致，15 项物性最大归一差约 `7.13e-9`。
删除临时副本与 transform 后，body/curve/surface/point 池和返回内存恢复基线。
单元测试还覆盖变换后再次自研 XT receive，再分类并释放实体。

最终审查补齐了未绑定 contact payload 的分配失败清理与 block handle 失败检查。
接触交线的 UV 读取预算按实际 `2×chartCount` 分配，保留普通 ICurve 的原有限额；
257 个真实 CHART 锚点的导入回归通过。未解析的非零 UV DATA 引用明确拒绝，
不当作无 UV。接触体 receive 的 25 档故障预算核对池数量及 native block 字节恢复。

镜像曾暴露两条接触 ICurve 故障：BLEND_BOUND sense 翻转不能代替交线支持顺序
翻转。最终实现保持 BOUND 正 sense，在反射时交换序列化支持顺序、UV 和参数
误差角色；导入恢复规范化内部顺序。这项修复通过严格独立生命周期复验。

XT 短修从实际端点反求 BCurve/ICurve/TRCurve 的有限参数区间，不能复制基础曲线
完整自然域。本轮同时恢复圆弧、椭圆弧区间，处理 sense 及周期跨越。
既有曲面 Boolean oracle 增加六个保存重载后分类/继续切削案例，由 27 增为 33。
有效但未支持的闭合 Help 接触仍保留 opaque 文档，不冒充可编辑 B-rep。

## 回归与检查

| 检查 | 结果 |
| --- | --- |
| 最终 Release KernelTests | 1,414 通过，0 失败，0 跳过 |
| 最终 Release XT 库测试 | 12 通过，0 失败，0 跳过 |
| 规则曲面 Boolean 独立 oracle | 33/33，包括六个重载后连续操作 |
| NURBS/Blend sheet 独立 oracle | 79/79 |
| 既有局部操作独立 oracle | 41/41 |
| 生产 Release 接口目录 | 174 项，通过 |
| 生产 Release 核心分配 IL guard | 2,266 个方法，CLEAN |
| 新圆角热操作托管分配 | 单元测试实测 0 字节 |
| 分配故障及非法圆角带 | 六档故障预算完成或完整回滚；穿过底面的圆角带拒绝 |
| 标量 iso/plane 求根 | 双根、两端点根及不确定端点区间拒绝回归通过 |

所有 test/build 使用禁用 MSBuild 节点复用的环境设置；test 后清理 testhost。
新增根隔离保留区间证据，不以中点符号排除根，也不把未收敛当作无交。

## 性能与仍未通过的边界

最终 Release 矩阵中，自研单边 fix 约 0.52–7.29 秒，后续切削约 5.94–139.25 秒；
同次运行的 PK 操作约 1–4 毫秒。这是正确性验收的单次观测，不是稳定性能基准。
凹 NURBS/NURBS 切削仍较慢，剖析指向接触边区间求交与面域绕数。已复用整带
严格单调证书定位圆截面，余下分类成本留给 M2/M5 定向推进。

早期非对称 `x=.55` 双 NURBS 切削探针超过七分钟未完成，已停止，不计为通过。
本轮不声称一般解析刀具、Blend/Blend 并交差、多工具、布尔新生边再圆角、
周期 seam/pole、精确相切、任意角点、变半径或自交修复已经完成。

## 复现与日志

```bash
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/RegularSolidBlendOracle.cs -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false
MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:UseSharedCompilation=false
```

第二条命令执行后按 `AGENTS.md` 清理 testhost 并保留原 test 退出状态。
脚本支持 `-- plane-nurbs lifecycle-only` 单独复验镜像生命周期，及
`-- plane-nurbs rounding-only` 单独检查普通方向圆角。

fresh XT 和临时日志保存在 `temp_docs/regular-solid-blend-oracle/` 及
`temp_docs/m1-*.log`，不作为私有 SDK 的分发路径。完整 M1 几何与生命周期矩阵
最终日志为 `temp_docs/m1-final-release-acceptance.log`，退出 0，总计 227.548 秒；
37 次完整体检查零故障，11 个结果严格对照通过。早期
`temp_docs/m1-final-release-matrix.log` 因旧镜像问题退出 134，不能当作整轮通过。
最终矩阵之后的修改仅涉及失败清理及 UV 读取边界，由针对性回归和完整测试覆盖。
这些修正后的生产 Release 再构建通过，并再次通过独立 PK 生命周期复验，日志为
`temp_docs/m1-final-production-build.log` 和 `temp_docs/m1-final-production-lifecycle.log`。
最终测试日志为 `temp_docs/m1-final-kernel-tests.log`、`temp_docs/m1-mirror-final-xt-tests.log`；
既有 oracle 为 `temp_docs/m1-final-release-sheet-oracle.log`、
`temp_docs/m1-final-release-boolean-oracle.log` 和
`temp_docs/m1-final-local-operation-oracle.log`。
