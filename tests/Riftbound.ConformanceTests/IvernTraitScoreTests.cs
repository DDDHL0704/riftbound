using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class IvernTraitScoreTests
{
    private static readonly (string Id, string No, string Tag)[] Animals = [
        ("BIRD", "OGN·216/298", "鸟类"), ("CAT", "OGN·135/298", "猫科"),
        ("DOG", "SFD·159/221", "犬形"), ("PORO", "OGN·052/298", "魄罗")];

    public static IEnumerable<object[]> Prints() => new[] { "UNL-177/219", "UNL-177a/219" }
        .Select(no => new object[] { no });
    public static IEnumerable<object[]> MissingTags() =>
        from no in new[] { "UNL-177/219", "UNL-177a/219" }
        from animal in Animals select new object[] { no, animal.Tag };

    private static MatchState Position(string no = "UNL-177/219", string? missing = null)
    {
        var state = ConquestTokenTriggerTests.Position(no);
        var source = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", no, "P1");
        // A legal earlier play chose one tag; the actual eight play-mode paths
        // are independently exercised by IvernTraitChoiceAuditTests.
        state = state with { RoomId = "IVERN-TRAIT-SCORE-" + no,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = source with { Tags = source.Tags.Where(tag => !Animals.Any(a => a.Tag == tag))
                    .Append(missing == "魄罗" ? "鸟类" : "魄罗").ToArray() } },
            RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(30, 30, new Dictionary<string, int> {
                    [RuneTrait.Red] = 8, [RuneTrait.Green] = 8, [RuneTrait.Blue] = 8,
                    [RuneTrait.Orange] = 8, [RuneTrait.Yellow] = 8 }),
                ["P2"] = new(30, 30, new Dictionary<string, int> { [RuneTrait.Red] = 8 }) } };
        foreach (var animal in Animals.Where(a => a.Tag != missing))
            state = RadGearTestPosition.Put(state, animal.Id, animal.No, "P1", "BASE");
        return state;
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
    }

    private static StackItemState ScoreSkill(Driver game)
        => Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo is "UNL-177/219" or "UNL-177a/219");

    [Theory, MemberData(nameof(Prints))]
    public async Task BothPrintingsEarnAnIndependentScoreAfterNaturalConquestAndResponses(string no)
    {
        var game = new Driver(Position(no));
        await Conquer(game);
        Assert.NotNull(ScoreSkill(game).UnitEvent);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.NotNull(ScoreSkill(game));
        await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    // Chinese UNL judge FAQ260511 section4/p12: a missing tag at capture
    // must not stop the skill from being earned. Check tags at resolution.
    [Theory, MemberData(nameof(MissingTags))]
    public async Task EachMissingTagStillQueuesTheSkillButDoesNotScoreAtResolution(string no, string missing)
    {
        var game = new Driver(Position(no, missing));
        await Conquer(game);
        Assert.NotNull(ScoreSkill(game));
        await game.Top();
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task ARealReturnResponseRechecksTheCurrentTagsInsteadOfTheirCapturedTruth(string no)
    {
        var state = RadGearTestPosition.Put(Position(no), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state);
        await Conquer(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["CAT"]));
        await game.Top();
        Assert.Contains("CAT", game.State.PlayerZones["P1"].Hand);
        Assert.NotNull(ScoreSkill(game));
        await game.Top();
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task ARealStandbyRevealCanSupplyTheMissingTagDuringTheResponseWindow(string no)
    {
        var state = Position(no, "猫科");
        state = RadGearTestPosition.Put(state, "OTHER", "OGN·296/298", "P1", "BATTLEFIELD", "OTHER");
        state = RadUnitEffectTestFixture.Place(state, "BIRD", "P1", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "CAT", "OGN·135/298", "P1", "HAND");
        for (var i = 0; i < 4; i++)
            state = RadGearTestPosition.Put(state, "P2-DRAW-" + i, "OGN·219/298", "P2", "MAIN_DECK");
        var game = new Driver(state);
        await game.Act("P1", new HideCardCommand("CAT", "OGN·135/298", "BATTLEFIELD:OTHER", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["CAT"].IsFaceDown);
        // Keep an actual public occupant and complete the placement turn.
        // A newly hidden standby card cannot be played in that same turn.
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.True(game.State.CardObjects["CAT"].IsFaceDown);
        var priorScore = game.State.PlayerScores["P1"];
        await Conquer(game);
        Assert.NotNull(ScoreSkill(game));
        await game.Act("P1", new RevealCardCommand("CAT", "OGN·135/298", []));
        // The real face-down unit enters the field before the score skill.
        while (game.State.StackItems[^1].UnitEvent is null) await game.Top();
        Assert.False(game.State.CardObjects["CAT"].IsFaceDown);
        Assert.Equal(priorScore + 1, game.State.PlayerScores["P1"]);
        await game.Top();
        Assert.Equal(priorScore + 2, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task ReturningTheSourceDoesNotCancelItsEarnedSkillWhenOtherAlliesHaveEveryTag(string no)
    {
        var state = RadGearTestPosition.Put(Position(no), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state);
        await Conquer(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.NotNull(ScoreSkill(game));
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("HAND", "P1")]
    [InlineData("MAIN_DECK", "P1")]
    [InlineData("GRAVEYARD", "P1")]
    [InlineData("BASE", "P2")]
    public async Task AnOffFieldOrOpposingAnimalCannotSupplyTheCurrentFriendlyTag(string zone, string owner)
    {
        var state = RadGearTestPosition.Put(Position(missing: "猫科"), "CAT", "OGN·135/298", owner, zone);
        var game = new Driver(state);
        await Conquer(game);
        Assert.NotNull(ScoreSkill(game));
        await game.Top();
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task TheActualOwnTurnHoldAlsoQueuesTheSameIndependentScore(string no)
    {
        var state = ExactOtherFriendlyHoldTests.Position(no, 0);
        var source = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = source with { Tags = source.Tags.Where(tag => !Animals.Any(a => a.Tag == tag)).Append("魄罗").ToArray() } } };
        foreach (var animal in Animals)
            state = RadGearTestPosition.Put(state, animal.Id, animal.No, "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.NotNull(ScoreSkill(game));
        await game.Top();
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("timing")]
    [InlineData("printing")]
    [InlineData("tick")]
    [InlineData("generation")]
    [InlineData("amount")]
    public async Task RecoveryBindsTheSkillToTheOriginalAcceptedConquestWithoutRewritingAnyJournal(string mutation)
    {
        var game = new Driver(Position());
        await Conquer(game);
        var item = ScoreSkill(game); var context = item.UnitEvent!;
        var beforeHistory = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        var changed = mutation switch {
            "timing" => item with { UnitEvent = context with { Timing = "HOLD" } },
            "printing" => item with { CardNo = "UNL-177a/219", UnitEvent = context with { CardNo = "UNL-177a/219" } },
            "tick" => item with { UnitEvent = context with { CapturedTick = context.CapturedTick + 1 } },
            "generation" => item with { UnitEvent = context with { SourceGeneration = context.SourceGeneration + 1 } },
            _ => item with { DamageAmount = 9 }
        };
        var forged = game.State with { StackItems = [changed] };
        Assert.Empty(ValidateOriginalHistory(game, game.State));
        Assert.NotEmpty(ValidateOriginalHistory(game, forged));
        Assert.Equal(beforeHistory, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
    }

    private static IReadOnlyList<string> ValidateOriginalHistory(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var sequence = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
    }
}
