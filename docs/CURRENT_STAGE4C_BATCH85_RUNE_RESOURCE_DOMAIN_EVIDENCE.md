# Stage 4C-85 Rune Resource Domain Evidence

审计日期：2026-05-13
结论：**代表性资源域证据已记录；项目整体仍 NOT READY。**

## 代码证据

- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
  - `OfficialRuleDomainBehaviorCatalog.RuneResourceDomainEffectKind = RUNE_RESOURCE_DOMAIN`
  - `MergeWithNonPlayCardDomains` maps official rune cards into the non-play rune resource domain
  - `IsRuneCard` uses `CardCategoryName == 符文`
  - `DetermineReason` records rune cards as outside `PLAY_CARD`
- `src/Riftbound.Engine/MatchSession.cs`
  - `RECYCLE_RUNE` prompt metadata uses controlled trait base rune policy
  - `PlayCardPaymentResourcePowerByChoiceForBehavior` exposes only server-computed payment resource choices from controlled base runes
  - payment resource metadata includes `trait` and `power`
  - development seeds cover single red recycle, double red recycle, red/blue mixed typed recycle, and red/blue mixed generic recycle
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - supports `RECYCLE_RUNE` payment resource actions during represented `PLAY_CARD` payment windows
  - records `RUNE_RECYCLED` and `COST_PAID` events with exact `paymentResourceActions` / `recycledRuneObjectIds`

## 测试证据

- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`
  - `P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `P7PlayCardRecyclesRuneAsPaymentResourceAction`
  - `P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount`
  - `P7PlayCardPaymentResourceContributionMetadataSeparatesTraits`
  - `P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution`
  - `P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions`
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`
  - `P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub`
  - `P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub`
  - `P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub`
  - `P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub`

## 命令证据

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount|FullyQualifiedName~P7PlayCardPaymentResourceContributionMetadataSeparatesTraits|FullyQualifiedName~P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution|FullyQualifiedName~P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions|FullyQualifiedName~P79TypedPowerPaymentRecycleSeedOffersPartialAmountAndPlaysThroughHub|FullyQualifiedName~P79TypedPowerPaymentDoubleRecycleSeedRequiresBothResourcesAndPlaysThroughHub|FullyQualifiedName~P79TypedPowerPaymentMixedRecycleSeedExposesTraitsAndAcceptsMatchingResourceThroughHub|FullyQualifiedName~P79TypedPowerPaymentGenericMixedRecycleSeedAcceptsAnyTraitResourceThroughHub"
```

结果：10/10 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RuneResourceDomain|FullyQualifiedName~RecycleRune|FullyQualifiedName~TypedPowerPayment|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~PayCost|FullyQualifiedName~PayCostWindow|FullyQualifiedName~Payment|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：240/240 passed。

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：3754/3754 passed。

```sh
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build
cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api
```

结果：frontend build passed；Chrome smoke passed。

## 矩阵证据

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - `stage4CBatch85RuneResourceDomainEvidence`
  - `functionalUnits[].stage4C85` for `FU-0ec69ae7e6` and `FU-39041f4562`
  - `snapshotEntries[].stage4C85` for all 16 red / blue basic rune entries in those two FUs

4B freeze status / status flags 保持不变，`fullOfficial=false`。本批只入账 red / blue basic rune resource-domain representative payment-resource evidence：non-play domain mapping、not direct playable cards、controlled base rune `RECYCLE_RUNE` contribution, typed wrong-trait rejection, generic mixed-trait acceptance, exact-resource / over-recycle guards, Hub seeds, frontend build and Chrome smoke. 完整符文生命周期、完整 PaymentEngine、hidden-info / redaction、formal 18-step E2E 与 full-official 仍 deferred。
