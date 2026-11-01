using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadBigHornSpellTests
{
    private const string Spell = "RAD·047/167";
    private const string UnitCard = "OGN·001/298";

    [Fact]
    public async Task BigHornDrawsForExactlyTheNextThreeFriendlyUnitPlays()
    {
        var initial = AddUnits(TurnUnitPlayGrantsTests.Position(Spell), "UNIT2", "UNIT3", "UNIT4", "UNIT5");
        var driver = new StandbyDefendObserverTests.Driver(initial);

        await driver.Act("P1", new PlayCardCommand("GRANT", Spell, []));
        await driver.Top();
        Assert.Equal(3, DrawGrants(driver.State));
        var drawsBeforeUnits = Draws(driver);

        foreach (var (id, expectedRemaining) in new[] {
            ("UNIT", 2), ("UNIT2", 1), ("UNIT3", 0) })
        {
            await driver.Act("P1", new PlayCardCommand(id, UnitCard, [], Destination: "BASE"));
            Assert.Single(driver.State.StackItems, item => item.TurnUnitPlayDraw?.Grant.Kind == "PLAY_DRAW");
            Assert.Equal(expectedRemaining, DrawGrants(driver.State));
            await driver.Top();
        }

        await driver.Act("P1", new PlayCardCommand("UNIT4", UnitCard, [], Destination: "BASE"));
        Assert.Equal(0, DrawGrants(driver.State));
        Assert.DoesNotContain(driver.State.StackItems, item => item.TurnUnitPlayDraw?.Grant.Kind == "PLAY_DRAW");
        Assert.Equal(drawsBeforeUnits + 3, Draws(driver));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BigHornDrawTriggerSurvivesThePlayedUnitReturningToHand()
    {
        var initial = TurnUnitPlayGrantsTests.Position(Spell);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { Power = 3 } } };
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "VEN·052", "P1");
        var driver = new StandbyDefendObserverTests.Driver(initial);

        await driver.Act("P1", new PlayCardCommand("GRANT", Spell, []));
        await driver.Top();
        var drawsBefore = Draws(driver);
        await driver.Act("P1", new PlayCardCommand("UNIT", UnitCard, [], Destination: "BASE"));
        var returned = await driver.Act("P1", new PlayCardCommand("RETURN", "VEN·052", ["UNIT"], Mode: "RETURN_FRIENDLY"));
        Assert.True(returned.Accepted, returned.ErrorMessage);
        for (var step = 0; step < 8 && driver.State.ObjectLocations["UNIT"].Zone != "HAND"; step++)
        {
            var priority = driver.State.PriorityPlayerId;
            if (priority is null) break;
            await driver.Act(priority, new PassPriorityCommand());
        }
        Assert.True(driver.State.ObjectLocations["UNIT"].Zone == "HAND",
            $"zone={driver.State.ObjectLocations["UNIT"].Zone}, stack={string.Join(',', driver.State.StackItems.Select(i => i.EffectKind))}, priority={driver.State.PriorityPlayerId}, events={string.Join(',', driver.Events().Select(e => e.Event.Kind))}");
        var resolved = await driver.Top();

        Assert.Contains(resolved.Events, e => e.Kind == "CARD_DRAWN" && Equals(e.Payload["playerId"], "P1"));
        Assert.Equal(2, DrawGrants(driver.State));
        Assert.Equal(drawsBefore + 1, Draws(driver));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BigHornDoesNotCreateAHiddenGrantWithoutResolvingTheSpell()
    {
        var initial = AddUnits(TurnUnitPlayGrantsTests.Position(Spell), "UNIT2");
        var driver = new StandbyDefendObserverTests.Driver(initial);

        await driver.Act("P1", new PlayCardCommand("UNIT", UnitCard, [], Destination: "BASE"));

        Assert.Equal(0, DrawGrants(driver.State));
        Assert.DoesNotContain(driver.State.TriggerQueue, trigger => trigger.TurnUnitPlayBoon?.Grant.Kind == "PLAY_DRAW");
        Assert.Equal(0, Draws(driver));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BigHornPlayDrawMarkerRejectsForgedKindDuringRecovery()
    {
        var driver = new StandbyDefendObserverTests.Driver(TurnUnitPlayGrantsTests.Position(Spell));
        await driver.Act("P1", new PlayCardCommand("GRANT", Spell, []));
        await driver.Top();
        var marker = driver.State.UntilEndOfTurnEffects.First(e => e.StartsWith("TURN_UNIT_PLAY_GRANT|"));
        var fields = marker.Split('|');
        fields[8] = "READY_ENTRY";
        var corrupted = driver.State with {
            UntilEndOfTurnEffects = driver.State.UntilEndOfTurnEffects
                .Select(e => e == marker ? string.Join('|', fields) : e).ToArray() };

        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(corrupted), e => e.Contains("temporary unit play grant"));
    }

    private static int DrawGrants(MatchState state)
        => state.UntilEndOfTurnEffects.Count(e => e.StartsWith("TURN_UNIT_PLAY_GRANT|"));

    private static int Draws(StandbyDefendObserverTests.Driver driver)
        => driver.Events().Where(e => e.Event.Kind == "CARD_DRAWN")
            .Sum(e => Convert.ToInt32(e.Event.Payload["count"]));

    private static MatchState AddUnits(MatchState state, params string[] ids)
    {
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in ids)
        {
            cards[id] = new(id, cardNo: UnitCard, manaCost: 1, power: 1, ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P1", "HAND");
            zones["P1"] = zones["P1"] with { Hand = zones["P1"].Hand.Append(id).ToArray() };
        }
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
