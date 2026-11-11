# 协调决策与候选变更审计（2026-11-11）

## 结论

本轮确认 **7项方向、项目约束或证据真实性问题**，另记录 **2项工作范围与协调方式问题**。附表列出22组候选实现或验证缺陷。这三组数字用途不同，存在关联，不相加为“31次独立错误”，也不把208个候选提交全部视为错误。

最重要的责任在协调端：没有持续持有项目源码和架构约束，以局部数学审查通过代替了方案是否符合项目架构的判断；随后批准SharedPlane路线继续向生产接入发展。

本审计只读源码、Git历史和既有报告，没有修改算法、运行内核测试、执行新PK实验、删除分支或改变门户。

## 范围和计数口径

- 当前Orca Run：`run_14f1d4bbcc8d`，任务清单89项：85项completed，4项dispatched。
- 取得72个远端分支的固定head；基线main为`610e3ce844bf99c85af6069905f9f3deaab08133`。
- 盘点从2026-11-10T17:29:00Z开始、当前origin远端可达且不属于main的208个候选提交。这是提交盘点，不是208次逐项代码验收。
- 本轮定点阅读项目AGENTS、计算架构、局部建模架构、XT包说明，以及23份关键既有报告，并核查相关代码diff。
- 一次错误决策的后续修复、工具、证明和文档提交按一个决策链追踪，不重复计为多个架构错误。
- “协调者批准的方向”“worker产出的错误候选”“独立审查拒收”“修复后上线”分列。报告中的历史测试不是本轮新执行。
- 2026-11-11新增的全局“不得自行历史兼容”禁令不追溯认定此前行为违反该新禁令；此前额外的兼容工作仍可审计为范围选择。

## 事先已存在的项目约束

[计算架构](../../Computational_Kernel_Architecture.md) 第1节已经规定拓扑、几何、拥有关系、容差模型和公开API语义参考Parasolid，这是架构输入条件。第5节要求内部导数布局不改变Parasolid参数化、handedness和输出约定。

[项目AGENTS](../../../AGENTS.md) 同时要求：只做被请求的最小修改；脚本统一.NET 10 C#；项目文档使用相对路径；几何/B-rep/x_t相关功能要有自研导出XT、真实PK加载及同操作基准比较。少量内部数学正例不能替代这些条件。

## 7项已确认问题

| ID | 行为和证据 | 协调责任与实际影响 |
|---|---|---|
| E01 | 在未证明符合PK ICurve/CHART定义的前提下，批准`SharedPlaneGenerated`成为拟接入生产的修复路线。`b973732`新增MapKind、SharedPlaneNode和Hermite残差`N(T)·X-d(T)`，并向Scalar/Series/Interval/inverse及D2传播。 | **协调端明确批准并连续推进的方向问题。** 数学证据证明的是新方程局部性质，没有证明它就是PK模型定义。至`704bcaf`仍被bind/locator guard拦截，没有写入XT，未合main；不能宣称发生过新kind输出后PK拒读。现已停止正式接入。 |
| E02 | `ec393598`的QuadrantSinCos修改公共Circle/Ellipse和Taylor求值，按浮点除法整数判断强制精确象限。独立报告证明scalar/jet不在Interval包围内，且318个邻角误被吸附；真实PK单位圆控制不支持该全局推论。 | 我协调的worker产出错误候选；**独立复核拒收**，不能记为已接受修复。`ec393598`不是当前`704bcaf`祖先。没有证据表明此候选已部署。 |
| E03 | `780fce8`导出器在GM原始材料缺失时调用PK CreateRoof/Fillet作为fallback，固定ident=75并将其展示成GM来源。 | 违反原任务明确要求的真实双侧来源；候选被拒收。之后删除fallback，使用同次capture实际GM/PK XT及receipt。当前015展示资源通过独立来源检查；不把780的问题写成当前线上仍存在。 |
| E04 | 同一`780fce8`直接覆盖历史run的GM/PK before/after四份mesh，且方向信息丢失、坐标round6，新增线标注强于实际数据。 | 违反原任务“不改历史run”和真实U/V要求。与E03是不同的数据操作，证据属于同一候选链。历史mesh随后恢复；当前015的`web/data`与main基线无diff，新显示派生资源单独存放。 |
| E05 | `b98287b`新增受版本控制的`scripts/gm-private-env-setup.sh`，文档还明确使用用户目录路径。 | 与项目“所有脚本统一C#、文档相对路径”约束不一致。通用hook/外部工具便利性不是自动例外；未查到用户明确批准该语言/路径偏离。该分支未合main，但工具已用于环境准备。 |
| E06 | PK工具初次部署写入了Orca专用目录以外的用户全局配置，随后清理现场时又漏改会再次写全局的installer。 | 属于本Run的实际配置越界与整改遗漏，非仅理论风险。任务`task_cb4db843faf9`和`task_9555242e309a`的结果记录了Mac Codex/Pi等差量清理及installer修复。未输出凭证，不能据此推断凭证泄漏。用户后来明确授权写全局AGENTS记忆不属于该越界。 |
| E07 | PK设施矩阵曾将host-level通用probe成功推广成每种agent原生tools/call都已验证。 | 属于证据层级夸大。整改报告承认当时没有各agent原生tools/call证据，改为配置、发现、host probe、client call分层。后续真正调用记录不使此前错误声明自动正确。 |

