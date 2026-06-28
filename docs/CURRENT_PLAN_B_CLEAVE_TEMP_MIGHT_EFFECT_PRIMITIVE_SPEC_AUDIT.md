# Plan B Cleave Temp Might Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk temporary power modifier primitive metadata for `OGN·004/298` 顺劈 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `PowerModifierAmount`.
- `EffectPhraseParser` parses `{{S}}+N` / `{{S}}-N` temporary power modifiers from official text.
- `EffectPhraseParser` maps unit target text to primitive target scope and keeps conditional attacking/defending text in `ConditionKind`.
- `BehaviorTemplatePrimitiveExecutor` now prefers `BehaviorSpec.Effects` metadata for `TempMight` primitives before falling back to P2 behavior metadata.
- Dev UI catalog types mirror the new optional effect field.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·004/298` 顺劈:
  - `{{迅捷}}（可在你的回合或法术对决中打出。）`
  - `让一名单位本回合内获得{{强攻3}}。（如果它是进攻方，则{{S}}+3。）`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Full Overwhelm / battle damage semantics, spell-duel timing breadth, LayerEngine duration cleanup breadth, and legal official-deck score-victory replay remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryCleaveTempMightPrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3410/3410 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8931/8931 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
