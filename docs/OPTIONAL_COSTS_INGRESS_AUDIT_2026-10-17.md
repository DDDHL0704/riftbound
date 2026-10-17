# 可选费用原始输入、日志与恢复契约

2026-10-17，CN/zh-CN、截至2026-10-17、核心2026-10-16生效。[issue52](https://github.com/DDDHL0704/riftbound/issues/52)保持OPEN。本批是协议入口修复，不提升整卡或官方条款认证。[来源及精确范围](evidence/optional-costs-ingress-2026-10-17/scope-and-source-manifest.json)保留前次官方取得时间和manifest，不冒称本协议批次重新下载了全量来源。

真实规则会话能接受非法raw optionalCosts，独立恢复守卫随后拒绝同一历史。原接受记录保留，不迁移旧开发局，不改写历史或放宽恢复。当前在SubmitAsync既有重试冲突检查后、状态/陈旧提示检查和任何journal写入前，统一检查八类命令的optionalCosts。显式null、非数组、错误/空白/首尾空格元素、重复属性、空格属性名均拒绝为INVALID_PAYLOAD。省略、空数组和规范未知费用ID仍进入原规则。默认.NET producer仅省略null，保留显式空数组和费用ID。

八类为PLAY_CARD、ACTIVATE_ABILITY、LEGEND_ACT、HIDE_CARD、REVEAL_CARD、MOVE_UNIT、ASSEMBLE_EQUIPMENT、DECLARE_BATTLE。其他raw字段另OPEN，不扩展通用DSL，不改变独立恢复守卫或旧mapper的非严格数组行为。

[真实先红后绿](evidence/optional-costs-ingress-2026-10-17/focus.json)：未改生产源码时185例中152失败/33通过；最终185例/7方法全部通过，旧映射、原生序列化、牌史与恢复相邻1858例全部通过。首轮编译错误及两次误放入口钩子的草稿与日志保留，均不计为行为通过。开发初期[441份原Conformance源码全部相同](evidence/optional-costs-ingress-2026-10-17/source-and-test-retention.json)；随后发现原完整对局手工MOVE_UNIT请求会写optionalCosts:null，现仅补一个空数组合并表达式。[最终441份中440份逐字节相同](evidence/optional-costs-ingress-2026-10-17/final-source-and-test-retention.json)，全部原方法、Theory行、143完整对局、110双向配对、原种子和胜利/回放断言不变。原26个SQL家族调用顺序保留，默认完整SQL新增可选费用家族；有限selector不代替完整SQL。

[独立实际PostgreSQL专项](evidence/optional-costs-ingress-2026-10-17/sql-scoped-receipt.json)：两个新房间保留来源显式种子0，八条普通开局动作使用各新房间当前提示。共272次错误费用请求逐条INVALID_PAYLOAD，实际七张表完整行摘要不变；合法省略/空数组出牌、冷注册表、恢复后的原下一条命令与完整原始行动/事件/状态回放均通过。首轮新房间提示身份错误保留，不计通过。不作新生产随机种子认证。

[两条真实认证SignalR连接](evidence/optional-costs-ingress-2026-10-17/hub-open-receipt.json)以新私密生产种子开局，120次错误格式均拒绝且不写命令/事件/状态/视图；默认typed出牌实际省略null并接受。[同一发布文件API冷启动后](evidence/optional-costs-ingress-2026-10-17/hub-reconnect-receipt.json)重新认证/重连，原状态与完整历史精确相同，八类错误费用继续拒绝，原下一条PASS_PRIORITY通过并完整回放。API停前两条连接已正常关闭，不把本次冷启动称为活跃连接掉线验收。

在线首轮误选含中国区禁卡的预组、第二轮探针错用第二位调度玩家，两次服务端正确拒绝均保留；随后继续同一第二轮房间，不删除其有效MULLIGAN拒绝。[计数更正](evidence/optional-costs-ingress-2026-10-17/online-count-clarification.json)：冷启动前10条总命令=9接受+1原调度拒绝、28事件；后11条=10接受+1拒绝、29事件。helper stdout误把总命令叫作接受数量，原文保留，JSON实际计数优先。没有错误费用行。

[完整SQL默认入口实际86路径全部通过](evidence/optional-costs-ingress-2026-10-17/complete-sql-summary.json)：原84路径加本批两个房间，未使用有限selector，固定发布字节至结束无漂移。[当前原生网络类](evidence/optional-costs-ingress-2026-10-17/native-network-current.json)经过实际断线代理，认证/房间恢复/新快照及错误令牌保护通过，15次跨身份请求全部拒绝、零私密消息；此项不代替可见GUI或Windows。

[唯一check](evidence/optional-costs-ingress-2026-10-17/check-parity-and-inputs.json)实际12295例/7281方法、601.31秒、零失败/跳过，原12110例/7274方法多重集合精确保留，12238份输入至结束无漂移，55项Python工具通过。runner的caseParityVerified:false原值保留，另有独立精确集合核对。check完成后才修正测试helper的一个表达式，因此不冒称该check已覆盖最终helper。

首轮原完整对局组出现10条失败，独立原all-units-aura真实红例明确在移动步骤被INVALID_PAYLOAD拒绝。[旧组在保留失败及原冻结输入后中断](evidence/optional-costs-ingress-2026-10-17/affected-originals-v1-interruption.json)，不计全部110完成或通过。[单点编码修正](evidence/optional-costs-ingress-2026-10-17/original-move-producer-correction.json)后，同一39原方法/全部110配对正在隔离冻结输入重跑；不进行第二次check。新固定提交完整CI及本候选可见双客户端、原生待选择/待结算冷恢复、Windows实机均OPEN。旧ed47命令/230事件、正常8:0及断线完整回放仍只属旧候选。

[共享输入变化](evidence/optional-costs-ingress-2026-10-17/prior-context-input-updates.json)明确重开18上下文/25pin；[后续编码修正另登记一个验证pin](evidence/optional-costs-ingress-2026-10-17/prior-context-input-updates-revision2.json)，119旧审查字典不变，不把旧CI改称当前通过。[统一义务](evidence/optional-costs-ingress-2026-10-17/obligation-update.json)保留原16542项，另登记八个精确消费者门禁得到16550项，分别记录语义、实现、测试、固定CI和产品状态。

[全量来源再取得及单独影响分析](evidence/source-reconfirmation-2026-10-17/source-impact.json)：CN接口16页/1504个精确ID全部字段未变；EN分页、整表和页面SSR返回1408个精确ID，仍报告1416，8项缺口保留。1382条未变，26条均仅flags从New标记变为null，其他返回字段包括卡号、文本与图片URI不变；没有重取全部图片字节，不能据此称图像内容不变，也不从New标记推断发布日期/生效日期。新版本作为独立DELTA:3登记，旧记录、来源分母、运行目录及119语义审查保留；当前统一义务16551项。全部来源缺口和整卡/条款语义仍OPEN；Goal active / NOT READY。
