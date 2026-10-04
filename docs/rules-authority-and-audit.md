# 规则权威与重审协议

2026-10-04 原生牌桌迭代补充：场景复位只保留房间、座位与 tick，消除旧战场记录造成的 WAIT；开发展示符文改用官方目录中 `OGN·007/298`（炽烈符文、rune、red），增加两次回合推进后的实际印刷符能支付回归。这是场景与展示修复，不扩张规则覆盖声明。验证 49 项针对性、9344 项全量，均通过；详情见 `docs/NATIVE_TABLE_REBUILD_2026-10-04.md`。

2026-10-04 统一费用增量：CN 356.4.e/f 的减费下限与额外费用先后已按官方 PDF 示例补回归；预览、提示和提交使用共享费用计算。`PlayCostPreviewTests` 记录修复前失败与修复后验证；全量 9339/9339。证据见 [统一出牌实施记录](UNIFIED_PLAY_ITERATION_2026-10-04.md)。此项仅覆盖现有费用族，不扩大为全部费用、常驻牌时机或效果再次打出的完成声明。


2026-10-04 卡面符能补充：中国区 131.3 / 135.2.e.5-6 的普通出牌卡面基础符能、多特性总额分配和彩虹支付进入共享 PrintedPowerCostRules / PaymentCostRules。多枚符文和临时资源可共同补足缺口，基础费用与可选费用分开记录；非法/不足费用保持状态不变。新增 19 项，最终全量 9328/9328（printed-power-delivery-verified.trx），真实原生 UI 请求另经协议解析/结算回放 19/19，相关 422/422。467 个旧 JSON fixture 的 471 条印刷费用有迁移清单；保持效果与最终资源预期。macOS 原生出牌、拒绝保留输入、混合符文选择和过期关闭有实测。完整嵌套打出、常驻牌时机及全部费用优先顺序未闭合，不登记全部费用条款完成。详见 [本批记录](PRINTED_POWER_ITERATION_2026-10-04.md)。

2026-10-04 效果打出单位补充：中国区 124.1 / 143.4 / 143.4.a 的入场属性计算已共用 PrepareUnitEntry，普通打出、废牌堆/手牌/对手牌堆效果打出及放逐后回场使用同一休眠默认、印刷关键词和当前等级/静态备战例外。放逐回场恢复原拥有者控制、解除旧装备并清除临时状态，记录两次区域边界。新增 OfficialEffectUnitEntryTests 8 项，旧代码 7 失败/1 通过，修复后 8/8；相关扩展回归 229/229，最终全量 9309/9309、零跳过（cn-effect-entry-full-verified.trx）。嵌套打出的完整费用、目的地、可选选择和触发顺序仍未完成；此条仅证明入场属性路径。


2026-10-04 时机与结算目标补充：中国区 308.1.a / 309.1.a / 806 / 813 / 819.1.b 区分开环焦点和闭环优先权；官网印刷的迅捷、反应及灵便权限统一加载，提醒文字和条件赋予不作为无条件许可。中国区 124 / 359.3.e.2-8 的通用卡牌效果结算重新检查区域、可见性、保护、属性、费用和战力条件；非法目标位置保留为空，其他合法目标及独立抽牌继续执行。新物体世代及堆叠目标绑定参与保存恢复。新增 19 项，相关 106/106、全量 9301/9301、零跳过（cn-continuity-timing-full-verified.trx）。旧触发测试的基地目标改用合法可指定基地的法术，隐藏对象防御清理改为预先具有致命伤害的清理场景；保留隐藏信息与触发断言。条件性关键词、组合目标子集选择、最后已知信息、全部内部转移及技能来源绑定仍未闭合，不标记完整 124 / 359 验证。


