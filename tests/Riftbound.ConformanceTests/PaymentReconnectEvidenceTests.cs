using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

// Independent strict expectations for the test gate. These run without opening
// PostgreSQL or a hub, and cannot be mistaken for an actual network acceptance.
public sealed class PaymentReconnectEvidenceTests
{
    [Fact]
    public void OnlyTheMeasuredTwoTokenRotationsAreAllowedAcrossReconnect()
        => PaymentReconnectEvidence.ValidateRotation(Proof());

    [Theory]
    [InlineData("command_log")]
    [InlineData("game_events")]
    [InlineData("state_snapshots")]
    [InlineData("snapshots")]
    [InlineData("action_prompts")]
    public void EveryGameplayTableRemainsExact(string table)
    {
        var proof = Proof(); proof.After.Tables[table][0]!["payload"]!["immutable"] = 2;
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("ruleset_version")]
    [InlineData("seed")]
    public void MatchesCannotUseItsTimestampExceptionToChangeOtherColumns(string column)
    {
        var proof = Proof(); proof.After.Tables["matches"][0]![column] = "FORGED";
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("seat")]
    [InlineData("connection_state")]
    [InlineData("player_id")]
    public void PlayerRowsCannotUseTheirTokenExceptionToChangeIdentityOrState(string column)
    {
        var proof = Proof(); proof.After.Tables["match_players"][0]![column] = "FORGED";
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("old-hash")]
    [InlineData("returned-hash")]
    [InlineData("unchanged-hash")]
    [InlineData("outside-time")]
    [InlineData("missing-player")]
    [InlineData("extra-player")]
    [InlineData("other-room")]
    [InlineData("other-player")]
    public void BothReturnedSessionsAndTheExactAllowedSqlFootprintMustBeProven(string attack)
    {
        var proof = Proof(); var row = proof.After.Tables["match_players"][0]!;
        switch (attack)
        {
            case "old-hash": proof.Before.Tables["match_players"][0]!["reconnect_token_hash"] = "FORGED"; break;
            case "returned-hash": row["reconnect_token_hash"] = "FORGED"; break;
            case "unchanged-hash": row["reconnect_token_hash"] = "old-P1"; break;
            case "outside-time": row["updated_at"] = "2000-01-01T00:00:00Z"; break;
            case "missing-player": proof.After.Tables["match_players"].RemoveAt(1); break;
            case "extra-player": proof.After.Tables["match_players"].Add(JsonNode.Parse("{\"player_id\":\"P3\"}")); break;
            case "other-room": proof.After.OtherMatches[0]!["status"] = "FORGED"; break;
            case "other-player": proof.After.OtherPlayers[0]!["seat"] = "FORGED"; break;
        }
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("matches")]
    [InlineData("match_players")]
    [InlineData("command_log")]
    [InlineData("game_events")]
    [InlineData("state_snapshots")]
    [InlineData("snapshots")]
    [InlineData("action_prompts")]
    public void DuplicateAndPureReadHaveNoAuthenticationException(string table)
    {
        var first = Rows(false); var second = Rows(false);
        PaymentReconnectEvidence.ValidateExact(first, second);
        second.Tables[table][0]!["extra"] = 1;
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateExact(first, second));
    }

    [Fact]
    public void PersistedEvidenceRoundTripRetainsExactUnicodeAndQuotedSqlValues()
    {
        var original = Rows(false);
        original.Tables["snapshots"][0]!["payload"]!["text"] = "符能 <private> \"quoted\"";
        var restored = JsonSerializer.Deserialize<PaymentReconnectEvidence.Rows>(JsonSerializer.Serialize(original, Web), Web)!;
        PaymentReconnectEvidence.ValidateExact(original, restored);
    }

    [Fact]
    public void NumericReformattingDoesNotBecomeAnExactRowMatch()
    {
        var first = Rows(false); var second = Rows(false);
        second.Tables["game_events"][0]!["payload"]!["immutable"] = JsonNode.Parse("1.0");
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateExact(first, second));
    }

    [Fact]
    public async Task AFailedReconnectFollowedByValidAuthenticatedViewsStillFails()
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-reconnect-guard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using var audit = new PaymentReconnectEvidence(source, "R", folder, "recover", new Dictionary<string, string> { ["P1"] = "old", ["P2"] = "old2" });
            audit.Observe("P1", "Error", new(MessageType.ERROR, "R", "P1", 0,
                JsonSerializer.SerializeToElement(new ErrorDto(ErrorCodes.InvalidReconnectToken, "actual rejected reconnect"), Web)));
            audit.Observe("P1", "Snapshot", new(MessageType.SNAPSHOT, "R", "P1", 7, JsonSerializer.SerializeToElement(new { tick = 7 })));
            await Assert.ThrowsAsync<InvalidOperationException>(() => audit.RequireJoined("P1", default));
            Assert.Throws<InvalidOperationException>(audit.Healthy);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("same-token")]
    [InlineData("foreign-player")]
    [InlineData("wrong-room")]
    [InlineData("duplicate")]
    [InlineData("number-token")]
    public async Task JoinedMustBeUniqueAndBindTheRecipientAndANewSession(string attack)
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-joined-guard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using var audit = new PaymentReconnectEvidence(source, "R", folder, "recover", new Dictionary<string, string> { ["P1"] = "old", ["P2"] = "old2" });
            var message = new WsServerMessage(MessageType.RECONNECT, attack == "wrong-room" ? "OTHER" : "R", "P1", 0,
                JsonSerializer.SerializeToElement(new PlayerSessionDto(attack == "foreign-player" ? "P2" : "P1", "P1", attack == "same-token" ? "old" : "new"), Web));
            if (attack == "number-token") message = message with { Payload = JsonSerializer.SerializeToElement(new { playerId = "P1", seat = "P1", reconnectToken = 7 }, Web) };
            audit.Observe("P1", "Joined", message); if (attack == "duplicate") audit.Observe("P1", "Joined", message);
            Assert.Throws<InvalidOperationException>(audit.Healthy);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static PaymentReconnectEvidence.Rotation Proof() => new(Rows(false), Rows(true),
        new Dictionary<string, string> { ["P1"] = "old-P1", ["P2"] = "old-P2" },
        new Dictionary<string, string> { ["P1"] = "new-P1", ["P2"] = "new-P2" });
    private static PaymentReconnectEvidence.Rows Rows(bool rotated)
    {
        var now = DateTimeOffset.Parse("2026-10-24T01:00:00Z"); var stamp = (rotated ? now.AddSeconds(1) : now.AddSeconds(-1)).ToString("O");
        var tables = new Dictionary<string, JsonArray> {
            ["matches"] = new(JsonSerializer.SerializeToNode(new { match_id = "R", status = "PLAYING", ruleset_version = "fixed", seed = "7", updated_at = stamp })),
            ["match_players"] = new(new[] { "P1", "P2" }.Select(player => JsonSerializer.SerializeToNode(new { match_id = "R", player_id = player,
                seat = player, connection_state = "CONNECTED", reconnect_token_hash = (rotated ? "new-" : "old-") + player, updated_at = stamp })).ToArray()) };
        foreach (var table in new[] { "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            tables[table] = new(JsonNode.Parse("{\"id\":1,\"match_id\":\"R\",\"payload\":{\"immutable\":1}}"));
        return new(now, now.AddSeconds(2), tables, new(JsonNode.Parse("{\"match_id\":\"OTHER\",\"status\":\"PLAYING\"}")),
            new(JsonNode.Parse("{\"match_id\":\"OTHER\",\"player_id\":\"P3\",\"seat\":\"P1\"}")));
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
