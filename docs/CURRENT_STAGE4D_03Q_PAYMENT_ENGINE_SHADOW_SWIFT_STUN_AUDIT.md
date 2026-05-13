# Stage 4D-03Q PaymentEngine Shadow Swift Stun Audit

日期：2026-05-14
结论：**4D-03Q FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03Q Shadow swift stun focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_HANDOFF.md` 的最小推进要求，把 `UNL-194/219` 黑影 `{{迅捷>}} 支付{{1}}和{{A}}，{{横置}}：{{眩晕}}一名进攻此处的敌方单位` 接入服务端 authoritative prompt / command / stack resolution / audit representative；但 P0-004 与 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03Q_PAYMENT_ENGINE_SHADOW_SWIFT_STUN_EVIDENCE.md`
- Focused regression：239/239 passed
- Adjacent regression：779/779 passed
- Backend full：4003/4003 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `P4ActivatedAbilityCatalog` now exposes `SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER` as an executable representative with `ManaCost=1`, `PowerCost=1`, `RequiredTargetCount=1`, `RequiresBattlefieldSource=true`, `ExhaustsSourceAsCost=true`, `AppliesSpellshieldTargetTax=true` and `ReactionSpeed=true`.
- `DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER` was removed from deferred-only surfaces. The P4 deferred surface audit now accepts an empty deferred list, which records that this P4 activated ability deferred surface set is cleared at representative scope.
- `ActionPromptBuilder` exposes Shadow only in focused battle-response priority state, from current-player controlled, public, face-up, ready battlefield `UNL-194/219` sources.
- Prompt metadata includes `swift=true`, `requiresBattlefieldSource=true`, `exhaustsSource=true`, `targetScope=enemy-attacking-unit-at-this-battlefield`, Spellshield target-tax metadata and `stackPolicy=ordinary-stack-item-before-stun`.
- Target choices are limited to same-battlefield enemy attacking public units. Friendly attackers, defenders, non-attacking enemy units, wrong-battlefield attackers, base units, face-down targets and standby targets are excluded or rejected.
- `CoreRuleEngine.ResolveShadowStunAbility` uses shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` for 1 mana + 1 generic power plus enemy Spellshield target tax, supports legal rune recycle for power shortfall, rejects invalid / duplicate / unnecessary / unsupported payment resource actions, and keeps failures no-mutation.
- Successful activation emits `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID` and `STACK_ITEM_ADDED`, exhausts source as cost, and creates an ordinary stack item without immediately applying `STUNNED`.
- Stack pass-pass revalidates the target. A still-legal same-battlefield attacking enemy receives `STUNNED`; stale targets no-effect without stunning another object.
- Raman produced the B-side draft under the 4D-03Q write lock. A reviewed the diff and reran focused, adjacent, backend full and whitespace validation. Raman did not commit.

## 3. Remaining No-Ready Items

- P0-004 remains open: this slice uses a focused battle-response priority representative and does not implement full battle lifecycle, complete swift/reaction flow or complete `DECLARE_BATTLE` state machine.
- P0-005 remains open: this slice covers one target-bearing swift activated stun representative, not the full PaymentEngine, full activated ability family or full `[A]` / `[C]` resource skill family.
- Complete payment quote parity, full Spellshield target-tax propagation, remaining target-bearing abilities, LayerEngine / keywords, 1009/811 full-official matrix and final Browser / hidden-info / replay-hash audit remain open.

## 4. Next Step

Continue P0-005 breadth after committing 4D-03Q. Highest-value next candidates are remaining target-bearing activated abilities, wider `[A]` / `[C]` resource skill coverage, or payment-window quote parity that reduces duplicated payment logic without claiming full battle lifecycle closure.
