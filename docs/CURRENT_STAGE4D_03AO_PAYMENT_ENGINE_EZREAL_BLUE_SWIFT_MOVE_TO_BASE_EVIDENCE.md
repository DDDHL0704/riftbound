# Stage 4D-03AO Ezreal Blue Swift Move To Base Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

This file records the concrete implementation and verification evidence for the accepted 4D-03AO focused slice.

## 1. Runtime Evidence

- `P4ActivatedAbilityCatalog` now exposes `EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE` with effect kind `EZREAL_ACTIVATED_SWIFT_MOVE_SELF_TO_BASE`.
- The ability aliases `SFD·082/221`, `SFD·082a/221` and `SFD·082b/221·P`.
- Cost shape: 0 mana, 0 generic power, blue typed power 1, 0 targets, no exhaust cost.
- Prompt metadata exposes `swift=true`, `targetScope=self`, `destinationZone=BASE`, `movePolicy=move-source-to-controller-base`, `stackPolicy=ordinary-stack-item-before-move-to-base`, `paymentPolicy=payment-plan-typed-blue`, plus deferred full-swift / combat-static / attack-trigger policies.
- Command payment uses shared `PaymentCostRules` and accepts ordinary blue power or necessary `RECYCLE_RUNE:*` that generates blue power.
- Resolution rechecks source legality and moves Ezreal to controller base with `UNIT_MOVED_TO_BASE` and `movementPermission=EZREAL_BLUE_SWIFT_MOVE_TO_BASE`.
- Stale / no-longer-legal source resolution emits `ABILITY_NO_EFFECT` and does not move the source.

## 2. Test Evidence

Focused test file:

- `tests/Riftbound.ConformanceTests/EzrealBlueSwiftMoveToBaseActivatedAbilityTests.cs`

Coverage in that file:

- catalog aliases for all three collector Nos.
- prompt source requirement / no-target metadata / blue typed cost / legal recycle choice.
- successful command and pass-pass stack resolution for all three collector Nos.
- necessary blue rune recycle.
- stale source after stack creation.
- controller change after stack creation.
- face-down / no-longer-public source after stack creation.
- invalid source zones and wrong-card / dirty-source guards.
- submitted target / battlefield destination target rejection.
- wrong trait, generic temporary resource, duplicate / invalid / unnecessary recycle, unsupported optional cost and insufficient blue no-mutation.

Regression manifest:

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` adds `EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE` to the implemented P4 activated ability manifest.

## 3. A-Side Verification

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

## 4. Non-Closure Statement

This evidence does not close full official Ezreal. Attack / defense damage trigger, cannot-combat-damage static, full swift / reaction timing, FAQ adjudication, card matrix full-official, frontend final validation and READY remain open.
