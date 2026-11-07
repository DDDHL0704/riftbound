using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class FriendlyUnitEquipmentObserverTests
{
    private static MatchState Position(string gear, string player = "P1")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", gear, player, "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return RadGearTestPosition.Put(state, "READY", "OGN·146/298", "P1", "HAND");
    }

    private static int Might(MatchState state, string id)
    {
        var player = state.CardObjects[id].ControllerId!;
        return JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player],
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id)
            .GetProperty("effectivePower").GetInt32();
    }

    private static UnitEventContext[] Captured(MatchState state)
        => state.StackItems.Select(item => item.UnitEvent).Concat(state.TriggerQueue.Select(item => item.UnitEvent))
            .Where(context => context?.FriendlyUnitAction is not null).Cast<UnitEventContext>().ToArray();

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MaskBuffsOnlyTheSoleFriendlyAttackerOrDefenderUntilTurnEnd(bool defend)
    {
        var state = Position("OGN·060/298", defend ? "P2" : "P1");
        state = RadUnitEffectTestFixture.Place(state, "BF", "P2", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        var returner = defend ? "P1" : "P2";
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", returner, "HAND");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        var unit = defend ? "ENEMY" : "ALLY";
        var context = Assert.Single(Captured(game.State));
        Assert.Equal(unit, context.Other!.Binding.ObjectId);
        Assert.Equal(defend ? "DEFEND" : "ATTACK", context.FriendlyUnitAction!.Action);
        Assert.Equal(1, context.FriendlyUnitAction.RoleUnitCount);
        Assert.Equal(4, Might(game.State, unit));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        Assert.Equal(5, Might(game.State, unit));
        // Remove the other side through a real reaction, preserving the buffed
        // unit on the field so turn expiry is tested independently of departure.
        if (game.State.FocusPlayerId != returner)
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act(returner, new PlayCardCommand("RETURN", "OGN·104/298", [defend ? "ALLY" : "ENEMY"]));
        await game.Top();
        for (var i = 0; i < 6 && game.State.FocusPlayerId is { } focus; i++)
            await game.Act(focus, new PassFocusCommand());
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, Might(game.State, unit));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MaskDoesNotTriggerForTwoFriendlyParticipants(bool defend)
    {
        var state = Position("OGN·060/298", defend ? "P2" : "P1");
        state = RadUnitEffectTestFixture.Place(state, "BF", "P2", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "EXTRA", "OGN·219/298", defend ? "P2" : "P1",
            defend ? "BATTLEFIELD" : "BASE", defend ? "BF" : null);
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", [],
            SourceObjectIds: defend ? null : ["ALLY", "EXTRA"]));
        Assert.Empty(Captured(game.State));
        Assert.Equal(4, Might(game.State, defend ? "ENEMY" : "ALLY"));
        await game.Replay();
    }

    private static MatchState Harbor(string owner = "P1")
    {
        var state = Position("OGN·143/298");
        state = RadGearTestPosition.Put(state, "READY2", "OGN·146/298", "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true, OwnerId = owner } } };
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task HarborObservesAnActualFriendlyReadyOnceAndItsBuffExpires(string owner)
    {
        var game = new Driver(Harbor(owner));
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["ALLY"]));
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(4, Might(game.State, "ALLY"));
        Assert.Equal("READIED", Assert.Single(Captured(game.State)).FriendlyUnitAction!.Action);
        await game.Top();
        Assert.Equal(5, Might(game.State, "ALLY"));
        await game.Act("P1", new PlayCardCommand("READY2", "OGN·146/298", ["ALLY"]));
        await game.Top();
        Assert.Empty(Captured(game.State));
        Assert.Equal(5, Might(game.State, "ALLY"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, Might(game.State, "ALLY"));
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentReadyingYourUnitDoesNotTriggerYourHarbor()
    {
        var state = Harbor();
        state = RadUnitEffectTestFixture.Place(state, "READY", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ALLY"]));
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Empty(Captured(game.State));
        Assert.Equal(4, Might(game.State, "ALLY"));
        await game.Replay();
    }

    [Fact]
    public async Task TheControllersWakeStepAlsoTriggersHarbor()
    {
        var game = new Driver(Harbor() with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("READIED", Assert.Single(Captured(game.State)).FriendlyUnitAction!.Action);
        await game.Top();
        Assert.Equal("P1", game.State.TurnPlayerId);
        Assert.Equal(5, Might(game.State, "ALLY"));
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheReadiedUnitDoesNotBuffItsNewGenerationInHand()
    {
        var game = new Driver(Harbor());
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["ALLY"]));
        await game.Top();
        var generation = Assert.Single(Captured(game.State)).Other!.Binding.Generation;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        var context = Assert.Single(Captured(game.State));
        Assert.Empty(ResolutionResult.TargetSelectionSubjectObjects(game.State, context, ProjectionViewers.Spectator)!);
        await game.Top();
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    private static MatchState AnimatePosition(string harborPlayer, bool moonveil = false)
    {
        var state = RadGearTestPosition.Put(Position("OGN·143/298", harborPlayer), "EQUIPMENT", "SFD·022/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "LEGEND", "RAD·145/167", "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Base = state.PlayerZones["P1"].Base.Where(id => id != "LEGEND").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        if (moonveil) {
            state = RadGearTestPosition.Put(state, "BF", "SFD·221/221", "P2", "BATTLEFIELD", "BF");
            state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BASE");
            state = RadGearTestPosition.Put(state, "SAFE", "OGN·296/298", "P1", "BATTLEFIELD", "SAFE");
            state = RadGearTestPosition.Put(state, "SAFE_UNIT", "OGN·219/298", "P1", "BATTLEFIELD", "SAFE");
        }
        return OriannaEquipmentConsumerFixture.WithResources(state);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HarborObservesNativeAndBorrowedLegendReadyingAnActuallyAnimatedEquipment(bool borrowed)
    {
        var initial = AnimatePosition("P2");
        initial = RadGearTestPosition.Put(initial, "JAYCE", "VEN·149", "P2", "BASE");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with {
                    Base = initial.PlayerZones["P2"].Base.Where(id => id != "JAYCE").ToArray(), LegendZone = ["JAYCE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["JAYCE"] = new("P2", "LEGEND") } };
        initial = RadGearTestPosition.Put(initial, "HEIMER", "OGN·111/298", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "STEAL", "OGN·203/298", "P2", "HAND");
        var mana = Enumerable.Range(0, 10).Select(i => "P2-READY-MANA-" + i).ToArray();
        var power = Enumerable.Range(0, 3).Select(i => "P2-READY-POWER-" + i).ToArray();
        foreach (var id in mana) initial = RadGearTestPosition.Put(initial, id, "UNL-R03", "P2", "BASE");
        foreach (var id in power) initial = RadGearTestPosition.Put(initial, id, "UNL-R05", "P2", "BASE");
        var game = new Driver(initial);
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "EQUIPMENT");
        await game.Act("P1", new MoveUnitCommand("EQUIPMENT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", mana);
        await game.Act("P2", new RecycleRuneCommand(power[0], power));
        await game.Act("P2", new PlayCardCommand("STEAL", "OGN·203/298", ["EQUIPMENT"]));
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["EQUIPMENT"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["EQUIPMENT"].ControllerId);
        Assert.True(game.State.CardObjects["EQUIPMENT"].IsExhausted);
        var before = Might(game.State, "EQUIPMENT");
        if (borrowed) {
            var skill = Assert.Single(BorrowedExhaustRules.Abilities(game.State, "P2", game.State.CardObjects["HEIMER"]),
                ability => ability.Donor.ObjectId == "JAYCE" && ability.Spec.Kind == "READY_EQUIPMENT" && ability.Spec.Targets == 1);
            await game.Act("P2", new ActivateAbilityCommand("HEIMER", skill.Id, ["EQUIPMENT"]));
        }
        else await game.Act("P2", new LegendActCommand("JAYCE", LegendActionAbilityCatalog.JayceLegendReadyOneAbilityId,
            ["EQUIPMENT"], ["SPEND_MANA:1"]));
        await game.Top();
        Assert.False(game.State.CardObjects["EQUIPMENT"].IsExhausted);
        Assert.Equal(before, Might(game.State, "EQUIPMENT"));
        Assert.Equal("EQUIPMENT", Assert.Single(Captured(game.State)).Other!.Binding.ObjectId);
        await game.Top();
        Assert.Equal(before + 1, Might(game.State, "EQUIPMENT"));
        await game.Replay();
    }

    [Fact]
    public async Task HarborObservesMoonveilReadyingAnActuallyAnimatedEquipment()
    {
        var game = new Driver(AnimatePosition("P1", moonveil: true));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "EQUIPMENT");
        await game.Act("P1", new MoveUnitCommand("EQUIPMENT", "BASE", "BATTLEFIELD:SAFE", []));
        var before = Might(game.State, "EQUIPMENT");
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        for (var passes = 0; game.State.PendingCardChoice is null && game.State.FocusPlayerId is { } focus; passes++) {
            Assert.True(passes < 4);
            await game.Act(focus, new PassFocusCommand());
        }
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("EQUIPMENT", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["EQUIPMENT"]));
        await game.Top();
        Assert.False(game.State.CardObjects["EQUIPMENT"].IsExhausted);
        Assert.Equal(before, Might(game.State, "EQUIPMENT"));
        Assert.Equal("EQUIPMENT", Assert.Single(Captured(game.State)).Other!.Binding.ObjectId);
        await game.Top();
        Assert.Equal(before + 1, Might(game.State, "EQUIPMENT"));
        await game.Replay();
    }

    private static MatchState BladeYard(bool exhausted = false, bool restricted = false)
    {
        var state = Position("OGN·152/298");
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·078/298", "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = exhausted } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = restricted ? new RunePool(0, 0) { EquipmentOnlyPower = 1 } : new(20, 20) } };
    }

    private static async Task GiveLeeABoon(Driver game)
    {
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), definition => definition.SourceCardNo == "OGN·078/298"
            && definition.Kind == ActivatedAbilityKinds.GrantSelfBoon);
        await game.Act("P1", new ActivateAbilityCommand("ALLY", ability.AbilityId, []));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ALLY"].BoonCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BladeYardRequiresOptionalOrangeAndExhaustBeforeAnIndependentReady(bool pay)
    {
        var game = new Driver(BladeYard());
        await GiveLeeABoon(game);
        var pending = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var context = Assert.Single(Captured(game.State));
        Assert.Equal(new[] { "ALLY" }, ResolutionResult.TargetSelectionSubjectObjects(game.State, context, ProjectionViewers.Spectator));
        Assert.Contains("李青", ResolutionResult.TargetSelectionSummary(game.State, context, "P1"));
        Assert.Equal(1, pending.PowerCost);
        Assert.Equal(new Dictionary<string, int> { [RuneTrait.Orange] = 1 }, pending.PowerCostByTrait);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(pay, game.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal(power - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        if (pay) await game.Top();
        Assert.Equal(!pay, game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyExhaustedBladeYardCannotPayItsTriggeredCost()
    {
        var game = new Driver(BladeYard(exhausted: true));
        var pool = game.State.RunePools["P1"];
        await GiveLeeABoon(game);
        Assert.Null(game.State.PendingPayment);
        Assert.Empty(Captured(game.State));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        await game.Replay();
    }

    [Fact]
    public async Task EquipmentOnlyPowerCanPayTheEquipmentTrigger()
    {
        var game = new Driver(BladeYard(restricted: true));
        await GiveLeeABoon(game);
        var pending = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]));
        Assert.Equal(0, game.State.RunePools["P1"].EquipmentOnlyPower);
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task PermanentPlayOnlyPowerCannotPayTheEquipmentTrigger()
    {
        var state = BladeYard();
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new RunePool(0, 0) { UnitOrEquipmentPlayOnlyPower = 1 } } };
        var game = new Driver(state);
        await GiveLeeABoon(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        await game.Replay();
    }

    [Fact]
    public async Task BladeYardReadyCreatesASeparateHarborBuff()
    {
        var game = new Driver(RadGearTestPosition.Put(BladeYard(), "HARBOR", "OGN·143/298", "P1", "BASE"));
        await GiveLeeABoon(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(6, Might(game.State, "ALLY"));
        Assert.Equal("OGN·143/298", Assert.Single(Captured(game.State)).CardNo);
        await game.Top();
        Assert.Equal(7, Might(game.State, "ALLY"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(6, Might(game.State, "ALLY"));
        Assert.Equal(1, game.State.CardObjects["ALLY"].BoonCount);
        await game.Replay();
    }

    [Theory]
    [InlineData("actor")] [InlineData("action")] [InlineData("count")]
    [InlineData("was-ready")] [InlineData("generation")] [InlineData("missing")]
    [InlineData("null-actor")]
    public async Task RecoveryRejectsForgedFriendlyUnitObservation(string forgery)
    {
        var game = new Driver(Harbor());
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["ALLY"]));
        await game.Top();
        var item = Assert.Single(game.State.StackItems);
        var context = item.UnitEvent!;
        var origin = context.FriendlyUnitAction!;
        var changed = forgery switch {
            "actor" => context with { FriendlyUnitAction = origin with { Actor = "P2" } },
            "null-actor" => context with { FriendlyUnitAction = origin with { Actor = null! } },
            "action" => context with { FriendlyUnitAction = origin with { Action = "BOON_GRANTED" } },
            "count" => context with { FriendlyUnitAction = origin with { RoleUnitCount = 1 } },
            "was-ready" => context with { FriendlyUnitAction = origin with { WasExhausted = false } },
            "generation" => context with { Other = context.Other! with { Binding = context.Other.Binding with { Generation = context.Other.Binding.Generation + 1 } } },
            _ => context with { FriendlyUnitAction = null } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { UnitEvent = changed }] }));
    }
}
