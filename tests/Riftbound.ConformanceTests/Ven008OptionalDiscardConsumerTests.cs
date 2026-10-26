using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven008OptionalDiscardConsumerTests
{
    private const string No = "VEN·008";
    private const string Text = "{{迅捷}}（可在你的回合或法术对决中打出。）\n你可以选择弃置一张手牌，作为打出此牌的额外费用。\n对战场上的一名单位造成3点伤害。如果你支付了该额外费用，则改为对其造成5点伤害。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualTypedCastKeepsItsOriginalOptionalDiscardAcrossJournalProjection(bool discard)
    {
        var initial = Position(); var run = new Driver(initial);
        await TypedCardJournalProjectionAssertions.Run("ven008-" + discard, initial, run, run.Commands, run.Events, Cast(discard), (state, events) =>
        {
            Assert.Equal(37, state.RunePools["P1"].Mana);
            Assert.Equal(discard, state.PlayerZones["P1"].Graveyard.Contains("FODDER"));
            var damage = Assert.Single(events, e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
            Assert.Equal(discard ? 5 : 3, damage.Event.Payload["damage"]);
        });
    }

    [Fact]
    public async Task ExactCn008SourceAndItsOrdinarySwiftDamageRouteAreRegistered()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37693, card.Id); Assert.Equal("无情打击", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal([RuneTrait.Red], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.Equal(3, behavior.ManaCost); Assert.Equal(3, behavior.DamageAmount);
        Assert.Equal(CardTargetScopes.BattlefieldUnit, behavior.TargetScope); Assert.Equal(1, behavior.RequiredTargetCount);
        Assert.True(behavior.CanPlayDuringSpellDuel); Assert.False(behavior.CanPlayDuringPriority);
        Assert.Equal(5, behavior.DamageAmountIfDiscardHandCardOptionalCost);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealCastPaysItsSelectedCostAndDealsThreeOrFiveDamage(bool discard)
    {
        var initial = Position(); var driver = new Driver(initial); var pool = driver.State.RunePools["P1"];
        var command = new PlayCardCommand("SPELL", No, ["ENEMY"], OptionalCosts: discard ? ["DISCARD_HAND_CARD:FODDER"] : []);
        try { await driver.Act("P1", command); } finally { Save("declared-" + discard, initial, driver); }
        Assert.Equal(pool.Mana - 3, driver.State.RunePools["P1"].Mana); Assert.Equal(pool.TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(discard, driver.State.PlayerZones["P1"].Graveyard.Contains("FODDER")); Assert.Equal(!discard, driver.State.PlayerZones["P1"].Hand.Contains("FODDER"));
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage); await driver.Top(); Save("resolved-" + discard, initial, driver);
        var damage = Assert.Single(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
        Assert.Equal(discard ? 5 : 3, damage.Event.Payload["damage"]); await driver.Replay();
    }

    [Fact]
    public void OptionalDiscardPromptListsOnlyTheActorsEligibleOtherHandCards()
    {
        var state = Add(Position(), "OPPONENT-FODDER", "VEN·093", "P2");
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var row = Assert.Single(rows, r => Equals(r["sourceObjectId"], "SPELL"));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["optionalCostChoices"]);
        Assert.Equal(new[] { "DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:UNIT" }, choices.Select(c => c.Id).Where(id => id.StartsWith("DISCARD_HAND_CARD:", StringComparison.Ordinal)).Order());
        foreach (var view in new object[] { ResolutionResult.BuildPrompts(state)["P2"], ResolutionResult.BuildSpectatorSnapshot(state) }) {
            var json = JsonSerializer.Serialize(view, view.GetType(), Json);
            Assert.DoesNotContain("DISCARD_HAND_CARD:FODDER", json); Assert.DoesNotContain("DISCARD_HAND_CARD:UNIT", json);
        }
    }

    [Fact]
    public async Task TheActualDiscardAndManaRemainPaidWhenTheWholeSpellIsCountered()
    {
        var initial = Add(Position(), "COUNTER", "OGN·045/298", "P2"); var driver = new Driver(initial);
        await driver.Act("P1", Cast(true)); Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Graveyard);
        var item = Assert.Single(driver.State.StackItems); Assert.Equal(5, item.DamageAmount); Assert.Equal(37, driver.State.RunePools["P1"].Mana);
        JsonRestore(initial, driver); await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item.StackItemId])); await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(37, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
        await Export("counter-keeps-actual-extra-cost", initial, driver);
    }

    [Fact]
    public async Task ARealReactionRecyclesTheDiscardedCardBeforeResolutionWithoutErasingThePaidFact()
    {
        var initial = Add(Position(), "RECYCLE", "UNL-103/219", "P2"); var driver = new Driver(initial); await driver.Act("P1", Cast(true));
        await driver.Act("P1", new PassPriorityCommand());
        try {
            await driver.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["FODDER"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await driver.Top();
        } finally { Save("recycled-cost-card-frontier", initial, driver); }
        Assert.Contains("FODDER", driver.State.PlayerZones["P1"].MainDeck); Assert.DoesNotContain("FODDER", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(5, Assert.Single(driver.State.StackItems).DamageAmount); JsonRestore(initial, driver); await driver.Top();
        Assert.Equal(5, Damage(driver)); await Export("paid-fact-survives-real-recycle", initial, driver);
    }

    [Theory]
    [InlineData("mana")] [InlineData("source")] [InlineData("opponent")] [InlineData("base")]
    [InlineData("graveyard")] [InlineData("missing")] [InlineData("duplicate")] [InlineData("two")]
    public async Task ActualInvalidExtraPaymentsRejectWithoutDiscardingOrSpendingAnything(string branch)
    {
        var initial = Add(Position(), "OPPONENT-FODDER", "VEN·093", "P2");
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = initial.RunePools["P1"] with { Mana = 2 } } };
        if (branch == "graveyard") initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = p.Value.Hand.Where(id => id != "FODDER").ToArray(), Graveyard = [..p.Value.Graveyard, "FODDER"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FODDER"] = new("P1", "GRAVEYARD") } };
        var id = branch switch { "source" => "SPELL", "opponent" => "OPPONENT-FODDER", "base" => "ALLY", "missing" => "MISSING", _ => "FODDER" };
        var costs = branch == "duplicate" ? new[] { "DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:FODDER" }
            : branch == "two" ? ["DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:UNIT"] : ["DISCARD_HAND_CARD:" + id];
        var command = new PlayCardCommand("SPELL", No, ["ENEMY"], OptionalCosts: costs); var driver = new Driver(initial);
        if (branch == "mana") {
            var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
            Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.MissingMana);
        }
        await Reject(driver, "P1", command);
        if (branch == "mana") Assert.DoesNotContain("Unsupported optional cost", driver.Commands().Last().ErrorMessage!);
        else Assert.Contains(branch is "two" or "duplicate" ? "Unsupported optional cost" : "requires a different hand card", driver.Commands().Last().ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Hand); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        await Export("invalid-extra-payment-" + branch, initial, driver);
    }

    [Theory]
    [InlineData("base")] [InlineData("equipment")] [InlineData("missing")] [InlineData("extra")]
    [InlineData("none")] [InlineData("wrong-turn")] [InlineData("priority")] [InlineData("grave-source")]
    public async Task InvalidTargetsAndTimingCannotMoveTheSourceOrDealDamage(string branch)
    {
        var initial = Add(Position(), "SECOND", No, "P1");
        if (branch == "wrong-turn") initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var driver = new Driver(initial); var source = "SPELL";
        if (branch == "priority") { await driver.Act("P1", Cast(false)); source = "SECOND"; }
        if (branch == "grave-source") { await driver.Act("P1", Cast(false)); await driver.Top(); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); }
        var targets = branch switch { "base" => new[] { "ALLY" }, "equipment" => ["GEAR"], "missing" => ["MISSING"], "extra" => ["ENEMY", "ALLY"], "none" => [], _ => ["ENEMY"] };
        var before = driver.State.CardObjects["ENEMY"].Damage; await Reject(driver, "P1", new PlayCardCommand(source, No, targets));
        Assert.Equal(before, driver.State.CardObjects["ENEMY"].Damage); await Export("illegal-target-timing-" + branch, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnOrdinaryControllerBonusAppliesToTheCapturedThreeOrFiveBaseExactlyOnce(bool discard)
    {
        var initial = OnBase(Position(), "ANNIE", "OGS·001/024", "P1"); var driver = new Driver(initial); await driver.Act("P1", Cast(discard));
        Assert.Equal(discard ? 5 : 3, Assert.Single(driver.State.StackItems).DamageAmount); await driver.Top();
        Assert.Equal(discard ? 6 : 4, Damage(driver)); await Export("existing-controller-bonus-" + discard, initial, driver);
    }

    [Theory]
    [InlineData("SFD·149/221", false)] [InlineData("SFD·149/221", true)]
    [InlineData("SFD·149a/221", false)] [InlineData("SFD·149a/221", true)]
    public async Task EzrealCannotReduceThePrintedManaOrReplaceTheOptionalDiscard(string reducer, bool discard)
    {
        var initial = OnBase(Position(), "EZREAL", reducer, "P1");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(reducer, out var behavior));
        Assert.True(behavior.MayReduceOtherCardOptionalAdditionalCostByOneManaOrPower);
        var play = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            play.Metadata!["sourceRequirements"]), r => Equals(r["sourceObjectId"], "SPELL"));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["optionalCostChoices"]);
        Assert.Contains(choices, c => c.Id == "DISCARD_HAND_CARD:FODDER");
        Assert.DoesNotContain(choices, c => c.Id.StartsWith("OPTIONAL_COST_REDUCTION:", StringComparison.Ordinal));
        var command = Cast(discard);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        var driver = new Driver(initial); await driver.Act("P1", command);
        Assert.Equal(initial.RunePools["P1"].Mana - 3, driver.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(discard, driver.State.PlayerZones["P1"].Graveyard.Contains("FODDER"));
        Assert.Equal(!discard, driver.State.PlayerZones["P1"].Hand.Contains("FODDER"));
        Assert.Equal(discard ? 5 : 3, Assert.Single(driver.State.StackItems).DamageAmount);
        await driver.Top(); Assert.Equal(discard ? 5 : 3, Damage(driver));
        await Export("ezreal-keeps-discard-" + reducer.Replace('/', '_') + "-" + discard, initial, driver);
    }

    [Theory]
    [InlineData("SFD·149/221", "MANA", false)] [InlineData("SFD·149/221", "MANA", true)]
    [InlineData("SFD·149/221", "POWER-any", false)] [InlineData("SFD·149/221", "POWER-any", true)]
    [InlineData("SFD·149a/221", "MANA", false)] [InlineData("SFD·149a/221", "MANA", true)]
    [InlineData("SFD·149a/221", "POWER-any", false)] [InlineData("SFD·149a/221", "POWER-any", true)]
    public async Task ForgingEzrealResourceReductionForTheDiscardRejectsWithoutPayment(string reducer, string mode, bool discard)
    {
        var initial = OnBase(Position(), "EZREAL", reducer, "P1"); var before = MatchStateHasher.Hash(initial);
        var reduction = "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:" + mode;
        var command = Cast(discard) with { OptionalCosts = discard ? ["DISCARD_HAND_CARD:FODDER", reduction] : [reduction] };
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("ezreal-cannot-pay-discard", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        var driver = new Driver(initial); await Reject(driver, "P1", command);
        Assert.Equal(before, MatchStateHasher.Hash(driver.State));
        Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Hand);
        Assert.Empty(driver.Events());
        await Export("ezreal-invalid-discard-reduction-" + reducer.Replace('/', '_') + "-" + mode + "-" + discard, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task VoluntaryBlueAcquisitionKeepsThePrintedManaAndActualDiscardPayment(bool discard)
    {
        var initial = VoluntaryPaymentResourceChoicesTests.WithRune(Position(), RuneTrait.Blue);
        var command = Cast(discard) with { OptionalCosts = discard
            ? ["DISCARD_HAND_CARD:FODDER", "RECYCLE_RUNE:EXTRA-RUNE"] : ["RECYCLE_RUNE:EXTRA-RUNE"] };
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower); Assert.Empty(quote.Cost.PowerByTrait);
        var driver = new Driver(initial); await driver.Act("P1", command);
        Assert.Equal(initial.RunePools["P1"].Mana - 3, driver.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower + 1, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(1, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Contains("EXTRA-RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(discard, driver.State.PlayerZones["P1"].Graveyard.Contains("FODDER"));
        Assert.Equal(!discard, driver.State.PlayerZones["P1"].Hand.Contains("FODDER"));
        await driver.Top(); Assert.Equal(discard ? 5 : 3, Damage(driver));
        await Export("voluntary-blue-keeps-discard-" + discard, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheOpponentsRealMovementDuelAllowsEitherPrintedPaymentBranch(bool discard)
    {
        var initial = Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = At(initial, "ALLY", "P1", "BATTLEFIELD", "BF"); initial = At(initial, "ENEMY", "P2", "BASE");
        var driver = new Driver(initial); await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P2", new PassFocusCommand()); await driver.Act("P1", Cast(discard)); await driver.Top();
        Assert.Equal(discard ? 5 : 3, Damage(driver)); await Export("off-turn-real-duel-" + discard, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealEmpoweredEnemyStillRequiresWardAlongsideManaAndTheSelectedDiscard(bool discard)
    {
        var initial = Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ENEMY"] = Printed("ENEMY", "VEN·122", "P2") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        for (var i = 0; i < 3; i++) initial = OnBase(initial, "WARD-RUNE-" + i, "UNL-R01", "P1");
        var driver = new Driver(initial); await driver.Act("P2", new ActivateAbilityCommand("ENEMY", "EMPOWER:0:0", [])); await driver.Top();
        await driver.Act("P2", new EndTurnCommand());
        for (var i = 0; i < 3; i++) await driver.Act("P1", new TapRuneCommand("WARD-RUNE-" + i));
        var costs = discard ? new[] { "DISCARD_HAND_CARD:FODDER", "RECYCLE_RUNE:WARD-RUNE-0", "RECYCLE_RUNE:WARD-RUNE-1" } : ["RECYCLE_RUNE:WARD-RUNE-0", "RECYCLE_RUNE:WARD-RUNE-1"];
        var command = new PlayCardCommand("SPELL", No, ["ENEMY"], OptionalCosts: costs);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(2, quote.Cost.GenericPower);
        await driver.Act("P1", command); Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, driver.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        await driver.Top(); Assert.Equal(discard ? 5 : 3, Damage(driver)); await Export("ward-with-confirmed-cost-" + discard, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheUntouchedOriginalLogRejectsACoordinatedForgedAmountAndDiscardFields(bool discard)
    {
        var initial = Position(); var driver = new Driver(initial); await driver.Act("P1", Cast(discard)); JsonRestore(initial, driver);
        var item = Assert.Single(driver.State.StackItems); Assert.Equal(discard ? 5 : 3, item.DamageAmount);
        var forged = driver.State with { StackItems = [item with { DamageAmount = discard ? 3 : 5, OptionalCosts = discard ? [] : ["DISCARD_HAND_CARD:FODDER"] }] };
        if (!discard) forged = forged with {
            PlayerZones = forged.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = p.Value.Hand.Where(id => id != "FODDER").ToArray(), Graveyard = [..p.Value.Graveyard, "FODDER"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(forged.ObjectLocations) { ["FODDER"] = new("P1", "GRAVEYARD") } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); Assert.Equal(discard ? 5 : 3, driver.State.StackItems.Single().DamageAmount);
        await Export("original-log-forged-paid-fields-" + discard, initial, driver);
    }

    [Fact]
    public async Task TheExistingDiscardManaReducerKeepsItsOriginalQuoteAndPayment()
    {
        var initial = Add(Position(), "RUDE", "OGN·002/298", "P1"); var command = new PlayCardCommand("RUDE", "OGN·002/298", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        var driver = new Driver(initial); await driver.Act("P1", command); Assert.Equal(36, driver.State.RunePools["P1"].Mana);
        Assert.Contains("RUDE", driver.State.PlayerZones["P1"].Base); Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(5, driver.State.CardObjects["RUDE"].Power); await Export("existing-discard-mana-reduction-control", initial, driver);
    }

    private static PlayCardCommand Cast(bool discard) => new("SPELL", No, ["ENEMY"], OptionalCosts: discard ? ["DISCARD_HAND_CARD:FODDER"] : []);
    private static int Damage(Driver driver) => Assert.IsType<int>(Assert.Single(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).Event.Payload["damage"]);
    private static MatchState Position()
    {
        var state = BasePosition() with { RoomId = "VEN008-OPTIONAL-DISCARD" };
        state = At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SPELL"] = Printed("SPELL", No, "P1"), ["FODDER"] = Printed("FODDER", "VEN·093", "P1"), ["ENEMY"] = Printed("ENEMY", "VEN·093", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SPELL"] = new("P1", "HAND"), ["FODDER"] = new("P1", "HAND") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, "SPELL", "FODDER"] } : p.Value) };
    }
    private static CardObjectState Printed(string id, string no, string owner) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static MatchState Add(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == owner ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value) };
    private static MatchState OnBase(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "BASE") },
        PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == owner ? p.Value with { Base = [..p.Value.Base, id] } : p.Value) };
    private static async Task Reject(Driver driver, string player, GameCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); node["promptId"] = ResolutionResult.BuildPrompts(driver.State)[player].PromptId; node["snapshotTick"] = driver.State.Tick;
        await driver.ActRetained(new(player, "ven008-reject-" + driver.Commands().Length, JsonSerializer.SerializeToElement(node, Json)), false);
    }
    private static void JsonRestore(MatchState initial, Driver driver)
    {
        var state = driver.State; var sequence = driver.Commands().Last().CompletedEventSequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, driver.Commands(), driver.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events, decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), driver);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Export(string name, MatchState initial, Driver driver) { await driver.Replay(); JsonRestore(initial, driver); Save(name, initial, driver); }
    private static void Save(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN008_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), runtime = new { ruleInputs = RuleInputIdentity.Current, baseDirectory = AppContext.BaseDirectory, catalogPath = OfficialCardCatalog.DefaultCatalogPath(), modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }.Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
        }, Json));
    }

    private static MatchState At(MatchState state, string id, string player, string zone, string? field = null)
    {
        var moved = LocalDestructionRecallTests.At(state, id, player, zone, field);
        var cards = moved.CardObjects.ToDictionary();
        foreach (var battlefield in new[] { "BF", "OTHER" }) {
            var controllers = moved.ObjectLocations.Where(p => p.Value.Zone == "BATTLEFIELD" && p.Value.BattlefieldObjectId == battlefield
                && cards.TryGetValue(p.Key, out var card) && card.Tags.Contains(CardObjectTags.UnitCard))
                .Select(p => cards[p.Key].ControllerId ?? p.Value.PlayerId).Distinct(StringComparer.Ordinal).ToArray();
            cards[battlefield] = cards[battlefield] with { ControllerId = controllers.Length == 1 ? controllers[0] : null };
        }
        return moved with { CardObjects = cards };
    }

    private static MatchState BasePosition()
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", "VEN·093", "P1", "HAND"); Put("ALLY", "OGN·219/298", "P1", "BASE"); Put("ENEMY", "OGN·219/298", "P2", "BASE");
        Put("GEAR", "SFD·022/221", "P1", "BASE"); Put("GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = null }; cards["OTHER"] = cards["OTHER"] with { ControllerId = null };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var p in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 3).Select(i => p + "-DRAW-" + i).ToArray(); foreach (var id in deck) Put(id, "OGN·219/298", p, "MAIN_DECK");
            zones[p] = PlayerZones.Empty with { Base = p == "P1" ? ["GEAR", "ALLY"] : ["ENEMY"], Battlefields = p == "P1" ? ["BF"] : ["OTHER"],
                Hand = p == "P1" ? ["UNIT"] : [], Graveyard = p == "P1" ? ["GRAVE"] : [], MainDeck = deck };
        }
        return new MatchState("VEN-UNIT-ENTRY", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, runePools: new Dictionary<string, RunePool> { ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Red] = 4 }), ["P2"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Green] = 4 }) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }
    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
        private MatchSession session;
        internal MatchState State { get; private set; }
        internal Driver(MatchState initial) { this.initial = initial; State = initial; session = new(initial, new CoreRuleEngine(), this); }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { entries.Add(entry); return ValueTask.CompletedTask; }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "ven008-" + entries.Count;
            var result = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var seq = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, seq, snapshots[p], State.Tick, seq, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, seq, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, seq, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, seq, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(duplicate.Accepted);
            Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(hash, MatchStateHasher.Hash(State));
            await Replay();
        }
        internal async Task ActRetained(ConformanceCommand retained, bool expectedAccepted)
        {
            var before = MatchStateHasher.Hash(State); var snapshotsBefore = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State));
            var promptsBefore = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State));
            var raw = retained.Cmd.Clone(); var command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
            Assert.Equal(expectedAccepted, result.Accepted); State = result.State;
            if (!expectedAccepted) {
                Assert.Equal(before, MatchStateHasher.Hash(State)); Assert.Empty(result.Events);
                Assert.Equal(snapshotsBefore, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(State)));
                Assert.Equal(promptsBefore, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(State)));
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence, snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(), views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, Commands(), Events(), views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            if (expectedAccepted) {
                var count = entries.Count; var hash = MatchStateHasher.Hash(State);
                var duplicate = await session.SubmitAsync(retained.PlayerId, retained.ClientIntentId, command, raw, default);
                Assert.True(duplicate.Accepted); Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            }
            await Replay();
        }
        internal async Task Top() { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task Replay()
        {
            var result = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State, new CoreRuleEngine(), default, Events());
            Assert.True(result.IsMatch, string.Join("; ", result.Errors));
        }
    }
}
