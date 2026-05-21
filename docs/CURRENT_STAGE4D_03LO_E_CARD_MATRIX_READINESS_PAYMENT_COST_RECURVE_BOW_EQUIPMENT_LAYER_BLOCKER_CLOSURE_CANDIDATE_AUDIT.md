4D-03LO-E audit: payment-cost Recurve Bow equipment / layer blocker closure candidate.

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `SFD·016/221` to `RECURVE_BOW_PLAY_EQUIPMENT`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-recurve-bow-equipment.fixture.json` covers zero-target equipment hand-play into base.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-recurve-bow-target-rejected.fixture.json` covers explicit-target rejection without mutation.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` contains Recurve Bow play, target-rejection and assemble representative regressions.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs` asserts the Recurve Bow `ASSEMBLE_RED` ActionPrompt metadata.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record catalog, rules and fixture evidence for Recurve Bow play and assemble paths.

Decision:
- Remove row-level `NEEDS_ENGINE_SUPPORT` from the selected functional unit and snapshot row.
- Keep `IMPLEMENTED_UNTESTED`.
- Keep `NEEDS_AUTOMATED_TEST_EVIDENCE`.
- Keep `fullOfficial=false`.
- Keep `ready=false`.

Validation: passed for 4D-03LO-E: matrix JSON valid (jq empty); 03LO matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 599/599; Recurve Bow focused regression 6/6; adjacent prompt/payment/equipment/assemble/target/stack/layer regression 2293/2293; backend full test 5170/5170; git diff --check passed.
