using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHereRobotTests
{
    public static IEnumerable<object[]> Cards() => new[] {
        new object[] { "RAD·009/167", 39499L, 6, 3, true },
        new object[] { "RAD·049/167", 39545L, 4, 1, false } };

    private static CardObjectState[] Robots(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "SFD·T01").ToArray();

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task ExactOfficialPrintKeepsCostsEntryAndTheHereTokenInstruction(string no, long id, int mana, int might, bool ready)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(might, card.Power);
        Assert.Equal(ready ? 1 : 0, card.ReturnEnergy ?? 0);
        Assert.Equal(ready
            ? "我以活跃状态进场。\n当你打出我时，在此处打出一个活跃的3{{S}}“机器人”单位指示物。"
            : "当你打出我时，在此处打出一名3{{S}}的“机器人”单位指示物。", card.CardEffect);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers,
            t => t.Kind == TriggerKinds.SourceUnitPlayedOrScoredCreateUnit);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, spec.Timing);
        Assert.Equal(TriggerTokenDestinations.SourceLocation, spec.CreatedTokenDestination);
        Assert.Equal("机器人", spec.CreatedTokenName); Assert.Equal(3, spec.CreatedTokenPower);
        Assert.Equal(!ready, spec.CreatedTokenExhausted);
        Assert.Equal(ready ? 1 : 0, PrintedPowerCostRules.ForCard(no).Amount);
    }

    public static IEnumerable<object[]> Entries() => Cards().SelectMany(c =>
        new[] { "BASE", "BATTLEFIELD:BF" }.Select(at => new object[] { c[0], c[2], c[3], c[4], at }));

    [Theory]
    [MemberData(nameof(Entries))]
    public async Task PaidEntryCreatesOneRobotHereOnlyAfterBothPlayersCanRespond(string no, int mana, int might, bool ready, string destination)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(ready ? 19 : 20, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(!ready, game.State.CardObjects["UNIT"].IsExhausted);
        if (ready) Assert.Contains("机械", game.State.CardObjects["UNIT"].Tags);
        Assert.Empty(Robots(game.State));
        Assert.Equal("PLAY", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        var expectedAt = game.State.ObjectLocations["UNIT"];
        await game.Top();
        var robot = Assert.Single(Robots(game.State));
        Assert.Equal(3, robot.Power); Assert.Equal(!ready, robot.IsExhausted);
        Assert.Contains("机械", robot.Tags); Assert.Equal("P1", robot.ControllerId); Assert.Equal("P1", robot.OwnerId);
        Assert.Equal(expectedAt, game.State.ObjectLocations[robot.ObjectId]);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
        await game.Replay();
    }

    public static IEnumerable<object[]> IllegalPlays() => Cards().SelectMany(c =>
        new[] { "mana", "opponent", "wrong-turn", "target", "enemy-field" }
            .Select(mode => new object[] { c[0], mode }));

    [Theory]
    [MemberData(nameof(IllegalPlays))]
    public async Task IllegalUnitPlayDoesNotPayEnterOrQueueRobots(string no, string mode)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (mode == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (mode == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        await Reject(state, mode == "opponent" ? "P2" : "P1",
            new PlayCardCommand("UNIT", no, mode == "target" ? ["ALLY"] : [],
                Destination: mode == "enemy-field" ? "BATTLEFIELD:OTHER" : "BASE"));
    }

    [Fact]
    public async Task AssaultMechStillRequiresItsPrintedRedPower()
    {
        var state = VenUnitEntryBasicTests.Position("RAD·009/167");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20 }) } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", "RAD·009/167", []));
    }

    public static IEnumerable<object[]> Responses() => Cards().SelectMany(c =>
        new[] { "move", "recall" }.Select(mode => new object[] { c[0], mode }));

    [Theory]
    [MemberData(nameof(Responses))]
    public async Task ARealResponseRechecksHereAndNeverCreatesATokenForASourceThatLeft(string no, string mode)
    {
        var responseNo = mode switch { "move" => "OGS·011/024", "recall" => "OGN·104/298", _ => "OGN·229/298" };
        var state = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(no), "RESPONSE", responseNo, "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("RESPONSE", responseNo, ["UNIT"]));
        await game.Top();
        Assert.Equal(mode switch { "move" => "BASE", "recall" => "HAND", _ => "GRAVEYARD" }, game.State.ObjectLocations["UNIT"].Zone);
        Assert.Empty(Robots(game.State));
        await game.Top();
        if (mode == "move") {
            var robot = Assert.Single(Robots(game.State));
            Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations[robot.ObjectId]);
        }
        else Assert.Empty(Robots(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·009/167", false)] [InlineData("RAD·009/167", true)]
    [InlineData("RAD·049/167", false)] [InlineData("RAD·049/167", true)]
    public async Task ZileansSharedReplacementDuplicatesTheSameRobotAtTheSameLocation(string no, bool accept)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["Z"] = OfficialGarenConquestBattlefieldCountTests.Printed("Z", "UNL-086/219", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = [..state.PlayerZones["P1"].Battlefields, "Z"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["Z"] = new("P1", "BATTLEFIELD", "BF") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", choice.ChoiceWindow); Assert.Empty(Robots(game.State));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["Z"] : []));
        Assert.Equal(accept ? 2 : 1, Robots(game.State).Length);
        Assert.All(Robots(game.State), robot => {
            Assert.Equal(3, robot.Power); Assert.Contains("机械", robot.Tags);
            Assert.Equal(no == "RAD·049/167", robot.IsExhausted);
            Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations[robot.ObjectId]);
        });
        Assert.Equal(accept, game.State.CardObjects["Z"].UntilEndOfTurnEffects.Contains("TOKEN_ENTRY_REPLACEMENT_USED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·009/167")] [InlineData("RAD·049/167")]
    public async Task HoldingDoesNotRetriggerAnAbilityThatOnlySaysWhenPlayed(string no)
    {
        var state = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·278/298"), "UNIT", no, "F", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Empty(Robots(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·009/167", "new-object")] [InlineData("RAD·049/167", "new-object")]
    [InlineData("RAD·009/167", "barred-field")] [InlineData("RAD·049/167", "barred-field")]
    public async Task HereCannotUseANewIncarnationOrBypassABattlefieldsPlayRestriction(string no, string boundary)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (boundary == "barred-field") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "SFD·216/221", "P1") with { ControllerId = "P1" } } };
        var played = await OfficialGraveyardRecastTests.Act(state, "P1", new PlayCardCommand("UNIT", no, []));
        var pending = played.State;
        if (boundary == "new-object") pending = pending with {
            CardObjects = new Dictionary<string, CardObjectState>(pending.CardObjects) {
                ["UNIT"] = pending.CardObjects["UNIT"] with { ObjectGeneration = pending.CardObjects["UNIT"].ObjectGeneration + 1 } } };
        else pending = pending with {
            PlayerZones = new Dictionary<string, PlayerZones>(pending.PlayerZones) {
                ["P1"] = pending.PlayerZones["P1"] with {
                    Base = pending.PlayerZones["P1"].Base.Where(id => id != "UNIT").ToArray(),
                    Battlefields = [..pending.PlayerZones["P1"].Battlefields, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(pending.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") } };
        var resolved = await OfficialGraveyardRecastTests.Top(pending);
        Assert.Empty(Robots(resolved.State)); Assert.Empty(resolved.State.StackItems);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
    }

    [Theory]
    [InlineData("RAD·009/167", "generation")] [InlineData("RAD·049/167", "generation")]
    [InlineData("RAD·009/167", "timing")] [InlineData("RAD·049/167", "timing")]
    public async Task RecoveryRejectsForgedCapturedSourceAndScoringTiming(string no, string mode)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        var item = Assert.Single(game.State.StackItems); var c = item.SourceUnitTokens!;
        var changed = mode == "generation" ? c with { Source = c.Source with { Generation = c.Source.Generation + 1 } }
            : c with { Timing = "SCORE", BattlefieldId = "BF" };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { SourceUnitTokens = changed }] }),
            error => error.Contains("source unit token"));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("reject-robot", player, command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Same(state, rejected.State);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(rejected.Snapshots));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
    }
}
