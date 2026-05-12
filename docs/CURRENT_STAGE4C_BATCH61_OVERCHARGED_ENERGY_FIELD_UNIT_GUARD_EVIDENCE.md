# Stage 4C-61 Overcharged Energy Field Unit Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGN·123/298` / cardId `31345` / Overcharged Energy / 过载能量 / `FU-b2e0e1d8da`: spell, cost 7; exhaust all friendly units, then deal 12 damage to all units on battlefields.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p33-p35 rules 327-340: target / FEPR frame for stack items.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- `CORE-260330` p50: damage / exhaustion context referenced by the frozen matrix.
- Full damage prevention / replacement / cleanup, lethal trigger, multi-battlefield precision, hidden-info, and FAQ adjudication references remain open in coverage / risk docs and are not closed by this representative evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield.fixture.json` covers ordinary hand play, pay 7, zero targets, stack / pass-pass, friendly unit exhaustion, battlefield-unit damage, and a representative lethal damage cleanup.
- New focused guard: `tests/Riftbound.ConformanceTests/OverchargedEnergyGuardTests.cs` covers valid zero-target friendly public field-unit exhaust, public battlefield-unit damage, battlefield equipment / spell / rune exclusion, face-down standby exclusion, dirty controller exclusion, base unit exclusion from damage, and explicit-target no-mutation guarantees.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now resolves all-battlefield damage through `GetBattlefieldUnitObjectIds` and Overcharged Energy's all-friendly exhaust through `GetControlledPublicFieldUnitObjectIds`.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~OverchargedEnergyGuardTests|FullyQualifiedName~OverchargedEnergy|FullyQualifiedName~Overcharged"` passed 12/12.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Overcharged|FullyQualifiedName~Tibbers|FullyQualifiedName~BladeWhirlwind|FullyQualifiedName~DamageAllBattlefield|FullyQualifiedName~Firestorm|FullyQualifiedName~CrescentStrike|FullyQualifiedName~EnemyBattlefield"` passed 53/53.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3722/3722.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-61 closes only this narrow representative guard. It does not close damage prevention / replacement / cleanup and lethal-damage trigger interactions beyond representative guard assertions, formal multi-battlefield precision beyond current public battlefield unit filtering, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full PaymentEngine, LayerEngine / effective power and continuous-effect interactions, hidden-info / redaction matrix, FAQ adjudication if later evidence appears, 1009/811 full-official coverage, or formal 18-step E2E.
