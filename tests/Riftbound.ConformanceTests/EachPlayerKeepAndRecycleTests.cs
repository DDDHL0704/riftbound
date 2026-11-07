using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EachPlayerKeepAndRecycleTests
{
    private const string Judgment = "OGN·244/298";
    private static MatchState Position(int count)
    {
        var state = RadGearTestPosition.Create();
        foreach (var player in new[] { "P1", "P2" })
        {
            state = RadUnitEffectTestFixture.Place(state, player == "P1" ? "ALLY" : "ENEMY", player, "GRAVEYARD");
            for (var index = 0; index < count; index++)
            {
                state = RadGearTestPosition.Put(state, player + "-U" + index, "OGN·219/298", player, "BASE");
                state = RadGearTestPosition.Put(state, player + "-G" + index, "OGN·212/298", player, "BASE");
                state = RadGearTestPosition.Put(state, player + "-R" + index, "VEN·R01", player, "BASE");
                state = RadGearTestPosition.Put(state, player + "-H" + index, "OGN·219/298", player, "HAND");
            }
            state = RadGearTestPosition.Put(state, player + "-RBOTTOM", "VEN·R01", player, "RUNE_DECK");
        }
        return RadGearTestPosition.Put(state, "UNIT", Judgment, "P1", "HAND");
    }

    private static Task Choose(Driver game, string selected)
    {
        var pending = game.State.PendingRuleChoice!;
        return game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selected]));
    }

    private static async Task KeepFirstTwo(Driver game, bool reverseRunes = false)
    {
        var choices = 0;
        while (game.State.PendingRuleChoice is { } pending)
        {
            Assert.True(choices++ < 80);
            var request = pending.Request;
            var other = request.PlayerId == "P1" ? "P2" : "P1";
            Assert.DoesNotContain(CommandTypes.PayCost, game.PromptFor(other).Actions);
            Assert.DoesNotContain(request.PlayerId + "-H", JsonSerializer.Serialize(new { Snapshot = ResolutionResult.BuildSnapshots(game.State)[other], Prompt = game.PromptFor(other) }));
            var selected = request.Kind == "RUNE_RECYCLE_ORDER" && reverseRunes
                ? request.Options[^1] : request.Options[0];
            await Choose(game, selected.Id);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task EachPlayerKeepsUpToTwoOfEveryCategoryAtResolutionAndRunesUseTheirOwnDeck(int count)
    {
        var initial = Position(count); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Judgment, []));
        Assert.Null(game.State.PendingRuleChoice); await game.Top();
        if (count <= 2) Assert.Null(game.State.PendingRuleChoice);
        else
        {
            Assert.Equal("P1", game.State.PendingRuleChoice!.Request.PlayerId);
            Assert.Equal("KEEP_TWO_UNIT", game.State.PendingRuleChoice.Request.Kind);
            await Choose(game, "P1-U0");
            Assert.Contains("P1-U2", game.State.PlayerZones["P1"].Base);
            Assert.Contains("P2-U2", game.State.PlayerZones["P2"].Base);
            Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
            await KeepFirstTwo(game, reverseRunes: true);
        }
        foreach (var player in new[] { "P1", "P2" })
        {
            var zones = game.State.PlayerZones[player];
            Assert.Equal(Enumerable.Range(0, Math.Min(count, 2)).Select(index => player + "-H" + index), zones.Hand);
            Assert.Equal(initial.PlayerZones[player].MainDeck, zones.MainDeck.Take(6));
            Assert.Equal(6 + Math.Max(0, count - 2) * 3, zones.MainDeck.Count);
            Assert.Equal(new[] { player + "-RBOTTOM" }.Concat(Enumerable.Range(2, Math.Max(0, count - 2))
                .Reverse().Select(index => player + "-R" + index)), zones.RuneDeck);
            for (var index = 0; index < count; index++)
                foreach (var kind in new[] { "U", "G", "R", "H" })
                {
                    var id = player + "-" + kind + index;
                    Assert.Equal(index < 2 ? 0 : 1, game.State.CardObjects[id].ObjectGeneration);
                    if (index >= 2) Assert.Contains(id, kind == "R" ? zones.RuneDeck : zones.MainDeck);
                }
        }
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED" or "CARD_DISCARDED");
        Assert.DoesNotContain("-H", JsonSerializer.Serialize(game.Events().Select(entry => entry.Event)));
        await game.Replay();
    }

    [Fact]
    public async Task PredeclaredKeepTargetsAndAnotherPlayersResolutionDecisionsAreRejectedWithoutMutation()
    {
        var initial = Position(3);
        await RadGearTestPosition.Reject(initial, "P1", new PlayCardCommand("UNIT", Judgment, ["P1-U0"]));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", Judgment, [])); await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        foreach (var (player, selected) in new[] { ("P2", "P1-U0"), ("P1", "P2-U0"), ("P1", "P1-G0"), ("P1", "UNIT") })
            await RadGearTestPosition.Reject(game.State, player, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
        await KeepFirstTwo(game); await game.Replay();
    }

    [Fact]
    public async Task ARealReactionChangesTheAvailablePermanentsBeforeTheirControllersChoose()
    {
        var state = RadGearTestPosition.Put(Position(3), "RESPONSE", "OGN·241/298", "P2", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", Judgment, []));
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·241/298", []));
        Assert.Contains("RESPONSE", game.State.PlayerZones["P2"].Base);
        var enteredGeneration = game.State.CardObjects["RESPONSE"].ObjectGeneration;
        await game.Top();
        while (game.State.PendingRuleChoice?.Request.PlayerId == "P1")
            await Choose(game, game.State.PendingRuleChoice.Request.Options[0].Id);
        Assert.Equal("KEEP_TWO_UNIT", game.State.PendingRuleChoice!.Request.Kind);
        Assert.Contains(game.State.PendingRuleChoice.Request.Options, option => option.Id == "RESPONSE");
        await KeepFirstTwo(game);
        Assert.Contains("RESPONSE", game.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(enteredGeneration + 1, game.State.CardObjects["RESPONSE"].ObjectGeneration);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedUnitsAndRunesGoToTheirOwnersDecksAndTheRuneOwnerOrdersTheCombinedBatch()
    {
        var state = Position(3); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P1-U2"] = state.CardObjects["P1-U2"] with { OwnerId = "P2" },
            ["P1-R2"] = state.CardObjects["P1-R2"] with { OwnerId = "P2", IsExhausted = true } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", Judgment, [])); await game.Top();
        await KeepFirstTwo(game, reverseRunes: true);
        Assert.DoesNotContain("P1-U2", game.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-U2", game.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(new[] { "P1-RBOTTOM" }, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(new[] { "P2-RBOTTOM", "P2-R2", "P1-R2" }, game.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal("P2", game.State.CardObjects["P1-U2"].ControllerId);
        Assert.False(game.State.CardObjects["P1-R2"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ARealCreatedMinionIsRecycledAndThenCeasesToExistWithoutBecomingADeckCard()
    {
        var state = RadGearTestPosition.Put(Position(3), "ZZ-FORGE", "OGN·212/298", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("ZZ-FORGE", "OGN·212/298", []));
        while (game.State.StackItems.Count > 0) await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, card => TokenObjectRules.IsToken(card));
        await game.Act("P1", new PlayCardCommand("UNIT", Judgment, [])); await game.Top(); await KeepFirstTwo(game);
        Assert.False(game.State.CardObjects.ContainsKey(token.ObjectId));
        Assert.DoesNotContain(token.ObjectId, game.State.PlayerZones.Values.SelectMany(zones => zones.MainDeck));
        Assert.Contains("ZZ-FORGE", game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED");
        await game.Replay();
    }
}
