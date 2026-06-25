# Plan B Friendly-Filtered Static Keyword Aura Spec Audit

更新时间：2026-06-25

## Scope

This slice advances Plan B / B2 RULE_TEXT static keyword aura coverage without adding card-number runtime branches.

Covered representatives:

- `SFD·026/221` / `SFD·026a/221` 兰博：`你的“机械”属性单位获得{{强攻}}。`
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
- `GrantedKeyword=强攻` / `坚守` / `壁垒`

Runtime evidence now covers:

- `MatchSession` continuous-effect projection produces RULE_TEXT object effects from public-field unit sources and legend-zone sources to matching friendly public units.
- Rumble hero grants Assault to friendly mechanical units and excludes non-mechanical friendly units plus opposing mechanical units.
- Rumble legend grants Steadfast to friendly mechanical defenders through the same `FRIENDLY_FILTERED_UNITS_KEYWORD` path, replacing the old Rumble-specific steadfast branch.
- Lillia grants Bulwark to friendly unit-token objects; Core and prompt legality treat the dynamic Bulwark as a battle-damage-assignment keyword for multi-defender declarations.

## Not Closed

This is a representative friendly-filtered keyword aura slice only.

Still open:

- Complete keyword removal and later-layer loss effects.
- Complete non-combat keyword grants such as prediction / spellshield / roam variants outside this representative combat path.
- Complete Rumble conquer recycle branch and graveyard mechanical play / cost-reduction branch.
- Complete Lillia token-play temporary power trigger.
- FU-level matrix blocker reduction / fullOfficial status for the covered cards.
- READY.

## Rule Authority

- Official catalog: `data/official/card-catalog.zh-CN.json`.
- Official text:
  - `SFD·026/221` / `SFD·026a/221`: `你的“机械”属性单位获得{{强攻}}。`
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

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FriendlyFiltered|FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~BattleDamageAssignment|FullyQualifiedName~MatchRecovery"
```

Result: 2085/2085 passed.

Hidden-information / recovery:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8587/8587 passed.
