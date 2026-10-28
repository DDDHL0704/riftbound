using System.Text.Json;
using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class LegendEmpowerSpentEffectTests
{
    private static string Ability(string cardNo) => cardNo is "VEN·151" or "VEN·195" or "VEN·195*"
        ? LegendActionAbilityCatalog.MelLegendAbilityId : LegendActionAbilityCatalog.AmbessaLegendAbilityId;

    private static MatchState Position(string cardNo)
    {
        var state = LegendAttachmentTests.PositionFor(cardNo);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsEmpowered = true },
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true } } };
    }

    private static MatchState EnemyPosition(string cardNo)
    {
        var state = VenFlowCombatKeywordTests.Position(false);
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new CardObjectState("LEGEND", cardNo: cardNo, ownerId: "P1", controllerId: "P1", tags: ["传奇"]) with { IsEmpowered = true },
                ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, "法盾2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
    }

    [Theory]
    [InlineData("VEN·151", true)]
    [InlineData("VEN·195", true)]
    [InlineData("VEN·195*", true)]
    [InlineData("VEN·153", false)]
    [InlineData("VEN·196", false)]
    [InlineData("VEN·196*", false)]
    public async Task PrintedAbilityPaysCostsBeforeResponsesAndResolvesOnTheUnit(string cardNo, bool reduce)
    {
        var state = Position(cardNo);
        var before = state.CardObjects["HOST"].Power;
        var pool = state.RunePools["P1"].TotalPower;
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new LegendActCommand("LEGEND", Ability(cardNo), ["HOST"], []));
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(pool - (reduce ? 0 : 1), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(before, game.State.CardObjects["HOST"].Power);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendUnitEffect);
        await game.Top();
        if (reduce) Assert.Equal(before - 2, game.State.CardObjects["HOST"].Power);
        else Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·151", "unempowered")]
    [InlineData("VEN·153", "unempowered")]
    [InlineData("VEN·151", "exhausted")]
    [InlineData("VEN·153", "exhausted")]
    [InlineData("VEN·151", "base")]
    [InlineData("VEN·153", "power")]
    [InlineData("VEN·151", "duplicate")]
    [InlineData("VEN·153", "missing")]
    [InlineData("VEN·151", "equipment")]
    [InlineData("VEN·151", "timing")]
    [InlineData("VEN·153", "wrong-player")]
    public async Task IllegalActivationDoesNotPayOrChangeState(string cardNo, string scenario)
    {
        var state = Position(cardNo);
        if (scenario == "base") state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        if (scenario == "unempowered") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsEmpowered = false } } };
        if (scenario == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        if (scenario == "power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0) } };
        if (scenario == "timing") state = state with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1" };
        var targets = scenario switch { "duplicate" => new[] { "HOST", "HOST" }, "missing" => [], "equipment" => new[] { "GEAR" }, _ => new[] { "HOST" } };
        var cmd = new LegendActCommand("LEGEND", Ability(cardNo), targets, []);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid", scenario == "wrong-player" ? "P2" : "P1", cmd.CmdType), cmd, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("VEN·151", 0)]
    [InlineData("VEN·153", 1)]
    public async Task EnemySpellshieldIsPaidOnTopOfThePrintedCost(string cardNo, int printedCost)
    {
        var state = EnemyPosition(cardNo);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.LegendAct);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, row => Equals(row["abilityId"], Ability(cardNo)));
        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["spellshieldTaxPowerByTargetObjectId"])["ENEMY"]);
        var cmd = new LegendActCommand("LEGEND", Ability(cardNo), ["ENEMY"], []);
        var before = state.RunePools["P1"].TotalPower;
        var activated = await Act(state, "P1", cmd);
        Assert.Equal(before - printedCost - 2, activated.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(activated.State.StackItems).LegendUnitEffect!.SpellshieldTaxPower);
        var done = await Top(activated.State);
        if (printedCost == 1) Assert.False(done.State.CardObjects["ENEMY"].IsExhausted);
        else Assert.Equal(state.CardObjects["ENEMY"].Power - 2, done.State.CardObjects["ENEMY"].Power);
    }

    [Theory]
    [InlineData("VEN·151", 1)]
    [InlineData("VEN·153", 2)]
    public async Task InsufficientPrintedCostPlusSpellshieldRejectsBeforeAnyCost(string cardNo, int availablePower)
    {
        var state = EnemyPosition(cardNo);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, availablePower) } };
        var cmd = new LegendActCommand("LEGEND", Ability(cardNo), ["ENEMY"], []);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("insufficient", "P1", cmd.CmdType), cmd, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ReadyInstructionRespectsAUnitsCannotBecomeActiveText()
    {
        var state = Position("VEN·153");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { CardNo = "UNL-144/219" } } };
        var activated = await Act(state, "P1", new LegendActCommand("LEGEND", Ability("VEN·153"), ["HOST"], []));
        var done = await Top(activated.State);
        Assert.True(done.State.CardObjects["HOST"].IsExhausted);
        Assert.False(done.State.CardObjects["LEGEND"].IsEmpowered);
    }

    [Fact]
    public async Task MelTargetMovedToBaseInResponseIsNoLongerEligible()
    {
        var state = EquipChainTests.Response(EnemyPosition("VEN·151"), "OGS·011/024", "P2");
        var activated = await Act(state, "P1", new LegendActCommand("LEGEND", Ability("VEN·151"), ["ENEMY"], []));
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGS·011/024", ["ENEMY"]));
        var moved = await Top(response.State);
        Assert.Equal("BASE", moved.State.ObjectLocations["ENEMY"].Zone);
        var done = await Top(moved.State);
        Assert.Equal(state.CardObjects["ENEMY"].Power, done.State.CardObjects["ENEMY"].Power);
        Assert.Contains(done.Events, e => e.Kind == "ABILITY_NO_EFFECT");
    }

    [Fact]
    public async Task MelPowerModifierExpiresAtTurnEnd()
    {
        var state = Position("VEN·151");
        var activated = await Act(state, "P1", new LegendActCommand("LEGEND", Ability("VEN·151"), ["HOST"], []));
        var done = await Top(activated.State);
        var ended = await Act(done.State, "P1", new EndTurnCommand());
        Assert.Equal(state.CardObjects["HOST"].Power, ended.State.CardObjects["HOST"].Power);
    }

    [Theory]
    [InlineData("context")]
    [InlineData("ability")]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    [InlineData("negative-tax")]
    public async Task RecoveryRejectsForgedTargetedLegendAbility(string change)
    {
        var state = (await Act(Position("VEN·151"), "P1", new LegendActCommand("LEGEND", Ability("VEN·151"), ["HOST"], []))).State;
        var item = Assert.Single(state.StackItems);
        var context = item.LegendUnitEffect!;
        var forged = item with {
            LegendUnitEffect = change switch { "context" => null, "ability" => context with { AbilityId = "FORGED" },
                "source-generation" => context with { SourceGeneration = context.SourceGeneration + 1 },
                "negative-tax" => context with { SpellshieldTaxPower = -1 }, _ => context },
            TargetGenerations = change == "target-generation" ? new Dictionary<string, long> { ["HOST"] = 999 } : item.TargetGenerations };
        Assert.Contains("invalid captured legend unit effect", OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [forged] }));
    }

    [Theory]
    [InlineData("VEN·151")]
    [InlineData("VEN·153")]
    public async Task TargetLeavingInResponseDoesNotApplyEffectOrRefundCosts(string cardNo)
    {
        var state = EquipChainTests.Response(Position(cardNo), "OGN·169/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(state);
        var beforePower = state.RunePools["P1"].TotalPower;
        await game.Act("P1", new LegendActCommand("LEGEND", Ability(cardNo), ["HOST"], []));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top();
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(beforePower - (cardNo == "VEN·153" ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·151")]
    [InlineData("VEN·195")]
    [InlineData("VEN·195*")]
    public async Task MelLegendOpensAnActualChinaStandardDeck(string legendNo)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(card, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(card => card.CardCategory == "rune" && card.CardColorList.Contains(color))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            legendNo, "VEN·069", Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("blue"), 6).Concat(Enumerable.Repeat(Rune("purple"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("mel-legend-" + legendNo,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var player in new[] { "P1", "P2" })
        {
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player, submit, JsonSerializer.SerializeToElement(submit, json), default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        var ready = JsonSerializer.SerializeToElement(new ReadyCommand(), json);
        Assert.True((await session.ReadyAsync("P1", "ready-P1", ready, default)).Accepted);
        var opened = await session.ReadyAsync("P2", "ready-P2", ready, default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        foreach (var player in new[] { "P1", "P2" })
        {
            Assert.Contains(opened.State.PlayerZones[player].LegendZone, id => opened.State.CardObjects[id].CardNo == legendNo);
            Assert.Contains(opened.State.PlayerZones[player].ChampionZone, id => opened.State.CardObjects[id].CardNo == "VEN·069");
        }
    }
}
