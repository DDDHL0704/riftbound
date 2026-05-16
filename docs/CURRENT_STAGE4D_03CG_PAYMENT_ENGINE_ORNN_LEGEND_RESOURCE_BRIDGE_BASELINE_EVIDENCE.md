# Stage 4D-03CG PaymentEngine Ornn Legend Resource Bridge Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03CG-B Ornn legend resource-action bridge / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `63fd4d1e docs: hand off diana legend resource bridge`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BY legend resource action bridge handoff / baseline has been accepted.
- 4D-03BZ next-dispatch gate has been accepted and keeps legend bridge rows separate from non-legend lanes.
- 4D-03CB through 4D-03CE separately handed off the four non-legend lanes.
- 4D-03CF separately handed off the Diana spell-duel-only legend bridge.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `DeferredResourceSkillFamilyManifest` contains `SFD·189/221` and `SFD·244/221` as `deferred-legend-resource-action-bridge` rows with current action domain `LEGEND_ACT`.
- `PaymentEngineDeferredResourceSkillNextDispatchGateManifest` includes both Ornn card nos only in `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER`, not in the non-legend runtime gate.
- The fixed official catalog says Ornn / `山隐之焰` is a green / blue legendary source whose reaction tap gains 1 power, and that power may only be spent to play equipment or use equipment skills.
- `MatchSession.cs` and `CoreRuleEngine.cs` expose `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` for the Ornn source card group and require pending equipment stack timing.
- Existing `ConformanceFixtureRunnerTests.cs` assertions prove base `SFD·189/221` gains power during a priority window for pending equipment.
- Those assertions are bridge inputs, not `RESOURCE_SKILLS` closure by proxy. A future verifier must explicitly bind the official resource-skill closure contract, equipment-only generated-resource restriction, lifetime / cleanup, rollback evidence and reprint parity.
- Ornn unit static-power / equipment-look / LayerEngine evidence is separate `SFD·085/221` / `SFD·085a/221` work and remains outside this legend bridge slice.
- Diana, KaiSa, Darius and premium / alternate bridge rows remain outside this Ornn slice.
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

This baseline does not prove Ornn resource-skill bridge closure. It preserves the open state for:

- explicit `RESOURCE_SKILLS` closure contract binding for `SFD·189/221` and `SFD·244/221`;
- equipment-only generated power consumption and lifetime;
- base / reprint parity for both Ornn source card nos;
- pending-equipment timing, wrong-timing, non-equipment stack, exhausted-source, stale-source and handwritten-command no-mutation evidence under the bridge contract;
- cleanup and duplicate-spend prevention for generated power;
- reminder-text boundary that generated resource skills cannot be response targets;
- Diana / KaiSa / Darius legend bridge rows outside this slice;
- Ornn unit static-power / equipment look / LayerEngine evidence outside this slice;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
