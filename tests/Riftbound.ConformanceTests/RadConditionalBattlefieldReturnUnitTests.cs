using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadConditionalBattlefieldReturnUnitTests
{
    private const string Stranger = "RAD·107/167";
    private static MatchState Position(int count, bool ward = false)
    {
        var state = VenUnitEntryBasicTests.Position(Stranger);
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        zones["P2"] = zones["P2"] with { Base = [], Battlefields = ["OTHER", "ENEMY"] };
        locations["ENEMY"] = new("P2", "BATTLEFIELD", "BF");
        cards["BF"] = cards["BF"] with { ControllerId = "P2" };
        if (ward) cards["ENEMY"] = cards["ENEMY"] with { Tags = [..cards["ENEMY"].Tags, "法盾"] };
        for (var i = 1; i < count; i++) {
            var id = "OTHER-ENEMY-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", "P2");
            locations[id] = new("P2", "BATTLEFIELD", "BF"); zones["P2"] = zones["P2"] with { Battlefields = [..zones["P2"].Battlefields, id] };
        }
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }
    private static async Task Choose(Driver game, params string[] targets)
    {
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
    }
    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-stranger", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(3, false, false)] [InlineData(4, false, true)] [InlineData(5, false, true)]
    [InlineData(4, true, false)]
    public async Task SelectedBattlefieldCountsAllUnitsAndReturningAnEnemyIsOptional(int count, bool decline, bool returned)
    {
        var game = new Driver(Position(count)); await game.Act("P1", new PlayCardCommand("UNIT", Stranger, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal("TRIGGER_CONFIRMATION", game.State.PendingCardChoice!.ChoiceWindow);
        Assert.Contains("ENEMY", game.State.PendingCardChoice.LegalObjectIds);
        await Choose(game, decline ? ["BF"] : ["BF", "ENEMY"]); await game.Top();
        Assert.Equal(returned, game.State.PlayerZones["P2"].Hand.Contains("ENEMY"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("UNIT", true)] [InlineData("OTHER-ENEMY-1", false)]
    public async Task ResponsesRecheckTheThresholdWhileTheSkillSurvivesSourceDeparture(string returned, bool bounces)
    {
        var player = returned == "UNIT" ? "P1" : "P2";
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(4), "RETURN", "OGN·104/298", player));
        await game.Act("P1", new PlayCardCommand("UNIT", Stranger, ["BF", "ENEMY"]));
        if (player == "P2") await game.Act("P1", new PassPriorityCommand());
        await game.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top(); await game.Top();
        Assert.Equal(bounces, game.State.PlayerZones["P2"].Hand.Contains("ENEMY")); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task WardIsConfirmedAfterEntryBeforeTheResponseWindow(bool pay)
    {
        var game = new Driver(Position(4, ward: true)); await game.Act("P1", new PlayCardCommand("UNIT", Stranger, []));
        await Choose(game, "BF", "ENEMY"); var payment = game.State.PendingPayment!;
        Assert.Equal("TRIGGER_TARGET_COST", payment.PaymentWindow); var power = game.State.RunePools["P1"].Power;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(pay, game.State.PlayerZones["P2"].Hand.Contains("ENEMY"));
        Assert.Equal(power - (pay ? 1 : 0), game.State.RunePools["P1"].Power);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("same")] [InlineData("other-field")] [InlineData("friendly")]
    [InlineData("equipment")] [InlineData("deck")] [InlineData("reverse")]
    public async Task InvalidTargetCombinationsLeaveThePostEntryChoiceUsable(string branch)
    {
        var game = new Driver(Position(4)); await game.Act("P1", new PlayCardCommand("UNIT", Stranger, []));
        var c = game.State.PendingCardChoice!;
        string[] targets = branch switch { "missing" => [], "same" => ["BF", "BF"], "other-field" => ["OTHER", "ENEMY"],
            "friendly" => ["BF", "ALLY"], "equipment" => ["BF", "GEAR"], "deck" => ["BF", "P2-DRAW-1"], _ => ["ENEMY", "BF"] };
        await Reject(game.State, new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, targets));
        await Choose(game, "BF", "ENEMY"); await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task OfficialIdentityAndPrintedCostsArePreserved()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Stranger);
        Assert.Equal(39609, card.Id); Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(2, card.Power);
        Assert.Contains("不少于四名单位", card.CardEffect);
    }

    [Fact]
    public async Task RecoveryRejectsATargetWhoseCapturedGenerationWasForged()
    {
        var game = new Driver(Position(4)); await game.Act("P1", new PlayCardCommand("UNIT", Stranger, ["BF", "ENEMY"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            TargetGenerations = new Dictionary<string, long>(item.TargetGenerations!) { ["ENEMY"] = item.TargetGenerations!["ENEMY"] + 1 } }] }));
    }
}
