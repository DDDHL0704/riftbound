using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.GroupedTargetTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ChosenMovementTests
{
    internal const string Spell = "UNL-054/219";
    internal static MatchState Board(int count = 2, int power = 2)
    {
        var s = OtherField(Group(count, power));
        return s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["AOE"] = s.CardObjects["AOE"] with { CardNo = Spell } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(4, 1) } };
    }
    internal static PlayCardCommand Command(string destination, params string[] ids) => new("AOE", Spell, ids, Destination: destination);
    internal static PlayCostQuoteDto Quote(MatchState s, string destination, params string[] ids) =>
        new CoreRuleEngine().PreviewPlayCard(s, "P1", new("quote", ResolutionResult.BuildPrompts(s)["P1"].PromptId!, s.Tick, Command(destination, ids)));
    internal static async Task<ResolutionResult> Cast(MatchState s, string destination, params string[] ids) => await Top((await Act(s, "P1", Command(destination, ids))).State);

    [Theory]
    [InlineData("BASE:P2")] [InlineData("BATTLEFIELD:BF2")]
    public async Task MovesAllSelectedUnitsToTheChosenLocationWithoutChangingState(string destination)
    {
        var s = Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { Damage = 1, IsExhausted = true, OwnerId = "P1" } } };
        Assert.True(Quote(s, destination, "D", "D2").CanPay);
        var done = await Cast(s, destination, "D", "D2");
        foreach (var id in Ids(2)) { Assert.Equal(destination.StartsWith("BASE") ? "BASE" : "BATTLEFIELD", done.State.ObjectLocations[id].Zone); Assert.Equal(destination.StartsWith("BASE") ? null : "BF2", done.State.ObjectLocations[id].BattlefieldObjectId); }
        Assert.Equal(1, done.State.CardObjects["D"].Damage); Assert.True(done.State.CardObjects["D"].IsExhausted);
        Assert.Equal("P1", done.State.CardObjects["D"].OwnerId); Assert.Equal("P2", done.State.CardObjects["D"].ControllerId);
        Assert.Equal(0, done.State.CardObjects["D"].ObjectGeneration); Restore(done.State);
    }

    [Fact]
    public async Task SourcesCanComeFromBaseAndDifferentBattlefields()
    {
        var s = At(Board(3), "D2", "P2", "BASE");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BF3"] = s.CardObjects["BF"] with { ObjectId = "BF3" } } };
        s = At(At(s, "BF3", "P2", "BATTLEFIELD", "BF3"), "D3", "P2", "BATTLEFIELD", "BF3");
        var done = await Cast(s, "BATTLEFIELD:BF2", Ids(3));
        Assert.All(Ids(3), id => Assert.Equal("BF2", done.State.ObjectLocations[id].BattlefieldObjectId)); Restore(done.State);
    }

    [Theory]
    [InlineData(0)] [InlineData(9)] [InlineData(12)]
    public async Task AnyNumberIncludesZeroAndMoreThanEightZeroPowerTargets(int count)
    {
        var done = await Cast(Board(Math.Max(count, 1), 0), "BATTLEFIELD:BF2", Ids(count));
        Assert.Equal(count, done.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.All(Ids(count), id => Assert.Equal("BF2", done.State.ObjectLocations[id].BattlefieldObjectId)); Restore(done.State);
    }

    [Theory]
    [InlineData("")] [InlineData("BASE:P1")] [InlineData("BASE:P3")]
    [InlineData("BATTLEFIELD:BF")] [InlineData("BATTLEFIELD:ABSENT")] [InlineData("HAND:P2")]
    public async Task RejectsMissingForgedOrUnchangedDestinationBeforePayment(string destination)
    {
        var s = Board(); Assert.False(Quote(s, destination, "D").IsValid);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("bad", "P1", CommandTypes.PlayCard), Command(destination, "D"), default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData(4, true)] [InlineData(5, false)]
    public void TotalPowerUsesTheOfficialEightLimit(int power, bool valid)
    { Assert.Equal(valid, Quote(Board(2, power), "BATTLEFIELD:BF2", "D", "D2").IsValid); }

    [Fact]
    public async Task WardStillChargesForEveryChosenEnemy()
    {
        var s = Board(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        Assert.False(Quote(s, "BATTLEFIELD:BF2", "D", "D2").CanPay);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(4, 3) } };
        var done = await Cast(s, "BATTLEFIELD:BF2", "D", "D2"); Assert.Equal(0, done.State.RunePools["P1"].Mana); Restore(done.State);
    }

    [Fact]
    public async Task EquipmentFollowsTheExactBattlefieldAndKeepsItsState()
    {
        var s = Board(1); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["GEAR"] = new("GEAR", ownerId: "P2", controllerId: "P2", attachedToObjectId: "D", isExhausted: true, tags: [CardObjectTags.EquipmentCard]) } };
        s = At(s, "GEAR", "P2", "BATTLEFIELD", "BF");
        var done = await Cast(s, "BATTLEFIELD:BF2", "D");
        Assert.Equal("BF2", done.State.ObjectLocations["GEAR"].BattlefieldObjectId); Assert.True(done.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal("D", done.State.CardObjects["GEAR"].AttachedToObjectId); Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_MOVED_WITH_UNIT"); Restore(done.State);
    }

    [Fact]
    public async Task RealResponseIncreasingPowerLetsCasterChooseALegalSubset()
    {
        var s = Board(2, 4); s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BUFF"] = new("BUFF", cardNo: "SFD·151/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = [..s.PlayerZones["P2"].Hand, "BUFF"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) } };
        var cast = await Act(s, "P1", Command("BATTLEFIELD:BF2", "D", "D2"));
        var pass = await Act(cast.State, "P1", new PassPriorityCommand());
        var buff = await Act(pass.State, "P2", new PlayCardCommand("BUFF", "SFD·151/221", ["D", "D2"]));
        var done = await Top((await Top(buff.State)).State); Assert.NotNull(done.State.PendingRuleChoice);
        Assert.All(Ids(2), id => Assert.Equal("BF", done.State.ObjectLocations[id].BattlefieldObjectId)); Restore(done.State);
        done = await Select(done.State, "REMOVE:D"); done = await Select(done.State, "CONFIRM");
        Assert.Equal("BF", done.State.ObjectLocations["D"].BattlefieldObjectId); Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId); Restore(done.State);
    }

    [Theory]
    [InlineData("prohibited")] [InlineData("already-there")] [InlineData("generation")]
    public async Task ResolutionHonorsMovementRestrictionsCurrentLocationAndObjectIdentity(string branch)
    {
        var cast = await Act(Board(), "P1", Command("BATTLEFIELD:BF2", "D", "D2")); var s = cast.State;
        if (branch == "already-there") s = At(s, "D", "P2", "BATTLEFIELD", "BF2");
        else s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { ObjectGeneration = branch == "generation" ? 1 : 0, UntilEndOfTurnEffects = branch == "prohibited" ? ["MOVEMENT_PROHIBITED:P1"] : [] } } };
        var done = await Top(s); Assert.Single(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId); Restore(done.State);
    }
    [Fact]
    public async Task EmptySelectionNeedsNoDestination()
    { var done = await Cast(Board(), ""); Assert.DoesNotContain(done.Events, e => e.Kind.StartsWith("UNIT_MOVED")); Restore(done.State); }

    [Fact]
    public async Task EnteringOpposingBattlefieldUsesTheNormalContestWindow()
    {
        var s = Board(1); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BF2"] = s.CardObjects["BF2"] with { ControllerId = "P1" } } };
        var done = await Cast(s, "BATTLEFIELD:BF2", "D");
        Assert.True(done.State.SpellDuelState.IsActive); Assert.Equal("BF2", done.State.ObjectLocations["D"].BattlefieldObjectId); Restore(done.State);
    }

    [Fact]
    public void GroupRequiresOneControllerEvenWhenBothAreOpponents()
    {
        var s = Board(); s = s with {
            Seats = new Dictionary<string, string>(s.Seats) { ["P3"] = "P3" },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P3"] = PlayerZones.Empty },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P3"] = RunePool.Empty } };
        s = At(s, "D2", "P3", "BASE");
        Assert.False(Quote(s, "BATTLEFIELD:BF2", "D", "D2").IsValid);
        Assert.True(Quote(s, "BATTLEFIELD:BF2", "D2").IsValid);
    }

    [Fact]
    public async Task BattlefieldPreventsReturnToBaseButAllowsAnotherBattlefield()
    {
        var s = Board(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BF"] = s.CardObjects["BF"] with { CardNo = "OGN·295/298" } } };
        var blocked = await Cast(s, "BASE:P2", "D"); Assert.Equal("BF", blocked.State.ObjectLocations["D"].BattlefieldObjectId);
        var moved = await Cast(s, "BATTLEFIELD:BF2", "D"); Assert.Equal("BF2", moved.State.ObjectLocations["D"].BattlefieldObjectId); Restore(moved.State);
    }

    [Fact]
    public async Task EffectRecastKeepsMovementDestinationSeparateFromCardPlayLocation()
    {
        // Kaisa requires mana cost strictly below the score (five after conquest).
        // Fizz's printed three-mana ceiling would exclude Tentacles.
        var s = OfficialGraveyardRecastTests.Kaisa("OGN·112/298");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["C"] = s.CardObjects["C"] with { CardNo = Spell } },
            PlayerScores = new Dictionary<string, int>(s.PlayerScores) { ["P1"] = 4 } };
        var conquered = await ConquestLifecycleRegressionTests.Conquer(s);
        var opened = await Top(conquered.State); Assert.NotNull(opened.State.PendingEffectPlay);
        var seed = Board(1);
        s = opened.State with { CardObjects = new Dictionary<string, CardObjectState>(opened.State.CardObjects) { ["D"] = seed.CardObjects["D"], ["BF2"] = seed.CardObjects["BF2"] } };
        s = At(At(s, "D", "P2", "BASE"), "BF2", "P2", "BATTLEFIELD", "BF2"); Restore(s);
        var command = new PlayCardCommand("C", Spell, ["D"], Destination: "BATTLEFIELD:BF2");
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", new("quote", ResolutionResult.BuildPrompts(s)["P1"].PromptId!, s.Tick, command));
        Assert.True(quote.IsValid, quote.Message);
        var played = await Act(s, "P1", command);
        var done = await Top(played.State); Assert.Equal("BF2", done.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Contains("C", done.State.PlayerZones["P1"].MainDeck); Restore(done.State);
    }

}
