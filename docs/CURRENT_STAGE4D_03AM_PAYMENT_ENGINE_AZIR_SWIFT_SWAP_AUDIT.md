# Stage 4D-03AM PaymentEngine Azir Swift Swap Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件是 A/D 侧对 4D-03AM B implementation 的验收审计。该切片实现 `SFD·050/221` 与 `SFD·050a/221` 阿兹尔 green swift position-swap activated ability representative，只收窄 P0-005 target-bearing colored-cost activated ability breadth，不关闭 full official Azir、完整 swift timing、optional armament reattach、card matrix full-official、frontend final validation 或 READY。

## 1. Scope

Implemented / changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Not touched:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- LayerEngine
- unrelated battle lifecycle / cleanup queue files
- `riftbound-dotnet.sln`

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Catalog covers both collector numbers | `AzirSwiftSwapAbilityId` added with alias support for `SFD·050/221` and `SFD·050a/221`; catalog test covers both | Accepted |
| Prompt exposes legal server candidate only | `MatchSession` adds Azir source requirement metadata, green typed cost, target slot, swift / once-per-turn / swap / deferred-armament policy; prompt test covers target/payment choices | Accepted for focused scope |
| Shared PaymentEngine path | `CoreRuleEngine` builds `PaymentCostRules.PaymentPlan`, authorizes and commits typed green cost, supports necessary green `RECYCLE_RUNE:*` | Accepted |
| Invalid payment resources rejected no-mutation | Tests cover wrong trait recycle, generic temporary resource, duplicate / invalid / unnecessary recycle, unsupported optional cost and insufficient green | Accepted |
| Target validation | Tests cover enemy unit, equipment, rune, battlefield, hand target, face-down, stale target, source self and dirty-controller target | Accepted |
| Server-authoritative resolution | Stack item resolves on server, swaps precise `ObjectLocations` and emits `UNIT_LOCATIONS_SWAPPED`; tests assert source / target battlefield ids are exchanged | Accepted |
| Once per turn | `UntilEndOfTurnEffects` marker blocks same-turn second activation; test confirms end-turn cleanup clears marker | Accepted |
| Optional armament reattach | Explicitly deferred via prompt/event metadata and audit docs | Residual, no full-official claim |

## 3. Verification

A-side verification:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap"
```

Result: 23/23 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~AzirSwiftSwap|FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 191/191 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Azir|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 384/384 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4268/4268 passed.

```sh
git diff --check
```

Result: passed.

## 4. Residual Risks

- Official optional branch “如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上” remains deferred.
- This slice uses an open-main representative timing policy and does not close full swift / reaction timing breadth.
- Matrix remains `fullOfficial=false`; see `docs/CURRENT_STAGE4D_03AM_CARD_MATRIX_READINESS_AUDIT.md`.
- Frontend final validation remains historical-only until final fresh-run; see `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md`.
- P0-005 remains open for remaining target-bearing colored-cost abilities, keyword payment branches, replacement / optional / alternative / tax parity and full PaymentEngine breadth.

## 5. Verdict

4D-03AM is accepted as a focused PaymentEngine representative. The project remains **NOT READY**.
