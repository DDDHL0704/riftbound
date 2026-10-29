using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenCardObserverTests
{
    [Theory]
    [InlineData("VEN·044", 37983, 7, 7)] [InlineData("VEN·094", 37791, 3, 3)]
    public async Task ExactPrintingHasAnOrdinaryUnitPlayAndRejectsInsufficientMana(string no, long id, int mana, int might)
    {
        var catalog = await Riftbound.CardCatalog.OfficialCardCatalog.LoadRuntimeAsync();
        var printed = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, printed.Id); Assert.Equal(mana, printed.Energy); Assert.Equal(might, printed.Power);
        var initial = VenUnitEntryBasicTests.Position(no);
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", no, []));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·118", 4, 0)] [InlineData("UNL-085/219", 0, 0)] [InlineData("OGN·043/298", 0, 0)]
    public async Task HeronDiscountsTheNextCardOfEveryTypeAfterItsSkillResolvesAndConsumesTheWholeDiscount(string next, int mana, int power)
    {
        var initial = Heron(true); initial = Put(initial, "NEXT", next);
        initial = Put(initial, "THIRD", "UNL-085/219");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        Assert.Single(game.State.StackItems); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount);
        await game.Top(); Assert.Single(game.State.StackItems); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount);
        await game.Top(); Assert.Single(game.State.UntilEndOfTurnEffects, IsDiscount);
        var command = new PlayCardCommand("NEXT", next, next == "OGN·043/298" ? ["ENEMY"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await Reject(game.State, "P1", command with { TargetObjectIds = ["GRAVE"] });
        var before = game.State.RunePools["P1"];
        await game.Act("P1", command); Assert.Equal(before.Mana - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - power, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount);
        if (game.State.StackItems.Count > 0) await game.Top();
        var third = new PlayCardCommand("THIRD", "UNL-085/219", []);
        Assert.Equal(2, new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, third)).Cost!.Mana);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent?.Timing == "FIRST_CARD_PLAYED");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HeronChecksItsBattlefieldAtPlayCompletionAndAnEarnedDiscountExpiresAtTurnEnd(bool onBattlefield)
    {
        var game = new Driver(Heron(onBattlefield)); await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        Assert.Single(game.State.StackItems); await game.Top();
        if (onBattlefield) await game.Top();
        Assert.Equal(onBattlefield ? 1 : 0, game.State.UntilEndOfTurnEffects.Count(IsDiscount));
        await game.Act("P1", new EndTurnCommand()); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount);
        await game.Replay();
    }

    [Fact]
    public async Task FirstCardCompletionQueuesItsObserverOnceAndConfirmationDoesNotQueueIt()
    {
        var game = new Driver(Heron(true));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        Assert.Single(game.State.StackItems); Assert.Empty(game.State.CardPlays.Resolved);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED");
        await game.Top();
        Assert.Equal("FIRST_CARD_PLAYED", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Single(game.State.CardPlays.Finalized); Assert.Single(game.State.CardPlays.Resolved);
        Assert.Single(game.State.UntilEndOfTurnEffects, IsDiscount);
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED");
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheFirstSpellDoesNotGrantADiscount()
    {
        var initial = AttackDamageDistributionTests.Spell(Heron(true), "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        var first = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [first]));
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount);
        Assert.DoesNotContain(game.State.CardPlays.Resolved, p => p.Source.ObjectId == "FIRST");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED");
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHeronInResponseRemovesItBeforeFirstCardCompletion()
    {
        var game = new Driver(Put(Heron(true), "RETURN", "OGN·104/298"));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED");
        await game.Replay();
    }

    [Fact]
    public async Task PlayingTheHeronItselfAsTheFirstCardToABattlefieldCapturesItsNewFieldIncarnation()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("VEN·044"));
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·044", [], Destination: "BATTLEFIELD:BF"));
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(game.State.CardObjects["UNIT"].ObjectGeneration, skill.UnitEvent!.SourceGeneration);
        await game.Top(); Assert.Single(game.State.UntilEndOfTurnEffects, IsDiscount); await game.Replay();
    }

    [Fact]
    public async Task ACardPlayedInResponseBeforeTheHeronSkillResolvesPaysItsFullCostAndDoesNotConsumeTheFutureDiscount()
    {
        var initial = Put(Put(Heron(true), "EARLY", "OGN·048/298"), "LATE", "UNL-085/219");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Act("P1", new PlayCardCommand("EARLY", "OGN·048/298", []));
        Assert.Equal(16, game.State.RunePools["P1"].Mana); await game.Top(); await game.Top(); await game.Top();
        Assert.Single(game.State.UntilEndOfTurnEffects, IsDiscount);
        await game.Act("P1", new PlayCardCommand("LATE", "UNL-085/219", []));
        Assert.Equal(16, game.State.RunePools["P1"].Mana); Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, IsDiscount); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DiscardedMotherChoosesAFriendlyUnitThenPaysOneManaBeforeOpeningResponses(bool decline)
    {
        var game = new Driver(Mother()); await game.Act("P1", Discard());
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var choice = game.State.PendingCardChoice!; Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(new[] { "ALLY" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var pay = game.State.PendingPayment!; Assert.Equal(1, pay.ManaCost); Assert.Equal(0, pay.PowerCost);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Act("P1", new PayCostCommand(pay.PaymentId, pay.PaymentWindow, [decline ? "DECLINE" : "PAY"]));
        Assert.Equal(decline ? 17 : 16, game.State.RunePools["P1"].Mana);
        if (!decline) { Assert.Equal(4, game.State.CardObjects["ALLY"].Power); await game.Top(); }
        Assert.Equal(decline ? 4 : 6, game.State.CardObjects["ALLY"].Power); await game.Top();
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task MotherCannotPayWithoutManaAndATargetReturnedInResponseDoesNotKeepTheBuff()
    {
        var initial = Put(Mother(), "RETURN", "OGN·104/298");
        var game = new Driver(initial); await game.Act("P1", Discard());
        var choice = game.State.PendingCardChoice!; await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var pay = game.State.PendingPayment!;
        await Reject(game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PayCostCommand(pay.PaymentId, pay.PaymentWindow, ["PAY"]));
        await game.Act("P1", new PayCostCommand(pay.PaymentId, pay.PaymentWindow, ["PAY"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); Assert.Equal(0, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }

    private static bool IsDiscount(string marker) => marker.StartsWith("HERON_NEXT_CARD_COST_REDUCTION:", StringComparison.Ordinal);
    private static MatchState Put(MatchState state, string id, string no) => HandDiscardObserverTests.Put(state, id, no, "P1", "HAND");
    private static MatchState Heron(bool field)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·044");
        initial = LocalDestructionRecallTests.At(initial, "UNIT", "P1", field ? "BATTLEFIELD" : "BASE", field ? "BF" : null);
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } } };
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        return Put(initial, "FIRST", "OGN·048/298");
    }
    private static MatchState Mother()
    {
        var initial = Put(VenUnitEntryBasicTests.Position("VEN·094"), "SPELL", "VEN·008");
        return LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
    }
    private static PlayCardCommand Discard() => new("SPELL", "VEN·008", ["ENEMY"], OptionalCosts: ["DISCARD_HAND_CARD:UNIT"]);
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-observer", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
