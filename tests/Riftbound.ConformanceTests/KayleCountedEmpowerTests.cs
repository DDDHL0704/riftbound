using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class KayleCountedEmpowerTests
{
    private static MatchState Position(string no) => EmpowerTests.Position(no);
    private static JsonElement View(MatchState state) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST");

    [Theory]
    [InlineData("VEN·134")] [InlineData("VEN·185")]
    public async Task CountedEmpowerCrossesMightyOnceAndThePaidReadyWaitsForItsOwnResponses(string no)
    {
        var state = Position(no);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["FIORA"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIORA", "SFD·180/221", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) } };
        state = LocalDestructionRecallTests.At(state, "FIORA", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["HOST"].EmpowermentCount);
        Assert.Equal(5, View(game.State).GetProperty("effectivePower").GetInt32());
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal("MIGHTY_SKILL_COST", payment.PaymentWindow);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_POWER:yellow:1"]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal("HOST", Assert.Single(game.State.StackItems).MightySkill!.Unit.ObjectId);
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        for (var count = 2; count <= 3; count++) {
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            await game.Top();
            Assert.Equal(3 + 2 * count, View(game.State).GetProperty("effectivePower").GetInt32());
            Assert.Empty(game.State.StackItems);
            Assert.Null(game.State.PendingPayment);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·134", 37834)] [InlineData("VEN·185", 37888)]
    public async Task BothOfficialPrintingsHaveThreeDistinctEmpowerStatesAndThreeManaCost(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("正义天降", card.SubTitle);
        Assert.Equal(3, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(3, card.Power);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        var self = EmpowerParser.Self(card.CardEffect);
        Assert.Equal(3, self.MaximumCount); Assert.Equal(2, self.MightPerCount);
        Assert.Equal(3, self.KeywordCount); Assert.Equal(new[] { "法盾3", "游走" }, self.KeywordsAtCount);
    }

    [Theory]
    [InlineData("VEN·134")] [InlineData("VEN·185")]
    public async Task ThreePaidActivationsResolveSeparatelyAndTheFourthCannotPayOrChangeState(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Equal(17, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.CardObjects["HOST"].EmpowermentCount);
        Assert.Equal(3, View(game.State).GetProperty("effectivePower").GetInt32());
        for (var count = 1; count <= 3; count++) {
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            Assert.Equal(17 - 3 * count, game.State.RunePools["P1"].Mana);
            Assert.Equal(count - 1, game.State.CardObjects["HOST"].EmpowermentCount);
            Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
            await game.Top();
            var host = game.State.CardObjects["HOST"];
            Assert.Equal(count, host.EmpowermentCount); Assert.True(host.IsEmpowered);
            Assert.Equal(3, host.Power); Assert.Equal(3 + 2 * count, View(game.State).GetProperty("effectivePower").GetInt32());
            Assert.Equal(count, View(game.State).GetProperty("empowermentCount").GetInt32());
            Assert.Equal(count == 3, host.Tags.Contains("法盾3"));
            Assert.Equal(count == 3, host.Tags.Contains("游走"));
        }
        Assert.Equal(3, game.Events().Count(e => e.Event.Kind == "OBJECT_EMPOWERED"));
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [], candidate =>
            candidate.Action == CommandTypes.ActivateAbility && candidate.Sources!.Any(source => source.Id == "HOST"));
        await Reject(game.State, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(3, game.State.CardObjects["HOST"].EmpowermentCount);
        Assert.Equal(9, View(game.State).GetProperty("effectivePower").GetInt32());
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·134")] [InlineData("VEN·185")]
    public async Task RemovingEmpowerRemovesExactlyOneStateAndTheDelayedInstructionRestoresOne(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no), "REMOVE", "VEN·035", "P1");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        var game = await PlayAndEmpowerThreeTimes(initial, no);
        await game.Act("P1", new PlayCardCommand("REMOVE", "VEN·035", ["HOST"], Mode: "REMOVE_THEN_EMPOWER"));
        Assert.Equal(3, game.State.CardObjects["HOST"].EmpowermentCount);
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["HOST"].EmpowermentCount);
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(7, View(game.State).GetProperty("effectivePower").GetInt32());
        Assert.DoesNotContain("法盾3", game.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain("游走", game.State.CardObjects["HOST"].Tags);
        await game.Act("P1", new EndTurnCommand());
        Assert.Single(game.State.StackItems, item => item.DelayedEmpower is not null);
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["HOST"].EmpowermentCount);
        Assert.Equal(9, View(game.State).GetProperty("effectivePower").GetInt32());
        Assert.Contains("法盾3", game.State.CardObjects["HOST"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·104/298", "HAND")]
    [InlineData("OGN·229/298", "GRAVEYARD")]
    public async Task ReturningOrDestroyingKayleClearsEveryEmpowerState(string spell, string zone)
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·134"), "LEAVE", spell, "P1");
        var game = await PlayAndEmpowerThreeTimes(initial, "VEN·134");
        await game.Act("P1", new PlayCardCommand("LEAVE", spell, ["HOST"])); await game.Top();
        Assert.Equal(zone, game.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(0, game.State.CardObjects["HOST"].EmpowermentCount);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > initial.CardObjects["HOST"].ObjectGeneration);
        await game.Replay();
    }

    [Fact]
    public async Task ASecondCopyOfThePrintedPermissionDoesNotDoubleTheThreeStateLimit()
    {
        var state = ArmamentMightTests.Position("SFD·059/221", "VEN·134", 3, attached: true);
        for (var count = 1; count <= 3; count++) {
            var activated = await Act(state, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            state = (await Top(activated.State)).State;
            Assert.Equal(count, state.CardObjects["HOST"].EmpowermentCount);
            // Both independent copies contribute +2 per state, while each grants a cap of three.
            Assert.Equal(3 + 4 * count, View(state).GetProperty("effectivePower").GetInt32());
            ArmamentMightTests.PublicRecovery(state); Restore(state);
        }
        Assert.Contains("法盾6", state.CardObjects["HOST"].Tags);
        await Reject(state, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
    }

    [Theory]
    [InlineData("mana")] [InlineData("target")] [InlineData("opponent")]
    public async Task InvalidSecondActivationKeepsTheFirstStateAndItsPaidResources(string branch)
    {
        var state = await AtCount("VEN·134", 1);
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 0) } };
        await Reject(state, branch == "opponent" ? "P2" : "P1",
            new ActivateAbilityCommand("HOST", "EMPOWER:0:0", branch == "target" ? ["HOST"] : []));
        Assert.Equal(1, state.CardObjects["HOST"].EmpowermentCount);
    }

    [Theory]
    [InlineData(-1)] [InlineData(int.MaxValue)]
    public async Task RecoveryRejectsInvalidAuthoritativeCounts(int count)
    {
        var state = await AtCount("VEN·134", 2);
        var invalid = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { EmpowermentCount = count } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(invalid), error => error.Contains("empowerment count"));
    }

    [Theory]
    [InlineData("missing")] [InlineData("wrong")] [InlineData("string")]
    public async Task RecoveryRejectsAVisibleCountThatDiffersFromTheAuthoritativeObject(string mutation)
    {
        var state = await AtCount("VEN·134", 2);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var snapshots = ResolutionResult.BuildSnapshots(state).ToDictionary();
        var json = JsonNode.Parse(JsonSerializer.Serialize(snapshots["P1"], options))!;
        var host = json["players"]!["P1"]!["objects"]!["HOST"]!.AsObject();
        if (mutation == "missing") host.Remove("empowermentCount");
        else host["empowermentCount"] = mutation == "wrong" ? JsonValue.Create(3) : JsonValue.Create("2");
        snapshots["P1"] = json.Deserialize<SnapshotDto>(options)!;
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, 0, snapshots[player], state.Tick, 0, prompts[player]));
        Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, null),
            error => error.Contains("empowerment count"));
        ArmamentMightTests.PublicRecovery(state); Restore(state);
    }

    [Theory]
    [InlineData("VEN·134")] [InlineData("VEN·185")]
    public async Task ChinaStandardDeckAcceptsTheHeroPrintingInTheMainDeckAndPreservesItsPrintedPower(string no)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(card, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(card => card.CardCategory == "rune" && card.CardColorList.Contains(color))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            "OGN·253/298", "OGN·027/298", new[] { no, no, no }, new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("red"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("kayle-deck-" + no,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(command, options);
        foreach (var player in new[] { "P1", "P2" }) Assert.True((await session.SubmitDeckAsync(player, "deck-" + player, command, raw, default)).Accepted);
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), options);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        foreach (var player in new[] { "P1", "P2" }) {
            var heroes = opened.State.CardObjects.Values.Where(card => card.OwnerId == player && card.CardNo == no).ToArray();
            Assert.Equal(3, heroes.Length);
            Assert.All(heroes, hero => { Assert.Equal(3, hero.Power); Assert.Equal(0, hero.EmpowermentCount); });
        }
    }

    private static async Task<MatchState> AtCount(string no, int count)
    {
        var state = await EmpowerTests.Enter(no);
        for (var i = 0; i < count; i++) state = (await Top((await EmpowerTests.Activate(state)).State)).State;
        Assert.Equal(count, state.CardObjects["HOST"].EmpowermentCount);
        return state;
    }

    private static async Task<Driver> PlayAndEmpowerThreeTimes(MatchState initial, string no)
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        for (var count = 0; count < 3; count++) {
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            await game.Top();
        }
        return game;
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("kayle-rejected", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
