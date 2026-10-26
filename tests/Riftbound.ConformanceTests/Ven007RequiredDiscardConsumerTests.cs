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

public sealed class Ven007RequiredDiscardConsumerTests
{
    private const string No = "VEN·007";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactNewPrintingKeepsAllPriorCatalogGroupsAndMembers()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var priorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1081, priorCards.Length); Assert.Equal(1082, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(priorCards); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(862, prior.Count); Assert.Equal(863, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
    }

    [Fact]
    public async Task ExactCnPrintingKeepsItsRequiredDiscardDistinctFromAFreeResourceCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37692, card.Id); Assert.Equal("拳拳魄罗", card.CardName);
        Assert.Equal("{{强化}} — 弃置一张手牌（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+1。", card.CardEffect);
        Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(2, card.Power);
        Assert.Equal([RuneTrait.Red], card.CardColorList); Assert.Equal("魄罗", card.Tag);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal("弃置一张手牌", cost.AdditionalCost); Assert.False(cost.SupportedResources);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.Equal(1, EmpowerParser.Self(card.CardEffect).Might);
    }

    [Theory]
    [InlineData("OGN·096/298")] [InlineData("VEN·049")] [InlineData("SFD·022/221")]
    public async Task ActualRequiredPaymentDiscardsOneChosenHandCardBeforeEmpowerResolves(string paymentCard)
    {
        var initial = Add(VenUnitEntryBasicTests.Position(No), "FODDER", paymentCard);
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var paymentGeneration = game.State.CardObjects["FODDER"].ObjectGeneration;
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
            Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal("GRAVEYARD", game.State.ObjectLocations["FODDER"].Zone);
            Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
            Assert.Equal(new ObjectBinding("FODDER", paymentGeneration), game.State.StackItems.Single().EmpowerActivation!.DiscardedHandCard);
            JsonRestore(initial, game);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
            Assert.Equal(3, Might(game.State)); Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("required-discard-" + paymentCard.Replace('/', '-'), initial, game); }
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("two")] [InlineData("opponent")] [InlineData("base")]
    [InlineData("graveyard")] [InlineData("rune")] [InlineData("unknown")] [InlineData("source")] [InlineData("target")]
    public async Task ActualInvalidRequiredSelectionsCannotDiscardAnythingOrPartlyActivate(string branch)
    {
        var initial = Add(Position(), "ENEMY-HAND", "VEN·049", "P2");
        var rune = OfficialGarenConquestBattlefieldCountTests.Printed("COST-RUNE", "UNL-R01", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [rune.ObjectId] = rune },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [rune.ObjectId] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, rune.ObjectId] } } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            var id = branch switch { "opponent" => "ENEMY-HAND", "base" => "ALLY", "graveyard" => "GRAVE", "rune" => "COST-RUNE", "unknown" => "ABSENT", "source" => "UNIT", _ => "FODDER" };
            string[] costs = branch switch {
                "missing" or "target" => [], "duplicate" => ["DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:FODDER"],
                "two" => ["DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:SPARE"], _ => ["DISCARD_HAND_CARD:" + id] };
            await Reject(game, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", branch == "target" ? ["FODDER"] : [], OptionalCosts: costs));
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(game.State.StackItems);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Hand); Assert.Contains("SPARE", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("invalid-" + branch, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualCostChoicesExposeOnlyOwnEligibleHandCardsAndNeverAutofillTheRequiredChoice(bool hasHand)
    {
        var initial = hasHand ? Position() : VenUnitEntryBasicTests.Position(No);
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"])
                .Where(row => (string?)row["sourceObjectId"] == "UNIT" && (string?)row["abilityId"] == "EMPOWER:0:0").ToArray();
            if (hasHand)
            {
                var row = Assert.Single(rows); Assert.Equal(0, row["minTargetCount"]); Assert.Equal(0, row["maxTargetCount"]);
                Assert.Contains("弃置", Assert.IsType<string>(row["abilityLabel"]));
                var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["optionalCostChoices"]);
                Assert.Equal(new[] { "DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:SPARE" }, choices.Select(c => c.Id).Where(id => id.StartsWith("DISCARD_HAND_CARD:", StringComparison.Ordinal)).Order());
                Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
                var required = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]).ToArray();
                Assert.Equal(new[] { "DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:SPARE" }, required.Select(c => c.Id).Order());
                Assert.All(required, choice => Assert.Equal(choice.Id["DISCARD_HAND_CARD:".Length..], Assert.Single(choice.ObjectIds!)));
                Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(row["requiredOptionalCosts"]));
            }
            else foreach (var row in rows)
                Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["optionalCostChoices"]), c => c.Id.StartsWith("DISCARD_HAND_CARD:", StringComparison.Ordinal));
            foreach (var visible in new[] { JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"], Json),
                JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"], Json), JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(game.State), Json) })
            { Assert.DoesNotContain("FODDER", visible, StringComparison.Ordinal); Assert.DoesNotContain("SPARE", visible, StringComparison.Ordinal); }
            await Reject(game, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("private-cost-choices-" + hasHand, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualSourceReturnKeepsTheDiscardPaidAndCannotCarryEmpowerToTheReplayedGeneration(bool before)
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", Paid()); if (!before) { await game.Top(); Assert.Equal(3, Might(game.State)); }
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(2, Might(game.State));
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
            if (before) { JsonRestore(initial, game); await game.Top(); }
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(2, Might(game.State)); Assert.Equal(1, game.State.PlayerZones["P1"].Graveyard.Count(id => id == "FODDER"));
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("source-return-" + before, initial, game); }
    }

    [Fact]
    public async Task ActualTargetFilteredCounterCannotTreatTheEmpowerSourceAsATargetOrRefundThePaidDiscard()
    {
        var initial = Add(Position(), "COUNTER", "SFD·045/221", "P2"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Paid());
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
            await game.Act("P1", new PassPriorityCommand());
            await Reject(game, new PlayCardCommand("COUNTER", "SFD·045/221", [item.StackItemId]), "P2");
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Act("P2", new PassPriorityCommand()); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, Might(game.State));
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("source-not-target-counter", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OriginalAcceptedCommandsRejectRefundedHandPaymentAndPrematureEmpower(bool premature)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); var before = game.State; await game.Act("P1", Paid()); JsonRestore(initial, game);
            var hash = MatchStateHasher.Hash(game.State); var commandHash = MatchStateHasher.HashValue(game.Commands()); var eventHash = MatchStateHasher.HashValue(game.Events());
            var forged = premature ? game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["UNIT"] = game.State.CardObjects["UNIT"] with { IsEmpowered = true } } } : Refund(game.State, before, "P1", "FODDER");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged, new CoreRuleEngine(), default, game.Events());
            Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors); Assert.Equal(hash, MatchStateHasher.Hash(game.State));
            Assert.Equal(commandHash, MatchStateHasher.HashValue(game.Commands())); Assert.Equal(eventHash, MatchStateHasher.HashValue(game.Events()));
            await game.Replay();
        }
        finally { await Save("forged-" + premature, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualCopiedFacePaysOnceWithItsExplicitOriginAndKeepsThePaidWitnessAfterDetach(bool detach)
    {
        var initial = Add(Add(ShadySpectaclesTests.Position(No), "FODDER", "VEN·049"), "DETACH", "SFD·011/221");
        var game = new Driver(initial);
        try
        {
            var basis = ObjectMight(game.State, "HOST"); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var choice = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", choice.Request.Kind);
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal(No, game.State.CardObjects["HOST"].CardNo); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
            var row = Assert.Single(rows, r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == "EMPOWER:0:0");
            var instance = Assert.IsType<string>(row["abilityInstanceId"]);
            var activate = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);
            await Reject(game, activate); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Hand);
            var paidGeneration = game.State.CardObjects["FODDER"].ObjectGeneration;
            await game.Act("P1", activate with { AbilityInstanceId = instance });
            var captured = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal("HOST", captured.Source.ObjectId); Assert.Equal(generation, captured.Source.Generation);
            Assert.Equal("GEAR", captured.Origin!.Equipment.ObjectId);
            Assert.Equal(new ObjectBinding("FODDER", paidGeneration), captured.DiscardedHandCard);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); JsonRestore(initial, game);
            if (detach)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
                Assert.Null(game.State.CardObjects["HOST"].AttachmentUnitCopies);
                Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
                Assert.Equal(captured, Assert.Single(game.State.StackItems).EmpowerActivation);
                JsonRestore(initial, game);
            }
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(detach ? basis : 3, ObjectMight(game.State, "HOST"));
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(1, game.State.PlayerZones["P1"].Graveyard.Count(id => id == "FODDER"));
            Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DISCARDED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "FODDER"));
            await game.Replay(); JsonRestore(initial, game);
        }
        finally { await Save("copied-cost-origin-" + detach, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningDiscardsANaturallyDrawnCardAndRejectsACoherentRefundInProductionColdRecovery(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "jhin-lowcurve", "UNL-R01", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 4, No); await game.Tap(actor);
            var unit = game.Hand(actor, No).First(); var mana = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(unit, No, [])); var before = game.State;
            var payment = before.PlayerZones[actor].Hand.First(); var generation = before.CardObjects[unit].ObjectGeneration;
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:" + payment]));
            Assert.Contains(payment, game.State.PlayerZones[actor].Graveyard); Assert.DoesNotContain(payment, game.State.PlayerZones[actor].Hand);
            Assert.Equal(mana - 2, game.State.RunePools[actor].Mana); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
            await game.ColdCheckpoint(); await game.RejectCoordinatedForgery("refunded-required-discard", Refund(game.State, before, actor, payment));
            var item = Assert.Single(game.State.StackItems); var other = game.State.PlayerZones[actor].Hand.First();
            var invented = game.State with { StackItems = [item with { EmpowerActivation = item.EmpowerActivation! with {
                DiscardedHandCard = new(other, game.State.CardObjects[other].ObjectGeneration) } }] };
            await game.RejectCoordinatedForgery("invented-paid-hand-card-binding", invented);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(2, game.State.CardObjects[unit].Power); Assert.Equal(3, game.Might(actor, unit));
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration); Assert.Equal(3, game.Might(actor, unit)); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven007-" + actor); }
    }

    private static MatchState Position() => Add(Add(VenUnitEntryBasicTests.Position(No), "FODDER", "OGN·096/298"), "SPARE", "VEN·049");
    private static ActivateAbilityCommand Paid() => new("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static MatchState Refund(MatchState state, MatchState before, string actor, string card) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [card] = before.CardObjects[card] },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [card] = before.ObjectLocations[card] },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [actor] = state.PlayerZones[actor] with { Hand = before.PlayerZones[actor].Hand, Graveyard = before.PlayerZones[actor].Graveyard } } };
    private static async Task Reject(Driver game, GameCommand command, string actor = "P1")
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)[actor].PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new(actor, "ven007-reject-" + game.Commands().Length, JsonSerializer.SerializeToElement(raw, Json)), false);
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
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static int ObjectMight(MatchState state, string id) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN007_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame consumer reproduction, including an original rejected command when unsupported; not canonical-opening or certification evidence",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), runtime, ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
