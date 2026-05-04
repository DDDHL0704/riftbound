# Current P5 Status

Last updated: 2026-05-05

P5 goal:

> 完成 P5 装备、控制权、触发和替换系统：基于 P4 已验证代表路径，小批次实现完整装备 owner/controller/attached 状态、控制权变更与归还、触发队列、替换/防止效果、持续效果层与最小 battle/room 验证；保持 P2/P2.5/P3/P4 绿色，补测试/文档/状态文件并提交。

## Baseline Confirmation

- Starting commit: `e1e46ec docs: complete p4 final audit`
- Expected dirty state at phase start: only untracked `riftbound-dotnet.sln`
- P2 core rules preflight: `811/811 = 100.0%`
- P2.5 development test UI: complete
- P3 card catalog and BehaviorSpec system: complete
- P4 high-frequency keywords and base card paths: `392/392 = 100.0%`
- Latest recorded full suite before P5: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed `2563/2563`
- Latest recorded focused suites before P5:
  - `ConformanceFixtureRunnerTests`: `2482/2482`
  - `CardCatalogBaselineTests`: `23/23`
  - `GameHubJoinTests`: `16/16`

## Evidence Sources Read

- `docs/CURRENT_P4_STATUS.md`
- `docs/CURRENT_P3_STATUS.md`
- `docs/CURRENT_P2_STATUS.md`
- `docs/CURRENT_P2_5_STATUS.md`
- `docs/master-development-plan.md`
- `docs/START_HERE.md`
- `README.md`
- `docs/rules-evidence-index.md`
- `docs/conformance-fixture-format.md`
- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `data/official/card-catalog.zh-CN.json`

## P5 Scope

In scope:

- Equipment state: explicit owner/controller/attachment invariants, attach, detach, host following, host leaving, assemble costs/colors, agile auto-attach, Forge optional attach, inactive/active text boundaries.
- Control state: unit control changes, expiry, return, owner/controller separation, zone ownership after control changes, event and snapshot visibility.
- Trigger system: on-play, enter-field, leave-field, Last Breath, attack, defend, move, conquer, score, optional trigger ordering, trigger queue boundaries with priority/stack resolution.
- Replacement/prevention: spell/skill damage prevention, banish and replay, countering, retargeting, control-gain effects, until-end-of-turn expiry, replacement ordering relative to normal resolution.
- Continuous effects/layers: temporary power modifiers, keyword grants, equipment power modifiers, end-turn cleanup, and source-leaves boundary.
- Minimal battle/room or equivalent E2E validation for player-visible state changes.

Out of scope:

- P6 full-card batch migration.
- P7 final product UI.
- Committing official rules PDFs or FAQ files.
- Committing untracked `riftbound-dotnet.sln`.

## Current Engine Model Audit

- `CardObjectState` currently stores `AttachedToObjectId`, optional `OwnerId` / `ControllerId`, tags, damage, power, temporary modifiers, face state, combat flags, exhaustion, mana cost, and card number.
- P5.1 added optional explicit object identity fields. Existing objects may still rely on the legacy `PlayerZones` location inference until each mutation path is migrated.
- Object ownership and control are still inferred from the `PlayerZones` collection for legacy paths that do not populate `OwnerId` / `ControllerId`.
- `StackItemState` already stores `ControllerId`, so stack control can diverge from source-card zone ownership for existing representative paths.
- Equipment can be attached by setting `AttachedToObjectId`, but P4 keeps attached equipment in its controller's base list for the Long Sword representative path.
- Movement currently rejects source units with attached equipment. This is the intended P4 zero-side-effect boundary before P5 equipment following.
- Existing end-turn cleanup clears damage, global until-end-of-turn effects, card until-end-of-turn effects, and temporary power modifiers.
- Replacement/prevention behavior exists as targeted representative branches, not as a general replacement queue or layer system.
- Trigger behavior exists as direct representative branches, not as a general trigger queue.

## P4/P2 Representative Paths Available For P5

Equipment:

- `SFD·022/221 长剑`: official text includes agile and assemble red. P4.388 accepts one minimal `ASSEMBLE_EQUIPMENT` path from base to friendly field unit and sets `attachedToObjectId`.
- `SFD·011/221 取放自如`: official text attaches or detaches a weapon controlled by the unit controller, then draws. P2/P4 representative path toggles attachment and draws.
- `SFD·064/221 布甲`, `SFD·056/221 斯特拉克的挑战护手`, `SFD·186/221 旋转飞斧`, `SFD·139/221 夜之锋刃`, and Forge units are candidate expansion surfaces only until each path is verified.

Control:

