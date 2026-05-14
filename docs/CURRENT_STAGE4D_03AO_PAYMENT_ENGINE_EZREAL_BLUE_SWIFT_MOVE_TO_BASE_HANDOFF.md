# Stage 4D-03AO PaymentEngine Ezreal Blue Swift Move To Base Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本 handoff 是 A 主控给下一枚 P0-005 PaymentEngine breadth 切片的写锁和验收规格。它不实现 runtime，不修改前端，不修改测试代码，不更新 card matrix。目标是继 4D-03AN Gatekeeper Maduli focused slice 后，继续把剩余 keyword payment / colored-cost activated ability breadth 落到一个 no-target self-movement representative。

## 1. Target

实现并验收 `SFD·082/221`、`SFD·082a/221` 与 `SFD·082b/221·P` 伊泽瑞尔的蓝色支付迅捷移动到基地 activated ability representative：

```txt
当我进攻或防守时，对此处的一名敌方单位造成等同于我战力的伤害。
我无法造成战斗伤害。
支付{{蓝色}}：{{迅捷}} — 将我移动到你的基地。
```

本切片只收窄 P0-005 / keyword payment / no-target activated self-movement representative。不得宣称 full official Ezreal、attack / defense damage trigger、cannot-combat-damage static、完整 swift / reaction timing、完整 movement family、card matrix full-official 或 READY。

## 2. Input Facts

- `data/official/card-catalog.zh-CN.json` 固定 2026-04-27 快照中存在 `SFD·082/221` / `33162`、`SFD·082a/221` / `33163` 与 `SFD·082b/221·P` / `33164`，energy 4，power 3，官方文本相同。
- `docs/rules-evidence-index.md` 仅记录 `p2-preflight-play-sfd-082-ezreal-combat-damage-static`、`p2-preflight-play-sfd-082a-ezreal-combat-damage-static` 与 `p2-preflight-play-sfd-082b-ezreal-combat-damage-static` 的普通手牌打出代表证据，并明确 attack / defense trigger、cannot combat damage static 与 blue swift move path 暂缓。
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` 当前只记录 `SFD_082_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT` / alt shared ordinary play-unit routes。
- `tests/Riftbound.ConformanceTests/EzrealCombatDamageTextPlayUnitGuardTests.cs` 当前只覆盖 ordinary play-unit guard、invalid input no-mutation / no-leak。
- `P4ActivatedAbilityCatalog.GetAll()` 当前没有 `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` 的 Ezreal blue swift move ability id。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` 将该 card family 记为 `FU-2dca1ad450`，functional unit size 3，`stage4B.fullOfficial=false`，flags 包含 `IMPLEMENTED_TESTED`、`SHARED_ORACLE_IMPLEMENTATION`、`NEEDS_ENGINE_SUPPORT` 与 `NEEDS_FAQ_REVIEW`。

## 3. Suggested B Write Scope

- `src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- minimal `src/Riftbound.Contracts` only if existing `ActionPrompt` / source requirement metadata fields are insufficient
- `tests/Riftbound.ConformanceTests/EzrealBlueSwiftMoveToBaseActivatedAbilityTests.cs` or adjacent focused test file

Suggested A/D doc write scope after implementation:

- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_AUDIT.md`
- `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_EVIDENCE.md`
- checkpoint / completion audit / server audit / closure plan

Forbidden in this slice:

- frontend runtime
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- attack / defense combat-damage trigger implementation
- cannot-combat-damage LayerEngine/static implementation unless separately dispatched
- broad swift / reaction timing rewrite
- unrelated battle lifecycle / cleanup queue files
- unrelated activated abilities
- `riftbound-dotnet.sln`

## 4. Runtime Acceptance

Minimum acceptance:

1. `P4ActivatedAbilityCatalog` exposes an executable ability or equivalent aliases for all three Ezreal collector Nos, with blue typed power cost 1, zero mana cost, zero targets, no exhaust cost and swift metadata. Suggested ids: `EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE` and `EZREAL_ACTIVATED_SWIFT_MOVE_SELF_TO_BASE`.
2. `ActionPrompt` exposes `ACTIVATE_ABILITY` only in a server-legal representative timing window. Minimum scope may remain open-main with `swift=true` metadata; if B expands to a battle-response / spell-duel swift-compatible window, that expansion must be explicitly tested and audited.
3. Prompt metadata includes source requirement, blue typed cost, no-target policy, destination base / self-movement policy and stack-before-move policy. Frontend must not infer source legality, payment legality or movement result.
4. Payment uses shared PaymentEngine / `PaymentCostRules`, accepting existing blue power or necessary `RECYCLE_RUNE:<objectId>` that generates blue power.
5. Wrong trait, generic temporary resource, duplicate / invalid / unnecessary recycle, insufficient blue and unsupported optional cost are rejected no-mutation.
6. Source must be a controlled public Ezreal unit from this three-card family in a precise battlefield location. Base, hand, deck, graveyard, face-down, enemy-controlled, stale, wrong-card and dirty source attempts are rejected no-mutation.
7. Command is no-target: submitted target object ids, battlefield destinations, enemy units or arbitrary destination overrides must be rejected no-mutation.
8. Resolution rechecks source control and source location. If still legal, server moves Ezreal to the activating player's base, updates authoritative `ObjectLocations` / snapshot and emits the existing move-to-base event shape, with a distinguishable movement permission such as `EZREAL_BLUE_SWIFT_MOVE_TO_BASE`.
9. If the source is stale, no longer controlled, no longer public, no longer in a battlefield or already in base at resolution, the stack item resolves no-effect without client-side movement.
10. Attack / defense damage trigger and "cannot deal combat damage" static text remain residual unless explicitly implemented and tested in this same slice. If left residual, audit must state that full official Ezreal remains false.

## 5. Prompt / Command Parity

Required tests:

- prompt source choices match command-side accepted Ezreal sources.
- prompt payment-resource choices match command-side accepted `RECYCLE_RUNE:*`.
- command cannot bypass prompt by sending targets or destination overrides.
- stale source after stack entry becomes no-effect rather than frontend-inferred movement.
- event / snapshot payload is sufficient for the frontend to display the resulting move without local rules.

## 6. No-Mutation Requirements

Failure branches should assert unchanged:

- tick / event count
- source zone and precise `ObjectLocations`
- source controller / face-up state / tags / exhaustion
- runePool mana / power / `powerByTrait`
- hand / deck / discard / base / battlefield zones
- stack / priority / focus
- temporary payment resources

## 7. Suggested Verification

Implementation-before baseline is recorded in `docs/CURRENT_STAGE4D_03AO_PAYMENT_ENGINE_EZREAL_BLUE_SWIFT_MOVE_TO_BASE_BASELINE_EVIDENCE.md`.

Post-implementation:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~EzrealBlueSwift|FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 8. Non-Goals

- Do not implement Ezreal attack / defense triggered damage.
- Do not implement Ezreal cannot-combat-damage static text unless explicitly dispatched with tests.
- Do not close full swift / reaction timing breadth from this narrow representative alone.
- Do not update card matrix full-official status.
- Do not close P0-005, P1, frontend final validation or READY.

## 9. Handoff Verdict

4D-03AO is ready as the next B-side focused implementation slice. It should implement only Ezreal's blue-pay swift self move-to-base representative with strict server prompt / payment / source / command / movement / rollback coverage. Project remains **NOT READY**.
