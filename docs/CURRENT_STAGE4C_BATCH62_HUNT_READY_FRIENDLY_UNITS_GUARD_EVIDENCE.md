# Stage 4C-62 Hunt Ready Friendly Units Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `SFD·204/221` / cardId `33303` / Hunt / 狩猎 / `FU-f877e60407`: signature spell, cost 1; ready all your units.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p33-p35 rules 327-340: target / FEPR frame for stack items.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- Full readiness lifecycle, multi-battlefield precision, hidden-info, and FAQ adjudication references remain open in coverage / risk docs and are not closed by this representative evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-hunt-ready-all-friendly-units.fixture.json` covers ordinary hand play, pay 1, zero targets, stack / pass-pass, and readying friendly units.
- New focused guard: `tests/Riftbound.ConformanceTests/HuntReadyGuardTests.cs` covers valid zero-target friendly public field-unit ready, battlefield equipment / spell / rune exclusion, face-down standby exclusion, dirty controller exclusion, enemy unit exclusion, and explicit-target no-mutation guarantees.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now resolves all-friendly ready through `GetControlledPublicFieldUnitObjectIds`.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HuntReadyGuardTests|FullyQualifiedName~HuntAndReadies|FullyQualifiedName~HuntReady"` passed 10/10.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Hunt|FullyQualifiedName~ReadyAll|FullyQualifiedName~ReadiesAll|FullyQualifiedName~UNIT_READIED|FullyQualifiedName~Overcharged|FullyQualifiedName~FieldUnit"` passed 121/121.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3731/3731.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-62 closes only this narrow representative guard. It does not close formal multi-battlefield precision beyond current public field-unit filtering, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full PaymentEngine, readiness duration / replacement / prevention interactions outside the representative ready set, LayerEngine / effective power and continuous-effect interactions, hidden-info / redaction matrix, FAQ adjudication if later evidence appears, 1009/811 full-official coverage, or formal 18-step E2E.
