using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenEmpowerCanonicalOpeningTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task CanonicalOpeningPaysVen032RuneDiscountAndRetainsPermanentMight(string actor)
    {
        var game = await Open(actor, "lillia-lowcurve", "UNL-R02", ["VEN·032", "VEN·032", "VEN·032"]);
        try
        {
            await game.WaitFor(actor, 8, "VEN·032");
            var runes = game.Runes(actor).Length;
            Assert.InRange(runes, 8, 11);
            await game.Tap(actor);
            var unit = game.Hand(actor, "VEN·032").First();
            var mana = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(unit, "VEN·032", []));
            Assert.Equal(mana - 3, game.State.RunePools[actor].Mana);
            Assert.Equal(3, game.Might(actor, unit));
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            var before = game.State.RunePools[actor].Mana;
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            var captured = Assert.Single(game.State.StackItems).EmpowerActivation!;
            Assert.Equal(runes, captured.ControlledRunes);
            Assert.Equal(12 - runes, captured.PaidMana);
            Assert.Equal(before - (12 - runes), game.State.RunePools[actor].Mana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(3, game.Might(actor, unit));
            await game.ColdCheckpoint();
            var pending = Assert.Single(game.State.StackItems);
            var forgedDiscount = game.State with
            {
                StackItems = [pending with { EmpowerActivation = captured with
                    { ControlledRunes = runes + 1, PaidMana = captured.PaidMana - 1 } }],
                RunePools = new Dictionary<string, RunePool>(game.State.RunePools)
                    { [actor] = game.State.RunePools[actor] with { Mana = game.State.RunePools[actor].Mana + 1 } }
            };
            await game.RejectCoordinatedForgery("invented-rune-discount-and-refunded-mana", forgedDiscount);
            await game.Top();
            Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(3, game.State.CardObjects[unit].Power);
            Assert.Equal(6, game.Might(actor, unit));
            await game.Act(actor, new EndTurnCommand());
            await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.Equal(6, game.Might(actor, unit));
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers);
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven032-" + actor); }
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task CanonicalOpeningUsesVen072TwoThenFourAfterActualEmpower(string actor)
    {
        var game = await Open(actor, "poppy-standby", "UNL-R04",
            ["VEN·074", "VEN·074", "VEN·074", "VEN·072", "VEN·072", "VEN·072"]);
        try
        {
            await game.WaitFor(actor, 7, "VEN·074", "VEN·072", "VEN·072");
            await game.Tap(actor);
            var unit = game.Hand(actor, "VEN·074").First();
            var howls = game.Hand(actor, "VEN·072").Take(2).ToArray();
            await game.Act(actor, new PlayCardCommand(unit, "VEN·074", []));
            Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(2, game.Might(actor, unit));
            var before = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(howls[0], "VEN·072", [unit]));
            Assert.Equal(before - 2, game.State.RunePools[actor].Mana);
            await game.ColdCheckpoint();
            await game.Top();
            Assert.Equal(2, Assert.Single(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers).RequestedPowerDelta);
            Assert.Equal(4, game.Might(actor, unit));
            before = game.State.RunePools[actor].Mana;
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(1, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.Equal(before - 1, game.State.RunePools[actor].Mana);
            var premature = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects)
                { [unit] = game.State.CardObjects[unit] with { IsEmpowered = true } } };
            await game.RejectCoordinatedForgery("premature-empowered-status-before-resolution", premature);
            await game.Top();
            Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(5, game.Might(actor, unit));
            before = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(howls[1], "VEN·072", [unit]));
            Assert.Equal(before - 2, game.State.RunePools[actor].Mana);
            await game.ColdCheckpoint();
            await game.Top();
            Assert.Equal(new[] { 2, 4 }, game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers.Select(m => m.RequestedPowerDelta));
            Assert.Equal(9, game.Might(actor, unit));
            await game.Act(actor, new EndTurnCommand());
            Assert.Empty(game.State.CardObjects[unit].UntilEndOfTurnPowerModifiers);
            Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(2, game.State.CardObjects[unit].Power);
            Assert.Equal(3, game.Might(actor, unit));
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven072-" + actor); }
    }

    internal static async Task<CanonicalGame> Open(string actor, string templateId, string rune, string[] additions)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var original = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == templateId).Decklist;
        var main = original.MainDeck.ToList();
        for (var i = 0; i < additions.Length; i++) main.RemoveAt(main.FindIndex(no => no != original.ChampionCardNo));
        main.InsertRange(0, additions);
        var deck = original with { MainDeck = main, RuneDeck = Enumerable.Repeat(rune, 12).ToArray() };
        Assert.True(OfficialDeckValidator.Validate(deck, catalog).IsValid);
        var initial = MatchReplayInitialStateBuilder.FromSeats("VEN-CANONICAL-" + templateId + "-" + actor,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new CanonicalGame(initial);
        try
        {
            foreach (var player in new[] { "P1", "P2" })
                await game.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand());
            await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([]));
            await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint();
            return game;
        }
        catch { await game.Save("opening-" + templateId + "-" + actor); throw; }
    }

    internal sealed class CanonicalGame : IMatchJournal
    {
        private readonly MatchState initial;
        internal MatchState State { get; private set; }

        internal CanonicalGame(MatchState initial)
        { this.initial = initial; State = initial; }

        private IMatchSession Session { get; set; } = null!;
        private readonly List<MatchJournalEntry> entries = [];
        private readonly HashSet<string> publicObjects = [];
        private readonly List<object> recoveryRejections = [];
        private (string Player, string Intent, GameCommand Command, JsonElement Raw)? last;

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { entries.Add(entry); return ValueTask.CompletedTask; }

        private async Task<ResolutionResult> Send(string player, string intent, GameCommand command, JsonElement raw)
            => command is SubmitDeckCommand deck ? await Session.SubmitDeckAsync(player, intent, deck, raw, default)
                : command is ReadyCommand ? await Session.ReadyAsync(player, intent, raw, default)
                : await Session.SubmitAsync(player, intent, command, raw, default);

        internal async Task Act(string player, GameCommand command)
        {
            Session ??= new MatchSession(initial, new CoreRuleEngine(), this);
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = Session.PromptFor(player).PromptId;
            node["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(node, Json);
            command = GameCommandJsonMapper.Map(raw);
            var intent = "canonical-empower-" + entries.Count;
            last = (player, intent, command, raw);
            var result = await Send(player, intent, command, raw);
            Assert.True(result.Accepted, command.GetType().Name + ": " + result.ErrorMessage);
            State = result.State;
            foreach (var location in State.ObjectLocations.Where(e => e.Value.Zone is "BASE" or "BATTLEFIELD" or "GRAVEYARD" or "BANISHED" or "LEGEND" or "CHAMPION"))
                publicObjects.Add(location.Key);
        }

        internal string[] Runes(string player) => State.PlayerZones[player].Base
            .Where(id => State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        internal IEnumerable<string> Hand(string player, string no) => State.PlayerZones[player].Hand.Where(id => State.CardObjects[id].CardNo == no);
        internal int Might(string player, string id) => JsonSerializer.SerializeToElement(Session.SnapshotFor(player), Json)
            .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
        internal async Task Tap(string actor)
        { foreach (var id in Runes(actor).Where(id => !State.CardObjects[id].IsExhausted)) await Act(actor, new TapRuneCommand(id)); }
        internal async Task Top()
        { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task WaitFor(string actor, int runes, params string[] cards)
        {
            for (var turn = 0; turn < 80; turn++)
            {
                if (State.TurnPlayerId == actor && Runes(actor).Length >= runes
                    && cards.GroupBy(no => no).All(g => Hand(actor, g.Key).Count() >= g.Count())) return;
                await Act(State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.Fail("The cards were not naturally drawn before the bounded turn limit.");
        }

        private MatchRecoveryFrame Frame(MatchState? authoritativeState = null)
        {
            var state = authoritativeState ?? State;
            var commands = entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { RuleInputs = e.RuleInputs, TypedCommand = e.TypedCommand }).ToArray();
            var events = entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
            var sequence = entries.Count == 0 ? 0 : entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }

        internal async Task RejectCoordinatedForgery(string label, MatchState forged)
        {
            var original = Frame(); var attempted = Frame(forged);
            var originalHash = MatchStateHasher.Hash(State); var count = entries.Count;
            Assert.Empty(original.ValidationErrors); Assert.Empty(attempted.ValidationErrors);
            Assert.Equal(MatchStateHasher.HashValue(original.Commands), MatchStateHasher.HashValue(attempted.Commands));
            Assert.Equal(MatchStateHasher.HashValue(original.Events), MatchStateHasher.HashValue(attempted.Events));
            var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(attempted, Json), Json)!;
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, new Store(decoded));
            var failure = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(State.RoomId, default));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code);
            Assert.Contains("match recovery action-log audit failed", failure.Message, StringComparison.Ordinal);
            Assert.Contains("replayed final state hash", failure.Message, StringComparison.Ordinal);
            Assert.Equal(count, entries.Count); Assert.Equal(originalHash, MatchStateHasher.Hash(State));
            recoveryRejections.Add(new { label, structuralErrors = attempted.ValidationErrors, failure.Code, failure.Message,
                originalStateHash = originalHash, forgedStateHash = MatchStateHasher.Hash(forged),
                commandHash = MatchStateHasher.HashValue(original.Commands), eventHash = MatchStateHasher.HashValue(original.Events), frame = decoded });
            await ColdCheckpoint();
        }

        internal async Task RejectCoordinatedForgeryTwiceThenRecover(string label, MatchState forged)
        {
            var original = Frame(); var attempted = Frame(forged);
            Assert.Empty(original.ValidationErrors); Assert.Empty(attempted.ValidationErrors);
            Assert.Equal(MatchStateHasher.HashValue(original.Commands), MatchStateHasher.HashValue(attempted.Commands));
            Assert.Equal(MatchStateHasher.HashValue(original.Events), MatchStateHasher.HashValue(attempted.Events));
            var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(attempted, Json), Json)!;
            var store = new RepairableStore(decoded); var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, store);
            var count = entries.Count; var hash = MatchStateHasher.Hash(State); var failures = new List<string>();
            for (var attempt = 1; attempt <= 2; attempt++) {
                var failure = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(State.RoomId, default));
                Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code);
                Assert.Contains("match recovery action-log audit failed", failure.Message, StringComparison.Ordinal);
                Assert.Contains("replayed final state hash", failure.Message, StringComparison.Ordinal);
                Assert.Equal(attempt, store.Loads); Assert.Equal(count, entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(State));
                failures.Add(failure.Message);
            }
            store.Current = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(original, Json), Json)!;
            Session = await registry.GetOrCreateAsync(State.RoomId, default); Assert.Equal(3, store.Loads);
            foreach (var viewer in State.Seats.Keys) {
                Assert.Equal(MatchStateHasher.HashValue(original.PlayerViews[viewer].Snapshot), MatchStateHasher.HashValue(Session.SnapshotFor(viewer)));
                Assert.Equal(MatchStateHasher.HashValue(original.PlayerViews[viewer].Prompt), MatchStateHasher.HashValue(Session.PromptFor(viewer)));
            }
            var previous = last!.Value; var duplicate = await Send(previous.Player, previous.Intent, previous.Command, previous.Raw);
            Assert.True(duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, entries.Count);
            Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            recoveryRejections.Add(new { label, failures, loadsAfterRepair = store.Loads, structuralErrors = attempted.ValidationErrors,
                originalStateHash = hash, forgedStateHash = MatchStateHasher.Hash(forged),
                commandHash = MatchStateHasher.HashValue(original.Commands), eventHash = MatchStateHasher.HashValue(original.Events),
                rejectedFrame = decoded, repairedFrame = store.Current });
        }

        private sealed class RepairableStore(MatchRecoveryFrame current) : IMatchRecoveryStore
        {
            internal MatchRecoveryFrame Current { get; set; } = current;
            internal int Loads { get; private set; }
            public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            { Loads++; return ValueTask.FromResult<MatchRecoveryFrame?>(Current); }
        }

        internal async Task ColdCheckpoint()
        {
            var frame = Frame(); Assert.Empty(frame.ValidationErrors);
            Assert.DoesNotContain(frame.Commands, command => command.CommandType.Contains("SCENARIO", StringComparison.Ordinal));
            var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, new Store(decoded));
            Session = await registry.GetOrCreateAsync(State.RoomId, default);
            foreach (var viewer in State.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.PlayerViews[viewer].Snapshot, Json), JsonSerializer.SerializeToNode(Session.SnapshotFor(viewer), Json)));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(frame.PlayerViews[viewer].Prompt, Json), JsonSerializer.SerializeToNode(Session.PromptFor(viewer), Json)));
                var visible = JsonSerializer.Serialize(Session.SnapshotFor(viewer), Json) + JsonSerializer.Serialize(Session.PromptFor(viewer), Json);
                foreach (var other in State.Seats.Keys.Where(p => p != viewer)) foreach (var id in Hidden(other).Where(id => !publicObjects.Contains(id)))
                    Assert.DoesNotContain(id, visible, StringComparison.Ordinal);
            }
            var publicReplay = JsonSerializer.Serialize(frame.SpectatorReplayFrame, Json);
            foreach (var id in State.Seats.Keys.SelectMany(Hidden).Where(id => !publicObjects.Contains(id))) Assert.DoesNotContain(id, publicReplay, StringComparison.Ordinal);
            var previous = last!.Value; var count = entries.Count; var hash = MatchStateHasher.Hash(State);
            var duplicate = await Send(previous.Player, previous.Intent, previous.Command, previous.Raw);
            Assert.True(duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, entries.Count);
            Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
        }

        private IEnumerable<string> Hidden(string player) => State.PlayerZones[player].Hand.Concat(State.PlayerZones[player].MainDeck).Concat(State.PlayerZones[player].RuneDeck);
        internal async Task Save(string label)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN_CANONICAL_EVIDENCE") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
                .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                    infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
            await File.WriteAllTextAsync(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
                scope = "Canonical seating, legal submitted decks, natural draws/channeling, real commands and production cold registry; no SQL/network or whole-card certification", initial,
                frame = Frame(), recoveryRejections, runtime, last = last is { } value ? new { value.Player, value.Intent, value.Raw } : null }, Json));
        }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    { public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame); }
}
