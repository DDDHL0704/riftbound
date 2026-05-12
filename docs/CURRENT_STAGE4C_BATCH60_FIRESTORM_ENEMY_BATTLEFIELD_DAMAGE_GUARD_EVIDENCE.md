# Stage 4C-60 Firestorm Enemy Battlefield Damage Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGS·002/024` / cardId `31581` / Firestorm / 烈火风暴 / `FU-fe9dbeea3d`: spell, cost 6; deal 3 damage to all enemy units on battlefields.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p33-p35 rules 327-340: target / FEPR frame for stack items.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- Full damage prevention / replacement / cleanup, lethal trigger, multi-battlefield precision, hidden-info, and FAQ adjudication references remain open in coverage / risk docs and are not closed by this representative evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-firestorm-damage-enemy-battlefield-units.fixture.json` covers ordinary hand play, pay 6, zero targets, stack / pass-pass, and damage to enemy battlefield units.
- Existing rejection fixture: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-firestorm-explicit-unit-target-rejected.fixture.json` covers explicit target rejection.
- New focused guard: `tests/Riftbound.ConformanceTests/FirestormEnemyBattlefieldDamageGuardTests.cs` covers valid zero-target enemy battlefield unit damage, battlefield equipment / spell / rune exclusion, face-down standby exclusion, dirty controller exclusion, friendly battlefield / enemy base exclusion, and explicit-target no-mutation guarantees.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now resolves enemy battlefield unit damage through `GetEnemyBattlefieldUnitObjectIds`, which delegates to the existing public battlefield unit / controller-consistency guard.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~FirestormEnemyBattlefieldDamageGuardTests|FullyQualifiedName~Firestorm"` passed 13/13.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Firestorm|FullyQualifiedName~CrescentStrike|FullyQualifiedName~BulletTime|FullyQualifiedName~DamageAllEnemyBattlefield|FullyQualifiedName~EnemyBattlefield"` passed 36/36.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3711/3711.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-60 closes only this narrow representative guard. It does not close damage prevention / replacement / cleanup and lethal-damage trigger interactions beyond the representative nonlethal damage set, formal multi-battlefield precision beyond current public battlefield unit filtering, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full PaymentEngine, LayerEngine / effective power and continuous-effect interactions, hidden-info / redaction matrix, FAQ adjudication if later evidence appears, 1009/811 full-official coverage, or formal 18-step E2E.
