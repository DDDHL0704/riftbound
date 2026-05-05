# Current P6 Status

Last updated: 2026-05-05

P6 goal:

> 完成 P6 全卡牌批量实现：基于 P2/P3/P4/P5 已验证规则、BehaviorSpec、模板和代表路径，按功能逻辑单元小批次接入官方 1009 条卡牌/811 个后端行为；每批补齐规则证据、官网卡面文本、engine/conformance 测试、必要的 GameHub/Room 或 Browser smoke、状态文档和提交；未实现能力必须有明确 blocked/deferred reason，保持 P2-P5 全部绿色。

## Baseline Confirmation

- Starting commit: `ea03153 docs: complete p5 final audit`
- Expected dirty state at phase start: only untracked `riftbound-dotnet.sln`
- Official catalog snapshot: `data/official/card-catalog.zh-CN.json`, fetched `2026-04-27`
- Official catalog entries: `1009/1009`
- Functional units: `811/811`, stable and unique
- Duplicate groups: `113`
- Duplicate entries: `311`
- Saved logic implementations from grouping: `198`
- Current `CardBehaviorRegistry` definitions: `800`
- Current unique registry card numbers: `785`
- P2 core rules preflight: `811/811 = 100.0%`
- P2.5 development test UI: complete
- P3 card catalog and BehaviorSpec system: complete
- P4 high-frequency keywords and base card paths: `392/392 = 100.0%`
- P5 representative equipment/control/trigger/replacement scope: `10/10 planned batches = 100.0%`
- Latest recorded full validation before P6: `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` passed `2574/2574`
- Latest recorded focused suites before P6:
  - `ConformanceFixtureRunnerTests`: `2493/2493`
  - `CardCatalogBaselineTests`: `23/23`
  - `GameHubJoinTests`: `16/16`

## Evidence Sources Read

- `docs/CURRENT_P5_STATUS.md`
- `docs/CURRENT_P4_STATUS.md`
- `docs/CURRENT_P3_STATUS.md`
- `docs/CURRENT_P2_STATUS.md`
- `docs/CURRENT_P2_5_STATUS.md`
- `docs/master-development-plan.md`, P6 section
- `docs/START_HERE.md`
- `README.md`
- `docs/rules-evidence-index.md`
- `docs/conformance-fixture-format.md`
- `src/Riftbound.Contracts/BehaviorSpecs.cs`
- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/CardBehaviorRegistry.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `data/official/card-catalog.zh-CN.json`

## P6 Scope

In scope:

- Promote the current representative P2-P5 paths into a full-card P6 status matrix.
- Batch official entries by functional behavior, not by product or card number order.
- Reuse existing `BehaviorSpec`, `CardBehaviorRegistry`, template/profile catalogs, conformance fixtures, and P5 state models.
- Implement same-text variants and reprints by mapping them to one functional unit instead of duplicating rules.
- Add small, separately validated batches for high-risk systems: response windows, control, trigger/replacement timing, attachment, combat damage, hidden information, battlefields, legends, runes, and token factories.
- Keep unsupported abilities at a zero-side-effect blocked/deferred boundary with explicit reason.

Out of scope:

- P7 product-grade Web battle UI.
- Complex AI.
- Mobile-specific adaptation.
- Multi-instance room hot migration.
- Committing official PDF/FAQ files.
- Committing untracked `riftbound-dotnet.sln`.

## Official Catalog Breakdown

Official entries by category:

| Category | Entries |
| --- | ---: |
| 英雄单位 | 235 |
| 单位 | 257 |
| 法术 | 158 |
| 装备 | 93 |
| 专属单位 | 3 |
| 专属法术 | 34 |
| 专属装备 | 5 |
| 符文 | 48 |
| 传奇 | 106 |
| 战场 | 57 |
| 指示物单位 | 9 |
| 指示物装备 | 2 |
| 指示物战场 | 2 |

Functional units by category:

| Category | Functional units |
| --- | ---: |
| 英雄单位 | 153 |
| 单位 | 255 |
| 法术 | 158 |
| 装备 | 86 |
| 专属单位 | 3 |
| 专属法术 | 34 |
| 专属装备 | 5 |
| 符文 | 6 |
| 传奇 | 44 |
| 战场 | 54 |
| 指示物单位 | 9 |
| 指示物装备 | 2 |
| 指示物战场 | 2 |

Current P6 BehaviorSpec status by official entry:

| Status | Entries | Meaning at P6.1a |
| --- | ---: | --- |
| `implemented` | 833 | Has a current `CardBehaviorRegistry` mapping or a P6 non-`PLAY_CARD` rule-domain mapping. |
| `manual-rule-required` | 163 | Non-`PLAY_CARD` domains: legends and battlefields. P6 must either implement the domain or block/defer explicitly. |
| `unimplemented` | 13 | Token categories awaiting explicit token factory binding. |

Current status by functional unit:

| Status | Functional units |
| --- | ---: |
| `implemented` | 700 |
| `manual-rule-required` | 98 |
| `unimplemented` | 13 |

Remaining uncovered non-`PLAY_CARD` functional units:

| Category | Functional units | Entries | P6 disposition |
| --- | ---: | ---: | --- |
| 传奇 | 44 | 106 | P6.9 high-risk/manual domain; active/passive identity rules need dedicated handlers or blocked reason. |
| 战场 | 54 | 57 | P6.10 high-risk/manual domain; battlefield effects and control need dedicated handlers or blocked reason. |
| 指示物单位 | 9 | 9 | P6.11 token factory domain. |
| 指示物装备 | 2 | 2 | P6.11 token factory domain. |
| 指示物战场 | 2 | 2 | P6.11 token factory domain. |

## Risk Layers

Low risk:

