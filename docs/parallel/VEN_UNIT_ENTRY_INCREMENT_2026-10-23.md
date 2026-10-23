# VEN 三张普通单位与私有来源投影增量（L2）

CN/zh-CN，API原行及卡图于2026-10-23复核；[官方接口](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)三行与旧原件逐字段一致，当前规则入口仍引用同PDF。核心2026-10-16生效，SHA256 `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`；383.4.a入场后打出技能、432燃尽、815壁垒、124新对象代际及卡面原文。个别印次发布/生效日期仍未知；不合并区外印次。
| 功能身份 / 精确印次 | 完整API原响应SHA256 | 行为、独立语义与状态 |
|---|---|---|
| 战地乐团 / VEN·026/166 SC / API37714 | `4e69b81192b0e160db26d9512309f0dfb00bef75f96810574a92e78a2b6b5eac` | 4费3战力；打出后必选任一公开单位本回合+3、己敌/自身/双方战场、法盾支付与放弃、目标/来源离场、期限和并发触发；语义通过待集成 |
| 云端亚龙 / VEN·048/166 SC / API37739 | `16cdb25bb612e3285a475619f4f21e100928a160477ecf52932facc8fbfed540` | 6费5战力/龙；入场后抽一、隐区、来源离场、首次/连续燃尽与终局、重入双技能和并发触发；语义通过待集成 |
| 龙角勇士 / VEN·118/166 SC / API37818 | `57c68de9fadacb233e003a8d49eaa3989348a53aa92d219aaa18b11dec0796b4` | 6费6战力/壁垒；普通出牌合法/非法、基地/受控战场入场、致命伤害优先与多个壁垒可选先后；语义通过待集成 |

L2共享接点经①分配：普通已确认技能在当前来源进入对手手牌/牌库后屏蔽该ID，保留原公开卡面/效果；独立玩家恢复校验拒绝source/face/effect/visibility/controller/playAbility篡改，旁观者期望独立。原FullGame守卫仅增加一个受审阅helper调用，以独立CN原文、精确旧Finalized/Resolved/POWER身份及完整正向算术核准单个已结算公开修正出处；20类伪造/重复与其他JSON字段泄漏反例、协调篡改的原日志回放拒绝保留。没有改支付、战斗或销毁替代共享实现。源码为本提交；基线4aabf78bd8da3c9ddad3e13e8de23f1e430f2b56。
验证：`final-affected-v9`实际648例/375方法全过、零跳过；新消费者/隐区/恢复、原相邻目录/流转/目标/永久牌确认/雷恩加尔/放逐/燃尽/德莱文恢复并集。全部接受命令走协议映射、真实MatchSession串行去重及每原前缀恢复、完整动作/事件/状态回放；拒绝核对状态/双方视图/提示不变及零事件。原1040整行/顺序和全部旧方法/Theory数据保留；新增目录1043仅为登记。旧35例/511例/55例等原失败与夹具/统计/时序校正理由保留，未追认为成功。
原件：本树`var/qa/ven-unit-entry-20261023/`的final-affected-v9、live-source-check.json及preserved-failures-and-focused-results.json；完整API/卡图和最早目录红测复用旧树`/Users/dinghaolin/MyProjects/.worktrees/riftbound-parallel-official-audit-20261023/var/qa/ven-unit-entry-basic-20261023/`，不复制新全库证据包。粗略墙钟约40分钟，开发/接口审阅、构建/测试和整理重叠，未分表计时；独立构建/TRX时间可取回。净整卡认证0；当前固定候选check/full、受影响SQL/API/真实重启与统一认证视图由①集中安排，后端Goal ACTIVE，产品未READY。
