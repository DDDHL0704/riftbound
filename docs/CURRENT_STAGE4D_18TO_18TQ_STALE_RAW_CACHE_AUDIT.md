# Stage 4D-18TO/18TP/18TQ Stale Raw Cache Audit

Date: 2026-06-06 21:09 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TO`: `GoldTokenResourceSkillTests.GoldTokenResourceSkillStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TP`: `MalzaharResourceSkillTests.MalzaharResourceSkillStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TQ`: `RageSigilResourceSkillTests.RageSigilResourceSkillStalePromptReplayUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18TO` source `945ba438` cherry-picked as `d814a060`: covers Gold Token `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after the token is destroyed and a generic temporary payment ledger is created.
- `18TP` source `2adc76e2` cherry-picked as `3c01906d`: covers Malzahar `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after a friendly unit cost is destroyed and a payment-only temporary ledger is created.
- `18TQ` source `44bb3ccf` cherry-picked as `15463475`: covers Rage Sigil typed reaction-resource `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after the red typed temporary ledger is created.

## Coverage Added

- First uncached stale prompt-scoped raw rejections record one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, temporary-resource, stack, session projection or journal drift as applicable.
- Existing valid-path assertions remain in place for Gold Token source destruction, Malzahar cost destruction, and Rage Sigil typed red temporary-payment resource creation.

## Validation

- Focused changed tests: `80/80`
- First adjacent server filter: `3752/3752`
- Broader adjacent server filter: `6439/6439`
- Backend full via tracked `Riftbound.slnx`: `7425/7425`
- Mechanical checks: `git diff --check`, `git diff 67ea29b4..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 21:09 CST.

## Remaining Open

This narrows rejected stale raw cache semantics for Gold Token, Malzahar, and Rage Sigil resource-skill `ACTIVATE_ABILITY` replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
