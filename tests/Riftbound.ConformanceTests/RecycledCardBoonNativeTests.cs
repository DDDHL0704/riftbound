using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RecycledCardBoonNativeTests
{
    [Theory]
    [InlineData("OGN·235/298", false)]
    [InlineData("OGN·235/298", true)]
    [InlineData("SFD·237/221", false)]
    [InlineData("SFD·237/221", true)]
    [InlineData("SFD·237*/221", false)]
    [InlineData("SFD·237*/221", true)]
    public async Task ProductionPredictHookRecycleAndBoonControlsColdRecoverDeduplicateAndReplay(string card, bool hook)
    {
        var initial = hook ? RecycledCardBoonTests.AddKarma(HookDeckPlayTests.Position(), card) : GrantedPredictTests.Hand(card);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_RECYCLE_BOON_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, (card.StartsWith("OGN") ? "primary" : card.Contains("*") ? "promo" : "alternate") + (hook ? "-hook" : "-predict"));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        Export("initial.json", initial);
        if (hook) {
            var ability = P4ActivatedAbilityCatalog.GetAll().Single(a => a.SourceCardNo == HookDeckPlayTests.Hook && a.Kind == ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest);
            await Submit("activate", "P1", new ActivateAbilityCommand("GEAR", ability.AbilityId, ["HOST"], OptionalCosts: []));
            for (var i = 0; i < 2; i++) await Submit("parent-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
            var deck = current.PendingCardChoice!;
            await Submit("recycle", "P1", new ChooseCardsCommand(deck.ChoiceId, deck.ChoiceWindow, ["HOOK_TOP"]));
            Assert.NotNull(current.PendingEffectPlay); Assert.Equal(2, current.TriggerQueue.Count);
            Assert.Single(current.TriggerQueue, t => t.SourceObjectId == "HOST" && t.LastBreathDraw is not null);
            Assert.Single(current.TriggerQueue, t => t.SourceObjectId == "KARMA" && t.FieldContext?.Kind == "RECYCLE_BOON");
            await Submit("play", "P1", new PlayCardCommand("HOOK_TOP", "OGN·096/298", [], OptionalCosts: []));
            Assert.Contains("HOOK_TOP", current.PlayerZones["P1"].Base);
        } else {
            await Submit("play", "P1", new PlayCardCommand("MECH", card, [], OptionalCosts: []));
            for (var i = 0; i < 2; i++) await Submit("parent-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
            var deck = current.PendingCardChoice!;
            await Submit("recycle", "P1", new ChooseCardsCommand(deck.ChoiceId, deck.ChoiceWindow, [deck.LegalObjectIds[0]]));
        }
        var choice = current.PendingCardChoice!; var target = hook ? "HOOK_TOP" : "MECH";
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Contains(target, choice.LegalObjectIds);
        Assert.DoesNotContain(CardObjectTags.Boon, current.CardObjects[target].Tags);
        await Submit("target", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        for (var i = 0; i < 2; i++) await Submit("boon-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains(CardObjectTags.Boon, current.CardObjects[target].Tags);
        if (hook)
        {
            Assert.NotNull(Assert.Single(current.StackItems).LastBreathDraw);
            var hand = current.PlayerZones["P1"].Hand.ToArray();
            await Submit("death-pass-0", current.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(hand, current.PlayerZones["P1"].Hand);
            await Submit("death-pass-1", current.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(hand.Length + 1, current.PlayerZones["P1"].Hand.Count);
            Assert.Contains(CardObjectTags.Boon, current.CardObjects[target].Tags);
        }
        Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { card, hook, native, commands = journal.Entries.Count, restores = steps.Count, events = Events().Length, replay });
        if (native > 0) Assert.Equal(steps.Count, native);
        async Task Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt); Export(name + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(name + "-quote.json", engine.PreviewPlayCard(current, actor, new("quote", prompt.PromptId!, current.Tick, play)));
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            if (dir is not null && File.Exists(Path.Combine(dir, name + "-command.json"))) {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json")));
                raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++;
            }
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString()); Assert.Equal(current.Tick, raw.GetProperty("snapshotTick").GetInt64());
            var result = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(result.Accepted, result.ErrorMessage);
            current = OfficialInsightAndSpellLockTests.Restore(result.State); steps.Add(name);
            var count = journal.Entries.Count; Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var sequence = journal.Entries[^1].CompletedEventSequence;
            session = MatchSession.Restore(new(initial.RoomId, current.Tick, sequence, Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], current, initial), engine, journal);
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var stale = await session.SubmitAsync(actor, "stale-" + name, command, raw, default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
