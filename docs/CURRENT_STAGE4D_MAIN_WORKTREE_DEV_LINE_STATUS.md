# Stage 4D Main Worktree Development Line Status

Date: 2026-05-21

Conclusion: **A-SIDE COORDINATION ACCEPTED / PROJECT NOT READY**

## Purpose

This file records the main worktree coordination boundary after the separate matrix-docs worktree was created for Stage 4D matrix number reduction.

The main worktree remains responsible for A-side architecture, planning, acceptance, P0/P1 closure routing, validation gates and development-line dispatch. The separate worktree owns pure matrix/docs number reduction.

## Worktrees

- Main A/dev worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`
  - Branch: `main`
  - Head at split verification: `1364dfbf`
  - Local status at split verification: only expected untracked `riftbound-dotnet.sln`
- Matrix-docs worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs`
  - Branch: `codex/stage4d-matrix-docs-20260521`
  - Head at split verification: `1364dfbf`

## Current Accepted Stage 4D State

Latest accepted shared baseline before the split remains `4D-03MQ-E`:

- Selected functional unit: `FU-7bb1b2d84a`
- Selected card: `SFD·083/221` 海克斯异常体
- Selected effect: `HEXTECH_ANOMALY_PLAY_EQUIPMENT`
- Snapshot entries: `1009`
- Functional units: `811`
- Payment-cost functional units: `360`
- Payment-cost snapshot entries: `446`
- Payment-cost `NEEDS_ENGINE_SUPPORT`: `172 -> 171`
- Primary payment-cost residual: `129 -> 129`
- Payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `359 -> 358`
- Payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `108 -> 107`
- `NEEDS_AUTOMATED_TEST_EVIDENCE` residual: `328`
- `NEEDS_FAQ_REVIEW` residual: `92`
- Primary FAQ residual: `61`
- `fullOfficialTrue`: `0`
- `ready`: `false`

Project status remains **NOT READY**.

## Write Lock Boundary

Matrix-docs worktree owns:

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `docs/CURRENT_CARD_EFFECT_COVERAGE_BASELINE.md`
- per-row Stage 4D matrix candidate / audit files
- FAQ/evidence row disposition docs needed only to reduce matrix numbers

Main worktree owns:

- A-side development-line dispatch and acceptance docs
- P0/P1 closure routing
- server/frontend implementation dispatch only after a concrete write lock is opened
- validation command records
- final acceptance and completion audit preparation

Main worktree must not edit matrix counts while the matrix-docs worktree is active unless the user explicitly asks to merge or hand the matrix lane back to this window.

## Next Main-Worktree Development Line

The next main-worktree task is not another row-level matrix reduction. It is to re-open the P0/P1 development line from current evidence and choose a concrete verifier or implementation slice.

Current high-priority route:

1. Re-read current P0/P1 closure evidence from `docs/CURRENT_COMPLETION_AUDIT.md`, `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md` and `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`.
2. Determine whether the next actionable slice belongs to:
   - `B_PAYMENT_ENGINE_OFFICIAL_BREADTH` / P0-005,
   - P1 LayerEngine / continuous effects,
   - FEPR target / stack lifecycle,
   - battle / spell-duel lifecycle only where current representative tests do not prove official breadth,
   - frontend final rerun preparation after backend/card gates are closer.
3. Open a narrow write lock before code changes.
4. Require focused tests, relevant adjacent regression, backend full test where warranted, and `git diff --check`.

## Locked Scope For This Coordination Batch

This coordination batch does not modify:

- runtime code
- frontend code
- Chrome / browser scripts
- formal 18-step E2E scripts
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `data/official/card-catalog.zh-CN.json`
- protocol core fields
- `fullOfficial`
- READY / READY-CANDIDATE status
- `riftbound-dotnet.sln`

## Validation

Validation run in the main worktree:

```txt
git diff --check
result: passed

jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
result: passed

source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
result: passed 655/655
```

Chrome smoke was not run because this batch does not modify frontend code, browser scripts or runtime interaction behavior.

## Non-Closure

This file is coordination evidence only. It does not close Stage 4D, P0-005, P0/P1, card matrix readiness, FAQ evidence readiness, frontend final validation, Chrome smoke, formal 18-step E2E, completion audit or READY.
