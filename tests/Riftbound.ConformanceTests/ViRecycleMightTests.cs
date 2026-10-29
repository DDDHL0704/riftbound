using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ViRecycleMightTests
{
    private const string Ability = "RECYCLE-MIGHT:0";
    private const string Cost = "RECYCLE_GRAVEYARD_CARD:";
    private static MatchState Position(string no, bool copied = false, bool battlefield = false)
    {
        var state = ArmamentMightTests.Position(unit: no, power: 3, gear: copied ? "SFD·059/221" : "SFD·022/221", attached: copied);
        if (battlefield) {
            state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["F"] = state.CardObjects["F"] with { ControllerId = "P1" } } };
        }
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var (id, owner) in new[] { ("C1", "P1"), ("C2", "P1"), ("ENEMY_COST", "P2") }) {
            cards[id] = new(id, cardNo: "OGN·048/298", ownerId: owner, controllerId: owner, tags: [CardObjectTags.SpellCard]);
            locations[id] = new(owner, "GRAVEYARD");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Graveyard = ["C1", "C2"] },
                ["P2"] = state.PlayerZones["P2"] with { Graveyard = ["ENEMY_COST"] } } };
    }
    private static ActivateAbilityCommand Activate(string id = "C1") => new("HOST", Ability, [], [Cost + id]);

    [Theory]
    [InlineData("OGN·036/298")]
    [InlineData("ARC-001/006")]
    [InlineData("VEN·167")]
    public async Task EveryPrintingPaysTheRecycleCostBeforeResponsesAndMayRepeat(string no)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal("铲除者", card.SubTitle); Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(3, card.Power);
        Assert.Single(ActivatedAbilityParser.Parse(card.CardEffect), a => a.Kind == ActivatedAbilityKinds.RecycleGraveyardCardGrantSelfMight);
        var initial = Position(no); var game = new VenBasicFlowSpellTests.Driver(initial);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == Ability);
        Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
        Assert.Equal(new[] { Cost + "C1", Cost + "C2" },
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]).Select(c => c.Id).Order());
        var beforeDeck = initial.PlayerZones["P1"].MainDeck.ToArray();
        await game.Act("P1", Activate());
        Assert.DoesNotContain("C1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(beforeDeck.Concat(["C1"]), game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(3, ArmamentMightTests.Might(game.State));
        Assert.NotNull(Assert.Single(game.State.StackItems).RecycleMightActivation);
        await game.Top(); Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Act("P1", Activate("C2")); await game.Top(); Assert.Equal(5, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(3, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    public static IEnumerable<object[]> Illegal() => new[] { "OGN·036/298", "ARC-001/006", "VEN·167" }
        .SelectMany(no => new[] { "missing", "duplicate", "enemy", "unknown", "target", "wrong-player", "empty", "rune" }
            .Select(scenario => new object[] { no, scenario }));
    [Theory]
    [MemberData(nameof(Illegal))]
    public async Task IllegalCostTargetOrPlayerCannotChangeState(string no, string scenario)
    {
        var state = Position(no); var command = Activate();
        if (scenario == "missing") command = command with { OptionalCosts = [] };
        if (scenario == "duplicate") command = command with { OptionalCosts = [Cost + "C1", Cost + "C1"] };
        if (scenario == "enemy") command = Activate("ENEMY_COST");
        if (scenario == "unknown") command = Activate("MISSING");
        if (scenario == "target") command = command with { TargetObjectIds = ["HOST"] };
        if (scenario == "empty") state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Graveyard = [] } } };
        if (scenario == "rune") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["C1"] = state.CardObjects["C1"] with { Tags = [CardObjectTags.RuneCard] } } };
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-" + scenario,
            scenario == "wrong-player" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("OGN·036/298")]
    [InlineData("ARC-001/006")]
    [InlineData("VEN·167")]
    public async Task ExhaustedUnitCanStillPayTheCostAndPowerChangesOnlyAfterResolution(string no)
    {
        var state = Position(no); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true } } };
        var game = new VenBasicFlowSpellTests.Driver(state); await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); Assert.Equal(3, ArmamentMightTests.Might(game.State));
        await game.Top(); Assert.Equal(4, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task AProtectiveCounterCannotChooseThisUntargetedSkill()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·167"), "COUNTER", "SFD·045/221", "P2");
        var game = new VenBasicFlowSpellTests.Driver(initial); await game.Act("P1", Activate());
        var id = Assert.Single(game.State.StackItems).StackItemId; await game.Act("P1", new PassPriorityCommand());
        var before = game.State; var counter = new PlayCardCommand("COUNTER", "SFD·045/221", [id]);
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("no-protected-target", "P2", counter.CmdType), counter, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        await game.Act("P2", new PassPriorityCommand()); Assert.Equal(4, ArmamentMightTests.Might(game.State));
        Assert.DoesNotContain("C1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("C1", game.State.PlayerZones["P1"].MainDeck.Last()); Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task PayingTheCostTriggersASeparateRecycleObserverBeforeTheSkillResolves()
    {
        var initial = RecycledCardBoonTests.AddKarma(Position("VEN·167"), "OGN·235/298");
        var game = new VenBasicFlowSpellTests.Driver(initial); await game.Act("P1", Activate());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(3, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Top(); Assert.Equal(5, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task SourceReturningToHandDoesNotRefundCostOrExposeItsNewObject()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·167", battlefield: true), "RETURN", "OGN·169/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(initial); await game.Act("P1", Activate());
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("HOST", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var priorEvents = game.Events().Length; await game.Top();
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(priorEvents)));
        Assert.Equal(3, game.State.CardObjects["HOST"].Power); Assert.Equal("C1", game.State.PlayerZones["P1"].MainDeck.Last());
        await game.Replay();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("cost")]
    [InlineData("index")]
    [InlineData("card-no")]
    public async Task RecoveryRejectsForgedCostOrCapturedAbility(string mutation)
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("VEN·167")); await game.Act("P1", Activate());
        var item = Assert.Single(game.State.StackItems); var context = item.RecycleMightActivation!;
        var forged = item with { RecycleMightActivation = mutation switch {
            "source" => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } },
            "cost" => context with { RecycledCard = new("NOT_A_CARD", 0) },
            "card-no" => context with { CardNo = null! },
            _ => context with { AbilityIndex = 99 } } };
        Assert.Contains("invalid captured recycle-cost ability", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
    }

    [Fact]
    public async Task CopiedPrintedTextCanBeActivatedIndependentlyWithTheSameRequiredCost()
    {
        var game = new VenBasicFlowSpellTests.Driver(Position("VEN·167", copied: true));
        var command = Activate() with { AbilityInstanceId = "ATTACHED:GEAR:0:1:" + Ability };
        await game.Act("P1", command); var item = Assert.Single(game.State.StackItems);
        Assert.NotNull(item.RecycleMightActivation!.Origin); await game.Top(); Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task NewPrintingPaysRedPowerAndCanOpenAChinaStandardDeck()
    {
        const string no = "VEN·167"; const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var bad = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var rejected = await new CoreRuleEngine().ResolveAsync(bad, new("wrong-power", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(bad), MatchStateHasher.Hash(rejected.State));
        var game = new VenBasicFlowSpellTests.Driver(state); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Contains("游走", game.State.CardObjects["UNIT"].Tags);
        await game.Replay();
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "UNL-187/219", no, Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("red"), 6).Concat(Enumerable.Repeat(Rune("yellow"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("vi-print-deck", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance, NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(false, format));
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); var raw = JsonSerializer.SerializeToElement(submit, options);
        foreach (var player in new[] { "P1", "P2" }) Assert.True((await session.SubmitDeckAsync(player, "deck-" + player, submit, raw, default)).Accepted);
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), options);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage); Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
    }
}
