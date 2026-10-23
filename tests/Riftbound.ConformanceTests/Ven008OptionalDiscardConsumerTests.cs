using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven008OptionalDiscardConsumerTests
{
    private const string No = "VEN·008";
    private const string Text = "{{迅捷}}（可在你的回合或法术对决中打出。）\n你可以选择弃置一张手牌，作为打出此牌的额外费用。\n对战场上的一名单位造成3点伤害。如果你支付了该额外费用，则改为对其造成5点伤害。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

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
    [InlineData(false)] [InlineData(true)]
    public async Task TheOpponentsRealMovementDuelAllowsEitherPrintedPaymentBranch(bool discard)
    {
        var initial = Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF"); initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
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
        var state = VenEmpoweredSelfConsumerTests.Position("VEN·093") with { RoomId = "VEN008-OPTIONAL-DISCARD" };
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
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
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
        }, Json));
    }
}
