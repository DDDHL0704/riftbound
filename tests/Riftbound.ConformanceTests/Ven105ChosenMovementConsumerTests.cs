using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven105ChosenMovementConsumerTests
{
    private const string No = "VEN·105";
    private const string Text = "移动一名不高于3{{S}}的单位。\n{{流转4紫色}}（你可以选择支付此牌的流转费用，以此将其从你的废牌堆中打出。然后将其放逐。）";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    static Ven105ChosenMovementConsumerTests()
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN105_RUNTIME") is not { Length: > 0 } path) return;
        Assembly[] modules = [typeof(Ven105ChosenMovementConsumerTests).Assembly, typeof(CoreRuleEngine).Assembly,
            typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly];
        using var output = new FileStream(path, FileMode.CreateNew);
        JsonSerializer.Serialize(output, new { processId = Environment.ProcessId, baseDirectory = AppContext.BaseDirectory,
            ruleInputs = RuleInputIdentity.Current, catalogPath = OfficialCardCatalog.DefaultCatalogPath(),
            modules = modules.Select(assembly => new { name = assembly.GetName().Name, path = assembly.Location,
                mvid = assembly.ManifestModule.ModuleVersionId, sha256 = Hash(File.ReadAllBytes(assembly.Location)) }) }, Json);
    }

    [Fact]
    public async Task ExactCurrentFaceRegistersOneSmallUnitMoveAndItsIndependentFlowCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, card => card.CardNo == No);
        Assert.Equal(37802, card.Id); Assert.Equal("奥义！幽步", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(2, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal([RuneTrait.Purple], card.CardColorList);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.Equal(2, behavior.ManaCost); Assert.Equal(1, behavior.RequiredTargetCount); Assert.Equal(3, behavior.MaxTargetPower);
        Assert.Equal(CardTargetScopes.AnyUnit, behavior.TargetScope); Assert.Equal(CardObjectTags.UnitCard, behavior.TargetRequiredTag);
        Assert.True(behavior.MovesTargetsToChosenLocation); Assert.Equal(0, behavior.DrawCount);
        Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
        Assert.Equal(4, behavior.FlowCost!.Mana); Assert.Equal(1, behavior.FlowCost.Power); Assert.Equal([RuneTrait.Purple], behavior.FlowCost.Traits);
    }

    public static IEnumerable<object[]> LegalMoves() =>
        from flow in new[] { false, true } from owner in new[] { "P1", "P2" }
        from echo in new[] { false, true } from toBase in new[] { false, true }
        select new object[] { flow, owner, echo, toBase };

    [Theory]
    [MemberData(nameof(LegalMoves))]
    public async Task OrdinaryFlowAndEchoMoveFriendlyOrEnemyMightThreeWithoutChangingItsIncarnation(
        bool flow, string owner, bool echo, bool toBase)
    {
        var initial = Position(flow, toBase, echo); var run = new Driver(initial);
        var target = owner == "P1" ? "ALLY" : "ENEMY"; var card = initial.CardObjects[target];
        var destination = toBase ? "BASE:" + owner : "BATTLEFIELD:BF";
        var command = new PlayCardCommand("SPELL", No, [target], OptionalCosts: echo ? ["ECHO"] : [], Destination: destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal((flow ? 4 : 2) + (echo ? 2 : 0), quote.Cost!.Mana);
        Assert.Equal(echo ? 2 : 1, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Purple)); Assert.Equal(0, quote.Cost.GenericPower);
        await run.Act("P1", command);
        var pending = Assert.Single(run.State.StackItems); Assert.Equal(flow, pending.FlowPlay is not null);
        Assert.Equal(echo ? 2 : 1, pending.EffectRepeatCount);
        if (echo) {
            Assert.Equal(2, pending.RepeatExecutions!.Count);
            Assert.All(pending.RepeatExecutions, execution => { Assert.Equal(destination, execution.Destination); Assert.Equal("P1", execution.MovementDeclarationPlayerId); });
        }
        await Export($"move-{flow}-{owner}-{echo}-{toBase}-pending", initial, run); await run.Top();
        Assert.Equal(toBase ? "BASE" : "BATTLEFIELD", run.State.ObjectLocations[target].Zone);
        Assert.Equal(owner, run.State.ObjectLocations[target].PlayerId);
        Assert.Equal(toBase ? null : "BF", run.State.ObjectLocations[target].BattlefieldObjectId);
        Assert.Equal(card.ObjectGeneration, run.State.CardObjects[target].ObjectGeneration);
        Assert.Equal(card.Power, run.State.CardObjects[target].Power); Assert.Equal(card.Damage, run.State.CardObjects[target].Damage);
        Assert.Equal(card.IsExhausted, run.State.CardObjects[target].IsExhausted); Assert.Equal(card.OwnerId, run.State.CardObjects[target].OwnerId);
        Assert.Equal(30 - (flow ? 4 : 2) - (echo ? 2 : 0), run.State.RunePools["P1"].Mana);
        Assert.Equal(20 - (echo ? 2 : 1), run.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        AssertExit(run.State, flow); Assert.Empty(run.State.StackItems);
        var moved = Assert.Single(run.Events(), item => item.Event.Kind == (toBase ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD")
            && Equals(item.Event.Payload.GetValueOrDefault("targetObjectId"), target));
        Assert.Equal("P1", moved.Event.Payload["movingPlayerId"]); Assert.Equal(destination, moved.Event.Payload["destination"]);
        Assert.DoesNotContain(run.Events(), item => item.Event.Kind == "CARD_DRAWN");
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, run.State.PlayerZones["P1"].MainDeck);
        await Export($"move-{flow}-{owner}-{echo}-{toBase}-done", initial, run);
    }

    public static IEnumerable<object[]> IllegalPlays() => new[] { false, true }.SelectMany(flow => new[] {
        "mana", "wrong-trait", "missing-power", "actor", "zero", "multiple", "might-four", "damaged-might-four",
        "wrong-base", "same-location", "unknown-field", "non-unit", "opponent-source", "banished-source", "deck-source",
        "mode", "extra", "wrong-turn", "duel" }.Select(kind => new object[] { flow, kind }));

    [Theory]
    [MemberData(nameof(IllegalPlays))]
    public async Task CostTargetSourceDestinationAndTimingRejectionsKeepFullStateViewsPromptsAndEvents(bool flow, string kind)
    {
        var initial = Position(flow); var targets = new[] { "ALLY" }; var destination = "BATTLEFIELD:BF";
        if (kind is "mana" or "wrong-trait" or "missing-power") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(kind == "mana" ? (flow ? 3 : 1) : 30, 0,
                kind == "wrong-trait" ? new Dictionary<string, int> { [RuneTrait.Blue] = 20 }
                    : kind == "missing-power" ? null : new Dictionary<string, int> { [RuneTrait.Purple] = 20 }) } };
        if (kind is "might-four" or "damaged-might-four") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = Printed("ALLY", "OGN·219/298", "P1") with { Damage = kind == "damaged-might-four" ? 3 : 0 } } };
        if (kind == "zero") targets = [];
        if (kind == "multiple") targets = ["ALLY", "ALLY2"];
        if (kind == "wrong-base") destination = "BASE:P2";
        if (kind == "same-location") destination = "BASE:P1";
        if (kind == "unknown-field") destination = "BATTLEFIELD:ABSENT";
        if (kind == "non-unit") targets = ["GEAR"];
        if (kind == "opponent-source") initial = Put(initial, "SPELL", No, "P2", flow ? "GRAVEYARD" : "HAND");
        if (kind == "banished-source") initial = At(initial, "SPELL", "P1", "BANISHED");
        if (kind == "deck-source") initial = At(initial, "SPELL", "P1", "MAIN_DECK");
        if (kind == "wrong-turn") initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (kind == "duel") initial = initial with { TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = "P1" };
        var run = new Driver(initial); await Reject(run, kind == "actor" ? "P2" : "P1",
            new PlayCardCommand("SPELL", No, targets, Mode: kind == "mode" ? "FORGED" : "", OptionalCosts: kind == "extra" ? ["FORGED"] : [], Destination: destination));
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(run.State));
        await Export($"reject-{flow}-{kind}", initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExactPrintedManaAndOnePurpleAreEnoughAndDamageDoesNotReduceTheMightLimit(bool flow)
    {
        var initial = Position(flow) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(flow ? 4 : 2, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }), ["P2"] = new(30, 0) } };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ALLY"] = initial.CardObjects["ALLY"] with { Damage = 2 } } };
        Assert.Equal(3, initial.CardObjects["ALLY"].Power); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], Destination: "BATTLEFIELD:BF")); await run.Top();
        Assert.Equal(0, run.State.RunePools["P1"].Mana); Assert.Equal(0, run.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, run.State.CardObjects["ALLY"].Damage); Assert.Equal(3, run.State.CardObjects["ALLY"].Power);
        Assert.Equal("BF", run.State.ObjectLocations["ALLY"].BattlefieldObjectId); AssertExit(run.State, flow);
        await Export("exact-cost-damaged-might-three-" + flow, initial, run);
    }

    [Fact]
    public async Task TheSamePhysicalSpellCanMoveThenFlowBackAndCannotBeCastFromBanish()
    {
        var initial = Position(); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], Destination: "BATTLEFIELD:BF")); await run.Top();
        Assert.Contains("SPELL", run.State.PlayerZones["P1"].Graveyard);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], Destination: "BASE:P1")); await run.Top();
        Assert.Equal("BASE", run.State.ObjectLocations["ALLY"].Zone); Assert.Equal(24, run.State.RunePools["P1"].Mana);
        Assert.Equal(18, run.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]); AssertExit(run.State, true);
        Assert.Equal(2, run.Events().Count(item => item.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"));
        await Reject(run, "P1", new PlayCardCommand("SPELL", No, ["ALLY"], Destination: "BATTLEFIELD:BF"));
        await Export("same-physical-ordinary-flow-banish-reject", initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnOldPromptCannotSpendFromAnotherStillLegalPhysicalCopy(bool flow)
    {
        var initial = Put(Position(flow), "COPY", No, "P1", flow ? "GRAVEYARD" : "HAND");
        var command = new PlayCardCommand("COPY", No, ["ENEMY"], Destination: "BATTLEFIELD:BF");
        var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(initial)["P1"].PromptId; raw["snapshotTick"] = initial.Tick;
        var retained = JsonSerializer.SerializeToElement(raw, Json); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], Destination: "BATTLEFIELD:BF")); await run.Top();
        var fresh = new CoreRuleEngine().PreviewPlayCard(run.State, "P1", PlayCostPreviewTests.Request(run.State, command));
        Assert.True(fresh.CanPay, fresh.Message);
        var before = MatchStateHasher.Hash(run.State);
        await run.ActRetained(new("P1", "ven105-stale-other-copy", retained), false);
        Assert.Equal(before, MatchStateHasher.Hash(run.State));
        Assert.Contains("COPY", flow ? run.State.PlayerZones["P1"].Graveyard : run.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.ObjectLocations["ENEMY"], run.State.ObjectLocations["ENEMY"]);
        await Export("stale-still-legal-copy-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EchoCanChooseTwoIndependentControllersBasesAndExitsOnePhysicalSpell(bool flow)
    {
        var initial = Position(flow, true, true); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], OptionalCosts: ["ECHO"], Destination: "BASE:P1",
            RepeatChoices: [new SpellRepeatChoice("", ["ENEMY"], Destination: "BASE:P2")]));
        var executions = Assert.Single(run.State.StackItems).RepeatExecutions!;
        Assert.Equal(new[] { "BASE:P1", "BASE:P2" }, executions.Select(execution => execution.Destination));
        await Export("echo-independent-pending-" + flow, initial, run); await run.Top();
        Assert.Equal(new ObjectLocationState("P1", "BASE"), run.State.ObjectLocations["ALLY"]);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), run.State.ObjectLocations["ENEMY"]);
        Assert.Equal(2, run.Events().Count(item => item.Event.Kind == "UNIT_MOVED_TO_BASE"));
        Assert.Equal(flow ? 24 : 26, run.State.RunePools["P1"].Mana); Assert.Equal(18, run.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        AssertExit(run.State, flow); await Export("echo-independent-done-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AControlledUnitUsesItsControllerBaseAndRetainsItsOriginalOwner(bool flow)
    {
        var initial = Position(flow, true); initial = At(initial, "ENEMY", "P1", "BATTLEFIELD", "BF") with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { ControllerId = "P1" } } };
        var run = new Driver(initial); await Reject(run, "P1", new PlayCardCommand("SPELL", No, ["ENEMY"], Destination: "BASE:P2"));
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ENEMY"], Destination: "BASE:P1")); await run.Top();
        Assert.Equal("P2", run.State.CardObjects["ENEMY"].OwnerId); Assert.Equal("P1", run.State.CardObjects["ENEMY"].ControllerId);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), run.State.ObjectLocations["ENEMY"]);
        await Export("controlled-target-base-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealCounterKeepsPaymentAndFlowExitButMovesNothing(bool flow)
    {
        var initial = Put(Position(flow), "COUNTER", "OGN·045/298", "P2", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], Destination: "BATTLEFIELD:BF")); var id = Assert.Single(run.State.StackItems).StackItemId;
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [id])); await run.Top();
        Assert.Empty(run.State.StackItems); Assert.Equal(initial.ObjectLocations["ALLY"], run.State.ObjectLocations["ALLY"]);
        Assert.DoesNotContain(run.Events(), item => item.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal(flow ? 26 : 28, run.State.RunePools["P1"].Mana); AssertExit(run.State, flow);
        await Export("countered-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealResponseRaisingMightAboveThreeMakesTheChosenMoveFailWithoutRefund(bool flow)
    {
        var initial = Put(Position(flow, true), "BUFF", "SFD·151/221", "P2", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ENEMY"], Destination: "BASE:P2"));
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("BUFF", "SFD·151/221", ["ENEMY", "ENEMY2"])); await run.Top();
        var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P2"], Json);
        Assert.Equal(4, view.GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty("ENEMY").GetProperty("effectivePower").GetInt32());
        await run.Top(); Assert.Equal("OTHER", run.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.DoesNotContain(run.Events(), item => item.Event.Kind == "UNIT_MOVED_TO_BASE"); AssertExit(run.State, flow);
        Assert.Equal(flow ? 26 : 28, run.State.RunePools["P1"].Mana); await Export("real-buff-fizzles-move-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealReturnChangesTheTargetIncarnationAndTheOldMoveDoesNotReachIntoHand(bool flow)
    {
        var initial = Put(Position(flow, true), "RETURN", "OGN·104/298", "P2", "HAND"); var run = new Driver(initial);
        var generation = initial.CardObjects["ENEMY"].ObjectGeneration;
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ENEMY"], Destination: "BASE:P2"));
        await run.Act("P1", new PassPriorityCommand()); await run.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await run.Top();
        Assert.Equal(generation + 1, run.State.CardObjects["ENEMY"].ObjectGeneration); Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Hand);
        await run.Top(); Assert.Contains("ENEMY", run.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(run.Events(), item => item.Event.Kind == "UNIT_MOVED_TO_BASE"); AssertExit(run.State, flow);
        await Export("real-return-invalidates-incarnation-" + flow, initial, run);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task ActualSpellControlKeepsOrRemakesBothEchoTargetsWithoutLosingTheCapturedDeclaration(bool flow, bool remake)
    {
        var initial = Put(Position(flow, true, true), "STEAL", "OGN·080/298", "P2", "HAND"); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], OptionalCosts: ["ECHO"], Destination: "BASE:P1"));
        var id = Assert.Single(run.State.StackItems).StackItemId; await run.Act("P1", new PassPriorityCommand());
        await run.Act("P2", new PlayCardCommand("STEAL", "OGN·080/298", [id])); await run.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
        Assert.Equal("SPELL_CHOICE_REMAKE", pending.Kind); Assert.Equal("P2", pending.PlayerId);
        var paid = JsonSerializer.Serialize(pending.Preview!.RunePools, Json);
        await Reject(run, "P1", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        if (remake) {
            for (var index = 1; index <= 2; index++) {
                pending = run.State.PendingRuleChoice!.Request;
                var option = Assert.Single(pending.Options, option => option.Label.Contains("第 " + index + " 次", StringComparison.Ordinal)
                    && option.Label.Contains("重选目标 1 为", StringComparison.Ordinal) && option.ObjectIds?.Contains("ALLY2") == true);
                await run.Act("P2", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", [option.Id]));
            }
            pending = run.State.PendingRuleChoice!.Request; await run.Act("P2", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", ["CONFIRM"]));
        } else await run.Act("P2", new PayCostCommand(pending.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        var controlled = Assert.Single(run.State.StackItems); Assert.Equal("P2", controlled.ControllerId);
        Assert.All(controlled.RepeatExecutions!, execution => {
            Assert.Equal(remake ? "P2" : "P1", execution.MovementDeclarationPlayerId); Assert.Equal("BASE:P1", execution.Destination);
            Assert.Equal(new[] { remake ? "ALLY2" : "ALLY" }, execution.TargetObjectIds);
        });
        Assert.Equal(paid, JsonSerializer.Serialize(run.State.RunePools, Json));
        await Export($"actual-control-{flow}-{remake}-pending", initial, run); await run.Top();
        Assert.Equal("BASE", run.State.ObjectLocations[remake ? "ALLY2" : "ALLY"].Zone);
        Assert.Equal(remake ? 1 : 0, run.Events().Count(item => item.Event.Kind == "SPELL_CHOICES_REMADE"));
        AssertExit(run.State, flow); await Export($"actual-control-{flow}-{remake}-done", initial, run);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ACoordinatedDestinationViewForgeryStillFailsUntouchedOriginalCommandAndEventReplay(bool flow)
    {
        var initial = Position(flow, true, true); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("SPELL", No, ["ALLY"], OptionalCosts: ["ECHO"], Destination: "BASE:P1"));
        var commandHash = MatchStateHasher.HashValue(run.Commands()); var eventHash = MatchStateHasher.HashValue(run.Events());
        var item = Assert.Single(run.State.StackItems);
        var forged = run.State with { StackItems = [item with { Destination = "BASE:P2",
            RepeatExecutions = item.RepeatExecutions!.Select(execution => execution with { Destination = "BASE:P2" }).ToArray() }] };
        var frame = Frame(initial, run, forged); Assert.Empty(frame.ValidationErrors);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), forged, new CoreRuleEngine(), default, run.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(commandHash, MatchStateHasher.HashValue(run.Commands())); Assert.Equal(eventHash, MatchStateHasher.HashValue(run.Events()));
        await Export("original-log-forgery-control-" + flow, initial, run);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN105_EVIDENCE") is { Length: > 0 } output)
            await File.WriteAllTextAsync(Path.Combine(output, "forged-destination-" + flow + ".json"), JsonSerializer.Serialize(new { frame, replay }, Json));
    }

    private static MatchState Position(bool flow = false, bool toBase = false, bool echo = false)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Add(string id, string no, string owner, string zone, string? field = null) {
            cards[id] = Printed(id, no, owner); locations[id] = new(owner, zone, field);
        }
        Add("SPELL", No, "P1", flow ? "GRAVEYARD" : "HAND");
        Add("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); Add("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = toBase ? "P1" : null }; cards["OTHER"] = cards["OTHER"] with { ControllerId = toBase ? "P2" : null };
        foreach (var (id, owner, field) in new[] { ("ALLY", "P1", "BF"), ("ALLY2", "P1", "BF"), ("ENEMY", "P2", "OTHER"), ("ENEMY2", "P2", "OTHER") }) {
            Add(id, "OGN·175/298", owner, toBase ? "BATTLEFIELD" : "BASE", toBase ? field : null);
            cards[id] = cards[id] with { IsExhausted = true }; Assert.Equal(3, cards[id].Power);
        }
        Add("GEAR", "SFD·022/221", "P1", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var owner in new[] { "P1", "P2" }) {
            Add(owner + "-PRIVATE-HAND", "OGN·219/298", owner, "HAND");
            var deck = Enumerable.Range(1, 3).Select(index => owner + "-PRIVATE-DECK-" + index).ToArray();
            foreach (var id in deck) Add(id, "OGN·219/298", owner, "MAIN_DECK");
            Add(owner + "-PRIVATE-RUNE", "UNL-R03", owner, "RUNE_DECK");
            zones[owner] = PlayerZones.Empty with { MainDeck = deck, RuneDeck = [owner + "-PRIVATE-RUNE"],
                Hand = locations.Where(pair => pair.Value.PlayerId == owner && pair.Value.Zone == "HAND").Select(pair => pair.Key).ToArray(),
                Base = locations.Where(pair => pair.Value.PlayerId == owner && pair.Value.Zone == "BASE").Select(pair => pair.Key).ToArray(),
                Battlefields = locations.Where(pair => pair.Value.PlayerId == owner && pair.Value.Zone == "BATTLEFIELD").Select(pair => pair.Key).ToArray(),
                Graveyard = locations.Where(pair => pair.Value.PlayerId == owner && pair.Value.Zone == "GRAVEYARD").Select(pair => pair.Key).ToArray() };
        }
        var state = new MatchState("VEN105-CHOSEN-MOVEMENT", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, seed: 424242,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 20 }),
                ["P2"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20, [RuneTrait.Green] = 20, [RuneTrait.Red] = 20, [RuneTrait.Purple] = 20 }) })
            with { CardPlays = new(5, [], []) };
        return echo ? state with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(state.UntilEndOfTurnEffects, "P1") } : state;
    }
    private static CardObjectState Printed(string id, string no, string owner)
        => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static MatchState Put(MatchState state, string id, string no, string owner, string zone)
        => At(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = Printed(id, no, owner) } }, id, owner, zone);
    private static MatchState At(MatchState state, string id, string owner, string zone, string? field = null)
    {
        var zones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value with {
            Hand = pair.Value.Hand.Where(value => value != id).ToArray(), Base = pair.Value.Base.Where(value => value != id).ToArray(),
            Battlefields = pair.Value.Battlefields.Where(value => value != id).ToArray(), Graveyard = pair.Value.Graveyard.Where(value => value != id).ToArray(),
            MainDeck = pair.Value.MainDeck.Where(value => value != id).ToArray(), Banished = pair.Value.Banished.Where(value => value != id).ToArray() });
        zones[owner] = zone switch {
            "HAND" => zones[owner] with { Hand = [..zones[owner].Hand, id] }, "BASE" => zones[owner] with { Base = [..zones[owner].Base, id] },
            "BATTLEFIELD" => zones[owner] with { Battlefields = [..zones[owner].Battlefields, id] }, "GRAVEYARD" => zones[owner] with { Graveyard = [..zones[owner].Graveyard, id] },
            "MAIN_DECK" => zones[owner] with { MainDeck = [..zones[owner].MainDeck, id] }, _ => zones[owner] with { Banished = [..zones[owner].Banished, id] } };
        return state with { PlayerZones = zones, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone, field) } };
    }
    private static void AssertExit(MatchState state, bool flow)
    {
        Assert.Single(flow ? state.PlayerZones["P1"].Banished : state.PlayerZones["P1"].Graveyard, id => id == "SPELL");
        Assert.DoesNotContain("SPELL", flow ? state.PlayerZones["P1"].Graveyard : state.PlayerZones["P1"].Banished);
    }
    private static async Task Reject(Driver run, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)[player].PromptId; raw["snapshotTick"] = run.State.Tick;
        await run.ActRetained(new(player, "ven105-reject-" + run.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), false);
    }
    private static MatchRecoveryFrame Frame(MatchState initial, Driver run, MatchState? state = null)
    {
        state ??= run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, run.Commands(), run.Events(), views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
    }
    private static async Task Export(string name, MatchState initial, Driver run)
    {
        await run.Replay(); var frame = Frame(initial, run); Assert.Empty(frame.ValidationErrors);
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var player in run.State.Seats.Keys) {
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(run.State)[player], Json), JsonSerializer.SerializeToNode(restored.SnapshotFor(player), Json)));
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(run.State)[player], Json), JsonSerializer.SerializeToNode(restored.PromptFor(player), Json)));
        }
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN105_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); var path = Path.Combine(output, name + ".json");
        using var file = new FileStream(path, FileMode.CreateNew); JsonSerializer.Serialize(file, new { initial, frame }, Json);
    }
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
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
            var id = "ven105-" + entries.Count;
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
