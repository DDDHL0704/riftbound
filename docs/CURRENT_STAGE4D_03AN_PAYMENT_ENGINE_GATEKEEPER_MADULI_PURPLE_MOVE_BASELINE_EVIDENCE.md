# Stage 4D-03AN Gatekeeper Maduli Purple Move Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

This baseline records the pre-implementation state for the next P0-005 target-bearing colored-cost activated ability representative: `UNL-144/219` Gatekeeper Maduli.

## 1. Current Evidence

- Official catalog text exists in `data/official/card-catalog.zh-CN.json`: `我无法变为活跃状态。支付{{紫色}}：如果我的战力大于一处敌方控制的战场上所有敌方单位的战力总和，则将我移动到该处。`
- Current rules evidence only covers ordinary hand play via `p2-preflight-play-gatekeeper-maduli-move-static`.
- Current implementation route is `GATEKEEPER_MADULI_POWER_MOVE_STATIC`; the purple activated movement path remains deferred.
- Current matrix state: `FU-d5d5707b0e`, `NEEDS_ENGINE_SUPPORT`, `fullOfficial=false`, `NO_FU_LEVEL_AUTOMATED_TEST_IN_MATRIX`.

## 2. Baseline Commands

Focused adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 163/163 passed.

Broader adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 356/356 passed.

Whitespace / diff baseline:

```sh
git diff --check
```

Result: passed.

## 3. Baseline Verdict

The adjacent test surface is green before implementing Gatekeeper Maduli's activated purple move path. This baseline does not implement runtime behavior and does not close P0-005 or READY.
