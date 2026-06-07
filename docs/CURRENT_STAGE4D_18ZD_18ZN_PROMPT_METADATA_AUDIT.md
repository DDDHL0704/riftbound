# Stage 4D-18ZD-18ZN Prompt Metadata Audit

Date: 2026-06-07

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN dispatched eleven patch-only workers across two consecutive parallel prompt-metadata batches:

- 18ZD: `GustReturnToHandTests`
- 18ZE: `RideTheWindMoveGuardTests`
- 18ZF: `FirestormEnemyBattlefieldDamageGuardTests`
- 18ZG: `ReflectionsSwapGuardTests`
- 18ZH: `SpiritFireDestroyGuardTests`
- 18ZI: `BerserkImpulseGuardTests`
- 18ZJ: `ReksaiNoOptionalHasteOverwhelmGuardTests`
- 18ZK: `EdgeOfNightAssembleGuardTests`
- 18ZL: `SfurSongGuardTests`
- 18ZM: `VexAltSpellshieldGuardTests`
- 18ZN: `HuntReadyGuardTests`

The workers changed only their assigned test files. A_MAIN handled focused validation, corrected the 18ZI Berserk disabled-prompt metadata assertion after focused validation showed the stable shape is empty `sourceRequirements`, committed source worktree diffs, and cherry-picked them into main. No runtime code changed in this batch.

## Accepted Commits

- `7f7543e7` -> `d339cc63`: Gust source target-choice metadata is mandatory and single-slot.
- `0775d641` -> `9a5b7a28`: Ride the Wind source target-choice metadata is mandatory and single-slot.
- `0840aab7` -> `358dd54b`: Firestorm source target-choice metadata is mandatory and empty.
- `8303dad3` -> `31072854`: Reflections source target-choice metadata exposes both swap target slots.
- `3fd987a5` -> `990c0c9e`: Spirit Fire source target-choice metadata exposes all four target slots.
- `ddd0266b` -> `bf6336eb`: Berserk Impulse disabled prompt metadata declares no source requirements or target choices.
- `4826d2b9` -> `f643efd0`: Rek'Sai no-optional prompt metadata declares empty target choices.
- `6fe1d348` -> `5cd5e0e4`: Edge of Night prompt metadata declares empty target choices.
- `0c18eddc` -> `192d5d59`: Sfur Song prompt metadata declares empty target choices.
- `75d5b2e6` -> `42e47a05`: Vex Alt prompt metadata declares empty target choices.
- `332f13cf` -> `d549348a`: Hunt prompt metadata declares empty target choices.

## Validation

- Pre-dispatch main baselines: prompt-choice batch `5/5`; zero-target metadata batch `7/7`.
- Worktree focused validation:
  - Gust: `1/1`
  - Ride the Wind: `1/1`
  - Firestorm: `1/1`
  - Reflections: `1/1`
  - Spirit Fire: `1/1`
  - Berserk Impulse: initial focused run failed on an over-specific source requirement assertion; A_MAIN corrected to the current empty-source-requirements shape and reran `1/1`.
  - Rek'Sai no optional: `2/2`
  - Edge of Night: `1/1`
  - Sfur Song: `1/1`
  - Vex Alt: `1/1`
  - Hunt: `1/1`
- Main changed-class filter: `74/74`
- Main adjacent prompt/action filter: `3063/3063`
- Backend full conformance project: `7572/7572`

## Remaining Risk

This narrows `PLAY_CARD` prompt metadata target-choice and no-target metadata exposure only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
