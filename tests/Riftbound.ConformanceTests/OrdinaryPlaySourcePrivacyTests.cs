using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OrdinaryPlaySourcePrivacyTests
{
    private const string Instructor = "OGN·087/298";
    private const string Effect = "YORDLE_INSTRUCTOR_PLAY_UNIT_DRAW_1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public OrdinaryPlaySourcePrivacyTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        var assembly = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={assembly.Location}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant()}; MVID={assembly.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ReturningAnExistingPrintedSourceKeepsThePublicSkillAndHidesItsPrivateIncarnation(string player)
    {
        var initial = Opening(player); var run = new Driver(initial); var complete = false;
        var mutations = new List<object>();
        try
        {
            await run.Act(player, new PlayCardCommand("SOURCE", Instructor, [], Destination: "BASE"));
            foreach (var viewer in new[] { "P1", "P2" }) AssertFace(ResolutionResult.BuildSnapshots(run.State)[viewer], "SOURCE", player);
            var generation = run.State.CardObjects["SOURCE"].ObjectGeneration;
            await run.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
            await run.Top();
            Assert.Contains("SOURCE", run.State.PlayerZones[player].Hand);
            Assert.True(run.State.CardObjects["SOURCE"].ObjectGeneration > generation);
            var skill = Assert.Single(run.State.StackItems);
            Assert.True(skill.SourceConfirmed); Assert.Null(skill.PermanentPlaySource);
            var hash = MatchStateHasher.Hash(run.State); var snapshots = ResolutionResult.BuildSnapshots(run.State);
            foreach (var viewer in new[] { "P1", "P2" }) AssertFace(snapshots[viewer], viewer == player ? "SOURCE" : "HIDDEN", player);
            AssertFace(ResolutionResult.BuildSpectatorSnapshot(run.State), "HIDDEN", player);
            Assert.Equal(hash, MatchStateHasher.Hash(run.State));
            var frame = Frame(initial, run);
            Assert.Empty(Errors(frame));
            var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
            Assert.Equal(hash, MatchStateHasher.Hash(decoded.AuthoritativeState!));
            var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
            foreach (var viewer in new[] { "P1", "P2" })
                Assert.Equal(MatchStateHasher.HashValue(snapshots[viewer]), MatchStateHasher.HashValue(restored.SnapshotFor(viewer)));

            var opponent = player == "P1" ? "P2" : "P1";
            foreach (var key in new[] { "sourceObjectId", "sourceVisibility", "cardNo", "effectKind", "controllerId", "playAbility" })
            {
                var row = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshots[opponent].Stack)));
                row[key] = key switch { "sourceObjectId" => "SOURCE", "sourceVisibility" => "VISIBLE", "controllerId" => opponent,
                    "playAbility" => false, _ => "FORGED" };
                var views = frame.PlayerViews.ToDictionary();
                views[opponent] = views[opponent] with { Snapshot = snapshots[opponent] with { Stack = [row] } };
                // The store validates persisted views before returning a frame.
                // Restore consumes that report and rebuilds served views from
                // the authoritative state, rather than replaying stored views.
                var forged = frame with { PlayerViews = views, ValidationErrors = [] };
                var errors = Errors(forged);
                Assert.Contains(errors, e => e.Contains("ordinary play skill", StringComparison.Ordinal));
                Assert.Equal(ErrorCodes.RecoveryInconsistent,
                    Assert.Throws<MatchSessionException>(() => MatchSession.Restore(forged with { ValidationErrors = errors }, new CoreRuleEngine(), run)).Code);
                var rebuilt = MatchSession.Restore(forged, new CoreRuleEngine(), run);
                foreach (var viewer in new[] { "P1", "P2" })
                    Assert.Equal(MatchStateHasher.HashValue(snapshots[viewer]), MatchStateHasher.HashValue(rebuilt.SnapshotFor(viewer)));
                mutations.Add(new { key, errors });
            }
            foreach (var malformed in new[] { "null-stack", "empty-stack", "duplicate-row" })
            {
                var views = frame.PlayerViews.ToDictionary();
                views[opponent] = views[opponent] with { Snapshot = snapshots[opponent] with { Stack = malformed switch
                    { "null-stack" => null!, "empty-stack" => [], _ => [..snapshots[opponent].Stack!, ..snapshots[opponent].Stack!] } } };
                var forged = frame with { PlayerViews = views, ValidationErrors = [] };
                var errors = Errors(forged);
                Assert.Contains(errors, e => e.Contains("ordinary play skill", StringComparison.Ordinal));
                Assert.Equal(ErrorCodes.RecoveryInconsistent,
                    Assert.Throws<MatchSessionException>(() => MatchSession.Restore(forged with { ValidationErrors = errors }, new CoreRuleEngine(), run)).Code);
                mutations.Add(new { key = malformed, errors });
            }
            var spectatorRow = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(Assert.Single(frame.SpectatorReplayFrame!.SpectatorSnapshot.Stack)))
                { ["sourceObjectId"] = "SOURCE" };
            var spectatorForgery = frame with { SpectatorReplayFrame = frame.SpectatorReplayFrame with
                { SpectatorSnapshot = frame.SpectatorReplayFrame.SpectatorSnapshot with { Stack = [spectatorRow] } }, ValidationErrors = [] };
            var spectatorErrors = Errors(spectatorForgery);
            Assert.NotEmpty(spectatorErrors);
            Assert.Equal(ErrorCodes.RecoveryInconsistent,
                Assert.Throws<MatchSessionException>(() => MatchSession.Restore(spectatorForgery with { ValidationErrors = spectatorErrors }, new CoreRuleEngine(), run)).Code);
            var alteredEvents = run.Events().Select(e => e.Event.Kind == "TRIGGER_QUEUED" ? e with
                { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["sourceObjectId"] = "OTHER" } } } : e).ToArray();
            var mismatch = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), run.State, new CoreRuleEngine(), default, alteredEvents);
            Assert.False(mismatch.IsMatch); Assert.NotEmpty(mismatch.Errors);
            await run.Replay();
            await run.Top();
            Assert.Empty(run.State.StackItems);
            Assert.Contains("PRIVATE-" + player + "-DRAW-1", run.State.PlayerZones[player].Hand);
            Assert.Contains("SOURCE", run.State.PlayerZones[player].Hand);
            Assert.Single(run.Events(), e => e.Event.Kind == "CARD_DRAWN");
            await run.Replay();
            var finalFrame = Frame(initial, run);
            var finalDecoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(finalFrame, Json), Json)!;
            Assert.Empty(Errors(finalDecoded));
            _ = MatchSession.Restore(finalDecoded, new CoreRuleEngine(), run);
            Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(finalDecoded.AuthoritativeState!));
            complete = true;
        }
        finally { Export(player + "-ordinary", initial, run, complete, mutations); }
    }

    public static IEnumerable<object[]> Locations() => new[] { "P1", "P2" }.SelectMany(p =>
        new[] { "HAND", "MAIN_DECK", "BASE", "GRAVEYARD" }.Select(zone => new object[] { p, zone }));

    [Theory]
    [MemberData(nameof(Locations))]
    public async Task AFiniteLocationQueryUsesCurrentVisibilityWithoutChangingTheCapturedFace(string player, string zone)
    {
        var run = new Driver(Opening(player));
        await run.Act(player, new PlayCardCommand("SOURCE", Instructor, [], Destination: "BASE"));
        // A finite projection frame isolates a current zone, not an accepted
        // move or a replacement for the ordinary RETURN command witness.
        var state = Relocate(run.State, player, zone);
        var hash = MatchStateHasher.Hash(state);
        foreach (var viewer in new[] { "P1", "P2" })
            AssertFace(ResolutionResult.BuildSnapshots(state)[viewer], zone is "HAND" or "MAIN_DECK" && viewer != player ? "HIDDEN" : "SOURCE", player);
        AssertFace(ResolutionResult.BuildSpectatorSnapshot(state), zone is "HAND" or "MAIN_DECK" ? "HIDDEN" : "SOURCE", player);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ThePublicSkillDoesNotReadTheCurrentPrivateCardFace(string player)
    {
        var run = new Driver(Opening(player));
        await run.Act(player, new PlayCardCommand("SOURCE", Instructor, [], Destination: "BASE"));
        var state = Relocate(run.State, player, "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["SOURCE"] = state.CardObjects["SOURCE"] with { CardNo = "OGN·210/298" } } };
        // Explicit private-face component; this is not forged accepted history.
        AssertFace(ResolutionResult.BuildSnapshots(state)[player == "P1" ? "P2" : "P1"], "HIDDEN", player);
        AssertFace(ResolutionResult.BuildSpectatorSnapshot(state), "HIDDEN", player);
    }

    private static void AssertFace(SnapshotDto snapshot, string source, string player)
    {
        var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(Instructor, row["cardNo"]); Assert.Equal(Effect, row["effectKind"]);
        Assert.Equal(player, row["controllerId"]); Assert.Equal(true, row["playAbility"]);
        Assert.Equal(source, row["sourceObjectId"]); Assert.Equal(source == "HIDDEN" ? "HIDDEN" : "VISIBLE", row["sourceVisibility"]);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Driver run)
    {
        var sequence = run.Events()[^1].Sequence; var state = run.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            snapshots[p], state.Tick, sequence, prompts[p]));
        return new(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
    }

    private static IReadOnlyList<string> Errors(MatchRecoveryFrame frame) => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence,
        frame.Commands, frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static MatchState Relocate(MatchState state, string player, string zone)
    {
        var old = state.PlayerZones[player];
        var zones = old with { Base = old.Base.Where(id => id != "SOURCE").ToArray(),
            Hand = old.Hand.Where(id => id != "SOURCE").ToArray(), MainDeck = old.MainDeck.Where(id => id != "SOURCE").ToArray(),
            Graveyard = old.Graveyard.Where(id => id != "SOURCE").ToArray() };
        zones = zone switch { "BASE" => zones with { Base = [..zones.Base, "SOURCE"] }, "HAND" => zones with { Hand = [..zones.Hand, "SOURCE"] },
            "MAIN_DECK" => zones with { MainDeck = ["SOURCE", ..zones.MainDeck] }, _ => zones with { Graveyard = [..zones.Graveyard, "SOURCE"] } };
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = zones },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SOURCE"] = new(player, zone) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["SOURCE"] = state.CardObjects["SOURCE"] with
                { ObjectGeneration = state.CardObjects["SOURCE"].ObjectGeneration + (zone == "BASE" ? 0 : 1) } } };
    }

    private static MatchState Opening(string player)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); locations[id] = new(owner, zone); }
        Put("SOURCE", Instructor, player, "HAND"); Put("RETURN", "OGN·104/298", player, "HAND");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var owner in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => "PRIVATE-" + owner + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", owner, "MAIN_DECK");
            var runes = Enumerable.Range(1, 2).Select(i => "PRIVATE-" + owner + "-RUNE-" + i).ToArray();
            foreach (var id in runes) Put(id, "UNL-R03", owner, "RUNE_DECK");
            zones[owner] = PlayerZones.Empty with { Hand = owner == player ? ["SOURCE", "RETURN"] : [], MainDeck = deck, RuneDeck = runes };
        }
        return new MatchState("ORDINARY-SOURCE-PRIVACY-" + player, 0, 5, player,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"], turnPlayerId: player, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, seed: 424242,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) }) with { CardPlays = new(5, [], []) };
    }

    private static void Export(string name, MatchState initial, Driver run, bool complete, object mutations)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_ORDINARY_SOURCE_EVIDENCE") is not { } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new
        {
            scope = "Explicit midgame HAND input with existing OGN087; ordinary typed play and RETURN; no canonical seating/full game",
            complete, initial, commands = run.Commands(), events = run.Events(), final = run.State, mutations,
            snapshots = ResolutionResult.BuildSnapshots(run.State), spectator = ResolutionResult.BuildSpectatorSnapshot(run.State),
            runtime = new { baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current,
                modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, typeof(OrdinaryPlaySourcePrivacyTests).Assembly }
                    .Select(a => new { path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
        }, Json));
    }
}
