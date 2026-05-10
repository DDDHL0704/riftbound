# Stage 4C-40 Sea Monster Hook Play Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Sea Monster Hook / 海兽钓钩 `OGN·242/298` / cardId `31482` / `FU-2653af0380` / `SEA_MONSTER_HOOK_PLAY_EQUIPMENT` ordinary hand play-equipment target guard representative baseline.

- B slice is guard-only; Core gap none, no Core change.
- Covered: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base equipment.
- Covered invalid guards with no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak: explicit target, wrong zone / source, opponent source, face-down standby source, insufficient mana.
- Validation: A/B focused passed 272/272. D did not rerun tests.

Remaining open: activated ability pay 1 + yellow + exhaust, destroy friendly unit, top-five look / choice, free play, recycle remainder, hidden / zone / payment / layer / FAQ, 1009/811 full-official, final 18-step E2E.