2026-10-04 区域转换补充：中国区 108.2.d / 124.1 / 141.1.b / 323.5 的正式卡牌场地→废牌堆、手牌、主牌堆路径保留印刷身份及属性，清除临时状态，按原拥有者归还；废牌堆公开、手牌/牌堆继续私密。共享 PrintedCardFactory 供正式开局和离场使用；战斗收尾按实际场地成员判断。OfficialNonFieldIdentityTests 新增 7 项，旧代码先复现 6 项失败，相关 107/107、全量 9282/9282（cn-zone-identity-full-verified.trx）通过。包含战斗阵亡→复活→再次打出和序列化恢复；macOS 实连废牌堆卡面可见。完整 124 新物体世代、非场地间全部转移和指示物消失仍待实现，不标记完整条款完成。

2026-10-04 整方伤害分配补充：中国区 465.2.a-c.7 的无替换效果路径按整方累计值验证致命伤害、同级自由排序、壁垒/普通/后排优先级和过量伤害归属；来源只保留为协议记账维度。新增 13 项 OfficialPooledCombatDamageTests 和 6 项 NativePooledDamageSelectionTests，最初 8 项中 6 项复现失败后修复；focused 68/68、全量 9275/9275（cn-pooled-full-verified.trx）通过。两个旧 fixture 的非法分配或固定顺序断言依据官方条款修正，未删除错误玩家、非法目标、溢出、状态不变和 stale prompt 保护。原生窗口使用总池、服务端建议、卡面预览及拒绝反馈；macOS 实连验证双方分配完成前不造成伤害、完成后四名参与者离场。伤害替换、相互冲突的优先级、免疫目标及完整战斗触发时序仍未完成。


2026-10-04 公开战力投影修正（本批回归通过）：快照 effectivePower 原先直接使用对象已物化的 Power，遗漏静态加成。现在公开单位额外投影当前 StaticAura 层；参与战斗时复用战斗实际战力投影，眩晕不把显示战力归零。临时战力修改已在对象中物化，不能再次叠加。P79FriendlyFilteredStaticPowerAddsOneToMatchingFriendlyUnits 对双方快照检查兰博友方机甲加成（2+1=3），修改前失败、修改后与相关快照/隐藏信息/眩晕用例共 153/153 通过。原生客户端仅展示该权威数值和伤害，不从卡面或桌面排列推算。最终后端全量 9256/9256 通过；这不是全连续效果与全部规则已完成的声明。


2026-10-04 眩晕伤害阈值补充：中国区 423.1.b/c 明确区分伤害贡献与实际战力。眩晕单位贡献的战斗伤害为 0，但致命阈值仍按完整战力减已有伤害计算；不能因眩晕跳过壁垒。自动结算、提交分配、prompt 的伤害池/致命阈值/参与者战力已同步修正。OfficialStunCombatTests 的四项新测试先复现旧错误，修复后相关 119/119、后端全量 9256/9256 通过；不代表跨来源总伤害或伤害替换已完成。


2026-10-04 多人战斗修正（本批回归通过）：中国区核心规则 464.2.c.3 要求实际战场上所有双方单位参与战斗，服务端不再接受通过客户端子集遗漏其他单位；伤害分配参与者、prompt 数量与保存恢复同步取消每方两名上限。自动伤害与已验证的玩家分配伤害共用战斗清理、战场技能、计分及控制权收尾。历史记录的参与者和事件顺序在保存/恢复中保留。寒谷弓手按 383.4.e / 464.2.e 在伤害前提出进攻费用，支付或放弃后恢复战斗；测试覆盖其随后死亡的场景。证据入口：OfficialBattleParticipationTests、BattleDamageAssignmentLifecycleTests、TriggerPaymentTests、CombatTestDriver 与 FullGameEndToEndTests。focused 64/64、相关 guard 168/168 和后端全量 9252/9252 均通过；465.2.c 跨来源总伤害/自由排序、替换效果、完整进攻防守触发顺序仍未完成。交付详情与后续实际通过记录统一见 NATIVE_DELIVERY_2026-10-04.md。


