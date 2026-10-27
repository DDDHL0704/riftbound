using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven070ResourceEmpowerConsumerTests
{
    private const string No = "VEN·070";
    private const string Text = "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+2和{{游走}}。（我可以向其他战场进行移动。）";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task TheExactNewPrintingAddsOneCatalogGroupWithoutChangingAnyExistingGroupOrMember()
    {
        var latestCatalog = await OfficialCardCatalog.LoadDefaultAsync();
        var catalog = latestCatalog with { Total = 1088, Cards = latestCatalog.Cards.Where(c => c.CardNo != "VEN·124").ToArray() };
        var controlCards = catalog.Cards.Where(c => c.CardNo is not ("VEN·128" or "VEN·078")).ToArray();
        var priorCards = controlCards.Where(card => card.CardNo != No).ToArray();
        Assert.Equal(1085, priorCards.Length); Assert.Equal(1086, controlCards.Length);
        var priorUnits = FunctionalUnitBuilder.Build(priorCards); var units = FunctionalUnitBuilder.Build(controlCards);
        Assert.Equal(866, priorUnits.Count); Assert.Equal(867, units.Count);
        foreach (var prior in priorUnits)
        {
            var current = Assert.Single(units, unit => unit.Id == prior.Id);
            Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(current));
        }
        var added = Assert.Single(units, unit => unit.Cards.Any(card => card.CardNo == No));
        Assert.Equal(No, Assert.Single(added.Cards).CardNo);
        var cumulativePriorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1087, cumulativePriorCards.Length); Assert.Equal(1088, catalog.Cards.Count);
        var cumulativePrior = FunctionalUnitBuilder.Build(cumulativePriorCards); var cumulative = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, cumulativePrior.Count); Assert.Equal(869, cumulative.Count);
        foreach (var unit in cumulativePrior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        foreach (var unit in units) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        Assert.Equal(1089, latestCatalog.Cards.Count);
        var latestUnits = FunctionalUnitBuilder.Build(latestCatalog.Cards);
        Assert.Equal(870, latestUnits.Count);
        foreach (var unit in FunctionalUnitBuilder.Build(catalog.Cards))
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(unit), System.Text.Json.JsonSerializer.Serialize(Assert.Single(latestUnits, c => c.Id == unit.Id)));
        Assert.Equal("VEN·124", Assert.Single(Assert.Single(latestUnits, c => c.Cards.Any(card => card.CardNo == "VEN·124")).Cards).CardNo);
    }

    [Fact]
    public async Task ExactCnPrintingSeparatesPrintedCostsFromItsConditionalSelfEffects()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37764, card.Id); Assert.Equal("残暴猎手", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal([RuneTrait.Orange], card.CardColorList); Assert.Equal("诺克萨斯", card.Region); Assert.Empty(card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(3, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.DoesNotContain("游走", behavior.SourceUnitTags ?? "");
        var spec = Assert.Single(EmpowerParser.Parse(card.CardEffect)); var cost = Assert.Single(spec.Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust); Assert.False(spec.ReduceManaPerRune);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(2, self.Might); Assert.Equal(["游走"], self.Keywords);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ActualPlayPaysOneOrangeAndActivationPaysOnlyThreeManaBeforePermanentEffectsResolve(string destination)
    {
        var initial = Position(); var game = await Play(initial, destination);
        Assert.Equal(37, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State));
        Assert.DoesNotContain("游走", game.State.CardObjects["UNIT"].Tags);
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", Activate());
        Assert.Equal(34, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
        var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
        Assert.Equal(3, item.EmpowerActivation!.PaidMana); Assert.Equal(generation, item.EmpowerActivation.Source.Generation);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State));
        JsonRestore(initial, game); await game.Top(); CheckSelf(game.State);
        await game.Act("P1", new EndTurnCommand()); CheckSelf(game.State);
        await game.Act("P2", new EndTurnCommand()); CheckSelf(game.State);
        Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
        await Finish("permanent-" + destination.Replace(':', '-'), initial, game);
    }

    [Fact]
    public async Task ActualPrintedPlayRejectsWrongTraitWithoutPayingManaOrEnteringPlay()
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial); await Reject(game, "P1", new PlayCardCommand("UNIT", No, []));
        Assert.Equal(40, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await Finish("wrong-printed-power-trait", initial, game);
    }

    [Theory]
    [InlineData("mana")] [InlineData("controller")] [InlineData("repeated")]
    [InlineData("target")] [InlineData("timing")] [InlineData("gear")]
    public async Task ActualIllegalActivationKeepsCostsStatusAndMovementPermissionUnchanged(string branch)
    {
        var initial = Position();
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = initial.RunePools["P1"] with { Mana = 5 } } };
        if (branch == "controller") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", No, "P2") } };
        var game = await Play(initial); if (branch == "repeated") { await game.Act("P1", Activate()); await game.Top(); }
        if (branch == "timing") await game.Act("P1", new EndTurnCommand());
        var source = branch == "controller" ? "ENEMY" : branch == "gear" ? "GEAR" : "UNIT";
        await Reject(game, "P1", new ActivateAbilityCommand(source, "EMPOWER:0:0", branch == "target" ? ["UNIT"] : []));
        Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(branch == "repeated" ? 6 : 4, Might(game.State));
        Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].Tags.Contains("游走"));
        await Finish("illegal-" + branch, initial, game);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualReturnClearsMightAndRoamAndAnOldActivationCannotEmpowerTheReplayedCard(bool before)
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = await Play(initial);
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", Activate()); if (!before) { await game.Top(); CheckSelf(game.State); }
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State));
        Assert.DoesNotContain("游走", game.State.CardObjects["UNIT"].Tags); Assert.Null(game.State.CardObjects["UNIT"].RuleDerivedTags);
        if (before)
        {
            Assert.Equal(generation, Assert.Single(game.State.StackItems).EmpowerActivation!.Source.Generation);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var item = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().Single();
                Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString()); Assert.False(item.TryGetProperty("empowerActivation", out _));
            }
            JsonRestore(initial, game); await game.Top();
        }
        await game.Act("P1", new PlayCardCommand("UNIT", No, []));
        Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State));
        Assert.DoesNotContain("游走", game.State.CardObjects["UNIT"].Tags);
        await Finish("return-and-replay-" + before, initial, game);
    }

    [Fact]
    public async Task ActualEmpowerEnablesRoamBetweenBattlefieldsAndThePermissionSurvivesTurnEnd()
    {
        var initial = Add(Position(), "READY", "UNL-009/219"); var game = await Play(initial, "BATTLEFIELD:BF");
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        var move = new MoveUnitCommand("UNIT", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]);
        await Reject(game, "P1", move); await game.Act("P1", Activate()); await game.Top(); CheckSelf(game.State);
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", move); Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand()); await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand()); CheckSelf(game.State);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD:OTHER", "BATTLEFIELD:BF", ["ROAM"]));
        Assert.Equal("BF", game.State.ObjectLocations["UNIT"].BattlefieldObjectId); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
        await Finish("actual-permanent-roam", initial, game);
    }

    [Fact]
    public async Task ActualTemporaryMightExpiresWithoutErasingThePermanentEmpoweredBonus()
    {
        var initial = Add(Position(), "HOWL", "VEN·072"); var game = await Play(initial);
        await game.Act("P1", Activate()); await game.Top(); CheckSelf(game.State);
        await game.Act("P1", new PlayCardCommand("HOWL", "VEN·072", ["UNIT"])); await game.Top();
        Assert.Equal(4, Assert.Single(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
        Assert.Equal(10, Might(game.State)); await game.Act("P1", new EndTurnCommand()); CheckSelf(game.State);
        await Finish("temporary-might-expired", initial, game);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UntouchedActualCommandsRejectRefundedActivationCostAndPrematureStatus(bool premature)
    {
        var initial = Position(); var game = await Play(initial); await game.Act("P1", Activate()); JsonRestore(initial, game);
        var item = Assert.Single(game.State.StackItems); Assert.Equal(3, item.EmpowerActivation!.PaidMana);
        var forged = premature ? game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["UNIT"] = game.State.CardObjects["UNIT"] with { IsEmpowered = true } } }
            : game.State with { StackItems = [item with { EmpowerActivation = item.EmpowerActivation with { PaidMana = 2 } }],
                RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = game.State.RunePools["P1"] with { Mana = 35 } } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged, new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(3, game.State.StackItems.Single().EmpowerActivation!.PaidMana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await Finish("forged-" + premature, initial, game);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningNaturallyPaysPrintedOrangeAndEmpowerAndColdRecoversPermanentSelfEffects(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R04", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 6, No); await game.Tap(actor);
            await game.Act(actor, new RecycleRuneCommand(game.Runes(actor).First()));
            var unit = game.Hand(actor, No).First(); var mana = game.State.RunePools[actor].Mana;
            Assert.Equal(1, game.State.RunePools[actor].TotalPower);
            await game.Act(actor, new PlayCardCommand(unit, No, []));
            Assert.Equal(mana - 3, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.Equal(4, game.Might(actor, unit)); Assert.DoesNotContain("游走", game.State.CardObjects[unit].Tags);
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(3, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.Equal(mana - 6, game.State.RunePools[actor].Mana); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            await game.ColdCheckpoint(); await game.Top();
            Assert.Equal(6, game.Might(actor, unit)); Assert.Equal(4, game.State.CardObjects[unit].Power);
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Contains("游走", game.State.CardObjects[unit].Tags);
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.Equal(6, game.Might(actor, unit)); Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Contains("游走", game.State.CardObjects[unit].Tags);
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven070-" + actor); }
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(No);
        return state with { RoomId = "VEN070-EXPLICIT-MIDGAME", CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = null } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 2 }) } };
    }
    private static MatchState Add(MatchState state, string id, string no) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, id] } } };
    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static void CheckSelf(MatchState state)
    {
        var unit = state.CardObjects["UNIT"]; Assert.True(unit.IsEmpowered); Assert.Equal(4, unit.Power); Assert.Equal(6, Might(state));
        Assert.Equal(["游走"], unit.RuleDerivedTags); Assert.Contains("游走", unit.Tags);
        Assert.Empty(unit.UntilEndOfTurnPowerModifiers); Assert.Empty(unit.UntilEndOfTurnEffects);
    }
    private static async Task<Driver> Play(MatchState initial, string destination = "BASE")
    { var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination)); return game; }
    private static async Task Reject(Driver game, string actor, GameCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        node["promptId"] = ResolutionResult.BuildPrompts(game.State)[actor].PromptId; node["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new(actor, "ven070-reject-" + game.Commands().Length, JsonSerializer.SerializeToElement(node, Json)), false);
    }
    private static void JsonRestore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var actor in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[actor]), MatchStateHasher.HashValue(restored.SnapshotFor(actor)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[actor]), MatchStateHasher.HashValue(restored.PromptFor(actor)));
        }
    }
    private static async Task Finish(string label, MatchState initial, Driver game)
    {
        await game.Replay(); JsonRestore(initial, game);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN070_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new { scope = "Explicit midgame semantic fixture; canonical openings are separate", initial,
            final = game.State, commands = game.Commands(), events = game.Events(), runtime, ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
