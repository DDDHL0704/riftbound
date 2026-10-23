using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NetworkReconnectSqlEvidenceTests
{
    [Fact]
    public void RealJoinedContractAllowsOnlyTwoTokenRotationsAndNoHistoryChanges()
    {
        Rows(Position()); Traces(Trace(false)); Traces(Trace(true));
    }

    [Theory]
    [InlineData("command_log")] [InlineData("game_events")] [InlineData("state_snapshots")] [InlineData("snapshots")] [InlineData("action_prompts")]
    public void ReconnectMustPreserveEveryHistoryRowAndTable(string table)
    {
        Reject(d => d["afterReconnect"]!["tables"]![table]![0] = "{\"changed\":true}");
        Reject(d => d["afterReconnect"]!["tables"]!.AsObject().Remove(table));
    }

    [Theory]
    [InlineData("status")] [InlineData("current_tick")] [InlineData("last_event_sequence")] [InlineData("rule_inputs")]
    [InlineData("seed")] [InlineData("winner_player_id")] [InlineData("created_at")]
    public void ReconnectCannotChangeOtherMatchColumns(string field)
        => Reject(d => ChangeRow(d, "matches", 0, row => row[field] = "FORGED"));

    [Theory]
    [InlineData("match_id")] [InlineData("player_id")] [InlineData("seat")] [InlineData("connection_state")]
    [InlineData("joined_at")] [InlineData("future_column")]
    public void ReconnectCannotChangeOtherPlayerColumnsIncludingUnknownFutureColumns(string field)
        => Reject(d => ChangeRow(d, "match_players", 0, row => row[field] = "FORGED"));

    [Theory]
    [InlineData("issued-hash")] [InlineData("consumed-token")] [InlineData("unchanged-token")] [InlineData("joined-seat")]
    [InlineData("missing-player")] [InlineData("extra-player")] [InlineData("late-timestamp")] [InlineData("early-timestamp")]
    [InlineData("wrong-match-timestamp")] [InlineData("extra-table")]
    public void ExactJoinedTokenHashesIdentitiesAndDatabaseTimeWindowAreRequired(string mode)
        => Reject(d => {
            switch (mode) {
                case "issued-hash": ChangeRow(d, "match_players", 0, row => row["reconnect_token_hash"] = Hash("wrong")); break;
                case "consumed-token": d["consumedTokens"]!["P1"] = "wrong"; break;
                case "unchanged-token": d["joined"]!["P1"]!["reconnectToken"] = "old-a"; break;
                case "joined-seat": d["joined"]!["P1"]!["seat"] = "P2"; break;
                case "missing-player": d["afterReconnect"]!["tables"]!["match_players"]!.AsArray().RemoveAt(0); break;
                case "extra-player": d["afterReconnect"]!["tables"]!["match_players"]!.AsArray().Add("{\"player_id\":\"P3\"}"); break;
                case "late-timestamp": ChangeRow(d, "match_players", 0, row => row["updated_at"] = "2026-10-23T20:02:00+00:00"); break;
                case "early-timestamp": ChangeRow(d, "match_players", 0, row => row["updated_at"] = "2026-10-23T20:00:01+00:00"); break;
                case "wrong-match-timestamp": ChangeRow(d, "matches", 0, row => row["updated_at"] = "2026-10-23T20:01:02.5+00:00"); break;
                case "extra-table": d["afterReconnect"]!["tables"]!["extra"] = new JsonArray(); break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        });

    [Theory]
    [InlineData("missing-joined")] [InlineData("extra-joined")] [InlineData("wrong-type")] [InlineData("wrong-room")]
    [InlineData("wrong-recipient")] [InlineData("wrong-token")] [InlineData("invalid-token-error")]
    [InlineData("missing-intentional-error")] [InlineData("wrong-intentional-error")]
    public void BothOwnReconnectJoinedAndOnlyExplicitIntentionalErrorsAreRequired(string mode)
    {
        var d = Trace(mode is not ("missing-intentional-error" or "wrong-intentional-error"));
        switch (mode) {
            case "missing-joined": d["traces"]!.AsArray().RemoveAt(0); break;
            case "extra-joined": d["traces"]!.AsArray().Add(d["traces"]![0]!.DeepClone()); break;
            case "wrong-type": d["traces"]![0]!["message"]!["type"] = "JOIN"; break;
            case "wrong-room": d["traces"]![0]!["message"]!["roomId"] = "OTHER"; break;
            case "wrong-recipient": d["traces"]![0]!["recipient"] = "P3"; break;
            case "wrong-token": d["traces"]![0]!["message"]!["payload"]!["reconnectToken"] = "wrong"; break;
            case "invalid-token-error": d["traces"]!.AsArray().Add(Event("P1", "Error", "ERROR", new JsonObject { ["code"] = "INVALID_RECONNECT_TOKEN" })); break;
            case "missing-intentional-error": d["traces"]!.AsArray().RemoveAt(3); break;
            case "wrong-intentional-error": d["traces"]![3]!["message"]!["payload"]!["code"] = "INVALID_RECONNECT_TOKEN"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
        Assert.ThrowsAny<InvalidOperationException>(() => Traces(d));
    }

    [Fact]
    public void AuthenticatedViewsAndAcceptedDuplicateCannotReplaceMissingJoined()
    {
        // Reproduces the contract failure in A2 39998674123/artifact12521512384,
        // not a claim that this synthetic trace performed an API restart.
        var d = Trace(true); d["traces"]!.AsArray().Clear();
        foreach (var player in new[] { "P1", "P2" })
            d["traces"]!.AsArray().Add(Event(player, "Error", "ERROR", new JsonObject { ["code"] = "INVALID_RECONNECT_TOKEN" }));
        d["failure"] = null; d["snapshots"] = new JsonObject { ["P1"] = new JsonObject(), ["P2"] = new JsonObject() };
        d["duplicateAccepted"] = true;
        Assert.ThrowsAny<InvalidOperationException>(() => Traces(d));
    }

    [Theory]
    [InlineData("matches")] [InlineData("match_players")] [InlineData("command_log")] [InlineData("game_events")]
    [InlineData("state_snapshots")] [InlineData("snapshots")] [InlineData("action_prompts")]
    public void PostReconnectDuplicateMustPreserveEveryRowEvenPreviouslyAllowedRotationColumns(string table)
    {
        var before = Position()["afterReconnect"]!; var changed = before.DeepClone();
        changed["tables"]![table]!.AsArray().Add("{\"unexpected\":true}");
        Assert.False(NetworkReconnectSqlCapture.SameSql(Sample(before), Sample(changed)));
    }

    [Fact]
    public void ANewSamplingClockDoesNotChangeTheExactSqlRows()
    {
        var sample = Position()["afterReconnect"]!;
        Assert.True(NetworkReconnectSqlCapture.SameSql(Sample(sample), Sample(sample, DateTimeOffset.Parse("2026-10-23T20:05:00+00:00"))));
    }

    private static void Rows(JsonNode d) => NetworkReconnectSqlEvidence.CheckReconnectRows(JsonSerializer.SerializeToElement(d), "ROOM", "P1", "P2");
    private static void Traces(JsonNode d) => NetworkReconnectSqlEvidence.CheckTrace(JsonSerializer.SerializeToElement(d), "ROOM", "P1", "P2");
    private static void Reject(Action<JsonObject> mutate)
    {
        var d = Position(); mutate(d);
        var error = Record.Exception(() => Rows(d));
        Assert.True(error is InvalidOperationException or KeyNotFoundException or ArgumentException, error?.ToString() ?? "A forged SQL delta was accepted.");
    }
    private static NetworkReconnectSqlCapture.SqlSample Sample(JsonNode n, DateTimeOffset? at = null)
        => new(at ?? DateTimeOffset.Parse(n["clock"]!.GetValue<string>()), n["tables"]!.AsObject()
            .ToDictionary(p => p.Key, p => p.Value!.AsArray().Select(r => r!.GetValue<string>()).ToArray()));
    private static string Hash(string token) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static void ChangeRow(JsonObject data, string table, int index, Action<JsonObject> mutate) {
        var rows = data["afterReconnect"]!["tables"]![table]!.AsArray(); var row = JsonNode.Parse(rows[index]!.GetValue<string>())!.AsObject();
        mutate(row); rows[index] = row.ToJsonString();
    }
    private static JsonObject Position() {
        JsonObject Sample(bool after) {
            var tables = new JsonObject();
            var match = new JsonObject { ["match_id"] = "ROOM", ["status"] = "IN_PROGRESS", ["current_tick"] = 12,
                ["last_event_sequence"] = 19, ["rule_inputs"] = "FIXED", ["seed"] = "424242", ["winner_player_id"] = null,
                ["created_at"] = "2026-10-23T20:00:00+00:00", ["updated_at"] = after ? "2026-10-23T20:01:02+00:00" : "2026-10-23T20:00:00+00:00" };
            tables["matches"] = new JsonArray(match.ToJsonString());
            var players = new JsonArray();
            foreach (var player in new[] { "P1", "P2" }) players.Add(new JsonObject { ["match_id"] = "ROOM", ["player_id"] = player,
                ["seat"] = player, ["connection_state"] = "CONNECTED", ["joined_at"] = "2026-10-23T20:00:00+00:00", ["future_column"] = "UNCHANGED",
                ["updated_at"] = after ? (player == "P1" ? "2026-10-23T20:01:01+00:00" : "2026-10-23T20:01:02+00:00") : "2026-10-23T20:00:00+00:00",
                ["reconnect_token_hash"] = Hash((after ? "new-" : "old-") + (player == "P1" ? "a" : "b")) }.ToJsonString());
            tables["match_players"] = players;
            foreach (var table in new[] { "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                tables[table] = new JsonArray("{\"id\":1,\"match_id\":\"ROOM\",\"payload\":{\"original\":true}}");
            return new JsonObject { ["clock"] = after ? "2026-10-23T20:01:03+00:00" : "2026-10-23T20:01:00+00:00", ["tables"] = tables };
        }
        return new JsonObject { ["beforeReconnect"] = Sample(false), ["afterReconnect"] = Sample(true),
            ["consumedTokens"] = new JsonObject { ["P1"] = "old-a", ["P2"] = "old-b" },
            ["joined"] = new JsonObject { ["P1"] = Session("P1"), ["P2"] = Session("P2") } };
    }
    private static JsonObject Session(string player) => new() { ["playerId"] = player, ["seat"] = player, ["reconnectToken"] = "new-" + (player == "P1" ? "a" : "b") };
    private static JsonObject Event(string player, string method, string type, JsonObject payload, string? recipient = null) => new() {
        ["recipient"] = recipient ?? player, ["method"] = method, ["message"] = new JsonObject {
            ["roomId"] = "ROOM", ["playerId"] = player, ["type"] = type, ["payload"] = payload } };
    private static JsonObject Trace(bool recover) {
        var expected = new JsonArray(); var traces = new JsonArray(Event("P1", "Joined", "RECONNECT", Session("P1")), Event("P2", "Joined", "RECONNECT", Session("P2")));
        if (!recover) {
            expected.Add(new JsonObject { ["recipient"] = "P2", ["playerId"] = "P1", ["code"] = "IDENTITY_MISMATCH" });
            expected.Add(new JsonObject { ["recipient"] = "P1", ["playerId"] = "P1", ["code"] = "PROMPT_EXPIRED" });
            traces.Add(Event("P1", "Error", "ERROR", new JsonObject { ["code"] = "IDENTITY_MISMATCH" }, "P2"));
            traces.Add(Event("P1", "Error", "ERROR", new JsonObject { ["code"] = "PROMPT_EXPIRED" }));
        }
        return new JsonObject { ["recover"] = recover, ["expectedErrors"] = expected, ["traces"] = traces,
            ["joined"] = new JsonObject { ["P1"] = Session("P1"), ["P2"] = Session("P2") } };
    }
}
