using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OwnedCardCombinationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ReturnedWarBandKeepsItsConfirmedBuffBesideTempleAndActualDragonhornEntry(string player)
    {
        var initial = Opening(player, false); var run = new Driver(initial);
        await run.Act(player, new PlayCardCommand("DRAGON", "VEN·118", [], Destination: "BATTLEFIELD:F"));
        Assert.True(run.State.CardObjects["DRAGON"].IsExhausted);
        Assert.Equal(34, run.State.RunePools[player].Mana);
        Assert.Contains(CardCombatKeywordNames.Bulwark, run.State.CardObjects["DRAGON"].Tags);
        Might(run.State, "DRAGON", 7);
        await run.Act(player, new PlayCardCommand("BAND", "VEN·026", [], Destination: "BATTLEFIELD:F"));
        var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
        Assert.Contains("DRAGON", choice.LegalObjectIds);
        await run.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAGON"]));
        await run.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", ["BAND"]));
        await run.Top();
        Assert.Contains("BAND", run.State.PlayerZones[player].Hand);
        Assert.True(Assert.Single(run.State.StackItems).SourceConfirmed);
        var opponent = Other(player);
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(run.State)[opponent], ResolutionResult.BuildSpectatorSnapshot(run.State) })
        {
            var row = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal("HIDDEN", row["sourceObjectId"]);
            Assert.Equal("VEN·026", row["cardNo"]);
        }
        await Checkpoint("band-returned-" + player, initial, run);
        await run.Top(); await Drain(run);
        Might(run.State, "DRAGON", 10);
        var aura = Assert.Single(run.State.ContinuousEffects, e => e.SourceObjectId == "F" && e.TargetObjectId == "DRAGON");
        Assert.Equal(1, aura.PowerDelta);
        await run.Act(player, new EndTurnCommand());
        Might(run.State, "DRAGON", 7);
        Assert.Contains("BAND", run.State.PlayerZones[player].Hand);
        await Checkpoint("band-final-" + player, initial, run, true);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task TempleKeepsActualGraveyardReadyAssassinAliveThroughPaidDiscardDamageUntilGrantExpires(string player)
    {
        var initial = Opening(player, true); var run = new Driver(initial); var opponent = Other(player);
        await run.Act(player, new PlayCardCommand("ASSASSIN", "VEN·013", [], Destination: "BATTLEFIELD:F"));
        Assert.False(run.State.CardObjects["ASSASSIN"].IsExhausted);
        Assert.Contains("NAMED-GRAVE", run.State.PlayerZones[player].Graveyard);
        Might(run.State, "ASSASSIN", 5);
        await run.Act(player, new PlayCardCommand("GRANT", "OGN·057/298", ["ASSASSIN"]));
        await run.Top(); await Drain(run);
        Might(run.State, "ASSASSIN", 6);
        await run.Act(player, new PlayCardCommand("PULSE", "VEN·049", []));
        await run.Act(player, new PassPriorityCommand());
        await run.Act(opponent, new PlayCardCommand("DAMAGE", "VEN·008", ["ASSASSIN"], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
        Assert.Equal(37, run.State.RunePools[opponent].Mana);
        Assert.Contains("FODDER", run.State.PlayerZones[opponent].Graveyard);
        await run.Top();
        Assert.Equal("BATTLEFIELD", run.State.ObjectLocations["ASSASSIN"].Zone);
        Assert.Equal(5, run.State.CardObjects["ASSASSIN"].Damage);
        Might(run.State, "ASSASSIN", 6);
        var damage = Assert.Single(run.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "DAMAGE"));
        Assert.Equal(5, damage.Event.Payload["damage"]);
        await Checkpoint("damage-survived-" + player, initial, run);
        await Drain(run);
        await run.Act(player, new EndTurnCommand());
        Assert.Equal("GRAVEYARD", run.State.ObjectLocations["ASSASSIN"].Zone);
        Assert.Contains("ASSASSIN", run.State.PlayerZones[player].Graveyard);
        Assert.Contains("NAMED-GRAVE", run.State.PlayerZones[player].Graveyard);
        Assert.Contains("FODDER", run.State.PlayerZones[opponent].Graveyard);
        await Checkpoint("damage-final-" + player, initial, run, true);
    }

    private static async Task Checkpoint(string name, MatchState initial, Driver run, bool final = false)
    {
        await run.Replay(); var state = run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            snapshots[p], state.Tick, sequence, prompts[p]));
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(decoded.AuthoritativeState!));
        Assert.Empty(MatchRecoveryValidator.Validate(decoded.RoomId, decoded.LastEventSequence, decoded.Commands, decoded.Events,
            decoded.PlayerViews, decoded.AuthoritativeState, decoded.CurrentTick, decoded.SpectatorReplayFrame));
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), run);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(decoded.ReplayInitialState!, decoded.Commands,
            decoded.AuthoritativeState!, new CoreRuleEngine(), default, decoded.Events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var forgeries = new List<object>();
        if (final)
        {
            var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                { ["ANCHOR"] = state.CardObjects["ANCHOR"] with { Power = 99 } } };
            var rejectedState = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), forged, new CoreRuleEngine(), default, run.Events());
            Assert.False(rejectedState.IsMatch); Assert.NotEmpty(rejectedState.Errors);
            var events = run.Events(); events[0] = events[0] with { Event = events[0].Event with { Kind = "FORGED_" + events[0].Event.Kind } };
            var rejectedEvents = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), state, new CoreRuleEngine(), default, events);
            Assert.False(rejectedEvents.IsMatch); Assert.NotEmpty(rejectedEvents.Errors);
            forgeries.Add(new { kind = "terminal-power", rejectedState.IsMatch, rejectedState.Errors });
            forgeries.Add(new { kind = "original-event", rejectedEvents.IsMatch, rejectedEvents.Errors });
        }
        if (Environment.GetEnvironmentVariable("RIFTBOUND_CARD_COMBINATION_EVIDENCE") is { } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new
            {
                scope = "Explicit midgame HAND and public battlefield/graveyard inputs; actual subsequent commands, not canonical seating/full game",
                initial, frame = decoded, commands = run.Commands(), events = run.Events(), final = state, terminal = final, forgeries,
                runtime = new { baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current,
                    modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, typeof(OwnedCardCombinationTests).Assembly }
                        .Select(a => new { path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
            }, Json));
        }
    }

    private static async Task Drain(Driver run)
    {
        for (var i = 0; i < 24; i++)
        {
            Assert.Null(run.State.PendingCardChoice);
            if (run.State.StackItems.Count > 0) { await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand()); continue; }
            if (run.State.TimingState == TimingStates.SpellDuelOpen && run.State.FocusPlayerId is { } focus)
            { await run.Act(focus, new PassFocusCommand()); continue; }
            Assert.Empty(run.State.TriggerQueue); Assert.Equal(TimingStates.NeutralOpen, run.State.TimingState); return;
        }
        Assert.Fail("Combined card commands did not return to neutral timing.");
    }

    private static void Might(MatchState state, string id, int expected)
    {
        var controller = state.CardObjects[id].ControllerId!;
        foreach (var viewer in new[] { "P1", "P2" })
            Assert.Equal(expected, CurrentPowerTargetTests.Snapshot(state, viewer).GetProperty("players").GetProperty(controller)
                .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32());
    }

    private static string Other(string player) => player == "P1" ? "P2" : "P1";

    private static MatchState Opening(string player, bool damage)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) with
                { ObjectGeneration = zone is "HAND" or "MAIN_DECK" or "RUNE_DECK" ? 0 : 1 };
            locations[id] = new(owner, zone, field);
        }
        Put("F", "VEN·159", "P1", "BATTLEFIELD", "F"); cards["F"] = cards["F"] with { ControllerId = player };
        Put("OTHER", "OGN·278/298", "P1", "BATTLEFIELD", "OTHER"); cards["OTHER"] = cards["OTHER"] with { ControllerId = player };
        Put("ANCHOR", "OGN·175/298", player, "BATTLEFIELD", "F");
        Put("OTHER-ANCHOR", "OGN·175/298", player, "BATTLEFIELD", "OTHER");
        if (damage)
        {
            Put("ASSASSIN", "VEN·013", player, "HAND"); Put("NAMED-GRAVE", "VEN·013", player, "GRAVEYARD");
            Put("GRANT", "OGN·057/298", player, "HAND"); Put("PULSE", "VEN·049", player, "HAND");
            Put("DAMAGE", "VEN·008", Other(player), "HAND"); Put("FODDER", "OGN·219/298", Other(player), "HAND");
        }
        else
        {
            Put("DRAGON", "VEN·118", player, "HAND"); Put("BAND", "VEN·026", player, "HAND");
            Put("RETURN", "OGN·104/298", player, "HAND");
        }
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var owner in new[] { "P1", "P2" })
        {
            foreach (var i in Enumerable.Range(1, 4))
            { Put(owner + "-DRAW-" + i, "OGN·219/298", owner, "MAIN_DECK"); Put(owner + "-RUNE-" + i, "UNL-R03", owner, "RUNE_DECK"); }
            string[] In(string zone) => locations.Where(p => p.Value.PlayerId == owner && p.Value.Zone == zone).Select(p => p.Key).ToArray();
            zones[owner] = PlayerZones.Empty with { Hand = In("HAND"), Base = In("BASE"), Battlefields = In("BATTLEFIELD"),
                MainDeck = In("MAIN_DECK"), RuneDeck = In("RUNE_DECK"), Graveyard = In("GRAVEYARD") };
        }
        return new MatchState("OWNED-CARD-COMBINATION-" + player + "-" + damage, 0, 5, player,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"], turnPlayerId: player, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, seed: 424242,
            runePools: new Dictionary<string, RunePool> { ["P1"] = Pool(), ["P2"] = Pool() }) with { CardPlays = new(5, [], []) };
        static RunePool Pool() => new(40, 0, new Dictionary<string, int>
            { [RuneTrait.Red] = 20, [RuneTrait.Green] = 20, [RuneTrait.Blue] = 20, [RuneTrait.Orange] = 20, [RuneTrait.Purple] = 20, [RuneTrait.Yellow] = 20 });
    }
}
