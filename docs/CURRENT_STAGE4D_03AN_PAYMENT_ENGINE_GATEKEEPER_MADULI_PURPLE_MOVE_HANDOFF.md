# Stage 4D-03AN PaymentEngine Gatekeeper Maduli Purple Move Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本 handoff 是 A 主控给下一枚 P0-005 PaymentEngine breadth 切片的写锁和验收规格。它不实现 runtime，不修改前端，不修改测试代码，不更新 card matrix。目标是继 4D-03AM Azir focused slice 后，继续把剩余 target-bearing colored-cost activated ability breadth 落到一个较窄的 movement representative。

## 1. Target

实现并验收 `UNL-144/219` 守门者马杜里的紫色支付战场移动 activated ability representative：

```txt
我无法变为活跃状态。
支付{{紫色}}：如果我的战力大于一处敌方控制的战场上所有敌方单位的战力总和，则将我移动到该处。
```

本切片只收窄 P0-005 / movement target-bearing activated ability representative。不得宣称 full official Maduli、完整 LayerEngine、完整 battlefield-control model、完整 target-bearing family、card matrix full-official 或 READY。

## 2. Input Facts

- `data/official/card-catalog.zh-CN.json` 固定 2026-04-27 快照中存在 `UNL-144/219`，card id `34688`，单位，purple，energy 7，power 6。
- `docs/rules-evidence-index.md` 仅记录 `p2-preflight-play-gatekeeper-maduli-move-static` 的普通手牌打出代表证据，并明确不能活跃静态、紫色支付、敌方战场战力比较和移动路径暂缓。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 当前只记录 `GATEKEEPER_MADULI_POWER_MOVE_STATIC` ordinary play/static behavior route。
- `P4ActivatedAbilityCatalog.GetAll()` 当前没有 `UNL-144/219` Gatekeeper Maduli purple move ability id。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 将该 FU 记为 `FU-d5d5707b0e`，`freezeStatus=NEEDS_ENGINE_SUPPORT`，`fullOfficial=false`，blockers 包含 engine support 与 automated test evidence。

## 3. Suggested B Write Scope

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- minimal `src/Riftbound.Contracts` only if existing `ActionPrompt` / target metadata fields are insufficient
- `tests/Riftbound.ConformanceTests/GatekeeperMaduliActivatedAbilityTests.cs` or adjacent focused test file

Suggested A/D doc write scope after implementation:

- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_EVIDENCE.md`
- checkpoint / completion audit / server audit / closure plan

Forbidden in this slice:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- LayerEngine broad rewrite
- unrelated battle lifecycle / cleanup queue files
- unrelated activated abilities
- `riftbound-dotnet.sln`

## 4. Runtime Acceptance

Minimum acceptance:

1. `P4ActivatedAbilityCatalog` exposes an executable ability for `UNL-144/219` with purple typed power cost 1, zero mana cost, one battlefield target and no exhaust cost.
2. `ActionPrompt` exposes `ACTIVATE_ABILITY` only in a server-legal open-main representative window, with source requirement, purple typed cost, target slot and current legal enemy-controlled battlefield targets.
3. Payment uses shared PaymentEngine / `PaymentCostRules`, accepting existing purple power or necessary `RECYCLE_RUNE:<objectId>` that generates purple power.
4. wrong trait, generic temporary resource, duplicate / invalid / unnecessary recycle, insufficient purple and unsupported optional cost are rejected no-mutation.
5. Source must be a controlled public `UNL-144/219` unit in a field zone; hand, deck, graveyard, face-down, enemy-controlled, stale, wrong card and dirty source are rejected no-mutation.
6. Target must be an enemy-controlled battlefield whose enemy-unit power sum is strictly less than Maduli's current representative power. Friendly-controlled battlefield, uncontrolled / stale battlefield, non-battlefield targets, battlefield without sufficient power gap and dirty target are rejected no-mutation.
7. Resolution rechecks source / target / power-sum condition. If still legal, server moves Maduli to that battlefield and updates authoritative `ObjectLocations` / snapshot / event payload. If stale, it resolves no-effect without frontend inference.
8. Existing "cannot become active" static text is not closed by this slice unless explicitly implemented with tests. If left deferred, record it as LayerEngine/static residual and do not claim full official Maduli.

## 5. Prompt / Command Parity

Required tests:

- prompt target choices match command-side accepted battlefields.
- command cannot submit a battlefield not exposed by prompt.
- prompt payment-resource choices match command-side accepted `RECYCLE_RUNE:*`.
- stale target after stack entry becomes no-effect rather than client-side movement.

## 6. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_03AN_PAYMENT_ENGINE_GATEKEEPER_MADULI_PURPLE_MOVE_BASELINE_EVIDENCE.md`.

Post-implementation:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Maduli|FullyQualifiedName~Gatekeeper|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 7. Non-Goals

- Do not implement Sea Monster Hook, Z-Drive banish play branch, Mysterious Weapon replacement branch or other colored activated abilities.
- Do not close Maduli "cannot become active" static text unless it is explicitly implemented and tested.
- Do not close full battlefield control / LayerEngine / movement family.
- Do not update card matrix full-official status.
- Do not close P0-005, P1 or READY.

## 8. Handoff Verdict

4D-03AN is ready as the next B-side focused implementation slice. It should implement only Gatekeeper Maduli's purple-pay enemy-battlefield movement representative with strict server prompt / payment / target / movement / rollback coverage. Project remains **NOT READY**.
