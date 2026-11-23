using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SimilarTargetGroupTests
{
    private const string Bellows = "SFD·080/221";
    private const string Wall = "SFD·043/221";
    private const string Discord = "VEN·107";
    private const string Unit = "OGN·219/298";

    [Theory]
    [InlineData(Bellows, "P1", "BASE", null, "P2", "BASE", null)]
    [InlineData(Bellows, "P1", "BATTLEFIELD", "BF", "P1", "BATTLEFIELD", "OTHER")]
    [InlineData(Wall, "P1", "BATTLEFIELD", "BF", "P1", "BATTLEFIELD", "OTHER")]
    public async Task HandPlayRejectsTargetsAtDifferentLocations(
        string card, string firstPlayer, string firstZone, string? firstField,
        string secondPlayer, string secondZone, string? secondField)
    {
        var state = Position(card);
        state = RadGearTestPosition.Put(state, "ONE", Unit, firstPlayer, firstZone, firstField);
        state = RadGearTestPosition.Put(state, "TWO", Unit, secondPlayer, secondZone, secondField);
        var command = new PlayCardCommand("SPELL", card, ["ONE", "TWO"]);

        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        await RadGearTestPosition.Reject(state, "P1", command);
        Assert.False(quote.IsValid);
    }

    [Fact]
    public void BellowsPromptCombinesOnlyUnitsAtTheSameBaseOrBattlefield()
    {
        var state = Position(Bellows);
        state = RadGearTestPosition.Put(state, "BASE-MATE", Unit, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "ON-FIELD", Unit, "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "SAME-FIELD", Unit, "P2", "BATTLEFIELD", "BF");
        var action = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!,
            candidate => candidate.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            action.Metadata!["sourceRequirements"]));
        var choices = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyList<string>>>(requirement["legalTargetSelections"]);

        Assert.Contains(choices, ids => ids.SequenceEqual(["ALLY", "BASE-MATE"]));
        Assert.Contains(choices, ids => ids.SequenceEqual(["ON-FIELD", "SAME-FIELD"]));
        Assert.DoesNotContain(choices, ids => ids.Contains("ALLY") && ids.Contains("ENEMY"));
        Assert.DoesNotContain(choices, ids => ids.Contains("ALLY") && ids.Contains("ON-FIELD"));
    }

    [Fact]
    public async Task BellowsKeepsAnEntireValidLocationGroupAfterAResponseSplitsTargets()
    {
        var state = Position(Bellows);
        foreach (var id in new[] { "ONE", "TWO", "THREE" })
            state = RadGearTestPosition.Put(state, id, Unit, "P1", "BATTLEFIELD", "BF");

        var cast = await Act(state, "P1", new PlayCardCommand("SPELL", Bellows, ["ONE", "TWO", "THREE"]));
        var split = RadUnitEffectTestFixture.Place(cast.State, "TWO", "P1", "BASE");
        split = RadUnitEffectTestFixture.Place(split, "THREE", "P1", "BASE");
        var opened = await Top(split);
        Assert.NotNull(opened.State.PendingRuleChoice);

        var baseGroup = await GroupedTargetTests.Select(opened.State, "REMOVE:ONE");
        var tooFew = await GroupedTargetTests.Select(baseGroup.State, "REMOVE:TWO");
        Assert.DoesNotContain(tooFew.State.PendingRuleChoice!.Request.Options, option => option.Id == "CONFIRM");
        Restore(tooFew.State);
        var restored = await GroupedTargetTests.Select(tooFew.State, "RESTORE:TWO");
        Restore(restored.State);
        var done = await GroupedTargetTests.Select(restored.State, "CONFIRM");
        Assert.Equal(0, done.State.CardObjects["ONE"].Damage);
        Assert.Equal(1, done.State.CardObjects["TWO"].Damage);
        Assert.Equal(1, done.State.CardObjects["THREE"].Damage);
        Restore(done.State);
    }

    [Fact]
    public async Task DiscordRechecksTotalPowerAtResolutionEvenWithoutACommonLocation()
    {
        var state = Position(Discord);
        state = RadGearTestPosition.Put(state, "YELLOW-ONE", "OGN·216/298", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "YELLOW-TWO", "OGN·216/298", "P2", "BATTLEFIELD", "OTHER");
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["YELLOW-ONE"] = state.CardObjects["YELLOW-ONE"] with { Power = 2 },
            ["YELLOW-TWO"] = state.CardObjects["YELLOW-TWO"] with { Power = 3 }
        };
        var cast = await Act(state with { CardObjects = cards }, "P1",
            new PlayCardCommand("SPELL", Discord, ["YELLOW-ONE", "YELLOW-TWO"]));

        var afterBuff = new Dictionary<string, CardObjectState>(cast.State.CardObjects)
        {
            ["YELLOW-ONE"] = cast.State.CardObjects["YELLOW-ONE"] with { Power = 3 }
        };
        var opened = await Top(cast.State with { CardObjects = afterBuff });
        Assert.NotNull(opened.State.PendingRuleChoice);
        var keepTwo = await GroupedTargetTests.Select(opened.State, "REMOVE:YELLOW-ONE");
        var done = await GroupedTargetTests.Select(keepTwo.State, "CONFIRM");
        Assert.Contains("YELLOW-ONE", done.State.PlayerZones["P2"].Base);
        Assert.Contains("YELLOW-TWO", done.State.PlayerZones["P2"].Hand);
        Restore(done.State);
    }

    [Fact]
    public async Task WallRevealedFromStandbyTargetsOnlyItsOriginalBattlefield()
    {
        var state = await StandbySourceRulesTests.ReadyShadow(Wall);
        var command = new RevealCardCommand("SHADOW", Wall, ["SHADOW-INSIDE", "SHADOW-OUTSIDE"]);
        await RadGearTestPosition.Reject(state, "P1", command);

        var cast = await Act(state, "P1", command with { TargetObjectIds = ["HOST", "SHADOW-INSIDE"] });
        var done = await Top(cast.State);
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Base);
        Assert.Contains("SHADOW-INSIDE", done.State.PlayerZones["P1"].Base);
        Restore(done.State);
    }

    private static MatchState Position(string spell)
        => RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", spell, "P1", "HAND");
}
