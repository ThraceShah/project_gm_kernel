# OCCT Core Subset

来源：Open CASCADE Technology `V7_9_3` 官方源码归档。

保留范围：

- `FoundationClasses`：`TKernel`、`TKMath`
- `ModelingData`：`TKG2d`、`TKG3d`、`TKGeomBase`、`TKBRep`
- `ModelingAlgorithms`：`TKGeomAlgo`、`TKTopAlgo`、`TKPrim`、`TKBO`、`TKBool`、`TKHLR`、`TKFillet`、`TKOffset`、`TKFeat`、`TKMesh`、`TKXMesh`、`TKShHealing`

目录说明：

- `adm/MODULES`：保留官方模块划分定义
- `src/<Toolkit>/PACKAGES`：保留 toolkit 到 package 的官方映射
- `src/<Toolkit>/EXTERNLIB`：保留 toolkit 依赖定义
- `src/<Package>`：保留对应 package 的头文件与源文件

明确排除：

- DataExchange
- Visualization
- Draw Test Harness
- OCAF / ApplicationFramework
- Samples / Tests / Tools
