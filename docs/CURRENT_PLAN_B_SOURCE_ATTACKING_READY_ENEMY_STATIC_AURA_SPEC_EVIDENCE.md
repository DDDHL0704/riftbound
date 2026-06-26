# Plan B / Source Attacking Ready Enemy Static Aura Spec Evidence

Date: 2026-06-26

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·131/298` 沙丘亚龙 official text is `当我进攻时，如果此处有处于活跃状态的敌方单位，则让我{{S}}+2。`
- `docs/CURRENT_SERVER_RULE_AUDIT.md`: existing P1-002/P1-004 Dune Drake ready-enemy combat-power representative evidence.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog and local evidence-index entries remain the authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesStaticAuraSpecsForExistingRepresentatives` verifies Dune Drake parses to `StaticAuraSpec.Kind = SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER`, `Layer = STATIC_AURA`, `TargetScope = SOURCE_OBJECT`, `ParticipantScope = READY_ENEMY_BATTLEFIELD_PUBLIC_UNITS`, `PowerDeltaPerParticipant = 2`, and `RequiredReadyEnemyUnitCount = 1`.
- `StaticAuraProjectionDoesNotUseMatchSessionCardNumberAllowList` now also verifies `CoreRuleEngine` no longer contains `DuneDrakeCardNo` or `OGN·131/298`.
- `P79DuneDrakeGainsPowerWhenAttackingReadyEnemyUnit` and `P79DuneDrakeSkipsPowerWhenDefending` keep the existing combat-power runtime representative green.
- `OfficialDeckMidgameAppliesDuneDrakeSourceAttackingReadyEnemyStaticAuraAndScoreVictoryActionLogReplaysToFinalStateHash` proves the same BehaviorSpec route survives a legal official-deck full-game path.

## Runtime Evidence

- `StaticAuraSpecRules.TryGetSourceAttackingReadyEnemyUnitPowerAura(...)` exposes source-attacking-ready-enemy combat static auras from `BehaviorSpecCatalogBuilder`.
- `CoreRuleEngine.ResolveSourceAttackingReadyEnemyUnitPowerBonus(...)` reads the ready-enemy threshold and power delta from `StaticAuraSpec` instead of selecting by Dune Drake card number.
- `FullGameEndToEndTests` now stages official `OGN·131/298` Dune Drake and an opposing ready defender through server-authored prompts, declares battle with Dune Drake as the sole attacker, verifies `SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER` projection metadata and participant dependency, and observes basePower 5 + staticPowerBonus 2 = combatPower/damage 7 before score-victory replay.
- `src/Riftbound.DevUi/src/types/catalog.ts` now includes `staticAuras[].requiredReadyEnemyUnitCount` so catalog consumers can read the new spec field.

## Validation

- Focused official-deck replay: `OfficialDeckMidgameAppliesDuneDrakeSourceAttackingReadyEnemyStaticAura...` `1/1` passing.
- FullGameEndToEnd cross-slice: `35/35` passing.
- DuneDrake / SourceAttackingReadyEnemy / SourceCombat / StaticAura / StaticPower / ContinuousEffect / FullGameEndToEnd / MatchRecovery adjacent representatives: `2114/2114` passing.
- Backend full conformance: `8727/8727` passing.

## Residual Risk

- This slice proves one source-attacking-ready-enemy combat static aura has moved to BehaviorSpec-driven routing and now has a legal official-deck full-game replay. It does not prove complete combat conditional power, complete battle / spell-duel lifecycle, assignment prompt breadth, frontend smoke, or READY.
