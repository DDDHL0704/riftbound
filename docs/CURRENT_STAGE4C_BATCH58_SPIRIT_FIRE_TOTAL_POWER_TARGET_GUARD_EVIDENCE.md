# Stage 4C-58 Spirit Fire Total Power Target Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGN·256/298` / cardId `31498` / Spirit Fire / 妖异狐火 / `FU-a9dc3495e1`: spell, cost 3, standby, swift; destroy any number of units in one battlefield whose total power is no more than 4.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- Full same-battlefield precision, spell-duel timing, cleanup, replacement / prevention, hidden-info, Spellshield tax, and FAQ adjudication references remain open in coverage / risk docs and are not closed by this representative evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-spirit-fire-destroy-total-power-four.fixture.json` covers ordinary hand play, pay 3, two battlefield unit targets totaling 4 power, stack / pass-pass, and destroy resolution.
- Existing rejection fixture: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-spirit-fire-total-power-too-high-rejected.fixture.json` covers total target power above 4 rejected with no payment / stack mutation.
- Existing Spellshield fixtures: `p4-play-spirit-fire-multiple-spellshield-tax.fixture.json` and `p4-play-spirit-fire-multiple-spellshield-tax-insufficient-rejected.fixture.json` cover representative multi-target Spellshield tax behavior, but do not close the full Spellshield matrix.
- New focused guard: `tests/Riftbound.ConformanceTests/SpiritFireDestroyGuardTests.cs` covers valid total-power-four destroy, total power above 4 rejection, non-unit battlefield equipment / spell / rune rejection, face-down standby rejection, stale target rejection, base / hand target rejection, dirty controller rejection, and no-mutation guarantees.
- Prompt parity guard: `SpiritFirePromptChoicesAndLegalSelectionsOnlyExposePublicBattlefieldUnits` verifies `targetChoicesByIndex` excludes non-unit / hidden / dirty / out-of-zone targets and `legalTargetSelections` excludes any selection containing the 5-power target.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now applies `IsSpiritFireTargetAllowed` during `PLAY_CARD` target validation before payment or stack mutation.
- Prompt code evidence: `src/Riftbound.Engine/MatchSession.cs` now applies `IsPromptSpiritFireTargetAllowed` when building target choices and server legal target selections.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpiritFireDestroyGuardTests|FullyQualifiedName~SpiritFire|FullyQualifiedName~TargetGuard|FullyQualifiedName~Spellshield"` passed 48/48.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~SpiritFire"` passed 112/112.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3690/3690.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-58 closes only this narrow representative guard. It does not close same-battlefield precision, standby / reaction and quick / spell-duel timing, full FEPR targeting / stack lifecycle, full destroy / cleanup / replacement / prevention / Last Breath interactions, full Spellshield tax matrix, full PaymentEngine, LayerEngine / effective power, hidden-info / redaction matrix, FAQ adjudication, 1009/811 full-official coverage, or formal 18-step E2E.
