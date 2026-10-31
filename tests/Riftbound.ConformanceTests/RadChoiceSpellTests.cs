using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadChoiceSpellTests
{
    private const string Card = "RAD·013/167";
    private const string Damage = "BATTLEFIELD_UNIT_DAMAGE_4";
    private const string Destroy = "DESTROY_EQUIPMENT";

    [Fact]
    public async Task OfficialPrintingOffersBothModesWithTheirChineseLabelsAndPrintedCost()
    {
        Assert.DoesNotContain((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == Card);
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(39503, card.Id); Assert.Equal("葬身沙海", card.CardName);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal("{{迅捷}}（可在你的回合或法术对决中打出。）\n选择一个效果 —\n- 对战场上的一名单位造成4点伤害。\n- 摧毁一件装备。", card.CardEffect);
        var play = Assert.Single(ResolutionResult.BuildPrompts(Position())["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"])
            .Where(r => Equals(r["sourceObjectId"], "UNIT")).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("对战场单位造成 4 点伤害", Assert.Single(rows, r => Equals(r["mode"], Damage))["modeLabel"]);
        Assert.Equal("摧毁一件装备", Assert.Single(rows, r => Equals(r["mode"], Destroy))["modeLabel"]);
    }

    [Theory]
    [InlineData(Damage, "ALLY", "P1")] [InlineData(Damage, "ENEMY", "P2")]
    [InlineData(Destroy, "GEAR", "P1")] [InlineData(Destroy, "ENEMY-GEAR", "P2")]
    public async Task EitherModePaysThreeAndOneRedAndResolvesOnlyAfterResponses(string mode, string target, string owner)
    {
        var game = new Driver(Position());
        var play = new PlayCardCommand("UNIT", Card, [target], Mode: mode);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        await game.Act("P1", play);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(target, game.State.PlayerZones[owner].Graveyard);
        Assert.Equal(0, game.State.CardObjects[target].Damage); Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones[owner].Graveyard);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED").ToArray();
        if (mode == Damage) Assert.Equal(4, Convert.ToInt32(Assert.Single(damage).Event.Payload["damage"]));
        else Assert.Empty(damage);
        await game.Replay();
    }

    [Theory]
    [InlineData(Damage, "BASE-UNIT")] [InlineData(Damage, "GEAR")]
    [InlineData(Damage, "GRAVE")] [InlineData(Damage, "BF")]
    [InlineData(Destroy, "ALLY")] [InlineData(Destroy, "ENEMY")]
    [InlineData(Destroy, "BROKEN")] [InlineData(Destroy, "BF")]
    [InlineData("FORGED", "ALLY")] [InlineData("", "ALLY")]
    public async Task WrongModesTypesAndZonesCannotSpendOrChangeState(string mode, string target)
        => await Reject(Position(), new PlayCardCommand("UNIT", Card, [target], Mode: mode));

    public static IEnumerable<object[]> InvalidSelections() => new[] { Damage, Destroy }.SelectMany(mode =>
        new[] { "zero-targets", "two-targets", "duplicate", "mana", "power", "wrong-trait", "wrong-turn", "graveyard" }
            .Select(branch => new object[] { mode, branch }));

    [Theory]
    [MemberData(nameof(InvalidSelections))]
    public async Task TargetCountPaymentSourceAndTurnRejectionsPreserveAllViews(string mode, string branch)
    {
        var initial = Position();
        var target = mode == Damage ? "ALLY" : "GEAR";
        var targets = branch switch { "zero-targets" => Array.Empty<string>(),
            "two-targets" => new[] { target, mode == Damage ? "ENEMY" : "ENEMY-GEAR" },
            "duplicate" => new[] { target, target }, _ => new[] { target } };
        if (branch is "mana" or "power" or "wrong-trait") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = branch switch {
                "mana" => new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }),
                "power" => new(3, 0), _ => new(3, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } } };
        if (branch == "wrong-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "graveyard") initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        await Reject(initial, new PlayCardCommand("UNIT", Card, targets, Mode: mode));
    }

    [Theory]
    [InlineData(Damage)] [InlineData(Destroy)]
    public async Task SwiftCanPlayInTheOpponentsRealMovementDuel(string mode)
    {
        var initial = LocalDestructionRecallTests.At(Position(), "ENEMY", "P2", "BASE")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        if (game.State.FocusPlayerId != "P1") await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P1", new PlayCardCommand("UNIT", Card, [mode == Damage ? "ALLY" : "GEAR"], Mode: mode));
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(Damage)] [InlineData(Destroy)]
    public async Task SwiftCannotRespondBeforeAnIncomingSpellResolves(string mode)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "INCOMING", "VEN·049", "P2")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("INCOMING", "VEN·049", []));
        await game.Act("P2", new PassPriorityCommand());
        await Reject(game.State, new PlayCardCommand("UNIT", Card, [mode == Damage ? "ALLY" : "GEAR"], Mode: mode));
        await game.Act("P1", new PassPriorityCommand());
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    private static MatchState Position()
    {
        var initial = LocalDestructionRecallTests.At(VenUnitEntryBasicTests.Position(Card), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var (id, no, owner, zone) in new[] {
            ("BASE-UNIT", "OGN·219/298", "P1", "BASE"),
            ("ENEMY-GEAR", "SFD·022/221", "P2", "BASE"),
            ("BROKEN", "SFD·022/221", "P1", "GRAVEYARD") }) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone);
        }
        return initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Base = [..p.Value.Base, "BASE-UNIT"], Graveyard = [..p.Value.Graveyard, "BROKEN"] }
                : p.Value with { Base = [..p.Value.Base, "ENEMY-GEAR"] }),
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-choice", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
