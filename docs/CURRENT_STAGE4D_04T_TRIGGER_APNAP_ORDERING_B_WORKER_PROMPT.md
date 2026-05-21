# Stage 4D-04T B Worker Prompt: Trigger Queue APNAP Ordering Breadth

Date: 2026-05-21

Status: **DISPATCH READY / PROJECT NOT READY**

Use this prompt only for the B-side server/test implementation slice. You are not alone in the codebase: `DOC_MATRIX_CURRENT` may continue the next small matrix-doc bundle in parallel under A's shared-board lock. Do not revert other people's edits, and keep this slice inside the write scope below.

## Role

You are B: server rules, trigger queue runtime, protocol snapshot safety and tests.

A owns acceptance, write-lock coordination and final readiness. E / DOC_MATRIX owns card-matrix reductions. C owns frontend only if a server prompt or snapshot change requires display support.

## Objective

Implement or prove a narrow 4D-04T trigger queue / `ORDER_TRIGGERS` breadth slice for server-authoritative APNAP ordering.

This slice follows the accepted 04S Lux paid-cost trigger work. It must reduce the open 04S audit risk that "broader trigger queue / APNAP ordering coverage" remains unproven, without claiming full trigger queue, full battle lifecycle, card-matrix readiness, frontend final validation or READY.

## Current Anchors

Runtime anchors:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolveOrderTriggersRuntime(...)`
  - `ResolveOrderTriggersOrderingPlayerId(...)`
  - `MatchesLegalApnapResolutionOrder(...)`
  - `BuildApnapTriggerControllerBlocks(...)`
  - `BuildLegalResolutionTriggerControllerBlocks(...)`
- `src/Riftbound.Contracts/GameCommandJsonMapper.cs`
  - strict `ORDER_TRIGGERS` command payload mapping.

Test anchors:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
  - `OrderTriggersApnapPromptMetadataExposesLegalDefaultOrderAndRedactsHiddenSources`
  - `OrderTriggersApnapLegalOrderingAcceptedAndMovesToStackDeterministically`
  - `OrderTriggersApnapIllegalCrossControllerReorderRejectedWithoutChangingState`
  - `BattleInitialTriggerOrderingRepresentativePathEntersOrderTriggersThenStackPriority`
- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`
  - real trigger queue representatives for destroyed-unit, draw, power and experience triggers.
- `docs/CURRENT_STAGE4D_04S_PAYMENTENGINE_LUX_HIGH_COST_PAID_COST_AUDIT.md`
  - keeps broader trigger queue / APNAP ordering coverage open after 04S.

## Allowed Write Scope

Default runtime/test write lock:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`

Optional contract/parser test if needed:

- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`

Optional docs after validation:

- `docs/CURRENT_STAGE4D_04T_TRIGGER_APNAP_ORDERING_AUDIT.md`
- `docs/CURRENT_STAGE4D_04T_TRIGGER_APNAP_ORDERING_EVIDENCE.md`

## Forbidden Scope

Do not touch without fresh A authorization:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- `data/official/card-catalog.zh-CN.json`
- frontend runtime / DevUi local rules
- Chrome / browser scripts
- formal 18-step E2E scripts
- broad PaymentEngine behavior unrelated to trigger ordering
- battle lifecycle rewrites outside the focused trigger-ordering representative
- protocol core fields unless a focused parser/contract test proves an existing mismatch
- `fullOfficial`
- READY / READY-CANDIDATE
- `riftbound-dotnet.sln`

## Implementation Contract

Keep the server as the only rules authority.

The slice must prove these properties with focused tests and only make runtime changes if a focused test exposes a real server bug:

1. `ORDER_TRIGGERS` prompt metadata exposes only server-authored legal order candidates and APNAP controller-block constraints.
2. The ordering player is server-derived from the current APNAP state; another player cannot submit the order.
3. Cross-controller reordering is rejected without mutation.
4. Within-controller reordering remains legal when all triggers belong to one controller block or stay inside the same controller block.
5. Submitted trigger ids must be complete, unique and current; missing, duplicate, stale or foreign ids are rejected without mutation.
6. The accepted order moves triggers to the stack deterministically, sets priority to the top stack item's controller and clears the trigger queue.
7. Hidden trigger source metadata remains redacted in prompts, snapshots and public events.
8. Real trigger queue representatives still pass through `ORDER_TRIGGERS -> stack priority -> resolution` without frontend rule calculation.

Do not close full APNAP / trigger queue, full battle lifecycle, full PaymentEngine, card matrix readiness or READY in this slice.

## Required Tests

Add or update focused tests for at least:

- non-ordering player rejection with no state mutation;
- missing / duplicate / foreign trigger ids rejection with no state mutation;
- within-controller reorder accepted while cross-controller reorder remains rejected;
- hidden source redaction remains intact in `ORDER_TRIGGERS` metadata;
- a real trigger queue representative still enters the order window and resolves through the stack.

Prefer extending focused existing tests over broad fixtures. Keep test names filterable by `OrderTriggers|TriggerQueue|APNAP|RealTriggerQueue`.

## Required Validation

Run focused trigger-ordering coverage:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerQueue|FullyQualifiedName~APNAP|FullyQualifiedName~RealTriggerQueue"
```

Run adjacent prompt / command / hub coverage:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~GameHub|FullyQualifiedName~OrderTriggers|FullyQualifiedName~TriggerPayment"
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
- whether any runtime bug was fixed or the slice is test/evidence-only;
- whether hidden-info leakage was found;
- whether protocol shape changed;
- remaining open P0/P1/P2 items;
- final conclusion: **NOT READY** unless A later proves all Stage 4 gates.

Do not output READY-CANDIDATE. Do not mark the goal complete.
