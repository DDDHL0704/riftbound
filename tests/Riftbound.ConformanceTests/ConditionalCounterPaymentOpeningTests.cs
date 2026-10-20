using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class ConditionalCounterPaymentOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules, "BASE_PAY")]
    [InlineData(OfficialDeckFormat.CoreRules, "BASE_DECLINE")]
    [InlineData(OfficialDeckFormat.CoreRules, "ECHO_PAY_BOTH")]
    [InlineData(OfficialDeckFormat.CoreRules, "ECHO_PAY_THEN_DECLINE")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "BASE_PAY")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "BASE_DECLINE")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "ECHO_PAY_BOTH")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "ECHO_PAY_THEN_DECLINE")]
    public async Task OriginalBlankLegalDecksKeepIndependentResolutionPaymentsThroughColdRecovery(OfficialDeckFormat format, string profile)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "conditional-counter-openings.json")));
        var recipe = fixture.RootElement;
        var run = new CopiedDuelInsightOpeningTests.Opening(32, format);
        var prefixCount = 0;
        foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
        {
            if (prefixCount == 6) await run.Cold();
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
            prefixCount++;
        }
        Assert.Equal(32, run.Frame().Commands.Count);
        Assert.Equal(12, run.State.TurnNumber);
        Assert.Equal(4, run.State.RunePools["P1"].Mana);
        Assert.Equal(4, run.State.RunePools["P2"].Mana);
        Assert.Equal(0, run.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, run.State.RunePools["P2"].TotalPower);
        var counter = recipe.GetProperty("sourceObjectId").GetString()!;
        var targets = recipe.GetProperty("targetStackItemIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
        var sources = recipe.GetProperty("targetSourceObjectIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
        Assert.Equal(targets, run.State.StackItems.Select(s => s.StackItemId));
        var hand = run.State.PlayerZones["P1"].Hand.Count;
        var deck = run.State.PlayerZones["P1"].MainDeck.Count;
        var firstEvent = run.Frame().Events.Count;
        var echo = profile.StartsWith("ECHO_", StringComparison.Ordinal);
        await run.Cold();
        await run.Submit("P2", new PlayCardCommand(counter, "SFD·136/221", [targets[0]],
            OptionalCosts: echo ? ["ECHO"] : null, RepeatChoices: echo ? [new("", [targets[1]])] : null));
        Assert.Equal(echo ? 0 : 2, run.State.RunePools["P2"].Mana);
        Assert.Equal(0, run.State.RunePools["P2"].TotalPower);
        await run.Cold();
        await Top();
        Assert.Equal(4, run.State.RunePools["P1"].Mana);
        await Save("first-payment");
        var firstPay = profile != "BASE_DECLINE";
        await Decide(firstPay);
        if (echo)
        {
            Assert.NotNull(run.State.PendingRuleChoice);
            Assert.Equal(2, run.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"].Mana);
            await Save("second-payment");
            await Decide(profile == "ECHO_PAY_BOTH");
        }
        Assert.Null(run.State.PendingRuleChoice);
        Assert.DoesNotContain(run.State.StackItems, item => item.SourceObjectId == counter);
        await Save("after-counter");
        var kept = profile is "BASE_PAY" or "ECHO_PAY_BOTH" ? 2 : 1;
        Assert.Equal(kept, run.State.StackItems.Count);
        for (var i = 0; run.State.StackItems.Count > 0 && i < 3; i++) await Top();
        Assert.Empty(run.State.StackItems);
        Assert.Empty(run.State.TriggerQueue);
        Assert.Null(run.State.PendingPayment);
        Assert.Null(run.State.PendingEffectPlay);
        Assert.Equal(MatchStatuses.InProgress, run.State.Status);
        Assert.Equal(hand + kept, run.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(deck - kept, run.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(profile == "BASE_DECLINE" ? 4 : profile == "ECHO_PAY_BOTH" ? 0 : 2, run.State.RunePools["P1"].Mana);
        Assert.All(sources, source => Assert.Contains(source, run.State.PlayerZones["P1"].Graveyard));
        Assert.Contains(counter, run.State.PlayerZones["P2"].Graveyard);
        var events = run.Frame().Events.Skip(firstEvent).Select(e => e.Event).ToArray();
        var counters = events.Where(e => e.Kind == "STACK_ITEM_COUNTERED").ToArray();
        if (profile is "BASE_PAY" or "ECHO_PAY_BOTH") Assert.Empty(counters);
        else Assert.Equal(targets[profile == "BASE_DECLINE" ? 0 : 1],
            Assert.Single(counters).Payload["stackItemId"]);
        if (echo) Assert.Equal(2, events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"
            && Equals(e.Payload.GetValueOrDefault("effectKind"), "HARD_BARGAIN_COUNTER_SPELL_UNLESS_CONTROLLER_PAYS_2")));
        Assert.Equal(profile == "BASE_DECLINE" ? 0 : profile == "ECHO_PAY_BOTH" ? 2 : 1,
            events.Count(e => e.Kind == "COST_PAID"
                && Equals(e.Payload.GetValueOrDefault("reason"), "COUNTER_TARGET_MANA_PAYMENT")));
        await run.Cold();
        await Save("final");
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(run.Frame(), new CoreRuleEngine(), default));
        output.WriteLine("ORDINARY_CONDITIONAL_COUNTER_PAYMENT " + JsonSerializer.Serialize(new {
            format, profile, commands = run.Frame().Commands.Count, events = run.Frame().Events.Count,
            run.ColdCount, run.OriginalNextMatches, originalBlankSeed = run.Initial.Seed,
            actualRuleInputs = run.State.RuleInputs, fullOriginalReplayMatched = true,
            completeGameOrWholeCardCertification = false }, CopiedDuelInsightOpeningTests.Opening.Json));

        async Task Top()
        {
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        async Task Decide(bool pay)
        {
            var request = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
            Assert.Equal("P1", request.PlayerId);
            Assert.Equal("COUNTER_TARGET_MANA_PAYMENT", request.Kind);
            Assert.Equal(2, request.ManaCost);
            Assert.Contains(request.Options, o => o.Id == "SPEND_MANA:2");
            await run.Cold();
            await run.Reject("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
            await run.Reject("P1", new PayCostCommand("stale-counter", "RULE_REPLACEMENT", ["SPEND_MANA:2"]));
            await run.Reject("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_MANA:0"]));
            await run.Submit("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "SPEND_MANA:2" : "DECLINE"]));
            await run.Cold();
        }
        async Task Save(string phase)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_CONDITIONAL_COUNTER_EVIDENCE") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            await using var file = new FileStream(Path.Combine(folder, $"{format}-{profile}-{phase}.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, run.Frame(), CopiedDuelInsightOpeningTests.Opening.JournalJson);
        }
    }
}
