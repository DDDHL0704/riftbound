# Stage 4D-03AN PaymentEngine Gatekeeper Maduli Purple Move Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

## 1. Official Text

`data/official/card-catalog.zh-CN.json` contains:

- `UNL-144/219`, card id 34688, 守门者马杜里, purple, unit, energy 7, power 6.

Official text:

```txt
我无法变为活跃状态。
支付{{紫色}}：如果我的战力大于一处敌方控制的战场上所有敌方单位的战力总和，则将我移动到该处。
```

## 2. Runtime Evidence

Implementation facts:

- `P4ActivatedAbilityCatalog` adds `GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD`, with `PowerCostByTrait[purple] = 1`, required target count 1, zero mana and no exhaust cost.
- `MatchSession` exposes a server-owned `ACTIVATE_ABILITY` prompt with purple typed payment metadata, legal target choices for enemy-controlled battlefields whose enemy-unit power sum is lower than Maduli's current power, and explicit deferred cannot-ready metadata.
- `CoreRuleEngine` validates source, target and payment resource actions, commits typed purple payment through `PaymentCostRules`, creates an ordinary stack item, and resolves by rechecking the source / target / power-sum condition.
- Legal resolution moves Maduli to the selected battlefield using authoritative `ObjectLocations` and emits `UNIT_MOVED_TO_BATTLEFIELD` with `movementPermission=GATEKEEPER_MADULI_PURPLE_MOVE`.
- Stale resolution emits `ABILITY_NO_EFFECT` and leaves Maduli unmoved.

## 3. Test Evidence

New focused tests in `tests/Riftbound.ConformanceTests/GatekeeperMaduliActivatedAbilityTests.cs` cover:

- catalog exposure and purple typed cost
- prompt source requirement / target choices / purple payment metadata
- successful command, stack creation and pass-pass resolution movement
- purple rune recycle for typed purple shortfall
- stale target no-effect at resolution
- wrong timing
- hand / graveyard / face-down / enemy-controlled / wrong-card / dirty source
- friendly / uncontrolled / non-battlefield / insufficient-power / dirty / stale targets
- wrong trait / generic temporary / duplicate / invalid / unnecessary payment resource actions
- unsupported optional cost
- insufficient purple payment
- no-mutation hash checks for rejected commands

`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` catalog audit whitelist now includes `GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD`.

## 4. Commands Run

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

## 5. Non-Completion Notes

- Maduli's cannot-ready static text remains deferred.
- This evidence does not update card matrix JSON.
- This evidence does not replace final frontend build / Chrome smoke / formal 18-step fresh-run.
- This evidence does not close P0-005, P1, full-card matrix or READY.
