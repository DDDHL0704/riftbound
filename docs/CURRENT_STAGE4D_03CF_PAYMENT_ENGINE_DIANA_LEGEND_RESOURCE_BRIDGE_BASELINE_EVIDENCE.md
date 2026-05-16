# Stage 4D-03CF PaymentEngine Diana Legend Resource Bridge Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the current repository state before any future 4D-03CF-B Diana legend resource-action bridge / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `df22d591 docs: hand off lux resource skill lane`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BY legend resource action bridge handoff / baseline has been accepted.
- 4D-03BZ next-dispatch gate has been accepted and keeps legend bridge rows separate from non-legend lanes.
- 4D-03CB through 4D-03CE separately handed off the four non-legend lanes.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `DeferredResourceSkillFamilyManifest` contains `UNL-197/219` as a `deferred-legend-resource-action-bridge` row with current action domain `LEGEND_ACT`.
- `PaymentEngineDeferredResourceSkillNextDispatchGateManifest` includes `UNL-197/219` only in `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER`, not in the non-legend runtime gate.
- The fixed official catalog says Diana / `皎月女神` is a blue / purple legendary source whose reaction tap gains 1 mana, and that mana may only be spent during a spell duel.
- `MatchSession.cs` and `CoreRuleEngine.cs` expose `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` for Diana source card groups and gate it to spell-duel focus.
- Existing `ConformanceFixtureRunnerTests.cs` assertions prove Diana gains mana during spell-duel focus and rejects outside spell-duel focus.
- Those assertions are bridge inputs, not `RESOURCE_SKILLS` closure by proxy. A future verifier must explicitly bind the official resource-skill closure contract, generated-resource restriction, lifetime / cleanup and rollback evidence.
- Ornn, KaiSa, Darius and premium / reprint bridge rows remain outside this Diana slice.
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

This baseline does not prove Diana resource-skill bridge closure. It preserves the open state for:

- explicit `RESOURCE_SKILLS` closure contract binding for `UNL-197/219`;
- spell-duel-only generated mana consumption and lifetime;
- source group / reprint parity for `UNL-197/219`, `UNL-234/219` and `UNL-234*/219`;
- wrong-timing, exhausted-source, stale-source and handwritten-command no-mutation evidence under the bridge contract;
- cleanup and duplicate-spend prevention for generated mana;
- reminder-text boundary that generated resource skills cannot be response targets;
- Ornn / KaiSa / Darius legend bridge rows outside this slice;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
