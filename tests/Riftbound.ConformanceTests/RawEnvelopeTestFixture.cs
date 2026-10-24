using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Riftbound.Api.Hubs;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal sealed record RawEnvelopeRecoveryProof(IReadOnlyList<string> Errors, IReadOnlyList<string> AuditErrors, MatchActionLogReplayResult Replay, string? RegistryFailure, int StoreLoads, int CommandCount, int EventCount);

internal sealed class RawEnvelopeTestClient : IGameClient
    {
        public List<WsServerMessage> Errors { get; } = [];
        public List<WsServerMessage> OtherMessages { get; } = [];
        public Task Error(WsServerMessage message) { Errors.Add(message); return Task.CompletedTask; }
        public Task Joined(WsServerMessage message) { OtherMessages.Add(message); return Task.CompletedTask; }
        public Task Snapshot(WsServerMessage message) { OtherMessages.Add(message); return Task.CompletedTask; }
        public Task Prompt(WsServerMessage message) { OtherMessages.Add(message); return Task.CompletedTask; }
        public Task Events(WsServerMessage message) { OtherMessages.Add(message); return Task.CompletedTask; }
        public Task Matchmaking(WsServerMessage message) { OtherMessages.Add(message); return Task.CompletedTask; }
    }
internal sealed class RawEnvelopeTestHubClients : IHubCallerClients<IGameClient>
    {
        public RawEnvelopeTestClient CallerClient { get; } = new(); public RawEnvelopeTestClient GroupClient { get; } = new();
        public IGameClient Caller => CallerClient; public IGameClient All => GroupClient; public IGameClient Others => GroupClient;
        public IGameClient AllExcept(IReadOnlyList<string> ids) => GroupClient;
        public IGameClient Client(string id) => GroupClient;
        public IGameClient Clients(IReadOnlyList<string> ids) => GroupClient;
        public IGameClient Group(string id) => GroupClient;
        public IGameClient GroupExcept(string id, IReadOnlyList<string> ids) => GroupClient;
        public IGameClient Groups(IReadOnlyList<string> ids) => GroupClient;
        public IGameClient OthersInGroup(string id) => GroupClient;
        public IGameClient User(string id) => GroupClient;
        public IGameClient Users(IReadOnlyList<string> ids) => GroupClient;
    }
internal sealed class RawEnvelopeTestGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
internal sealed class RawEnvelopeTestHubContext : HubCallerContext
    {
        public override string ConnectionId => "raw-envelope-hub";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
internal sealed class RawEnvelopeTestRun(string name, string primaryPlayer) : IMatchJournal
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
        public IMatchSession Session { get; private set; } = null!;
        public IMatchSessionRegistry Registry { get; private set; } = null!;
        public List<MatchJournalEntry> Entries { get; } = [];
        public MatchState State => Entries[^1].AuthoritativeState;
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }

        public static async Task<RawEnvelopeTestRun> Create(string name, string firstPlayer = "p1", string secondPlayer = "p2")
        {
            var run = new RawEnvelopeTestRun(name, firstPlayer);
            run.Session = new MatchSession(name, new CoreRuleEngine(), run);
            await run.Session.EnsurePlayerAsync(firstPlayer, default); await run.Session.EnsurePlayerAsync(secondPlayer, default);
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
            var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
            await run.Accept("deck-p1", command, firstPlayer); await run.Accept("deck-p2", command, secondPlayer);
            await run.Accept("ready-p1", new ReadyCommand(), firstPlayer); await run.Accept("ready-p2", new ReadyCommand(), secondPlayer);
            Assert.Equal(MatchStatuses.InProgress, run.State.Status);
            Assert.Equal(6, run.Entries.Count);
            return run;
        }

        private async Task Accept(string intent, GameCommand command, string player = "p1")
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            node["promptId"] = Session.PromptFor(player).PromptId; node["snapshotTick"] = State.Tick;
            var raw = JsonSerializer.SerializeToElement(node);
            var result = await Send(intent, raw, player); Assert.True(result.Accepted, result.ErrorMessage);
        }

        public ValueTask<ResolutionResult> Send(string intent, JsonElement raw, string? player = null)
        {
            player ??= primaryPlayer;
            var command = GameCommandJsonMapper.Map(raw);
            return command switch {
                ReadyCommand => Session.ReadyAsync(player, intent, raw, default),
                SubmitDeckCommand deck => Session.SubmitDeckAsync(player, intent, deck, raw, default),
                _ => Session.SubmitAsync(player, intent, command, raw, default)
            };
        }

        public async Task<RawEnvelopeRecoveryProof> Recovery(string label)
        {
            var last = Entries[^1];
            var commands = Entries.Select(entry => JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(entry, Web), Web)!).ToArray();
            var events = Entries.SelectMany(entry => entry.Events.Select((e, i) => new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, e))).ToArray();
            var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key,
                last.CompletedTick, last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[pair.Key]));
            var initial = MatchState.Create(name) with { Seed = Entries[0].OpeningSeed };
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(name, last.CompletedTick, last.CompletedEventSequence, events.Select(e => e.Event).ToArray(), State);
            var frame = new MatchRecoveryFrame(name, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], State, initial, spectator)
                { OpeningSeed = Entries[0].OpeningSeed };
            var loaded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
            var errors = MatchRecoveryValidator.Validate(name, loaded.LastEventSequence, loaded.Commands, loaded.Events, loaded.PlayerViews,
                loaded.AuthoritativeState, loaded.CurrentTick, loaded.SpectatorReplayFrame);
            loaded = loaded with { ValidationErrors = errors };
            Save(label + "-frame", loaded);
            var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(loaded, new CoreRuleEngine(), default);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, loaded.Commands, State, new CoreRuleEngine(), default, loaded.Events);
            var store = new RawEnvelopeFrameStore(loaded); string? failure = null;
            try { Registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, store); Session = await Registry.GetOrCreateAsync(name, default); }
            catch (MatchSessionException ex) { failure = ex.Code + ": " + ex.Message; }
            var proof = new RawEnvelopeRecoveryProof(errors, audit, replay, failure, store.Loads, commands.Length, events.Length);
            Save(label + "-recovery", proof);
            return proof;
        }

        public void Save(string label, object value)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE") is not { Length: > 0 } root) return;
            var folder = Path.Combine(root, name); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
            var assemblies = new[] { typeof(RawEnvelopeTestRun).Assembly, typeof(CoreRuleEngine).Assembly,
                typeof(GameCommand).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameHub).Assembly }.Select(a => new { name = a.GetName().Name,
                path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                informationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) });
            File.WriteAllText(Path.Combine(folder, "runtime.json"), JsonSerializer.Serialize(new { assemblies, ruleInputs = RuleInputIdentity.Current }, Web));
        }
    }

internal sealed class RawEnvelopeFrameStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public int Loads { get; private set; }
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
        { Assert.Equal(frame.RoomId, roomId); Loads++; return ValueTask.FromResult<MatchRecoveryFrame?>(frame); }
    }
