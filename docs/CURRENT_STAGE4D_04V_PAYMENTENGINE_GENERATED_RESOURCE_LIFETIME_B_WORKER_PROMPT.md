# Stage 4D-04V B Worker Prompt: PaymentEngine Generated Resource Lifetime

Date: 2026-05-21

Status: **DISPATCH READY / PROJECT NOT READY**

Use this prompt only for the B-side server/test slice. You are not alone in the codebase: `DOC_MATRIX_CURRENT` is running a rolling matrix-doc lane in parallel and currently owns matrix JSON plus `PaymentEngineCoverageAuditTests.cs`. Do not revert other people's edits, and keep this slice inside the write scope below.

## Role

You are B: server rules, PaymentEngine runtime, protocol snapshot safety and tests.

A owns acceptance, write-lock coordination and final readiness. E / DOC_MATRIX owns card-matrix reductions. C owns frontend only if a server prompt or snapshot change requires display support.

## Objective

Implement or prove a narrow 4D-04V PaymentEngine generated-resource lifetime slice.

The goal is to reduce P0-005 risk around generated resources used for payment: server-issued quote prompts must stay authoritative, commands must revalidate current generated-resource state, spent or stale generated resources must not be replayable, and rejected commands must not mutate state.

This slice follows accepted 04S/04T/04U work. It must not claim full PaymentEngine, full official matrix, card-matrix readiness, frontend final validation or READY.

## Current Anchors

Runtime anchors:

- `src/Riftbound.Engine/PaymentCostRules.cs`
  - `PaymentPlan`
  - `AuthorizePayment(...)`
  - `TryCommitPayment(...)`
  - temporary payment resource action handling.
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - generated resource activation / creation paths.
  - `PAY_COST`, `PLAY_CARD`, `ACTIVATE_ABILITY`, `LEGEND_ACT` payment commit paths.

Test anchors:

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
- `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`

Current audit anchor:

- `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md` says P0-005 still needs fresh B-side work for concrete runtime/card-row interaction, generated-resource lifetime, rollback breadth, full official matrix or card-matrix readiness slices.

## Allowed Write Scope

Default runtime/test write lock:

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- `tests/Riftbound.ConformanceTests/GoldTokenResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`
- `tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`

Optional docs after validation:

- `docs/CURRENT_STAGE4D_04V_PAYMENTENGINE_GENERATED_RESOURCE_LIFETIME_AUDIT.md`
- `docs/CURRENT_STAGE4D_04V_PAYMENTENGINE_GENERATED_RESOURCE_LIFETIME_EVIDENCE.md`

## Forbidden Scope

Do not touch without fresh A authorization:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `data/official/card-catalog.zh-CN.json`
- frontend runtime / DevUi local rules
- Chrome / browser scripts
- formal 18-step E2E scripts
- broad battle lifecycle, trigger ordering or LayerEngine rewrites
- protocol core fields unless a focused parser/contract test proves an existing mismatch
- `fullOfficial`
- READY / READY-CANDIDATE
- `riftbound-dotnet.sln`

## Implementation Contract

Keep the server as the only rules authority.

First inspect current tests and runtime. Add runtime code only if a focused test exposes a real server bug. Prefer focused tests that prove these invariants:

1. Server prompts expose current generated-resource choices only from authoritative state.
2. A generated resource spent once cannot be replayed by resubmitting the same action id.
3. A stale generated-resource action id from a previous prompt is rejected without mutation after the resource has been consumed or cleared.
4. A generated resource cannot be used by the wrong player or in the wrong payment window.
5. Temporary payment-only generated resources cannot pay disallowed mana-only, spell-only or non-rune costs.
6. Generated mana / power represented in rune pools cannot be spent twice and obeys cleanup lifetime.
7. Failed generated-resource payment attempts leave card locations, rune pools, pending payment, stack and temporary ledgers unchanged.
8. `COST_PAID`, generated-resource and cleanup audit events continue to describe the server-authoritative source without leaking hidden information.

Do not close full official PaymentEngine, all card rows, P0/P1, Chrome smoke or READY in this slice.

## Required Tests

Add or update focused tests for at least:

- stale `TEMP_PAYMENT_RESOURCE:*` replay rejection with no mutation;
- wrong-player or wrong-window generated-resource rejection with no mutation;
- generated rune-pool mana/power cannot be spent twice;
- one representative generated-resource prompt still quotes a legal command and clears after successful payment;
- one representative invalid generated-resource payment leaves stack / pending payment / zones unchanged.

Prefer extending existing focused tests over broad fixtures. Keep test names filterable by `GeneratedResource|TemporaryPaymentResource|Gold|ResourceConversion|LegendResourceBridge|Lux|Honeyfruit|BlueSentinel|PaymentEngine`.

## Required Validation

Run focused generated-resource coverage:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GeneratedResource|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~GoldToken|FullyQualifiedName~ResourceConversion|FullyQualifiedName~LegendResourceBridge|FullyQualifiedName~LuxResource|FullyQualifiedName~Honeyfruit|FullyQualifiedName~BlueSentinel|FullyQualifiedName~PaymentEngineUnification"
```

Run adjacent payment / prompt coverage:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~ResourceSkill"
```

Run final hygiene:

```sh
git diff --check
```

If runtime changes are made, recommend backend full test for A acceptance.

## Expected Output

When done, report:

- changed files;
- new files;
- exact tests run and pass/fail counts;
- whether any runtime bug was fixed or the slice is test/evidence-only;
- whether hidden-info leakage was found;
- whether protocol shape changed;
- remaining open P0/P1/P2 items;
- final conclusion: **NOT READY** unless A later proves all Stage 4 gates.

Do not output READY-CANDIDATE. Do not mark the goal complete.
