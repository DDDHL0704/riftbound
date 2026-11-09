using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// UNL-095; CN 391-393 (independent delay), 191.5.b (controller),
// 468.3.c (actual surviving units inherit the result), 403 (response).
public sealed class LastStandTurnBattleWinTests
{
    private const string Spell = "UNL-095/219";
    private const string Prefix = "TURN_BATTLE_WIN_REWARD:";
    private static MatchState Position(bool defend = false, string enemyNo = "OGN·175/298")
    {
        var state = RadGearTestPosition.Create();
        state = RadGearTestPosition.Put(state, "SPELL", Spell, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "SPELL2", Spell, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "CHARM", "OGN·043/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "READY", "UNL-009/219", "P1", "HAND");
        for (var i = 0; i < 2; i++) state = RadGearTestPosition.Put(state, "GREEN" + i, "UNL-R02", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "ENEMY", enemyNo, "P2", defend ? "BASE" : "BATTLEFIELD", defend ? null : "OTHER");
        if (defend) state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return state;
    }
    private static async Task Cast(Driver game, string id = "SPELL")
    {
        await game.Act("P1", new PlayCardCommand(id, Spell, ["ALLY"])); await game.Top();
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
    }
    private static async Task Battle(Driver game, bool defend = false)
    {
        if (defend) {
            await game.Act("P1", new RecycleRuneCommand("GREEN0"));
            await game.Act("P1", new PlayCardCommand("CHARM", "OGN·043/298", ["ENEMY"], Destination: "BATTLEFIELD:BF"));
            await game.Top();
        } else await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        for (var i = 0; game.State.FocusPlayerId is { } focus; i++) {
            Assert.InRange(i, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act(defend ? "P2" : "P1", new DeclareBattleCommand(defend ? "BF" : "OTHER",
            defend ? ["ENEMY"] : ["ALLY"], defend ? ["ALLY"] : ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
    }
    private static TurnBattleWinReward Read(string marker)
        => JsonSerializer.Deserialize<TurnBattleWinReward>(Convert.FromBase64String(marker[Prefix.Length..]))!;
    private static string Marker(TurnBattleWinReward reward)
        => Prefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(reward));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealAttackingOrDefendingWinQueuesAnIndependentExperienceRewardBeforeControl(bool defend)
    {
        var game = new Driver(Position(defend)); await Cast(game); Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        var reward = Read(Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal)));
        await Battle(game, defend); var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(reward, skill.UnitBattleWinDraw!.TurnReward); Assert.Equal("P1", skill.ControllerId);
        Assert.Equal(defend ? "DEFENDER" : "ATTACKER", skill.UnitBattleWinDraw.Role);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P1"].Stack);
        Assert.Contains("获得 2 经验", view.EnumerateArray().Single().GetProperty("abilityLabel").GetString());
        await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]);
        var earned = Assert.Single(game.Events(), e => e.Event.Kind == "EXPERIENCE_GAINED");
        Assert.True(earned.Sequence < Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·192/298")] [InlineData("OGN·142/298")]
    public async Task ADrawOrLossDoesNotEarnExperience(string enemyNo)
    {
        // 4+3 vs 7 trades; 4+3 vs 10 loses. Neither is a surviving winner.
        var game = new Driver(Position(enemyNo: enemyNo)); await Cast(game); await Battle(game);
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitBattleWinDraw?.TurnReward is not null);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1")); await game.Replay();
    }

    [Fact]
    public async Task TwoActualSpellsCreateTwoSeparateRewardsAndControllerOrdersTheirSkills()
    {
        var game = new Driver(Position()); await Cast(game); await Cast(game, "SPELL2"); await Battle(game);
        Assert.Equal(2, game.State.TriggerQueue.Count); Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).Reverse().ToArray()));
        Assert.Equal(2, game.State.StackItems.Count); await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]);
        await game.Top(); Assert.Equal(4, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheWinnerDuringResponsesPreservesItsAlreadyEarnedRewardAndHidesTheNewIncarnation()
    {
        var game = new Driver(Position()); await Cast(game); await Battle(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"ALLY\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task ATargetReturnedBeforeSpellResolutionCreatesNeitherTheBuffNorTheDelayedReward()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal));
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TURN_BATTLE_WIN_REWARD_CREATED");
        await game.Replay();
    }

    [Fact]
    public async Task AReturnedAndReplayedUnitDoesNotInheritItsPreviousIncarnationsReward()
    {
        var game = new Driver(Position()); await Cast(game);
        var reward = Read(Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal)));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > reward.Unit.Generation);
        var state = game.State; Assert.Equal(4, state.CardObjects["ALLY"].Power);
        var changed = reward with { Unit = new("ALLY", state.CardObjects["ALLY"].ObjectGeneration) };
        var forged = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Select(effect => effect == Marker(reward) ? Marker(changed) : effect).ToArray() };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await game.Top();
        await Battle(game); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitBattleWinDraw?.TurnReward is not null);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1")); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("amount")] [InlineData("controller")]
    [InlineData("generation")] [InlineData("turn")] [InlineData("malformed")]
    public async Task RecoveryRejectsRemovedDuplicatedOrRewrittenOriginalDelayedInstructions(string change)
    {
        var game = new Driver(Position()); await Cast(game);
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal));
        var reward = Read(marker);
        var changed = change switch {
            "amount" => reward with { ExperienceCount = 8 }, "controller" => reward with { ControllerId = "P2" },
            "generation" => reward with { Unit = reward.Unit with { Generation = reward.Unit.Generation + 1 } },
            "turn" => reward with { TurnNumber = reward.TurnNumber + 1 }, _ => reward };
        var effects = game.State.UntilEndOfTurnEffects.Where(effect => effect != marker).ToList();
        if (change != "erase") effects.Add(change == "malformed" ? Prefix + "invalid" : Marker(changed));
        if (change == "duplicate") effects.Add(marker);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { UntilEndOfTurnEffects = effects }));
        await Battle(game); await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]);
    }

    [Theory]
    [InlineData("erase")] [InlineData("amount")] [InlineData("source")] [InlineData("unit")] [InlineData("role")]
    public async Task RecoveryBindsTheEarnedSkillToItsOriginalAcceptedBattleAndCreation(string change)
    {
        var game = new Driver(Position()); await Cast(game); await Battle(game); var skill = Assert.Single(game.State.StackItems);
        var context = skill.UnitBattleWinDraw!; var reward = context.TurnReward!;
        var changed = change switch {
            "amount" => context with { ExperienceCount = 4 }, "source" => context with { Source = new("SPELL2", 0) },
            "unit" => context with { TurnReward = reward with { Unit = new("ENEMY", 0) } },
            _ => context with { Role = "DEFENDER" } };
        var forged = game.State with { StackItems = change == "erase" ? [] : [skill with { UnitBattleWinDraw = changed }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task AnActualTurnEndExpiresTheDelayAndThePublicExpiryNeverContainsItsPrivateReceipt()
    {
        var game = new Driver(Position()); await Cast(game);
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal));
        ResolutionResult Current() => new(true, null, game.State, [],
            ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State));
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command) {
            await game.Act(player, command); return Current();
        }
        await game.Act("P1", new EndTurnCommand()); await TurnSequenceTestDriver.Complete(Current(), Submit);
        Assert.DoesNotContain(marker, game.State.UntilEndOfTurnEffects);
        var expiry = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNTIL_END_OF_TURN_EXPIRED");
        Assert.DoesNotContain(Prefix, JsonSerializer.Serialize(expiry.Event));
        Assert.Contains(Read(marker).InstanceId, JsonSerializer.Serialize(expiry.Event));
        await game.Act("P2", new EndTurnCommand()); await TurnSequenceTestDriver.Complete(Current(), Submit);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); await Battle(game);
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitBattleWinDraw?.TurnReward is not null);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1")); await game.Replay();
    }

    [Fact]
    public async Task ACoherentReplacementReceiptStillRequiresTheOriginalAcceptedCreationHash()
    {
        var game = new Driver(Position()); await Cast(game);
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal));
        var changed = Read(marker) with { ExecutionIndex = 9, InstanceId = "" };
        changed = changed with { InstanceId = "turn-battle-win-" + MatchStateHasher.HashValue(changed) };
        var forged = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(effect => effect == marker ? Marker(changed) : effect).ToArray() };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged), error => error.Contains("original accepted instruction", StringComparison.Ordinal));
        await Battle(game); await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]);
    }

    [Fact]
    public async Task TheDelayRewardsEachActualWinDuringItsTurnInsteadOfBeingConsumedByTheFirstWin()
    {
        var state = RadGearTestPosition.Put(Position(), "ENEMY2", "OGN·010/298", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "BLINK", "OGS·011/024", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        var game = new Driver(state); await Cast(game); await Battle(game); await game.Top();
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        await game.Act("P1", new PlayCardCommand("BLINK", "OGS·011/024", ["ALLY"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["ALLY"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        for (var pass = 0; game.State.FocusPlayerId is { } focus; pass++) {
            Assert.InRange(pass, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act("P1", new DeclareBattleCommand("BF", ["ALLY"], ["ENEMY2"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(2, game.State.PlayerExperience["P1"]); await game.Top();
        Assert.Equal(4, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task ARealControlChangeBeforeTheWinLeavesTheDelayedRewardWithItsOriginalCaster()
    {
        var state = RadGearTestPosition.Put(Position(defend: true), "STEAL", "OGN·203/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "CHARM2", "OGN·043/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "VICTIM", "OGN·175/298", "P1", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1" } } };
        for (var i = 0; i < 11; i++) state = RadGearTestPosition.Put(state, "STEAL-RUNE" + i, "UNL-R05", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "CHARM-MANA", "UNL-R03", "P1", "BASE");
        var game = new Driver(state); await Cast(game);
        await game.Act("P1", new RecycleRuneCommand("GREEN0"));
        await game.Act("P1", new PlayCardCommand("CHARM", "OGN·043/298", ["ENEMY"], Destination: "BATTLEFIELD:BF")); await game.Top();
        if (game.State.FocusPlayerId == "P1") await game.Act("P1", new PassFocusCommand());
        await game.TapRunes("P2", Enumerable.Range(0, 8).Select(i => "STEAL-RUNE" + i));
        for (var i = 8; i < 11; i++) await game.Act("P2", new RecycleRuneCommand("STEAL-RUNE" + i));
        await game.Act("P2", new PlayCardCommand("STEAL", "OGN·203/298", ["ALLY"])); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["ALLY"].ControllerId); Assert.Contains("ALLY", game.State.PlayerZones["P2"].Base);
        for (var pass = 0; game.State.FocusPlayerId is { } focus; pass++) {
            Assert.InRange(pass, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act("P1", new TapRuneCommand("CHARM-MANA")); await game.Act("P1", new RecycleRuneCommand("GREEN1"));
        await game.Act("P1", new PlayCardCommand("CHARM2", "OGN·043/298", ["ALLY"], Destination: "BATTLEFIELD:OTHER")); await game.Top();
        for (var pass = 0; game.State.FocusPlayerId is { } focus; pass++) {
            Assert.InRange(pass, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act("P2", new DeclareBattleCommand("OTHER", ["ALLY"], ["VICTIM"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal("P2", game.State.PendingBattleResult!.WinnerPlayerId);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId); await game.Top();
        Assert.Equal(2, game.State.PlayerExperience["P1"]); Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P2"));
        await game.Replay();
    }

    [Fact]
    public async Task ATargetRestrictedCounterCannotChooseTheUntargetedDelayedReward()
    {
        var state = RadGearTestPosition.Put(Position(), "COUNTER", "SFD·045/221", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "GREEN", "UNL-R02", "P2", "BASE");
        var game = new Driver(state); await Cast(game); await Battle(game);
        var id = Assert.Single(game.State.StackItems).StackItemId; await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new RecycleRuneCommand("GREEN"));
        await RadGearTestPosition.Reject(game.State, "P2", new PlayCardCommand("COUNTER", "SFD·045/221", [id]));
        Assert.Single(game.State.StackItems); Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Prefix, StringComparison.Ordinal)); await game.Replay();
    }
    [Fact]
    public async Task ARealAnnieReturnMakesTheOriginalSpellPrivateWithoutChangingTheDelayedReward()
    {
        var state = RadGearTestPosition.Put(Position(), "ANNIE", "OGS·010/024", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ANNIE-PURPLE", "UNL-R05", "P1", "BASE");
        var game = new Driver(state); await Cast(game); await game.Act("P1", new RecycleRuneCommand("ANNIE-PURPLE"));
        await game.Act("P1", new PlayCardCommand("ANNIE", "OGS·010/024", [], Destination: "BASE"));
        for (var i = 0; !game.State.PlayerZones["P1"].Hand.Contains("SPELL"); i++) {
            Assert.InRange(i, 0, 8);
            if (game.State.PendingCardChoice is { } choice)
                await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL"]));
            else if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
            else await game.Top();
        }
        var historicalView = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"],
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
        TurnBattleWinPrivacyAssertions.CheckHistoricalModifiers(historicalView, game.State);
        Assert.DoesNotContain("\"SPELL\"", historicalView.ToJsonString());
        await Battle(game); var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("SPELL", skill.SourceObjectId); Assert.True(skill.UnitBattleWinDraw!.Source.Generation < game.State.CardObjects["SPELL"].ObjectGeneration);
        var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.DoesNotContain("\"SPELL\"", view.GetRawText()); Assert.DoesNotContain("turnReward", view.GetRawText());
        Assert.DoesNotContain("\"SPELL\"", JsonSerializer.Serialize(Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED" && entry.Event.Payload.ContainsKey("battleWinContextHash")).Event));
        await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]);
        var experience = Assert.Single(game.Events(), entry => entry.Event.Kind == "EXPERIENCE_GAINED");
        Assert.Equal("HIDDEN", experience.Event.Payload["sourceObjectId"]); await game.Replay();
    }

    [Fact]
    public async Task AnEarnedRewardSurvivesItsWinningTokenCeasingButItsFutureListeningIsRetired()
    {
        var state = Position(); Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.FaerieTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = factory.CreateObject("ALLY", "P1", "P1", isExhausted: false) } };
        var game = new Driver(state); await Cast(game); await Battle(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.DoesNotContain("ALLY", game.State.CardObjects.Keys);
        const string inactive = "INACTIVE_TURN_BATTLE_WIN_REWARD:";
        var retired = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(inactive, StringComparison.Ordinal));
        var forged = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(effect => effect != retired).ToArray() };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged), error => error.Contains("original accepted token departures", StringComparison.Ordinal));
        await game.Top(); Assert.Equal(2, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Fact]
    public async Task ANewTokenReusingTheSamePublicIdAndGenerationCannotInheritTheCeasedTokensReward()
    {
        var state = RadGearTestPosition.Put(Position(enemyNo: "OGN·010/298"), "SUMMON", "OGN·094/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ANNIE", "OGS·010/024", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ANNIE-PURPLE", "UNL-R05", "P1", "BASE");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SUMMON", "OGN·094/298", [])); await game.Top();
        var created = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_TOKEN_CREATED");
        var original = game.State.CardObjects[(string)created.Event.Payload["tokenObjectId"]!];
        Assert.Equal(3, original.Power); Assert.NotNull(original.TokenFactoryCardNo);
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, [original.ObjectId])); await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [original.ObjectId])); await game.Top();
        Assert.DoesNotContain(original.ObjectId, game.State.CardObjects.Keys);
        await game.Act("P1", new RecycleRuneCommand("ANNIE-PURPLE"));
        await game.Act("P1", new PlayCardCommand("ANNIE", "OGS·010/024", [], Destination: "BASE"));
        for (var i = 0; !game.State.PlayerZones["P1"].Hand.Contains("SUMMON"); i++) {
            Assert.InRange(i, 0, 8);
            if (game.State.PendingCardChoice is { } choice)
                await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SUMMON"]));
            else await game.Top();
        }
        await game.Act("P1", new PlayCardCommand("SUMMON", "OGN·094/298", [])); await game.Top();
        var recreated = game.Events().Last(entry => entry.Event.Kind == "UNIT_TOKEN_CREATED");
        var fresh = game.State.CardObjects[(string)recreated.Event.Payload["tokenObjectId"]!];
        Assert.Equal(original.TokenFactoryCardNo, fresh.TokenFactoryCardNo);
        Assert.Equal(original.ObjectId, fresh.ObjectId); Assert.Equal(original.ObjectGeneration, fresh.ObjectGeneration); Assert.Equal(3, fresh.Power);
        await game.Act("P1", new MoveUnitCommand(fresh.ObjectId, "BASE", "BATTLEFIELD:OTHER", []));
        for (var i = 0; game.State.FocusPlayerId is { } focus; i++) {
            Assert.InRange(i, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act("P1", new DeclareBattleCommand("OTHER", [fresh.ObjectId], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitBattleWinDraw?.TurnReward is not null);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1")); await game.Replay();
    }

}
