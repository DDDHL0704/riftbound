using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialHeldConfirmationTests
{
    internal static MatchState Position(string kind)
    {
        var s = OfficialHoldSequenceTests.State(kind switch { "boon" => "OGN·283/298", "move" => "UNL-207/219", "channel" => "OGN·288/298", _ => "OGN·294/298" });
        s = OfficialHoldSequenceTests.AddUnit(s, "UNIT", "OGN·096/298", "F", "P1");
        s = OfficialHoldSequenceTests.AddUnit(s, "ENEMY", "OGN·096/298", "OTHER", "P2");
        if (kind != "return") return s;
        s = OfficialHoldSequenceTests.AddUnit(s, "KEEPER", "SFD·035/221", "F", "P1");
        var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var positions = s.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        cards["G1"] = new("G1", cardNo: "OGN·096/298", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");
        cards["G2"] = new("G2", cardNo: "OGN·023/298", tags: [CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: "P1");
        cards["G3"] = new("G3", cardNo: "OGN·169/298", tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1");
        foreach (var id in new[] { "G1", "G2", "G3" }) positions[id] = new("P1", "GRAVEYARD");
        return s with { CardObjects = cards, ObjectLocations = positions, PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Graveyard = ["G1", "G2", "G3"] } } };
    }
    internal static string Pick(string kind) => kind switch { "channel" => "F", "return" => "G2", "move" => "ENEMY", _ => "UNIT" };
    internal static Task<ResolutionResult> Choose(MatchState state, params string[] ids) => Act(state, "P1",
        new ChooseCardsCommand(state.PendingCardChoice!.ChoiceId, state.PendingCardChoice.ChoiceWindow, ids));

    internal static Task<ResolutionResult> Pay(MatchState state, bool pay) => Act(state, "P1",
        new PayCostCommand(state.PendingPayment!.PaymentId, state.PendingPayment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));

    [Theory]
    [InlineData("boon")]
    [InlineData("move")]
    [InlineData("return")]
    [InlineData("channel")]
    public async Task EveryDecisionIsConfirmedBeforeFirstResponseAndEffectsWait(string kind)
    {
        var opened = await Act(Position(kind), "P1", new PassPriorityCommand());
        Assert.Null(opened.State.PriorityPlayerId); Assert.Single(opened.State.StackItems); Restore(opened.State);
        Assert.Equal(kind == "boon" ? 1 : 0, opened.State.PendingCardChoice!.RequiredCount);
        var paid = await Choose(opened.State, Pick(kind));
        Assert.NotNull(paid.State.PriorityPlayerId); Assert.Null(paid.State.PendingCardChoice); Restore(paid.State);
        Assert.DoesNotContain(paid.Events, e => e.Kind is "BOON_GRANTED" or "UNIT_MOVED_TO_BASE" or "CARD_RETURNED_TO_HAND" or "RUNES_CALLED");
        if (kind != "channel") Assert.Equal(paid.State.CardObjects[Pick(kind)].ObjectGeneration, paid.State.StackItems.Single().TargetGenerations![Pick(kind)]);
        var first = await Act(paid.State, "P1", new PassPriorityCommand());
        Assert.Equal("P2", first.State.PriorityPlayerId);
        var done = await Act(first.State, "P2", new PassPriorityCommand());
        AssertOutcome(done.State, kind, true); Restore(done.State);
    }
    internal static void AssertOutcome(MatchState state, string kind, bool accept)
    {
        if (kind == "boon") Assert.Equal(accept, state.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Boon));
        if (kind == "move") Assert.Equal(accept, state.PlayerZones["P2"].Base.Contains("ENEMY"));
        if (kind == "return") Assert.Equal(accept, state.PlayerZones["P1"].Hand.Contains("G2"));
        if (kind == "channel") Assert.Equal(accept ? 0 : 1, state.PlayerZones["P1"].RuneDeck.Count);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("return")]
    [InlineData("channel")]
    public async Task OptionalDeclineRemovesTriggerWithoutOpeningResponse(string kind)
    {
        var opened = await Act(Position(kind), "P1", new PassPriorityCommand());
        var done = await Choose(opened.State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        AssertOutcome(done.State, kind, false); Restore(done.State);
    }

    [Theory]
    [InlineData("boon")]
    [InlineData("move")]
    public async Task EnemyWardUsesGenericPowerBeforeResponseAndIsNotPaidTwice(string kind)
    {
        var s = Position(kind);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY"] = s.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ENEMY"] = new("P2", "BATTLEFIELD", "F") } };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        var selected = await Choose(opened.State, "ENEMY");
        Assert.Null(selected.State.PriorityPlayerId); Restore(selected.State);
        var paid = await Pay(selected.State, true);
        Assert.Equal(8, paid.State.RunePools["P1"].TotalPower); Assert.Equal(7, paid.State.RunePools["P1"].Mana);
        Assert.Contains(paid.Events, e => e.Kind == "COST_PAID");
        var done = await Top(paid.State);
        Assert.DoesNotContain(done.Events, e => e.Kind == "COST_PAID");
        if (kind == "boon") Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ENEMY"].Tags);
        else Assert.Contains("ENEMY", done.State.PlayerZones["P2"].Base);
    }

    [Theory]
    [InlineData("boon")]
    [InlineData("move")]
    [InlineData("return")]
    public async Task TargetThatLeavesAndReturnsAsNewObjectIsNotAffected(string kind)
    {
        var opened = await Act(Position(kind), "P1", new PassPriorityCommand());
        var confirmed = await Choose(opened.State, Pick(kind));
        var cards = confirmed.State.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards[Pick(kind)] = cards[Pick(kind)] with { ObjectGeneration = cards[Pick(kind)].ObjectGeneration + 2 };
        var changed = confirmed.State with { CardObjects = cards }; Restore(changed);
        var done = await Top(changed); AssertOutcome(done.State, kind, false); Restore(done.State);
    }

    [Fact]
    public async Task GustCanRemoveConfirmedBoonTargetBeforeItResolves()
    {
        var s = Position("boon");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GUST"] = new("GUST", cardNo: "OGN·169/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = ["GUST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["GUST"] = new("P2", "HAND") } };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        var confirmed = await Choose(opened.State, "UNIT");
        var passed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var gust = await Act(passed.State, "P2", new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
        var returned = await Top(gust.State); Assert.Contains("UNIT", returned.State.PlayerZones["P1"].Hand);
        var done = await Top(returned.State);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["UNIT"].Tags);
        Assert.DoesNotContain(done.Events, e => e.Kind == "BOON_GRANTED"); Restore(done.State);
    }

    [Theory]
    [InlineData("boon")]
    [InlineData("move")]
    [InlineData("return")]
    public async Task RecoveryRejectsMissingBindingsAndWrongPlayerCannotChoose(string kind)
    {
        var opened = await Act(Position(kind), "P1", new PassPriorityCommand());
        var choice = opened.State.PendingCardChoice!;
        var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("wrong", "P2", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [Pick(kind)]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        var paid = await Choose(opened.State, Pick(kind)); var item = paid.State.StackItems.Single();
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [item with { TargetGenerations = null }] }), e => e.Contains("held target confirmation"));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [item with { TargetGenerations = new Dictionary<string, long>() }] }), e => e.Contains("held target confirmation"));
    }

    [Fact]
    public async Task ChannelChoiceDoesNotExposeRuneOrderAndAllowsEmptyRuneDeck()
    {
        var s = Position("channel");
        s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { RuneDeck = [], Base = s.PlayerZones["P1"].Base.Concat(s.PlayerZones["P1"].RuneDeck).ToArray() } },
            ObjectLocations = s.ObjectLocations.ToDictionary(e => e.Key, e => s.PlayerZones["P1"].RuneDeck.Contains(e.Key) ? new ObjectLocationState("P1", "BASE") : e.Value) };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        Assert.Equal(["F"], opened.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("R1", JsonSerializer.Serialize(opened.Prompts));
        var paid = await Choose(opened.State, "F");
        var done = await Top(paid.State); Assert.Empty(done.State.PlayerZones["P1"].RuneDeck); Restore(done.State);
    }
    internal static MatchState WardPosition(bool resources = false)
    {
        var s = Position("boon");
        var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["ENEMY"] = cards["ENEMY"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] };
        var locations = s.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        locations["ENEMY"] = new("P2", "BATTLEFIELD", "F");
        locations["UNIT"] = new("P1", "BASE");
        locations["R1"] = new("P1", "BASE");
        return s with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = resources ? s.RunePools["P1"] : RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
                Battlefields = s.PlayerZones["P1"].Battlefields.Where(id => id != "UNIT").ToArray(), Base = ["UNIT", "R1"],
                RuneDeck = ["R2", "R3"] } } };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MandatoryWardTriggerCanBeDeclinedWithOrWithoutFunds(bool resources)
    {
        var opened = await Act(WardPosition(resources), "P1", new PassPriorityCommand());
        Assert.Equal(["ENEMY"], opened.State.PendingCardChoice!.LegalObjectIds);
        var selected = await Choose(opened.State, "ENEMY"); Restore(selected.State);
        var declined = await Pay(selected.State, false);
        Assert.Empty(declined.State.StackItems); Assert.Null(declined.State.PendingPayment);
        Assert.DoesNotContain(CardObjectTags.Boon, declined.State.CardObjects["ENEMY"].Tags);
        Assert.DoesNotContain(declined.Events, e => e.Kind == "COST_PAID"); Restore(declined.State);
    }

    [Fact]
    public async Task WardPaymentIsAtomicAndRuneRecyclingResumesLockedTarget()
    {
        var opened = await Act(WardPosition(), "P1", new PassPriorityCommand());
        var selected = await Choose(opened.State, "ENEMY");
        var payment = selected.State.PendingPayment!;
        var failed = await new CoreRuleEngine().ResolveAsync(selected.State, new("poor", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]), default);
        Assert.False(failed.Accepted); Assert.Equal(MatchStateHasher.Hash(selected.State), MatchStateHasher.Hash(failed.State));
        var recycled = await Act(selected.State, "P1", new RecycleRuneCommand("R1")); Restore(recycled.State);
        Assert.Equal(["ENEMY"], recycled.State.StackItems.Single().TargetObjectIds);
        var paid = await Pay(recycled.State, true); Assert.Equal(0, paid.State.RunePools["P1"].TotalPower);
        var done = await Top(paid.State); Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ENEMY"].Tags); Restore(done.State);
    }

    [Theory]
    [InlineData("boon")]
    [InlineData("move")]
    public async Task EnemyProtectionIsExcludedBeforeConfirmation(string kind)
    {
        var s = Position(kind);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY"] = s.CardObjects["ENEMY"] with { CardNo = "UNL-147/219" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ENEMY"] = new("P2", "BATTLEFIELD", "F") } };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        Assert.DoesNotContain("ENEMY", opened.State.PendingCardChoice!.LegalObjectIds);
        Assert.Contains("UNIT", opened.State.PendingCardChoice.LegalObjectIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MovementRestrictionsAreRecheckedAtResolution(bool battlefieldRestriction)
    {
        var opened = await Act(Position("move"), "P1", new PassPriorityCommand());
        var paid = await Choose(opened.State, "ENEMY");
        var cards = paid.State.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        if (battlefieldRestriction) cards["OTHER"] = cards["OTHER"] with { CardNo = "OGN·295/298" };
        else cards["ENEMY"] = cards["ENEMY"] with { UntilEndOfTurnEffects = ["MOVEMENT_PROHIBITED:P1"] };
        var done = await Top(paid.State with { CardObjects = cards });
        Assert.DoesNotContain("ENEMY", done.State.PlayerZones["P2"].Base); Restore(done.State);
    }

    [Fact]
    public async Task GraveyardOnlyOffersOwnPermanentsAndDoesNotChargeInactiveWard()
    {
        var s = Position("return");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["G2"] = s.CardObjects["G2"] with { Tags = [CardObjectTags.EquipmentCard, CardObjectTags.Spellshield], ControllerId = "P2" } } };
        var opened = await Act(s, "P1", new PassPriorityCommand());
        Assert.Equal(["G1", "G2"], opened.State.PendingCardChoice!.LegalObjectIds);
        var confirmed = await Choose(opened.State, "G2"); Assert.Null(confirmed.State.PendingPayment);
        var done = await Top(confirmed.State); Assert.Contains("G2", done.State.PlayerZones["P1"].Hand); Restore(done.State);
    }

    [Fact]
    public async Task MandatoryFreeTargetCannotBeDeclinedAndNoTargetsSkipCleanly()
    {
        var opened = await Act(Position("boon"), "P1", new PassPriorityCommand());
        var choice = opened.State.PendingCardChoice!;
        var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("decline", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        var s = Position("boon");
        s = s with { ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "OTHER") } };
        var done = await Act(s, "P1", new PassPriorityCommand());
        Assert.Null(done.State.PendingCardChoice); Assert.Empty(done.State.StackItems); Assert.Equal(MatchPhases.Main, done.State.Phase); Restore(done.State);
    }

}
