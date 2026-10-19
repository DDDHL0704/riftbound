using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class HweiMoveDiscardOrdinaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules, false)]
    [InlineData(OfficialDeckFormat.CoreRules, true)]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, false)]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, true)]
    public async Task OriginalBlankOpeningDrawsDiscardsDualTypeAndResolvesEitherOrderWithCompleteOriginalReplay(OfficialDeckFormat format, bool reverse)
    {
        using var recipe = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "hwei-move-discard-openings.json")));
        var run = new CopiedDuelInsightOpeningTests.Opening(80, format);
        foreach (var entry in recipe.RootElement.GetProperty("openingIntents").EnumerateArray())
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        var host = recipe.RootElement.GetProperty("host").GetString()!;
        Assert.Equal(HweiMoveDiscardTypesTests.Hwei, run.State.CardObjects[host].CardNo);
        var runes = run.State.PlayerZones["P1"].Base.Where(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).Take(2).ToArray();
        Assert.Equal(2, runes.Length);
        foreach (var id in runes) await run.Submit("P1", new TapRuneCommand(id));
        var before = run.State; var top = before.PlayerZones["P1"].MainDeck[0];
        Assert.Equal(recipe.RootElement.GetProperty("top").GetString(), top);
        Assert.Equal(HweiMoveDiscardTypesTests.Dual, before.CardObjects[top].CardNo);
        Assert.Contains(CardObjectTags.UnitCard, before.CardObjects[top].Tags);
        Assert.Contains(CardObjectTags.EquipmentCard, before.CardObjects[top].Tags);
        await run.Cold(); await Save(run, format, reverse, "before-move");
        var start = run.Frame().Events[^1].Sequence;
        var field = before.PlayerZones["P1"].Battlefields.First(before.BattlefieldStates.ContainsKey);
        var move = new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field);
        await run.Reject("P2", move); await run.Submit("P1", move); await run.Cold();
        if (run.State.TriggerQueue.Count > 0)
            await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var discard = run.State.PendingHandChoice!;
        Assert.Equal(0, discard.DrawCount); Assert.Contains(top, discard.LegalObjectIds);
        Assert.Equal(before.PlayerZones["P1"].Hand.Append(top), run.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain('"' + top + '"', JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)["P2"]));
        Assert.DoesNotContain('"' + top + '"', JsonSerializer.Serialize(ResolutionResult.BuildPrompts(run.State)["P2"]));
        await run.Cold(); await Save(run, format, reverse, "pending-discard");
        await run.Reject("P2", new ChooseHandCardsCommand(discard.ChoiceId, discard.ChoiceWindow, [top]));
        await run.Reject("P1", new ChooseHandCardsCommand(discard.ChoiceId, discard.ChoiceWindow, []));
        await run.Reject("P1", new ChooseHandCardsCommand("expired-choice", discard.ChoiceWindow, [top]));
        await run.Submit("P1", new ChooseHandCardsCommand(discard.ChoiceId, discard.ChoiceWindow, [top]));
        Assert.Equal(2, run.State.TriggerQueue.Count);
        Assert.All(run.State.TriggerQueue, t => { Assert.Equal("P1", t.ControllerId);
            Assert.Equal(3, t.MovementObserver!.DiscardInline!.Types);
            Assert.Equal(new ObjectBinding(top, before.CardObjects[top].ObjectGeneration + 1), t.MovementObserver.DiscardInline.DiscardedCard); });
        await run.Cold(); await Save(run, format, reverse, "after-discard");
        var ordered = run.State.TriggerQueue.OrderBy(t => t.MovementObserver!.DiscardInline!.BranchType).ToArray();
        if (reverse) Array.Reverse(ordered);
        await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered.Select(t => t.TriggerId).ToArray()));
        var byStack = run.State.StackItems.Where(s => s.MovementObserver?.DiscardInline is not null)
            .ToDictionary(s => s.StackItemId, s => s.MovementObserver!.DiscardInline!.BranchType);
        for (var i = 0; i < 40 && (run.State.TriggerQueue.Count > 0 || run.State.StackItems.Count > 0
            || run.State.PendingCardChoice is not null || run.State.PriorityPlayerId is not null || run.State.FocusPlayerId is not null); i++)
        {
            if (run.State.PendingCardChoice is { } choice)
            {
                Assert.Equal("MOVEMENT_DISCARD_RUNE_TARGETS", choice.ChoiceWindow);
                Assert.All(runes, id => Assert.Contains(id, choice.LegalObjectIds));
                await run.Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, runes));
                await run.Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, runes)); await run.Cold();
            }
            else if (run.State.TriggerQueue.Count > 0)
                await run.Submit(run.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (run.State.PriorityPlayerId is { } priority) await run.Submit(priority, new PassPriorityCommand());
            else await run.Submit(run.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        Assert.Null(run.State.PendingHandChoice); Assert.Null(run.State.PendingCardChoice);
        Assert.Equal(TimingStates.NeutralOpen, run.State.TimingState);
        Assert.Equal(before.PlayerZones["P1"].Hand, run.State.PlayerZones["P1"].Hand);
        Assert.Equal(before.PlayerZones["P1"].MainDeck.Skip(1), run.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(before.PlayerZones["P1"].Graveyard.Append(top), run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(JsonSerializer.Serialize(before.CardPlays), JsonSerializer.Serialize(run.State.CardPlays));
        Assert.Equal(8, CurrentPowerTargetTests.Snapshot(run.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32());
        Assert.All(runes, id => Assert.False(run.State.CardObjects[id].IsExhausted));
        var events = run.Frame().Events.Where(e => e.Sequence > start).Select(e => e.Event).ToArray();
        Assert.Single(events, e => e.Kind == "CARD_DRAWN");
        Assert.Single(events, e => e.Kind == "CARD_DISCARDED");
        Assert.Equal(2, events.Count(e => e.Kind == "RUNE_READIED"));
        Assert.DoesNotContain(events, e => e.Kind is "UNIT_PLAYED_TO_BASE" or "CARDS_REVEALED" or "BURNOUT_APPLIED");
        Assert.Equal(reverse ? new[] { 2, 1 } : new[] { 1, 2 }, events.Where(e => e.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Payload["effectKind"], "MOVEMENT_DISCARD_TYPE_INLINE")).Select(e => byStack[(string)e.Payload["triggerId"]!]));
        Assert.DoesNotContain('"' + before.PlayerZones["P1"].MainDeck[1] + '"', JsonSerializer.Serialize(events));
        await run.Cold(); await Save(run, format, reverse, "complete");
        await run.Submit("P1", new EndTurnCommand());
        Assert.Empty(run.State.CardObjects[host].UntilEndOfTurnPowerModifiers);
        Assert.Equal(5, CurrentPowerTargetTests.Snapshot(run.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32());
        await run.Cold(); await Save(run, format, reverse, "next-turn");
        output.WriteLine("HWEI_ORIGINAL_OPENING " + JsonSerializer.Serialize(new { format = format.ToString(), reverse,
            originalSeed = run.Initial.Seed, commands = run.Frame().Commands.Count, events = run.Frame().Events.Count,
            run.ColdCount, run.OriginalNextMatches, originalFullReplayMatched = true, drawCount = 1,
            inlineCount = 2, readiedRunes = 2, expectedMight = 8, nextTurnMight = 5, completedOrdinaryGame = false }));
    }

    private static async Task Save(CopiedDuelInsightOpeningTests.Opening run, OfficialDeckFormat format, bool reverse, string phase)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_HWEI_TYPED_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); var path = Path.Combine(root, $"{format}-{reverse}-{phase}.json.gz");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(zip, run.Frame(), CopiedDuelInsightOpeningTests.Opening.JournalJson);
    }
}
