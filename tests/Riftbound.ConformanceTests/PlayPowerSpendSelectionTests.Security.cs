using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class PlayPowerSpendSelectionTests
{
    public static IEnumerable<object[]> MalformedSpendChoices()
        => new[] { "", "A:0", "A:-1", "A:-0", "A:+1", "A:01", "A:2147483648", "A:1e0", "A:1.0",
            "A: 1", " A:1", "A:１", "A:١", "A:1,", ",A:1", "A:1,A:1", "purple:1,purple:1",
            "any:1", "rainbow:1", "Purple:1", "紫色:1", "black:1", "A", "A:1:1",
            "red:1,purple:1", "A:2147483647,purple:1", "A:1,purple:1", "A:2147483647",
            "A:1,blue:1,green:1,orange:1,purple:1,red:1,yellow:1,black:1", new string('9', 400), "DUPLICATE_SELECTOR" }
            .Select(value => new object[] { "POWER_SPEND:" + value });

    [Theory]
    [MemberData(nameof(MalformedSpendChoices))]
    public async Task MalformedUnfundedOrMismatchedSelectorsRejectBeforeAnyMutation(string selector)
    {
        var initial = Hand("invalid-spend-" + Array.IndexOf(MalformedSpendChoices().Select(x => (string)x[0]).ToArray(), selector),
            "SFD·143/221", new(4, 1, Colors(("purple", 1))));
        var run = new Run(initial);
        var choices = selector.EndsWith("DUPLICATE_SELECTOR", StringComparison.Ordinal) ? new[] { "POWER_SPEND:A:1", "POWER_SPEND:A:1" } : [selector];
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: choices);
        var quote = await run.Quote("P1", command); var result = await run.Submit("P1", command);
        Assert.False(quote.IsValid); Assert.False(quote.CanPay); Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
        AssertRejected(initial, result);
        var last = run.Entries[^1]; var count = run.Entries.Count;
        Assert.False(last.Accepted); Assert.Equal(last.StartedTick, last.CompletedTick); Assert.Equal(last.StartedEventSequence, last.CompletedEventSequence);
        var warm = await run.Repeat(last.PlayerId, last.ClientIntentId, last.RawCommand!.Value);
        Assert.Equal(result.ErrorCode, warm.ErrorCode); Assert.Equal(count, run.Entries.Count);
        await run.Cold("invalid-selector");
        Assert.Equal(result.ErrorCode, run.Entries[^1].ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(run.State));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ASelectedSourceCannotBorrowFromAnotherFundedBucket(bool wildcard)
    {
        var initial = Hand("borrow-" + wildcard, "SFD·143/221", wildcard ? new(4, 0, Colors(("purple", 1))) : new(4, 1));
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [wildcard ? "POWER_SPEND:A:1" : "POWER_SPEND:purple:1"]);
        Assert.True((await run.Quote("P1", command with { OptionalCosts = [] })).CanPay);
        var result = await run.Submit("P1", command); AssertRejected(initial, result);
        await run.Cold("borrow-rejected");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ZeroPowerFeeKeepsTheUniqueAbsentSelectorAndRejectsPositiveSpending(bool selected)
    {
        var initial = Hand("zero-fee-" + selected, "SFD·029/221", new(3, 1)); var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "SFD·029/221", [], OptionalCosts: selected ? ["POWER_SPEND:A:1"] : []);
        var quote = await run.Quote("P1", command); var result = await run.Submit("P1", command);
        Assert.Equal(!selected, quote.CanPay); Assert.Equal(!selected, result.Accepted);
        if (selected) AssertRejected(initial, result);
        else { Assert.Equal(new RunePool(0, 1), run.State.RunePools["P1"]); AssertFee(result, quote, 0); }
        await run.Cold("zero-final");
    }

    [Fact]
    public async Task MaximumSupportedWalletRetainsExactSelectedRemainders()
    {
        var run = new Run(Hand("maximum-spend", "SFD·143/221", new(4, int.MaxValue - 1, Colors(("purple", 1)))));
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: ["POWER_SPEND:A:1"]);
        var quote = await run.Quote("P1", command); Assert.True(quote.CanPay, quote.Message);
        var result = await run.Submit("P1", command); Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, int.MaxValue - 2, Colors(("purple", 1))), run.State.RunePools["P1"]);
        AssertQuoteRemainder(quote, run.State.RunePools["P1"]);
        Assert.Equal("POWER_SPEND:A:1", run.Entries[^1].RawCommand!.Value.GetProperty("optionalCosts")[0].GetString());
        await run.Cold("maximum-final");
    }

    [Theory]
    [InlineData(" POWER_SPEND:A:1")] [InlineData("POWER_SPEND:A:1 ")]
    public async Task ExistingWireCanonicalityAlsoRejectsOuterWhitespaceWithoutAJournalWrite(string selector)
    {
        var initial = Hand("outer-space-" + (selector[0] == ' '), "SFD·143/221", new(4, 1, Colors(("purple", 1))));
        var run = new Run(initial); var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [selector]);
        var quote = await run.Quote("P1", command); Assert.False(quote.CanPay); Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
        var result = await run.Submit("P1", command);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode); Assert.Empty(result.Events); Assert.Empty(run.Entries);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(run.State));
    }

    [Fact]
    public async Task OriginalIntentBindsTheExactRawSelectorAcrossWarmAndColdRetries()
    {
        var run = await KnownCase("typed", "retry-spend");
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: ["POWER_SPEND:A:1"]);
        var accepted = await run.Submit("P1", command); Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var last = run.Entries[^1]; var count = run.Entries.Count; var state = MatchStateHasher.Hash(run.State);
        var warm = await run.Repeat("P1", last.ClientIntentId, last.RawCommand!.Value);
        Assert.True(warm.Accepted); Assert.Equal(Hash(accepted.Events), Hash(warm.Events)); Assert.Equal(count, run.Entries.Count);
        Assert.Equal(state, MatchStateHasher.Hash(run.State));
        var changed = JsonSerializer.SerializeToNode(last.RawCommand.Value, Web)!.AsObject();
        changed["optionalCosts"] = JsonSerializer.SerializeToNode(new[] { "POWER_SPEND:purple:1" }, Web);
        var conflict = await run.Repeat("P1", last.ClientIntentId, JsonSerializer.SerializeToElement(changed));
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode); Assert.Empty(conflict.Events);
        Assert.Equal(state, MatchStateHasher.Hash(run.State)); Assert.Equal(count, run.Entries.Count);
        await run.Cold("retry-final");
        Assert.Equal(new RunePool(0, 0, Colors(("purple", 1))), run.State.RunePools["P1"]);
    }

    [Fact]
    public async Task AReadOnlyQuoteDoesNotAuthorizeAnOldEnvelopeOrFreezeTheWallet()
    {
        var initial = WithBase(Hand("stale-spend", "SFD·143/221", new(4, 1, Colors(("purple", 1)))), "P1",
            OfficialGarenConquestBattlefieldCountTests.Printed("RUNE", "OGN·007/298", "P1"));
        var run = new Run(initial);
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: ["POWER_SPEND:A:1"]);
        var oldQuote = await run.Quote("P1", command); var oldWire = run.Wire("P1", command);
        await run.Act("P1", new RecycleRuneCommand("RUNE"));
        var before = MatchStateHasher.Hash(run.State);
        var stale = await run.Repeat("P1", "stale-quote-envelope", oldWire);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Empty(stale.Events); Assert.Equal(before, MatchStateHasher.Hash(run.State));
        var freshQuote = await run.Quote("P1", command); var accepted = await run.Submit("P1", command);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.False(oldQuote.Cost!.RemainingPowerByTrait!.ContainsKey("red"));
        Assert.Equal(new RunePool(0, 0, Colors(("purple", 1), ("red", 1))), run.State.RunePools["P1"]);
        AssertQuoteRemainder(freshQuote, run.State.RunePools["P1"]);
        Assert.Single(run.Entries.SelectMany(e => e.Events), e => e.Kind == "RUNE_RECYCLED");
        await run.Cold("stale-final");
    }

    [Fact]
    public void SourceRequirementsExposeTheBoundedActualSpendGrammar()
    {
        var state = Hand("metadata-spend", "SFD·143/221", new(4, 1, Colors(("purple", 1))));
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements);
        var meta = JsonSerializer.SerializeToElement(requirement["powerSpendSelection"], Web);
        Assert.Equal("POWER_SPEND:", meta.GetProperty("choicePrefix").GetString()); Assert.Equal("A", meta.GetProperty("wildcardKey").GetString());
        Assert.Equal(new[] { "blue", "green", "orange", "purple", "red", "yellow" }, meta.GetProperty("typedKeys").Deserialize<string[]>(Web));
        Assert.Equal(7, meta.GetProperty("maximumComponents").GetInt32()); Assert.Equal(int.MaxValue, meta.GetProperty("maximumTotal").GetInt32());
        Assert.True(meta.GetProperty("ordinalKeys").GetBoolean()); Assert.True(meta.GetProperty("exactTotal").GetBoolean());
        Assert.True(meta.GetProperty("positivePowerCostOnly").GetBoolean()); Assert.True(meta.GetProperty("usePreviewAvailableWallet").GetBoolean());
    }

    [Fact]
    public void SevenBucketFeasibilityMatchesAnIndependentResourceAssignmentSearch()
    {
        var traits = new[] { "blue", "green", "orange", "purple", "red", "yellow" };
        var pool = new RunePool(0, 1, traits.ToDictionary(t => t, _ => 1));
        var compared = 0; var legal = 0;
        for (var mask = 1; mask < 128; mask++)
        {
            var selected = traits.Select((trait, n) => (trait, amount: (mask >> (n + 1)) & 1)).Where(p => p.amount > 0).ToDictionary(p => p.trait, p => p.amount);
            var wild = mask & 1;
            var parsed = ParseSpend(Spend(wild, selected));
            for (var fees = 0; fees < 64; fees++)
            for (var generic = 0; generic < 3; generic++)
            {
                var typed = traits.Select((trait, n) => (trait, amount: (fees >> n) & 1)).Where(p => p.amount > 0).ToDictionary(p => p.trait, p => p.amount);
                var actual = CanSpend(parsed, pool, generic, typed);
                var resources = (wild == 1 ? new[] { "A" } : []).Concat(selected.Keys).ToArray();
                var requirements = typed.Keys.Concat(Enumerable.Repeat("*", generic)).ToArray();
                var expected = resources.Length == requirements.Length && Assign(0, 0);
                Assert.True(expected == actual, $"walletMask={mask},feeMask={fees},generic={generic},expected={expected},actual={actual}");
                compared++; if (actual) legal++;
                bool Assign(int next, int used)
                {
                    if (next == requirements.Length) return true;
                    for (var i = 0; i < resources.Length; i++)
                        if ((used & (1 << i)) == 0 && (requirements[next] == "*" || resources[i] == "A" || resources[i] == requirements[next])
                            && Assign(next + 1, used | (1 << i))) return true;
                    return false;
                }
            }
        }
        Assert.Equal(24384, compared); Assert.True(legal > 0);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_PLAY_POWER_SPEND_EVIDENCE") is { Length: > 0 } output)
        { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "seven-bucket-oracle.json"), JsonSerializer.Serialize(new { compared, legal, scope = "all seven buckets with 0/1 counts, six typed requirement masks, generic0..2; not all integers" }, Web)); }
    }

    [Fact]
    public void DebitPlanPreservesSpellOnlyManaExperienceAndTheOriginalFeeReceipt()
    {
        var fee = new PaymentCostRules.PaymentPlan("EXACT", "PLAY_CARD", "P1", baseManaCost: 4, totalManaCost: 2,
            genericPowerCost: 1, totalPowerCost: 2, powerCostByTrait: Colors(("red", 1)), experienceCost: 3,
            optionalCostIds: ["ORIGINAL_FEE"], paymentResourceActionIds: ["LUX_SPELL_ONLY_RESOURCE:LUX"],
            reason: "SOURCE_REASON", sourceObjectId: "CARD", auditMetadata: new Dictionary<string, object?> { ["sourceAudit"] = "unchanged" },
            manaPurpose: ManaPaymentPurpose.PlaySpell);
        var selection = ParseSpend("POWER_SPEND:A:1,purple:1");
        var available = new RunePool(0, 1, Colors(("purple", 1))) with { SpellOnlyMana = 2 };
        Assert.True(CanSpend(selection, available, 1, Colors(("red", 1))));
        var debit = Assert.IsType<PaymentCostRules.PaymentPlan>(SpendRules.GetMethod("DebitPlan", StaticMembers)!.Invoke(null, [fee, selection]));
        Assert.Equal(ManaPaymentPurpose.PlaySpell, debit.ManaPurpose); Assert.Equal(4, debit.BaseManaCost); Assert.Equal(2, debit.TotalManaCost); Assert.Equal(3, debit.ExperienceCost);
        Assert.Equal(new[] { "ORIGINAL_FEE" }, debit.OptionalCostIds); Assert.Equal(new[] { "LUX_SPELL_ONLY_RESOURCE:LUX" }, debit.PaymentResourceActionIds);
        Assert.Equal("SOURCE_REASON", debit.Reason); Assert.Equal("CARD", debit.SourceObjectId); Assert.Equal("unchanged", debit.AuditMetadata["sourceAudit"]);
        var paid = PaymentCostRules.TryCommitPayment(debit, new Dictionary<string, RunePool> { ["P1"] = available }, new Dictionary<string, int> { ["P1"] = 3 });
        Assert.True(paid.Accepted); Assert.Equal(RunePool.Empty, paid.RunePools["P1"]); Assert.Equal(0, paid.PlayerExperience["P1"]);
        var receipt = PaymentCostRules.BuildCostPaidPayload(fee, paid.RunePools, paid.PlayerExperience, new Dictionary<string, object?>());
        Assert.Equal(1, receipt["genericPower"]); Assert.Equal(Colors(("red", 1)), receipt["powerByTrait"]);
        var maximum = ParseSpend("POWER_SPEND:A:2147483647");
        Assert.True(CanSpend(maximum, new(0, int.MaxValue), int.MaxValue, Colors()));
        Assert.False(CanSpend(maximum, new(0, int.MaxValue), int.MaxValue, Colors(("red", 1))));
    }

    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type SpendRules => typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PlayPowerSpendSelectionRules", throwOnError: true)!;
    private static object ParseSpend(string choice)
    {
        object?[] args = [new[] { choice }, null, null];
        Assert.True((bool)SpendRules.GetMethod("TryExtract", StaticMembers)!.Invoke(null, args)!);
        return Assert.IsAssignableFrom<object>(args[2]);
    }
    private static bool CanSpend(object selected, RunePool pool, int generic, IReadOnlyDictionary<string, int> typed)
        => (bool)SpendRules.GetMethod("CanPay", StaticMembers)!.Invoke(null, [selected, pool, generic, typed])!;
}
