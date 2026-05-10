# Stage 4C-41 Giant Arm Kato Play Keyword Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Giant Arm Kato / 巨腕加藤 `SFD·112/221` / cardId `33198` / `FU-464ec8c275` / `GIANT_ARM_KATO_PLAY_KEYWORD_UNIT` ordinary hand play-unit keyword-tag target guard representative baseline.

- B slice is guard-only; Core gap none, no Core change.
- Covered: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 3, tags `CARD_TYPE:UNIT` + `法盾`.
- Covered invalid guards with no tick / no events / no payment / no hand movement / no stack / no unit entry / no leak: explicit target, wrong zone / source, opponent source, face-down standby source, insufficient mana.
- Validation: A/B focused passed 99/99. D did not rerun tests.

Remaining open: Spellshield target tax, move-to-battlefield trigger, friendly-unit choice / prompt, keyword grant, +power until EOT, LayerEngine / duration cleanup, movement / control matrix, FAQ, 1009/811 full-official, final 18-step E2E.
