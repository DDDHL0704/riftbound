# Stage 4D-18WF/18WG/18WH/18WI/18WJ Resource and Task Boundary Audit

Date: 2026-06-07

Status: partially accepted into A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN accepted four effective server-test slices from the 18WF-18WJ parallel batch, covering legend resource bridge payment replay, resource-conversion payload rejection, Dragon Soul Sage generated-mana payment, and board task queue waiting-prompt redaction. 18WF produced no accepted Blue Sentinel patch after a cwd mistake was cleaned up by the worker.

Runtime changed: no. Test coverage only.

## Accepted Commits

- 18WI LegendResourceBridge: worker accidentally patched main instead of its assigned worktree. A_MAIN reviewed the main patch, narrowed two over-broad `PAYMENT_WINDOW_CLOSED` payload assertions to the current event contract, validated it, and accepted it as A_MAIN commit `1b470c18`, adding `LegendResourceBridgeGeneratedResourcePaysMatchingCostOnceThenRejectsReplayWithoutMutation` in `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs`.
- 18WH ResourceConversion: worker commit `8ceef4febb32f3c9854084cad609f24d0de77e10` accepted into main as `3fcd09b9`, adding `HextechAnomalyRejectsTemporaryPaymentResourcePayloadWithoutBoundaryMutation` in `tests/Riftbound.ConformanceTests/ResourceConversionEquipmentSkillTests.cs`.
- 18WG Dragon Soul Sage: worker commit `ae00c60bf466023c4453f77bf91a46dc03216a95` accepted into main as `29aa1247`, adding `DragonSoulSageGeneratedManaPaysLaterManaCostAndSecondPayCostRejectsWithoutMutation` in `tests/Riftbound.ConformanceTests/ReactionResourceSkillTests.cs`.
- 18WJ BoardTaskQueue: worker commit `ac170c5ea7422b493afe828691f193d39542bd8b` accepted into main as `28352fd6`, adding `StateBasedCleanupWaitingPromptsHideOrdinaryActionsButKeepQueueAuditSnapshots` in `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`.

## Excluded Shard

- 18WF Blue Sentinel: worker reported a cwd mistake that briefly patched `/Users/dinghaolin/IdeaProjects/riftbound/tests/Riftbound.ConformanceTests/BlueSentinelResourceSkillTests.cs`, then removed its own block before A_MAIN integration. The assigned 18WF worktree stayed at `e37402ff` with no committed source patch, so A_MAIN excluded this shard from the accepted bundle.

## Coordination Note

This batch preserved parallel throughput for four accepted slices but repeated the cwd failure mode seen in earlier batches. Future worker prompts must keep strict pre-edit `pwd`, `git status --short --branch`, and `git rev-parse HEAD` checks, and A_MAIN must continue checking main status before cherry-picks. 18WI was accepted only after A_MAIN validated and narrowed the accidental main patch; 18WF was excluded because no validated source patch remained.

## Validation

- 18WI focused LegendResourceBridge filter on main after A_MAIN assertion narrowing: `117/117`.
- 18WH focused ResourceConversionEquipment filter on main: `24/24`.
- 18WG focused ReactionResource filter on main: `17/17`.
- 18WJ focused BoardTaskQueue filter on main: `14/14`.
- Combined effective changed-test filter: `172/172` for `LegendResourceBridgeVerifierTests|ResourceConversionEquipmentResourceSkillTests|ReactionResourceSkillTests|BoardTaskQueueFoundationTests`.
- Adjacent/broader server filter: `5555/5555` for legend bridge, resource conversion, reaction resource, board task queue, Blue Sentinel, PaymentEngine, conformance fixture shape/runner, GameHub, recovery and official opening coverage.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7517/7517`.
- `git diff --check e37402ff HEAD` passed before docs sync; conflict-marker scan over `docs src tests` had no matches; matrix JSON parse passed.

## Remaining Risk

This narrows selected resource-payment replay and board-task queue prompt/snapshot boundary coverage only. It does not add the intended Blue Sentinel boundary test from 18WF, and it does not close P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, DOC_MATRIX future scope or final readiness.
