using System.Collections;
using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class LinkedAttributeDeclarationTests
{
    private const string List = "UNL-138/219";
    private const string Skill = "EXHAUST_DECLARED_ATTRIBUTE_MINUS_2";

    private static MatchState Position(string host = "OGN·111/298")
    {
        var state = Put(Create(), "LIST", List, "P1", "HAND");
        state = Put(state, "ENEMY", "UNL-160/219", "P2", "BASE");
        state = Put(state, "ALLY", "OGS·007/024", "P1", "BASE");
        state = Put(state, "HOST", host, "P1", "BASE");
        state = Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        return Put(state, "RECALL", "SFD·135/221", "P1", "HAND");
    }

    private static async Task Declare(Driver game, string attribute)
    {
        await game.Top();
        var rule = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECLARE_ATTRIBUTE", rule.Request.Kind);
        Assert.Contains(rule.Request.Options, option => option.Id == attribute);
        foreach (var invalid in new[] { "虚构属性", "CARD_TYPE:UNIT", "{{法盾}}" })
            await Reject(game.State, "P1", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [invalid]));
        await Reject(game.State, "P2", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [attribute]));
        await game.Act("P1", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [attribute]));
        Assert.Empty(game.State.StackItems);
    }

    [Theory]
    [InlineData("魄罗", "ENEMY")]
    [InlineData("盖伦", "ALLY")]
    [InlineData("精锐", "ALLY")]
    public async Task ARealDeclarationLimitsTargetsAndReducesOnlyTheChosenUnit(string attribute, string target)
    {
        var game = new Driver(Position());
        await Reject(game.State, "P1", new PlayCardCommand("LIST", List, [target]));
        await game.Act("P1", new PlayCardCommand("LIST", List, []));
        Assert.Null(game.State.CardObjects["LIST"].LinkedAttributes);
        await Reject(game.State, "P1", new ActivateAbilityCommand("LIST", Skill, [target]));
        await Declare(game, attribute);
        var declaration = Assert.Single(game.State.CardObjects["LIST"].LinkedAttributes!);
        Assert.Equal(attribute, declaration.Attribute);
        var other = target == "ENEMY" ? "ALLY" : "ENEMY";
        await Reject(game.State, "P1", new ActivateAbilityCommand("LIST", Skill, [other]));
        await Reject(game.State, "P2", new ActivateAbilityCommand("LIST", Skill, [target]));
        var might = game.State.CardObjects[target].Power;
        var unaffected = game.State.CardObjects[other].Power;
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, [target]));
        Assert.True(game.State.CardObjects["LIST"].IsExhausted);
        Assert.Equal(might, game.State.CardObjects[target].Power);
        Assert.Equal(declaration, Assert.Single(game.State.StackItems).SimpleActivation!.LinkedAttribute);
        await game.Top();
        Assert.Equal(might - 2, game.State.CardObjects[target].Power);
        Assert.Equal(unaffected, game.State.CardObjects[other].Power);
        await game.Replay();
    }

    [Fact]
    public async Task TwoPrintedListsKeepIndependentDeclarations()
    {
        var game = new Driver(Put(Position(), "LIST2", List, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "魄罗");
        await game.Act("P1", new PlayCardCommand("LIST2", List, [])); await Declare(game, "盖伦");
        await Reject(game.State, "P1", new ActivateAbilityCommand("LIST2", Skill, ["ENEMY"]));
        await Reject(game.State, "P1", new ActivateAbilityCommand("LIST", Skill, ["ALLY"]));
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["ENEMY"])); await game.Top();
        await game.Act("P1", new ActivateAbilityCommand("LIST2", Skill, ["ALLY"])); await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Power);
        Assert.Equal(3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task ATargetedReductionStillUsesGangplanksSharedReplacement(string printing)
    {
        var initial = Put(Position(), "ENEMY", printing, "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { IsEmpowered = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "普朗克");
        var might = game.State.CardObjects["ENEMY"].Power;
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["ENEMY"])); await game.Top();
        Assert.Equal(might + 3, game.State.CardObjects["ENEMY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningAndReplayingTheListRequiresANewDeclaration()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "魄罗");
        var generation = game.State.CardObjects["LIST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RECALL", "SFD·135/221", ["LIST"])); await game.Top();
        Assert.Contains("LIST", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["LIST"].LinkedAttributes);
        Assert.True(game.State.CardObjects["LIST"].ObjectGeneration > generation);
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "盖伦");
        await Reject(game.State, "P1", new ActivateAbilityCommand("LIST", Skill, ["ENEMY"]));
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["ALLY"])); await game.Top();
        Assert.Equal(3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheTargetBeforeResolutionDoesNotReduceItsNewHandGeneration()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "魄罗");
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["ENEMY"]));
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        await game.Top();
        Assert.Empty(game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(5, game.State.CardObjects["ENEMY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheAnimatedSourceStillResolvesItsCapturedDeclaration()
    {
        var initial = Put(Position(), "SOURCE_RETURN", "OGN·104/298", "P1", "HAND");
        initial = OriannaEquipmentConsumerFixture.WithResources(initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } });
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "魄罗");
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "LIST");
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["ENEMY"]));
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("SOURCE_RETURN", "OGN·104/298", ["LIST"])); await game.Top();
        Assert.Contains("LIST", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["LIST"].LinkedAttributes);
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Power);
        Assert.Equal("LIST", Assert.Single(game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers).SourceObjectId);
        var snapshots = ResolutionResult.BuildSnapshots(game.State);
        foreach (var (snapshot, expectedSource) in new[] {
            (snapshots["P1"], "LIST"), (snapshots["P2"], "HIDDEN"),
            (ResolutionResult.BuildSpectatorSnapshot(game.State), "HIDDEN") })
        {
            var view = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var effect = Assert.Single(view.GetProperty("timing").GetProperty("continuousEffects").EnumerateArray(),
                effect => effect.TryGetProperty("effectKind", out var kind) && kind.GetString() == Skill);
            Assert.Equal(expectedSource, effect.GetProperty("sourceObjectId").GetString());
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("attribute")]
    [InlineData("source-generation")]
    [InlineData("future-tick")]
    [InlineData("face-revision")]
    [InlineData("missing")]
    public async Task RecoveryRejectsForgedOrRemovedCapturedDeclarations(string change)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "魄罗");
        await game.Act("P1", new ActivateAbilityCommand("LIST", Skill, ["ENEMY"]));
        var item = Assert.Single(game.State.StackItems);
        var context = item.SimpleActivation!;
        var declaration = context.LinkedAttribute!;
        var forged = change switch {
            "attribute" => declaration with { Attribute = "虚构属性" },
            "source-generation" => declaration with { Ability = declaration.Ability with { SourceGeneration = 999 } },
            "future-tick" => declaration with { CreatedTick = game.State.Tick + 1 },
            "face-revision" => declaration with { Ability = declaration.Ability with { FaceRevision = 999 } },
            _ => null
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            SimpleActivation = context with { LinkedAttribute = forged } }] }));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·111/298")]
    [InlineData("ARC-003/006")]
    public async Task BorrowingOnlyTheActivatedComponentCannotReadTheDonorsDeclaration(string printing)
    {
        var game = new Driver(Position(printing));
        await game.Act("P1", new PlayCardCommand("LIST", List, [])); await Declare(game, "魄罗");
        var card = (await OfficialCardCatalog.LoadRuntimeAsync()).Cards.Single(card => card.CardNo == List);
        Assert.Single(BorrowedExhaustParser.Parse(card.CardEffect));
        var rules = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.BorrowedExhaustRules")!;
        var abilities = (IEnumerable)rules.GetMethod("Abilities", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [game.State, "P1", game.State.CardObjects["HOST"]])!;
        var inherited = Assert.Single(abilities.Cast<object>(), value =>
            ((CardObjectState)value.GetType().GetProperty("Donor")!.GetValue(value)!).ObjectId == "LIST");
        var id = (string)inherited.GetType().GetProperty("Id")!.GetValue(inherited)!;
        foreach (var target in new[] { "ENEMY", "ALLY" })
            await Reject(game.State, "P1", new ActivateAbilityCommand("HOST", id, [target]));
        await Reject(game.State, "P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["ENEMY"].Power);
        await game.Replay();
    }
}
