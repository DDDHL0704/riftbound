# Plan B / Unit Boon-Granted Trigger Spec Audit

Date: 2026-06-27

Status: focused unit boon-granted ready-self TriggerSpec slice accepted; project remains **NOT READY**.

## 2026-07-02 Follow-up: Generic Predicate Surface

`UnitBoonGrantedTriggerSpecRules.TryGetUnitBoonGrantedReadySelfTrigger(...)` has been removed. `CoreRuleEngine.ApplyBoon(...)` now reaches the parsed ready-self trigger through `UnitBoonGrantedTriggerSpecRules.TryGetTrigger(cardNo, UnitBoonGrantedTriggerSpecRules.IsUnitBoonGrantedReadySelfTrigger, out trigger)`. Existing boon-grant mutation, ready event payloads, control guard, and already-booned skip behavior stay unchanged. Validation: focused guard / representative runtime set `38/38`, adjacent move / discard / boon / battlefield-held / recovery / full-game representative set `2790/2790`, backend full conformance `9141/9141`. This follow-up only removes the per-effect rules API; it does not add new official-text interpretation, complete boon-trigger ordering, or mark the project READY.

## Scope

This slice moves the implemented `SFD·047/221` Mountain Ape Elder boon-ready representative away from a Core card-number branch and into a shared BehaviorSpec trigger route.

- Official source: `data/official/card-catalog.zh-CN.json`, `SFD·047/221` / 山猿老祖: `当你给予我增益时，让我变为活跃状态。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = UNIT_BOON_GRANTED_READY_SELF`
  - `Timing = UNIT_BOON_GRANTED`
  - `TargetScope = SOURCE_UNIT`
  - `ReadiesSource = true`
- `CoreRuleEngine.ApplyBoon(...)` now asks `UnitBoonGrantedTriggerSpecRules.TryGetTrigger(..., UnitBoonGrantedTriggerSpecRules.IsUnitBoonGrantedReadySelfTrigger, ...)` for the target unit's parsed trigger spec after a new boon is successfully granted.
- Runtime events now use the parsed trigger kind for `TRIGGER_RESOLVED.trigger`, `TRIGGER_RESOLVED.effectKind`, and `UNIT_READIED.reason`.
- The old `MountainApeElderCardNo` / `MountainApeElderBoonReadyEffectKind` Core branch is removed.

## Boundaries

This is a narrow representative trigger-routing slice. It keeps the existing `ApplyBoon` semantics:

- the trigger only runs when the boon grant actually adds a new `增益` tag;
- an already-booned target does not receive another boon or ready again;
- the target must be controlled by the granting player, preserving the existing source-control guard.

This slice does not close the complete boon family, optional trigger ordering, generic boon-consume costs, full `ORDER_TRIGGERS`, full payment breadth, or overall READY.

## Validation

- Focused TDD: `UnitBoonGrantedReadySelf|MountainApeElder` passed 5/5 after the initial missing-contract red compile.
- Adjacent representatives: `UnitBoonGrantedReadySelf|MountainApeElder|Boon|ApplyBoon|MatchRecovery|CardCatalogBaseline` passed 2356/2356.
- Backend full conformance: `tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed 8809/8809.
- Current source-helper guard remains clean: no `private/static bool Is*CardNo(...)` helpers in `src/Riftbound.Engine`, `src/Riftbound.CardCatalog`, `src/Riftbound.Contracts`, or tests.
