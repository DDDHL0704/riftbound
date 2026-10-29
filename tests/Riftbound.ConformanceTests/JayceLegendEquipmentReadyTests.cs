using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class JayceLegendEquipmentReadyTests
{
    private const string One = LegendActionAbilityCatalog.JayceLegendReadyOneAbilityId;
    private const string Two = LegendActionAbilityCatalog.JayceLegendReadyTwoAbilityId;
    private static MatchState Position(string cardNo = "VEN·149", bool empowered = false)
    {
        var state = LegendAttachmentTests.PositionFor(cardNo);
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, "P2-GEAR-JAYCE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = state.CardObjects["LEGEND"] with { IsEmpowered = empowered },
                ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = true },
                ["P2-GEAR-JAYCE"] = new("P2-GEAR-JAYCE", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2", isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["P2-GEAR-JAYCE"] = new("P2", "BASE") } };
    }
    private static LegendActCommand ActOn(string ability, params string[] targets) => new("LEGEND", ability, targets, ["SPEND_MANA:1"]);

    [Theory]
    [InlineData("VEN·149", false)] [InlineData("VEN·194", false)] [InlineData("VEN·194*", false)]
    [InlineData("VEN·149", true)] [InlineData("VEN·194", true)] [InlineData("VEN·194*", true)]
    public async Task PhysicalPrintsPayOneAndReadyOneOrTwoTargetsAfterResponses(string cardNo, bool empowered)
    {
        var state = Position(cardNo, empowered);
        var game = new VenBasicFlowSpellTests.Driver(state);
        var targets = empowered ? new[] { "GEAR", "P2-GEAR-JAYCE" } : new[] { "GEAR" };
        var mana = state.RunePools["P1"].Mana;
        await game.Act("P1", ActOn(empowered ? Two : One, targets));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(empowered, game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.All(targets, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendEquipmentReady);
        await game.Top();
        Assert.All(targets, id => Assert.False(game.State.CardObjects[id].IsExhausted));
        if (!empowered) Assert.True(game.State.CardObjects["P2-GEAR-JAYCE"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·149")] [InlineData("VEN·194")] [InlineData("VEN·194*")]
    public async Task PrintedEmpowerPaysTwoManaAndTwoPowerAndAddsTheSecondAbility(string cardNo)
    {
        var state = Position(cardNo);
        var before = state.RunePools["P1"];
        var activated = await Act(state, "P1", new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", []));
        Assert.Equal(before.Mana - 2, activated.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 2, activated.State.RunePools["P1"].TotalPower);
        Assert.False(activated.State.CardObjects["LEGEND"].IsEmpowered);
        var done = await Top(activated.State);
        Assert.True(done.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);
        var prompt = ResolutionResult.BuildPrompts(done.State)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.LegendAct);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        Assert.Contains(requirements, row => Equals(row["abilityId"], One));
        Assert.Contains(requirements, row => Equals(row["abilityId"], Two));
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task EmpowermentAddsTheTwoTargetSkillWithoutRemovingTheOriginalSkill()
    {
        var state = Position(empowered: true);
        var activated = await Act(state, "P1", ActOn(One, "GEAR"));
        var done = await Top(activated.State);
        Assert.False(done.State.CardObjects["GEAR"].IsExhausted);
        Assert.True(done.State.CardObjects["P2-GEAR-JAYCE"].IsExhausted);
        Assert.True(done.State.CardObjects["LEGEND"].IsEmpowered);
    }

    [Theory]
    [InlineData("not-empowered")]
    [InlineData("duplicate")]
    [InlineData("one-target")]
    [InlineData("unit")]
    [InlineData("exhausted")]
    [InlineData("mana")]
    [InlineData("extra-cost")]
    [InlineData("wrong-player")]
    public async Task IllegalAbilityNeverPaysOrChangesState(string scenario)
    {
        var state = Position(empowered: scenario != "not-empowered");
        if (scenario == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = true } } };
        if (scenario == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        var targets = scenario switch { "duplicate" => new[] { "GEAR", "GEAR" }, "one-target" => new[] { "GEAR" },
            "unit" => new[] { "GEAR", "HOST" }, _ => new[] { "GEAR", "P2-GEAR-JAYCE" } };
        var command = new LegendActCommand("LEGEND", Two, targets, scenario == "extra-cost" ? ["SPEND_MANA:1", "EXTRA"] : ["SPEND_MANA:1"]);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-jayce", scenario == "wrong-player" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public void TheTwoTargetAbilityIsNotOfferedWhenOnlyOneEquipmentExists()
    {
        var state = Position(empowered: true);
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P2"] = state.PlayerZones["P2"] with { Base = state.PlayerZones["P2"].Base.Where(id => id != "P2-GEAR-JAYCE").ToArray() } },
            CardObjects = state.CardObjects.Where(pair => pair.Key != "P2-GEAR-JAYCE").ToDictionary(pair => pair.Key, pair => pair.Value),
            ObjectLocations = state.ObjectLocations.Where(pair => pair.Key != "P2-GEAR-JAYCE").ToDictionary(pair => pair.Key, pair => pair.Value) };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.LegendAct);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        Assert.Contains(requirements, row => Equals(row["abilityId"], One));
        Assert.DoesNotContain(requirements, row => Equals(row["abilityId"], Two));
    }

    [Fact]
    public async Task EnemyEquipmentSpellshieldIsPaidForTheConfirmedTargets()
    {
        var state = Position(empowered: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P2-GEAR-JAYCE"] = state.CardObjects["P2-GEAR-JAYCE"] with { Tags = [..state.CardObjects["P2-GEAR-JAYCE"].Tags, "法盾2"] } } };
        var before = state.RunePools["P1"];
        var activated = await Act(state, "P1", ActOn(Two, "GEAR", "P2-GEAR-JAYCE"));
        Assert.Equal(before.Mana - 1, activated.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 2, activated.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(activated.State.StackItems).LegendEquipmentReady!.SpellshieldTaxPower);
        var done = await Top(activated.State);
        Assert.False(done.State.CardObjects["GEAR"].IsExhausted);
        Assert.False(done.State.CardObjects["P2-GEAR-JAYCE"].IsExhausted);
    }

    [Fact]
    public async Task InsufficientSpellshieldPowerDoesNotSpendManaOrExhaustTheLegend()
    {
        var state = Position(empowered: true);
        state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["P2-GEAR-JAYCE"] = state.CardObjects["P2-GEAR-JAYCE"] with { Tags = [..state.CardObjects["P2-GEAR-JAYCE"].Tags, "法盾2"] } } };
        var cmd = ActOn(Two, "GEAR", "P2-GEAR-JAYCE");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("ward-insufficient", "P1", cmd.CmdType), cmd, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task OneDepartingTargetDoesNotPreventTheOtherEquipmentFromReadying()
    {
        var state = Position(empowered: true);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var gold));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P2-GEAR-JAYCE"] = gold.CreateObject("P2-GEAR-JAYCE", "P2", "P2") with { IsExhausted = false } } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", ActOn(Two, "GEAR", "P2-GEAR-JAYCE"));
        var captured = Assert.Single(game.State.StackItems);
        Assert.Equal("UNL·T05", captured.LegendEquipmentReady!.TargetTokenFactories!["P2-GEAR-JAYCE"]);
        var missingWitness = captured with { LegendEquipmentReady = captured.LegendEquipmentReady with { TargetTokenFactories = null } };
        Assert.Contains("invalid captured legend equipment effect", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [missingWitness] }));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new ActivateAbilityCommand("P2-GEAR-JAYCE", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        Assert.False(game.State.CardObjects.ContainsKey("P2-GEAR-JAYCE"));
        var item = Assert.Single(game.State.StackItems);
        var forged = item with { LegendEquipmentReady = item.LegendEquipmentReady! with { TargetTokenFactories = null } };
        Assert.Contains("invalid captured legend equipment effect", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        Assert.Equal("P2", game.State.PriorityPlayerId);
        Assert.Contains("P1", game.State.PassedPriorityPlayerIds);
        await game.Act("P2", new PassPriorityCommand());
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("context")]
    [InlineData("ability")]
    [InlineData("empowered")]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    public async Task RecoveryRejectsForgedEquipmentAbility(string change)
    {
        var state = (await Act(Position(empowered: true), "P1", ActOn(Two, "GEAR", "P2-GEAR-JAYCE"))).State;
        var item = Assert.Single(state.StackItems);
        var context = item.LegendEquipmentReady!;
        var forged = item with { LegendEquipmentReady = change switch {
            "context" => null, "ability" => context with { AbilityId = "FORGED" },
            "empowered" => context with { SourceWasEmpowered = false },
            "source-generation" => context with { SourceGeneration = context.SourceGeneration + 1 }, _ => context },
            TargetGenerations = change == "target-generation" ? new Dictionary<string, long> { ["GEAR"] = 999, ["P2-GEAR-JAYCE"] = 0 } : item.TargetGenerations };
        Assert.Contains("invalid captured legend equipment effect", OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [forged] }));
    }

    [Theory]
    [InlineData("VEN·149")] [InlineData("VEN·194")] [InlineData("VEN·194*")]
    public async Task PrintedLegendCanOpenAnActualChinaStandardJayceDeck(string legendNo)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(card, format)).ToArray() };
        string Rune(string color) => allowed.Cards.Where(card => card.CardCategory == "rune" && card.CardColorList.Contains(color))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            legendNo, "SFD·084/221", Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("blue"), 6).Concat(Enumerable.Repeat(Rune("orange"), 6)).ToArray()])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("jayce-legend-" + legendNo,
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
            Assert.Contains(opened.State.PlayerZones[player].ChampionZone, id => opened.State.CardObjects[id].CardNo == "SFD·084/221");
        }
    }
}
