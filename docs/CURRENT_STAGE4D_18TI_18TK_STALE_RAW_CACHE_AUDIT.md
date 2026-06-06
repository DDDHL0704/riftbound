# Stage 4D-18TI/18TJ/18TK Stale Raw Cache Audit

Date: 2026-06-06 20:28 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TI`: `BlueSentinelResourceSkillTests.BlueSentinelStalePromptScopedPayCostReplayAfterWindowClosesUsesRejectedCache`
- `18TJ`: `CrimsonRoseActivatedAbilityTests.CrimsonRoseEnemySpellshieldStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`
- `18TK`: `ArmedAssaulterHasteTemperedTests.HasteReadyTemperedPlayCardStalePromptReplayUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18TI` source `748cf8bb` cherry-picked as `5df7df77`: covers Blue Sentinel delayed resource `PAY_COST` stale prompt replay/cache/conflict behavior after the next-main payment window closes.
- `18TJ` source `cfa4fb12` cherry-picked as `43d3864d`: covers Crimson Rose enemy spellshield-target `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18TK` source `6aab5068` cherry-picked as `ed279ca6`: covers Armed Assaulter haste-ready plus Tempered `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.

## Coverage Added

- First uncached stale prompt-scoped raw rejections record one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, payment, stack, prompt projection or journal drift as applicable.
- Existing valid-path assertions remain in place for Blue Sentinel delayed payment resource use, Crimson Rose spellshield-tax activation, and Armed Assaulter haste/Tempered optional-cost play.

## Validation

- Focused changed tests: `70/70`
- First adjacent server filter: `3732/3732`
- Broader adjacent server filter: `4592/4592`
- Backend full via tracked `Riftbound.slnx`: `7419/7419`
- Mechanical checks: `git diff --check`, `git diff f022fff5..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 20:28 CST.

## Remaining Open

This narrows rejected stale raw cache semantics for Blue Sentinel delayed `PAY_COST`, Crimson Rose `ACTIVATE_ABILITY`, and Armed Assaulter haste/Tempered `PLAY_CARD` replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
