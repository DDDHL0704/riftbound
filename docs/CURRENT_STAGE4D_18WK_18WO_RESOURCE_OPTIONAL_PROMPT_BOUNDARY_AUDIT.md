# Stage 4D-18WK/18WL/18WM/18WN/18WO Resource Optional Prompt Boundary Audit

Date: 2026-06-07

Status: accepted into A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN accepted five server-test slices from the 18WK-18WO parallel batch, covering Blue Sentinel non-rune payment prompt suppression, Sentinel Adept Tempered attach prompt isolation, SFD Sigil typed resource mana-only prompt suppression, Jax trigger-payment prompt isolation, and Armed Assaulter haste plus Tempered optional-cost prompt ordering/isolation.

Runtime changed: no. Test coverage only.

## Accepted Commits

- 18WK Blue Sentinel: worker left no source diff in its assigned worktree, but an equivalent main-worktree patch was later found in `tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`. A_MAIN reviewed and validated it, then accepted it as A_MAIN commit `9017134e`, adding `BlueSentinelDelayedResourceDoesNotLeakPromptMetadataForManaOnlyPayment`. The duplicate A_MAIN worktree commit `7b06f721` was not cherry-picked.
- 18WL Sentinel Tempered attach: worker commit `7749b685008b0684d88db1c5f0030495ed0f7e41` accepted into main as `f7f3378e`, adding `TemperedAttachPromptChoiceIsIsolatedFromOpponentPrompt` in `tests/Riftbound.ConformanceTests/TemperedEquipmentOptionalAttachTests.cs`.
- 18WM SFD Sigil: A_MAIN fixed the worker draft's `Assert.NotNull` assignment shape, committed source worktree `7040c7cc3cfac480586a635ac3a88a05ac6e6b99`, and accepted it into main as `5ce5f0b1`, adding `SfdSigilTemporaryTypedResourceDoesNotExposeManaOnlyPromptResourceChoices` in `tests/Riftbound.ConformanceTests/SfdSigilResourceSkillTests.cs`.
- 18WN Jax Tempered payment: A_MAIN narrowed the worker draft's P2 prompt assertion to the current non-actionable prompt contract, committed source worktree `193be92d58e603bac1f9e0f5591dacf6c68dd2d8`, and accepted it into main as `07375e16`, adding `JaxTemperedWeaponAttachPaymentPromptIsIsolatedToController` in `tests/Riftbound.ConformanceTests/JaxTemperedOptionalAttachTests.cs`.
- 18WO Armed Assaulter: worker draft passed focused validation and was committed as `1100e240fbd91ccbd7703c50542fe9a3d57dd3e9`, then accepted into main as `bdf7c7e9`, adding `PlayCardPromptKeepsHasteTemperedOptionalCostOrderAndP2Isolation` in `tests/Riftbound.ConformanceTests/ArmedAssaulterHasteTemperedTests.cs`.

## Coordination Note

This batch used five simultaneous worktrees from `8c9ac515`. 18WL completed independently. 18WM, 18WN and 18WO stopped with uncommitted diffs after A_MAIN status interrupts; A_MAIN reviewed, fixed where needed, validated and committed them in their assigned worktrees before main integration. 18WK repeated the cwd/main-worktree failure mode: the assigned worktree initially stayed clean while a valid Blue Sentinel patch appeared on main. A_MAIN accepted the main patch only after focused validation and kept the duplicate 18WK branch commit out of main.

Future worker prompts must continue requiring exact `pwd`, `git status --short --branch`, and `git rev-parse HEAD` checks immediately before any `apply_patch`, plus an explicit stop if the worktree is not the assigned path.

## Validation

- Pre-integration baseline for the five target classes on main: `95/95`.
- 18WK focused BlueSentinel filter on main after accepting the main patch: `14/14`.
- 18WL focused TemperedEquipmentOptionalAttach filter in worker: `14/14`.
- 18WM focused SfdSigilResourceSkill filter after A_MAIN fix: `27/27`.
- 18WN focused JaxTemperedOptionalAttach filter after A_MAIN fix: `19/19`.
- 18WO focused ArmedAssaulterHasteTempered filter: `26/26`.
- Combined changed-class filter on main: `100/100` for `BlueSentinelResourceSkillTests|TemperedEquipmentOptionalAttachTests|SfdSigilResourceSkillTests|JaxTemperedOptionalAttachTests|ArmedAssaulterHasteTemperedTests`.
- Backend full via `/Users/dinghaolin/.dotnet/dotnet test` under the current no-DB environment: `7522/7522`.
- `git diff --check HEAD~5 HEAD` passed before docs sync.

## Remaining Risk

This narrows selected prompt metadata, optional-cost isolation, delayed-resource and typed-resource payment boundary coverage only. It does not close P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, DOC_MATRIX future scope or final readiness.
