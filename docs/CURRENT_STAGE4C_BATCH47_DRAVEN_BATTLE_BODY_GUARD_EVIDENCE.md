# Stage 4C-47 Draven Battle Body Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·020/221` / cardId `33092` / 德莱文: hero unit, cost 4, power 4; when Draven wins battle, play a dormant Gold equipment token; when Draven attacks or defends, you may pay red to give him +2 power this turn.
- `CATALOG` `SFD·020a/221` / cardId `33093` / 德莱文: same official text as `SFD·020/221`.
- `CORE-260330` p4-p8 rules 107-129: object visibility and public / hidden information frame.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- Existing FAQ refs remain open in coverage/risk docs: `BREAK-JFAQ-260416 p4`, `SOUL-JFAQ-260114 p25`, `SOUL-JFAQ-260114 p4`, `SOUL-OFAQ-260114 p16`, `SOUL-OFAQ-260114 p17`.

Implementation evidence:

- Existing preflight fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-020-draven-vanilla-unit.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-020a-draven-vanilla-unit.fixture.json`.
- B guard tests: `tests/Riftbound.ConformanceTests/DravenVanillaGuardTests.cs`.
- Core / frontend / protocol unchanged.
- Validation from A/B: focused command `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Draven|FullyQualifiedName~SFD020|FullyQualifiedName~VanillaPlayUnit|FullyQualifiedName~PlayUnit"` passed 14/14.

Boundary: closes only SFD·020 / SFD·020a ordinary hand play-unit body + guard representative evidence. It does not close battle win dormant Gold, attack / defense optional red payment, +2 until EOT, full PaymentEngine, Layer / duration cleanup, FAQ refs, 1009/811 full-official, or final 18-step E2E.
