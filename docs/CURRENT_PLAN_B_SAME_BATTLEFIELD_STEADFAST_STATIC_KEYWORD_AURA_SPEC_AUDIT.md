# Plan B Same-Battlefield Steadfast Static Keyword Aura Spec Audit

更新时间：2026-06-26

## Scope

This slice advances Plan B / B2 RULE_TEXT static keyword aura coverage without adding new card-number runtime logic.

Covered representative:

- `OGN·074/298` 塔里克：`此处的其他友方单位获得{{坚守}}。`
- `OGN·015/298` 法荣队长：`此处的其他友方单位获得{{强攻}}。（如果他们是进攻方，则{{S}}+1。）`

The official text parses into:

- `StaticAuraSpec.Kind=SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD`
- `Layer=RULE_TEXT`
- `Duration=WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD`
- `TargetScope=SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS`
- `ParticipantScope=SAME_BATTLEFIELD_OTHER_FRIENDLY_PUBLIC_UNITS`
- `GrantedKeyword=坚守` for Taric, `GrantedKeyword=强攻` for Farron Captain

Runtime evidence now covers:

- `MatchSession` continuous-effect projection produces a RULE_TEXT object effect from visible Taric to the other friendly unit at the same battlefield.
- The projection excludes Taric itself, enemy units at the same battlefield, and friendly units at a different battlefield.
- `CoreRuleEngine` reads the same `BehaviorSpec` static aura during `DECLARE_BATTLE`; a defending friendly target receives `keyword=坚守`, `keywordBonus=1`, and adjusted combat power.
- The opposing attacker keeps `keywordBonus=0` and receives no static power bonus.
- Source-leaves lifecycle guard: when Taric is no longer on the field, the RULE_TEXT continuous effect is absent and the formerly granted defender recomputes to `keywordBonus=0` / base combat power.
- Target-moves lifecycle guard: when the friendly target moves to another battlefield while Taric remains at the original battlefield, the RULE_TEXT continuous effect is absent and the target recomputes without the granted `坚守`.
- Controller-scope guard: `other friendly` is based on the source object's current controller; a controlled Farron Captain grants `强攻` to the controller's same-battlefield attacker, not to the owner's unit.
- Official-deck full-game replay: legal official Jhin deck prompts stage Farron Captain and Ascended Believer to the same battlefield, project a `RULE_TEXT` object effect ending in `:强攻`, resolve real battle damage with `basePower=1`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=2`, and `damage=2`, then score-victory action-log replay reaches the same final state hash.
- Official-deck defensive replay: legal official Lillia deck prompts stage Taric and LeBlanc to an opposing battlefield, project a `RULE_TEXT` object effect ending in `:坚守`, resolve defender battle damage with `basePower=4`, `keywordBonus=1`, no `staticPowerBonus`, `combatPower=5`, and `damage=5`, then score-victory action-log replay reaches the same final state hash after a follow-up battle declaration and no-legal `BATTLE_SKIPPED`.
- Face-down source guard: a face-down source does not project RULE_TEXT static keyword effects or grant combat keyword bonuses, preserving hidden-information boundaries.
- Face-down target guard: a face-down same-battlefield friendly unit is not emitted as a RULE_TEXT continuous-effect target, preventing hidden target dependency leakage.
- Standby source guard: a standby source does not project RULE_TEXT static keyword effects or grant combat keyword bonuses from the same-battlefield other-friendly aura path.

## Not Closed

This is a representative same-battlefield static keyword aura slice only.

Still open:

- Full `坚守` keyword family breadth beyond the representative combat bonus path.
- Full `壁垒` damage-assignment ordering for Taric.
- Complete RULE_TEXT keyword grant scopes and broader keyword removal/loss layering beyond this same-battlefield represented scope.
- Farron same-battlefield `强攻` and Taric same-battlefield `坚守` now have legal official-deck full-game replay representatives, but broader RULE_TEXT keyword grant scopes and other official deck archetypes remain open.
- Complete standby / face-down identity matrix beyond the covered same-battlefield source and target representatives.
- Complete battle / spell-duel lifecycle and assignment prompt breadth.
- FU-level matrix blocker reduction / fullOfficial status for `OGN·074/298`.
- READY.

## Rule Authority

- Official catalog: `data/official/card-catalog.zh-CN.json`, `OGN·074/298` 塔里克.
- Official text: `{{坚守}}（如果我是防守方，则{{S}}+1。）`, `{{壁垒}}（我在战斗中首先承担伤害。）`, `此处的其他友方单位获得{{坚守}}。`
- Official catalog: `data/official/card-catalog.zh-CN.json`, `OGN·015/298` 法荣队长.
- Official text: `此处的其他友方单位获得{{强攻}}。（如果他们是进攻方，则{{S}}+1。）`
- `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p92-p105 keyword rules 800+.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives|FullyQualifiedName~P79SameBattlefieldOtherFriendlyStaticKeywordGrantsSteadfastToOnlyOtherFriendlyDefenders"
```

Result: 2/2 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticPower|FullyQualifiedName~ContinuousEffect|FullyQualifiedName~Steadfast|FullyQualifiedName~Taric|FullyQualifiedName~DeclareBattle|FullyQualifiedName~FullGameEndToEnd"
```

