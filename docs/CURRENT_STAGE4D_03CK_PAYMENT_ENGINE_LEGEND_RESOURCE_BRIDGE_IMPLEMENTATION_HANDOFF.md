# Stage 4D-03CK PaymentEngine Legend Resource Bridge Implementation Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B LEGEND BRIDGE IMPLEMENTATION BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CK follows the accepted 4D-03CJ PaymentEngine legend resource bridge aggregate guard. The 03CJ guard proved the exact 9-card `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER` candidate set and prevented current `LEGEND_ACT` evidence from being used as `RESOURCE_SKILLS` closure by proxy.

This handoff converts that aggregate guard into the next concrete future B-side implementation / verifier boundary. It does not dispatch a worker in this batch, implement runtime behavior, modify tests, change frontend code, edit browser scripts, edit formal 18-step scripts, update card matrix JSON, change `fullOfficial`, change READY status or touch `riftbound-dotnet.sln`.

## 2. Candidate Set

Future B work must treat these four groups as one legend bridge implementation family, while preserving per-group timing and resource semantics:

| Bridge group | Source cards | Ability id | Resource / timing boundary |
|---|---|---|---|
| Diana spell-duel mana | `UNL-197/219` | `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` | Spell-duel-only focus timing, generated 1 mana, rollback and cleanup evidence. |
| Ornn equipment power | `SFD·189/221`, `SFD·244/221` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` | Pending-equipment reaction timing, generated 1 equipment-only power, rollback and cleanup evidence. |
| KaiSa spell power | `OGN·247/298`, `OGN·299/298`, `OGN·299*/298` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` | Pending-spell reaction timing, generated 1 spell-only power, rollback and cleanup evidence. |
| Darius Inspire mana | `OGN·253/298`, `OGN·302/298`, `OGN·302*/298` | `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` | Inspire / previous-card-this-turn timing, generated 1 mana, rollback and cleanup evidence. |

The future implementation must not collapse those rows into a single generic `LEGEND_ACT` pass. Each source group needs explicit source-card parity, prompt legality, command revalidation, generated-resource audit and lifetime evidence.

## 3. Future B Acceptance Contract

Future 4D-03CK-B work requires a fresh explicit A dispatch and a fresh write lock. The acceptance contract should include:

- official resource-skill text / reminder text bound to the current `LEGEND_ACT` ability id and source-card group;
- server-filtered prompt choices that expose only currently legal sources and actions, with no frontend inference;
- command-side revalidation of ability id, source object, source group, timing gate and resource action;
- no-mutation rollback evidence for wrong timing, stale pending item, missing previous card, stale source, exhausted source and handwritten illegal command;
- generated mana / power event or audit payload that proves source, type, amount, lifetime and consumption;
- duplicate-spend prevention and cleanup evidence for generated resource;
- base / premium / alternate parity for Ornn, KaiSa and Darius grouped source cards;
- preservation of the reminder boundary that generated resource skills cannot be targeted as responses by other spells;
- explicit `RESOURCE_SKILLS` closure evidence rather than reuse of existing `LEGEND_ACT` tests by proxy.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused legend bridge tests only if the verifier exposes a concrete closure gap: `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` or a new focused legend bridge test file;
- runtime only if A explicitly opens it after a concrete mismatch is proven: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs` and minimal protocol / mapper files if prompt metadata needs authoritative exposure;
- docs: 4D-03CK-B audit / evidence docs plus A-side checkpoint / completion / closure / dispatch / server audit / checklist updates.

## 4. No-Go Scope

This 4D-03CK batch and any future worker inheriting it must not touch without a fresh A write lock:

- Jhin, Honeyfruit, Blue Sentinel or Lux non-legend resource-skill runtime lanes;
- Darius unit HASTE_READY, Darius / Draven non-legend work, Ornn static-power / equipment-look work or KaiSa conquest draw work;
- broad PaymentEngine rewrites unrelated to the 9-card legend bridge implementation family;
- battle lifecycle, cleanup queues, LayerEngine or unrelated keyword execution boundaries;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- `fullOfficial` / READY status;
- `riftbound-dotnet.sln`.

## 5. Baseline Validation

Commands run for this handoff:

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
- `git diff --check`: passed after final 4D-03CK doc sync.

Future acceptance for a dispatched B-side legend bridge implementation must rerun focused legend bridge / verifier coverage, adjacent PaymentEngine / resource skill / legend / prompt / hub regression, backend full and `git diff --check`.

## 6. Remaining Risk

4D-03CK does not close P0-005, P1, full-card matrix or READY. It only turns the accepted 03CJ aggregate guard into a precise future implementation handoff so the next B-side slice can bridge Diana, Ornn, KaiSa and Darius generated-resource legend actions into explicit `RESOURCE_SKILLS` closure evidence without mixing in non-legend lanes or claiming completion from representative `LEGEND_ACT` tests alone.
