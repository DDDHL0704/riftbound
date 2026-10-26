using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PatchedPorobotTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task RealDualTypePlayIsExhaustedAndChecksOtherEquipmentOnlyAtPlay(int equipment)
    {
        var initial = Position(equipment);
        var result = await Act(initial, "P1", new PlayCardCommand("UNIT", "VEN·058", []));
        var state = result.State; var card = state.CardObjects["UNIT"];
        Assert.Contains(CardObjectTags.UnitCard, card.Tags); Assert.Contains(CardObjectTags.EquipmentCard, card.Tags);
        Assert.True(card.IsExhausted); Assert.Equal(2, card.Power); Assert.Equal(2, card.ManaCost);
        Assert.Contains("UNIT", state.PlayerZones["P1"].Base);
        Assert.Equal(3, Assert.Single(state.CardPlays.Finalized).Types);
        Assert.Equal(3, Assert.Single(state.CardPlays.Resolved).Types);
        Assert.DoesNotContain("sourcePlayDrawObservation", JsonSerializer.Serialize(result.Events));
        if (equipment < 3) { Assert.Empty(state.StackItems); Restore(state); return; }
        var skill = Assert.Single(state.StackItems); Assert.Equal(equipment, skill.SourcePlayDraw!.OtherEquipment.Count);
        Assert.False(skill.PermanentPlayTargetsPending); Assert.Null(skill.PlayCost);
        Assert.Empty(state.PlayerZones["P1"].Hand); Restore(state);
        var expected = state.PlayerZones["P1"].MainDeck[0];
        state = (await Top(state)).State;
        Assert.Equal(new[] { expected }, state.PlayerZones["P1"].Hand);
        Assert.Empty(state.StackItems); Restore(state);
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("hidden")]
    [InlineData("hand")]
    [InlineData("unit")]
    public async Task ThirdObjectMustBeAnotherControlledPublicFieldEquipment(string invalid)
    {
        var state = Position(3); var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        cards["E2"] = invalid switch {
            "opponent" => cards["E2"] with { ControllerId = "P2" },
            "hidden" => cards["E2"] with { IsFaceDown = true },
            "unit" => cards["E2"] with { Tags = [CardObjectTags.UnitCard] }, _ => cards["E2"] };
        state = state with { CardObjects = cards };
        if (invalid == "hand") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "E2").ToArray(), Hand = [..state.PlayerZones["P1"].Hand, "E2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["E2"] = new("P1", "HAND") } };
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "VEN·058", []));
        Assert.Empty(played.State.StackItems); Assert.DoesNotContain(played.Events, e => e.Kind == "TRIGGER_QUEUED");
    }

    [Fact]
    public async Task ReturningTheRealSourceDoesNotCancelItsAlreadyCapturedDraw()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(3), "RETURN", "OGN·104/298", "P1");
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", "VEN·058", []));
        var returned = await Act(played.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        var state = (await Top(returned.State)).State;
        Assert.Contains("UNIT", state.PlayerZones["P1"].Hand); Assert.Single(state.StackItems);
        Restore(state); var top = state.PlayerZones["P1"].MainDeck[0];
        state = (await Top(state)).State;
        Assert.Contains(top, state.PlayerZones["P1"].Hand); Assert.Contains("UNIT", state.PlayerZones["P1"].Hand);
        Assert.Empty(state.StackItems); Restore(state);
    }

    [Fact]
    public async Task SpendingCountedGoldInResponseDoesNotRecheckTheAdditionalTriggerCondition()
    {
        var initial = Position(3);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["E2"] = factory.CreateObject("E2", "P1", "P1") with { IsExhausted = false } } };
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", "VEN·058", []));
        var response = await Act(played.State, "P1", new ActivateAbilityCommand("E2", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        var state = response.State;
        Assert.False(state.CardObjects.ContainsKey("E2")); Assert.Single(state.StackItems);
        Restore(state); var handCount = state.PlayerZones["P1"].Hand.Count;
        state = (await Top(state)).State; Assert.Equal(handCount + 1, state.PlayerZones["P1"].Hand.Count); Restore(state);
    }

    [Fact]
    public async Task RealImageGainingTheDualTypeFaceAfterEntryDoesNotRetroactivelyTriggerTheDraw()
    {
        var initial = Position(3);
        var porobot = initial.CardObjects["UNIT"];
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = porobot with { IsExhausted = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Hand = [], Base = [..initial.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
        initial = AttackDamageDistributionTests.Spell(initial, "COPY", "UNL-200/219", "P1");
        var copied = await Top((await Act(initial, "P1", new PlayCardCommand("COPY", "UNL-200/219", ["UNIT"]))).State);
        copied = await Top(copied.State);
        var token = copied.State.CardObjects["COPY-TOKEN-001"];
        Assert.Equal("VEN·058", token.CardNo); Assert.Contains(CardObjectTags.EquipmentCard, token.Tags);
        Assert.Empty(copied.State.StackItems);
        Assert.DoesNotContain(copied.State.CardPlays.Resolved, e => e.Source.ObjectId == token.ObjectId);
        Assert.Empty(copied.State.PlayerZones["P1"].Hand); Restore(copied.State);
    }

    [Fact]
    public async Task CountUsesTheControllerRatherThanTheOwnerOfOtherEquipment()
    {
        var state = Position(3);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["E2"] = state.CardObjects["E2"] with { OwnerId = "P2" } } };
        var played = await Act(state, "P1", new PlayCardCommand("UNIT", "VEN·058", []));
        Assert.NotNull(Assert.Single(played.State.StackItems).SourcePlayDraw); Restore(played.State);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("count")]
    [InlineData("duplicate")]
    [InlineData("generation")]
    [InlineData("tick")]
    [InlineData("printing")]
    [InlineData("missing")]
    public async Task RecoveryIndependentlyRejectsForgedConditionalDrawContext(string mutation)
    {
        var state = (await Act(Position(3), "P1", new PlayCardCommand("UNIT", "VEN·058", []))).State;
        var item = Assert.Single(state.StackItems); var context = item.SourcePlayDraw!;
        var changed = mutation switch {
            "source" => context with { OtherEquipment = [context.OtherEquipment[0] with { Source = new("UNIT", 1) }, ..context.OtherEquipment.Skip(1)] },
            "count" => context with { OtherEquipment = context.OtherEquipment.Take(2).ToArray() },
            "duplicate" => context with { OtherEquipment = [context.OtherEquipment[0], context.OtherEquipment[0], context.OtherEquipment[2]] },
            "generation" => context with { SourceGeneration = 999 }, "tick" => context with { CapturedTick = state.Tick + 1 },
            "printing" => context with { CardNo = "OGN·096/298" }, _ => null };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { SourcePlayDraw = changed }] }),
            e => e.Contains("source play draw", StringComparison.Ordinal)); Restore(state);
    }

    [Fact]
    public async Task ExactPrintedClauseIsParsedWithoutAGenericDuplicateTrigger()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == "VEN·058");
        Assert.Equal(37943, card.Id); Assert.Equal(new[] { "单位", "装备" }, card.CardCategoryNameList);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers);
        Assert.Equal(TriggerKinds.SourceUnitPlayedOtherEquipmentDraw, spec.Kind);
        Assert.Equal(TriggerTimings.SourceUnitPlayed, spec.Timing); Assert.Equal(3, spec.RequiredOtherControlledEquipment); Assert.Equal(1, spec.DrawCount);
        using var raw = JsonDocument.Parse(File.ReadAllText(OfficialCardCatalog.DefaultCatalogPath()));
        Assert.False(Assert.Single(raw.RootElement.GetProperty("cards").EnumerateArray(),
            c => c.GetProperty("cardNo").GetString() == "VEN·058").GetProperty("isPreview").GetBoolean());
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("reverse", true)]
    [InlineData("unknown", false)]
    [InlineData("empty", false)]
    [InlineData("duplicate", false)]
    [InlineData("mismatch", false)]
    public async Task SchemaValidatesEachOfficialTypeWithoutAcceptingUnknownOrInconsistentCategories(string branch, bool valid)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = catalog.Cards.Single(c => c.CardNo == "VEN·058");
        card = branch switch {
            "reverse" => card with { CardCategoryName = "装备 / 单位", CardCategoryNameList = ["装备", "单位"] },
            "unknown" => card with { CardCategoryName = "单位 / 未知", CardCategoryNameList = ["单位", "未知"] },
            "empty" => card with { CardCategoryNameList = [] },
            "duplicate" => card with { CardCategoryName = "单位 / 单位", CardCategoryNameList = ["单位", "单位"] },
            "mismatch" => card with { CardCategoryName = "单位" }, _ => card };
        var result = OfficialCardSchemaValidator.Validate(catalog with {
            Cards = catalog.Cards.Select(c => c.CardNo == card.CardNo ? card : c).ToArray() });
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Contains(result.Violations, v => v.CardNo == card.CardNo && v.Field == nameof(card.CardCategoryName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ProductionDualTypePlayCommandsColdRecoverDeduplicateAndReplay(int equipment)
    {
        var initial = Position(equipment); var state = initial; var engine = new CoreRuleEngine();
        var journal = new NativeJournal(); var session = new MatchSession(state, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_POROBOT_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, "equipment-" + equipment);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var steps = new List<string>(); var native = 0;
        void Export(string name, object value) { if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json)); }
        Export("initial.json", initial);
        await Submit("play", "P1", new PlayCardCommand("UNIT", "VEN·058", [], OptionalCosts: []));
        if (equipment >= 3) for (var i = 0; i < 2; i++) await Submit("pass-" + i, state.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(state.StackItems); Assert.Equal(equipment >= 3 ? 1 : 0, state.PlayerZones["P1"].Hand.Count);
        Assert.Equal(3, Assert.Single(state.CardPlays.Finalized).Types); Assert.Equal(3, Assert.Single(state.CardPlays.Resolved).Types);
        Assert.True(state.CardObjects["UNIT"].IsExhausted);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), state, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { equipment, native, commands = journal.Entries.Count,
            events = Events().Length, restores = steps.Count, finalized = state.CardPlays.Finalized, resolved = state.CardPlays.Resolved, replay });
        if (native > 0) Assert.Equal(steps.Count, native);
        async Task Submit(string step, string player, GameCommand fallback)
        {
            var prompt = session.PromptFor(player); Export(step + "-prompt.json", prompt); Export(step + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(step + "-quote.json", engine.PreviewPlayCard(state, player, new("quote", prompt.PromptId!, state.Tick, play)));
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(state.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            if (dir is not null && File.Exists(Path.Combine(dir, step + "-command.json"))) {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-command.json")));
                raw = doc.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++; }
            var result = await session.SubmitAsync(player, step, command, raw, default); Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State; Restore(state); steps.Add(step);
            var count = journal.Entries.Count; Assert.True((await session.SubmitAsync(player, step, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            session = MatchSession.Restore(new(initial.RoomId, state.Tick, journal.Entries[^1].CompletedEventSequence,
                Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], state, initial), engine, journal);
            Assert.True((await session.SubmitAsync(player, step, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var stale = await session.SubmitAsync(player, "stale-" + step, command, raw, default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(stale.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
    }

    private sealed class NativeJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    internal static MatchState Position(int equipment)
    {
        var state = PermanentPlayTargetTests.Position("VEN·058");
        var cards = state.CardObjects.Where(e => e.Key != "GEAR").ToDictionary(e => e.Key, e => e.Value);
        var own = state.PlayerZones["P1"];
        var ids = Enumerable.Range(0, equipment).Select(i => "E" + i).ToArray();
        foreach (var id in ids) cards[id] = new(id, cardNo: "OGN·072/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]);
        return state with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = own with { Base = own.Base.Where(id => id != "GEAR").Concat(ids).ToArray() } },
            ObjectLocations = state.ObjectLocations.Where(e => e.Key != "GEAR").Concat(ids.Select(id => new KeyValuePair<string, ObjectLocationState>(id, new("P1", "BASE")))).ToDictionary(e => e.Key, e => e.Value) };
    }
}
