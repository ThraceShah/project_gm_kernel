# PK_DEBUG_BODY_compare 等价曲面表示实验

日期：2026-11-06。分支：`probe/debug-body-compare`，基于 `e1f3247`。

## 结论

曲面形状相同、控制点数量或数值不同，**不能保证全部局部检查返回 no_diffs**。
本次升阶、插结、重新参数化、等比缩放齐次控制点等用例，数学几何相同，
但 `all_tests=true, max_diffs=64` 都返回 `local_result=diffs`，
原因是约 `1e-17` 至 `1e-15` 的 `surf_dev/curve_dev`。
这些差异远小于本次会话精度 `1e-8`。

切换为 `all_tests=false, acc_dev_tests=false, tol_dev_tests=true`，
保留数量、类型、方向、容差、缺失和匹配检查后，上述用例通过，
参数双向反转用例除外：它仍报告边界曲线的 `curve_sense`。
关闭方向检查后，该用例也通过几何容差检查。

这说明接口会比较几何偏差和其他被启用的属性；
返回差异不等于控制点数组不相等，也不一定表示存在有意义的形状变化。
反过来，仅关闭几何偏差检查会漏掉真实变形。

## 环境与复现

- 真实 Parasolid：38.0，build 150，`PK_SESSION_ask_precision=1e-8`。
- .NET SDK：10.0.302；托管单文件脚本，无 NativeAOT 发布，无项目内核修改。
- 脚本：`scripts/ProbeDebugBodyCompare.cs`。
- 运行时由 `scripts/ParasolidScriptHost.cs` 定位，绑定复用 PKToy。
- 临时 worktree 使用主副本的 `third_party/parasolid`，通过相对符号链接接入；
  PKToy submodule 固定在仓库记录的版本。
- 所有曲面先用 `PK_SURF_make_sheet_body` 创建 body，经过 `PK_BODY_check`，零 faults。
- 面上附着的曲面实测均保持 `PK_CLASS_bsurf=4006`，没有简化成解析平面。
- 本次只研究真实 Parasolid 的接口行为，不涉及项目内核 API、B-rep 或 XT 实现改动。

在项目根目录运行：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ProbeDebugBodyCompare.cs
```

原始本地输出：`temp_docs/debug-body-compare/results.txt`（临时文件，不提交）。
总共 12 对 body、每对 7 组选项，即 84 次比较，所有 API 调用成功。
编译过程存在已有依赖的 NU1903 包审计警告，不影响本次运行。

## 实验构造

曲面基准是双二次非有理 Bézier patch：`3×3` 控制点，
`P[i,j]=(i/2,j/2,z)`，仅中心控制点 `z=0.8`，其他 `z=0`。
其数学形式是 `S(u,v)=(u,v,3.2u(1-u)v(1-v))`，`u,v∈[0,1]`。
边界保持单位正方形，因此仅移动中心控制点会改变面内部而不改变边界。

平面用例将所有 `z` 设为零。非线性参数化用例把中间一列控制点的
`x=0.5` 改为 `x=0.2`，保持次数、控制点数量、节点和边界不变。
新曲面是 `S'(u,v)=(0.4u+0.6u²,v,0)`，x 映射在 `[0,1]` 上严格单调，
所以两张曲面精确覆盖同一个单位方形。

该用例的相同 UV 采样位置最大相差 `0.15`，
按逆参数映射后 `21×21` 网格最大距离只有 `4.44e-16`。
比较器报告的 `surf_dev=3.33e-16`，表明此次比较能够处理不同参数化，
并非直接用同一 UV 上的位置相减。
采样用于交叉检查，几何等价依据是上述公式和保形操作；
有限采样本身不是任意曲面的完整等价证明。

## 实测结果

下表的“容差全项”是后文 `tolerant-all`；“全部局部”是 `full`。
除最后一行外，所有用例 `global_result` 均为 no_diffs。

