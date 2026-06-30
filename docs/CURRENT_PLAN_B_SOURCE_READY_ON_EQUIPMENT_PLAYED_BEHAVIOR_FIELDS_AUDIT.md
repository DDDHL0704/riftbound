# Plan B Source Ready On Equipment Played Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the Arena Service Crew-specific runtime effect-kind selector from the shared equipment-played trigger path.

The stable catalog effect id `ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. `CoreRuleEngine` no longer references that id to decide whether a field unit readies when its controller plays equipment.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·091/298` 竞技场勤务小队: when the controller plays an equipment, ready the source.
- Existing evidence index entries `p2-preflight-play-arena-service-crew-equipment-trigger-static` and `p2-preflight-play-arena-service-crew-equipment-ready` record the official card row, `CORE-260330` unit/play/payment authorities, representative equipment play, `TRIGGER_RESOLVED`, and `UNIT_READIED`.
- Existing `P79ArenaServiceCrew*` tests cover the authoritative ready event, opponent-equipment rejection, and standby-source rejection.

## Implementation

- `CardBehaviorDefinition` now carries equipment-played source-ready trigger metadata:
  - `SourceReadiesWhenControllerPlaysEquipment`
  - `SourceReadyOnEquipmentPlayedEffectKind`
- `OGN·091/298` fills those fields with `true` and `ARENA_SERVICE_CREW_EQUIPMENT_READY`.
- `CoreRuleEngine.ResolveSourceReadyOnEquipmentPlayedTriggers` now scans public, controlled field source units, resolves each source through `CardBehaviorRegistry.TryGetByCardNo`, and applies matching behavior fields.
- Existing `TRIGGER_RESOLVED.trigger` and `UNIT_READIED.reason` payload values remain `ARENA_SERVICE_CREW_EQUIPMENT_READY`.

## Validation

- Baseline before this slice: backend full conformance passed `9031/9031`.
- Red focused source guard failed before implementation because `SourceReadiesWhenControllerPlaysEquipment` and `SourceReadyOnEquipmentPlayedEffectKind` did not exist.
- Green focused gate: `CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable|ArenaServiceCrewEquipmentReadyCarriesOfficialBehaviorFields|ArenaServiceCrew` passed `7/7`.
- Adjacent / hidden-info gate: `ArenaServiceCrew|TriggerSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3028/3028`.
- Backend full conformance passed `9032/9032`.

## Holdbacks

This does not close complete equipment-played trigger breadth, complete TriggerSpec migration, complete `ORDER_TRIGGERS` / APNAP ordering, P0 full objective, P1, or READY.
