using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class ReversalTargetChoiceOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules, false)]
    [InlineData(OfficialDeckFormat.CoreRules, true)]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, false)]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, true)]
    public async Task OriginalBlankLegalOpeningKeepsOrRemakesTargetsAcrossColdRecovery(OfficialDeckFormat format, bool remake)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "reversal-target-openings.json")));
        var recipe = fixture.RootElement;
        var run = new CopiedDuelInsightOpeningTests.Opening(49, format);
        var count = 0;
        foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
        {
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
            if (++count == 6) await run.Cold();
        }
        Assert.Equal(49, run.Frame().Commands.Count); Assert.Equal(28, run.State.TurnNumber);
        var counter = recipe.GetProperty("sourceObjectId").GetString()!;
        var source = recipe.GetProperty("targetSourceObjectId").GetString()!;
        var target = recipe.GetProperty("targetStackItemId").GetString()!;
        var oldUnit = recipe.GetProperty("originalTargetObjectId").GetString()!;
        var newUnit = recipe.GetProperty("newTargetObjectId").GetString()!;
        Assert.Equal(target, Assert.Single(run.State.StackItems).StackItemId);
        Assert.Equal(4, run.State.RunePools["P2"].Mana);
        Assert.Equal(3, run.State.RunePools["P2"].PowerByTrait.GetValueOrDefault("green"));
        Assert.Equal(2, run.State.CardObjects[oldUnit].Power); Assert.Equal(2, run.State.CardObjects[newUnit].Power);
        var hand = run.State.PlayerZones["P2"].Hand.Count;
        var p1Deck = run.State.PlayerZones["P1"].MainDeck.Count;
        var p2Deck = run.State.PlayerZones["P2"].MainDeck.Count;
        var firstEvent = run.Frame().Events.Count;
        await run.Cold();
        await run.Submit("P2", new PlayCardCommand(counter, ReversalTargetChoiceTests.Card, [target]));
        await run.Cold(); await Top(); await run.Cold(); await Save("target-choice");
        var request = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
        Assert.Equal("P2", request.PlayerId); Assert.Equal("SPELL_CHOICE_REMAKE", request.Kind);
        Assert.Equal("P2", request.Preview!.StackItems.Single(i => i.StackItemId == target).ControllerId);
        await run.Reject("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        await run.Reject("P2", new PayCostCommand(request.Id + "-old", "RULE_REPLACEMENT", ["KEEP_ALL"]));
        await run.Reject("P2", new PayCostCommand(request.Id, "WRONG", ["KEEP_ALL"]));
        await run.Reject("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["KEEP_ALL", "CONFIRM"]));
        if (remake)
        {
            await Choose(Assert.Single(request.Options, o => o.ObjectIds?.Contains(newUnit) == true).Id);
            await run.Cold(); await Save("draft"); await Choose("CONFIRM");
        }
        else await Choose("KEEP_ALL");
        var controlled = Assert.Single(run.State.StackItems);
        Assert.Equal("P2", controlled.ControllerId);
        Assert.Equal(new[] { remake ? newUnit : oldUnit }, controlled.TargetObjectIds);
        Assert.Equal(0, run.State.RunePools["P2"].Mana); Assert.Equal(0, run.State.RunePools["P2"].TotalPower);
        Assert.Contains(counter, run.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(source, run.State.PlayerZones["P1"].Graveyard);
        await run.Cold(); await Save("after-control"); await Top();
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        Assert.Null(run.State.PendingRuleChoice); Assert.Null(run.State.PendingCardChoice);
        Assert.Equal(remake ? 2 : 1, run.State.CardObjects[oldUnit].Power);
        Assert.Equal(remake ? 1 : 2, run.State.CardObjects[newUnit].Power);
        Assert.Equal(hand, run.State.PlayerZones["P2"].Hand.Count);
        Assert.Equal(p1Deck, run.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(p2Deck - 1, run.State.PlayerZones["P2"].MainDeck.Count);
        Assert.Contains(source, run.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(source, run.State.PlayerZones["P2"].Graveyard);
        var events = run.Frame().Events.Skip(firstEvent).Select(e => e.Event).ToArray();
        Assert.Single(events, e => e.Kind == "STACK_ITEM_CONTROL_GAINED");
        Assert.DoesNotContain(events, e => e.Kind == "STACK_ITEM_COUNTERED");
        Assert.Equal(remake ? 1 : 0, events.Count(e => e.Kind == "SPELL_CHOICES_REMADE"));
        var cost = Assert.Single(events, e => e.Kind == "COST_PAID");
        var payload = JsonSerializer.SerializeToElement(cost.Payload);
        Assert.Equal(4, payload.GetProperty("mana").GetInt32());
        Assert.Equal(3, payload.GetProperty("powerByTrait").GetProperty("green").GetInt32());
        await run.Cold(); await Save("final");
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(run.Frame(), new CoreRuleEngine(), default));
        Assert.True(run.ColdCount >= 6); Assert.True(run.OriginalNextMatches >= 5);
        output.WriteLine("ORDINARY_REVERSAL_TARGET_CHOICE " + JsonSerializer.Serialize(new {
            format, remake, commands = run.Frame().Commands.Count, events = run.Frame().Events.Count,
            run.ColdCount, run.OriginalNextMatches, originalBlankSeed = run.Initial.Seed,
            actualRuleInputs = run.State.RuleInputs, allOriginalReplayMatched = true,
            completeGameOrWholePrintingCertification = false }, CopiedDuelInsightOpeningTests.Opening.Json));

        async Task Top() { await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand()); await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand()); }
        async Task Choose(string option)
        {
            var current = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
            await run.Submit("P2", new PayCostCommand(current.Id, "RULE_REPLACEMENT", [option]));
        }
        async Task Save(string phase)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_REVERSAL_TARGET_EVIDENCE") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            await using var file = new FileStream(Path.Combine(folder, $"{format}-{remake}-{phase}.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, run.Frame(), CopiedDuelInsightOpeningTests.Opening.JournalJson);
        }
    }
}
