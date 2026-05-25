4D-03SU-E..4D-03SV-E non-payment FAQ / rule-source disposition evidence

Status: DOC_MATRIX_CURRENT evidence log. Project remains **NOT READY**.

Repository / branch evidence

- GitHub clone path: `/Users/dinghaolin/MyProjects/riftbound-dotnet`.
- Clone `main` status: clean at `09d57f6f checkpoint: stage 4D recovery spectator temporary payment resources`.
- `git merge-base --is-ancestor 09d57f6f HEAD` passed in the clone before DOC_MATRIX work proceeded.
- DOC_MATRIX worktree path: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`.
- DOC_MATRIX branch: `codex/stage4d-matrix-docs-current`.

Matrix evidence

- `stage4D03Su03SvFaqRuleSourceDispositionBundle` was added to `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- `FU-5cea85e7c3` / `SFD·128/221` 狂热粉丝 now keeps `IMPLEMENTED_TESTED` only and removes selected-row `NEEDS_FAQ_REVIEW`.
- `FU-422b450261` / `SFD·170/221` and `SFD·170a/221` 雷克塞 now keep `IMPLEMENTED_TESTED` + `SHARED_ORACLE_IMPLEMENTATION` and remove selected-row `NEEDS_FAQ_REVIEW`.
- Selected rows remain `fullOfficial=false`.

Count evidence

- Functional-unit FAQ blockers: `172`.
- Snapshot-entry FAQ blockers: `224`.
- Payment-cost FAQ blockers: `92`.
- Payment-or-targeting-stack-timing FAQ blockers: `122`.
- Payment-and-targeting-stack-timing FAQ blockers: `65`.
- Primary `freezeStatus=NEEDS_FAQ_REVIEW`: `128`.
- `fullOfficialTrue=0`; `ready=false`.

Validation evidence

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.
- Initial `PaymentEngineCoverageAuditTests` run failed because current residual baseline adjustment still reflected only `4D-03SP..03ST`; all-functional-units FAQ expected `174` but matrix had `172`, and payment-or-targeting expected `124` but matrix had `122`.
- Residual-baseline sync was limited to `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`.
- Re-run `PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed `697/697`.
- Adjacent/focused combined re-run `PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `3735/3735`.
- Final post-metadata-sync re-run `PATH="$HOME/.dotnet:$PATH" dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed `697/697`.
- `git diff --check`: passed.
- Conflict-marker scan `rg -n '^(<<<<<<<|=======|>>>>>>>)' docs tests --glob '!src/Riftbound.DevUi/node_modules/**'`: passed with no matches.

Stop / non-READY audit

- No runtime, frontend, protocol, official catalog, ordinary test implementation, Chrome/browser/formal E2E script, `fullOfficial`, final readiness, or `riftbound-dotnet.sln` change is included.
- No historical evidence is promoted to final acceptance evidence.
- Project remains **NOT READY**.
