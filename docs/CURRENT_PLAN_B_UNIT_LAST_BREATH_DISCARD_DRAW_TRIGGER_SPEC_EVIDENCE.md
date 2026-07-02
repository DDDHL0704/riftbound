# Plan B Unit Last-Breath Discard-Draw TriggerSpec Evidence

日期：2026-06-27
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records evidence for routing the existing Undercover Agent last-breath discard/draw representative through `BehaviorSpec.Triggers` instead of a `CoreRuleEngine` local Undercover effect constant.

## 1. Official Rule Evidence

- Official catalog entry `OGN·178/298`: `{{绝念}}—弃置两张手牌，然后抽两张牌。（当我被摧毁后，发动此效果。）`
- `CORE-260330` p52-p55 rules 383.3.d-383.3.e are the existing destroyed-trigger timing authority for this representative.
- `CORE-260330` p62 rule 422.4 is the existing hand-size shortfall authority already covered by Undercover Agent hand-choice tests.

No official data file was edited.

## 2. Runtime Evidence

- `RuleTextParser` parses Undercover Agent last-breath discard/draw text into `TriggerSpec` with:
  - `Kind=UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT` via `TriggerKinds.UnitLastBreathDiscardDraw`
  - `Timing=UNIT_DESTROYED`
  - `TargetScope=SOURCE_UNIT`
  - `DiscardCount=2`
  - `DrawCount=2`
- `UnitDestroyedTriggerSpecRules.TryGetTrigger(..., IsLastBreathDiscardDrawTrigger, ...)` exposes the parsed trigger shape to the shared engine.
- `ResolveUndercoverAgentLastBreathPlayerId(...)` now accepts a destroyed source through the parsed TriggerSpec plus the shared visible unit boundary.
- `ResolveUndercoverAgentLastBreathStackItem(...)` reads discard and draw counts from TriggerSpec while keeping the existing hand-choice prompt and shortfall behavior.
- `CoreRuleEngine` no longer defines `UndercoverAgentCardNo` or `UndercoverAgentLastBreathEffectKind`.
- The public effect string remains `UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT`, so existing recovery and replay validators remain compatible.

## 3. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`

Coverage:

- `BehaviorSpecCatalogParsesUnitLastBreathDiscardDrawTrigger` proves the official Undercover Agent entry produces the expected `TriggerSpec` row.
- `UnitLastBreathDiscardDrawTriggerDoesNotUseCoreCardNumberBehavior` blocks reintroducing the old Core local card-number / effect constant branch.
- Existing Undercover Agent tests prove hand-choice prompt, wrong-player/stale/invalid no-mutation boundaries, shortfall auto-discard and no-hand draw behavior remain green.
- Existing MatchRecovery representatives prove trigger queue / pending hand-choice / spectator redaction compatibility remains green.

## 4. Verification

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesUnitLastBreathDiscardDrawTrigger|FullyQualifiedName~UnitLastBreathDiscardDrawTriggerDoesNotUseCoreCardNumberBehavior" --nologo
```

Result: 2/2 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~UndercoverAgent|FullyQualifiedName~LastBreath|FullyQualifiedName~TriggerSourceIdentityGuard|FullyQualifiedName~CardCatalogBaseline|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2327/2327 passed.

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8819/8819 passed.

DevUi build was not rerun because this slice did not change DevUi source or shared TypeScript catalog payload shape.

## 5. Non-Closure Statement

This evidence does not close complete generic hand-choice/discard-draw resolution, complete last-breath trigger timing, complete trigger queue ordering, Kogmaw or Unsung Hero TriggerSpec migration, card matrix full-official state, frontend final validation, or READY.
