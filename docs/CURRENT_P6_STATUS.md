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
| P6.1b | Done | Same-text variant/reprint audit for already implemented functional units. | Catalog status matrix + no duplicate rule handlers. |
| P6.2a | Done | Draw/damage/destroy/stun existing-template promotion. | Conformance fixtures for representative paths. |
| P6.2b | Done | Recall/move/recycle/banish/temp might/boon existing-template promotion. | Conformance fixtures for representative paths. |
| P6.3a | Done | Swift/reaction/spell-duel online response-window smoke. | GameHub/Room smoke passed. |
| P6.3b | Done | Swift spell-duel timing expansion for 6 simple official spell representatives. | Conformance + catalog profile tests passed. |
| P6.3c | Done | Reaction priority-window timing expansion for 6 simple official spell representatives. | Conformance + catalog profile tests passed. |
| P6.3d | Planned | Remaining reaction/standby/ambush boundary triage. | Conformance + zero-side-effect blocked/deferred checks. |
| P6.4a | Done | Movement + burnout scoring/win GameHub core-path smoke. | GameHub/Room smoke passed. |
| P6.4b | Done | Battle declaration GameHub core-path smoke. | GameHub/Room smoke passed. |
| P6.4c | Planned | Remaining movement/battle/scoring boundary checks. | Conformance + GameHub/Room smoke where player-visible. |
| P6.5a | Done | Standby reaction, Ambush reaction, and Echo representative GameHub smokes. | GameHub/Room smoke passed. |
| P6.5b | Done | Standby/Ambush/Echo keyword surface matrix and deferred boundary triage. | Catalog profile matrix passed. |
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
- P6.1b same-text/reprint audit progress: `74/113 duplicate groups = 65.5%` are already covered by shared implementation; remaining `39/113 = 34.5%` are legend/battlefield duplicate groups for P6.9/P6.10.
- P6.2a high-frequency template-family audit:
  - draw: `105/131 entries = 80.2%`, `99/114 units = 86.8%`
  - damage: `141/148 entries = 95.3%`, `124/129 units = 96.1%`
  - destroy: `115/127 entries = 90.6%`, `109/118 units = 92.4%`
  - stun: `30/33 entries = 90.9%`, `28/29 units = 96.6%`
- P6.2b secondary template-family audit:
  - recall: `39/49 entries = 79.6%`, `36/43 units = 83.7%`
  - move: `116/136 entries = 85.3%`, `100/111 units = 90.1%`
  - recycle: `55/63 entries = 87.3%`, `45/51 units = 88.2%`
  - banish: `8/11 entries = 72.7%`, `8/9 units = 88.9%`
  - temp might: `255/292 entries = 87.3%`, `208/230 units = 90.4%`
  - boon: `51/66 entries = 77.3%`, `41/48 units = 85.4%`
- P6.3a response-window smoke progress: `1/1 GameHub smoke = 100.0%`.
- P6.3b swift spell-duel timing expansion: `6/6 selected official swift spell representatives = 100.0%`.
- P6.3c reaction priority-window timing expansion: `6/6 selected official reaction spell representatives = 100.0%`.
- P6.4a movement/scoring smoke progress: `2/2 GameHub core paths = 100.0%`.
- P6.4b battle declaration smoke progress: `1/1 GameHub core path = 100.0%`.
- P6.5a standby/ambush/echo smoke progress: `3/3 GameHub core paths = 100.0%`.
- P6.5b interaction keyword surface triage: `95/95 keyword-surface entries = 100.0%`; `85/85 keyword-surface functional units = 100.0%`.
  - Profile implemented boundary: `10/95 entries = 10.5%`, `10/85 functional units = 11.8%` (mana-only Echo representatives).
  - Profile deferred boundary: `85/95 entries = 89.5%`, `75/85 functional units = 88.2%` (Standby, Ambush, and complex Echo surfaces).
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

## P6.1b Delivered