Result: 543/543 passed.

2026-06-26 source-leaves lifecycle focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantExpiresWhenSourceLeavesBattlefield" --nologo
```

Result: 1/1 passed.

2026-06-26 source-leaves lifecycle adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Taric|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2063/2063 passed.

2026-06-26 backend full after lifecycle guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8611/8611 passed.

2026-06-26 target-moves lifecycle focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantExpiresWhenSourceLeavesBattlefield|FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantExpiresWhenTargetMovesToAnotherBattlefield" --nologo
```

Result: 2/2 passed.

2026-06-26 target-moves lifecycle adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Taric|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2064/2064 passed.

2026-06-26 backend full after target-moves guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8612/8612 passed.

2026-06-26 controller-scope focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantUsesCurrentControllerForFriendlyScope" --nologo
```

Result: 1/1 passed.

2026-06-26 controller-scope adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Farron|FullyQualifiedName~Control|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2271/2271 passed.

2026-06-26 backend full after controller-scope guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8613/8613 passed.

2026-06-26 face-down source focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromFaceDownSource" --nologo
```

Result: 1/1 passed.

2026-06-26 face-down source adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Taric|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2163/2163 passed.

2026-06-26 backend full after face-down source guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8614/8614 passed.

2026-06-26 face-down target focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantDoesNotProjectToFaceDownTarget" --nologo
```

Result: 1/1 passed.

2026-06-26 face-down target adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Taric|FullyQualifiedName~Hidden|FullyQualifiedName~FaceDown|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2164/2164 passed.

2026-06-26 backend full after face-down target guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8615/8615 passed.

2026-06-26 standby source focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~P79SameBattlefieldStaticKeywordGrantDoesNotProjectFromStandbySource" --nologo
```

Result: 1/1 passed.

2026-06-26 same-battlefield static keyword adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefieldStaticKeyword|FullyQualifiedName~SameBattlefieldOtherFriendlyStaticKeyword|FullyQualifiedName~StaticKeywordGrant" --nologo
```

Result: 15/15 passed.

2026-06-26 static-aura / static-keyword / recovery adjacent after standby source guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~StaticAura|FullyQualifiedName~StaticKeyword|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2050/2050 passed.

2026-06-26 backend full after standby source guard:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8629/8629 passed.

2026-06-26 official-deck Farron replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSameBattlefieldStaticKeyword" --nologo
```

Result: 1/1 passed.

2026-06-26 official-deck Farron replay FullGameEndToEnd:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 22/22 passed.

2026-06-26 official-deck Farron replay adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Farron|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2090/2090 passed.

2026-06-26 official-deck Taric replay focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~OfficialDeckMidgameAppliesSameBattlefieldSteadfastStaticKeyword" --nologo
```

Result: 1/1 passed.

2026-06-26 official-deck Taric replay FullGameEndToEnd:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~FullGameEndToEndTests" --nologo
```

Result: 23/23 passed.

2026-06-26 official-deck Taric replay adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~SameBattlefield|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Steadfast|FullyQualifiedName~Taric|FullyQualifiedName~Farron|FullyQualifiedName~FullGameEndToEnd|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2105/2105 passed.

Hidden-information / recovery:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8715/8715 passed.
