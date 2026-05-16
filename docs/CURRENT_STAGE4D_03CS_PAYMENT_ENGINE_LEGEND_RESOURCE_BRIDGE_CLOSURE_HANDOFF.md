# Stage 4D-03CS PaymentEngine Legend Resource Bridge Closure Handoff

Audit date: 2026-05-16
Conclusion: **A-SIDE HANDOFF / FUTURE B LEGEND BRIDGE CLOSURE BOUNDARY / PROJECT NOT READY**

## 1. Scope

4D-03CS follows the accepted 4D-03CR Lux spell-only tap-reaction resource skill. After 4D-03CO / 03CP / 03CQ / 03CR, the deferred non-legend resource-skill runtime lanes for Jhin, Honeyfruit, Blue Sentinel and Lux are closed as focused representatives.

This handoff converts the remaining resource-skill gap into a single future B-side legend bridge closure boundary. It is docs-only. It does not dispatch a worker, implement runtime behavior, modify tests, change frontend code, edit Chrome / browser scripts, edit formal 18-step scripts, update card matrix JSON, change `fullOfficial`, change READY status or touch `riftbound-dotnet.sln`.

## 2. Current Manifest Facts

The current `PaymentEngineCoverageAuditTests` manifest state is:

- `ResourceSkillOfficialBreadthManifest` implemented candidates: 23.
- `ResourceSkillOfficialBreadthManifest` deferred candidates: 9.
- `PaymentEngineDeferredResourceSkillFamilyManifest` non-legend deferred candidates: none.
- The only remaining deferred family is `DeferredLegendResourceActionBridge`.
- The only current next-dispatch gate is `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER`.

The remaining candidate set is the exact 9-card Diana / Ornn / KaiSa / Darius legend bridge family already guarded by 4D-03CJ through 4D-03CN:

| Bridge group | Source cards | Ability id | Closure boundary |
|---|---|---|---|
| Diana spell-duel mana | `UNL-197/219` | `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` | `RESOURCE_SKILLS` closure must be explicit, not inferred from existing `LEGEND_ACT` evidence. |
| Ornn equipment power | `SFD·189/221`, `SFD·244/221` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` | Equipment-only timing, source-card parity, generated power lifetime and rollback must be proven under the resource-skill contract. |
| KaiSa spell power | `OGN·247/298`, `OGN·299/298`, `OGN·299*/298` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` | Spell-only timing, grouped source-card parity, generated power lifetime and rollback must be proven under the resource-skill contract. |
| Darius Inspire mana | `OGN·253/298`, `OGN·302/298`, `OGN·302*/298` | `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` | Inspire / previous-card timing, grouped source-card parity, generated mana lifetime and rollback must be proven under the resource-skill contract. |

## 3. Future B Acceptance Contract

Future B work must start from a fresh explicit A dispatch. The acceptance contract should prove the remaining 9-card legend bridge family as `RESOURCE_SKILLS`, not as a proxy reuse of `LEGEND_ACT` coverage:

- current official resource-skill text and reminder boundary are bound to the bridge action and source-card group;
- server-filtered `ActionPrompt` exposes only currently legal source objects and payment actions;
- command-side revalidation covers ability id, source object, source group, timing gate, exhausted state and requested resource action;
- generated mana / power audit payload proves source card, bridge group, resource kind, amount, lifetime, consumption and cleanup;
- wrong timing, stale pending item, missing previous card, stale source, exhausted source and handwritten illegal command all produce no-mutation rollback evidence;
- duplicate-spend prevention is explicit for generated resource;
- Ornn, KaiSa and Darius grouped variants prove base / premium / alternate source-card parity;
- existing 4D-03CM / 03CN `LegendResourceBridgeVerifierTests` evidence may be reused as input, but cannot by itself close the `RESOURCE_SKILLS` official axis.

Likely future B write scope:

- primary verifier: `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`;
- focused verifier: `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`;
- runtime only if a concrete mismatch is proven and A opens the lock: `src/Riftbound.Engine/MatchSession.cs`, `src/Riftbound.Engine/CoreRuleEngine.cs`, `src/Riftbound.Engine/P6LegendAbilityCatalog.cs`, and minimal protocol / mapper files if prompt metadata needs authoritative exposure;
- docs: future 4D-03CS-B audit / evidence docs plus checkpoint / completion / dispatch / server audit / checklist updates.

## 4. No-Go Scope

This 4D-03CS batch and any future worker inheriting it must not touch without a fresh A write lock:

- Jhin, Honeyfruit, Blue Sentinel or Lux non-legend resource-skill runtime lanes;
- unrelated Darius / Draven / Ornn / KaiSa card text outside the 9-card legend bridge resource family;
- broad PaymentEngine rewrites unrelated to the 9-card legend bridge closure;
- battle lifecycle, cleanup queues, LayerEngine or unrelated keyword execution boundaries;
- frontend runtime, stores, views, smoke scripts or formal 18-step scripts;
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`;
- `fullOfficial` / READY status;
- `riftbound-dotnet.sln`.

## 5. Baseline Validation

Command run for this handoff:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
```

Result:

- Focused PaymentEngine coverage guard: passed 133/133.

Required future B validation after implementation:

```sh
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~LegendResourceBridgeVerifierTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~LegendResourceBridgeVerifierTests|FullyQualifiedName~ResourceSkill|FullyQualifiedName~LegendAct|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Remaining Risk

4D-03CS does not close P0-005, P1, full-card matrix, frontend final-state reruns or READY. It only records that non-legend deferred resource-skill lanes are no longer the next direct resource-skill blocker and that the remaining direct resource-skill closure boundary is the exact 9-card legend bridge family.
