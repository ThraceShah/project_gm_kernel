# Hosting 618144f 独立复审证据索引

结论见 `docs/web-progress-hosting-618-independent-review.md`。固定源 `618144f1381cc14d98cc0c9dd582e52fc4b81a09`，baseline `1b4efa0fac20515a2b846a5738000fdfa46de587`，只在reviewer自己的loopback fixture中制造存储故障。

## 实际命令 / exit

临时目录 `temp_docs/hosting-618-review/` 的 `Directory.Build.props` 导入 `../../scripts/Directory.Build.props`，原脚本输入保持hash不变。命令均从项目根目录执行：

```sh
MSBUILDDISABLENODEREUSE=1 dotnet build web/host -c Release -m:1 -p:UseSharedCompilation=false > temp_docs/hosting-618-review/build-host.log 2>&1

MSBUILDDISABLENODEREUSE=1 NO_PROXY=127.0.0.1,127.0.0.2 dotnet run --no-cache --file temp_docs/hosting-618-review/RollbackClosure.cs > temp_docs/hosting-618-review/rollback-closure.log 2>&1

MSBUILDDISABLENODEREUSE=1 NO_PROXY=127.0.0.1,127.0.0.2 dotnet run --no-cache --file temp_docs/hosting-618-review/RollbackValid.cs > temp_docs/hosting-618-review/rollback-valid.log 2>&1

MSBUILDDISABLENODEREUSE=1 NO_PROXY=127.0.0.1,127.0.0.2 dotnet run --no-cache --file temp_docs/hosting-618-review/ClosureProbe.cs > temp_docs/hosting-618-review/closure-matrix.log 2>&1

MSBUILDDISABLENODEREUSE=1 NO_PROXY=127.0.0.1,127.0.0.2 dotnet run --no-cache --file temp_docs/hosting-618-review/Integrity.cs > temp_docs/hosting-618-review/integrity.log 2>&1

MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache --file temp_docs/hosting-618-review/Postcheck.cs > temp_docs/hosting-618-review/postcheck.log 2>&1
```

以上最终各命令exit均0。两原rollback诊断内部CLI分别**3/3**；矩阵25个负例各**3**、最终合法回滚**0**；Integrity各内部CLI均**0**。外层诊断结束不得替代内部操作判定。

独立矩阵初次准备时，reviewer诊断的 `JsonArray.Add<string>` 在禁反射设置下抛NotSupportedException，外层exit134，发生于source fixture构造、target activation/host启动之前；保留 `closure-matrix-setup.log` 和 `matrix-setup-jsonarray/`。改为显式JsonNode后，在新 `matrix/` 目录执行最终26/26，不归因于候选代码，不覆盖前置证据。现有NU1903及诊断中的JSON泛型AOT分析警告保留；没有NativeAOT构建。

## 持久复现入口

`docs/hosting-618-review/ClosureProbe.cs` 保存实际执行黑盒探针，唯一实质路径改动是将输出由脚本相邻matrix移到 `temp_docs/hosting-618-reproducer/matrix/`。脚本自己从所在目录求项目根，调用真实Prepare/Activate CLI、真实host DLL，使用动态loopback端口并记录PID，退出finally只清理自己的host；拒绝覆盖旧证据。未为这个输出路径改动重复26次操作，使用diff核对其余逻辑与实测源码完全相同。

复现时先按上文managed build host，然后：

```sh
MSBUILDDISABLENODEREUSE=1 NO_PROXY=127.0.0.1,127.0.0.2 dotnet run --no-cache --file docs/hosting-618-review/ClosureProbe.cs
```

应输出25个明确拒绝PASS、合法multi-run/reordered-map PASS、SUMMARY26/26、source unchanged及OWN_HOST_CLEANED。脚本非host生产或正式tests的一部分，不启动外部端口、portal、units或发布。

## 独立实测 SHA-256

