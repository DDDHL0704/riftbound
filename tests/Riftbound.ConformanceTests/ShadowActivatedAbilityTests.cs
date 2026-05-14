using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ShadowActivatedAbilityTests
{
    private const string BattlefieldObjectId = "BF-SHADOW";
    private const string OtherBattlefieldObjectId = "BF-OTHER";
    private const string ShadowObjectId = "P1-SHADOW";
    private const string EnemyAttackerObjectId = "P2-ATTACKER";
    private const string EnemySpellshieldAttackerObjectId = "P2-SPELLSHIELD-ATTACKER";
    private const string EnemyDefenderObjectId = "P2-DEFENDER";
    private const string EnemyNonAttackerObjectId = "P2-NON-ATTACKER";
    private const string EnemyWrongBattlefieldAttackerObjectId = "P2-WRONG-BF-ATTACKER";
    private const string FriendlyAttackerObjectId = "P1-FRIENDLY-ATTACKER";
    private const string EnemyBaseUnitObjectId = "P2-BASE-UNIT";
    private const string BlueRuneObjectId = "P1-RUNE-BLUE";

    [Fact]
    public void ShadowBattleResponsePromptExposesSwiftStunRequirement()
    {
        var state = BuildShadowState(mana: 2, power: 1);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([ShadowObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                StringComparison.Ordinal));

        Assert.Equal(ShadowObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowCardNo, requirement["cardNo"]);
        Assert.Equal(1, requirement["manaCost"]);
        Assert.Equal(1, requirement["powerCost"]);
        Assert.Equal(0, requirement["experienceCost"]);
        Assert.Equal(1, requirement["minTargetCount"]);
        Assert.Equal(1, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.True(Assert.IsType<bool>(requirement["requiresBattlefieldSource"]));
        Assert.True(Assert.IsType<bool>(requirement["swift"]));
        Assert.True(Assert.IsType<bool>(requirement["appliesSpellshieldTargetTax"]));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal("enemy-attacking-unit-at-this-battlefield", requirement["targetScope"]);
        Assert.Equal("STUNNED", requirement["statusEffectId"]);
        Assert.Equal("battle-response-priority-representative", requirement["timingPolicy"]);
        Assert.Equal("ordinary-stack-item-before-stun", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-mana-generic-power-spellshield-tax-exhaust-as-cost", requirement["paymentPolicy"]);

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        var targetIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targetChoicesByIndex["0"])
            .Select(choice => choice.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([EnemyAttackerObjectId, EnemySpellshieldAttackerObjectId], targetIds);
        var spellshieldTaxByTarget = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            requirement["spellshieldTaxManaByTargetObjectId"]);
        Assert.Equal(0, spellshieldTaxByTarget[EnemyAttackerObjectId]);
        Assert.Equal(1, spellshieldTaxByTarget[EnemySpellshieldAttackerObjectId]);
    }

    [Fact]
    public async Task NaturalStartBattleOpensBattleResponsePriorityAndExposesShadowPrompt()
    {
        var opened = await OpenNaturalShadowBattleResponseAsync(mana: 1, power: 1);

        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, opened.State.TimingState);
        Assert.Equal("P1", opened.State.PriorityPlayerId);
        Assert.True(opened.State.BattleState.IsActive);
        Assert.Equal(BattlefieldObjectId, opened.State.BattleState.BattlefieldObjectId);
        Assert.Equal([EnemyAttackerObjectId], opened.State.BattleState.AttackerObjectIds);
        Assert.Equal([FriendlyAttackerObjectId], opened.State.BattleState.DefenderObjectIds);
        Assert.True(opened.State.CardObjects[EnemyAttackerObjectId].IsAttacking);
        Assert.True(opened.State.CardObjects[FriendlyAttackerObjectId].IsDefending);
        Assert.False(opened.State.CardObjects[ShadowObjectId].IsDefending);
        Assert.Contains(opened.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(opened.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(opened.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        var p1Prompt = opened.Prompts["P1"];
        Assert.Equal(PromptTypes.StackPriority, p1Prompt.View?.Type);
        Assert.Equal(BattlefieldObjectId, p1Prompt.View?.RelatedBattlefieldId);
        Assert.Equal($"battle:{BattlefieldObjectId}", p1Prompt.View?.RelatedBattleId);
        Assert.Contains(CommandTypes.ActivateAbility, p1Prompt.Actions);

        var activateCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Equal(ShadowObjectId, requirement["sourceObjectId"]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Equal(
            [EnemyAttackerObjectId],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targetChoicesByIndex["0"])
                .Select(choice => choice.Id)
                .ToArray());

        Assert.DoesNotContain(CommandTypes.ActivateAbility, opened.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task ShadowActivatesAndResolvesFromNaturalBattleResponseWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await OpenNaturalShadowBattleResponseAsync(mana: 1, power: 1, engine: engine);
        Assert.True(opened.Accepted, opened.ErrorMessage);

        var activated = await ActivateShadowAsync(opened.State, engine: engine);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], activated.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(activated.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Single(activated.State.StackItems);
        Assert.DoesNotContain("STUNNED", activated.State.CardObjects[EnemyAttackerObjectId].UntilEndOfTurnEffects);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-shadow-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-natural-shadow-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.True(p2Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, p2Pass.State.TimingState);
        Assert.Equal("P1", p2Pass.State.PriorityPlayerId);
        Assert.True(p2Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Contains("STUNNED", p2Pass.State.CardObjects[EnemyAttackerObjectId].UntilEndOfTurnEffects);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));

        var responseP1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-shadow-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);

        var responseP2Pass = await engine.ResolveAsync(
            responseP1Pass.State,
            new PlayerIntent("intent-natural-shadow-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        Assert.False(responseP2Pass.State.BattleState.IsActive);
        Assert.Empty(responseP2Pass.State.StackItems);
        Assert.Contains(responseP2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-player")]
    [InlineData("wrong-battlefield-target")]
    [InlineData("stale-target")]
    public async Task ShadowNaturalBattleResponseRejectsWrongPlayerBattlefieldOrStaleTargetWithoutMutation(string scenario)
    {
        var opened = await OpenNaturalShadowBattleResponseAsync(mana: 1, power: 1);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var state = opened.State;
        var playerId = "P1";
        var command = ShadowCommand();

        if (string.Equals(scenario, "wrong-player", StringComparison.Ordinal))
        {
            playerId = "P2";
        }
        else if (string.Equals(scenario, "wrong-battlefield-target", StringComparison.Ordinal))
        {
            state = state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EnemyWrongBattlefieldAttackerObjectId,
                    state.CardObjects[EnemyWrongBattlefieldAttackerObjectId] with { IsAttacking = true })
            };
            command = ShadowCommand(targetObjectIds: [EnemyWrongBattlefieldAttackerObjectId]);
        }
        else if (string.Equals(scenario, "stale-target", StringComparison.Ordinal))
        {
            state = state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EnemyAttackerObjectId,
                    state.CardObjects[EnemyAttackerObjectId] with { IsAttacking = false })
            };
        }

        await AssertRejectedNoMutationAsync(state, command, playerId);
    }

    [Fact]
    public async Task NaturalBattleResponseReconnectSnapshotExposesBattleContextWithoutHiddenLeakage()
    {
        var opened = await OpenNaturalShadowBattleResponseAsync(mana: 1, power: 1);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var hiddenObjectId = "P2-HIDDEN-STANDBY";
        var hiddenState = opened.State with
        {
            CardObjects = ReplaceCardObject(
                ReplaceCardObject(
                    opened.State.CardObjects,
                    OtherBattlefieldObjectId,
                    opened.State.CardObjects[OtherBattlefieldObjectId] with
                    {
                        OwnerId = "P2",
                        ControllerId = "P2"
                    }),
                hiddenObjectId,
                new CardObjectState(
                    hiddenObjectId,
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2")),
            PlayerZones = ReplacePlayerZones(
                opened.State.PlayerZones,
                "P2",
                opened.State.PlayerZones["P2"] with
                {
                    Battlefields = opened.State.PlayerZones["P2"].Battlefields
                        .Concat([OtherBattlefieldObjectId, hiddenObjectId])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                }),
            ObjectLocations = ReplaceObjectLocation(
                opened.State.ObjectLocations,
                hiddenObjectId,
                new ObjectLocationState("P2", "BATTLEFIELD", OtherBattlefieldObjectId))
        };
        var session = new MatchSession(hiddenState, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1 = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P1", p1.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", Assert.IsType<string>(queue["activeTaskId"]));
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        Assert.Equal(BattlefieldObjectId, prompt.View?.RelatedBattlefieldId);
        Assert.Equal($"battle:{BattlefieldObjectId}", prompt.View?.RelatedBattleId);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        var activeTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "ACTIVE", StringComparison.Ordinal)
            && string.Equals(task["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        Assert.Equal($"battle:{BattlefieldObjectId}", Assert.IsType<string>(activeTask["battleId"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantControllerIds"]));
        Assert.Contains(ShadowObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));
        Assert.Contains(EnemyAttackerObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));

        var p2Snapshot = session.SnapshotFor("P2");
        Assert.Contains(hiddenObjectId, System.Text.Json.JsonSerializer.Serialize(p2Snapshot));
        Assert.DoesNotContain(hiddenObjectId, Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));
    }

    [Theory]
    [InlineData("open-main")]
    [InlineData("source-base")]
    [InlineData("source-exhausted")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    [InlineData("standby-source")]
    [InlineData("no-legal-target")]
    [InlineData("wrong-priority")]
    [InlineData("spell-duel")]
    public void ShadowPromptHidesOutsideBattleResponseOrIllegalSources(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = (prompt.Candidates ?? [])
            .SingleOrDefault(candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        if (activateCandidate is null)
        {
            return;
        }

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var abilityIds = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"])
            .Select(entry => entry["abilityId"] as string)
            .ToArray();
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.ShadowStunAbilityId, abilityIds);
    }

    [Fact]
    public async Task ShadowActivationPaysManaPowerExhaustsAndCreatesStackWithoutImmediateStun()
    {
        var result = await ActivateShadowAsync(BuildShadowState(mana: 1, power: 1));

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.DoesNotContain("STUNNED", result.State.CardObjects[EnemyAttackerObjectId].UntilEndOfTurnEffects);
        Assert.Equal([ShadowObjectId, FriendlyAttackerObjectId], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowCardNo, stackItem.CardNo);
        Assert.Equal([EnemyAttackerObjectId], stackItem.TargetObjectIds);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunAbilityId, costEvent.Payload["abilityId"]);
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(1, costEvent.Payload["printedManaCost"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.True(Assert.IsType<bool>(costEvent.Payload["exhaustsSource"]));
    }

    [Fact]
    public async Task ShadowCanRecycleRuneForGenericPowerShortfall()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{BlueRuneObjectId}";
        var state = BuildShadowState(
            mana: 1,
            power: 0,
            p1BaseObjectIds: [BlueRuneObjectId],
            runeDeckObjectIds: ["P1-RUNE-BOTTOM"],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue),
                ["P1-RUNE-BOTTOM"] = RuneCard("P1-RUNE-BOTTOM", RuneTrait.Red)
            });

        var result = await ActivateShadowAsync(state, optionalCosts: [paymentResourceAction]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.DoesNotContain(BlueRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM", BlueRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([BlueRuneObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task ShadowEnemySpellshieldTargetPaysManaTax()
    {
        var result = await ActivateShadowAsync(
            BuildShadowState(mana: 2, power: 1),
            targetObjectId: EnemySpellshieldAttackerObjectId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["printedManaCost"]);
        Assert.Equal(1, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal(2, costEvent.Payload["baseManaCost"]);
        Assert.Equal(2, costEvent.Payload["totalManaCost"]);
        Assert.Equal([EnemySpellshieldAttackerObjectId], Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task ShadowStackPassPassStunsTargetAndKeepsSourceBattlefieldExhausted()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateShadowAsync(BuildShadowState(mana: 1, power: 1), engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-shadow-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-shadow-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal([ShadowObjectId, FriendlyAttackerObjectId], p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.True(p2Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Contains("STUNNED", p2Pass.State.CardObjects[EnemyAttackerObjectId].UntilEndOfTurnEffects);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, EnemyAttackerObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["statusEffectId"] as string, "STUNNED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShadowResolutionNoEffectsWhenTargetStopsAttacking()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateShadowAsync(BuildShadowState(mana: 1, power: 1), engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var staleState = activated.State with
        {
            CardObjects = ReplaceCardObject(
                activated.State.CardObjects,
                EnemyAttackerObjectId,
                activated.State.CardObjects[EnemyAttackerObjectId] with { IsAttacking = false })
        };

        var p1Pass = await engine.ResolveAsync(
            staleState,
            new PlayerIntent("intent-shadow-stale-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-shadow-stale-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.DoesNotContain("STUNNED", p2Pass.State.CardObjects[EnemyAttackerObjectId].UntilEndOfTurnEffects);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_NO_EFFECT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "TARGET_NO_LONGER_LEGAL", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("wrong-priority")]
    [InlineData("missing-target")]
    [InlineData("too-many-targets")]
    [InlineData("friendly-target")]
    [InlineData("non-attacking-target")]
    [InlineData("defender-target")]
    [InlineData("wrong-battlefield-target")]
    [InlineData("base-target")]
    [InlineData("face-down-target")]
    [InlineData("standby-target")]
    [InlineData("insufficient-mana")]
    [InlineData("insufficient-power")]
    [InlineData("insufficient-tax-mana")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("unnecessary-recycle")]
    [InlineData("duplicate-recycle")]
    [InlineData("invalid-recycle")]
    [InlineData("temporary-resource")]
    [InlineData("source-base")]
    [InlineData("source-exhausted")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    [InlineData("standby-source")]
    public async Task ShadowRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "missing-target" => ShadowCommand(targetObjectIds: []),
            "too-many-targets" => ShadowCommand(targetObjectIds: [EnemyAttackerObjectId, EnemySpellshieldAttackerObjectId]),
            "friendly-target" => ShadowCommand(targetObjectIds: [FriendlyAttackerObjectId]),
            "non-attacking-target" => ShadowCommand(targetObjectIds: [EnemyNonAttackerObjectId]),
            "defender-target" => ShadowCommand(targetObjectIds: [EnemyDefenderObjectId]),
            "wrong-battlefield-target" => ShadowCommand(targetObjectIds: [EnemyWrongBattlefieldAttackerObjectId]),
            "base-target" => ShadowCommand(targetObjectIds: [EnemyBaseUnitObjectId]),
            "face-down-target" => ShadowCommand(targetObjectIds: ["P2-FACE-DOWN-ATTACKER"]),
            "standby-target" => ShadowCommand(targetObjectIds: ["P2-STANDBY-ATTACKER"]),
            "insufficient-tax-mana" => ShadowCommand(targetObjectIds: [EnemySpellshieldAttackerObjectId]),
            "unsupported-optional-cost" => ShadowCommand(optionalCosts: ["UNSUPPORTED_OPTIONAL_COST"]),
            "unnecessary-recycle" => ShadowCommand(optionalCosts: [$"RECYCLE_RUNE:{BlueRuneObjectId}"]),
            "duplicate-recycle" => ShadowCommand(optionalCosts: [$"RECYCLE_RUNE:{BlueRuneObjectId}", $"RECYCLE_RUNE:{BlueRuneObjectId}"]),
            "invalid-recycle" => ShadowCommand(optionalCosts: ["RECYCLE_RUNE:P1-RUNE-MISSING"]),
            "temporary-resource" => ShadowCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-SHADOW")]),
            _ => ShadowCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateShadowAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null,
        string targetObjectId = EnemyAttackerObjectId,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-shadow", "P1", CommandTypes.ActivateAbility),
            ShadowCommand([targetObjectId], optionalCosts),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand ShadowCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            ShadowObjectId,
            P4ActivatedAbilityCatalog.ShadowStunAbilityId,
            targetObjectIds ?? [EnemyAttackerObjectId],
            optionalCosts);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command,
        string playerId = "P1")
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-shadow-invalid-{playerId}", playerId, CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static async Task<ResolutionResult> OpenNaturalShadowBattleResponseAsync(
        int mana,
        int power,
        CoreRuleEngine? engine = null)
    {
        var state = BuildNaturalShadowStartBattleState(mana, power);
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-natural-shadow-declare-battle", "P2", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [EnemyAttackerObjectId],
                [FriendlyAttackerObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static MatchState BuildNaturalShadowStartBattleState(int mana, int power)
    {
        var state = BuildShadowState(mana, power);
        return state with
        {
            Tick = 7,
            ActivePlayerId = "P2",
            TurnPlayerId = "P2",
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)],
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Battlefields = state.PlayerZones["P1"].Battlefields
                        .Prepend(BattlefieldObjectId)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                }),
            CardObjects = ReplaceCardObjects(
                state.CardObjects,
                new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [FriendlyAttackerObjectId] = state.CardObjects[FriendlyAttackerObjectId] with { IsAttacking = false },
                    [EnemyAttackerObjectId] = state.CardObjects[EnemyAttackerObjectId] with { IsAttacking = false },
                    [EnemySpellshieldAttackerObjectId] = state.CardObjects[EnemySpellshieldAttackerObjectId] with { IsAttacking = false },
                    [EnemyDefenderObjectId] = state.CardObjects[EnemyDefenderObjectId] with { IsDefending = false }
                })
        };
    }

    private static MatchState BuildInvalidScenarioState(string scenario)
    {
        var extraCardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BlueRuneObjectId] = RuneCard(BlueRuneObjectId, RuneTrait.Blue),
            ["P2-FACE-DOWN-ATTACKER"] = Unit("P2-FACE-DOWN-ATTACKER", "UNL-111/219", "P2", isAttacking: true, isFaceDown: true),
            ["P2-STANDBY-ATTACKER"] = Unit("P2-STANDBY-ATTACKER", "UNL-112/219", "P2", isAttacking: true, extraTags: [CardObjectTags.Standby])
        };
        var state = scenario switch
        {
            "insufficient-mana" => BuildShadowState(mana: 0, power: 1, p1BaseObjectIds: [BlueRuneObjectId], extraCardObjects: extraCardObjects),
            "insufficient-power" => BuildShadowState(mana: 1, power: 0, p1BaseObjectIds: [BlueRuneObjectId], extraCardObjects: extraCardObjects),
            "insufficient-tax-mana" => BuildShadowState(mana: 1, power: 1, p1BaseObjectIds: [BlueRuneObjectId], extraCardObjects: extraCardObjects),
            "unnecessary-recycle" => BuildShadowState(mana: 1, power: 1, p1BaseObjectIds: [BlueRuneObjectId], extraCardObjects: extraCardObjects),
            "duplicate-recycle" => BuildShadowState(mana: 1, power: 0, p1BaseObjectIds: [BlueRuneObjectId], extraCardObjects: extraCardObjects),
            "temporary-resource" => BuildShadowState(mana: 1, power: 1, temporaryPaymentResources: [TemporaryResource("MALZAHAR:TEMP-SHADOW")], extraCardObjects: extraCardObjects),
            _ => BuildShadowState(mana: 1, power: 1, p1BaseObjectIds: [BlueRuneObjectId], extraCardObjects: extraCardObjects)
        };

        state = state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P2",
                state.PlayerZones["P2"] with
                {
                    Battlefields = state.PlayerZones["P2"].Battlefields
                        .Concat(["P2-FACE-DOWN-ATTACKER", "P2-STANDBY-ATTACKER"])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                }),
            ObjectLocations = ReplaceObjectLocations(
                state.ObjectLocations,
                new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
                {
                    ["P2-FACE-DOWN-ATTACKER"] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                    ["P2-STANDBY-ATTACKER"] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
                })
        };

        return scenario switch
        {
            "wrong-timing" => state with { TimingState = TimingStates.NeutralOpen, PriorityPlayerId = null },
            "wrong-priority" => state with { PriorityPlayerId = "P2" },
            "open-main" => state with { TimingState = TimingStates.NeutralOpen, PriorityPlayerId = null },
            "spell-duel" => state with
            {
                TimingState = TimingStates.SpellDuelOpen,
                PriorityPlayerId = null,
                FocusPlayerId = "P1"
            },
            "source-base" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [ShadowObjectId],
                        Battlefields = [FriendlyAttackerObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    ShadowObjectId,
                    new ObjectLocationState("P1", "BASE"))
            },
            "source-exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    ShadowObjectId,
                    state.CardObjects[ShadowObjectId] with { IsExhausted = true })
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    ShadowObjectId,
                    state.CardObjects[ShadowObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    ShadowObjectId,
                    state.CardObjects[ShadowObjectId] with { CardNo = "UNL-195/219" })
            },
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    ShadowObjectId,
                    state.CardObjects[ShadowObjectId] with { IsFaceDown = true })
            },
            "standby-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    ShadowObjectId,
                    state.CardObjects[ShadowObjectId] with
                    {
                        Tags = state.CardObjects[ShadowObjectId].Tags
                            .Concat([CardObjectTags.Standby])
                            .ToArray()
                    })
            },
            "no-legal-target" => state with
            {
                CardObjects = ReplaceCardObjects(
                    state.CardObjects,
                    new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                    {
                        [EnemyAttackerObjectId] = state.CardObjects[EnemyAttackerObjectId] with { IsAttacking = false },
                        [EnemySpellshieldAttackerObjectId] = state.CardObjects[EnemySpellshieldAttackerObjectId] with { IsAttacking = false }
                    })
            },
            _ => state
        };
    }

    private static MatchState BuildShadowState(
        int mana,
        int power,
        IReadOnlyList<string>? p1BaseObjectIds = null,
        IReadOnlyList<string>? runeDeckObjectIds = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null,
        IReadOnlyList<TemporaryPaymentResourceState>? temporaryPaymentResources = null)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = Battlefield(BattlefieldObjectId, "P1"),
            [OtherBattlefieldObjectId] = Battlefield(OtherBattlefieldObjectId, "P1"),
            [ShadowObjectId] = Unit(ShadowObjectId, P4ActivatedAbilityCatalog.ShadowCardNo, "P1"),
            [FriendlyAttackerObjectId] = Unit(FriendlyAttackerObjectId, "UNL-101/219", "P1", isAttacking: true),
            [EnemyAttackerObjectId] = Unit(EnemyAttackerObjectId, "UNL-102/219", "P2", isAttacking: true),
            [EnemySpellshieldAttackerObjectId] = Unit(EnemySpellshieldAttackerObjectId, "UNL-103/219", "P2", isAttacking: true, extraTags: [CardObjectTags.Spellshield]),
            [EnemyDefenderObjectId] = Unit(EnemyDefenderObjectId, "UNL-104/219", "P2", isDefending: true),
            [EnemyNonAttackerObjectId] = Unit(EnemyNonAttackerObjectId, "UNL-105/219", "P2"),
            [EnemyWrongBattlefieldAttackerObjectId] = Unit(EnemyWrongBattlefieldAttackerObjectId, "UNL-106/219", "P2"),
            [EnemyBaseUnitObjectId] = Unit(EnemyBaseUnitObjectId, "UNL-107/219", "P2")
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        return new MatchState(
            "room-shadow",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, power),
                ["P2"] = RunePool.Empty
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = p1BaseObjectIds ?? [],
                    RuneDeck = runeDeckObjectIds ?? [],
                    Battlefields = [ShadowObjectId, FriendlyAttackerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [EnemyBaseUnitObjectId],
                    Battlefields =
                    [
                        EnemyAttackerObjectId,
                        EnemySpellshieldAttackerObjectId,
                        EnemyDefenderObjectId,
                        EnemyNonAttackerObjectId,
                        EnemyWrongBattlefieldAttackerObjectId
                    ]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            priorityPlayerId: "P1",
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [OtherBattlefieldObjectId] = new("P2", "BATTLEFIELD", OtherBattlefieldObjectId),
                [ShadowObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [FriendlyAttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [EnemyAttackerObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [EnemySpellshieldAttackerObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [EnemyDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [EnemyNonAttackerObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [EnemyWrongBattlefieldAttackerObjectId] = new("P2", "BATTLEFIELD", OtherBattlefieldObjectId),
                [EnemyBaseUnitObjectId] = new("P2", "BASE")
            },
            temporaryPaymentResources: temporaryPaymentResources);
    }

    private static CardObjectState Battlefield(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            cardNo: "BATTLEFIELD-TEST",
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        bool isAttacking = false,
        bool isDefending = false,
        bool isFaceDown = false,
        IReadOnlyList<string>? extraTags = null)
    {
        return new CardObjectState(
            objectId,
            power: 2,
            isFaceDown: isFaceDown,
            isAttacking: isAttacking,
            isDefending: isDefending,
            tags: new[] { CardObjectTags.UnitCard }
                .Concat(extraTags ?? [])
                .ToArray(),
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState RuneCard(string objectId, string trait)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            cardNo: $"RUNE-{trait}",
            ownerId: "P1",
            controllerId: "P1");
    }

    private static TemporaryPaymentResourceState TemporaryResource(string resourceId)
    {
        return new TemporaryPaymentResourceState(
            resourceId,
            "P1",
            "P1-MALZAHAR",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            "ACTIVATE_ABILITY",
            2,
            2,
            [PaymentCostRules.RuneCostPaymentKind],
            1);
    }

    private static IReadOnlyDictionary<string, CardObjectState> ReplaceCardObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        CardObjectState replacement)
    {
        var next = cardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, CardObjectState> ReplaceCardObjects(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, CardObjectState> replacements)
    {
        var next = cardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var entry in replacements)
        {
            next[entry.Key] = entry.Value;
        }

        return next;
    }

    private static IReadOnlyDictionary<string, PlayerZones> ReplacePlayerZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string playerId,
        PlayerZones replacement)
    {
        var next = playerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[playerId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocation(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocations(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, ObjectLocationState> replacements)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var entry in replacements)
        {
            next[entry.Key] = entry.Value;
        }

        return next;
    }
}
