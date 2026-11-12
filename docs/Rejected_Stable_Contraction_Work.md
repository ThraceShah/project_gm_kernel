# 未保留逐位不变参数盒计算复用实验

Spline-only计算957d49d、Native d7c281af，94项定向原0；扩展到原通用Joint后的计算67553ed3173a1698c3d3c4f9e1d856069dbe7861、Native d4f5d7502a9063dd6e2939a653311bac8b9ba792，115项定向原0。开关对照证实所有六次映射及9项包围/导数、conic witness仍逐位一致。它们保留为正确性/重复输入事实，不构成保留性能实现的依据。

一次每源六fresh pair的NURBS开发对照，固定17eedbf/Native116→957d49d，暖GM中位517.4336→484.5941ms（−6.347%）；全部五暖基线604.7651/517.4336/523.7155/516.0768/504.5839，候选513.5192/490.6221/484.5941/460.7547/459.2092，冷1130.8982/927.9022。Jac每rep4179→2979，其他原root/SSI/winding/budget保持。

基于256bb9b原路径shortedge 12450次收缩/3172次全部盒不变事实扩展后，一次每源六fresh pair的standard shortedge开发对照，固定17eedbf→67553ed，暖GM中位539.6681→538.1180ms（−0.287%）；全部五暖基线643.0777/539.6681/541.8845/516.8719/496.6565，候选692.3165/547.5572/538.1180/510.1785/513.0365，冷1446.1287/1328.3325。没有值得保留的完整操作收益，停止本实验；不追加全suite/正式ABBA或微审，不把减少的Jac调用当CPU或预签收益。

两次开发分别属于其固定计算版本；不将Spline-only时间冒称通用版本时间。当前helper与真实PK保持，所有24pair/48API原0，四进程及各runner原2、Unresolved/Missing保留。冷/暖、PK时间及原件SHA在 `temp_docs/stable-contraction/development-comparison.json` 和 `shortedge-development-comparison.json`；对应四个原runner目录dev-baseline/dev-candidate/std-baseline/std-candidate、源清单/实际loaded DLL/PDB、日志、Capture/XT均保留。

该实验不进入正式候选，未独立接受、合main或发布。已接受185原件不动。后续从17eedbf另判断真正的原生多项式/有理导数计算需求，不沿用本实验或4e5b584/cut950已拒实现。
