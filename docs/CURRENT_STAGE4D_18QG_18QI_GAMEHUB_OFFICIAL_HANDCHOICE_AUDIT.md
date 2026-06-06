# Stage 4D 18QG-18QI GameHub Official Hand Choice Audit

Date: 2026-06-06 13:33 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QG added `GameHubJoinTests.ChooseHandCardsAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18QH added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshOrderTriggersAfterMatchFinishedThrowsStableErrorWithoutMutation`.
- 18QI added `UndercoverAgentTriggerTests.UndercoverAgentHandChoiceStaleRawPromptAfterWindowClosesRecordsRejectedJournalWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `CHOOSE_HAND_CARDS` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug, hand-choice and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Official session coverage now proves a fresh `ORDER_TRIGGERS` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.
- Undercover Agent hand-choice coverage now proves a stale prompt-scoped raw `CHOOSE_HAND_CARDS` replay after the hand-choice window has closed records a rejected journal entry with preserved raw prompt fields, returns `PromptExpired`, emits no events, removes hand-choice actions from prompts, and preserves state, prompts and snapshots.

## Source Commits

- 18QG worker source `d4eff298d0885817e705f17d6114498cff1bf1a1`, cherry-picked to main as `2f78df25`.
- 18QH worker source `3ce342a44696bc7a2007f6bca5938878c8be47e1`, cherry-picked to main as `79c9c9ad`.
- 18QI worker source `824817b498ecd6529680c733cf9ba1558bed8a25`, cherry-picked to main as `120fea10`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `789/789`.
- Broader adjacent server filter: `5547/5547`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7390/7390`.
- `git diff --check`: passed.
- `git diff 316ff5bd..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 13:33 CST.

## Remaining Open

This narrows GameHub finished-session `CHOOSE_HAND_CARDS` redaction, official finished-session fresh `ORDER_TRIGGERS` rejection, and hand-choice stale raw rejected-journal coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
