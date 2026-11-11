# fixed39 / fixedf2复现与证据

审查 source分别为 `39c1dc8c037082b040defc7f3a202d6fa86a145b` / `f2a258b7b5290a5e5160841e1c24668589faf4bf`；before `04c7ed00f39c09cb10faad132d77e50a6a669f07`。本目录四份capture全是无敏感合成输入，不是新的真实建模结果。

## 四个未改生产代码的资格反例

复制固定f2的 `scripts/ModelingPairedBenchmark.cs` 至 `temp_docs/perf-optA-review/QualifierProbe.cs`，只在ValidateFixtures的 `Check("valid",true,_=>{});` 后追加：

```csharp
Check("review-no-steps",false,c=>c.Remove("steps"));
Check("review-unparseable-step-ids",false,c=>{foreach(var step in c["steps"]!.AsArray())step!["id"]="not-an-operation";});
Check("review-required-primary-only-diagnostic",false,c=>{foreach(var check in c["checks"]!.AsArray())check!["status"]="diagnostic";});
Check("review-extra-unknown-kernel-error",false,c=>c["timings"]!.AsArray().Add((JsonNode)new JsonObject{["step"]=999,["kernel"]="other",["ms"]=-1,["error"]=901}));
```

复制脚本的路径变化只影响selftest默认输出目录；显式传下面output，无oracle被执行。Qualify、RequiredCheckIds和ValidCapture(2rep)、summary/overall helpers均来自固定f2原文，未修改。

```sh
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ModelingPairedBenchmark.cs -- --validate-fixtures --output temp_docs/perf-optA-review/original
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/perf-optA-review/QualifierProbe.cs -- --validate-fixtures --output temp_docs/perf-optA-review/independent
```

原脚本exit**0**，17自测通过；独立副本exit**3**，原输出为：

```text
fixture review-no-steps: qualified=True expected=False WRONG
fixture review-unparseable-step-ids: qualified=True expected=False WRONG
fixture review-required-primary-only-diagnostic: qualified=True expected=False WRONG
fixture review-extra-unknown-kernel-error: qualified=True expected=False WRONG
FAIL 4 fixture verdicts wrong
```

持久inputs与原ignored产物逐字节复制：

| 本目录文件 | SHA-256 |
| --- | --- |
| no-steps.capture.json | `ac792b98371b194bda183f64db6f5ba23cce858a60fb7d3dfde3a1910e797e60` |
| unparseable-step-ids.capture.json | `9927834289880f065b7c55ad67ee5005442b57ff53530d884404d080ca3b0956` |
| diagnostic-primary.capture.json | `5cd88e26ff020e4c30f35fd961fb34b3253453d51881319886eae4c9cdc6ea48` |
| unknown-timing-row.capture.json | `ebb51edf630f02157791db4e8537fa63485110d4ac51a5975ac14afba84940f1` |

| ignored来源/诊断 | SHA-256 |
| --- | --- |
| 固定f2 ModelingPairedBenchmark.cs | `40e747a2d092f438ee2b8e18a9a37948d03261299f3a4b539ab7d32314a8152a` |
| QualifierProbe.cs | `9e4f86907317ad957d53cb274589a19964a6d712d0cc178e2dcca19d7456e363` |
| qualifier-original.log | `aab7ab3f212ae71fbc5086de0352862ab5e42ae1f715120fb78dac0657c33bc5` |
| qualifier-independent.log | `f38912101d0673e5829bc8de07a8bf8d99d10adac0a413b3396913cd995659a3` |

## q2测试/真实PK smoke与只读Mac产物

```sh
MSBUILDDISABLENODEREUSE=1 NESTED_BLEND_BOUNDS_DUMP=temp_docs/perf-optA-review/q2-dump.txt dotnet test tests/KernelTests -c Release -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --filter 'FullyQualifiedName~NestedBlendBoundsInitializationContractTests|FullyQualifiedName~NestedBlendSplineCacheTests' --logger 'console;verbosity=normal'
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ModelingPairedBenchmark.cs -- --mode measure --cold 1 --warm 1 --cases sphereBoolean --output temp_docs/perf-optA-review/cheap-smoke
MSBUILDDISABLENODEREUSE=1 dotnet run temp_docs/perf-optA-review/AuditArtifacts.cs
```

实际退出分别**0/0/0**。测试dump的相对路径由testhost工作目录解析，原文件在 `tests/KernelTests/bin/Release/net10.0/temp_docs/perf-optA-review/q2-dump.txt`，结束后复制到review ignored目录。8/8与原allocation guard没有替代全60/full suite；q2 dump和Mac dump的哈希不相同不被用作跨平台失败/通过证明，各自环境分开。

smoke immutable report位于 `temp_docs/perf-optA-review/cheap-smoke/runs/20261111T014913Z-f2a258b/benchmark/report.json`；未使用stdout硬编码显示的默认REPORT路径作为实际文件。独立AuditArtifacts逐3个step查预期Boolean、唯一GM/PK owner、正有限ms/零error、26个required ID唯一且全pass、captureErrors0，全部成立。只核正确性和采集链路，不提取ratio作收益，也不把q2计时与Mac相除。

| ignored文件 | SHA-256 |
| --- | --- |
| bounds.log（8/8） | `aa53bfd8059c4e9e2627839640a02f402b3eb44fe557e78a9e4b34324f720154` |
| q2-dump.txt | `2b4e766fac67fa2c991984b0711249ac0deb1b4a5ce6dc36b7a46a9d5114c0dc` |
| cheap-smoke.log | `94b4934b3200ca7e2710e9241bb023f1013cfa090bee068d14d16866d690f846` |
| cheap-smoke report.json | `1a20545f447fe3281cf87eca56eb483df26036113cefbabb1994dcc30f34c1bb` |
| cold-0/capture.json | `e034dd248ffa20b525db6f585e0b052ccd99ea5ae81a980dad7ef606273aaa28` |
| warm/capture.json | `8e8d0366aaee6996ce1f20b5e37e15e2aab2ff62bbfe1c0c81d2c2c6a5277019` |
| artifacts.log | `eb303632ff3c2090bcdc883fa1416a3f61cb9a8aad4e508eb838a764ac127f30` |

Mac仅只读拉取其 `temp_docs/modeling-performance/opta/` 原产物，未在Mac启动基准/测试或写实施者工作树；JIT/dump不是本review新生成。两个JIT均为相同BoundSegment完整签名、Apple ARM64 FullOpts/No PGO，分别一个listing；独立解析各zero-store self-loop的立即数并相加得到15/0与226288/0；完整程序集hash/当次环境未被这些文本记录，不能补造。

| 本地只读副本 | SHA-256 |
| --- | --- |
| dump-base.txt / dump-candidate.txt（各70行） | `c5b09e1387b8dafb4cde1a31fd7ea7525e53b36c95d85dbcd244fbbe6980da21` |
| jit-base-disasm.txt | `b0ca57401d5654f963cc9c704b359e4dccb083530dbe9e8c0a373762f870d228` |
| jit-candidate-disasm.txt | `a55a39e5f0a219bdfb09c9855d6f55ad2c7eed34b5529ec0e4e9de00d9179bbc` |

固定源码hash：NestedBlendBounds.cs `a580a030e7a189ce151534e52caabc56a4188cbcee5d365c406d0161162be898`；新增合同tests `09780e1cc5d15f869669da2683ded66dd9cd8b72d77ff3e2dc27f7d02ba40aab`。结束后精准过滤本review工作树的testhost无残留，没有全局pkill或中断其他worker。无AOT、完整bench和参数/容差更改；最终 `git diff --check` 通过。
