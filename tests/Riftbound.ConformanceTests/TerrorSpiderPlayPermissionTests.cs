using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

internal static class AloneEnemyBattlefieldTestPosition
{
    internal static MatchState Create()
        => RadGearTestPosition.Create() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = Pool(), ["P2"] = Pool() } };
    private static RunePool Pool() => new(30, 30, new Dictionary<string, int> {
        [RuneTrait.Orange] = 10, [RuneTrait.Purple] = 10, [RuneTrait.Green] = 10, [RuneTrait.Red] = 10 });
}

// CN355.2.b adds a location permission; it does not add reaction timing.
// CN740.2.a counts the enemy's friends here, including unit tokens.
public sealed class TerrorSpiderPlayPermissionTests
{
    private const string Spider = "UNL-117/219";
    private static MatchState Position(int enemies = 1)
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "PLAY", Spider, "P1", "HAND");
        for (var i = 0; i < enemies; i++) state = RadGearTestPosition.Put(state, "FOE" + i, "OGN·175/298", "P2", "BATTLEFIELD", "OTHER");
        return state;
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(2, false)]
    public async Task TheSpiderItselfMayBePlayedToAnEnemyBattlefieldOnlyWithAnAloneEnemy(int enemies, bool allowed)
    {
        var game = new Driver(Position(enemies)); var command = new PlayCardCommand("PLAY", Spider, [], Destination: "BATTLEFIELD:OTHER");
        if (allowed) {
            await game.Act("P1", command); Assert.Equal("OTHER", game.State.ObjectLocations["PLAY"].BattlefieldObjectId);
            Assert.True(game.State.CardObjects["PLAY"].IsExhausted); await game.Replay();
        } else await RadGearTestPosition.Reject(game.State, "P1", command);
    }

    [Theory]
    [InlineData("P1", "P2", true)] [InlineData("P2", "P1", false)]
    public async Task AVisibleSpiderGrantsThePermissionToItsControllersOtherFriendlyUnits(string controller, string owner, bool allowed)
    {
        var state = RadGearTestPosition.Put(Position(), "SOURCE", Spider, controller, "BASE");
        state = RadGearTestPosition.Put(state, "PLAY", "OGN·219/298", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SOURCE"] = state.CardObjects["SOURCE"] with { OwnerId = owner } } };
        var game = new Driver(state); var command = new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER");
        if (allowed) { await game.Act("P1", command); Assert.Equal("OTHER", game.State.ObjectLocations["PLAY"].BattlefieldObjectId); await game.Replay(); }
        else await RadGearTestPosition.Reject(game.State, "P1", command);
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")] [InlineData("FACE_DOWN")]
    public async Task AHiddenOrNonFieldSpiderDoesNotGrantAnOrdinaryUnitsDestination(string zone)
    {
        var state = RadGearTestPosition.Put(Position(), "SOURCE", Spider, "P1", zone == "FACE_DOWN" ? "BATTLEFIELD" : zone,
            zone == "FACE_DOWN" ? "BF" : null);
        state = RadGearTestPosition.Put(state, "PLAY", "OGN·219/298", "P1", "HAND");
        if (zone == "FACE_DOWN") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SOURCE"] = state.CardObjects["SOURCE"] with { IsFaceDown = true } } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
    }

    [Fact]
    public async Task EnemyEquipmentDoesNotMakeTheEnemyUnitLessAlone()
    {
        var state = RadGearTestPosition.Put(Position(), "ENEMY-GEAR", "OGN·017/298", "P2", "BATTLEFIELD", "OTHER");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY-GEAR"] = factory.CreateObject("ENEMY-GEAR", "P2", "P2", isExhausted: false) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Spider, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Equal("OTHER", game.State.ObjectLocations["PLAY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task AnotherEnemyUnitTokenCountsAsItsCompanionAndPreventsTheExtraDestination()
    {
        var state = RadGearTestPosition.Put(Position(), "TOKEN", "OGN·175/298", "P2", "BATTLEFIELD", "OTHER");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TOKEN"] = factory.CreateObject("TOKEN", "P2", "P2", isExhausted: false) } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("PLAY", Spider, [], Destination: "BATTLEFIELD:OTHER"));
    }

    [Fact]
    public async Task AnEnemyBaseOnlyRestrictionStillOverridesTheExtraPlayPermission()
    {
        var state = RadGearTestPosition.Put(Position(), "WARDEN", "OGN·070/298", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("PLAY", Spider, [], Destination: "BATTLEFIELD:OTHER"));
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("PLAY", Spider, [], Destination: "BASE"));
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task TheLocationPermissionDoesNotAllowPlayingAnOrdinaryUnitAsAReaction()
    {
        var state = RadGearTestPosition.Put(Position(), "WAIT", "OGN·058/298", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("WAIT", "OGN·058/298", ["ALLY"]));
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("PLAY", Spider, [], Destination: "BATTLEFIELD:OTHER"));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task TheFriendlyPermissionAlsoOffersAnAloneEnemyDestinationForAnUnrestrictedReadyUnitToken()
    {
        var state = RadGearTestPosition.Put(Position(), "SOURCE", Spider, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "SUMMON", "OGN·094/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PlayCardCommand("SUMMON", "OGN·094/298", [])); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice); Assert.Equal("TOKEN_PLAY_LOCATION", choice.Request.Kind);
        Assert.Contains(choice.Request.Options, option => option.Id == "BATTLEFIELD:OTHER");
        await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:OTHER"]));
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is not null);
        Assert.Equal("OTHER", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId); Assert.False(token.IsExhausted); await game.Replay();
    }
}
