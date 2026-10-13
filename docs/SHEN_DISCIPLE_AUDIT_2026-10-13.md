# 慎的弟子与恰好一个己方其他单位

2026-10-13，Goal active，平台 **NOT READY**。本批新增当前非预览中文印次 VEN·117，并扩展共享条件关键词层；注册、限定运行结果不提升整卡或全量规则清单。

[中文回读](evidence/shen-disciple-2026-10-13/source-readback.json)固定 API、取得时间、ID37817、完整记录及SHA-256；[英文精确印次](evidence/shen-disciple-2026-10-13/en-readback.json)为ven-117-166 / VEN-117/166，地区语言与中文无分母合并。两者均描述同战场恰有一个己方其他单位时获得坚守3。[官方发布](evidence/shen-disciple-2026-10-13/vendetta-release.json)记录化神争锋中英文7月31日发售，单卡发布/生效日期未知仍明确保留。[本批当前中文核心](evidence/shen-disciple-2026-10-13/current-core.json)字节回读与已有哈希一致；[当前FAQ](evidence/shen-disciple-2026-10-13/vendetta-faq.json)没有为未提及的单卡补造裁定，未来10月16日核心不提前应用。[条款](evidence/shen-disciple-2026-10-13/core-clauses.json)按原精确身份保留。

[五条原基线失败](evidence/shen-disciple-2026-10-13/red-evidence.json)证明运行目录和解析器缺少该印次/条件。现新增来源明确的记录，[原1024条逐记录不变](evidence/shen-disciple-2026-10-13/catalog-input-parity.json)，运行目录变为1025条/826个功能单元。目录数量及家族统计按实际新增一项更新，原方法和Theory行保留，RepresentativeRulePass等注册派生标签继续不作为语义认证。

卡面条件进入静态技能spec，并由既有RuleDerivedKeywords层重新计算。计数排除来源本身，只统计来源当前真实战场上的正面朝上单位，以当前控制者为准；基地、其他位置、对手、隐蔽待命和非单位物体不计入。印刷原文和复制实例共用条件求值。数字关键词按独立实例相加，来源/伙伴移动、控制变化或武装离开会撤销不再成立的授予，保留独立关键词来源。坚守3对防守方增加当前战力和实际战斗伤害，对进攻方不增加战力。

18个新增用例覆盖原文本解析/当前记录、0/1/2伙伴的真实移动、手牌与后续回合待命打出、进攻/防守伤害分配和战斗收尾、实际恶意收购改变来源或伙伴控制、真实隐蔽待命不计数、真实一份/两份效应复制的坚守6/9及返回武装后的撤销、恢复篡改拒绝。另比较增加印次前后全部11套实际预组输入的完整JSON，保持原110配对的牌组/种子基础。每条命令进行公开隐私、冷恢复、去重和最终完整动作/事件回放。

[Godot生产控件读回](evidence/shen-disciple-2026-10-13/native-replay.json)为15路径55条接受命令、55次冷恢复/去重和完整回放。使用现有移动多选、战斗参与者、伤害分配、手牌/待命和支付控件；仅增加验证脚本的控件调用，没有修改产品页面。无窗口证据不替代可见双客户端或用户Windows。第一次脚本找不到动态按钮、默认未选完多参与者、以及参与者顺序与服务器索引不同的中间失败不计作通过；最终命令逐一由服务端验证。[相邻focus](evidence/shen-disciple-2026-10-13/adjacent-focus.json)444项通过。首次check的10,913项中10,911通过，两条旧目录保护测试因1024→1025及历史增量15→16的统计断言失败；[失败收据](evidence/shen-disciple-2026-10-13/failed-first-check.json)保留，原1009记录逐条不变和全部断言继续检查。修正实际新增后的统计并定向验证后，重新冻结最终输入，执行必要的修复后check；不能将首次check记为通过。固定新提交完整CI另行记录。

上一固定实现3a20a0ea的[完整CI38406008898](https://github.com/DDDHL0704/riftbound/actions/runs/38406008898)已终态成功，[独立核对](evidence/shen-disciple-2026-10-13/fixed-3a20a0ea-ci.json)为11,034用例/6,955方法、2854.80秒，原96716eee的10,970/6,929全部保留，新增64/26，110配对/种子424242完整，全部110最终哈希的变化由该版新增公开历史/状态记录保留，不当作输入删减。[实际CI SQL](evidence/shen-disciple-2026-10-13/fixed-3a20a0ea-sql.json)保留官方开局8命令/5检查点及四条56命令/148事件/60冷恢复、四次篡改拒绝；必需网络与双平台桌面步骤成功。仅认证该固定提交和具体门禁，当前未提交实现不在其范围内。

旧32b7b581的[CI38402687822](https://github.com/DDDHL0704/riftbound/actions/runs/38402687822)仍为失败：[收据](evidence/shen-disciple-2026-10-13/fixed-32b7b581-ci.json)记录普通规则10,881全通过、games-2的49全通过、games-4的52中50通过/同两条旧备战驱动失败，其他分片中断，SQL/网络跳过，双平台桌面通过，后续修复不回写该旧失败。

[计数消费者清单](evidence/shen-disciple-2026-10-13/exact-count-consumers.json)另保留六个慎的据守抽牌/得分与神圣守护者伤害许可条目，不能将本批条件关键词结论扩大到它们。全量卡牌/规则/FAQ/来源缺口、全部控制/复制/重进场、可见双客户端/用户Windows、完整SQL得分/并发/故障切换仍开放；平台NOT READY。

[最终修复后check](evidence/shen-disciple-2026-10-13/check-parity.json)为 **10,913用例/6,826方法，152.33秒，零失败或跳过**，原10,895/6,816全部保留，新增18用例/10方法，2,235个源文件/测试/数据及原生脚本输入不变，五项真实专用SQL smoke和Python27通过，专用数据库已停止。[完整计划](evidence/shen-disciple-2026-10-13/full-plan-parity.json)为6,967方法、预期11,062用例，143原完整对局及110配对/种子、11套具体牌组输入保留；新固定提交完整CI仍须终态。

随后固定a0b3c004的[完整CI38408640057](https://github.com/DDDHL0704/riftbound/actions/runs/38408640057)也终态成功：[独立收据](evidence/shen-disciple-2026-10-13/fixed-a0b3c004-ci.json)为11,044用例/6,957方法、2831.92秒，原96716eee的10,970/6,929保留、新增74/28及110配对齐全，必需网络与双平台桌面成功；[真实SQL收据](evidence/shen-disciple-2026-10-13/fixed-a0b3c004-sql.json)保留原官方开局和四条历史恢复门禁。该成功只覆盖a0b3c004，不认证本批或全量语义。