- Added `FunctionalUnitBehaviorCoverageReporter` to aggregate BehaviorSpec status by functional unit.
- Locked the P6 duplicate/reprint matrix:
  - duplicate groups: `113`
  - duplicate entries: `311`
  - already implemented duplicate groups: `74`
  - already implemented duplicate entries: `207`
  - pending duplicate groups: `39`
  - pending duplicate entries: `104`
- Confirmed every implemented duplicate group has one shared `implementedByCardNo` inside its functional-unit card list, so variants/reprints do not need duplicated rule handlers.
- Confirmed pending duplicate groups are only `传奇` and `战场`, which remain assigned to P6.9/P6.10.
- This batch does not change `CoreRuleEngine` behavior and does not close additional functional units; it formalizes the low-risk mapping/audit surface for completion checks.

P6.1b validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2576/2576`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `25/25`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.
- `git diff --check`: passed.

## P6.2a Delivered

- Added `BehaviorTemplateFamilyCoverageReporter` to report entry and functional-unit coverage for high-frequency template families.
- Locked the current P6 coverage for `draw`, `damage`, `destroy`, and `stun`:
  - `draw`: `131` entries / `114` functional units; `105` implemented entries / `99` implemented units.
  - `damage`: `148` entries / `129` functional units; `141` implemented entries / `124` implemented units.
  - `destroy`: `127` entries / `118` functional units; `115` implemented entries / `109` implemented units.
  - `stun`: `33` entries / `29` functional units; `30` implemented entries / `28` implemented units.
- The implemented rows are promoted only as existing-template coverage; pending rows remain assigned to later manual domains or token factories rather than silently entering playable paths.
- Existing representative conformance coverage remains the rule gate for playable paths, including draw (`先知的预言` and draw-on-effect fixtures), damage (`焚烧` / `惩戒` / multi-damage fixtures), destroy (`复仇` / conditional destroy fixtures), and stun (`符文禁锢` / stun-reaction fixtures).
- This batch does not change `CoreRuleEngine` behavior and does not close additional functional units; it makes high-frequency template promotion auditable for P6 completion.

P6.2a validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2577/2577`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `26/26`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.
- `git diff --check`: passed.

## P6.2b Delivered

- Reused `BehaviorTemplateFamilyCoverageReporter` for the next high-reuse template families: `recall`, `move`, `recycle`, `banish`, `temp_might`, and `boon`.
- Locked the current P6 coverage:
  - `recall`: `49` entries / `43` functional units; `39` implemented entries / `36` implemented units.
  - `move`: `136` entries / `111` functional units; `116` implemented entries / `100` implemented units.
  - `recycle`: `63` entries / `51` functional units; `55` implemented entries / `45` implemented units.
  - `banish`: `11` entries / `9` functional units; `8` implemented entries / `8` implemented units.
  - `temp_might`: `292` entries / `230` functional units; `255` implemented entries / `208` implemented units.
  - `boon`: `66` entries / `48` functional units; `51` implemented entries / `41` implemented units.
- Pending rows remain later manual domains or token factories; no pending row is silently promoted into playable paths.
- Existing representative conformance remains the rule gate for playable paths: `召回/移动` fixtures, `潜行破坏` recycle fixtures, `奥术跃迁` banish-and-replay fixtures, P4 temporary might fixtures, and boon fixtures.
- This batch does not change `CoreRuleEngine` behavior and does not close additional functional units; it expands the template-family completion audit surface.

