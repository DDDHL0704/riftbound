using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldTargetRelationTests
{
    private const string Skullcrack = "OGN·220/298";
    private const string HeroicCharge = "UNL-155/219";
    private const string Unit = "OGN·219/298";

    [Theory]
    [InlineData(Skullcrack, true)]
    [InlineData(HeroicCharge, false)]
    public async Task DefendingSpellUsesUnitControllerAndOnlyOffersSameBattlefieldPairs(string card, bool stunsFriend)
    {
        var state = MoonfallTests.BeforeBattle();
        state = RadGearTestPosition.Put(state, "SPELL", card, "P2", "HAND");
        state = RadGearTestPosition.Put(state, "CROSS-FIELD-ENEMY", Unit, "P1", "BATTLEFIELD", "BF2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(8, 0) } };
        state = (await Act(state, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []))).State;
        if (state.FocusPlayerId == "P1")
            state = (await Act(state, "P1", new PassFocusCommand())).State;
        Assert.Equal("P2", state.FocusPlayerId);
        Assert.Equal("P2", state.CardObjects["BF"].ControllerId);

        var legal = new PlayCardCommand("SPELL", card, ["D", "A"]);
        var illegal = legal with { TargetObjectIds = ["D", "CROSS-FIELD-ENEMY"] };
        var engine = new CoreRuleEngine();
        var prompt = ResolutionResult.BuildPrompts(state)["P2"];
        PlayCostQuoteDto Quote(PlayCardCommand command) =>
            engine.PreviewPlayCard(state, "P2", new("quote", prompt.PromptId!, state.Tick, command));
        Assert.True(Quote(legal).IsValid);
        Assert.False(Quote(illegal).IsValid);
        await RadGearTestPosition.Reject(state, "P2", illegal);

        var candidate = Assert.Single(prompt.Candidates!, c => c.Enabled && c.Action == CommandTypes.PlayCard);
        var sourceRows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]);
        var source = Assert.Single(sourceRows, row => Equals(row["sourceObjectId"], "SPELL"));
        var pairs = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyList<string>>>(source["legalTargetSelections"]);
        Assert.Contains(pairs, pair => pair.SequenceEqual(legal.TargetObjectIds));
        Assert.DoesNotContain(pairs, pair => pair.SequenceEqual(illegal.TargetObjectIds));

        var cast = await Act(state, "P2", legal);
        var done = await Top(cast.State);
        Assert.Contains("STUNNED", done.State.CardObjects["A"].UntilEndOfTurnEffects);
        Assert.Equal(stunsFriend, done.State.CardObjects["D"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.Equal(stunsFriend ? 0 : 1, done.State.CardObjects["D"].UntilEndOfTurnPowerModifier);
        Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·260/298", "")]
    [InlineData("UNL-192/219", "")]
    [InlineData("SFD·114/221", "")]
    [InlineData("SFD·200/221", "")]
    [InlineData("VEN·140", "BATTLEFIELD:BF")]
    public async Task EnemyUnitTargetMatchesItsControllerAtEitherBattlefield(string card, string destination)
    {
        var state = RadGearTestPosition.Create();
        state = RadGearTestPosition.Put(state, "SPELL", card, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY-AT-OUR-FIELD", Unit, "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "ENEMY-AT-THEIR-FIELD", Unit, "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "FRIEND-AT-THEIR-FIELD", Unit, "P1", "BATTLEFIELD", "OTHER");

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Enabled && c.Action == CommandTypes.PlayCard);
        var sources = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]);
        var source = Assert.Single(sources, row => Equals(row["sourceObjectId"], "SPELL"));
        var byIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(source["targetChoicesByIndex"]);
        var targets = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(byIndex["1"]);
        Assert.Contains(targets, target => target.Id == "ENEMY-AT-OUR-FIELD");
        Assert.Contains(targets, target => target.Id == "ENEMY-AT-THEIR-FIELD");
        Assert.DoesNotContain(targets, target => target.Id == "FRIEND-AT-THEIR-FIELD");

        var command = new PlayCardCommand("SPELL", card, ["ALLY", "ENEMY-AT-OUR-FIELD"], Destination: destination);
        var engine = new CoreRuleEngine();
        PlayCostQuoteDto Quote(PlayCardCommand play) =>
            engine.PreviewPlayCard(state, "P1", new("quote", prompt.PromptId!, state.Tick, play));
        Assert.True(Quote(command).CanPay);
        Assert.True(Quote(command with { TargetObjectIds = ["ALLY", "ENEMY-AT-THEIR-FIELD"] }).CanPay);
        var invalid = command with { TargetObjectIds = ["ALLY", "FRIEND-AT-THEIR-FIELD"] };
        Assert.False(Quote(invalid).IsValid);
        await RadGearTestPosition.Reject(state, "P1", invalid);

        var cast = await Act(state, "P1", command);
        Assert.Contains(cast.State.StackItems, item =>
            item.SourceObjectId == "SPELL" && item.TargetObjectIds.SequenceEqual(command.TargetObjectIds));
    }

    [Fact]
    public async Task AResponseSplitsTheGroupBeforeEitherTargetReceivesItsEffect()
    {
        var state = RadGearTestPosition.Create();
        state = RadGearTestPosition.Put(state, "CHARGE", HeroicCharge, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "MOVE", "VEN·034", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "TARGET-FRIEND", Unit, "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "TARGET-ENEMY", Unit, "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "OTHER-HOLDER", Unit, "P1", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1" } } };

        var charge = await Act(state, "P1", new PlayCardCommand("CHARGE", HeroicCharge, ["TARGET-FRIEND", "TARGET-ENEMY"]));
        var response = await Act(charge.State, "P1", new PlayCardCommand("MOVE", "VEN·034", ["OTHER", "TARGET-FRIEND"]));
        var split = await Top(response.State);
        Assert.Equal("OTHER", split.State.ObjectLocations["TARGET-FRIEND"].BattlefieldObjectId);

        var opened = await Top(split.State);
        Assert.NotNull(opened.State.PendingRuleChoice);
        Assert.Equal("TARGET_GROUP", opened.State.PendingRuleChoice.Request.Kind);
        Assert.DoesNotContain("STUNNED", opened.State.CardObjects["TARGET-ENEMY"].UntilEndOfTurnEffects);

        var selection = await GroupedTargetTests.Select(opened.State, "REMOVE:TARGET-FRIEND");
        var done = await GroupedTargetTests.Select(selection.State, "CONFIRM");
        Assert.Equal(2, done.State.CardObjects["TARGET-FRIEND"].UntilEndOfTurnPowerModifier);
        Assert.Contains("STUNNED", done.State.CardObjects["TARGET-ENEMY"].UntilEndOfTurnEffects);
        Restore(done.State);
    }

    [Fact]
    public async Task StandbyRequiresBothUnitsAtItsOwnBattlefield()
    {
        var state = await StandbySourceRulesTests.ReadyShadow(Skullcrack);
        state = RadGearTestPosition.Put(state, "SAME-FIELD-ENEMY", Unit, "P2", "BATTLEFIELD", "F");
        state = RadGearTestPosition.Put(state, "SEPARATE-FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "SEPARATE-FIELD");
        state = RadGearTestPosition.Put(state, "OTHER-FIELD-ENEMY", Unit, "P2", "BATTLEFIELD", "SEPARATE-FIELD");
        var cast = new RevealCardCommand("SHADOW", Skullcrack, ["HOST", "SAME-FIELD-ENEMY"]);

        await RadGearTestPosition.Reject(state, "P1", cast with { TargetObjectIds = ["HOST", "OTHER-FIELD-ENEMY"] });
        var played = await Act(state, "P1", cast);
        var done = await Top(played.State);
        Assert.Contains("STUNNED", done.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Contains("STUNNED", done.State.CardObjects["SAME-FIELD-ENEMY"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED", done.State.CardObjects["OTHER-FIELD-ENEMY"].UntilEndOfTurnEffects);
        Restore(done.State);
    }
}
