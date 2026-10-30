using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenDragonPeakTests
{
    private const string Fee = "BATTLEFIELD_UNIT_PLAY:BF";
    private const string Dragon = "OGN·131/298";

    [Theory]
    [InlineData("P1")] [InlineData("P2")] [InlineData(null)]
    public async Task EitherPlayerCanPayTwoGenericPowerRegardlessOfTheBattlefieldController(string? controller)
    {
        var initial = Position(Dragon, controller); var game = new Driver(initial);
        var prompt = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(prompt.Destinations!, d => d.Id == "BATTLEFIELD:BF");
        var play = new PlayCardCommand("UNIT", Dragon, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]);
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.GenericPower);
        await game.Act("P1", play);
        Assert.Equal("BF", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(20 - quote.Cost.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(20 - quote.Cost.GenericPower - quote.Cost.PowerByTrait.Values.Sum(), game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task TheOpponentsDragonUsesItsOwnersResourcesAndTheSameAdditionalCost()
    {
        var initial = HandDiscardObserverTests.Put(Position(Dragon, "P1"), "P2-DRAGON", Dragon, "P2", "HAND");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("P2-DRAGON", Dragon, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]));
        Assert.Equal("P2", game.State.CardObjects["P2-DRAGON"].ControllerId);
        Assert.Equal("BF", game.State.ObjectLocations["P2-DRAGON"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task AnUnpaidWrongOrRepeatedFeeAndANonDragonNeverMutateState()
    {
        var state = Position(Dragon, null);
        var play = new PlayCardCommand("UNIT", Dragon, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]);
        await Reject(state, "P1", play with { OptionalCosts = [] });
        await Reject(state, "P1", play with { OptionalCosts = [Fee, Fee] });
        await Reject(state, "P1", play with { OptionalCosts = ["BATTLEFIELD_UNIT_PLAY:OTHER"] });
        await Reject(state, "P1", play with { Destination = "BASE" });
        await Reject(state, "P1", play with { Destination = "BATTLEFIELD:OTHER" });
        await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } }, "P1", play);
        await Reject(Position("OGN·219/298", null), "P1", play with { CardNo = "OGN·219/298" });
        await Reject(state, "P2", play);
    }

    [Fact]
    public async Task AControlledPeakIsAnOrdinaryDestinationWhenItsOptionalCostIsNotChosen()
    {
        var game = new Driver(Position(Dragon, "P1"));
        var play = new PlayCardCommand("UNIT", Dragon, [], Destination: "BATTLEFIELD:BF");
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.GenericPower);
        await game.Act("P1", play); Assert.Equal("BF", game.State.ObjectLocations["UNIT"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task TheAdditionalCostCoexistsWithPrintedPowerAndHaste()
    {
        const string no = "OGN·001/298"; var game = new Driver(Position(no, null));
        var play = new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee, HasteOptionalCostNames.HasteReady]);
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(6, quote.Cost!.Mana); Assert.Equal(2, quote.Cost.GenericPower);
        Assert.True(quote.Cost.PowerByTrait.Values.Sum() >= 1);
        await game.Act("P1", play); Assert.False(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IgnoringAllCostsKeepsTheChosenPeakAndHasteBenefitsWithoutChargingTheirFees(bool haste)
    {
        const string no = "OGN·001/298";
        var initial = DeckPlaySequenceTests.TopCard(HookDeckPlayTests.Position("SFD·148/221"), "HOOK_TOP", no);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["F"] = OfficialGarenConquestBattlefieldCountTests.Printed("F", "VEN·157", "P1") with { ControllerId = null } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) } };
        var game = HookDeckPlayTests.Driver(initial, "free-peak-haste-" + haste);
        await HookDeckPlayTests.Activate(game); await game.Top();
        await DeckPlaySequenceTests.Select(game, "HOOK_TOP");
        Assert.True(game.State.PendingEffectPlay!.IgnoreAllCosts);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        var play = new PlayCardCommand("HOOK_TOP", no, [], Destination: "BATTLEFIELD:F",
            OptionalCosts: haste ? ["BATTLEFIELD_UNIT_PLAY:F", HasteOptionalCostNames.HasteReady] : ["BATTLEFIELD_UNIT_PLAY:F"]);
        await Reject(game.State, "P1", play with { Destination = "BASE" });
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower); Assert.Empty(quote.Cost.PowerByTrait);
        var prompt = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(prompt.Destinations!, d => d.Id == "BATTLEFIELD:F");
        await game.Act("P1", play);
        Assert.Equal("F", game.State.ObjectLocations["HOOK_TOP"].BattlefieldObjectId);
        Assert.Equal(!haste, game.State.CardObjects["HOOK_TOP"].IsExhausted);
        Assert.Equal(RunePool.Empty, game.State.RunePools["P1"]);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task OptionalAdditionalCostReducersApplyToThePeakFeeThroughTheSharedPaymentPlan()
    {
        var initial = HandDiscardObserverTests.Put(Position(Dragon, null), "SUPPORT", "SFD·149/221", "P1", "BASE");
        var game = new Driver(initial);
        var play = new PlayCardCommand("UNIT", Dragon, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]);
        await Reject(game.State, "P1", play);
        var reduction = "OPTIONAL_COST_REDUCTION:SUPPORT:0:" + Uri.EscapeDataString(Fee) + ":POWER-any";
        Assert.Contains(reduction, JsonSerializer.Serialize(game.PromptFor("P1")));
        play = play with { OptionalCosts = [Fee, reduction] };
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.GenericPower);
        await game.Act("P1", play); Assert.Equal(19, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task IndependentPeakFeesCanBothBePaidAndThePlayerChoosesOneInstructedLocation()
    {
        var initial = Position(Dragon, null);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER", "VEN·157", "P2") with { ControllerId = null } } };
        var game = new Driver(initial);
        var play = new PlayCardCommand("UNIT", Dragon, [], Destination: "BATTLEFIELD:OTHER", OptionalCosts: [Fee, "BATTLEFIELD_UNIT_PLAY:OTHER"]);
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.GenericPower);
        await game.Act("P1", play); Assert.Equal(16, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task ThePeakGrantsAPlaceButDoesNotGrantReactionTiming()
    {
        var initial = HandDiscardObserverTests.Put(Position("VEN·066", null), "DRAGON", Dragon, "P1", "HAND");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·066", ["ALLY"]));
        await Reject(game.State, "P1", new PlayCardCommand("DRAGON", Dragon, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]));
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task TimeFissureAndThePaidPeakPermitEitherPositiveInstructionButBothRoutesPayTheFee(string destination)
    {
        var initial = Position("VEN·066", null);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "UNL-118/219", "P1") } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·066", ["ALLY"])); await game.Top();
        Assert.Equal("ORIGINAL_LOCATION", game.State.PendingEffectPlay!.DestinationPolicy);
        var prompt = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Equal(["BASE", "BATTLEFIELD:BF"], prompt.Destinations!.Select(d => d.Id));
        var pool = game.State.RunePools["P1"];
        var play = new PlayCardCommand("ALLY", "UNL-118/219", [], Destination: destination, OptionalCosts: [Fee]);
        if (destination != "BASE") await Reject(game.State, "P1", play with { OptionalCosts = [] });
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(2, quote.Cost.GenericPower); Assert.Empty(quote.Cost.PowerByTrait);
        await game.Act("P1", play); Assert.Equal(pool.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(destination == "BASE" ? null : "BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Theory]
    [InlineData(true, "BF")] [InlineData(true, "OTHER")]
    [InlineData(false, "BF")] [InlineData(false, "OTHER")]
    public async Task HelpArrivesCombinesCompatibleLocationRequirementsAndOnlyAllowsAlternativesWhenTheyConflict(bool controlledPeak, string chosen)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Dragon, controlledPeak ? "P1" : null), "HELP", "SFD·111/221", "P1");
        initial = HandDiscardObserverTests.Put(initial, "OTHER-ALLY", "OGN·219/298", "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "OTHER-ALLY", "P1", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HELP", "SFD·111/221", [])); await game.Top();
        Assert.Equal("CONTROLLED_BATTLEFIELD", game.State.PendingEffectPlay!.DestinationPolicy);
        var play = new PlayCardCommand("UNIT", Dragon, [], Destination: "BATTLEFIELD:" + chosen, OptionalCosts: [Fee]);
        if (controlledPeak && chosen == "OTHER")
        {
            await Reject(game.State, "P1", play);
            chosen = "BF"; play = play with { Destination = "BATTLEFIELD:BF" };
        }
        var pool = game.State.RunePools["P1"];
        var quote = Quote(game.State, play); Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.GenericPower);
        await game.Act("P1", play);
        Assert.Equal(chosen, game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.Equal(pool.TotalPower - 2, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task APlayerCanRefuseThePaidPeakRouteOfAnOtherwiseFreeMandatoryReplay()
    {
        var initial = Position("VEN·066", null);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", Dragon, "P1") } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·066", ["ALLY"])); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Banished); Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Theory]
    [InlineData("BF")] [InlineData("OTHER")]
    public async Task RoostingDarkDrakeCanOnlyUseTheExactBattlefieldActuallyConqueredThisTurn(string conquered)
    {
        const string no = "SFD·015/221"; var initial = Position(no, null);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = null } } };
        var game = new Driver(initial);
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:" + conquered, []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Contains(new BattlefieldConquestRecord("P1", new(conquered, game.State.CardObjects[conquered].ObjectGeneration)), game.State.BattlefieldConquests!.Entries);
        if (conquered != "BF") await Reject(game.State, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]));
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:" + conquered));
        Assert.Equal(conquered, game.State.ObjectLocations["UNIT"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task PreviousTurnConquestsCannotAuthorizeADarkDrakeEvenIfThePeakRemainsControlled()
    {
        const string no = "SFD·015/221"; var game = new Driver(Position(no, null));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Null(game.State.BattlefieldConquests);
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF", OptionalCosts: [Fee]));
        await Reject(game.State, "P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF")); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsAConquestInventedInTheSnapshotEvenWithAllViewsRebuilt()
    {
        var game = new Driver(Position("SFD·015/221", null));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        var bad = game.State with { BattlefieldConquests = new(game.State.TurnNumber, [new("P1", new("OTHER", 0))]) };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events(), options), options)!;
        var sequence = events[^1].Sequence; var snapshots = ResolutionResult.BuildSnapshots(bad); var prompts = ResolutionResult.BuildPrompts(bad);
        var views = bad.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, bad.Tick, sequence, snapshots[p], bad.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(bad.RoomId, bad.Tick, sequence, [], bad);
        var errors = MatchRecoveryValidator.Validate(bad.RoomId, sequence, game.Commands(), events, views, bad, bad.Tick, spectator);
        Assert.Contains("turn battlefield conquests differ from accepted history", errors);
    }

    [Fact]
    public async Task LegacyDarkDrakeBaseFixtureNowRejectsThatProhibitedDestination()
    {
        var fixture = await ConformanceFixture.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "p2-preflight-play-roosting-dark-drake-dragon-static.fixture.json"), default);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), default);
        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("P1-UNIT-ROOSTING-DARK-DRAKE", result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task TheCatalogExposesTheOptionalUnitPlayLocationAndItsDragonFilter()
    {
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        var play = CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName)).ToArray();
        var implementations = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(runtime.Cards, play);
        var spec = BehaviorSpecCatalogBuilder.Build(runtime.Cards, FunctionalUnitBuilder.Build(runtime.Cards), implementations)
            .Single(s => s.CardNo == "VEN·157");
        var ability = Assert.Single(spec.StaticAbilities); Assert.Equal(2, ability.Amount);
        Assert.Equal(StaticAbilityKinds.BattlefieldFilteredUnitPlayAdditionalPower, ability.Kind);
        Assert.Equal(StaticAuraTargetFilters.TagPrefix + "龙", ability.TargetFilter);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
    }

    private static MatchState Position(string no, string? controller)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "VEN·157", "P1") with { ControllerId = controller } } };
        return controller is null ? state : LocalDestructionRecallTests.At(state, controller == "P1" ? "ALLY" : "ENEMY", controller, "BATTLEFIELD", "BF");
    }
    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-peak", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
