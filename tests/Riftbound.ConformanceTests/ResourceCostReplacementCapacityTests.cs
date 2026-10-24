using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

public sealed class ResourceCostReplacementCapacityTests
{
    [Theory]
    [InlineData("pay1")]
    [InlineData("convert2")]
    public async Task ADestructionCostCanPauseAndFreeCapacityBeforeTheExactGain(string branch)
    {
        var initial = Position(branch); var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        Assert.Contains(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility && (c.Sources ?? []).Any(s => s.Id == "MALZ"));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", BoundedResourceQuantityTests.Native(new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["A"])));
        Assert.Equal("DESTRUCTION_REPLACEMENT", driver.State.PendingRuleChoice!.Request.Kind);
        Assert.Equal(initial.RunePools["P1"], driver.State.RunePools["P1"]);
        Assert.False(driver.State.CardObjects["MALZ"].IsExhausted);
        if (branch == "convert2")
        {
            var resource = driver.State.PendingRuleChoice.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:CONVERTER:", StringComparison.Ordinal)
                && o.Id.EndsWith(P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "2", StringComparison.Ordinal));
            await Decide(driver, resource.Id);
            Assert.Equal(initial.RunePools["P1"], driver.State.RunePools["P1"]);
            // This existing destruction window exposes its working balance in
            // the reason, without a full Preview board. The origin stays frozen.
            Assert.Contains("2147483647 法力", driver.State.PendingRuleChoice!.Request.Reason);
            Assert.Contains("2147483645 符能", driver.State.PendingRuleChoice.Request.Reason);
            Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id.StartsWith("RESOURCE:CONVERTER:", StringComparison.Ordinal));
        }
        await Decide(driver, branch == "pay1" ? "SETT:A:LEGEND:ANY" : "DECLINE");
        Assert.Null(driver.State.PendingRuleChoice); Assert.Equal(int.MaxValue, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(branch == "convert2" ? int.MaxValue : 0, driver.State.RunePools["P1"].Mana);
        Assert.True(driver.State.CardObjects["MALZ"].IsExhausted);
        Assert.Equal(branch == "pay1", driver.State.PlayerZones["P1"].Base.Contains("A"));
        Assert.Equal(branch == "convert2", driver.State.PlayerZones["P1"].Graveyard.Contains("A"));
        Assert.Equal(branch == "pay1" ? 1 : 0, driver.Events().Count(e => e.Event.Kind == "COST_PAID"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OneAutomaticReplacementDoesNotMakeAnUnsupportedImmediateGainExecutable()
    {
        var initial = Position("pay1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["LEGEND"] = initial.CardObjects["LEGEND"] with { IsExhausted = true },
            ["A"] = initial.CardObjects["A"] with { UntilEndOfTurnEffects = ["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"] } } };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.ActivateAbility && (c.Sources ?? []).Any(s => s.Id == "MALZ"));
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("automatic-capacity", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["A"]), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public void RepeatedCapacityPromptsPreserveTheBoardBothViewsAndAnExistingRuleChoiceFrame()
    {
        var initial = Position("pay1"); var stateHash = MatchStateHasher.Hash(initial);
        var snapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(initial));
        var promptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(initial));
        var engineType = typeof(CoreRuleEngine);
        var slot = engineType.GetField("RuleChoices", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var value = slot.GetType().GetProperty("Value")!; var previous = value.GetValue(slot);
        var frameType = engineType.GetNestedType("RuleChoiceFrame", BindingFlags.NonPublic)!;
        var frame = frameType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().Length == 2).Invoke([initial, Array.Empty<RuleChoiceAnswer>()]);
        frameType.GetProperty("CompletionTick")!.SetValue(frame, initial.Tick + 500);
        frameType.GetProperty("AppliedDestructionReplacements")!.SetValue(frame, new HashSet<string> { "UNCHANGED" });
        var cursor = frameType.GetField("cursor", BindingFlags.Instance | BindingFlags.NonPublic)!; cursor.SetValue(frame, 7);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var beforeFrame = JsonSerializer.Serialize(frame, frameType, web);
        try
        {
            value.SetValue(slot, frame);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(promptsHash, MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(initial)));
                Assert.Equal(snapshotsHash, MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(initial)));
                Assert.Equal(stateHash, MatchStateHasher.Hash(initial)); Assert.Same(frame, value.GetValue(slot));
                Assert.Equal(beforeFrame, JsonSerializer.Serialize(frame, frameType, web)); Assert.Equal(7, cursor.GetValue(frame));
            }
        }
        finally { value.SetValue(slot, previous); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFullPowerPoolCannotStartACostWhoseOnlyReplacementSpendsTooLittle(bool typed)
    {
        var initial = Position("pay1");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = typed ? new(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = int.MaxValue }) : new(0, int.MaxValue) } };
        var before = MatchStateHasher.Hash(initial);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var opened = await new CoreRuleEngine().ResolveAsync(initial,
            new("no-room-start", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["A"]), default);
        var attempts = new List<object>();
        var queue = new Queue<MatchState>();
        if (opened.Accepted && opened.State.PendingRuleChoice is not null) queue.Enqueue(opened.State);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0 && visited.Count < 16)
        {
            var state = queue.Dequeue();
            if (!visited.Add(MatchStateHasher.Hash(state))) continue;
            var request = state.PendingRuleChoice!.Request;
            foreach (var option in request.Options)
            {
                if (option.Id != "DECLINE" && !option.Id.StartsWith("SETT:", StringComparison.Ordinal)
                    && !option.Id.StartsWith("TAP_RUNE:", StringComparison.Ordinal)) continue;
                try
                {
                    var next = await new CoreRuleEngine().ResolveAsync(state,
                        new("no-room-attempt-" + attempts.Count, request.PlayerId, CommandTypes.PayCost),
                        new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]), default);
                    attempts.Add(new { option.Id, options = request.Options, next.Accepted, next.ErrorCode,
                        events = next.Events, stillPending = next.State.PendingRuleChoice is not null });
                    if (next.Accepted && next.State.PendingRuleChoice is not null
                        && option.Id.StartsWith("TAP_RUNE:", StringComparison.Ordinal)) queue.Enqueue(next.State);
                }
                catch (Exception exception)
                {
                    attempts.Add(new { option.Id, options = request.Options, exception = exception.ToString() });
                }
            }
        }
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RESOURCE_COST_DEAD_END_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "full-" + (typed ? "typed" : "wild") + ".json"),
                JsonSerializer.Serialize(new { initial, prompt, opened, attempts, explored = visited.Count,
                    unfinished = queue.Count }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility
            && (c.Sources ?? []).Any(source => source.Id == "MALZ"));
        Assert.False(opened.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, opened.ErrorCode);
        Assert.Empty(opened.Events);
        Assert.Null(opened.State.PendingRuleChoice);
        Assert.Equal(before, MatchStateHasher.Hash(opened.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    [Theory]
    [InlineData("zero", false)] [InlineData("zero", true)]
    [InlineData("restricted-zero", false)] [InlineData("restricted-zero", true)]
    [InlineData("mandatory", false)] [InlineData("mandatory", true)]
    public async Task NeitherZeroConversionNorUnpaidReplacementChoicesCanSupplyTheMissingCapacity(string scenario, bool typed)
    {
        var initial = Position(scenario == "mandatory" ? "pay1" : "convert2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(scenario == "zero" ? int.MaxValue : 0, typed ? 0 : int.MaxValue,
                typed ? new Dictionary<string, int> { [RuneTrait.Red] = int.MaxValue } : null,
                spellOnlyMana: scenario == "restricted-zero" ? int.MaxValue : 0) } };
        if (scenario == "mandatory")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = initial.CardObjects["LEGEND"] with { IsExhausted = true },
                ["A"] = initial.CardObjects["A"] with { UntilEndOfTurnEffects = [
                    "RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN", "BANISH_IF_DESTROYED_THIS_TURN"] } } };
        var before = MatchStateHasher.Hash(initial);
        if (scenario != "mandatory")
        {
            var zero = await new CoreRuleEngine().ResolveAsync(initial, new("zero-still-legal", "P1", CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("CONVERTER", P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, [],
                    [P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "0"]), default);
            Assert.True(zero.Accepted, zero.ErrorMessage);
            Assert.Equal(initial.RunePools["P1"], zero.State.RunePools["P1"]);
            Assert.True(zero.State.CardObjects["CONVERTER"].IsExhausted);
        }
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility
            && (c.Sources ?? []).Any(source => source.Id == "MALZ"));
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("no-capacity-route", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["A"]), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ExistingPowerPaymentAndRealConversionCanTogetherProvideExactlyEnoughRoom(bool typed, bool convertOne)
    {
        var initial = Position(convertOne ? "convert2" : "pay1");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(convertOne ? int.MaxValue - 1 : 0, typed ? 0 : int.MaxValue - (convertOne ? 0 : 1),
                typed ? new Dictionary<string, int> { [RuneTrait.Red] = int.MaxValue - (convertOne ? 0 : 1) } : null) } };
        Assert.Contains(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility
            && (c.Sources ?? []).Any(source => source.Id == "MALZ"));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", BoundedResourceQuantityTests.Native(new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["A"])));
        if (convertOne)
        {
            var choice = driver.State.PendingRuleChoice!.Request.Options.Single(option => option.Id.StartsWith("RESOURCE:CONVERTER:", StringComparison.Ordinal)
                && option.Id.EndsWith(P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "1", StringComparison.Ordinal));
            await Decide(driver, choice.Id);
            Assert.Equal(initial.RunePools["P1"], driver.State.RunePools["P1"]);
        }
        await Decide(driver, typed ? "SETT:A:LEGEND:TRAIT:red" : "SETT:A:LEGEND:ANY");
        var pool = driver.State.RunePools["P1"];
        Assert.True(pool.HasSupportedAmounts); Assert.Equal(int.MaxValue, pool.TotalPower);
        Assert.Equal(convertOne ? int.MaxValue : 0, pool.Mana);
        Assert.Equal(typed ? 2 : int.MaxValue, pool.Power);
        if (typed) Assert.Equal(int.MaxValue - 2, pool.PowerByTrait[RuneTrait.Red]);
        Assert.Null(driver.State.PendingRuleChoice); Assert.True(driver.State.CardObjects["MALZ"].IsExhausted);
        Assert.True(driver.State.CardObjects["LEGEND"].IsExhausted); Assert.Contains("A", driver.State.PlayerZones["P1"].Base);
        Assert.Single(driver.Events(), entry => entry.Event.Kind == "COST_PAID");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState Position(string branch)
    {
        var state = AddBase(WithPool(ImposedMovementContinuationTests.Position("standard"),
            new(branch == "convert2" ? int.MaxValue - 2 : 0, branch == "convert2" ? int.MaxValue : int.MaxValue - 1)),
            "MALZ", P4ActivatedAbilityCatalog.MalzaharCardNo);
        state = OfficialHoldSequenceTests.AddLegend(state, "OGN·269/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["A"] = state.CardObjects["A"] with { Tags = state.CardObjects["A"].Tags.Append(CardObjectTags.Boon).ToArray(), Power = state.CardObjects["A"].Power + 1 } } };
        return branch == "convert2" ? AddBase(state, "CONVERTER", P4ActivatedAbilityCatalog.HextechAnomalyCardNo) : state;
    }
}
