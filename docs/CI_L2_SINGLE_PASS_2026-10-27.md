# L2 单次执行与六片验证

阶段A从main `1a84888a`独立实现，不包含尚未合入的S7包。准备任务只构建一次Conformance及其API依赖，使用同一固定SHA的程序集发现完整L2方法；两条原完整对局smoke及全部原Theory行保留，只有既有18个精确L3方法和原完整矩阵留在夜间。

普通VSTest发现不能展开部分不可序列化MemberData。独立清单工具只枚举这些方法的数据，不执行测试体，使用项目现有xUnit 2.9.2格式器记录每个参数行。参数行清单、发现原文、构建日志、六个实载模块的Info/MVID/SHA和完整bundle清单均保留。清单枚举失败或实际行不符即失败，不使用旧TRX补新候选应测行。

按方法历史耗时LPT分配六个runner，新方法默认一秒；权重仅影响调度，不能决定成员、豁免或通过。六片从同一已构建bundle直接运行，不重新编译。每片验证精确checkout、同run/attempt、SDK、全部文件SHA及六模块身份，保存原TRX/日志/发现清单和原始参数行映射。`fail-fast=false`；缺片、重片、漏方法/行、重行、缺计数器、失败、skip、错SHA/程序集均阻断。

targeted是同批原始TRX的覆盖校验视图，不是第二次执行。当前尚无审明的依赖闭包，明确回退完整L2；不靠文件名、五大域关键词或“仅文档”缩小必测集。聚合重新校验该视图的每行原件出处，保留名称`L2 required`及现有Actions15368/strict/admin/审批0保护。

③的完整来源清单工具`a2130d55`普通纳入：每个新候选重新按Git源码声明和本次真实发现生成范围，并与本次构建的SHA/六模块/发现哈希绑定。source、targeted、rule只在同批原TRX上派生视图，不各跑一次；原18个L3方法和完整矩阵执行归属不变。550/559只是两个旧输入的分类参考，当前不使用固定profile清单过滤新输入；语法helper关系不是完整语义依赖图，未选或未知方法仍在完整L2。

基线原件为run40475671761、实际synthetic914f：8339方法/16129例，targeted6150方法/11335例的精确case multiset全部包含在suite。历史suite81.8分钟、targeted87.9分钟。20–30分钟仅为目标，真实耗时以各次固定输入原记录为准。分别记录墙钟、全部job runner时间、每片行并集与原始产物，不把原绿灯改绑到新SHA。

阶段B单独处理PR→main复用。Git tree相同不证明运行时等价：`RuleInputIdentity.Capture`使用Engine/CardCatalog/Contracts的实载MVID构造ImplementationSha256，恢复安全依赖它；SDK默认把SourceRevisionId放入InformationalVersion。不能关闭该属性、改写历史或放宽身份校验来命中缓存。缺原件、环境/依赖/事件/构建身份不等价或证据过期时正常执行。阶段A不实现跨SHA复用，也不启用当前个人仓库不具备的原生merge queue。

实际比较已确认原PR914f/run40475671761与main1a848/run40500054422的tree/sourceIndex、脚本/配置/package pins相同，日志安装版本均含SDK10.0.403/8.0.427；但Conformance DLL SHA分别为`1f38a0cc…`与`67c4cb43…`，Git/event SHA及ref也不同。两个旧run六模块MVID/Info与完整restore图未留存为可比较原件，不声称已证明相等。本次判定为正常main执行，未接入tree-only复用。可取回比较、两份plan和原环境日志索引：ignored `var/qa/ci-pr-main-equivalence-20261027/equivalence-decision-v1.json`；[SDK行为依据](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link)。
