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
    public async Task OfficialOnlyRoomsRejectReadyBeforeDeckSubmission()
    {
        var session = new MatchSession(
            "official-only-room",
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false));
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var result = await session.ReadyAsync(
            "P1",
            "ready-without-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.DoesNotContain("P1", result.State.ReadyPlayerIds);
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
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
    public void SnapshotsDoNotExposeRandomSeedOrCursor()
    {
        var state = new MatchState(
            "privacy-room",
            9,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            seed: 260330,
            rngCursor: 7);

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];

        Assert.DoesNotContain("seed", snapshot.Timing.Keys);
        Assert.DoesNotContain("rngCursor", snapshot.Timing.Keys);
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
        Assert.Equal(string.Empty, command.Destination);
    }

    [Fact]
    public void GameCommandMapperParsesAmbushPlayCardDestination()
    {
        var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-HAND-UNL-GLOOMY-APOTHECARY",
              "cardNo": "UNL-021/219",
              "targetObjectIds": [],
              "mode": "AMBUSH",
              "destination": "BATTLEFIELD:P1-MAIN"
            }
            """).RootElement));

        Assert.Equal("P1-HAND-UNL-GLOOMY-APOTHECARY", command.SourceObjectId);
        Assert.Equal("UNL-021/219", command.CardNo);
        Assert.Empty(command.TargetObjectIds);
        Assert.Equal("AMBUSH", command.Mode);
        Assert.Empty(command.OptionalCosts ?? []);
        Assert.Equal("BATTLEFIELD:P1-MAIN", command.Destination);
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
    public void GameCommandMapperParsesRevealCardPayload()
    {
        var command = Assert.IsType<RevealCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "REVEAL_CARD",
              "sourceObjectId": "P1-FACEDOWN-OGN-TEEMO",
              "cardNo": "OGN·121/298",
              "targetObjectIds": ["P2-BATTLEFIELD-UNIT-001"],
              "mode": "STANDBY_REACTION",
              "optionalCosts": ["STANDBY_REVEAL_0"],
              "destination": "STACK"
            }
            """).RootElement));

        Assert.Equal("P1-FACEDOWN-OGN-TEEMO", command.SourceObjectId);
        Assert.Equal("OGN·121/298", command.CardNo);
        Assert.Equal(new[] { "P2-BATTLEFIELD-UNIT-001" }, command.TargetObjectIds);
        Assert.Equal("STANDBY_REACTION", command.Mode);
        Assert.Equal(new[] { "STANDBY_REVEAL_0" }, command.OptionalCosts);
        Assert.Equal("STACK", command.Destination);
    }

    [Fact]
    public void GameCommandMapperParsesMoveUnitPayload()
    {
        var command = Assert.IsType<MoveUnitCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-SFD-YASUO",
              "origin": "BATTLEFIELD:P1-LEFT",
              "destination": "BATTLEFIELD:P1-RIGHT",
              "optionalCosts": ["ROAM"]
            }
            """).RootElement));

        Assert.Equal("P1-BATTLEFIELD-SFD-YASUO", command.SourceObjectId);
        Assert.Equal("BATTLEFIELD:P1-LEFT", command.Origin);
        Assert.Equal("BATTLEFIELD:P1-RIGHT", command.Destination);
        Assert.Equal(new[] { "ROAM" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesAssembleEquipmentPayload()
    {
        var command = Assert.IsType<AssembleEquipmentCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "ASSEMBLE_EQUIPMENT",
              "sourceObjectId": "P1-EQUIPMENT-LONG-SWORD",
              "targetObjectId": "P1-UNIT-ASSEMBLE-TARGET",
              "optionalCosts": ["ASSEMBLE_RED"]
            }
            """).RootElement));

        Assert.Equal("P1-EQUIPMENT-LONG-SWORD", command.SourceObjectId);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", command.TargetObjectId);
        Assert.Equal(new[] { "ASSEMBLE_RED" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesDeclareBattlePayload()
    {
        var command = Assert.IsType<DeclareBattleCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLEFIELD-GAREN"],
              "defenderObjectIds": ["P2-BATTLEFIELD-MUTANT-KITTEN"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement));

        Assert.Equal("BATTLEFIELD:P1-MAIN", command.BattlefieldId);
        Assert.Equal(new[] { "P1-BATTLEFIELD-GAREN" }, command.AttackerObjectIds);
        Assert.Equal(new[] { "P2-BATTLEFIELD-MUTANT-KITTEN" }, command.DefenderObjectIds);
        Assert.Equal(new[] { "COMBAT_ASSIGNMENT" }, command.OptionalCosts);
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
    public void SnapshotsRedactOpponentFaceDownObjects()
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
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty,
                ["bob"] = PlayerZones.Empty with
                {
                    Base = ["B-FACEDOWN-STANDBY-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["B-FACEDOWN-STANDBY-1"] = new(
                    "B-FACEDOWN-STANDBY-1",
                    isFaceDown: true,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    manaCost: 1)
            });

        var aliceSnapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var aliceBobObjects = ObjectView(PlayerView(aliceSnapshot, "bob"));
        var aliceHiddenObject = Assert.IsType<Dictionary<string, object?>>(aliceBobObjects["B-FACEDOWN-STANDBY-1"]);
        Assert.Equal("B-FACEDOWN-STANDBY-1", Assert.IsType<string>(aliceHiddenObject["objectId"]));
        Assert.True(Assert.IsType<bool>(aliceHiddenObject["isFaceDown"]));
        Assert.DoesNotContain("power", aliceHiddenObject.Keys);
        Assert.DoesNotContain("tags", aliceHiddenObject.Keys);
        Assert.DoesNotContain("manaCost", aliceHiddenObject.Keys);

        var bobSnapshot = ResolutionResult.BuildSnapshots(state)["bob"];
        var bobObjects = ObjectView(PlayerView(bobSnapshot, "bob"));
        var bobOwnObject = Assert.IsType<Dictionary<string, object?>>(bobObjects["B-FACEDOWN-STANDBY-1"]);
        Assert.Equal(2, Assert.IsType<int>(bobOwnObject["power"]));
        Assert.Contains(CardObjectTags.Standby, StringList(bobOwnObject["tags"]));
        Assert.Equal(1, Assert.IsType<int>(bobOwnObject["manaCost"]));
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

    [Fact]
    public async Task SeedScenarioCreatesTwoPlayerTestDecks()
    {
        var session = new MatchSession("dev-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-test-decks",
            "test-decks",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "test-decks" }),
            CancellationToken.None);

        Assert.True(seed.Accepted);

        var p1View = PlayerView(seed.Snapshots["P1"], "P1");
        var p1Zones = ZoneView(p1View);
        Assert.Equal(12, Assert.IsType<int>(p1Zones["mainDeckCount"]));
        Assert.Equal(8, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        Assert.Equal(5, StringList(p1Zones["hand"]).Count);
        Assert.Contains("P1-UNIT-MIGHTY-FAERIE", StringList(p1Zones["hand"]));
        Assert.Contains("P1-LEGEND-POPPY", StringList(p1Zones["legendZone"]));
        Assert.Contains("P1-CHAMPION-001", StringList(p1Zones["championZone"]));

        var p2OwnView = PlayerView(seed.Snapshots["P2"], "P2");
        var p2OwnZones = ZoneView(p2OwnView);
        Assert.Equal(12, Assert.IsType<int>(p2OwnZones["mainDeckCount"]));
        Assert.Equal(8, Assert.IsType<int>(p2OwnZones["runeDeckCount"]));
        Assert.Equal(5, StringList(p2OwnZones["hand"]).Count);
        Assert.Contains("P2-SPELL-HEXTECH-RAY", StringList(p2OwnZones["hand"]));
        Assert.Contains("P2-LEGEND-YASUO", StringList(p2OwnZones["legendZone"]));
        Assert.Contains("P2-CHAMPION-001", StringList(p2OwnZones["championZone"]));

        var p2FromP1Zones = ZoneView(PlayerView(seed.Snapshots["P1"], "P2"));
        Assert.Empty(StringList(p2FromP1Zones["hand"]));
        Assert.Equal(5, Assert.IsType<int>(p2FromP1Zones["handHidden"]));

        var p1Objects = ObjectView(p1View);
        var p1HandObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-MIGHTY-FAERIE"]);
        Assert.Equal("SFD·125/221", Assert.IsType<string>(p1HandObject["cardNo"]));

        var p2Objects = ObjectView(p2OwnView);
        var p2HandObject = Assert.IsType<Dictionary<string, object?>>(p2Objects["P2-SPELL-HEXTECH-RAY"]);
        Assert.Equal("OGN·009/298", Assert.IsType<string>(p2HandObject["cardNo"]));

        var playCandidate = Assert.Single(
            seed.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-UNIT-MIGHTY-FAERIE", StringComparison.Ordinal));
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