2026-10-04 争夺与同时移动补充：CN 464.2.c.1 按实际令战场进入争夺的玩家确立进攻方，记录到权威局面并参与保存/回放，物理战场所属者不再决定进攻方。CN 144.3 / 144.3.a-c 支持一条 MOVE_UNIT 携带 sourceObjectIds，在验证所有来源、目的地与额外费用后共同休眠/移动，之后才进行触发、清理与争夺。允许不同起点共享终点，战场间移动仍要求游走或对应战场许可；144.4 / 810 不以战场对象 ID 的玩家前缀限制真实战场。UNL-163/219 搜魔人巡管按额外单位逐名收取任意符能。新用例 StandardGroupMovementTests 与 OfficialBattlefieldControlTests 覆盖拒绝不变性、原子移动、进攻方恢复和费用；多人参战重构前全量 9246/9246 通过；原生客户端群组选取、取消与过期关闭已接入并实测，新战斗重构全量 9252/9252 已通过，不能视为全规则完成。

2026-10-04 中国区基础规则修正：除开局控制权、入场休眠及普通单位合法目的地外，资源反应按 429.3 保留当前优先权/焦点；再次争夺按 190.3.a.1 清理旧对决完成标记。SFD·209/221「遗忘丰碑」只限制“此处”得分，不能抑制其他战场；第三回合后仍可正常据守。OGN·290/298 的首回合触发不替代正常据守。461 / 464.2.c.3 不要求单位备战，标准移动后的休眠单位仍参与战斗。新增用例先复现旧错误再修正；本批最终全量 9252/9252 通过，尚非完整战斗规则或全卡完成证明。官方来源为本次 rules-manifest 与 card-catalog 快照，旧测试中的相反预期作废。


2026-10-04 CN core correction: the current Chinese PDF (SHA-256 in `data/official/upstream/2026-10-04/rules-manifest.json`) supersedes historical fixture assumptions. Rule 143.4 (p17) makes ordinary units enter exhausted; explicit ready-entry effects and paid Haste are exceptions. Rules 190.2/190.4 (p27), 439.4.b (p83) separate battlefield ownership from control and start battlefields uncontrolled. Rule 355.2.a (p43) limits ordinary unit play to the player base or controlled battlefields. `OfficialBattlefieldControlTests` independently checks opening no-score, command/prompt destination parity, no payment on rejection, legacy-destination spoof rejection, and exhausted entry to both zones. Historical full-game scripts are being migrated to legal base -> ready -> move -> spell-duel claim sequences. The current batch passes 9252/9252 full conformance tests; this does not establish complete official-rule coverage.

更新时间：2026-10-04

2026-10-04 权威更新：用户确认中国区官方规则与禁限牌。当前抓取的 13 份官方文档及 SHA-256 见 `data/official/upstream/2026-10-04/rules-manifest.json`；其更新版本优先于下文历史文档清单。中国区 2026-07-24 生效的标准禁卡已进入正式 API 的服务端提交/准备校验与预组生成，证据见 `docs/NATIVE_DELIVERY_2026-10-04.md`、`ChinaStandardLegalityTests`。新卡池与 2,381 个编号核心规则条目仍需逐条重审，禁止从历史代表测试数量推断最新版全规则完成。

## 1. 结论

从 2026-04-28 起，新项目的底层规则权威由五份官方 PDF 与官网卡牌快照共同构成。旧 Java 项目没有纳入四份 FAQ，因此不能再作为最终规则裁决源，只能作为历史实现参考、fixture 导出工具和回归对照样本。

任何已经开发完成的能力，如果只对齐了旧 Java 行为但没有核对五份 PDF，状态必须降级为 `NEEDS_RULE_AUDIT`，通过重审后才能标记为完成。

