using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReplayPaymentPowerSpendInteractionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExplicitWalletAllocationPreservesThePrintedTraitDuringAlternativePlay(bool observed, bool extra)
    {
        var player = observed ? "P1" : "P2";
        var trait = observed ? "purple" : "red";
        var source = observed ? "NOCT" : "GRENADE";
        var card = observed ? DeckObservationPlayTests.Nocturne : DancingGrenadeTests.Card;
        var initial = observed ? DeckObservationPlayTests.Position() : DancingGrenadeTests.Position();
        if (observed && extra)
        {
            initial = OfficialHoldSequenceTests.AddUnit(initial, "REPLAY-ALLOCATION-HASTE", "SFD·029/221", "F", "P1");
            initial = LocalDestructionRecallTests.At(initial, "REPLAY-ALLOCATION-HASTE", "P1", "BASE");
        }
        initial = initial with
        {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools)
            {
                [player] = new(observed ? 20 : 2, 1,
                    new Dictionary<string, int> { ["blue"] = 1, [trait] = 1 })
            },
            UntilEndOfTurnEffects = !observed && extra
                ? [..initial.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + player]
                : initial.UntilEndOfTurnEffects
        };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", observed ? new EndTurnCommand()
            : new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["VICTIM"]));
        await driver.Top();
        if (observed) await DeckObservationPlayTests.Decide(driver, true);
        Assert.NotNull(driver.State.PendingEffectPlay);
        var declaration = extra ? observed ? HasteOptionalCostNames.HasteReady : "ECHO" : null;
        var spend = extra ? "POWER_SPEND:A:1,blue:1" : "POWER_SPEND:blue:1";
        var command = new PlayCardCommand(source, card, observed ? [] : ["HOST"],
            OptionalCosts: declaration is null ? [spend] : [declaration, spend],
            RepeatChoices: !observed && extra ? [new("", ["HOST"])] : null);
        var before = MatchStateHasher.Hash(driver.State);
        var journal = new Journal();
        var session = new MatchSession(driver.State, new CoreRuleEngine(), journal);
        var quote = await session.PreviewPlayCardAsync(player,
            new PlayCostPreviewRequestDto("alternative-explicit-wallet", session.PromptFor(player).PromptId!, driver.State.Tick, command), default);
        Assert.True(quote.IsValid, quote.Message);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.GenericPower);
        Assert.Equal(extra ? 1 : 0, quote.Cost.PowerByTrait.GetValueOrDefault(trait));
        Assert.Equal(extra ? observed ? 1 : 2 : 0, quote.Cost.Mana);
        Assert.Equal(extra ? 0 : 1, quote.Cost.RemainingRainbowPower);
        var remaining = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(quote.Cost.RemainingPowerByTrait);
        Assert.Equal(0, remaining.GetValueOrDefault("blue"));
        Assert.Equal(1, remaining.GetValueOrDefault(trait));
        Assert.Equal(before, MatchStateHasher.Hash(driver.State));
        Assert.Empty(journal.Entries);
        var paid = await driver.Act(player, command);
        var costEvent = Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(extra ? 2 : 1, costEvent.Payload["power"]);
        Assert.Equal(extra ? 0 : 1, costEvent.Payload["remainingPower"]);
        var actualRemaining = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]);
        Assert.Equal(0, actualRemaining.GetValueOrDefault("blue"));
        Assert.Equal(1, actualRemaining[trait]);
        if (observed)
        {
            // The observed permission resumes the original turn-start DRAW/MAIN
            // continuation, which clears every pool after this payment.
            Assert.True(paid.Events.ToList().FindIndex(e => e.Kind == "RUNE_POOL_CLEARED")
                > paid.Events.ToList().FindIndex(e => e.Kind == "COST_PAID"));
            Assert.Equal(0, driver.State.RunePools[player].TotalPower);
            Assert.Equal(0, driver.State.RunePools[player].Mana);
            Assert.Equal("P2", driver.State.TurnPlayerId);
            Assert.Equal(MatchPhases.Main, driver.State.Phase);
        }
        else
        {
            Assert.DoesNotContain(paid.Events, e => e.Kind == "RUNE_POOL_CLEARED");
            Assert.Equal(extra ? 0 : 1, driver.State.RunePools[player].Power);
            Assert.Equal(0, driver.State.RunePools[player].PowerByTrait.GetValueOrDefault("blue"));
            Assert.Equal(1, driver.State.RunePools[player].PowerByTrait[trait]);
        }
        if (observed) Assert.Equal(!extra, driver.State.CardObjects[source].IsExhausted);
        else Assert.Equal(extra ? 2 : 1, Assert.Single(driver.State.StackItems).EffectRepeatCount);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_REPLAY_PAYMENT_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, $"explicit-wallet-{observed}-{extra}.json"),
                JsonSerializer.Serialize(new { quote, commands = driver.Commands(), events = driver.Events(),
                    fullOriginalReplayPassed = true, finalHash = MatchStateHasher.Hash(driver.State) },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
