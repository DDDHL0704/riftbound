# Stage 4D-03AB Token Factory Brush Replacement Evidence

日期：2026-05-14
结论：**VALIDATED / PROJECT NOT READY**

## 2026-06-28 Catalog-Boundary Evidence

- `P6TokenFactoryCatalog.IsBrushBattlefieldToken(cardNo)` is the shared runtime identity query for Brush token battlefield objects.
- `MatchSession.TryBuildBrushReplacementChoice` and `CoreRuleEngine.TryResolveBrushReplacementChoice` no longer directly compare Brush token cardNo.
- Ivern Brush token creation and audit events still use `P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo` as object factory / payload data, not as a runtime choice branch.
- `CardCatalogBaselineTests.P6TokenBattlefieldIdentityRoutesThroughCatalogHelpers` covers positive/negative helper identity and source guards against reintroducing the direct runtime comparisons.
- Validation passed: focused guard 1/1; BaronNest / BrushReplacement / BrushStaticAura / P6TokenFactory representatives 43/43; CardCatalogBaseline / BaronNest / BrushReplacement / BrushStaticAura / MoveUnit / DeclareBattle / BattlefieldHeld / MatchRecovery adjacent 2557/2557; backend full 8868/8868.

## Change Evidence

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` is empty.
- `P6TokenFactoryCatalog.GetImplementedRuleSurfaces()` includes Brush replacement, Image copy-token, and Baron Nest movement static.
- Brush replacement prompt exposes a server-authored `BRUSH_USE_REPLACED_BATTLEFIELD:<original>` choice only for valid Brush memory pointing to the supported score battlefield representative.
- Submitting the Brush replacement choice during the Brush held-score path uses the original battlefield identity for `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`.
- Successful replacement writes `BATTLEFIELD_REPLACEMENT_APPLIED` with Brush / original / effective battlefield audit metadata.
- Not submitting the choice does not auto-apply replacement.
- Invalid replacement choices reject with no mutation.

## Validation Commands

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~P79LegendTriggerIvern|FullyQualifiedName~BattlefieldHeld|FullyQualifiedName~DeclareBattle|FullyQualifiedName~BoardTaskQueue"
```

Result: passed 141/141.

Adjacent prompt / battlefield:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Battlefield"
```

Result: passed 511/511.

Brush focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BrushReplacement"
```

Result: passed 8/8.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4144/4144.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AB is complete as a focused Brush battlefield replacement representative slice. The project remains **NOT READY**.
