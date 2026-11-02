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
        Assert.True(game.State.Phase == MatchPhases.Main && game.State.TimingState == TimingStates.NeutralOpen
            && game.State.ActivePlayerId == "P1" && game.State.CardObjects["SOURCE"].Tags.Contains(CardObjectTags.UnitCard)
            && game.State.PlayerZones["P1"].Base.Contains("SONG"),
            $"phase={game.State.Phase}; timing={game.State.TimingState}; active={game.State.ActivePlayerId}; focus={game.State.FocusPlayerId}; "
                + $"sourceTags={string.Join(',', game.State.CardObjects["SOURCE"].Tags)}; song={game.State.ObjectLocations["SONG"]}");
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "SOURCE", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(Card, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await game.Act("P1", new PlayCardCommand("FINISH", simultaneous ? "OGN·022/298" : "OGN·229/298",
            simultaneous ? [] : ["SOURCE"])); await game.Top();
        Assert.Equal(2, game.State.TriggerQueue.Count); Assert.Single(game.State.TriggerQueue, t => t.LastBreathDraw!.Origin is not null);
        Assert.Equal(2, game.State.TriggerQueue.Select(t => t.LastBreathDraw!.InstanceId).Distinct().Count());
        if (simultaneous) Assert.Contains("SONG", game.State.PlayerZones["P1"].Graveyard);
        await ResolveDraws(game, 2); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnActualEquipmentDestructionCostEarnsTheMinionAndOnlyAnUnreplacedDeathDraws(bool replaced)
    {
        var initial = Put(replaced ? WithOrianna(Position()) : Position(), "GYRO", "RAD·022/167", "P1", "BASE");
        if (replaced) initial = Put(initial, "GLASS", "OGN·077/298", "P1", "BASE");
        var game = new Driver(initial); await Deploy(game);
        if (replaced) await Animate(game);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        var generation = game.State.CardObjects["SOURCE"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("GYRO", "RAD_022_DESTROY_EQUIPMENT_EXHAUST_CREATE_MINION", [],
            OptionalCosts: ["DESTROY_FRIENDLY_PERMANENT:SOURCE"]));
        if (replaced)
        {
            if (game.State.PendingRuleChoice is { } pending)
            {
                var replacement = Assert.Single(pending.Request.Options, option => option.Id.StartsWith("GEAR:", StringComparison.Ordinal)
                    && option.ObjectIds?.Contains("GLASS") == true);
                await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [replacement.Id]));
            }
            Assert.Contains("GLASS", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects["SOURCE"].IsExhausted);
            Assert.Empty(Births(game.State));
        }
        else
        {
            Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(generation, Assert.Single(Births(game.State)).Source.Generation);
        }
        Assert.Empty(game.State.DeathLedger.Counts);
        if (game.State.TriggerQueue.Count > 0) await Order(game);
        for (var step = 0; game.State.StackItems.Count > 0; step++)
        {
            Assert.InRange(step, 0, 3); await game.Top();
            if (game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } location)
                await game.Act(location.Request.PlayerId, new PayCostCommand(location.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "随从");
        Assert.Equal(hand + (replaced ? 0 : 1), game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task AnActualResourceActionDestroysTheAnimatedEquipmentAndKeepsItsPowerAndDraw()
    {
        var game = new Driver(Put(WithOrianna(Position()), "MALZ", P4ActivatedAbilityCatalog.MalzaharCardNo, "P1", "BASE"));
        await Deploy(game); await Animate(game);
        var pool = game.State.RunePools["P1"];
        var generation = game.State.CardObjects["SOURCE"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["SOURCE"]));
        Assert.True(game.State.CardObjects["MALZ"].IsExhausted);
        Assert.Equal(pool.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower + 2, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(generation, Assert.Single(Births(game.State)).Source.Generation);
        await ResolveDraws(game, 1); await game.Replay();
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
    public async Task StagedAnimatedGoldKeepsItsBorrowedDrawAfterActualCopyAndDestruction()
    {
        // Established unit characteristics are explicit fixture setup. The copy,
        // destruction, captured origin and recovery use the actual command history.
        var initial = Put(Put(Put(Put(Create(), "SOURCE", P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, "P1", "BASE"),
            "COPY-SOURCE", "OGN·096/298", "P1", "BASE"), "SPECS", "VEN·137", "P1", "BASE"),
            "FINISH", "OGN·229/298", "P1", "HAND");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, out var factory));
        var gold = factory.CreateObject("SOURCE", "P1", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [gold.ObjectId] = gold with { Tags = gold.Tags.Append(CardObjectTags.UnitCard).Order(StringComparer.Ordinal).ToArray() } } };
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SPECS", gold.ObjectId, ["ASSEMBLE_YELLOW"])); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("OGN·096/298", game.State.CardObjects[gold.ObjectId].CardNo);
        await game.Act("P1", new PlayCardCommand("FINISH", "OGN·229/298", [gold.ObjectId])); await game.Top();
        Assert.DoesNotContain(gold.ObjectId, game.State.CardObjects.Keys);
        var birth = Assert.Single(Births(game.State));
        Assert.Equal(gold.TokenFactoryCardNo, birth.SourceTokenFactoryCardNo);
        Assert.Equal("SPECS", Assert.IsType<RuleTextInstanceBinding>(birth.Origin).Equipment.ObjectId);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        foreach (var invalidFactory in new[] { null, "UNKNOWN-FACTORY", P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(WithBirth(game.State,
                birth with { SourceTokenFactoryCardNo = invalidFactory })), e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
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
        return OriannaEquipmentConsumerFixture.WithResources(state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: "RAD·145/167", ownerId: "P1", controllerId: "P1", tags: ["传奇"]) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } });
    }
    private static Task Animate(Driver game)
        => OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "SOURCE");
}
