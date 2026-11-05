using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadKaisaLocationPermissionTests
{
    private const string Kaisa = "RAD·063/167";
    private const string Draw = "VEN·049";
    private const string Meditation = "OGN·048/298";

    private static CardObjectState PrintedKaisa(string id)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Kaisa, out var behavior));
        var card = OfficialGarenConquestBattlefieldCountTests.Printed(id, Kaisa, "P1");
        return card with { Tags = card.Tags.Concat(behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToArray() };
    }

    private static async Task<Driver> Duel(int sources = 1)
    {
        var state = VenUnitEntryBasicTests.Position(Draw);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = PrintedKaisa("ALLY") } };
        state = LastBreathDrawBoundaryTests.Add(state, "ANCHOR", "OGN·219/298", "P1", "BASE");
        if (sources == 2)
        {
            state = LastBreathDrawBoundaryTests.Add(state, "SECOND", Kaisa, "P1", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SECOND"] = PrintedKaisa("SECOND") } };
        }
        foreach (var (id, no) in new[] { ("FLASH", "OGS·011/024"), ("OPEN", Meditation), ("FOLLOW", Draw) })
            state = OfficialCopyIdentityTests.AddSpell(state, id, no);
        foreach (var (id, no) in new[] { ("TAKE", "OGN·203/298"), ("OPEN-P2", Meditation), ("DRAW-P2", Draw) })
            state = AttackDamageDistributionTests.Spell(state, id, no, "P2");
        var game = new Driver(state);
        var movers = sources == 2 ? new[] { "ALLY", "SECOND", "ANCHOR" } : ["ALLY", "ANCHOR"];
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", [], SourceObjectIds: movers));
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        Assert.True(game.State.SpellDuelState.IsActive);
        Assert.Equal("OTHER", game.State.SpellDuelState.BattlefieldObjectId);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.True(Offered(game, "P1", "UNIT"));
        return game;
    }

    private static bool Offered(Driver game, string player, string source)
        => ResolutionResult.BuildPrompts(game.State)[player].Candidates?.Any(candidate =>
            candidate.Action == CommandTypes.PlayCard && candidate.Enabled
                && candidate.Sources?.Any(option => option.Id == source) == true) == true;

    private static async Task RejectDraw(Driver game, string player = "P1", string source = "FOLLOW")
    {
        Assert.Equal(player, game.State.PriorityPlayerId);
        Assert.False(Offered(game, player, source));
        var command = new PlayCardCommand(source, Draw, []);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(game.State, player,
            PlayCostPreviewTests.Request(game.State, command)).CanPay);
        var hash = MatchStateHasher.Hash(game.State);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
        var result = await new CoreRuleEngine().ResolveAsync(game.State,
            new("kaisa-location-rejected-" + player + game.State.Tick, player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    public async Task RealMovementRecalculatesTheRemainingSourcesWithoutInvalidatingAnAlreadyPlayedSpell(
        int sources, int moved, bool retainsPermission)
    {
        var game = await Duel(sources);
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("OPEN", Meditation, []));
        await game.Act("P1", new PlayCardCommand("UNIT", Draw, []));
        var targets = moved == 2 ? new[] { "ALLY", "SECOND" } : ["ALLY"];
        await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", targets));
        await game.Top();
        Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations["ALLY"]);
        Assert.Equal(generation, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.True(game.State.SpellDuelState.IsActive);
        Assert.Equal("OTHER", game.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("OTHER", game.State.ObjectLocations["ANCHOR"].BattlefieldObjectId);
        Assert.Equal(retainsPermission, Offered(game, "P1", "FOLLOW"));
        var firstEvent = game.Events().Length;
        if (retainsPermission)
        {
            var command = new PlayCardCommand("FOLLOW", Draw, []);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1",
                PlayCostPreviewTests.Request(game.State, command));
            Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
            var mana = game.State.RunePools["P1"].Mana;
            var power = game.State.RunePools["P1"].TotalPower;
            await game.Act("P1", command);
            Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
            Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
            await game.Top();
        }
        else await RejectDraw(game);
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(retainsPermission ? 2 : 1,
            game.Events().Skip(firstEvent).Count(entry => entry.Event.Kind == "CARD_DRAWN"));
        if (!retainsPermission) await RejectDraw(game);
        await game.Top(); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task ActualControlAndRecallToTheOtherBaseGrantsNeitherPlayerReactionPermission()
    {
        var game = await Duel();
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["ALLY"]));
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["ALLY"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["ALLY"].ControllerId);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), game.State.ObjectLocations["ALLY"]);
        Assert.True(game.State.SpellDuelState.IsActive);
        if (game.State.FocusPlayerId is { } focus && focus != "P2")
            await game.Act(focus, new PassFocusCommand());
        await game.Act("P2", new PlayCardCommand("OPEN-P2", Meditation, []));
        await RejectDraw(game, "P2", "DRAW-P2");
        await game.Act("P2", new PassPriorityCommand());
        await RejectDraw(game);
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task ClosingTheActualShowdownRevokesPermissionEvenWhileKaisaRemainsAtThatBattlefield()
    {
        var game = await Duel();
        for (var passes = 0; game.State.FocusPlayerId is { } player; passes++)
        {
            Assert.InRange(passes, 0, 3);
            await game.Act(player, new PassFocusCommand());
        }
        Assert.False(game.State.SpellDuelState.IsActive);
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.Act("P1", new PlayCardCommand("OPEN", Meditation, []));
        await RejectDraw(game);
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }
}
