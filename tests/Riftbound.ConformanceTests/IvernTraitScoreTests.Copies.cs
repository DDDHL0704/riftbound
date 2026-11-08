using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class IvernTraitScoreTests
{
    [Fact]
    public async Task AnActuallyAssembledSongEarnsTwoSeparatelyRespondableTraitScoreSkills()
    {
        var state = RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.Equal("UNL-177/219", game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await Conquer(game);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, item => item.UnitEvent!.Origin is not null);
        Assert.Single(game.State.StackItems, item => item.UnitEvent!.Origin is null);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.Top();
        Assert.Single(game.State.StackItems);
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(3, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnActualImageEarnsTheScoreAndItsPendingSkillSurvivesTheTokenCeasing(bool returnCopy)
    {
        const string copy = "COPY-TOKEN-001";
        var state = RadGearTestPosition.Put(Position(), "COPY", "UNL-200/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COPY-RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["UNIT"]));
        await game.Top(); await game.Top();
        Assert.Equal("UNL-177/219", game.State.CardObjects[copy].CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, game.State.CardObjects[copy].TokenFactoryCardNo);
        await DrainFocus(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top(); await DrainFocus(game);
        await game.Act("P1", new MoveUnitCommand(copy, "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.NotNull(ScoreSkill(game));
        if (returnCopy)
        {
            await game.Act("P1", new PlayCardCommand("COPY-RETURN", "OGN·104/298", [copy]));
            await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(copy));
            Assert.NotNull(ScoreSkill(game));
            await game.ColdCheckpoint();
            var original = ScoreSkill(game); var captured = original.UnitEvent!;
            var history = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
            foreach (var forged in new[] {
                original with { SourceObjectId = "COPY-TOKEN-FORGED" },
                original with { UnitEvent = captured with { SourceGeneration = captured.SourceGeneration + 1 } },
                original with { UnitEvent = captured with { TokenFactoryCardNo = P6TokenFactoryCatalog.WarhawkTokenCardNo } },
                original with { UnitEvent = captured with { CapturedTick = captured.CapturedTick + 1 } } })
                Assert.Contains(ValidateOriginalHistory(game, game.State with { StackItems = [forged] }),
                    error => error == "battlefield timing skill differs from its original accepted capture");
            Assert.Equal(history, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        }
        else
        {
            // Keep the accepted journal intact. Merely deleting a live token
            // from a snapshot must not invent a proved departure.
            var missing = game.State with {
                CardObjects = game.State.CardObjects.Where(p => p.Key != copy).ToDictionary(p => p.Key, p => p.Value),
                ObjectLocations = game.State.ObjectLocations.Where(p => p.Key != copy).ToDictionary(p => p.Key, p => p.Value),
                PlayerZones = game.State.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
                    Battlefields = p.Value.Battlefields.Where(id => id != copy).ToArray() }) };
            Assert.Contains(ValidateOriginalHistory(game, missing),
                error => error == "battlefield unit event source disappearance lacks its original accepted token cessation");
            await game.ColdCheckpoint();
        }
        await game.Top();
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    private static async Task DrainFocus(Driver game)
    {
        for (var passes = 0; game.State.FocusPlayerId is { } player; passes++)
        {
            Assert.True(passes < 4);
            await game.Act(player, new PassFocusCommand());
        }
    }
}
