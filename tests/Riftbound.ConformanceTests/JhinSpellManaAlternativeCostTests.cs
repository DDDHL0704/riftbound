using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 356.1–356.4, 420.4b; Unleashed FAQ 2026-05-11, p10:
// Rocket Barrage still qualifies when Defiance counters it.
public sealed class JhinSpellManaAlternativeCostTests
{
    private const string Alternative = "SPELL_MANA_ALTERNATIVE_COST:4:1:blue";
    private const string Rocket = "SFD·077/221";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("UNL-089/219", false, true)]
    [InlineData("UNL-089a/219", false, true)]
    [InlineData("UNL-089/219", true, true)]
    [InlineData("UNL-089a/219", true, true)]
    [InlineData("UNL-089/219", false, false)]
    [InlineData("UNL-089a/219", true, false)]
    public async Task ConfirmedPaymentUnlocksAnExplicitAlternativeEvenWhenTheSpellIsCountered(
        string card, bool countered, bool alternative)
    {
        var initial = Position(card, alternative ? 4 : 8);
        var game = new Driver(initial);
        Assert.DoesNotContain(Alternative, JsonSerializer.Serialize(game.PromptFor("P1"), Web));
        await game.Act("P1", RocketPlay());
        var confirmed = Assert.Single(game.State.CardPlays.Finalized);
        Assert.Equal(4, confirmed.PaidMana); Assert.Empty(game.State.CardPlays.Resolved);
        Assert.True(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        // Payment grants a price, never permission to deploy a unit into a closed chain.
        await RadGearTestPosition.Reject(game.State, "P1", Play(card, true));
        if (countered)
        {
            var target = Assert.Single(game.State.StackItems).StackItemId;
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [target]));
        }
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(countered ? 0 : 1, game.State.CardPlays.Resolved.Count(e => e.Source.ObjectId == "SPELL"));
        Assert.Equal(4, Assert.Single(game.State.CardPlays.Finalized, e => e.Source.ObjectId == "SPELL").PaidMana);
        await game.ColdCheckpoint();
        var requirement = Requirements(game.PromptFor("P1")).Where(r => Equals(r["sourceObjectId"], "JHIN")).ToArray();
        Assert.NotEmpty(requirement);
        Assert.Contains(requirement, r => Equals(r["spellManaAlternativeCostChoiceId"], Alternative));
        Assert.DoesNotContain("JHIN", JsonSerializer.Serialize(game.PromptFor("P2"), Web));
        Assert.DoesNotContain(card, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"], Web));
        if (alternative) await RadGearTestPosition.Reject(game.State, "P1", Play(card, false));
        var play = Play(card, alternative);
        var before = game.State;
        var quote = Quote(before, play);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(alternative ? 0 : 4, quote.Cost!.Mana);
        Assert.Equal(alternative ? 1 : PrintedPowerCostRules.ForCard(card).Amount,
            quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(quote.Cost.Mana, quote.Cost.PrintedMana + quote.Cost.Adjustments.Sum(a => a.Mana));
        Assert.Equal(quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum(),
            quote.Cost.PrintedPower + quote.Cost.Adjustments.Sum(a => a.Power));
        await game.Act("P1", play);
        Assert.Equal(before.RunePools["P1"].Mana - quote.Cost.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower - quote.Cost.GenericPower - quote.Cost.PowerByTrait.Values.Sum(),
            game.State.RunePools["P1"].TotalPower);
        Assert.Contains("JHIN", game.State.PlayerZones["P1"].Base);
        Assert.Equal(4, game.State.CardObjects["JHIN"].Power);
        Assert.Equal("PRINTED_PREDICT", Assert.Single(game.State.StackItems).InsightContext!.Kind);
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("reduced", 3, false)]
    [InlineData("increased", 4, true)]
    [InlineData("echo", 4, true)]
    public async Task QualificationUsesTheSingleSpellsActualCombinedManaPayment(string branch, int mana, bool qualifies)
    {
        var initial = Position("UNL-089/219", 10);
        if (branch == "reduced") initial = RadGearTestPosition.Put(initial, "REDUCER", "OGN·084/298", "P1", "BATTLEFIELD", "BF");
        if (branch == "increased")
        {
            initial = RadGearTestPosition.Put(initial, "SPELL", "VEN·049", "P1", "HAND");
            initial = RadGearTestPosition.Put(initial, "TAX1", "VEN·045", "P2", "BASE");
            initial = RadGearTestPosition.Put(initial, "TAX2", "VEN·045", "P2", "BASE");
        }
        if (branch == "echo")
        {
            initial = RadGearTestPosition.Put(initial, "SPELL", "UNL-061/219", "P1", "HAND");
        }
        var game = new Driver(initial);
        var command = branch == "echo" ? new PlayCardCommand("SPELL", "UNL-061/219", [], OptionalCosts: ["ECHO"])
            : branch == "increased" ? new PlayCardCommand("SPELL", "VEN·049", []) : RocketPlay();
        Assert.Equal(mana, Quote(initial, command).Cost!.Mana);
        await game.Act("P1", command);
        Assert.Equal(mana, Assert.Single(game.State.CardPlays.Finalized).PaidMana);
        Assert.Equal(qualifies, CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        await game.Top();
        Assert.Equal(qualifies, Quote(game.State, Play("UNL-089/219", true)).CanPay);
        if (!qualifies) await RadGearTestPosition.Reject(game.State, "P1", Play("UNL-089/219", true));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TwoSmallPaymentsDoNotCombineAndAClientMarkerCannotSupplyTheMissingHistory()
    {
        var initial = Position("UNL-089/219", 8);
        initial = RadGearTestPosition.Put(initial, "SMALL1", "VEN·049", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "SMALL2", "VEN·049", "P1", "HAND");
        var game = new Driver(initial);
        foreach (var id in new[] { "SMALL1", "SMALL2" })
        { await game.Act("P1", new PlayCardCommand(id, "VEN·049", [])); await game.Top(); }
        Assert.Equal(new int?[] { 2, 2 }, game.State.CardPlays.Finalized.Select(e => e.PaidMana));
        await RadGearTestPosition.Reject(game.State, "P1", Play("UNL-089/219", true));
        var forgedMarker = game.State with { UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects,
            "PLAYED_SPELL_PAID_MANA:P1:4", "SPELL_MANA_ALTERNATIVE_COST:4:1:blue"] };
        await RadGearTestPosition.Reject(forgedMarker, "P1", Play("UNL-089/219", true));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task SpellOnlyManaCountsButTheOpponentsPaymentDoesNot()
    {
        var initial = Position("UNL-089/219", 0);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }, spellOnlyMana: 4) } };
        var game = new Driver(initial);
        await game.Act("P1", RocketPlay()); await game.Top();
        Assert.Equal(4, Assert.Single(game.State.CardPlays.Finalized).PaidMana);
        await game.Act("P1", Play("UNL-089/219", true));
        var other = Position("UNL-089/219", 4);
        other = RadGearTestPosition.Put(other, "SPELL", Rocket, "P2", "HAND");
        other = other with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var opponent = new Driver(other);
        await opponent.Act("P2", RocketPlay()); await opponent.Top();
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(opponent.State, "P1", 4));
        Assert.True(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(opponent.State, "P2", 4));
        await game.ColdCheckpoint(); await opponent.ColdCheckpoint();
    }

    [Theory]
    [InlineData("wrong-color")]
    [InlineData("duplicate-choice")]
    [InlineData("wrong-amount")]
    [InlineData("wrong-source")]
    public async Task AQualifiedPlayerStillNeedsTheExactPriceAndAnAuthorizedCardSource(string branch)
    {
        var initial = Position("UNL-089a/219", 4);
        if (branch == "wrong-color") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1, [RuneTrait.Red] = 1 }) } };
        if (branch == "wrong-source") initial = RadGearTestPosition.Put(initial, "JHIN", "UNL-089a/219", "P1", "GRAVEYARD");
        var game = new Driver(initial); await game.Act("P1", RocketPlay()); await game.Top();
        var command = Play("UNL-089a/219", true);
        if (branch == "duplicate-choice") command = command with { OptionalCosts = [Alternative, Alternative] };
        if (branch == "wrong-amount") command = command with { OptionalCosts = ["SPELL_MANA_ALTERNATIVE_COST:4:0:"] };
        await RadGearTestPosition.Reject(game.State, "P1", command);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task WildcardPowerCanPayTheBlueAlternativeAndANewTurnClearsTheCondition()
    {
        var initial = Position("UNL-089/219", 4);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(4, 2) } };
        var game = new Driver(initial); await game.Act("P1", RocketPlay()); await game.Top();
        Assert.True(Quote(game.State, Play("UNL-089/219", true)).CanPay);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(initial.TurnNumber + 1, game.State.TurnNumber);
        Assert.Empty(game.State.CardPlays.Finalized);
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        await game.ColdCheckpoint();
        var paid = new Driver(initial); await paid.Act("P1", RocketPlay()); await paid.Top();
        await paid.Act("P1", Play("UNL-089/219", true));
        Assert.Equal(0, paid.State.RunePools["P1"].TotalPower);
    }

    [Theory]
    [InlineData("UNL-089/219")]
    [InlineData("UNL-089a/219")]
    public async Task ReplacingTheBaseCostStillPaysTheExistingHeldBattlefieldUnitTax(string card)
    {
        // This seed already carries the shared hold instruction's current-turn tax.
        var initial = Position(card, 5) with { UntilEndOfTurnEffects = ["BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1"] };
        var game = new Driver(initial); await game.Act("P1", RocketPlay()); await game.Top();
        var quote = Quote(game.State, Play(card, true));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(1, Assert.Single(quote.Cost.Adjustments, a => a.Label == "战场增费").Mana);
        await game.Act("P1", Play(card, true));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(1, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["mana"]);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARealBanishReplayKeepsTheNormalIgnoredBaseCostAndOptionalBlueReplacement(bool alternative)
    {
        var initial = RadGearTestPosition.Put(Position("UNL-089/219", 30), "JHIN", "UNL-089/219", "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "HUNT", "UNL-184/219", "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(30, 10) } };
        var game = new Driver(initial); await game.Act("P1", RocketPlay()); await game.Top();
        await game.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["JHIN"])); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
        var command = Play("UNL-089/219", alternative) with { Destination = "BATTLEFIELD:BF" };
        var quote = Quote(game.State, command);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(alternative ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["JHIN"].Zone);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARealAuroraPermissionCanIgnoreBasePowerAfterTheChosenReplacement(bool alternative)
    {
        var initial = DeckObservationPlayTests.Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["NOCT"] = OfficialGarenConquestBattlefieldCountTests.Printed("NOCT", "UNL-089/219", "P1") } };
        initial = RadGearTestPosition.Put(initial, "SPELL", Rocket, "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "VICTIM", "OGN·219/298", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", RocketPlay() with { TargetObjectIds = ["VICTIM"] }); await game.Top();
        await game.Act("P1", new EndTurnCommand()); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
        Assert.True(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        var command = new PlayCardCommand("NOCT", "UNL-089/219", [],
            OptionalCosts: alternative ? ["SPELL_MANA_ALTERNATIVE_COST:4:0:"] : []);
        var quote = Quote(game.State, command);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.Contains("NOCT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["power"]);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheActualRevealCardIntentAlsoProducesAnAuthenticatedSpellPayment()
    {
        var initial = AttackDamageDistributionTests.Spell(StandbySourceRulesTests.Position(), "REPLY", "OGN·048/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand()); Assert.Null(game.State.TurnStartStep);
        await game.Act("P2", new TapRuneCommand("SHADOW-P2-RUNE-1", ["SHADOW-P2-RUNE-1", "SHADOW-P2-RUNE-2", "SHADOW-P2-RUNE-3"]));
        await game.Act("P2", new RecycleRuneCommand("SHADOW-P2-RUNE-4", ["SHADOW-P2-RUNE-4", "SHADOW-P2-RUNE-5"]));
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new RevealCardCommand("SHADOW", "OGN·057/298", ["HOST"], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        Assert.Equal(CommandTypes.RevealCard, game.Commands()[^1].CommandType);
        Assert.Equal(0, Assert.Single(game.State.CardPlays.Finalized, e => e.Source.ObjectId == "SHADOW").PaidMana);
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task PaymentCreditBelongsToThePlayerWhoCastTheSpellRatherThanItsOwner()
    {
        var initial = Position("UNL-089/219", 4);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SPELL"] = initial.CardObjects["SPELL"] with { OwnerId = "P2" } } };
        var game = new Driver(initial); await game.Act("P1", RocketPlay()); await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P2"].Graveyard);
        Assert.True(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P2", 4));
        await game.Act("P1", Play("UNL-089/219", true)); await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("actor")]
    [InlineData("source-generation")]
    [InlineData("play-id")]
    [InlineData("turn")]
    [InlineData("missing-receipt")]
    [InlineData("payment-event")]
    public async Task RecoveryRejectsPaymentCreditThatDiffersFromTheOriginalAcceptedHistory(string mutation)
    {
        var initial = RadGearTestPosition.Put(Position("UNL-089/219", 8), "REDUCER", "OGN·084/298", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P1", RocketPlay()); await game.Top();
        var original = game.State; var history = original.CardPlays; var entry = Assert.Single(history.Finalized);
        Assert.Equal(3, entry.PaidMana);
        var changed = mutation switch {
            "mana" => entry with { PaidMana = 4 }, "actor" => entry with { PlayerId = "P2" },
            "source-generation" => entry with { Source = entry.Source with { Generation = entry.Source.Generation + 1 } },
            "play-id" => entry with { PlayId = "STACK-1-JHIN" }, _ => entry };
        var forged = original with { CardPlays = history with {
            TurnNumber = mutation == "turn" ? history.TurnNumber + 1 : history.TurnNumber,
            Finalized = [changed], Resolved = [Assert.Single(history.Resolved) with {
                PaidMana = changed.PaidMana, PlayerId = changed.PlayerId, Source = changed.Source, PlayId = changed.PlayId }] } };
        var events = game.Events();
        if (mutation is "missing-receipt" or "payment-event")
            events = events.Select(e => e.Event.Kind == (mutation == "missing-receipt" ? "CARD_PLAYED" : "COST_PAID")
                ? e with { Event = e.Event with { Payload = e.Event.Payload.Where(p => p.Key != "spellManaPayment")
                    .ToDictionary(p => p.Key, p => p.Key == "mana" ? (object?)4 : p.Value) } } : e).ToArray();
        var sequence = events[^1].Sequence; var snapshots = ResolutionResult.BuildSnapshots(forged);
        var prompts = ResolutionResult.BuildPrompts(forged);
        var views = forged.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, forged.Tick, sequence,
            snapshots[p], forged.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(forged.RoomId, forged.Tick, sequence, [], forged);
        var frame = new MatchRecoveryFrame(forged.RoomId, forged.Tick, sequence, game.Commands(), events, views, [],
            forged, initial, spectator) { OpeningSeed = initial.Seed };
        frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.Contains(errors, e => e.Contains("confirmed spell mana history", StringComparison.Ordinal));
        var structural = MatchRecoveryValidator.Validate(frame.RoomId, sequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.Contains(structural, e => e.Contains("confirmed spell mana history", StringComparison.Ordinal));
        Assert.Equal(MatchStateHasher.Hash(original), MatchStateHasher.Hash(game.State));
        await game.ColdCheckpoint();
    }

    private static MatchState Position(string card, int mana)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "JHIN", card, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "SPELL", Rocket, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COUNTER", "OGN·045/298", "P2", "HAND");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }) } };
    }

    private static PlayCardCommand RocketPlay() => new("SPELL", Rocket, ["ENEMY"], Mode: "BASE_UNIT_DAMAGE_4");
    private static PlayCardCommand Play(string card, bool alternative)
        => new("JHIN", card, [], OptionalCosts: alternative ? [Alternative] : []);
    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
    private static IEnumerable<IReadOnlyDictionary<string, object?>> Requirements(ActionPromptDto prompt)
        => (prompt.Candidates ?? []).Where(c => c.Action == CommandTypes.PlayCard)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!);
}
