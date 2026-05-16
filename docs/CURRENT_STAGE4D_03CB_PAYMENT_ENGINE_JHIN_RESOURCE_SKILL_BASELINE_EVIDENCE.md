# Stage 4D-03CB PaymentEngine Jhin Movement Resource Skill Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03CB-B Jhin movement-triggered resource-skill implementation / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `d847f27d test: split non-legend resource skill lanes`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03CA non-legend resource skill runtime lane gate has been accepted.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifest` contains `LANE_JHIN_MOVE_TRIGGERED_RESOURCE_SKILL` for `UNL-022/219`.
- The lane currently requires movement-trigger capture, generated mana plus generated power, payment-only lifetime, audit evidence and no-mutation rollback.
- The fixed official catalog says Jhin is a red hero unit with Spellshield / Roam and movement-triggered generated resource text.
- Existing code and fixtures prove Jhin play / preflight / Roam shape, but they do not expose a P4 resource-skill prompt / command / audit path for generated mana plus power.
- Honeyfruit, Blue Sentinel and Lux remain separate non-legend lanes; the 9 `LEGEND_ACT` bridge candidates remain outside this Jhin slice.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` remains full-official incomplete and must not be updated by this baseline.

## 4. Baseline Validation

Commands run:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Results:

- Focused PaymentEngine coverage guard: passed 127/127.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 685/685.
- Backend full: passed 4564/4564.
- `git diff --check`: passed.

## 5. Non-Closure

This baseline does not prove Jhin resource-skill runtime closure. It preserves the open state for:

- server-owned movement-trigger capture;
- generated mana plus generated power creation;
- payment-only lifetime and cleanup;
- command-side source / movement / payment revalidation;
- wrong-window, missing-trigger, stale-source, stale-movement-context, wrong-resource and handwritten-command rollback;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
