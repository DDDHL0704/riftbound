using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven072ConditionalMightConsumerTests
{
    private const string No = "VEN·072";
    private const string Text = "{{迅捷}}（可在你的回合或法术对决中打出。）\n给予一名单位在本回合内{{S}}+2。如果该单位{{已强化}}，则改为给予其在本回合内{{S}}+4。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCn072SourceAndItsSwiftUnitTargetRouteAreRegistered()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37766, card.Id); Assert.Equal("深沉咆哮", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal([RuneTrait.Orange], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.Equal(2, behavior.ManaCost);
        Assert.Equal(CardTargetScopes.AnyUnit, behavior.TargetScope); Assert.Equal(1, behavior.RequiredTargetCount);
        Assert.True(behavior.CanPlayDuringSpellDuel); Assert.False(behavior.CanPlayDuringPriority); Assert.Equal(2, behavior.PowerModifierAmount);
        Assert.Equal(4, behavior.PowerModifierAmountIfTargetEmpowered);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealHowlUsesTwoOrFourMightFromTheActualEmpoweredTarget(bool empowered)
    {
        var initial = Position(); var driver = await Enter(initial, empowered);
        Assert.Equal(empowered, driver.State.CardObjects["UNIT"].IsEmpowered);
        var before = driver.State.RunePools["P1"]; await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"]));
        Assert.Equal(before.Mana - 2, driver.State.RunePools["P1"].Mana); Assert.Equal(before.TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, driver.State.CardObjects["UNIT"].Power); await driver.Top();
        Save("ordinary-or-empowered-" + empowered, initial, driver);
        var delta = empowered ? 4 : 2; var modifier = Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(delta, modifier.RequestedPowerDelta); Assert.Equal(delta, modifier.PowerDelta);
        Assert.Equal(4 + delta, driver.State.CardObjects["UNIT"].Power); Assert.Equal(4 + delta + (empowered ? 1 : 0), Might(driver.State));
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(4, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(empowered, driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(empowered ? 5 : 4, Might(driver.State));
        await driver.Replay(); Save("expired-" + empowered, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TwoRealCopiesAddSeparateTemporaryLedgerEntriesWithoutChangingStatus(bool empowered)
    {
        var initial = Add(Position(), "SECOND", No, "P1"); var driver = await Enter(initial, empowered); var delta = empowered ? 4 : 2;
        foreach (var id in new[] { "HOWL", "SECOND" }) { await driver.Act("P1", new PlayCardCommand(id, No, ["UNIT"])); await driver.Top(); }
        var unit = driver.State.CardObjects["UNIT"]; Assert.Equal(2, unit.UntilEndOfTurnPowerModifiers.Count);
        Assert.All(unit.UntilEndOfTurnPowerModifiers, m => Assert.Equal(delta, m.RequestedPowerDelta)); Assert.Equal(4 + 2 * delta, unit.Power);
        Assert.Equal(empowered, unit.IsEmpowered); await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(4, driver.State.CardObjects["UNIT"].Power); Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(empowered ? 5 : 4, Might(driver.State)); await Export("two-copies-" + empowered, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealReturnBeforeResolutionCannotBuffTheNewHandIncarnation(bool empowered)
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298", "P1"); var driver = await Enter(initial, empowered);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration; await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Equal(generation + 1, driver.State.CardObjects["UNIT"].ObjectGeneration); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        JsonRestore(initial, driver); await driver.Top(); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(4, driver.State.CardObjects["UNIT"].Power); Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        Assert.Null(driver.State.CardObjects["UNIT"].RuleDerivedTags); await Export("real-return-old-generation-" + empowered, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealNegativeReactionPreservesRawArithmeticAndDoesNotReplaceTheEmpoweredConditionWithCurrentMight(bool empowered)
    {
        var initial = Add(Position(), "NEGATIVE", "UNL-066/219", "P2"); var driver = await Enter(initial, empowered);
        await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"])); await driver.Top();
        Assert.Equal(-6, driver.State.CardObjects["UNIT"].Power); Assert.Equal(0, Might(driver.State));
        await driver.Top(); var delta = empowered ? 4 : 2; var unit = driver.State.CardObjects["UNIT"];
        Assert.Equal(-6 + delta, unit.Power); Assert.Equal(0, Might(driver.State)); Assert.Equal(empowered, unit.IsEmpowered);
        Assert.Equal(new[] { -10, delta }, unit.UntilEndOfTurnPowerModifiers.Select(m => m.RequestedPowerDelta));
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(4, driver.State.CardObjects["UNIT"].Power); Assert.Equal(empowered ? 5 : 4, Might(driver.State));
        await Export("real-negative-arithmetic-" + empowered, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheRealMovementDuelAllowsSwiftAndUsesTheCurrentTargetStatus(bool empowered)
    {
        var initial = Add(Position(), "READY", "UNL-009/219", "P1"); var driver = await Enter(initial, empowered);
        await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await driver.Top();
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState);
        if (driver.State.FocusPlayerId != "P1") await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); await driver.Top();
        var delta = empowered ? 4 : 2; Assert.Equal(4 + delta, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(delta, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        await Export("actual-movement-duel-" + empowered, initial, driver);
    }

    [Fact]
    public async Task TheOpponentsActualMovementDuelAllowsSwiftOnEitherSideOfTheBoard()
    {
        var initial = Position() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var driver = new Driver(initial); var printed = initial.CardObjects["ALLY"].Power;
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P2", new PassFocusCommand()); await driver.Act("P1", new PlayCardCommand("HOWL", No, ["ALLY"])); await driver.Top();
        Assert.Equal(printed + 2, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(2, Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        await Export("off-turn-real-duel", initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealWholeSpellCounterPreventsTheModifierWithoutRefundingMana(bool empowered)
    {
        var initial = Add(Position(), "COUNTER", "OGN·045/298", "P2"); var driver = await Enter(initial, empowered); var mana = driver.State.RunePools["P1"].Mana;
        await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); var stack = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand()); await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [stack.StackItemId])); await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers); Assert.Equal(4, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(mana - 2, driver.State.RunePools["P1"].Mana); Assert.Equal(empowered ? 5 : 4, Might(driver.State));
        await Export("actual-whole-counter-" + empowered, initial, driver);
    }

    [Theory]
    [InlineData("mana")] [InlineData("equipment")] [InlineData("battlefield")] [InlineData("graveyard")]
    [InlineData("missing")] [InlineData("extra")] [InlineData("none")] [InlineData("source-graveyard")]
    [InlineData("wrong-turn")] [InlineData("priority")] [InlineData("duel-focus")]
    public async Task ActualIllegalCastsLeaveTheSourcePaymentViewsPromptsAndModifierStateUnchanged(string branch)
    {
        var initial = Add(Position(), "SECOND", No, "P1");
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = initial.RunePools["P1"] with { Mana = 5 } } };
        if (branch == "duel-focus") initial = Add(initial, "READY", "UNL-009/219", "P1");
        var driver = await Enter(initial, false); var source = "HOWL";
        if (branch == "source-graveyard") {
            await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); await driver.Top();
            Assert.Contains("HOWL", driver.State.PlayerZones["P1"].Graveyard);
        }
        if (branch == "wrong-turn") await driver.Act("P1", new EndTurnCommand());
        if (branch == "priority") { await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); source = "SECOND"; }
        if (branch == "duel-focus") {
            await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await driver.Top();
            await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:OTHER", []));
            if (driver.State.FocusPlayerId == "P1") await driver.Act("P1", new PassFocusCommand());
            Assert.Equal("P2", driver.State.FocusPlayerId);
        }
        var targets = branch switch { "equipment" => new[] { "GEAR" }, "battlefield" => ["BF"], "graveyard" => ["GRAVE"],
            "missing" => ["MISSING"], "extra" => ["UNIT", "ALLY"], "none" => [], _ => ["UNIT"] };
        await Reject(driver, "P1", new PlayCardCommand(source, No, targets));
        if (branch == "source-graveyard") Assert.Equal(2, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        else Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        await Export("illegal-" + branch, initial, driver);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ARealEnemyBuffStillPaysTwoWardBeforeTheConditionalFourMight(int selectedRunes)
    {
        var initial = Add(Position("VEN·122"), "ENEMY-HOWL", No, "P2");
        for (var i = 0; i < 2; i++) initial = OnBase(initial, "WARD-RUNE-" + i, "UNL-R02", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = RunePool.Empty } };
        var driver = await Enter(initial, true, "VEN·122"); await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new TapRuneCommand("WARD-RUNE-0")); await driver.Act("P2", new TapRuneCommand("WARD-RUNE-1"));
        Assert.Equal(2, driver.State.RunePools["P2"].Mana); Assert.Equal(0, driver.State.RunePools["P2"].TotalPower);
        var command = new PlayCardCommand("ENEMY-HOWL", No, ["UNIT"], OptionalCosts: Enumerable.Range(0, selectedRunes).Select(i => "RECYCLE_RUNE:WARD-RUNE-" + i).ToArray());
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", PlayCostPreviewTests.Request(driver.State, command));
        Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(2, quote.Cost.GenericPower); Assert.Equal(selectedRunes == 2, quote.CanPay);
        Assert.Equal(0, quote.Cost.MissingMana); Assert.Equal(2 - selectedRunes, quote.Cost.MissingPower);
        if (selectedRunes == 1) {
            await Reject(driver, "P2", command); Assert.Contains("ENEMY-HOWL", driver.State.PlayerZones["P2"].Hand);
            Assert.DoesNotContain("WARD-RUNE-0", driver.State.PlayerZones["P2"].RuneDeck); Assert.Equal(3, driver.State.CardObjects["UNIT"].Power);
        } else {
            await driver.Act("P2", command); Assert.Equal(2, driver.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
            Assert.Equal(0, driver.State.RunePools["P2"].TotalPower); await driver.Top();
            Assert.Equal(7, driver.State.CardObjects["UNIT"].Power); Assert.Equal(8, Might(driver.State));
            Assert.Equal(4, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        }
        await Export("enemy-buff-two-ward-" + selectedRunes, initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheUntouchedOriginalLogRejectsACoordinatedInventedModifierAndPower(bool empowered)
    {
        var initial = Position(); var driver = await Enter(initial, empowered); await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); await driver.Top();
        var unit = driver.State.CardObjects["UNIT"]; var modifier = Assert.Single(unit.UntilEndOfTurnPowerModifiers); var expected = empowered ? 4 : 2;
        Assert.Equal(expected, modifier.RequestedPowerDelta); var wrong = empowered ? 2 : 4; var shift = wrong - expected;
        var changed = modifier with { PowerDelta = wrong, RequestedPowerDelta = wrong, EffectivePower = modifier.EffectivePower + shift, ResultingPower = modifier.ResultingPower + shift };
        var forged = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
            ["UNIT"] = unit with { Power = unit.Power + shift, UntilEndOfTurnPowerModifiers = [changed] } } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); Assert.Equal(expected, driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers.Single().RequestedPowerDelta);
        await Export("forged-ledger-and-power-" + empowered, initial, driver);
    }

    [Fact]
    public async Task AnotherControlledEmpoweredUnitCannotEnableFourMightForAnOrdinaryTarget()
    {
        var initial = Position(); var driver = await Enter(initial, true); var printed = driver.State.CardObjects["ALLY"].Power;
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.False(driver.State.CardObjects["ALLY"].IsEmpowered);
        await driver.Act("P1", new PlayCardCommand("HOWL", No, ["ALLY"])); await driver.Top();
        Assert.Equal(printed + 2, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(2, Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        await Export("target-not-another-controlled-source", initial, driver);
    }

    [Fact]
    public async Task ActualSwiftIsRejectedWhileEmpowerIsPendingAndGivesFourAfterItResolves()
    {
        var initial = Position(); var driver = await Enter(initial, false);
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        await Reject(driver, "P1", new PlayCardCommand("HOWL", No, ["UNIT"])); Assert.Contains("HOWL", driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.State.StackItems); JsonRestore(initial, driver); await driver.Top(); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        await driver.Act("P1", new PlayCardCommand("HOWL", No, ["UNIT"])); await driver.Top();
        Assert.Equal(4, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        await Export("pending-empower-real-swift-rejection-then-four", initial, driver);
    }

    [Fact]
    public async Task ARealCopyKeepsItsOwnEmpowerStatusAndEachLaterHowlUsesItsActualTarget()
    {
        var initial = Add(Add(Add(ShadySpectaclesTests.Position("VEN·093") with { RoomId = "VEN072-REAL-COPY" }, "HOWL", No, "P1"), "SECOND", No, "P1"), "THIRD", No, "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["COPY-SOURCE"] = Printed("COPY-SOURCE", "VEN·093", "P1") } };
        Assert.Equal(4, initial.CardObjects["COPY-SOURCE"].Power);
        var driver = new Driver(initial); await driver.Act("P1", new ActivateAbilityCommand("COPY-SOURCE", "EMPOWER:0:0", [])); await driver.Top();
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await driver.Top();
        var pending = driver.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", pending.Request.Kind);
        await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("VEN·093", driver.State.CardObjects["HOST"].CardNo); Assert.False(driver.State.CardObjects["HOST"].IsEmpowered); Assert.True(driver.State.CardObjects["COPY-SOURCE"].IsEmpowered);
        await driver.Act("P1", new PlayCardCommand("HOWL", No, ["HOST"])); await driver.Top();
        Assert.Equal(2, Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        await driver.Act("P1", new PlayCardCommand("SECOND", No, ["COPY-SOURCE"])); await driver.Top();
        Assert.Equal(4, Assert.Single(driver.State.CardObjects["COPY-SOURCE"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var ability = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "HOST") && Equals(r["abilityId"], "EMPOWER:0:0"));
        await driver.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) { AbilityInstanceId = Assert.IsType<string>(ability["abilityInstanceId"]) }); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("THIRD", No, ["HOST"])); await driver.Top();
        Assert.Equal(new[] { 2, 4 }, driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers.Select(m => m.RequestedPowerDelta));
        Assert.True(driver.State.CardObjects["HOST"].IsEmpowered); JsonRestore(initial, driver);
        Save("real-copy-independent-statuses-before-expiry", initial, driver);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(4, driver.State.CardObjects["HOST"].Power); Assert.Equal(4, driver.State.CardObjects["COPY-SOURCE"].Power);
        await Export("real-copy-independent-statuses", initial, driver);
    }

    private static MatchState Position(string unitNo = "VEN·093")
    {
        var state = BasePosition();
        state = state with { RoomId = "VEN072-CONDITIONAL-MIGHT",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = Printed("UNIT", unitNo, "P1") } };
        return Add(state, "HOWL", No, "P1");
    }
    private static async Task<Driver> Enter(MatchState initial, bool empowered, string unitNo = "VEN·093")
    {
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", unitNo, []));
        if (empowered) { await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await driver.Top(); }
        return driver;
    }
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
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
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(driver.State)[player].PromptId; raw["snapshotTick"] = driver.State.Tick;
        await driver.ActRetained(new(player, "ven072-reject-" + driver.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), false);
    }
    private static void JsonRestore(MatchState initial, Driver driver)
    {
        var state = driver.State; var sequence = driver.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, driver.Commands(), driver.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), driver);
        foreach (var player in state.Seats.Keys) {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Export(string name, MatchState initial, Driver driver)
    { await driver.Replay(); JsonRestore(initial, driver); Save(name, initial, driver); }
    private static void Save(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN072_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), runtime = new { ruleInputs = RuleInputIdentity.Current, baseDirectory = AppContext.BaseDirectory, catalogPath = OfficialCardCatalog.DefaultCatalogPath(), modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }.Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
        }, Json));
    }

    private static MatchState BasePosition()
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", "VEN·093", "P1", "HAND"); Put("ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF"); Put("ENEMY", "OGN·219/298", "P2", "BASE");
        Put("GEAR", "SFD·022/221", "P1", "BASE"); Put("GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = "P1" }; cards["OTHER"] = cards["OTHER"] with { ControllerId = null };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var p in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 3).Select(i => p + "-DRAW-" + i).ToArray(); foreach (var id in deck) Put(id, "OGN·219/298", p, "MAIN_DECK");
            zones[p] = PlayerZones.Empty with { Base = p == "P1" ? ["GEAR"] : ["ENEMY"], Battlefields = p == "P1" ? ["BF", "ALLY"] : ["OTHER"],
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
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs, TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "ven072-" + entries.Count;
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
