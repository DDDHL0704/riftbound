# Stage 4C-43 Sfur Song Play Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·059/221` / cardId `33139` / 斯弗尔尚歌: equipment / armament, cost 3; assemble cost is 1 + green; while attached, copies the host unit's skill text and keeps that text active while attached.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- `CORE-260330` p89 rules 718-719: equipment / attachment frame.
- Existing FAQ refs remain attached in coverage/risk docs: `SOUL-JFAQ-260114 p24`, `SOUL-JFAQ-260114 p25`, `SOUL-JFAQ-260114 p8`, `SOUL-OFAQ-260114 p18`, `SOUL-OFAQ-260114 p19`.

Implementation evidence:

- Existing preflight fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfur-song-equipment.fixture.json`.
- Existing target-reject fixture: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-sfur-song-target-rejected.fixture.json`.
- Existing assemble evidence: `p4-assemble-equipment-svarshang-song-attach` covers representative `ASSEMBLE_GREEN` attach, but not copied text / layer behavior.
- B guard tests: `tests/Riftbound.ConformanceTests/SfurSongGuardTests.cs`.
- Validation from A: focused 268/268 passed; backend full 3576/3576 passed; frontend build passed; Chrome smoke passed.
- Core unchanged; no Core change.

Boundary: closes only ordinary hand play-equipment target guard representative evidence. It does not close copied host skill text, continuous text / layer, full assemble / equipment attach lifecycle, equipment control / zone movement, FAQ full behavior, 1009/811 full-official, or final 18-step E2E.
