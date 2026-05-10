# Stage 4C-41 Giant Arm Kato Play Keyword Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·112/221` / cardId `33198` / 巨腕加藤: unit, cost 4, power 3, `法盾`; when it moves to a battlefield, a friendly unit gains Kato's keywords and +power equal to Kato's power until end of turn.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- `CORE-260330` p92-p105 keyword rules 800+: keyword tag evidence, including Spellshield as a keyword family.
- Existing FAQ refs remain attached in coverage/risk docs: `SOUL-JFAQ-260114 p12`, `SOUL-JFAQ-260114 p3`, `SOUL-OFAQ-260114 p12`.

Implementation evidence:

- Existing preflight fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-giant-arm-kato-keyword-unit.fixture.json`.
- B guard tests: `tests/Riftbound.ConformanceTests/GiantArmKatoGuardTests.cs`.
- Focused validation from A/B: 99/99 passed.
- Core gap none; no Core change.

Boundary: closes only ordinary hand play-unit keyword-tag target guard representative evidence. It does not close Spellshield target tax, move-to-battlefield trigger, friendly-unit choice / prompt, keyword grant, +power until EOT, LayerEngine / duration cleanup, movement / control matrix, FAQ, 1009/811 full-official, or final 18-step E2E.
