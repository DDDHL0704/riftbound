using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialInsightSourceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BlossomPaysAndDestroysBeforeResponseThenDrawsAfterChoice(int recycle)
    {
        var initial = Blossom();
        var played = await Act(initial, "P1", new ActivateAbilityCommand("B", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, []));
        Assert.Contains("B", played.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.RunePools["P1"].Mana - 1, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.PlayerExperience.GetValueOrDefault("P1"));
        Restore(played.State);
        var opened = await ResolveTop(played.State); Restore(opened.State);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(opened.Prompts["P2"]));
        Assert.Empty(opened.State.PlayerZones["P1"].Hand);
        var done = await Choose(opened.State, new[] { "D1", "D2" }.Take(recycle).ToArray());
        if (recycle == 0)
        {
            Restore(done.State); Assert.Empty(done.State.PlayerZones["P1"].Hand);
            done = await Choose(done.State, ["D2", "D1"]);
        }
        Assert.Equal(recycle == 2 ? "D3" : "D2", Assert.Single(done.State.PlayerZones["P1"].Hand));
        Assert.Equal(1, done.State.PlayerExperience["P1"]);
        Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("opponent")]
    [InlineData("hidden")]
    [InlineData("no-mana")]
    [InlineData("response")]
    public async Task BlossomRejectsIllegalActivationWithoutCosts(string reason)
    {
        var state = Blossom();
        if (reason == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        else if (reason == "response") state = state with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1" };
        else state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["B"] = state.CardObjects["B"] with { IsExhausted = reason == "exhausted", IsFaceDown = reason == "hidden", ControllerId = reason == "opponent" ? "P2" : "P1" } } };
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("bad", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("B", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, []), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("UNL-079/219", false)]
    [InlineData("UNL-079/219", true)]
    [InlineData("UNL-079a/219", true)]
    public async Task DianaOptionalPaymentThenRevealsAdjustedTopAndReturnsInitiatorFocus(string cardNo, bool recycle)
    {
        var moved = await Act(Diana(cardNo), "P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        Assert.Single(moved.State.StackItems); Assert.Equal("P2", moved.State.StackItems[0].ControllerId);
        Assert.NotNull(moved.State.PendingPayment); Assert.Null(moved.State.PriorityPlayerId); Restore(moved.State);
        var paying = moved;
        Assert.NotNull(paying.State.PendingPayment); Assert.Single(paying.State.StackItems);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(paying.Prompts));
        var confirmed = await Pay(paying.State, "PAY"); Restore(confirmed.State);
        Assert.Null(confirmed.State.PendingCardChoice); Assert.NotNull(confirmed.State.PriorityPlayerId);
        var opened = await ResolveTop(confirmed.State); Restore(opened.State);
        Assert.Equal(paying.State.RunePools["P2"].Mana - 1, opened.State.RunePools["P2"].Mana);
        Assert.Equal("P2", opened.State.PendingCardChoice!.PlayerId);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(opened.Prompts["P1"]));
        var done = await Choose(opened.State, recycle ? ["D1"] : []);
        var reveal = Assert.Single(done.Events, e => e.Kind == "CARD_REVEALED");
        Assert.Equal(recycle ? "D2" : "D1", reveal.Payload["cardObjectId"]);
        Assert.Equal(recycle ? 1 : 0, done.State.PlayerZones["P2"].Hand.Count);
        Assert.Equal(TimingStates.SpellDuelOpen, done.State.TimingState);
        Assert.Equal("P1", done.State.FocusPlayerId); Restore(done.State);
    }

    [Fact]
    public async Task DianaCanDeclineOrGenerateManaDuringConfirmationWithoutLosingParent()
    {
        var state = Diana() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(10, 0), ["P2"] = RunePool.Empty } };
        var moved = await Act(state, "P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        var pending = moved;
        var forged = pending.State with { PendingPayment = pending.State.PendingPayment! with { ManaCost = 0 } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("Insight"));
        var payment = pending.State.PendingPayment!;
        var rejected = await new CoreRuleEngine().ResolveAsync(pending.State, new("bad", "P2", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(pending.State), MatchStateHasher.Hash(rejected.State));
        var declined = await Pay(pending.State, "DECLINE");
        Assert.Empty(declined.State.StackItems); Assert.Null(declined.State.PendingCardChoice);
        Assert.DoesNotContain(declined.Events, e => e.Kind == "CARD_REVEALED"); Restore(declined.State);
        var tapped = await Act(pending.State, "P2", new TapRuneCommand("R"));
        Assert.NotNull(tapped.State.PendingPayment); Restore(tapped.State);
        var confirmed = await Pay(tapped.State, "PAY"); Assert.Null(confirmed.State.PendingCardChoice);
        var opened = await ResolveTop(confirmed.State); Assert.NotNull(opened.State.PendingCardChoice); Restore(opened.State);
    }

    [Theory]
    [InlineData("OTHER", false)]
    [InlineData("HILL", true)]
    public async Task DianaDoesNotTriggerFromAnotherBattlefieldOrFaceDown(string field, bool hidden)
    {
        var state = Diana();
        state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["DIANA"] = new("P2", "BATTLEFIELD", field) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["DIANA"] = state.CardObjects["DIANA"] with { IsFaceDown = hidden } } };
        var moved = await Act(state, "P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        Assert.Empty(moved.State.StackItems); Assert.Null(moved.State.PendingPayment);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task BlossomShortDeckStillExecutesDrawAndExperience(int count, bool recyclable)
    {
        var state = Blossom();
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { MainDeck = state.PlayerZones["P1"].MainDeck.Take(count).ToArray(), Graveyard = recyclable ? ["D3"] : [] } } };
        var activated = await Act(state, "P1", new ActivateAbilityCommand("B", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, []));
        var done = await ResolveTop(activated.State);
        if (count == 1) done = await Choose(done.State, []);
        var finished = count == 0 && !recyclable;
        Assert.Equal(finished ? 0 : 1, done.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(finished ? 0 : 1, done.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.Equal(finished ? 8 : count == 0 ? 1 : 0, done.State.PlayerScores["P2"]);
        Assert.Equal(finished ? "P2" : null, done.State.WinnerPlayerId);
        Restore(done.State);
    }

    [Fact]
    public async Task DianaEmptyDeckDoesNotBurnAndCapturedAbilitySurvivesSourceLeaving()
    {
        var moved = await Act(Diana(), "P1", new MoveUnitCommand("UNIT", Destination: "BATTLEFIELD:HILL", SourceObjectIds: ["UNIT"]));
        var paid = await Pay(moved.State, "PAY");
        var state = paid.State with { PlayerZones = new Dictionary<string, PlayerZones>(moved.State.PlayerZones) {
            ["P2"] = moved.State.PlayerZones["P2"] with { MainDeck = [], Hand = ["D1", "D2"], Battlefields = ["OTHER"] },
            ["P1"] = moved.State.PlayerZones["P1"] with { Graveyard = ["DIANA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(moved.State.ObjectLocations) { ["DIANA"] = new("P1", "GRAVEYARD"), ["D1"] = new("P2", "HAND"), ["D2"] = new("P2", "HAND") } };
        var done = await ResolveTop(state); Restore(done.State);
        Assert.Null(done.State.PendingCardChoice); Assert.Empty(done.State.StackItems);
        Assert.Equal(state.PlayerScores, done.State.PlayerScores);
        Assert.DoesNotContain(done.Events, e => e.Kind == "CARD_REVEALED" || e.Kind == "CARD_DRAWN");
        Restore(done.State);
    }

    internal static MatchState Blossom()
    {
        var state = OfficialInsightTriggerTests.Visionary();
        return state with { PlayerZones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Base = ["B"], MainDeck = ["D1", "D2", "D3"] }, ["P2"] = PlayerZones.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["B"] = new("B", cardNo: "UNL-136/219", ownerId: "P2", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState> { ["B"] = new("P1", "BASE") } };
    }
    internal static MatchState Diana(string cardNo = "UNL-079/219")
    {
        var state = BattlefieldLocalPermissionTests.Position("OGN·294/298", "P1", "HILL");
        return state with { PlayerZones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Base = ["UNIT"], Battlefields = ["HILL"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER", "DIANA"], MainDeck = ["D1", "D2"], Base = ["R"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HILL"] = state.CardObjects["HILL"] with { ControllerId = "P2" },
                ["DIANA"] = new("DIANA", cardNo: cardNo, ownerId: "P1", controllerId: "P2", power: 3, tags: [CardObjectTags.UnitCard]),
                ["D1"] = new("D1", cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
                ["D2"] = new("D2", cardNo: "OGN·083/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]),
                ["R"] = new("R", cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["UNIT"] = new("P1", "BASE"), ["DIANA"] = new("P2", "BATTLEFIELD", "HILL"), ["R"] = new("P2", "BASE") } };
    }
    internal static Task<ResolutionResult> Act(MatchState s, string p, GameCommand c) => OfficialInsightTriggerTests.Act(s, p, c);
    internal static Task<ResolutionResult> ResolveTop(MatchState s) => OfficialInsightTriggerTests.ResolveTop(s);
    internal static MatchState Restore(MatchState s) => OfficialInsightTriggerTests.Restore(s);
    internal static Task<ResolutionResult> Choose(MatchState s, string[] ids) => OfficialInsightTriggerTests.Choose(s, ids);
    internal static Task<ResolutionResult> Pay(MatchState s, string choice) => Act(s, s.PendingPayment!.PlayerId,
        new PayCostCommand(s.PendingPayment.PaymentId, s.PendingPayment.PaymentWindow, [choice]));
}
