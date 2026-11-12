# 统一 GM/PK 托管运行策略

当前全部项目内GM/PK比较统一开启TieredCompilation；TieredPGO、QuickJit、
QuickJitForLoops固定开启，ReadyToRun固定关闭。选择适用于整组工作，不能按
case、操作或报告换开关。它控制托管宿主及GM IL，PK本体仍是独立38.0.150原生库。
没有修改用户全局环境或dashboard服务配置；旧报告的未知继承环境仍保持未知。

入口示例（项目根目录）：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/RunNativeOracle.cs -- RegularSolidBlendOracle.cs plane-nurbs --output-name=new-immutable-run
```

入口建立子进程环境白名单并明确五个开关；不继承COMPlus或额外DOTNET调参。
`scripts/Directory.Build.props` 给使用ParasolidScriptHost的项目生成匹配的四项
runtimeconfig。ReadyToRun使用官方环境开关，不虚构runtimeconfig字段。
`ParasolidScriptHost` 在启动PK之前检查环境及runtimeconfig冲突，失败属于运行环境，
不是模型失败。直接运行未设完整策略的旧调用命令会被拒绝，当前原型不保留旧默认入口。
GM单测同样显式使用上述五个DOTNET环境值，MSBuild不复用节点；不触发NativeAOT。

一次有界ABBA比较见 `temp_docs/native-jit-policy/bounded-whole-operation-v1/`，
脚本为 `scripts/CompareNativeJitPolicy.cs`。固定source
`1bbf3daedb787e8e5cf520f3ee1ea5cd077a4316`、同一份Release Native IL
`c3dcd7c113a5a9bfe194d1466fc6927b068e3917f47e2ba7a63ae1822f0770ad`，
Linux x64、实际.NET10.0.10。四进程顺序关/开/开/关，每进程四族各两次fresh模型，
共32次完整场景、GM64及PK64次实际Fix/Boolean API；每步真实XT full/strict通过。
四个原始进程exit均为2，物性请求U单列，不能把该退出码改成0。

| 场景族 | 关闭GM API合计 ms | 开启GM API合计 ms |
|---|---:|---:|
| plane/NURBS | 2686.711 | 3438.194 |
| depressed roof | 1343.418 | 1231.864 |
| NURBS/NURBS | 25974.574 | 24708.706 |
| concave NURBS/NURBS | 102537.097 | 96364.419 |
| 全部 | 132541.800 | 125743.183 |

全组GM API合计降低约5.13%，因此选开启；plane/NURBS退化也保留，不能只报告
有利族。第一次序号只表示该family首次使用，后一次fresh调用也不冒称长期稳态。
这是统一配置选择依据，不是跨平台最终性能接受，更不代表GM已追平PK。
原始comparison.json SHA256为
`880c7abe9bcf13f845c7fa0b78e7b174ac73cafff0f1b7a8781c86f039f71693`，
逐进程配置、环境白名单、原始退出和每次API时间保持原字节。

不计时的独立最小GM方法JIT观察分别为FullOpts/Tier0，验证两个配置实际进入不同
编译分支；该观察不声称每个大方法最终tier相同。配置字段的AppContext表示按实际
启动值解析，不把RuntimeFeature.IsDynamicCodeSupported=false误当NativeAOT执行。
官方[编译配置](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation)
说明了这些开关及DynamicCodeSupport的含义。

新入口实际 `native-uv-scalar-v2.log/.exit` 打印完整匹配配置，并完成八次完整建模，
原exit2、几何/拓扑通过。首轮未生成runtimeconfig的原exit134保留：属性条件在
文件脚本导入时机未生效，改为显式RuntimeHostConfigurationOption后解决，没有执行
模型来补签该失败。`jit-policy-negative-tc0.log/.exit` 则以故意冲突TC0在PK启动前
被拒绝，原exit134；该负例不算建模失败。
