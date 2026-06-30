# Plan B Source Ready On Equipment Played Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·091/298` 竞技场勤务小队 states that when the controller plays an equipment, the source becomes ready.

Existing engine evidence:

- `CoreRuleEngineReadiesArenaServiceCrewWhenEquipmentPlayed` proves the representative equipment-played ready trigger path.
- `P79ArenaServiceCrewReadiesWhenControllerPlaysEquipment` proves the P7.9 representative path writes `TRIGGER_RESOLVED` and `UNIT_READIED`.
- `P79ArenaServiceCrewSkipsOpponentEquipment` proves opponent equipment does not trigger the source.
- `P79ArenaServiceCrewSkipsEquipmentTriggerWhenSourceIsStandby` proves standby sources do not trigger.
- `docs/rules-evidence-index.md` already records the `p2-preflight-play-arena-service-crew-equipment-trigger-static`, `p2-preflight-play-arena-service-crew-equipment-ready`, and target-rejection evidence rows.

## Engine Evidence

Before this slice, `CoreRuleEngine.ResolveArenaServiceCrewEquipmentPlayedTriggers` selected sources through `ArenaServiceCrewEquipmentTriggerSourceEffectKind = ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT`.

After this slice:

- `CoreRuleEngine` no longer contains `ArenaServiceCrewEquipmentTriggerSourceEffectKind`.
- `CoreRuleEngine` no longer contains `ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT` as a runtime selector.
- `CardBehaviorRegistry` stores the official source-ready trigger flag and emitted effect kind on the `OGN·091/298` behavior row.
- `CoreRuleEngine.ResolveSourceReadyOnEquipmentPlayedTriggers` scans public controlled field source units and applies any matching `SourceReadiesWhenControllerPlaysEquipment` behavior fields.

## Test Evidence

- `TriggerSourceIdentityGuardTests.CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable` blocks reintroducing the Arena Service Crew runtime effect-kind selector in Core.
- `CardCatalogBaselineTests.ArenaServiceCrewEquipmentReadyCarriesOfficialBehaviorFields` locks the official row to `SourceReadiesWhenControllerPlaysEquipment=true` and effect kind `ARENA_SERVICE_CREW_EQUIPMENT_READY`.
- Existing Arena Service Crew focused regressions passed unchanged, proving trigger behavior is preserved.
- Baseline before this slice: backend full conformance passed `9031/9031`.
- Focused behavior-field gate passed `7/7`.
- Adjacent / hidden-info gate passed `3028/3028`.
- Backend full conformance passed `9032/9032`.

## Non-Claims

This evidence does not claim complete equipment-played trigger breadth, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, P0 completion, P1, or READY.