2026-07-07 补充：Plan B / Sea Monster Hook activated ability representative 继续以官网卡面与官方 catalog 为权威来源。`OGN·242/298`《海兽钓钩》官方文本要求支付 1 法力与 1 黄色符能并横置来源，摧毁一名友方单位，查看主牌堆顶部五张牌，可从中免费打出战力比被摧毁单位最多高 1 点的单位并回收其余牌。本批仅实现 BehaviorSpec-driven 代表路径：合法源为己方基地公开未横置装备，合法目标为友方公开单位；结算时不公开“查看”的五张牌，合法可打出单位唯一时自动打出，多个合法单位时打开 controller-only `CARD_CHOICE` / `CHOOSE_CARDS`，选择一名合法单位或提交空选择均可在 action-log replay 中保留完整 payload，零合法单位时不打开选择窗口并私下回收全部查看牌，回收事件只公开数量。证据见 `docs/CURRENT_PLAN_B_SEA_MONSTER_HOOK_ACTIVATED_ABILITY_AUDIT.md`、`docs/CURRENT_PLAN_B_SEA_MONSTER_HOOK_ACTIVATED_ABILITY_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-07 条目；完整 FAQ adjudication、完整 hidden-zone UX、P0/READY 仍不得标记完成。

2026-07-06 补充：Plan B / B0 LeBlanc Mirror Image official-deck replay 继续以官网卡面与官方构筑规则为权威来源。`UNL-200/219`《镜花水月》是乐芙兰专属法术，`UNL-199/219`《诡术妖姬》传奇与 `UNL-090/219` 乐芙兰英雄单位共享英雄标签“乐芙兰”，因此本批只使用合法 LeBlanc official deck opening 派生中局覆盖该法术；`UNL-200/219` 不能作为合法 Kai'Sa official deck B0 代表。新增 B0 回放通过服务端 `PLAY_CARD` prompt 指定公共战场单位目标，结算活跃“映像”到基地、记录 copied target / token factory metadata、继续 score victory 并 action-log replay 到相同 final state hash。证据见 `docs/CURRENT_PLAN_B_B0_FULL_GAME_E2E_AUDIT.md`、`docs/CURRENT_PLAN_B_B0_FULL_GAME_E2E_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-06 条目；完整复制牌面、忽略复制打出效果、瞬息 token 完整 cleanup、乐芙兰传奇触发打出映像能力、完整目标选择 prompt 和 P0/READY 仍不得标记完成。

2026-07-06 补充：Plan B / shared graveyard-spell free-play recycle bridge 继续以官网卡面为权威来源。`UNL-200/219`《镜花水月》官方文本要求选择一名单位，打出一个活跃“映像”到控制者基地，该映像复制所选单位并获得 `瞬息`；本批仅把共享桥接扩到 `BehaviorSpec` 已能描述的 exact-one copy-target base-unit token 代表形状：当当前公共合法单位目标集合唯一时，菲兹 source-unit-played 与卡莎 unit-conquest 的墓地低费法术路径可自动携带该唯一目标，从 `GRAVEYARD` 临时打到 `STACK`，复用现有 stack resolver 创建复制 `映像` 并在结算后回收；当合法单位目标不唯一时仍不猜选。证据见 `docs/CURRENT_PLAN_B_SOURCE_UNIT_PLAYED_TRIGGER_SPEC_EVIDENCE.md`、`docs/CURRENT_PLAN_B_B3_UNIT_CONQUEST_TRIGGER_SPEC_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-06 条目；完整 optional spell selection prompt、显式墓地法术选择、prompt-authored target selection、多目标 / 任意目标 targeted spell resolution、完整复制语义、符能支付 prompt 和完整 Kai'Sa / Fizz 墓地法术广度仍不得标记完成。

2026-07-03 补充：Plan B / B3 单位征服触发继续以官网卡面与核心规则为权威来源。`OGN·112/298` / `OGN·112a/298` 卡莎官方文本要求征服战场后可从控制者废牌堆打出费用低于当前分数的法术、免除法力费用、仍支付符能费用并在结算后回收；本批已实现并记录无目标抽牌法术代表路径，并补充官方 `OGN·134/298`《动员》的无目标召符文 / 无法召出改抽牌自然征服代表路径，以及官方 `OGN·094/298`《精灵召唤》的无目标基础单位 token 自然征服代表路径：共享桥接从废牌堆打到栈，复用现有 stack resolver 打出一个活跃 3 战力 `瞬息` 精灵到基地，再回收法术。合法 Kai'Sa 官方 deck opening 派生中局已覆盖官方蓝色 `UNL-061/219`《台前作秀》B0 抽牌代表，以及官方蓝色 `OGN·104/298`《择日再战》 exact-one 友方单位目标法术代表：共享桥接只在友方单位目标集合唯一时自动携带目标，结算回手、召符文、回收、score victory 和 action-log final-state replay。证据见 `docs/CURRENT_PLAN_B_B3_UNIT_CONQUEST_TRIGGER_SPEC_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-03 Plan B / B3 Kai'Sa 条目。完整可选选择、显式法术选择、目标选择 prompt、多目标 / 任意目标法术、符能支付 prompt、条件 token、非唯一/完整复制目标 token 法术和 APNAP / simultaneous ordering 仍不得标记完成。

