using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialBattlefieldReplacementTests
{
    internal static MatchState Position(string legend = "UNL-195/219", string battlefield = "OGN·296/298")
    {
        var s = OfficialLeblancCreationTests.Position();
        var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["LEGEND"] = cards["LEGEND"] with { CardNo = legend };
        cards["BF"] = cards["BF"] with { CardNo = battlefield };
        return s with { CardObjects = cards };
    }
    internal static async Task<ResolutionResult> Conquer(MatchState state)
    {
        var r = await Act(state, "P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        r = await Act(r.State, "P1", new PassFocusCommand());
        return await Act(r.State, "P2", new PassFocusCommand());
    }
    internal static Task<ResolutionResult> Choose(MatchState state, params string[] ids)
        => Act(state, state.PendingCardChoice!.PlayerId, new ChooseCardsCommand(state.PendingCardChoice.ChoiceId, state.PendingCardChoice.ChoiceWindow, ids));
    internal static async Task<ResolutionResult> CreateBrush(MatchState state)
    {
        var r = await Conquer(state); r = await Choose(r.State, "LEGEND"); return await Top(r.State);
    }
    internal static MatchState NextHold(MatchState state) => state with { Phase = MatchPhases.TurnStart, TurnStartStep = null,
        TurnNumber = state.TurnNumber + 2, TimingState = TimingStates.NeutralClosed, ActivePlayerId = "P1", TurnPlayerId = "P1",
        UntilEndOfTurnEffects = [], PriorityPlayerId = null, PassedPriorityPlayerIds = [] };
    internal static MatchState WithoutLegend(MatchState state) => state with {
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = [] } },
        CardObjects = state.CardObjects.Where(e => e.Key != "LEGEND").ToDictionary(e => e.Key, e => e.Value),
        ObjectLocations = state.ObjectLocations.Where(e => e.Key != "LEGEND").ToDictionary(e => e.Key, e => e.Value) };

    [Theory]
    [InlineData("UNL-195/219")]
    [InlineData("UNL-233/219")]
    [InlineData("UNL-233*/219")]
    public async Task ConquestPaysBeforeResponsesAndReplacesOnePhysicalLocation(string no)
    {
        var initial = Position(no);
        var opened = await Conquer(initial);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", opened.State.PendingCardChoice!.ChoiceWindow);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(initial.CardObjects["BF"].CardNo, opened.State.CardObjects["BF"].CardNo);
        Restore(opened.State);
        var paid = await Choose(opened.State, "LEGEND");
        Assert.True(paid.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Empty(paid.State.PlayerZones["P2"].Banished);
        var before = paid.State;
        var done = await Top(before);
        AssertBrush(before, done.State);
        Assert.Contains(done.Events, e => e.Kind == "BATTLEFIELD_REPLACED");
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_TOKEN_CREATED" or "CARD_PLAYED" or "BATTLEFIELD_CONQUERED" or "CARD_BANISHED");
    }

    internal static void AssertBrush(MatchState before, MatchState after)
    {
        var field = after.CardObjects["BF"];
        Assert.Equal("UNL·T03", field.CardNo); Assert.Equal("UNL·T03", field.TokenFactoryCardNo);
        Assert.Equal(before.CardObjects["BF"].ObjectGeneration, field.ObjectGeneration);
        Assert.Equal(before.CardObjects["BF"].ControllerId, field.ControllerId);
        Assert.Equal(before.ObjectLocations["BF"], after.ObjectLocations["BF"]);
        Assert.Equal(before.ObjectLocations["UNIT"], after.ObjectLocations["UNIT"]);
        Assert.Equal(before.UntilEndOfTurnEffects, after.UntilEndOfTurnEffects);
        Assert.Equal(before.PlayerScores, after.PlayerScores);
        Assert.Equal(before.PlayerZones["P1"].Battlefields, after.PlayerZones["P1"].Battlefields);
        Assert.Equal(before.PlayerZones["P2"].Battlefields, after.PlayerZones["P2"].Battlefields);
        var origin = Assert.IsType<ObjectBinding>(field.ReplacedBattlefieldCard);
        Assert.Contains(origin.ObjectId, after.PlayerZones["P2"].Banished);
        Assert.Equal(before.CardObjects["BF"].CardNo, after.CardObjects[origin.ObjectId].CardNo);
        Assert.Equal("P2", after.CardObjects[origin.ObjectId].OwnerId);
        Restore(after);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeldCreationCanBeDeclinedAndEffectsWaitForResponses(bool accept)
    {
        var s = OfficialHoldSequenceTests.AddLegend(OfficialHoldSequenceTests.State("OGN·294/298"), "UNL-195/219");
        var r = await Act(s, "P1", new PassPriorityCommand());
        Assert.Equal("TRIGGER_COST_CONFIRMATION", r.State.PendingCardChoice!.ChoiceWindow);
        r = await Choose(r.State, accept ? ["LEGEND"] : []);
        Assert.Equal(accept, r.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal("OGN·294/298", r.State.CardObjects["F"].CardNo);
        if (accept) r = await Top(r.State);
        Assert.Equal(accept ? "UNL·T03" : "OGN·294/298", r.State.CardObjects["F"].CardNo);
        Restore(r.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealHoldScoreOffersReturnBeforeResponseAndDoesNotTriggerRestoredHold(bool accept)
    {
        var brush = await CreateBrush(Position(battlefield: "SFD·214/221"));
        var r = await Act(NextHold(WithoutLegend(brush.State)), "P1", new PassPriorityCommand());
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", r.State.PendingCardChoice!.ChoiceWindow);
        Assert.Null(r.State.PendingPayment); Restore(r.State);
        var scores = r.State.PlayerScores["P1"];
        r = await Choose(r.State, accept ? ["BF"] : []);
        Assert.Equal("UNL·T03", r.State.CardObjects["BF"].CardNo);
        if (accept) r = await Top(r.State);
        Assert.Equal(accept ? "SFD·214/221" : "UNL·T03", r.State.CardObjects["BF"].CardNo);
        Assert.Equal(scores, r.State.PlayerScores["P1"]);
        Assert.Null(r.State.PendingPayment); Assert.Null(r.State.PendingCardChoice);
        Assert.Equal(accept ? 0 : 1, r.State.PlayerZones["P2"].Banished.Count);
        if (accept) { Assert.Null(r.State.CardObjects["BF"].TokenFactoryCardNo); Assert.Equal("P2", r.State.CardObjects["BF"].OwnerId); }
        Restore(r.State);
    }

    [Fact]
    public async Task ReplacingBrushAgainKeepsOriginalCardAndReturnLink()
    {
        var brush = await CreateBrush(Position());
        var original = brush.State.CardObjects["BF"].ReplacedBattlefieldCard;
        var opened = await Act(NextHold(brush.State), "P1", new PassPriorityCommand());
        Assert.Equal(2, opened.State.TriggerQueue.Count);
        // Confirmation order is reverse of the submitted resolution order.
        var order = opened.State.TriggerQueue.OrderBy(t => t.HeldContext!.Kind == "IVERN" ? 0 : 1).Select(t => t.TriggerId).ToArray();
        var r = await Act(opened.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", r.State.PendingCardChoice!.ChoiceWindow);
        r = await Choose(r.State); // Keep brush now; confirm Ivern's second replacement.
        r = await Choose(r.State, "LEGEND"); r = await Top(r.State);
        Assert.Equal(original, r.State.CardObjects["BF"].ReplacedBattlefieldCard);
        Assert.Single(r.State.PlayerZones["P2"].Banished);
        Restore(r.State);
        r = await Act(NextHold(WithoutLegend(r.State)), "P1", new PassPriorityCommand());
        r = await Choose(r.State, "BF"); r = await Top(r.State);
        Assert.Equal("OGN·296/298", r.State.CardObjects["BF"].CardNo);
        Assert.Empty(r.State.PlayerZones["P2"].Banished); Restore(r.State);
    }

    [Fact]
    public async Task ReplacementPreservesOccupantsAndLocalAuraSwitchesOnAndOff()
    {
        var s = Position(); var cards = s.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["UNIT"] = cards["UNIT"] with { Tags = [CardObjectTags.UnitCard, "鸟类"], Damage = 1, Power = 3 };
        s = OfficialHoldSequenceTests.AddUnit(s with { CardObjects = cards }, "ELSEWHERE", "OGN·096/298", "OTHER", "P2");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ELSEWHERE"] = s.CardObjects["ELSEWHERE"] with { Tags = [CardObjectTags.UnitCard, "鸟类"] } } };
        var brush = await CreateBrush(s);
        Assert.Contains(brush.State.ContinuousEffects, e => e.SourceObjectId == "BF" && e.TargetObjectId == "UNIT" && e.PowerDelta == 1);
        Assert.DoesNotContain(brush.State.ContinuousEffects, e => e.SourceObjectId == "BF" && e.TargetObjectId == "ELSEWHERE");
        Assert.Equal(1, brush.State.CardObjects["UNIT"].Damage);
        var r = await Act(NextHold(WithoutLegend(brush.State)), "P1", new PassPriorityCommand());
        r = await Choose(r.State, "BF"); r = await Top(r.State);
        Assert.DoesNotContain(r.State.ContinuousEffects, e => e.SourceCardNo == "UNL·T03");
    }

    [Fact]
    public async Task RecoveryRejectsForgedOriginAndWrongPlayerReturn()
    {
        var brush = await CreateBrush(Position()); var field = brush.State.CardObjects["BF"];
        foreach (var origin in new[] { new ObjectBinding("H1", 0), field.ReplacedBattlefieldCard! with { Generation = 99 } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(brush.State with { CardObjects = new Dictionary<string, CardObjectState>(brush.State.CardObjects) {
                ["BF"] = field with { ReplacedBattlefieldCard = origin } } }), e => e.Contains("replaced battlefield"));
        var pending = await Act(NextHold(WithoutLegend(brush.State)), "P1", new PassPriorityCommand());
        var bad = await new CoreRuleEngine().ResolveAsync(pending.State, new("wrong", "P2", CommandTypes.ChooseCards),
            new ChooseCardsCommand(pending.State.PendingCardChoice!.ChoiceId, pending.State.PendingCardChoice.ChoiceWindow, ["BF"]), default);
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(pending.State), MatchStateHasher.Hash(bad.State));
    }    [Fact]
    public async Task AlreadyCapturedBattlefieldEffectStillUsesSameLocationAfterReplacement()
    {
        var r = await Conquer(Position(battlefield: "OGN·283/298"));
        r = await Choose(r.State, "LEGEND");
        var prior = new StackItemState("OLDER-HOLD", "P1", "BF", "HOLD_BOON", "OGN·283/298") {
            TargetObjectIds = ["UNIT"], TargetGenerations = new Dictionary<string, long> { ["UNIT"] = r.State.CardObjects["UNIT"].ObjectGeneration },
            HeldContext = new("OGN·283/298", "BF", "BOON", 1, r.State.CardObjects["BF"].ObjectGeneration) };
        r = await Top(r.State with { StackItems = new[] { prior }.Concat(r.State.StackItems).ToArray() });
        Assert.Equal("UNL·T03", r.State.CardObjects["BF"].CardNo);
        r = await Top(r.State);
        Assert.Null(r.State.PendingCardChoice);
        Assert.Contains("增益", r.State.CardObjects["UNIT"].Tags);
        Restore(r.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExhaustedOrOpponentControlledLegendCannotPayOwnConquest(bool opponent)
    {
        var s = Position(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["LEGEND"] = s.CardObjects["LEGEND"] with { IsExhausted = !opponent, ControllerId = opponent ? "P2" : "P1" } } };
        var r = await Conquer(s);
        Assert.Null(r.State.PendingCardChoice); Assert.Empty(r.State.StackItems);
        Assert.Equal("OGN·296/298", r.State.CardObjects["BF"].CardNo);
    }

    [Fact]
    public async Task PaidCreationSurvivesLegendDepartureAndChangedFieldController()
    {
        var r = await Conquer(Position()); r = await Choose(r.State, "LEGEND");
        var changed = WithoutLegend(r.State);
        changed = changed with { CardObjects = new Dictionary<string, CardObjectState>(changed.CardObjects) {
            ["BF"] = changed.CardObjects["BF"] with { ControllerId = "P2", Damage = 3, IsExhausted = true, UntilEndOfTurnEffects = ["INHERITED"] } } };
        r = await Top(changed);
        Assert.Equal("UNL·T03", r.State.CardObjects["BF"].CardNo);
        Assert.Equal(3, r.State.CardObjects["BF"].Damage); Assert.True(r.State.CardObjects["BF"].IsExhausted);
        Assert.Contains("INHERITED", r.State.CardObjects["BF"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(r.Events, e => e.Kind == "LEGEND_EXHAUSTED");
        Restore(r.State);
    }

    [Fact]
    public async Task NaturalConquestReturnDoesNotTriggerRestoredMonastery()
    {
        var brush = await CreateBrush(Position(battlefield: "OGN·282/298"));
        var s = WithoutLegend(brush.State);
        s = s with { UntilEndOfTurnEffects = [],
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
                Base = s.PlayerZones["P1"].Base.Append("UNIT").ToArray(), Battlefields = s.PlayerZones["P1"].Battlefields.Where(id => id != "UNIT").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["BF"] = s.CardObjects["BF"] with { ControllerId = null },
                ["UNIT"] = s.CardObjects["UNIT"] with { IsExhausted = false, Tags = s.CardObjects["UNIT"].Tags.Append("增益").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
        var r = await Conquer(s);
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", r.State.PendingCardChoice!.ChoiceWindow);
        var hand = r.State.PlayerZones["P1"].Hand.ToArray();
        r = await Choose(r.State, "BF"); r = await Top(r.State);
        Assert.Equal("OGN·282/298", r.State.CardObjects["BF"].CardNo);
        Assert.Contains("增益", r.State.CardObjects["UNIT"].Tags);
        Assert.Equal(hand, r.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(r.Events, e => e.Kind == "BATTLEFIELD_CONQUERED"); Restore(r.State);
    }

    [Fact]
    public async Task UnitSkillScoreOnBrushDoesNotCountAsScoringAtBattlefield()
    {
        var r = await CreateBrush(Position());
        var s = OfficialHoldSequenceTests.AddUnit(r.State, "RENATA", "SFD·088/221", "BF", "P1");
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { ["blue"] = 4 }) } };
        var before = s.PlayerScores["P1"];
        r = await Act(s, "P1", new ActivateAbilityCommand("RENATA", P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId, []));
        r = await Top(r.State);
        Assert.Equal(before + 1, r.State.PlayerScores["P1"]);
        Assert.Null(r.State.PendingCardChoice); Assert.Empty(r.State.StackItems);
        Assert.Equal("UNL·T03", r.State.CardObjects["BF"].CardNo); Restore(r.State);
    }

    [Fact]
    public async Task WinningHoldFinishesWithoutReturnPrompt()
    {
        var r = await CreateBrush(Position());
        var s = NextHold(WithoutLegend(r.State)) with { PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 } };
        r = await Act(s, "P1", new PassPriorityCommand());
        Assert.Equal(MatchStatuses.Finished, r.State.Status); Assert.Equal("P1", r.State.WinnerPlayerId);
        Assert.Null(r.State.PendingCardChoice); Assert.Empty(r.State.StackItems);
    }

    [Fact]
    public async Task SecondPlayerCanReturnOpponentsOriginalCardAfterScoring()
    {
        var brush = await CreateBrush(Position());
        var json = System.Text.Json.JsonSerializer.Serialize(NextHold(WithoutLegend(brush.State)))
            .Replace("P1", "SWAP_PLAYER").Replace("P2", "P1").Replace("SWAP_PLAYER", "P2");
        var s = System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!;
        Restore(s);
        var r = await Act(s, "P2", new PassPriorityCommand());
        Assert.Equal("P2", r.State.PendingCardChoice!.PlayerId);
        r = await Choose(r.State, "BF"); r = await Top(r.State);
        Assert.Equal("OGN·296/298", r.State.CardObjects["BF"].CardNo);
        Assert.Equal("P1", r.State.CardObjects["BF"].OwnerId);
        Assert.Equal("P2", r.State.CardObjects["BF"].ControllerId);
        Assert.Empty(r.State.PlayerZones["P1"].Banished); Restore(r.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimultaneousReturnAndReplacementRespectResolutionOrder(bool replaceFirst)
    {
        var brush = await CreateBrush(Position());
        var r = await Act(NextHold(brush.State), "P1", new PassPriorityCommand());
        var order = r.State.TriggerQueue.OrderBy(t => (t.HeldContext!.Kind == "IVERN") == replaceFirst ? 0 : 1).Select(t => t.TriggerId).ToArray();
        r = await Act(r.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        for (var n = 0; n < 2; n++) r = await Choose(r.State, r.State.PendingCardChoice!.SourceObjectId);
        r = await Top(r.State); Restore(r.State);
        r = await Top(r.State); Restore(r.State);
        Assert.Equal(replaceFirst ? "OGN·296/298" : "UNL·T03", r.State.CardObjects["BF"].CardNo);
        Assert.Equal(replaceFirst ? 0 : 1, r.State.PlayerZones["P2"].Banished.Count);
        Assert.Empty(r.State.StackItems);
    }

}
