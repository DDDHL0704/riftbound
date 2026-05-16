# Stage 4D-03CG PaymentEngine Ornn Legend Resource Bridge Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B ORNN LEGEND BRIDGE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CG follows the accepted 4D-03BY legend resource-action bridge handoff, the 4D-03BZ next-dispatch gate, the 4D-03CB through 4D-03CE non-legend lane handoffs, and the 4D-03CF Diana legend bridge handoff. It reserves only the Ornn `SFD·189/221` / `SFD·244/221` equipment-only `LEGEND_ACT` generated-power branch for a future B-side bridge / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entries for `SFD·189/221` and `SFD·244/221` Ornn / `山隐之焰` contain these relevant facts:

- card category: legendary;
- colors / hero: green and blue / Ornn;
- official resource-skill text: reaction tap obtains 1 power;
- payment restriction: that generated power may be spent only to play equipment or use equipment skills;
- official reminder text: generated resource skills cannot be targeted as responses by other spells.

Current evidence is useful but intentionally insufficient for `RESOURCE_SKILLS` closure:

- `MatchSession.cs` exposes `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` for `SFD·189/221` and `SFD·244/221` through the `LEGEND_ACT` action domain.
- `CoreRuleEngine.cs` maps the same Ornn legend ability to gain 1 power and gates it to a priority window with a pending equipment stack item.
- `ConformanceFixtureRunnerTests.cs` proves `P79LegendActOrnnGainsPowerInPriorityWindowForPendingEquipment` for the base `SFD·189/221` source.
- `PaymentEngineCoverageAuditTests.cs` still keeps both Ornn rows as deferred legend resource-action bridge rows, not implemented `RESOURCE_SKILLS` closure rows.
- Existing Ornn unit static-power, equipment-look and LayerEngine evidence belongs to separate `SFD·085/221` / `SFD·085a/221` work and cannot proxy this legend generated-resource bridge.

## 3. Future B Dispatch Boundary

Future 4D-03CG-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- build or extend a bridge verifier that names both Ornn source card nos `SFD·189/221` and `SFD·244/221` explicitly and links the official resource-skill text to the current `LEGEND_ACT` implementation;
- bind the current ability id, source card group, reaction / priority timing, pending-equipment restriction, generated power amount, equipment-only payment restriction and explicit `RESOURCE_SKILLS` closure gap;
- prove base / reprint parity instead of borrowing a single base-card representative by proxy;
- require evidence for equipment-only generated-power consumption, wrong-timing rejection, non-equipment pending-stack rejection, exhausted or stale source rejection, no-mutation rollback and cleanup / lifetime behavior;
- preserve the reminder-text boundary that generated resource skills cannot be response targets;
- keep Diana, KaiSa, Darius and all premium / alternate parity rows outside this slice.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused legend tests only if the verifier exposes a concrete bridge gap: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a new focused legend bridge test file;
- runtime only if A explicitly opens it after a concrete mismatch is proven: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs` and minimal contract types if required;
- docs: 4D-03CG-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CG handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Diana spell-duel-only bridge rows, KaiSa spell-only bridge rows, Darius Inspire-gated bridge rows or any premium / alternate parity rows;
- Jhin, Honeyfruit, Blue Sentinel or Lux non-legend lanes;
- Ornn `SFD·085/221` / `SFD·085a/221` unit static power, equipment look, equipment keyword or LayerEngine work;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Ornn legend bridge;
- broad stack, equipment or battle lifecycle rewrites beyond the future narrow Ornn bridge context if explicitly dispatched;
- `fullOfficial` / READY status;
- `riftbound-dotnet.sln`.

## 5. Required Validation

Baseline validation for this handoff:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~SpellshieldTax|FullyQualifiedName~HasteReady|FullyQualifiedName~TriggerPayment|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

Future acceptance for a dispatched B-side Ornn bridge must rerun focused Ornn legend bridge tests or verifier coverage, adjacent PaymentEngine / resource skill / legend / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CG does not close P0-005, P1, full-card matrix or READY. It only reserves a narrow Ornn legend bridge boundary so future B-side work can explicitly connect existing equipment-only `LEGEND_ACT` evidence to the official `RESOURCE_SKILLS` closure contract without borrowing Diana / KaiSa / Darius bridge evidence, Ornn unit static evidence or non-legend resource-skill lanes by proxy.
