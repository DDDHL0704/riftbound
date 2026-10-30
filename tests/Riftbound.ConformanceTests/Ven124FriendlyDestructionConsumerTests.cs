using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven124FriendlyDestructionConsumerTests
{
    private const string No = "VEN·124";
    private const string Text = "{{强化}} — 摧毁一名友方单位（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+2。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCn37824NormalProfileRetainsItsPrintedFriendlyDestructionCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37824, card.Id); Assert.Equal("脱逃的灰背", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(3, card.Power);
        Assert.Equal([RuneTrait.Yellow], card.CardColorList); Assert.Equal(["unit"], card.CardCategoryList);
        Assert.Equal("诺克萨斯", card.Region); Assert.Empty(card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(3, behavior.ManaCost); Assert.Equal(3, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal("摧毁一名友方单位", cost.AdditionalCost); Assert.False(cost.SupportedResources);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower); Assert.False(cost.Exhaust);
        Assert.Equal(2, EmpowerParser.Self(card.CardEffect).Might); Assert.Empty(EmpowerParser.Self(card.CardEffect).Keywords);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ActualThreeManaPlayDoesNotPrepayItsFriendlyUnitOrStartEmpowered(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial);
        try
        {
            await Play(game, owner); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.Contains("COST", game.State.PlayerZones[owner].Base); Assert.Contains("SPARE", game.State.PlayerZones[owner].Base);
            Assert.DoesNotContain("COST", game.State.PlayerZones[owner].Graveyard);
            Assert.Equal(3, game.State.CardObjects["UNIT"].Power); Assert.Equal(3, Might(game.State, owner));
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay();
        }
        finally { await Save("normal-play-" + owner, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ARealFriendlyUnitIsDestroyedAsCostBeforeNativeEmpowerResolves(string owner)
    {
        var initial = Position(owner); var game = new Driver(initial);
        try
        {
            await Play(game, owner); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var costGeneration = game.State.CardObjects["COST"].ObjectGeneration;
            var cost = CostChoice(game, owner, "COST"); var spare = CostChoice(game, owner, "SPARE");
            await game.Act(owner, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: [cost.Id]));
            Assert.DoesNotContain("COST", game.State.PlayerZones[owner].Base); Assert.Contains("COST", game.State.PlayerZones[owner].Graveyard);
            Assert.Equal("GRAVEYARD", game.State.ObjectLocations["COST"].Zone);
            Assert.Equal(costGeneration + 1, game.State.CardObjects["COST"].ObjectGeneration);
            Assert.Equal(0, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, Might(game.State, owner));
            var pending = Assert.Single(game.State.StackItems); Assert.Empty(pending.TargetObjectIds);
            Assert.Equal(0, pending.EmpowerActivation!.PaidMana); Assert.Equal(generation, pending.EmpowerActivation.Source.Generation);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(3, game.State.CardObjects["UNIT"].Power); Assert.Equal(5, Might(game.State, owner));
            Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
            var raw = JsonSerializer.SerializeToNode(new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: [spare.Id]), Json)!.AsObject();
            raw["promptId"] = game.PromptFor(owner).PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new(owner, "ven124-repeat-" + owner, JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Contains("SPARE", game.State.PlayerZones[owner].Base); Assert.Equal(5, Might(game.State, owner));
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Empty(game.State.StackItems);
            await game.Replay();
        }
        finally { await Save("native-friendly-cost-" + owner, initial, game); }
    }

    private static ActionPromptChoiceDto CostChoice(Driver game, string owner, string objectId)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)[owner].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            r => (string?)r["sourceObjectId"] == "UNIT" && (string?)r["abilityId"] == "EMPOWER:0:0");
        Assert.Equal(0, row["minTargetCount"]); Assert.Equal(0, row["maxTargetCount"]);
        Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]);
        return Assert.Single(choices, c => c.ObjectIds is not null && c.ObjectIds.SequenceEqual([objectId]));
    }
    private static MatchState Position(string owner)
    {
        var state = VenUnitEntryBasicTests.Position(No); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        cards["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", No, owner); locations["UNIT"] = new(owner, "HAND");
        zones["P1"] = zones["P1"] with { Hand = zones["P1"].Hand.Where(id => id != "UNIT").ToArray() };
        zones[owner] = zones[owner] with { Hand = [..zones[owner].Hand, "UNIT"] };
        var added = new List<string>();
        foreach (var id in new[] { "COST", "SPARE" })
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·001/298", owner); locations[id] = new(owner, "BASE"); added.Add(id); }
        for (var i = 1; i <= 3; i++)
        {
            var id = "VEN124-RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R06", owner);
            locations[id] = new(owner, "BASE"); added.Add(id);
        }
        zones[owner] = zones[owner] with { Base = [..zones[owner].Base, ..added] };
        return state with { RoomId = "VEN124-EXPLICIT-TURN-FIVE-" + owner, ActivePlayerId = owner, TurnPlayerId = owner,
            CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(0, 0) } };
    }
    private static async Task Play(Driver game, string owner)
    {
        await game.TapRunes(owner, Enumerable.Range(1, 3).Select(i => "VEN124-RUNE-" + i));
        Assert.Equal(3, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
        await game.Act(owner, new PlayCardCommand("UNIT", No, []));
        Assert.Equal(0, game.State.RunePools[owner].Mana); Assert.Equal(0, game.State.RunePools[owner].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones[owner].Base); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
    }
    private static int Might(MatchState state, string owner) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN124_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN37824 No124 explicit turn-five fixture, zero pools and physical normal play. Existing friendly units pre-exist. Required cost chosen through public additional-cost choices; no fabricated source/profile or empowered fixture state. Per-command privacy/recovery/dedup/original replay assertions remain. No canonical opening, SQL/network, full L2/L3 or whole-card claim.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
