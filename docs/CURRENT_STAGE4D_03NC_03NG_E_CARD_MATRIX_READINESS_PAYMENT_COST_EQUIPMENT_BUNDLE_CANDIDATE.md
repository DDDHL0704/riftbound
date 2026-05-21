# 4D-03NC-E..4D-03NG-E Payment-Cost Equipment Bundle Candidate

Scope: DOC_MATRIX_CURRENT only. This candidate reduces row-level `NEEDS_ENGINE_SUPPORT` for five payment-cost equipment functional units with existing runtime, fixture, and rules-evidence coverage. It does not modify runtime, frontend, official catalog, protocol core fields, general tests, `fullOfficial`, FAQ status, final readiness status, or `riftbound-dotnet.sln`.

Selected rows:

- 4D-03NC-E: `FU-f869715f3f` / `SFD·108/221` 狂徒铠甲 / `WARMOGS_ARMOR_PLAY_EQUIPMENT`
- 4D-03ND-E: `FU-33b03a8a16` / `SFD·115/221` 三相之力 / `TRINITY_FORCE_PLAY_EQUIPMENT`
- 4D-03NE-E: `FU-9b15a2d5e9` / `SFD·118/221` 碎骨棒 / `BONE_CLUB_PLAY_EQUIPMENT;BONECLUB_PROMO_PLAY_EQUIPMENT`
- 4D-03NF-E: `FU-a6dbffe5f4` / `SFD·124/221` 多兰之戒 / `DORANS_RING_PLAY_EQUIPMENT`
- 4D-03NG-E: `FU-16ed19339f` / `SFD·133/221` 轻灵之靴 / `BOOTS_OF_SWIFTNESS_PLAY_EQUIPMENT`

Evidence basis:

- Runtime: existing `CardBehaviorRegistry` entries for the five play-equipment effects and existing `CoreRuleEngine` / `MatchSession` assemble profiles for the selected equipment rows.
- Fixtures/tests: existing preflight play, target-rejection, and assemble-equipment focused tests for Warmog's Armor, Trinity Force, Bone Club ordinary/promo, Doran's Ring, and Boots of Swiftness.
- Rules evidence: `docs/rules-evidence-index.md` records RULE_AUDITED entries for each selected play-equipment row plus `ASSEMBLE_EQUIPMENT` representative attach paths.
- FAQ: selected functional units have no matrix FAQ refs; this bundle does not close any FAQ residual.

Count delta:

- all functional units `NEEDS_ENGINE_SUPPORT`: 558 -> 553
- payment-cost `NEEDS_ENGINE_SUPPORT`: 160 -> 155
- primary residual: 119 -> 115
- targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 288 -> 288
- cleanup-replacement-duration `NEEDS_ENGINE_SUPPORT`: 215 -> 215
- hidden-info-random-zone `NEEDS_ENGINE_SUPPORT`: 176 -> 176
- payment-or-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 347 -> 342
- payment-and-targeting-stack-timing `NEEDS_ENGINE_SUPPORT`: 101 -> 101
- `NEEDS_AUTOMATED_TEST_EVIDENCE`: 328 remains open
- `NEEDS_FAQ_REVIEW`: 92 remains open
- `fullOfficialTrue`: 0 remains 0
- `ready`: false remains false

Non-closure:

This is only matrix + audit-test baseline synchronization for row-level engine-support blockers. Automated evidence disposition remains open for all five selected rows. Complete equipment attach/follow lifecycle, LayerEngine / continuous-effect breadth, control-zone movement breadth, complete PaymentEngine / PAY_COST matrix, E_CARD_MATRIX_READINESS, full card matrix, P0/P1 closure, frontend/formal E2E, `fullOfficial`, and READY remain open. Project remains **NOT READY**.
