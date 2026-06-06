# Stage 4D-18TF/18TG/18TH Stale Raw Cache Audit

Date: 2026-06-06 20:06 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TF`: `GatekeeperMaduliActivatedAbilityTests.MaduliActivationStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`
- `18TG`: `ShadowActivatedAbilityTests.ShadowBattleResponseActivationStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation`
- `18TH`: `SfdSigilResourceSkillTests.SfdSigilReactionResourceSkillStalePromptReplayAfterTypedTemporaryLedgerUsesRejectedCache`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18TF` source `c8af9aa2` cherry-picked as `ebd0ee4c`: covers Gatekeeper Maduli `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18TG` source `d5c35996` cherry-picked as `950674ad`: covers Shadow battle-response `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18TH` source `a51fc235` cherry-picked as `3fa5fa23`: covers SFD Sigil typed resource-skill `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after typed temporary ledger creation across the remaining SFD Sigil profiles.

## Coverage Added

- First uncached stale prompt-scoped raw rejection records one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack/resource state or journal drift as applicable.
- Existing valid-path assertions remain in place for matching Maduli activation, Shadow battle-response activation and SFD Sigil typed temporary resource creation.

## Validation

- Focused changed tests: `7/7`
- Touched class filter: `101/101`
- Broader adjacent server filter: `5476/5476`
- Backend full via tracked `Riftbound.slnx`: `7416/7416`
- Mechanical checks: `git diff --check`, `git diff 97585178..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 20:06 CST.

## Remaining Open

This narrows rejected stale raw cache semantics for Gatekeeper Maduli `ACTIVATE_ABILITY`, Shadow battle-response `ACTIVATE_ABILITY`, and SFD Sigil typed resource-skill `ACTIVATE_ABILITY` replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
