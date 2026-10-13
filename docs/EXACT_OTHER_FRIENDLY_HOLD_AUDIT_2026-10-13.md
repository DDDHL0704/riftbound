# 据守时恰好一个其他友方单位

2026-10-13，Goal active，平台 **NOT READY**。本批范围为CN VEN·042、042a、170的条件据守抽牌，以及VEN·138、138a的条件据守得分，扩展既有共享Hold捕获/结算。

[中文官方回读](evidence/exact-other-friendly-hold-2026-10-13/source-readback.json)保存五条非预览记录、ID、时间及逐记录hash；[英文](evidence/exact-other-friendly-hold-2026-10-13/en-current.json)保存ven-042-166、ven-170-166和ven-138-166的独立身份/文本，不合并地区分母。英文数量仍报告1389/返回1381，八项缺口不消失。[本批核心](evidence/exact-other-friendly-hold-2026-10-13/current-core.json)现场字节/hash相同，未来10月16日版本不提前应用；[精确条款](evidence/exact-other-friendly-hold-2026-10-13/core-clauses.json)包括383.2.a.1和359.3.f，附加条件在触发时检查，之后响应离场不使已触发技能失效。[现行FAQ](evidence/exact-other-friendly-hold-2026-10-13/vendetta-faq.json)只用于通用引用/复制，不补造单卡QA。[发布依据](evidence/exact-other-friendly-hold-2026-10-13/vendetta-release.json)与未知单卡生效日期分别保留。

[目录记录不变](evidence/exact-other-friendly-hold-2026-10-13/catalog-input-parity.json)保留原1026记录，只添加五条，现1031条/830功能单元；138/138a因提醒文字不同仍是各自功能输入，不合并或改写官方文字。原注册统计保护方法及所有断言保留，仅按实际新增更新数量；注册/RepresentativeRulePass不等于语义认证。

解析器保存精确其他友方单位数量、抽牌/分数参数。Hold时使用当前真实战场、正面朝上UNIT与当前控制者，排除自身和非当前位置对象，条件成立才生成独立触发。原文、效应复制、眼镜当前复制面共用文字实例枚举，每个实例独立捕获来源世代、文字取得时间/版本及武装绑定；蓝色哨兵复制沿原共享增幅流程。抽牌与额外得分沿既有Hold结算，不因后来来源/伙伴回手或武装脱离重新判断条件。

新增路径覆盖五印次0/1/2伙伴、对手回合不触发、响应来源/伙伴回手、一份/两份复制及独立排序、复制来源回手后武装脱离仍结算、普通据守分之后额外得分获胜、恢复伪造字段拒绝，以及11套实际预组输入JSON不变。每条接受命令经隐私、玩家/旁观恢复、冷恢复、去重和完整动作事件状态回放。历史来源回手时的旧世代公开ID经独立印次/数量/效果/实例检查排除，当前私有手牌内容检查继续保留；原隐私断言没有删除。

复制据守的真实响应发现共享ORDER_TRIGGERS将非空结算链标为NeutralOpen，阻止合法优先权玩家使用符文。现改为相应NeutralClosed/SpellDuelClosed；与既有其他触发入链流程一致，排序/费用与相邻检查须完整验证。[中间失败](evidence/exact-other-friendly-hold-2026-10-13/intermediate-corrections.json)分开保存这个真实引擎错误和测试玩家/费用/统计等错误。Godot排序验证脚本使用真实上下移动按钮选择所需排列，取消只适用于反序的旧脚本假设；不修改产品页面。

上一固定37971a42的[完整CI](evidence/exact-other-friendly-hold-2026-10-13/fixed-37971a42-ci.json)独立核对11,062/6,967、2900.61秒，旧11,044/6,957全保留，新增18/10；seed424242、110配对及最终哈希完全相同，网络及Windows/macOS步骤成功。[真实SQL](evidence/exact-other-friendly-hold-2026-10-13/fixed-37971a42-sql.json)保存官方开局和四条56命令/148事件/60冷恢复/4篡改拒绝。它不覆盖后续ebdef634或本批工作区。

[扩大相邻检查](evidence/exact-other-friendly-hold-2026-10-13/adjacent-focus.json)668项/451方法通过，新增43用例/9方法；[原生读回](evidence/exact-other-friendly-hold-2026-10-13/native-replay.json)37路径199条生产控件命令、199次冷恢复/去重和完整回放。无窗口运行不替代可见双客户端。[本批check](evidence/exact-other-friendly-hold-2026-10-13/check-parity.json)10,979/6,847、165.62秒全部通过，原10,936/6,838保留、新增43/9，2,237冻结输入不变；Python27和真实专用SQL五项smoke通过，实例已停止。[完整计划](evidence/exact-other-friendly-hold-2026-10-13/full-plan-parity.json)6,988方法/预期11,128用例、143对局与110配对/seed424242及11套具体输入保留；新固定full须独立终态。整卡手牌打出/全部控制与复制重进场组合、全源逐条审查、可见双客户端、用户Windows和完整SQL并发/故障切换仍开放，不完成总Goal。
