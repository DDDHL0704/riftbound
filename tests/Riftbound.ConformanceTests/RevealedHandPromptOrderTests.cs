using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RevealedHandPromptOrderTests
{
    [Fact]
    public async Task UnorderedObjectMembersKeepTheSameRevealedHandPromptAndOriginalOwnerPlay()
    {
        var original = (await OfficialRevealedHandPlayTests.Open()).State;
        var tree = JsonSerializer.SerializeToNode(original)!;
        var changed = Reorder(tree);
        Assert.True(JsonNode.DeepEquals(tree, changed));
        var restored = changed.Deserialize<MatchState>()!;
        Assert.Equal(MatchStateHasher.Hash(original), MatchStateHasher.Hash(restored));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(restored));
        Assert.True(Same(ResolutionResult.BuildSnapshots(original), ResolutionResult.BuildSnapshots(restored)));
        Assert.True(Same(ResolutionResult.BuildPrompts(original), ResolutionResult.BuildPrompts(restored)));

        var pending = restored.PendingEffectPlay!;
        Assert.True(pending.RevealedHand!.Choosing);
        Assert.Equal("P1", pending.PlayerId);
        var engine = new CoreRuleEngine();
        var selected = await engine.ResolveAsync(restored, new("hand-order-select", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(pending.ChoiceId, "REVEALED_HAND_PLAY", ["U"]), default);
        Assert.True(selected.Accepted, selected.ErrorMessage);
        Assert.Equal("P2", selected.State.PendingEffectPlay!.PlayerId);
        var played = await engine.ResolveAsync(selected.State, new("hand-order-owner-play", "P2", CommandTypes.PlayCard),
            new PlayCardCommand("U", "OGN·208/298", [], Destination: "BATTLEFIELD:BF"), default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Null(played.State.PendingEffectPlay);
        Assert.Equal("BF", played.State.ObjectLocations["U"].BattlefieldObjectId);
        Assert.Contains("STUNNED", played.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.Contains("SPELL", played.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("UNL-200/219", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(played.State)["P1"]));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(played.State));
    }

    private static bool Same<T>(T a, T b)
        => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a), JsonSerializer.SerializeToNode(b));

    private static JsonNode Reorder(JsonNode node)
        => node is JsonObject obj
            ? new JsonObject(obj.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : Reorder(p.Value))))
            : node is JsonArray array
                ? new JsonArray(array.Select(value => value is null ? null : Reorder(value)).ToArray())
                : node.DeepClone();
}
