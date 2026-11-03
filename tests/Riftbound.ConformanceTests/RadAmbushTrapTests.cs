using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadAmbushTrapTests
{
    private const string Trap = "RAD·108/167", Shadow = "OGN·135/298";

    [Theory]
    [InlineData("BF")]
    [InlineData("OTHER")]
    public async Task OfficialPrintingDeploysAtEitherBattlefieldForItsPrintedCost(string field)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Trap);
        Assert.Equal(39610, card.Id);
        Assert.Equal(1, card.Energy);
        Assert.Equal(1, StandbyParser.ExtraCapacityForBattlefieldController(card.CardEffect));
        var game = new Driver(Put(Position(), "CARD", Trap, "P1", "HAND"));
        await Reject(game.State, "P1", new PlayCardCommand("CARD", Trap, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("CARD", Trap, [], Destination: "BATTLEFIELD:" + field));
        Assert.Equal(field, game.State.ObjectLocations["CARD"].BattlefieldObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["CARD"].Tags);
        Assert.Equal(29, game.State.RunePools["P1"].Mana);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task EveryTrapAndBandleTreeAddIndependentSlotsAndPromptsCloseAtCapacity(int traps, bool tree)
    {
        var game = new Driver(Position(traps, tree));
        var capacity = 1 + traps + (tree ? 1 : 0);
        for (var i = 0; i < capacity; i++)
        {
            Assert.Contains(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.HideCard && c.Enabled);
            await game.Act("P1", Hide(i));
        }
        Assert.Equal(capacity, game.State.CardObjects.Values.Count(c => c.IsFaceDown));
        await Reject(game.State, "P1", Hide(capacity));
        Assert.DoesNotContain(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.HideCard && c.Enabled);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("P1", 1)]
    [InlineData("P2", 2)]
    public async Task ActualBattlefieldAndTrapControllersDetermineTheBenefitIndependentlyOfOwnership(string trapController, int capacity)
    {
        var state = Position(1);
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "TRAP-0", trapController, "BATTLEFIELD", "BF");
        state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        for (var i = 0; i < 3; i++) state = Put(state, "P2-SHADOW-" + i, Shadow, "P2", "HAND");
        Assert.Equal("P1", state.CardObjects["BF"].OwnerId);
        Assert.Equal("P1", state.CardObjects["TRAP-0"].OwnerId);
        var game = new Driver(state);
        for (var i = 0; i < capacity; i++)
            await game.Act("P2", Hide(i) with { SourceObjectId = "P2-SHADOW-" + i });
        await Reject(game.State, "P2", Hide(capacity) with { SourceObjectId = "P2-SHADOW-" + capacity });
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapacityLossCollectsOnlyPrivateDecisionsThenMovesExactlyTheExcess(bool destroyAll)
    {
        var removal = destroyAll ? "OGN·022/298" : "VEN·003";
        var game = new Driver(Put(Position(2), "REMOVE", removal, "P1", "HAND"));
        for (var i = 0; i < 3; i++) await game.Act("P1", Hide(i));
        await game.Act("P1", new PlayCardCommand("REMOVE", removal, destroyAll ? [] : ["TRAP-0"]));
        await game.Top();
        var selected = new List<string>();
        while (game.State.PendingRuleChoice is { } continuation)
        {
            var request = continuation.Request;
            Assert.Equal("STANDBY_CAPACITY_REDUCTION", request.Kind);
            Assert.Equal("P1", request.PlayerId);
            Assert.All(Enumerable.Range(0, 3), i => Assert.True(request.Preview!.CardObjects["SHADOW-" + i].IsFaceDown));
            foreach (var view in new object[] { ResolutionResult.BuildSnapshots(game.State)["P2"], game.PromptFor("P2"),
                MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, game.Events().Last().Sequence, [], game.State) })
            {
                var strings = Strings(JsonSerializer.SerializeToElement(view)).ToArray();
                Assert.DoesNotContain(Shadow, strings);
                Assert.DoesNotContain("STANDBY:0", strings);
                Assert.DoesNotContain(strings, value => value.StartsWith("SHADOW-", StringComparison.Ordinal));
            }
            var command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [request.Options.Last().Id]);
            await Reject(game.State, "P2", command);
            await Reject(game.State, "P1", command with { PaymentChoiceIds = ["STANDBY:999"] });
            await Reject(game.State, "P1", command with { PaymentChoiceIds = [request.Options[0].Id, request.Options[0].Id] });
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
                PendingRuleChoice = continuation with { Request = request with { PlayerId = "P2" } } }));
            selected.Add(Assert.Single(request.Options.Last().ObjectIds!));
            await game.ColdCheckpoint();
            await game.Act("P1", command);
        }
        Assert.Equal(destroyAll ? 2 : 1, selected.Count);
        Assert.All(selected, id => {
            Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard);
            Assert.False(game.State.CardObjects[id].IsFaceDown);
            Assert.Null(game.State.CardObjects[id].StandbyPlacement);
            Assert.True(game.State.CardObjects[id].ObjectGeneration > 1);
        });
        Assert.Equal(destroyAll ? 1 : 2, game.State.CardObjects.Values.Count(c => c.IsFaceDown));
        Assert.Equal(selected.Count, game.Events().Count(e => e.Event.Kind == "STANDBY_CARD_REMOVED"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task LosingOneOfTwoSourcesNeedsNoDiscardWhenTheRemainingCapacityIsEnough()
    {
        var game = new Driver(Put(Position(2), "REMOVE", "VEN·003", "P1", "HAND"));
        await game.Act("P1", Hide(0));
        await game.Act("P1", Hide(1));
        await game.Act("P1", new PlayCardCommand("REMOVE", "VEN·003", ["TRAP-0"]));
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(2, game.State.CardObjects.Values.Count(c => c.IsFaceDown));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STANDBY_CARD_REMOVED");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task LosingBattlefieldControlStillCleansEveryStandbyCard()
    {
        var game = new Driver(Position(2));
        await game.Act("P1", Hide(0));
        await game.Act("P1", Hide(1));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD", "BASE"));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.IsFaceDown);
        Assert.Contains(game.Events(), e => e.Event.Kind == "BATTLEFIELD_STANDBY_REMOVED");
        await game.ColdCheckpoint();
    }

    private static HideCardCommand Hide(int index) => new("SHADOW-" + index, Shadow, "BATTLEFIELD:BF", ["STANDBY_A"]);

    private static MatchState Position(int traps = 0, bool tree = false)
    {
        var state = Create(tree ? "OGN·278/298" : "OGN·275/298");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < traps; i++)
        {
            var id = "TRAP-" + i;
            state = Put(state, id, Trap, "P1", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { Tags = [..state.CardObjects[id].Tags, CardEquipmentKeywordNames.Deploy] } } };
        }
        for (var i = 0; i < 5; i++) state = Put(state, "SHADOW-" + i, Shadow, "P1", "HAND");
        return state;
    }

    private static IEnumerable<string> Strings(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? [value.GetString()!]
            : value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().SelectMany(p => Strings(p.Value))
            : value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().SelectMany(Strings) : [];
}
