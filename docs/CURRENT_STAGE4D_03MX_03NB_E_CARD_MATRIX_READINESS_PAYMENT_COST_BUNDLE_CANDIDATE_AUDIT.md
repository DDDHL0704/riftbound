# 4D-03MX-E..4D-03NB-E Payment-Cost Bundle Candidate Audit

Date: 2026-05-21

Project status: **NOT READY**

## Audit Summary

The 4D-03MX-E..4D-03NB-E bundle is an A_MAIN-approved post-03MW matrix/audit-test baseline synchronization. It selects five payment-cost residual rows where existing server runtime registration, existing conformance evidence and existing rules-evidence-index entries justify removing the selected row's `NEEDS_ENGINE_SUPPORT` blocker. It does not claim automated-test evidence closure, FAQ review closure, `fullOfficial`, READY, runtime behavior changes or frontend behavior changes.

## Evidence Table

| Slice | Selected row | Existing implementation | Existing tests / fixtures | Rules / FAQ evidence | Audit decision |
| --- | --- | --- | --- | --- | --- |
| 4D-03MX-E | `FU-a78407b08e` / `SFD·100/221` / `YORDLE_EXPLORER_RUNE_COST_DRAW_PLAY_UNIT` | `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the effect | `p2-preflight-play-yordle-explorer-rune-cost-static.fixture.json`; `p4-play-yordle-explorer-target-rejected.fixture.json`; fixture runner coverage | `docs/p2-rules-preflight.md`; `docs/CURRENT_P4_STATUS.md`; `docs/rules-evidence-index.md`; matrix FAQ status `NO_FAQ_CANDIDATE_IN_MATRIX` | Close selected `NEEDS_ENGINE_SUPPORT`; keep automated evidence/full breadth open |
| 4D-03MY-E | `FU-c9781c5b92` / `SFD·101/221` / `FAERIE_DRAGON_PLAY_UNIT_GRANT_UP_TO_FOUR_BOONS` | `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the effect | `p2-preflight-play-faerie-dragon-grant-four-boons.fixture.json`; direct no-target/enemy-target validation | `docs/p2-rules-preflight.md`; `docs/rules-evidence-index.md`; matrix FAQ status `NO_FAQ_CANDIDATE_IN_MATRIX` | Close selected `NEEDS_ENGINE_SUPPORT`; keep automated evidence/full breadth open |
| 4D-03MZ-E | `FU-467f4c3cf4` / `SFD·102/221` / `HEXDRINKER_PLAY_EQUIPMENT` | `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the effect; assemble support exists in server engine path | `p2-preflight-play-hexdrinker-equipment.fixture.json`; `p4-play-hexdrinker-target-rejected.fixture.json`; `P4AssembleEquipmentCommandAttachesHexdrinkerWithOrangeCost` | `docs/CURRENT_P4_STATUS.md`; `docs/rules-evidence-index.md`; matrix FAQ status `NO_FAQ_CANDIDATE_IN_MATRIX` | Close selected `NEEDS_ENGINE_SUPPORT`; keep automated evidence/full breadth open |
| 4D-03NA-E | `FU-a53f864324` / `SFD·103/221` / `XERSAI_FISH_PLAY_UNIT_NO_OPTIONAL_HASTE` | `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the effect | `p2-preflight-play-xersai-fish-no-optional-haste.fixture.json`; `p4-play-xersai-fish-haste-ready.fixture.json`; fixture runner coverage | `docs/p2-rules-preflight.md`; `docs/CURRENT_P4_STATUS.md`; `docs/rules-evidence-index.md`; matrix FAQ status `NO_FAQ_CANDIDATE_IN_MATRIX` | Close selected `NEEDS_ENGINE_SUPPORT`; keep automated evidence/full breadth open |
| 4D-03NB-E | `FU-d65987cbb3` / `SFD·104/221` / `PETRICITE_MONUMENT_PLAY_EQUIPMENT_EPHEMERAL` | `src/Riftbound.Engine/CardBehaviorRegistry.cs` registers the effect | `p2-preflight-play-petricite-monument-equipment-ephemeral.fixture.json`; `p4-play-petricite-monument-target-rejected.fixture.json`; fixture runner coverage | `docs/CURRENT_P4_STATUS.md`; `docs/rules-evidence-index.md`; matrix FAQ status `NO_FAQ_CANDIDATE_IN_MATRIX` | Close selected `NEEDS_ENGINE_SUPPORT`; keep automated evidence/full breadth open |

## Before / After

| Counter | Before | After |
| --- | ---: | ---: |
| all functionalUnits `NEEDS_ENGINE_SUPPORT` | 563 | 558 |
| payment-cost `NEEDS_ENGINE_SUPPORT` | 165 | 160 |
| primary residual | 124 | 119 |
| targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 290 | 288 |
| cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT` | 216 | 215 |
| hidden-info-random-zone `NEEDS_ENGINE_SUPPORT` | 177 | 176 |
| payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 352 | 347 |
| payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT` | 103 | 101 |
| `NEEDS_AUTOMATED_TEST_EVIDENCE` | 328 | 328 |
| `NEEDS_FAQ_REVIEW` | 92 | 92 |
| primary FAQ residual | 61 | 61 |
| `fullOfficialTrue` | 0 | 0 |
| `ready` | false | false |

## Non-Closure

The following remain open and must not be represented as closed by this bundle:

- automated evidence disposition for all five selected rows
- complete PaymentEngine / PAY_COST matrix
- rune-cost draw and hidden-info breadth
- boon/layer and continuous-effect breadth
- equipment attach/follow and assemble breadth
- exact `HASTE_READY` optional-cost breadth
- ephemeral cleanup / replacement-duration breadth
- FEPR target-stack lifecycle breadth
- FAQ review residuals
- `fullOfficial`
- READY

## Validation

Validation is pending until the final command run is recorded in this file and on the shared coordination board.
