using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadGravityFieldLastBreathTests
{
    private const string Card = "RAD·062/167";
    private const string Kill = "RAD·101/167";

    [Fact]
    public async Task ActualDeploymentPaysThePrintedFiveManaAndOneBlue()
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(39559, printed.Id); Assert.Equal(5, printed.Energy); Assert.Equal(1, printed.ReturnEnergy);
        var game = new Driver(Position()); await Deploy(game);
        Assert.Equal(25, game.State.RunePools["P1"].Mana); Assert.Equal(29, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("BF", game.State.ObjectLocations["SOURCE"].BattlefieldObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, game.State.CardObjects["SOURCE"].Tags);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(4, 1)] [InlineData(5, 0)]
    public async Task MissingPrintedResourcesRejectBeforeEntry(int mana, int power)
        => await Reject(Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(mana, power), ["P2"] = RunePool.Empty } }, "P1", Play("BF"));

    [Theory]
    [InlineData("BASE")] [InlineData("")] [InlineData("BATTLEFIELD:ABSENT")]
    public async Task DeployRequiresAnExistingBattlefield(string destination)
        => await Reject(Position(), "P1", new PlayCardCommand("SOURCE", Card, [], Destination: destination));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EquipmentDestructionEarnsOneDrawWithoutCountingAUnitDeath(bool stolen)
    {
        var initial = Position();
        if (stolen) initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "SOURCE" ? p.Value with { OwnerId = "P2" } : p.Value) };
        var game = new Driver(initial); await Deploy(game);
        var generation = game.State.CardObjects["SOURCE"].ObjectGeneration;
        await Destroy(game);
        Assert.Contains("SOURCE", game.State.PlayerZones[stolen ? "P2" : "P1"].Graveyard);
        Assert.Empty(game.State.DeathLedger.Counts);
        var birth = Assert.Single(Births(game.State));
        Assert.Equal("P1", birth.ControllerId); Assert.Equal(generation, birth.Source.Generation);
        Assert.Equal(TriggerKinds.UnitLastBreathDrawOne, birth.Kind);
        await ResolveDraws(game, 1); await game.Replay();
    }

    [Fact]
    public async Task SimultaneousEquipmentDestructionCapturesTwoIndependentDraws()
    {
        var game = new Driver(Put(Put(Position(), "SECOND", Card, "P1", "HAND"), "MASS", "OGN·022/298", "P1", "HAND"));
        await Deploy(game); await game.Act("P1", new PlayCardCommand("SECOND", Card, [], Destination: "BATTLEFIELD:OTHER"));
        await game.Act("P1", new PlayCardCommand("MASS", "OGN·022/298", [])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Equal(2, game.State.TriggerQueue.Select(t => t.LastBreathDraw!.DeathId).Distinct().Count());
        Assert.Empty(game.State.DeathLedger.Counts);
        await ResolveDraws(game, 2); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualAnimationAndCopiedTextEachEarnOneDrawEvenWhenTheCopyEquipmentAlsoDies(bool simultaneous)
    {
        var initial = Put(Put(WithOrianna(Position()), "SONG", "SFD·059/221", "P1", "BASE"),
            "FINISH", simultaneous ? "OGN·022/298" : "OGN·229/298", "P1", "HAND");
        var game = new Driver(initial); await Deploy(game); await Animate(game);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "SOURCE", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(Card, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await game.Act("P1", new PlayCardCommand("FINISH", simultaneous ? "OGN·022/298" : "OGN·229/298",
            simultaneous ? [] : ["SOURCE"])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count); Assert.Single(game.State.TriggerQueue, t => t.LastBreathDraw!.Origin is not null);
        Assert.Equal(2, game.State.TriggerQueue.Select(t => t.LastBreathDraw!.InstanceId).Distinct().Count());
        if (simultaneous) Assert.Contains("SONG", game.State.PlayerZones["P1"].Graveyard);
        await ResolveDraws(game, 2); await game.Replay();
    }

    [Fact]
    public async Task ADisappearingStagedImageKeepsItsCapturedDrawAndRequiresItsFactoryReceipt()
    {
        // An already established copy face; its actual creation is outside this fixture.
        var initial = Put(Put(Create(), "SOURCE", Card, "P1", "BATTLEFIELD", "BF"), "KILL", Kill, "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SOURCE"] = initial.CardObjects["SOURCE"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                Tags = initial.CardObjects["SOURCE"].Tags.Append(CardEquipmentKeywordNames.Deploy).ToArray(),
                CopyFaceTransitions = [new(0, 0, P6TokenFactoryCatalog.ImageTokenCardNo, Card)] } } };
        var game = new Driver(initial); await Destroy(game);
        Assert.DoesNotContain("SOURCE", game.State.CardObjects.Keys);
        var birth = Assert.Single(Births(game.State)); Assert.NotNull(birth.SourceTokenFactoryCardNo);
        var forged = WithBirth(game.State, birth with { SourceTokenFactoryCardNo = null });
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
        await ResolveDraws(game, 1); await game.Replay();
    }

    [Fact]
    public async Task ADrawBirthRejectsCurrentGenerationFutureTickAndAChangedInstance()
    {
        var game = new Driver(Position()); await Deploy(game); await Destroy(game);
        var birth = Assert.Single(Births(game.State));
        foreach (var invalid in new[] {
            birth with { Source = birth.Source with { Generation = game.State.CardObjects["SOURCE"].ObjectGeneration } },
            birth with { CreatedAtTick = game.State.Tick + 1 }, birth with { InstanceId = "PRINTED:FORGED:0" },
            birth with { TriggerIndex = 1 }, birth with { SourceTokenFactoryCardNo = P4ActivatedAbilityCatalog.GoldTokenUnlCardNo } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(WithBirth(game.State, invalid)),
                e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
        var duplicate = game.State.TriggerQueue.Count > 0
            ? game.State with { TriggerQueue = [..game.State.TriggerQueue, game.State.TriggerQueue[0]] }
            : game.State with { StackItems = [..game.State.StackItems, game.State.StackItems[0]] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(duplicate),
            e => e.Contains("duplicate captured last-breath draw", StringComparison.Ordinal));
        await ResolveDraws(game, 1); await game.Replay();
    }

    [Fact]
    public async Task AnActualOpponentMovePaysTheMixedFeeAndItsNextHoldDestroysTheDeployment()
    {
        var game = new Driver(Put(Position(), "RUNE", "OGN·007/298", "P2", "BASE"));
        await Deploy(game); await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new TapRuneCommand("RUNE")); await game.Act("P2", new RecycleRuneCommand("RUNE"));
        var pool = game.State.RunePools["P2"];
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF"));
        Assert.Equal(pool.Mana - 1, game.State.RunePools["P2"].Mana);
        Assert.Equal(pool.TotalPower - 1, game.State.RunePools["P2"].TotalPower);
        for (var steps = 0; game.State.FocusPlayerId is { } focus; steps++) {
            Assert.InRange(steps, 0, 6); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act("P2", new EndTurnCommand()); await game.Act("P1", new EndTurnCommand());
        // The shared Deploy hold skill resolves before its resulting Last Breath.
        for (var steps = 0; !game.State.PlayerZones["P1"].Graveyard.Contains("SOURCE"); steps++)
        {
            Assert.InRange(steps, 0, 5);
            if (game.State.TriggerQueue.Count > 0) await Order(game); else await game.Top();
        }
        Assert.True(game.State.TriggerQueue.Any(t => t.LastBreathDraw is not null)
            || game.State.StackItems.Any(t => t.LastBreathDraw is not null));
        if (game.State.TriggerQueue.Any(t => t.LastBreathDraw is not null)) await Order(game);
        while (game.State.StackItems.Any(t => t.LastBreathDraw is not null)) await game.Top();
        await game.Replay();
    }

    private static LastBreathDrawContext[] Births(MatchState state) => state.TriggerQueue.Select(t => t.LastBreathDraw)
        .Concat(state.StackItems.Select(t => t.LastBreathDraw)).OfType<LastBreathDrawContext>().ToArray();
    private static MatchState WithBirth(MatchState state, LastBreathDrawContext birth) => state with {
        TriggerQueue = state.TriggerQueue.Select(t => t.LastBreathDraw is null ? t : t with { LastBreathDraw = birth }).ToArray(),
        StackItems = state.StackItems.Select(t => t.LastBreathDraw is null ? t : t with { LastBreathDraw = birth }).ToArray() };

    private static MatchState Position() => Put(Put(Create(), "SOURCE", Card, "P1", "HAND"), "KILL", Kill, "P1", "HAND");
    private static PlayCardCommand Play(string field) => new("SOURCE", Card, [], Destination: "BATTLEFIELD:" + field);
    private static Task Deploy(Driver game) => game.Act("P1", Play("BF"));
    private static async Task Destroy(Driver game)
    { await game.Act("P1", new PlayCardCommand("KILL", Kill, ["SOURCE"])); await game.Top(); }
    private static Task Order(Driver game) => game.Act(game.State.TriggerQueue[0].ControllerId,
        new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    private static async Task ResolveDraws(Driver game, int count)
    {
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        if (game.State.TriggerQueue.Count > 0) await Order(game);
        for (var i = 0; i < count; i++) await game.Top();
        Assert.Equal(hand + count, game.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Payload.ContainsKey("equipmentLastBreathDraws"));
    }
    private static MatchState WithOrianna(MatchState state)
    {
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: "RAD·145/167", ownerId: "P1", controllerId: "P1", tags: ["传奇"]) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };
    }
    private static async Task Animate(Driver game)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.OriannaLegendAbilityId, ["SOURCE"], ["SPEND_MANA:1"])); await game.Top();
    }
}
