using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadRobotReplacementTests
{
    private const string Hero = "RAD·132/167", Robot = "SFD·T01";

    private static MatchState WithHero(MatchState state, string player = "P1", string field = "BFZ", string id = "HEIMER")
        => LocalDestructionRecallTests.At(HandDiscardObserverTests.Put(state, id, Hero, player, "BASE"),
            id, player, "BATTLEFIELD", field);

    private static MatchState Position(string card = "OGN·094/298", int copies = 0, int heroes = 1)
    {
        var state = OfficialTokenReplacementTests.Position(copies, card);
        for (var i = 0; i < heroes; i++) state = WithHero(state, id: "HEIMER" + i);
        return state with { RoomId = "RAD-ROBOT-REPLACEMENT" };
    }

    private static CardObjectState[] Tokens(MatchState state) => OfficialTokenReplacementTests.Tokens(state);
    private static async Task Choose(Driver game, string kind, string option)
    {
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(kind, choice.Request.Kind);
        Assert.Contains(choice.Request.Options, candidate => candidate.Id == option);
        await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [option]));
    }

    private static async Task Base(Driver game)
    {
        while (game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" })
            await Choose(game, "TOKEN_PLAY_LOCATION", "BASE");
    }

    private static async Task Cast(Driver game, string card, params string[] targets)
    {
        await game.Act("P1", new PlayCardCommand("C", card, targets));
        await game.Top(); await Base(game);
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-robot", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Fact]
    public async Task OfficialHeroPaysSixManaAndYellowPowerAndEntersDormantWithoutCreatingAToken()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == Hero);
        Assert.Equal(39637, card.Id); Assert.Equal("黑默丁格", card.CardName); Assert.Equal("学院导师", card.SubTitle);
        Assert.Equal(6, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(5, card.Power);
        var initial = VenUnitEntryBasicTests.Position(Hero);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(6, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) } };
        var game = new Driver(initial); var play = new PlayCardCommand("UNIT", Hero, [], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(6, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        await game.Act("P1", play);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        var hero = game.State.CardObjects["UNIT"];
        Assert.Equal(5, hero.Power); Assert.True(hero.IsExhausted); Assert.Contains("约德尔人", hero.Tags);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Empty(Tokens(game.State)); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("mana")] [InlineData("power")] [InlineData("trait")] [InlineData("target")] [InlineData("turn")]
    public async Task InvalidHeroPaymentTargetsAndTimingLeaveAllStateAndViewsUnchanged(string branch)
    {
        var state = VenUnitEntryBasicTests.Position(Hero);
        if (branch is "mana" or "power" or "trait") state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(branch == "mana" ? 5 : 6, 0,
                new Dictionary<string, int> { [branch == "trait" ? RuneTrait.Blue : RuneTrait.Yellow] = branch == "power" ? 0 : 1 }) } };
        if (branch == "turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await Reject(state, "P1", new PlayCardCommand("UNIT", Hero, branch == "target" ? ["ALLY"] : [], Destination: "BASE"));
    }

    [Theory]
    [InlineData("base")] [InlineData("hidden")] [InlineData("standby")] [InlineData("opponent")] [InlineData("unlocated")]
    public async Task OnlyTheControllersPublicUnitAtARealBattlefieldOffersTheReplacement(string branch)
    {
        var state = Position(); var hero = state.CardObjects["HEIMER0"];
        if (branch == "base") state = LocalDestructionRecallTests.At(state, "HEIMER0", "P1", "BASE");
        if (branch is "hidden" or "standby") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [hero.ObjectId] = hero with { IsFaceDown = true,
                Tags = branch == "standby" ? hero.Tags.Append(CardObjectTags.Standby).ToArray() : hero.Tags } } };
        if (branch == "opponent") state = LocalDestructionRecallTests.At(state, "HEIMER0", "P2", "BATTLEFIELD", "BFZ");
        if (branch == "unlocated") state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            [hero.ObjectId] = new("P1", "BATTLEFIELD", "MISSING") } };
        // Invalid fixture locations are deliberately checked at the engine boundary.
        var cast = await OfficialGraveyardRecastTests.Act(state, "P1", new PlayCardCommand("C", "OGN·094/298", []));
        var result = await OfficialGraveyardRecastTests.Top(cast.State);
        while (result.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } choice)
            result = await OfficialGraveyardRecastTests.Act(result.State, "P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        Assert.Null(result.State.PendingRuleChoice); Assert.Single(Tokens(result.State));
        Assert.DoesNotContain(Tokens(result.State), token => token.TokenFactoryCardNo == Robot);
    }

    [Fact]
    public async Task EveryOriginalTokenIsOptionalAndTheSourceCanReplaceAgainInTheSameTurn()
    {
        var initial = HandDiscardObserverTests.Put(Position("UNL-069/219"), "SECOND", "SFD·031/221", "P1", "HAND");
        var game = new Driver(initial); await Cast(game, "UNL-069/219");
        Assert.Empty(Tokens(game.State));
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY"); await Base(game);
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "DECLINE");
        Assert.Equal(2, Tokens(game.State).Length);
        Assert.Single(Tokens(game.State), token => token.TokenFactoryCardNo == Robot);
        Assert.All(Tokens(game.State), token => { Assert.False(token.IsExhausted); Assert.Contains(CardObjectTags.Ephemeral, token.Tags); });
        var robot = Assert.Single(Tokens(game.State), token => token.TokenFactoryCardNo == Robot);
        Assert.Equal(3, robot.Power); Assert.Contains("机械", robot.Tags); Assert.DoesNotContain("仙灵", robot.Tags);
        await game.Act("P1", new PlayCardCommand("SECOND", "SFD·031/221", [])); await game.Top(); await Base(game);
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        Assert.Equal(3, Tokens(game.State).Length);
        var latest = Assert.Single(Tokens(game.State), token => token.ObjectId.StartsWith("SECOND", StringComparison.Ordinal));
        Assert.Equal(Robot, latest.TokenFactoryCardNo); Assert.True(latest.IsExhausted);
        Assert.DoesNotContain(CardObjectTags.Ephemeral, latest.Tags); Assert.DoesNotContain(CardObjectTags.SandSoldier, latest.Tags);
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", game.State.CardObjects["HEIMER0"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task TheControllerOrdersCopyAndRobotReplacementsAndLaterReplacementsSeeAllResults(bool robotFirst)
    {
        var game = new Driver(Position(copies: 1)); await Cast(game, "OGN·094/298");
        var order = game.State.PendingRuleChoice!;
        await Choose(game, "TOKEN_REPLACEMENT_ORDER", Assert.Single(order.Request.Options,
            option => option.Id.StartsWith(robotFirst ? "ROBOT:" : "COPY:", StringComparison.Ordinal)).Id);
        await Base(game); Assert.Empty(Tokens(game.State));
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        if (!robotFirst) await Choose(game, "UNIT_TOKEN_REPLACEMENT", "DECLINE");
        Assert.Equal(2, Tokens(game.State).Length);
        Assert.Equal(robotFirst ? 2 : 1, Tokens(game.State).Count(token => token.TokenFactoryCardNo == Robot));
        Assert.All(Tokens(game.State), token => { Assert.False(token.IsExhausted); Assert.Contains(CardObjectTags.Ephemeral, token.Tags); });
        Assert.Contains("TOKEN_ENTRY_REPLACEMENT_USED", game.State.CardObjects["Z1"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task TwoRobotSourcesTerminateWithoutReapplyingTheSameReplacementToItsOwnResults()
    {
        var game = new Driver(Position(heroes: 2)); await Cast(game, "OGN·094/298");
        await Choose(game, "TOKEN_REPLACEMENT_ORDER", game.State.PendingRuleChoice!.Request.Options[1].Id);
        await Base(game);
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        var robot = Assert.Single(Tokens(game.State)); Assert.Equal(Robot, robot.TokenFactoryCardNo);
        Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ChoicesCannotBeStolenOrInventedAndRecoveryRejectsAChangedTranscript()
    {
        var game = new Driver(Position()); await Cast(game, "OGN·094/298");
        var pending = game.State.PendingRuleChoice!;
        await Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["APPLY"]));
        await Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["ROBOT:FORGED"]));
        var forged = game.State with { PendingRuleChoice = pending with { Request = pending.Request with {
            Options = [new("APPLY", "伪造来源", ["TARGET"]), new("DECLINE", "不替换")] } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), error => error.Contains("replacement choice", StringComparison.OrdinalIgnoreCase));
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY"); await game.Replay();
    }

    [Fact]
    public async Task AReplacedImageStillReceivesItsReflexiveCopyWithoutKeepingTheRobotsPrintedPowerOrTraits()
    {
        var game = new Driver(Position("UNL-200/219")); await Cast(game, "UNL-200/219", "TARGET");
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        var token = Assert.Single(Tokens(game.State)); Assert.Equal(3, token.Power); Assert.Equal(Robot, token.CardNo);
        var item = Assert.Single(game.State.StackItems); Assert.NotNull(item.ReflexiveCopy);
        Assert.Equal(Robot, item.ReflexiveCopy!.RecipientFactories![token.ObjectId]);
        var forged = game.State with { StackItems = [item with { ReflexiveCopy = item.ReflexiveCopy with {
            RecipientFactories = new Dictionary<string, string> { [token.ObjectId] = "OGN·096/298" } } }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), error => error.Contains("reflexive copy", StringComparison.Ordinal));
        await game.Top(); token = game.State.CardObjects[token.ObjectId];
        Assert.Equal("SFD·068/221", token.CardNo); Assert.Equal(Robot, token.TokenFactoryCardNo); Assert.Equal(3, token.Power);
        Assert.Contains(CardObjectTags.Ephemeral, token.Tags); Assert.DoesNotContain("机械", token.Tags);
        await game.Replay();
    }

    [Fact]
    public async Task ARealCopyOfTheHeroGainsItsReplacementOnlyAfterItHasEnteredAndReachedABattlefield()
    {
        var initial = HandDiscardObserverTests.Put(Position("UNL-200/219"), "SECOND", "SFD·031/221", "P1", "HAND");
        var game = new Driver(initial); await Cast(game, "UNL-200/219", "HEIMER0");
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "DECLINE"); await game.Top();
        var copy = Assert.Single(Tokens(game.State)); Assert.Equal(Hero, copy.CardNo);
        // Returning the printed source leaves only the copied rules text.
        await game.Act("P1", new MoveUnitCommand("HEIMER0", Destination: "BASE:P1"));
        await game.Act("P1", new MoveUnitCommand(copy.ObjectId, Destination: "BATTLEFIELD:BFZ"));
        await game.Act("P1", new PlayCardCommand("SECOND", "SFD·031/221", [])); await game.Top(); await Base(game);
        var pending = game.State.PendingRuleChoice!; Assert.Equal("UNIT_TOKEN_REPLACEMENT", pending.Request.Kind);
        Assert.Equal([copy.ObjectId], Assert.Single(pending.Request.Options, option => option.Id == "APPLY").ObjectIds);
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        Assert.Single(Tokens(game.State), token => token.TokenFactoryCardNo == Robot); await game.Replay();
    }

    [Fact]
    public async Task LastBreathRetainsItsExplicitBaseDormancyAndGrantedWardOnTheRobot()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "DEAD", "UNL-153/219", "P1", "BASE");
        initial = HandDiscardObserverTests.Put(initial, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["DEAD"]));
        await game.Top(); await game.Top();
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        var robot = Assert.Single(Tokens(game.State)); Assert.Equal(Robot, robot.TokenFactoryCardNo);
        Assert.True(robot.IsExhausted); Assert.Contains(CardObjectTags.Spellshield, robot.Tags);
        Assert.DoesNotContain("鸟类", robot.Tags); Assert.Equal(new("P1", "BASE"), game.State.ObjectLocations[robot.ObjectId]);
        await game.Replay();
    }

    [Fact]
    public async Task HeldBattlefieldUsesTheSameReplacementBeforePlayingItsDormantBaseToken()
    {
        var initial = Position() with { Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BFZ"] = initial.CardObjects["BFZ"] with { CardNo = "OGN·275/298" } } };
        var game = new Driver(initial); await game.Act("P1", new PassPriorityCommand()); await game.Top();
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        var robot = Assert.Single(Tokens(game.State)); Assert.Equal(Robot, robot.TokenFactoryCardNo);
        Assert.True(robot.IsExhausted); Assert.Equal(new("P1", "BASE"), game.State.ObjectLocations[robot.ObjectId]);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·265/298")] [InlineData("UNL-189/219")]
    public async Task LegendTokenInstructionsKeepTheirReadyStateAndExplicitKeywords(string legend)
    {
        var initial = WithHero(OfficialLegendTokenTests.Position(legend, 0));
        var game = new Driver(initial); await game.Act("P1", new LegendActCommand("LEGEND",
            legend == "OGN·265/298" ? LegendActionAbilityCatalog.ViktorLegendAbilityId : LegendActionAbilityCatalog.LilliaLegendAbilityId,
            [], [legend == "OGN·265/298" ? "SPEND_MANA:1" : "SPEND_MANA:4"]));
        await game.Top(); await Base(game); await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        var robot = Assert.Single(Tokens(game.State)); Assert.Equal(Robot, robot.TokenFactoryCardNo);
        Assert.Equal(legend == "OGN·265/298", robot.IsExhausted);
        Assert.Equal(legend == "UNL-189/219", robot.Tags.Contains(CardObjectTags.Ephemeral));
        Assert.DoesNotContain(CardObjectTags.MinionTokenFamily, robot.Tags); await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD")]
    public async Task MovementReplacementsKeepTheCapturedOriginAndInstructionEphemeral(string origin)
    {
        var initial = WithHero(MovementTokenTests.Position("UNL-082/219", origin), "P2", "BF");
        var game = new Driver(initial);
        await game.Act("P1", ChosenMovementTests.Command("BATTLEFIELD:BF2", "D"));
        await game.Top(); await game.Top();
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        var robot = Assert.Single(Tokens(game.State)); Assert.Equal(Robot, robot.TokenFactoryCardNo);
        Assert.True(robot.IsExhausted); Assert.Contains(CardObjectTags.Ephemeral, robot.Tags); Assert.DoesNotContain("仙灵", robot.Tags);
        Assert.Equal(new ObjectLocationState("P2", origin, origin == "BASE" ? null : "BF"), game.State.ObjectLocations[robot.ObjectId]);
        await game.Replay();
    }

    [Fact]
    public async Task FluftPoroReplacesEachWarhawkWhileRetainingTheInstructionsGrantedWard()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "PORO", P4ActivatedAbilityCatalog.FluftPoroCardNo, "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "PORO", "P1", "BATTLEFIELD", "BFZ");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("PORO", P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, []));
        await game.Top();
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "APPLY");
        await Choose(game, "UNIT_TOKEN_REPLACEMENT", "DECLINE");
        Assert.Equal(2, Tokens(game.State).Length); Assert.Single(Tokens(game.State), token => token.TokenFactoryCardNo == Robot);
        Assert.All(Tokens(game.State), token => { Assert.False(token.IsExhausted); Assert.Contains(CardObjectTags.Spellshield, token.Tags); });
        await game.Replay();
    }
}

