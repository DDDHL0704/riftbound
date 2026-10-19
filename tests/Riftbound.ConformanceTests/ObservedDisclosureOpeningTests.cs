using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class ObservedDisclosureOpeningTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases()
        => from reveal in new[] { false, true }
           from format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 }
           from option in new[] { "KEEP", "BANISH_DECLINE", "BANISH_PLAY" }
           select new object[] { reveal, format, option };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryOriginalOpeningPreservesDisclosureThroughBanishOriginalChoicePlayAndRecovery(
        bool reveal, OfficialDeckFormat format, string option)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "pakaa-protector-openings.json")));
        var recipe = fixture.RootElement.GetProperty("branches").EnumerateArray()
            .Single(row => row.GetProperty("profile").GetString() == "NOCTURNE");
        var run = new CopiedDuelInsightOpeningTests.Opening(reveal ? 41 : 40, format);
        foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        Assert.Equal(35, run.Frame().Commands.Count);
        Assert.Equal(0, run.Initial.Tick); Assert.Equal(0, run.Initial.Seed); Assert.Empty(run.Initial.CardObjects);
        var originalDeck = run.State.PlayerZones["P1"].MainDeck.ToArray();
        var seen = originalDeck.Take(reveal ? 1 : 3).ToArray();
        var nocturne = recipe.GetProperty("top").GetString()!;
        Assert.Equal(nocturne, seen[0]); Assert.Equal(DeckObservationPlayTests.Nocturne, run.State.CardObjects[nocturne].CardNo);
        if (!reveal && run.State.RunePools["P1"].Mana < 1)
            await run.Submit("P1", new TapRuneCommand(Rune()));
        if (run.State.RunePools["P1"].TotalPower < 1)
            await run.Submit("P1", new RecycleRuneCommand(Rune()));
        await run.Cold();
        if (reveal)
        {
            var host = recipe.GetProperty("host").GetString()!;
            var destination = run.State.PlayerZones["P1"].Battlefields.First(run.State.BattlefieldStates.ContainsKey);
            await run.Submit("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination));
            if (run.State.TriggerQueue.Count > 0)
                await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        }
        else
        {
            var trick = run.State.PlayerZones["P1"].Hand.First(id => run.State.CardObjects[id].CardNo == "OGN·183/298");
            await run.Submit("P1", new PlayCardCommand(trick, "OGN·183/298", []));
        }
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var pending = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", pending.Request.Kind);
        Assert.Equal(reveal, pending.Request.Preview!.DeckObservationView!.Public);
        var prompt = run.Prompt("P1");
        Assert.Contains(reveal ? "展示" : "查看", prompt.Reason);
        Assert.Contains(reveal ? "展示" : "查看", Assert.Single(Assert.Single(prompt.Candidates!,
            c => c.Action == CommandTypes.PayCost).SelectionSteps!).Label);
        Assert.DoesNotContain(run.Prompt("P2").Candidates!, c => c.Action == CommandTypes.PayCost && c.Enabled);
        foreach (var view in ResolutionResult.BuildSnapshots(run.State).Values.Append(ResolutionResult.BuildSpectatorSnapshot(run.State)))
            Assert.Equal(reveal ? "REVEAL" : "LOOK", JsonSerializer.SerializeToElement(view.Timing)
                .GetProperty("pendingPayment").GetProperty("deckObservationKind").GetString());
        foreach (var id in seen)
        {
            Assert.Contains(id, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)["P1"]));
            if (reveal) Assert.Contains(id, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)["P2"]));
            else Assert.DoesNotContain(id, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)["P2"]));
        }
        foreach (var id in originalDeck.Skip(seen.Length))
            Assert.DoesNotContain(id, JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(run.State)));
        await Export("banish", nocturne);
        await run.Cold();
        await run.Reject("P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
        await run.Reject("P1", new PayCostCommand("STALE", "RULE_REPLACEMENT", ["BANISH"]));
        await run.Reject("P1", new EndTurnCommand());
        var banish = option != "KEEP";
        await run.Submit("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [banish ? "BANISH" : "KEEP"]));
        if (!reveal)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal("DECK_EFFECT", choice.ChoiceWindow);
            Assert.Equal(seen.Where(id => !banish || id != nocturne), choice.ContextObjectIds);
            Assert.Null(run.State.PendingEffectPlay);
            await run.Cold();
            await run.Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                [banish ? choice.LegalObjectIds[0] : nocturne]));
        }
        if (banish)
        {
            var play = Assert.IsType<PendingEffectPlayState>(run.State.PendingEffectPlay);
            Assert.Equal(nocturne, Assert.Single(play.Sources).Key);
            Assert.Equal(reveal ? "REVEAL" : "LOOK", play.Parent.ObservedPlayPermission!.Context.Instruction.Kind);
            await Export("play", nocturne);
            await run.Cold(); await run.Reject("P1", new EndTurnCommand());
            if (option == "BANISH_PLAY")
                await run.Submit("P1", new PlayCardCommand(nocturne, DeckObservationPlayTests.Nocturne, []));
            else await run.Submit("P1", new ChooseCardsCommand(play.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains(nocturne, option == "BANISH_PLAY" ? run.State.PlayerZones["P1"].Base : run.State.PlayerZones["P1"].Banished);
        }
        else Assert.Contains(nocturne, run.State.PlayerZones["P1"].Hand);
        Assert.Null(run.State.PendingRuleChoice); Assert.Null(run.State.PendingEffectPlay); Assert.Null(run.State.ObservedDeckPlays);
        if (reveal) Assert.Equal(originalDeck.Skip(1), run.State.PlayerZones["P1"].MainDeck);
        else Assert.Equal(originalDeck.Skip(3), run.State.PlayerZones["P1"].MainDeck.Take(originalDeck.Length - 3));
        await run.Cold();
        for (var i = 0; i < 16 && (run.State.TriggerQueue.Count > 0 || run.State.StackItems.Count > 0 || run.State.FocusPlayerId is not null); i++)
        {
            if (run.State.TriggerQueue.Count > 0)
                await run.Submit(run.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (run.State.PriorityPlayerId is not null) await run.Submit(run.State.PriorityPlayerId, new PassPriorityCommand());
            else await run.Submit(run.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.Empty(run.State.TriggerQueue); Assert.Empty(run.State.StackItems);
        await run.Cold(); await run.Submit("P1", new EndTurnCommand()); await run.Cold();
        var frame = run.Frame();
        Assert.All(frame.Commands, c => Assert.Equal(run.State.RuleInputs, c.RuleInputs));
        Assert.Empty(frame.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DISCLOSURE_EVIDENCE") is { Length: > 0 } directory)
        {
            var path = Path.Combine(directory, $"{Mode()}-{format}-{option}-original-frame.json.gz");
            Assert.False(File.Exists(path)); await using var file = File.Create(path);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, frame, CopiedDuelInsightOpeningTests.Opening.JournalJson);
        }
        output.WriteLine("ORDINARY_OBSERVATION_DISCLOSURE " + JsonSerializer.Serialize(new {
            mode = Mode(), format = format.ToString(), option, commands = frame.Commands.Count, events = frame.Events.Count,
            run.ColdCount, run.OriginalNextMatches, originalBlankSeed = 0, originalUnchanged35CommandPrefix = true,
            privateUnseenCardsRemainHidden = true, originalActionEventStateAndBothViewsReplayed = true, ruleInputs = run.State.RuleInputs }));

        string Rune() => run.State.PlayerZones["P1"].Base.First(id => run.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !run.State.CardObjects[id].IsExhausted);
        string Mode() => reveal ? "REVEAL" : "LOOK";
        async Task Export(string kind, string source)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_DISCLOSURE_EVIDENCE") is not { Length: > 0 } directory) return;
            Directory.CreateDirectory(directory); var name = $"{Mode()}-{format}-{option}-{kind}";
            var folder = Path.Combine(directory, name); Assert.False(Directory.Exists(folder)); Directory.CreateDirectory(folder);
            foreach (var player in new[] { "P1", "P2" })
            {
                await File.WriteAllTextAsync(Path.Combine(folder, player + "-snapshot.json"), JsonSerializer.Serialize(
                    ResolutionResult.BuildSnapshots(run.State)[player], CopiedDuelInsightOpeningTests.Opening.Json));
                await File.WriteAllTextAsync(Path.Combine(folder, player + "-prompt.json"), JsonSerializer.Serialize(
                    run.Prompt(player), CopiedDuelInsightOpeningTests.Opening.Json));
            }
            await File.WriteAllTextAsync(Path.Combine(folder, "branch.json"), JsonSerializer.Serialize(new {
                name, kind, sourceId = source, expectedObservationKind = Mode(), originalBlankSeed = 0,
                format = format.ToString(), ruleInputs = run.State.RuleInputs }));
        }
    }
}
