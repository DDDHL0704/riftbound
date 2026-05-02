using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConformanceFixtureShapeTests
{
    [Fact]
    public async Task DuplicateClientIntentDoesNotAdvanceTickTwice()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        var first = await session.SubmitAsync("P1", "intent-1", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var duplicate = await session.SubmitAsync("P1", "intent-1", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, "PASS", StringComparison.Ordinal))
            .ToArray();

        Assert.True(first.Accepted);
        Assert.True(duplicate.Accepted);
        Assert.Equal(first.State.Tick, duplicate.State.Tick);
        Assert.Equal(first.Events, duplicate.Events);
        Assert.Single(gameplayEntries);
    }

    [Fact]
    public async Task JournalEntriesCarryMonotonicEventSequenceBounds()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-end-turn", new EndTurnCommand(), RawCommand("END_TURN"), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => !string.Equals(entry.CommandType, "READY", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, gameplayEntries.Length);
        Assert.Equal(3, gameplayEntries[0].StartedEventSequence);
        Assert.Equal(4, gameplayEntries[0].CompletedEventSequence);
        Assert.Equal(4, gameplayEntries[1].StartedEventSequence);
        Assert.Equal(9, gameplayEntries[1].CompletedEventSequence);
    }

    [Fact]
    public async Task JournalEntryKeepsOriginalCommandPayload()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);
        var raw = JsonDocument.Parse("""{"cmdType":"PASS","clientNote":"keep-me"}""").RootElement.Clone();

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), raw, CancellationToken.None);

        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.CommandType, "PASS", StringComparison.Ordinal));
        Assert.NotNull(entry.RawCommand);
        Assert.Equal("keep-me", entry.RawCommand.Value.GetProperty("clientNote").GetString());
    }

    [Fact]
    public async Task SubmitRequiresPlayerToJoinRoomFirst()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.PlayerNotInRoom, error.Code);
        Assert.Equal("player is not in room", error.Message);
    }

    [Fact]
    public async Task SubmitRequiresMatchToStart()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.MatchNotStarted, error.Code);
        Assert.Equal("match has not started", error.Message);
    }

    [Fact]
    public async Task SubmitRequiresClientIntentId()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("P1", " ", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.ClientIntentIdRequired, error.Code);
        Assert.Equal("clientIntentId is required", error.Message);
    }

    [Fact]
    public async Task ReadyRequiresClientIntentId()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("P1");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.ReadyAsync("P1", "", RawCommand("READY"), CancellationToken.None));

        Assert.Equal(ErrorCodes.ClientIntentIdRequired, error.Code);
        Assert.Equal("clientIntentId is required", error.Message);
    }

    [Fact]
    public void JoinAssignsStableP1P2SeatsAndSnapshotsExposeSeatStatus()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");
        session.EnsurePlayer("alice");

        var aliceSnapshot = session.SnapshotFor("alice");
        var bobSnapshot = session.SnapshotFor("bob");

        Assert.Equal("alice", aliceSnapshot.ActivePlayerId);
        Assert.Equal("alice", bobSnapshot.ActivePlayerId);
        Assert.Equal("P1", PlayerSeat(aliceSnapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(aliceSnapshot, "bob"));
        Assert.Equal("P1", PlayerSeat(bobSnapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(bobSnapshot, "bob"));
    }

    [Fact]
    public async Task MatchStateCarriesP2AuthorityFields()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");
        await ReadyBothAsync(session, "alice", "bob");

        var snapshot = session.SnapshotFor("alice");
        var timing = snapshot.Timing;

        Assert.Equal("MAIN", timing["phase"]);
        Assert.Equal("NEUTRAL_OPEN", timing["timingState"]);
        Assert.Equal("alice", timing["turnPlayerId"]);
        Assert.Equal("IN_PROGRESS", timing["roomStatus"]);
    }

    [Fact]
    public void JoinRejectsThirdPlayer()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");

        var error = Assert.Throws<MatchSessionException>(() => session.EnsurePlayer("charlie"));
        Assert.Equal(ErrorCodes.RoomFull, error.Code);
        Assert.Equal("room already has two players", error.Message);
    }

    [Fact]
    public void ReconnectTokenIsStableAndRequired()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var join = session.EnsurePlayer("alice");
        var duplicateJoin = session.EnsurePlayer(" alice ");
        var reconnect = session.ReconnectPlayer("alice", join.ReconnectToken);

        Assert.Equal(join, duplicateJoin);
        Assert.Equal(join, reconnect);
        Assert.Throws<MatchSessionException>(() => session.ReconnectPlayer("alice", "bad-token"));
    }

    [Fact]
    public void ProtocolEnvelopeKeepsCurrentContractFields()
    {
        var message = new WsServerMessage(
            MessageType.SNAPSHOT,
            "room",
            "P1",
            7,
            new { tick = 7 });

        Assert.Equal(MessageType.SNAPSHOT, message.Type);
        Assert.Equal("room", message.RoomId);
        Assert.Equal("P1", message.PlayerId);
        Assert.Equal(7, message.ServerTick);
        Assert.Equal(ProtocolDefaults.ProtocolVersion, message.ProtocolVersion);
        Assert.Equal(ProtocolDefaults.SchemaVersion, message.SchemaVersion);
    }

    [Fact]
    public void ClientEnvelopeDefaultsProtocolVersions()
    {
        var cmd = JsonDocument.Parse("""{"cmdType":"READY"}""").RootElement.Clone();

        var message = new WsClientMessage(MessageType.READY, "room", "P1", "intent-ready", Cmd: cmd);

        Assert.Equal(MessageType.READY, message.Type);
        Assert.Equal("room", message.RoomId);
        Assert.Equal("P1", message.PlayerId);
        Assert.Equal("intent-ready", message.ClientIntentId);
        Assert.Equal(ProtocolDefaults.ProtocolVersion, message.ProtocolVersion);
        Assert.Equal(ProtocolDefaults.SchemaVersion, message.SchemaVersion);
    }

    [Theory]
    [InlineData("READY", typeof(ReadyCommand))]
    [InlineData("PASS_PRIORITY", typeof(PassPriorityCommand))]
    [InlineData("PASS_FOCUS", typeof(PassFocusCommand))]
    [InlineData("PASS", typeof(PassCommand))]
    [InlineData("END_TURN", typeof(EndTurnCommand))]
    public void GameCommandMapperKeepsPassAndEndTurnSemanticsDistinct(string cmdType, Type expectedType)
    {
        var command = GameCommandJsonMapper.Map(JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement);

        Assert.IsType(expectedType, command);
        Assert.Equal(cmdType, command.CmdType);
    }

    [Fact]
    public void GameCommandMapperParsesPlayCardPayload()
    {
        var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-PUNISHMENT",
              "cardNo": "UNL-007/219",
              "targetObjectIds": ["P2-UNIT-001"],
              "mode": "BASE_UNIT_DAMAGE_4",
              "optionalCosts": ["ECHO"]
            }
            """).RootElement));

        Assert.Equal("P1-SPELL-PUNISHMENT", command.SourceObjectId);
        Assert.Equal("UNL-007/219", command.CardNo);
        Assert.Equal(new[] { "P2-UNIT-001" }, command.TargetObjectIds);
        Assert.Equal("BASE_UNIT_DAMAGE_4", command.Mode);
        Assert.Equal(new[] { "ECHO" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesActivateAbilityPayload()
    {
        var command = Assert.IsType<ActivateAbilityCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "ACTIVATE_ABILITY",
              "sourceObjectId": "P1-UNIT-VI",
              "abilityId": "PAY_2_RED_DOUBLE_POWER",
              "targetObjectIds": ["P2-SPELLSHIELD-UNIT-001"],
              "optionalCosts": ["SPEND_EXPERIENCE:1"]
            }
            """).RootElement));

        Assert.Equal("P1-UNIT-VI", command.SourceObjectId);
        Assert.Equal("PAY_2_RED_DOUBLE_POWER", command.AbilityId);
        Assert.Equal(new[] { "P2-SPELLSHIELD-UNIT-001" }, command.TargetObjectIds);
        Assert.Equal(new[] { "SPEND_EXPERIENCE:1" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesHideCardPayload()
    {
        var command = Assert.IsType<HideCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "HIDE_CARD",
              "sourceObjectId": "P1-HAND-OGN-TEEMO",
              "cardNo": "OGN·121/298",
              "destination": "STANDBY",
              "optionalCosts": ["STANDBY_A"]
            }
            """).RootElement));

        Assert.Equal("P1-HAND-OGN-TEEMO", command.SourceObjectId);
        Assert.Equal("OGN·121/298", command.CardNo);
        Assert.Equal("STANDBY", command.Destination);
        Assert.Equal(new[] { "STANDBY_A" }, command.OptionalCosts);
    }

    [Fact]
    public void SnapshotsExposeDevUiZonesWithoutLeakingOpponentHand()
    {
        var state = new MatchState(
            "dev-room",
            7,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["alice"] = new(3, 1),
                ["bob"] = new(2, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = new(
                    MainDeck: ["A-DECK-1"],
                    RuneDeck: ["A-RUNE-1"],
                    Hand: ["A-HAND-1"],
                    Base: ["A-BASE-1"],
                    Battlefields: [],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: [],
                    ChampionZone: []),
                ["bob"] = new(
                    MainDeck: ["B-DECK-1"],
                    RuneDeck: ["B-RUNE-1"],
                    Hand: ["B-HAND-1"],
                    Base: [],
                    Battlefields: ["B-FIELD-1"],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: [],
                    ChampionZone: [])
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["A-HAND-1"] = new("A-HAND-1", power: 1, tags: ["CARD_TYPE:UNIT"]),
                ["A-BASE-1"] = new("A-BASE-1", power: 2, tags: ["CARD_TYPE:UNIT"]),
                ["B-HAND-1"] = new("B-HAND-1", power: 3, tags: ["CARD_TYPE:UNIT"]),
                ["B-FIELD-1"] = new("B-FIELD-1", power: 4, tags: ["CARD_TYPE:UNIT"])
            });

        var aliceSnapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var aliceView = PlayerView(aliceSnapshot, "alice");
        var bobView = PlayerView(aliceSnapshot, "bob");
        var aliceZones = ZoneView(aliceView);
        var bobZones = ZoneView(bobView);
        var bobObjects = ObjectView(bobView);
        var lanes = Assert.IsType<Dictionary<string, object?>>(aliceSnapshot.Lanes);

        Assert.Equal("P1", Assert.IsType<string>(aliceView["seat"]));
        Assert.Contains("A-HAND-1", StringList(aliceZones["hand"]));
        Assert.Equal(0, Assert.IsType<int>(aliceZones["handHidden"]));
        Assert.Empty(StringList(bobZones["hand"]));
        Assert.Equal(1, Assert.IsType<int>(bobZones["handHidden"]));
        Assert.DoesNotContain("B-HAND-1", bobObjects.Keys);
        Assert.Contains("B-FIELD-1", bobObjects.Keys);
        Assert.Equal(1, Assert.IsType<int>(lanes["battlefieldCount"]));
    }

    [Fact]
    public async Task SeedScenarioCreatesPlayableDevelopmentState()
    {
        var session = new MatchSession("dev-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-basic-play",
            "basic-play",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "basic-play" }),
            CancellationToken.None);

        var p1View = PlayerView(seed.Snapshots["P1"], "P1");
        var p1Zones = ZoneView(p1View);

        Assert.True(seed.Accepted);
        Assert.Contains(seed.Events, evt => string.Equals(evt.Kind, "DEV_SCENARIO_SEEDED", StringComparison.Ordinal));
        Assert.Contains("P1-UNIT-MIGHTY-FAERIE", StringList(p1Zones["hand"]));
        Assert.Contains("PLAY_CARD", seed.Prompts["P1"].Actions);

        var play = await session.SubmitAsync(
            "P1",
            "play-mighty-faerie",
            new PlayCardCommand("P1-UNIT-MIGHTY-FAERIE", "SFD·125/221", []),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-MIGHTY-FAERIE",
                cardNo = "SFD·125/221",
                targetObjectIds = Array.Empty<string>()
            }),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.Contains(play.Events, evt => string.Equals(evt.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains("PASS_PRIORITY", play.Prompts["P1"].Actions);
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static async Task ReadyBothAsync(MatchSession session)
    {
        await ReadyBothAsync(session, "P1", "P2");
    }

    private static async Task ReadyBothAsync(MatchSession session, string firstPlayerId, string secondPlayerId)
    {
        await session.ReadyAsync(firstPlayerId, $"ready-{firstPlayerId}", RawCommand("READY"), CancellationToken.None);
        await session.ReadyAsync(secondPlayerId, $"ready-{secondPlayerId}", RawCommand("READY"), CancellationToken.None);
    }

    private static string PlayerSeat(SnapshotDto snapshot, string playerId)
    {
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
        return Assert.IsType<string>(player["seat"]);
    }

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static Dictionary<string, object?> ObjectView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["objects"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement.Clone();
    }
}
