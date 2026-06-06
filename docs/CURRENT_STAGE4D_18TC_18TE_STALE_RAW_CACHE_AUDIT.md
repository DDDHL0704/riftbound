# Stage 4D-18TC/18TD/18TE Stale Raw Cache Audit

Date: 2026-06-06 19:45 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TC`: `GameHubJoinTests.SubmitIntentRejectsStalePromptStamp` and `GameHubJoinTests.SubmitIntentRejectsStaleSnapshotTickWithMatchingPromptId`
- `18TD`: `UndercoverAgentTriggerTests.UndercoverAgentHandChoiceRejectsInvalidCommandsWithoutMutation`
- `18TE`: `OfficialOpeningTests.WrongPlayerFirstReadyBothDecksPromptAfterFinalReadyRejectsWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18TC` source `d0c0d118` cherry-picked as `936ff91d`: covers GameHub `PLAY_CARD` stale prompt-stamp and snapshot-tick rejected journal/cache/conflict behavior at the protocol boundary.
- `18TD` source `0dd0f19d` cherry-picked as `53fccbb4`: covers Undercover Agent `CHOOSE_HAND_CARDS` stale snapshot replay/cache/conflict behavior after the hand-choice window closes.
- `18TE` source `38c49b6f` cherry-picked as `474884d9`: covers official wrong-player first `READY` replay/cache/conflict behavior after final ready starts mulligan.

## Coverage Added

- First uncached stale prompt-scoped raw rejection records one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, session projection, caller/group broadcast or journal drift as applicable.
- Existing valid-path assertions remain in place for matching GameHub `PLAY_CARD`, Undercover Agent hand-choice and official opening prompt progression.

## Validation

- Focused changed tests: `4/4`
- Touched class filter: `791/791`
- Broader adjacent server filter: `5438/5438`
- Backend full via tracked `Riftbound.slnx`: `7409/7409`
- Mechanical checks: `git diff --check`, `git diff bcfccd96..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 19:45 CST.

## Remaining Open

This narrows rejected stale raw cache semantics for GameHub `PLAY_CARD`, Undercover Agent `CHOOSE_HAND_CARDS`, and official wrong-player `READY` replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
