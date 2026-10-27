using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven130EmpoweredUnitAuraConsumerTests
{
    private const string No = "VEN·130";
    private const string Text = "{{强化3黄色}}（支付{{3}}和{{黄色}}：强化我。仅在未强化时可用。）\n{{已强化>}} 你的{{已强化}}单位获得{{S}}+2（包括我在内）。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task TheExactPrintingAddsOneFunctionalGroupWithoutChangingExistingMembers()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var controlCards = catalog.Cards.Where(c => c.CardNo is not ("VEN·128" or "VEN·078")).ToArray();
        var oldCards = controlCards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1085, oldCards.Length); Assert.Equal(1086, controlCards.Length);
        var prior = FunctionalUnitBuilder.Build(oldCards); var current = FunctionalUnitBuilder.Build(controlCards);
        Assert.Equal(866, prior.Count); Assert.Equal(867, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
        var cumulativePriorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1087, cumulativePriorCards.Length); Assert.Equal(1088, catalog.Cards.Count);
        var cumulativePrior = FunctionalUnitBuilder.Build(cumulativePriorCards); var cumulative = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, cumulativePrior.Count); Assert.Equal(869, cumulative.Count);
        foreach (var unit in cumulativePrior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        foreach (var unit in current) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
    }

    [Fact]
    public async Task ExactCnSourceSeparatesTypedActivationCostFromTheConditionalFriendlyAura()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37830, card.Id); Assert.Equal("奥洛克将军", card.CardName); Assert.Empty(card.SubTitle); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(5, card.Energy); Assert.Equal(5, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Yellow], card.CardColorList); Assert.Equal(["unit"], card.CardCategoryList);
        Assert.Equal("诺克萨斯", card.Region); Assert.Equal("崔法利", card.Tag);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Equal(1, Assert.Single(cost.TypedPower).Value);
        Assert.Equal(RuneTrait.Yellow, Assert.Single(cost.TypedPower).Key); Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        Assert.Equal(0, EmpowerParser.Self(card.CardEffect).Might);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(5, behavior.ManaCost); Assert.Equal(5, behavior.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ActualPlayAndEmpowerPayTheirSeparatePrintedManaAndYellowPower(string destination)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
            Assert.Equal(35, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(5, Might(game.State, "UNIT"));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", Activate()); Assert.Equal(32, game.State.RunePools["P1"].Mana);
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
            Assert.Equal(new ObjectBinding("UNIT", generation), item.EmpowerActivation!.Source);
            Assert.Equal(3, item.EmpowerActivation.PaidMana); Assert.Equal(0, item.EmpowerActivation.Keyword); Assert.Equal(0, item.EmpowerActivation.Alternative);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-cost-" + destination.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task TheActuallyEmpoweredSourceReceivesItsOwnCurrentAuraWithoutChangingPrintedMight(string destination)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination)); await game.Act("P1", Activate());
            Assert.Equal(5, Might(game.State, "UNIT")); await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(7, Might(game.State, "UNIT")); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(7, Might(game.State, "UNIT"));
            await game.Act("P2", new EndTurnCommand()); Assert.Equal(7, Might(game.State, "UNIT"));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("self-aura-" + destination.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("BASE", "BASE")] [InlineData("BASE", "BATTLEFIELD:BF")]
    [InlineData("BATTLEFIELD:BF", "BASE")] [InlineData("BATTLEFIELD:BF", "BATTLEFIELD:BF")]
    public async Task CurrentFriendlyEmpoweredUnitsAtEitherFieldLocationGainAndLoseTheAura(string sourceDestination, string allyDestination)
    {
        var initial = Add(Add(Add(Position(), "EMPOWER-ALLY", "VEN·070"), "RETURN", "OGN·104/298"), "PLAIN", "OGN·175/298", "P1", "BASE");
        initial = Add(initial, "EMPOWER-ENEMY", "VEN·070", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["EMPOWER-ENEMY"] = initial.CardObjects["EMPOWER-ENEMY"] with { IsEmpowered = true } } };
        if (sourceDestination == "BATTLEFIELD:BF" && allyDestination == "BASE")
            initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("EMPOWER-ALLY", "VEN·070", [], Destination: allyDestination));
            await game.Act("P1", new ActivateAbilityCommand("EMPOWER-ALLY", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["EMPOWER-ALLY"].IsEmpowered); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            Assert.Equal(6, Might(game.State, "EMPOWER-ENEMY", "P2"));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: sourceDestination));
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); await game.Act("P1", Activate());
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); await game.Top();
            Assert.Equal(8, Might(game.State, "EMPOWER-ALLY")); Assert.Equal(7, Might(game.State, "UNIT"));
            Assert.Equal(3, Might(game.State, "PLAIN")); Assert.Equal(6, Might(game.State, "EMPOWER-ENEMY", "P2"));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            await game.Act("P1", Activate()); await game.Top(); Assert.Equal(8, Might(game.State, "EMPOWER-ALLY"));
            Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("ally-aura-" + sourceDestination.Replace(':', '-') + "-" + allyDestination.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("unempowered")] [InlineData("pending")] [InlineData("returned")]
    public async Task InactivePendingOrReturnedSourceCannotGrantItsDependentAura(string branch)
    {
        var initial = Add(Add(Position(), "EMPOWER-ALLY", "VEN·070"), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("EMPOWER-ALLY", "VEN·070", []));
            await game.Act("P1", new ActivateAbilityCommand("EMPOWER-ALLY", "EMPOWER:0:0", [])); await game.Top();
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            if (branch != "unempowered") { await game.Act("P1", Activate()); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); }
            if (branch == "returned")
            {
                var paidMana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top(); await game.Top();
                Assert.Equal(paidMana - 1, game.State.RunePools["P1"].Mana); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(game.State.StackItems);
            }
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("inactive-source-" + branch, initial, game); }
    }

    [Theory]
    [InlineData("wrong-trait")] [InlineData("mana")] [InlineData("target")] [InlineData("repeated")]
    public async Task IllegalTypedEmpowerPaymentsAndTargetsLeaveTheOriginalStateUnchanged(string branch)
    {
        var initial = Position();
        if (branch == "wrong-trait") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 2 }) } };
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(7, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2 }) } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            if (branch == "repeated") { await game.Act("P1", Activate()); await game.Top(); }
            var command = Activate() with { TargetObjectIds = branch == "target" ? ["UNIT"] : [] };
            var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "ven130-rejected-" + branch, JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Equal(branch == "repeated", game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); await game.Replay(); Restore(game.State);
        }
        finally { await Save("invalid-activation-" + branch, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActuallyCopiedTextRequiresTheEmpoweredHostAndStopsGrantingItsAuraWhenDetached(bool detachBeforeEmpower)
    {
        var initial = Add(ShadySpectaclesTests.Position(No), "DETACH", "SFD·011/221") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 3, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            var original = Might(game.State, "HOST");
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var copy = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", copy.Request.Kind);
            await game.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal(No, game.State.CardObjects["HOST"].CardNo); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            var copiedMight = Might(game.State, "HOST");
            var requirement = ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!.Where(c => c.Action == CommandTypes.ActivateAbility)
                .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]))
                .Single(row => (string?)row["sourceObjectId"] == "HOST" && (string?)row["abilityId"] == "EMPOWER:0:0");
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) with { AbilityInstanceId = Assert.IsType<string>(requirement["abilityInstanceId"]) });
            Assert.Equal("GEAR", Assert.Single(game.State.StackItems).EmpowerActivation!.Origin!.Equipment.ObjectId);
            Assert.Equal(copiedMight, Might(game.State, "HOST"));
            if (detachBeforeEmpower)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
                Assert.Equal(original, Might(game.State, "HOST")); Assert.NotEqual(No, game.State.CardObjects["HOST"].CardNo);
            }
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.False(game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            Assert.Equal(detachBeforeEmpower ? original : copiedMight + 2, Might(game.State, "HOST"));
            if (!detachBeforeEmpower)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
                Assert.Equal(original, Might(game.State, "HOST")); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            }
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-copy-" + detachBeforeEmpower, initial, game); }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyAttachedRuleTextInstanceAddsItsOwnCurrentFriendlyEmpoweredBonus(int copies)
    {
        var initial = Add(EmpowerTests.Position(No), "EMPOWER-ALLY", "VEN·070") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(60, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2, [RuneTrait.Green] = 3, [RuneTrait.Orange] = 2 }), ["P2"] = new(20, 20) } };
        if (copies == 2) initial = AttachedRuleTextTests.AddSong(initial, "SONG2");
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("HOST", No, []));
            await game.Act("P1", new PlayCardCommand("EMPOWER-ALLY", "VEN·070", []));
            await game.Act("P1", new ActivateAbilityCommand("EMPOWER-ALLY", "EMPOWER:0:0", [])); await game.Top();
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new AssembleEquipmentCommand(i == 0 ? "GEAR" : "SONG2", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal("HOST", game.State.CardObjects[i == 0 ? "GEAR" : "SONG2"].AttachedToObjectId);
            }
            var hostMight = Might(game.State, "HOST"); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            Assert.Equal(hostMight, Might(game.State, "HOST")); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(6 + 2 * (copies + 1), Might(game.State, "EMPOWER-ALLY"));
            Assert.Equal(hostMight + 2 * (copies + 1), Might(game.State, "HOST"));
            Assert.False(game.State.CardObjects["GEAR"].IsEmpowered); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-text-instances-" + copies, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningPaysRealYellowPowerAndColdRecoversTheCurrentPassive(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "poppy-standby", "UNL-R06", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 8, No); await game.Tap(actor);
            await game.Act(actor, new RecycleRuneCommand(game.Runes(actor).First()));
            Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Yellow]);
            var unit = game.Hand(actor, No).First(); var mana = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(unit, No, [])); Assert.Equal(mana - 5, game.State.RunePools[actor].Mana);
            Assert.Equal(5, game.Might(actor, unit)); await game.ColdCheckpoint();
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(mana - 8, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(5, game.Might(actor, unit)); await game.ColdCheckpoint();
            var pool = game.State.RunePools[actor];
            var refunded = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
                [actor] = pool with { Mana = pool.Mana + 3, PowerByTrait = new Dictionary<string, int>(pool.PowerByTrait) { [RuneTrait.Yellow] = 1 } } } };
            await game.RejectCoordinatedForgery("refunded-printed-empower-mana-and-yellow", refunded);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(7, game.Might(actor, unit));
            Assert.Equal(5, game.State.CardObjects[unit].Power); await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.Equal(7, game.Might(actor, unit)); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven130-" + actor); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AnActualTakeoverAndRecallMoveTheAuraToItsCurrentControllerWithoutChangingItsOwner(string thief)
    {
        var originalController = thief == "P1" ? "P2" : "P1";
        var initial = Add(Add(Add(Add(Position(), "TAKEN", No, originalController), "TAKE", "OGN·203/298", thief),
            "OLD-FRIEND", "VEN·070", originalController, "BASE"), "NEW-FRIEND", "VEN·070", thief, "BASE");
        initial = initial with { TurnPlayerId = originalController, ActivePlayerId = originalController,
            RunePools = new Dictionary<string, RunePool> { [originalController] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }), [thief] = new(0, 0) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["OLD-FRIEND"] = initial.CardObjects["OLD-FRIEND"] with { IsEmpowered = true },
                ["NEW-FRIEND"] = initial.CardObjects["NEW-FRIEND"] with { IsEmpowered = true } } };
        initial = Add(initial, "RETURN", "OGN·104/298", thief);
        for (var i = 0; i < 9; i++) initial = Add(initial, "CONTROL-RUNE-" + i, "UNL-R05", thief, "BASE");
        var game = new Driver(initial);
        try
        {
            var field = originalController == "P1" ? "BF" : "OTHER";
            await game.Act(originalController, new PlayCardCommand("TAKEN", No, [], Destination: "BATTLEFIELD:" + field));
            await game.Act(originalController, new ActivateAbilityCommand("TAKEN", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["TAKEN"].IsEmpowered); Assert.Equal(6, Might(game.State, "NEW-FRIEND", thief));
            await game.Act(originalController, new EndTurnCommand()); Assert.Equal(0, game.State.RunePools[thief].TotalPower);
            for (var i = 0; i < 9; i++) await game.Act(thief, new TapRuneCommand("CONTROL-RUNE-" + i));
            for (var i = 0; i < 3; i++) await game.Act(thief, new RecycleRuneCommand("CONTROL-RUNE-" + i));
            Assert.Equal(9, game.State.RunePools[thief].Mana); Assert.Equal(3, game.State.RunePools[thief].PowerByTrait[RuneTrait.Purple]);
            await game.Act(thief, new PlayCardCommand("TAKE", "OGN·203/298", ["TAKEN"])); await game.Top();
            Assert.Equal(thief, game.State.CardObjects["TAKEN"].ControllerId); Assert.Equal(originalController, game.State.CardObjects["TAKEN"].OwnerId);
            Assert.Equal("BASE", game.State.ObjectLocations["TAKEN"].Zone); Assert.Contains("TAKEN", game.State.PlayerZones[thief].Base);
            Assert.True(game.State.CardObjects["TAKEN"].IsEmpowered); Assert.Equal(1, game.State.RunePools[thief].Mana); Assert.Equal(0, game.State.RunePools[thief].TotalPower);
            Assert.Equal(8, Might(game.State, "NEW-FRIEND", thief)); Assert.Equal(6, Might(game.State, "OLD-FRIEND", originalController));
            await game.Act(thief, new PlayCardCommand("RETURN", "OGN·104/298", ["TAKEN"])); await game.Top();
            Assert.Contains("TAKEN", game.State.PlayerZones[originalController].Hand); Assert.False(game.State.CardObjects["TAKEN"].IsEmpowered);
            Assert.Equal(6, Might(game.State, "NEW-FRIEND", thief)); Assert.Equal(6, Might(game.State, "OLD-FRIEND", originalController));
            Assert.Equal(0, game.State.RunePools[thief].Mana);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED"); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-takeover-" + thief, initial, game); }
    }

    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static MatchState Position() => VenUnitEntryBasicTests.Position(No) with { RunePools = new Dictionary<string, RunePool> {
        ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2, [RuneTrait.Orange] = 2, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1", string zone = "HAND") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone == "BASE"
            ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] }
            : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static int Might(MatchState state, string id, string owner = "P1")
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json).GetProperty("players").GetProperty(owner)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static void Restore(MatchState state)
    {
        var restored = OfficialInsightAndSpellLockTests.Restore(state);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN130_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN VEN130 explicit midgame actual play and resource-Empower consumer; source applicability, L2/L3 and whole-card certification remain OPEN",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
