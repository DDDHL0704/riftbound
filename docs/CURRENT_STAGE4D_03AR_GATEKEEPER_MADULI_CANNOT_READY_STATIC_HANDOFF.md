# Stage 4D-03AR Gatekeeper Maduli Cannot-Ready Static Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本 handoff 是 A 主控给下一枚 Gatekeeper Maduli static engine-support 切片的写锁和验收规格。它不实现 runtime，不修改前端，不修改 card matrix。目标是在 4D-03AN purple move representative 之后，补齐 `UNL-144/219` 守门者马杜里官方文本中的 `我无法变为活跃状态。` cannot-ready static representative，避免任何 ready effect 把 Maduli 从横置变为活跃状态。

## 1. Target

实现并测试 `UNL-144/219` 守门者马杜里的 cannot-ready static representative：

- Maduli 处于横置状态时，服务端 ready effect 不得把它设置为 `IsExhausted=false`。
- 可预测的 prompt 路径不得把 exhausted Maduli 当作合法 ready target 推荐给玩家。
- 如果旧 stack item、手写命令或 stale path 已经指向 Maduli，解析时仍必须服务端权威跳过或 no-effect，不得发出 Maduli 的 `UNIT_READIED`。
- 4D-03AN purple move ability 必须继续通过，不得回退 typed-purple payment、enemy battlefield target guard、movement event 或 stale no-effect evidence。

## 2. Input Facts

- `data/official/card-catalog.zh-CN.json` 固定快照包含 `UNL-144/219` / card id 34688 / 守门者马杜里 / purple / unit / energy 7 / power 6。
- 官方文本为 `我无法变为活跃状态。支付{{紫色}}：如果我的战力大于一处敌方控制的战场上所有敌方单位的战力总和，则将我移动到该处。`
- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md` 已验收 purple-pay move representative，但明确记录 cannot-ready static 仍 deferred。
- `docs/CURRENT_STAGE4D_03AN_CARD_MATRIX_READINESS_AUDIT.md` 确认 `FU-d5d5707b0e` 仍为 `fullOfficial=false`，且 cannot-ready static 未实现时不得升级 full-official。
- `tests/Riftbound.ConformanceTests/GatekeeperMaduliActivatedAbilityTests.cs` 当前断言 `staticCannotBecomeActivePolicy == "deferred"`，这应在实现后改为 implemented / no-longer-deferred metadata。
- `src/Riftbound.Engine/MatchSession.cs` 当前 Crimson Rose ready-unit prompt 只按公开单位和费用过滤目标，未识别 Maduli cannot-ready static。
- `src/Riftbound.Engine/CoreRuleEngine.cs` 当前 Crimson Rose stack resolution 直接把 target `IsExhausted=false`，generic `ApplyReadyState` 也直接发出 `UNIT_READIED`。

## 3. Suggested B Write Scope

Default write scope:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/GatekeeperMaduliActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/CrimsonRoseActivatedAbilityTests.cs`
- `tests/Riftbound.ConformanceTests/HuntReadyGuardTests.cs`

Optional only if B finds the smallest local home for shared metadata:

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`

Forbidden in this slice:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- broad LayerEngine rewrite
- broad ready-state rewrite beyond the touched representative ready paths
- Maduli full-official matrix upgrade
- unrelated activated abilities, battle lifecycle, HASTE_READY or swift timing work
- `riftbound-dotnet.sln`

## 4. Acceptance

Minimum acceptance:

1. Server has a single explicit policy or helper that recognizes Maduli by official card number `UNL-144/219` as unable to become active.
2. Crimson Rose ready-unit prompt must not offer exhausted Maduli as a legal ready target; a hand-written command targeting Maduli must reject no-mutation or, if accepted by a stale path, resolve without readying Maduli.
3. Crimson Rose ready-unit stack resolution must leave exhausted Maduli exhausted and must not emit `UNIT_READIED` for Maduli.
4. A mass ready representative, preferably Hunt `HUNT_READY_ALL_FRIENDLY_UNITS`, must ready other legal friendly units while skipping exhausted Maduli.
5. Existing 4D-03AN Maduli purple move prompt / command / movement / stale no-effect tests remain green.
6. Maduli prompt metadata must stop claiming `staticCannotBecomeActivePolicy="deferred"` after implementation; use implemented metadata or remove the deferred marker.
7. The slice must not update card matrix full-official status, P0/P1 status, frontend final validation or READY.

## 5. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_03AR_GATEKEEPER_MADULI_CANNOT_READY_STATIC_BASELINE_EVIDENCE.md`.

Post-implementation:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~CrimsonRose|FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Non-Goals

- Do not implement full LayerEngine / continuous effects architecture in this slice.
- Do not claim full-official Maduli while other matrix gates remain unresolved.
- Do not broaden all possible ready-state mutations unless needed for the representative acceptance above.
- Do not modify frontend behavior or introduce local frontend rule inference.
- Do not close P0-005, P1, frontend final validation, full-card matrix or READY.

## 7. Handoff Verdict

4D-03AR is ready as a narrow server-authoritative cannot-ready static slice. No runtime write lock has been opened in this A-side batch; the project remains **NOT READY**.