- Pure status and display mapping.
- Same-text variants and reprints that already share a functional unit.
- Rune resource cards, provided they remain non-`PLAY_CARD` and reuse the P2 rune call/pay/clear conformance path.
- Already-verified `CardBehaviorRegistry` cards that only need P6 ledger promotion.

Medium risk:

- New parameters in existing templates: draw counts, damage counts, target scopes, optional costs, and cost reductions.
- New equipment or token definitions that use existing object state fields without introducing new timing.
- Existing trigger/replacement representatives generalized only within their proven timing boundary.

High risk:

- Response windows and spell duel focus.
- Control changes, return, owner/controller separation, and hidden information.
- Attachment, detachment, equipment following, equipment leaving, assemble/agile/forge/tempered paths.
- Combat assignment, combat damage, conquest, defense, scoring, and victory.
- Generic trigger queues, replacement ordering, and simultaneous event ordering.
- Legends, battlefields, and token factories because they are not normal `PLAY_CARD` behaviors.

Epic/single-card risk:

- Cards that cross multiple high-risk domains in one resolution path.
- Cards that introduce a new queue, delayed choice, hidden selection, or multi-player ordered choice.

## P6 Initial Batch Ledger

P6.0 is audit/status only. It does not change engine behavior and must not change `CoreRuleEngine`.

| Batch | Status | Target | Planned gate |
| --- | --- | --- | --- |
| P6.0 | Done | Audit/status file, risk layering, first migration plan. | Full P6 gate passed. |
| P6.1a | Done | Rune resource-domain mapping: 6 functional units / 48 entries. | Existing P2 rune conformance fixtures + `CardCatalogBaselineTests`. |
| P6.1b | Planned | Same-text variant/reprint audit for already implemented functional units. | Catalog status matrix + no duplicate rule handlers. |
| P6.2a | Planned | Draw/damage/destroy/stun existing-template promotion. | Conformance fixtures for representative paths. |
| P6.2b | Planned | Recall/move/recycle/banish/temp might/boon existing-template promotion. | Conformance fixtures for representative paths. |
| P6.3 | Planned | Swift/reaction/spell-duel batches. | GameHub/Room smoke required. |
| P6.4 | Planned | Movement/battle/scoring batches. | GameHub/Room smoke required. |
| P6.5 | Planned | Standby/ambush/echo batches. | Response-window conformance required. |
| P6.6 | Planned | Equipment/assemble/agile/forge/tempered batches. | P5 equipment invariant tests required. |
| P6.7 | Planned | Experience/level/hunt/encourage batches. | Engine + conformance tests. |
| P6.8 | Planned | Last Breath/ephemeral/replacement/trigger batches. | High-risk small batches. |
| P6.9 | Planned | Legend active/passive batches. | Dedicated non-`PLAY_CARD` domain tests. |
| P6.10 | Planned | Battlefield effect batches. | Battlefield/domain tests and smoke where player-visible. |
| P6.11 | Planned | Token and copy factory batches. | Token object factory tests. |
| P6.12 | Planned | Unique complex cards, one card or tiny group at a time. | Full relevant gates per card. |
| P6.x | Planned | Completion audit and final verification. | Full suite, focused suites, status matrix, no unexpected dirty files. |

Initial estimated remaining implementation/audit batches after P6.0: at least `15`, likely more after high-risk card triage. The batch count is intentionally conservative and will be updated after each P6 slice lands.

## Current Progress

- P6.0 audit progress: `1/1 audit slice = 100.0%`.
- P6.1a rune resource-domain progress: `6/6 rune functional units = 100.0%`; `48/48 rune entries = 100.0%`.
- P6 implementation progress: `700/811 functional units = 86.3%`.
- P6 non-`PLAY_CARD` backlog remaining after P6.1a: `111/811 functional units = 13.7%`.
- P6 official-entry status coverage: `1009/1009 entries = 100.0%`, with `176/1009 entries = 17.4%` still requiring P6 implementation or explicit final blocked/deferred reason.
- P6-specific newly closed backlog after P6.1a: `6/117 = 5.1%` functional units and `48/224 = 21.4%` official entries.

## Validation Policy

Every .NET command must be run through:

```bash
source scripts/dev-env.sh
```

Per-batch gates:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`
- `git diff --check`
- After each batch commit, `git status --short` should show only `?? riftbound-dotnet.sln`.

High-risk rule abilities also require GameHub/Room or Browser smoke with local URL, roomId, operation path, observed events, and final snapshot summary recorded here.

## P6.0 Validation

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2574/2574`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `23/23`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.
- `git diff --check`: passed.

## P6.1a Delivered

- Added `OfficialRuleDomainBehaviorCatalog` as the P6 bridge for non-`PLAY_CARD` rule domains.
- Mapped all official rune cards to `RUNE_RESOURCE_DOMAIN` without adding rune card numbers to `CardBehaviorRegistry`.
- Updated `/catalog/summary`, `/catalog/p3-status`, and `/catalog/behavior-specs` so BehaviorSpecs merge play-card registry mappings with the P6 rune resource-domain mapping.
- Updated catalog baseline tests so all `48` rune official entries and `6` rune functional units are `implemented`, while `CardBehaviorRegistry.TryGetByCardNo` still rejects them as playable cards.
- P6.1a relies on existing P2 conformance evidence for rune call, rune pool payment, and end-turn rune pool clearing: `p2-preflight-turn-start-runes-and-draw`, `p2-preflight-turn-start-short-rune-deck`, `p2-preflight-turn-start-first-p2-extra-rune`, and the many existing cost-payment fixtures.

P6.1a validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2575/2575`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `24/24`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.
- `git diff --check`: passed.

## Next Step

Commit P6.1a, then continue into P6.1b same-text variant/reprint audit for already implemented functional units.
