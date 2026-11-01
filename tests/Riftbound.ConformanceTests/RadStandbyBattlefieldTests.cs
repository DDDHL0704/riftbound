using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadStandbyBattlefieldTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task BandleScoutSchoolQueuesDormantGoldForTheHidingPlayer(string player)
    {
        var game = new Driver(Position(player));
        await game.Act(player, Hide());
        Assert.True(game.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.Tags.Contains("金币"));
        var trigger = Assert.Single(game.State.StackItems);
        Assert.Equal(player, trigger.ControllerId);
        Assert.Equal("FIELD", trigger.SourceObjectId);
        Assert.Equal("STANDBY_HIDDEN_CREATE_EQUIPMENT_TOKEN", trigger.EffectKind);
        Assert.Equal(4, game.State.RunePools[player].TotalPower);
        await game.Top();
        var gold = Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains("金币"));
        Assert.True(gold.IsExhausted);
        Assert.Equal(player, gold.ControllerId);
        Assert.Equal(player, gold.OwnerId);
        Assert.Contains(gold.ObjectId, game.State.PlayerZones[player].Base);
        Assert.True(game.State.CardObjects["SHADOW"].IsFaceDown);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing-fee")]
    [InlineData("uncontrolled-field")]
    [InlineData("capacity")]
    public async Task InvalidHideDoesNotCreateGoldOrQueueASkill(string boundary)
    {
        var game = new Driver(Position("P1"));
        if (boundary == "capacity")
        {
            await game.Act("P1", Hide());
            await game.Top();
        }
        var command = Hide() with {
            SourceObjectId = boundary == "capacity" ? "SECOND" : "SHADOW",
            OptionalCosts = boundary == "missing-fee" ? [] : ["STANDBY_A"],
            Destination = boundary == "uncontrolled-field" ? "BATTLEFIELD:MISSING" : "BATTLEFIELD:FIELD" };
        var state = game.State;
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-school-hide", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        await game.Replay();
    }

    [Fact]
    public async Task HidingElsewhereDoesNotTriggerTheSchool()
    {
        var state = Position("P1");
        state = Put(state, "OTHER", "OGN·275/298", "P1", "BATTLEFIELD", "OTHER");
        state = Put(state, "OTHER-ANCHOR", "OGN·219/298", "P1", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await game.Act("P1", Hide() with { Destination = "BATTLEFIELD:OTHER" });
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.Tags.Contains("金币"));
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-field")]
    [InlineData("generation")]
    public async Task RecoveryRejectsForgedSchoolSkillBindings(string boundary)
    {
        var game = new Driver(Position("P1"));
        await game.Act("P1", Hide());
        var item = Assert.Single(game.State.StackItems);
        var context = item.StandbyObserver!;
        var forged = item with { StandbyObserver = boundary switch {
            "missing" => null,
            "wrong-field" => context with { BattlefieldId = "OTHER" },
            _ => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } } };
        var changed = game.State with { StackItems = [forged] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(changed), e => e.Contains("invalid captured standby play observer", StringComparison.Ordinal));
        await game.Replay();
    }

    private static HideCardCommand Hide() => new("SHADOW", "OGN·135/298", "BATTLEFIELD:FIELD", ["STANDBY_A"]);

    private static MatchState Position(string player)
    {
        var state = new MatchState("RAD-STANDBY-FIELD", 0, 5, player,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: player,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty, ["P2"] = PlayerZones.Empty },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(5, 5), ["P2"] = new(5, 5) }, seed: 1537);
        state = Put(state, "FIELD", "RAD·157/167", "P1", "BATTLEFIELD", "FIELD");
        state = Put(state, "ANCHOR", "OGN·219/298", player, "BATTLEFIELD", "FIELD");
        state = Put(state, "SHADOW", "OGN·135/298", player, "HAND");
        state = Put(state, "SECOND", "OGN·135/298", player, "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FIELD"] = state.CardObjects["FIELD"] with { ControllerId = player } } };
    }

    private static MatchState Put(MatchState state, string id, string no, string player, string zone, string? field = null)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) } };
        return RadUnitEffectTestFixture.Place(state, id, player, zone, field);
    }
}
