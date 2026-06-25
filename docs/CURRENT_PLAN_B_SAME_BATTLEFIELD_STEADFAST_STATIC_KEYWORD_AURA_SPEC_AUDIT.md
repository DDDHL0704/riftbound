# Plan B Same-Battlefield Steadfast Static Keyword Aura Spec Audit

更新时间：2026-06-26

## Scope

This slice advances Plan B / B2 RULE_TEXT static keyword aura coverage without adding new card-number runtime logic.

Covered representative:

- `OGN·074/298` 塔里克：`此处的其他友方单位获得{{坚守}}。`

The official text parses into:

- `StaticAuraSpec.Kind=SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD`
- `Layer=RULE_TEXT`
- `Duration=WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD`
- `TargetScope=SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS`
- `ParticipantScope=SAME_BATTLEFIELD_OTHER_FRIENDLY_PUBLIC_UNITS`
- `GrantedKeyword=坚守`

Runtime evidence now covers:

- `MatchSession` continuous-effect projection produces a RULE_TEXT object effect from visible Taric to the other friendly unit at the same battlefield.
- The projection excludes Taric itself, enemy units at the same battlefield, and friendly units at a different battlefield.
- `CoreRuleEngine` reads the same `BehaviorSpec` static aura during `DECLARE_BATTLE`; a defending friendly target receives `keyword=坚守`, `keywordBonus=1`, and adjusted combat power.
- The opposing attacker keeps `keywordBonus=0` and receives no static power bonus.
- Source-leaves lifecycle guard: when Taric is no longer on the field, the RULE_TEXT continuous effect is absent and the formerly granted defender recomputes to `keywordBonus=0` / base combat power.
- Target-moves lifecycle guard: when the friendly target moves to another battlefield while Taric remains at the original battlefield, the RULE_TEXT continuous effect is absent and the target recomputes without the granted `坚守`.
- Controller-scope guard: `other friendly` is based on the source object's current controller; a controlled Farron Captain grants `强攻` to the controller's same-battlefield attacker, not to the owner's unit.

## Not Closed

This is a representative same-battlefield static keyword aura slice only.

Still open:

- Full `坚守` keyword family breadth beyond the representative combat bonus path.
- Full `壁垒` damage-assignment ordering for Taric.
- Complete RULE_TEXT keyword grant scopes and broader keyword removal/loss layering beyond this same-battlefield source-leaves representative.
- Complete battle / spell-duel lifecycle and assignment prompt breadth.
- FU-level matrix blocker reduction / fullOfficial status for `OGN·074/298`.
- READY.

## Rule Authority

- Official catalog: `data/official/card-catalog.zh-CN.json`, `OGN·074/298` 塔里克.
- Official text: `{{坚守}}（如果我是防守方，则{{S}}+1。）`, `{{壁垒}}（我在战斗中首先承担伤害。）`, `此处的其他友方单位获得{{坚守}}。`
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

Hidden-information / recovery:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"
```

Result: 1989/1989 passed.

Backend full:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj
```

Result: 8532/8532 passed.
