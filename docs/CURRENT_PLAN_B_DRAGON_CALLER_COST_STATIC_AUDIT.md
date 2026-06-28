# Plan B Dragon Caller Cost Static Audit

## Scope

This slice closes the runtime gap for `OGN·140/298 / 唤龙使者 / DRAGON_CALLER_COST_STATIC_PLAY_UNIT`.

Official catalog text: `你的“龙”属性单位法力费用减少{{2}}，不得低于{{1}}。`

The implementation is a shared payment / prompt cost modifier, not a card-number settlement branch:

- `CoreRuleEngine` applies a Dragon-unit play cost reduction before payment authorization and records `dragonUnitCostReductionMana` in `COST_PAID`.
- `MatchSession` applies the same reduction order for `PLAY_CARD` prompt `sourceRequirements`.
- The source must be a public, controlled Dragon Caller unit in `Base` or `Battlefields`.
- The played card must be a BehaviorSpec source-to-base unit with `SourceUnitTags` containing `龙`.
- Multiple Dragon Caller sources stack, capped by the one-mana floor.

## Non-Closure

This does not close complete PaymentEngine / PAY_COST breadth, FAQ review, full official matrix readiness, or READY.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~DragonCallerCostStatic"` passed: 11/11.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~DragonCallerCostStatic|FullyQualifiedName~DragonCaller|FullyQualifiedName~DRAGON_CALLER|FullyQualifiedName~PaymentEngineUnification|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~RagingDrake|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~Dragon|FullyQualifiedName~Stack|FullyQualifiedName~MatchRecovery"` passed: 3931/3931.
- Backend conformance full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` passed: 8925/8925.
