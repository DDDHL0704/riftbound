using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadNeekoLegendTests
{
    private const string Neeko = "RAD·167/167";
    private const string Viktor = "OGN·265/298";
    private const string Darius = "OGN·253/298";

    [Fact]
    public async Task RegisteredLegendsRemainPrivateUntilTheResolutionDecisionAndEnterAsRealActiveCards()
    {
        var game = await Position(Viktor, Darius);
        await Play(game, "NEEKO");
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.Null(game.State.PendingRuleChoice);
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("REGISTERED_LEGEND_SELECTION", choice.Kind);
        Assert.Equal(new[] { Viktor, Darius }, choice.Options.Select(option => option.Id));
        Assert.DoesNotContain(game.PromptFor("P2").Candidates!, candidate => candidate.Action == CommandTypes.PayCost && candidate.Enabled);
        var views = ResolutionResult.BuildSnapshots(game.State);
        foreach (var no in new[] { Viktor, Darius })
        {
            Assert.Contains(no, Strings(JsonSerializer.SerializeToElement(game.PromptFor("P1"))));
            Assert.DoesNotContain(no, Strings(JsonSerializer.SerializeToElement(views["P2"])));
            Assert.DoesNotContain(no, Strings(JsonSerializer.SerializeToElement(game.PromptFor("P2"))));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick,
                game.Events().Last().Sequence, [], game.State);
            Assert.DoesNotContain(no, Strings(JsonSerializer.SerializeToElement(spectator)));
        }
        await game.ColdCheckpoint();
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Viktor]));
        var id = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var legend = game.State.CardObjects[id];
        Assert.Equal(Viktor, legend.CardNo);
        Assert.False(legend.IsExhausted); Assert.Null(legend.TokenFactoryCardNo);
        Assert.Contains("CARD_TYPE:LEGEND", legend.Tags);
        Assert.Equal("LEGEND", game.State.ObjectLocations[id].Zone);
        Assert.All(ResolutionResult.BuildSnapshots(game.State).Values,
            snapshot => Assert.Contains(Viktor, Strings(JsonSerializer.SerializeToElement(snapshot))));
        Assert.DoesNotContain(game.Events(), ev => ev.Event.Kind == "TARGETS_CONFIRMED");
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ZeroOrOneAvailableLegendNeedsNoAdditionalDecision(int count)
    {
        var game = await Position(count == 0 ? [] : [Viktor]);
        await Play(game, "NEEKO"); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(count, game.State.PlayerZones["P1"].LegendZone.Count);
        await game.Replay();
    }

    [Fact]
    public async Task MultipleNeekosShareThePoolAndCannotCreateTheSameRegisteredLegendTwice()
    {
        var game = await Position(Viktor, Darius);
        await Play(game, "NEEKO"); await game.Top();
        var choice = game.State.PendingRuleChoice!.Request;
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Darius]));
        await Play(game, "SECOND"); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(2, game.State.PlayerZones["P1"].LegendZone.Count);
        Assert.Equal(new[] { Darius, Viktor }.Order(), game.State.PlayerZones["P1"].LegendZone
            .Select(id => game.State.CardObjects[id].CardNo).Order());
        await Play(game, "THIRD"); await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].LegendZone.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceBeforeItsPlaySkillResolvesDoesNotCreateALegend()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"]));
        await game.Top(); await game.Top();
        Assert.Contains("NEEKO", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.Null(game.State.BoundLegends);
        await game.Replay();
    }

    [Fact]
    public async Task SourceDepartureCompletelyRemovesTheLegendAndReentryCreatesANewObject()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO"); await game.Top();
        var original = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var generation = game.State.CardObjects["NEEKO"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.False(game.State.CardObjects.ContainsKey(original));
        Assert.False(game.State.ObjectLocations.ContainsKey(original));
        Assert.DoesNotContain(original, JsonSerializer.Serialize(game.State.PlayerZones), StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(game.State.BoundLegends!).DepartedAtTick);
        await game.ColdCheckpoint();
        await Play(game, "NEEKO"); await game.Top();
        var current = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        Assert.NotEqual(original, current);
        Assert.True(game.State.CardObjects["NEEKO"].ObjectGeneration > generation);
        Assert.Equal(Viktor, game.State.CardObjects[current].CardNo);
        Assert.Equal(2, game.State.BoundLegends!.Count);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ARealLegendAbilityAlreadyOnTheChainResolvesAfterItsLegendCeasesToExist()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new LegendActCommand(legend, LegendActionAbilityCatalog.ViktorLegendAbilityId, [], ["SPEND_MANA:1"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(legend));
        Assert.Equal(legend, Assert.Single(game.State.StackItems).SourceObjectId);
        var item = game.State.StackItems.Single();
        Assert.Contains(HistoryErrors(game, game.State with { StackItems = [item with { StackItemId = item.StackItemId + "-invented" }] }),
            error => error.Contains("accepted activation receipt", StringComparison.Ordinal));
        await game.ColdCheckpoint(); await game.Top();
        Assert.Single(game.Events(), ev => ev.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ForgedChoicesAreAtomicAndForgedDurationBindingsFailRecovery()
    {
        var game = await Position(Viktor, Darius);
        await Play(game, "NEEKO"); await game.Top();
        var choice = game.State.PendingRuleChoice!.Request;
        var original = MatchStateHasher.Hash(game.State);
        foreach (var (player, selected) in new[] { ("P2", Viktor), ("P1", "UNL-181/219"), ("P1", Neeko) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
                new PlayerIntent("forged-neeko", player, CommandTypes.PayCost),
                new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [selected]), default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(original, MatchStateHasher.Hash(rejected.State));
        }
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Viktor]));
        var binding = Assert.Single(game.State.BoundLegends!);
        Assert.Contains(HistoryErrors(game, game.State with { BoundLegends = null }),
            error => error.Contains("provenance differs from accepted history", StringComparison.Ordinal));
        foreach (var forged in new[] { binding with { PlayerId = null! },
            binding with { Source = null! }, binding with { LegendObjectId = null! },
            binding with { PlayerId = "P2" },
            binding with { Source = binding.Source with { Generation = binding.Source.Generation + 1 } },
            binding with { LegendCardNo = Darius }, binding with { DepartedAtTick = game.State.Tick } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { BoundLegends = [forged] }));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnOpponentOwnedNeekoUsesOnlyTheAbilityControllersRegisteredPool(bool hasOwnPool)
    {
        var position = (await Position(Darius)).State;
        var deck = position.PlayerDecklists["P1"];
        var game = new Driver(position with {
            CardObjects = position.CardObjects.ToDictionary(pair => pair.Key,
                pair => pair.Key == "NEEKO" ? pair.Value with { OwnerId = "P2" } : pair.Value),
            PlayerDecklists = new Dictionary<string, OfficialDecklist> {
                ["P1"] = deck with { AdditionalLegendCardNos = hasOwnPool ? [Darius] : null },
                ["P2"] = deck with { AdditionalLegendCardNos = [Viktor] } }
        }) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        Assert.Empty(game.State.PlayerZones["P2"].LegendZone);
        Assert.DoesNotContain(game.State.CardObjects.Values, card => card.CardNo == Viktor);
        Assert.Equal(hasOwnPool ? 1 : 0, game.State.PlayerZones["P1"].LegendZone.Count);
        if (hasOwnPool)
            Assert.Equal(Darius, game.State.CardObjects[Assert.Single(game.State.PlayerZones["P1"].LegendZone)].CardNo);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task MovingTheSameFieldObjectPreservesItsAddedLegend()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var generation = game.State.CardObjects["NEEKO"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["NEEKO"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("NEEKO", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(generation, game.State.CardObjects["NEEKO"].ObjectGeneration);
        Assert.Contains(legend, game.State.PlayerZones["P1"].LegendZone);
        Assert.Null(Assert.Single(game.State.BoundLegends!).DepartedAtTick);
        await game.ColdCheckpoint(); await game.Replay();
    }

    private static IReadOnlyList<string> HistoryErrors(Driver game, MatchState state)
    {
        var sequence = game.Events().Last().Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
    }

    private static IEnumerable<string> Strings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) yield return element.GetString()!;
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                foreach (var value in Strings(child)) yield return value;
        else if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                foreach (var value in Strings(property.Value)) yield return value;
    }

    private static Task Play(Driver game, string id)
        => game.Act("P1", new PlayCardCommand(id, Neeko, [], Destination: "BASE"));

    private static async Task<Driver> Position(params string[] pool)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(item => item.Id == "jhin-lowcurve").Decklist;
        var main = deck.MainDeck.ToArray();
        main[Array.FindLastIndex(main, no => no != deck.ChampionCardNo)] = Neeko;
        deck = deck with { MainDeck = main, AdditionalLegendCardNos = pool.Length == 0 ? null : pool };
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298", 0);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in new[] { "NEEKO", "SECOND", "THIRD" }) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, Neeko, "P1");
            locations[id] = new("P1", "HAND");
        }
        state = state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                ? pair.Value with { Hand = ["NEEKO", "SECOND", "THIRD"] } : pair.Value),
            PlayerDecklists = new Dictionary<string, OfficialDecklist> { ["P1"] = deck },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(50, 50), ["P2"] = new(50, 50) } };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
        return new(state) { RecoverAfterEveryCommand = false };
    }
}
