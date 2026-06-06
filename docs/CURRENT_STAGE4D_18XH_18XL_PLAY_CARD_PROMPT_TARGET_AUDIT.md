# Stage 4D 18XH-18XL Play-Card Prompt Target Audit

Date: 2026-06-07 05:34 CST

Status: accepted on `main`. Project remains **NOT READY**.

## Scope

A_MAIN integrated one runtime prompt-target fix plus five parallel server-test slices:

- 18XH Hostile Takeover prompt target filtering: `4badf6ac` -> `e57bccac`.
- 18XI Reprimand prompt target filtering: `749d988e` -> `a06dbe33`.
- 18XJ Megashark Cannon prompt target filtering: `4c47588a` -> `3e4975b0`.
- 18XK First Mate any-unit prompt target filtering: `248d9ee6` -> `bd45ca37`.
- 18XL Charm prompt target filtering: `f50d44bf` -> `a75e3988`.
- A_MAIN runtime fix: `6485e682` filters `PLAY_CARD` prompt target choices through effect-specific target semantics, required/forbidden target tags, and zone-player control consistency.

## Runtime Finding

18XI and 18XK intentionally started as patch-only tests. Focused worktree validation against the old runtime exposed two prompt gaps:

- Reprimand top-level `PLAY_CARD` targets listed non-unit battlefield objects even though command revalidation rejected them.
- First Mate `ANY_UNIT` prompt targets listed a dirty battlefield object whose zone player did not control the object.

A_MAIN fixed `ActionPromptBuilder` so prompt target choices align with the command-side target semantics and zone-control guardrails before accepting the tests.

## Validation

- Pre-dispatch target-class baseline on main: `54/54`.
- Worktree focused validation before runtime fix: Hostile Takeover `13/13`, Megashark `14/14`, Charm `10/10`; Reprimand and First Mate exposed the runtime prompt gaps above.
- Main changed-class filter after runtime fix and all five test slices: `59/59`.
- Adjacent target/guard filter: `164/164`.
- Backend full via explicit conformance test project under the current no-DB environment: `7545/7545`.
- `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed before checkpoint.

## Open Locks

Frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, final readiness status, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, matrix JSON behavior changes and `riftbound-dotnet.sln` remain locked. Project remains **NOT READY**.
