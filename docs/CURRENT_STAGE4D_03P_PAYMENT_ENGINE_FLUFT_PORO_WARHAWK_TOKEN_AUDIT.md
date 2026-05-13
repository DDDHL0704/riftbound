# Stage 4D-03P PaymentEngine Fluft Poro Warhawk Token Audit

日期：2026-05-14
结论：**4D-03P FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本审计验收 4D-03P Fluft Poro Warhawk token focused slice。A 主控结论：本切片满足 `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_HANDOFF.md` 的最小推进要求，把 `UNL-160/219` 绵绵魄罗 `{{横置}}：打出两名1{{S}}的“战鹰”，它们拥有{{法盾}}。我必须位于战场上才能使用此技能。` 接入服务端 authoritative prompt / command / stack resolution / audit representative；但 P0-005 仍不宣称 full-official resolved，项目整体仍 **NOT READY**。

## 1. Accepted Evidence

- 实现文件：
  - `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
- 更新测试：
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/FluftPoroActivatedAbilityTests.cs`
- 证据记录：
  - `docs/CURRENT_STAGE4D_03P_PAYMENT_ENGINE_FLUFT_PORO_WARHAWK_TOKEN_EVIDENCE.md`
- Focused regression：189/189 passed
- Adjacent regression：685/685 passed
- Backend full：3962/3962 passed
- Whitespace：`git diff --check` no output

## 2. A Review Notes

- `P4ActivatedAbilityCatalog` now exposes `FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS` as an executable representative with `ManaCost=0`, `PowerCost=0`, `ExperienceCost=0`, `RequiredTargetCount=0`, `RequiresBattlefieldSource=true`, `ExhaustsSourceAsCost=true` and `AppliesSpellshieldTargetTax=false`.
- The previous `DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS` surface is removed while Shadow swift stun remains deferred.
- `ActionPromptBuilder` exposes Fluft Poro only from current-player controlled, public, face-up, ready battlefield `UNL-160/219` sources in the current active player's open-main window.
- Prompt metadata includes zero-cost payment fields, `requiresBattlefieldSource=true`, `exhaustsSource=true`, no target choices, no payment resource choices, Warhawk token card number, token count, token power, token tags, open-main timing policy and ordinary stack-before-token-create policy.
- `CoreRuleEngine.ResolveFluftPoroWarhawkAbility` rejects wrong timing, spell-duel focus, non-active player, targets, unsupported optional costs, `RECYCLE_RUNE:*`, `TEMP_PAYMENT_RESOURCE:*`, missing / base / exhausted / wrong-controller / wrong-card / face-down / standby source variants with no-mutation coverage.
- Successful activation uses shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` for the zero-cost payment audit, exhausts the source after payment succeeds, emits `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `COST_PAID` and `STACK_ITEM_ADDED`, and creates an ordinary stack item without immediately creating tokens.
- Pass-pass stack resolution emits `ABILITY_RESOLVED` and creates two `UNL·T02` Warhawk tokens in the controller base. The resulting token objects have controller / owner set to the source controller, 1 power, `CARD_TYPE:UNIT`, `法盾` and matching `ObjectLocations`.
- Raman produced the B-side draft under the 4D-03P write lock. A reviewed the diff, confirmed the prompt standby-source guard, and reran focused, adjacent, backend full and whitespace validation. Raman did not commit.

## 3. Remaining No-Ready Items

- P0-005 remains open: this slice covers one no-target battlefield-only activated token representative, not the full PaymentEngine or complete activated ability family.
- Shadow swift stun, broader `[A]` / `[C]` resource skills, complete target-bearing skill family, full Spellshield target-tax propagation, token-play replacement breadth and full payment quote parity remain open.
- P0-002 / P0-003 / P0-004 full-official lifecycle residuals, P1 LayerEngine / keyword pass, 1009/811 matrix completion and final Browser / hidden-info / replay-hash audit remain open.

## 4. Next Step

Continue P0-005 breadth. Highest-value next candidates are Shadow swift stun, a remaining target-bearing activated ability, wider `[A]` / `[C]` resource skill coverage, or payment-window quote parity where it reduces duplicated logic without mixing unrelated rule families.
