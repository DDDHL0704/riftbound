using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class WildclawAlphaTargetProtectionTests
{
    private const string Beast = "UNL-057/219", DarkBlade = "OGN·213/298", Charm = "OGN·043/298";

    private static MatchState Position(string sourceAt = "BF", string targetAt = "BF", string targetNo = "SFD·094/221")
    {
        static string Zone(string at) => at is "BF" or "OTHER" ? "BATTLEFIELD" : at;
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "BEAST", Beast,
            "P2", Zone(sourceAt), sourceAt is "BF" or "OTHER" ? sourceAt : null);
        state = RadGearTestPosition.Put(state, "TARGET", targetNo, "P2", Zone(targetAt), targetAt is "BF" or "OTHER" ? targetAt : null);
        state = RadGearTestPosition.Put(state, "STUDENT", "OGN·103/298", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "DARK", DarkBlade, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "CHARM", Charm, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "TRAIN", "OGN·058/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "BLINK", "OGS·011/024", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "HOST", "OGN·017/298", "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
    }

    private static async Task GiveAction(Driver game, string player)
    {
        if (game.State.PriorityPlayerId is { } priority && priority != player)
            await game.Act(priority, new PassPriorityCommand());
        else if (game.State.FocusPlayerId is { } focus && focus != player)
            await game.Act(focus, new PassFocusCommand());
    }

    private static async Task RejectTarget(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-current-might-target", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static async Task GrowAfterDarkBlade(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("DARK", DarkBlade, ["TARGET"]));
        await GiveAction(game, "P2");
        await game.Act("P2", new PlayCardCommand("TRAIN", "OGN·058/298", ["BEAST"])); await game.Top();
        Assert.Equal(9, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["BEAST"]));
    }

    private static async Task ResolveRemaining(Driver game)
    {
        for (var step = 0; step < 4 && game.State.StackItems.Count > 0; step++) await game.Top();
        Assert.Empty(game.State.StackItems);
    }

    [Fact]
    public async Task PrintedAbilityParsesTheSameLocationCurrentMightComparison()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == Beast);
        Assert.Equal(7, card.Power);
        Assert.Single(StaticAbilityParser.Parse(card.CardEffect!, []),
            ability => ability.Kind == StaticAbilityKinds.HereLowerMightFriendlyUnitEnemyTargetProtection);
    }

    // CN UNL judge FAQ260511 p23, all three questions, using the named cards.
    [Fact]
    public async Task OfficialFaqResponseGrowthInvalidatesBothDarkBladeInstructionsButTheStudentStillObservesTheSpell()
    {
        var game = new Driver(Position()); await GrowAfterDarkBlade(game);
        var hand = game.State.PlayerZones["P2"].Hand.Count; var before = game.Events().Length;
        await ResolveRemaining(game);
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(hand, game.State.PlayerZones["P2"].Hand.Count);
        Assert.DoesNotContain(game.Events().Skip(before), entry => entry.Event.Kind == "CARD_DRAWN"
            && Equals(entry.Event.Payload.GetValueOrDefault("playerId"), "P2"));
        Assert.Equal(3, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["STUDENT"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE", "BASE", "OGN·219/298", false)]
    [InlineData("BF", "BF", "OGN·219/298", false)]
    [InlineData("OTHER", "BF", "OGN·219/298", true)]
    [InlineData("BASE", "BF", "OGN·219/298", true)]
    [InlineData("HAND", "BF", "OGN·219/298", true)]
    [InlineData("GRAVEYARD", "BF", "OGN·219/298", true)]
    [InlineData("BF", "BF", "SFD·094/221", true)]
    [InlineData("BF", "BF", "OGN·088/298", true)]
    public async Task OnlyStrictlyLowerCurrentMightAtTheSamePublicLocationPreventsTheEnemySpell(
        string sourceAt, string targetAt, string targetNo, bool accepted)
    {
        var game = new Driver(Position(sourceAt, targetAt, targetNo));
        var command = new PlayCardCommand("CHARM", Charm, ["TARGET"], Destination: "BATTLEFIELD:OTHER");
        if (!accepted) await RejectTarget(game.State, "P1", command);
        else {
            await game.Act("P1", command); await ResolveRemaining(game);
            Assert.Equal("OTHER", game.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeActivatedSkillsUseTheSameStrictCurrentMightProtection(bool equal)
    {
        var game = new Driver(Position(targetNo: equal ? "SFD·094/221" : "OGN·219/298"));
        var ability = PrintedExhaustAbilityTests.Ability(game.State);
        var command = new ActivateAbilityCommand("HOST", ability, ["TARGET"]);
        if (!equal) {
            await RejectTarget(game.State, "P1", command); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        } else {
            await game.Act("P1", command); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            await ResolveRemaining(game); Assert.Equal(2, game.State.CardObjects["TARGET"].Damage);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CurrentControlGovernsProtectionEvenWhenBothPhysicalCardsBelongToTheOtherPlayer(bool ownSpell)
    {
        var initial = Position(targetNo: "OGN·219/298");
        initial = initial with { TurnPlayerId = ownSpell ? "P2" : "P1", ActivePlayerId = ownSpell ? "P2" : "P1",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BEAST"] = initial.CardObjects["BEAST"] with { OwnerId = "P1" },
                ["TARGET"] = initial.CardObjects["TARGET"] with { OwnerId = "P1" } } };
        var game = new Driver(initial);
        Assert.Equal(4, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["TARGET"]));
        if (!ownSpell) await RejectTarget(game.State, "P1", new PlayCardCommand("CHARM", Charm, ["TARGET"], Destination: "BATTLEFIELD:OTHER"));
        else {
            await game.Act("P2", new PlayCardCommand("TRAIN", "OGN·058/298", ["TARGET"])); await ResolveRemaining(game);
            Assert.Equal(6, CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["TARGET"]));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("RETURN", "OGN·104/298")] [InlineData("BLINK", "OGS·011/024")]
    public async Task ARealSourceDepartureEndsItsProtectionBeforeTheOriginalDarkBladeResolves(string source, string no)
    {
        var game = new Driver(Position()); await GrowAfterDarkBlade(game); await GiveAction(game, "P2");
        await game.Act("P2", new PlayCardCommand(source, no, ["BEAST"])); await game.Top();
        Assert.DoesNotContain("BEAST", game.State.PlayerZones["P2"].Battlefields);
        var hand = game.State.PlayerZones["P2"].Hand.Count; await ResolveRemaining(game);
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(hand + 2, game.State.PlayerZones["P2"].Hand.Count); await game.Replay();
    }

    [Fact]
    public async Task AnActuallyAnimatedEquipmentUnitReceivesTheSameProtectionInItsCurrentBase()
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "BEAST", Beast, "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "HOST", "OGN·017/298", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "LEGEND", "RAD·145/167", "P1", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "CHARM", Charm, "P2", "HAND");
        for (var i = 0; i < 3; i++) initial = RadGearTestPosition.Put(initial, "P2-BLUE-" + i, "UNL-R03", "P2", "BASE");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Where(id => id != "LEGEND").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
        var game = new Driver(OriannaEquipmentConsumerFixture.WithResources(initial));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "HOST");
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["HOST"].Tags);
        Assert.Contains(CardObjectTags.EquipmentCard, game.State.CardObjects["HOST"].Tags);
        Assert.True(CoreRuleEngine.ResolveCurrentFieldUnitPower(game.State, game.State.CardObjects["HOST"]) < 7);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", [])); await GiveAction(game, "P2");
        await game.TapRunes("P2", ["P2-BLUE-0", "P2-BLUE-1"]);
        await game.Act("P2", new RecycleRuneCommand("P2-BLUE-2"));
        await RejectTarget(game.State, "P2", new PlayCardCommand("CHARM", Charm, ["HOST"], Destination: "BATTLEFIELD:OTHER"));
        await game.Replay();
    }
}
