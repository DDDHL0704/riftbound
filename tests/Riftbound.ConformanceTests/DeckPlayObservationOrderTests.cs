using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeckPlayObservationOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObjectMemberReorderingPreservesObservedOrderThroughBothChoicesAndPendingPlays(bool reinforcement)
    {
        MatchState state;
        if (reinforcement)
            state = (await OfficialReinforcementsTests.Open("SFD·125/221", select: false)).State;
        else
        {
            var driver = new StandbyDefendObserverTests.Driver(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BrightFuture));
            await driver.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BrightFuture, []));
            await driver.Top();
            state = driver.State;
        }
        var originalOrder = state.PendingCardChoice!.ContextObjectIds.ToArray();
        Assert.True(originalOrder.Length > 1);
        Assert.Equal(reinforcement ? "U" : "P1_TOP", originalOrder[0]);
        state = await SelectAfterMemberReordering(state, reinforcement ? "U" : "P1_TOP");
        if (!reinforcement) state = await SelectAfterMemberReordering(state, "P2_TOP");
        Assert.Equal(reinforcement ? ["U"] : new[] { "P2_TOP", "P1_TOP" },
            state.StackItems.Select(s => s.SourceObjectId));
        if (!reinforcement) state = await ActAfterMemberReordering(state, "P2", new PlayCardCommand("P2_TOP", "OGN·096/298", []));
        state = await ActAfterMemberReordering(state, "P1", new PlayCardCommand(reinforcement ? "U" : "P1_TOP",
            reinforcement ? "SFD·125/221" : "OGN·096/298", []));
        Assert.Null(state.PendingEffectPlay);
        Assert.Contains(reinforcement ? "U" : "P1_TOP", state.PlayerZones["P1"].Base);
        if (!reinforcement) Assert.Contains("P2_TOP", state.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task ObjectMemberReorderingKeepsTheFirstRevealedUnitLastAndItsOriginalFreePlayPermission()
    {
        var driver = new StandbyDefendObserverTests.Driver(EndDeckPlayTests.Position());
        await driver.Act("P1", new EndTurnCommand());
        await driver.Top();
        var state = driver.State;
        var pending = Assert.IsType<PendingEffectPlayState>(state.PendingEffectPlay);
        var viewed = pending.Parent.DeckPlaySequence!.Viewed["P1"];
        Assert.True(viewed.Count > 1);
        Assert.Equal("END_UNIT", viewed[^1].ObjectId);
        Assert.Single(viewed, binding => state.CardObjects[binding.ObjectId].Tags.Contains(CardObjectTags.UnitCard));
        Assert.True(pending.IgnoreAllCosts);
        state = await ActAfterMemberReordering(state, "P1", new PlayCardCommand("END_UNIT", "OGN·096/298", []));
        Assert.Contains("END_UNIT", state.PlayerZones["P1"].Base);
        Assert.Null(state.PendingEffectPlay);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("null-list")]
    [InlineData("null-binding")]
    [InlineData("null-id")]
    [InlineData("generation")]
    [InlineData("outside")]
    public async Task RecoveryStillRejectsMalformedOrForgedOrderedObservations(string mutation)
    {
        var state = (await OfficialReinforcementsTests.Open("SFD·125/221", select: false)).State;
        var item = Assert.Single(state.StackItems);
        var context = item.DeckPlaySequence!;
        var viewed = context.Viewed["P1"];
        IReadOnlyList<ObjectBinding> changed = mutation switch {
            "order" => viewed.Reverse().ToArray(),
            "duplicate" => viewed.Take(viewed.Count - 1).Append(viewed[0]).ToArray(),
            "missing" => viewed.Skip(1).ToArray(),
            "null-list" => null!,
            "null-binding" => viewed.Skip(1).Prepend(null!).ToArray(),
            "null-id" => viewed.Skip(1).Prepend(viewed[0] with { ObjectId = null! }).ToArray(),
            "generation" => viewed.Skip(1).Prepend(viewed[0] with { Generation = 99 }).ToArray(),
            _ => viewed.Skip(1).Prepend(new ObjectBinding("TAIL", 0)).ToArray()
        };
        var forged = state with { StackItems = [item with { DeckPlaySequence = context with {
            Viewed = new Dictionary<string, IReadOnlyList<ObjectBinding>>(context.Viewed) { ["P1"] = changed } } }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), error => error.Contains("deck play"));
    }

    private static Task<MatchState> SelectAfterMemberReordering(MatchState state, string id)
    {
        var choice = state.PendingCardChoice!;
        return ActAfterMemberReordering(state, choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    private static async Task<MatchState> ActAfterMemberReordering(MatchState state, string player, GameCommand command)
    {
        var original = JsonSerializer.SerializeToNode(state)!;
        var changed = ReorderObjectMembers(original);
        Assert.True(JsonNode.DeepEquals(original, changed));
        var cold = changed.Deserialize<MatchState>()!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(cold));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(cold));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state)),
            JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(cold))));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state)),
            JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(cold))));
        var intent = new PlayerIntent("ordered-observation-" + state.Tick, player, command.CmdType);
        var engine = new CoreRuleEngine();
        var expected = await engine.ResolveAsync(state, intent, command, default);
        var actual = await engine.ResolveAsync(cold, intent, command, default);
        Assert.True(expected.Accepted, expected.ErrorMessage);
        Assert.True(actual.Accepted, actual.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(expected.State), MatchStateHasher.Hash(actual.State));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(actual.Events)));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(actual.State));
        return actual.State;
    }

    private static JsonNode ReorderObjectMembers(JsonNode node)
        => node is JsonObject obj
            ? new JsonObject(obj.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : ReorderObjectMembers(p.Value))))
            : node is JsonArray array
                ? new JsonArray(array.Select(value => value is null ? null : ReorderObjectMembers(value)).ToArray())
                : node.DeepClone();
}
