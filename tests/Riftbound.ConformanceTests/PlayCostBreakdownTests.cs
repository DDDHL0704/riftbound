using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayCostBreakdownTests
{
    [Theory]
    [InlineData("OGN·057/298")]
    [InlineData("OGN·121/298")]
    public async Task ActualStandbyPlayExplainsBothWaivedBaseResources(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(StandbySourceRulesTests.Position(card), "REPLY", "OGN·048/298", "P2");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new HideCardCommand("SHADOW", card, "BATTLEFIELD:F", ["STANDBY_A"]));
        var ended = await driver.Act("P1", new EndTurnCommand());
        await TurnSequenceTestDriver.Complete(ended, async (player, command) => await driver.Act(player, command));
        foreach (var id in new[] { "SHADOW-P2-RUNE-1", "SHADOW-P2-RUNE-2", "SHADOW-P2-RUNE-3" })
            await driver.Act("P2", new TapRuneCommand(id));
        foreach (var id in new[] { "SHADOW-P2-RUNE-4", "SHADOW-P2-RUNE-5" })
            await driver.Act("P2", new RecycleRuneCommand(id));
        await driver.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await driver.Act("P2", new PassPriorityCommand());
        Assert.True(driver.State.CardObjects["SHADOW"].IsFaceDown);
        var play = new PlayCardCommand("SHADOW", card, card == "OGN·057/298" ? ["HOST"] : []);
        var quote = await Quote(driver.State, "P1", play);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var before = driver.State;
        var paid = await driver.Act("P1", play);
        AssertPayment(quote, before, paid, "P1");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("standby-" + card.Replace('·', '-').Replace('/', '-'), quote, driver);
        AssertBalanced(quote.Cost);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(2, 3, true)]
    public async Task ActualSourceReplayExplainsItsAlternativeCostAndEveryWardFee(int ward, int power, bool payable)
    {
        var initial = DancingGrenadeTests.Position();
        initial = initial with
        {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(0, power) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                ["HOST"] = initial.CardObjects["HOST"] with
                { Tags = ward == 0 ? initial.CardObjects["HOST"].Tags : [..initial.CardObjects["HOST"].Tags, "法盾2"] }
            }
        };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["VICTIM"]));
        await driver.Top();
        Assert.NotNull(driver.State.PendingEffectPlay!.SourceReplay);
        var play = new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["HOST"]);
        var before = driver.State;
        var quote = await Quote(before, "P2", play);
        Assert.Equal(payable, quote.CanPay);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(1 + ward, quote.Cost.GenericPower);
        Assert.Equal(Math.Max(0, 1 + ward - power), quote.Cost.MissingPower);
        if (payable)
        {
            var paid = await driver.Act("P2", play);
            AssertPayment(quote, before, paid, "P2");
            await driver.Top();
        }
        else
        {
            var refused = await new CoreRuleEngine().ResolveAsync(before,
                new("unaffordable-breakdown", "P2", CommandTypes.PlayCard), play, default);
            Assert.False(refused.Accepted);
            Assert.Equal(ErrorCodes.InsufficientCost, refused.ErrorCode);
            Assert.Empty(refused.Events);
            Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(refused.State));
        }
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        await driver.Act(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export($"source-replay-{ward}-{power}", quote, driver);
        AssertBalanced(quote.Cost);
        AssertAlternativeBaseCost(quote.Cost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualObservedPlayDistinguishesIgnoredCostsFromTheAlternativePower(bool banish)
    {
        var initial = DeckObservationPlayTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new EndTurnCommand());
        await driver.Top();
        await DeckObservationPlayTests.Decide(driver, banish);
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        var play = new PlayCardCommand("NOCT", DeckObservationPlayTests.Nocturne, []);
        var before = driver.State;
        var quote = await Quote(before, pending.PlayerId, play);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(banish ? 1 : 0, quote.Cost.GenericPower);
        var paid = await driver.Act(pending.PlayerId, play);
        AssertPayment(quote, before, paid, pending.PlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("observed-" + banish, quote, driver);
        AssertBalanced(quote.Cost);
        if (banish) AssertAlternativeBaseCost(quote.Cost);
    }

    [Theory]
    [InlineData(false, 4, 1)]
    [InlineData(true, 5, 2)]
    public async Task OrdinaryPlayStillExplainsPrintedAndOptionalCosts(bool haste, int mana, int power)
    {
        var initial = PlayCostPreviewTests.Position(new(8, 4)) with { Tick = 0 };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var play = PlayCostPreviewTests.Command(haste ? [HasteOptionalCostNames.HasteReady] : []);
        var quote = await Quote(initial, "P1", play);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.PowerByTrait["purple"]);
        var paid = await driver.Act("P1", play);
        AssertPayment(quote, initial, paid, "P1");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("ordinary-" + haste, quote, driver);
        AssertBalanced(quote.Cost);
    }

    private static async Task<PlayCostQuoteDto> Quote(MatchState state, string player, PlayCardCommand play)
    {
        var journal = new Journal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var prompt = session.PromptFor(player);
        var request = new PlayCostPreviewRequestDto("breakdown", prompt.PromptId!, state.Tick, play);
        var hash = MatchStateHasher.Hash(state);
        var first = await session.PreviewPlayCardAsync(player, request, default);
        var second = await session.PreviewPlayCardAsync(player, request, default);
        Assert.True(first.IsValid, first.Message);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(hash, MatchStateHasher.Hash(state));
        Assert.Equal(state.Tick, session.SnapshotFor(player).Tick);
        Assert.Equal(MatchStateHasher.HashValue(prompt), MatchStateHasher.HashValue(session.PromptFor(player)));
        Assert.Empty(journal.Entries);
        return first;
    }

    private static void AssertPayment(PlayCostQuoteDto quote, MatchState before, ResolutionResult paid, string player)
    {
        var cost = Assert.Single(paid.Events, item => item.Kind == "COST_PAID");
        Assert.Equal(quote.Cost!.Mana, cost.Payload["mana"]);
        Assert.Equal(quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum(), cost.Payload["power"]);
        Assert.Equal(before.RunePools[player].Mana - quote.Cost.Mana, quote.Cost.RemainingMana);
        Assert.Equal(before.RunePools[player].TotalPower - quote.Cost.GenericPower - quote.Cost.PowerByTrait.Values.Sum(),
            quote.Cost.RemainingRainbowPower + quote.Cost.RemainingPowerByTrait!.Values.Sum());
        if (before.TurnNumber == paid.State.TurnNumber)
        {
            Assert.Equal(quote.Cost.RemainingMana, paid.State.RunePools[player].Mana);
            Assert.Equal(quote.Cost.RemainingRainbowPower + quote.Cost.RemainingPowerByTrait.Values.Sum(),
                paid.State.RunePools[player].TotalPower);
        }
        else
        {
            // Completing Aurora's suspended end step starts the opponent's turn and empties the old pool.
            Assert.Equal(before.TurnNumber + 1, paid.State.TurnNumber);
            Assert.Equal(RunePool.Empty, paid.State.RunePools[player]);
        }
    }

    private static void AssertBalanced(PlayCostBreakdownDto cost)
    {
        Assert.Equal(cost.Mana, cost.PrintedMana + cost.Adjustments.Sum(item => item.Mana));
        Assert.Equal(cost.GenericPower + cost.PowerByTrait.Values.Sum(),
            cost.PrintedPower + cost.Adjustments.Sum(item => item.Power));
    }

    private static void AssertAlternativeBaseCost(PlayCostBreakdownDto cost)
    {
        Assert.True(cost.PrintedPower > 0);
        Assert.Contains(cost.Adjustments, item => item.Power == -cost.PrintedPower);
        Assert.Contains(cost.Adjustments, item => item.Power == 1);
    }

    private static void Export(string branch, PlayCostQuoteDto quote, StandbyDefendObserverTests.Driver driver)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_PREVIEW_BREAKDOWN_EVIDENCE");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, branch + ".json"), JsonSerializer.Serialize(new
        { quote, commands = driver.Commands(), events = driver.Events(), finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
