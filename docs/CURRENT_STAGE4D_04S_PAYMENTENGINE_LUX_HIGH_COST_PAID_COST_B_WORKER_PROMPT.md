# Stage 4D-04S B Worker Prompt: PaymentEngine Lux High-Cost Paid-Cost Trigger

Date: 2026-05-21

Status: **DISPATCH READY / PROJECT NOT READY**

Use this prompt only for the B-side server/test implementation slice. You are not alone in the codebase: `DOC_MATRIX_CURRENT` is authorized to work on the 4D-03MR matrix + `PaymentEngineCoverageAuditTests.cs` sync in parallel. Do not revert other people's edits, and keep this slice inside the write scope below.

## Role

You are B: server rules, PaymentEngine/runtime, protocol snapshot safety and tests.

A owns acceptance, write-lock coordination and final readiness. E / DOC_MATRIX owns the active 03MR card-matrix sync.

## Objective

Implement or prove a narrow 4D-04S PaymentEngine official-breadth slice for Lux high-cost spell triggers:

- `OGS·006/024` Lux unit high-cost spell +3 power trigger.
- `OGS·021/024` Lux intro legend high-cost spell draw trigger.
- The high-cost trigger predicate must be server-authored and must follow the project’s accepted paid-cost / quote-command-audit semantics if existing rules evidence supports that interpretation.

If official evidence or existing project audit evidence does not support paid-cost semantics, stop and report the conflict to A instead of choosing a new rule interpretation.

## Current Anchors

Runtime anchors:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolveOgsLuxHighCostSpellPlayedTriggers(...)` currently checks `IsHighCostSpellForLux(behavior)`.
  - `TryGetLuxHighCostSpellDrawCardNo(...)` currently checks `IsHighCostSpellForLux(playedBehavior)`.
  - `IsHighCostSpellForLux(...)` currently uses printed `behavior.ManaCost >= 5`.
  - `TryResolveBattlefieldHighCostSpellInsightTrigger(...)` already receives `plan.TotalManaCost`.
  - `MarkPlayerPlayedFourPlusCostSpellThisTurn(...)` already uses `totalManaCost`.
  - `BuildPlayCardPaymentAuditMetadata(...)` and `COST_PAID` event metadata already carry quote / command audit signals for cost reductions, taxes and payment resources.

Test anchors:

- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`
  - `LuxHighCostSpellQueuesResolvesAndGainsPowerUntilEndOfTurn`
  - `LuxLowCostSpellDoesNotTrigger`
  - hidden / standby / invalid source no-trigger tests
- Existing completion/audit records:
  - `docs/CURRENT_COMPLETION_AUDIT.md` records 4D-03LF / 4D-03LG as NOT READY because Lux full trigger queue / APNAP ordering and paid-cost override matrix remain open.
  - `docs/CURRENT_SERVER_RULE_AUDIT.md` states future work should continue remaining payment windows and quote parity.

## Allowed Write Scope

Default runtime/test write lock:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`

Optional focused test file if cleaner than expanding `RealTriggerQueueTests`:

- `tests/Riftbound.ConformanceTests/LuxHighCostPaidCostTriggerTests.cs`

Optional docs after validation:

- `docs/CURRENT_STAGE4D_04S_PAYMENTENGINE_LUX_HIGH_COST_PAID_COST_AUDIT.md`
- `docs/CURRENT_STAGE4D_04S_PAYMENTENGINE_LUX_HIGH_COST_PAID_COST_EVIDENCE.md`

## Forbidden Scope

Do not touch without fresh A authorization:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `data/official/card-catalog.zh-CN.json`
- frontend runtime / DevUi rule paths
- Chrome / browser scripts
- formal 18-step E2E scripts
- broad PaymentEngine rewrites unrelated to Lux high-cost trigger paid-cost semantics
- unrelated trigger queue or APNAP ordering behavior
- protocol core fields
- `fullOfficial`
- READY / READY-CANDIDATE
- `riftbound-dotnet.sln`

## Implementation Contract

Keep the server as the only rules authority.

If paid-cost semantics are confirmed by existing evidence, replace the printed-cost-only Lux predicate with a server-authored predicate based on the resolved play-card payment plan / total mana cost. Prefer passing the resolved paid mana cost into the Lux trigger helpers over recalculating cost.

The implementation must prove these properties:

1. A Lux high-cost trigger fires when the resolved paid mana cost reaches the Lux threshold.
2. A printed high-cost spell reduced below the threshold does not fire if the project’s paid-cost rule requires resolved cost.
3. A printed lower-cost spell increased to the threshold by server-authored tax / added cost fires if the paid-cost rule requires resolved cost.
4. The unit Lux trigger and intro-legend Lux draw trigger use the same server-authored cost predicate.
5. Existing hidden / standby / invalid-source no-trigger guards still pass.
6. No hidden information is added to prompts, snapshots or event payloads.
7. Frontend remains display-only; no frontend rule calculation is required.

Do not close full Lux official breadth, full trigger queue / APNAP ordering, complete PaymentEngine, card matrix readiness or READY in this slice.

## Required Tests

Add or update focused tests that prove:

- unit Lux +3 power trigger uses the resolved paid-cost threshold;
- intro legend Lux draw trigger uses the same resolved paid-cost threshold without leaking deck order or hidden metadata;
- reduced high printed cost and increased lower printed cost are both covered if paid-cost semantics are accepted;
- rejected / no-trigger paths leave state unmutated where applicable;
- existing Lux / trigger / PaymentEngine adjacency remains green.

Use focused state construction tests where possible. Keep test names explicit so A can filter by `Lux|HighCost|PaidCost|RealTriggerQueue`.

## Required Validation

Run the focused Lux / trigger tests:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Lux|FullyQualifiedName~HighCost|FullyQualifiedName~RealTriggerQueue"
```

Run adjacent PaymentEngine / trigger coverage that is still practical for the slice:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~LuxResourceSkillTests"
```

Run final hygiene:

```sh
git diff --check
```

If the slice changes broader runtime behavior than the allowed scope, stop and report the blocker to A before editing further.

## Expected Output

When done, report:

- changed files;
- new files;
- exact tests run and pass/fail counts;
- whether paid-cost semantics were implemented or rejected as a rules conflict;
- whether any hidden-info leakage was found;
- whether protocol shape changed;
- remaining open P0/P1/P2 items;
- final conclusion: **NOT READY** unless A later proves all Stage 4 gates.

Do not output READY-CANDIDATE. Do not mark the goal complete.
