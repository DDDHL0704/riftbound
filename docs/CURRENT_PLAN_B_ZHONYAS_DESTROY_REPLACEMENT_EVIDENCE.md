# Plan B Zhonya's Hourglass Destroy Replacement Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGN·077/298` / cardId `31291` / 中娅沙漏: equipment, standby, next-friendly-unit-destroyed replacement recall text.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p39-p42 rules 355-356: stack / resolution / state update frame.
- `CORE-260330` p92-p105 keyword rules 800+: standby keyword remains adjacent and not closed here.

Implementation evidence:

- `src/Riftbound.Contracts/BehaviorSpecs.cs`: adds `ReplacementKinds.FriendlyUnitDestroyedDestroySourceRecallExhausted`.
- `src/Riftbound.CardCatalog/RuleTextParsers.cs`: parses the official replacement text into `BehaviorSpec.Replacements`.
- `src/Riftbound.Engine/CardReplacementSpecRules.cs`: builds a shared card replacement-spec lookup from official catalog BehaviorSpec data.
- `src/Riftbound.Engine/CoreRuleEngine.cs`: state-based cleanup applies the replacement by spec, not by card number.

Automated evidence:

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
  - `BehaviorSpecCatalogParsesFriendlyUnitDestroyedEquipmentRecallReplacement`
  - `FriendlyUnitDestroyedEquipmentRecallReplacementUsesGenericSpecPredicate`
- `tests/Riftbound.ConformanceTests/ZhonyasHourglassGuardTests.cs`
  - `ZhonyasHourglassReplacementDestroysSourceAndRecallsFriendlyUnitInsteadOfDestroyingIt`
  - `ZhonyasHourglassReplacementIgnoresFaceDownAndOpponentSources`

Validation so far:

- Baseline before changes: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore` passed 9183/9183.
- Focused after implementation: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ZhonyasHourglassGuardTests|FullyQualifiedName~BehaviorSpecCatalogParsesFriendlyUnitDestroyedEquipmentRecallReplacement|FullyQualifiedName~FriendlyUnitDestroyedEquipmentRecallReplacementUsesGenericSpecPredicate"` passed 12/12.
- Adjacent after implementation: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~ZhonyasHourglassGuardTests|FullyQualifiedName~CardCatalogBaselineTests|FullyQualifiedName~SettLegendActionDomainGuardTests|FullyQualifiedName~BrushStaticAuraReplacementLifecycleTests|FullyQualifiedName~OfficialDeckMidgameResolvesBloodAltarBattleDestroyedRecall|FullyQualifiedName~P79BattlefieldBattleDestroyed|FullyQualifiedName~MatchRecovery"` passed 2398/2398.
- Backend full after implementation: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore` passed 9187/9187.

Boundary:

This closes only the public-equipment friendly-unit-destroyed replacement representative route for Zhonya's Hourglass. It does not close standby reaction play, multiple replacement ordering, complete equipment lifecycle, full FAQ adjudication, 1009/811 full-official coverage, formal 18-step E2E, or READY.
