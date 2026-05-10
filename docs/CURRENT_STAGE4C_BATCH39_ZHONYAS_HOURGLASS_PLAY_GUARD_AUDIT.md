# Stage 4C-39 Zhonya's Hourglass Play Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Zhonya's Hourglass / 中娅沙漏 `OGN·077/298` / cardId `31291` / `FU-fb79eea7fc` / `ZHONYAS_HOURGLASS_PLAY_EQUIPMENT` ordinary hand play-equipment target guard representative baseline.

- B slice is guard-only; Core gap none, no Core change.
- Covered: hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base equipment.
- Covered: explicit target reject no mutation.
- Covered invalid guards with no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak: source not in hand / wrong zone, opponent source, face-down standby source, insufficient mana.
- Validation: A/B focused passed 268/268. D did not rerun tests.

Remaining open: standby / reaction timing, destroy replacement recall, full equipment / layer / FAQ, hidden info, 1009/811 full-official, final 18-step E2E.