### E01的实际投入

以`b973732^`至`704bcaf`为单一决策链，Git独立统计为：

| 类别 | 文件数 | 新增行 | 删除行 |
|---|---:|---:|---:|
| 生产src | 15 | 629 | 17 |
| scripts | 15 | 1391 | 14 |
| tests | 1 | 205 | 0 |
| docs | 16 | 1566 | 0 |
| 合计 | 47 | 3791 | 31 |

共16个提交，时间从2026-11-11 12:36:22+08:00至16:54:34+08:00；这只是提交时间跨度，不是可精确计费或独占工时。关联7个独立审查任务：core、residual成本、residual实现、39段覆盖、187前段、P/S续查、node-use。少量通用结论可能仍有价值，但这些投入不能算成复杂Boolean已完成的进展。

## 2项范围和协调方式问题

### M01：自行扩大历史兼容工作

已在实际代码中确认至少3处新机制：

- `b973732`的`FiniteProjectionSnapshot.cs`为旧自有v1快照解码并另设generated v2。
- `1d341a4`对旧benchmark report缺失字段回退。
- `eecab6d`等新增旧operation解释版本回退及双版本处理。

这些均发生在本次全局禁止指令之前，因此不以新禁令倒推违规；问题在于我把额外兼容义务纳入原型任务，增加了支线。保留原始证据可以在其固定历史版本上只读核查，不当然需要新代码继续读取所有旧格式。新的用户全局禁令已在协调端和当前q2 worker持久化。

当前有效的PK模型类型、真实PK读取要求与原型当前功能，不应被误称为“历史兼容”而删除。

### M02：以局部gate推进代替完整交付

同一核心任务长期等待越来越小的范围授权，7次独立审查支持了数学证据，却没有先检查这条方案是否符合PK原生模型。性能线最后没有持续开发负责人；两条旧文档代理任务及一条已退出调度任务仍显示未结算。89个任务中有大量合法UI、工具和缺陷修复，不能全部算成浪费，但任务多不代表完整建模目标接近完成。

## 附表：22组候选实现或验证缺陷

这些是本Run既有报告明确记录的缺陷或测量问题，未全部构成本轮架构决策错误，不与上表简单相加。修复状态仅按报告声明范围填写。

