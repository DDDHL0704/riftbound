# Stage 4D-18TV/18TW/18TX Stale Raw Cache Audit

Date: 2026-06-06 21:56 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TV`: `ReksaiHasteReadyRedPaymentTests.RecycleRedRuneHasteReadyPlayCardStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TW`: `JaxTemperedOptionalAttachTests.JaxTemperedOptionalAttachPlayCardStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TX`: `AzirSwiftSwapActivatedAbilityTests.AzirSwiftSwapStalePromptReplayUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

`18TU` is intentionally not listed as accepted output. That worker self-reported a cwd mistake, produced no accepted source commit, was closed by A_MAIN, and the Azir slice was rerun in the corrected `18TX` worktree.

## Integrated Commits

- `18TV` source `12b4a8ad` cherry-picked as `7e365a2b`: covers RekSai recycle-red-rune haste-ready `PLAY_CARD` stale prompt replay/cache/conflict behavior after optional payment and stack creation.
- `18TW` source `06390a2c` cherry-picked as `6a4c4afa`: covers Jax Tempered optional-attach `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18TX` source `4635329e` cherry-picked as `ec6af641`: covers Azir Swift Swap `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after stack priority starts.

## Coverage Added

- First uncached stale prompt-scoped raw rejections record one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, resource, equipment, session projection or journal drift as applicable.
- Existing valid-path assertions remain in place for RekSai haste-ready red payment and rune recycling, Jax Tempered optional attachment, and Azir Swift Swap activation.

## Validation

- Focused changed tests: `71/71`
- First adjacent server filter: `714/714`
- Broader adjacent server filter: `4798/4798`
- Backend full via tracked `Riftbound.slnx`: `7436/7436`
- Mechanical checks: `git diff --check`, `git diff 66d7f142..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 21:56 CST.
- Real DB-backed Postgres smoke remains open because `ConnectionStrings__Riftbound` was unset in this environment.

## Remaining Open

This narrows rejected stale raw cache semantics for RekSai, Jax and Azir play/activation replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