| 路径（相对项目根） | SHA-256 |
| --- | --- |
| `scripts/HostedWebProgressSite.cs` | `71c41fc492c96cc3b2ca1b67adc31b75c235595b88078fcea18b03ea503686cb` |
| `scripts/VerifyHostedWebProgressGaps.cs` | `4022c96f4a08d11b4c86a1293d2c8ec3a2306d86bf029b45433c7f4a56bbb97f` |
| `web/host/bin/Release/net10.0/GmProgressHost.dll` | `1300ef07ac42a1cf7d7f1cfc95a513bce3d6c025b15ec991337449897baded16` |
| `temp_docs/hosting-618-review/ClosureProbe.cs`，实际执行版 | `2e433118f45a6b6493f589527659fb0b875a9b45fe3a0b4aebb068eceb5d9c40` |
| `docs/hosting-618-review/ClosureProbe.cs`，持久输出路径版 | `2c0cfefa38b14c9d2968124cd8930c058d7a027afe460d35c468413999d6277f` |
| `temp_docs/hosting-618-review/Integrity.cs`，原文副本 | `f2a9b2e25e3a60d275c8ad33211c0d84e38e4cabec33a57bf66bb3a6d450733e` |
| `temp_docs/hosting-618-review/rollback-closure.log` | `02c2654a97af0f98594e237b7771099a5ee6bd560758c1a40ce56dff91f73873` |
| `temp_docs/hosting-618-review/rollback-valid.log` | `e8c3654d10fb1e575bfbecdaf36af964c047a0014e9b55135c401f87a03c3185` |
| `temp_docs/hosting-618-review/closure-matrix.log` | `d4928abc2f874841fe959f62d479cca0a87f41c92b8cd0431a76efa0ff3ab5e7` |
| `temp_docs/hosting-618-review/closure-matrix-setup.log` | `5d979b3cb5bd2babd69df4a573b13bdc89e1a75faa7dde1446f3de9b9400c9b0` |
| `temp_docs/hosting-618-review/integrity.log` | `f2df7bd02b533fa4974b2c4ddb5657b56aa6f2142cee201f1b754379e30615d5` |
| `temp_docs/hosting-618-review/postcheck.log` | `74527aa95644efa57bc9acdf8f3093abd1dce906a07d7e5213a788fcc0b44f8c` |

每个矩阵操作另有 `temp_docs/hosting-618-review/matrix/<case>.cli.log` 保存真实内部exit和完整stdout/stderr；`host.log` 为该自有host日志。目标metadata/tree/object故障已还原；最终合法回滚状态和源cache留在该ignored目录供核对。

## 只读引用的实施者日志，非本轮重跑

下列路径前缀均为 `../gm-hosting-gemini/temp_docs/hosting-closure-sol/`：

| 日志 | SHA-256 / 实际计数 |
| --- | --- |
| `gaps-all/logs/gaps-all.log` | `809f6019fbb6e300b61def4a275a7f2938faeaecf6dea73edfae3e3d0f59bf91`；84/84 |
| `gaps-h1c-final/logs/gaps-H1c.log` | `23fd79cfee23a9ae0262edb737161ab5fd789fa3a4b4352be2ce2a01b9930482`；39/39 |
| `verify-original-data/logs/verify-hosting-log.txt` | `417981302e1eade31c9e08d34c05942c8feba801388c4b1a96ffe21ef1a2a52e`；53/53 |
| `final-rollback-closure.log` | `ffd2f265fb8ac76b07f6f06dba2c8eb8cd7534a01af1ee00f27fce6074170db6` |
| `final-rollback-valid.log` | `00ff70637f479c5d6afd134cdc3453a754fdca483b4a725a099e7d3cce361f01` |
| `final-integrity.log` | `59278a64ab0f9aec2b52e1ff2e0d11c178c1a4431c2faafa62a3564102e23444` |

没把分段84和39合成“一次86”；没把53host/RSS数据算成本轮新执行。旧报告、原diagnostic、前seam/OptA/R3分支及所有ignored证据均保留。
