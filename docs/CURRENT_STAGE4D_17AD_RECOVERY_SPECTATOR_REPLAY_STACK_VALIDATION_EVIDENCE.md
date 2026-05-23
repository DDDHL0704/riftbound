# Stage 4D-17AD Recovery Spectator Replay Stack Validation Evidence

2026-05-24 Stage 4D-17AD accepted evidence.

Commands run in A_MAIN:

- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"`
  - Result: passed `56/56`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Result: passed `637/637`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
  - Result: passed `6002/6002`.

Mechanical checks passed before commit:

- `git diff --check`
  - Result: passed.
- `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`
  - Result: passed, no conflict markers found.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
  - Result: passed.

Observed behavior:

- Recovery validation reports `spectator replay frame snapshot stack is required` when a recovered spectator replay snapshot omits stack metadata.
- The validation keeps protocol shape unchanged and only rejects malformed recovery-frame data.

Out of scope:

- No protocol shape change.
- No frontend change.
- No matrix JSON change.
- No official catalog, `fullOfficial`, READY or READY-CANDIDATE change.

Project remains **NOT READY**.
