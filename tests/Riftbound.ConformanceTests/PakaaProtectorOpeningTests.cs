using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class PakaaProtectorOpeningTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        {
            yield return ["UNIT", false, false, format];
            yield return ["NONUNIT", false, false, format];
            yield return ["NOCTURNE", false, false, format];
            yield return ["NOCTURNE", true, false, format];
            yield return ["NOCTURNE", true, true, format];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryBlankOpeningPaysForPakaThenMovesThroughSharedRevealAndOriginalReplay(
        string profile, bool banish, bool play, OfficialDeckFormat format)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "pakaa-protector-openings.json")));
        var row = fixture.RootElement.GetProperty("branches").EnumerateArray().Single(b => b.GetProperty("profile").GetString() == profile);
        var run = new CopiedDuelInsightOpeningTests.Opening(profile == "UNIT" ? 33 : profile == "NONUNIT" ? 34 : 35, format);
        foreach (var entry in row.GetProperty("openingIntents").EnumerateArray())
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        var host = row.GetProperty("host").GetString()!; var top = row.GetProperty("top").GetString()!;
        Assert.Equal("P1", run.State.TurnPlayerId); Assert.Contains(host, run.State.PlayerZones["P1"].Base);
        Assert.Equal("VEN·033", run.State.CardObjects[host].CardNo); Assert.False(run.State.CardObjects[host].IsExhausted);
        Assert.Equal(top, run.State.PlayerZones["P1"].MainDeck[0]);
        if (profile == "NOCTURNE" && run.State.RunePools["P1"].TotalPower < 1)
        {
            var rune = run.State.PlayerZones["P1"].Base.First(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
            await run.Submit("P1", new RecycleRuneCommand(rune));
        }
        await run.Cold();
        var deck = run.State.PlayerZones["P1"].MainDeck.ToArray(); var hand = run.State.PlayerZones["P1"].Hand.ToArray();
        var grave = run.State.PlayerZones["P1"].Graveyard.ToArray(); var beforeEvents = run.Frame().Events.Count;
        var destination = run.State.PlayerZones["P1"].Battlefields.First(id => run.State.BattlefieldStates.ContainsKey(id));
        await run.Reject("P2", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination));
        await run.Submit("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination));
        var captured = Assert.Single(run.State.TriggerQueue.Select(t => t.MovementObserver)
            .Concat(run.State.StackItems.Select(i => i.MovementObserver)), c => c is { CardNo: "VEN·033" });
        Assert.Equal(TriggerKinds.UnitMovedRevealTopDrawUnitOrTrashPower, captured!.Kind);
        await run.Cold();
        if (run.State.TriggerQueue.Count > 0)
            await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Top(run);
        if (profile == "NOCTURNE")
        {
            var choice = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
            Assert.Equal("DECK_OBSERVATION_BANISH", choice.Request.Kind);
            Assert.True(choice.Request.Preview!.DeckObservationView!.Public);
            await run.Cold();
            await run.Reject("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
            await run.Reject("P1", new PayCostCommand("STALE", "RULE_REPLACEMENT", ["BANISH"]));
            await run.Submit("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [banish ? "BANISH" : "KEEP"]));
            if (banish)
            {
                Assert.Equal(hand, run.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(1), run.State.PlayerZones["P1"].MainDeck);
                var pending = Assert.IsType<PendingEffectPlayState>(run.State.PendingEffectPlay);
                Assert.Equal(top, Assert.Single(pending.Sources).Key);
                Assert.Equal("REVEAL", pending.Parent.ObservedPlayPermission!.Context.Instruction.Kind);
                await run.Cold(); await run.Reject("P1", new EndTurnCommand());
                await run.Reject("P2", new PlayCardCommand(top, DeckObservationPlayTests.Nocturne, []));
                if (play) await run.Submit("P1", new PlayCardCommand(top, DeckObservationPlayTests.Nocturne, []));
                else await run.Submit("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                Assert.Contains(top, play ? run.State.PlayerZones["P1"].Base : run.State.PlayerZones["P1"].Banished);
            }
        }
        Assert.Equal(deck.Skip(1), run.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(profile == "NONUNIT" ? grave.Append(top) : grave, run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(profile == "UNIT" || profile == "NOCTURNE" && !banish ? hand.Append(top) : hand, run.State.PlayerZones["P1"].Hand);
        var snapshot = CurrentPowerTargetTests.Snapshot(run.State);
        Assert.Equal(profile == "NONUNIT" ? 6 : 4, snapshot.GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32());
        Assert.Null(run.State.PendingEffectPlay); Assert.Null(run.State.ObservedDeckPlays);
        await run.Cold();
        for (var count = 0; count < 12 && (run.State.TriggerQueue.Count > 0 || run.State.StackItems.Count > 0 || run.State.FocusPlayerId is not null); count++)
        {
            if (run.State.TriggerQueue.Count > 0)
                await run.Submit(run.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (run.State.PriorityPlayerId is not null) await run.Submit(run.State.PriorityPlayerId, new PassPriorityCommand());
            else await run.Submit(run.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        await run.Cold(); await run.Submit("P1", new EndTurnCommand()); await run.Cold();
        var frame = run.Frame();
        Assert.All(frame.Commands, command => Assert.Equal(run.State.RuleInputs, command.RuleInputs));
        var revealed = frame.Events.Skip(beforeEvents).Where(e => e.Event.Kind == "CARDS_REVEALED").ToArray();
        Assert.Single(revealed);
        output.WriteLine("PAKA_OPENING_EVIDENCE " + JsonSerializer.Serialize(new { profile, banish, play, format = format.ToString(),
            commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            originalSeed = run.Initial.Seed, originalFullReplayMatched = true, sourceCardNo = "VEN·033" }));
    }

    private static async Task Top(CopiedDuelInsightOpeningTests.Opening run)
    {
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
    }
}
