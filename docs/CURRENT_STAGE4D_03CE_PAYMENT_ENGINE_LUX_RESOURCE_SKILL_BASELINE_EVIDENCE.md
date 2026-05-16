# Stage 4D-03CE PaymentEngine Lux Resource Skill Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03CE-B Lux spell-only tap-reaction resource-skill implementation / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `8610f0eb docs: hand off blue sentinel resource skill lane`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03CA non-legend resource skill runtime lane gate has been accepted.
- 4D-03CB Jhin movement-triggered resource skill lane has been separately handed off.
- 4D-03CC Honeyfruit equipment-reaction resource skill lane has been separately handed off.
- 4D-03CD Blue Sentinel delayed next-main resource skill lane has been separately handed off.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `PaymentEngineDeferredNonLegendResourceSkillRuntimeLaneManifest` contains `LANE_LUX_SPELL_ONLY_TAP_REACTION_RESOURCE_SKILL` for `OGS·014/024`.
- The lane currently requires tap reaction timing, ready source state, pending spell payment context, generated mana lifetime, audit evidence, spell-only restriction and no-mutation rollback.
- The fixed official catalog says Lux / `拉克丝` is a yellow Demacia hero unit, costs 4, has 2 power, and can tap as a reaction to gain 2 generated resources only usable to play spells.
- Existing code and fixtures prove Lux play / preflight / target-rejection shape, but they do not expose a P4 resource-skill prompt / command / audit path for spell-only generated resource creation, consumption or cleanup.
- Jhin, Honeyfruit and Blue Sentinel have separate non-legend lane handoffs; the 9 `LEGEND_ACT` bridge candidates remain outside this Lux slice.
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
- `git diff --check`: passed after doc edits.

## 5. Non-Closure

This baseline does not prove Lux resource-skill runtime closure. It preserves the open state for:

- tap reaction timing and ready source gating;
- pending spell payment context capture;
- generated mana availability and spell-only consumption;
- source / pending spell state revalidation;
- generated-resource spend, cleanup and duplicate-spend prevention;
- command-side tap source / generated mana / spell-only restriction revalidation;
- non-spell payment, wrong-timing, exhausted-source, stale-source and handwritten-command rollback;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
