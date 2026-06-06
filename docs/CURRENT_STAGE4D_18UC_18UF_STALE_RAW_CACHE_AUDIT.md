# Stage 4D-18UC/18UD/18UE/18UF Stale Raw Cache Audit

Date: 2026-06-06 22:55 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates four parallel server-test slices:

- `18UC`: `BattleOrFlightMoveToBaseTests.BattleOrFlightPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`
- `18UD`: `IsolateMoveToBaseGuardTests.IsolatePlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`
- `18UE`: `GustReturnToHandTests.GustPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`
- `18UF`: `ReprimandReturnToHandGuardTests.ReprimandPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18UC` source `48844589` cherry-picked as `a4be78bf`: covers Battle or Flight battlefield-unit move-to-base `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18UD` source `9ca76732` cherry-picked as `bf43aca2`: covers Isolate enemy battlefield-unit move-to-base `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18UE` source `206f4c39` cherry-picked as `0460b901`: covers Gust public small battlefield-unit return-to-hand `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18UF` source `e33f2ffa` cherry-picked as `9ad2d48b`: covers Reprimand public battlefield-unit return-to-hand `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.

## Coverage Added

- First uncached stale prompt-scoped raw rejections after stack priority starts record one rejected journal entry with preserved raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, target-zone, hand/base/battlefield, card-object or journal drift as applicable.
- Existing valid-path assertions remain in place for Battle or Flight and Isolate move-to-base resolution, plus Gust and Reprimand return-to-hand resolution.

## Validation

- Focused changed tests: `30/30`
- First adjacent server filter: `1276/1276`
- Broader adjacent server filter: `5202/5202`
- Backend full via tracked `Riftbound.slnx`: `7443/7443`
- Mechanical checks: `git diff --check`, `git diff 5c746bd6..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 22:55 CST.
- Real DB-backed Postgres smoke remains open because `ConnectionStrings__Riftbound` was unset in this environment.

## Remaining Open

This narrows rejected stale raw cache semantics for Battle or Flight, Isolate, Gust and Reprimand play-card replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
