using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenUnitEntryRecoveryWitnessTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static IEnumerable<object[]> Cases() => new[] { "VEN·026", "VEN·048", "VEN·118" }.SelectMany(no =>
        new[] { "P1", "P2" }.SelectMany(player => new[] { "BASE", "BATTLEFIELD:BF" }.Select(destination => new object[] { no, player, destination })));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BothPlayersOrdinaryEntryKeepsJsonRecoveryDuplicatesAndTheCompleteOriginalLog(string no, string player, string destination)
    {
        var initial = Opening(no, player); var run = new Driver(initial);
        await run.Act(player, new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.True(run.State.CardObjects["UNIT"].IsExhausted);
        Assert.True(run.State.CardObjects["UNIT"].ObjectGeneration > initial.CardObjects["UNIT"].ObjectGeneration);
        Assert.Equal(no == "VEN·026" ? 16 : 14, run.State.RunePools[player].Mana);
        var entryKind = destination == "BASE" ? "UNIT_PLAYED_TO_BASE" : "UNIT_PLAYED_TO_BATTLEFIELD";
        var events = run.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(events, "COST_PAID") < Array.IndexOf(events, entryKind));
        if (no == "VEN·026")
        {
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Contains("UNIT", choice.LegalObjectIds);
            await run.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
            await run.Top();
            Assert.Equal(6, run.State.CardObjects["UNIT"].Power);
        }
        if (no == "VEN·048")
        {
            await run.Top();
            Assert.Equal(["PRIVATE-" + player + "-DRAW-1"], run.State.PlayerZones[player].Hand);
            Assert.Single(run.Events(), e => e.Event.Kind == "CARD_DRAWN");
        }
        if (no == "VEN·118") Assert.Contains(CardCombatKeywordNames.Bulwark, run.State.CardObjects["UNIT"].Tags);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", run.State.ObjectLocations["UNIT"].Zone);
        Assert.Empty(run.State.StackItems); Assert.Null(run.State.PendingCardChoice);
        await run.Replay();
        var sequence = run.Events()[^1].Sequence; var snapshots = ResolutionResult.BuildSnapshots(run.State); var prompts = ResolutionResult.BuildPrompts(run.State);
        var views = run.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, run.State.Tick, sequence,
            snapshots[p], run.State.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(run.State.RoomId, run.State.Tick, sequence, run.Commands(), run.Events(), views, [], run.State, initial,
            MatchReplayRedactor.BuildSpectatorFrame(run.State.RoomId, run.State.Tick, sequence, [], run.State)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(decoded.ReplayInitialState!));
        Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var viewer in new[] { "P1", "P2" })
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[viewer]), MatchStateHasher.HashValue(restored.SnapshotFor(viewer)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[viewer]), MatchStateHasher.HashValue(restored.PromptFor(viewer)));
        }
        var roundtripReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(decoded.ReplayInitialState!, decoded.Commands,
            decoded.AuthoritativeState!, new CoreRuleEngine(), default, decoded.Events);
        Assert.True(roundtripReplay.IsMatch, string.Join("; ", roundtripReplay.Errors));
        var forged = run.State with { CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects)
            { ["UNIT"] = run.State.CardObjects["UNIT"] with { Power = 99 } } };
        var rejected = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), forged, new CoreRuleEngine(), default, run.Events());
        Assert.False(rejected.IsMatch); Assert.NotEmpty(rejected.Errors);
        var forgedEvents = run.Events().Select(e => e.Event.Kind == entryKind ? e with
            { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["sourceObjectId"] = "OTHER" } } } : e).ToArray();
        var rejectedEvents = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), run.State, new CoreRuleEngine(), default, forgedEvents);
        Assert.False(rejectedEvents.IsMatch); Assert.NotEmpty(rejectedEvents.Errors);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_ENTRY_WITNESS_EVIDENCE") is { } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, no + "-" + player + "-" + destination.Replace(':', '_') + ".json"), JsonSerializer.Serialize(new
            {
                scope = "Explicit midgame HAND input with public battlefield anchors; actual ordinary entry; not canonical seating/full game",
                initial, commands = run.Commands(), events = run.Events(), final = run.State,
                forgeries = new[] { new { kind = "current-power", rejected.IsMatch, rejected.Errors },
                    new { kind = "entry-event", rejectedEvents.IsMatch, rejectedEvents.Errors } },
                runtime = new { baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current,
                    modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, typeof(VenUnitEntryRecoveryWitnessTests).Assembly }
                        .Select(a => new { path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
            }, Json));
        }
    }

    private static MatchState Opening(string no, string player)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner) with
                { ObjectGeneration = zone is "HAND" or "MAIN_DECK" ? 0 : 1 };
            locations[id] = new(owner, zone, field);
        }
        var opponent = player == "P1" ? "P2" : "P1";
        Put("UNIT", no, player, "HAND");
        Put("BF", "OGN·278/298", player, "BATTLEFIELD", "BF");
        Put("OTHER", "OGN·278/298", opponent, "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = player };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = opponent };
        Put("ALLY", "OGN·219/298", player, "BATTLEFIELD", "BF");
        Put("ENEMY", "OGN·219/298", opponent, "BATTLEFIELD", "OTHER");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var owner in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => "PRIVATE-" + owner + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", owner, "MAIN_DECK");
            zones[owner] = PlayerZones.Empty with { Hand = owner == player ? ["UNIT"] : [], MainDeck = deck,
                Battlefields = owner == player ? ["BF", "ALLY"] : ["OTHER", "ENEMY"] };
        }
        return new MatchState("VEN-ENTRY-WITNESS-" + no + "-" + player, 0, 5, player,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"], turnPlayerId: player, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, seed: 424242,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) }) with { CardPlays = new(5, [], []) };
    }
}
