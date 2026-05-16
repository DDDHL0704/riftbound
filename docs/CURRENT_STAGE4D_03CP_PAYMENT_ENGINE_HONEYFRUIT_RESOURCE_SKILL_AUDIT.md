# Stage 4D-03CP PaymentEngine Honeyfruit Resource Skill Audit

Audit date: 2026-05-16
Conclusion: **FOCUSED HONEYFRUIT RESOURCE-SKILL SLICE ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CP follows the 4D-03CC Honeyfruit handoff and implements only the `UNL-049/219` Honeyfruit / `蜜糖果实` equipment-reaction resource-skill representative:

- ready base equipment reaction prompt;
- base branch: exhaust Honeyfruit to gain `A` as payment-only temporary power;
- level-six branch: exhaust Honeyfruit to gain `1` mana plus `A`, gated by at least 6 experience;
- immediate resolution without an ordinary stack item;
- command-side source, timing, level-six choice and generated-resource request revalidation.

This batch does not close Blue Sentinel, Lux, the 9 legend bridge candidates, frontend runtime, Chrome smoke, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Runtime Contract

`P4ActivatedAbilityCatalog` now exposes `HONEYFRUIT_REACTION_EXHAUST_GAIN_GENERIC_POWER` for `UNL-049/219` as a reaction-speed base-equipment resource skill.

`CoreRuleEngine` resolves Honeyfruit only in a stack-priority reaction window held by the controller. Accepted base resolution exhausts the source and creates a `TemporaryPaymentResourceState` with `PAY_RUNE_COSTS_ONLY_HONEYFRUIT_TEMPORARY_LEDGER_4D_03CP`. If the command includes the service-issued level-six choice and the controller has at least 6 experience, the resolver also adds 1 mana to `RunePool.Mana`.

`MatchSession` exposes the prompt only for a ready, public, controlled base equipment source. It exposes the level-six optional choice only when the player has enough experience, and source requirement metadata carries the restriction, generated power, upgraded mana, reaction policy and no ordinary stack policy.

## 3. Verifier Coverage

`tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs` covers 16 focused cases:

| Coverage slice | Assertion |
|---|---|
| Catalog shape | Ability id, source card, reaction speed, base-equipment source, generated power and restriction are catalog-bound. |
| Prompt gating | Honeyfruit appears only in legal reaction priority with a ready base equipment source; level-six choice appears only at 6 experience. |
| Base branch | Successful activation exhausts Honeyfruit, creates payment-only generic temporary power and opens no ordinary stack item. |
| Level-six branch | Upgraded activation also adds 1 mana while preserving the temporary payment-only power ledger. |
| Later legal payment | Generated mana plus temporary power can pay a later legal rune cost and clear cleanly. |
| Cleanup | Unused temporary power and rune-pool mana clear at turn cleanup. |
| No-mutation rejection | Wrong timing, targets, exhausted / stale / non-Honeyfruit source, illegal upgraded branch, wrong branch source, unsupported amount, handwritten temporary resource and duplicate spend reject without mutation. |

## 4. Coverage Manifest Changes

`PaymentEngineCoverageAuditTests.cs` now records Honeyfruit as an implemented representative:

- `ResourceSkillCoverageManifest` tracks 21 current catalog `IsResourceSkill=true` representative ability ids.
- `ResourceSkillOfficialBreadthManifest` marks `UNL-049/219` as implemented representative.
- `DeferredResourceSkillFamilyManifest`, `DeferredResourceSkillNextDispatchGateManifest` and `DeferredNonLegendResourceSkillRuntimeLaneManifest` now leave only Blue Sentinel and Lux in the non-legend deferred runtime lane.
- `ResourceSkillAllWindowMatrixManifest` expands to 48 representative rows because Honeyfruit is now a catalog-bound family.

These are representative audit updates only; P0-005 and full official `[A]` / `[C]` resource-skill breadth remain open.

## 5. Validation

Commands run:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~HoneyfruitResourceSkillTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HoneyfruitResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HoneyfruitResourceSkillTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~P4ActivatedAbilityCatalogAuditsDeferredSkillSurfacesAgainstOfficialText"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Results:

- Focused Honeyfruit resource skill tests: passed 16/16.
- PaymentEngine coverage audit tests: passed 133/133.
- Adjacent PaymentEngine / resource skill / legend / prompt / hub regression: passed 721/721.
- Focused Honeyfruit / coverage / fixture-runner catalog audit recheck: passed 150/150.
- Backend full: passed 4636/4636.

## 6. Non-Closure

4D-03CP is accepted as a focused Honeyfruit representative only. It does not close P0-005, P1, full-card matrix, frontend final validation, Chrome smoke, formal 18-step E2E or READY.

Remaining non-legend resource-skill lanes are Blue Sentinel and Lux. Legend bridge resource-skill closure, card matrix JSON, `fullOfficial` and final frontend gates remain future work.
