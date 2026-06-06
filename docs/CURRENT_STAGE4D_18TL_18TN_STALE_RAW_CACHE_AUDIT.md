# Stage 4D-18TL/18TM/18TN Stale Raw Cache Audit

Date: 2026-06-06 20:53 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18TL`: `TemperedEquipmentOptionalAttachTests.TemperedOptionalAttachPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation`
- `18TM`: `ResourceConversionEquipmentResourceSkillTests.AncientSteleResourceConversionStalePromptReplayUsesRejectedCacheWithoutMutation`
- `18TN`: `JhinMovementResourceSkillTests.JhinMovementResourceSkillStalePromptReplayUsesRejectedCacheWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18TL` source `4b9e4b7a` cherry-picked as `0e6ac92e`: covers simple Tempered optional-attach `PLAY_CARD` stale prompt replay/cache/conflict behavior after stack priority starts.
- `18TM` source `8e8e8085` cherry-picked as `27e4435e`: covers Ancient Stele resource-conversion `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after a temporary generic payment resource ledger is created.
- `18TN` source `28c3de9c` cherry-picked as `23e53a06`: covers Jhin movement-triggered resource-skill `ACTIVATE_ABILITY` stale prompt replay/cache/conflict behavior after the movement trigger resolves into mana plus a temporary payment resource.

## Coverage Added

- First uncached stale prompt-scoped raw rejections record one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, resource, session projection or journal drift as applicable.
- Existing valid-path assertions remain in place for simple Tempered optional attachment, Ancient Stele mana-to-generic resource conversion, and Jhin movement-triggered resource generation.

## Validation

- Focused changed tests: `51/51`
- First adjacent server filter: `3802/3802`
- Broader adjacent server filter: `6489/6489`
- Backend full via tracked `Riftbound.slnx`: `7422/7422`
- Mechanical checks: `git diff --check`, `git diff 61bde199..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 20:53 CST.

## Remaining Open

This narrows rejected stale raw cache semantics for simple Tempered `PLAY_CARD`, Ancient Stele `ACTIVATE_ABILITY`, and Jhin movement-resource `ACTIVATE_ABILITY` replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
