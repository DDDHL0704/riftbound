# Stage 4C-51 Rek'Sai Attack Reveal Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Rek'Sai / 雷克塞 `SFD·170/221` / cardId `33264` and `SFD·170a/221` / cardId `33265` / `FU-422b450261` attack reveal / movement text ordinary hand play-unit guard representative baseline.

- Covered only: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 5, tag `CARD_TYPE:UNIT`, two printings covered.
- Covered guard: invalid target, wrong zone-source, opponent source, face-down standby source, insufficient mana rejected with no mutation / no leak.
- Validation: focused 25/25 passed; backend full 3633/3633 passed; frontend build passed; Chrome smoke passed.

Holdback: this batch does not implement or claim attack reveal runtime or movement runtime. Attack reveal runtime, top-2 reveal, free play, recycle remainder, unit destination to current battlefield / "here", hidden-info redaction / reveal matrix, `ORDER_TRIGGERS`, battle lifecycle full matrix, movement / control-zone, FAQ refs, 1009/811 full-official, and formal 18-step E2E remain open.