- `SFD·202/221 恶意收购`: gains control of an enemy battlefield unit and readies it. Official text also requires end-turn loss of control and recall; that expiry/return is P5-owned.
- `UNL-140/219 强制征召` and `OGN·203/298 据为己有`: control-to-base representatives.
- `OGN·080/298 倒转神通`: stack spell control representative; stack item controller is already explicit.

Triggers:

- P4.386 standby/reaction representative paths provide controlled low-risk entry points.
- P4.384 and P4.385 movement, hunt, battle, conquest, and score representatives provide event surfaces for future trigger queue expansion.
- Last Breath and leave-field triggers remain deferred except for the P5.5 Watchful Sentinel draw representative.

Replacement/prevention:

- `OGN·145/298 坚毅不倒`: prevents all spell/skill damage this turn.
- `p2-preflight-play-counterstorm-prevent-next-damage`: prevents next damage.
- `p2-preflight-play-noxian-guillotine-next-damage-destroys`: delayed destroy on next damage.
- `SFD·200/221 奥术跃迁`: banish and replay representative.

Continuous effects:

- Temporary power and keyword-like effects currently exist as direct card object/global until-end-of-turn state.
- P5.7 owns the first explicit cleanup/layer boundary tests before broader behavior migration.

## Risk Layers

High risk:

- Explicit owner/controller fields and migration of every zone mutation.
- Host leaving play while equipment is attached.
- Equipment following a host between base and battlefield.
- Temporary control expiry plus recall to owner-controlled base.
- Trigger queue ordering across stack resolution, optional triggers, and priority windows.
- Multiple replacement effects applying to one event.
- Continuous effect layers when source equipment leaves play.

Medium risk:

- Assemble cost/color matrix for known equipment.
- Agile auto-attach after playing equipment.
- Forge optional attach choice and no-attach fallback.
- Public snapshot exposure of attached equipment and owner/controller.
- Retargeting and stack control edge cases.
- End-turn cleanup of temporary power, keyword grants, and prevention effects.

Low risk:

- Additional conformance coverage for existing representative paths.
- Zero-side-effect rejection boundaries for unsupported P5 features.
- Status documentation and evidence mapping.

## Deferred Boundaries At P5 Start

Until a P5 slice implements and validates a path, the following must remain rejected or limited to already-tested representative behavior with zero unintended side effects:

- Generic equipment movement/following.
- Generic assemble colors and costs outside the verified Long Sword representative.
- Agile auto-attach for arbitrary equipment.
- Forge optional attach.
- Inactive/active equipment text enforcement.
- Generic owner/controller separation for all object movements.
- Control expiry and return for temporary control effects.
- Generic trigger queue commands and trigger ordering.
- Generic replacement ordering.
- Multiple simultaneous replacement effects.
- Equipment power/keyword layer application after source leaves play.

## P5 Batch Plan

| Batch | Status | Target |
| --- | --- | --- |
| P5.0 | Done | Audit/status file only; no engine behavior changes. |
| P5.1 | Done | Equipment state invariant tests for owner/controller/attached boundaries with minimal model changes. |
| P5.2 | Done | Equipment attach/detach/follow/leave representative paths using `长剑` and `取放自如`. |
| P5.3 | Done | Control owner/controller separation and end-turn return using `恶意收购`. |
| P5.4 | Done | Trigger queue skeleton with one low-risk on-play or enter-field representative. |
| P5.5 | Done | Last Breath or leave-field trigger slice without broad trigger migration. |
| P5.6 | Done | Replacement/prevention slice using `坚毅不倒` and existing prevention representatives. |
| P5.7 | Done | Continuous effect/layer and end-turn cleanup slice. |
| P5.8 | Done | Host-leaves equipment detach boundary for explicit attached equipment. |
| P5.9 | Done | Completion audit, full validation, docs sync, and final P5 status update. |

Current progress after P5.9 lands: `P5 10/10 planned batches = 100.0%`; remaining planned batches: `0`.

## P5.1 Delivered

- Added optional `ownerId` / `controllerId` fields to `CardObjectState`.
- Exposed visible object `ownerId` / `controllerId` in snapshots while preserving face-down redaction.
- Extended conformance fixture parsing and partial expected-state comparison for object identity fields.
- Locked the verified Long Sword assemble path so declared identity must match the current field controller before attachment.
- Added a zero-side-effect rejection for an equipment source whose declared controller conflicts with its current field zone.
- Added `p5-equipment-state-assemble-long-sword-owner-controller.fixture.json` for official Long Sword text plus owner/controller/attachment evidence.
- Kept the scope intentionally narrow: no generic equipment following, generic assemble cost matrix, agile auto-attach, Forge, or control expiry was added in P5.1.

P5.1 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2566/2566`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2485/2485`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.2 Delivered

