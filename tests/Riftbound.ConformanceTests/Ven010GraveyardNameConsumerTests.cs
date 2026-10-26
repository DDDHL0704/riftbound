using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven010GraveyardNameConsumerTests
{
    private const string No = "VEN·010";
    private const string Text = "{{迅捷}}（可在你的回合或法术对决中打出。）\n对战场上的一名单位造成2点伤害。你的废牌堆中每有一张此牌的同名卡牌，此牌便造成1点额外伤害。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task ActualTypedCastKeepsItsGraveyardDamageArgumentsAcrossJournalProjection(int copies)
    {
        var initial = Position(copies); var run = new Driver(initial);
        await TypedCardJournalProjectionAssertions.Run("ven010-" + copies, initial, run, run.Commands, run.Events, Cast(), (state, events) =>
        {
            Assert.Equal(38, state.RunePools["P1"].Mana);
            var damage = Assert.Single(events, e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
            Assert.Equal(2 + copies, damage.Event.Payload["damage"]);
        });
    }

    [Fact]
    public async Task ExactCn010SourceAndItsOrdinarySwiftDamageRouteAreRegistered()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37695, card.Id); Assert.Equal("蚀骨诅咒", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal([RuneTrait.Red], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.Equal(2, behavior.ManaCost); Assert.Equal(2, behavior.DamageAmount);
        Assert.Equal(CardTargetScopes.BattlefieldUnit, behavior.TargetScope); Assert.Equal(1, behavior.RequiredTargetCount);
        Assert.True(behavior.CanPlayDuringSpellDuel); Assert.False(behavior.CanPlayDuringPriority);
        Assert.Equal(1, behavior.ExtraDamagePerSameNameInControllerGraveyard);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ARealCastCountsOnlyItsControllersCurrentGraveyardCopies(int copies)
    {
        var initial = Position(copies); var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("SPELL", No, ["ENEMY"]));
        Assert.Equal(38, driver.State.RunePools["P1"].Mana); Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, Assert.Single(driver.State.StackItems).DamageAmount); await driver.Top(); Save("resolved-" + copies, initial, driver);
        var damage = Assert.Single(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
        Assert.Equal(2 + copies, damage.Event.Payload["damage"]); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ARealReactionRecyclesMatchingCardsBeforeTheCountIsEvaluated(int removed)
    {
        var initial = Add(Position(2), "RECYCLE", "UNL-103/219", "P2"); var driver = new Driver(initial); await driver.Act("P1", Cast());
        Assert.Equal(2, driver.State.PlayerZones["P1"].Graveyard.Count(id => driver.State.CardObjects[id].CardNo == No));
        await driver.Act("P1", new PassPriorityCommand()); var targets = Enumerable.Range(0, removed).Select(i => "OWN-GRAVE-" + i).ToArray();
        await driver.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", targets, "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3")); await driver.Top();
        Assert.All(targets, id => Assert.Contains(id, driver.State.PlayerZones["P1"].MainDeck)); Assert.Equal(2, Assert.Single(driver.State.StackItems).DamageAmount);
        JsonRestore(initial, driver); await driver.Top(); Assert.Equal(4 - removed, Damage(driver)); await Export("real-recycle-current-count-" + removed, initial, driver);
    }

    [Fact]
    public async Task ARealReactionDiscardAddsAMatchingCardBeforeTheCountIsEvaluated()
    {
        var initial = Add(Add(Position(0), "MOON", "UNL-125/219", "P1"), "SECOND-COPY", No, "P1"); var driver = new Driver(initial); await driver.Act("P1", Cast());
        await driver.Act("P1", new PlayCardCommand("MOON", "UNL-125/219", ["SECOND-COPY"])); await driver.Top();
        Assert.Contains("SECOND-COPY", driver.State.PlayerZones["P1"].Graveyard); Assert.Equal(2, Assert.Single(driver.State.StackItems).DamageAmount);
        JsonRestore(initial, driver); await driver.Top(); Assert.Equal(3, Damage(driver)); await Export("real-discard-increases-current-count", initial, driver);
    }

    [Fact]
    public async Task ARealReversalUsesTheNewControllersGraveyardAndTheSpellStillReturnsToItsOwner()
    {
        var initial = Add(Position(2), "STEAL", "OGN·080/298", "P2"); var driver = new Driver(initial); await driver.Act("P1", Cast());
        var item = Assert.Single(driver.State.StackItems); await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("STEAL", "OGN·080/298", [item.StackItemId])); await driver.Top();
        var pending = driver.State.PendingRuleChoice!; Assert.Equal("SPELL_CHOICE_REMAKE", pending.Request.Kind);
        await driver.Act("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        Assert.Equal("P1", driver.State.CardObjects["SPELL"].OwnerId); Assert.Equal("P2", Assert.Single(driver.State.StackItems).ControllerId);
        JsonRestore(initial, driver); await driver.Top(); Assert.Equal(3, Damage(driver));
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P2"].Graveyard);
        await Export("real-reversal-current-controller", initial, driver);
    }

    [Fact]
    public async Task MatchingCardsInHandAndDeckAndAnotherRedSpellInGraveyardDoNotAddDamage()
    {
        var initial = Add(Position(0), "SECOND-COPY", No, "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["DECK-COPY"] = Printed("DECK-COPY", No, "P1"), ["OTHER-SPELL"] = Printed("OTHER-SPELL", "VEN·008", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["DECK-COPY"] = new("P1", "MAIN_DECK"), ["OTHER-SPELL"] = new("P1", "GRAVEYARD") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { MainDeck = [..p.Value.MainDeck, "DECK-COPY"], Graveyard = [..p.Value.Graveyard, "OTHER-SPELL"] } : p.Value) };
        var driver = new Driver(initial); await driver.Act("P1", Cast()); await driver.Top(); Assert.Equal(2, Damage(driver));
        await Export("same-name-only-current-graveyard", initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task TheExistingControllerBonusAddsOnceAfterTheCurrentSameNameCount(int count)
    {
        var initial = OnBase(Position(count), "ANNIE", "OGS·001/024", "P1"); var driver = new Driver(initial); await driver.Act("P1", Cast());
        Assert.Equal(2, Assert.Single(driver.State.StackItems).DamageAmount); await driver.Top(); Assert.Equal(3 + count, Damage(driver));
        await Export("existing-bonus-after-current-count-" + count, initial, driver);
    }

    [Theory]
    [InlineData("mana")] [InlineData("base")] [InlineData("gear")] [InlineData("extra-target")]
    [InlineData("no-target")] [InlineData("unsupported-discard")] [InlineData("wrong-turn")]
    public async Task IllegalCastsCannotSpendMoveTheSourceOrChangeEitherGraveyard(string branch)
    {
        var initial = Position(1);
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = initial.RunePools["P1"] with { Mana = 1 } } };
        if (branch == "wrong-turn") initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var targets = branch switch { "base" => new[] { "ALLY" }, "gear" => ["GEAR"], "extra-target" => ["ENEMY", "ALLY"], "no-target" => [], _ => ["ENEMY"] };
        var command = new PlayCardCommand("SPELL", No, targets, OptionalCosts: branch == "unsupported-discard" ? ["DISCARD_HAND_CARD:UNIT"] : []);
        var driver = new Driver(initial); await Reject(driver, "P1", command); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Hand);
        await Export("illegal-" + branch, initial, driver);
    }

    [Fact]
    public async Task ARealWholeSpellCounterPaysOnceAndDoesNotApplyAnyGraveyardDamage()
    {
        var initial = Add(Position(2), "COUNTER", "OGN·045/298", "P2"); var driver = new Driver(initial); await driver.Act("P1", Cast());
        var item = Assert.Single(driver.State.StackItems); await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item.StackItemId])); await driver.Top();
        Assert.Equal(38, driver.State.RunePools["P1"].Mana); Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard); await Export("actual-whole-counter", initial, driver);
    }

    [Fact]
    public async Task ARealTargetReturnCannotRebindTheDamageToItsNewHandGeneration()
    {
        var initial = Add(Position(2), "RETURN", "OGN·104/298", "P2"); var driver = new Driver(initial); var generation = initial.CardObjects["ENEMY"].ObjectGeneration;
        await driver.Act("P1", Cast()); await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await driver.Top();
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Hand); Assert.Equal(generation + 1, driver.State.CardObjects["ENEMY"].ObjectGeneration);
        JsonRestore(initial, driver); await driver.Top(); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
        await Export("actual-return-new-generation", initial, driver);
    }

    [Fact]
    public async Task TheOriginalLogRejectsACoordinatedInventedGraveyardAndCapturedDamage()
    {
        var initial = Add(Position(0), "SECOND-COPY", No, "P1"); var driver = new Driver(initial); await driver.Act("P1", Cast()); JsonRestore(initial, driver);
        var item = Assert.Single(driver.State.StackItems); var forged = driver.State with {
            StackItems = [item with { DamageAmount = 3 }],
            PlayerZones = driver.State.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = p.Value.Hand.Where(id => id != "SECOND-COPY").ToArray(), Graveyard = [..p.Value.Graveyard, "SECOND-COPY"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(driver.State.ObjectLocations) { ["SECOND-COPY"] = new("P1", "GRAVEYARD") } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); Assert.Contains("SECOND-COPY", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, driver.State.StackItems.Single().DamageAmount); await Export("original-log-forged-count-and-amount", initial, driver);
    }

    [Theory]
    [InlineData("OGN·039/298", "OGN·039a/298", true)]
    [InlineData("OGN·039/298", "OGN·112/298", false)]
    [InlineData("VEN·086", "VEN·181", true)]
    [InlineData("VEN·010", "VEN·008", false)]
    [InlineData("VEN·010", "蚀骨诅咒", false)]
    [InlineData("UNKNOWN", "UNKNOWN", false)]
    [InlineData(null, null, false)]
    [InlineData("VEN·010", "VEN·010", true)]
    public void KnownNamesUseTheWholeSubtitleAndNeverTreatAnUnknownDisplayFallbackAsAName(string? first, string? second, bool expected)
        => Assert.Equal(expected, SameKnownName(first, second));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task AnActualEmpoweredEnemyRequiresWardBeforeTheIndependentGraveyardIncrement(int count)
    {
        var initial = Position(count) with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ENEMY"] = Printed("ENEMY", "VEN·122", "P2") },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        for (var i = 0; i < 2; i++) initial = OnBase(initial, "WARD-RUNE-" + i, "UNL-R01", "P1");
        var driver = new Driver(initial); await driver.Act("P2", new ActivateAbilityCommand("ENEMY", "EMPOWER:0:0", [])); await driver.Top();
        await driver.Act("P2", new EndTurnCommand()); await driver.Act("P1", new TapRuneCommand("WARD-RUNE-0")); await driver.Act("P1", new TapRuneCommand("WARD-RUNE-1"));
        var command = new PlayCardCommand("SPELL", No, ["ENEMY"], OptionalCosts: ["RECYCLE_RUNE:WARD-RUNE-0", "RECYCLE_RUNE:WARD-RUNE-1"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(2, quote.Cost.GenericPower);
        await driver.Act("P1", command); Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, driver.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        await driver.Top(); Assert.Equal(2 + count, Damage(driver)); await Export("actual-ward-with-current-count-" + count, initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task TheOpponentsRealMovementDuelAllowsSwiftWithTheActorsOwnGraveyardCount(int count)
    {
        var initial = Position(count) with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = At(initial, "ALLY", "P1", "BATTLEFIELD", "BF"); initial = At(initial, "ENEMY", "P2", "BASE");
        var driver = new Driver(initial); await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P2", new PassFocusCommand()); await driver.Act("P1", Cast()); await driver.Top();
        Assert.Equal(2 + count, Damage(driver)); await Export("off-turn-real-duel-current-count-" + count, initial, driver);
    }

    private static PlayCardCommand Cast() => new("SPELL", No, ["ENEMY"]);
    private static bool SameKnownName(string? first, string? second)
    {
        var type = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.OfficialCardSourceIdentityGroups"); Assert.NotNull(type);
        var method = type.GetMethod("SameKnownName", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public); Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [first, second]));
    }
    private static int Damage(Driver driver) => Assert.IsType<int>(Assert.Single(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).Event.Payload["damage"]);
    private static MatchState Position(int count)
    {
        var state = BasePosition() with { RoomId = "VEN010-GRAVEYARD-NAME" };
        state = At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards["ENEMY"] = Printed("ENEMY", "VEN·093", "P2"); cards["SPELL"] = Printed("SPELL", No, "P1"); locations["SPELL"] = new("P1", "HAND"); zones["P1"] = zones["P1"] with { Hand = [..zones["P1"].Hand, "SPELL"] };
        for (var i = 0; i < count; i++) { var id = "OWN-GRAVE-" + i; cards[id] = Printed(id, No, "P1"); locations[id] = new("P1", "GRAVEYARD"); zones["P1"] = zones["P1"] with { Graveyard = [..zones["P1"].Graveyard, id] }; }
        cards["OTHER-GRAVE"] = Printed("OTHER-GRAVE", No, "P2"); locations["OTHER-GRAVE"] = new("P2", "GRAVEYARD"); zones["P2"] = zones["P2"] with { Graveyard = ["OTHER-GRAVE"] };
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
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
        await driver.ActRetained(new(player, "ven010-reject-" + driver.Commands().Length, JsonSerializer.SerializeToElement(node, Json)), false);
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
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN010_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new { initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), runtime = new { ruleInputs = RuleInputIdentity.Current, baseDirectory = AppContext.BaseDirectory, catalogPath = OfficialCardCatalog.DefaultCatalogPath(), modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }.Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) } }, Json));
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
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs, ErrorCode = e.ErrorCode }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "ven010-" + entries.Count;
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
