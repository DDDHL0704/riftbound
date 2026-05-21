4D-03LP-E audit: payment-cost Assembly Rack equipment / hidden blocker closure candidate.

Evidence:
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` maps `SFD·019/221` to `ASSEMBLY_RACK_PLAY_EQUIPMENT`.
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-assembly-rack-equipment.fixture.json` covers zero-target equipment hand-play into base.
- `tests/Riftbound.ConformanceTests/Fixtures/p4-play-assembly-rack-target-rejected.fixture.json` covers explicit-target rejection without mutation.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` contains Assembly Rack play and target-rejection regressions.
- `docs/rules-evidence-index.md` and `docs/p2-rules-preflight.md` record catalog, rules and fixture evidence for Assembly Rack play.

Decision:
- Remove row-level `NEEDS_ENGINE_SUPPORT` from the selected functional unit and snapshot row.
- Keep `IMPLEMENTED_UNTESTED`.
- Keep `NEEDS_AUTOMATED_TEST_EVIDENCE`.
- Keep `fullOfficial=false`.
- Keep `ready=false`.
- Do not close the tap-to-create-robot activated-skill, hidden-info / random-zone or full equipment lifecycle breadth.

Validation: passed for 4D-03LP-E: matrix JSON valid (jq empty); 03LP matrix/current-state guards 2/2; PaymentEngineCoverageAuditTests 601/601; Assembly Rack focused regression 5/5; adjacent prompt/payment/equipment/target/stack/hidden/visibility regression 2208/2208; backend full test 5172/5172; git diff --check passed.
