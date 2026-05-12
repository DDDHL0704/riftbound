# Stage 4C-57 Reflections Swap Draw Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `UNL-083/219` / cardId `34618` / Reflections / 镜中幻影 / `FU-f0eb0fb704`: spell, cost 2, standby, swift; choose two controlled units in different positions, at least one has Ephemeral / `瞬息`, move each to the other's position, draw 1.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- Full timing, movement, hidden-info, draw replacement, and FAQ adjudication references remain open in coverage / risk docs and are not closed by this representative evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-reflections-swap-draw.fixture.json` covers ordinary hand play, pay 2, two friendly unit targets in different public zones, one Ephemeral target, stack / pass-pass, swap, and draw 1.
- Existing historical tests in `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`: `CoreRuleEnginePlaysReflectionsSwapDraw`, `CoreRuleEngineRejectsReflectionsWithoutEphemeralTarget`, and `CoreRuleEngineReflectionsResolutionSkipsOpponentControlledFriendlyZoneTarget`.
- New focused guard: `tests/Riftbound.ConformanceTests/ReflectionsSwapGuardTests.cs` covers valid base + battlefield swap/draw, no-Ephemeral rejection, same-position rejection, friendly equipment / spell / rune rejection, face-down standby rejection, stale target rejection, enemy unit rejection, dirty controller rejection, and no-mutation guarantees.
- Prompt parity guard: `ReflectionsPromptLegalSelectionsRequireEphemeralAndDifferentPositions` verifies `legalTargetSelections` include only Ephemeral-qualified different-position pairs while excluding non-unit, hidden, enemy, and dirty targets.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now uses `HasValidSwapTargetLocations` for `SwapsTargetLocations` play-card validation before any payment or stack mutation.
- Prompt code evidence: `src/Riftbound.Engine/MatchSession.cs` now marks `AnyTargetRequiredTag` and `SwapsTargetLocations` as server target-selection constraints and filters prompt `legalTargetSelections` through tag and different-position guards.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Reflections|FullyQualifiedName~Swap|FullyQualifiedName~FriendlyUnit"` passed 54/54.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~SandSoldiersRise|FullyQualifiedName~Reflections"` passed 112/112.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3679/3679.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-57 closes only this narrow representative guard. It does not close exact multi-battlefield / different-position precision beyond the representative model, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full movement / control-zone lifecycle, owner/controller split across all zones, hidden-info / redaction matrix, full target prompt matrix, PaymentEngine full officialization, Ephemeral lifecycle / cleanup interactions, draw replacement / deck exhaustion, FAQ adjudication, 1009/811 full-official, or formal 18-step E2E.
