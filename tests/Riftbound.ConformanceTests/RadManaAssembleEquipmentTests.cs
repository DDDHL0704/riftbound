using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadManaAssembleEquipmentTests
{
    private const string Blades = "RAD·071/167";
    private const string Shield = "RAD·123/167";
    private const string Assemble = "ASSEMBLE_1_MANA";

    [Theory]
    [InlineData(Blades, 39570L, 1, 0, 0, "坚守2")]
    [InlineData(Shield, 39628L, 2, 1, 1, "壁垒")]
    public async Task TheExactOfficialPrintingAndItsSeparateLowerBoxAreAdmitted(string no, long id,
        int mana, int power, int bonus, string keyword)
    {
        Assert.DoesNotContain((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.ReturnEnergy ?? 0);
        Assert.Equal(no == Blades ? "装配{{1}}（支付{{1}}：将此牌贴附到你控制的一名单位上。）"
            : "{{灵便}}（此牌具有{{反应}}。当你打出此牌时，将其贴附到你控制的一名单位上。）\n{{装配}}{{1}}（支付{{1}}：将此牌贴附到你控制的一名单位上。）", card.CardEffect);
        var lower = Assert.IsType<OfficialArmamentDefinition>(OfficialArmamentCatalog.Find(no));
        Assert.Equal(no, lower.SourceCardNo); Assert.Equal(bonus, lower.BaseMightBonus);
        Assert.Equal(new[] { keyword }, lower.GrantedKeywords);
        Assert.Equal(power, PrintedPowerCostRules.ForCard(no).Amount);
        if (power > 0) Assert.Equal(new[] { RuneTrait.Yellow }, PrintedPowerCostRules.ForCard(no).Traits);
    }

    [Theory]
    [InlineData(Blades)] [InlineData(Shield)]
    public async Task NormalPlayAndAttachmentPaySeparateCostsAndKeepTheHostBaseMight(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, no == Shield ? ["ALLY"] : []));
        Assert.Equal(no == Blades ? 19 : 18, game.State.RunePools["P1"].Mana);
        Assert.Equal(no == Blades ? 20 : 19, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId);
        if (no == Blades) {
            Assert.Empty(game.State.StackItems);
            await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", [Assemble]));
            Assert.Equal(18, game.State.RunePools["P1"].Mana);
            Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
            Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId);
        }
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(no == Blades ? 4 : 5, Might(game.State, "ALLY"));
        Assert.Contains(no == Blades ? "坚守2" : "壁垒", game.State.CardObjects["ALLY"].Tags);
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(Blades)] [InlineData(Shield)]
    public async Task OneManaAndNoPowerCanAssembleThroughTheNormalPrompt(string no)
    {
        var initial = Position(no, onField: true) with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 0), ["P2"] = new(20, 20) } };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.AssembleEquipment);
        Assert.Contains(candidate.Sources!, c => c.Id == "UNIT");
        Assert.Contains(candidate.OptionalCosts!, c => c.Id == Assemble);
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", [Assemble]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        var cost = game.Events().Last(e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(1, cost.Payload["baseManaCost"]); Assert.Equal(0, cost.Payload["power"]);
        await game.Top(); Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
        await game.Replay();
    }

    public static IEnumerable<object[]> IllegalAssembly() => new[] { Blades, Shield }.SelectMany(no =>
        new[] { "mana", "wrong-cost", "missing-cost", "extra-cost", "enemy-source", "face-down", "source-hand",
            "enemy-target", "grave-target", "equipment-target", "self-target", "hidden-target", "wrong-turn" }
            .Select(branch => new object[] { no, branch }));

    [Theory]
    [MemberData(nameof(IllegalAssembly))]
    public async Task InvalidAssemblyCannotSpendAttachOrChangeViews(string no, string branch)
    {
        var state = Position(no, onField: branch != "source-hand");
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (branch is "enemy-source" or "face-down" or "hidden-target") {
            var id = branch == "hidden-target" ? "ALLY" : "UNIT";
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { ControllerId = branch == "enemy-source" ? "P2" : "P1", IsFaceDown = branch != "enemy-source" } } };
        }
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var target = branch switch { "enemy-target" => "ENEMY", "grave-target" => "GRAVE", "equipment-target" => "GEAR", "self-target" => "UNIT", _ => "ALLY" };
        string[] costs = branch switch { "wrong-cost" => ["ASSEMBLE_ANY_POWER"], "missing-cost" => [],
            "extra-cost" => [Assemble, "ASSEMBLE_RED"], _ => [Assemble] };
        await Reject(state, "P1", new AssembleEquipmentCommand("UNIT", target, costs));
    }

    [Theory]
    [InlineData(Blades)] [InlineData(Shield)]
    public async Task DestroyingTheEquipmentRemovesOnlyItsGrantedKeywordsAndPrintedBonus(string no)
    {
        var initial = HandCard(Position(no, onField: true), "REMOVE", "VEN·003", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", [Assemble])); await game.Top();
        Assert.Contains(no == Blades ? "坚守2" : "壁垒", game.State.CardObjects["ALLY"].Tags);
        await game.Act("P1", new PlayCardCommand("REMOVE", "VEN·003", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(no == Blades ? "坚守2" : "壁垒", game.State.CardObjects["ALLY"].Tags);
        Assert.Null(game.State.CardObjects["ALLY"].RuleDerivedTags);
        Assert.Equal(4, Might(game.State, "ALLY")); await game.Replay();
    }

    [Fact]
    public async Task AgileShieldCanReactOnTheOpponentsTurnWhileBladesCannot()
    {
        var initial = HandCard(Position(Shield), "OPEN", "VEN·049", "P2");
        initial = HandCard(initial, "SLOW", Blades, "P1");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("OPEN", "VEN·049", [])); await game.Act("P2", new PassPriorityCommand());
        await Reject(game.State, "P1", new PlayCardCommand("SLOW", Blades, []));
        await game.Act("P1", new PlayCardCommand("UNIT", Shield, ["ALLY"])); await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Contains("壁垒", game.State.CardObjects["ALLY"].Tags);
        Assert.Single(game.State.StackItems); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task NumericGrantsFromTwoRealAssembliesAddToPrintedSteadfastAndSurviveTurnEnd()
    {
        var initial = HandCard(Position(Blades), "SECOND", Blades, "P1");
        initial = HandCard(initial, "BODY", "RAD·088/167", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("BODY", "RAD·088/167", []));
        foreach (var id in new[] { "UNIT", "SECOND" }) {
            await game.Act("P1", new PlayCardCommand(id, Blades, []));
            await game.Act("P1", new AssembleEquipmentCommand(id, "BODY", [Assemble])); await game.Top();
        }
        Assert.Contains("坚守2", game.State.CardObjects["BODY"].Tags);
        Assert.Equal(new[] { "坚守4" }, game.State.CardObjects["BODY"].RuleDerivedTags);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(new[] { "坚守4" }, game.State.CardObjects["BODY"].RuleDerivedTags);
        await game.Replay();
    }

    [Theory]
    [InlineData(Blades, 6)] [InlineData(Shield, 5)]
    public async Task TheAttachedLowerBoxChangesRealDefendingMightAndBulwarkDamageOrder(string no, int defendingMight)
    {
        var state = Position(no, onField: true);
        var cards = state.CardObjects.ToDictionary();
        cards["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2");
        cards["EXTRA"] = OfficialGarenConquestBattlefieldCountTests.Printed("EXTRA", "OGN·219/298", "P1");
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in new[] { "ALLY", "EXTRA" }) locations[id] = new("P1", "BATTLEFIELD", "BF");
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Base = ["GEAR", "UNIT"], Battlefields = ["BF", "ALLY", "EXTRA"] };
        var game = new Driver(state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones });
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", [Assemble])); await game.Top();
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        for (var index = 0; index < 4 && game.State.FocusPlayerId is { } focus; index++)
            await game.Act(focus, new PassFocusCommand());
        if (ResolutionResult.BuildPrompts(game.State)["P2"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P2", new DeclareBattleCommand("BF", ["ENEMY"], ["ALLY", "EXTRA"], OptionalCosts: ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(defendingMight, Might(game.State, "ALLY"));
        if (no == Shield) await Reject(game.State, "P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("ENEMY", "ALLY", 4), new("ENEMY", "EXTRA", 4)]));
        await game.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("ENEMY", "ALLY", defendingMight), new("ENEMY", "EXTRA", 8 - defendingMight)]));
        await game.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF", [new("ALLY", "ENEMY", defendingMight), new("EXTRA", "ENEMY", 4)]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("EXTRA", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.DoesNotContain(no == Blades ? "坚守2" : "壁垒", game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    private static MatchState HandCard(MatchState state, string id, string no, string player)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { Hand = [..state.PlayerZones[player].Hand, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") } };

    private static MatchState Position(string no, bool onField = false)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return onField ? state with {
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Base = [..p.Value.Base, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } } : state;
    }

    private static int Might(MatchState state, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("players")
            .GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-mana-assemble", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
