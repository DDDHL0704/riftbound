using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldPlayRecoveryGuardTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("UNL-003/219")]
    [InlineData("OGN·067/298")]
    public async Task ProductionRegistryUsesTheUnchangedAcceptedLogWhenAnOriginIsForgedAsBattlefield(string cardNo)
    {
        var (initial, journal, session, plain, actor, source) = await ActualBaseOpening(cardNo);
        Assert.Empty(plain.State.StackItems);

        var independent = await Act(WithLegend(PermanentPlayTargetTests.Position(cardNo), "P1", "UNL-183/219"), "P1",
            new PlayCardCommand("UNIT", cardNo, ["HOST"], Destination: "BASE"));
        var forged = plain.State with {
            StackItems = [Assert.Single(independent.State.StackItems) with {
                StackItemId = Assert.Single(plain.State.CardPlays.Finalized).PlayId,
                ControllerId = actor, SourceObjectId = source,
                Destination = "BATTLEFIELD:" + plain.State.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First() }],
            PriorityPlayerId = actor,
            TimingState = independent.State.TimingState
        };
        // A source can legitimately move after its trigger. A well-formed
        // battlefield-origin claim therefore needs the actual action-log audit.
        Assert.Empty(RecoveryErrors(forged));
        var normalFrame = Frame(initial, journal, plain.State);
        var restored = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(normalFrame))
            .GetOrCreateAsync(initial.RoomId, default);
        Assert.Equal(JsonSerializer.Serialize(session.SnapshotFor(actor)), JsonSerializer.Serialize(restored.SnapshotFor(actor)));
        var bad = Frame(initial, journal, forged);
        var originalLog = JsonSerializer.Serialize(new { bad.Commands, bad.Events });
        var writes = new Journal();
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, new Store(bad))
                .GetOrCreateAsync(initial.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Empty(writes.Entries);
        Assert.Equal(originalLog, JsonSerializer.Serialize(new { bad.Commands, bad.Events }));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_BATTLEFIELD_PLAY_EVIDENCE") is { Length: > 0 } directory)
        {
            var name = cardNo == "UNL-003/219" ? "merfolk" : "blitzcrank";
            var path = Path.Combine(directory, "battlefield-base-" + name + ".json");
            var exported = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            exported["forgedPrimaryItem"] = JsonSerializer.SerializeToNode(Assert.Single(forged.StackItems), WebJson);
            await File.WriteAllTextAsync(path, exported.ToJsonString(new JsonSerializerOptions(WebJson) { WriteIndented = true }));
        }
    }

    private static async Task<(MatchState Initial, Journal Journal, MatchSession Session, ResolutionResult Played, string Actor, string Source)>
        ActualBaseOpening(string cardNo)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "card-play-history-current-opening-20261017.json")));
        var fixture = doc.RootElement;
        var actor = fixture.GetProperty("expectedActor").GetString()!;
        var initial = MatchReplayInitialStateBuilder.FromSeats(fixture.GetProperty("sourceRoomId").GetString()!,
            fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        Assert.Empty(initial.CardObjects);
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var state = initial;
        foreach (var recorded in fixture.GetProperty("acceptedCommands").EnumerateArray().Take(6))
        {
            var player = recorded.GetProperty("playerId").GetString()!;
            var type = recorded.GetProperty("commandType").GetString();
            var node = JsonNode.Parse(recorded.GetProperty("rawCommand").GetRawText())!.AsObject();
            if (type == "SUBMIT_DECK" && player == actor)
            {
                if (cardNo == "OGN·067/298")
                {
                    var catalog = await OfficialCardCatalog.LoadDefaultAsync();
                    var greenDeck = PreconstructedDeckCatalog.Build(catalog, OfficialDeckFormat.ChinaStandard20260724)
                        .Single(deck => deck.Id == "lillia-lowcurve").Decklist;
                    node = JsonSerializer.SerializeToNode(greenDeck, WebJson)!.AsObject();
                    node["cmdType"] = CommandTypes.SubmitDeck;
                }
                var deck = node["mainDeck"]!.AsArray();
                if (cardNo == "UNL-003/219")
                {
                    var previous = deck[32]!.GetValue<string>();
                    var swap = Enumerable.Range(0, deck.Count).First(i => deck[i]!.GetValue<string>() == cardNo);
                    deck[swap] = previous;
                }
                deck[32] = cardNo; // A legal deck submitted before play, not a rewritten accepted command.
            }
            node["promptId"] = ResolutionResult.BuildPrompts(state)[player].PromptId;
            node["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(node);
            var intent = "origin-opening-" + journal.Entries.Count;
            var result = type switch {
                "SUBMIT_DECK" => await session.SubmitDeckAsync(player, intent, raw.Deserialize<SubmitDeckCommand>(WebJson)!, raw, default),
                "READY" => await session.ReadyAsync(player, intent, raw, default),
                "MULLIGAN" => await session.SubmitAsync(player, intent, raw.Deserialize<MulliganCommand>(WebJson)!, raw, default),
                _ => throw new InvalidOperationException("Unexpected opening command: " + type)
            };
            Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State;
        }
        var source = actor + "-MAIN-" + cardNo.Replace('·', '-').Replace('/', '-') + "-32";
        Assert.Contains(source, state.PlayerZones[actor].Hand);
        var cost = cardNo == "UNL-003/219" ? 2 : 5;
        for (var i = 0; (state.TurnPlayerId != actor || state.PlayerZones[actor].Base.Count(id =>
            state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)) < cost) && i < 12; i++)
            state = (await Submit(state.TurnPlayerId, new EndTurnCommand())).State;
        Assert.Equal(actor, state.TurnPlayerId);
        foreach (var id in state.PlayerZones[actor].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !state.CardObjects[id].IsExhausted).Take(cost).ToArray())
            state = (await Submit(actor, new TapRuneCommand(id))).State;
        if (cardNo == "OGN·067/298")
        {
            var green = state.PlayerZones[actor].Base.First(id => state.CardObjects[id].Tags.Contains("COLOR:green"));
            state = (await Submit(actor, new RecycleRuneCommand(green))).State;
        }
        var played = await Submit(actor, new PlayCardCommand(source, cardNo, [], Destination: "BASE"));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_BATTLEFIELD_PLAY_EVIDENCE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            var name = cardNo == "UNL-003/219" ? "merfolk" : "blitzcrank";
            await File.WriteAllTextAsync(Path.Combine(output, "battlefield-base-" + name + ".json"),
                JsonSerializer.Serialize(new {
                    schemaVersion = 1, sourceRoomId = initial.RoomId, openingSeed = 0, seats = initial.Seats,
                    expectedActor = actor, expectedCardNo = cardNo, expectedPlayedObject = source,
                    acceptedCommands = journal.Entries.Select(e => new {
                        playerId = e.PlayerId, clientIntentId = e.ClientIntentId, commandType = e.CommandType,
                        rawCommand = e.RawCommand!.Value }),
                    scope = "New legal deck submitted before play, blank SEATING, original explicit seed0, ordinary actions only. The earlier source fixture and all accepted histories remain unchanged. Runtime input identity must be recorded independently by each actual execution."
                }, new JsonSerializerOptions(WebJson) { WriteIndented = true }));
        }
        return (initial, journal, session, played, actor, source);

        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), WebJson)!.AsObject();
            node["promptId"] = ResolutionResult.BuildPrompts(state)[player].PromptId;
            node["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "origin-action-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(node), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            return result;
        }
    }

    [Theory]
    [InlineData("UNL-003/219")]
    [InlineData("OGN·067/298")]
    public async Task RebuiltViewsCannotAuthorizeAForgedBattlefieldOnlyAbilityAfterBasePlay(string cardNo)
        => await RejectForgedBaseAbility(cardNo, opponentLegend: false);

    [Theory]
    [InlineData("UNL-003/219")]
    [InlineData("OGN·067/298")]
    public async Task AnOpponentsLegendCannotAuthorizeTheOtherPlayersBasePlayAbility(string cardNo)
        => await RejectForgedBaseAbility(cardNo, opponentLegend: true);

    [Theory]
    [InlineData("UNL-003/219", "UNL-183/219")]
    [InlineData("UNL-003/219", "UNL-227/219")]
    [InlineData("UNL-003/219", "UNL-227*/219")]
    [InlineData("OGN·067/298", "UNL-183/219")]
    [InlineData("OGN·067/298", "UNL-227/219")]
    [InlineData("OGN·067/298", "UNL-227*/219")]
    public async Task EachCurrentLegendPrintingKeepsItsIndependentBasePlayAbility(string cardNo, string legendCardNo)
    {
        var initial = WithLegend(PermanentPlayTargetTests.Position(cardNo), "P1", legendCardNo);
        Assert.Empty(RecoveryErrors(initial));
        var played = await Act(initial, "P1", new PlayCardCommand("UNIT", cardNo, ["HOST"], Destination: "BASE"));
        Assert.Single(played.State.StackItems);
        Assert.Empty(RecoveryErrors(played.State));
        var resolved = await Top(played.State);
        Assert.Equal(initial.CardObjects["HOST"].Power + 1, resolved.State.CardObjects["HOST"].Power);
        Assert.Empty(resolved.State.StackItems);
        Assert.Empty(RecoveryErrors(resolved.State));
    }

    private async Task RejectForgedBaseAbility(string cardNo, bool opponentLegend)
    {
        var initial = PermanentPlayTargetTests.Position(cardNo);
        if (opponentLegend) initial = WithLegend(initial, "P2", "UNL-183/219");
        var plain = await Act(initial, "P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        Assert.Empty(plain.State.StackItems);
        Assert.Empty(RecoveryErrors(plain.State));

        var withLegend = WithLegend(initial, "P1", "UNL-183/219");
        Assert.Empty(RecoveryErrors(withLegend));
        var independent = await Act(withLegend, "P1",
            new PlayCardCommand("UNIT", cardNo, ["HOST"], Destination: "BASE"));
        var item = Assert.Single(independent.State.StackItems);
        Assert.True(item.SourceConfirmed);
        Assert.Empty(item.Destination); // The stack stores canonical base placement as an empty destination.
        Assert.Empty(RecoveryErrors(independent.State));

        // The permanent and all public/private views are internally consistent.
        // This state has no legend or captured legend trigger to justify a skill.
        var forged = plain.State with {
            StackItems = [item],
            PriorityPlayerId = independent.State.PriorityPlayerId,
            TimingState = independent.State.TimingState
        };
        Assert.Empty(forged.PlayerZones["P1"].LegendZone);
        Assert.DoesNotContain(forged.UntilEndOfTurnEffects, e => e.StartsWith("RENGAR_UNIT_PLAYED_TARGET:", StringComparison.Ordinal));
        var errors = RecoveryErrors(forged);
        output.WriteLine("BASE_ONLY_TRIGGER_FORGED_RECOVERY " + JsonSerializer.Serialize(new { cardNo, opponentLegend, errors }));
        Assert.NotEmpty(errors);
    }

    private static MatchState WithLegend(MatchState initial, string player, string cardNo)
    {
        var id = "LEGEND-" + player;
        return initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = new(id, cardNo: cardNo, ownerId: player, controllerId: player) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { LegendZone = [id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                [id] = new(player, "LEGEND") }
        };
    }

    private static IReadOnlyList<string> RecoveryErrors(MatchState state)
    {
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, 0, snapshots[player], state.Tick, 0, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState final)
    {
        var sequence = journal.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(final);
        var prompts = ResolutionResult.BuildPrompts(final);
        var views = final.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, final.Tick, sequence, snapshots[player], final.Tick, sequence, prompts[player]));
        return new(initial.RoomId, final.Tick, sequence,
            journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { RuleInputs = e.RuleInputs }).ToArray(),
            journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray(),
            views, [], final, initial,
            MatchReplayRedactor.BuildSpectatorFrame(initial.RoomId, final.Tick, sequence, journal.Entries[^1].Events, final));
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
