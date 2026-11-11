# PK 原生验收合同审计

此记录追加当前定义的审计证据，不改变 `f9c59201c0b9c5d103dee45124e3f43ca59a0921` 的固定产物、原始测量和退出码。默认 Boolean 入口的计算修正仍在开发，不属于下面原生对照的已验证范围。

## 物性积分的真实面片细分对照

官方 Function Descriptions 31.1.1 将 `accuracy` 定义为计算工作量/迭代控制，不是百分比或所要求的误差。31.4 与 31.4.1 要求保存积分误差界，并在比较相同或相似实体时使用它；区间相交本身不能证明实体相同，也不能证明固定点值精度。

`scripts/NativeAcceptanceContractOracle.cs` 通过现有 PKToy 绑定和 ParasolidScriptHost 实际调用 Linux x64 PK 38.0.150。脚本读取真实原生 XT，在包围盒中心沿三根坐标轴分别执行 `PK_BODY_imprint_plane`，没有修改 CHART 或坐标。每次从同一输入字节重新 receive；压印只增加边、顶点及有限面的划分。两侧完整 BODY_check 均为零故障，region/shell 数保持 2/2，面数由 10 增至 15；各输出的支持面通过 `PK_GEOM_is_coincident` 与原支持面匹配，oriented-surface sense 保持一致。

输入 SHA256 为 `3047cc3e0d7366fdb655dfae534ad9bc0fe32183443f55cd333923ba891e6a99`。脚本 SHA256 为 `15beb5031b59d21bcf04434ca0c2d4407cd2a0db58b75e623eff0f8220c281cc`；已验证日志 SHA256 为 `1cac2ba2153198b40c8cda29fa9e14c01e21cf8e9f06bddf4bb1929c59f6cc2a`。实际命令：

```text
MSBUILDDISABLENODEREUSE=1 dotnet run scripts/NativeAcceptanceContractOracle.cs -c Release -p:UseSharedCompilation=false -- imprint-verified temp_docs/short-edge-blend-oracle/native-transverse/a1-r0.5-t1E-07-reverseFalse-default-transverse-local-cut-reload-before-cut-self-constructed-product-copy-reflected-native.x_t
```

原始日志及外部退出码 `0`：`temp_docs/pk-native-acceptance-audit/native-contract-verified.{log,exit}`。实际输入和三个输出位于 `temp_docs/native-acceptance-contract/imprint-verified/`；该目录的 `artifacts.sha256` SHA256 为 `6c43ecc4c43ff03a2ab85dd6fdb909ce1525819ab6dc9e4eca40087eb8849027`。已存在目录拒绝重写，重跑须指定新输出名。

| 压印轴 | 原固定 normalized15 点值差 | 两个 accuracy |
|---|---:|---|
| 0 | 5.544753303860703e-7 | .99999999 / .99999999999 |
| 1 | 1.3852500837859584e-7 | 同上 |
| 2 | 2.9305446827897e-8 | 同上 |

前两项说明原点值门槛会拒绝真实 PK 自己进行的合法面片细分。这不是 GM 产品的等价证明，不能据此把 GM 的几何、拓扑、裁剪或材料差异改为通过；原生数值误差界仍需独立记录和判定。

## 边界精度对照

同一脚本实际新建 2×2×2 原生 block。session precision 为 1e-8，顶点报告 precision 为 5e-9；沿顶点向实体外平移至 9.9e-9，`PK_BODY_contains_vector` 仍返回 On，1.01e-8 和 2e-8 返回 Outside。因此不能仅凭顶点报告精度就把 GM 边界阈值缩到 session precision 的一半。510 点审计必须保留四方分类，并结合实际有限边界、拥有关系及有效精度判断。

首次编译失败、首次支持面 tag 子集断言失败及其原始退出码仍保留在同一审计目录；它们不属于成功对照。匹配支持面使用原生几何重合查询，不能用压印后 tag 必须不变的错误假设。

## 有误差界的数值判定

`scripts/ParasolidMassProperties.cs` 为原生积分结果保留每个分量的估计与 `PK_mass_bound_modulus_c` 返回的模量。volume/mass/periphery 使用各自模量，中心三分量共享中心模量，惯量九分量共享惯量模量。对每个分量，使用向外舍入计算所有报告区间内数值的归一化差下界与上界，分母仍为 `1+abs(reference)`，阈值仍为 1e-7。

- 上界严格小于阈值：Verified，仅表示在报告误差范围内可验证该数值精度。
- 下界大于或等于阈值：Different。
- 其余：Unresolved，不签署固定精度通过；区间相交不能提升为 Verified。

这不取代严格实体比较、完整 BODY_check、有限裁剪、材料和回滚验收。非法/非有限估计或模量直接失败；不通过缩放姿态、增大阈值或挑选分量消除差异。

`native-contract-bounds.{log,exit}` 实际外部退出 0 验证的是控制实验执行及断言，三个压印数值结果均为 Unresolved，不能将该进程成功说成 15 项数值精度已经通过。新建真实 2.1×2×2 对 2×2×2 block 判 Different；相同精确数值判 Verified；宽误差界包住两个不同点值仍判 Unresolved。命令同上，仅输出名改为 `imprint-bounds-v1`，对应原生输入/输出保存在 `temp_docs/native-acceptance-contract/imprint-bounds-v1/`。此增量尚未切换原 ShortEdge/看板生产合同，后续整套候选统一接入。
