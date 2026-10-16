using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SandSoldierEmbeddedReadyTests
{
    internal const string Protect = "SFD·154/221";
    internal const string Rise = "SFD·198/221";
    internal const string Source = "SAND-PARENT";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProtectCreatesAnExhaustedUnitBeforeConfirmingItsSeparateYellowPayment(bool pay)
    {
        var initial = Position(Protect);
        var driver = Driver(initial, "protect-" + pay);
        await driver.Act("P1", new PlayCardCommand(Source, Protect, []));
        var entered = await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02");
        Assert.True(token.IsExhausted);
        Assert.Contains(Source, driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(17, driver.State.RunePools["P1"].Mana);
        Assert.Equal(2, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        var pending = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Null(driver.State.PriorityPlayerId);
        Assert.Equal(0, pending.ManaCost); Assert.Equal(1, pending.PowerCost);
        Assert.Equal(new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }, pending.PowerCostByTrait);
        Assert.Contains(entered.Events, e => e.Kind == "TRIGGER_QUEUED");
        await driver.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.True(driver.State.CardObjects[token.ObjectId].IsExhausted);
        Assert.Equal(pay ? 1 : 2, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        if (pay)
        {
            Assert.Single(driver.State.StackItems);
            await driver.Top();
            Assert.False(driver.State.CardObjects[token.ObjectId].IsExhausted);
        }
        else Assert.Empty(driver.State.StackItems);
        Assert.Equal(17, driver.State.RunePools["P1"].Mana);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(RuneTrait.Red)]
    [InlineData("")]
    public async Task ProtectStillOpensItsOptionalTriggerWhenYellowIsUnavailableAndRejectsWrongPowerAtomically(string trait)
    {
        var initial = Position(Protect) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = trait == "" ? new(20, 0) : new(20, 0, new Dictionary<string, int> { [trait] = 2 }), ["P2"] = RunePool.Empty } };
        var driver = Driver(initial, "wrong-color-" + trait);
        await driver.Act("P1", new PlayCardCommand(Source, Protect, [])); await driver.Top();
        var pending = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        var before = MatchStateHasher.Hash(driver.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("wrong-color", "P1", CommandTypes.PayCost),
            new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(before, MatchStateHasher.Hash(rejected.State)); Assert.Empty(rejected.Events);
        await driver.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["DECLINE"]));
        Assert.True(Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02").IsExhausted);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task RiseCountsCurrentControlledEquipmentThenTargetsOnlyItsNewTokensAfterEntry(int equipment)
    {
        var initial = Position(Rise);
        initial = AddBase(initial, new("OLD-SOLDIER", power: 2, cardNo: "SFD·T02", isExhausted: true,
            ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard, CardObjectTags.SandSoldier]));
        for (var i = 0; i < equipment; i++) initial = AddBase(initial, new("GEAR-" + i,
            ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]));
        var driver = Driver(initial, "rise-" + equipment);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"].Candidates!.Single(c => c.Action == CommandTypes.PlayCard && (c.Sources ?? []).Any(s => s.Id == Source));
        Assert.Empty(prompt.Targets ?? []);
        await driver.Act("P1", new PlayCardCommand(Source, Rise, [])); var entered = await driver.Top();
        var tokens = driver.State.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "SFD·T02").OrderBy(c => c.ObjectId).ToArray();
        Assert.Equal(equipment, tokens.Length); Assert.All(tokens, c => Assert.True(c.IsExhausted));
        Assert.Contains(entered.Events, e => e.Kind == "TRIGGER_QUEUED");
        if (equipment > 0)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal(0, choice.RequiredCount); Assert.Equal(Math.Min(2, equipment), choice.MaxCount);
            Assert.Equal(tokens.Select(c => c.ObjectId), choice.LegalObjectIds);
            Assert.DoesNotContain("OLD-SOLDIER", choice.LegalObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, tokens.Take(2).Select(c => c.ObjectId).ToArray()));
        }
        Assert.Single(driver.State.StackItems); await driver.Top();
        Assert.All(tokens.Take(2), c => Assert.False(driver.State.CardObjects[c.ObjectId].IsExhausted));
        Assert.All(tokens.Skip(2), c => Assert.True(driver.State.CardObjects[c.ObjectId].IsExhausted));
        Assert.True(driver.State.CardObjects["OLD-SOLDIER"].IsExhausted);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task RainbowPowerCanPayYellowWithoutManaAndRealYellowRuneCanFundThePendingTrigger()
    {
        foreach (var rainbow in new[] { true, false })
        {
            var initial = Position(Protect) with { RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(3, rainbow ? 1 : 0), ["P2"] = RunePool.Empty } };
            var catalog = await Riftbound.CardCatalog.OfficialCardCatalog.LoadDefaultAsync();
            var rune = catalog.Cards.First(c => c.CardCategory == "rune" && c.CardColorList.Contains(RuneTrait.Yellow));
            initial = AddBase(initial, new("YELLOW-RUNE", cardNo: rune.CardNo, ownerId: "P1", controllerId: "P1",
                isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:yellow"]));
            var driver = Driver(initial, "resource-" + rainbow);
            await driver.Act("P1", new PlayCardCommand(Source, Protect, [])); await driver.Top();
            Assert.Equal(0, driver.State.RunePools["P1"].Mana);
            if (!rainbow) await driver.Act("P1", new RecycleRuneCommand("YELLOW-RUNE"));
            var pending = driver.State.PendingPayment!;
            await driver.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]));
            Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
            Assert.Equal(0, driver.State.RunePools["P1"].Mana);
            await driver.Top();
            Assert.False(Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02").IsExhausted);
            Assert.Equal(!rainbow, driver.State.PlayerZones["P1"].RuneDeck.Contains("YELLOW-RUNE"));
            await Replay(initial, driver);
        }
    }

    [Theory]
    [InlineData(Protect)]
    [InlineData(Rise)]
    public async Task RemovingTheNewTokenDuringTheIndependentResponseCannotReadyOrRecreateIt(string card)
    {
        var initial = Position(card);
        if (card == Rise) initial = AddBase(initial, new("GEAR-NEW", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]));
        initial = AttackDamageDistributionTests.Spell(initial, "BOUNCE", "OGN·104/298", "P1");
        var driver = Driver(initial, "response-" + card.Replace('/', '-'));
        await driver.Act("P1", new PlayCardCommand(Source, card, [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02");
        if (card == Protect)
        {
            var pending = driver.State.PendingPayment!;
            await driver.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]));
        }
        else
        {
            var choice = driver.State.PendingCardChoice!;
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [token.ObjectId]));
        }
        if (driver.State.PriorityPlayerId != "P1") await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        await driver.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", [token.ObjectId]));
        await driver.Top(); Assert.False(driver.State.CardObjects.ContainsKey(token.ObjectId));
        var done = await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey(token.ObjectId)); Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_READIED");
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ChoosingZeroRiseRecipientsStillConfirmsAMandatoryEmbeddedAbilityWithResponses()
    {
        var initial = AddBase(Position(Rise), new("GEAR-NEW", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]));
        var driver = Driver(initial, "rise-zero-choice");
        await driver.Act("P1", new PlayCardCommand(Source, Rise, [])); await driver.Top();
        var choice = driver.State.PendingCardChoice!;
        var confirmed = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Single(driver.State.StackItems); Assert.NotNull(driver.State.PriorityPlayerId);
        Assert.DoesNotContain(confirmed.Events, e => e.Kind == "TRIGGER_NOT_CONFIRMED");
        await driver.Top();
        Assert.True(Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02").IsExhausted);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("id")]
    [InlineData("window")]
    [InlineData("choice")]
    [InlineData("combined")]
    public async Task InvalidPaymentCannotStealTheWindowOrChangeState(string mutation)
    {
        var initial = Position(Protect); var driver = Driver(initial, "forged-payment-" + mutation);
        await driver.Act("P1", new PlayCardCommand(Source, Protect, [])); await driver.Top();
        var p = driver.State.PendingPayment!;
        var before = MatchStateHasher.Hash(driver.State);
        var command = new PayCostCommand(mutation == "id" ? "forged" : p.PaymentId,
            mutation == "window" ? "PLAY_CARD" : p.PaymentWindow,
            mutation == "combined" ? ["PAY", "DECLINE"] : mutation == "choice" ? ["PAY:TRAIT:red"] : ["PAY"]);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State,
            new("forged", mutation == "player" ? "P2" : "P1", CommandTypes.PayCost), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await driver.Act("P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, ["DECLINE"]));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(Protect)]
    [InlineData(Rise)]
    public async Task RecoveryIndependentlyRejectsMissingForeignDuplicatedAndFutureTokenBindings(string card)
    {
        var initial = Position(card);
        if (card == Rise) initial = AddBase(initial, new("GEAR-NEW", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]));
        var driver = Driver(initial, "forged-binding-" + card.Replace('/', '-'));
        await driver.Act("P1", new PlayCardCommand(Source, card, [])); await driver.Top();
        var state = driver.State; var item = Assert.Single(state.StackItems); var captured = Assert.IsType<CreatedTokenReadyContext>(item.CreatedTokenReady);
        var unit = Assert.Single(captured.Units);
        foreach (var invalid in new[] {
            item with { CreatedTokenReady = null },
            item with { CreatedTokenReady = captured with { CardNo = "SFD·031/221" } },
            item with { CreatedTokenReady = captured with { Source = new("HOST", 0) } },
            item with { CreatedTokenReady = captured with { Units = [new("HOST", 0)] } },
            item with { CreatedTokenReady = captured with { Units = [unit with { Generation = unit.Generation + 100 }] } },
            item with { CreatedTokenReady = captured with { Units = [unit, unit] } },
            item with { CreatedTokenReady = captured with { Source = captured.Source with { Generation = 100 } } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [invalid] }), e => e.Contains("created token ready"));
        if (state.PendingPayment is { } p)
            foreach (var invalid in new[] {
                p with { PowerCostByTrait = new Dictionary<string, int>() },
                p with { PowerCostByTrait = new Dictionary<string, int> { [RuneTrait.Red] = 1 } },
                p with { ManaCost = 1 }, p with { PowerCost = 0 } })
                Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { PendingPayment = invalid }), e => e.Contains("trigger cost"));
        var invented = state with { StackItems = [item with { CreatedTokenReady = captured with {
            Units = [unit, new(Source + "-TOKEN-999", 0)] } }] };
        var causal = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), invented,
            new CoreRuleEngine(), default, driver.Events());
        Assert.False(causal.IsMatch); Assert.NotEmpty(causal.Errors);
    }

    [Theory]
    [InlineData("SFD·029/221", false, false)]
    [InlineData("SFD·029/221", false, true)]
    [InlineData("SFD·029/221", true, false)]
    [InlineData("SFD·029/221", true, true)]
    [InlineData("SFD·029a/221", false, false)]
    [InlineData("SFD·029a/221", false, true)]
    [InlineData("SFD·029a/221", true, false)]
    [InlineData("SFD·029a/221", true, true)]
    public async Task PreEntryHasteAndPostEntryYellowAreIndependentCostsAndDecisions(string grant, bool haste, bool ready)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(Position(Protect), "HASTE-GRANT", grant, "F", "P1") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0,
                new Dictionary<string, int> { [RuneTrait.Yellow] = 3, [RuneTrait.Red] = 1 }), ["P2"] = RunePool.Empty } };
        var driver = Driver(initial, "distinct-" + grant.Replace('/', '-') + "-" + haste + "-" + ready);
        await driver.Act("P1", new PlayCardCommand(Source, Protect, [])); await driver.Top();
        var unborn = driver.State.PendingRuleChoice!.Request;
        Assert.Equal("TOKEN_HASTE_PAYMENT", unborn.Kind);
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02");
        await driver.Act("P1", new PayCostCommand(unborn.Id, "RULE_REPLACEMENT", [haste ? "PAY:TRAIT:red" : "DECLINE"]));
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02");
        Assert.Equal(!haste, token.IsExhausted);
        var afterEntry = driver.State.PendingPayment!;
        Assert.Equal(0, afterEntry.ManaCost); Assert.Equal(1, afterEntry.PowerCostByTrait[RuneTrait.Yellow]);
        await driver.Act("P1", new PayCostCommand(afterEntry.PaymentId, afterEntry.PaymentWindow, [ready ? "PAY" : "DECLINE"]));
        Assert.Equal(!haste, driver.State.CardObjects[token.ObjectId].IsExhausted);
        Assert.Equal(17 - (haste ? 1 : 0), driver.State.RunePools["P1"].Mana);
        Assert.Equal(3 - (ready ? 1 : 0), driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        Assert.Equal(1 - (haste ? 1 : 0), driver.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        if (ready) await driver.Top();
        Assert.Equal(!haste && !ready, driver.State.CardObjects[token.ObjectId].IsExhausted);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(Protect)]
    [InlineData(Rise)]
    public async Task ANewIncarnationWithTheSameTransportIdIsNotReadiedByTheOldBinding(string card)
    {
        var initial = AddBase(Position(card), new("GEAR-NEW", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]));
        var driver = Driver(initial, "incarnation-" + card.Replace('/', '-'));
        await driver.Act("P1", new PlayCardCommand(Source, card, [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02");
        if (driver.State.PendingPayment is { } payment)
            await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        else
        {
            var choice = driver.State.PendingCardChoice!;
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [token.ObjectId]));
        }
        await Replay(initial, driver);
        // A synthetic later incarnation tests the reference boundary. It is not
        // inserted into this driver's accepted history or claimed as an original replay.
        var later = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
            [token.ObjectId] = token with { ObjectGeneration = token.ObjectGeneration + 2 } } };
        OfficialGraveyardRecastTests.Restore(later);
        var done = await OfficialGraveyardRecastTests.Top(later);
        Assert.True(done.State.CardObjects[token.ObjectId].IsExhausted);
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_READIED");
    }

    [Fact]
    public async Task ASyntheticControlChangeBeforeRiseConfirmationUsesTheSharedTargetWardPayment()
    {
        var initial = AddBase(Position(Rise), new("GEAR-NEW", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]));
        var driver = Driver(initial, "ward-prefix");
        await driver.Act("P1", new PlayCardCommand(Source, Rise, [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "SFD·T02");
        var state = driver.State with {
            CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) { [token.ObjectId] = token with {
                ControllerId = "P2", Tags = token.Tags.Append("法盾2").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), GrantedTags = ["法盾2"] } },
            PlayerZones = new Dictionary<string, PlayerZones>(driver.State.PlayerZones) {
                ["P1"] = driver.State.PlayerZones["P1"] with { Base = driver.State.PlayerZones["P1"].Base.Where(id => id != token.ObjectId).ToArray() },
                ["P2"] = driver.State.PlayerZones["P2"] with { Base = [..driver.State.PlayerZones["P2"].Base, token.ObjectId] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(driver.State.ObjectLocations) { [token.ObjectId] = new("P2", "BASE") } };
        var choice = state.PendingCardChoice!;
        var chosen = await OfficialGraveyardRecastTests.Act(state, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [token.ObjectId]));
        var payment = Assert.IsType<PendingPaymentState>(chosen.State.PendingPayment);
        Assert.Equal("TRIGGER_TARGET_COST", payment.PaymentWindow); Assert.Equal(2, payment.PowerCost);
        OfficialGraveyardRecastTests.Restore(chosen.State);
        var paid = await OfficialGraveyardRecastTests.Act(chosen.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        OfficialGraveyardRecastTests.Restore(paid.State);
        Assert.True(paid.State.CardObjects[token.ObjectId].IsExhausted);
        var done = await OfficialGraveyardRecastTests.Top(paid.State);
        Assert.False(done.State.CardObjects[token.ObjectId].IsExhausted);
    }

    internal static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        if (driver.NativeCommands > 0)
            Assert.Equal(driver.Commands().Count(c => c.CommandType is CommandTypes.PayCost or CommandTypes.ChooseCards), driver.NativeCommands);
        driver.Export("steps.json", driver.Steps);
        driver.Export("events.json", driver.Events());
        driver.Export("initial.json", initial);
        driver.Export("replay.json", new { nativePaymentAndChoiceCommands = driver.NativeCommands,
            acceptedCommands = driver.Commands().Length, coldRestores = driver.Commands().Length, fullOriginalPositionReplay = replay });
    }

    internal static MatchState Position(string card)
    {
        var state = AttackDamageDistributionTests.Spell(SacredProtectorTests.Position(), Source, card, "P1");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value); cards.Remove("GEAR");
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value); locations.Remove("GEAR");
        return state with {
            CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "GEAR").ToArray(),
                Battlefields = state.PlayerZones["P1"].Battlefields.Where(id => id != "GEAR").ToArray() } },
            Tick = 0, RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2, [RuneTrait.Green] = 1 }),
                ["P2"] = RunePool.Empty } };
    }

    internal static MatchState AddBase(MatchState state, CardObjectState card)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [card.ObjectId] = card },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = [..state.PlayerZones["P1"].Base, card.ObjectId] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [card.ObjectId] = new("P1", "BASE") } };

    internal static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_EMBEDDED_READY_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
