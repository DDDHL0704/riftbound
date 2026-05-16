# 4D-03CZ PaymentEngine Typed Sigil Resource Skill Runtime Card-Row Evidence

日期：2026-05-16
状态：**ACCEPTED / PROJECT NOT READY**

## Evidence Rows

| row | ability evidence | runtime evidence |
| --- | --- | --- |
| `SFD·222/221` | `RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER` | Rage Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `SFD·226/221` | `FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER` | SFD Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `SFD·229/221` | `INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER` | SFD Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `SFD·231/221` | `POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER` | SFD Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `SFD·234/221` | `DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER` | SFD Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `SFD·238/221` | `UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER` | SFD Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `OGN·040/298` | `OGN_RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER` | OGN Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `OGN·081/298` | `OGN_FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER` | OGN Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `OGN·120/298` | `OGN_INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER` | OGN Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `OGN·163/298` | `OGN_POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER` | OGN Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `OGN·204/298` | `OGN_DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER` | OGN Sigil prompt / command / audit / lifetime / rollback focused verifier |
| `OGN·245/298` | `OGN_UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER` | OGN Sigil prompt / command / audit / lifetime / rollback focused verifier |

## Runtime Assertions

- Prompt sourceRequirements expose exact `cardNo`, `abilityId`, `typedPaymentOnlyResource=true`, `resourceRestriction`, reaction timing, no-target bounds and generated trait power.
- Command resolution revalidates source card, controlled public base equipment, ability id, no targets, no optional-cost payload and stack-priority reaction timing.
- `ABILITY_ACTIVATED` and `POWER_GAINED` audit payloads bind `cardNo`, `abilityId`, `effectKind`, trait power, resource restriction, temporary payment resource id and allowed payment kind.
- Generated typed temporary resources can pay same-color or generic rune costs, reject wrong-color and mana-only windows without mutation, and clear after legal payment.
- OGN and SFD reprint ability ids cannot activate the other printing.

## Matrix / Readiness

`PaymentEngineCoverageAuditTests` reads `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` by exact `cardNo` / `collectorId` for all 12 rows and asserts `stage4B.fullOfficial=false`. This batch does not edit the matrix JSON, does not close P0-005/P1, and does not claim READY.