- Added a minimal equipment-following path for `MOVE_UNIT` when the host unit and all attached equipment explicitly declare matching `ownerId` / `controllerId`.
- Preserved the P4 rejection boundary for attached equipment that lacks explicit identity, so older unmodeled attachment states still reject with zero side effects.
- Added `EQUIPMENT_MOVED_WITH_UNIT` events for the accepted host-following representative path.
- Added direct engine tests for attached Long Sword moving with its host from base to battlefield and from battlefield to base.
- Added `p5-move-unit-attached-equipment-follows-host.fixture.json`.
- Upgraded the existing `取放自如` attach/detach conformance fixtures to compare unit/equipment `ownerId` / `controllerId`, proving attach and detach preserve explicit identity.
- Kept precise battlefield roaming, generic equipment movement, agile auto-attach, Forge, inactive text, and broader host-leaves zone-choice handling deferred; P5.8 covers the explicit host-destroyed detach invariant.

P5.2 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2569/2569`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2488/2488`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.3 Delivered

- Upgraded battlefield control gain so controlled units keep original `ownerId` while `controllerId` changes to the gaining player.
- Added a narrow Hostile Takeover temporary-control effect: `RETURN_CONTROL_TO_OWNER_AT_TURN_END:{ownerId}`.
- At end turn, units with that effect return control to their owner and are recalled to the owner's base before the next turn starts.
- Added `UNIT_CONTROL_RETURNED` and `UNIT_RECALLED_TO_OWNER_BASE` events.
- Upgraded `p2-preflight-play-hostile-takeover-gain-control-ready-battlefield-unit.fixture.json` to compare owner/controller and the temporary return effect.
- Added `p5-hostile-takeover-end-turn-return-recall.fixture.json` for end-turn return and recall.
- Kept standby reveal, battle/conquest branch choice, control of attached equipment, and broader control-to-base expiry deferred.

P5.3 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2570/2570`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2489/2489`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.4 Delivered

- Added `TriggerQueueItemState` and a public `MatchState.TriggerQueue` / snapshot `triggerQueue` shape.
- Added a narrow queued-trigger representative for Teemo-style “when you play me, I get +3 this turn”.
- The representative writes `TRIGGER_QUEUED`, immediately resolves the trigger in the current safe stack-resolution boundary, writes `TRIGGER_RESOLVED`, then applies the existing power modifier.
- Updated hand-play Teemo fixtures and the standby-reaction Teemo fixture to include trigger queue events.
- Added direct assertions that the queue is empty after resolution.
- Kept optional trigger ordering, priority choices, attack/defense/move/conquer/score triggers, and generalized trigger windows deferred.

P5.4 validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2570/2570`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2489/2489`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.5 Delivered

- Added a narrow Last Breath representative for `OGN·096/298 警觉的哨兵`: when it is destroyed into graveyard from an explicit unit object, its controller queues and resolves `WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`.
- Reused the P5.4 trigger event shape: `TRIGGER_QUEUED`, `TRIGGER_RESOLVED`, then the existing `CARD_DRAWN` draw application.
- Added `p5-last-breath-watchful-sentinel-draw-on-destroy.fixture.json`, using `OGN·229/298 复仇` as the already-verified destroy representative.
- Added direct assertions that the destroyed Sentinel leaves `CardObjects`, the controller draws exactly one card, and the trigger queue is empty after resolution.
- Kept conditional Last Breath, optional trigger ordering, trigger choices, lethal-cleanup triggers, battle triggers, and generic leave-field/equipment leave triggers deferred.

P5.5 validation:

- Narrow preflight: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineQueuesWatchfulSentinelLastBreathDrawWhenDestroyed"` passed `1/1`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2571/2571`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2490/2490`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.6 Delivered

- Extended `OGN·145/298 坚毅不倒` from its existing spell-damage representative to the P4-verified Xerath activated-skill damage representative.
- `ResolveXerathDamageAbilityStackItem` now applies `PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN` when present, writes `DAMAGE_APPLIED` with `damage: 0`, `preventedDamage`, and `preventionEffectId`, and leaves the target undamaged.
- Added `p5-stand-firm-prevents-xerath-skill-damage.fixture.json`, covering the priority sequence where Xerath's skill is on the stack, Stand Firm is played as a reaction, Stand Firm resolves first, then the skill damage is prevented.
- Kept multiple replacement ordering, retargeting, replacement choice prompts, non-damage skill effects, and general replacement/prevention queues deferred.

P5.6 validation:

- Narrow preflight: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEnginePlaysStandFirmAndPreventsXerathSkillDamageThisTurn"` passed `1/1`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2572/2572`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2491/2491`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.7 Delivered

