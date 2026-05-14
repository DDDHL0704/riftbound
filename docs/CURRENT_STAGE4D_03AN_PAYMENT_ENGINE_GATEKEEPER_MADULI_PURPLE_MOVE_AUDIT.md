# Stage 4D-03AN PaymentEngine Gatekeeper Maduli Purple Move Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件是 A/D 侧对 4D-03AN B implementation 的验收审计。该切片实现 `UNL-144/219` 守门者马杜里 purple-pay enemy-controlled battlefield movement representative，只收窄 P0-005 target-bearing colored-cost activated ability breadth，不关闭 full official Maduli、不能变为活跃状态静态、完整 LayerEngine / battlefield-control model、card matrix full-official、frontend final validation 或 READY。

## 1. Scope

Implemented / changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/GatekeeperMaduliActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Not touched:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- LayerEngine broad rewrite
- unrelated battle lifecycle / cleanup queue files
- unrelated activated abilities
- `riftbound-dotnet.sln`

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Catalog exposes executable Maduli ability | `GatekeeperMaduliMoveAbilityId` added for `UNL-144/219`, with purple typed power cost 1, zero mana, one target, no exhaust cost | Accepted |
| Prompt exposes legal server candidate | `MatchSession` adds source requirement metadata, purple typed cost, target slot, legal enemy-controlled battlefield choices, stack / move / payment policy metadata | Accepted |
| Shared PaymentEngine path | `CoreRuleEngine` builds `PaymentCostRules.PaymentPlan`, authorizes and commits typed purple cost, supports necessary purple `RECYCLE_RUNE:*` | Accepted |
| Invalid payment resources rejected no-mutation | Tests cover wrong trait recycle, generic temporary resource, duplicate / invalid / unnecessary recycle, unsupported optional cost and insufficient purple | Accepted |
| Source validation | Tests cover wrong timing, hand source, graveyard source, face-down source, enemy-controlled source, wrong card source and dirty source | Accepted |
| Target validation | Tests cover friendly battlefield, uncontrolled battlefield, non-battlefield target, insufficient power gap, dirty target and stale target | Accepted |
| Server-authoritative resolution | Stack item resolves on server, rechecks source / target / power gap, moves Maduli to target battlefield with authoritative `ObjectLocations`, and emits `UNIT_MOVED_TO_BATTLEFIELD`; stale target resolves `ABILITY_NO_EFFECT` | Accepted |
| Cannot-ready static text | Explicitly retained as deferred static / LayerEngine residual | Residual, no full-official claim |

## 3. Verification

A-side verification:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper"
```

Result: 25/25 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 188/188 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 381/381 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4293/4293 passed.

```sh
git diff --check
```

Result: passed.

## 4. Residual Risks

- Official line “我无法变为活跃状态” remains deferred as a static / LayerEngine residual.
- This slice covers a focused open-main activated movement representative and does not close full battlefield-control or movement-family official breadth.
- Matrix remains `fullOfficial=false`; see `docs/CURRENT_STAGE4D_03AN_CARD_MATRIX_READINESS_AUDIT.md`.
- Frontend final validation remains historical-only until final fresh-run; see `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md`.
- P0-005 remains open for remaining target-bearing colored-cost abilities, keyword payment branches, replacement / optional / alternative / tax parity and full PaymentEngine breadth.

## 5. Verdict

4D-03AN is accepted as a focused PaymentEngine / movement representative. The project remains **NOT READY**.
