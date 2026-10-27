using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnActuallyGrantedAndPaidEchoKeepsEachEarnedInverseAndItsExecutionIdentity(bool remove)
    {
        // Controlled initial field, not a legal-opening or equipment-entry claim.
        // The grant, all resource payments, spell executions and inverse actions are real commands.
        var initial = Add(Add(Position("P1"), "GATE", "SFD·078/221", zone: "BASE"), "COPY-TARGET", UnitNo, zone: "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["COPY-TARGET"] = initial.CardObjects["COPY-TARGET"] with { IsEmpowered = remove } } };
        var game = new Driver(initial);
        try
        {
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
            Assert.True(game.State.CardObjects["GATE"].IsExhausted); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Single(game.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1"));
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-2"));
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-3"));
            var mana = game.State.RunePools["P1"].Mana; var generation = game.State.CardObjects["COPY-TARGET"].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["COPY-TARGET"], OptionalCosts: ["ECHO"], Mode: remove ? Remove : Empower));
            Assert.Equal(mana - 6, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(2, Assert.Single(game.State.StackItems).RepeatExecutions!.Count);
            Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => EchoCostRules.IsGrant(e, "P1"));
            await game.Top();
            Assert.Equal(!remove, game.State.CardObjects["COPY-TARGET"].IsEmpowered);
            Assert.Equal(remove ? 1 : 2, game.Events().Count(e => e.Event.Kind == "DELAYED_EMPOWER_SCHEDULED"));
            Assert.Single(game.Events(), e => e.Event.Kind == (remove ? "OBJECT_EMPOWER_REMOVED" : "OBJECT_EMPOWERED"));
            await game.Act("P1", new EndTurnCommand());
            var contexts = game.State.StackItems.Select(i => i.DelayedEmpower).Concat(game.State.TriggerQueue.Select(i => i.DelayedEmpower)).OfType<DelayedEmpowerContext>().ToArray();
            Assert.Equal(remove ? 1 : 2, contexts.Length);
            Assert.Equal(contexts.Length, contexts.Select(c => c.InstanceId).Distinct().Count());
            Assert.Equal(remove ? new[] { 0 } : new[] { 0, 1 }, contexts.Select(c => c.ExecutionIndex).Order().ToArray());
            await FinishEnd(game, "P1"); Assert.Equal(remove, game.State.CardObjects["COPY-TARGET"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["COPY-TARGET"].ObjectGeneration);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("paid-echo-independent-inverses-remove-" + remove, initial, game); }
    }

    [Fact]
    public async Task AnEarnedInverseSurvivesItsFactoryTokenBeingActuallyBanishedAndCeasingToExist()
    {
        var initial = Add(Add(Position("P1"), "SECOND-SPELL", No), "MEL", "VEN·110", zone: "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.SandSoldierTokenCardNo, out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TOKEN"] = factory.CreateObject("TOKEN", "P2", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["TOKEN"] = new("P2", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, "TOKEN"] } } };
        initial = StatusBattlefield(initial, "TOKEN", "P2");
        var game = new Driver(initial);
        try
        {
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["TOKEN"], Mode: Empower)); await game.Top();
            Assert.True(game.State.CardObjects["TOKEN"].IsEmpowered);
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-2"));
            await game.Act("P1", new PlayCardCommand("SECOND-SPELL", No, ["MEL"], Mode: Empower)); await game.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["TOKEN"])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey("TOKEN")); Assert.False(game.State.ObjectLocations.ContainsKey("TOKEN"));
            Assert.DoesNotContain("TOKEN", game.State.PlayerZones["P2"].Banished);
            await game.Act("P1", new EndTurnCommand()); await FinishEnd(game, "P1");
            Assert.False(game.State.CardObjects.ContainsKey("TOKEN")); Assert.False(game.State.CardObjects["MEL"].IsEmpowered);
            Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "DELAYED_EMPOWER_SCHEDULED"));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("delayed-factory-token-actually-ceased", initial, game); }
    }

    [Fact]
    public async Task ARealControlAndRecallKeepsTheInverseOwnerButUsesTheEmpoweredUnitsCurrentControllerForItsNewSkill()
    {
        var initial = Add(Add(Add(Position("P1"), "MEL", "VEN·110", zone: "BASE"), "CONTROL", "OGN·203/298", "P2"), "VICTIM", "OGN·096/298", zone: "BASE");
        initial = StatusBattlefield(StatusBattlefield(initial, "MEL", "P1"), "VICTIM", "P1");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = RunePool.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["MEL"] = initial.CardObjects["MEL"] with { IsEmpowered = true } } };
        for (var i = 0; i < 9; i++) initial = Add(initial, "CONTROL-RUNE-" + i, "UNL-R05", "P2", "BASE");
        var game = new Driver(initial);
        try
        {
            foreach (var rune in game.State.PlayerZones["P2"].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray())
                await game.Act("P2", new TapRuneCommand(rune));
            for (var i = 0; i < 3; i++) await game.Act("P2", new RecycleRuneCommand("CONTROL-RUNE-" + i));
            var generation = game.State.CardObjects["MEL"].ObjectGeneration;
            await game.Act("P2", new PlayCardCommand("CONTROL", "OGN·203/298", ["MEL"]));
            Assert.Equal(1, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["MEL"], Mode: Remove)); await game.Top();
            Assert.False(game.State.CardObjects["MEL"].IsEmpowered); Assert.Equal("P1", game.State.CardObjects["MEL"].ControllerId);
            await game.Top(); Assert.Equal("P2", game.State.CardObjects["MEL"].ControllerId);
            Assert.Equal("P1", game.State.CardObjects["MEL"].OwnerId); Assert.Equal(generation, game.State.CardObjects["MEL"].ObjectGeneration);
            Assert.Contains("MEL", game.State.PlayerZones["P2"].Base);
            await game.Act("P2", new EndTurnCommand());
            var inverse = Assert.Single(game.State.StackItems); Assert.Equal("P1", inverse.ControllerId);
            Assert.Equal("P2", inverse.DelayedEmpower!.TurnPlayerId); await game.Top();
            Assert.True(game.State.CardObjects["MEL"].IsEmpowered);
            var skill = Assert.Single(game.State.StackItems, i => i.FieldContext?.Kind == "BECAME_EMPOWERED_BANISH");
            Assert.Equal("P2", skill.ControllerId); Assert.Equal(generation, skill.FieldContext!.SourceGeneration);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice); Assert.Equal("P2", choice.PlayerId);
            await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["VICTIM"])); await game.Top();
            Assert.Contains("VICTIM", game.State.PlayerZones["P1"].Banished);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("delayed-actual-control-recall-current-observer", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealDrawCanMoveTheCreatingSpellIntoPrivateHandAndNormalVictoryStopsItsLowerInverse(bool winningBurnout)
    {
        // Explicit empty-deck/score fixture; Meditation's optional exhaust cost is declined.
        var initial = Add(Position("P1"), "DRAW", "OGN·048/298");
        var moved = initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
        initial = initial with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = winningBurnout ? 7 : 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
        var game = new Driver(initial); ResolutionResult? probe = null;
        try
        {
            var unit = await PrepareTarget(game, "P1", false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: Empower)); await game.Top();
            Assert.Equal(["SPELL"], game.State.PlayerZones["P1"].Graveyard);
            await game.Act("P1", new EndTurnCommand()); var inverse = Assert.Single(game.State.StackItems).StackItemId;
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(winningBurnout ? 8 : 1, game.State.PlayerScores["P2"]);
            Assert.Equal(inverse, Assert.Single(game.State.StackItems).StackItemId);
            var opponent = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"], Json);
            var shown = Assert.Single(opponent.GetProperty("stack").EnumerateArray()); Assert.Equal("HIDDEN", shown.GetProperty("sourceObjectId").GetString());
            Assert.False(shown.TryGetProperty("delayedEmpower", out _));
            AssertPrivateDelayedSpectatorSource(game);
            if (winningBurnout)
            {
                Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal("P2", game.State.WinnerPlayerId);
                Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STACK_ITEM_RESOLVED" && Equals(e.Event.Payload.GetValueOrDefault("stackItemId"), inverse));
                Assert.All(ResolutionResult.BuildPrompts(game.State).Values, p => Assert.False(p.Actionable));
                var hash = MatchStateHasher.Hash(game.State);
                probe = await new CoreRuleEngine().ResolveAsync(game.State, new("after-normal-victory", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
                Assert.False(probe.Accepted); Assert.Empty(probe.Events); Assert.Equal(hash, MatchStateHasher.Hash(probe.State));
            }
            else { await FinishEnd(game, "P1"); Assert.False(game.State.CardObjects[unit].IsEmpowered); }
            await game.Replay(); Restore(game.State);
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN035_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "private-source-views-winning-burnout-" + winningBurnout + ".json"),
                    JsonSerializer.Serialize(new { spectator = ResolutionResult.BuildSpectatorSnapshot(game.State), players = ResolutionResult.BuildSnapshots(game.State) }, Json));
            }
            await Save("delayed-source-private-winning-burnout-" + winningBurnout, initial, game, probe);
        }
    }

    private static void AssertPrivateDelayedSpectatorSource(Driver game)
    {
        var state = game.State; var sequence = game.Events().Last().Sequence;
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var original = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(Assert.Single(spectator.SpectatorSnapshot.Stack), Json), Json)!;
        Assert.Equal("HIDDEN", Assert.IsType<JsonElement>(original["sourceObjectId"]).GetString());
        original["sourceObjectId"] = "SPELL"; original["sourceVisibility"] = "VISIBLE";
        var leaky = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Stack = [original] } };
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator));
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, leaky);
        Assert.Contains(errors, e => e.Contains("source object", StringComparison.Ordinal) || e.Contains("sourceObjectId", StringComparison.Ordinal));
    }
}
