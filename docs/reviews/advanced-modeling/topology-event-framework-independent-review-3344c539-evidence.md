# 3344c539 独立复审证据索引

本文件从固定 `3344c539ab919b3f545cde2c3f15e9072440f86e` 运行记录整理。结论见 `docs/reviews/advanced-modeling/topology-event-framework-independent-review-3344c539.md`。所有路径相对于项目根目录；私有库、schema、凭证和厂商反编译内容不进入本报告。临时源码/日志保留于 ignored 目录，以下摘要使结论不依赖读取整份临时日志。

## 本轮实际命令与退出

先确认 clean tree 并从固定对象建立 `ThraceShah/gm-source-root-3344-review`，`git rev-parse HEAD` 返回上述 SHA。源码审查使用 `git diff 9b2f4c6c989a393fd2cbaa3e66d6ea5b52035017..3344c539ab919b3f545cde2c3f15e9072440f86e`。原生产和正式测试文件未编辑。

临时 `Directory.Build.props` 导入 `../../scripts/Directory.Build.props`。两个临时 C# 文件均引用 `../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj` 和 `../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj`，启用 `UsePskernelSharpUsings` / `UseParasolidScriptHost` / `AllowUnsafeBlocks`，诊断 assembly name 为 `BooleanOperationOracle`；构建使用 test hooks。无手写 P/Invoke/ABI，无库重定向。

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/source-root-3344-review/Preflight.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true > temp_docs/source-root-3344-review/preflight.log 2>&1

MSBUILDDISABLENODEREUSE=1 dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter FullyQualifiedName~InteriorSourceRootPreservesTheStationaryPlaneConstraintAndItsFiniteBranch --logger 'console;verbosity=normal' > temp_docs/source-root-3344-review/new-test.log 2>&1

MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/source-root-3344-review/ReviewOracle.cs -c Release -p:KernelTestHooks=true -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- review-reverse-3344b --reverse > temp_docs/source-root-3344-review/reverse-oracle-final.log 2>&1
```

| 命令 | 实际结果 |
| --- | --- |
| Preflight | exit0：真实 PK start、unit block/full0，managed SessionStart0；继续使用已授权 Linux x64 runtime/schema，未切换活跃库 |
| 单测试 | exit0：1 passed，Total time 39.8670 seconds |
| 反向选边 Oracle | exit134：只实际进入一次 Boolean；最终异常 `GM Boolean contract incomplete` |

构建出现现有 ILCompiler 10.0.10 的 NU1903 警告；没有 NativeAOT 编译或发布。运行后按当前 worktree 过滤检查 dotnet/testhost，无该树残留 testhost，未广泛 kill 其他任务。

## 最小诊断如何从固定 Oracle 派生

`ReviewOracle.cs` 基于固定 `scripts/RollingBooleanConsumerOracle.cs`：仅调整临时文件的 project 路径和根目录层级；添加 options 同名字段 offset/sizeof 验证；将 Fix 阶段 trace 回调替换为局部 hook 诊断。Boolean 调用、工具构造、序列化输入、回滚、PK compare 等流程不改。恢复所有临时 line 数据后才继续 Fix/Boolean，最后 target hash 与原候选一致。

回调只读取仍存活的 trace carrier：取得源 line 和目标 ellipse；用目标 ellipse 的 center/refdir/axis/radii 从 `t.EndPoint` 推导 atan2 station，调整到 `[0,Tau)`；调用正式 `TestCertifiedContactRootPoint`，正例则检查 `t.StartPoint==t.EndPoint` 各坐标相等。stationary 点上的额外诊断核心如下（标识符与临时脚本一致）：

```csharp
var original=KernelRuntime.LineDataPool[source.DataIndex];
var reversed=original;
reversed.AxisX=-original.AxisX;
reversed.AxisY=-original.AxisY;
reversed.AxisZ=-original.AxisZ;
KernelRuntime.LineDataPool[source.DataIndex]=reversed;
try
{
    Require(KernelRuntime.TestCertifiedContactRootPoint(
        t.Curve,t.ProductCurve,t.Surface,t.OtherSurface,t.EndPoint,
        -t.Last,-t.First,t.SpatialError,0,Math.Tau,at),
        "reversed line and reversed finite root interval");
    reverseLinePositive++;
    Require(!KernelRuntime.TestCertifiedContactRootPoint(
        t.Curve,t.ProductCurve,t.Surface,t.OtherSurface,t.EndPoint,
        -t.First,-t.Last,t.SpatialError,0,Math.Tau,at),
        "reversed interval with wrong order");
}
finally { KernelRuntime.LineDataPool[source.DataIndex]=original; }
```

此片段是**受控受信 root 的 helper 方向检查**，不证明反向 provider 自动生成 enclosure 正确。完整回调另要求 acceptedRoots>0、reverseLinePositive==1，实际出现四个正例。

最终日志关键内容：

```text
OPTIONS_LAYOUT size=168 matchedFieldOffsets=38
GUARD reversedLinePositive=1 wrongIntervalRejected=True event=20 at=3.141592653589793 source=[4.499999999999939,4.500000000000061] p=(-1.5,-2,5)
BLEND error=0 count=2 stage=Publish
GUARD markOrderReverse=True acceptedRoots=4 reverseLinePositive=1 interventionRestored=True
FULL target native receive faults=0
FULL tool-1 native receive faults=0
FULL tool-2 native receive faults=0
OPTIONS version=19 function=15902 default_tol=1E-05
GM BOOLEAN error=157 result=21653 bodies=0 entered=False stage=Components faces=36/-1 sheet=RayIntersections curve=547 parameter=-1.996903548889591 status=Success root_failure=None root_box=[0,0.007581288159474386]
PK SAME INPUT BOOLEAN error=0 result=21650 bodies=1
FULL PK output faults=0
Unhandled exception. System.Exception: GM Boolean contract incomplete
```

正式 Oracle 在 GM error!=0 后、PK 调用前检查三个输入 transmit 字节与 before 相同。最终异常是上述 contract incomplete，未发生 rollback 失败异常；不能把它写成注入901的证明。没有 GM 输出，因此也没有本轮 Boolean strict/mass 结果。

### 诊断自身的两次前置失败

如实保留：第一次临时代码变量 `q` 与后文局部变量冲突，编译失败，未执行几何；第二次错误地在 Fix 完成后读取 trace 临时 carrier，临时曲线已被发布清理，诊断 AccessViolation。第二次发生在 `BLEND error=0 / Publish` 后、任何 Boolean 前，不能归因于候选内核缺陷；原错误源码保留为 `ReviewOracle-expired-handles.cs.txt`。修正为存活回调内检查后得到上面的最终结果。先前部分输出目录 `temp_docs/contract-sol/review-reverse-3344/` 没有覆盖，最终使用新目录 `review-reverse-3344b/`。

## 本轮 SHA-256

| 文件（相对于 `temp_docs/source-root-3344-review/`） | SHA-256 |
| --- | --- |
| `Preflight.cs` | `92cfa810f3dde3cf0adb2c07e64a715a218f8acea0dbee3f37ca26eba58039cb` |
| `ReviewOracle.cs` | `25ee06dab10f4f01200415c4f48c83bd8709ee9023e9b39d12d93439a73dd534` |
| `preflight.log` | `7ec8f382dcb89d3cb8e474366638d2af30cc5132bd09906227c211688155d6f7` |
| `new-test.log` | `80ecd9f32fae4729ad80dd6fba261ed9347f7f7eea86be1d373419b49302d615` |
| `reverse-oracle-final.log` | `9327ae1ff46fdf87bfbd8d0556645db9a4cac582fd799066c97ec0ccb576beb1` |
| `reverse-oracle-setup.log` | `fa6d674d5cfd4f5e6252ea989ed8325a404ad5aa0a21903f417a192300a812fe` |
| `reverse-oracle-expired-handles.log` | `1dc21f5f77ba13dea2e3ff7d3ba39e55ce1c3dd8d9ba3237ca3b061cdfa57673` |

| 文件（相对于 `temp_docs/contract-sol/review-reverse-3344b/`） | SHA-256 |
| --- | --- |
| `target.x_t` | `399e580b74e050cc454aca4879e90d692a6639b159710e166a1cf2ea364f8dde` |
| `tool-1.x_t` | `ad885c46aeb0c22a384cc4f3c567cbdc0030ed55c30a57ee99af98d4d186aec2` |
| `tool-2.x_t` | `8056a1bf6862831b1d4de1da36af94f6bd5164fcaf019b5f5fc8d51644c40792` |
| `boolean-options.bin` | `39389f95d808a300867f18bdac6ed34d8b2283f059417ad43d13519b7253c813` |
| `inputs.json` | `34389f125d9736ad7d50bc936392559d7574404d6419595c127ea78e37ea11b4` |
| `pk-output-0.x_t` | `e0060b63c99c528b6ace056ea0da8f2bd40c49bea6eab87c27e9d8c039ce4601` |

该结果 hash 标识本轮 PK 输出，不宣称不同 session 的 transmit 必须逐字节稳定。工具/target/option hash 则用于核对这一次输入。旧 9b target hash 为 `7de009139ea2dde494d83859adeb80141871dd61d7d8ca0e4354e0022f5a16dc`，并未拿它替代新 target 进行本轮 Boolean。

## 只读复用的固定实施者证据

以下日志位于相邻实施 worktree 的 `temp_docs/contract-sol/`。没有再次执行对应命令；本轮报告以源 SHA、原日志内容和 hash 引用，不把它们累计成本轮151或primary新跑。

| 日志 | SHA-256 / 用途 |
| --- | --- |
| `contact-root-unit-final.log` | `f3fb2d6f71ecb8575c33f58b0aba5fc3b2bb8281f10dbe0443eb82cddaeb289f`；原新单测含 wrong-support |
| `contact-root-focused.log` | `6d12a0edae4bced9f8f8c3547cd5b8e0064d3519047989298adee34f79fdd6ee`；原151/151 |
| `contact-root-native-primary.log` | `cabc23c7db5ba4483c966ffdf0fc0f2e6b70252c974500eee5d4225b13aed3a6`；冻结独立 native reference 对 fresh candidate |
| `contact-root-1.log` | `c398abbe78c52a94a6af7b78401dc109faba579e58f7a990f64acb1f618f7e7c`；原默认选边 same-input failure，exit134 |

native primary 原日志：双方 full0，strict global21921/local21931/face_pairs0 通过既有严格比较器（没有用 face_pairs 数量当匹配总数），normalized15 `2.1627000794607434e-10`；volume `121.31064762553429 / 121.31064762568423`，area `142.80473617288544 / 142.80473617299885`。

冻结 native target `bd75b7926956c1506e00c172816a0bfc1bb07db1660d74da65aaf1734f219ef1` 在 `../gm-topology-contract-sol/temp_docs/contract-sol/native-reference/target.x_t` 与 `../gm-opus-r1r2-review/temp_docs/opus-r2-r4-review/native-product.x_t` 的哈希一致。本轮没有重建纯 PK native Fix。这条主比较与本轮同输入 Boolean 是两条不同证据链，不能互相替代。

未重跑：151、完整19、self96、全生命周期、347、全套、NativeAOT 或 Mac 性能。当前局部接受不含这些 gate。
