# 4D-03MX-E..4D-03NB-E Payment-Cost Bundle Candidate

Date: 2026-05-21

Owner lane: `DOC_MATRIX_CURRENT`

Worktree: `/Users/dinghaolin/MyProjects/riftbound-dotnet-stage4d-matrix-docs-current`

Branch: `codex/stage4d-matrix-docs-current-20260521`

## Scope

This bundle uses the A_MAIN 14:23 shared-board lock for one small post-03MW matrix/audit-baseline synchronization batch. The write scope is limited to current matrix/checkpoint/audit coordination docs, `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, and `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` residual count/current-slice baseline synchronization.

No runtime, frontend, official catalog, protocol core field, Chrome/browser/formal E2E script, unrelated test, `fullOfficial`, READY flag or `riftbound-dotnet.sln` change is included.

## Selected Rows

| Slice | functionalUnit | Card | Effect | Evidence basis | Matrix transition |
| --- | --- | --- | --- | --- | --- |
| 4D-03MX-E | `FU-a78407b08e` | `SFD·100/221` 约德尔探险家 | `YORDLE_EXPLORER_RUNE_COST_DRAW_PLAY_UNIT` | `CardBehaviorRegistry`; `p2-preflight-play-yordle-explorer-rune-cost-static`; `p4-play-yordle-explorer-target-rejected`; `rules-evidence-index.md`; no FAQ dependency in matrix | `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED` |
| 4D-03MY-E | `FU-c9781c5b92` | `SFD·101/221` 仙灵龙 | `FAERIE_DRAGON_PLAY_UNIT_GRANT_UP_TO_FOUR_BOONS` | `CardBehaviorRegistry`; `p2-preflight-play-faerie-dragon-grant-four-boons`; Faerie Dragon direct target validation tests; `rules-evidence-index.md`; no FAQ dependency in matrix | `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED` |
| 4D-03MZ-E | `FU-467f4c3cf4` | `SFD·102/221` 海克斯饮魔刀 | `HEXDRINKER_PLAY_EQUIPMENT` | `CardBehaviorRegistry`; `p2-preflight-play-hexdrinker-equipment`; `p4-play-hexdrinker-target-rejected`; `P4AssembleEquipmentCommandAttachesHexdrinkerWithOrangeCost`; `rules-evidence-index.md`; no FAQ dependency in matrix | `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED` |
| 4D-03NA-E | `FU-a53f864324` | `SFD·103/221` 琢珥鱼 | `XERSAI_FISH_PLAY_UNIT_NO_OPTIONAL_HASTE` | `CardBehaviorRegistry`; `p2-preflight-play-xersai-fish-no-optional-haste`; `p4-play-xersai-fish-haste-ready`; `rules-evidence-index.md`; no FAQ dependency in matrix | `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED` |
| 4D-03NB-E | `FU-d65987cbb3` | `SFD·104/221` 禁魔石丰碑 | `PETRICITE_MONUMENT_PLAY_EQUIPMENT_EPHEMERAL` | `CardBehaviorRegistry`; `p2-preflight-play-petricite-monument-equipment-ephemeral`; `p4-play-petricite-monument-target-rejected`; `rules-evidence-index.md`; no FAQ dependency in matrix | `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED` |

## Count Impact

Starting continuity from 03MW:

- all functionalUnits `NEEDS_ENGINE_SUPPORT 563 -> 558`
- payment-cost `NEEDS_ENGINE_SUPPORT 165 -> 160`
- primary residual `124 -> 119`
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT 290 -> 288`
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT 216 -> 215`
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT 177 -> 176`
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT 352 -> 347`
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT 103 -> 101`
- `NEEDS_AUTOMATED_TEST_EVIDENCE` remains `328`
- `NEEDS_FAQ_REVIEW` remains `92`
- primary FAQ residual remains `61`
- `fullOfficialTrue` remains `0`
- `ready` remains `false`

## Closed Blockers

Closed for the selected rows only:

- selected functionalUnit and matching snapshot entry `stage4B.freezeStatus`: `NEEDS_ENGINE_SUPPORT` -> `IMPLEMENTED_UNTESTED`
- selected functionalUnit `stage4B.fullOfficialBlockers`: remove only `NEEDS_ENGINE_SUPPORT`
- selected functionalUnit/snapshot `stage4B.statusFlags`: remove only `NEEDS_ENGINE_SUPPORT`

## Still Open

The bundle intentionally does not close these blockers:

- Yordle Explorer / Faerie Dragon / Hexdrinker / Xersai Fish / Petricite Monument automated evidence disposition
- rune-cost draw, boon/layer, equipment attach-follow, `HASTE_READY` exact optional-cost, ephemeral cleanup / replacement-duration, hidden-info, FEPR target-stack and complete PaymentEngine / PAY_COST breadth
- `NEEDS_AUTOMATED_TEST_EVIDENCE`
- `NEEDS_FAQ_REVIEW`
- `fullOfficial`
- E_CARD_MATRIX_READINESS
- card matrix
- READY

## Why Not READY

This is a row-level blocker-count synchronization only. It proves five selected rows no longer need the `NEEDS_ENGINE_SUPPORT` blocker based on existing runtime/fixture/rules-evidence, but it does not prove full automated evidence disposition, FAQ closure, complete PaymentEngine / PAY_COST official matrix closure, frontend readiness, formal E2E, `fullOfficial=true` or final Stage 4 readiness.

Project status remains **NOT READY**.

## Development Handoff

Development windows still own the real code gaps for full closure:

- complete PaymentEngine / PAY_COST official breadth
- complete FEPR target / stack lifecycle breadth
- hidden-info/random-zone breadth
- layer/continuous-effect breadth
- cleanup/replacement-duration breadth
- exact optional-cost and equipment attach/follow edge cases

## Validation Plan

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`
- focused selected evidence filter covering Yordle Explorer / Faerie Dragon / Hexdrinker / Xersai Fish / Petricite Monument
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`

Validation passed on the DOC_MATRIX branch:

- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- `git diff --check`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineCoverageAuditTests"`: passed 660/660
- `source scripts/dev-env.sh && dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~YordleExplorer|FullyQualifiedName~FaerieDragon|FullyQualifiedName~Hexdrinker|FullyQualifiedName~XersaiFish|FullyQualifiedName~PetriciteMonument"`: passed 12/12
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed 5236/5236

Chrome smoke / frontend build were not run because this bundle has no frontend, browser script, Chrome/formal E2E or runtime changes.
