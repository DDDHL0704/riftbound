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

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.DragonSoulSageCardNo, false, "", 8, 1, 0, false)]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, false, "", 7, 2, 0, false)]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo, true, P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix + "RESOURCE", 8, 2, 0, false)]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, false, "", 7, 2, 0, true)]
    [InlineData(P4ActivatedAbilityCatalog.LuxCardNo, false, "", 7, 1, 2, false)]
    [InlineData(P4ActivatedAbilityCatalog.AncientSteleCardNo, false, P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix + "2", 5, 3, 0, false)]
    public async Task NestedResourceChildrenCaptureTheirCostsWithoutTheOuterReplacement(string cardNo,
        bool levelSix, string costs, int manaAfter, int powerAfter, int spellOnlyAfter, bool removed)
    {
        // Existing scoped midgame fixtures isolate resource-child costs from a
        // pending destruction replacement; this is not ordinary-room acceptance.
        var initial = ReplacementResourceTests.AddResource(OfficialSettReplacementTests.Position(power: 1), cardNo);
        if (cardNo == P4ActivatedAbilityCatalog.LuxCardNo)
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["RESOURCE"] = OfficialGarenConquestBattlefieldCountTests.Printed("RESOURCE", cardNo, "P2") } };
        if (levelSix)
            initial = initial with { PlayerExperience = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 6 } };
        var opened = await OfficialSettReplacementTests.Open(initial);
        var generated = await ReplacementResourceTests.Resource(opened.State, costs: costs);
        var pending = Assert.IsType<RuleChoiceContinuation>(generated.State.PendingRuleChoice);
        var original = JsonSerializer.Serialize(generated.State, Json);
        var step = Assert.Single(Replay(pending, true));
        Assert.Equal("P2", step.GetProperty("playerId").GetString());
        Assert.StartsWith("RESOURCE:RESOURCE:", step.GetProperty("choiceId").GetString());
        var before = step.GetProperty("before").Deserialize<MatchState>(Json)!;
        var after = step.GetProperty("after").Deserialize<MatchState>(Json)!;
        Assert.Equal(7, before.RunePools["P2"].Mana);
        Assert.Equal(1, before.RunePools["P2"].TotalPower);
        Assert.Equal(0, before.RunePools["P2"].SpellOnlyMana);
        Assert.Equal(manaAfter, after.RunePools["P2"].Mana);
        Assert.Equal(powerAfter, after.RunePools["P2"].TotalPower);
        Assert.Equal(spellOnlyAfter, after.RunePools["P2"].SpellOnlyMana);
        Assert.Equal(JsonSerializer.Serialize(before.RunePools["P1"], Json), JsonSerializer.Serialize(after.RunePools["P1"], Json));
        Assert.False(before.CardObjects["RESOURCE"].IsExhausted);
        Assert.Contains("RESOURCE", before.PlayerZones["P2"].Base);
        if (removed)
        {
            Assert.False(after.CardObjects.ContainsKey("RESOURCE"));
            Assert.False(after.ObjectLocations.ContainsKey("RESOURCE"));
            Assert.DoesNotContain("RESOURCE", after.PlayerZones["P2"].Base);
            Assert.DoesNotContain("RESOURCE", after.PlayerZones["P2"].Graveyard);
        }
        else
        {
            Assert.True(after.CardObjects["RESOURCE"].IsExhausted);
            Assert.Contains("RESOURCE", after.PlayerZones["P2"].Base);
        }
        Assert.Equal(JsonSerializer.Serialize(before.CardObjects["D"], Json), JsonSerializer.Serialize(after.CardObjects["D"], Json));
        Assert.Equal(JsonSerializer.Serialize(before.ObjectLocations["D"], Json), JsonSerializer.Serialize(after.ObjectLocations["D"], Json));
        Assert.Equal(before.PlayerZones["P2"].Graveyard, after.PlayerZones["P2"].Graveyard);
        var events = step.GetProperty("events").EnumerateArray().ToArray();
        Assert.Single(events, value => value.GetProperty("kind").GetString() == "ABILITY_ACTIVATED");
        Assert.DoesNotContain(events, value => value.GetProperty("kind").GetString() is "COST_PAID" or "DAMAGE_APPLIED" or "UNIT_RECALLED_TO_BASE");
        if (removed) Assert.Single(events, value => value.GetProperty("kind").GetString() == "TOKEN_CEASED_TO_EXIST");
        Assert.Equal(original, JsonSerializer.Serialize(generated.State, Json));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            var folder = Path.Combine(output, "producer-skill-" + cardNo.Replace('/', '-') + "-" + levelSix); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "proof.json"), JsonSerializer.Serialize(new {
                cardNo, levelSix, costs, initial, generated.State, step,
                scope = "Scoped resource-child costs, restrictions, deletion and event boundaries only; no consumer/surrender/ordinary-room/SQL/API/L3 claim.",
                ruleInputs = RuleInputIdentity.Current }, Json));
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
