# 原生物性最大精度控制

Parasolid v380 官方 FD Mass Properties 31.1 定义 `accuracy` 的合法范围为 0 到 1，它是控制计算工作量的指标，不是正确率。31.4 定义 `PK_mass_bound_modulus_c` 为估计值加减一个误差模数，并要求保存误差界。MCP 已实际查 `PK_TOPOL_eval_mass_props`，真实本机 Linux x64 PK 38.0 执行如下同字节产品控制：

```
MSBUILDDISABLENODEREUSE=1 dotnet run --no-cache scripts/NativeAcceptanceContractOracle.cs -c Release -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true -- maximum-accuracy-actual-block-v1 temp_docs/regular-curved-boolean-oracle/native-boundary-integration-v1/block-15903-0-native.x_t temp_docs/regular-curved-boolean-oracle/native-boundary-integration-v1/block-15903-0.x_t
```

两个输入实际由独立 GM/PK roof 加 block union 产生，不重建、不换输入。reference SHA256 `a1e76d581618bf57c6f13699df9f9f12eef3f4cdbd375bda26062d404634f053`，actual SHA256 `bbc7ecae9bbd0212ba18328407d6366f5e09e1965aefd3ff25dfc91f28aa8420`。二者 fresh receive 和完整 BODY_check 均为零错误；本控制没有执行建模 API，不能用于补签建模成功。

`accuracy=1` 时 volume/mass 为 Verified，cog、inertia、area 仍 Unresolved，**原 exit 2**。例如 area reference `8.50879339331351 ± 1.1091258702577989e-6`，actual `8.508793396776333 ± 1.170386804361101e-6`；归一化差的外上界 `2.4009100555128543e-7` 高于既定 `1e-7`。这些界与原 `accuracy=.99999999999` 的实际界接近；不能通过把精度字段设为 1 或误差区间相交，就声称已证明同值。

脚本新增显式 reference/actual pair 控制模式，复用当前共享 `ParasolidMassProperties`，按两输入原字节保存、独立记录积分返回及状态；不修改共享默认精度、阈值、建模容差或判定规则。全部原始输入、15 分量数据与 hashes 在 `temp_docs/native-acceptance-contract/maximum-accuracy-actual-block-v1/`，原 command 输出及 exit 在 `temp_docs/pk-native-acceptance-audit/mass-maximum-actual-v1.*`。这是可复制的原生积分不确定性边界，不是几何差异的证明或物性验收通过。
