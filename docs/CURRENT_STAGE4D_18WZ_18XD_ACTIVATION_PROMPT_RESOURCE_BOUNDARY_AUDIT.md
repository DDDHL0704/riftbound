# Stage 4D-18WZ/18XA/18XB/18XC/18XD Activation Prompt Resource Boundary Audit

Date: 2026-06-07

## Summary

A_MAIN integrated five server-test slices from the 18WZ-18XD parallel batch, covering activation and play-card prompt resource metadata boundaries for Fluft Poro, Crimson Rose, Shadow, Renata, and Akshan.

Runtime changed: no. Test coverage changed: yes.

## Accepted Slices

- 18WZ Fluft Poro: worker source commit `65afbb39` was accepted into main as `23d73c6a`, adding `FluftPoroOpenMainPromptDoesNotExposeUnrelatedTemporaryPaymentResources`.
- 18XA Crimson Rose: worker source commit `979112e8` was accepted into main as `6df04e22`, adding `CrimsonRoseExperienceOnlyReadyUnitPromptHidesUnrelatedTemporaryPaymentResource`.
- 18XB Shadow: worker patch required A_MAIN metadata assertion correction from required shortfall to available temporary-resource power. Source commit `b3b7196f` was accepted into main as `284b661a`, adding `ShadowBattleResponsePromptQuotesTemporaryGenericPowerWhenShortOnePower`.
- 18XC Renata: worker patch required A_MAIN assertion-shape correction after validation showed the generic temporary resource prevents the typed-blue draw requirement from appearing at all. Source commit `e4b6ad43` was accepted into main as `f09a43af`, adding `RenataOpenMainPromptDoesNotTreatGenericTemporaryResourceAsBluePayment`.
- 18XD Akshan: worker source commit `b5b3d4fa` was accepted into main as `11432b56`, adding `AkshanOrangeStealPromptDoesNotQuoteGenericTemporaryResourceInPlayCardWindow`.

## Coordination Notes

This batch used five simultaneous worktrees from `c1dc850b`. Absolute-path patches again prevented main-worktree drift. The recurring worker validation failure persisted: all five worker-started `dotnet test` processes launched from each worktree's `src/Riftbound.Api` directory and stalled. A_MAIN interrupted the agents, killed only the batch-specific stuck processes, reviewed the diffs, reran validation, made the Shadow and Renata assertion corrections, committed each worktree, and cherry-picked the accepted commits into main.

A_MAIN's first root-level `dotnet test` attempt still inherited the same cwd drift into `src/Riftbound.Api`. The stable validation command for this environment was the explicit test project form:

`/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter "..."`

## Validation

- Pre-dispatch target-class baseline on main: `179/179`.
- 18WZ Fluft Poro focused in worktree: `26/26`.
- 18XA Crimson Rose focused in worktree: `33/33`.
- 18XB Shadow focused in worktree after A_MAIN assertion correction: `50/50`.
- 18XC Renata focused in worktree after A_MAIN assertion correction: `47/47`.
- 18XD Akshan focused in worktree: `28/28`.
- Combined changed-class filter on main: `184/184`.
- Backend full via explicit conformance test project: `7537/7537`.
- `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint.

## Remaining Risk

This narrows prompt metadata behavior for activation/play-card resource choices around zero-cost, experience-only, generic-power, typed-blue, and typed-orange optional-cost surfaces. It does not close broader P0/P1, command/recovery/random determinism, full PaymentEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
