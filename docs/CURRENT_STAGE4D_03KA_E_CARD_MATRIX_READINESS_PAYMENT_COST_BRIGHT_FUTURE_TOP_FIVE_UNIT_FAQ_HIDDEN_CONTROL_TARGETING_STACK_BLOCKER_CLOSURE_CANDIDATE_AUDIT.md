# 4D-03KA-E Audit: Payment-Cost Bright Future Top-Five Unit FAQ/Hidden/Control Targeting-Stack Blocker Closure Candidate

## Decision

`FU-0df86523aa / OGN·115/298 / 光明未来` may lose the row-level `NEEDS_ENGINE_SUPPORT` blocker for the payment-cost matrix lane because existing runtime and automated evidence already prove the narrow representative top-five unit path and invalid target guards for `BRIGHT_FUTURE_PLAY_EACH_PLAYER_TOP_FIVE_UNIT`.

This is not a full-official closure. The row remains `NEEDS_FAQ_REVIEW` and still keeps `NEEDS_FAQ_REVIEW` plus `NEEDS_AUTOMATED_TEST_EVIDENCE` as full official blockers.

## Accepted Evidence

- `ConformanceFixtureRunnerTests` covers the P2 fixture through pay-cost, each-player top-five unit selection, recycle-remainder resolution and next-player play order.
- Direct Bright Future tests reject non-unit selections, duplicate player selections, top-five-outside selections and dirty deck-player control mismatches.
- `p2-preflight-play-bright-future-play-each-player-top-five-unit.fixture.json` proves the current representative path.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` and `docs/CURRENT_SERVER_RULE_AUDIT.md` record the evidence and deferred official breadth.
- `src/Riftbound.Engine/CardBehaviorRegistry.cs` binds the selected OGN card number to the selected effect kind.

## Write Locks

Runtime, frontend, Chrome / browser scripts, formal 18-step scripts, protocol core fields, official catalog, fullOfficial status, final readiness status and `riftbound-dotnet.sln` remain locked.

## Non-Closure

Chrome smoke is not required for this candidate unless frontend or browser-script files change.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; BrightFuture/BRIGHT_FUTURE/TopFive/ConformanceFixtureRunnerTests focused regression 3023/3023 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/BrightFuture/TopFive/Stack adjacent regression 653/653 passed; PaymentEngineCoverageAuditTests 540/540 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5111/5111 passed; `git diff --check` passed after final doc write.

## Remaining Blockers

Payment-cost blocker closure remains partially open. `B/D_ENGINE_SUPPORT`, `A_CONFORMANCE_AUTOMATED_TEST_EVIDENCE`, `E_CARD_MATRIX_FAQ_REVIEW`, P0-005, P0-004 adjacency audit-sensitive, P1, Bright Future FAQ adjudication, automated evidence disposition, hidden-info / main-deck look-window visibility breadth, rune-cost free-play branch, control-zone movement / play-to-base breadth, layer / continuous-effect breadth, full official PaymentEngine matrix closure, formal 18-step E2E, card matrix readiness, and READY all remain open.
