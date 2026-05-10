# Stage 4C-44 Akshan Play Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·109/221` / cardId `33194` / 阿克尚: hero unit, cost 4, power 4, `哨兵`, `百炼`; when playing, may pay orange-orange extra cost to move enemy equipment to your base, control it until Akshan leaves, and attach it if it is a weapon / armament.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- `CORE-260330` p92-p105 keyword rules 800+: keyword / tag frame for representative keyword evidence.
- Existing FAQ refs remain attached in coverage/risk docs: `SOUL-JFAQ-260114 p18`, `SOUL-JFAQ-260114 p23`.

Implementation evidence:

- Existing preflight fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-akshan-no-optional-assemble-no-orange-extra.fixture.json`.
- B guard tests: `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs`.
- Validation from B and A: focused command `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Akshan|FullyQualifiedName~PlayUnit|FullyQualifiedName~KeywordUnit|FullyQualifiedName~Assemble"` passed; A result 189/189.
- Backend full passed 3582/3582; frontend build passed; Chrome smoke passed.

Boundary: closes only ordinary hand no-optional / no-extra play-unit guard representative evidence. It does not close optional assemble, orange-orange extra play, enemy equipment move / control, weapon attach, control-until-leaves cleanup, LayerEngine / continuous effects, FAQ full behavior, 1009/811 full-official, or final 18-step E2E.
