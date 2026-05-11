# Stage 4C-48 Vex Spellshield Stun Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `UNL-150/219` / cardId `34697` / Vex / 薇古丝: hero unit, cost 4, power 4, Yordle tag, Spellshield text, and opponent-unit stun static text.
- `CORE-260330` p4-p8 rules 107-129: object visibility and public / hidden information frame.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- `CORE-260330` p92-p105 keyword rules 800+: keyword frame, including Spellshield as a keyword anchor.
- Existing FAQ refs remain open in coverage/risk docs: `BREAK-JFAQ-260416 p5`, `BREAK-JFAQ-260416 p9`, `SOUL-JFAQ-260114 p17`.

Implementation evidence:

- Existing preflight fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-vex-spellshield-stun-static.fixture.json`.
- 4C-48 records only the test-only guard baseline for invalid source / target / timing no mutation / no leak.
- Validation: focused 35/35 passed; backend full 3607/3607 passed; frontend build passed; Chrome smoke passed.

Boundary: closes only Vex ordinary hand spellshield-tag play-unit + guard representative evidence. It does not close opponent unit-play listener, battlefield-only condition, `STUNNED` application, cannot-move-this-turn duration, movement guard / cleanup, Spellshield full target tax, FAQ adjudication, 1009/811 full-official, or final 18-step E2E.
