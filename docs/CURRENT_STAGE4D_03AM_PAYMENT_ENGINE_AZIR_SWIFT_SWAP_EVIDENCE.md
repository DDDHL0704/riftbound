# Stage 4D-03AM PaymentEngine Azir Swift Swap Evidence

日期：2026-05-15
结论：**EVIDENCE RECORDED / PROJECT NOT READY**

## 1. Official Text

`data/official/card-catalog.zh-CN.json` contains:

- `SFD·050/221`, card id 33126, 阿兹尔, green, hero unit, energy 6, power 6.
- `SFD·050a/221`, card id 33127, 阿兹尔, green, hero unit, energy 6, power 6.

Official ability text:

```txt
支付{{绿色}}：{{迅捷}} — 选择一个受你控制的单位，将我移动到它的位置，再将它移动到我原来的位置。如果该单位已配有武装，则你可以选择将其中一件武装贴附到我身上。每回合仅可使用一次。
```

## 2. Runtime Evidence

Implementation facts:

- `P4ActivatedAbilityCatalog` adds `AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT`, with `PowerCostByTrait[green] = 1`, required target count 1 and alias coverage for both Azir collector numbers.
- `MatchSession` exposes a server-owned `ACTIVATE_ABILITY` prompt with green typed payment metadata, target choices for controlled face-up units, swift marker, once-per-turn marker, precise swap policy and deferred armament metadata.
- `CoreRuleEngine` validates source, target and payment resource actions, commits typed green payment through `PaymentCostRules`, creates an ordinary stack item, adds a per-turn use marker, and resolves the stack item by swapping precise field locations.
- Resolution event `UNIT_LOCATIONS_SWAPPED` carries source/target ids, origin/destination locations and `armamentReattachPolicy=deferred`.

## 3. Test Evidence

New focused tests in `tests/Riftbound.ConformanceTests/AzirSwiftSwapActivatedAbilityTests.cs` cover:

- catalog and collector alias coverage
- prompt source requirement / target choices / green payment metadata
- successful command and stack resolution for both collector numbers
- green rune recycle for typed green shortfall
- once-per-turn rejection and end-turn cleanup
- wrong timing
- enemy unit target
- equipment / rune / battlefield / hand / face-down / stale / source self targets
- dirty-controller target
- wrong trait / generic temporary / duplicate / invalid / unnecessary payment resource actions
- unsupported optional cost
- insufficient green payment
- no-mutation hash checks for rejected commands

`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` catalog audit whitelist now includes `AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT`.

## 4. Commands Run

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

## 5. Non-Completion Notes

- Optional armament reattach remains deferred.
- This evidence does not update card matrix JSON.
- This evidence does not replace final frontend build / Chrome smoke / formal 18-step fresh-run.
- This evidence does not close P0-005, P1, full-card matrix or READY.
