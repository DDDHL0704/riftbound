using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenNetworkReplyEnvelopeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Dictionary<string, string> Tokens => new() { ["actor"] = "old-actor", ["peer"] = "old-peer" };

    [Fact]
    public async Task TwoExactJoinedEnvelopesAndOnePreregisteredErrorComplete()
    {
        var replies = Ready();
        Assert.Equal("new-actor", (await replies.WaitForJoinedAsync("actor", default)).ReconnectToken);
        Assert.Equal("new-peer", (await replies.WaitForJoinedAsync("peer", default)).ReconnectToken);
        replies.ExpectError("peer", "actor", ErrorCodes.IdentityMismatch);
        replies.Observe("Error", "peer", Error("actor", ErrorCodes.IdentityMismatch));
        var message = await replies.WaitForErrorAsync("peer", "actor", ErrorCodes.IdentityMismatch, default);
        Assert.Equal("actor", message.PlayerId); replies.AssertComplete();
    }

    [Theory]
    [InlineData("room")] [InlineData("envelopePlayer")] [InlineData("type")]
    [InlineData("payloadPlayer")] [InlineData("oldToken")] [InlineData("recipient")]
    public async Task AnInvalidJoinedEnvelopeImmediatelyFailsTheOtherPlayersPendingWait(string mutation)
    {
        var replies = new QaNetworkReplyAudit("room", Tokens, Json);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waiting = replies.WaitForJoinedAsync("peer", deadline.Token); var message = Joined("actor");
        if (mutation == "room") message = message with { RoomId = "another-room" };
        if (mutation == "envelopePlayer") message = message with { PlayerId = "peer" };
        if (mutation == "type") message = message with { Type = MessageType.ERROR };
        if (mutation == "payloadPlayer") message = message with { Payload = JsonSerializer.SerializeToElement(new PlayerSessionDto("peer", "P1", "new-actor"), Json) };
        if (mutation == "oldToken") message = message with { Payload = JsonSerializer.SerializeToElement(new PlayerSessionDto("actor", "P1", "old-actor"), Json) };
        replies.Observe("Joined", mutation == "recipient" ? "intruder" : "actor", message);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await waiting);
        Assert.Throws<InvalidOperationException>(replies.AssertComplete);
    }

    [Fact]
    public async Task DuplicateJoinedCannotOverwriteTheFirstAcknowledgementOrLeaveAnotherWaitPending()
    {
        var replies = new QaNetworkReplyAudit("room", Tokens, Json);
        replies.Observe("Joined", "actor", Joined("actor"));
        Assert.Equal("new-actor", (await replies.WaitForJoinedAsync("actor", default)).ReconnectToken);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waiting = replies.WaitForJoinedAsync("peer", deadline.Token);
        replies.Observe("Joined", "actor", Joined("actor") with {
            Payload = JsonSerializer.SerializeToElement(new PlayerSessionDto("actor", "P1", "second-new-actor"), Json) });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await waiting);
        Assert.Throws<InvalidOperationException>(replies.AssertComplete);
    }

    [Theory]
    [InlineData("room")] [InlineData("envelopePlayer")] [InlineData("recipient")]
    [InlineData("type")] [InlineData("code")] [InlineData("payload")]
    public async Task ADeliberateErrorMustMatchItsExactEnvelopeRecipientPlayerAndCode(string mutation)
    {
        var replies = Ready(); replies.ExpectError("peer", "actor", ErrorCodes.IdentityMismatch);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waiting = replies.WaitForErrorAsync("peer", "actor", ErrorCodes.IdentityMismatch, deadline.Token);
        var message = Error("actor", ErrorCodes.IdentityMismatch);
        if (mutation == "room") message = message with { RoomId = "another-room" };
        if (mutation == "envelopePlayer") message = message with { PlayerId = "peer" };
        if (mutation == "type") message = message with { Type = MessageType.RECONNECT };
        if (mutation == "code") message = Error("actor", ErrorCodes.InvalidReconnectToken);
        if (mutation == "payload") message = message with { Payload = null };
        replies.Observe("Error", mutation == "recipient" ? "actor" : "peer", message);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await waiting);
        Assert.Throws<InvalidOperationException>(replies.AssertComplete);
    }

    [Fact]
    public void ErrorCannotBeRegisteredAfterArrivalAndDuplicateRegistrationFails()
    {
        var replies = Ready(); replies.Observe("Error", "peer", Error("actor", ErrorCodes.IdentityMismatch));
        Assert.Throws<InvalidOperationException>(() => replies.ExpectError("peer", "actor", ErrorCodes.IdentityMismatch));
        replies = Ready(); replies.ExpectError("peer", "actor", ErrorCodes.IdentityMismatch);
        Assert.Throws<InvalidOperationException>(() => replies.ExpectError("peer", "actor", ErrorCodes.IdentityMismatch));
    }

    [Fact]
    public async Task MissingDeliberateErrorCannotCompleteEvenWithBothJoinedAndValidViews()
    {
        var replies = Ready(); replies.ExpectError("actor", "actor", ErrorCodes.PromptExpired);
        Assert.Throws<InvalidOperationException>(replies.AssertComplete);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await replies.WaitForErrorAsync("actor", "actor", ErrorCodes.PromptExpired, deadline.Token));
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors([], "room",
            new Dictionary<(string, string, string), int> { [("actor", "actor", ErrorCodes.PromptExpired)] = 1 }));
    }

    [Fact]
    public async Task SecondErrorFailsAfterTheFirstWasAlreadyConsumedAndWakesAnOutstandingRpc()
    {
        var replies = Ready(); replies.ExpectError("actor", "actor", ErrorCodes.PromptExpired);
        replies.Observe("Error", "actor", Error("actor", ErrorCodes.PromptExpired));
        await replies.WaitForErrorAsync("actor", "actor", ErrorCodes.PromptExpired, default);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waiting = replies.UntilFaultAsync(new TaskCompletionSource<bool>().Task, deadline.Token);
        replies.Observe("Error", "actor", Error("actor", ErrorCodes.PromptExpired));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await waiting);
        Assert.Throws<InvalidOperationException>(replies.AssertComplete);
    }

    [Fact]
    public void StaticErrorAuditAlsoRejectsWrongMessageType()
    {
        Assert.Throws<InvalidOperationException>(() => QaReconnectAssertions.CheckErrors(
            [("actor", Error("actor", ErrorCodes.PromptExpired) with { Type = MessageType.RECONNECT })], "room",
            new Dictionary<(string, string, string), int> { [("actor", "actor", ErrorCodes.PromptExpired)] = 1 }));
    }

    [Theory]
    [InlineData("nestedRuleInputs")] [InlineData("ordinaryProtectedNumber")]
    public void ReconnectMustNotNormalizeProtectedDatabaseNumbers(string mutation)
    {
        var before = SqlFrame(false); var after = SqlFrame(true);
        var oldNode = JsonNode.Parse(after.Matches[0].GetRawText())!.AsObject(); var changed = oldNode.DeepClone().AsObject();
        if (mutation == "nestedRuleInputs") changed["rule_inputs"]!["schemaVersion"] = JsonNode.Parse("1.0");
        else changed["current_tick"] = JsonNode.Parse("1.0");
        Assert.True(JsonNode.DeepEquals(oldNode, changed)); // The old semantic comparator accepts this raw representation change.
        Assert.NotEqual(oldNode.ToJsonString(), changed.ToJsonString());
        after = after with { Matches = [JsonSerializer.SerializeToElement(changed, Json)] };
        Assert.Throws<InvalidOperationException>(() => VenEmpoweredSelfSqlAudit.CheckReconnect("room", before, after,
            Tokens, new Dictionary<string, string> { ["actor"] = "new-actor", ["peer"] = "new-peer" }));
    }

    private static QaNetworkReplyAudit Ready()
    {
        var replies = new QaNetworkReplyAudit("room", Tokens, Json);
        replies.Observe("Joined", "actor", Joined("actor")); replies.Observe("Joined", "peer", Joined("peer"));
        replies.AssertComplete(); return replies;
    }
    private static WsServerMessage Joined(string player) => new(MessageType.RECONNECT, "room", player, 3,
        JsonSerializer.SerializeToElement(new PlayerSessionDto(player, player == "actor" ? "P1" : "P2", "new-" + player), Json));
    private static WsServerMessage Error(string player, string code) => new(MessageType.ERROR, "room", player, 3,
        JsonSerializer.SerializeToElement(new ErrorDto(code, "deliberate negative"), Json));

    private static VenEmpoweredSelfSqlAuditFrame SqlFrame(bool after)
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); var time = start.AddSeconds(after ? 2 : 0);
        var matches = JsonSerializer.SerializeToElement(new { match_id = "room", updated_at = time, rule_inputs = new { schemaVersion = 1 }, current_tick = 1 }, Json);
        var players = new[] { "actor", "peer" }.Select((p, i) => JsonSerializer.SerializeToElement(new {
            match_id = "room", player_id = p, seat = i == 0 ? "P1" : "P2", updated_at = time,
            reconnect_token_hash = VenEmpoweredSelfSqlAudit.TokenHash((after ? "new-" : "old-") + p)
        }, Json)).ToArray();
        var tables = new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" };
        return new(start.AddSeconds(after ? 3 : 0), start.AddSeconds(after ? 4 : 1), after ? "AFTER" : "BEFORE",
            tables.ToDictionary(t => t, t => t is "matches" or "match_players" ? t + time : t), [matches], players);
    }
}
