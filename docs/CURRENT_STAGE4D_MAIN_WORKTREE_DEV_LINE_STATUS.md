# Stage 4D Main Worktree Development Line Status

Date: 2026-05-22

Conclusion: **A-SIDE 03RX-03SB ACCEPTED / DOC_MATRIX NO-IDLE CONTINUES / PROJECT NOT READY**

## Purpose

This file records the main worktree coordination boundary after the separate matrix-docs worktree was created for Stage 4D matrix number reduction.

The main worktree remains responsible for A-side architecture, planning, acceptance, P0/P1 closure routing, validation gates and development-line dispatch. The separate worktree owns pure matrix/docs number reduction.

## Worktrees

- Main A/dev worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet`
  - Branch: `main`
  - Latest observed head: `f7f7b9c5` plus A_MAIN accept guard in progress
  - Local status at latest verification: only expected untracked `riftbound-dotnet.sln`
- Matrix-docs current worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`
  - Branch: `codex/stage4d-matrix-docs-current-20260521-next`
  - Latest observed closure head: `603b5ae0`
  - Local status at latest verification: clean

## Current Accepted Stage 4D State

Latest accepted shared baseline is the 03RX..03SB implemented-tested evidence bundle accepted on main:

- Snapshot entries: `1009`
- Functional units: `811`
- Source commit: `8e852ee9`, accepted on main as `1e4250fe`
- DOC_MATRIX handoff guard: `1ecf5931`, accepted on main as `f7f7b9c5`
- All FU `NEEDS_ENGINE_SUPPORT`: `428`
- Implemented-tested evidence residual: `13`
- Payment-cost `NEEDS_ENGINE_SUPPORT`: `35`
- Primary payment-cost residual: `0`
- Targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `203`
- Cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: `164`
- Hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: `144`
- Payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `217`
- Payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: `21`
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

Main worktree must not edit matrix counts while the matrix-docs worktree is active unless the user explicitly asks to merge or hand the matrix lane back to this window. A_MAIN may, however, open / close DOC_MATRIX scopes through the shared board and then accept/reject the resulting handoff.

## Next Main-Worktree Development Line

The immediate next coordination task is to keep DOC_MATRIX_CURRENT productive without widening into runtime/frontend/READY work. The primary payment-cost B/D residual lane has no executable candidates, so A_MAIN opened a new docs-only implemented-tested evidence lane.

Current DOC_MATRIX route:

1. `DOC_MATRIX_CURRENT` may select the next executable 3-5 current-matrix rows where:
   - `stage4B.fullOfficialBlockers` includes `NEEDS_ENGINE_SUPPORT`
   - `stage4B.freezeStatus=IMPLEMENTED_TESTED`
   - `stage4B.automatedTests.status=REPRESENTATIVE_AUTOMATED_EVIDENCE_PRESENT` or an equivalent runtime-window evidence status
2. First seed candidates from current matrix order are 顺劈, 海克斯射线, 自适应机器人, 拉文布鲁姆学生 and 决斗.
3. The bundle remains docs-only: matrix JSON, baseline/current docs, per-bundle candidate/audit docs, shared board and `PaymentEngineCoverageAuditTests.cs` baseline synchronization.
4. Required handoff validation remains `jq`, `git diff --check`, conflict-marker scan, count script, PaymentEngineCoverageAuditTests, ConformanceFixtureRunnerTests and backend full unless A_MAIN narrows after seeing the exact diff.

Current main-worktree development route remains open behind the DOC_MATRIX no-idle lane:

1. Re-read current P0/P1 closure evidence from `docs/CURRENT_COMPLETION_AUDIT.md`, `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md` and server-rule audits.
2. Choose the next concrete B/C/D implementation or verifier slice only after the DOC_MATRIX handoff state is clean.
3. Open a narrow write lock before code changes.
4. Require focused tests, relevant adjacent regression, backend full test where warranted, and `git diff --check`.

## Locked Scope For This Coordination Batch

This coordination batch does not modify:

- runtime code
- frontend code
- Chrome / browser scripts
- formal 18-step E2E scripts
- `data/official/card-catalog.zh-CN.json`
- protocol core fields
- `fullOfficial`
- READY / READY-CANDIDATE status
- `riftbound-dotnet.sln`

The active DOC_MATRIX lane may modify matrix JSON and `PaymentEngineCoverageAuditTests.cs` only inside the docs-only evidence synchronization scope above.

## Validation

Validation run in the main worktree:

```txt
git diff --check
result: passed

jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json
result: passed

/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter FullyQualifiedName~PaymentEngineCoverageAuditTests
result: passed 697/697

/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore
result: passed 5344/5344
```

Chrome smoke was not run because this batch does not modify frontend code, browser scripts or runtime interaction behavior.

## Non-Closure

This file is coordination evidence only. It does not close Stage 4D, P0-005, P0/P1, card matrix readiness, FAQ evidence readiness, frontend final validation, Chrome smoke, formal 18-step E2E, completion audit or READY.
