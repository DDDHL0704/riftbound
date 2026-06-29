# Plan B Zaun Bodyguard Recall Effect Primitive Spec Audit

更新时间：2026-06-28

## Scope

This slice advances Plan B by moving low-risk recall primitive metadata for `OGN·188/298` 祖安保镖 from the old P2 `CardBehaviorDefinition` surface into `BehaviorSpec.Effects`.

2026-06-29 follow-up: the same return-to-hand battlefield-unit family now also removes the `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` effect-kind target guard from `CoreRuleEngine` / `MatchSession`. `OGN·172/298` 责退 still keeps its catalog effect id and existing stack/event semantics, but target legality is now driven by shared `ReturnsTargetToHand` + `BATTLEFIELD_UNIT` rules instead of a Reprimand-specific branch.

Implemented in this slice:

- `EffectPhraseSpec` now carries optional `ReturnsTargetToHand` and `ReturnDestinationZone`.
- `EffectPhraseParser` parses `返回` target-effect text from official text.
- `EffectPhraseParser` maps `另一名单位从战场上` to `TargetScope=BATTLEFIELD_UNIT`.
- `EffectPhraseParser` maps `其所属的手牌` to `ReturnDestinationZone=HAND`.
- `BehaviorTemplatePrimitiveExecutor` now prefers `BehaviorSpec.Effects` metadata for `Recall` primitives before falling back to P2 behavior metadata.
- Dev UI catalog types mirror the new optional effect fields.
- `CoreRuleEngine` and `MatchSession` now use a shared visible battlefield-unit return-target guard for `ReturnsTargetToHand` + `BATTLEFIELD_UNIT`, preserving legacy untyped public-unit compatibility while rejecting face-down standby, equipment, spell and rune objects.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `OGN·188/298` 祖安保镖:
  - `当你打出我时，让另一名单位从战场上返回其所属的手牌。`
- `OGN·172/298` 责退:
  - `让一名战场上的单位返回其所属的手牌。`
- Rule authority protocol: `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`.

## Not Closed

- Runtime stack resolution still uses the existing CoreRuleEngine / P2 behavior route; this slice only makes the primitive parameters data-driven and auditable in `BehaviorSpec.Effects`.
- Complete recall / return-to-hand official breadth, on-play trigger breadth, control-zone movement breadth, hidden-info owner-hand placement breadth, legal official-deck score-victory replay, and READY remain open.
- Project remains NOT READY.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecEffectPhrasesCarryZaunBodyguardRecallPrimitiveMetadata" --no-restore --nologo
```

Result: 1/1 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorTemplate|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~ConformanceFixtureRunner" --no-restore --nologo
```

Result: 3414/3414 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 8935/8935 passed.

Dev UI catalog contract build:

```bash
PATH="/opt/homebrew/bin:/Users/dinghaolin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:/usr/bin:/bin:/usr/sbin:/sbin" npm --prefix src/Riftbound.DevUi run build
```

Result: passed.

2026-06-29 follow-up validation:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ReprimandReturnToHandGuardTests"
```

Result: 11/11 passed.

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo --filter "FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~BehaviorSpecEffectPhrasesCarryZaunBodyguardRecallPrimitiveMetadata|FullyQualifiedName~P4BasicActionProfilesKeepExistingRepresentativeFixturesGreen"
```

Result: 236/236 passed.

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --nologo
```

Result: 9022/9022 passed.
