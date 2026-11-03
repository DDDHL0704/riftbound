using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class LastBreathBombRepetitionTests
{
    private const string Amateur = "RAD·001/167";
    private const string Destroy = "SFD·032/221";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ActuallyCreatedBombEarnsIndependentTargetedSkillsAndResponses(int amplifiers)
    {
        var game = await CreateBombs(amplifiers);
        var bomb = Assert.Single(Bombs(game.State));
        await DestroyBomb(game, bomb);
        var captures = Captures(game.State);
        Assert.Equal(amplifiers + 1, captures.Length);
        Assert.Equal(amplifiers + 1, captures.Select(c => c.Repetition?.Occurrence ?? 0).Distinct().Count());
        Assert.False(game.State.CardObjects.ContainsKey(bomb));
        Assert.DoesNotContain("bombDestruction", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)), StringComparison.OrdinalIgnoreCase);
        await ConfirmAll(game);
        Assert.Equal(amplifiers + 1, game.State.StackItems.Count);
        for (var i = 0; i <= amplifiers; i++)
        {
            var prefix = game.Events().Length;
            await game.Top();
            Assert.Equal(2 * (i + 1), game.State.CardObjects["ENEMY"].Damage);
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "TRIGGER_RESOLVED");
        }
        Assert.Equal(0, game.State.CardObjects["OTHER-ENEMY"].Damage);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task EachActuallyCreatedChainedBombCapturesItsOwnAmplification()
    {
        var game = await CreateBombs(1, count: 2);
        var bombs = Bombs(game.State);
        Assert.Equal(2, bombs.Length);
        await DestroyBomb(game, bombs[0]);
        await Drain(game);
        Assert.Empty(Bombs(game.State));
        Assert.Equal(8, game.State.CardObjects["ENEMY"].Damage);
        Assert.All(bombs, bomb => Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), bomb))));
        Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "BOMB_CHAIN"));
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task SimultaneousEquipmentDestructionCapturesBeforeEitherPermanentLeaves(bool amplifierFirst)
    {
        // A factory-created public token fixture; the actual mass destruction,
        // confirmation, responses and original replay are exercised below.
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        var initial = LastBreathRepetitionTests.AddAmplifier(Put(Position(), "MASS", "OGN·022/298", "P1", "HAND"),
            "AMP", "P1", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BOMB"] = factory.CreateObject("BOMB", "P1", "P1") } };
        initial = RadUnitEffectTestFixture.Place(initial, "BOMB", "P1", "BATTLEFIELD", "BF");
        var order = amplifierFirst ? new[] { "AMP", "BOMB" } : new[] { "BOMB", "AMP" };
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Battlefields = ["BF", ..order] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "OGN·022/298", [])); await game.Top();
        Assert.Equal(order, game.Events().Where(e => e.Event.Kind == "EQUIPMENT_DESTROYED")
            .Select(e => (string)e.Event.Payload["targetObjectId"]!).ToArray());
        Assert.Equal(2, Captures(game.State).Length);
        Assert.All(Captures(game.State), c => Assert.Contains("AMP", c.Repetition!.Frame.CardObjects.Keys));
        await Drain(game); Assert.Equal(4, game.State.CardObjects["ENEMY"].Damage); await game.Replay();
    }

    [Fact]
    public async Task TheCapturedActualControllerKeepsTheBombSkillDespiteAnotherOwner()
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        var initial = LastBreathRepetitionTests.AddAmplifier(Put(Position(), "KILL", Destroy, "P1", "HAND"),
            "AMP", "P1", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BOMB"] = factory.CreateObject("BOMB", "P2", "P1") } };
        initial = RadUnitEffectTestFixture.Place(initial, "BOMB", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await DestroyBomb(game, "BOMB");
        Assert.Equal(2, Captures(game.State).Length);
        Assert.All(Captures(game.State), c => { Assert.Equal("P1", c.ControllerId); Assert.Equal("P2", c.OwnerId); });
        await Drain(game); Assert.Equal(4, game.State.CardObjects["ENEMY"].Damage); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsDuplicateMissingAndForgedBombReceiptsAcrossQueueAndStack()
    {
        var game = await CreateBombs(1);
        await DestroyBomb(game, Assert.Single(Bombs(game.State)));
        var item = game.State.TriggerQueue[0]; var field = item.FieldContext!; var capture = field.BombDestruction!;
        foreach (var invalid in new[] {
            capture with { Bomb = null! }, capture with { Repetition = null }, capture with { CapturedTick = game.State.Tick + 1 },
            capture with { Repetition = capture.Repetition! with { Occurrence = 2 } },
            capture with { Repetition = capture.Repetition! with { Frame = capture.Repetition.Frame with {
                CardObjects = capture.Repetition.Frame.CardObjects.Where(p => p.Key != "AMP-0").ToDictionary(p => p.Key, p => p.Value) } } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = game.State.TriggerQueue.Select(t =>
                t.TriggerId == item.TriggerId ? t with { FieldContext = field with { BombDestruction = invalid } } : t).ToArray() }),
                e => e.Contains("field trigger context", StringComparison.Ordinal));
        var forgedLocation = game.State with { TriggerQueue = game.State.TriggerQueue.Select(t => t.TriggerId == item.TriggerId
            ? t with { FieldContext = field with { BattlefieldId = "OTHER", BombDestruction = capture with { BattlefieldId = "OTHER" } } } : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forgedLocation), e => e.Contains("field trigger context", StringComparison.Ordinal));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, item] }),
            e => e.Contains("duplicate captured bomb destruction", StringComparison.Ordinal));
        var shifted = item with {
            TriggerId = item.TriggerId.Replace($"bomb-death-{capture.CapturedTick}-", $"bomb-death-{capture.CapturedTick - 1}-", StringComparison.Ordinal),
            FieldContext = field with { BombDestruction = capture with { CapturedTick = capture.CapturedTick - 1 } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, shifted] }),
            e => e.Contains("duplicate captured bomb destruction", StringComparison.Ordinal));
        await ConfirmAll(game);
        var stack = game.State.StackItems.Single(t => t.FieldContext?.BombDestruction?.Repetition?.Occurrence == capture.Repetition!.Occurrence);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [item],
            StackItems = [stack] }), e => e.Contains("duplicate captured bomb destruction", StringComparison.Ordinal));
        await Drain(game); await game.Replay();
    }

    private static MatchState Position()
    {
        var initial = RadUnitEffectTestFixture.Place(Create("OGN·278/298"), "ENEMY", "P2", "BATTLEFIELD", "BF");
        initial = Put(initial, "OTHER-ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 30 } } };
    }
    private static async Task<Driver> CreateBombs(int amplifiers, int count = 1)
    {
        var initial = Put(Position(), "KILL", Destroy, "P1", "HAND");
        for (var i = 0; i < amplifiers; i++) initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP-" + i, "P1", "BF");
        for (var i = 0; i < count; i++) initial = Put(initial, "UNIT-" + i, Amateur, "P1", "HAND");
        var game = new Driver(initial);
        for (var i = 0; i < count; i++) {
            await game.Act("P1", new PlayCardCommand("UNIT-" + i, Amateur, ["BF"], Destination: "BASE")); await Drain(game);
        }
        return game;
    }
    private static async Task DestroyBomb(Driver game, string bomb)
    { await game.Act("P1", new PlayCardCommand("KILL", Destroy, [bomb])); await game.Top(); }
    private static async Task ConfirmAll(Driver game)
    {
        if (game.State.TriggerQueue.Count > 0 && game.State.PendingCardChoice is null)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; game.State.PendingCardChoice is { } choice; i++) {
            Assert.InRange(i, 0, 8); Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
            Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        }
    }
    private static async Task Drain(Driver game)
    {
        for (var i = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0 || game.State.PendingCardChoice is not null; i++) {
            Assert.InRange(i, 0, 30); await ConfirmAll(game);
            if (game.State.StackItems.Count > 0) await game.Top();
        }
    }
    private static string[] Bombs(MatchState state) => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo)
        .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    private static BombDestructionOrigin[] Captures(MatchState state) => state.TriggerQueue.Select(t => t.FieldContext?.BombDestruction)
        .Concat(state.StackItems.Select(t => t.FieldContext?.BombDestruction)).OfType<BombDestructionOrigin>().ToArray();
}
