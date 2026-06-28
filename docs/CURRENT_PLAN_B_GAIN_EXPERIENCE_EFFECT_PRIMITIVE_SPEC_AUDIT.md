# Plan B Gain Experience Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving direct gain-experience primitive metadata from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `ExperienceCount`.
- `EffectPhraseParser` parses direct `获得N经验` text from official card text.
- `EffectPhraseParser` avoids treating `{{狩猎}}` reminder text as the direct gain-experience effect phrase.
- Dynamic formulas such as `每有一名友方单位，便获得1经验` keep `ExperienceCount=null` and remain delegated.
- `BehaviorSpecCatalogBuilder` marks `GainExperience` as a safe existing P2 template mapping when a representative implementation exists.
- `BehaviorTemplatePrimitiveExecutor` adds a `gain-experience` primitive kind and only emits it when `BehaviorSpec.Effects` supplies a positive `ExperienceCount`.
- Dev UI catalog types mirror the new optional effect field.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `UNL-092/219` 德玛西亚使节:
  - `当你打出我时，获得1经验。`
- `UNL-034/219` 暖春之使:
  - `{{狩猎}}（当我征服或据守一处战场时，获得1经验。）`
  - `当你打出我时，获得2经验。`
- Existing representative rule evidence:
  - `docs/rules-evidence-index.md` entries `p2-preflight-play-demacia-envoy-experience-static` and `p2-preflight-play-spring-messenger-experience-static`.
  - `docs/p2-rules-preflight.md` entries for the same fixtures.
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive amount data-driven and auditable in `BehaviorSpec.Effects`.
- Dynamic experience formulas such as "每有一名友方单位，便获得1经验" remain delegated.
- Experience payment, activated abilities, Hunt conquest/hold experience, conditional delayed experience, legal official-deck score-victory replay breadth, and READY remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryGainExperiencePrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3418/3418 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8939/8939 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