2026-07-06 补充：Plan B / shared graveyard-spell free-play recycle bridge 继续以官网卡面为权威来源。`SFD·076/221`《产量激增》官方文本要求若控制“机械”单位则费用减少 2，随后打出一名 3 战力“机器人”到基地并抽一张牌；本批仅把共享桥接的安全无目标基础单位 token shell 扩展为允许普通 controller draw，使 `OGN·112/298` 卡莎征服后的废牌堆低费法术路径可复用现有 stack resolver 结算《产量激增》的机器人 token + 抽牌 + 回收。该证据见 `docs/CURRENT_PLAN_B_B3_UNIT_CONQUEST_TRIGGER_SPEC_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-06 条目；条件 token、非唯一/完整复制目标 token、多模式法术、显式墓地法术选择、目标选择 prompt、符能支付 prompt 和完整 Kai'Sa / Fizz 墓地法术广度仍不得标记完成。

2026-07-06 补充：Plan B / source-unit enemy spell-skill target protection 继续以官网卡面与核心规则目标合法性为权威来源。`UNL-147/219` / `UNL-147a/219` / `UNL-238/219` 纳什男爵官方文本要求其无法被敌方法术和技能选作目标；`SFD·105/221` 沙墟啸匪使用同义措辞“敌方法术和技能无法将我选作目标”；`UNL-059/219` / `UNL-059a/219` 易在等级 16 获得同类保护。本批将这些文本解析为 `BehaviorSpec.StaticAbilities` 的 `SOURCE_UNIT_ENEMY_SPELL_SKILL_TARGET_PROTECTION`，并由共享 `TargetProtectionRules` 在 `PLAY_CARD` 和已实现启动技能目标路径拒绝非法目标，同时从服务端 prompt 中过滤。证据见 `docs/CURRENT_PLAN_B_TARGET_PROTECTION_SPEC_AUDIT.md`、`docs/CURRENT_PLAN_B_TARGET_PROTECTION_SPEC_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-06 条目；野爪兽王同战场低战力友方单位保护光环、男爵巢穴创建/替代进场目的地、完整官方 deck breadth 和未来未实现 target-bearing skill family 仍不得标记完成。

2026-07-06 补充：Plan B / Teemo unit identity shared-catalog increment 不改变 Teemo 传奇行动的官方裁决语义，只收窄目标身份路由实现。`LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT` 的可选目标仍必须是可见、单位标签、由行动玩家拥有、位于允许区域的 Teemo 单位；本批把 `CoreRuleEngine` / `MatchSession` 规则路径里的 `"提莫"` 显示名判断移入共享 `UnitIdentityCatalog`，由当前已实现单位行为数据导出 Teemo 单位来源集合。证据见 `docs/CURRENT_PLAN_B_TEEMO_UNIT_IDENTITY_CATALOG_SOURCE_AUDIT.md`、`docs/CURRENT_PLAN_B_TEEMO_UNIT_IDENTITY_CATALOG_SOURCE_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-06 条目；Teemo full official、standby replacement breadth、hidden-info / random-zone breadth 和完整 legend-action official breadth 仍不得标记完成。

2026-07-06 补充：Plan B / Zhonya's Hourglass friendly-unit-destroyed replacement 继续以官网卡面和核心规则公开/隐藏信息边界为权威来源。`OGN·077/298`《中娅沙漏》官方文本要求下一次当友方单位被摧毁时，改为摧毁该装备并以休眠状态召回该单位；本批将该文本解析为 `BehaviorSpec.Replacements` 的 `FRIENDLY_UNIT_DESTROYED_DESTROY_SOURCE_RECALL_EXHAUSTED`，并由共享 `CardReplacementSpecRules` 在 state-based cleanup 中应用公开己方装备来源。暗置待命和对手来源不会作为替代源。证据见 `docs/CURRENT_PLAN_B_ZHONYAS_DESTROY_REPLACEMENT_AUDIT.md`、`docs/CURRENT_PLAN_B_ZHONYAS_DESTROY_REPLACEMENT_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-06 条目；Zhonya standby/reaction、多个替代效果选择排序、完整 equipment lifecycle、完整 FAQ adjudication、1009/811 full-official 和 READY 仍不得标记完成。

