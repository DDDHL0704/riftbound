# Plan B Stay Away Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk effect primitive metadata for `UNL-042/219` 走开 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional primitive fields: `TargetScope`, `DrawCount`, `StatusEffectId`, and `ConditionKind`.
- `BehaviorEffectConditionKinds.PlayedFromHand` records card text that only applies when the source is played from hand.
- `EffectPhraseParser` parses:
  - `{{眩晕}}一名单位` as `TemplateId=stun`, `TargetScope=ANY_UNIT`, `StatusEffectId=STUNNED`.
  - `如果你从手牌中打出此牌，则抽一张牌` as `TemplateId=draw`, `DrawCount=1`, `ConditionKind=PLAYED_FROM_HAND`.
- `BehaviorTemplatePrimitiveExecutor` now prefers primitive metadata from `BehaviorSpec.Effects`; if a template phrase has no primitive metadata yet, it still falls back to the existing P2 behavior mapping.
- Dev UI catalog types mirror the new optional effect fields.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `UNL-042/219` 走开:
  - `{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）`
  - `{{迅捷}}（可在你的回合或法术对决中打出。）`
  - `{{眩晕}}一名单位。（使其在本回合内无法造成战斗伤害。）`
  - `如果你从手牌中打出此牌，则抽一张牌。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Standby reaction play of `走开`, full swift/reaction timing breadth, complete stun/draw hidden-info breadth, and full official deck score-victory replay remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryStayAwayStunDrawPrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3409/3409 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8930/8930 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.
