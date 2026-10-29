using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;
using static Riftbound.ConformanceTests.DestructionGraveyardFamilyTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionGraveyardOrdinaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(OfficialDeckFormat.CoreRules, "SELF_PAY")]
    [InlineData(OfficialDeckFormat.CoreRules, "DECREE_PAY")]
    [InlineData(OfficialDeckFormat.CoreRules, "DECREE_DECLINE")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "SELF_PAY")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "DECREE_PAY")]
    [InlineData(OfficialDeckFormat.ChinaStandard20260724, "DECREE_DECLINE")]
    public async Task OriginalBlankLegalDecksExerciseSelfDestructionAndIndependentDecreeThenPaidOrDeclinedPhoenix(OfficialDeckFormat format, string profile)
    {
        using var recipe = await Recipe(); var r = recipe.RootElement;
        var run = await Opening(r, format); var phoenix = r.GetProperty("phoenixObjectId").GetString()!;
        await Intent(run, r.GetProperty("selfRayIntent")); await Top(run); await run.Cold();
        var source = Assert.Single(run.State.StackItems).GraveyardPlay!;
        Assert.Equal(phoenix, source.Source.ObjectId); Assert.Equal(new ObjectBinding(phoenix, 3), source.Source);
        await Save(run, format, profile, "self-leading-cost");
        if (profile == "SELF_PAY")
        {
            await Pay(run, true); await Top(run); await run.Cold();
            Assert.NotNull(run.State.PendingEffectPlay); await Save(run, format, profile, "self-effect-play");
            await run.Submit("P1", new PlayCardCommand(phoenix, Phoenix, []));
        }
        else
        {
            await Pay(run, false);
            await Intents(run, r.GetProperty("afterSelfTriggerExplicitlyDeclinedIntents"));
            await run.Cold(); await Intent(run, r.GetProperty("decreeRayIntent")); await Top(run);
            var elder = r.GetProperty("elderObjectId").GetString()!;
            Assert.Equal(3, run.State.CardObjects[elder].Damage); Assert.Contains(elder, run.State.PlayerZones["P2"].Battlefields);
            var delayed = Assert.Single(run.State.StackItems);
            Assert.Equal(Decree, delayed.CardNo); Assert.Equal(elder, delayed.DamageDestruction!.DamagedUnit!.ObjectId);
            Assert.Null(run.State.PendingPayment); await run.Cold(); await Save(run, format, profile, "independent-decree");
            await Top(run); Assert.Contains(elder, run.State.PlayerZones["P2"].Graveyard);
            var phi = Assert.Single(run.State.StackItems).GraveyardPlay!;
            Assert.Equal(Decree, phi.Attribution.CardNo); Assert.Equal(delayed.SourceObjectId, phi.Attribution.Source.ObjectId);
            await run.Cold(); await Save(run, format, profile, "decree-leading-cost");
            var pay = profile == "DECREE_PAY"; await Pay(run, pay);
            if (pay) { await Top(run); await run.Cold(); await Save(run, format, profile, "decree-effect-play"); await run.Submit("P1", new PlayCardCommand(phoenix, Phoenix, [])); }
        }
        Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue); Assert.Null(run.State.PendingPayment); Assert.Null(run.State.PendingEffectPlay);
        Assert.Equal(profile == "DECREE_DECLINE", run.State.PlayerZones["P1"].Graveyard.Contains(phoenix));
        if (profile != "DECREE_DECLINE") { Assert.Contains(phoenix, run.State.PlayerZones["P1"].Base); Assert.Equal(4, run.State.CardObjects[phoenix].ObjectGeneration); }
        Assert.Equal(MatchStatuses.InProgress, run.State.Status);
        await run.Cold(); await Save(run, format, profile, "final");
        var frame = run.Frame(); Assert.Empty(frame.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        output.WriteLine("ORDINARY_DESTRUCTION_GRAVEYARD " + JsonSerializer.Serialize(new {
            format, profile, commands = frame.Commands.Count, events = frame.Events.Count, run.ColdCount, run.OriginalNextMatches,
            originalSeed = run.Initial.Seed, originalBlankOpening = run.Initial.CardObjects.Count == 0, ruleInputs = run.State.RuleInputs,
            fullOriginalActionEventReplayMatched = true, completeGameOrWholeCardCertification = false }, CopiedDuelInsightOpeningTests.Opening.Json));
    }

    [Fact]
    public async Task CoordinatedEarlierDelayedCreationTickPassesShapeButOriginalJournalRejectsIt()
    {
        using var recipe = await Recipe(); var r = recipe.RootElement; var run = await Opening(r, OfficialDeckFormat.CoreRules);
        await Intent(run, r.GetProperty("selfRayIntent")); await Top(run); await Pay(run, false);
        await Intents(run, r.GetProperty("afterSelfTriggerExplicitlyDeclinedIntents"));
        await Intent(run, r.GetProperty("decreeRayIntent")); await Top(run); await run.Cold();
        var state = run.State; var item = Assert.Single(state.StackItems); var c = item.DamageDestruction!;
        Assert.True(c.CreatedAtTick > 1);
        var forged = state with { StackItems = [item with { DamageDestruction = c with { CreatedAtTick = c.CreatedAtTick - 1 } }] };
        var frame = run.Frame(forged); Assert.Empty(frame.ValidationErrors);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(run.Frame(), new CoreRuleEngine(), default));
    }

    private static async Task<JsonDocument> Recipe() => JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "destruction-graveyard-openings.json")));
    private static async Task<CopiedDuelInsightOpeningTests.Opening> Opening(JsonElement recipe, OfficialDeckFormat format)
    {
        var run = new CopiedDuelInsightOpeningTests.Opening(82, format);
        Assert.Equal(82, recipe.GetProperty("openingIntents").GetArrayLength());
        await Intents(run, recipe.GetProperty("openingIntents"));
        Assert.Equal(49, run.Frame().Commands.Count); Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        return run;
    }
    private static async Task Intents(CopiedDuelInsightOpeningTests.Opening run, JsonElement intents)
    {
        var entries = intents.EnumerateArray().ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var actor = entry.GetProperty("playerId").GetString()!;
            var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
            if (command is TapRuneCommand { SourceObjectIds: null } tap)
            {
                // Only consecutive resource preparation shares a production batch command.
                // Every play, recycle, choice and cold-recovery boundary remains separate.
                var runes = new List<string> { tap.SourceObjectId };
                while (index + 1 < entries.Length && runes.Count < 12
                    && entries[index + 1].GetProperty("playerId").GetString() == actor
                    && GameCommandJsonMapper.Map(entries[index + 1].GetProperty("rawCommand"))
                        is TapRuneCommand { SourceObjectIds: null } next)
                {
                    runes.Add(next.SourceObjectId);
                    index++;
                }
                if (runes.Count > 1) command = tap with { SourceObjectIds = runes.ToArray() };
            }
            await run.Submit(actor, command);
        }
    }
    private static Task Intent(CopiedDuelInsightOpeningTests.Opening run, JsonElement entry)
        => run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
    private static async Task Top(CopiedDuelInsightOpeningTests.Opening run) { await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand()); await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand()); }
    private static async Task Pay(CopiedDuelInsightOpeningTests.Opening run, bool pay)
    {
        var cost = run.State.PendingPayment!; Assert.Equal(1, cost.ManaCost); Assert.Equal(1, cost.PowerCost); Assert.Equal(1, cost.PowerCostByTrait["red"]);
        await run.Reject("P2", new PayCostCommand(cost.PaymentId, cost.PaymentWindow, ["PAY"]));
        await run.Reject("P1", new PayCostCommand("stale-payment", cost.PaymentWindow, ["DECLINE"]));
        if (pay)
        {
            if (run.State.RunePools["P1"].Mana == 0) await run.Submit("P1", new TapRuneCommand(run.State.PlayerZones["P1"].Base.First(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !run.State.CardObjects[id].IsExhausted)));
            if (run.State.RunePools["P1"].PowerByTrait.GetValueOrDefault("red") == 0) await run.Submit("P1", new RecycleRuneCommand(run.State.PlayerZones["P1"].Base.First(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && run.State.CardObjects[id].Tags.Contains("COLOR:red"))));
        }
        await run.Submit("P1", new PayCostCommand(cost.PaymentId, cost.PaymentWindow, [pay ? "PAY" : "DECLINE"])); await run.Cold();
    }
    private static async Task Save(CopiedDuelInsightOpeningTests.Opening run, OfficialDeckFormat format, string profile, string phase)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DESTRUCTION_FAMILY_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"{format}-{profile}-{phase}.json.gz"); Assert.False(File.Exists(path));
        await using var file = File.Create(path); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(zip, run.Frame(), CopiedDuelInsightOpeningTests.Opening.JournalJson);
    }
}
