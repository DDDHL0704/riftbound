# Stage 4C-97 Arena / Minion / Annie Evidence Alignment Audit

Date: 2026-05-13

Conclusion: **representative evidence aligned; project remains NOT READY**.

## Scope

This batch aligns five remaining `IMPLEMENTED_UNTESTED` FUs that already have representative automated evidence:

| FU | Card | Evidence route |
|---|---|---|
| `FU-d5e1143438` | `OGN·091/298` 竞技场勤务小队 | controller equipment-play ready trigger, opponent equipment no-trigger, zero-target play rejection |
| `FU-fe2295424f` | `OGN·271/298` 随从（德玛西亚） | official Minion token factory identity and `TOKEN_FAMILY:MINION` tag |
| `FU-bf81e73326` | `OGN·272/298` 随从（诺克萨斯） | official Minion token factory identity and `TOKEN_FAMILY:MINION` tag |
| `FU-77e07d2cad` | `OGN·273/298` 随从（祖安） | official Minion token factory identity, `TOKEN_FAMILY:MINION` tag, and represented runtime creation routes |
| `FU-4faaf1a186` | `OGS·017/024` 黑暗之女 | turn-end ready up to two exhausted runes legend trigger |

## Matrix Impact

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` now records:

- `stage4CBatch97ArenaServiceCrewMinionAnnieEvidenceAlignment`
- `functionalUnits[].stage4C97`
- `snapshotEntries[].stage4C97`

For the five FUs above, `stage4B.freezeStatus` changes from `IMPLEMENTED_UNTESTED` to `IMPLEMENTED_TESTED`, and `stage4B.automatedTests.status` changes to `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`.

Matrix status count now records:

- `IMPLEMENTED_TESTED`: 73
- `IMPLEMENTED_UNTESTED`: 7

`fullOfficial=false` remains unchanged.

## Validation

Focused regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineReadiesArenaServiceCrewWhenEquipmentPlayed|FullyQualifiedName~P79ArenaServiceCrewReadiesWhenControllerPlaysEquipment|FullyQualifiedName~P79ArenaServiceCrewSkipsOpponentEquipment|FullyQualifiedName~P4ArenaServiceCrewTargetRejectedFixture|FullyQualifiedName~P6TokenFactoryMarksOnlyOfficialMinionTokenFamily|FullyQualifiedName~P79LegendTriggerAnnieReadiesTwoRunesAtTurnEnd"
```

Result: 6/6 passed.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ArenaServiceCrew|FullyQualifiedName~TokenFactory|FullyQualifiedName~MinionTokenFamily|FullyQualifiedName~P79LegendTriggerAnnie|FullyQualifiedName~ReadyRunes|FullyQualifiedName~TurnEnd|FullyQualifiedName~CardCatalogBaselineTests"
```

Result: 87/87 passed.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 3771/3771 passed.

## Holdbacks

This batch does not close complete equipment-trigger ordering, full token factory domain, full legend action domain, FEPR, PaymentEngine, trigger engine, LayerEngine, hidden-info/redaction, FAQ full adjudication, 1009/811 full-official coverage, or final READY audit.
