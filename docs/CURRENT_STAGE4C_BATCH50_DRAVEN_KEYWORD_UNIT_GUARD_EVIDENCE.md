# Stage 4C-50 Draven Keyword Unit Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·148/221` / cardId `33240` / Draven / 德莱文: hero unit, cost 6, power 6, Spellshield text, first battle-win scoring text, and destroyed-in-battle opponent scoring text.
- `CATALOG` `SFD·148a/221` / cardId `33241`: same official text as `SFD·148/221`.
- `CORE-260330` p4-p8 rules 107-129: object visibility and public / hidden information frame.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- `CORE-260330` p92-p105 keyword rules 800+: keyword frame, including Spellshield as a keyword anchor.
- Existing FAQ refs remain open in coverage/risk docs: `BREAK-JFAQ-260416 p4`, `SOUL-JFAQ-260114 p25`, `SOUL-JFAQ-260114 p4`, `SOUL-OFAQ-260114 p16`, `SOUL-OFAQ-260114 p17`.

Implementation evidence:

- Existing preflight fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-draven-keyword-unit.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-draven-alt-a-keyword-unit.fixture.json`.
- 4C-50 records only the play-unit + `法盾` tag guard baseline for invalid target, wrong zone-source, opponent source, face-down standby source, and insufficient mana no mutation / no leak.
- Validation numbers: focused 17/17 passed; backend full 3625/3625 passed; frontend build passed; Chrome smoke passed.

Boundary: closes only Draven `SFD·148` / `SFD·148a` ordinary hand play-unit + `法盾` tag guard representative evidence. It does not close battle win scoring, destroyed-in-battle opponent scoring, Spellshield target tax, battle cleanup / score once-per-turn matrix, PaymentEngine, FAQ adjudication, 1009/811 full-official, or formal 18-step E2E.
