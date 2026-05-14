# Stage 4D-03AA Token Factory Image Copy Evidence

日期：2026-05-14
结论：**VALIDATED / PROJECT NOT READY**

## Change Evidence

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` returns only Brush battlefield replacement.
- `P6TokenFactoryCatalog.GetImplementedRuleSurfaces()` includes Image copy-token and Baron Nest movement static.
- Mirror Image now creates an active base Image copy whose `CardNo`, `Power`, and tags come from the copied unit, with `瞬息` / `映像` added.
- Mirror Image `UNIT_TOKEN_CREATED` payload includes copied target / copied card audit metadata and `tokenFactoryCardNo=UNL·T06`.
- LeBlanc Image trigger retains copied card identity and now also emits the Image token factory marker.
- Invalid Image copy targets reject with no mutation, including missing `CardNo`, non-unit, face-down, non-field, and dirty-control cases.
- Copying a unit with on-play behavior does not trigger that copied card's on-play effects.

## Validation Commands

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~MirrorImage|FullyQualifiedName~P79LegendTriggerLeblanc"
```

Result: passed 16/16.

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~UNIT_TOKEN_CREATED|FullyQualifiedName~Ephemeral|FullyQualifiedName~Token|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result: passed 253/253.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4136/4136.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03AA is complete as a focused Image copy-token representative slice. The project remains **NOT READY**.
