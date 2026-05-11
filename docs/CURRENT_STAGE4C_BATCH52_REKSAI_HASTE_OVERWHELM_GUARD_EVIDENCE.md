# Stage 4C-52 Rek'Sai Haste / Overwhelm Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·029/221` / cardId `33104` / Rek'Sai / 雷克塞: hero unit, cost 3, power 3, Haste optional additional cost text, Overwhelm / 强攻 text, and non-hand friendly unit gains haste text.
- `CATALOG` `SFD·029a/221` / cardId `33105`: same official text as `SFD·029/221`.
- `CORE-260330` p4-p8 rules 107-129: object visibility and public / hidden information frame.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- `CORE-260330` p92-p105 keyword rules 800+: keyword frame for Haste and Overwhelm / 强攻 anchors.
- Existing FAQ refs remain open in coverage/risk docs: `BREAK-JFAQ-260416 p3`, `SOUL-JFAQ-260114 p19`, `SOUL-OFAQ-260114 p4`.

Implementation evidence:

- Existing no-optional preflight fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-reksai-no-optional-haste.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-reksai-alt-a-no-optional-haste.fixture.json`.
- Related representative paid-branch fixtures remain non-full-official: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-reksai-haste-ready.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-reksai-alt-a-haste-ready.fixture.json`.
- 4C-52 records only the ordinary hand no-optional play-unit + keyword tag guard baseline for invalid target, wrong zone-source, opponent source, face-down standby source, and insufficient mana no mutation / no leak.
- Validation numbers: focused 305/305 passed; backend full 3641/3641 passed; frontend build passed; Chrome smoke passed.

Boundary: closes only Rek'Sai `SFD·029` / `SFD·029a` ordinary hand no-optional play-unit + `强攻` / `急速` keyword tag guard representative evidence. It does not close `HASTE_READY` paid branch full matrix, red resource exactness, Overwhelm / 强攻 battle modifier, `ASSIGN_COMBAT_DAMAGE` overflow behavior, non-hand friendly unit gains haste, LayerEngine, hidden-info, FAQ adjudication, 1009/811 full-official, or formal 18-step E2E.
