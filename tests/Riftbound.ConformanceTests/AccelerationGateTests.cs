using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AccelerationGateTests
{
    private const string Card = "VEN·150";
    private static readonly string[] Targets = ["HOST", "GEAR", "RUNE", "ENEMY"];

    [Fact]
    public async Task AdmitsTheExactOfficialChinesePrinting()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(37853, card.Id); Assert.Equal("加速之门", card.CardName); Assert.Equal("杰斯", card.Hero);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal("让最多四个单位、装备或符文变为活跃状态。", card.CardEffect);
    }

    private static MatchState Position(string color = RuneTrait.Blue, bool irelia = false)
    {
        var state = AttackDamageDistributionTests.Spell(ArmamentMightTests.Position(
            unit: irelia ? "VEN·174" : "OGN·001/298"), "GATE", Card, "P1");
        var catalog = OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult();
        var rune = catalog.Cards.First(c => c.CardCategory == "rune" && c.CardColorList.Contains(RuneTrait.Blue));
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = true },
                ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = true },
                ["RUNE"] = new("RUNE", cardNo: rune.CardNo, ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.RuneCard], isExhausted: true),
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·219/298", "P2") with { IsExhausted = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "RUNE"] },
                ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, "ENEMY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["RUNE"] = new("P1", "BASE"), ["ENEMY"] = new("P2", "BASE") },
            RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(3, 0, new Dictionary<string, int> { [color] = 1 }), ["P2"] = new(10, 10) } };
    }

    [Theory]
    [InlineData(RuneTrait.Blue, 0)] [InlineData(RuneTrait.Blue, 1)] [InlineData(RuneTrait.Blue, 4)]
    [InlineData(RuneTrait.Orange, 0)] [InlineData(RuneTrait.Orange, 1)] [InlineData(RuneTrait.Orange, 4)]
    public async Task PaysThreeManaAndOneOfItsPrintedColorsThenReadiesUpToFourObjectsAfterResponses(string color, int count)
    {
        var state = Position(color); var selected = Targets.Take(count).ToArray();
        var command = new PlayCardCommand("GATE", Card, selected);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait.GetValueOrDefault(color));
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Single(game.State.StackItems);
        Assert.All(Targets, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        await game.Top();
        Assert.All(Targets, id => Assert.Equal(!selected.Contains(id), game.State.CardObjects[id].IsExhausted));
        Assert.Contains("GATE", game.State.PlayerZones["P1"].Graveyard);
        if (count == 4) {
            Assert.Contains(game.Events(), e => e.Event.Kind == "RUNE_READIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "RUNE"));
            Assert.Contains(game.Events(), e => e.Event.Kind == "EQUIPMENT_READIED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "GEAR"));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("five")]
    [InlineData("legend")] [InlineData("battlefield")] [InlineData("hand")]
    [InlineData("hidden")] [InlineData("rune-deck")] [InlineData("no-mana")] [InlineData("wrong-color")]
    public async Task IllegalTargetsOrPaymentRejectTheWholePlayWithoutSpendingOrReadyingAnything(string scenario)
    {
        var state = Position(); string[] targets = ["HOST"];
        if (scenario == "missing") targets = ["MISSING"];
        if (scenario == "duplicate") targets = ["HOST", "HOST"];
        if (scenario == "five") {
            targets = [..Targets, "EXTRA"];
            state = state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    ["EXTRA"] = OfficialGarenConquestBattlefieldCountTests.Printed("EXTRA", "OGN·219/298", "P2") },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, "EXTRA"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["EXTRA"] = new("P2", "BASE") } };
        }
        if (scenario == "battlefield") targets = ["F"];
        if (scenario == "hand") targets = ["GATE"];
        if (scenario == "legend") {
            targets = ["LEGEND"];
            state = state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    ["LEGEND"] = new("LEGEND", cardNo: "VEN·189", ownerId: "P1", controllerId: "P1", isExhausted: true,
                        tags: ["CARD_TYPE:LEGEND"]) },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        }
        if (scenario == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = true } } };
        if (scenario == "rune-deck") {
            targets = ["RUNE"];
            state = state with {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Where(id => id != "RUNE").ToArray(),
                        RuneDeck = [..state.PlayerZones["P1"].RuneDeck, "RUNE"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "RUNE_DECK") } };
        }
        if (scenario == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(2, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        if (scenario == "wrong-color") state = Position(RuneTrait.Green);
        await Reject(state, new PlayCardCommand("GATE", Card, targets));
    }

    [Fact]
    public async Task AReturnedTargetIsSkippedWhileTheOtherChosenObjectStillReadies()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(10, 10, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), ["P2"] = new(10, 10) } });
        await game.Act("P1", new PlayCardCommand("GATE", Card, ["HOST", "GEAR"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var returned = game.State.CardObjects["HOST"]; await game.Top();
        Assert.Equal(returned, game.State.CardObjects["HOST"]); Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TargetingAndActuallyReadyingIreliaEarnSeparateSkillsOnlyForActionsThatHappen(bool exhausted)
    {
        var state = Position(irelia: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = exhausted } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("GATE", Card, ["HOST"]));
        Assert.Equal(2, game.State.StackItems.Count); Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        await game.Top(); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["HOST"].Power);
        if (exhausted) { Assert.Single(game.State.StackItems); await game.Top(); }
        else Assert.Empty(game.State.StackItems);
        Assert.Equal(exhausted ? 6 : 5, game.State.CardObjects["HOST"].Power);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public void PromptOffersTheSameZeroToFourTargetsAcrossUnitsEquipmentAndRunes()
    {
        var rows = (ResolutionResult.BuildPrompts(Position())["P1"].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.PlayCard)
            .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]));
        var row = Assert.Single(rows, r => Equals(r.GetValueOrDefault("sourceObjectId"), "GATE"));
        Assert.Equal(0, row["minTargetCount"]); Assert.Equal(4, row["maxTargetCount"]);
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(row["targetChoicesByIndex"]);
        foreach (var values in choices.Values) {
            var legal = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(values).Select(c => c.Id);
            Assert.Equal(Targets.Order(StringComparer.Ordinal), legal.Order(StringComparer.Ordinal));
        }
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)]
    public async Task EnemyWardIsPaidInAdditionToThePrintedCostBeforeAnyObjectsReady(int genericPower, bool payable)
    {
        var state = Position();
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, "法盾2"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(3, genericPower, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        var command = new PlayCardCommand("GATE", Card, Targets);
        if (!payable) { await Reject(state, command); return; }
        var game = new Driver(state); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.All(Targets, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        await game.Top(); Assert.All(Targets, id => Assert.False(game.State.CardObjects[id].IsExhausted)); await game.Replay();
    }

    [Fact]
    public async Task EnemyRunesAndEquipmentCanAlsoBeReadied()
    {
        var state = Position(); var moved = new[] { "GEAR", "RUNE" };
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2" },
                ["RUNE"] = state.CardObjects["RUNE"] with { OwnerId = "P2", ControllerId = "P2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Except(moved).ToArray() },
                ["P2"] = state.PlayerZones["P2"] with { Base = [..state.PlayerZones["P2"].Base, ..moved] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["GEAR"] = new("P2", "BASE"), ["RUNE"] = new("P2", "BASE") } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("GATE", Card, moved));
        await game.Top(); Assert.All(moved, id => Assert.False(game.State.CardObjects[id].IsExhausted)); await game.Replay();
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-gate", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
