# Stage 4D-03AO Ezreal Blue Swift Move To Base Baseline Evidence

日期：2026-05-15
结论：**BASELINE RECORDED / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

This baseline records the pre-implementation state for the next P0-005 keyword payment / colored-cost activated ability representative: `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` Ezreal blue swift move to base.

## 1. Current Evidence

- Official catalog text exists in `data/official/card-catalog.zh-CN.json`: `当我进攻或防守时，对此处的一名敌方单位造成等同于我战力的伤害。` / `我无法造成战斗伤害。` / `支付{{蓝色}}：{{迅捷}} — 将我移动到你的基地。`
- Current rules evidence only covers ordinary hand play via `p2-preflight-play-sfd-082-ezreal-combat-damage-static`, `p2-preflight-play-sfd-082a-ezreal-combat-damage-static` and `p2-preflight-play-sfd-082b-ezreal-combat-damage-static`.
- Current implementation routes are `SFD_082_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT`, `SFD_082A_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT` and `SFD_082B_EZREAL_COMBAT_DAMAGE_MOVE_PLAY_UNIT`; the blue swift activated movement path remains deferred.
- Current matrix state: `FU-2dca1ad450`, `IMPLEMENTED_TESTED` representative play evidence, `fullOfficial=false`, `NEEDS_ENGINE_SUPPORT`, `NEEDS_FAQ_REVIEW`, `REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT`.
- `P4ActivatedAbilityCatalog.GetAll()` currently has no executable Ezreal blue swift move-to-base ability.

## 2. Baseline Commands

Focused adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine"
```

Result: 179/179 passed.

Broader adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Ezreal|FullyQualifiedName~ActivateAbility|FullyQualifiedName~MoveUnit|FullyQualifiedName~PaymentEngine|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Priority"
```

Result: 372/372 passed.

Whitespace / diff baseline:

```sh
git diff --check
```

Result: passed.

## 3. Baseline Verdict

The adjacent test surface is green before implementing Ezreal's activated blue swift move-to-base path. This baseline does not implement runtime behavior and does not close P0-005, full official Ezreal or READY.
