# Stage 4D-03CI PaymentEngine Darius Legend Resource Bridge Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03CI-B Darius legend resource-action bridge / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `32d6ec3d docs: hand off kaisa legend resource bridge`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BY legend resource action bridge handoff / baseline has been accepted.
- 4D-03BZ next-dispatch gate has been accepted and keeps legend bridge rows separate from non-legend lanes.
- 4D-03CB through 4D-03CE separately handed off the four non-legend lanes.
- 4D-03CF separately handed off the Diana spell-duel-only legend bridge.
- 4D-03CG separately handed off the Ornn equipment-only legend bridge.
- 4D-03CH separately handed off the KaiSa spell-only legend bridge.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `DeferredResourceSkillFamilyManifest` contains `OGN·253/298`, `OGN·302/298` and `OGN·302*/298` as `deferred-legend-resource-action-bridge` rows with current action domain `LEGEND_ACT`.
- `PaymentEngineDeferredResourceSkillNextDispatchGateManifest` includes all three Darius card nos only in `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER`, not in the non-legend runtime gate.
- The fixed official catalog says Darius / `诺克萨斯之手` is a red / yellow legendary source whose reaction tap, Inspire ability gains 1 mana only if another card has already been played this turn.
- `MatchSession.cs` and `CoreRuleEngine.cs` expose `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` for the Darius source card group, set `RequiresPlayedAnotherCardThisTurn` and gain 1 mana.
- Existing `ConformanceFixtureRunnerTests.cs` assertions prove base `OGN·253/298` gains mana after another card this turn and premium `OGN·302/298` rejects without a prior card this turn.
- Those assertions are bridge inputs, not `RESOURCE_SKILLS` closure by proxy. A future verifier must explicitly bind the official resource-skill closure contract, Inspire / previous-card gate, generated-mana lifetime / cleanup, rollback evidence and base / premium / alternate parity.
- Darius unit `HASTE_READY` or Darius / Draven non-legend evidence is separate card work and remains outside this legend bridge slice.
- Diana, Ornn, KaiSa and non-Darius bridge rows remain outside this Darius slice.
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

This baseline does not prove Darius resource-skill bridge closure. It preserves the open state for:

- explicit `RESOURCE_SKILLS` closure contract binding for `OGN·253/298`, `OGN·302/298` and `OGN·302*/298`;
- generated mana consumption and lifetime;
- base / premium / premium-alternate parity for all three Darius source card nos;
- Inspire / previous-card-this-turn gate, wrong-timing, no-prior-card, exhausted-source, stale-source and handwritten-command no-mutation evidence under the bridge contract;
- cleanup and duplicate-spend prevention for generated mana;
- reminder-text boundary that generated resource skills cannot be response targets;
- Diana / Ornn / KaiSa legend bridge rows outside this slice;
- Darius unit HASTE_READY and Darius / Draven non-legend evidence outside this slice;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
