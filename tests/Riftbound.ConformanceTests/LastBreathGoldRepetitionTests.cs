using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class LastBreathGoldRepetitionTests
{
    private const string Broker = "SFD·155/221";
    private const string Kill = "OGN·229/298";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachAmplifierEarnsAnIndependentDormantGoldFromAnActuallyPlayedUnit(int amplifiers)
    {
        var game = await Destroy(amplifiers);
        Assert.Equal(amplifiers + 1, Births(game.State).Length);
        Assert.Equal(amplifiers + 1, Births(game.State).Select(c => c.Repetition?.Occurrence ?? 0).Distinct().Count());
        await Resolve(game, amplifiers + 1);
    }

    [Fact]
    public async Task ActuallyCopiedGoldTextAndAmplificationMultiplyIndependentInstances()
    {
        var game = await Destroy(1, copy: true);
        var births = Births(game.State);
        Assert.Equal(4, births.Length); Assert.Equal(2, births.Select(c => c.InstanceId).Distinct().Count());
        Assert.Equal(2, births.Count(c => c.Origin is not null));
        await Resolve(game, 4);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task SimultaneousEquipmentDestructionCapturesGoldBeforeEitherRemoval(bool amplifierFirst)
    {
        // Explicit established unit/equipment characteristics, not a claim of
        // actually animating this source. Destruction and replay are real commands.
        var initial = LastBreathRepetitionTests.AddAmplifier(Put(Put(Create(), "UNIT", Broker, "P1", "BATTLEFIELD", "BF"),
            "MASS", "OGN·022/298", "P1", "HAND"), "AMP", "P1", "BF");
        var removalOrder = amplifierFirst ? new[] { "AMP", "UNIT" } : new[] { "UNIT", "AMP" };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { Tags = [..initial.CardObjects["UNIT"].Tags, CardObjectTags.EquipmentCard] } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Battlefields = ["BF", ..removalOrder] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "OGN·022/298", [])); await game.Top();
        Assert.Equal(removalOrder,
            game.Events().Where(e => e.Event.Kind is "EQUIPMENT_DESTROYED" or "UNIT_DESTROYED")
                .Select(e => (string)e.Event.Payload["targetObjectId"]!).ToArray());
        Assert.Equal(2, Births(game.State).Length);
        await Resolve(game, 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DestructionCostsCaptureTheActualControllerAndReplacedDestructionEarnsNoGold(bool replaced)
    {
        var initial = LastBreathRepetitionTests.AddAmplifier(Put(Put(Create(), "UNIT", Broker, "P1", "BATTLEFIELD", "BF"),
            "GYRO", "RAD·022/167", "P1", "BASE"), "AMP", "P1", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = "P2" } } };
        if (replaced) initial = Put(initial, "GLASS", "OGN·077/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("GYRO", "RAD_022_DESTROY_UNIT_EXHAUST_CREATE_ROBOT", [],
            OptionalCosts: ["DESTROY_FRIENDLY_PERMANENT:UNIT"]));
        if (game.State.PendingRuleChoice is { } pending)
        {
            Assert.True(replaced);
            var choice = Assert.Single(pending.Request.Options, o => o.Id.StartsWith("GEAR:", StringComparison.Ordinal)
                && o.ObjectIds?.Contains("GLASS") == true);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [choice.Id]));
        }
        Assert.Equal(replaced ? 0 : 2, Births(game.State).Length);
        Assert.All(Births(game.State), c => Assert.Equal("P1", c.ControllerId));
        if (replaced)
        {
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
            Assert.Contains("GLASS", game.State.PlayerZones["P1"].Graveyard);
        }
        else Assert.Contains("UNIT", game.State.PlayerZones["P2"].Graveyard);
        if (game.State.TriggerQueue.Count > 0) await Order(game);
        for (var step = 0; game.State.StackItems.Count > 0; step++)
        { Assert.InRange(step, 0, 3); await game.Top(); }
        Assert.Equal(replaced ? 0 : 2, Gold(game.State).Length);
        Assert.All(Gold(game.State), c => Assert.Equal("P1", c.ControllerId));
        Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "机器人");
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsDuplicateMissingAndForgedGoldCaptureReceipts()
    {
        var game = await Destroy(1);
        var item = game.State.TriggerQueue[0]; var birth = item.LastBreathUnitCreation!;
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, item] }),
            e => e.Contains("duplicate captured last-breath unit creation", StringComparison.Ordinal));
        foreach (var invalid in new LastBreathUnitCreationContext?[] {
            null, birth with { TriggerIndex = 99 }, birth with { CreatedAtTick = game.State.Tick + 1 },
            birth with { Repetition = birth.Repetition! with { Occurrence = 2 } },
            birth with { Repetition = birth.Repetition! with { Frame = birth.Repetition.Frame with {
                CardObjects = birth.Repetition.Frame.CardObjects.Where(p => p.Key != "AMP-0").ToDictionary(p => p.Key, p => p.Value) } } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = game.State.TriggerQueue.Select(t =>
                t.TriggerId == item.TriggerId ? t with { LastBreathUnitCreation = invalid } : t).ToArray() }),
                e => e.Contains("captured last-breath unit creation", StringComparison.Ordinal));
        await Resolve(game, 2);
    }

    private static async Task<Driver> Destroy(int amplifiers, bool copy = false)
    {
        var initial = Put(Put(Create(), "UNIT", Broker, "P1", "HAND"), "KILL", Kill, "P1", "HAND");
        for (var i = 0; i < amplifiers; i++) initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP-" + i, "P1", "BF");
        if (copy) initial = Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Broker, [], Destination: "BATTLEFIELD:BF"));
        if (copy) { await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        return game;
    }

    private static async Task Resolve(Driver game, int count)
    {
        if (game.State.TriggerQueue.Count > 0) await Order(game);
        for (var i = 0; i < count; i++)
        {
            var prefix = game.Events().Length; await game.Top();
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "TRIGGER_RESOLVED");
            Assert.Equal(i + 1, Gold(game.State).Length);
            Assert.All(Gold(game.State), c => {
                Assert.True(c.IsExhausted); Assert.Contains(CardObjectTags.EquipmentCard, c.Tags);
                Assert.Contains(c.ObjectId, game.State.PlayerZones["P1"].Base);
            });
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    private static CardObjectState[] Gold(MatchState state) => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is { } no
        && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币").ToArray();
    private static LastBreathUnitCreationContext[] Births(MatchState state) => state.TriggerQueue.Select(t => t.LastBreathUnitCreation)
        .Concat(state.StackItems.Select(t => t.LastBreathUnitCreation)).OfType<LastBreathUnitCreationContext>().ToArray();
    private static Task Order(Driver game) => game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
}
