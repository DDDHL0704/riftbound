# Stage 4D-04Y B Worker Prompt

Owner: `B_SERVER`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

Branch / baseline: `main` after A_MAIN coordination commit `56ea755f`.

Project status remains **NOT READY**. Do not output READY-CANDIDATE, do not mark the goal complete, and do not touch `riftbound-dotnet.sln`.

You are not alone in the codebase. `DOC_MATRIX_CURRENT` is active in a separate worktree on matrix/current-docs and `PaymentEngineCoverageAuditTests.cs`; do not revert or overwrite that work, and do not touch the locked files below.

## Objective

Add focused PaymentEngine coverage, and only fix runtime if the tests expose a real bug, for ordinary non-trigger pending `PAY_COST` stale replay after the payment window closes.

Required behavior:

- First submission of a valid server-authored `PAY_COST` command succeeds and closes the pending payment window.
- Replaying the exact same command after the window closes is rejected.
- Replay emits no events.
- Replay preserves exact `MatchStateHasher.Hash(...)`.
- Replay does not restore `PendingPayment`, does not re-open a prompt and does not double-spend rune-pool resources.

Cover these ordinary pending-payment spend shapes:

- mana via `SPEND_MANA:1`
- generic power via `SPEND_POWER:1`
- typed power via `SPEND_POWER:red:1` or an equivalent normalized trait

## Allowed Files

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- Optional narrow helper extraction inside the same test file
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the new tests expose a runtime bug
- Optional focused docs:
  - `docs/CURRENT_STAGE4D_04Y_PAYMENTENGINE_PENDING_PAY_COST_REPLAY_AUDIT.md`
  - `docs/CURRENT_STAGE4D_04Y_PAYMENTENGINE_PENDING_PAY_COST_REPLAY_EVIDENCE.md`

## Locked Files

Do not modify:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- frontend files
- official catalog / snapshot data
- API/protocol core fields
- Chrome/browser/formal E2E scripts
- `fullOfficial` / READY status
- `riftbound-dotnet.sln`

## Validation

Run at least:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests"
```

If runtime changes, also run:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngine|FullyQualifiedName~PaymentPlan|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt"
```

Always run:

```bash
git diff --check
```

Return changed files, validation commands/results, and whether runtime changed.
