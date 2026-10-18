using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RavenbloomPutIntoHandTests
{
    private const string Battlefield = "P1-TEEMO-DEFEND-BATTLEFIELD";
    private const string Attacker = "P1-TEEMO-DEFEND-ATTACKER";
    private const string Defender = "P2-TEEMO-FACE-UP-DEFENDER";
    private const string Spell = "P2-MAIN-NON-STANDBY-002";
    private const string Unit = "P2-MAIN-NON-STANDBY-001";
    private const string Gem = "RAVENBLOOM-FROZEN-GEM";

    // Controlled continuation cases: natural blank openings are a separate gate.
    // CN SFD215 says put into hand; CN414.3 reserves draw for a draw instruction.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RevealedSpellEntersHandWithoutDrawingOrCountingAsTheSecondDraw(int previousDraws)
    {
        var initial = Position(previousDraws, spell: true);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Declare(driver);

        Assert.Equal(previousDraws, driver.State.DrawLedger.Counts.GetValueOrDefault("P2"));
        Assert.Contains(Spell, driver.State.PlayerZones["P2"].Hand);
        Assert.Equal(initial.PlayerZones["P2"].MainDeck.Skip(1), driver.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        var added = Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_ADDED_TO_HAND");
        Assert.Equal("P2", added.Event.Payload["playerId"]);
        Assert.Equal(Battlefield, added.Event.Payload["sourceObjectId"]);
        Assert.Equal("HAND", added.Event.Payload["destinationZone"]);
        Assert.Equal([Spell], Assert.IsType<string[]>(added.Event.Payload["cardIds"]));
        Assert.DoesNotContain(driver.State.StackItems, item => item.FieldContext?.Kind == "SECOND_DRAW");
        Assert.DoesNotContain(driver.State.TriggerQueue, item => item.FieldContext?.Kind == "SECOND_DRAW");
        Assert.Null(driver.State.PendingCardChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RevealedNonSpellStillRecyclesWithoutDrawingOrAddingAnythingToHand(int previousDraws)
    {
        var initial = Position(previousDraws, spell: false);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Declare(driver);

        Assert.Equal(previousDraws, driver.State.DrawLedger.Counts.GetValueOrDefault("P2"));
        Assert.Equal(initial.PlayerZones["P2"].Hand, driver.State.PlayerZones["P2"].Hand);
        Assert.Equal(initial.PlayerZones["P2"].MainDeck.Skip(1).Append(Unit), driver.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "CARD_DRAWN" or "CARD_ADDED_TO_HAND");
        var recycled = Assert.Single(driver.Events(), e => e.Event.Kind == "CARDS_RECYCLED");
        Assert.Equal(Battlefield, recycled.Event.Payload["sourceObjectId"]);
        Assert.Equal([Unit], Assert.IsType<string[]>(recycled.Event.Payload["cardIds"]));
        Assert.DoesNotContain(driver.State.StackItems, item => item.FieldContext?.Kind == "SECOND_DRAW");
        Assert.DoesNotContain(driver.State.TriggerQueue, item => item.FieldContext?.Kind == "SECOND_DRAW");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ExistingStackedDeckPutIntoHandAlsoKeepsTheRealDrawLedgerAndFrozenGemUnchanged()
    {
        var initial = AddGem(DeckObservationPlayTests.Position(), "P1", 1);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("TRICK", "OGN·183/298", []));
        await driver.Top();
        await DeckObservationPlayTests.Decide(driver, false);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["END_PREFIX"]));

        Assert.Equal(1, driver.State.DrawLedger.Counts["P1"]);
        Assert.Contains("END_PREFIX", driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_ADDED_TO_HAND");
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.DoesNotContain(driver.State.StackItems, item => item.FieldContext?.Kind == "SECOND_DRAW");
        Assert.DoesNotContain(driver.State.TriggerQueue, item => item.FieldContext?.Kind == "SECOND_DRAW");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static Task<ResolutionResult> Declare(StandbyDefendObserverTests.Driver driver)
        => driver.Act("P1", new DeclareBattleCommand(Battlefield, [Attacker], [Defender], ["COMBAT_ASSIGNMENT"]));

    private static MatchState Position(int previousDraws, bool spell)
    {
        var state = StandbyDefendTeemoTests.BuildState("UNL-036/219",
            battlefieldCardNo: "SFD·215/221", battlefieldControllerId: "P2");
        var top = spell ? Spell : Unit;
        state = state with
        {
            Tick = 0,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P2"] = state.PlayerZones["P2"] with
                {
                    MainDeck = new[] { top }.Concat(state.PlayerZones["P2"].MainDeck.Where(id => id != top)).ToArray()
                }
            }
        };
        return AddGem(state, "P2", previousDraws);
    }

    private static MatchState AddGem(MatchState state, string player, int previousDraws)
        => state with
        {
            DrawLedger = new(state.TurnNumber, new Dictionary<string, int> { [player] = previousDraws }),
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                [player] = state.PlayerZones[player] with { Base = state.PlayerZones[player].Base.Append(Gem).ToArray() }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                [Gem] = new(Gem, cardNo: "UNL-074/219", manaCost: 2,
                    tags: [CardObjectTags.EquipmentCard], ownerId: player, controllerId: player)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            {
                [Gem] = new(player, "BASE")
            }
        };
}
