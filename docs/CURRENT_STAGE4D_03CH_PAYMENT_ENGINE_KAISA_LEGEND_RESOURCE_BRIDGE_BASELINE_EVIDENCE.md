# Stage 4D-03CH PaymentEngine KaiSa Legend Resource Bridge Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03CH-B KaiSa legend resource-action bridge / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `dccbac74 docs: hand off ornn legend resource bridge`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BY legend resource action bridge handoff / baseline has been accepted.
- 4D-03BZ next-dispatch gate has been accepted and keeps legend bridge rows separate from non-legend lanes.
- 4D-03CB through 4D-03CE separately handed off the four non-legend lanes.
- 4D-03CF separately handed off the Diana spell-duel-only legend bridge.
- 4D-03CG separately handed off the Ornn equipment-only legend bridge.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `DeferredResourceSkillFamilyManifest` contains `OGN·247/298`, `OGN·299/298` and `OGN·299*/298` as `deferred-legend-resource-action-bridge` rows with current action domain `LEGEND_ACT`.
- `PaymentEngineDeferredResourceSkillNextDispatchGateManifest` includes all three KaiSa card nos only in `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER`, not in the non-legend runtime gate.
- The fixed official catalog says KaiSa / `虚空之女` is a red / blue legendary source whose reaction tap gains 1 power, and that power may only be spent to play spells.
- `MatchSession.cs` and `CoreRuleEngine.cs` expose `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` for the KaiSa source card group and require pending spell stack timing.
- Existing `ConformanceFixtureRunnerTests.cs` assertions prove base `OGN·247/298` gains power during a priority window for a pending spell and premium `OGN·299/298` rejects a pending equipment stack item.
- Those assertions are bridge inputs, not `RESOURCE_SKILLS` closure by proxy. A future verifier must explicitly bind the official resource-skill closure contract, spell-only generated-resource restriction, lifetime / cleanup, rollback evidence and base / premium / alternate parity.
- KaiSa unit HASTE_READY / conquest draw evidence is separate unit-card work and remains outside this legend bridge slice.
- Diana, Ornn, Darius and non-KaiSa bridge rows remain outside this KaiSa slice.
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

- Focused PaymentEngine coverage guard: 127/127 passed.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 685/685 passed.
- Backend full: 4564/4564 passed.
- `git diff --check`: passed.

## 5. Non-Closure

This baseline does not prove KaiSa resource-skill bridge closure. It preserves the open state for:

- explicit `RESOURCE_SKILLS` closure contract binding for `OGN·247/298`, `OGN·299/298` and `OGN·299*/298`;
- spell-only generated power consumption and lifetime;
- base / premium / premium-alternate parity for all three KaiSa source card nos;
- pending-spell timing, wrong-timing, non-spell stack, exhausted-source, stale-source and handwritten-command no-mutation evidence under the bridge contract;
- cleanup and duplicate-spend prevention for generated power;
- reminder-text boundary that generated resource skills cannot be response targets;
- Diana / Ornn / Darius legend bridge rows outside this slice;
- KaiSa unit HASTE_READY / conquest draw / non-legend evidence outside this slice;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
