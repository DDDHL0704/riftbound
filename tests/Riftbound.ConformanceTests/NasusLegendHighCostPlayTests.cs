using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class NasusLegendHighCostPlayTests
{
    private static MatchState Position(string legendNo = "VEN·145", string unitNo = "OGN·088/298", string legendPlayer = "P1")
    {
        var state = VenUnitEntryBasicTests.Position(unitNo);
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        cards["LEGEND"] = new("LEGEND", cardNo: legendNo, ownerId: legendPlayer, controllerId: legendPlayer, tags: ["传奇"]);
        zones[legendPlayer] = zones[legendPlayer] with { LegendZone = ["LEGEND"] };
        locations["LEGEND"] = new(legendPlayer, "LEGEND");
        foreach (var player in new[] { "P1", "P2" })
        {
            var id = player + "-RUNE";
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R02", player) with { IsExhausted = true };
            zones[player] = zones[player] with { Base = [..zones[player].Base, id] };
            locations[id] = new(player, "BASE");
        }
        return state with { RoomId = "NASUS-HIGH-COST", CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    private static async Task Choose(Driver game, params string[] targets)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
    }
    private static async Task Pay(Driver game, string answer = "PAY")
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [answer]));
    }

    [Theory]
    [InlineData("VEN·145", 37848)] [InlineData("VEN·192", 37898)] [InlineData("VEN·192*", 37899)]
    public async Task EachPrintingObservesARealSevenManaUnitAndReadiesEitherPlayersRunesAfterResponses(string legend, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == legend);
        Assert.Equal(id, card.Id); Assert.Equal("沙漠死神", card.CardName);
        Assert.Contains("当你打出一个法力费用不低于{{7}}的单位、装备或主动技能时", card.CardEffect);
        var game = new Driver(Position(legend));
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        Assert.Contains(game.State.StackItems, i => i.HighCostPlayObserver is not null);
        Assert.Equal(new[] { "P1-RUNE", "P2-RUNE" }, game.State.PendingCardChoice!.LegalObjectIds);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await Choose(game, "P1-RUNE", "P2-RUNE");
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await Pay(game);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.True(game.State.CardObjects["P1-RUNE"].IsExhausted);
        Assert.True(game.State.CardObjects["P2-RUNE"].IsExhausted);
        await game.Top();
        Assert.False(game.State.CardObjects["P1-RUNE"].IsExhausted);
        Assert.False(game.State.CardObjects["P2-RUNE"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·088/298", true)] [InlineData("SFD·168/221", true)] [InlineData("OGS·005/024", false)]
    public async Task UnitAndEquipmentUsePrintedManaAtTheSevenBoundary(string cardNo, bool qualifies)
    {
        var game = new Driver(Position(unitNo: cardNo));
        await game.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Equal(qualifies, game.State.StackItems.Any(i => i.HighCostPlayObserver is not null));
        if (qualifies) { await Choose(game, "P1-RUNE"); await Pay(game); await game.Top(); Assert.False(game.State.CardObjects["P1-RUNE"].IsExhausted); }
        await game.Replay();
    }

    [Fact]
    public async Task SevenManaEmpowerIsObservedOnlyAfterItsResolution()
    {
        var state = Ven103AnyGraveyardReturnConsumerTests.At(Position(unitNo: "VEN·043"), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, i => i.HighCostPlayObserver is not null);
        await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.NotNull(game.State.PendingCardChoice);
        var observer = Assert.Single(game.State.StackItems, i => i.HighCostPlayObserver is not null);
        Assert.Equal(7, observer.HighCostPlayObserver!.SkillPlay!.BaseMana);
        await Choose(game, "P1-RUNE"); await Pay(game);
        await game.Top(); Assert.False(game.State.CardObjects["P1-RUNE"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ASourceReturnedToHandInResponseDoesNotPreventItsIndependentSkillFromResolving()
    {
        var state = Ven103AnyGraveyardReturnConsumerTests.At(Position(unitNo: "VEN·043"), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Purple] = 4 }) } };
        state = AttackDamageDistributionTests.Spell(state, "BOUNCE", "UNL-128/219", "P2");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BOUNCE", "UNL-128/219", ["ENEMY", "UNIT"]));
        await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        Assert.Contains(game.State.StackItems, i => i.HighCostPlayObserver is not null);
        await Choose(game, "P1-RUNE"); await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects["P1-RUNE"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData(6)] [InlineData(12)]
    public async Task ReducedEmpowerStillQualifiesFromItsTwelveManaBaseCost(int controlledRunes)
    {
        var state = Ven103AnyGraveyardReturnConsumerTests.At(Position(unitNo: "VEN·050"), "UNIT", "P1", "BASE");
        var zones = state.PlayerZones.ToDictionary(); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        zones["P1"] = zones["P1"] with { Hand = zones["P1"].Hand.Where(id => id != "UNIT").ToArray() };
        for (var i = 1; i < controlledRunes; i++)
        {
            var id = "EXTRA-RUNE-" + i;
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R02", "P1") with { IsExhausted = true };
            locations[id] = new("P1", "BASE"); zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] };
        }
        state = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(12 - controlledRunes, 20) } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.Null(game.State.PendingCardChoice); await game.Top();
        var witness = Assert.Single(game.State.StackItems, i => i.HighCostPlayObserver is not null).HighCostPlayObserver!.SkillPlay!;
        Assert.Equal(12, witness.BaseMana); Assert.Equal(12 - controlledRunes, witness.Empower!.PaidMana);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await Choose(game, "P2-RUNE"); await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects["P2-RUNE"].IsExhausted);
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
    }

    [Fact]
    public async Task AReducedFiveManaEmpowerDoesNotQualify()
    {
        var state = Ven103AnyGraveyardReturnConsumerTests.At(Position(unitNo: "VEN·001"), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, i => i.HighCostPlayObserver is not null);
        await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
    }

    [Fact]
    public async Task ASevenManaUnitStillQualifiesWhenMightyUnitsReduceItsActualCost()
    {
        var state = Position(unitNo: "SFD·103/221");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { Power = 5 } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(5, 20) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "SFD·103/221", [], Destination: "BASE"));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.NotNull(Assert.Single(game.State.StackItems, i => i.HighCostPlayObserver is not null).HighCostPlayObserver!.CardPlay);
        await Choose(game, "P1-RUNE"); await Pay(game); await game.Top();
        Assert.False(game.State.CardObjects["P1-RUNE"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ASevenManaSpellAndTheOpponentsUnitDoNotQualify()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "SPELL", "UNL-066/219", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "UNL-066/219", ["ALLY"])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        var other = new Driver(Position(legendPlayer: "P2"));
        await other.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        Assert.Empty(other.State.StackItems); Assert.Null(other.State.PendingCardChoice);
        await game.Replay(); await other.Replay();
    }

    [Fact]
    public async Task AlreadyExhaustedSourceCannotPayForItsTrigger()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.True(game.State.CardObjects["P1-RUNE"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task WardOnAnEnemyRuneMustBePaidWithTheExhaustionCostOrDeclined()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P2-RUNE"] = state.CardObjects["P2-RUNE"] with { Tags = [..state.CardObjects["P2-RUNE"].Tags, "法盾2"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        await Choose(game, "P2-RUNE");
        var before = game.State; var pending = before.PendingPayment!;
        Assert.Equal(2, pending.PowerCost);
        var command = new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("unpaid", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await Pay(game, "DECLINE");
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted); Assert.True(game.State.CardObjects["P2-RUNE"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task PayingWardAndExhaustionConfirmsTheEnemyRuneTarget()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P2-RUNE"] = state.CardObjects["P2-RUNE"] with { Tags = [..state.CardObjects["P2-RUNE"].Tags, "法盾2"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 2) } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        await Choose(game, "P2-RUNE"); Assert.Equal(2, game.State.PendingPayment!.PowerCost);
        await Pay(game); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Top(); Assert.False(game.State.CardObjects["P2-RUNE"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task WithNoRunesOnTheFieldThePlayerCanStillPayForTheZeroTargetSkill()
    {
        var state = Position(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var player in new[] { "P1", "P2" })
        {
            var id = player + "-RUNE";
            zones[player] = zones[player] with { Base = zones[player].Base.Where(x => x != id).ToArray(), RuneDeck = [id] };
            locations[id] = new(player, "RUNE_DECK");
        }
        var game = new Driver(state with { PlayerZones = zones, ObjectLocations = locations });
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        Assert.Null(game.State.PendingCardChoice); Assert.NotNull(game.State.PendingPayment);
        await Pay(game); await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task TargetRecycledInResponseIsNotReadiedOrExposedFromTheRuneDeck()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        await Choose(game, "P1-RUNE", "P2-RUNE"); await Pay(game);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new RecycleRuneCommand("P2-RUNE"));
        await game.Act("P2", new PassPriorityCommand());
        Assert.Contains("P2-RUNE", game.State.PlayerZones["P2"].RuneDeck);
        Assert.False(game.State.CardObjects["P1-RUNE"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNE_READIED"
            && e.Event.Payload.TryGetValue("objectIds", out var ids) && ids is string[] array && array.Contains("P2-RUNE"));
        await game.Replay();
    }

    [Theory]
    [InlineData("card")] [InlineData("controller")] [InlineData("play")] [InlineData("source")]
    [InlineData("missing-card")]
    public async Task RecoveryRejectsForgedTriggerEvidence(string scenario)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        var state = game.State; var item = Assert.Single(state.StackItems); var context = item.HighCostPlayObserver!;
        var forged = scenario switch { "card" => context with { CardNo = "VEN·143" },
            "controller" => context with { ControllerId = "P2" },
            "missing-card" => context with { CardNo = null! },
            "play" => context with { CardPlay = context.CardPlay! with { CardNo = "OGS·005/024" } },
            _ => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } };
        Assert.Contains("invalid high-cost play observer stack", OfficialInsightAndSpellLockTests.Errors(state with {
            StackItems = [item with { HighCostPlayObserver = forged }] }));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ZeroTargetsCanPayOrDeclineWithoutConfusingTheTwo(bool pay)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        await Choose(game);
        await Pay(game, pay ? "PAY" : "DECLINE");
        Assert.Equal(pay, game.State.CardObjects["LEGEND"].IsExhausted);
        if (pay) await game.Top();
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("three")] [InlineData("unit")] [InlineData("wrong-player")]
    public async Task IllegalTargetChoicesDoNotChangeState(string scenario)
    {
        var initial = Position();
        if (scenario == "three") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["RUNE-3"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE-3", "UNL-R02", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RUNE-3"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "RUNE-3"] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        var state = game.State; var choice = state.PendingCardChoice!;
        var targets = scenario switch { "duplicate" => new[] { "P1-RUNE", "P1-RUNE" },
            "three" => new[] { "P1-RUNE", "P2-RUNE", "RUNE-3" }, "unit" => new[] { "ALLY" }, _ => new[] { "P1-RUNE" } };
        var cmd = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets);
        var rejected = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid", scenario == "wrong-player" ? "P2" : "P1", cmd.CmdType), cmd, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }
}
