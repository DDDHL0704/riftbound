# Stage 4C-45 Switcheroo Swap Guard Evidence

Representative evidence only; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·145/221` / cardId `33237` / 换换乐: spell, cost 2; choose two units at the same battlefield and switch their power this turn.
- `CORE-260330` p4-p8 rules 107-129: public / hidden information and object visibility.
- `CORE-260330` p14-p15 rules 142-143: unit object / power frame.
- `CORE-260330` p31-p33 rules 318-324: cleanup / this-turn duration frame.
- `CORE-260330` p39-p42 rules 355-356: play / stack / resolution frame.
- Existing FAQ ref remains attached in coverage/risk docs: `SOUL-JFAQ-260114 p14`.

Implementation evidence:

- Existing preflight fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-switcheroo-swap-battlefield-unit-powers.fixture.json`.
- Existing reject fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-switcheroo-duplicate-target-rejected.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-switcheroo-base-target-rejected.fixture.json`.
- B guard tests: `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`.
- B implementation touch: `src/Riftbound.Engine/CoreRuleEngine.cs`, limited to Switcheroo target guard hardening.
- Validation from A: focused command `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Switcheroo|FullyQualifiedName~PowerSwap|FullyQualifiedName~Power"` passed 284/284.
- Backend full passed 3594/3594; frontend build passed; Chrome smoke passed.

Boundary: closes only representative battlefield power-swap target guard overlay. It does not close true LayerEngine, later modifier ordering, duration cleanup / EOT expiry, same-battlefield precision beyond current representative model, damage / battle math, full FAQ `SOUL-JFAQ-260114 p14`, 1009/811 full-official, or final 18-step E2E.
