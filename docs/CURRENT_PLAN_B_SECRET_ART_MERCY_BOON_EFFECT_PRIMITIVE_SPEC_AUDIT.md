# Plan B Secret Art Mercy Boon Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk boon primitive metadata for `OGN·053/298` 秘奥义！慈悲度魂落 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `GrantsBoon` and `BoonPowerBonusAmount`.
- `EffectPhraseParser` parses `给予` + `增益` target-effect text from official text.
- `EffectPhraseParser` maps `一名友方单位` to `TargetScope=FRIENDLY_UNIT`.
- `EffectPhraseParser` maps the official `{{S}}+1增益` reminder to `BoonPowerBonusAmount=1`.
- `BehaviorTemplatePrimitiveExecutor` now prefers `BehaviorSpec.Effects` metadata for `Boon` primitives before falling back to P2 behavior metadata.
- TempMight template detection now scans per rules-text segment and skips boon reminder/global-boon text that contains `增益` without direct `战力` wording, preventing `{{S}}+1增益` from being misclassified as a direct until-end-of-turn power modifier.
- Dev UI catalog types mirror the new optional effect fields.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·053/298` 秘奥义！慈悲度魂落:
  - `给予一名友方单位增益。（如果该单位未拥有增益，则获得一个{{S}}+1增益。）`
  - `在本回合内，所有增益可额外给予友方单位{{S}}+1。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- The second Secret Art Mercy effect, `在本回合内，所有增益可额外给予友方单位{{S}}+1`, remains open and is not modeled as a direct TempMight primitive.
- Complete boon official breadth, repeat-boon stacking breadth, boon-trigger breadth, global boon modifier duration cleanup, legal official-deck score-victory replay, and READY remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarrySecretArtMercyBoonPrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3415/3415 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8936/8936 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
