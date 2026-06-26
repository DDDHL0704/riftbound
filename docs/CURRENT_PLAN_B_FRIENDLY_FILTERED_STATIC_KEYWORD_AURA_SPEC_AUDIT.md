# Plan B Friendly-Filtered Static Keyword Aura Spec Audit

更新时间：2026-06-26

## Scope

This slice advances Plan B / B2 RULE_TEXT static keyword aura coverage without adding card-number runtime branches.

Covered representatives:

- `SFD·026/221` / `SFD·026a/221` 兰博：`你的“机械”属性单位获得{{强攻}}。`
- `SFD·065/221` 先见机甲：`你的“机械”属性单位获得{{预知}}。`
- `SFD·071/221` 疾驰机械：`你的“机械”属性单位获得{{法盾}}和{{游走}}。`
- `SFD·181/221` / `SFD·240/221` 机械公敌：`你的“机械”属性单位获得{{坚守}}。`
- `UNL-058/219` / `UNL-058a/219` 莉莉娅：`你的指示物单位获得{{壁垒}}。`

The official text parses into:

- `StaticAuraSpec.Kind=FRIENDLY_FILTERED_UNITS_KEYWORD`
- `Layer=RULE_TEXT`
- `Duration=WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD`
- `TargetScope=FRIENDLY_FILTERED_UNITS`
- `ParticipantScope=FRIENDLY_FILTERED_PUBLIC_UNITS`
- `PowerDeltaPerParticipant=0`
- `TargetFilter=TAG:机械` or `UNIT_TOKEN`
- `GrantedKeyword=强攻` / `预知` / `法盾` / `游走` / `坚守` / `壁垒`

Runtime evidence now covers:

- `MatchSession` continuous-effect projection produces RULE_TEXT object effects from public-field unit sources and legend-zone sources to matching friendly public units.
- Rumble hero grants Assault to friendly mechanical units and excludes non-mechanical friendly units plus opposing mechanical units.
- Rumble legend grants Steadfast to friendly mechanical defenders through the same `FRIENDLY_FILTERED_UNITS_KEYWORD` path, replacing the old Rumble-specific steadfast branch.
- `FullGameEndToEndTests.OfficialDeckMidgameAppliesRumbleLegendFriendlyMechanicalSteadfastAndScoreVictoryActionLogReplaysToFinalStateHash` now carries that Rumble legend Steadfast grant through legal official deck submission/opening, focused midgame battle declaration, real defender damage, score victory, and action-log replay.
- Lillia grants Bulwark to friendly unit-token objects; Core and prompt legality treat the dynamic Bulwark as a battle-damage-assignment keyword for multi-defender declarations.
- `SFD·071/221` grants two keyword specs from one official sentence; dynamic Roam is visible in move prompts and accepted by Core precise battlefield movement without a printed Roam tag.
- `SFD·071/221` dynamic Spellshield contributes one mana of enemy spell target tax in both prompt legality and Core payment.
- `SFD·065/221` parses and projects static-granted `预知` as RULE_TEXT; when the public source already grants `预知` to a later-played matching friendly mechanical unit, prompts and Core stack resolution now reuse the shared top-1 optional main-deck recycle path.
- Printed/source-tag `预知` permanents now receive a generic lifecycle default in `CardBehaviorRegistry`: if no explicit look/target model exists, the shared engine path exposes only the controller's top main-deck card as an optional recycle target. `OGN·100/298` Gemstone Seer is the representative runtime fixture.
- Face-down source and target guards: a face-down friendly-filtered keyword source is not projected and grants no combat keyword bonus, and a face-down matching friendly target is not emitted as a RULE_TEXT continuous-effect target.

## Not Closed

This is a representative friendly-filtered keyword aura slice only.

Still open:

- Complete keyword removal and later-layer loss effects.
- Complete static-granted `预知` breadth outside the covered public-source / later-played matching mechanical unit path, including simultaneous self-grant questions and broader Predict trigger sequencing.
- Complete non-combat keyword grants outside the covered `SFD·065/221` representative and `SFD·071/221` Spellshield/Roam representatives.
- Complete Rumble conquer recycle branch and graveyard mechanical play / cost-reduction branch.
- Complete Lillia token-play temporary power trigger.
- FU-level matrix blocker reduction / fullOfficial status for the covered cards.
- READY.

## Rule Authority

- Official catalog: `data/official/card-catalog.zh-CN.json`.
- Official text:
  - `SFD·026/221` / `SFD·026a/221`: `你的“机械”属性单位获得{{强攻}}。`
  - `SFD·065/221`: `你的“机械”属性单位获得{{预知}}。`
  - `SFD·071/221`: `你的“机械”属性单位获得{{法盾}}和{{游走}}。`
  - `SFD·181/221` / `SFD·240/221`: `你的“机械”属性单位获得{{坚守}}。`
  - `UNL-058/219` / `UNL-058a/219`: `你的指示物单位获得{{壁垒}}。`
- `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79FriendlyFilteredStaticKeywordGrantsKeywordsToMatchingFriendlyUnits|FullyQualifiedName~P79FriendlyFilteredStaticKeywordBulwarkSupportsMultiDefenderAssignment|FullyQualifiedName~P79LegendStaticRumbleGrantsSteadfastToMechanicalDefender"
```

Result: 4/4 passed.

Additional focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79FriendlyFilteredStaticKeywordGrantsMultipleNonCombatKeywordsToMatchingFriendlyUnits"
```

Result: 2/2 passed.

Source-tag Predict lifecycle focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GemstoneSeerPredictPrompt|FullyQualifiedName~CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard"
```

Result: 8/8 passed.

Static-granted Predict lifecycle focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PrescientMechStaticGrantedPredict|FullyQualifiedName~CoreRuleEnginePlaysStaticGrantedPredict"
```

Result: 2/2 passed.

Source-tag Predict adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Predict|FullyQualifiedName~Gemstone|FullyQualifiedName~Lifecycle"
```

Result: 112/112 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~Roam|FullyQualifiedName~Spellshield"
```

Result: 313/313 passed.

2026-06-26 hidden-boundary focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79FriendlyFilteredStaticKeywordGrantDoesNotProject" --nologo
```

Result: 2/2 passed.

2026-06-26 hidden-boundary adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Roam|FullyQualifiedName~Spellshield|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2238/2238 passed.

2026-06-26 official-deck replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RumbleLegendFriendlyMechanicalSteadfast" --nologo
```

Result: 1/1 passed.

2026-06-26 full-game replay class:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 40/40 passed.

2026-06-26 official-deck replay adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RumbleLegendFriendlyMechanicalSteadfast|FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Rumble|FullyQualifiedName~FullGameEndToEndTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2112/2112 passed.

Hidden-information / recovery:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8592/8592 passed.

2026-06-26 backend full after hidden-boundary guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8617/8617 passed.

2026-06-26 backend full after Rumble legend official-deck replay:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8734/8734 passed.
