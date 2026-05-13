# Stage 4C-96 Legacy Guard Evidence Alignment Audit

Date: 2026-05-13

Conclusion: **representative evidence aligned; project remains NOT READY**.

## Scope

This batch aligns ten previously verified Stage 4C-60 through Stage 4C-69 direct-card representative evidence overlays with the current Stage 4B matrix status model.

Covered FUs:

| FU | Card | Effect | Source overlay |
|---|---|---|---|
| `FU-fe9dbeea3d` | `OGS·002/024` 烈火风暴 | enemy battlefield unit damage guard | `stage4C60` |
| `FU-b2e0e1d8da` | `OGN·123/298` 过载能量 | exhaust friendly field units plus battlefield damage guard | `stage4C61` |
| `FU-f877e60407` | `SFD·204/221` 狩猎 | ready all friendly field units guard | `stage4C62` |
| `FU-abf504d74e` | `OGN·132/298` 大副 | any-unit ready target-scope guard | `stage4C63` |
| `FU-6d67456a80` | `OGN·092/298` 怒海大鲨炮 | enemy battlefield unit target-scope damage guard | `stage4C64` |
| `FU-d68c203b01` | `UNL-092/219` 德玛西亚使节 | fixed experience source-unit route | `stage4C65` |
| `FU-c168bd394c` | `OGS·018/024` 提伯斯 | all battlefield unit damage guard | `stage4C66` |
| `FU-3f5a9ef0e0` | `SFD·062/221` 泡泡机 | friendly Mechanical ready target guard | `stage4C67` |
| `FU-7472703e56` | `SFD·174/221` 宝藏魔像 | create four Gold tokens route | `stage4C68` |
| `FU-2e2a00f575` | `OGN·211/298` 忠实的工坊主 | create Minion token route | `stage4C69` |

## Matrix Impact

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` now records:

- `stage4CBatch96LegacyGuardEvidenceAlignment`
- `functionalUnits[].stage4C96`
- `snapshotEntries[].stage4C96`

For the ten FUs above, `stage4B.freezeStatus` changes from `IMPLEMENTED_UNTESTED` to `IMPLEMENTED_TESTED`, and `stage4B.automatedTests.status` changes to `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`.

`fullOfficial=false` remains unchanged. This batch does not claim full official coverage.

## Validation

Focused legacy guard regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~FirestormEnemyBattlefieldDamageGuardTests|FullyQualifiedName~OverchargedEnergyGuardTests|FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~EnemyBattlefieldUnitTargetScopeGuardTests|FullyQualifiedName~CoreRuleEnginePlaysP2PreflightFixture|FullyQualifiedName~P4FixedExperienceGainOnPlayUpdatesControllerExperience|FullyQualifiedName~ResourceExperience|FullyQualifiedName~CoreRuleEnginePlaysTibbersDamageAllBattlefieldUnits|FullyQualifiedName~CoreRuleEngineTibbersResolutionSkipsOpponentControlledBattlefieldObject|FullyQualifiedName~CoreRuleEngineRejectsTibbersWhenTargetsAreProvided|FullyQualifiedName~CoreRuleEnginePlaysBubblebotReadyFriendlyMechanical|FullyQualifiedName~CoreRuleEngineRejectsBubblebotWhenTargetIsNotMechanical|FullyQualifiedName~CoreRuleEnginePlaysTreasureGolemCreateFourGold|FullyQualifiedName~CoreRuleEngineRejectsTreasureGolemWhenTargetsAreProvided|FullyQualifiedName~P4TreasureGolemTargetRejectedFixture|FullyQualifiedName~CoreRuleEnginePlaysFaithfulCraftsmanCreateMinion|FullyQualifiedName~CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided"
```

Result: 67/67 passed.

Adjacent legacy guard regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Firestorm|FullyQualifiedName~Overcharged|FullyQualifiedName~Hunt|FullyQualifiedName~ReadyAll|FullyQualifiedName~ReadiesAll|FullyQualifiedName~AnyUnit|FullyQualifiedName~TargetScope|FullyQualifiedName~EnemyBattlefield|FullyQualifiedName~Megashark|FullyQualifiedName~CrescentStrike|FullyQualifiedName~Demacia|FullyQualifiedName~Experience|FullyQualifiedName~GainExperience|FullyQualifiedName~Tibbers|FullyQualifiedName~DamageAllBattlefield|FullyQualifiedName~Bubblebot|FullyQualifiedName~Mechanical|FullyQualifiedName~TreasureGolem|FullyQualifiedName~Gold|FullyQualifiedName~FaithfulCraftsman|FullyQualifiedName~MinionTokenFamily|FullyQualifiedName~Viktor"
```

Result: 193/193 passed.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 3771/3771 passed.

## Holdbacks

This batch does not close complete same-battlefield, standby/reaction, quick, spell-duel, FEPR, PaymentEngine, replacement/prevention, cleanup, LayerEngine, hidden-info/redaction, FAQ full adjudication, 1009/811 full-official coverage, or final READY audit.