P6.2b validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2578/2578`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `27/27`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `16/16`.
- `git diff --check`: passed.

## P6.3a Delivered

- Added a GameHub/Room smoke for the response-window handoff after a spell is played from the `spell-duel` development seed.
- Test path: `P6SpellDuelSeedTransfersOnlinePriorityAfterSpellIsPlayed` in `GameHubJoinTests`.
- Local URL: N/A; this is an in-memory SignalR `GameHub` smoke with no browser/server listener.
- Room ID: `p6-3a-response-window`.
- Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(spell-duel)` -> `SubmitIntent(PLAY_CARD Hextech Ray, OGN·009/298, target P2-UNIT-001)` -> `SubmitIntent(PASS_PRIORITY by P1)`.
- Observed events after play: `CARD_PLAYED`, `COST_PAID`, `STACK_ITEM_ADDED`.
- Observed events after pass: `PRIORITY_PASSED`.
- Final prompt/snapshot summary: stack remains at `1` pending item, `timingState = NEUTRAL_CLOSED`, `priorityPlayerId = P2`; P1 prompt is `WAIT`, P2 prompt is actionable `PASS_PRIORITY`.
- This batch does not close additional functional units; it verifies the online response-window boundary required before expanding swift/reaction/spell-duel playable coverage.

P6.3a validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2579/2579`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2493/2493`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `27/27`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `17/17`.
- `git diff --check`: passed.

## P6.3b Delivered

- Enabled `CanPlayDuringSpellDuel` for 6 implemented official `迅捷` spell representatives that already had P2/P4 conformance coverage:
  - `OGS·003/024` 焚烧: simple battlefield unit damage.
  - `OGN·009/298` 海克斯射线: simple battlefield unit damage.
  - `OGN·050/298` 符文禁锢: simple stun.
  - `OGN·102/298` 传送门大营救: friendly unit banish then play to base.
  - `OGN·172/298` 责退: battlefield unit recall.
  - `SFD·135/221` 紧急召回: equipment recall.
- Kept activated `{{迅捷>}}` abilities out of this batch; they remain later activated-ability work, not card-play timing.
- Added catalog baseline coverage to prove each selected card has official `{{迅捷}}` card-play text, is `implemented`, and exposes `CardPermissionKeywordRules.BuildProfile(...).HasSwift`.
- Added `p6-play-swift-hextech-ray-in-spell-duel-focus.fixture.json` with official card text and rule evidence, proving a newly expanded swift spell can be played by the focus player during `SPELL_DUEL_OPEN`, enter the stack, pass priority, resolve, clear focus, and return to `NEUTRAL_OPEN`.
- This batch expands playable timing for already implemented functional units; it does not change BehaviorSpec status counts.

P6.3b validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2581/2581`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2494/2494`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `28/28`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `17/17`.
- `git diff --check`: passed.

## P6.3c Delivered

- Enabled `CanPlayDuringPriority` for 6 implemented official `反应` spell representatives that already had P2/P4 conformance coverage:
  - `SFD·087/221` 先知之兆: draw 3.
  - `OGN·058/298` 训练有素: temporary power +2 and draw 1.
  - `OGN·093/298` 烟幕弹: temporary power -4 with floor 1.
  - `OGN·095/298` “敲”诈: temporary power -1 with floor 1 and draw 1.
  - `UNL-066/219` 月光之殇: temporary power -10.
  - `OGN·169/298` 罡风: recall a battlefield unit with power no higher than 3.
- Kept `{{反应>}}` activated resource/legend/equipment abilities, `伏击`, `待命`, and `灵便` out of this batch; those remain P6.3d/P6.5/P6.6 boundaries.
- Added catalog baseline coverage to prove each selected card has official `{{反应}}` card-play text, is `implemented`, and exposes `CardPermissionKeywordRules.BuildProfile(...).HasReaction`.
- Added `p6-play-reaction-prophets-omen-in-priority.fixture.json` with official card text and rule evidence. The fixture opens a priority window with `焚烧`, plays `先知之兆` as the priority player, resolves it first, then returns priority to the original stack item before resolving `焚烧`.
- This batch expands playable timing for already implemented functional units; it does not change BehaviorSpec status counts.

P6.3c validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2583/2583`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2495/2495`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `29/29`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `17/17`.
- `git diff --check`: passed.

## P6.4a Delivered

