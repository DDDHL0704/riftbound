# Stage 4D-18TY/18TZ/18UB Stale Raw Cache Audit

Date: 2026-06-06 22:34 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TY`: `AnyUnitTargetScopeGuardTests.FirstMateAnyUnitTargetScopeStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TZ`: `AgileEquipmentDirectPlayAttachTests.AgileEquipmentDirectPlayStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18UB`: `CharmMoveToBaseGuardTests.CharmPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

`18UA` is intentionally not listed as accepted output. That Battle or Flight worker produced no accepted patch and no source commit, so A_MAIN excluded it from the integrated bundle.

## Integrated Commits

- `18TY` source `05d9d707` cherry-picked as `d92ea39a`: covers First Mate any-unit target-scope `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18TZ` source `c83dcde5` cherry-picked as `7f6c9d03`: covers Agile equipment direct-play attach `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18UB` source `2c722a6f` cherry-picked as `3fe406d4`: covers Charm move-to-base `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.

## Coverage Added

- First uncached stale prompt-scoped raw rejections record one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, resource, target-zone, equipment or journal drift as applicable.
- Existing valid-path assertions remain in place for First Mate unit-ready target scope, Agile direct equipment attachment, and Charm enemy unit move-to-base resolution.

## Validation

- Focused changed tests: `35/35`
- First adjacent server filter: `1266/1266`
- Broader adjacent server filter: `5192/5192`
- Backend full via tracked `Riftbound.slnx`: `7439/7439`
- Mechanical checks: `git diff --check`, `git diff 3bc61a6c..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 22:34 CST.
- Real DB-backed Postgres smoke remains open because `ConnectionStrings__Riftbound` was unset in this environment.

## Notes

The first worker/test wave saturated or stalled the shared Roslyn/MSBuild server. A_MAIN shut down the build server, reran focused validation serially with `--no-restore -m:1 -p:UseSharedCompilation=false`, fixed worker compile/assertion issues in the source worktrees, and only then committed/cherry-picked the accepted slices.

## Remaining Open

This narrows rejected stale raw cache semantics for First Mate, Agile equipment and Charm play-card replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
