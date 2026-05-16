# 4D-03DA PaymentEngine Target / Typed Activated Ability Official Runtime Card-Row Evidence

日期：2026-05-16
状态：**ACCEPTED / PROJECT NOT READY**

## Evidence Rows

| ability | source-card group | focused runtime evidence |
| --- | --- | --- |
| `PAY_RED_EXHAUST_DAMAGE_3` | `UNL-026/219` | `PaymentEngineUnificationTests` Xerath prompt / command / Spellshield tax / rollback verifier |
| `RENATA_GLASC_PAY_1_BLUE_DRAW_1` | `SFD·088/221`, `SFD·088a/221` | `RenataActivatedAbilityTests` draw prompt / command / typed-blue payment / temporary resource / stack / rollback verifier |
| `RENATA_GLASC_PAY_4_BLUE4_EXHAUST_SCORE_1` | `SFD·088/221`, `SFD·088a/221` | `RenataActivatedAbilityTests` score prompt / command / typed-blue payment / temporary resource / stack / rollback verifier |
| `AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT` | `SFD·050/221`, `SFD·050a/221` | `AzirSwiftSwapActivatedAbilityTests` green payment / target swap / optional armament reattach / once-per-turn / rollback verifier |
| `GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD` | `UNL-144/219` | `GatekeeperMaduliActivatedAbilityTests` purple payment / battlefield target / move resolution / stale-target rollback verifier |
| `EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE` | `SFD·082/221`, `SFD·082a/221`, `SFD·082b/221·P` | `EzrealBlueSwiftMoveToBaseActivatedAbilityTests` blue payment / no-target move-to-base / stale-source rollback verifier |
| `CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT` | `UNL-109/219` | `CrimsonRoseActivatedAbilityTests` experience payment / Spellshield target tax / ready-unit / cannot-ready rollback verifier |
| `SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER` | `UNL-194/219` | `ShadowActivatedAbilityTests` battle-response prompt / mana and generic power payment / Spellshield tax / stun / rollback verifier |

## Runtime Assertions

- `P4ActivatedAbilityCatalog.GetAll().Where(IsTargetColoredOrExperienceActivatedAbility)` remains exactly 8 entries and all remain `IsResourceSkill=false`.
- Source-card groups come from `P4ActivatedAbilityCatalog.SourceCardNosForAbility`, so Renata, Azir and Ezreal reprint groups are not collapsed.
- Focused verifier methods are executable `[Fact]` / `[Theory]` methods, not loose doc strings.
- Prompt, command, audit, runtime outcome and no-mutation rollback evidence are bound to each ability id and effect kind.
- `PaymentEngineCoverageAuditTests` reads `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` by exact `cardNo` / `collectorId` and confirms each row remains `fullOfficial=false`.

## Matrix / Readiness

This batch does not edit the card matrix JSON, does not upgrade `fullOfficial`, does not run Chrome smoke / formal 18-step, and does not claim P0-005/P1/READY closure. Project status remains **NOT READY**.

## Validation

- Focused target / typed activated ability verifier passed 406/406 on 2026-05-16.
- Adjacent PaymentEngine / activated ability / Spellshield / prompt / GameHub regression passed 666/666 on 2026-05-16.
- Backend full passed 4727/4727 on 2026-05-16.
- `git diff --check` passed on 2026-05-16.
