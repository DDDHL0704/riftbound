using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyNativeTests
{
    [Theory]
    [InlineData("spell", "OGN·057/298")]
    [InlineData("draw", "OGN·083/298")]
    [InlineData("equipment", "SFD·139/221")]
    [InlineData("mixed", "OGN·057/298")]
    [InlineData("defend", "SFD·230/221")]
    public async Task ActualHideTurnResourcesAndReactionUseProductionControlsWithColdReplay(string branch, string card)
    {
        var initial = StandbySourceRulesTests.Position(card);
        initial = AttackDamageDistributionTests.Spell(initial, "REPLY", "OGN·048/298", "P2");
        if (branch == "mixed")
        {
            initial = StandbyTestDriver.WithControlledBattlefield(initial);
            initial = OfficialHoldSequenceTests.AddUnit(initial, "MIXED-ANCHOR", "OGN·001/298", StandbyTestDriver.BattlefieldId, "P1");
            initial = AttackDamageDistributionTests.Spell(initial, "MIXED-DRAW", "OGN·083/298", "P1");
        }
        if (branch == "defend")
        {
            initial = OfficialHoldSequenceTests.AddUnit(initial, "DEFEND-NATIVE-ATTACKER", "UNL-057/219", "F", "P2");
            initial = LocalDestructionRecallTests.At(initial, "DEFEND-NATIVE-ATTACKER", "P2", "BASE");
            var cards = initial.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            var deck = initial.PlayerZones["P1"].MainDeck;
            cards[deck[0]] = cards[deck[0]] with { CardNo = "OGN·121/298" };
            cards[deck[1]] = cards[deck[1]] with { CardNo = "OGN·199/298" };
            foreach (var id in initial.PlayerZones["P1"].RuneDeck)
                cards[id] = cards[id] with { CardNo = "UNL-R01", Tags = [CardObjectTags.RuneCard, "COLOR:red"] };
            initial = initial with { CardObjects = cards };
        }
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_STANDBY_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var current = initial;
        var journal = new Journal();
        var engine = new CoreRuleEngine();
        var session = new MatchSession(initial, engine, journal);
        var steps = new List<object>();
        var native = 0;
        var restores = 0;
        var staleChecked = false;
        void Export(string name, object value)
        {
            if (dir is null) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string actor, GameCommand fallback)
        {
            var prompt = session.PromptFor(actor);
            Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[actor]);
            Export(name + "-selection.json", fallback);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), options), options)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var command = fallback;
            var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                raw = doc.RootElement.Clone();
                command = GameCommandJsonMapper.Map(raw);
                native++;
            }
            if (fallback is PlayCardCommand play)
                Export(name + "-quote.json", engine.PreviewPlayCard(current, actor, new("quote", prompt.PromptId!, current.Tick, play)));
            steps.Add(new { name, action = fallback.CmdType });
            var result = await session.SubmitAsync(actor, name, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            current = result.State;
            OfficialGraveyardRecastTests.Restore(current);
            ArmamentMightTests.PublicRecovery(current);
            var count = journal.Entries.Count;
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted);
            Assert.Equal(count, journal.Entries.Count);
            var commands = Commands(journal);
            var events = Events(journal);
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(current);
            var prompts = ResolutionResult.BuildPrompts(current);
            var views = current.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, current.Tick, sequence, snapshots[id], current.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(current.RoomId, current.Tick, sequence, [], current);
            var errors = MatchRecoveryValidator.Validate(current.RoomId, sequence, commands, events, views, current, current.Tick, spectator);
            Assert.Empty(errors);
            session = MatchSession.Restore(new(current.RoomId, current.Tick, sequence, commands, events, views, errors, current, initial, spectator), new CoreRuleEngine(), journal);
            restores++;
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted);
            Assert.Equal(count, journal.Entries.Count);
            if (!staleChecked)
            {
                var stale = await session.SubmitAsync(actor, "stale-shadow", command, raw, default);
                Assert.False(stale.Accepted);
                Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
                Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
                staleChecked = true;
            }
            return result;
        }
        await Submit("hide", "P1", new HideCardCommand("SHADOW", card, "BATTLEFIELD:F", ["STANDBY_A"]));
        if (branch == "mixed")
            await Submit("hide-second", "P1", new HideCardCommand("MIXED-DRAW", "OGN·083/298", "BATTLEFIELD:" + StandbyTestDriver.BattlefieldId, ["STANDBY_A"]));
        Assert.Equal("F", current.CardObjects["SHADOW"].StandbyPlacement!.Battlefield.ObjectId);
        await Submit("end-turn", "P1", new EndTurnCommand());
        Assert.Null(current.TurnStartStep);
        foreach (var i in new[] { 1, 2, 3 }) await Submit("tap-" + i, "P2", new TapRuneCommand("SHADOW-P2-RUNE-" + i));
        foreach (var i in new[] { 4, 5 }) await Submit("recycle-" + i, "P2", new RecycleRuneCommand("SHADOW-P2-RUNE-" + i));
        await Submit("reply", "P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await Submit("offer", "P2", new PassPriorityCommand());
        if (branch == "defend")
        {
            await Submit("resolve-reply", "P1", new PassPriorityCommand());
            await Submit("move-attacker", "P2", new MoveUnitCommand("DEFEND-NATIVE-ATTACKER", "BASE", "BATTLEFIELD:F", []));
            await Submit("offer-focus", "P2", new PassFocusCommand());
        }
        var resources = current.RunePools["P1"];
        if (branch == "mixed")
            await Submit("shadow-play", "P1", new RevealCardCommand("SHADOW", card, ["HOST"], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        else
            await Submit("shadow-play", "P1", new PlayCardCommand("SHADOW", card, branch == "spell" ? ["HOST"] : []));
        Assert.Equal(resources, current.RunePools["P1"]);
        if (branch == "equipment")
        {
            var choice = current.PendingCardChoice!;
            await Submit("attach-target", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        }
        if (branch == "defend")
        {
            var choice = Assert.IsType<PendingCardChoiceState>(current.PendingCardChoice);
            await Submit("defend-target", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DEFEND-NATIVE-ATTACKER"]));
            Assert.Equal("STANDBY_DEFEND", Assert.Single(current.StackItems).FieldContext!.Kind);
            Assert.Equal(0, current.CardObjects["DEFEND-NATIVE-ATTACKER"].Damage);
        }
        await Submit("shadow-pass-1", current.PriorityPlayerId!, new PassPriorityCommand());
        await Submit("shadow-pass-2", current.PriorityPlayerId!, new PassPriorityCommand());
        if (branch == "equipment")
        {
            Assert.Equal("HOST", current.CardObjects["SHADOW"].AttachedToObjectId);
            Assert.Equal("F", current.ObjectLocations["SHADOW"].BattlefieldObjectId);
        }
        else if (branch == "defend")
        {
            Assert.Contains("SHADOW", current.PlayerZones["P1"].Battlefields);
            Assert.True(current.CardObjects["SHADOW"].IsDefending);
            Assert.Equal(2, current.CardObjects["DEFEND-NATIVE-ATTACKER"].Damage);
            Assert.Empty(current.StackItems);
            Assert.Equal(TimingStates.SpellDuelOpen, current.TimingState);
        }
        else Assert.Contains("SHADOW", current.PlayerZones["P1"].Graveyard);
        if (branch == "mixed")
        {
            Assert.True(current.CardObjects["MIXED-DRAW"].IsFaceDown);
            Assert.Equal(StandbyTestDriver.BattlefieldId, current.CardObjects["MIXED-DRAW"].StandbyPlacement!.Battlefield.ObjectId);
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(journal), current, new CoreRuleEngine(), default, Events(journal));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (native > 0) Assert.Equal(steps.Count, native);
        Assert.Equal(steps.Count, restores);
        Export("steps.json", steps);
        Export("replay.json", new { nativeCommands = native, accepted = steps.Count, coldRestores = restores, staleRejected = 1, dedupAfterColdRestore = true, replay });
    }

    private static RecoveredCommand[] Commands(Journal j) => j.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
    private static RecoveredEvent[] Events(Journal j) => j.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
