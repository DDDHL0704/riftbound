# Stage 4D-03CI PaymentEngine Darius Legend Resource Bridge Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DARIUS LEGEND BRIDGE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CI follows the accepted 4D-03BY legend resource-action bridge handoff, the 4D-03BZ next-dispatch gate, the 4D-03CB through 4D-03CE non-legend lane handoffs, the 4D-03CF Diana legend bridge handoff, the 4D-03CG Ornn legend bridge handoff, and the 4D-03CH KaiSa legend bridge handoff. It reserves only the Darius `OGN·253/298` / `OGN·302/298` / `OGN·302*/298` Inspire-gated `LEGEND_ACT` generated-mana branch for a future B-side bridge / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entries for `OGN·253/298`, `OGN·302/298` and `OGN·302*/298` Darius / `诺克萨斯之手` contain these relevant facts:

- card category: legendary;
- colors / hero: red and yellow / Darius;
- official resource-skill text: reaction tap, Inspire obtains 1 mana;
- activation restriction: the effect can be used only if another card has already been played this turn;
- official reminder text: generated resource skills cannot be targeted as responses by other spells.

Current evidence is useful but intentionally insufficient for `RESOURCE_SKILLS` closure:

- `MatchSession.cs` exposes `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` for `OGN·253/298`, `OGN·302/298` and `OGN·302*/298` through the `LEGEND_ACT` action domain.
- `CoreRuleEngine.cs` maps the same Darius legend ability to gain 1 mana and gates it with `RequiresPlayedAnotherCardThisTurn`.
- `ConformanceFixtureRunnerTests.cs` proves `P79LegendActDariusGainsManaAfterAnotherCardThisTurn` for the base `OGN·253/298` source and proves `P79LegendActDariusRequiresAnotherCardPlayedThisTurn` rejects premium `OGN·302/298` without a prior card this turn.
- `PaymentEngineCoverageAuditTests.cs` still keeps all three Darius rows as deferred legend resource-action bridge rows, not implemented `RESOURCE_SKILLS` closure rows.
- Existing Darius unit `HASTE_READY` or Draven / Darius non-legend evidence belongs to separate card work and cannot proxy this legend generated-resource bridge.

## 3. Future B Dispatch Boundary

Future 4D-03CI-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- build or extend a bridge verifier that names all three Darius source card nos `OGN·253/298`, `OGN·302/298` and `OGN·302*/298` explicitly and links the official resource-skill text to the current `LEGEND_ACT` implementation;
- bind the current ability id, source card group, reaction / Inspire timing, previous-card-this-turn gate, generated mana amount and explicit `RESOURCE_SKILLS` closure gap;
- prove base / premium / premium-alternate parity instead of borrowing a single base-card representative by proxy;
- require evidence for generated-mana consumption, wrong-timing rejection, no-prior-card rejection, exhausted or stale source rejection, handwritten-command no-mutation rollback and cleanup / lifetime behavior;
- require duplicate-spend prevention evidence for generated mana;
- preserve the reminder-text boundary that generated resource skills cannot be response targets;
- keep Diana, Ornn, KaiSa and all non-Darius bridge rows outside this slice.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused legend tests only if the verifier exposes a concrete bridge gap: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a new focused legend bridge test file;
- runtime only if A explicitly opens it after a concrete mismatch is proven: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs` and minimal contract types if required;
- docs: 4D-03CI-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CI handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Diana spell-duel-only bridge rows, Ornn equipment-only bridge rows, KaiSa spell-only bridge rows or any non-Darius parity rows;
- Jhin, Honeyfruit, Blue Sentinel or Lux non-legend lanes;
- Darius unit HASTE_READY, Darius / Draven non-legend work or other non-legend no-prior-card branches;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Darius legend bridge;
- broad stack, play-history or battle lifecycle rewrites beyond the future narrow Darius bridge context if explicitly dispatched;
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

Future acceptance for a dispatched B-side Darius bridge must rerun focused Darius legend bridge tests or verifier coverage, adjacent PaymentEngine / resource skill / legend / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CI does not close P0-005, P1, full-card matrix or READY. It only reserves a narrow Darius legend bridge boundary so future B-side work can explicitly connect existing Inspire-gated `LEGEND_ACT` evidence to the official `RESOURCE_SKILLS` closure contract without borrowing Diana / Ornn / KaiSa bridge evidence, Darius unit evidence or non-legend resource-skill lanes by proxy.
