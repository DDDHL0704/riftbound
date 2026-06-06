# Stage 4D-18XE/18XF/18XG Typed Prompt Generic Boundary Audit

Date: 2026-06-07

## Summary

A_MAIN integrated three server-test slices from the 18XE-18XG parallel batch, covering typed activation prompt boundaries where generic temporary payment resources must not make typed-blue, typed-green, or typed-purple abilities appear payable.

Runtime changed: no. Test coverage changed: yes.

## Accepted Slices

- 18XE Ezreal Blue Swift: worker source commit `dd1dc053` was accepted into main as `71658fdc`, adding `PromptDoesNotExposeEzrealSwiftMoveRequirementWithOnlyGenericTemporaryResource`.
- 18XF Azir Swift Swap: worker source commit `b11e21f1` was accepted into main as `9ce97552`, adding `PromptDoesNotTreatGenericTemporaryResourceAsAzirGreenPayment`.
- 18XG Gatekeeper Maduli: worker source commit `4bb8ee8c` was accepted into main as `da04fdeb`, adding `MaduliOpenMainPromptDoesNotTreatGenericTemporaryResourceAsPurplePayment`.

## Coordination Notes

This batch used three simultaneous worktrees from `72253355`. Workers were deliberately instructed to patch only, run `git diff --check`, and avoid `dotnet test`, staging, and commits. That avoided the repeated `src/Riftbound.Api` test-cwd hang from prior batches. A_MAIN reviewed the diffs, ran focused validation with restore in each fresh worktree, committed the accepted slices, and cherry-picked them into main.

## Validation

- Pre-dispatch target-class baseline on main: `89/89`.
- 18XE Ezreal focused in worktree: `30/30`.
- 18XF Azir focused in worktree: `35/35`.
- 18XG Gatekeeper Maduli focused in worktree: `27/27`.
- Combined changed-class filter on main: `92/92`.
- Backend full via explicit conformance test project: `7540/7540`.
- `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint.

## Remaining Risk

This narrows typed activation prompt filtering for generic temporary resources. It does not close broader P0/P1, command/recovery/random determinism, full PaymentEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.

Project remains **NOT READY**.
