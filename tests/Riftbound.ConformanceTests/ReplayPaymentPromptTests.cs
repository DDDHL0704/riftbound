using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReplayPaymentPromptTests
{
    private const string Rune = "REPLAY-PAYMENT-RUNE";
    private const string Recycle = "RECYCLE_RUNE:" + Rune;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlternativeBaseCostOffersTheRealInlineRunePayment(bool observed)
    {
        var initial = observed ? ObservedPosition(0, true, false) : GrenadePosition(0, true, false);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Open(driver, observed);
        var player = observed ? "P1" : "P2";
        var source = observed ? "NOCT" : "GRENADE";
        var card = observed ? DeckObservationPlayTests.Nocturne : DancingGrenadeTests.Card;
        var requirement = Requirement(driver.State, player, source);
        var command = new PlayCardCommand(source, card, observed ? [] : ["HOST"], OptionalCosts: [Recycle]);
        var quote = await Quote(driver.State, player, command);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower);
        var paid = await driver.Act(player, command);
        Assert.Equal(1, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["power"]);
        Assert.Contains(Rune, driver.State.PlayerZones[player].RuneDeck);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("inline-" + observed, requirement, quote, driver);
        Assert.Contains(Recycle, Choices(requirement, "paymentResourceChoices"));
        Assert.Contains(Recycle, Choices(requirement, "optionalCostChoices"));
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, true)]
    [InlineData(1, true, true)]
    public async Task GrantedEchoRequiresItsPowerTogetherWithTheAlternativeBaseCost(int power, bool rune, bool payable)
    {
        var initial = GrenadePosition(power, rune, true);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Open(driver, false);
        var requirement = Requirement(driver.State, "P2", "GRENADE");
        var command = new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["HOST"],
            OptionalCosts: rune ? ["ECHO", Recycle] : ["ECHO"], RepeatChoices: [new("", ["HOST"])]);
        var quote = await Quote(driver.State, "P2", command);
        Assert.Equal(payable, quote.CanPay);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        if (!payable) await AssertUnpayable(driver.State, "P2", command);
        var paid = await driver.Act("P2", payable ? command : new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["HOST"]));
        Assert.Equal(payable ? 2 : 1, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["power"]);
        Assert.Equal(payable ? 2 : 1, Assert.Single(driver.State.StackItems).EffectRepeatCount);
        if (rune) Assert.Contains(Rune, driver.State.PlayerZones["P2"].RuneDeck);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export($"echo-{power}-{rune}", requirement, quote, driver);
        Assert.Equal(payable, Choices(requirement, "optionalCostChoices").Contains("ECHO"));
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, true)]
    [InlineData(1, true, true)]
    public async Task ObservedHasteRequiresItsPowerTogetherWithTheAlternativeBaseCost(int power, bool rune, bool payable)
    {
        var initial = ObservedPosition(power, rune, true);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Open(driver, true);
        var requirement = Requirement(driver.State, "P1", "NOCT");
        var command = new PlayCardCommand("NOCT", DeckObservationPlayTests.Nocturne, [],
            OptionalCosts: rune ? [HasteOptionalCostNames.HasteReady, Recycle] : [HasteOptionalCostNames.HasteReady]);
        var quote = await Quote(driver.State, "P1", command);
        Assert.Equal(payable, quote.CanPay);
        Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        if (!payable) await AssertUnpayable(driver.State, "P1", command);
        var paid = await driver.Act("P1", payable ? command : new PlayCardCommand("NOCT", DeckObservationPlayTests.Nocturne, []));
        Assert.Equal(payable ? 2 : 1, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["power"]);
        Assert.Equal(!payable, driver.State.CardObjects["NOCT"].IsExhausted);
        if (rune) Assert.Contains(Rune, driver.State.PlayerZones["P1"].RuneDeck);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export($"haste-{power}-{rune}", requirement, quote, driver);
        Assert.Equal(payable, Choices(requirement, "optionalCostChoices").Contains(HasteOptionalCostNames.HasteReady));
    }

    [Fact]
    public async Task AnExplicitEchoPowerReductionKeepsTheFundedAlternativePlayAvailable()
    {
        const string reducer = "REPLAY-REDUCER";
        var initial = GrenadePosition(1, false, true);
        initial = initial with
        {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { [reducer] = OfficialGarenConquestBattlefieldCountTests.Printed(reducer, "SFD·149/221", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [reducer] = new("P2", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
            { ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, reducer] } }
        };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Open(driver, false);
        var requirement = Requirement(driver.State, "P2", "GRENADE");
        var reduction = "OPTIONAL_COST_REDUCTION:" + reducer + ":0:ECHO:POWER-red:"
            + Uri.EscapeDataString("PRINTED_POWER:red:1");
        var command = new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["HOST"],
            OptionalCosts: ["ECHO", reduction], RepeatChoices: [new("", ["HOST"])]);
        var quote = await Quote(driver.State, "P2", command);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.GenericPower);
        Assert.Empty(quote.Cost.PowerByTrait);
        var paid = await driver.Act("P2", command);
        Assert.Equal(1, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["power"]);
        Assert.Equal(2, Assert.Single(driver.State.StackItems).EffectRepeatCount);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("echo-reduced", requirement, quote, driver);
        Assert.Contains("ECHO", Choices(requirement, "optionalCostChoices"));
        Assert.Contains(reduction, Choices(requirement, "optionalCostChoices"));
    }

    private static MatchState GrenadePosition(int power, bool rune, bool echo)
    {
        var state = DancingGrenadeTests.Position();
        state = state with
        {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(2, power) },
            UntilEndOfTurnEffects = echo ? [..state.UntilEndOfTurnEffects, EchoCostRules.GrantPrefix + "P2"] : state.UntilEndOfTurnEffects
        };
        return rune ? AddRune(state, "P2") : state;
    }

    private static MatchState ObservedPosition(int power, bool rune, bool haste)
    {
        var state = DeckObservationPlayTests.Position();
        if (haste)
        {
            state = OfficialHoldSequenceTests.AddUnit(state, "REPLAY-HASTE-GRANT", "SFD·029/221", "F", "P1");
            state = LocalDestructionRecallTests.At(state, "REPLAY-HASTE-GRANT", "P1", "BASE");
        }
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, power) } };
        return rune ? AddRune(state, "P1") : state;
    }

    private static MatchState AddRune(MatchState state, string player)
        => state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { [Rune] = new(Rune, cardNo: "UNL-R03", ownerId: player, controllerId: player, tags: [CardObjectTags.RuneCard, "COLOR:blue"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [Rune] = new(player, "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            { [player] = state.PlayerZones[player] with { Base = [..state.PlayerZones[player].Base, Rune] } }
        };

    private static async Task Open(StandbyDefendObserverTests.Driver driver, bool observed)
    {
        await driver.Act("P1", observed ? new EndTurnCommand()
            : new PlayCardCommand("GRENADE", DancingGrenadeTests.Card, ["VICTIM"]));
        await driver.Top();
        if (observed) await DeckObservationPlayTests.Decide(driver, true);
        Assert.NotNull(driver.State.PendingEffectPlay);
    }

    private static JsonElement Requirement(MatchState state, string player, string source)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)[player].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return metadata.GetProperty("sourceRequirements").EnumerateArray()
            .Single(row => row.GetProperty("sourceObjectId").GetString() == source).Clone();
    }

    private static string[] Choices(JsonElement requirement, string key)
        => requirement.GetProperty(key).EnumerateArray().Select(row => row.GetProperty("id").GetString()!).ToArray();

    private static async Task<PlayCostQuoteDto> Quote(MatchState state, string player, PlayCardCommand command)
    {
        var journal = new Journal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var request = new PlayCostPreviewRequestDto("replay-payment", session.PromptFor(player).PromptId!, state.Tick, command);
        var before = MatchStateHasher.Hash(state);
        var quote = await session.PreviewPlayCardAsync(player, request, default);
        Assert.True(quote.IsValid, quote.Message);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.Equal(state.Tick, session.SnapshotFor(player).Tick);
        Assert.Empty(journal.Entries);
        return quote;
    }

    private static async Task AssertUnpayable(MatchState state, string player, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("unpayable-replay", player, CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    private static void Export(string branch, JsonElement requirement, PlayCostQuoteDto quote, StandbyDefendObserverTests.Driver driver)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_REPLAY_PAYMENT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, branch + ".json"), JsonSerializer.Serialize(new
        { requirement, quote, commands = driver.Commands(), events = driver.Events(), finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
