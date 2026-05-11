# Stage 4C-49 Ezreal Play-Unit Guard Audit

Status: representative-only; project **NOT READY**; `fullOfficial=false`.

Scope: Ezreal / 伊泽瑞尔 `SFD·082/221` / cardId `33162`, `SFD·082a/221` / cardId `33163`, and `SFD·082b/221·P` / cardId `33164` / `FU-2dca1ad450` combat-damage text play-unit guard representative baseline.

- Covered only: ordinary hand `PLAY_CARD` with 0 targets -> stack / pass-pass -> base unit, power 3, tag `CARD_TYPE:UNIT`, three printings covered.
- Covered guard: invalid target, wrong zone-source, opponent source, face-down standby source, insufficient mana rejected with no mutation / no leak.
- Validation: focused 21/21 passed; backend full 3617/3617 passed; frontend build passed; Chrome smoke passed.

Holdback: this batch does not implement or claim combat-damage / move runtime. Attack / defense trigger, "this location" enemy unit target selection, damage equal to Ezreal power, cannot combat damage static, blue swift move to base, swift / reaction timing, blue payment / `PAY_COST`, movement / control-zone matrix, damage prevention / replacement / cleanup, Layer / effective power, FAQ refs, 1009/811 full-official, and final 18-step E2E remain open.
