using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

public sealed class VenRunePrintingConsumerTests
{
    private sealed record Printing(string No, int Id, string Name, string Trait, string SourceHash);
    private static readonly Printing[] Printings = [
        new("VEN·R01", 37910, "炽烈符文", "red", "715da066bf8d32c194d55ebea9cb845d0be370781c587a02bc23e1d8e8abd0d1"),
        new("VEN·R01a", 37927, "炽烈符文", "red", "93ac3b8974de490f152f27b64cfee534c9698d28138a4b4a6f504b3779fb4ce1"),
        new("VEN·R01b", 37961, "炽烈符文", "red", "979aeb97183eede45c4402071d078f2257228741b3608f27057212add00d42c5"),
        new("VEN·R02", 37911, "翠意符文", "green", "e03ad9d56747ac46779eb3ff0ffee9143fea64e6523f347ed232175788f45cad"),
        new("VEN·R02a", 37928, "翠意符文", "green", "b2dc627680fa0377acf8decd2381417381e556e6e1418b1c2774f5425f3ff16a"),
        new("VEN·R02b", 37962, "翠意符文", "green", "68e1e64e31dc5b89e52abc3e9923a9d672b2778e8b15786830b6ecb8be243cfb"),
        new("VEN·R03", 37912, "灵光符文", "blue", "db52f6b8d7315b0043f1d1274b3d8df0e763fffa5695622f31b6fd58fb7f1fa6"),
        new("VEN·R03a", 37929, "灵光符文", "blue", "c7462ddc05b74f57fcc7c4d7106081bcb56c787862a916ae47802a67866fcdbc"),
        new("VEN·R03b", 37963, "灵光符文", "blue", "f6c3cd077fd7fbc7952334e0d990cd889a577c5a9cad37dd638ffdbeb46d4ca9"),
        new("VEN·R04", 37913, "摧破符文", "orange", "9cac45f4cf51841590f279733378709d6bfa01764026e2a2a6381082467c7e15"),
        new("VEN·R04a", 37930, "摧破符文", "orange", "de41df8ca955789269b69bff1455686b74ad57ea51ce1013b6d288dfcaa52484"),
        new("VEN·R04b", 37964, "摧破符文", "orange", "3c8aa788ff008536cb5ba20f939a5971c5c8ce8654daaf15a75829069cd2b9d6"),
        new("VEN·R05", 37914, "混沌符文", "purple", "0c677f7c38f2612544cae43cb74299b314ab28af73bc5afa88a91244c4e757a9"),
        new("VEN·R05a", 37931, "混沌符文", "purple", "3f59348b0eadfa42ef74b87283b085df416a6f52c377e454a975a2b60e282e49"),
        new("VEN·R05b", 37965, "混沌符文", "purple", "0510f4cac396481da839ab013b48c5328eac2558a64174116efa10658495cd3a"),
        new("VEN·R06", 37915, "序理符文", "yellow", "d3fea063aeaec6de85fc6767aa4482dd15a5898816122f4f0f8cfd2eef5ad2fc"),
        new("VEN·R06a", 37932, "序理符文", "yellow", "f5f9164b344a40800f5ad2a736ce68358fbbf03c9ad18d211ad6f067ce1c530d"),
        new("VEN·R06b", 37966, "序理符文", "yellow", "13cbb29fc48acf17bd2be7ab912dcd299226e789d2a1bbeeb05f5ffd0bd57d31"),
    ];
    private static readonly string[] Traits = ["red", "green", "blue", "orange", "purple", "yellow"];
    private static readonly string[] Controls = ["OGN·007/298", "OGN·042/298", "OGN·089/298", "OGN·126/298", "OGN·166/298", "OGN·214/298"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static IEnumerable<object[]> RunePrintings => Printings.Select(p => new object[] { p.No, p.Id, p.Name, p.Trait, p.SourceHash });
    public static IEnumerable<object[]> RuneNamesAndTraits => Printings.Select(p => new object[] { p.No, p.Trait });
    public static IEnumerable<object[]> RuneTraits => Traits.Select(t => new object[] { t });

    [Theory, MemberData(nameof(RunePrintings))]
    public async Task ExactPrintingRetainsItsOfficialIdentityAndBasicRuneDomain(string no, int id, string name, string trait, string sourceHash)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal("符文", card.CardCategoryName);
        Assert.Equal(new[] { trait }, card.CardColorList); Assert.Equal("", card.CardEffect);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(OfficialCardCatalog.DefaultCatalogPath()));
        var row = document.RootElement.GetProperty("cards").EnumerateArray().Single(r => r.GetProperty("cardNo").GetString() == no);
        Assert.Equal(sourceHash, CanonicalHash(row));
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        object?[] args = ["RUNE", "P1", no, null];
        Assert.True((bool)factory.GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!);
        var rune = Assert.IsType<CardObjectState>(args[3]);
        Assert.Equal(no, rune.CardNo); Assert.Equal("P1", rune.OwnerId);
        Assert.Contains(CardObjectTags.RuneCard, rune.Tags); Assert.Contains("COLOR:" + trait, rune.Tags);
        Assert.DoesNotContain(CardObjectTags.UnitCard, rune.Tags); Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
        var spec = Specs(catalog).Single(s => s.CardNo == no);
        Assert.Equal(OfficialRuleDomainBehaviorCatalog.RuneResourceDomainEffectKind, spec.ImplementedEffectKind);
    }

    [Theory, MemberData(nameof(RuneNamesAndTraits))]
    public async Task MatchingLegendCanUseTwelveCopiesOfAnyExactRuneArt(string no, string trait)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = Template(catalog, trait, true).Decklist with { RuneDeck = Enumerable.Repeat(no, 12).ToArray() };
        var result = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory, MemberData(nameof(RuneNamesAndTraits))]
    public async Task OffTraitRuneArtCannotEnterTheRuneDeck(string no, string trait)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var template = Template(catalog, trait, false);
        Assert.True(OfficialDeckValidator.Validate(template.Decklist, catalog).IsValid);
        var result = OfficialDeckValidator.Validate(template.Decklist with { RuneDeck = Enumerable.Repeat(no, 12).ToArray() }, catalog);
        Assert.False(result.IsValid); Assert.Contains(result.Errors, e => e.Contains(no, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EighteenPrintingsReuseTheSixExistingRuneFunctionalUnits()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var specs = Specs(catalog);
        Assert.Equal(6, Printings.Select(p => specs.Single(s => s.CardNo == p.No).FunctionalUnitId).Distinct(StringComparer.Ordinal).Count());
        for (var i = 0; i < Traits.Length; i++)
        {
            var original = specs.Single(s => s.CardNo == Controls[i]);
            foreach (var printing in Printings.Where(p => p.Trait == Traits[i]))
                Assert.Equal(original.FunctionalUnitId, specs.Single(s => s.CardNo == printing.No).FunctionalUnitId);
        }
    }

    [Theory, MemberData(nameof(RuneTraits))]
    public async Task CanonicalOpeningExercisesBothSeatsRuneResourcesBatchesAndReentry(string trait)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var numbers = Printings.Where(p => p.Trait == trait).Select(p => p.No).ToArray();
        var deck = Template(catalog, trait, true).Decklist with { RuneDeck = numbers.SelectMany(no => Enumerable.Repeat(no, 4)).ToArray() };
        Assert.True(OfficialDeckValidator.Validate(deck, catalog).IsValid);
        var initial = MatchReplayInitialStateBuilder.FromSeats("N22-VEN-RUNE-" + trait, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var driver = new Driver(initial); var lifecycle = new List<object>(); var cycled = new List<(string Player, string Id, string No, long Generation)>();
        try
        {
            foreach (var player in new[] { "P1", "P2" }) await driver.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await driver.Act("P1", new ReadyCommand()); await driver.Act("P2", new ReadyCommand());
            var active = driver.State.ActivePlayerId; var second = driver.State.OpeningSecondActionPlayerId!;
            await driver.Act(active, new MulliganCommand([])); await driver.Act(second, new MulliganCommand([]));
            for (var guard = 0; driver.State.PlayerZones.Any(e => e.Value.RuneDeck.Count > 0); guard++)
            {
                Assert.True(guard < 16, "Real turn channeling did not exhaust the initial rune decks.");
                await driver.Act(driver.State.TurnPlayerId!, new EndTurnCommand());
            }
            foreach (var player in new[] { "P1", "P2" })
            {
                if (driver.State.TurnPlayerId != player) await driver.Act(driver.State.TurnPlayerId!, new EndTurnCommand());
                Assert.Equal(12, driver.State.PlayerZones[player].Base.Count(id => driver.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)));
                foreach (var no in numbers)
                {
                    var id = driver.State.PlayerZones[player].Base.First(id => driver.State.CardObjects[id].CardNo == no);
                    var entered = driver.State.CardObjects[id]; Assert.Equal(1, entered.ObjectGeneration); Assert.False(entered.IsExhausted);
                    var mana = driver.State.RunePools[player].Mana;
                    await driver.Act(player, new TapRuneCommand(id)); Assert.Equal(mana + 1, driver.State.RunePools[player].Mana); Assert.True(driver.State.CardObjects[id].IsExhausted);
                    await driver.Act(player, new TapRuneCommand(id), false);
                    var before = driver.State; var power = before.RunePools[player].PowerByTrait.GetValueOrDefault(trait);
                    await driver.Act(player, new RecycleRuneCommand(id));
                    Assert.Equal(power + 1, driver.State.RunePools[player].PowerByTrait.GetValueOrDefault(trait));
                    Assert.Equal(before.PlayerZones[player].RuneDeck.Append(id), driver.State.PlayerZones[player].RuneDeck);
                    Assert.Equal(before.RngCursor, driver.State.RngCursor); Assert.DoesNotContain(id, driver.State.PlayerZones[player].Base);
                    Assert.Equal("RUNE_DECK", driver.State.ObjectLocations[id].Zone); Assert.Equal(no, driver.State.CardObjects[id].CardNo);
                    Assert.Equal(entered.ObjectGeneration + 1, driver.State.CardObjects[id].ObjectGeneration); Assert.False(driver.State.CardObjects[id].IsExhausted);
                    cycled.Add((player, id, no, entered.ObjectGeneration));
                    lifecycle.Add(new { player, no, id, enteredGeneration = entered.ObjectGeneration, recycledGeneration = driver.State.CardObjects[id].ObjectGeneration });
                    await driver.Act(player, new RecycleRuneCommand(id), false);
                }
                var available = numbers.Reverse().Select(no => driver.State.PlayerZones[player].Base.First(id => driver.State.CardObjects[id].CardNo == no && !driver.State.CardObjects[id].IsExhausted)).ToArray();
                var opponent = player == "P1" ? "P2" : "P1";
                var foreign = driver.State.PlayerZones[opponent].Base.First(id => driver.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
                await driver.Act(player, new TapRuneCommand(foreign), false);
                await driver.Act(player, new TapRuneCommand(available[0], [available[0], foreign, available[1]]), false);
                await driver.Act(player, new TapRuneCommand("NO-SUCH-RUNE"), false);
                var batchBefore = driver.State; var batchMana = batchBefore.RunePools[player].Mana;
                await driver.Act(player, new TapRuneCommand(available[0], available));
                Assert.Equal(batchMana + 3, driver.State.RunePools[player].Mana); Assert.All(available, id => Assert.True(driver.State.CardObjects[id].IsExhausted));
                var recycleBefore = driver.State; var typed = recycleBefore.RunePools[player].PowerByTrait.GetValueOrDefault(trait);
                await driver.Act(player, new RecycleRuneCommand(available[0], available));
                Assert.Equal(typed + 3, driver.State.RunePools[player].PowerByTrait.GetValueOrDefault(trait));
                Assert.Equal(recycleBefore.PlayerZones[player].RuneDeck.Concat(available), driver.State.PlayerZones[player].RuneDeck);
                Assert.Equal(recycleBefore.RngCursor, driver.State.RngCursor);
                foreach (var id in available)
                {
                    var original = batchBefore.CardObjects[id];
                    Assert.Equal(original.ObjectGeneration + 1, driver.State.CardObjects[id].ObjectGeneration);
                    cycled.Add((player, id, original.CardNo!, original.ObjectGeneration));
                }
                Assert.Equal(6, driver.State.RunePools[player].PowerByTrait.GetValueOrDefault(trait));
                Assert.Equal(6, driver.State.RunePools[player].Mana);
                Assert.All(driver.State.RunePools[player].PowerByTrait.Where(p => p.Key != trait), p => Assert.Equal(0, p.Value));
            }
            for (var guard = 0; cycled.Any(c => driver.State.ObjectLocations[c.Id].Zone != "BASE"); guard++)
            {
                Assert.True(guard < 10, "Recycled physical runes did not reenter by normal turn channeling.");
                await driver.Act(driver.State.TurnPlayerId!, new EndTurnCommand());
                Assert.All(driver.State.RunePools.Values, pool => { Assert.Equal(0, pool.Mana); Assert.Equal(0, pool.TotalPower); });
            }
            foreach (var c in cycled)
            {
                var rune = driver.State.CardObjects[c.Id]; Assert.Equal(c.No, rune.CardNo); Assert.Equal(c.Player, rune.OwnerId);
                Assert.Equal(c.Generation + 2, rune.ObjectGeneration); Assert.False(rune.IsExhausted);
                Assert.Empty(rune.UntilEndOfTurnEffects); Assert.Equal(c.Player, driver.State.ObjectLocations[c.Id].PlayerId);
            }
        }
        finally { await driver.Save(trait, lifecycle); }
    }

    private static PreconstructedDeck Template(OfficialCardCatalog catalog, string trait, bool matching)
        => PreconstructedDeckCatalog.Build(catalog).First(d => catalog.Cards.Single(c => c.CardNo == d.Decklist.LegendCardNo).CardColorList.Contains(trait) == matching);
    private static IReadOnlyList<BehaviorSpec> Specs(OfficialCardCatalog catalog)
        => BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards, CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName)).ToArray()));
    private static string CanonicalHash(JsonElement root)
    {
        using var output = new MemoryStream(); using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) Write(root, writer);
        return Convert.ToHexString(SHA256.HashData(output.ToArray())).ToLowerInvariant();
        static void Write(JsonElement node, Utf8JsonWriter writer)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Object: writer.WriteStartObject(); foreach (var p in node.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(p.Value, writer); } writer.WriteEndObject(); break;
                case JsonValueKind.Array: writer.WriteStartArray(); foreach (var child in node.EnumerateArray()) Write(child, writer); writer.WriteEndArray(); break;
                default: node.WriteTo(writer); break;
            }
        }
    }
    private sealed class Driver
    {
        internal MatchState Initial { get; } internal MatchState State { get; private set; }
        private Journal Log { get; } = new(); private IMatchSession Session { get; set; }
        private HashSet<string> EverPublic { get; } = []; private List<object> Steps { get; } = [];
        internal Driver(MatchState initial) { Initial = initial; State = initial; Session = new MatchSession(initial, new CoreRuleEngine(), Log); }
        private async Task<ResolutionResult> Send(string player, string intent, GameCommand command, JsonElement raw) => command switch
        {
            SubmitDeckCommand d => await Session.SubmitDeckAsync(player, intent, d, raw, default),
            ReadyCommand => await Session.ReadyAsync(player, intent, raw, default),
            _ => await Session.SubmitAsync(player, intent, command, raw, default)
        };
        internal async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = Session.PromptFor(player).PromptId; node["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(node, Json); var intent = "n22-" + Log.Entries.Count; var before = MatchStateHasher.Hash(State);
            var result = await Send(player, intent, GameCommandJsonMapper.Map(raw), raw); Assert.Equal(accepted, result.Accepted); State = result.State;
            if (!accepted) { Assert.Equal(before, MatchStateHasher.Hash(State)); Assert.Empty(result.Events); }
            var frame = Frame(); Assert.Empty(frame.ValidationErrors);
            foreach (var entry in State.ObjectLocations.Where(e => e.Value.Zone is "BASE" or "BATTLEFIELD" or "GRAVEYARD" or "BANISHED" or "LEGEND" or "CHAMPION")) EverPublic.Add(entry.Key);
            foreach (var viewer in State.Seats.Keys)
            {
                var text = JsonSerializer.Serialize(Session.SnapshotFor(viewer), Json) + JsonSerializer.Serialize(Session.PromptFor(viewer), Json);
                foreach (var opponent in State.Seats.Keys.Where(p => p != viewer)) foreach (var hidden in Hidden(opponent).Where(id => !EverPublic.Contains(id))) Assert.DoesNotContain(hidden, text, StringComparison.Ordinal);
            }
            var spectator = JsonSerializer.Serialize(frame.SpectatorReplayFrame, Json);
            foreach (var hidden in State.Seats.Keys.SelectMany(Hidden).Where(id => !EverPublic.Contains(id))) Assert.DoesNotContain(hidden, spectator, StringComparison.Ordinal);
            var copy = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
            // The real registry performs the full original canonical action/event audit before admission.
            // Avoid redundantly executing that same audit a second time on identical inputs here.
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), Log, new Store(copy)); Session = await registry.GetOrCreateAsync(State.RoomId, default);
            foreach (var viewer in State.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Session.SnapshotFor(viewer), Json), JsonSerializer.SerializeToNode(frame.PlayerViews[viewer].Snapshot, Json)));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Session.PromptFor(viewer), Json), JsonSerializer.SerializeToNode(frame.PlayerViews[viewer].Prompt, Json)));
            }
            var count = Log.Entries.Count; var duplicate = await Send(player, intent, GameCommandJsonMapper.Map(raw), raw);
            Assert.Equal(accepted, duplicate.Accepted); Assert.Equal(count, Log.Entries.Count); Assert.Equal(MatchStateHasher.Hash(State), MatchStateHasher.Hash(duplicate.State));
            Steps.Add(new { player, intent, raw, result.Accepted, result.ErrorCode, result.Events, structuralRecovery = "PASS", originalActionEventAuditViaActualRegistry = "PASS", actualRegistryAdmitted = true, coldDuplicateUnchanged = true, unrevealedIdentityPrivacy = true, state = State });
        }
        private IEnumerable<string> Hidden(string player) => State.PlayerZones[player].Hand.Concat(State.PlayerZones[player].MainDeck).Concat(State.PlayerZones[player].RuneDeck);
        private MatchRecoveryFrame Frame()
        {
            var commands = Log.Commands(); var events = Log.Events(); var sequence = events.Length == 0 ? 0 : events[^1].Sequence;
            var snapshots = ResolutionResult.BuildSnapshots(State); var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, State.Tick, sequence, snapshots[p], State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, commands, events, views, State, State.Tick, spectator);
            return new(State.RoomId, State.Tick, sequence, commands, events, views, errors, State, Initial, spectator) { OpeningSeed = Initial.Seed };
        }
        internal async Task Save(string trait, List<object> lifecycle)
        {
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_N22_ARTIFACT_DIR"); if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            var runtime = new { processId = Environment.ProcessId, baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current,
                modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(StaticAuraParser).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }.Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) };
            await File.WriteAllTextAsync(Path.Combine(folder, "canonical-" + trait + ".json"), JsonSerializer.Serialize(new { scope = "Actual canonical SEATING and production registry audit, no QA setup, SQL/API or CI claim", trait, initial = Initial, lifecycle, steps = Steps, finalFrame = Frame(), runtime }, Json));
        }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore { public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame); }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
        internal RecoveredCommand[] Commands() => Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs, TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        internal RecoveredEvent[] Events() => Entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
    }
}