- Added a continuous-effect cleanup representative that combines two P5 surfaces: Teemo's trigger-created temporary power modifier and Stand Firm's global spell/skill-damage prevention.
- Added `p5-end-turn-cleans-trigger-power-and-prevention.fixture.json`, starting from a verified in-turn state and ending the turn.
- The fixture locks simultaneous expiry of global `untilEndOfTurnEffects` and object `untilEndOfTurnPowerModifier`, with `UNTIL_END_OF_TURN_EXPIRED`, `POWER_MODIFIER_EXPIRED`, and `CLEANUP_REPEATED`.
- Confirmed the Teemo object remains on the field, power returns from `4` to `1`, and `untilEndOfTurnPowerModifier` returns to `0`.
- Kept static equipment power layers, keyword-grant layers from equipment, source-leaves recalculation, and cross-layer dependency ordering deferred.

P5.7 validation:

- Narrow preflight: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineCleansTriggerPowerAndPreventionAtEndTurn"` passed `1/1`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2573/2573`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2492/2492`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.8 Delivered

- Added host-leaves cleanup for explicit attached equipment: when a unit is destroyed or banished through `TryDestroyTarget`, any equipment whose `attachedToObjectId` points to that host is detached before the host object is removed.
- Added `detachedEquipmentObjectIds` to the field-removal event payload when detach cleanup occurs.
- Added `p5-equipment-detaches-when-host-destroyed.fixture.json`, using `复仇` to destroy a unit that has an explicitly owned/controlled Long Sword attached.
- Confirmed the host leaves `CardObjects`, the equipment remains a public field object, and its `attachedToObjectId` is cleared instead of pointing at a removed object.
- Kept equipment destination choice after host leaves, attached equipment with incomplete identity, and source-leaves continuous layer recalculation deferred.

P5.8 validation:

- Narrow preflight: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed"` passed `1/1`.
- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2574/2574`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.

## P5.9 Completion Audit

P5 completion status: complete for the P5 representative scope. P5 deliberately did not batch-migrate all cards and did not enter P6/P7.

Completed representative capabilities:

- Equipment: explicit `ownerId` / `controllerId` / `attachedToObjectId`, Long Sword assemble identity, Take Up attach/detach identity preservation, attached equipment following explicit host movement, and host-destroyed detach cleanup.
- Control: Hostile Takeover battlefield control gain preserves owner, changes controller, schedules end-turn return, returns control to owner, and recalls the unit to owner base.
- Trigger queue: public `TriggerQueueItemState` / snapshot shape, Teemo on-play queued trigger, standby-reaction Teemo queued trigger, and Watchful Sentinel Last Breath queued/resolved draw.
- Replacement/prevention: Stand Firm global spell/skill damage prevention covers both spell damage and Xerath activated-skill damage; existing prevent-next-damage and banish-if-destroyed representatives remain green.
- Continuous effects: trigger-created temporary power modifier and global prevention effect expire together at end-turn cleanup.
- Minimal E2E: `GameHubJoinTests` remains green after all P5 batches; no Dev UI/P7 work was introduced.

Explicit deferred boundaries after P5:

- No P6 full-card migration; only verified representative paths are playable.
- Generic assemble color/cost matrix, agile auto-attach, Forge optional attach, inactive/active equipment text, and equipment destination choice after host leaves remain deferred.
- Generic control expiry beyond the verified Hostile Takeover path, control of attached equipment, and control-zone edge cases remain deferred.
- Generic trigger ordering, optional trigger prompts, attack/defense/move/conquer/score triggers, and simultaneous trigger ordering remain deferred.
- Multiple replacement ordering, retargeting, replacement choice prompts, non-damage skill prevention/replacement, and general replacement queues remain deferred.
- Equipment static power/keyword layers, source-leaves recalculation, and cross-layer dependency ordering remain deferred.

P5 commits:

- `e09d4c3 docs: start p5 audit`
- `ba719a6 feat: lock p5 equipment identity invariants`
- `02031cc feat: move attached equipment with host`
- `4eb09f1 feat: return hostile takeover control at end turn`
- `f710939 feat: queue teemo on-play trigger`
- `4ef58e0 feat: queue watchful sentinel last breath`
- `fb1e240 feat: prevent xerath skill damage`
- `4d82abd test: lock p5 continuous cleanup`
- `c27a2d5 feat: detach equipment when host leaves`

## Validation Policy

Each batch must keep prior P2/P2.5/P3/P4 suites green. `dotnet` commands must be run through:

```bash
source scripts/dev-env.sh
```

Required gates for P5 completion:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`
- `git diff --check`
- After each batch commit, `git status --short` should show only `?? riftbound-dotnet.sln`.

## Final P5 Validation

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2574/2574`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.
- `git diff --check`: passed.

## Next Step

P5 is complete. Do not enter P6 full-card batch migration, P7 final product UI, or commit `riftbound-dotnet.sln` unless the user explicitly starts the next goal.