| 用例 | 形状关系 | 全部局部 | 容差全项 | 关键诊断 |
| --- | --- | --- | --- | --- |
| 独立创建相同控制点曲面 | 相同 | no_diffs | no_diffs | 无差异 |
| U、V 各升一阶，`3×3→4×4` 控制点 | 等价 | diffs | no_diffs | surf_dev `2.29e-16`，curve_dev `1.11e-16` |
| U 插 `0.37`、V 插 `0.61`，`3×3→4×4` 控制点 | 等价 | diffs | no_diffs | surf_dev `2.50e-16`，curve_dev `2.22e-16` |
| 同时反转 U、V，控制点顺序改变 | 等价 | diffs | diffs | 微小偏差和 4 个 curve_sense；几何容差模式通过 |
| 参数域改为 `[2,5]×[-3,4]` | 等价 | diffs | no_diffs | surf_dev `2.54e-16` |
| 平面中间列 x 从 `0.5→0.2` | 等价 | diffs | no_diffs | surf_dev `3.33e-16`，curve_dev `6.55e-15` |
| 有理曲面所有齐次控制点整体乘 2 | 等价 | diffs | no_diffs | 网格位置差 0，surf_dev `8.33e-17` |
| 曲面中心控制点 z 增加 `0.01` | 不同 | diffs | diffs | surf_dev `0.0024195389428034795` |
| 曲面中心控制点 z 增加 `1e-10` | 微小变化 | diffs | no_diffs | surf_dev `2.42e-11` |
| 曲面整体沿 z 平移 `0.01` | 不同 | diffs | diffs | surf_dev/curve_dev/vx_dev `0.01` |
| 方块高度 `1→1.01`，拓扑数量相同 | 不同 | diffs | diffs | 面、曲线、顶点偏差 |
| 单面 sheet 与六面 solid | 不同 | diffs | diffs | global=diffs，n_faces/n_acc_edges/n_acc_vxs 及 face_match |

这些数值是本版本、本模型下的返回值，不能据此推定其内部采样算法、
数学上的全局最大距离，或其他模型和版本的精确阈值。
例如中心控制点增加 `0.01` 时，同参数网格最大距离是 `0.0025`，
而比较器的 `surf_dev` 是约 `0.00241954`，两者不应混用。

## 选项和返回值

默认构造器与官方头文件宏一致，`max_diffs=0`，只做全局比较。
本次即使整体平移 `0.01` 或方块高度改变，默认模式也返回
`global=no_diffs, local=no_diffs`；这里 local 没有执行有效的局部比较。

`max_diffs>0` 才启用面匹配和局部检查；它限制被记录为不同的面组数量，
不是局部差异条目的数量。`PK_ERROR_no_errors` 表示调用成功，不表示 body 相同。

脚本选项矩阵（其余字段来自默认构造器）：

| 模式 | max_diffs | all_tests | acc_dev | tol_dev | 其他局部检查 |
| --- | --- | --- | --- | --- | --- |
| default | 0 | true | true | false | 默认值 |
| full | 64 | true | true | false | 默认值 |
| accurate | 64 | false | true | false | 仅保留 non_match |
| tolerant | 64 | false | true | true | 仅保留 non_match |
| tolerant-only | 64 | false | false | true | 仅保留 non_match |
| tolerant-all | 64 | false | false | true | 默认的数量、容差、类型、方向、缺失、匹配检查 |
| structure | 64 | false | false | false | 默认检查，但关闭 non_match |

本次 `tolerant` 与 `tolerant-only` 的结果相同。
`structure` 在明显改变曲面内部、整体平移、改变方块高度时仍返回局部 no_diffs，
说明不能通过关闭两种偏差检查来验证几何形状。

返回值：

- `global_result=21921`：`PK_DEBUG_global_res_no_diffs_c`。
- `global_result=21920`：`PK_DEBUG_global_res_diffs_c`。
- `local_result=21931`：`PK_DEBUG_local_res_no_diffs_c`。
- `local_result=21930`：`PK_DEBUG_local_res_diffs_c`。
- `n_global_diffs` 本次总是 6，即使无差异；它是全局数量比较记录数，
  不能简单要求为零。应读取 global_result，必要时检查记录两侧数量。
- `n_face_pairs` 本次相同用例为 0，单面有差异时为 1；
  不能当作所有成功匹配面数量。具体差异在 face_pairs[].local_diffs[]。
- 返回的相关 face/entity 是 session tag，两边数值不同本身不构成几何差异。
- 结果通过 `PK_DEBUG_BODY_compare_r_f` 释放。

## 对几何验证的用法

如果验收标准是“容差内形状相同，且拓扑结构和属性匹配”，本次验证可用：

```csharp
var options = new PK_DEBUG_BODY_compare_o_t
{
    max_diffs = 64,
    all_tests = PK_LOGICAL_false,
    acc_dev_tests = PK_LOGICAL_false,
    tol_dev_tests = PK_LOGICAL_true,
};
```

在确认局部检查开启后，同时检查 global_result 和 local_result。
若允许参数反转，还需单独处理 sense 差异；不应无条件忽略其他结构差异。
若只要求相同几何点集，数量、类型、方向等检查可能比这一标准更严格，
应明确选择所需检查，并保留无法匹配的诊断。

来源：本地官方 Functional Description §106.7（Comparing bodies），
`../../.local/share/parasolid-docs/html/chapters/fd_chap.107.html`，
以及 `third_party/parasolid/include/parasolid_debug.h` 中的选项宏和结果类型。
全局/局部流程来自文档；不同表示和开关的具体结果来自本次真实库实验，
没有据此宣称已知 Parasolid 内部实现。
