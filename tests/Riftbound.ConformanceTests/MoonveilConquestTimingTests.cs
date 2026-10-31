using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MoonveilConquestTimingTests
{
    [Theory]
    [InlineData("P1", "leave")] [InlineData("P1", "generation")] [InlineData("P1", "controller")]
    [InlineData("P2", "leave")] [InlineData("P2", "generation")] [InlineData("P2", "controller")]
    public async Task AConfirmedTargetThatChangesIdentityOrLegalityIsNotReplacedAtResolution(string player, string change)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true);
        var trace = new ThreeBattlefieldTimingProbe.Trace(initial, $"moonveil-target-{player}-{change}-before");
        try
        {
            await trace.Conquer(player);
            Assert.IsType<PendingCardChoiceState>(trace.State.PendingCardChoice);
            await trace.Choose(true, "EQUIP", targetMustBeChosen: true);
            var item = Assert.Single(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
            Assert.Equal(initial.CardObjects["EQUIP"].ObjectGeneration, item.TargetGenerations!["EQUIP"]);

            // Component-level invalidation models a response boundary; it is not a claim that a response was played.
            var state = trace.State;
            var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
            var zones = new Dictionary<string, PlayerZones>(state.PlayerZones);
            var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
            var opponent = ThreeBattlefieldTimingProbe.Other(player);
            if (change == "generation") cards["EQUIP"] = cards["EQUIP"] with { ObjectGeneration = cards["EQUIP"].ObjectGeneration + 1 };
            else if (change == "leave")
            {
                zones[player] = zones[player] with { Base = zones[player].Base.Where(id => id != "EQUIP").ToArray(),
                    Graveyard = zones[player].Graveyard.Concat(["EQUIP"]).ToArray() };
                cards["EQUIP"] = cards["EQUIP"] with { ObjectGeneration = cards["EQUIP"].ObjectGeneration + 1 };
                locations["EQUIP"] = new(player, "GRAVEYARD");
            }
            else
            {
                zones[player] = zones[player] with { Base = zones[player].Base.Where(id => id != "EQUIP").ToArray() };
                zones[opponent] = zones[opponent] with { Base = zones[opponent].Base.Concat(["EQUIP"]).ToArray() };
                cards["EQUIP"] = cards["EQUIP"] with { ControllerId = opponent };
                locations["EQUIP"] = new(opponent, "BASE");
            }
            var changed = state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
            var continuation = new ThreeBattlefieldTimingProbe.Trace(changed, $"moonveil-target-{player}-{change}-after");
            try
            {
                var resolved = await continuation.Top();
                Assert.DoesNotContain(resolved.Events, e => e.Kind is "EQUIPMENT_READIED" or "EQUIPMENT_DETACHED");
                Assert.True(continuation.State.CardObjects["EQUIP"].IsExhausted);
                Assert.Null(continuation.State.PendingCardChoice);
            }
            finally { continuation.Save(); }
        }
        finally { trace.Save(); }
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ConfirmedGoldTargetMayCeaseDuringARealResponseWithoutRetargeting(string player)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory));
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GOLD"] = factory.CreateObject("GOLD", player, player) with { IsExhausted = false } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Base = initial.PlayerZones[player].Base.Concat(["GOLD"]).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["GOLD"] = new(player, "BASE") } };
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_THREE_FIELD_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            evidence is null ? null : Path.Combine(evidence, "moonveil-gold-" + player));
        var opponent = ThreeBattlefieldTimingProbe.Other(player);
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(opponent, new PassFocusCommand());
        var confirmation = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Contains("GOLD", confirmation.LegalObjectIds);
        await driver.Act(player, new ChooseCardsCommand(confirmation.ChoiceId, confirmation.ChoiceWindow, ["GOLD"]));
        var pending = Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
        Assert.Equal(new[] { "GOLD" }, pending.TargetObjectIds);
        Assert.Equal(initial.CardObjects["GOLD"].ObjectGeneration, pending.TargetGenerations!["GOLD"]);
        Assert.Equal(player, driver.State.PriorityPlayerId);
        await driver.Act(player, new ActivateAbilityCommand("GOLD", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        Assert.False(driver.State.CardObjects.ContainsKey("GOLD"));
        Assert.False(driver.State.ObjectLocations.ContainsKey("GOLD"));
        pending = Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
        Assert.Equal(new[] { "GOLD" }, pending.TargetObjectIds);
        Assert.Equal(initial.CardObjects["GOLD"].ObjectGeneration, pending.TargetGenerations!["GOLD"]);
        var departed = Assert.Single(pending.DepartedTokenTargets!);
        Assert.Equal(new ObjectBinding("GOLD", initial.CardObjects["GOLD"].ObjectGeneration), departed.Target);
        var forged = pending with { DepartedTokenTargets = [departed with { DepartedTick = driver.State.Tick + 1 }] };
        Assert.Contains("invalid captured battlefield conquest choice stack", OfficialInsightAndSpellLockTests.Errors(
            driver.State with { StackItems = driver.State.StackItems.Select(i => i.StackItemId == pending.StackItemId ? forged : i).ToArray() }));
        var resolved = await driver.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind is "EQUIPMENT_READIED" or "EQUIPMENT_DETACHED");
        Assert.True(driver.State.CardObjects["EQUIP"].IsExhausted);
        Assert.Null(driver.State.PendingCardChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task AlreadyActiveArmamentIsAValidTargetAndCanStillBeDetachedAtResolution(string player)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true,
            armament: true, equipmentInitiallyReady: true);
        var trace = new ThreeBattlefieldTimingProbe.Trace(initial, $"moonveil-ready-{player}");
        try
        {
            Assert.Equal(5, ThreeBattlefieldTimingProbe.EffectivePower(initial, player, "HOST"));
            var conquered = await trace.Conquer(player);
            Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            var choice = Assert.IsType<PendingCardChoiceState>(trace.State.PendingCardChoice);
            Assert.Contains("EQUIP", choice.LegalObjectIds);
            await trace.Choose(true, "EQUIP", targetMustBeChosen: true);
            var confirmed = Assert.Single(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
            Assert.Equal(initial.CardObjects["EQUIP"].ObjectGeneration, confirmed.TargetGenerations!["EQUIP"]);
            await trace.ReactWithPaidGust(player, TriggerKinds.BattlefieldConquerReadyEquipment);
            await trace.Top();
            var detach = Assert.IsType<PendingCardChoiceState>(trace.State.PendingCardChoice);
            Assert.Contains("EQUIP", detach.LegalObjectIds); Assert.Equal(0, detach.RequiredCount);
            await trace.Act(player, new ChooseCardsCommand(detach.ChoiceId, detach.ChoiceWindow, ["EQUIP"]));
            Assert.Null(trace.State.CardObjects["EQUIP"].AttachedToObjectId);
            Assert.Equal(3, ThreeBattlefieldTimingProbe.EffectivePower(trace.State, player, "HOST"));
            Assert.DoesNotContain(trace.Events, e => e.Kind == "EQUIPMENT_READIED");
            Assert.Single(trace.Events, e => e.Kind == "EQUIPMENT_DETACHED");
            Assert.Single(trace.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
                && Equals(e.Payload.GetValueOrDefault("trigger"), TriggerKinds.BattlefieldConquerReadyEquipment));
            var reported = Assert.Single(trace.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
                && Equals(e.Payload.GetValueOrDefault("trigger"), TriggerKinds.BattlefieldConquerReadyEquipment));
            Assert.NotEqual("UNIT", reported.Payload.GetValueOrDefault("sourceObjectId"));
        }
        finally { trace.Save(); }
    }

    [Theory]
    [InlineData("P1", false, false, false)] [InlineData("P2", false, false, false)]
    [InlineData("P1", true, false, false)] [InlineData("P2", true, false, false)]
    [InlineData("P1", true, true, false)] [InlineData("P2", true, true, false)]
    [InlineData("P1", true, true, true)] [InlineData("P2", true, true, true)]
    public async Task MoonveilOffersTheSkillBeforeResponsesAndResolvesItsEquipmentChoicesAfterward(
        string player, bool accept, bool armament, bool detach)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "SFD·221/221", equipment: true, armament: armament);
        var trace = new ThreeBattlefieldTimingProbe.Trace(initial, $"moonveil-{player}-{accept}-{armament}-{detach}");
        try
        {
            if (armament) Assert.Equal(5, ThreeBattlefieldTimingProbe.EffectivePower(initial, player, "HOST"));
            var conquered = await trace.Conquer(player);
            Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.DoesNotContain(conquered.Events, e => e.Kind is "EQUIPMENT_READIED" or "EQUIPMENT_DETACHED");
            Assert.True(trace.State.CardObjects["EQUIP"].IsExhausted);
            if (armament) Assert.Equal("HOST", trace.State.CardObjects["EQUIP"].AttachedToObjectId);
            var choice = Assert.IsType<PendingCardChoiceState>(trace.State.PendingCardChoice);
            Assert.Equal(player, choice.PlayerId);
            await trace.Choose(accept, "EQUIP", targetMustBeChosen: accept);
            Assert.True(trace.State.CardObjects["EQUIP"].IsExhausted);
            if (!accept)
            {
                Assert.DoesNotContain(trace.Events, e => e.Kind is "EQUIPMENT_READIED" or "EQUIPMENT_DETACHED");
                Assert.DoesNotContain(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
                return;
            }

            var confirmed = Assert.Single(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerReadyEquipment);
            Assert.Equal(new[] { "EQUIP" }, confirmed.TargetObjectIds);
            Assert.Equal(initial.CardObjects["EQUIP"].ObjectGeneration, confirmed.TargetGenerations!["EQUIP"]);
            await trace.ReactWithPaidGust(player, TriggerKinds.BattlefieldConquerReadyEquipment);
            Assert.True(trace.State.CardObjects["EQUIP"].IsExhausted);
            await trace.Top();
            var offeredDetach = false;
            for (var i = 0; i < 3 && trace.State.PendingCardChoice is { } pending; i++)
            {
                Assert.True(armament); Assert.False(trace.State.CardObjects["EQUIP"].IsExhausted);
                Assert.Equal(0, pending.RequiredCount); offeredDetach = true;
                string[] selected = detach ? [pending.LegalObjectIds.Contains("EQUIP", StringComparer.Ordinal)
                    ? "EQUIP" : Assert.Single(pending.LegalObjectIds)] : [];
                await trace.Act(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, selected));
            }
            Assert.Null(trace.State.PendingCardChoice);
            Assert.False(trace.State.CardObjects["EQUIP"].IsExhausted);
            Assert.Single(trace.Events, e => e.Kind == "EQUIPMENT_READIED");
            var reported = Assert.Single(trace.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
                && Equals(e.Payload.GetValueOrDefault("trigger"), TriggerKinds.BattlefieldConquerReadyEquipment));
            Assert.NotEqual("UNIT", reported.Payload.GetValueOrDefault("sourceObjectId"));
            if (armament)
            {
                Assert.True(offeredDetach);
                Assert.Equal(detach ? null : "HOST", trace.State.CardObjects["EQUIP"].AttachedToObjectId);
                Assert.Equal(detach ? 1 : 0, trace.Events.Count(e => e.Kind == "EQUIPMENT_DETACHED"));
                Assert.Equal(detach ? 3 : 5, ThreeBattlefieldTimingProbe.EffectivePower(trace.State, player, "HOST"));
            }
        }
        finally { trace.Save(); }
    }
}
