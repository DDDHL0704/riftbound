using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldSourceRestrictionTests
{
    private static MatchState Position(string actor = "P2", bool wardenOnBattlefield = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "WARDEN", "OGN·070/298", "P1",
            wardenOnBattlefield ? "BATTLEFIELD" : "BASE", wardenOnBattlefield ? "BF" : null);
        state = RadGearTestPosition.Put(state, "READY", "OGN·146/298", actor, "HAND");
        state = RadGearTestPosition.Put(state, "PLAY", "OGN·219/298", actor, "HAND");
        return state with { TurnPlayerId = actor, ActivePlayerId = actor,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true },
                ["ENEMY"] = state.CardObjects["ENEMY"] with { IsExhausted = true } } };
    }

    [Theory]
    [InlineData("P1", true, true)]
    [InlineData("P2", true, false)]
    [InlineData("P2", false, true)]
    public async Task TheCurrentOpponentCanOnlyPlayUnitsToTheirOwnBaseWhileTheSourceIsOnABattlefield(
        string actor, bool onBattlefield, bool allowed)
    {
        var game = new Driver(Position(actor, onBattlefield));
        var destination = "BATTLEFIELD:" + (actor == "P1" ? "BF" : "OTHER");
        if (!allowed) {
            await RadGearTestPosition.Reject(game.State, actor,
                new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: destination));
            Assert.Contains("PLAY", game.State.PlayerZones[actor].Hand);
            destination = "BASE";
        }
        await game.Act(actor, new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["PLAY"].Zone);
        Assert.True(game.State.CardObjects["PLAY"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "P2", true)] [InlineData("P2", "P2", true)]
    [InlineData("P1", "P1", false)] [InlineData("P2", "P1", false)]
    public async Task ReadyPreventionDependsOnTheUnitsControllerRegardlessOfWhoCastsTheSpell(
        string caster, string controller, bool prevented)
    {
        var game = new Driver(Position(caster));
        var target = controller == "P1" ? "ALLY" : "ENEMY";
        var mana = game.State.RunePools[caster].Mana;
        await game.Act(caster, new PlayCardCommand("READY", "OGN·146/298", [target]));
        Assert.True(game.State.RunePools[caster].Mana < mana);
        await game.Top();
        Assert.Equal(prevented, game.State.CardObjects[target].IsExhausted);
        Assert.Contains("READY", game.State.PlayerZones[caster].Graveyard);
        if (prevented) Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_READIED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), target));
        await game.Replay();
    }

    [Fact]
    public async Task ASourceInBaseDoesNotPreventEnemyReadyEffects()
    {
        var game = new Driver(Position(wardenOnBattlefield: false));
        await game.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ENEMY"]));
        await game.Top();
        Assert.False(game.State.CardObjects["ENEMY"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceInResponseAllowsThePendingReadyInstruction()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ENEMY"]));
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["WARDEN"]));
        await game.Top();
        Assert.Contains("WARDEN", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["ENEMY"].IsExhausted);
        await game.Top();
        Assert.False(game.State.CardObjects["ENEMY"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task RestrictionsFollowTheSourcesCurrentControlInsteadOfItsOwner()
    {
        var initial = RadUnitEffectTestFixture.Place(Position("P1"), "WARDEN", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        Assert.Equal("P1", game.State.CardObjects["WARDEN"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["WARDEN"].ControllerId);
        await RadGearTestPosition.Reject(game.State, "P1",
            new PlayCardCommand("PLAY", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["ALLY"]));
        await game.Top();
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task TheWakeTaskCanStillReadyEnemyUnitsAndEquipment()
    {
        var initial = RadGearTestPosition.Put(Position("P1"), "GEAR", "SFD·022/221", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.False(game.State.CardObjects["ENEMY"].IsExhausted);
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ActiveEntryIsAReplacementAndStillWorksInTheOpponentsBase()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "ACTIVE", "ARC-004/006", "P2", "HAND"));
        await game.Act("P2", new PlayCardCommand("ACTIVE", "ARC-004/006", [], Destination: "BASE"));
        Assert.Contains("ACTIVE", game.State.PlayerZones["P2"].Base);
        Assert.False(game.State.CardObjects["ACTIVE"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeAndBorrowedEquipmentReadySkillsPayTheirCostsButCannotReadyEnemyGear(bool borrowed)
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR", "SFD·022/221", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "HEIMER", "OGN·111/298", "P2", "BASE");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["JAYCE"] = OfficialGarenConquestBattlefieldCountTests.Printed("JAYCE", "VEN·149", "P2"),
                ["GEAR"] = initial.CardObjects["GEAR"] with { IsExhausted = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { LegendZone = ["JAYCE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["JAYCE"] = new("P2", "LEGEND") } };
        var game = new Driver(initial); var mana = initial.RunePools["P2"].Mana;
        if (borrowed) {
            var skill = Assert.Single(BorrowedExhaustRules.Abilities(game.State, "P2", game.State.CardObjects["HEIMER"]),
                ability => ability.Donor.ObjectId == "JAYCE" && ability.Spec.Kind == "READY_EQUIPMENT" && ability.Spec.Targets == 1);
            await game.Act("P2", new ActivateAbilityCommand("HEIMER", skill.Id, ["GEAR"]));
        }
        else await game.Act("P2", new LegendActCommand("JAYCE", LegendActionAbilityCatalog.JayceLegendReadyOneAbilityId,
            ["GEAR"], ["SPEND_MANA:1"]));
        await game.Top();
        Assert.Equal(mana - 1, game.State.RunePools["P2"].Mana);
        Assert.True(game.State.CardObjects[borrowed ? "HEIMER" : "JAYCE"].IsExhausted);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_READIED");
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P2", true)]
    public async Task FixedBattlefieldTokenInstructionsRespectTheSameBaseOnlyRestriction(string actor, bool prevented)
    {
        var initial = RadGearTestPosition.Put(Position(actor), "TOKENS", "RAD·007/167", actor, "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        var field = actor == "P1" ? "OTHER" : "BF";
        await game.Act(actor, new PlayCardCommand("TOKENS", "RAD·007/167", [field]));
        await game.Top();
        var tokens = game.State.CardObjects.Values.Where(card => card.TokenFactoryCardNo is not null
            && card.Tags.Contains(CardObjectTags.UnitCard)).ToArray();
        Assert.Equal(prevented ? 0 : 3, tokens.Length);
        Assert.All(tokens, token => Assert.Equal(field, game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId));
        Assert.Equal(prevented, game.Events().Any(entry => entry.Event.Kind == "UNIT_PLAY_PREVENTED"));
        await game.Replay();
    }

    [Fact]
    public async Task PreventingReadyDoesNotPreventConsumingThenGrantingBoonsInTheSameSpell()
    {
        var initial = RadGearTestPosition.Put(Position(), "BOONS", "OGN·153/298", "P2", "HAND");
        Assert.True(BoonRules.TryGrant(initial.CardObjects["ENEMY"], initial.PlayerZones, initial.CardObjects, out var unit));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ENEMY"] = unit } };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("BOONS", "OGN·153/298", ["ENEMY"]));
        await game.Top();
        Assert.True(game.State.CardObjects["ENEMY"].IsExhausted);
        Assert.Equal(1, game.State.CardObjects["ENEMY"].BoonCount);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "BOON_CONSUMED");
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "BOON_GRANTED");
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_READIED");
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ImperialShrineStillPaysAndReturnsTheUnitWhenItsFixedTokenPlayIsPrevented(bool onBattlefield)
    {
        var initial = RadGearTestPosition.Put(Position(wardenOnBattlefield: onBattlefield),
            "SHRINE", "SFD·207/221", "P1", "BATTLEFIELD", "SHRINE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:SHRINE", []));
        for (var passes = 0; game.State.PendingPayment is null && game.State.FocusPlayerId is { } focus; passes++) {
            Assert.True(passes < 4);
            await game.Act(focus, new PassFocusCommand());
        }
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var mana = game.State.RunePools["P2"].Mana;
        await game.Act("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        Assert.Equal(mana - 1, game.State.RunePools["P2"].Mana);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        var trigger = Assert.Single(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
            && Equals(entry.Event.Payload.GetValueOrDefault("trigger"), TriggerKinds.BattlefieldConquerPayReturnUnitCreateSandSoldier));
        Assert.Equal(!onBattlefield, trigger.Event.Payload.ContainsKey("tokenObjectId"));
        Assert.Equal(!onBattlefield, trigger.Event.Payload.ContainsKey("tokenCardNo"));
        Assert.Equal(!onBattlefield, game.Events().Any(entry => entry.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Equal(onBattlefield, game.Events().Any(entry => entry.Event.Kind == "UNIT_PLAY_PREVENTED"));
        await game.Replay();
    }
}
