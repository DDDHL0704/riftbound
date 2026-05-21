# 4D-05C B_SERVER Prompt: Fiora Trigger Payment Identity And Replay Guards

Date: 2026-05-21

Owner: `B_SERVER`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`

## Objective

Add focused server-side coverage for SFD Fiora typed/yellow `TRIGGER_PAYMENT` windows so they have active-window identity and closed-window replay hardening comparable to the Battlefield Conquer Gold trigger-payment path.

Recent P0-005 slices covered ordinary pending `PAY_COST` replay / identity / illegal choice / temporary-resource guards and generic Battlefield Conquer Gold trigger-payment replay / identity. Fiora already covers typed temporary-resource quote / spend / invalid-resource behavior, but still needs focused typed trigger-payment identity and replay regression coverage.

This is P0-005 rollback / revalidation narrowing evidence only. It does not close complete PaymentEngine, card matrix, P0/P1, frontend gates, Chrome smoke, formal E2E or READY.

## Allowed Write Scope

- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- Optional helper extraction inside that same file.
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused tests expose an actual runtime bug.
- Optional 05C audit/evidence docs if needed.

## Locked Scope

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
- Frontend files
- Official catalog / source snapshot files
- API/protocol core fields
- Browser / Chrome / formal E2E scripts
- `fullOfficial`
- READY / READY-CANDIDATE status
- `riftbound-dotnet.sln`

## Required Coverage

Add compact tests in `TriggerPaymentTests` using the existing SFD Fiora helpers around `ResolveFioraPowerfulReadyTriggerAsync`, `BuildFioraPowerfulReadyState`, `PayOneYellowPower`, and `Decline`.

Cover at least:

- active Fiora `TRIGGER_PAYMENT` rejects wrong `paymentId` without mutation
- active Fiora `TRIGGER_PAYMENT` rejects wrong `paymentWindow` without mutation
- replaying the original accepted typed payment command after the window closes rejects without mutation
- replaying the original decline command after the window closes rejects without mutation

Assertions should prove:

- rejected result has no events
- exact `MatchStateHasher` hash is preserved for rejected submissions
- active-window rejects keep the original `PendingPayment` with same `paymentId`, `paymentWindow`, `PlayerId` and legal choices
- active-window rejects do not ready / recycle / spend resources
- closed-window replays do not reopen `PendingPayment`
- closed-window replays do not emit duplicate `COST_PAID`, `PAYMENT_WINDOW_CLOSED`, `UNIT_READIED`, rune recycle, power-gain or other trigger side effects
- post-payment replay does not spend or ready a second time
- post-decline replay does not recycle / ready / spend
- authoritative `PAY_COST` prompt state remains consistent with the server snapshot

Prefer helper assertions that are local to `TriggerPaymentTests.cs` and reuse existing trigger-payment no-side-effect assertions when practical.

## Validation

Run at least:

```bash
source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~TriggerPaymentTests"
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RealTriggerQueue|FullyQualifiedName~ActionPrompt|FullyQualifiedName~Prompt|FullyQualifiedName~TemporaryPaymentResource"
git diff --check
```

If runtime code changes, also run full backend:

```bash
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## Stop Conditions

Stop and report to A_MAIN if:

- Any backend test fails after the intended change.
- The fix would require protocol core field changes.
- Hidden information leakage is observed.
- The implementation requires matrix JSON or `PaymentEngineCoverageAuditTests.cs` edits.
- The behavior conflicts with the service-side authority principle.
