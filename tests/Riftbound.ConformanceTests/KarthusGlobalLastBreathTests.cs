using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class KarthusGlobalLastBreathTests
{
    private const string Karthus = "OGN·236/298";
    private const string Sentinel = "OGN·096/298";
    private const string Kill = "OGN·229/298";

    [Theory]
    [InlineData(1, "BASE")] [InlineData(2, "BASE")]
    [InlineData(1, "BATTLEFIELD:BF")] [InlineData(2, "BATTLEFIELD:BF")]
    public async Task ActuallyPlayedKarthusAddsSeparateDrawsAtTheBaseAndBattlefield(int count, string destination)
    {
        var initial = Position();
        for (var i = 0; i < count; i++) initial = Put(initial, "KARTHUS-" + i, Karthus, "P1", "HAND");
        var game = new Driver(initial);
        for (var i = 0; i < count; i++)
        {
            await game.Act("P1", new PlayCardCommand("KARTHUS-" + i, Karthus, [], Destination: "BASE"));
            Assert.Empty(game.State.StackItems);
        }
        await PlayVictim(game, Sentinel, destination); await Destroy(game);
        Assert.Equal(count + 1, Draws(game.State).Length);
        Assert.Equal(count + 1, Draws(game.State).Select(c => c.Repetition!.Occurrence).Distinct().Count());
        await ResolveDraws(game, count + 1);
    }

    [Theory]
    [InlineData("BASE", "P1", false, 2)]
    [InlineData("BF", "P1", false, 2)]
    [InlineData("OTHER", "P1", false, 2)]
    [InlineData("BASE", "P2", false, 1)]
    [InlineData("OTHER", "P2", false, 1)]
    [InlineData("BF", "P1", true, 1)]
    [InlineData("HAND", "P1", false, 1)]
    [InlineData("GRAVEYARD", "P1", false, 1)]
    public async Task OnlyFaceUpControlledFieldTextAppliesButItsLocationDoesNotLimitIt(
        string location, string controller, bool hidden, int expected)
    {
        var initial = AddKarthus(Position(), "KARTHUS", controller, location);
        if (hidden) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["KARTHUS"] = initial.CardObjects["KARTHUS"] with { IsFaceDown = true } } };
        var game = new Driver(initial); await PlayVictim(game); await Destroy(game);
        Assert.Equal(expected, Draws(game.State).Length); await ResolveDraws(game, expected);
    }

    [Fact]
    public async Task TheControllerOfBothUnitsReceivesTheTriggersDespiteDifferentOwners()
    {
        var initial = AddKarthus(Position(), "KARTHUS", "P1", "BASE");
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
            p => p.Key is "UNIT" or "KARTHUS" ? p.Value with { OwnerId = "P2" } : p.Value) };
        var game = new Driver(initial); await PlayVictim(game); await Destroy(game);
        Assert.Contains("UNIT", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, Draws(game.State).Length); Assert.All(Draws(game.State), c => Assert.Equal("P1", c.ControllerId));
        await ResolveDraws(game, 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GlobalAndLocalAmplifiersAddBeforeEachPrintedOrCopiedLastBreath(bool copy)
    {
        var initial = AddKarthus(Position(), "KARTHUS", "P1", "BASE");
        initial = LastBreathRepetitionTests.AddAmplifier(initial, "LOCAL", "P1", "BF");
        initial = LastBreathRepetitionTests.AddAmplifier(initial, "REMOTE", "P1", "OTHER");
        if (copy) initial = Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial); await PlayVictim(game);
        if (copy) { await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
        await Destroy(game);
        Assert.Equal(copy ? 6 : 3, Draws(game.State).Length);
        Assert.Equal(copy ? 2 : 1, Draws(game.State).Select(c => c.InstanceId).Distinct().Count());
        await ResolveDraws(game, copy ? 6 : 3);
    }

    [Fact]
    public async Task ActuallyCopiedKarthusTextAddsAnotherGlobalOccurrence()
    {
        var initial = Put(Put(Position(), "KARTHUS", Karthus, "P1", "HAND"), "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("KARTHUS", Karthus, [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "KARTHUS", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(Karthus, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await PlayVictim(game); await Destroy(game);
        Assert.Equal(3, Draws(game.State).Length); await ResolveDraws(game, 3);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SimultaneouslyDestroyedKarthusAppliesBeforeEitherUnitLeaves(bool karthusFirst)
    {
        var initial = AddKarthus(Position(), "KARTHUS", "P1", "BASE");
        initial = Put(initial, "MASS", "UNL-180/219", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "UNIT", "P1", "BASE");
        var order = karthusFirst ? new[] { "KARTHUS", "UNIT" } : new[] { "UNIT", "KARTHUS" };
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = order } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "UNL-180/219", [])); await game.Top();
        Assert.Equal(order, game.Events().Where(e => e.Event.Kind == "UNIT_DESTROYED"
            && order.Contains((string)e.Event.Payload["targetObjectId"]!)).Select(e => (string)e.Event.Payload["targetObjectId"]!));
        Assert.Contains("KARTHUS", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, Draws(game.State).Length);
        Assert.All(Draws(game.State), c => Assert.Contains("KARTHUS", c.Repetition!.Frame.PlayerZones["P1"].Base));
        await ResolveDraws(game, 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningKarthusBeforeDeathPreventsTheExtraTriggerButAfterCaptureDoesNotRemoveIt(bool beforeDeath)
    {
        var initial = AddKarthus(Position(), "KARTHUS", "P1", "BASE");
        initial = Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); await PlayVictim(game);
        if (!beforeDeath) { await Destroy(game); await Order(game); }
        if (game.State.PriorityPlayerId == "P2") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["KARTHUS"])); await game.Top();
        Assert.Contains("KARTHUS", game.State.PlayerZones["P1"].Hand);
        if (beforeDeath) await Destroy(game);
        Assert.Equal(beforeDeath ? 1 : 2, Draws(game.State).Length);
        await ResolveDraws(game, beforeDeath ? 1 : 2);
    }

    [Fact]
    public async Task ReplacedDestructionCreatesNoLastBreathEvenWithKarthusInPlay()
    {
        var initial = Put(AddKarthus(Position(), "KARTHUS", "P1", "BASE"), "SAVE", "UNL-175/219", "P1", "HAND");
        var game = new Driver(initial); await PlayVictim(game);
        await game.Act("P1", new PlayCardCommand("SAVE", "UNL-175/219", ["UNIT"])); await game.Top();
        await Destroy(game);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Empty(Draws(game.State));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·216/298", 1, 0, "")]
    [InlineData("OGN·075/298", 2, 1, "")]
    [InlineData("RAD·119/167", 0, 0, "战鹰")]
    [InlineData("SFD·155/221", 0, 0, "金币")]
    public async Task GlobalOccurrencesAlsoResolveIndependentRuneCallsAndUnitOrEquipmentCreation(
        string cardNo, int runesPerSkill, int cardsPerSkill, string tokenFamily)
    {
        var game = new Driver(AddKarthus(Position(cardNo), "KARTHUS", "P1", "BASE"));
        await PlayVictim(game, cardNo); await Destroy(game);
        Assert.Equal(2, game.State.TriggerQueue.Count + game.State.StackItems.Count);
        var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await Order(game);
        for (var i = 1; i <= 2; i++)
        {
            var prefix = game.Events().Length; await game.Top();
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "TRIGGER_RESOLVED");
            Assert.Equal(runes.Skip(i * runesPerSkill), game.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal(hand + i * cardsPerSkill, game.State.PlayerZones["P1"].Hand.Count);
            if (tokenFamily.Length > 0) Assert.Equal(i, game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo is { } no
                && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == tokenFamily));
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsMissingAndForgedGlobalSourcesAndDuplicateOccurrences()
    {
        var game = new Driver(AddKarthus(Position(), "KARTHUS", "P1", "BASE"));
        await PlayVictim(game); await Destroy(game);
        var item = game.State.TriggerQueue[0]; var birth = item.LastBreathDraw!; var repetition = birth.Repetition!;
        foreach (var invalid in new[] {
            birth with { Repetition = null },
            birth with { Repetition = repetition with { Occurrence = 2 } },
            birth with { Repetition = repetition with { Frame = repetition.Frame with { CardObjects = repetition.Frame.CardObjects
                .Where(p => p.Key != "KARTHUS").ToDictionary(p => p.Key, p => p.Value) } } },
            birth with { Repetition = repetition with { Frame = repetition.Frame with { PlayerZones = new Dictionary<string, PlayerZones>(repetition.Frame.PlayerZones) {
                ["P1"] = repetition.Frame.PlayerZones["P1"] with { Base = repetition.Frame.PlayerZones["P1"].Base.Where(id => id != "KARTHUS").ToArray() } } } } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = game.State.TriggerQueue.Select(t =>
                t.TriggerId == item.TriggerId ? t with { LastBreathDraw = invalid } : t).ToArray() }),
                e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, item] }),
            e => e.Contains("duplicate captured last-breath draw", StringComparison.Ordinal));
        Assert.DoesNotContain("repetition", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)), StringComparison.OrdinalIgnoreCase);
        await ResolveDraws(game, 2);
    }

    private static MatchState Position(string cardNo = Sentinel)
    {
        var state = Put(Put(Create(), "UNIT", cardNo, "P1", "HAND"), "KILL", Kill, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 6; i < 16; i++) state = Put(state, "P1-D" + i, "OGN·219/298", "P1", "MAIN_DECK");
        for (var i = 0; i < 8; i++) state = Put(state, "RUNE-" + i, "UNL-R02", "P1", "RUNE_DECK");
        return state;
    }
    private static MatchState AddKarthus(MatchState state, string id, string controller, string location)
    {
        state = Put(state, id, Karthus, controller, location is "BF" or "OTHER" ? "BATTLEFIELD" : location,
            location is "BF" or "OTHER" ? location : null);
        // Established control must agree with the occupied battlefield before
        // the real commands begin; this is not a conquest setup.
        return location is "BF" or "OTHER" ? state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [location] = state.CardObjects[location] with { ControllerId = controller } } } : state;
    }
    private static Task PlayVictim(Driver game, string cardNo = Sentinel, string destination = "BATTLEFIELD:BF")
        => game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: destination));
    private static async Task Destroy(Driver game)
    { await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top(); }
    private static LastBreathDrawContext[] Draws(MatchState state) => state.TriggerQueue.Select(t => t.LastBreathDraw)
        .Concat(state.StackItems.Select(t => t.LastBreathDraw)).OfType<LastBreathDrawContext>().ToArray();
    private static Task Order(Driver game) => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask
        : game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    private static async Task ResolveDraws(Driver game, int count)
    {
        var hand = game.State.PlayerZones["P1"].Hand.Count; await Order(game);
        for (var i = 1; i <= count; i++)
        {
            var prefix = game.Events().Length; await game.Top();
            Assert.Equal(hand + i, game.State.PlayerZones["P1"].Hand.Count);
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "TRIGGER_RESOLVED");
        }
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }
}
