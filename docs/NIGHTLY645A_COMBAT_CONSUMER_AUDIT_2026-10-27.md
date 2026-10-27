# 固定 nightly645a：征服初态与战斗续段消费者修正

跟踪：[Issue 195](https://github.com/DDDHL0704/riftbound/issues/195)。本包基于完整 main `3839e13547b47f15983bbe5e82fc2f75c9ee2fb6`，代码提交 `13d7e2511c5bd2528c39b7fb8f5b730c1a363d7b`、`04997c00640bdb7359ec72eeab10a0c90b7cfc88`、`20236137f5a1948f36cb87ccfd2ffaccedc87edf`。仅修改测试初态、驱动和有独立预期的断言；生产、官方目录和 CI 没有差异。

## 固定输入与原失败

原夜间 [40413203883](https://github.com/DDDHL0704/riftbound/actions/runs/40413203883) 固定于 `645a045e435dbb4521eca5e5318c10ffcee36511`。games-2 job `121889339609` 实际 49 项：41P/8F/0Skip，TRX SHA `ace77a177ca86809b8159499e0b9f0ed689147f06724f259697ad305dbc4869a`。本线另负责 partition 1 的 battle-result-continuation 和 partition 3 的 combat-assignment 两处失败；partition 0 的 rune-destination 由付款线独立修复，不能沿用其结论代替本包的因果验证。其他停止或未完成的分区没有全量通过结论。

原生产者材料：`/Volumes/ExternalSSD/MyProjects/.worktrees/riftbound-payment-20261023/var/qa/imposed-movement-current-20261027/nightly645a-failure-audit-v31/triage-delivery.json`，SHA `191820df549a30919778d6d3bf033f6462a8682fd3bc71d6d8289ff1938c3fed`。原 ZIP、日志、SQL 前缀和失败均留在该位置；本包没有重新打包或改写历史。

## 官方依据与因果边界

CN DOC-01 [官方 PDF](https://cdn.playloltcg.com/lol/2026/10/2026-10-10/a21ead876c6b44d6abe386b6ae54ea28.pdf)，SHA `d6fd7140f106f9b5b3dc0d5817fd040110fe870a6da9ee335ba7f4f70f892d11`。本次重新取得的字节与本地原件相同，已实际看页 91/92/94/95/96。463、466.2.c.3 允许旧战斗结束后立即开始新争夺并重新建立角色；468.5 与 471.1 要求真实取得控制权才征服；468.7 清理旧角色。版本日期 2026-10-10、生效日期 2026-10-16 仅按已采纳索引记录；这不构成来源全集或整卡适用性认证。

初始“Thunder 未等待控制结算完成”的猜测已被否定。原 645a 的完整事件尾部和本次实际 3839 原 Thunder 复现均包含 `BATTLEFIELD_CONTROL_RESOLVED`、`BATTLE_CLOSED`；控制载荷为 previous=P1/controller=P1/changed=false。不能据此修改生产结算时序。

| 原失败 | 独立证据与修正 |
|---|---|
| Thunder Sigil 征服回收符文 | 原构造器令 P1 已控制目标场；设置敌方控制，保留所有权、物体身份与其他初态事实。 |
| Kai’Sa 定向墓地符文法术 | 初态继承开局的 P1 控制；明确敌方控制，使原墓地重施与目标断言实际发生。 |
| Hunting Grounds 过量伤害 Warhawk | 原 P1 控制不会产生新征服；仅改目标控制权后原 token/过量伤害断言通过。 |
| Hall of Legends 重置传奇 | 同一初态错误；保留原支付及重置断言。 |
| Sunken Temple 强力抽牌 | 初态修正后还暴露真实支付边界：自动传优先权在 `PendingPayment` 返回；支付前禁止的是征服玩家抽牌，不能误禁对手已经赚取的阵亡抽牌。 |
| Imperial Shrine 支付 | 修正原初态，保留原支付与 Sand Soldier 断言。 |
| Imperial Shrine 拒付 | 独立执行拒付路径，保留原拒付、资源与不生成 token 断言。 |
| Treasure Pile 支付 | 修正原初态，保留原支付与 Gold 断言。 |

七种构造器逐一运行控制臂与控制权反事实。记录保留完整初态、实际 journal 命令/事件和结果；控制权改变也会自然改变只读派生的 battlefield/cleanup/task 投影，不能把这些派生差异误称额外输入修改。有界验证只运行原开局、原构造器、首次战斗与原即时断言，没有执行八条完整获胜尾段。

## SQL 消费者

`CombatAssignmentSqlAcceptance` 的 `declared-source-second` 原显式初态由 P1 控制 F，故没有征服事件。对该原始 JSON 保留原件，另建立当前规则身份的新显式实验，四个征服分支分别比较 P1/P2 控制臂；新实验没有冒充 645a 原始 SQL 恢复。修正构造器的征服分支控制者，natural 分支仍保留原所有权与布局。

该修正使原夜间尚未执行到的 Vayne 支付/拒付分支到达真实 `CONTROL_TRIGGERS` + `PendingPayment`，暴露“决定前已经关战”的另一条消费者错误。现在先核对同时伤害、阵亡和未关闭的控制技能，再在真实支付/拒付后要求恰好一次 `BATTLE_CLOSED`。原生 Vayne 八行也保留各独立断言并增加关战先后约束。

`BattleResultContinuationSqlAcceptance` 原 `entry-11-original-before-persist.json.gz`（SHA `d8932821ea8db409324a54a00a93453bbee29fedcb2698ebfdeb913712c58cf9`）在同一命令中先发 `BATTLE_CLOSED`，明确清理 SOURCE/LATE，然后 `BATTLEFIELD_CONTESTED`、`SPELL_DUEL_STARTED`、`BATTLE_ROLES_ESTABLISHED`。旧 scope tick4 已退休，新 scope tick11 已生效；两个 true 角色属于新战斗。消费者现在独立检查旧清理事件及范围退休、当前新范围与角色，并真实 `PASS_FOCUS` 后才第二次宣战。原 SQL 写入、逐步重连、完整原日志重放、七表和篡改拒绝段均保留。

新增 Nidalee/Enforcer 两行从真实 ROOM_SEAT、ROOM_SEAT、DEV_SEED_SCENARIO 起步，每条接受命令后验证原始前缀并冷恢复；新对决帧另经严格 registry 入口验证，最后完成第二次战斗、两次抽牌、两次关战和一次征服。该 QA seed 是明确受控位置，不是普通合法构筑开局认证。

## 实际验证与保留失败

所有本地执行限单工作线程的有界定位/回归；没有本地 SQL、重启、网络或整套获胜矩阵执行。证据根为本 worktree 的 `var/qa/nightly645a-combat-audit-20261027/`。

| 固定源码 / 证据目录 | 实际结果 |
|---|---|
| 3839 / current3839-thunder-red-v2 | 原 Thunder 方法 0P/1F；TRX `c814fa406f4f67b3698cab04eb54617de79166f4fc8a7d07155174de9cd860bf`。 |
| 3839 / controller-counterfactual-v4 | 7 个反事实探针 6P/1F；Sunken 正确打开支付窗口后驱动越界。TRX `ef99b03c9bbf583cae387bb25c48dcfe8d9a426b1b26ca461a9abc64d38cee5f`。 |
| 13d7 / sunken-payment-causal-v5 | 0P/1F；驱动边界修正后，旧“任何玩家不得抽牌”断言误报对手阵亡抽牌。TRX `f946b658deb139f10e2d45abd0f2b7918ac31ce02f5c8384a99354a8ea6f600e`。 |
| 04997 / fixed-prefix-and-continuation-v6 | 43 项 41P/2F/0Skip；八个 FullGame 有界前缀、两条新增真实前缀冷恢复及既有相邻断言通过；两 F 为新到达的 Vayne 消费者关战阶段断言，完整原证据保留。TRX `4e96f3dcc24165727089ad196d354186331201bc960daafc98abea3b637bb3da`。 |
| 202361 / conquest-payment-continuation-v8 | 12/12/0Skip：四种显式 SQL 构造器反事实及原生 Vayne 八行；TRX `68166ff92bf31d2df3b342cefe04fa31e0e54e8b29fb2a82769865ae4f58e9cb`。 |
| 202361 / postgres-source-build-v9.log | PostgreSQL 验收项目只编译，0 warning/0 error，没有执行 SQL。 |
| full-game-discovery-v10.log | 仅发现：原 143 方法/151 行仍在；12 个临时反事实行另列，发现数不代表语义通过。 |

临时探针曾有一处语法错误，`controller-counterfactual-v3` 的原编译失败、源码与零执行事实也保留。每个有实际运行的阶段分别保存四模块 Info/SHA/MVID 和必要原 DLL、源码哈希及编译清单。所有 685 个已跟踪 Conformance C# 都编入；只有原五个外来未跟踪草稿按原约定排除，哈希未变。原 143 个测试方法体与 Theory 属性相对 645a 逐字节相同；修改发生在私有构造器/驱动/断言，不删原胜利与回放检查。

## c9 相关性与未完成范围

`c9-relatedness-source-v7/relatedness.json` SHA `517d98dd8da71ccbadd82ab298bc6034a038500f6508a20a9b6da143e35cb946` 比较实际 main3839 与 c9：官方目录只追加 VEN110/130，原 1082 行未变。七个失败控制臂的实际 journal 状态，加原 SQL late 11 帧，均无强化对象、OBJECT_EMPOWERED 或 DeathObserver 上下文。因此这些保留前缀没有激活 c9 新强化光环、强化放逐观察或死亡观察者专属投影；这是一项限定于前缀的源码与输入排除结论，没有执行或认证 c9。

各结果属于各固定输入，不能将 41P 与 12P 相加、重标成一个候选全绿。最新 PR/main 的完整 L2、八条原完整获胜尾段、实际 PostgreSQL 全续段、其余 SQL 问题及固定主线 L3 仍待各自结果。Issue 195 保持 OPEN；本包没有整卡或全后端认证增量。
