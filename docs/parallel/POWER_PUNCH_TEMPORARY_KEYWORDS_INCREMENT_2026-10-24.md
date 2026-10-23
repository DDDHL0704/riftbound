# N8 强能冲拳临时强攻与游走消费者

- 基线 `d93d2b1f19beef5b7936c9d9ff74c7f5b77c750b`；复用已审C6机制和N5 `f11a7871`的测试helper，①下一批先带N5；不接管C7共享方法。
- 精确CN34533 / UNL-010/219；2026-10-23T18:44:50Z重取[官方API](https://lol-api.playloltcg.com/xcx/card/searchCardCraftWeb)，记录SHA4016499206006f03c8a76762ca336e7619b491589f5322caaa1cd66d6701c3cc，原响应4b69080c222826ecaf9a36cd2b4e33aa2469dd6ca0b2269ee9efd0f395366a70，目视卡图26c0bfb5b7225290a11a87a4ae1aea85e05a6dad6e342fa634e5d0e2ef8554f4；1法力1红、迅捷、本回合强攻2与游走，errata为空，记录未提供出版/生效日。
- 复用现行CN核心SHA d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11 的807.1.c/d.1、807.2/3、810.1/2：强攻是进攻身份期间的被动战力贡献，各来源相加；游走只扩展标准移动权限，多次不叠加，不提供额外移动或活跃机会。
- 仅注册把OVERWHELM_2与施放当下条件+2改为既有COMBAT_KEYWORD:强攻2；游走/费用/权限不改，无Core/payment/recovery机制变更。旧EffectKind保持，原目录文件和有效内容均不改。
- 原v1 11例中2个旧预期通过、9个新来源/真实战斗例失败；提前施放预期5/7（加光环6/8）实际3（加光环4），对决中施放虽有效值偶合但储存战力错为4/6。全部原件留存。
- v2定向66例=64 PASS/2 FAIL；校准旧fixture事件ID与新游走用例遗漏的真实法术对决关闭后，v3只复验该2例，2/2 PASS。两批实际Engine/catalog/authority身份一致，非一次66例全绿；未重复其余无变化用例。
- 新8方法31例：真实Poro打出/独立强攻×提前或对决中×1/2张×Farron光环、实际双边伤害分配、双方/休眠目标、本回合游走移动及到期拒绝、反制两效果、回手前后代际、12种费用/时机/来源/目标拒绝；原方法/Theory均保留。
- 八条原战斗初态仅当前RuleInputs不同、语义命令仅prompt身份不同；旧fixture初态/命令/身份不变，2个旧方法仅校准预期。18份不同实际路径原journal共181命令705事件，逐前缀内存恢复/幂等/拒绝不变/隐藏检查及原日志回放；不称JSON冷进程或真实SQL/API验收。
- 原件 `var/qa/power-punch-temporary-assault-20261024/`，含来源、原红、v2/v3、final-source-test-receipt.json；实际限定RuleInputs 361a4b96b2b1849e08473965b4a218cb45e9fb3ebf04cf7092e4d3fd6b64fcfe，有效目录a62b4edb295f738ea027a8c911e4975759eee9dc1163063cb5dbdf1e3fbbfcf1，原文件a7a917729b7ed1431b158d89392228f135000ccec4504b3a23e4d2da6ad4c158。
- L1既有机制消费者；CI注册路径仍保守full、下一批待①固定候选Actions及所需后端门禁，本机未新跑重型。五个未跟踪C7消费者红测试不属于此提交，待已审共享机制后独立复验。
- 净整卡认证0，1740印次/14293条款及未知功能分母不改；后端/产品未完成，Goal ACTIVE。