public sealed class RadRobotReplacementParserTests
{
    private const string Text = "如果你将打出一个单位指示物到某一位置，只要我位于战场，你便可以选择改为打出一名3{{S}}的“机器人”单位指示物到该位置。";

    [Theory]
    [InlineData(Text)] [InlineData("  " + Text + "\n")]
    public void TheOfficialTextParsesTheBattlefieldOnlyOptionalReplacement(string text)
    {
        var spec = Assert.Single(StaticAbilityParser.Parse(text, []));
        Assert.Equal(StaticAbilityKinds.BattlefieldUnitTokenRobotReplacement, spec.Kind); Assert.Equal(3, spec.Amount);
    }

    [Theory]
    [InlineData("装备指示物")] [InlineData("2{{S}}")] [InlineData("只要我位于基地")]
    [InlineData("强制改为")]
    public void DifferentObjectsStatsLocationsAndMandatoryEffectsDoNotMatch(string change)
    {
        var text = change switch {
            "装备指示物" => Text.Replace("单位指示物", change, StringComparison.Ordinal),
            "2{{S}}" => Text.Replace("3{{S}}", change, StringComparison.Ordinal),
            "只要我位于基地" => Text.Replace("只要我位于战场", change, StringComparison.Ordinal),
            _ => Text.Replace("你便可以选择改为", change, StringComparison.Ordinal) };
        Assert.DoesNotContain(StaticAbilityParser.Parse(text, []), spec => spec.Kind == StaticAbilityKinds.BattlefieldUnitTokenRobotReplacement);
    }
}
