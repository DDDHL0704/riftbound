using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMinefieldEndStepTests
{
    private const string Minefield = "RAD·142/167";

    private static MatchState Position()
    {
        var state = RadHextechMinefieldTests.Board();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["B4"] = factory.CreateObject("B4", "P1", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["B4"] = new("P1", "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Battlefields = [..state.PlayerZones["P1"].Battlefields, "B4"] } }
        };
        state = AttackDamageDistributionTests.Spell(state, "SECOND", Minefield, "P1");
        state = AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·064/298", "P2");
        return AttackDamageDistributionTests.Spell(state, "RECYCLE", "UNL-103/219", "P2");
    }

    private static CardObjectState[] Bombs(Driver game)
        => game.State.CardObjects.Values.Where(card => card.ControllerId == "P1"
            && card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo).ToArray();

    private static async Task Cast(Driver game, string source, params string[] bombs)
    {
        await game.Act("P1", new PlayCardCommand(source, Minefield, bombs));
        await game.Top();
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
    }

    private static async Task ChooseField(Driver game, string field)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("BOMB_REBUILD_BATTLEFIELD", choice.ChoiceWindow);
        Assert.Equal(new[] { "BF", "OTHER" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [field]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeparateMinefieldsKeepTheirOwnCountsAndBattlefieldChoicesInEitherTriggerOrder(bool reversed)
    {
        var game = new Driver(Position());
        await Cast(game, "UNIT", "B1");
        await Cast(game, "SECOND", "B2", "B4");
        Assert.Empty(Bombs(game));
        Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(effect => effect.StartsWith("BOMB_REBUILD:", StringComparison.Ordinal)));
        await game.Act("P1", new EndTurnCommand());
        Assert.Null(game.State.PendingCardChoice);
        var queued = game.State.TriggerQueue.OrderBy(item => item.SourceObjectId, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, queued.Length);
        if (reversed) Array.Reverse(queued);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queued.Select(item => item.TriggerId).ToArray()));
        var chosen = new Dictionary<string, string> { ["UNIT"] = "BF", ["SECOND"] = "OTHER" };
        // The command lists resolution order; CN 337.1.b confirms insertion order.
        foreach (var expected in queued.Reverse())
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            var confirming = Assert.Single(game.State.StackItems, item => item.StackItemId == choice.ResolvingStackItemId);
            Assert.Equal(expected.SourceObjectId, confirming.SourceObjectId);
            await ChooseField(game, chosen[confirming.SourceObjectId]);
            Assert.Empty(Bombs(game));
        }
        Assert.Null(game.State.PendingCardChoice);
        for (var index = 0; index < 2; index++)
        {
            var item = game.State.StackItems[^1];
            Assert.Equal(queued[index].SourceObjectId, item.SourceObjectId);
            var count = item.SourceObjectId == "UNIT" ? 1 : 2;
            Assert.Equal(new[] { chosen[item.SourceObjectId] }, item.TargetObjectIds);
            Assert.Equal(count, item.BombRebuild!.BombCount);
            var before = Bombs(game).Length;
            await game.Top();
            Assert.Equal(before + count, Bombs(game).Length);
        }
        Assert.Single(Bombs(game), bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "BF");
        Assert.Equal(2, Bombs(game).Count(bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "OTHER"));
        Assert.All(Bombs(game), bomb => {
            Assert.False(bomb.IsExhausted);
            Assert.Equal("P1", bomb.OwnerId);
            Assert.DoesNotContain(bomb.ObjectId, new[] { "B1", "B2", "B4" });
        });
        Assert.True(game.State.CardObjects.ContainsKey("B3"));
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith("BOMB_REBUILD:", StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheSecondMinefieldKeepsTheFirstDelayedInstructionAndTheSecondBombs()
    {
        var game = new Driver(Position());
        await Cast(game, "UNIT", "B1");
        var first = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith("BOMB_REBUILD:", StringComparison.Ordinal));
        await game.Act("P1", new PlayCardCommand("SECOND", Minefield, ["B2", "B4"]));
        var target = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [target]));
        await game.Top();
        Assert.Equal(first, Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith("BOMB_REBUILD:", StringComparison.Ordinal)));
        Assert.Equal(new[] { "B2", "B4" }, Bombs(game).Select(bomb => bomb.ObjectId).Order(StringComparer.Ordinal));
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Graveyard);
        await game.Act("P1", new EndTurnCommand());
        await ChooseField(game, "OTHER");
        await game.Top();
        Assert.Equal(3, Bombs(game).Length);
        Assert.Single(Bombs(game), bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "OTHER");
        await game.Replay();
    }

    [Fact]
    public async Task RecyclingTheResolvedSourceDoesNotRemoveTheDelayedRebuild()
    {
        var game = new Driver(Position());
        await Cast(game, "UNIT", "B1");
        var sourceGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new EndTurnCommand());
        await ChooseField(game, "OTHER");
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["UNIT"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].MainDeck);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > sourceGeneration);
        Assert.Single(game.State.StackItems, item => item.BombRebuild is not null);
        await game.Top();
        Assert.Single(Bombs(game), bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "OTHER");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].MainDeck);
        await game.Replay();
    }

    [Fact]
    public async Task InvalidEndStepChoicesAndASpellOnlyCounterLeaveTheRealRebuildUsable()
    {
        var game = new Driver(Position());
        await Cast(game, "UNIT", "B1", "B2");
        await game.Act("P1", new EndTurnCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BF"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BF", "OTHER"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await ChooseField(game, "OTHER");
        var item = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await RadSpellDuelReactionTests.Reject(game.State, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [item.StackItemId]));
        await game.Top();
        Assert.Equal(2, Bombs(game).Count(bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "OTHER"));
        Assert.Contains("COUNTER", game.State.PlayerZones["P2"].Hand);
        await game.Replay();
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-minefield-end-choice", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
