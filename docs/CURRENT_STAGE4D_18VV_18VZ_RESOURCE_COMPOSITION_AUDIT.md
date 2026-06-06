# Stage 4D-18VV/18VW/18VX/18VY/18VZ Resource Composition Audit

Date: 2026-06-07

Status: partially accepted into A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN reviewed five parallel resource-composition shards covering Malzahar, Gold Token, Rage Sigil, Ogn Sigil and Honeyfruit temporary-resource payment behavior.

Runtime changed: no. Test coverage only.

## Accepted Commits

- 18VZ Honeyfruit: worker commit `fc55706250be7e89004ca4b5bec2d8bd5d78daaf` accepted into main as `ccfe61d0`, adding `HoneyfruitLevelSixGeneratedManaAndTemporaryPowerCombineWithRunePoolForLargerMixedCost` in `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs`.
- 18VY Ogn Sigil: worker commit `dc2ea04fcb2f1b51143822ec7e7420494b83749c` accepted into main as `73a2dbf8`, adding `OgnSigilTemporaryTypedResourceCombinesWithMatchingRunePoolForLargerTypedCost` in `tests/Riftbound.ConformanceTests/OgnSigilResourceSkillTests.cs`.
- 18VX Rage Sigil: worker commit `f0c99bbc5041c1d1c14ccf39acb09854c5862385` accepted into main as `ad30e3de`, adding `RageSigilTemporaryRedResourceCombinesWithRunePoolForLargerTypedRuneCost` in `tests/Riftbound.ConformanceTests/RageSigilResourceSkillTests.cs`.

## Rejected Shards

- 18VW Gold Token: worker commit `7795749ecb82b40d9693a8fc20ca7e53f9b15370` was cherry-picked as `a9c46ed8`, then rejected by A_MAIN focused validation and reverted as `95c2b2ce`. The new test expected two pending `paymentResourceChoices`, but main produced an empty choice list for the simultaneous two-temporary-resource pending `PayCost` scenario.
- 18VV Malzahar: worker commit `eaff1fb3340bff6ca7e07062cf94f6fb939b78a2` was cherry-picked as `d68ba58a`, then rejected by A_MAIN focused validation and reverted as `03dc6784`. The new test expected two pending `paymentResourceChoices`, but main produced an empty choice list for the simultaneous two-temporary-resource pending `PayCost` scenario.

## Coordination Note

This batch kept parallel throughput across five disjoint resource-skill files, while A_MAIN enforced serial main validation before accepting each result. The two rejected shards intentionally remain in main history as add/revert audit evidence, with no net test-file diff from the rejected changes. Worker prompts for the next batch must keep the strict pre-edit `pwd`, `git status --short --branch`, and `git rev-parse HEAD` checks because prior batches showed some workers can still apply patches to the default main worktree.

## Validation

- Honeyfruit focused filter on main: `18/18`.
- Ogn Sigil focused filter on main: `38/38`.
- Rage Sigil focused filter on main: `20/20`.
- Accepted resource-composition bundle: `76/76` for `OgnSigilResourceSkillTests|RageSigilResourceSkillTests|HoneyfruitResourceSkillTests`.
- Five-file resource filter after rejecting and reverting Gold Token and Malzahar: `137/137` for `MalzaharResourceSkillTests|GoldTokenResourceSkillTests|OgnSigilResourceSkillTests|RageSigilResourceSkillTests|HoneyfruitResourceSkillTests`.
- Adjacent resource/payment/server filter: `5610/5610` for resource skills, payment engine, conformance fixture, GameHub, official opening and recovery coverage.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7492/7492`.
- `git diff --check e8c96a69 HEAD` passed before docs sync.

## Remaining Risk

This narrows Honeyfruit, Ogn Sigil and Rage Sigil resource-composition coverage only. It does not add runtime support for Malzahar or Gold Token simultaneous two-temporary-resource pending payment choices, and it does not close P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, DOC_MATRIX future scope or final readiness.
