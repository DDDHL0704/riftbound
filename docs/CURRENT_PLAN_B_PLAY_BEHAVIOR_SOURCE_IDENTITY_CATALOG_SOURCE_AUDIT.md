# Plan B Play Behavior Source Identity Catalog Source Audit

日期：2026-06-26
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文件记录 Plan B 小切片：把 Raging Drake / 狂暴龙怪、Poro Herder / 魄罗牧者、Balanced Disciple / 均衡门徒、Crescent Guard / 新月禁卫、Ascended Believer / 晋升信徒、Sly Salamander / 狡猾的蝾螈、Rampaging Soul / 肆虐狂魂、Armed Assaulter / 武装强袭者、Akshan / 阿克尚的 play-behavior 来源身份，从引擎直接 cardNo 分支迁移到 catalog effect kind 分支。该切片只收窄当前 play resolution / optional-cost / conditional-entry representative source identity 硬编码；不关闭完整 play-trigger family、完整 PaymentEngine breadth、完整 LayerEngine breadth、frontend final validation 或 READY。

## Scope

Changed:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/PlayBehaviorSourceIdentityGuardTests.cs`
- `docs/CURRENT_PLAN_B_PLAY_BEHAVIOR_SOURCE_IDENTITY_CATALOG_SOURCE_AUDIT.md`
- `docs/CURRENT_PLAN_B_PLAY_BEHAVIOR_SOURCE_IDENTITY_CATALOG_SOURCE_EVIDENCE.md`
- `docs/rules-evidence-index.md`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`

Not changed:

- official card catalog JSON
- card matrix JSON
- protocol shape
- next-spell cost-reduction semantics
- frontend runtime
- `fullOfficial` / READY status

## Acceptance Review

| Requirement | Evidence | Verdict |
|---|---|---|
| Raging Drake play resolution source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT` instead of `behavior.CardNo == RagingDrakeCardNo` | Accepted |
| Poro Herder play resolution source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT` instead of `behavior.CardNo == PoroHerderCardNo` | Accepted |
| Balanced Disciple play resolution source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT` instead of `behavior.CardNo == BalancedDiscipleCardNo` | Accepted |
| Crescent Guard ready optional-cost source identity no longer directly selects by card number | `CoreRuleEngine` and `MatchSession` now check `behavior.EffectKind == CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT` instead of `behavior.CardNo == CrescentGuardCardNo` | Accepted |
| Ascended Believer conditional power source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == ASCENDED_BELIEVER_NO_SPELL_VANILLA_PLAY_UNIT` instead of `behavior.CardNo == AscendedBelieverCardNo` | Accepted |
| Sly Salamander conditional power / keyword source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == SLY_SALAMANDER_NO_EXPERIENCE_VANILLA_PLAY_UNIT` instead of `behavior.CardNo == SlySalamanderCardNo` | Accepted |
| Rampaging Soul conditional keyword source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == RAMPAGING_SOUL_NO_DISCARD_SPIRIT_PLAY_UNIT` instead of `behavior.CardNo == RampagingSoulCardNo` | Accepted |
| Armed Assaulter haste / tempered source identity no longer directly selects by card number | `CoreRuleEngine` now checks `behavior.EffectKind == ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE` instead of `behavior.CardNo == ArmedAssaulterCardNo` | Accepted |
| Akshan orange-extra source identity no longer directly selects by card number | `CoreRuleEngine` and `MatchSession` now check `behavior.EffectKind == AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT` instead of `behavior.CardNo == AkshanCardNo` for representative source selection | Accepted |
| Existing next-spell marker semantics are preserved | the branch still creates `RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:<playerId>:<sourceObjectId>` and emits `TRIGGER_RESOLVED.effectKind=RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION` | Accepted |
| Existing Poro Herder semantics are preserved | the branch still requires a controlled face-up Poro unit, grants boon to the source, and draws 1 | Accepted |
| Existing Balanced Disciple semantics are preserved | the branch still requires other controlled unit power total at least 5 and draws 1 | Accepted |
| Existing Crescent Guard semantics are preserved | the branch still requires `PlayerPlayedSpellThisTurn`, still prompts only when service-side purple payment resources are available, and still consumes `SPEND_POWER:purple:1` for ready entry | Accepted |
| Existing conditional entry semantics are preserved | Ascended Believer still requires `PlayerPlayedFourPlusCostSpellThisTurn` for +4 power; Sly Salamander still requires gained-experience memory for +1 power and roam; Rampaging Soul still requires discarded-hand memory for assault and roam | Accepted |
| Existing optional-cost representative semantics are preserved | Armed Assaulter still requires the haste / tempered optional-cost branch; Akshan still requires legal enemy equipment and orange power before exposing / resolving the steal option | Accepted |
| Registry identity is exact enough to reject wrong rows | `PlayBehaviorSourceIdentityGuardTests` accepts `OGN·031/298`, `OGN·061/298`, `UNL-097/219`, `UNL-122/219`, `UNL-004/219`, `UNL-108/219`, `OGN·019/298`, `SFD·002/221`, and `SFD·109/221` only for their matching source effect kinds and rejects wrong-card / wrong-effect examples | Accepted |
| Existing representative behavior is preserved | Raging Drake, Poro Herder, Balanced Disciple, Crescent Guard, Ascended Believer, Sly Salamander, Rampaging Soul, Armed Assaulter, and Akshan focused regression and adjacent play / recovery regression remain green | Accepted |
| Full play-trigger family breadth | complete play-trigger routing, complete PaymentEngine breadth, and complete LayerEngine breadth remain residual | Residual, no READY claim |

## Verification

Focused source identity guard:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests" --nologo
```

Initial results before implementation: failed on `RagingDrakeCardNo` in the first slice, then failed on `PoroHerderCardNo` and `BalancedDiscipleCardNo` in the follow-up slice, then failed on `CrescentGuardCardNo` in the Crescent Guard slice, then failed on `AscendedBelieverCardNo` / `SlySalamanderCardNo` / `RampagingSoulCardNo` in the conditional-entry slice, then failed on `ArmedAssaulterCardNo` / `AkshanCardNo` behavior-source comparisons in the optional-cost representative slice.

Focused play-behavior source identity and representative behavior:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulter" --nologo
```

Result after implementation: 87/87 passed.

Adjacent play-behavior / recovery regression:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Akshan|FullyQualifiedName~ArmedAssaulter|FullyQualifiedName~PlayBehaviorSourceIdentityGuardTests|FullyQualifiedName~PaymentEngineCoverageAuditTests|FullyQualifiedName~ConformanceFixtureShapeTests|FullyQualifiedName~MatchRecovery" --nologo
```

Result: 2916/2916 passed.

Hidden-info / recovery boundary:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo
```

Result: 1989/1989 passed.

Full backend conformance:

```sh
/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo
```

Result: 8700/8700 passed.

## Residual Risks

- This does not generalize every play-trigger representative; it removes the current Raging Drake, Poro Herder, Balanced Disciple, Crescent Guard, Ascended Believer, Sly Salamander, Rampaging Soul, Armed Assaulter, and Akshan source card-number dependencies and routes those branches through implemented catalog effect kinds.
- This does not close complete play-trigger ordering, complete `ORDER_TRIGGERS` / APNAP breadth, complete PaymentEngine breadth, complete LayerEngine breadth, frontend final validation, or READY.
- Project remains **NOT READY**.