- Added a GameHub/Room smoke covering two P6.4 core paths from development seeds.
- Movement smoke:
  - Local URL: N/A; in-memory SignalR `GameHub` smoke with no browser/server listener.
  - Room ID: `p6-4a-movement-core`.
  - Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(movement)` -> `SubmitIntent(PLAY_CARD Ride the Wind, OGN·173/298, target P1-BATTLEFIELD-UNIT-001)` -> `PASS_PRIORITY(P1)` -> `PASS_PRIORITY(P2)`.
  - Observed events: `STACK_ITEM_ADDED`, `PRIORITY_PASSED`, `STACK_ITEM_RESOLVED`, `UNIT_MOVED_TO_BASE`.
  - Final snapshot summary: `P1-BATTLEFIELD-UNIT-001` is in P1 base after resolution.
- Scoring/win smoke:
  - Local URL: N/A; in-memory SignalR `GameHub` smoke with no browser/server listener.
  - Room ID: `p6-4a-score-core`.
  - Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(battle-score)` -> `SubmitIntent(END_TURN by P1)`.
  - Observed events: `TURN_END_DECLARED`, `TURN_PLAYER_ADVANCED`, `TURN_START_BEGAN`, `BURNOUT_APPLIED`, `MATCH_WON`.
  - Final snapshot summary: turn advances to `76`, active player is `P2`, `winnerPlayerId = P1`, room status is `FINISHED`, and P1 score is `8`.
- This batch does not change engine behavior or BehaviorSpec status counts; it locks the player-visible Room path for movement and the current burnout-to-win scoring path before broader P6.4 expansion.

P6.4a validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2584/2584`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2495/2495`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `29/29`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `18/18`.
- `git diff --check`: passed.

## P6.4b Delivered

