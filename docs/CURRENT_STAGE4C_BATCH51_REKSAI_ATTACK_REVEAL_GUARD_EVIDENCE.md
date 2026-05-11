# Stage 4C-51 Rek'Sai Attack Reveal Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·170/221` / cardId `33264` / Rek'Sai / 雷克塞: hero unit, cost 5, power 5, attack-trigger text to optionally reveal the top two main-deck cards, play one, recycle the remainder, and optionally play a unit to the current battlefield / "here".
- `CATALOG` `SFD·170a/221` / cardId `33265`: same official text as `SFD·170/221`.
- `CORE-260330` p4-p8 rules 107-129: object visibility and public / hidden information frame.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- Existing FAQ refs remain open in coverage/risk docs: `BREAK-JFAQ-260416 p3`, `SOUL-JFAQ-260114 p19`, `SOUL-OFAQ-260114 p4`.

Implementation evidence:

- Existing preflight fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-170-reksai-attack-reveal-static.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-170a-reksai-attack-reveal-static.fixture.json`.
- Existing target-reject fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-sfd-170-reksai-target-rejected.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-sfd-170a-reksai-target-rejected.fixture.json`.
- 4C-51 records only the ordinary hand play-unit guard baseline for invalid target, wrong zone-source, opponent source, face-down standby source, and insufficient mana no mutation / no leak.
- Validation numbers: focused 25/25 passed; backend full 3633/3633 passed; frontend build passed; Chrome smoke passed.

Boundary: closes only Rek'Sai `SFD·170` / `SFD·170a` ordinary hand play-unit guard representative evidence. It does not close attack reveal runtime, top-2 reveal, free play, recycle remainder, unit destination to current battlefield / "here", hidden-info redaction / reveal matrix, `ORDER_TRIGGERS`, battle lifecycle full matrix, movement / control-zone, FAQ adjudication, 1009/811 full-official, or formal 18-step E2E.