| ID | 候选问题 | 固定证据 | 后续状态 |
|---|---|---|---|
| B01 | fixed-X inverse混入输入点导数，错误Success区间不含真根 | b973独立review | ae3原probe窄修通过 |
| B02 | Prepare负ChartOffset在校验前越界抛异常 | b973独立review | ae3边界修复通过 |
| B03 | benchmark资格门未封闭steps/owner/check/timing集合，多种缺项仍qualified | 04c/f2独立review | v4限定范围接受 |
| B04 | 合法timing换序后按数组位置配GM/PK，真实ratio10报100 | a8/v3独立review | v4按owner join修复 |
| B05 | 初版operation状态重算使失败复活，缺return/缺primary可被升级 | 5d独立review | 后续共享层整改 |
| B06 | not-run优先于已知901/primary失败，公共validator接受矛盾 | 12e独立review | a2原输入窄复验通过 |
| B07 | 输入/输出hash getter借出live嵌套对象，可篡改后续记录 | 12e独立review | a2深拷贝窄修通过 |
| B08 | lifecycle ambient Context未恢复，manual fillet与实际API分属不同owner | bd7/a0独立review | a0受管源码链复验通过，非完整PK lifecycle |
| B09 | 显式numeric/null/object版本被当missing并fallback，伪资格通过 | a0/0935独立review | 0935原输入拒绝通过 |
| B10 | 父已有primary失败被后续cleanup child覆盖首因 | 2c06独立review | 0d原probe窄修通过 |
| B11 | Header reset后后续snapshot持续抢手动相机 | 780独立review | 后续UI修复 |
| B12 | 工具开关只改状态，真实工具几何仍留主mesh | 780独立review | 后续UI真实几何/像素检查通过 |
| B13 | 隐藏及换case后选择仍指向旧上下文 | 780独立review | 后续ownership/selection修复 |
| B14 | 新case在旧mesh上消耗首次fit，新mesh到达不再fit | d75独立review | 424及后续限定接受 |
| B15 | 黄色关键边部分可见区段点击命中普通边 | c710独立review | 53原probe和边界控制通过 |
| B16 | UV资产彼此hash正确，选择After仍能接受Before资源；path cache缺选择上下文 | f3独立review | 1eb原probe及冷热context控制通过 |
| B17 | 首次UV合体包正常，下轮fresh producer prepare丢全部15资源 | 3c融合独立review | 015两轮固定hash装配及实际自动timer轮通过 |
| B18 | 显式owned升级缺旧hook时降级fresh install，仍控制timer并写hook | d9独立review | 1b原probe拒绝且零副作用通过 |
| B19 | rollback只遍历已有meta绑定，runs空集合可通过，index200而manifest404 | host1b4独立review | 618原输入及独立CLI控制通过 |
| B20 | 清零诊断文档与实际state/dump实现不符、漏成功出口、统计全session却混称FIX范围 | 6bb结构独立review | 归因和适用范围已更正，旧95%具体因果未证 |
| B21 | S1 A侧实际旧v2 runner，B侧v4；单warm且负载突变，无法确认优化收益 | OptA 0ba报告 | 历史raw保留、未宣称收益、未进S2 |
| B22 | node-use原进程外部exit未保存，只有完成marker和内部结果 | d15独立review | 只接受内部数学证据，不将checker exit0补作原exit |

## 证据与现状

主要既有固定报告：

- [ec393审查](https://github.com/ThraceShah/project_gm_kernel/blob/272a51e4f71640028e9f5e11380857eacd63af84/docs/reviews/advanced-modeling/topology-event-framework-independent-review-ec393598.md)
- [780来源与历史mesh审查](https://github.com/ThraceShah/project_gm_kernel/blob/bd11830327589bbd01e6753d9fbc3522ea2e5f30/docs/web-operation-inspection-780-independent-review.md)
- [SharedPlane core实际设计](https://github.com/ThraceShah/project_gm_kernel/blob/b973732f1b0238a6e2a95cb631a31ae565f4e8df/docs/reviews/advanced-modeling/topology-event-framework-shared-plane-core.md)
- [主比较与数据时间审计](https://github.com/ThraceShah/project_gm_kernel/blob/c9b88aff710d48ff64f60ec1c151c8be680f3b84/docs/reviews/advanced-modeling/web-primary-comparison-audit-610e3ce.md)
- [015融合独立接受及原3c拒收](https://github.com/ThraceShah/project_gm_kernel/blob/f9ed821287c7bfb4e23963a10238c9de32a3854c/docs/reviews/web/inspection-reviewed-integration-independent/README.md)
- [d15执行状态缺口与下一范围](https://github.com/ThraceShah/project_gm_kernel/blob/ec82b850dd58a4bf30fa89d6bbbbb54467ccf77a/docs/reviews/advanced-modeling/topology-event-framework-node-use-independent-review-d15.md)

配置越界和clientcall夸大证据来自当前Run中任务`task_cb4db843faf9`、`task_9555242e309a`及`task_cfca050fc669`的已保存spec/result。它们涉及主机配置，不属于Git源码diff，本审计未重新改动这些主机配置。

当前main仍610；SharedPlane和ec393没有合入main。线上UI为已接受015，现有计算数据仍610/17案例。本轮没有证明“所有项目历史均无其他问题”，也没有复跑旧PK/内核实验；结论来自以上固定范围。

## 已立即落实的协调端约束

1. 持有完整Git只读参照并读取实际AGENTS/架构文件；每5分钟fetch远端refs，main只允许clean fast-forward。
2. 每次派发、架构决策、接受结果前再次同步，并核目标工作分支固定SHA/diff，不能只读聊天摘要。
3. 不在协调端构建、测试或实现GM代码。
4. 用户级禁止擅自历史兼容；GM的XT原生、PK类型和参数化约束写入用户级协调规则。
5. 所有实现worker保持待命；未删除实验、未回滚已接受功能，未继续SharedPlane持久化/绑定或输出路线。

后续纠正方案需要先与用户把原生模型及完整交付边界说清楚，再开始实现；不能以本次审计为由又启动一条长期基础设施支线。