- Added a development-only `battle-declare` scenario seed for a minimal one-attacker, one-defender battlefield battle.
- Added a GameHub/Room smoke for the `DECLARE_BATTLE` player-visible path:
  - Local URL: N/A; in-memory SignalR `GameHub` smoke with no browser/server listener.
  - Room ID: `p6-4b-battle-declare-core`.
  - Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(battle-declare)` -> `SubmitIntent(DECLARE_BATTLE, battlefield BATTLEFIELD:P1-MAIN, attacker P1-BATTLE-ATTACKER-001, defender P2-BATTLE-DEFENDER-001, optional cost COMBAT_ASSIGNMENT)`.
  - Observed events: `BATTLE_DECLARED`, two `DAMAGE_APPLIED`, `UNIT_DESTROYED`.
  - Final snapshot summary: stack is empty, `P1-BATTLE-ATTACKER-001` remains on P1 battlefield, `P2-BATTLE-DEFENDER-001` leaves P2 battlefield and is in P2 graveyard.
- This batch does not change engine behavior or BehaviorSpec status counts; it locks the current battle declaration and combat-damage path through Room before deeper P6.4 boundary expansion.

P6.4b validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2585/2585`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2495/2495`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `29/29`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `19/19`.
- `git diff --check`: passed.

## P6.5a Delivered

- Added three development-only scenario seeds for existing representative interaction paths:
  - `echo-stack`: `UNL-061/219` 台前作秀 with `ECHO` optional cost and two-card draw deck.
  - `standby-reaction`: face-down `OGN·197/298` 提莫 in base with a pending opponent stack item and P1 holding priority.
  - `ambush-reaction`: `UNL-021/219` 阴森药剂师 in hand with a friendly battlefield unit, a pending opponent stack item, and P1 holding priority.
- Added GameHub/Room smokes:
  - Local URL: N/A; in-memory SignalR `GameHub` smoke with no browser/server listener.
  - Room ID: `p6-5a-echo-stack-core`.
  - Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(echo-stack)` -> `SubmitIntent(PLAY_CARD Center Stage, UNL-061/219, optionalCosts ECHO)` -> `PASS_PRIORITY(P1)` -> `PASS_PRIORITY(P2)`.
  - Observed events: `COST_PAID`, `STACK_ITEM_ADDED`, `STACK_ITEM_RESOLVED`, `CARD_DRAWN`.
  - Final snapshot summary: stack is empty, P1 main deck count is `0`, P1 hand contains `P1-DRAW-001` and `P1-DRAW-002`, and `P1-SPELL-CENTER-STAGE` is in graveyard.
  - Room ID: `p6-5a-standby-reaction-core`.
  - Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(standby-reaction)` -> `SubmitIntent(REVEAL_CARD Teemo, mode STANDBY_REACTION, destination STACK)` -> `PASS_PRIORITY(P1)` -> `PASS_PRIORITY(P2)`.
  - Observed events: `CARD_REVEALED`, `CARD_PLAYED`, `STACK_ITEM_ADDED`, `STACK_ITEM_RESOLVED`, `UNIT_PLAYED_TO_BASE`, `POWER_MODIFIED_UNTIL_END_OF_TURN`.
  - Final snapshot summary: one original opponent stack item remains, and `P1-FACEDOWN-OGN-TEEMO-PURPLE` is in P1 base face-up on the resolved path.
  - Room ID: `p6-5a-ambush-reaction-core`.
  - Operation path: `JoinRoom(P1/P2)` -> `SeedScenario(ambush-reaction)` -> `SubmitIntent(PLAY_CARD Gloomy Apothecary, mode AMBUSH, destination BATTLEFIELD:P1-MAIN)` -> `PASS_PRIORITY(P1)` -> `PASS_PRIORITY(P2)`.
  - Observed events: `CARD_PLAYED`, `COST_PAID`, `STACK_ITEM_ADDED`, `STACK_ITEM_RESOLVED`, `UNIT_PLAYED_TO_BATTLEFIELD`.
  - Final snapshot summary: one original opponent stack item remains, P1 hand is empty, and P1 battlefield contains the existing friendly unit plus `P1-HAND-UNL-GLOOMY-APOTHECARY`.
- This batch does not change engine behavior or BehaviorSpec status counts; it promotes already-conformant P4/P2 representative paths to Room-visible P6 coverage.

P6.5a validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2588/2588`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2495/2495`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `29/29`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `22/22`.
- `git diff --check`: passed.

## P6.5b Delivered

- Added a P6 interaction keyword coverage matrix to `CardCatalogBaselineTests`.
- Locked official keyword-surface counts and BehaviorSpec/profile boundaries:

| Keyword | Entries | Spec implemented entries | Functional units | Spec implemented units | Profile implemented entries | Profile deferred entries | Profile implemented units | Profile deferred units |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 待命 | 53 | 47 | 43 | 41 | 0 | 53 | 0 | 43 |
| 回响 | 24 | 22 | 24 | 22 | 10 | 14 | 10 | 14 |
| 伏击 | 18 | 18 | 18 | 18 | 0 | 18 | 0 | 18 |

- Deferred reason:
  - `待命`: P4/P6 has representative hide/reveal/reaction paths and Room smoke, but broad target damage, per-card standby triggers, and hidden-choice flows remain deferred.
  - `伏击`: P4/P6 has one Room-smoked representative battlefield reaction play, but broad Ambush target scopes and per-card combat triggers remain deferred.
  - `回响`: mana-only Echo representatives are implemented; complex Echo costs or cards with other high-risk interaction keywords remain deferred unless their ordinary play effect already has a separate conformance path.
- This batch does not change engine behavior or BehaviorSpec status counts; it prevents P6 from accidentally promoting deferred interaction surfaces into full `CONFORMANCE_PASS`.

P6.5b validation:

- `source scripts/dev-env.sh && dotnet build Riftbound.slnx --no-restore`: passed, `0` warnings, `0` errors.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore`: passed `2589/2589`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ConformanceFixtureRunnerTests"`: passed `2495/2495`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CardCatalogBaselineTests"`: passed `30/30`.
- `source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~GameHubJoinTests"`: passed `22/22`.
- `git diff --check`: passed.

## Next Step

Commit P6.5b, then continue into P6.6 equipment/assemble/agile/forge/tempered batches.
