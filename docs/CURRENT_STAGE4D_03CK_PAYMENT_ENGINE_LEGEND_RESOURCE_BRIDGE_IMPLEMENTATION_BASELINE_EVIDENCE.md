# Stage 4D-03CK PaymentEngine Legend Resource Bridge Implementation Baseline Evidence

Audit date: 2026-05-16
Conclusion: **BASELINE / NO WORKER DISPATCHED / PROJECT NOT READY**

## 1. Baseline Scope

This baseline records the repository state after 4D-03CJ and before any future 4D-03CK-B legend resource bridge implementation / verifier dispatch.

This batch is docs-only. It does not modify runtime, tests, frontend, browser scripts, formal 18-step scripts, card matrix JSON, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Repository Facts

- Branch: `main`.
- Latest commit before this handoff batch: `e059f66b test: guard legend resource bridge aggregate`.
- Expected untracked file: `riftbound-dotnet.sln`.
- 4D-03BY legend resource action bridge handoff / baseline has been accepted.
- 4D-03BZ next-dispatch gate has been accepted and keeps legend bridge rows separate from non-legend lanes.
- 4D-03CF through 4D-03CI separately handed off Diana, Ornn, KaiSa and Darius per-champion legend bridge boundaries.
- 4D-03CJ accepted `LegendResourceBridgeAggregateManifest` as the exact 9-card aggregate guard for the future `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER` family.
- Active goal remains **NOT READY**.

## 3. Current Evidence Facts

- `PaymentEngineCoverageAuditTests.cs` contains `LegendResourceBridgeAggregateManifest` and verifies the 9-card candidate set exactly.
- The aggregate currently covers Diana `UNL-197/219`, Ornn `SFD·189/221` / `SFD·244/221`, KaiSa `OGN·247/298` / `OGN·299/298` / `OGN·299*/298`, and Darius `OGN·253/298` / `OGN·302/298` / `OGN·302*/298`.
- The current bridge ability ids are `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA`, `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT`, `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` and `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA`.
- Existing `ConformanceFixtureRunnerTests.cs` `P79LegendAct*` assertions are useful bridge inputs, but they remain insufficient for `RESOURCE_SKILLS` closure until source group parity, timing, generated-resource lifetime, consumption, cleanup, rollback and reminder-text boundaries are verified under the official resource-skill contract.
- Non-legend lanes for Jhin, Honeyfruit, Blue Sentinel and Lux remain separate future implementation lanes and must not be bundled into this legend bridge implementation boundary.
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

- Focused PaymentEngine coverage guard: passed 130/130.
- Adjacent PaymentEngine / resource skill / prompt / hub regression: passed 688/688.
- Backend full: passed 4567/4567.
- `git diff --check`: passed after final doc edits.

## 5. Non-Closure

This baseline does not prove legend resource-skill bridge closure. It preserves the open state for:

- explicit `RESOURCE_SKILLS` closure contract binding for all 9 source cards;
- source-card parity for Ornn, KaiSa and Darius grouped variants;
- generated mana / power consumption, lifetime, cleanup and duplicate-spend prevention;
- wrong timing, stale pending item, no-prior-card, stale source, exhausted source and handwritten-command no-mutation evidence;
- reminder-text boundary that generated resource skills cannot be targeted as responses;
- official card-matrix alignment for 1009 snapshot entries / 811 functional units.

Project status remains **NOT READY** and the active goal must remain open.
