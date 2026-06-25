# Plan B / B5 Armament Equipment Source Tag Audit

Date: 2026-06-25

Status: focused armament equipment source-tag slice accepted; project remains **NOT READY**.

## Scope

This slice removes the Core engine card-number fallback that identified played armament equipment:

- Official catalog source: `data/official/card-catalog.zh-CN.json` marks the implemented equipment entries below with `Tag = 武装`.
- `CardBehaviorRegistry` now carries `SourceEquipmentTags = 武装` for implemented armament equipment that previously relied on `CoreRuleEngine.IsOfficialArmamentEquipmentCardNo(...)`.
- Existing compound equipment tags are preserved, for example `武装|灵便` and `武装|灵便|瞬息`.
- `CoreRuleEngine.IsArmamentPlayBehavior(...)` now checks only `CardBehaviorDefinition.SourceEquipmentTags` for `CardEquipmentKeywordNames.Weapon`.
- The old `IsOfficialArmamentEquipmentCardNo(...)` Core card-number allow-list is removed.

The implemented official armament entries covered by this registry guard are:

`UNL-019/219`, `UNL-039/219`, `UNL-096/219`, `UNL-158/219`,
`SFD·009/221`, `SFD·016/221`, `SFD·022/221`, `SFD·030/221`,
`SFD·033/221`, `SFD·042/221`, `SFD·051/221`, `SFD·056/221`,
`SFD·059/221`, `SFD·064/221`, `SFD·073/221`, `SFD·090/221`,
`SFD·095/221`, `SFD·102/221`, `SFD·108/221`, `SFD·115/221`,
`SFD·118/221`, `SFD·118a/221·P`, `SFD·124/221`, `SFD·133/221`,
`SFD·134/221`, `SFD·139/221`, `SFD·150/221`, `SFD·153/221`,
`SFD·161/221`, `SFD·172/221`, and `SFD·178/221`.

## Runtime Effect

- Playing those equipment cards now creates public equipment objects with the `武装` tag because the tag is carried by registry data, not inferred from a Core card-number list.
- `PLAYED_ARMAMENT_THIS_TURN:{playerId}` tracking remains available to effects that require the controller to have played an armament this turn, but the source predicate is now data-driven.
- Existing no-attach equipment fixtures were updated where they previously expected a bare `CARD_TYPE:EQUIPMENT` object despite the official catalog marking the card as `武装`.

## Non-Goals

- This does not implement full armament attach / detach / reattach lifecycle breadth.
- This does not complete `装配`, `灵便`, `百炼`, or weapon static modifier coverage.
- This does not migrate legend identity helpers or battlefield rule helpers.
- This does not close B0 full-game readiness or project READY.

## Validation

- Focused registry/source guard representatives: `2/2` passing.
- Adjacent `EquipmentKeyword|Armament|Assemble|EquipmentState` representatives: `181/181` passing.
- `FullGameEndToEnd`: `15/15` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8525/8525` passing.
- Source-helper count for `private static bool Is*CardNo(...)`: `35` total / `32` in `CoreRuleEngine`.
