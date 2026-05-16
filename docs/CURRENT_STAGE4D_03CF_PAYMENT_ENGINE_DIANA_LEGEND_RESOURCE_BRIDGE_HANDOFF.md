# Stage 4D-03CF PaymentEngine Diana Legend Resource Bridge Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B DIANA LEGEND BRIDGE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CF follows the accepted 4D-03BY legend resource-action bridge handoff, the 4D-03BZ next-dispatch gate, and the 4D-03CB through 4D-03CE non-legend lane handoffs. It reserves only the Diana `UNL-197/219` spell-duel-only `LEGEND_ACT` generated-mana branch for a future B-side bridge / verifier slice.

This handoff does not implement runtime behavior, tests, frontend behavior, browser scripts, formal 18-step scripts, card matrix changes, `fullOfficial`, READY status or `riftbound-dotnet.sln`.

## 2. Current Inputs

The fixed official catalog entry for `UNL-197/219` Diana / `皎月女神` contains these relevant facts:

- card category: legendary;
- colors / hero: blue and purple / Diana;
- official resource-skill text: reaction tap obtains 1 mana;
- payment restriction: that mana may be spent only during a spell duel;
- official reminder text: generated resource skills cannot be targeted as responses by other spells.

Current evidence is useful but intentionally insufficient for `RESOURCE_SKILLS` closure:

- `MatchSession.cs` exposes `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` for `UNL-197/219`, `UNL-234/219` and `UNL-234*/219` through the `LEGEND_ACT` action domain.
- `CoreRuleEngine.cs` maps the same Diana legend ability to gain 1 mana and gates it to spell-duel focus timing.
- `ConformanceFixtureRunnerTests.cs` proves `P79LegendActDianaGainsManaDuringSpellDuelFocus` and `P79LegendActDianaRejectsOutsideSpellDuelFocus`.
- `P6LegendAbilityCatalog.cs` records the old deferred Diana representative as retired because `LEGEND_ACTION_DOMAIN` implements spell-duel focus resource timing for legend sources.
- `PaymentEngineCoverageAuditTests.cs` still keeps `UNL-197/219` as a deferred legend resource-action bridge row, not an implemented `RESOURCE_SKILLS` closure row.

## 3. Future B Dispatch Boundary

Future 4D-03CF-B work requires a fresh explicit A dispatch and a fresh write lock. Candidate B-side scope:

- build or extend a bridge verifier that names Diana `UNL-197/219` explicitly and links the official resource-skill text to the current `LEGEND_ACT` implementation;
- bind the current ability id, source card group, spell-duel focus timing, generated mana amount, payment-only restriction and explicit `RESOURCE_SKILLS` closure gap;
- prove that existing `LEGEND_ACT` prompt / command tests are bridge inputs only until the verifier names the resource-skill closure contract;
- require evidence for spell-duel-only consumption, wrong-timing rejection, exhausted or stale source rejection, no-mutation rollback and cleanup / lifetime behavior;
- preserve the reminder-text boundary that generated resource skills cannot be response targets;
- keep Ornn, KaiSa, Darius and all premium / reprint bridge rows outside this slice.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused legend tests only if the verifier exposes a concrete bridge gap: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a new focused legend bridge test file;
- runtime only if A explicitly opens it after a concrete mismatch is proven: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs` and minimal contract types if required;
- docs: 4D-03CF-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

This 4D-03CF handoff does not dispatch B and does not open those files now.

## 4. No-Go Scope

This batch and any future worker inheriting it must not touch without a fresh A write lock:

- Ornn equipment-only legend bridge rows, KaiSa spell-only legend bridge rows, Darius Inspire-gated legend bridge rows or any premium / alternate parity rows;
- Jhin, Honeyfruit, Blue Sentinel or Lux non-legend lanes;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- broad PaymentEngine rewrites outside the Diana legend bridge;
- broad spell-duel / stack / battle lifecycle rewrites beyond the future narrow Diana bridge context if explicitly dispatched;
- LayerEngine or unrelated keyword implementation;
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

Future acceptance for a dispatched B-side Diana bridge must rerun focused Diana legend bridge tests or verifier coverage, adjacent PaymentEngine / resource skill / legend / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CF does not close P0-005, P1, full-card matrix or READY. It only reserves a single Diana legend bridge boundary so future B-side work can explicitly connect existing spell-duel `LEGEND_ACT` evidence to the official `RESOURCE_SKILLS` closure contract without borrowing Ornn / KaiSa / Darius bridge evidence or non-legend resource-skill lanes by proxy.
