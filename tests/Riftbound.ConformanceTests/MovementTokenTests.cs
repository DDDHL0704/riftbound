using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ChosenMovementTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MovementTokenTests
{
    internal static MatchState Position(string card, string origin = "BATTLEFIELD")
        => MovementSimpleTriggerTests.Position(card, origin);
    internal static CardObjectState[] Tokens(MatchState state)
        => OfficialTokenReplacementTests.Tokens(state);
    internal static bool Sprite(string card) => card.StartsWith("UNL-082", StringComparison.Ordinal);
    internal static int Count(string card) => card == "SFD·179/221" ? 3 : 1;

    public static IEnumerable<object[]> Routes()
    {
        foreach (var card in new[] { "OGN·222/298", "SFD·179/221", "UNL-082/219", "UNL-082a/219" })
        foreach (var route in new[] { ("BASE", "BATTLEFIELD:BF2"), ("BATTLEFIELD", "BASE:P2"), ("BATTLEFIELD", "BATTLEFIELD:BF2") })
            yield return [card, route.Item1, route.Item2];
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task OfficialMovementCreatesDormantTokensAtTheSpecifiedLocation(string card, string origin, string destination)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card, origin), destination);
        Assert.Empty(Tokens(moved.State));
        if (!Sprite(card) && destination == "BASE:P2") { Assert.Empty(moved.State.StackItems); return; }
        Assert.Single(moved.State.StackItems); Restore(moved.State);
        var done = await Top(moved.State); var tokens = Tokens(done.State); Assert.Equal(Count(card), tokens.Length);
        var expectedZone = Sprite(card) ? origin : "BATTLEFIELD";
        var expectedField = Sprite(card) ? origin == "BASE" ? null : "BF" : "BF2";
        Assert.All(tokens, t => {
            Assert.Equal("P2", t.OwnerId); Assert.Equal("P2", t.ControllerId); Assert.True(t.IsExhausted);
            Assert.Equal(Sprite(card) ? 3 : 1, t.Power);
            Assert.Equal(Sprite(card), t.Tags.Contains(CardObjectTags.Ephemeral));
            Assert.Contains(Sprite(card) ? "仙灵" : CardObjectTags.MinionTokenFamily, t.Tags);
            Assert.Contains(Sprite(card) ? "仙灵" : "随从", t.Tags);
            Assert.Equal(new ObjectLocationState("P2", expectedZone, expectedField), done.State.ObjectLocations[t.ObjectId]);
        });
        Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·222/298")]
    [InlineData("SFD·179/221")]
    [InlineData("UNL-082/219")]
    [InlineData("UNL-082a/219")]
    public async Task HereUsesResolutionLocationWhileOriginalLocationIsCaptured(string card)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card));
        var changed = At(moved.State, "D", "P2", "BASE"); Restore(changed);
        var done = await Top(changed); Assert.Equal(Count(card), Tokens(done.State).Length);
        Assert.All(Tokens(done.State), t => Assert.Equal(Sprite(card) ? new("P2", "BATTLEFIELD", "BF") : new ObjectLocationState("P2", "BASE"), done.State.ObjectLocations[t.ObjectId]));
        Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·222/298")]
    [InlineData("SFD·179/221")]
    [InlineData("UNL-082/219")]
    [InlineData("UNL-082a/219")]
    public async Task SourceLeavingOnlyInvalidatesTheLiveHereReference(string card)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card)); var s = moved.State;
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Battlefields = s.PlayerZones["P2"].Battlefields.Where(id => id != "D").ToArray() }, ["P1"] = s.PlayerZones["P1"] with { Graveyard = [..s.PlayerZones["P1"].Graveyard, "D"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["D"] = new("P1", "GRAVEYARD") },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { ObjectGeneration = 1, MovementCountThisTurn = 0, ControllerId = "P1" } } };
        Restore(s); var done = await Top(s); Assert.Equal(Sprite(card) ? 1 : 0, Tokens(done.State).Length); Restore(done.State);
    }
    [Theory]
    [InlineData("OGN·222/298", false)] [InlineData("OGN·222/298", true)]
    [InlineData("SFD·179/221", false)] [InlineData("SFD·179/221", true)]
    [InlineData("UNL-082/219", false)] [InlineData("UNL-082/219", true)]
    [InlineData("UNL-082a/219", false)] [InlineData("UNL-082a/219", true)]
    public async Task EachTokenInstructionOffersSharedReplacementWithoutPrematureEntry(string card, bool accept)
    {
        var s = Position(card);
        foreach (var id in new[] { "Z1", "Z2" }) s = At(DeathObserverAuditTests.Source(s, "UNL-086/219", id), id, "P2", "BATTLEFIELD", "BF");
        var moved = await MovementObserverAuditTests.Move(s);
        var pending = await Top(moved.State); Assert.NotNull(pending.State.PendingCardChoice); Assert.Empty(Tokens(pending.State)); Restore(pending.State);
        if (accept)
        {
            pending = await OfficialTokenReplacementTests.Choose(pending.State, "Z2");
            Assert.Empty(Tokens(pending.State)); Restore(pending.State);
            pending = await OfficialTokenReplacementTests.Choose(pending.State, "Z1");
        }
        else
        {
            for (var index = 0; index < Count(card); index++)
            {
                Assert.NotNull(pending.State.PendingCardChoice); Assert.Empty(Tokens(pending.State));
                pending = await OfficialTokenReplacementTests.Choose(pending.State); Restore(pending.State);
            }
        }
        Assert.Null(pending.State.PendingCardChoice); Assert.Equal(Count(card) + (accept ? 2 : 0), Tokens(pending.State).Length);
        foreach (var id in new[] { "Z1", "Z2" }) Assert.Equal(accept, pending.State.CardObjects[id].UntilEndOfTurnEffects.Contains("TOKEN_ENTRY_REPLACEMENT_USED"));
        Assert.All(Tokens(pending.State), t => Assert.Equal(Sprite(card) ? "BF" : "BF2", pending.State.ObjectLocations[t.ObjectId].BattlefieldObjectId));
        Restore(pending.State);
    }

    [Theory]
    [InlineData("OGN·222/298")]
    [InlineData("SFD·179/221")]
    [InlineData("UNL-082/219")]
    public async Task ForbiddenTokenLocationDoesNotSpendAReplacement(string card)
    {
        var s = Position(card); var field = Sprite(card) ? "BF" : "BF2";
        s = At(DeathObserverAuditTests.Source(s, "UNL-086/219", "Z"), "Z", "P2", "BATTLEFIELD", "BF");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [field] = s.CardObjects[field] with { CardNo = "SFD·216/221" } } };
        var moved = await MovementObserverAuditTests.Move(s); var done = await Top(moved.State);
        Assert.Empty(Tokens(done.State)); Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", done.State.CardObjects["Z"].UntilEndOfTurnEffects); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·222/298")]
    [InlineData("SFD·179/221")]
    [InlineData("UNL-082/219")]
    public async Task ChangingControlDoesNotRedirectTheSkillOrPermitEnemyBaseEntry(string card)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card));
        var changed = At(moved.State, "D", "P1", "BASE"); Restore(changed);
        var done = await Top(changed); Assert.Equal(Sprite(card) ? 1 : 0, Tokens(done.State).Length);
        Assert.All(Tokens(done.State), t => { Assert.Equal("P2", t.ControllerId); Assert.Equal("BF", done.State.ObjectLocations[t.ObjectId].BattlefieldObjectId); });
        Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·222/298")]
    [InlineData("SFD·179/221")]
    [InlineData("UNL-082/219")]
    public async Task ARealFlashResponseRechecksHereAndKeepsBothLilliaOrigins(string card)
    {
        var s = Position(card);
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["FLASH"] = new("FLASH", cardNo: "OGS·011/024", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = [..s.PlayerZones["P2"].Hand, "FLASH"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(10, 10) } };
        var earned = await MovementObserverAuditTests.Move(s);
        if (earned.State.PriorityPlayerId != "P2") earned = await Act(earned.State, earned.State.PriorityPlayerId!, new PassPriorityCommand());
        var response = await Act(earned.State, "P2", new PlayCardCommand("FLASH", "OGS·011/024", ["D"]));
        var done = await Top(response.State);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), done.State.ObjectLocations["D"]);
        for (var index = 0; done.State.StackItems.Count > 0 && index < 8; index++) done = await Top(done.State);
        Assert.Empty(done.State.StackItems); Assert.Equal(Sprite(card) ? 2 : Count(card), Tokens(done.State).Length);
        if (Sprite(card)) Assert.Equal(new[] { "BF", "BF2" }, Tokens(done.State).Select(t => done.State.ObjectLocations[t.ObjectId].BattlefieldObjectId).Order());
        else Assert.All(Tokens(done.State), t => Assert.Equal(new ObjectLocationState("P2", "BASE"), done.State.ObjectLocations[t.ObjectId]));
        Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·222/298")]
    [InlineData("UNL-082/219")]
    public async Task ANewIncarnationCannotSupplyTheOldHereReference(string card)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card));
        var s = moved.State with { CardObjects = new Dictionary<string, CardObjectState>(moved.State.CardObjects) {
            ["D"] = moved.State.CardObjects["D"] with { ObjectGeneration = 1, MovementCountThisTurn = 0 } } };
        Restore(s); var done = await Top(s); Assert.Equal(Sprite(card) ? 1 : 0, Tokens(done.State).Length); Restore(done.State);
    }

}
