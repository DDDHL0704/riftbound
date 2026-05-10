# Stage 4C-43 Sfur Song Play Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Sfur Song / 斯弗尔尚歌 `SFD·059/221` / cardId `33139` / `FU-9a623b3185` / `SFUR_SONG_PLAY_EQUIPMENT` ordinary hand play-equipment target guard representative baseline.

- B slice is guard-only; Core unchanged, no Core change.
- Covered: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base equipment.
- Covered invalid guards with no tick / no events / no payment / no hand movement / no stack / no equipment entry / no leak: explicit target, wrong zone / source, opponent source, face-down standby source, insufficient mana.
- Validation: A rerun focused passed 268/268; backend full passed 3576/3576; frontend build passed; Chrome smoke passed. D did not run full tests.

Remaining open: copied host skill text, continuous text / layer, full assemble / equipment attach lifecycle, equipment control / zone movement, FAQ full behavior, 1009/811 full-official, final 18-step E2E.
