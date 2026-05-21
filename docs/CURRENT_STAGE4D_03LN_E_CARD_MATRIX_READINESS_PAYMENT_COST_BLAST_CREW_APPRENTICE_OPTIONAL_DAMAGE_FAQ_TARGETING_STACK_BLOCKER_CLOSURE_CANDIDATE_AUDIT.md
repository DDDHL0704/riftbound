4D-03LN-E audit: payment-cost Blast Crew Apprentice optional-damage FAQ / targeting-stack blocker closure candidate.

Evidence anchors:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `SFD·013/221` 爆破队学员 to `BLAST_CREW_APPRENTICE_PLAY_UNIT_OPTIONAL_DAMAGE`.
- `src/Riftbound.Engine/MatchSession.cs` exposes server-authored optional-cost prompt candidates for the selected row.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-blast-crew-apprentice-no-optional-damage.fixture.json` covers base hand-play without optional damage.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-blast-crew-apprentice-optional-damage.fixture.json` covers optional `SPEND_MANA:1` plus `SPEND_POWER:red:1` damage.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` keeps the no-optional, optional-damage and direct target-rejection representatives.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` retain the current catalog and rules evidence mapping.

Audit decision:
- This batch only removes the row-level `NEEDS_ENGINE_SUPPORT` blocker from `FU-13a792549c` and its single snapshot entry.
- `freezeStatus` stays `NEEDS_FAQ_REVIEW`.
- `NEEDS_FAQ_REVIEW` and `NEEDS_AUTOMATED_TEST_EVIDENCE` remain blockers.
- `fullOfficial` stays false and the project remains NOT READY.
- Runtime, frontend, Chrome/browser scripts, official catalog, protocol core fields, final readiness flags and `riftbound-dotnet.sln` remain locked.

Validation: passed for 4D-03LN-E: matrix JSON valid (jq empty); 03LN matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 597/597; Blast Crew focused regression 39/39; adjacent prompt/payment/target/stack/damage regression 1985/1985; backend full test 5168/5168; git diff --check passed.
