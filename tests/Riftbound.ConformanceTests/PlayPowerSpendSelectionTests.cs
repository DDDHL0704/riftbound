using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class PlayPowerSpendSelectionTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("generic", false)] [InlineData("generic", true)]
    [InlineData("typed", false)] [InlineData("typed", true)]
    [InlineData("mixed", false)] [InlineData("mixed", true)]
    public async Task APlayerCanChooseTheOtherFundedSourceVectorWithoutChangingTheFee(string kind, bool selected)
    {
        var run = await KnownCase(kind, "known-" + kind + "-" + selected);
        var actor = kind == "generic" ? "P2" : "P1";
        var command = kind == "typed" ? new PlayCardCommand("CARD", "SFD·143/221", [])
            : new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, [kind == "generic" ? "HOST" : "VICTIM"]);
        if (selected) command = command with { OptionalCosts = [kind == "typed" ? "POWER_SPEND:A:1"
            : kind == "generic" ? "POWER_SPEND:red:1" : "POWER_SPEND:blue:1,red:1"] };
        var quote = await run.Quote(actor, command);
        var result = await run.Submit(actor, command);
        run.Save("known-observation", new { kind, selected, quote, result });
        await run.Cold("known-result");
        Assert.True(quote.IsValid && quote.CanPay, quote.Message);
        Assert.True(result.Accepted, result.ErrorMessage);
        var expected = kind == "typed" ? new RunePool(0, selected ? 0 : 1, Colors(("purple", selected ? 1 : 0)))
            : kind == "generic" ? new RunePool(0, 0, Colors((selected ? "blue" : "red", 1)))
            : new RunePool(0, selected ? 1 : 0, Colors(("blue", selected ? 0 : 1)));
        Assert.Equal(expected, run.State.RunePools[actor]);
        Assert.Equal(expected.Power, quote.Cost!.RemainingRainbowPower);
        Assert.Equal(expected.PowerByTrait.OrderBy(p => p.Key), quote.Cost.RemainingPowerByTrait!.OrderBy(p => p.Key));
        Assert.Equal(kind == "typed" ? 0 : 1, quote.Cost.GenericPower);
        Assert.Equal(kind == "generic" ? Colors() : Colors((kind == "typed" ? "purple" : "red", 1)), quote.Cost.PowerByTrait);
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        var payload = JsonSerializer.SerializeToElement(paid.Payload, Web);
        Assert.Equal(kind == "typed" ? 0 : 1, payload.GetProperty("genericPower").GetInt32());
        Assert.Equal(kind == "mixed" ? 2 : 1, payload.GetProperty("totalPowerCost").GetInt32());
        Assert.Equal(quote.Cost.PowerByTrait.OrderBy(p => p.Key), payload.GetProperty("powerByTrait").Deserialize<Dictionary<string, int>>(Web)!.OrderBy(p => p.Key));
        await run.Cold("known-final");
    }

    private static Dictionary<string, int> Colors(params (string Trait, int Amount)[] entries)
        => entries.Where(p => p.Amount > 0).ToDictionary(p => p.Trait, p => p.Amount, StringComparer.Ordinal);

    private static async Task<Run> KnownCase(string kind, string room)
    {
        var initial = kind == "typed" ? PlayCostPreviewTests.Position(new(4, 1, Colors(("purple", 1))), "SFD·143/221")
            : DancingGrenadeTests.Position();
        initial = initial with { RoomId = room, Tick = 0 };
        if (kind == "generic") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P2"] = new(0, 0, Colors(("blue", 1), ("red", 1))) } };
        if (kind == "mixed") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(2, 1, Colors(("blue", 1), ("red", 1))) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                // Explicit midgame granted-keyword fixture, in canonical tag order.
                ["VICTIM"] = initial.CardObjects["VICTIM"] with { Tags = initial.CardObjects["VICTIM"].Tags
                    .Append(CardObjectTags.Spellshield).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() } } };
        var run = new Run(initial);
        if (kind == "generic")
        {
            await run.Act("P1", new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["VICTIM"]));
            await run.Top();
            Assert.Equal("P2", run.State.PendingEffectPlay!.PlayerId);
        }
        return run;
    }

    private sealed class Run : IMatchJournal
    {
        private IMatchSession session;
        private readonly RecordedPlayers players = new();
        private readonly MatchSessionOptions options;
        private readonly bool ordinary;
        private int intents;
        private int artifacts;
        public MatchState Initial { get; }
        public MatchState State { get; private set; }
        public List<MatchJournalEntry> Entries { get; } = [];
        public Run(MatchState initial, bool ordinary = false)
        {
            Initial = initial; State = initial; this.ordinary = ordinary;
            options = ordinary ? new(AllowLegacyReadyWithoutDeck: false, DeckFormat: OfficialDeckFormat.ChinaStandard20260724) : new();
            session = new MatchSession(initial, new CoreRuleEngine(), this, players, options);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
        public JsonElement Wire(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            return JsonSerializer.SerializeToElement(raw);
        }
        public Task<ResolutionResult> Submit(string actor, GameCommand command)
            => Repeat(actor, Initial.RoomId + "-" + intents++, Wire(actor, command));
        public async Task<ResolutionResult> Repeat(string actor, string intent, JsonElement raw)
        {
            var command = GameCommandJsonMapper.Map(raw);
            var result = await Dispatch(actor, intent, command, raw);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (result.Accepted) State = result.State;
            Save("command-" + Entries.Count, new { actor, intent, raw, result });
            return result;
        }
        private ValueTask<ResolutionResult> Dispatch(string actor, string intent, GameCommand command, JsonElement raw)
            => command switch {
                SubmitDeckCommand deck => session.SubmitDeckAsync(actor, intent, deck, raw, default),
                ReadyCommand => session.ReadyAsync(actor, intent, raw, default),
                _ => session.SubmitAsync(actor, intent, command, raw, default) };
        public async Task Act(string actor, GameCommand command)
        { var result = await Submit(actor, command); Assert.True(result.Accepted, result.ErrorMessage); }
        public async Task Top()
        {
            var id = State.StackItems[^1].StackItemId;
            for (var n = 0; State.StackItems.Any(item => item.StackItemId == id); n++)
            { Assert.InRange(n, 0, 20); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        }
        public async Task<PlayCostQuoteDto> Quote(string actor, PlayCardCommand command)
        {
            var hash = MatchStateHasher.Hash(State); var count = Entries.Count;
            var quote = await session.PreviewPlayCardAsync(actor, new("quote-" + artifacts, session.PromptFor(actor).PromptId!, State.Tick, command), default);
            Assert.Equal(hash, MatchStateHasher.Hash(State)); Assert.Equal(count, Entries.Count);
            Save("quote", quote); return quote;
        }
        public MatchRecoveryFrame Frame(MatchState? authority = null)
        {
            var state = authority ?? State; var last = Entries[^1];
            var commands = Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, last.CompletedEventSequence,
                snapshots[p], state.Tick, last.CompletedEventSequence, prompts[p]));
            return new(state.RoomId, state.Tick, last.CompletedEventSequence, commands, events, views, [], state, Initial,
                MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, last.CompletedEventSequence, events.Select(e => e.Event).ToArray(), state))
                { OpeningSeed = Entries[0].OpeningSeed };
        }
        public async Task Cold(string label)
        {
            var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(Frame(), Web), Web)!;
            var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
                frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
            frame = frame with { ValidationErrors = errors }; Save(label + "-frame", frame); Assert.Empty(errors);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(Initial, frame.Commands, State, new CoreRuleEngine(), default, frame.Events);
            Save(label + "-replay", replay); Assert.True(replay.IsMatch, string.Join(';', replay.Errors));
            if (ordinary)
            {
                var birth = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
                Save(label + "-birth", new { errors = birth }); Assert.Empty(birth);
            }
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this, players, options);
            foreach (var actor in State.Seats.Keys)
            {
                Assert.Equal(Hash(ResolutionResult.BuildSnapshots(State)[actor]), Hash(session.SnapshotFor(actor)));
                Assert.Equal(Hash(ResolutionResult.BuildPrompts(State)[actor]), Hash(session.PromptFor(actor)));
            }
            var last = Entries[^1]; var count = Entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await Dispatch(last.PlayerId, last.ClientIntentId, GameCommandJsonMapper.Map(last.RawCommand!.Value), last.RawCommand.Value);
            Save(label + "-duplicate", new { originalAccepted = last.Accepted, duplicate });
            Assert.Equal(last.Accepted, duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, Entries.Count);
            Assert.Equal(last.ErrorCode, duplicate.ErrorCode);
            Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
        }
        public async Task Registry(string label, MatchRecoveryFrame? forged = null)
        {
            var frame = forged ?? JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(Frame(), Web), Web)!;
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, new FrameStore(frame), players, options);
            if (forged is not null)
            {
                var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(State.RoomId, default));
                Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code); Save(label, new { frame, actualRegistryCode = error.Code });
                return;
            }
            var recovered = await registry.GetOrCreateAsync(State.RoomId, default);
            foreach (var actor in State.Seats.Keys)
            {
                Assert.Equal(Hash(session.SnapshotFor(actor)), Hash(recovered.SnapshotFor(actor)));
                Assert.Equal(Hash(session.PromptFor(actor)), Hash(recovered.PromptFor(actor)));
            }
            session = recovered; Save(label, new { frame, registryRecovered = true, playerStoreWrites = players.Writes });
        }
        public void Save(string label, object value)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_PLAY_POWER_SPEND_EVIDENCE") is not { Length: > 0 } root) return;
            var folder = Path.Combine(root, Initial.RoomId); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, artifacts++ + "-" + label + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
            var loaded = new[] { typeof(PlayPowerSpendSelectionTests).Assembly, typeof(CoreRuleEngine).Assembly, typeof(GameCommand).Assembly,
                typeof(OfficialCardCatalog).Assembly }.Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                    informationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) });
            File.WriteAllText(Path.Combine(folder, "runtime.json"), JsonSerializer.Serialize(new { loaded, ruleInputs = RuleInputIdentity.Current }, Web));
        }
    }

    private static string Hash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
}
