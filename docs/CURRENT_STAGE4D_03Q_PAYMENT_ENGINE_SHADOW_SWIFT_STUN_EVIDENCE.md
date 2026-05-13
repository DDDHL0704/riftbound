# Stage 4D-03Q PaymentEngine Shadow Swift Stun Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03Q Shadow swift stun focused slice 的实现证据。该证据接受 4D-03Q focused slice，不关闭 P0-004 / P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Shadow executable ability constants and definition。
  - `SourceCardNo=UNL-194/219`、`EffectKind=SHADOW_ACTIVATED_STUN_ATTACKER`、`ManaCost=1`、`PowerCost=1`、`ExperienceCost=0`、`RequiredTargetCount=1`、`RequiresBattlefieldSource=true`、`ExhaustsSourceAsCost=true`、`AppliesSpellshieldTargetTax=true`、`ReactionSpeed=true`。
  - `DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER` 从 deferred-only surface 移除；P4 deferred surface audit 允许空列表。
- `MatchSession`：
  - battle-response priority representative window 会为 priority player 构建 prompt。
  - Prompt 只在 `TimingState=NEUTRAL_CLOSED`、stack empty、`PriorityPlayerId` 为当前玩家、`BattleState.IsActive=true` 且 battle battlefield 可确定时公开 Shadow source requirement。
  - source requirement metadata 包含 `swift=true`、`requiresBattlefieldSource=true`、`exhaustsSource=true`、`targetScope=enemy-attacking-unit-at-this-battlefield`、`statusEffectId=STUNNED`、Spellshield target tax map、`stackPolicy=ordinary-stack-item-before-stun` 与 `paymentPolicy=payment-plan-mana-generic-power-spellshield-tax-exhaust-as-cost`。
  - target choices 只公开同一战场、敌方、公开、单位、正在进攻的对象，并按可支付 target tax / power 过滤。
- `CoreRuleEngine`：
  - 新增 Shadow command resolver，成功路径使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 支付 1 mana + 1 generic power + enemy Spellshield target tax。
  - 支持合法 `RECYCLE_RUNE:*` 补足 generic power shortfall，并拒绝 invalid / duplicate / unnecessary resource action。
  - 成功 activation 横置 battlefield source，创建普通 stack item，不立即应用 stun。
  - stack pass-pass 后解析 Shadow stack item，重新验证 target；合法 target 获得 `STUNNED`，stale target 记录 no-effect。
  - wrong timing、wrong priority、missing / extra / illegal target、invalid source、insufficient cost、unsupported optional costs、temporary resource misuse variants rejected no-mutation。
- `ShadowActivatedAbilityTests`：
  - prompt surface、payment metadata、target choices、Spellshield target tax map。
  - open-main、base source、exhausted source、wrong controller / wrong card / face-down / standby / no legal target / wrong priority / spell-duel prompt hiding。
  - activation payment / source exhaust / stack item / no immediate stun。
  - legal rune recycle and Spellshield tax payment。
  - pass-pass stun resolution and stale target no-effect。
  - invalid target/source/payment/timing no-mutation matrix。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog 断言 Shadow。
  - deferred surface audit 更新为空表接受，并确认 Shadow 不再 deferred-only。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~Swift|FullyQualifiedName~Stun"
```

结果：

```text
Passed! - Failed: 0, Passed: 239, Skipped: 0, Total: 239
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Shadow|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Swift|FullyQualifiedName~Stun|FullyQualifiedName~SpellDuel|FullyQualifiedName~Battlefield|FullyQualifiedName~DeclareBattle"
```

结果：

```text
Passed! - Failed: 0, Passed: 779, Skipped: 0, Total: 779
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 4003, Skipped: 0, Total: 4003
```

## 5. Whitespace

命令：

```sh
git diff --check
```

结果：no output.

## 6. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本切片未读取、未修改、未暂存。

## 7. Verdict

4D-03Q focused slice accepted. Shadow swift stun is now covered by server-authoritative prompt / command / stack resolution / audit representative tests. P0-004 and P0-005 remain open because complete battle lifecycle, full swift / reaction family, complete activated / resource skill family and full PaymentEngine quote parity are still not full-official.
