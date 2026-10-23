using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenNetworkReconnectAuditTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private static Dictionary<string, string> OldTokens => new() { ["actor"] = "old-actor", ["peer"] = "old-peer" };
    private static Dictionary<string, string> NewTokens => new() { ["actor"] = "new-actor", ["peer"] = "new-peer" };

    [Fact]
    public void OnlyTheActualTwoCredentialRotationsAndThreeAuditTimestampsMayChange()
    {
        VenEmpoweredSelfSqlAudit.CheckReconnect("room", Frame(false), Frame(true), OldTokens, NewTokens);
        VenEmpoweredSelfSqlAudit.CheckCurrentPlayers("room", Frame(true), NewTokens);
        Assert.Equal(ReconnectTokenHasher.Hash("new-actor"), VenEmpoweredSelfSqlAudit.TokenHash("new-actor"));
    }

    [Theory]
    [InlineData("matches.status")] [InlineData("matches.seed")] [InlineData("matches.extraTimestamp")]
    [InlineData("matches.beforeWindow")] [InlineData("matches.afterWindow")] [InlineData("matches.extraRow")] [InlineData("matches.extraTouchWithinWindow")]
    [InlineData("players.hash")] [InlineData("players.oldHash")] [InlineData("players.seat")]
    [InlineData("players.connectionState")] [InlineData("players.room")] [InlineData("players.identity")]
    [InlineData("players.extraRow")] [InlineData("players.missingRow")] [InlineData("players.extraTimestamp")]
    [InlineData("players.beforeWindow")] [InlineData("players.afterWindow")] [InlineData("players.extraTouchWithinWindow")]
    public void ReconnectRejectsEveryRowOrTimeChangeOutsideItsExactBoundary(string mutation)
    {
        var after = Frame(true); var matches = after.Matches.Select(r => JsonNode.Parse(r.GetRawText())!.AsObject()).ToList();
        var players = after.Players.Select(r => JsonNode.Parse(r.GetRawText())!.AsObject()).ToList();
        switch (mutation) {
            case "matches.status": matches[0]["status"] = "FINISHED"; break;
            case "matches.seed": matches[0]["seed"] = "999"; break;
            case "matches.extraTimestamp": matches[0]["other_timestamp"] = Start.AddSeconds(2).ToString("O"); break;
            case "matches.beforeWindow": matches[0]["updated_at"] = Start.AddMilliseconds(500).ToString("O"); break;
            case "matches.afterWindow": matches[0]["updated_at"] = Start.AddSeconds(5).ToString("O"); break;
            case "matches.extraTouchWithinWindow": matches[0]["updated_at"] = Start.AddMilliseconds(2500).ToString("O"); break;
            case "matches.extraRow": matches.Add(matches[0].DeepClone().AsObject()); break;
            case "players.hash": players[0]["reconnect_token_hash"] = "sha256:FORGED"; break;
            case "players.oldHash": players[0]["reconnect_token_hash"] = ReconnectTokenHasher.Hash("old-actor"); break;
            case "players.seat": players[0]["seat"] = "P2"; break;
            case "players.connectionState": players[0]["connection_state"] = "DISCONNECTED"; break;
            case "players.room": players[0]["match_id"] = "another-room"; break;
            case "players.identity": players[0]["player_id"] = "intruder"; break;
            case "players.extraRow": players.Add(players[0].DeepClone().AsObject()); break;
            case "players.missingRow": players.RemoveAt(1); break;
            case "players.extraTimestamp": players[0]["other_timestamp"] = Start.AddSeconds(2).ToString("O"); break;
            case "players.beforeWindow": players[0]["updated_at"] = Start.AddMilliseconds(500).ToString("O"); break;
            case "players.afterWindow": players[0]["updated_at"] = Start.AddSeconds(5).ToString("O"); break;
            case "players.extraTouchWithinWindow": players[0]["updated_at"] = Start.AddMilliseconds(2500).ToString("O"); break;
        }
        after = after with { Matches = matches.Select(n => JsonSerializer.SerializeToElement(n, Json)).ToArray(),
            Players = players.Select(n => JsonSerializer.SerializeToElement(n, Json)).ToArray() };
        Assert.Throws<InvalidOperationException>(() => VenEmpoweredSelfSqlAudit.CheckReconnect("room", Frame(false), after, OldTokens, NewTokens));
    }

    [Theory]
    [InlineData("command_log")] [InlineData("game_events")] [InlineData("state_snapshots")]
    [InlineData("snapshots")] [InlineData("action_prompts")]
    public void ReconnectCannotWriteAnyOfTheFiveGameTables(string table)
    {
        var after = Frame(true); var hashes = after.TableDigests.ToDictionary(); hashes[table] = "CHANGED";
        Assert.Throws<InvalidOperationException>(() => VenEmpoweredSelfSqlAudit.CheckReconnect("room", Frame(false), after with { TableDigests = hashes }, OldTokens, NewTokens));
    }

    [Theory]
    [InlineData("matches")] [InlineData("match_players")] [InlineData("command_log")] [InlineData("game_events")]
    [InlineData("state_snapshots")] [InlineData("snapshots")] [InlineData("action_prompts")] [InlineData("wholeDigest")]
    public void DuplicateAndRecoveryHaveNoCredentialOrTimestampException(string table)
    {
        var before = Frame(true); var after = before; VenEmpoweredSelfSqlAudit.CheckExact(before, after);
        if (table == "wholeDigest") after = after with { SevenTableDigest = "CHANGED" };
        else { var hashes = after.TableDigests.ToDictionary(); hashes[table] = "CHANGED"; after = after with { TableDigests = hashes }; }
        Assert.Throws<InvalidOperationException>(() => VenEmpoweredSelfSqlAudit.CheckExact(before, after));
    }

    [Fact]
    public void SnapshotAndDuplicateReceiptsCannotStandInForMissingJoinedAcknowledgements()
    {
        // A2 run 39998674123 / artifact 12521512384 reported pass with zero
        // Joined and two INVALID_RECONNECT_TOKEN errors in each N3 after trace.
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckJoined(["actor", "peer"], new Dictionary<string, PlayerSessionDto>()));
        var errors = new[] { ("actor", Error("actor", ErrorCodes.InvalidReconnectToken)), ("peer", Error("peer", ErrorCodes.InvalidReconnectToken)) };
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors(errors, "room", new Dictionary<(string, string, string), int>()));
    }

    [Theory]
    [InlineData("missingPeer")] [InlineData("substitutedPeer")] [InlineData("wrongIdentity")] [InlineData("blankToken")]
    public void BothExactJoinedIdentitiesAndTheirRealTokensAreRequired(string mutation)
    {
        var joined = new Dictionary<string, PlayerSessionDto> { ["actor"] = new("actor", "P1", "a"), ["peer"] = new("peer", "P2", "p") };
        QaReconnectAssertions.CheckJoined(["actor", "peer"], joined);
        if (mutation == "missingPeer") joined.Remove("peer");
        if (mutation == "substitutedPeer") { joined.Remove("peer"); joined["intruder"] = new("intruder", "P2", "x"); }
        if (mutation == "wrongIdentity") joined["peer"] = new("actor", "P2", "p");
        if (mutation == "blankToken") joined["peer"] = new("peer", "P2", "");
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckJoined(["actor", "peer"], joined));
    }

    [Theory]
    [InlineData("wrongPlayer")] [InlineData("oldToken")] [InlineData("blankToken")] [InlineData("null")]
    public void TheActualJoinedPayloadMustReturnTheSamePlayerWithANewToken(string mutation)
    {
        var valid = JsonSerializer.SerializeToElement(new PlayerSessionDto("actor", "P1", "new"), Json);
        Assert.Equal("new", QaReconnectAssertions.ReadJoined(valid, "actor", "old", Json).ReconnectToken);
        var payload = mutation == "null" ? JsonSerializer.SerializeToElement<object?>(null, Json)
            : JsonSerializer.SerializeToElement(new PlayerSessionDto(mutation == "wrongPlayer" ? "peer" : "actor", "P1",
                mutation == "oldToken" ? "old" : mutation == "blankToken" ? "" : "new"), Json);
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.ReadJoined(payload, "actor", "old", Json));
    }

    [Fact]
    public void ExpectedNegativeErrorsAreLimitedToTheirExactRecipientPlayerCodeAndCount()
    {
        var expected = new Dictionary<(string, string, string), int> { [("actor", "actor", ErrorCodes.PromptExpired)] = 1 };
        QaReconnectAssertions.CheckErrors([("actor", Error("actor", ErrorCodes.PromptExpired))], "room", expected);
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors([("peer", Error("actor", ErrorCodes.PromptExpired))], "room", expected));
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors([("actor", Error("actor", ErrorCodes.InvalidReconnectToken))], "room", expected));
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors([("actor", Error("actor", ErrorCodes.PromptExpired)), ("actor", Error("actor", ErrorCodes.PromptExpired))], "room", expected));
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors([("actor", Error("actor", ErrorCodes.PromptExpired) with { RoomId = "another-room" })], "room", expected));
    }

    private static WsServerMessage Error(string player, string code) => new(MessageType.ERROR, "room", player, 0,
        JsonSerializer.SerializeToElement(new ErrorDto(code, "expected negative fixture"), Json));

    private static VenEmpoweredSelfSqlAuditFrame Frame(bool after)
    {
        var time = Start.AddSeconds(after ? 2 : 0); var tokens = after ? NewTokens : OldTokens;
        var matches = JsonSerializer.SerializeToElement(new { match_id = "room", status = "IN_PROGRESS", seed = "424242", updated_at = time, other_timestamp = Start }, Json);
        var players = new[] { "actor", "peer" }.Select((p, i) => JsonSerializer.SerializeToElement(new {
            match_id = "room", player_id = p, seat = i == 0 ? "P1" : "P2", connection_state = "CONNECTED",
            reconnect_token_hash = ReconnectTokenHasher.Hash(tokens[p]), updated_at = time, other_timestamp = Start
        }, Json)).ToArray();
        return new(Start.AddSeconds(after ? 3 : 0), Start.AddSeconds(after ? 4 : 1), after ? "AFTER" : "BEFORE",
            Tables.ToDictionary(t => t, t => t is "matches" or "match_players" ? t + time.ToString("O") : t + "-UNCHANGED"), [matches], players);
    }
}
