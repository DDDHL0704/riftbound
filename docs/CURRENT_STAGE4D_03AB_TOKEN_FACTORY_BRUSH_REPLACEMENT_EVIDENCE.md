# Stage 4D-03AB Token Factory Brush Replacement Evidence

日期：2026-05-14
结论：**VALIDATED / PROJECT NOT READY**

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
