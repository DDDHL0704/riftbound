# Stage 4C-97 Arena / Minion / Annie Evidence Alignment Evidence

Date: 2026-05-13

This evidence file records a matrix-only alignment pass for Arena Service Crew, the three official Minion token factory FUs, and Dark Child / Annie.

## Evidence Sources

- `p2-preflight-play-arena-service-crew-equipment-ready.fixture.json`
- `p2-preflight-play-arena-service-crew-equipment-trigger-static.fixture.json`
- `p4-play-arena-service-crew-target-rejected.fixture.json`
- `P6TokenFactoryMarksOnlyOfficialMinionTokenFamily`
- `P79LegendTriggerAnnieReadiesTwoRunesAtTurnEnd`
- `CoreRuleEngineReadiesArenaServiceCrewWhenEquipmentPlayed`
- `P79ArenaServiceCrewReadiesWhenControllerPlaysEquipment`
- `P79ArenaServiceCrewSkipsOpponentEquipment`

Current HEAD validation:

- Focused regression: 6/6 passed.
- Adjacent regression: 87/87 passed.
- Backend full: 3771/3771 passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed before doc sync.

## Matrix Evidence

`docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` now contains `stage4CBatch97ArenaServiceCrewMinionAnnieEvidenceAlignment`, with five verified FUs:

- `FU-d5e1143438` / `OGN·091/298` / 竞技场勤务小队
- `FU-fe2295424f` / `OGN·271/298` / 随从（德玛西亚）
- `FU-bf81e73326` / `OGN·272/298` / 随从（诺克萨斯）
- `FU-77e07d2cad` / `OGN·273/298` / 随从（祖安）
- `FU-4faaf1a186` / `OGS·017/024` / 黑暗之女

The matrix status count moves from:

- `IMPLEMENTED_TESTED`: 68
- `IMPLEMENTED_UNTESTED`: 12

to:

- `IMPLEMENTED_TESTED`: 73
- `IMPLEMENTED_UNTESTED`: 7

## Boundary

This is not a new runtime implementation. It is a representative evidence alignment pass based on already documented routes plus current HEAD validation.

`fullOfficial=false` remains unchanged for all five FUs. Final READY remains blocked by P0/P1 rule gaps, full-official card coverage, final audit, and post-clearance validation reruns.
