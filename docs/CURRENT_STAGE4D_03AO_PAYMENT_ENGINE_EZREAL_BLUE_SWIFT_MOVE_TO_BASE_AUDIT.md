# Stage 4D-03AO PaymentEngine Ezreal Blue Swift Move To Base Audit

日期：2026-05-15
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件是 A/D 侧对 4D-03AO B implementation 的验收审计。该切片实现 `SFD·082/221`、`SFD·082a/221` 与 `SFD·082b/221·P` 伊泽瑞尔 blue-pay swift no-target self move-to-base activated ability representative，只收窄 P0-005 keyword payment / colored-cost activated ability breadth，不关闭 full official Ezreal、attack / defense damage trigger、cannot-combat-damage static、完整 swift timing、card matrix full-official、frontend final validation 或 READY。

## 1. Scope

Implemented / changed:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/EzrealBlueSwiftMoveToBaseActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Not touched:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- attack / defense damage trigger runtime
- cannot-combat-damage static runtime
- LayerEngine broad rewrite
- unrelated battle lifecycle / cleanup queue files
- unrelated activated abilities
- `riftbound-dotnet.sln`

## 2. Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Catalog covers all collector numbers | `EzrealBlueSwiftMoveAbilityId` added with alias support for `SFD·082/221`, `SFD·082a/221` and `SFD·082b/221·P`; catalog test covers all three | Accepted |
| Ability cost / target shape | Catalog uses 0 mana, 0 generic power, blue typed power 1, 0 targets, no exhaust cost; prompt exposes `swift=true` | Accepted for focused scope |
| Prompt exposes legal server candidate | `MatchSession` adds Ezreal source requirement metadata, no-target policy, destination base / self-move policy, open-main representative timing, deferred full-swift / combat-static / attack-trigger metadata | Accepted |
| Shared PaymentEngine path | `CoreRuleEngine` builds `PaymentCostRules.PaymentPlan`, authorizes and commits typed blue cost, supports necessary blue `RECYCLE_RUNE:*` | Accepted |
| Invalid payment resources rejected no-mutation | Tests cover wrong trait recycle, generic temporary resource, duplicate / invalid / unnecessary recycle, unsupported optional cost and insufficient blue | Accepted |
| Source validation | Tests cover base source, hand source, deck source, graveyard source, face-down source, enemy-controlled source, wrong card source, dirty source and missing precise location | Accepted |
| No-target command guard | Tests reject submitted enemy unit target and battlefield destination target with no mutation | Accepted |
| Server-authoritative resolution | Stack item resolves on server, moves Ezreal from precise battlefield to controller base, updates `ObjectLocations`, emits `UNIT_MOVED_TO_BASE` with `movementPermission=EZREAL_BLUE_SWIFT_MOVE_TO_BASE` | Accepted |
| Resolution recheck | Tests cover source moved to base, controller changed and face-down / no-longer-public after stack creation; all resolve `ABILITY_NO_EFFECT` without movement | Accepted |
| Full official Ezreal | Attack / defense damage trigger, cannot-combat-damage static, full swift / reaction timing and FAQ breadth explicitly remain residual | Residual, no full-official claim |

## 3. Verification

A-side verification:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EzrealBlueSwift"
```

Result: 28/28 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 207/207 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 400/400 passed.

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: 4321/4321 passed.

```sh
git diff --check
```

Result: passed.

## 4. Residual Risks

- Official attack / defense trigger damage equal to Ezreal's power remains deferred.
- Official "我无法造成战斗伤害" static remains deferred as LayerEngine / combat damage prevention residual.
- This slice uses an open-main representative timing policy and does not close full swift / reaction timing breadth.
- Matrix remains `fullOfficial=false`; see `docs/CURRENT_STAGE4D_03AO_CARD_MATRIX_READINESS_AUDIT.md`.
- Frontend final validation remains historical-only until final fresh-run; see `docs/CURRENT_STAGE4D_FRONTEND_FINAL_VALIDATION_PREFLIGHT.md`.
- P0-005 remains open for remaining keyword payment branches, replacement / optional / alternative / tax parity and full PaymentEngine breadth.

## 5. Verdict

4D-03AO is accepted as a focused PaymentEngine / movement representative. The project remains **NOT READY**.
