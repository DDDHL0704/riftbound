using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven093And122EmpoweredSelfConsumerTests
{
    private static readonly string[] Cards = ["VEN·093", "VEN·122"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static int PrintedMight(string no) => no == "VEN·093" ? 4 : 3;
    private static string Text(string no) => no == "VEN·093"
        ? "{{强化2}}（支付{{2}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+1和{{游走}}。（我可以向其他战场进行移动。）"
        : "{{强化2}}（支付{{2}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+1和{{法盾2}}。（对手必须支付{{A}}{{A}}才能将我选作法术或技能的目标。）";

    [Theory]
    [InlineData("VEN·093", 37790, "均衡渡命人", "purple", "")]
    [InlineData("VEN·122", 37822, "烈阳之鹰", "yellow", "鸟类")]
    public async Task ExactCn093And122SourcesAndWholePrintedSelfProfilesAreRegistered(string no, long id, string name, string trait, string tag)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal(Text(no), card.CardEffect);
        Assert.Equal([trait], card.CardColorList); Assert.Equal(PrintedMight(no), card.Energy); Assert.Equal(PrintedMight(no), card.Power);
        Assert.Null(card.ReturnEnergy); Assert.Equal(tag, card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(PrintedMight(no), behavior.ManaCost); Assert.Equal(PrintedMight(no), behavior.SourceUnitPower);
        Assert.Equal(tag, behavior.SourceUnitTags ?? "");
        var spec = Assert.Single(EmpowerParser.Parse(card.CardEffect)); var cost = Assert.Single(spec.Alternatives);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust); Assert.False(spec.ReduceManaPerRune);
        Assert.Equal(2, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(1, self.Might);
        Assert.Equal(no == "VEN·093" ? ["游走"] : new[] { "法盾2" }, self.Keywords);
    }

    public static IEnumerable<object[]> Destinations() => Cards.SelectMany(no => new[] { "BASE", "BATTLEFIELD:BF" }.Select(d => new object[] { no, d }));
    [Theory]
    [MemberData(nameof(Destinations))]
    public async Task RealPlayAndActivationPaySeparatelyAndPermanentSelfEffectsSurviveBothTurns(string no, string destination)
    {
        var initial = Position(no); var driver = await Play(initial, no, destination); var pool = driver.State.RunePools["P1"];
        Assert.Equal(40 - PrintedMight(no), pool.Mana); Assert.Equal(PrintedMight(no), Might(driver.State));
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(driver.State.CardObjects["UNIT"].Tags.Intersect([CardObjectTags.Spellshield, "法盾2", CardCombatKeywordNames.Roam]));
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", Activate()); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(PrintedMight(no), Might(driver.State)); Assert.Equal(pool.Mana - 2, driver.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(driver.State.StackItems).EmpowerActivation!.PaidMana); Assert.Empty(driver.State.StackItems.Single().TargetObjectIds);
        JsonRestore(initial, driver); await driver.Top(); CheckSelf(driver.State, no);
        await driver.Act("P1", new EndTurnCommand()); CheckSelf(driver.State, no);
        await driver.Act("P2", new EndTurnCommand()); CheckSelf(driver.State, no);
        Assert.Equal(generation, driver.State.CardObjects["UNIT"].ObjectGeneration);
        await Export($"permanent-{no}-{destination.Replace(':', '-')}", initial, driver);
    }

    public static IEnumerable<object[]> Returns() => Cards.SelectMany(no => new[] { false, true }.Select(b => new object[] { no, b }));
    [Theory]
    [MemberData(nameof(Returns))]
    public async Task RealReturnBeforeOrAfterResolutionClearsSelfEffectsAndKeepsTheOldGenerationBound(string no, bool before)
    {
        var initial = Add(Position(no), "RETURN", "OGN·104/298", "P1"); var driver = await Play(initial, no);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", Activate()); if (!before) { await driver.Top(); CheckSelf(driver.State, no); }
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, driver.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(driver.State.CardObjects["UNIT"].RuleDerivedTags);
        Assert.Equal(PrintedMight(no), Might(driver.State));
        Assert.Empty(driver.State.CardObjects["UNIT"].Tags.Intersect([CardObjectTags.Spellshield, "法盾2", CardCombatKeywordNames.Roam]));
        if (before) {
            Assert.Equal(generation, Assert.Single(driver.State.StackItems).EmpowerActivation!.Source.Generation);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(driver.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(driver.State) }) {
                var row = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().Single();
                Assert.Equal("HIDDEN", row.GetProperty("sourceObjectId").GetString()); Assert.Equal("HIDDEN", row.GetProperty("sourceVisibility").GetString());
                Assert.Equal(no, row.GetProperty("cardNo").GetString()); Assert.False(row.TryGetProperty("empowerActivation", out _));
            }
            JsonRestore(initial, driver); await driver.Top(); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        }
        Assert.Empty(driver.State.StackItems); await Export($"return-{no}-{before}", initial, driver);
    }

    public static IEnumerable<object[]> IllegalActivations() => Cards.SelectMany(no => new[] { "mana", "controller", "repeated", "target" }.Select(b => new object[] { no, b }));
    [Theory]
    [MemberData(nameof(IllegalActivations))]
    public async Task EachIllegalActivationIsActuallyLoggedWithoutPartialPaymentOrSelfEffects(string no, string branch)
    {
        var initial = Position(no);
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = initial.RunePools["P1"] with { Mana = PrintedMight(no) + 1 } } };
        if (branch == "controller") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = Printed("ENEMY", no, "P2") } };
        var driver = await Play(initial, no); if (branch == "repeated") await Empower(driver);
        await Reject(driver, "P1", new ActivateAbilityCommand(branch == "controller" ? "ENEMY" : "UNIT", "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []));
        Assert.Equal(branch == "mana" ? "资源不足，无法支付强化费用。" : branch == "target"
            ? "强化来源不是目标，请选择当前存在的技能与费用。" : "请在你的开放主阶段选择一名尚未强化的公开单位。", driver.Commands().Last().ErrorMessage);
        await Export($"illegal-{no}-{branch}", initial, driver);
    }

    [Fact]
    public async Task Actual093ActivationEnablesRoamAndThePermissionSurvivesTurnEnd()
    {
        const string no = "VEN·093"; var initial = Add(Position(no), "READY", "UNL-009/219", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = null } } };
        var driver = await Play(initial, no, "BATTLEFIELD:BF"); await driver.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await driver.Top();
        var move = new MoveUnitCommand("UNIT", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]);
        await Reject(driver, "P1", move); await Empower(driver); CheckSelf(driver.State, no);
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", move); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand()); await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act("P1", new EndTurnCommand()); await driver.Act("P2", new EndTurnCommand()); CheckSelf(driver.State, no);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD:OTHER", "BATTLEFIELD:BF", ["ROAM"]));
        Assert.Equal("BF", driver.State.ObjectLocations["UNIT"].BattlefieldObjectId); Assert.Equal(generation, driver.State.CardObjects["UNIT"].ObjectGeneration);
        await Export("actual093-permanent-roam", initial, driver);
    }

    [Theory]
    [MemberData(nameof(Returns))]
    public async Task ActualEnemyReactionPaysTwoWardOnlyForEmpowered122AndItsNegativeMightExpires(string no, bool empowered)
    {
        var initial = Add(Add(Position(no), "OPEN", "VEN·081", "P1"), "NEGATIVE", "UNL-066/219", "P2"); var driver = await Play(initial, no);
        if (empowered) await Empower(driver);
        await driver.Act("P1", new PlayCardCommand("OPEN", "VEN·081", ["ALLY"])); await driver.Act("P1", new PassPriorityCommand());
        var command = new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"]); var tax = no == "VEN·122" && empowered ? 2 : 0;
        var before = driver.State.RunePools["P2"]; var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(7, quote.Cost!.Mana); Assert.Equal(tax, quote.Cost.GenericPower);
        await driver.Act("P2", command); Assert.Equal(before.Mana - 7, driver.State.RunePools["P2"].Mana);
        Assert.Equal(before.TotalPower - tax, driver.State.RunePools["P2"].TotalPower);
        Assert.Equal(tax, driver.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        await driver.Top(); Assert.Equal(PrintedMight(no) - 10, driver.State.CardObjects["UNIT"].Power); Assert.Equal(0, Might(driver.State));
        Assert.Equal(-10, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await driver.Top(); await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(empowered, driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(PrintedMight(no), driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(PrintedMight(no) + (empowered ? 1 : 0), Might(driver.State)); await Export($"actual-tax-{no}-{empowered}", initial, driver);
    }

    [Fact]
    public async Task ActualEnemyPlayCannotPartlyPayOnePowerAgainstTwoWard()
    {
        const string no = "VEN·122"; var initial = Add(Add(Position(no), "OPEN", "VEN·081", "P1"), "NEGATIVE", "UNL-066/219", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 1) } };
        var driver = await Play(initial, no); await Empower(driver);
        await driver.Act("P1", new PlayCardCommand("OPEN", "VEN·081", ["ALLY"])); await driver.Act("P1", new PassPriorityCommand());
        var command = new PlayCardCommand("NEGATIVE", "UNL-066/219", ["UNIT"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", PlayCostPreviewTests.Request(driver.State, command));
        Assert.False(quote.CanPay); Assert.Equal(2, quote.Cost!.GenericPower); Assert.Equal(1, quote.Cost.MissingPower);
        await Reject(driver, "P2", command); Assert.Contains("NEGATIVE", driver.State.PlayerZones["P2"].Hand);
        Assert.Equal(20, driver.State.RunePools["P2"].Mana); Assert.Equal(1, driver.State.RunePools["P2"].TotalPower);
        await Export("ward-two-insufficient-power", initial, driver);
    }

    [Fact]
    public async Task ARealFriendlySpellPaysOnlyItsPrintedCostAgainstEmpowered122()
    {
        const string no = "VEN·122"; var initial = Add(Position(no), "FRIENDLY", "VEN·081", "P1"); var driver = await Play(initial, no); await Empower(driver);
        var command = new PlayCardCommand("FRIENDLY", "VEN·081", ["UNIT"]); var before = driver.State.RunePools["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        await driver.Act("P1", command); Assert.Equal(before.Mana - 4, driver.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, driver.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
        await driver.Top(); Assert.Equal(10, Might(driver.State)); await driver.Act("P1", new EndTurnCommand()); CheckSelf(driver.State, no);
        await Export("friendly-target-no-ward-tax", initial, driver);
    }

    [Theory]
    [InlineData(false, 2)] [InlineData(true, 1)] [InlineData(true, 2)]
    public async Task AnActualEnemyAbilityPaysTwoWardBeforeExhaustingItsSource(bool empowered, int runes)
    {
        const string no = "VEN·122"; var initial = OnBase(Position(no), "ORB", OrbOfRegretTests.Card, "P2");
        for (var i = 0; i < runes; i++) initial = OnBase(initial, "ABILITY-RUNE-" + i, "UNL-R02", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = RunePool.Empty } };
        var driver = await Play(initial, no); if (empowered) await Empower(driver); await driver.Act("P1", new EndTurnCommand());
        var optional = empowered ? Enumerable.Range(0, runes).Select(i => "RECYCLE_RUNE:ABILITY-RUNE-" + i).ToArray() : [];
        var command = new ActivateAbilityCommand("ORB", OrbOfRegretTests.Ability, ["UNIT"], OptionalCosts: optional);
        Assert.False(driver.State.CardObjects["ORB"].IsExhausted); Assert.Equal(0, driver.State.RunePools["P2"].TotalPower);
        if (empowered && runes == 1) {
            await Reject(driver, "P2", command); Assert.Equal("资源不足，无法支付目标的法盾。", driver.Commands().Last().ErrorMessage);
            Assert.False(driver.State.CardObjects["ORB"].IsExhausted); Assert.Contains("ABILITY-RUNE-0", driver.State.PlayerZones["P2"].Base);
            Assert.DoesNotContain("ABILITY-RUNE-0", driver.State.PlayerZones["P2"].RuneDeck); CheckSelf(driver.State, no);
        } else {
            await driver.Act("P2", command); Assert.True(driver.State.CardObjects["ORB"].IsExhausted);
            Assert.Equal(empowered ? 2 : 0, driver.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
            Assert.Equal(0, driver.State.RunePools["P2"].TotalPower);
            for (var i = 0; i < runes; i++) Assert.Equal(empowered, driver.State.PlayerZones["P2"].RuneDeck.Contains("ABILITY-RUNE-" + i));
            await driver.Top(); Assert.Equal(2, driver.State.CardObjects["UNIT"].Power); Assert.Equal(empowered ? 3 : 2, Might(driver.State));
            await driver.Act("P2", new EndTurnCommand()); Assert.Equal(3, driver.State.CardObjects["UNIT"].Power);
            Assert.Equal(empowered ? 4 : 3, Might(driver.State)); Assert.Equal(empowered, driver.State.CardObjects["UNIT"].IsEmpowered);
        }
        await Export($"ability-ward-{empowered}-{runes}", initial, driver);
    }

    [Theory]
    [InlineData("VEN·093")] [InlineData("VEN·122")]
    public async Task ARealTargetFilteredCounterCannotTreatTheEmpowerSourceAsATarget(string no)
    {
        var initial = Add(Position(no), "COUNTER", "SFD·045/221", "P2"); var driver = await Play(initial, no); await driver.Act("P1", Activate());
        var id = Assert.Single(driver.State.StackItems).StackItemId; await driver.Act("P1", new PassPriorityCommand());
        await Reject(driver, "P2", new PlayCardCommand("COUNTER", "SFD·045/221", [id]));
        await driver.Act("P2", new PassPriorityCommand()); CheckSelf(driver.State, no); await Export("target-filtered-counter-" + no, initial, driver);
    }

    public static IEnumerable<object[]> ForgeryCases() => Cards.SelectMany(no => new[] { "paid-mana", "premature-status" }.Select(m => new object[] { no, m }));
    [Theory]
    [MemberData(nameof(ForgeryCases))]
    public async Task TheUntouchedOriginalLogRejectsInventedActivationCostOrPrematureStatus(string no, string mutation)
    {
        var initial = Position(no); var driver = await Play(initial, no); await driver.Act("P1", Activate()); JsonRestore(initial, driver);
        var item = Assert.Single(driver.State.StackItems); Assert.Equal(2, item.EmpowerActivation!.PaidMana);
        var forged = mutation == "paid-mana" ? driver.State with { StackItems = [item with { EmpowerActivation = item.EmpowerActivation with { PaidMana = 1 } }],
            RunePools = new Dictionary<string, RunePool>(driver.State.RunePools) { ["P1"] = driver.State.RunePools["P1"] with { Mana = driver.State.RunePools["P1"].Mana + 1 } } }
            : driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) { ["UNIT"] = driver.State.CardObjects["UNIT"] with { IsEmpowered = true } } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(2, driver.State.StackItems.Single().EmpowerActivation!.PaidMana); await Export($"forgery-{no}-{mutation}", initial, driver);
    }

    private static MatchState Position(string no)
    {
        var state = BasePosition();
        return state with { RoomId = "VEN093-122-EMPOWERED-SELF",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = Printed("UNIT", no, "P1") } };
    }
    private static CardObjectState Printed(string id, string no, string owner) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static async Task<Driver> Play(MatchState initial, string no, string destination = "BASE")
    { var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination)); return driver; }
    private static async Task Empower(Driver driver) { await driver.Act("P1", Activate()); await driver.Top(); }
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static void CheckSelf(MatchState state, string no)
    {
        var unit = state.CardObjects["UNIT"]; Assert.True(unit.IsEmpowered); Assert.Equal(PrintedMight(no), unit.Power); Assert.Equal(PrintedMight(no) + 1, Might(state));
        Assert.Equal(no == "VEN·122", unit.Tags.Contains("法盾2")); Assert.DoesNotContain(CardObjectTags.Spellshield, unit.Tags);
        Assert.Equal(no == "VEN·093" ? new[] { "游走" } : ["法盾2"], unit.RuleDerivedTags);
        Assert.Equal(no == "VEN·093", unit.Tags.Contains(CardCombatKeywordNames.Roam));
        Assert.Empty(unit.UntilEndOfTurnEffects); Assert.Empty(unit.UntilEndOfTurnPowerModifiers);
    }
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
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        node["promptId"] = ResolutionResult.BuildPrompts(driver.State)[player].PromptId; node["snapshotTick"] = driver.State.Tick;
        await driver.ActRetained(new(player, "ven093-122-reject-" + driver.Commands().Length, JsonSerializer.SerializeToElement(node, Json)), false);
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
    {
        await driver.Replay(); JsonRestore(initial, driver);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN093122_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), runtime = new { ruleInputs = RuleInputIdentity.Current, baseDirectory = AppContext.BaseDirectory, catalogPath = OfficialCardCatalog.DefaultCatalogPath(), modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly }.Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
        }, Json));
    }

    private static MatchState BasePosition()
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", Cards[0], "P1", "HAND"); Put("ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF"); Put("ENEMY", "OGN·219/298", "P2", "BASE");
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
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        internal async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json); command = GameCommandJsonMapper.Map(wire);
            var id = "ven093122-" + entries.Count;
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
