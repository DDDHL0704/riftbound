# Stage 4D-03L PaymentEngine Reaction Resource Skill Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03L 的 B 侧服务端实现交接范围。A 主控只记录官方候选、当前代码事实、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03I / 4D-03J / 4D-03K 已用 `OGN·113/298` 玛尔扎哈建立 open-main / spell-duel focus resource skill、temporary payment-only ledger 与 inline payment consumption representative。4D-03L 的目标是继续收窄完整 `[A]` / `[C]` resource skill family，选择一个更小的 reaction-speed resource skill representative：

- `UNL-093/219`，卡名 `龙魂贤者` / `Dragon Soul Sage`。
- 当前 catalog deferred surface：`DEFERRED_TAP_REACTION_GAIN_1_MANA`。
- 官方文本锚点：`{{反应>}} {{横置}}：{{获得}}{{1}}`。
- 现有普通入场证据：`p2-preflight-play-dragon-soul-sage-activated-skill-unit` 与 `p4-play-dragon-soul-sage-target-rejected`。

本切片只处理 Dragon Soul Sage 的 reaction-speed no-target resource skill。不要同时实现 Renata Glasc 抽牌 / 得分、Crimson Rose 目标 ready-unit、Shadow swift stun、Fluft Poro token creation 或完整 activated ability family。

## 2. 当前代码事实

- `P4ActivatedAbilityCatalog.GetAll()` 当前只执行 Vi、Xerath 与 Malzahar 三条 representative ability。
- `P4ActivatedAbilityCatalog.GetDeferredSurfaces()` 已登记 `DEFERRED_TAP_REACTION_GAIN_1_MANA`，并标注 `RequiresBattlefieldSource: true`、`IsTargetBearing: false`、`EnemySpellshieldTaxRisk: false`。
- `ConformanceFixtureRunnerTests.P4ActivatedAbilityCatalogAuditsDeferredSkillSurfacesAgainstOfficialText` 已用官方文本锚点确认 Dragon Soul Sage 的 reaction resource skill 仍 deferred。
- `CoreRuleEngine.ResolveActivateAbility` 当前遇到未登记的 Dragon Soul Sage ability id 会 rejected / unsupported，不应推进 tick、支付资源、横置 source 或创建 stack item。
- `MatchSession` 不会为 Dragon Soul Sage 生成 `ACTIVATE_ABILITY.sourceRequirements`。
- 4D-03J / 4D-03K 的 Malzahar temporary payment resource helper 不应被 Dragon Soul Sage 复用成 payment-only ledger；Dragon Soul Sage 文本是 `获得1`，实现前必须按官方文本和现有 rune pool lifecycle 明确它获得的是普通 mana/resource 还是更窄的临时资源。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 必要时新增窄域 `ReactionResourceSkillTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- Renata / Crimson Rose / Shadow / Fluft Poro 的 deferred ability 实现
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Catalog / Prompt

- 将 Dragon Soul Sage 从 deferred surface 移入 executable activated ability catalog，或增加等价的 executable definition，同时保留审计说明：4D-03L 只开放这一条 representative。
- prompt 只在服务端确认 reaction-speed activated ability 合法的窗口公开 `ACTIVATE_ABILITY` source requirement。
- source 必须由当前玩家控制、公开、正面、未横置、位于服务端允许区域；当前 deferred surface 标注 `RequiresBattlefieldSource: true`，除非 B 以官方规则证据证明 base source 也合法，否则本切片按 battlefield source 收窄。
- Dragon Soul Sage ability 不需要目标；prompt 不得暴露 target slot，也不得让前端自行推断可提交时机。
- metadata 应标明 `resourceSkill=true`、`reactionSpeed=true`、`exhaustsSource=true` 与 generated resource amount。

### 4.2 Timing / Command Guard

- 命令侧必须绑定服务端 priority / reaction / spell-duel focus 状态机，不允许在 cleanup、ordinary open main、wrong focus、non-priority player 或 pending task blocking 期间偷用 reaction resource skill。
- 成功时横置 source 并获得 1 点官方文本对应资源；事件 payload 应记录 source、abilityId、timingContext、generated amount 与 resource skill marker。
- 若实现选择把 `获得1` 记入 `RunePool.Mana`，必须沿用现有 mana reset lifecycle 并在审计中写明；若实现为更窄 temporary resource，也必须由 prompt / snapshot / commit tests 固定限制。
- Dragon Soul Sage resource skill 不应创建普通可被 counter / reaction target filter 选中的 stack item。若 B 认为官方需要 stack object，必须同时实现不可被普通 counter / reaction 选择的 target filter，并补 no-mutation 测试。

### 4.3 No-Mutation Guards

必须 rejected no-mutation：

- 未知 ability id、错误 cardNo、source 不存在。
- source 非当前玩家控制、隐藏 / face-down、已横置、不在合法区域。
- source 不是 Dragon Soul Sage 或 ability / cardNo mismatch。
- 提交任意 targetObjectIds、unsupported optional costs 或 payment resource actions。
- wrong timing：open-main、non-focus spell duel、closed stack priority、cleanup/task queue blocking。

No-mutation 至少断言 tick、zones、cardObjects、runePool、stack、priority/focus 与 temporary payment resources 不变。

### 4.4 Regression Boundaries

- 不得破坏 Vi / Xerath `ACTIVATE_ABILITY` payment plan、Spellshield tax、recycle rune payment resource actions。
- 不得破坏 Malzahar resource skill lifecycle、temporary payment resource pending / inline consumption。
- 不得把 Dragon Soul Sage 产生的资源混入 Malzahar `TemporaryPaymentResources`。
- 不得修改前端以补服务端 prompt 缺口。

## 5. 必补测试

Focused tests 至少覆盖：

- catalog 不再把 Dragon Soul Sage executable representative 留在 deferred-only surface。
- reaction-speed prompt 暴露 Dragon Soul Sage source requirement，且无 target slot。
- 成功命令横置 source、获得 1 点资源、无普通 stack item。
- generated resource 可被后续服务端 payment model 看见，且 reset / cleanup lifecycle 符合实现口径。
- open-main / wrong player / wrong focus / cleanup blocking / source invalid / target submitted / optional cost submitted 均 rejected no-mutation。
- Vi / Xerath / Malzahar existing `ACTIVATE_ABILITY` tests 继续通过。

相邻回归必须覆盖：

- `PaymentEngineUnificationTests`
- `MalzaharResourceSkillTests`
- priority / reaction / spell-duel prompt surfaces
- payment resource actions、rune pool、ActionPrompt、GameHub

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSage|FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~SpellDuel|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

实现通过后由 A 决定是否再跑 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要把 4D-03L 扩展成完整 activated ability / resource skill family。
- 不要同时实现 Renata、Crimson Rose、Shadow 或 Fluft Poro deferred abilities。
- 不要让前端本地推断 reaction resource skill 时机。
- 不要在 wrong priority / cleanup / open-main 窗口公开或接受 Dragon Soul Sage resource skill。
- 不要创建普通可被 counter / reaction target 的 stack item。
- 不要修改 coverage matrix 或升级 Dragon Soul Sage 到 full-official。
- 不要因为 4D-03L 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03L 是 4D-03K 后的下一枚 PaymentEngine breadth 切片，只用 Dragon Soul Sage 验证 reaction-speed resource skill 的 prompt / command / audit representative。它可以继续收窄 P0-005，但不能替代完整 `[A]` / `[C]` family、target-bearing activated skills、full reaction/counter target model、LayerEngine 或最终 READY audit。