2026-07-03 补充：Plan B / Source Unit Played 触发同样以官网卡面与核心规则为权威来源。`SFD·140/221` 菲兹官方文本要求打出源单位时可从控制者废牌堆打出一个法力费用不高于 3 的法术、免除法力费用、仍支付符能费用，并在该法术结算后回收；本批已实现并记录无目标抽牌法术代表路径，并补充官方 `OGN·134/298`《动员》的无目标召符文 / 无法召出改抽牌代表路径、官方 `OGN·094/298`《精灵召唤》的无目标基础单位 token 代表路径，以及官方 `OGN·104/298`《择日再战》的 focused exact-one 友方单位目标法术代表：菲兹入基地后若友方单位目标集合唯一，共享桥接自动携带该目标，结算回手、召符文并回收法术；若多个友方单位目标合法，当前桥接不会自动猜选目标，保持该触发不移动墓地法术/目标/符文，等待后续显式 prompt 工作。合法 `UNL-201/219` / `UNL-119/219` 官方 deck opening 派生中局、官方《动员》召符文、score victory 和 action-log final-state replay 仍承载 B0 召符文代表；《择日再战》和《精灵召唤》为蓝色，不作为该橙/紫 Fizz 官方 deck 的 B0 覆盖。证据见 `docs/CURRENT_PLAN_B_SOURCE_UNIT_PLAYED_TRIGGER_SPEC_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-03 Plan B / Source Unit Played Fizz 条目。完整可选选择、显式废牌堆法术选择、目标选择 prompt、多目标 / 任意目标法术、符能支付 prompt、条件 token、非唯一/完整复制目标 token 法术和 broader stack handoff 仍不得标记完成。

2026-07-03 补充：Plan B / B3 兰博单位征服触发以官网卡面与核心规则为权威来源。`SFD·026/221` / `SFD·026a/221` 兰博官方文本要求征服战场后可选择回收另一名友方单位，以此从控制者废牌堆打出一名 `机械` 属性单位，并将所需法力费用减去被回收单位的战力；本批实现并记录回收 4 战力友方单位后把 4 费墓地机械单位减到 0 费并打到基地的代表路径，以及回收 2 战力友方单位后打开 `TRIGGER_PAYMENT` / `PAY_COST(SPEND_MANA:2)`、支付后再回收/打出、拒绝时不移动区域、支付不足时保留窗口和区域不变的代表路径。支付成功与支付不足路径另由合法 Rumble 官方 deck opening 派生中局、score victory 和 action-log final-state replay 代表承载。证据见 `docs/CURRENT_PLAN_B_B3_UNIT_CONQUEST_TRIGGER_SPEC_EVIDENCE.md` 与 `docs/rules-evidence-index.md` 的 2026-07-03 Plan B / B3 Rumble 条目。完整可选选择、显式回收/墓地目标选择、战场目的地选择和完整兰博家族 breadth 仍不得标记完成。

## 2. 当前官方 PDF 清单

这些 PDF 只作为本地规则资料，不提交到 Git。

| 文件 | 页数 | 作用 |
|---|---:|---|
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》核心规则_260330.pdf` | 105 | 核心规则主干。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/裁判FAQ_251023.pdf` | 10 | 官方裁判 FAQ，补充和澄清特定场景。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_裁判FAQ.pdf` | 25 | `铸魂淬炼` 系列裁判 FAQ。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/铸魂淬炼系列_官方FAQ_260114.pdf` | 21 | `铸魂淬炼` 系列官方 FAQ。 |
| `/Users/dinghaolin/MyProjects/riftbound-dotnet/《符文战场》破限系列_裁判FAQ_260416.pdf` | 11 | `破限` 系列裁判 FAQ。 |

## 3. 裁决优先级

1. 官网卡牌原文与核心规则中的黄金法则。
2. 五份官方 PDF 规则资料，其中 FAQ 对核心规则中不准确、不完整或未覆盖的具体场景具有澄清权。
3. 同属官方资料但存在冲突时，优先采用更具体、更新、且明确针对该场景的 FAQ 条目。
4. 官网卡牌快照 `data/official/card-catalog.zh-CN.json`，用于卡面文本、类型、费用、数值、关键词和素材索引。
5. 旧 Java 实现、测试、fixture 和生成矩阵，仅作历史参考；若与五份 PDF 或官网卡面冲突，必须修改新项目规则设计，不能为了 conformance 迁就 Java。

## 4. Java Oracle 的新定位

`java-oracle` fixture 仍然有价值，但语义从“最终规则金标准”调整为“旧实现行为样本”。

使用规则：

- 可继续用 Java exporter 生成 command log、events、snapshots，帮助新引擎保持迁移可控。
- 每条 fixture 必须补充 `rulesEvidence` 或等价文档记录，指向核心规则 PDF、FAQ 和官网卡面依据。
- 如果 Java 与 FAQ 不一致，新增 `expected` 应以 PDF/FAQ 裁决为准，同时保留 Java 输出作为 `legacyOracle` 对照。
- 已对齐 Java 的测试不能单独作为完成门禁，只能作为回归门禁之一。

## 5. 已开发部分重审清单

当前新项目仍处于骨架期，但以下已完成内容必须重新审查：

| 模块 | 当前状态 | 重审要求 |
|---|---|---|
| `MatchSession` 串行与幂等 | 工程行为可保留 | 核对 FAQ 中是否有重复提交、让过、响应窗口相关裁决；若无冲突，保持现状。 |
| `PASS` fixture | 已对齐 Java | 核对核心规则与 FAQ 中让过、连续让过、普通开环/闭环、结算链和法术对决规则。 |
| `END_TURN` fixture | 已对齐 Java | 核对回合结束、回合开始、通道符文、抽牌、临时效果清理、控制权归还与 FAQ 特例。 |
| 玩家视角 snapshot | 只有骨架 | 必须核对公开、私密、隐秘信息边界后再扩展。 |
| 事件日志与 prompt | 只有骨架 | 必须确保事件表达能覆盖 FAQ 中的特例裁决和玩家选择窗口。 |
| 卡牌功能分组 | 已按官网快照生成 | 需要用 FAQ 标注特殊牌、系列规则和容易被文本误解析的场景。 |

## 6. 后续开发门禁

任何规则能力进入 `ENGINE_READY` 或 `CARD_VALIDATED` 前，必须满足：

- 已查看五份 PDF 中与该能力相关的章节或 FAQ。
- 已记录规则依据，至少包括 PDF 文件名和页码或章节标识。
- 已确认官网卡面原文。
- 已写出 command log -> events -> snapshots 的测试。
- 若旧 Java 不覆盖或覆盖错误，必须建立手工 fixture 或新的 C# expected fixture。
- 若发现旧 Java 行为错误，优先修正新项目设计；旧 Java 只补导出或对照，不强行改成新权威。

## 7. 立即调整

P1 下一步不直接扩展更多玩法规则，先完成：

1. 抽取五份 PDF 的目录、关键词和 FAQ 问题索引。
2. 给现有 `PASS`、`END_TURN`、重复 `PASS` 三条 fixture 增加规则依据记录。
3. 把 fixture 语义从单纯 `java-oracle` 扩展为 `rulesEvidence + legacyOracle + expected`。
4. 建立 `NEEDS_RULE_AUDIT` 状态，防止已开发能力绕过 FAQ 重审。

当前已开发内容的逐项状态见 `docs/development-audit-status.md`。
