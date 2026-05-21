# 4D-03KA-E Card Matrix Readiness Payment-Cost Bright Future Top-Five Unit FAQ/Hidden/Control Targeting-Stack Blocker Closure Candidate

## Scope

This candidate records one E_CARD_MATRIX_READINESS row-level blocker reduction for `FU-0df86523aa / OGN·115/298 / 光明未来 / BRIGHT_FUTURE_PLAY_EACH_PLAYER_TOP_FIVE_UNIT`.

It is a matrix, evidence and audit sync only. It does not change runtime, frontend, Chrome / browser scripts, protocol core fields, official catalog, fullOfficial status, final readiness status, or `riftbound-dotnet.sln`.

## Evidence

- `ConformanceFixtureRunnerTests` proves the current Bright Future representative path: P1 pays 5, each player selects a unit from their own main-deck top five, unselected cards are recycled, and selected units are played starting with the next player.
- Direct Bright Future regression tests reject non-unit selections, duplicate player selections, selections outside top five, and dirty control states where a card in P2's top-five window is still controlled by P1.
- `p2-preflight-play-bright-future-play-each-player-top-five-unit.fixture.json` provides the representative fixture and effect-kind trace.
- `docs/rules-evidence-index.md`, `docs/p2-rules-preflight.md`, `docs/CURRENT_P2_STATUS.md`, `docs/CURRENT_FRONTEND_REBUILD_PLAN.md` and `docs/CURRENT_SERVER_RULE_AUDIT.md` record the accepted representative evidence and deferred official breadth.
- `CardBehaviorRegistry` binds the selected functional unit to `BRIGHT_FUTURE_PLAY_EACH_PLAYER_TOP_FIVE_UNIT`.

## Matrix Impact

- `NEEDS_ENGINE_SUPPORT`: 240 -> 239.
- Primary residual: 162 -> 162 because this row remains `NEEDS_FAQ_REVIEW`.
- `payment-or-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 428 -> 427.
- `payment-and-targeting-stack-timing` `NEEDS_ENGINE_SUPPORT`: 149 -> 148.
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 -> 328.
- `NEEDS_FAQ_REVIEW`: 92 -> 92.
- Primary FAQ residual: 61 -> 61.
- `fullOfficialTrue`: 0 -> 0.
- `ready`: false -> false.

This candidate does not close Bright Future FAQ adjudication, automated evidence disposition, hidden-info / main-deck look-window visibility, rune-cost free-play, control-zone movement / play-to-base breadth, layer / continuous-effect breadth, complete FEPR target / stack lifecycle matrix, complete PaymentEngine / PAY_COST matrix, formal 18-step E2E, or READY.

## Validation Results

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed; BrightFuture/BRIGHT_FUTURE/TopFive/ConformanceFixtureRunnerTests focused regression 3023/3023 passed; ActionPrompt/Prompt/PaymentResource/SpendPower/RunePool/BrightFuture/TopFive/Stack adjacent regression 653/653 passed; PaymentEngineCoverageAuditTests 540/540 passed; backend full `dotnet test Riftbound.slnx --no-restore` 5111/5111 passed; `git diff --check` passed after final doc write.
