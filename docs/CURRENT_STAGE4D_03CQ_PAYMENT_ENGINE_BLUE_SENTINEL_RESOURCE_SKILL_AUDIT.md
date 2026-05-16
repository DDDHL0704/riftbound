# Stage 4D-03CQ PaymentEngine Blue Sentinel Resource Skill Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED BLUE SENTINEL RESOURCE-SKILL SLICE ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CQ follows the 4D-03CD Blue Sentinel handoff and implements only the `UNL-087/219` Blue Sentinel held-battlefield delayed next-main resource-skill representative:

- server-side trigger capture when Blue Sentinel helps hold a battlefield;
- delayed generated power availability only in the controller's next main-phase pending rune payment;
- payment-only temporary power materialization during `PAY_COST`;
- trigger, source, battlefield, timing and generated-resource revalidation before mutation;
- audit events for trigger queueing/resolution, ability activation, generated power, spend and cleanup.

This batch does not close Lux, the legend resource bridge closure, frontend runtime, Chrome smoke, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Runtime Contract

`P4ActivatedAbilityCatalog` now exposes `BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_GAIN_GENERIC_POWER` for `UNL-087/219` as a battlefield-source resource skill with one generated generic payment-only power.

`CoreRuleEngine` queues a server-owned delayed trigger when Blue Sentinel is part of the defending side that holds the battlefield. During the controller's next main phase, a pending rune payment can request the server-issued `BLUE_SENTINEL_DELAYED_RESOURCE:<triggerId>` action. The resolver revalidates the trigger identity, source card, battlefield location, controller, next-main timing and rune-payment use before materializing a temporary payment resource with `PAY_RUNE_COSTS_ONLY_BLUE_SENTINEL_DELAYED_TEMPORARY_LEDGER_4D_03CQ`.

`MatchSession` exposes the delayed resource payment action only from the authoritative trigger queue and only when that generated power can help the current pending rune payment. Blue Sentinel does not appear as an ordinary `ACTIVATE_ABILITY` source, and the generated resource is not represented as a targetable ordinary stack item.

## 3. Verifier Coverage

`tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs` covers focused cases for:

| Coverage slice | Assertion |
|---|---|
| Catalog shape | Ability id, source card, battlefield-source requirement, generated power and payment-only restriction are catalog-bound. |
| Trigger capture | Held-battlefield resolution queues a server-owned Blue Sentinel trigger and emits `TRIGGER_QUEUED`. |
| Prompt gating | The delayed resource action appears only in the controller's next main pending rune-payment context. |
| Payment resolution | `PAY_COST` materializes the generated power, resolves the trigger, spends the temporary payment resource and clears it. |
| Audit shape | `TRIGGER_RESOLVED`, `ABILITY_ACTIVATED`, `POWER_GAINED`, `TEMPORARY_PAYMENT_RESOURCE_SPENT` and `TEMPORARY_PAYMENT_RESOURCE_CLEARED` are asserted without `STACK_ITEM_ADDED`. |
| No-mutation rejection | Wrong phase, late next-main window, missing trigger, stale source, stale battlefield, unsupported amount, duplicate spend, non-rune payment and forged temporary-resource commands reject without mutation. |

## 4. Coverage Manifest Changes

`PaymentEngineCoverageAuditTests.cs` now records Blue Sentinel as an implemented representative:

- `ResourceSkillCoverageManifest` tracks 22 current catalog `IsResourceSkill=true` representative ability ids.
- `ResourceSkillOfficialBreadthManifest` marks `UNL-087/219` as implemented representative.
- `DeferredResourceSkillFamilyManifest`, `DeferredResourceSkillNextDispatchGateManifest` and `DeferredNonLegendResourceSkillRuntimeLaneManifest` now leave only Lux in the non-legend deferred runtime lane.
- `ResourceSkillAllWindowMatrixManifest` expands to 54 representative rows because Blue Sentinel is now a catalog-bound family.

These are representative audit updates only; P0-005 and full official `[A]` / `[C]` resource-skill breadth remain open.

## 5. Validation

Final validation was refreshed after this audit file was created:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~BlueSentinelResourceSkillTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~P4ActivatedAbilityCatalogAuditsDeferredSkillSurfacesAgainstOfficialText"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BlueSentinelResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results will remain batch-local evidence only and do not imply READY.

Observed results:

- Focused Blue Sentinel / coverage / fixture-runner catalog audit recheck: passed 146/146.
- Adjacent PaymentEngine / resource skill / legend / prompt / hub regression: passed 733/733.
- Backend full: passed 4648/4648.
- `git diff --check`: passed.

## 6. Non-Closure

4D-03CQ is accepted as a focused Blue Sentinel representative only. It does not close P0-005, P1, full-card matrix, frontend final validation, Chrome smoke, formal 18-step E2E or READY.

Remaining non-legend resource-skill lane is Lux. Legend resource bridge closure, card matrix JSON, `fullOfficial` and final frontend gates remain future work.
