# Stage 4C-42 Time Gate Play Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·078/221` / cardId `33158` / 预时之门: equipment, cost 3; activated text pays `[A]` and exhausts to make the next spell played this turn gain Echo equal to that spell's cost.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- Existing FAQ refs remain attached in coverage/risk docs: `BREAK-JFAQ-260416 p11`, `SOUL-JFAQ-260114 p15`, `SOUL-JFAQ-260114 p19`, `SOUL-JFAQ-260114 p25`, `SOUL-JFAQ-260114 p6`, `SOUL-OFAQ-260114 p21`.

Implementation evidence:

- Existing preflight fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-time-gate-equipment.fixture.json`.
- Existing target-reject fixture: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-time-gate-target-rejected.fixture.json`.
- Focused validation from A/B: 292/292 passed.
- Core gap none; no Core change.

Boundary: closes only ordinary hand play-equipment target guard representative evidence. It does not close activated / tap ability, payment `[A]`, next spell gains Echo, optional echo payment / repeat, duration cleanup, equipment exhaust / readiness lifecycle, FAQ timing, 1009/811 full-official, or final 18-step E2E.
