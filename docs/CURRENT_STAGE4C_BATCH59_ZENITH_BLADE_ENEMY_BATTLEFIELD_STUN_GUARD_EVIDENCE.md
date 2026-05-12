# Stage 4C-59 Zenith Blade Enemy Battlefield Stun Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGN·262/298` / cardId `31504` / Zenith Blade / 天顶之刃 / `FU-64a7f67581`: spell, cost 3, swift; stun an enemy unit on any battlefield; optionally move a friendly unit to that enemy unit's battlefield.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p33-p35 rules 327-340: target / FEPR frame for stack items.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- Full optional movement, spell-duel timing, cleanup, replacement / prevention, hidden-info, Spellshield tax, and FAQ adjudication references remain open in coverage / risk docs and are not closed by this representative evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-zenith-blade-stun-enemy-battlefield-unit.fixture.json` covers ordinary hand play, pay 3, enemy battlefield unit target, stack / pass-pass, and `STUNNED` application.
- Existing rejection fixtures: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-zenith-blade-base-unit-target-rejected.fixture.json` and `tests/Riftbound.ConformanceTests/Fixtures/p4-play-zenith-blade-friendly-target-rejected.fixture.json` cover enemy base unit and friendly battlefield unit rejection.
- New focused guard: `tests/Riftbound.ConformanceTests/ZenithBladeStunGuardTests.cs` covers valid enemy battlefield stun, non-unit battlefield equipment / spell / rune rejection, face-down standby rejection, stale target rejection, base / hand / friendly target rejection, dirty controller rejection, and no-mutation guarantees.
- Prompt parity guard: `ZenithBladePromptChoicesOnlyExposeEnemyPublicBattlefieldUnits` verifies `targetChoicesByIndex` exposes only enemy public battlefield unit choices for this representative prompt and excludes non-unit / hidden / dirty / friendly / out-of-zone targets.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now applies `IsZenithBladeTargetAllowed` during `PLAY_CARD` target validation before payment or stack mutation.
- Prompt code evidence: no prompt code change was needed; `MatchSession` already filters `EnemyBattlefieldUnit` candidates through visible enemy field-unit helpers. The new prompt test freezes that parity.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZenithBladeStunGuardTests|FullyQualifiedName~ZenithBlade"` passed 15/15.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ZenithBlade|FullyQualifiedName~Stun|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt"` passed 154/154.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3701/3701.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-59 closes only this narrow representative guard. It does not close optional friendly unit movement, precise multi-battlefield destination selection, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, status duration cleanup / replacement / prevention interactions, full Spellshield tax matrix, full PaymentEngine, LayerEngine / effective power and duration ordering, hidden-info / redaction matrix, FAQ adjudication, 1009/811 full-official coverage, or formal 18-step E2E.
