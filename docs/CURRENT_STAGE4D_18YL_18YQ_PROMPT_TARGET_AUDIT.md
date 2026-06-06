# Stage 4D-18YL-18YQ Prompt Target Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched six patch-only workers against disjoint guard files to extend prompt target / no-target metadata coverage across disabled play-card, single-target spell, direct no-target unit, movement resource skill, typed haste-ready play and spellshield-tax target surfaces:

- 18YL: `BerserkImpulseGuardTests`
- 18YM: `SecretArtMercyBoonGuardTests`
- 18YN: `AkshanGuardTests`
- 18YO: `JhinMovementResourceSkillTests`
- 18YP: `ReksaiHasteReadyRedPaymentTests`
- 18YQ: `LuxHighCostPaidCostTriggerTests`

The workers changed only their assigned test files. A_MAIN handled focused validation, one Rek'Sai assertion adjustment, source commits and cherry-pick integration. No runtime code changed in this batch.

## Accepted Commits

- `8c798d23` -> `0b57b42b`: Berserk Impulse disabled prompt target leak guard
- `e81332b6` -> `8ded068b`: Secret Art: Mercy legal friendly-unit prompt target guard
- `6facf288` -> `fc8d4995`: Akshan no-target direct unit prompt guard
- `ab90def3` -> `a347836d`: Jhin movement resource no-target prompt metadata guard
- `c4421aaf` -> `5f7fdc40`: Rek'Sai haste-ready no-target prompt metadata guard
- `f18bee85` -> `932f23f5`: Lux paid-cost spellshield target prompt guard

## Validation

- Pre-dispatch main target-class baseline: `96/96`
- Worktree focused validation:
  - Berserk Impulse: `15/15`
  - Secret Art: Mercy: `13/13`
  - Akshan: `29/29`
  - Jhin movement resource skill: `16/16`
  - Rek'Sai haste-ready: initially failed on over-narrow `optionalCostChoices`, then `20/20`
  - Lux paid-cost spellshield target: `5/5`
- Main changed-class filter: `98/98`
- Main adjacent prompt/action filter: `1194/1194`
- Backend full conformance project: `7572/7572`

## Remaining Risk

This narrows prompt target exposure for the listed server-authoritative prompt surfaces only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
