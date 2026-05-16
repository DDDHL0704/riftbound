# Stage 4D-03CJ PaymentEngine Legend Resource Bridge Aggregate Audit

Audit date: 2026-05-16
Conclusion: **TEST-ONLY AGGREGATE GUARD ACCEPTED / PROJECT NOT READY**

## 1. Scope

4D-03CJ follows the accepted 4D-03BY legend resource-action bridge handoff, the 4D-03BZ next-dispatch gate, the 4D-03CF Diana legend bridge handoff, the 4D-03CG Ornn legend bridge handoff, the 4D-03CH KaiSa legend bridge handoff and the 4D-03CI Darius legend bridge handoff.

This batch adds only an aggregate verifier in `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` plus A-side audit artifacts. It does not implement runtime behavior, modify frontend code, change browser scripts, edit formal 18-step scripts, update card matrix JSON, change `fullOfficial`, change READY status or touch `riftbound-dotnet.sln`.

## 2. Aggregate Guard

`LegendResourceBridgeAggregateManifest` records the four per-champion legend bridge groups that compose the official 9-card `B_LEGEND_RESOURCE_ACTION_BRIDGE_VERIFIER` candidate set:

| Bridge group | Candidate cards | Ability id | Required future boundary |
|---|---|---|---|
| Diana spell-duel mana | `UNL-197/219` | `LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA` | Bind spell-duel focus timing, generated mana lifetime / consumption, rollback and explicit `RESOURCE_SKILLS` closure gap. |
| Ornn equipment power | `SFD·189/221`, `SFD·244/221` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT` | Bind pending-equipment timing, equipment-only generated power, rollback and explicit `RESOURCE_SKILLS` closure gap. |
| KaiSa spell power | `OGN·247/298`, `OGN·299/298`, `OGN·299*/298` | `LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL` | Bind pending-spell timing, spell-only generated power, rollback and explicit `RESOURCE_SKILLS` closure gap. |
| Darius Inspire mana | `OGN·253/298`, `OGN·302/298`, `OGN·302*/298` | `LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA` | Bind Inspire / previous-card-this-turn timing, generated mana lifetime / consumption, rollback and explicit `RESOURCE_SKILLS` closure gap. |

The guard intentionally treats existing Diana / Ornn / KaiSa / Darius `LEGEND_ACT` tests as bridge inputs only. They cannot close `RESOURCE_SKILLS` by proxy.

## 3. New Test Assertions

The 4D-03CJ verifier adds three focused assertions:

- `PaymentEngineLegendResourceBridgeAggregateManifestMatchesLegendBridgeGateSet` proves the aggregate manifest exactly matches the 9-card legend bridge gate and the deferred family split.
- `PaymentEngineLegendResourceBridgeAggregateManifestRequiresPerChampionHandoffDocs` proves each champion group carries its ability id, timing / resource profile, required future evidence, per-champion handoff docs and 03CJ doc anchors.
- `PaymentEngineLegendResourceBridgeAggregateManifestRejectsProxyClosureAndReady` proves the aggregate guard keeps `NOT READY`, `P0-005 remains open`, `fullOfficial / READY` locked and `riftbound-dotnet.sln` locked.

## 4. Validation

Commands to run:

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
- `git diff --check`: passed after final 4D-03CJ doc sync.

## 5. Non-Closure

4D-03CJ does not close P0-005, P1, frontend final validation, full-card matrix or READY. It only makes the four accepted legend bridge handoffs executable as one aggregate guard so future B work cannot omit a champion group, mix with non-legend lanes or claim `RESOURCE_SKILLS` closure from existing `LEGEND_ACT` evidence by proxy.
