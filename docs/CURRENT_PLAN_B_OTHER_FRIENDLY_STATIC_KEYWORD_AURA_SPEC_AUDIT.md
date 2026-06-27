# Plan B Other-Friendly Static Keyword Aura Spec Audit

更新时间：2026-06-27

## Scope

This slice advances Plan B / B2 RULE_TEXT static keyword aura coverage for global other-friendly unit grants without adding card-number runtime branches.

Covered representative:

- `OGN·100/298` 宝石真知者：`其他友方单位获得{{预知}}。`

The official text parses into:

- `StaticAuraSpec.Kind=OTHER_FRIENDLY_UNITS_KEYWORD`
- `Layer=RULE_TEXT`
- `Duration=WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD`
- `TargetScope=OTHER_FRIENDLY_UNITS`
- `ParticipantScope=OTHER_FRIENDLY_PUBLIC_UNITS`
- `PowerDeltaPerParticipant=0`
- `GrantedKeyword=预知`

Runtime evidence now covers:

- `RuleTextParsers.StaticAuraParser` parses `其他友方单位获得{{...}}` keyword grants into `OTHER_FRIENDLY_UNITS_KEYWORD`, while preserving the existing `其他友方单位获得{{S}}+N` power-aura route.
- `MatchSession.BuildOtherFriendlyUnitsKeywordAuraEffects` projects RULE_TEXT continuous effects from public-field unit sources to other friendly public-field units, excluding the source object itself.
- Play-card prompts use the same public-source static grant check to expose the `预知` top-card optional recycle target for a later-played other friendly unit.
- `CoreRuleEngine.ApplyStaticGrantedPredictLifecycleDefault` now reads both friendly-filtered and other-friendly static keyword grants before building the stack item.
- `CoreRuleEngine` combat/resource keyword resolution also reads `OTHER_FRIENDLY_UNITS_KEYWORD`, so later other-friendly Assault / Steadfast / Roam / Spellshield-style grants use the same shared path.
- The representative runtime path plays a non-printed-Predict other friendly unit under Gemstone Seer, resolves it to base, recycles the selected top main-deck card, and then projects the RULE_TEXT aura to the now-public unit.

## Not Closed

This is a representative other-friendly keyword aura slice only.

Still open:

- Complete keyword removal and later-layer loss effects.
- Static-granted `预知` simultaneous-entry and self-grant edge cases.
- Other-friendly keyword breadth beyond the covered `预知` representative.
- Gemstone Seer official-deck full-game route.
- READY.

## Rule Authority

- Official catalog: `data/official/card-catalog.zh-CN.json`.
- Official text:
  - `OGN·100/298` 宝石真知者：`{{预知}}。其他友方单位获得{{预知}}。`
- `CORE-260330` p4-p8 rules 107-129; p14-p15 rules 142-143; p39-p42 rules 355-356; p58-p59 rule 416; p92-p105 keyword rules 800+.
- Rule authority protocol: `docs/rules-authority-and-audit.md`.

## Validation

Focused:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~GemstoneSeerStaticGrantedPredict|FullyQualifiedName~OtherFriendlyStaticGrantedPredict|FullyQualifiedName~BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives" --nologo
```

Result: 3/3 passed.

Adjacent:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Gemstone|FullyQualifiedName~OtherFriendly|FullyQualifiedName~StaticKeyword|FullyQualifiedName~StaticAura|FullyQualifiedName~Predict|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2119/2119 passed.

Backend full conformance:

```bash
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8805/8805 passed.
