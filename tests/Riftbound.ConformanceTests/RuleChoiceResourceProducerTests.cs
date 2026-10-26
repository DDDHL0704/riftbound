using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuleChoiceResourceProducerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsecutiveResourceChildrenKeepTheirExactOrderedPrefix(bool reverse)
    {
        // Real seats and the existing finite QA seed retain the original command
        // history. This checks producer steps, not surrender or SQL acceptance.
        var journal = new Journal();
        IMatchSession session = new MatchSession("resource-producer-prefix-" + reverse, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        const string scenario = "typed-power-payment-mixed-recycle";
        var seeded = await session.SeedScenarioAsync("P1", "producer-prefix-source", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        await Act("P1", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        await Act(State().PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(Replay(State().PendingRuleChoice!, true));
        string[] runes = ["P1-RUNE-RED-PARTIAL-PAYMENT-001", "P1-RUNE-BLUE-EXTRA-PAYMENT-001"];
        if (reverse) Array.Reverse(runes);
        await Select("RECYCLE_RUNE:" + runes[0]);
        var prefix = Assert.Single(Replay(State().PendingRuleChoice!, true));
        await Select("RECYCLE_RUNE:" + runes[1]);
        var pending = Assert.IsType<RuleChoiceContinuation>(State().PendingRuleChoice);
        var original = JsonSerializer.Serialize(journal.Entries, Json);
        var steps = Replay(pending, true);
        Assert.Equal(2, steps.Length);
        Assert.Equal(prefix.GetRawText(), steps[0].GetRawText());
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index]; var rune = runes[index];
            Assert.Equal("P1", step.GetProperty("playerId").GetString());
            Assert.Equal("RECYCLE_RUNE:" + rune, step.GetProperty("choiceId").GetString());
            var before = step.GetProperty("before").Deserialize<MatchState>(Json)!;
            var after = step.GetProperty("after").Deserialize<MatchState>(Json)!;
            Assert.Equal(1 + index, before.RunePools["P1"].TotalPower);
            Assert.Equal(2 + index, after.RunePools["P1"].TotalPower);
            Assert.Contains(rune, before.PlayerZones["P1"].Base);
            Assert.DoesNotContain(rune, after.PlayerZones["P1"].Base);
            Assert.Equal(new[] { "P1-RUNE-BOTTOM-001" }.Concat(runes.Take(index + 1)), after.PlayerZones["P1"].RuneDeck);
            Assert.Equal("RUNE_DECK", after.ObjectLocations[rune].Zone);
            Assert.True(before.CardObjects[rune].IsExhausted);
            Assert.False(after.CardObjects[rune].IsExhausted);
            Assert.Equal(0, after.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
            var events = step.GetProperty("events").EnumerateArray().ToArray();
            Assert.Equal(new[] { "RUNE_RECYCLED", "POWER_GAINED" }, events.Select(value => value.GetProperty("kind").GetString()));
            Assert.All(events, value => Assert.Equal(rune, value.GetProperty("payload").GetProperty("sourceObjectId").GetString()));
        }
        var restored = JsonSerializer.Deserialize<RuleChoiceContinuation>(JsonSerializer.Serialize(pending, Json), Json)!;
        Assert.Equal(steps.Select(value => value.GetRawText()), Replay(restored, true).Select(value => value.GetRawText()));
        Assert.Empty(Replay(pending with { Answers = pending.Answers.Reverse().ToArray() }, false));
        Assert.Empty(Replay(pending with { Answers = pending.Answers.Skip(1).ToArray() }, false));
        Assert.Empty(Replay(pending with { Request = pending.Request with { Kind = "FORGED_RESOURCE_STEP" } }, false));
        Assert.Equal(original, JsonSerializer.Serialize(journal.Entries, Json));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, "producer-prefix-" + reverse); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                reverse, steps, pending, originalJournal = journal.Entries,
                scope = "Producer ordered child differences/events and strict transcript replay only; no surrender/SQL/API/L3 claim.",
                ruleInputs = RuleInputIdentity.Current }, Json));
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        Task Select(string option)
        {
            var request = State().PendingRuleChoice!.Request;
            return Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        async Task Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State().Tick;
            var result = await session.SubmitAsync(actor, "producer-prefix-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
    }

    private static JsonElement[] Replay(RuleChoiceContinuation pending, bool expected)
    {
        var method = typeof(CoreRuleEngine).GetMethod("TryReplayRuleChoiceResources",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        object?[] arguments = [pending, null];
        Assert.Equal(expected, Assert.IsType<bool>(method.Invoke(null, arguments)));
        Assert.NotNull(arguments[1]);
        return JsonSerializer.SerializeToElement(arguments[1], arguments[1]!.GetType(), Json).EnumerateArray().ToArray();
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
