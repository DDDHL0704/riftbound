# Stage 4C-40 Sea Monster Hook Play Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGN·242/298` / cardId `31482` / 海兽钓钩: equipment; activated text pays 1 + yellow and exhausts, destroys a friendly unit, looks at top five, may free-play a qualifying unit, then recycles the remainder.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- Existing FAQ refs remain attached in coverage/risk docs: `BREAK-JFAQ-260416 p9`, `JFAQ-251023 p2`, `JFAQ-251023 p3`, `SOUL-JFAQ-260114 p22`.

Implementation evidence:

- B guard tests: `tests/Riftbound.ConformanceTests/SeaMonsterHookGuardTests.cs`.
- Focused validation from A/B: 272/272 passed.
- Core gap none; no Core change.

Boundary: closes only ordinary hand play-equipment target guard representative evidence. It does not close activated ability, destroy friendly unit, top-five look / choice, free play, recycle remainder, hidden / zone / payment / layer / FAQ, 1009/811 full-official, or final 18-step E2E.
