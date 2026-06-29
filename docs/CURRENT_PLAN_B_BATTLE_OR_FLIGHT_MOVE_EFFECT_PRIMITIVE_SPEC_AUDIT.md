# Plan B Battle Or Flight Move Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk move primitive metadata for `OGN·168/298` 战或逃 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

2026-06-29 follow-up: move-primitive target validation/prompt filtering now also consumes shared primitive metadata. `CoreRuleEngine` and `MatchSession` no longer branch on `BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE` or other representative move effect ids to require public field-unit targets; `RequiresVisibleFieldUnitPrimitiveTarget` derives that guard from `MovesTargetToBase` plus a unit target scope.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `MovesTarget`, `MoveCount`, and `MoveDestination`.
- `EffectPhraseParser` parses explicit `移动` target-effect text from official text.
- `EffectPhraseParser` maps `一名单位从战场上` to `TargetScope=BATTLEFIELD_UNIT`.
- `EffectPhraseParser` maps one moved unit to `MoveCount=1`.
- `EffectPhraseParser` maps `其所属的基地` to `MoveDestination=OWNER_BASE`.
- `BehaviorTemplatePrimitiveExecutor` now prefers fully specified `BehaviorSpec.Effects` metadata for `Move` primitives before falling back to P2 behavior metadata.
- Dev UI catalog types mirror the new optional effect fields.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·168/298` 战或逃:
  - `{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）`
  - `{{迅捷}}（可在你的回合或法术对决中打出。）`
  - `将一名单位从战场上移动到其所属的基地。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Move texts without an explicit destination, such as `移动一名敌方单位。`, remain delegated because this slice does not infer a destination from old behavior metadata.
- Multi-target movement, swap movement, controller-chosen destinations, Roam movement, combat movement, attachment-following movement breadth, legal official-deck score-victory replay, and READY remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryBattleOrFlightMovePrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3417/3417 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8938/8938 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.

2026-06-29 runtime target-guard follow-up:

- Red/green focused `PrimitiveTargetGuardSourceTests`: 1/1 passed after proving the old effect-id target guard was present.
- Source guard + Reprimand source guard: 2/2 passed.
- Affected representative move/return/destroy/control/power-swap guards: 105/105 passed.
- Adjacent primitive/catalog/recovery regression: 2401/2401 passed.
- Backend full conformance: 9023/9023 passed.
