# Stage 4C-49 Ezreal Play-Unit Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·082/221` / cardId `33162` / Ezreal / 伊泽瑞尔: hero unit, cost 4, power 3; attack / defense trigger damage text, cannot combat damage static, and blue swift move text remain holdback.
- `CATALOG` `SFD·082a/221` / cardId `33163`: same official text as `SFD·082/221`.
- `CATALOG` `SFD·082b/221·P` / cardId `33164`: same official text as `SFD·082/221`.
- `CORE-260330` p4-p8 rules 107-129: object visibility and public / hidden information frame.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- Existing FAQ refs remain open in coverage/risk docs: `BREAK-JFAQ-260416 p5`, `SOUL-JFAQ-260114 p19`, `SOUL-JFAQ-260114 p25`, `SOUL-OFAQ-260114 p20`.

Implementation evidence:

- Existing preflight fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-082-ezreal-combat-damage-static.fixture.json`, `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-082a-ezreal-combat-damage-static.fixture.json`, and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-082b-ezreal-combat-damage-static.fixture.json`.
- 4C-49 records only the play-unit guard baseline for invalid target, wrong zone-source, opponent source, face-down standby source, and insufficient mana no mutation / no leak.
- Validation numbers: focused 21/21 passed; backend full 3617/3617 passed; frontend build passed; Chrome smoke passed.

Boundary: closes only Ezreal ordinary hand play-unit + guard representative evidence. It does not close attack / defense trigger, "this location" enemy unit target selection, damage equal to Ezreal power, cannot combat damage static, blue swift move to base, swift / reaction timing, blue payment / `PAY_COST`, movement / control-zone matrix, damage prevention / replacement / cleanup, Layer / effective power, FAQ adjudication, 1009/811 full-official, or final 18-step E2E.
