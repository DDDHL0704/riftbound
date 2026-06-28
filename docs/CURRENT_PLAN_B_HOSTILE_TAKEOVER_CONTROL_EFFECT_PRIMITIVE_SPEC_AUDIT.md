# Plan B Hostile Takeover Control Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving the low-risk control primitive metadata for `SFD·202/221` 恶意收购 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional control metadata:
  - `GainsControl`
  - `ControlDestinationZone`
  - `ReadiesTarget`
  - `ExhaustsControlledTarget`
  - `ControlDuration`
  - `ControlReturnDestinationZone`
  - `ControlReturnCountsAsMove`
- `EffectPhraseParser` parses Hostile Takeover's direct control text into enemy battlefield-unit target scope, battlefield control destination, ready-target flag, and end-turn control-return / recall metadata.
- `BehaviorTemplatePrimitiveExecutor` adds a `gain-control-target` primitive kind and emits it only when `BehaviorSpec.Effects` supplies a complete control target scope and destination.
- The primitive executor treats Hostile Takeover's parsed end-turn `Recall` secondary template as covered by the control primitive metadata instead of requiring an immediate recall primitive.
- Dev UI catalog types mirror the new optional effect fields.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `SFD·202/221` 恶意收购:
  - `获得战场上一名敌方单位的控制权。让其变为活跃状态。（如果该战场上存在其他敌方单位，则开始战斗。否则征服此战场。）`
  - `回合结束时，失去该单位的控制权，然后将它召回。（把它送回基地，此行动不算作移动。）`
- Existing representative rule evidence:
  - `docs/rules-evidence-index.md` entry for `p2-preflight-play-hostile-takeover-gain-control-ready-battlefield-unit`.
  - `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hostile-takeover-gain-control-ready-battlefield-unit.fixture.json`.
  - `tests/Riftbound.ConformanceTests/Fixtures/p5-hostile-takeover-end-turn-return-recall.fixture.json`.
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- This slice does not implement Hostile Takeover's battle/conquer branch after control is gained.
- Reversal stack-spell control, Forced Conscription optional experience branch, immediate control-and-recall variants, full control-zone movement lifecycle, legal official-deck score-victory replay breadth, and READY remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryHostileTakeoverControlPrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3419/3419 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8940/8940 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
