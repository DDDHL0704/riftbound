# Stage 4C-56 Secret Art! Mercy Boon Guard Evidence

Representative evidence only, currently **VERIFIED REPRESENTATIVE**; project **NOT READY**; `fullOfficial=false`.

Official anchors:

- `CATALOG` `OGN·053/298` / cardId `31265` / Secret Art! Mercy / 秘奥义！慈悲度魂落 / `FU-3461727400`: spell, cost 3, standby, quick, grants Boon to a friendly unit; if that unit does not have Boon, it gains a +1 Boon; all Boons extra +1 this turn remains open.
- `CORE-260330` p14-p15 rules 142-143: unit / power frame.
- `CORE-260330` p31-p33 rules 318-324: stack / priority frame.
- `CORE-260330` p39-p42 rules 355-356: play / cost / resolution frame.
- `CORE-260330` p92-p105 keyword rules 800+: Spellshield keyword frame.
- Existing FAQ refs for Spellshield and Boon behavior remain open in coverage / risk docs and are not closed by this D-only evidence note.

Implementation evidence:

- Existing P2 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-secret-art-mercy-grant-boon.fixture.json` covers ordinary hand play, pay 3, friendly unit target, stack / pass-pass, `OBJECT_TAG_ADDED` `增益`, and `BOON_GRANTED` +1.
- Existing P4 fixture: `tests/Riftbound.ConformanceTests/Fixtures/p4-play-secret-art-mercy-friendly-spellshield-no-tax.fixture.json` covers friendly Spellshield target no-tax with `spellshieldTaxMana = 0`.
- New focused guard: `tests/Riftbound.ConformanceTests/SecretArtMercyBoonGuardTests.cs` covers friendly public unit success, friendly Spellshield no tax, friendly equipment / spell / rune rejection, stale target rejection, face-down standby rejection, invalid source rejection, insufficient mana rejection, and already-booned target no duplicate Boon / power.
- Prompt parity guard: `SecretArtMercyPromptOffersLegacyCustomTagFriendlyUnitButNotNonUnits` covers a legacy custom-tag public field unit (`黄沙士兵`) appearing in prompt target choices while friendly equipment, spell, rune, and face-down standby objects are excluded.
- Core code evidence: `src/Riftbound.Engine/CoreRuleEngine.cs` now uses `IsPlayerControlledFieldUnitObject` / `IsVisibleFieldUnitObject` for `FriendlyUnit`, `FriendlyUnitThenFriendlyUnit`, the first target of `FriendlyThenEnemyUnits`, and the first target of `FriendlyThenEnemyBattlefieldUnits`.
- Prompt code evidence: `src/Riftbound.Engine/MatchSession.cs` now uses the same visible-field-unit exclusion model for prompt candidates, allowing custom-tag public field units and excluding equipment, spell, rune, standby, and face-down objects.
- Focused validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SecretArtMercy|FullyQualifiedName~Boon|FullyQualifiedName~Spellshield"` passed 87/87.
- Regression validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SandSoldiersRise|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~FriendlyUnit"` passed 133/133.
- Full validation: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed 3668/3668.
- Frontend validation: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` passed.
- Chrome smoke: `cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` passed.

Boundary: 4C-56 closes only this narrow representative guard. It does not close standby / reaction, quick / spell-duel breadth, global all-boons extra +1 this turn, LayerEngine / duration cleanup, full target matrix, full Spellshield tax, PaymentEngine, FAQ adjudication, 1009/811 full-official, or formal 18-step E2E.
