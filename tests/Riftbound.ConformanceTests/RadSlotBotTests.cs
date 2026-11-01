using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadSlotBotTests
{
    private const string Slot = "RAD·111/167";

    [Fact]
    public async Task ThePrintedEquipmentPaysFourManaAndOnePurpleWithoutDrawingOnPlay()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Slot);
        Assert.Equal(39614, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        var initial = Put(Create(), "GEAR", Slot, "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("GEAR", Slot, []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·040/298", true)] [InlineData("OGN·219/298", true)] [InlineData("OGN·192/298", false)]
    public async Task LowerOrTiedManaDrawsTheRevealedCardAndHigherManaRecyclesIt(string ownTop, bool draws)
    {
        var game = new Driver(Position(ownTop)); await Start(game);
        Assert.Equal("SCORE", game.State.TurnStartStep); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        await game.Top();
        Assert.Equal(draws, game.State.PlayerZones["P1"].Hand.Contains("P1-D0"));
        Assert.Equal(draws ? 2 : 1, game.State.PlayerZones["P1"].Hand.Count); // Includes the ordinary draw.
        if (!draws) Assert.Equal("P1-D0", game.State.PlayerZones["P1"].MainDeck[^1]);
        Assert.Equal("P2-D0", game.State.PlayerZones["P2"].MainDeck[^1]);
        Assert.Empty(game.State.PlayerZones["P2"].Hand);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")] [InlineData("OPPONENT")]
    public async Task OnlyAnInPlayEquipmentControlledByTheStartingPlayerTriggers(string boundary)
    {
        var initial = Position();
        initial = RadUnitEffectTestFixture.Place(initial, "GEAR", boundary == "OPPONENT" ? "P2" : "P1",
            boundary == "OPPONENT" ? "BASE" : boundary);
        var game = new Driver(initial); await Start(game);
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        Assert.Equal(boundary == "HAND" ? 2 : 1, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Fact]
    public async Task MultipleSourcesCompareFreshTopsAfterTheChosenEarlierSkillResolves()
    {
        var game = new Driver(Put(Position("OGN·219/298", "OGN·049/298"), "SECOND", Slot, "P1", "BASE"));
        await Start(game); Assert.Equal(2, game.State.TriggerQueue.Count);
        var ids = game.State.TriggerQueue.OrderBy(t => t.SourceObjectId, StringComparer.Ordinal).Select(t => t.TriggerId).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await game.Top(); await game.Top();
        Assert.Equal(3, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(new[] { "P2-D2", "P2-D3", "P2-D4", "P2-D5", "P2-D0", "P2-D1" }, game.State.PlayerZones["P2"].MainDeck);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AnEmptyDeckRevealsNothingAndDoesNotBurnOutForTheComparison(string emptyPlayer)
    {
        var initial = Position();
        foreach (var id in initial.PlayerZones[emptyPlayer].MainDeck.ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, id, emptyPlayer, "GRAVEYARD");
        var game = new Driver(initial); await Start(game); await game.Top();
        var reveal = Assert.Single(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        Assert.NotEqual(emptyPlayer, reveal.Event.Payload["playerId"]);
        // Only the ordinary turn draw can burn out; the comparison draws no absent card.
        Assert.Equal(emptyPlayer == "P1" ? 1 : 0, game.Events().Count(e => e.Event.Kind == "BURNOUT_APPLIED"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RevealingNocturneUsesItsSharedReplacementAndNeverDrawsTheNextCardInstead(bool banish)
    {
        var game = new Driver(Position(DeckObservationPlayTests.Nocturne, "OGN·049/298"));
        await Start(game); await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", pending.Request.Kind);
        var observation = Assert.IsType<DeckObservationView>(pending.Request.Preview!.DeckObservationView);
        Assert.True(observation.Public);
        Assert.Equal(new[] { "P1-D0", "P2-D0" }, observation.Cards.Keys.Order(StringComparer.Ordinal));
        foreach (var snapshot in ResolutionResult.BuildSnapshots(game.State).Values.Append(ResolutionResult.BuildSpectatorSnapshot(game.State)))
        {
            var json = JsonSerializer.Serialize(snapshot);
            Assert.Contains("P1-D0", json); Assert.Contains("P2-D0", json);
            Assert.DoesNotContain("P1-D1", json); Assert.DoesNotContain("P2-D1", json);
        }
        var forgedView = observation with { Cards = new Dictionary<string, long>(observation.Cards) {
            ["P2-D1"] = game.State.CardObjects["P2-D1"].ObjectGeneration } };
        var forgedChoice = pending with { Request = pending.Request with {
            Preview = pending.Request.Preview! with { DeckObservationView = forgedView } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingRuleChoice = forgedChoice }));
        await Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
        await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [banish ? "BANISH" : "KEEP"]));
        if (banish)
        {
            var play = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            await game.Act("P1", new ChooseCardsCommand(play.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished);
        }
        Assert.Equal(banish ? 1 : 2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-D2", game.State.PlayerZones["P1"].Hand);
        foreach (var snapshot in ResolutionResult.BuildSnapshots(game.State).Values.Append(ResolutionResult.BuildSpectatorSnapshot(game.State)))
            Assert.DoesNotContain("P2-D0", JsonSerializer.Serialize(snapshot));
        await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("generation")] [InlineData("tick")] [InlineData("missing")]
    public async Task RecoveryRejectsAlteredComparisonCapture(string fault)
    {
        var game = new Driver(Position()); await Start(game);
        var item = Assert.Single(game.State.StackItems); var context = item.BeginningDraw!; var capture = context.Comparison!;
        var forged = fault switch {
            "source" => capture with { Source = new("ALLY", capture.Source.Generation) },
            "generation" => capture with { Source = capture.Source with { Generation = capture.Source.Generation + 1 } },
            "tick" => capture with { AcquiredTick = game.State.Tick + 1 },
            _ => null
        };
        var state = game.State with { StackItems = [item with { BeginningDraw = context with { Comparison = forged } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state));
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(game.State))!));
    }

    private static Task Start(Driver game) => game.Act("P1", new PassPriorityCommand());

    private static MatchState Position(string ownTop = "OGN·219/298", string enemyTop = "OGN·219/298")
    {
        var state = Put(Create("OGN·294/298"), "GEAR", Slot, "P1", "BASE") with {
            Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P1-D0"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-D0", ownTop, "P1"),
            ["P2-D0"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-D0", enemyTop, "P2") } };
    }
}
