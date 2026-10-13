# 天顶之刃的可选友方移动

2026-10-14，Goal active，平台 **NOT READY**。本批补齐CN OGN·262/298原“只眩晕、不移动”的缺口，不提升整卡认证。

[中文当前回读](evidence/zenith-blade-movement-2026-10-14/source-readback.json)保存ID31504、完整源记录/hash与取得时间；[英文](evidence/zenith-blade-movement-2026-10-14/en-current.json)保存ogn-262-298的独立身份/文本，均为眩晕战场敌方单位后可选移动友方单位到该敌方单位所在战场。没有区域分母合并或编造单卡生效日期。[当前核心](evidence/zenith-blade-movement-2026-10-14/current-core.json)字节/hash仍一致，未来10月16日版本不提前应用；[精确条款](evidence/zenith-blade-movement-2026-10-14/core-clauses.json)包括355.4有效终点、355.5确认选择、359.3.e局部失效、359.3.f执行时引用、423.1眩晕。[FAQ](evidence/zenith-blade-movement-2026-10-14/vendetta-faq.json)只用于通用引用/复制规则，不补造单卡裁定。

敌方目标和可选友方移动目标在法术确认时选择，并保存世代；可不选择第二目标。共享目标范围与关系校验进入初始声明、费用预览、提示、重新选择和结算合法性。只对第一目标眩晕，然后用引用单位结算时的合法当前战场执行可选移动，沿既有移动观察者、附带装备、战斗与恢复流程；当前位置已是终点、移动禁令、非己方单位、错误顺序和失效引用不能绕过校验。敌方目标回手使相关引用移动失效；友方目标回手仍可使敌方目标受到眩晕。原目标位置不因失效后数组缩短而调换。

运行目录[1031条/830功能输入完全不变](evidence/zenith-blade-movement-2026-10-14/catalog-input-parity.json)。旧NO_MOVE效果名称被替换为共享可选移动名称，原方法/Theory行保持；[夹具逐字段对照](evidence/zenith-blade-movement-2026-10-14/fixture-input-parity.json)仅更改expected事件的effectKind，初始状态和全部输入命令完全相同，没有改写接受历史。

11项新增用例/6方法覆盖选择/放弃移动、声明/预览非法目标的状态不变、真实双方回手响应、横置单位移动与效应复制装备跟随、两次回响的独立移动目标，以及[当前薇古丝](evidence/zenith-blade-movement-2026-10-14/vex-source.json)真实进场技能产生的移动禁令；纯眩晕选择在禁令下仍合法。所有接受命令检查隐私、玩家/旁观恢复、冷恢复、去重与完整动作事件状态回放。当前位置引用移到另一战场、所有控制转移/防护、原签名牌资格/费用、重复眩晕观察者及全部其他移动消费者继续开放。

[中间失败](evidence/zenith-blade-movement-2026-10-14/intermediate-corrections.json)保存真实缺失和测试装配费用/验证脚本回响选择等错误。Godot验证脚本现实际操作每次重复的模式/目标控件，校验费用预览保留独立重复；产品控件本来支持该输入，没有改产品页面。

上一固定ebdef634的[完整CI](evidence/zenith-blade-movement-2026-10-14/fixed-ebdef634-ci.json)独立核对11,085/6,979、2059.66秒全部通过，旧11,062/6,967保留+23/12，110配对/seed424242及最终哈希完全相同，必需网络与Windows/macOS成功。[真实SQL](evidence/zenith-blade-movement-2026-10-14/fixed-ebdef634-sql.json)保存官方开局与四条56命令/148事件/60冷恢复/4篡改拒绝；不覆盖后续031a6ecd或本批改动。

[本批扩大相邻检查](evidence/zenith-blade-movement-2026-10-14/adjacent-focus.json)590项/399方法通过；[原生最终读回](evidence/zenith-blade-movement-2026-10-14/native-replay.json)7路径34条实际生产控件命令、34次冷恢复/去重和完整回放通过，含独立回响目标。无窗口控件运行不替代可见双客户端。[本批check](evidence/zenith-blade-movement-2026-10-14/check-parity.json)10,990/6,853、164.25秒全部通过，原10,979/6,847保留、新增11/6，2,239输入冻结不变；Python27和真实专用SQL五项smoke通过，实例已停止。[完整计划](evidence/zenith-blade-movement-2026-10-14/full-plan-parity.json)6,994方法/预期11,139用例，143原对局方法及110配对/seed424242保留，目录和牌组输入未变；新固定full须独立终态。全源逐条语义、可见双客户端、用户Windows、完整SQL并发/故障切换及英文8项来源缺口仍开放，不完成总Goal。

后续[驭风而行/移动许可审计](RIDE_THE_WIND_MOVEMENT_AUDIT_2026-10-14.md)按CN359.3.e.6纠正本批过严的移动禁令确认：被禁止移动的友方单位仍可被选择，结算只忽略移动，敌方眩晕仍执行。旧aa8d34ca与当时34命令收据保持原版本范围，不能当作该许可语义正确的证明。
