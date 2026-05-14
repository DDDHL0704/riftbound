# Stage 4D-03Z Token Factory Baron Nest Static Evidence

日期：2026-05-14
结论：**VALIDATED / PROJECT NOT READY**

## Change Evidence

- `P6TokenFactoryCatalog.GetDeferredRuleSurfaces()` count is now 2.
- Deferred token surfaces now retain only:
  - `TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED`
  - `TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT`
- `P6TokenFactoryCatalog.GetImplementedRuleSurfaces()` returns Baron Nest's retired static representative:
  - `TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC`
  - `UNL·T01`
  - `battlefield-static`
  - `单位可从任意位置移动到此处`
- `MOVE_UNIT` supports no-ROAM precise movement from another battlefield to a controlled Baron Nest destination.
- `UNIT_MOVED_TO_BATTLEFIELD` for this path includes `movementPermission = "BARON_NEST_MOVE_STATIC"` and does not include `movementKeyword = "游走"`.
- ActionPrompt metadata exposes a `BARON_NEST_MOVE_STATIC` source requirement with no optional cost requirements, and hides the destination when the Baron Nest object is opponent-controlled.

## Validation Commands

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~P6TokenFactoryCatalog|FullyQualifiedName~GoldTokenDeferredResourceSurfaces|FullyQualifiedName~BaronNest|FullyQualifiedName~P79BattlefieldStaticRoam|FullyQualifiedName~P4MoveUnitCommand|FullyQualifiedName~P5MoveUnitCommand|FullyQualifiedName~PreciseRoam|FullyQualifiedName~BoardTaskQueue"
```

Result: passed 77/77.

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MoveUnit|FullyQualifiedName~BattlefieldStatic|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~DeclareBattle"
```

Result: passed 345/345.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed 4131/4131.

Diff hygiene:

```sh
git diff --check
```

Result: passed.

## Verdict

4D-03Z is complete as a focused Baron Nest token battlefield static representative. The project remains **NOT READY**.
