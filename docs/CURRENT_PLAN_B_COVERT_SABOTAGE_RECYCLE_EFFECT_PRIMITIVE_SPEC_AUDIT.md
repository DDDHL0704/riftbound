# Plan B Covert Sabotage Recycle Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk recycle primitive metadata for `OGN·156/298` 暗中破坏 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `RecyclesTarget`, `RecycleSourceZone`, `RecycleDestinationZone`, and `TargetForbiddenTag`.
- `EffectPhraseParser` parses `回收` target-effect text from official text.
- `EffectPhraseParser` maps `对手` + `手牌` to `TargetScope=OPPONENT_HAND_CARD`.
- `EffectPhraseParser` maps the official `非单位卡牌` restriction to `TargetForbiddenTag=CARD_TYPE:UNIT`.
- `EffectPhraseParser` maps hand recycle source/destination as `RecycleSourceZone=HAND` and `RecycleDestinationZone=MAIN_DECK`.
- `BehaviorTemplatePrimitiveExecutor` now prefers `BehaviorSpec.Effects` metadata for `Recycle` primitives before falling back to P2 behavior metadata.
- Dev UI catalog types mirror the new optional effect fields.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·156/298` 暗中破坏:
  - `指定一名对手，让其展示手牌，并从中选择一张非单位卡牌，让对手将其回收。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Opponent-hand reveal / target-selection hidden-info execution remains governed by existing prompt and snapshot boundaries; this slice does not add a new runtime reveal prompt.
- Complete recycle official breadth, hidden hand reveal UX/protocol breadth, owner deck placement breadth, legal official-deck score-victory replay, and READY remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryCovertSabotageRecyclePrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3416/3416 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8937/8937 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
