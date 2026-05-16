# Stage 4D-03CH PaymentEngine KaiSa Legend Resource Bridge Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B KAISA LEGEND BRIDGE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CH follows the accepted 4D-03BY legend resource-action bridge handoff, the 4D-03BZ next-dispatch gate, the 4D-03CB through 4D-03CE non-legend lane handoffs, the 4D-03CF Diana legend bridge handoff, and the 4D-03CG Ornn legend bridge handoff. It reserves only the KaiSa `OGN·247/298` / `OGN·299/298` / `OGN·299*/298` spell-only `LEGEND_ACT` generated-power branch for a future B-side bridge / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entries for `OGN·247/298`, `OGN·299/298` and `OGN·299*/298` KaiSa / `虚空之女` contain these relevant facts:

- card category: legendary;
- colors / hero: red and blue / KaiSa;
- official resource-skill text: reaction tap obtains 1 power;
- payment restriction: that generated power may be spent only to play spells;
- official reminder text: generated resource skills cannot be targeted as responses by other spells.

Current evidence is useful but intentionally insufficient for `RESOURCE_SKILLS` closure:

- `MatchSession.cs` exposes `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` for `OGN·247/298`, `OGN·299/298` and `OGN·299*/298` through the `LEGEND_ACT` action domain.
- `CoreRuleEngine.cs` maps the same KaiSa legend ability to gain 1 power and gates it to a priority window with a pending spell stack item.
- `ConformanceFixtureRunnerTests.cs` proves `P79LegendActKaisaGainsPowerInPriorityWindowForPendingSpell` for the base `OGN·247/298` source and proves `P79LegendActKaisaRequiresPendingSpellStackItem` rejects a premium source when the pending stack item is equipment.
- `PaymentEngineCoverageAuditTests.cs` still keeps all three KaiSa rows as deferred legend resource-action bridge rows, not implemented `RESOURCE_SKILLS` closure rows.
- Existing KaiSa unit HASTE_READY / conquest draw evidence belongs to separate unit-card work and cannot proxy this legend generated-resource bridge.

## 3. Future B Dispatch Boundary

Future 4D-03CH-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- build or extend a bridge verifier that names all three KaiSa source card nos `OGN·247/298`, `OGN·299/298` and `OGN·299*/298` explicitly and links the official resource-skill text to the current `LEGEND_ACT` implementation;
- bind the current ability id, source card group, reaction / priority timing, pending-spell restriction, generated power amount, spell-only payment restriction and explicit `RESOURCE_SKILLS` closure gap;
- prove base / premium / premium-alternate parity instead of borrowing a single base-card representative by proxy;
- require evidence for spell-only generated-power consumption, wrong-timing rejection, non-spell pending-stack rejection, exhausted or stale source rejection, no-mutation rollback and cleanup / lifetime behavior;
- preserve the reminder-text boundary that generated resource skills cannot be response targets;
- keep Diana, Ornn, Darius and all non-KaiSa bridge rows outside this slice.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused legend tests only if the verifier exposes a concrete bridge gap: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a new focused legend bridge test file;
- runtime only if A explicitly opens it after a concrete mismatch is proven: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs` and minimal contract types if required;
- docs: 4D-03CH-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CH handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Diana spell-duel-only bridge rows, Ornn equipment-only bridge rows, Darius Inspire-gated bridge rows or any non-KaiSa parity rows;
- Jhin, Honeyfruit, Blue Sentinel or Lux non-legend lanes;
- KaiSa unit HASTE_READY, conquest draw, spell damage or non-legend unit behavior;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the KaiSa legend bridge;
- broad stack, spell or battle lifecycle rewrites beyond the future narrow KaiSa bridge context if explicitly dispatched;
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

Future acceptance for a dispatched B-side KaiSa bridge must rerun focused KaiSa legend bridge tests or verifier coverage, adjacent PaymentEngine / resource skill / legend / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CH does not close P0-005, P1, full-card matrix or READY. It only reserves a narrow KaiSa legend bridge boundary so future B-side work can explicitly connect existing spell-only `LEGEND_ACT` evidence to the official `RESOURCE_SKILLS` closure contract without borrowing Diana / Ornn / Darius bridge evidence, KaiSa unit evidence or non-legend resource-skill lanes by proxy.
