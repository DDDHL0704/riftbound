# Stage 4C-38 Edge of Night Assemble Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Edge of Night / 夜之锋刃 `SFD·139/221` / cardId `33229` / `FU-804412488c` / `EDGE_OF_NIGHT_PLAY_EQUIPMENT` play-equipment / assemble-purple target guard representative baseline.

- B slice is test-only; Core gap none, no Core change.
- Covered: normal `PLAY_CARD` from hand with 0 targets -> stack / pass-pass -> base equipment.
- Covered: explicit play target rejected with no payment and no mutation.
- Covered: face-up controlled base Edge of Night `ASSEMBLE_PURPLE` -> friendly public unit target -> pay 1 purple -> `COST_PAID` + `EQUIPMENT_ATTACHED`.
- Covered invalid assemble no tick / no events / no payment / no stack / no attach / no leak: face-down / hidden source, source in hand, opponent source, already-attached source, unknown source, unknown / opponent / face-down standby / non-unit target, missing / wrong optional cost, insufficient purple.
- Validation: A focused filter passed 98/98. D did not rerun tests.

Remaining open: full official standby immediate attach, hidden redaction, equipment layer, FAQ regression, 1009/811 full-official, final 18-step E2E. Edge of Night face-down standby immediate attach remains P0 / design-gated.
