# Stage 4D-03CR PaymentEngine Lux Resource Skill Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED LUX RESOURCE-SKILL SLICE ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CR implements only the `OGS·014/024` Lux spell-only tap-reaction resource-skill representative:

- server-filtered `PLAY_CARD` payment resource choices for ready controlled Lux sources in base or battlefield;
- generated 2 mana that can be consumed only by spell `PLAY_CARD` payment;
- command-side revalidation of source card, readiness, controller, field zone, spell-only use, necessity and duplicate source shape;
- inline generated-mana spend and cleanup audit without creating an ordinary stack item for the resource skill.

This batch does not close legend resource bridge closure, frontend runtime, Chrome smoke, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Runtime Contract

`P4ActivatedAbilityCatalog` exposes `LUX_REACTION_EXHAUST_GAIN_2_SPELL_ONLY_MANA` for `OGS·014/024` as a reaction-speed, payment-only resource skill that exhausts Lux and generates 2 spell-only mana.

`MatchSession` now lets a spell remain playable when the current mana shortfall can be covered by ready controlled Lux sources in base or battlefield. The prompt exposes `LUX_SPELL_ONLY_RESOURCE:<sourceObjectId>` as a payment resource choice with spell-only metadata, generated mana amount, restriction id and non-targetable generated-resource policy.

`CoreRuleEngine` handles the Lux resource action inside the authoritative `PLAY_CARD` payment path. It adds generated mana only for the current spell payment, exhausts the selected Lux source, records `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `MANA_GAINED`, `TEMPORARY_PAYMENT_RESOURCE_SPENT` and `TEMPORARY_PAYMENT_RESOURCE_CLEARED`, then removes leftover generated mana before the next state is published.

## 3. Verifier Coverage

`tests/Riftbound.ConformanceTests/LuxResourceSkillTests.cs` covers focused cases for:

| Coverage slice | Assertion |
|---|---|
| Catalog shape | Ability id, source card, reaction speed, generated mana, exhaustion cost and spell-only restriction are catalog-bound. |
| Prompt gating | Lux can make a short-mana spell playable and is not exposed as a unit-play resource. |
| Payment resolution | `PLAY_CARD` consumes Lux generated mana, exhausts Lux, pays the spell, cleans leftover generated mana and creates only the spell stack item. |
| Audit shape | `ABILITY_ACTIVATED`, `UNIT_EXHAUSTED`, `MANA_GAINED`, spend, cleanup and `COST_PAID` Lux payload fields are asserted. |
| No-mutation rejection | Non-spell use, exhausted source, wrong source card, unnecessary resource and duplicate source commands reject without mutation. |

## 4. Coverage Manifest Changes

`PaymentEngineCoverageAuditTests.cs` now records Lux as an implemented representative:

- `ResourceSkillCoverageManifest` tracks 23 current catalog `IsResourceSkill=true` representative ability ids.
- `ResourceSkillOfficialBreadthManifest` marks `OGS·014/024` as implemented representative.
- `DeferredResourceSkillFamilyManifest`, `DeferredResourceSkillNextDispatchGateManifest` and `DeferredNonLegendResourceSkillRuntimeLaneManifest` no longer carry a non-legend deferred Lux lane.
- `ResourceSkillAllWindowMatrixManifest` expands to 60 representative rows because Lux is now a catalog-bound family.

These are representative audit updates only; P0-005 and full official `[A]` / `[C]` resource-skill breadth remain open.

## 5. Validation

Validation commands for this batch:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~LuxResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results will remain batch-local evidence only and do not imply READY.

Final local results:

- focused Lux resource-skill tests: 9/9 passed;
- Lux + PaymentEngine coverage + fixture catalog audit: 143/143 passed;
- adjacent PaymentEngine / resource skill / legend / prompt / hub regression: 742/742 passed;
- backend full: 4657/4657 passed;
- `git diff --check`: passed.

## 6. Non-Closure

4D-03CR is accepted as a focused Lux representative only. It does not close P0-005, P1, full-card matrix, frontend final validation, Chrome smoke, formal 18-step E2E or READY.

Legend resource bridge closure, card matrix JSON, `fullOfficial` and final frontend gates remain future work.
