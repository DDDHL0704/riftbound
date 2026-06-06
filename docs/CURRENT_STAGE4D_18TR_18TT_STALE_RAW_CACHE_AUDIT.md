# Stage 4D-18TR/18TS/18TT Stale Raw Cache Audit

Date: 2026-06-06 21:29 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TR`: `OgnSigilResourceSkillTests.OgnSigilReactionResourceSkillStalePromptReplayAfterTypedTemporaryLedgerUsesRejectedCache`
- `18TS`: `LuxResourceSkillTests.LuxSpellOnlyResourcePlayCardStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TT`: `RenataActivatedAbilityTests.RenataDrawStalePromptReplayUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18TR` source `2f645d83` cherry-picked as `4dbc1c3d`: covers OGN Sigil typed reaction-resource `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after typed temporary ledger creation.
- `18TS` source `21d79446` cherry-picked as `81d5b64b`: covers Lux spell-only resource `PLAY_CARD` stale prompt replay/cache/conflict behavior after optional resource payment, stack creation and temporary-resource cleanup.
- `18TT` source `a21ddaf5` cherry-picked as `1dbec825`: covers Renata typed-blue draw `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after stack creation without source exhaustion.

## Coverage Added

- First uncached stale prompt-scoped raw rejections record one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, resource, session projection or journal drift as applicable.
- Existing valid-path assertions remain in place for OGN typed temporary-payment resource creation, Lux spell-only resource optional payment cleanup, and Renata typed-blue draw stack creation.

## Validation

- Focused changed tests: `88/88`
- First adjacent server filter: `693/693`
- Broader adjacent server filter: `4760/4760`
- Backend full via tracked `Riftbound.slnx`: `7433/7433`
- Mechanical checks: `git diff --check`, `git diff 3d76d3cc..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 21:29 CST.

## Remaining Open

This narrows rejected stale raw cache semantics for OGN Sigil, Lux and Renata resource/activation replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
