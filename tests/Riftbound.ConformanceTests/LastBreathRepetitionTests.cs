using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// Amplifiers are explicit public fixtures, not an admission or real-play claim
// for RAD017. Its remaining last-breath consumers are tracked in BACKLOG.
public sealed class LastBreathRepetitionTests
{
    private const string Amplifier = "RAD·017/167";
    private const string Draw = "OGN·096/298";
    private const string Kill = "OGN·229/298";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachAmplifierAddsAnIndependentDrawWithItsOwnResponse(int amplifiers)
    {
        var game = await Destroy(Draw, amplifiers);
        var births = Draws(game.State);
        Assert.Equal(amplifiers + 1, births.Length);
        Assert.Equal(amplifiers + 1, births.Select(c => c.Repetition?.Occurrence ?? 0).Distinct().Count());
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await Order(game);
        for (var i = 0; i <= amplifiers; i++) {
            await game.Top(); Assert.Equal(hand + i + 1, game.State.PlayerZones["P1"].Hand.Count);
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·216/298", 1, false)] [InlineData("OGN·216/298", 2, false)]
    [InlineData("OGN·075/298", 1, true)] [InlineData("OGN·075/298", 2, true)]
    public async Task RuneCallsAndCompoundDrawsRemainSeparateSkills(string no, int amplifiers, bool compound)
    {
        var game = await Destroy(no, amplifiers);
        var births = game.State.TriggerQueue.Select(t => t.LastBreathRuneCall)
            .Concat(game.State.StackItems.Select(t => t.LastBreathRuneCall)).OfType<LastBreathRuneCallContext>().ToArray();
        Assert.Equal(amplifiers + 1, births.Length); Assert.All(births, c => Assert.NotNull(c.Repetition));
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
        await Order(game);
        for (var i = 0; i <= amplifiers; i++) {
            await game.Top();
            Assert.Equal(runes.Skip((i + 1) * (compound ? 2 : 1)), game.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal(hand + (compound ? i + 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task UnitCreationUsesDistinctSkillsAndTheExistingTokenFactory(int amplifiers)
    {
        var game = await Destroy("RAD·119/167", amplifiers);
        Assert.Equal(amplifiers + 1, game.State.TriggerQueue.Count + game.State.StackItems.Count);
        await Order(game);
        for (var i = 0; i <= amplifiers; i++) {
            await game.Top();
            Assert.Equal(i + 1, game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("OTHER", "P1", false, 1)]
    [InlineData("BF", "P2", false, 1)]
    [InlineData("BF", "P1", true, 1)]
    [InlineData("HAND", "P1", false, 1)]
    [InlineData("BASE", "P1", false, 1)]
    [InlineData("BF", "P1", false, 2)]
    public async Task OnlyFaceUpFriendlyTextAtTheExactLocationAddsATrigger(string location, string controller, bool hidden, int expected)
    {
        var initial = Position(Draw);
        initial = AddAmplifier(initial, "AMP", controller, location, hidden);
        var game = await Destroy(initial, Draw);
        Assert.Equal(expected, Draws(game.State).Length);
        await Order(game); for (var i = 0; i < expected; i++) await game.Top();
        await game.Replay();
    }

    [Fact]
    public async Task OwnershipDoesNotChangeTheAmplifiersController()
    {
        var initial = AddAmplifier(Position(Draw), "AMP", "P1", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["AMP"] = initial.CardObjects["AMP"] with { OwnerId = "P2" } } };
        var game = await Destroy(initial, Draw); Assert.Equal(2, Draws(game.State).Length);
        await Order(game); await game.Top(); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task CopiedDrawTextAndAmplifiersMultiplyTextInstancesIndependently()
    {
        var initial = AddAmplifier(Put(Position(Draw), "SONG", "SFD·059/221", "P1", "BASE"), "AMP", "P1", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Draw, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        await KillUnit(game);
        Assert.Equal(4, Draws(game.State).Length);
        Assert.Equal(2, Draws(game.State).Select(c => c.InstanceId).Distinct().Count());
        await Order(game); for (var i = 0; i < 4; i++) await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsDuplicateOccurrencesAndForgedRepetitionEvidence()
    {
        var game = await Destroy(Draw, 1);
        var trigger = game.State.TriggerQueue.First(t => t.LastBreathDraw is not null);
        var birth = trigger.LastBreathDraw!; var repetition = birth.Repetition!;
        var frame = repetition.Frame;
        foreach (var invalid in new[] {
            birth with { Repetition = null },
            birth with { Repetition = repetition with { Occurrence = -1 } },
            birth with { Repetition = repetition with { Occurrence = 2 } },
            birth with { Repetition = repetition with { Frame = frame with { CardObjects = frame.CardObjects
                .Where(p => p.Key != "AMP-0").ToDictionary(p => p.Key, p => p.Value) } } },
            birth with { Repetition = repetition with { Frame = frame with { CardObjects = new Dictionary<string, CardObjectState>(frame.CardObjects) {
                ["UNIT"] = frame.CardObjects["UNIT"] with { ObjectGeneration = birth.Source.Generation + 1 } } } } },
            birth with { Repetition = repetition with { Frame = frame with { PlayerZones = new Dictionary<string, PlayerZones>(frame.PlayerZones) {
                ["P1"] = frame.PlayerZones["P1"] with { Hand = ["P1-D0"] } } } } } })
        {
            var forged = game.State with { TriggerQueue = game.State.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId
                ? t with { LastBreathDraw = invalid } : t).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
        }
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
            TriggerQueue = [..game.State.TriggerQueue, trigger] }), e => e.Contains("duplicate captured last-breath draw", StringComparison.Ordinal));
        await Order(game); await game.Top(); await game.Top(); await game.Replay();
    }

    private static MatchState Position(string no)
    {
        var state = Put(Put(Create(), "UNIT", no, "P1", "HAND"), "KILL", Kill, "P1", "HAND");
        for (var i = 0; i < 8; i++) state = Put(state, "RUNE-" + i, "UNL-R02", "P1", "RUNE_DECK");
        return state;
    }

    private static MatchState AddAmplifier(MatchState state, string id, string controller, string location, bool hidden = false)
    {
        var zone = location is "HAND" or "BASE" ? location : "BATTLEFIELD";
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: Amplifier, manaCost: 4, ownerId: controller, controllerId: controller,
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy], isFaceDown: hidden) } };
        return RadUnitEffectTestFixture.Place(state, id, controller, zone, zone == "BATTLEFIELD" ? location : null);
    }

    private static Task<Driver> Destroy(string no, int amplifiers)
    {
        var state = Position(no);
        for (var i = 0; i < amplifiers; i++) state = AddAmplifier(state, "AMP-" + i, "P1", "BF");
        return Destroy(state, no);
    }
    private static async Task<Driver> Destroy(MatchState initial, string no)
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await KillUnit(game); return game;
    }
    private static async Task KillUnit(Driver game)
    { await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top(); }
    private static LastBreathDrawContext[] Draws(MatchState state) => state.TriggerQueue.Select(t => t.LastBreathDraw)
        .Concat(state.StackItems.Select(t => t.LastBreathDraw)).OfType<LastBreathDrawContext>().ToArray();
    private static Task Order(Driver game) => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask
        : game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
}
