# Plan B Conditional Source Unit Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `UNL-004/219` 晋升信徒 has the 4+ spell turn-memory condition and `S+4` source-unit bonus.
- `data/official/card-catalog.zh-CN.json` row `UNL-108/219` 狡猾的蝾螈 has the gained-experience turn-memory condition and grants the source `S+1` plus `游走`.
- `data/official/card-catalog.zh-CN.json` row `OGN·019/298` 肆虐狂魂 has the discarded-hand-card turn-memory condition and grants the source `强攻` plus `游走`.

Existing engine evidence:

- The engine already writes turn-memory markers for discarded hand cards, gained experience, and played 4+ cost spells this turn.
- Existing fixtures cover the three representative play paths:
  - `p2-preflight-play-ascended-believer-four-plus-spell-unit.fixture.json`
  - `p2-preflight-play-sly-salamander-experience-keyword-unit.fixture.json`
  - `p2-preflight-play-rampaging-soul-discarded-hand-keyword-unit.fixture.json`

## Engine Evidence

Before this slice, `CoreRuleEngine` selected these branches through dedicated runtime constants:

- `AscendedBelieverConditionalSourceEffectKind`
- `SlySalamanderConditionalSourceEffectKind`
- `RampagingSoulConditionalSourceEffectKind`

After this slice:

- `CoreRuleEngine` no longer contains those constants or the corresponding catalog effect ids as runtime selectors.
- `CardBehaviorRegistry` stores the official condition kind, power bonus, and conditional tags on each representative behavior row.
- `ResolveConditionalSourceUnitPowerBonus` returns `ConditionalSourceUnitPowerBonus` only when the configured condition applies.
- `ResolveConditionalSourceUnitTags` parses `ConditionalSourceUnitTags` only when the configured condition applies.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.ConditionalSourceUnitPowerAndTagsUseBehaviorFields` failed red before implementation because `CoreRuleEngine` still contained `AscendedBelieverConditionalSourceEffectKind`.
- The same guard now blocks reintroducing the three runtime effect-kind selectors and requires the shared conditional source-unit behavior fields.
- `CardCatalogBaselineTests.ConditionalSourceUnitPowerAndTagsCarryOfficialTurnMemoryFields` locks the three official rows to their condition kind, power bonus, and conditional tags.
- Focused source guard / registry baseline / fixture representative gate passed `3114/3114`.
- Adjacent / hidden-info representative gate `PlayBehaviorSourceIdentityGuardTests|ConditionalSourceUnitPowerAndTagsCarryOfficialTurnMemoryFields|ConformanceFixtureRunnerTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `5827/5827`.
- Backend full conformance passed `9025/9025`.

## Non-Claims

This evidence does not claim complete conditional source-unit official breadth, complete source-object continuous effect breadth, complete keyword grant/removal `RULE_TEXT` layer breadth, complete PaymentEngine, P0 completion, or READY.
