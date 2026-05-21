# Stage 4D-04R LayerEngine Timestamp Dependency Audit

日期：2026-05-21
结论：**IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

本文件记录 4D-04R-B 的 A 侧验收。B-Implementation / Faraday `019e483b-a316-7312-a641-a7b8f3921814` 已完成 continuous-effect sequence / dependency representative slice；A 已审查 diff 并复跑 focused、adjacent、backend full 与 patch hygiene。

该切片只补服务端权威 continuous-effect metadata foundation，不关闭完整 LayerEngine、P1-001、P1-002、card matrix fullOfficial、frontend final validation、Chrome smoke、formal 18-step E2E 或 READY。

## 1. Scope

Runtime / test write scope used:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/OrnnFriendlyEquipmentStaticPowerTests.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

Not touched:

- frontend runtime / DevUi stores
- card matrix JSON / card coverage baseline
- official card catalog
- broad PaymentEngine
- battle lifecycle / task queue semantics
- hidden-info filter internals
- Chrome / browser scripts
- formal 18-step E2E scripts
- fullOfficial / READY status
- `riftbound-dotnet.sln`

## 2. Accepted Behavior

4D-04R-B is accepted because A verified all of the following:

1. `ContinuousEffectState` now exposes server-authored `Sequence` metadata after the existing deterministic continuous-effect sort.
2. Snapshot `timing.continuousEffects[]` includes additive `sequence` metadata for every continuous-effect entry.
3. Static aura representatives can expose `sourceDependencyObjectIds`, `targetDependencyObjectIds` and `participantDependencyObjectIds`.
4. Dependency metadata is derived from current authoritative public field state and filters out face-down objects.
5. Ornn friendly-equipment static aura exposes source / target / participant dependency metadata without leaking hidden face-down equipment ids.
6. Battlefield all-units static aura exposes source / target / participant dependency metadata and recomputes it when a participant leaves the battlefield.
7. Static aura dependency metadata disappears from state and snapshot when the source leaves the public field.
8. Existing `appliedOrder` semantics for ledger-backed power modifiers remain intact.
9. A representative mixed state with direct power modifier, minimum-power floor and static aura produces stable server sequence metadata.

## 3. Hidden Information Review

The new dependency helper only admits object ids that:

- are non-empty;
- exist in `CardObjects`;
- are not face-down;
- are currently located in a public field zone.

Focused tests prove that hidden face-down equipment object ids do not appear in static-aura participant lists or dependency arrays. This slice does not modify hidden-info filter internals and does not expose internal task ids.

## 4. Protocol / Snapshot Contract

This is an additive server-authored snapshot contract:

- `timing.continuousEffects[].sequence`
- `timing.continuousEffects[].sourceDependencyObjectIds`
- `timing.continuousEffects[].targetDependencyObjectIds`
- `timing.continuousEffects[].participantDependencyObjectIds`

Frontend remains display-only for this metadata. It must not compute LayerEngine ordering, dependency legality, power totals or hidden information.

## 5. A-Side Validation

Validation details are recorded in `docs/CURRENT_STAGE4D_04R_LAYERENGINE_TIMESTAMP_DEPENDENCY_EVIDENCE.md`:

- 04R focused timestamp / dependency tests: 5/5 passed.
- LayerEngine focused / adjacent filter: 42/42 passed.
- Backend full test: 5231/5231 passed.
- `git diff --check`: passed.

## 6. Residual Risk

Still open:

- full official LayerEngine;
- complete timestamp / dependency ordering beyond these representatives;
- source-ordering breadth;
- keyword gain/loss layering;
- multiple equipment/static aura interactions outside current representatives;
- replacement / prevention / prohibition / continuous-effect breadth;
- P1-001 and P1-002;
- card matrix fullOfficial;
- frontend final validation;
- Chrome smoke;
- formal 18-step E2E;
- completion audit and READY.

## 7. Verdict

4D-04R-B is accepted and its runtime / focused-test write lock is closed. Project remains **NOT READY**.
