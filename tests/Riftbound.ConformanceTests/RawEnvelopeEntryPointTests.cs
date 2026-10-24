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

using Run = Riftbound.ConformanceTests.RawEnvelopeTestRun;
using RecoveryProof = Riftbound.ConformanceTests.RawEnvelopeRecoveryProof;
using HubClients = Riftbound.ConformanceTests.RawEnvelopeTestHubClients;
using Context = Riftbound.ConformanceTests.RawEnvelopeTestHubContext;
using Groups = Riftbound.ConformanceTests.RawEnvelopeTestGroups;

namespace Riftbound.ConformanceTests;

public sealed class RawEnvelopeEntryPointTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData("ready", "null-tick")] [InlineData("ready", "string-tick")]
    [InlineData("deck", "null-tick")] [InlineData("deck", "string-tick")]
    [InlineData("seed", "null-tick")] [InlineData("seed", "string-tick")]
    public async Task EachJournalEntryPointRejectsMalformedPresentEnvelopeBeforeStatusStaleOrEffects(string kind, string mutation)
    {
        var run = await Run.Create("entry-invalid-" + kind + "-" + mutation);
        var before = await run.Recovery("before"); AssertRecovery(before);
        var wire = await EntryWire(run, kind);
        wire["snapshotTick"] = mutation == "null-tick" ? null : System.Text.Json.Nodes.JsonValue.Create("0");
        var raw = JsonSerializer.SerializeToElement(wire); var count = run.Entries.Count; var hash = MatchStateHasher.Hash(run.State);
        var first = await Entry(run, kind, "malformed-entry", raw);
        var warm = await Entry(run, kind, "malformed-entry", raw);
        var after = await run.Recovery("after");
        run.Save("entry-observation", new { kind, mutation, raw, first, warm, before, after,
            commandsBefore = count, commandsAfter = run.Entries.Count, stateHashUnchanged = hash == MatchStateHasher.Hash(run.State),
            callerScope = kind == "seed" ? "Direct privileged QA SeedScenarioAsync with supplied raw; not SubmitIntent network exposure." : "Actual native Mapper dispatch to READY / SUBMIT_DECK entry." });
        Assert.Equal(first.Accepted, warm.Accepted); Assert.Equal(first.ErrorCode, warm.ErrorCode); Assert.Empty(warm.Events);
        Assert.False(first.Accepted); Assert.Equal(ErrorCodes.InvalidPayload, first.ErrorCode);
        Assert.Equal(count, run.Entries.Count); Assert.Empty(first.Events); Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        AssertRecovery(after);
    }

    [Theory]
    [InlineData("ready")] [InlineData("deck")] [InlineData("seed")]
    public async Task CanonicalEntryEnvelopesRetainExistingStatusAndFiniteQaBehavior(string kind)
    {
        var run = await Run.Create("entry-canonical-" + kind);
        var before = await run.Recovery("before"); AssertRecovery(before);
        var raw = JsonSerializer.SerializeToElement(await EntryWire(run, kind)); var count = run.Entries.Count;
        var first = await Entry(run, kind, "canonical-entry", raw);
        var warm = await Entry(run, kind, "canonical-entry", raw);
        var after = await run.Recovery("after");
        run.Save("entry-observation", new { kind, raw, first, warm, before, after,
            commandsBefore = count, commandsAfter = run.Entries.Count,
            note = "Ready in progress remains an accepted no-op with no journal. Deck in progress keeps its rejected audit. Finite QA seed remains an accepted real state replacement." });
        Assert.Equal(kind != "deck", first.Accepted); Assert.Equal(first.Accepted, warm.Accepted);
        Assert.Equal(first.ErrorCode, warm.ErrorCode); Assert.Equal(Hash(first.Events), Hash(warm.Events));
        Assert.Equal(count + (kind == "ready" ? 0 : 1), run.Entries.Count);
        if (kind == "seed") Assert.NotEmpty(first.Events); else Assert.Empty(first.Events);
        AssertRecovery(after);
    }

    [Fact]
    public async Task JsonNullWireCannotAcquireAnIntentCacheEntryWhoseFingerprintDisappearsOnRecovery()
    {
        var run = await Run.Create("json-null-cold-fingerprint");
        AssertRecovery(await run.Recovery("before"));
        var (hub, _) = await AuthenticatedHub(run);
        using var document = JsonDocument.Parse("null"); var raw = document.RootElement.Clone();
        var changed = JsonSerializer.SerializeToElement(new { cmdType = "UNKNOWN", probeChangedPayload = true });
        var count = run.Entries.Count;
        var first = await hub.SubmitIntent(run.State.RoomId, "p1", "json-null-intent", raw);
        var countAfterFirst = run.Entries.Count;
        var actualJournalRawKind = run.Entries.Count == count ? "NO_NEW_ROW" : run.Entries[^1].RawCommand?.ValueKind.ToString();
        var warmChanged = await hub.SubmitIntent(run.State.RoomId, "p1", "json-null-intent", changed);
        var after = await run.Recovery("after");
        var (coldHub, _) = await AuthenticatedHub(run);
        var coldChanged = await coldHub.SubmitIntent(run.State.RoomId, "p1", "json-null-intent", changed);
        run.Save("null-fingerprint-observation", new { raw, changed, first, warmChanged, coldChanged, after,
            commandsBefore = count, countAfterFirst, commandsAfter = run.Entries.Count, actualJournalRawKind,
            note = "Actual authenticated Hub before/after native registry; serialized frame is unchanged. No cache/RawCommand field is manufactured." });
        AssertRecovery(after);
        Assert.Equal(ErrorCodes.InvalidPayload, first.ErrorCode); Assert.Equal(count, countAfterFirst);
        Assert.Equal(ErrorCodes.UnsupportedCommand, warmChanged.ErrorCode);
        Assert.Equal(warmChanged.ErrorCode, coldChanged.ErrorCode);
        Assert.Equal(count + 1, run.Entries.Count);
    }

    private static async Task<System.Text.Json.Nodes.JsonObject> EntryWire(Run run, string kind)
    {
        object command;
        if (kind == "seed") command = new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "typed-power-payment-mixed-recycle" };
        else if (kind == "ready") command = new ReadyCommand();
        else
        {
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
            command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        }
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = run.Session.PromptFor("p1").PromptId; raw["snapshotTick"] = run.State.Tick;
        return raw;
    }
    private static ValueTask<ResolutionResult> Entry(Run run, string kind, string intent, JsonElement raw)
        => kind == "seed" ? run.Session.SeedScenarioAsync("p1", intent, "typed-power-payment-mixed-recycle", raw, default)
            : run.Send(intent, raw);
    private static async Task<(GameHub Hub, HubClients Clients)> AuthenticatedHub(Run run)
    {
        var clients = new HubClients();
        var hub = new GameHub(run.Registry, playerIdentity: new PlayerIdentityService(new InMemoryPlayerIdentityStore()))
            { Context = new Context(), Clients = clients, Groups = new Groups() };
        Assert.True((await hub.Authenticate("p1", "raw-envelope-test-key-1234")).Authenticated);
        return (hub, clients);
    }
    private static void AssertRecovery(RecoveryProof proof)
    {
        Assert.Empty(proof.Errors); Assert.Empty(proof.AuditErrors); Assert.True(proof.Replay.IsMatch);
        Assert.Null(proof.RegistryFailure); Assert.Equal(1, proof.StoreLoads);
    }
    private static string Hash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
}
