# Stage 4C-48 Vex Spellshield Stun Guard Audit

Status: test-only representative baseline; project **NOT READY**; `fullOfficial=false`.

Scope: Vex / 薇古丝 `UNL-150/219` / cardId `34697` / `FU-9f7cb73dc4` / `VEX_SPELLSHIELD_OPPONENT_UNIT_STUN_STATIC` spellshield opponent-unit stun static guard representative baseline.

- Covered only: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 4, tags `CARD_TYPE:UNIT` + `法盾` + `约德尔人`.
- Covered guard: invalid source / target / timing rejected with no mutation / no leak.
- Validation: focused 35/35 passed; backend full 3607/3607 passed; frontend build passed; Chrome smoke passed.

Holdback: this batch does not implement or claim opponent-unit stun runtime. Opponent unit-play listener, battlefield-only condition, `STUNNED` application, cannot-move-this-turn duration, movement guard / cleanup, Spellshield full target tax, FAQ adjudication, 1009/811 full-official, and final 18-step E2E remain open.
