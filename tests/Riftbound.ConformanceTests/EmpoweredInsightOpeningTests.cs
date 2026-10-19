using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class EmpoweredInsightOpeningTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases()
        => from songs in new[] { 0, 1, 2 }
           from format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 }
           from recycle in new[] { false, true }
           select new object[] { songs, format, recycle };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryBlankOpeningPaysRealEmpowerThenResolvesEveryPrivateInsightAndOriginalReplay(
        int songs, OfficialDeckFormat format, bool recycle)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "empowered-insight-openings.json")));
        var row = fixture.RootElement.GetProperty("branches").EnumerateArray().Single(value => value.GetProperty("songs").GetInt32() == songs);
        var host = row.GetProperty("host").GetString()!;
        // Reuse the existing journal/recovery harness. Engine format validation
        // does not certify VEN regional tournament eligibility.
        var run = new CopiedDuelInsightOpeningTests.Opening(songs, format);
        foreach (var entry in row.GetProperty("openingIntents").EnumerateArray())
        {
            var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
            await run.Submit(entry.GetProperty("playerId").GetString()!, command);
        }
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        Assert.Equal("P1", run.State.TurnPlayerId);
        Assert.Contains(host, run.State.PlayerZones["P1"].Base);
        Assert.False(run.State.CardObjects[host].IsEmpowered);
        Assert.Equal(songs, run.State.CardObjects.Values.Count(card => card.AttachedRuleText?.Unit.ObjectId == host));
        var needed = Math.Max(0, 2 - run.State.RunePools["P1"].Mana);
        var available = run.State.PlayerZones["P1"].Base.Where(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !run.State.CardObjects[id].IsExhausted).Take(needed).ToArray();
        Assert.Equal(needed, available.Length);
        foreach (var id in available) await run.Submit("P1", new TapRuneCommand(id));
        await run.Cold(); var mana = run.State.RunePools["P1"].Mana;
        var activation = new ActivateAbilityCommand(host, "EMPOWER:0:0", []);
        await run.Reject("P2", activation);
        await run.Submit("P1", activation);
        Assert.Equal(mana - 2, run.State.RunePools["P1"].Mana);
        Assert.False(run.State.CardObjects[host].IsEmpowered);
        Assert.Empty(run.State.TriggerQueue); Assert.Null(run.State.PendingCardChoice);
        await run.Cold(); await Top(run);
        Assert.True(run.State.CardObjects[host].IsEmpowered);
        var contexts = run.State.TriggerQueue.Select(trigger => trigger.InsightContext!).Concat(
            run.State.StackItems.Where(item => item.InsightContext is not null).Select(item => item.InsightContext!)).ToArray();
        Assert.Equal(songs + 1, contexts.Length); Assert.Equal(songs, contexts.Count(context => context.Origin is not null));
        Assert.All(contexts, context => { Assert.Equal("EMPOWERED", context.Kind); Assert.Equal(2, context.Count); Assert.Equal("P1", context.ControllerId); });
        await run.Cold();
        if (run.State.TriggerQueue.Count > 0) await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds:
            run.State.TriggerQueue.Select(trigger => trigger.TriggerId).Reverse().ToArray()));
        var hand = run.State.PlayerZones["P1"].Hand.ToArray(); var rejections = 1; string? previous = null;
        for (var index = 0; index <= songs; index++)
        {
            var deck = run.State.PlayerZones["P1"].MainDeck.ToArray(); Assert.True(deck.Length >= 2);
            await run.Cold(); await Top(run);
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal(deck.Take(2), choice.ContextObjectIds);
            Assert.Equal("P1", choice.PlayerId); Assert.Null(run.State.PendingPayment);
            foreach (var id in deck.Take(2))
            {
                Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(run.Prompt("P2")));
                Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(run.Frame().SpectatorReplayFrame!.SpectatorSnapshot));
            }
            await run.Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); rejections++;
            await run.Reject("P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, [])); rejections++;
            await run.Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [deck[2]])); rejections++;
            if (previous is not null) { await run.Reject("P1", new ChooseCardsCommand(previous, choice.ChoiceWindow, [])); rejections++; }
            await run.Cold(); var selected = recycle && index == 0 ? deck.Take(2).ToArray() : [];
            await run.Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
            if (selected.Length == 0)
            {
                var order = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
                Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
                await run.Reject("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, [deck[0], deck[0]])); rejections++;
                await run.Cold(); await run.Submit("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, [deck[1], deck[0]]));
                Assert.Equal(new[] { deck[1], deck[0] }.Concat(deck.Skip(2)), run.State.PlayerZones["P1"].MainDeck);
            }
            else
            {
                Assert.Equal(deck.Skip(2), run.State.PlayerZones["P1"].MainDeck.Take(deck.Length - 2));
                Assert.Equal(selected.Order(StringComparer.Ordinal), run.State.PlayerZones["P1"].MainDeck.TakeLast(2).Order(StringComparer.Ordinal));
            }
            previous = choice.ChoiceId; Assert.Equal(hand, run.State.PlayerZones["P1"].Hand); await run.Cold();
        }
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue); Assert.Null(run.State.PendingCardChoice);
        Assert.DoesNotContain(run.Frame().Events, entry => entry.Event.Kind is "CARD_REVEALED" or "MAIN_DECK_CARDS_REVEALED"
            || entry.Event.Kind.Contains("BURN", StringComparison.Ordinal));
        await run.Reject("P1", activation); rejections++;
        await run.Submit("P1", new EndTurnCommand()); await run.Cold();
        Assert.True(run.State.CardObjects[host].IsEmpowered);
        var frame = run.Frame();
        if (Environment.GetEnvironmentVariable("RIFTBOUND_EMPOWERED_INSIGHT_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"songs-{songs}-{format}-recycle-{recycle}.json.gz");
            Assert.False(File.Exists(path)); await using var file = File.Create(path);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, frame, CopiedDuelInsightOpeningTests.Opening.JournalJson);
        }
        output.WriteLine("ORDINARY_EMPOWERED_INSIGHT " + JsonSerializer.Serialize(new { songs, format, recycle,
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            atomicEngineRejections = rejections, originalBlankSeed = 0, originalFullActionEventReplayMatched = true,
            ruleInputs = run.State.RuleInputs, regionalTournamentEligibilityCertified = false }));
    }

    private static async Task Top(CopiedDuelInsightOpeningTests.Opening run)
    {
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
    }
}
