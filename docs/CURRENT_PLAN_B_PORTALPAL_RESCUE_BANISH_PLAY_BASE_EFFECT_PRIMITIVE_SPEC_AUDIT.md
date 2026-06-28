# Plan B Portalpal Rescue Banish Play Base Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk banish/play-base primitive metadata for `OGN·102/298` 传送门大营救 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `BanishesTarget`, `PlayDestinationZone`, and `IgnoreCosts`.
- `EffectPhraseParser` parses `放逐` target-effect text from official text.
- `EffectPhraseParser` maps `一名友方单位` to `TargetScope=FRIENDLY_UNIT`.
- `EffectPhraseParser` maps `打出到其所属的基地` to `PlayDestinationZone=BASE`.
- `EffectPhraseParser` maps `无视费用` to `IgnoreCosts=true`.
- `BehaviorTemplatePrimitiveExecutor` now prefers `BehaviorSpec.Effects` metadata for `Banish` primitives before falling back to P2 behavior metadata.
- Dev UI catalog types mirror the new optional effect fields.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·102/298` 传送门大营救:
  - `{{迅捷}}（可在你的回合或法术对决中打出。）`
  - `放逐一名友方单位，然后让其拥有者将它打出到其所属的基地，无视费用。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Complete banish / play-to-base official breadth, battle / spell-duel timing breadth, control-zone movement breadth, automated evidence disposition, and legal official-deck score-victory replay remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryPortalpalRescueBanishPlayBasePrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3413/3413 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8934/8934 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
