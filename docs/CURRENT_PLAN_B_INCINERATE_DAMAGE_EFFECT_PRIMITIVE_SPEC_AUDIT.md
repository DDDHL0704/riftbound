# Plan B Incinerate Damage Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk damage primitive metadata for `OGS·003/024` 焚烧 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `DamageAmount`.
- `BehaviorEffectConditionKinds.None` records unconditional parsed effect primitives.
- `EffectPhraseParser` parses `造成N点伤害` from official text.
- `EffectPhraseParser` maps `战场上的一名单位` to `TargetScope=BATTLEFIELD_UNIT`.
- `BehaviorTemplatePrimitiveExecutor` now prefers `BehaviorSpec.Effects` metadata for `Damage` primitives before falling back to P2 behavior metadata.
- Dev UI catalog types mirror the new optional effect field.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGS·003/024` 焚烧:
  - `{{迅捷}}（可在你的回合或法术对决中打出。）`
  - `对战场上的一名单位造成2点伤害。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Full damage prevention/replacement, lethal cleanup breadth, spell-duel timing breadth, and legal official-deck score-victory replay remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryIncinerateDamagePrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3411/3411 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8932/8932 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
