using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialSettReplacementTests
{
    internal static MatchState Position(string legend = "OGN·269/298", int power = 1, int mana = 7, bool two = false)
    {
        var s = DeathAndDuelLifecycleTests.State();
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["BF"] = s.CardObjects["BF"] with { CardNo = "OGN·296/298" },
            ["D"] = s.CardObjects["D"] with { Power = 2, Damage = 1, Tags = [CardObjectTags.UnitCard, CardObjectTags.Boon] },
            ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P2", controllerId: "P2", tags: ["CARD_TYPE:LEGEND"]),
            ["AOE"] = new("AOE", cardNo: "OGN·133/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) };
        if (two) cards["D2"] = cards["D"] with { ObjectId = "D2" };
        var locations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations);
        if (two) locations["D2"] = new("P2", "BATTLEFIELD", "BF");
        return s with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20,20), ["P2"] = new(mana,power) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Hand = ["AOE"] },
                ["P2"] = s.PlayerZones["P2"] with { LegendZone = ["LEGEND"], Battlefields = two ? ["BF","D","D2"] : ["BF","D"] } } };
    }
    internal static async Task<ResolutionResult> Open(MatchState? initial = null, string spell = "OGN·133/298", params string[] targets)
    {
        var state = initial ?? Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["AOE"] = state.CardObjects["AOE"] with { CardNo = spell } } };
        var cast = await Act(state, "P1", new PlayCardCommand("AOE", spell, targets));
        var pass = await Act(cast.State, cast.State.PriorityPlayerId!, new PassPriorityCommand());
        var original = MatchStateHasher.Hash(pass.State);
        var opened = await Act(pass.State, pass.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(original, MatchStateHasher.Hash(pass.State));
        return opened;
    }
    internal static Task<ResolutionResult> Choose(MatchState s, string target = "D", string payment = "ANY")
    {
        var p = s.PendingRuleChoice!;
        var id = target == "DECLINE" ? target : p.Request.Options.Single(o => o.Id == $"SETT:{target}:LEGEND:{payment}").Id;
        return Act(s, p.Request.PlayerId, new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", [id]));
    }

    [Theory]
    [InlineData("OGN·269/298")]
    [InlineData("OGN·310/298")]
    [InlineData("OGN·310*/298")]
    public async Task OptionalReplacementPausesWithoutDamagePaymentOrDeathThenPaysPower(string no)
    {
        var opened = await Open(Position(no)); Assert.NotNull(opened.State.PendingRuleChoice);
        Assert.Equal(1, opened.State.CardObjects["D"].Damage); Assert.Equal(1, opened.State.RunePools["P2"].Power);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted); Assert.Empty(opened.State.PlayerZones["P2"].Graveyard);
        Assert.Single(opened.Events); Assert.Equal("RULE_CHOICE_REQUESTED", opened.Events[0].Kind); Restore(opened.State);
        Assert.False(opened.Prompts["P1"].Actionable); Assert.True(opened.Prompts["P2"].Actionable);
        Assert.DoesNotContain("pendingRuleChoice", JsonSerializer.Serialize(opened.Snapshots), StringComparison.OrdinalIgnoreCase);
        var done = await Choose(opened.State); Restore(done.State);
        Assert.Null(done.State.PendingRuleChoice); Assert.Equal(opened.State.Tick+1, done.State.Tick);
        Assert.Equal(7, done.State.RunePools["P2"].Mana); Assert.Equal(0, done.State.RunePools["P2"].Power);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base); Assert.True(done.State.CardObjects["D"].IsExhausted);
        Assert.Equal(1, done.State.CardObjects["D"].Power); Assert.Equal(0, done.State.CardObjects["D"].Damage);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["D"].Tags); Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_DESTROYED" && e.Payload.GetValueOrDefault("targetObjectId") as string=="D");
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue);
        Assert.Single(done.Events,e=>e.Kind=="COST_PAID"); Assert.Single(done.Events,e=>e.Kind=="UNIT_RECALLED_TO_BASE");
    }

    [Fact]
    public async Task DeclineStillDestroysAndQueuesSentinelDeathWithoutPaying()
    {
        var opened = await Open(); var done = await Choose(opened.State,"DECLINE"); Restore(done.State);
        Assert.Contains("D", done.State.PlayerZones["P2"].Graveyard); Assert.Equal(1, done.State.RunePools["P2"].Power);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Assert.Single(done.State.StackItems);
        var draw = await Top(done.State); Assert.Contains("DRAW",draw.State.PlayerZones["P2"].Hand);
    }

    [Theory]
    [InlineData("OGN·133/298")]
    [InlineData("UNL-180/219")]
    [InlineData("OGN·256/298")]
    public async Task SimultaneousDestructionLetsControllerSaveSecondUnit(string spell)
    {
        var opened = await Open(Position(two:true), spell, spell == "OGN·256/298" ? ["D","D2"] : []);
        Assert.Contains(opened.State.PendingRuleChoice!.Request.Options,o=>o.Id.Contains("SETT:D2:"));
        var done = await Choose(opened.State,"D2"); Restore(done.State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard); Assert.Contains("D2",done.State.PlayerZones["P2"].Base);
        Assert.Single(done.Events,e=>e.Kind=="UNIT_RECALLED_TO_BASE");
        var ev = done.Events.ToList(); Assert.True(ev.FindIndex(e=>e.Kind=="UNIT_RECALLED_TO_BASE") < ev.FindIndex(e=>e.Kind=="UNIT_DESTROYED"));
    }

    [Fact]
    public async Task ManaAloneCannotPayAndNoChoiceIsOffered()
    {
        var done = await Open(Position(power:0)); Assert.Null(done.State.PendingRuleChoice);
        Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard); Assert.Equal(7,done.State.RunePools["P2"].Mana);
    }

    [Fact]
    public async Task ForeignOrForgedChoiceRejectedWithoutMutation()
    {
        var opened = await Open(); var p=opened.State.PendingRuleChoice!; var hash=MatchStateHasher.Hash(opened.State);
        foreach(var (player,id) in new[]{("P1","DECLINE"),("P2","SETT:forged"),("P2","")}) {
            var result=await new CoreRuleEngine().ResolveAsync(opened.State,new("bad",player,CommandTypes.PayCost),new PayCostCommand(p.Request.Id,"RULE_REPLACEMENT",[id]),default);
            Assert.False(result.Accepted); Assert.Equal(hash,MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunePaymentWorksForActiveAndExhaustedRunes(bool exhausted)
    {
        var s = WithRune(Position(power: 0), exhausted);
        var opened = await Open(s); Restore(opened.State);
        var done = await Choose(opened.State, payment: "RUNE:RUNE"); Restore(done.State);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Contains("RUNE", done.State.PlayerZones["P2"].RuneDeck);
        Assert.DoesNotContain("RUNE", done.State.PlayerZones["P2"].Base);
        Assert.Equal(0, done.State.RunePools["P2"].TotalPower);
        Assert.Single(done.Events, e => e.Kind == "RUNE_RECYCLED");
    }
    internal static MatchState WithRune(MatchState s, bool exhausted = false) => s with {
        PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = ["RUNE"] } },
        CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["RUNE"] = new("RUNE", cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2", isExhausted: exhausted, tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["RUNE"] = new("P2", "BASE") } };

    [Fact]
    public async Task ColoredPowerChoicePreservesOtherColorsAndMana()
    {
        var s = Position(power: 0);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(7,0,new Dictionary<string,int> { ["red"] = 1, ["blue"] = 1 }) } };
        var done = await Choose((await Open(s)).State, payment: "TRAIT:blue");
        Assert.Equal(1, done.State.RunePools["P2"].PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(0, done.State.RunePools["P2"].PowerByTrait.GetValueOrDefault("blue"));
        Assert.Equal(7, done.State.RunePools["P2"].Mana); Restore(done.State);
    }

    [Fact]
    public async Task RecalledStolenUnitGoesToControllerBaseAndDoesNotChangeObjectGeneration()
    {
        var s = Position(); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { OwnerId = "P1", ObjectGeneration = 3 } } };
        var opened = await Open(s); Assert.Equal("P2",opened.State.PendingRuleChoice!.Request.PlayerId);
        var done = await Choose(opened.State); Restore(done.State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base); Assert.DoesNotContain("D",done.State.PlayerZones["P1"].Base);
        Assert.Equal("P1",done.State.CardObjects["D"].OwnerId); Assert.Equal(3,done.State.CardObjects["D"].ObjectGeneration);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_MOVED");
    }

    [Fact]
    public async Task CombatDamagePausesAndResumesBeforeDeaths()
    {
        var s = Position();
        var moved = await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var first = await Act(moved.State,"P1",new PassFocusCommand());
        var second = await Act(first.State,"P2",new PassFocusCommand());
        var battle = await Act(second.State,"P1",new DeclareBattleCommand("BF",["A"],["D"],["COMBAT_ASSIGNMENT"]));
        var opened = await Top(battle.State); Assert.NotNull(opened.State.PendingRuleChoice); Restore(opened.State);
        var done = await Choose(opened.State);
        Assert.True(OfficialInsightAndSpellLockTests.Errors(done.State).Count == 0, string.Join("; ",OfficialInsightAndSpellLockTests.Errors(done.State))); Restore(done.State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base); Assert.Equal(0,done.State.CardObjects["A"].Damage);
        Assert.Contains(done.Events,e=>e.Kind=="BATTLE_CLOSED");
        Assert.Equal("P1",done.State.BattleResolutions[0].WinnerPlayerId);
        Assert.Empty(done.State.BattleResolutions[0].SurvivingDefenderObjectIds);
        Assert.False(done.State.CardObjects["D"].IsDefending);
    }

    [Fact]
    public async Task ReplacedDestructionStillPaysAdditionalCostAndOriginalPaymentOccursOnce()
    {
        var s = Position();
        s = s with { ActivePlayerId = "P2", TurnPlayerId = "P2", RunePools = new Dictionary<string,RunePool>(s.RunePools) { ["P2"] = new(20,5) },
            CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { ["SPELL"] = s.CardObjects["SPELL"] with { CardNo = "OGN·208/298", Tags = [CardObjectTags.UnitCard] } } };
        var opened = await Act(s,"P2",new PlayCardCommand("SPELL","OGN·208/298",[],OptionalCosts:["DESTROY_FRIENDLY_UNIT:D"]));
        Assert.NotNull(opened.State.PendingRuleChoice); Assert.Equal(20,opened.State.RunePools["P2"].Mana);
        Assert.Contains("SPELL",opened.State.PlayerZones["P2"].Hand); Restore(opened.State);
        var done = await Choose(opened.State); Restore(done.State);
        Assert.Contains("SPELL",done.State.PlayerZones["P2"].Base); Assert.Contains("D",done.State.PlayerZones["P2"].Base);
        Assert.Equal(16,done.State.RunePools["P2"].Mana); Assert.Empty(done.State.TriggerQueue);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_DESTROYED");
    }

    [Fact]
    public async Task RecoveryRejectsAlteredChoicesOriginAndTranscript()
    {
        var s = (await Open()).State; var p = s.PendingRuleChoice!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { PendingRuleChoice = p with {
            Request = p.Request with { Options = [new("DECLINE","forged")] } } }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { PendingRuleChoice = p with {
            Answers = [new("forged","DECLINE")] } }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { RunePools = new Dictionary<string,RunePool>(s.RunePools) { ["P2"] = new(99,99) } }));
    }

    [Fact]
    public async Task BothControllersChooseInTurnOrderAndCommitOnlyAfterBothDecisions()
    {
        var s = Position(); s = s with { PlayerZones = new Dictionary<string,PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["L1"] } },
            CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["A"] = s.CardObjects["A"] with { Tags = [CardObjectTags.UnitCard,CardObjectTags.Boon] },
                ["L1"] = s.CardObjects["LEGEND"] with { ObjectId = "L1", OwnerId = "P1", ControllerId = "P1" } } };
        var opened = await Open(s,"UNL-180/219"); var p=opened.State.PendingRuleChoice!; Assert.Equal("P1",p.Request.PlayerId);
        var first = await Act(opened.State,"P1",new PayCostCommand(p.Request.Id,"RULE_REPLACEMENT",["SETT:A:L1:ANY"]));
        Restore(first.State); Assert.Equal("P2",first.State.PendingRuleChoice!.Request.PlayerId);
        Assert.Single(first.State.PendingRuleChoice.Answers); Assert.False(first.State.CardObjects["L1"].IsExhausted);
        Assert.Single(first.Events); Assert.Equal("RULE_CHOICE_REQUESTED",first.Events[0].Kind);
        var restored = OfficialInsightAndSpellLockTests.Restore(first.State);
        var done = await Choose(restored); Restore(done.State);
        Assert.True(done.State.CardObjects["L1"].IsExhausted); Assert.True(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Contains("A",done.State.PlayerZones["P1"].Base); Assert.Contains("D",done.State.PlayerZones["P2"].Base);
        Assert.Equal(2,done.Events.Count(e=>e.Kind=="COST_PAID")); Assert.Equal(2,done.Events.Count(e=>e.Kind=="UNIT_RECALLED_TO_BASE"));
        Assert.Equal(first.State.Tick+1,done.State.Tick);
    }

    [Fact]
    public async Task ColoredUnitCannotBeUsedAsRunePayment()
    {
        var s=WithRune(Position(power:0)); s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["RUNE"]=s.CardObjects["RUNE"] with { CardNo="SFD·125/221",Tags=[CardObjectTags.UnitCard,"COLOR:red"] } } };
        var done=await Open(s); Assert.Null(done.State.PendingRuleChoice); Assert.Contains("RUNE",done.State.PlayerZones["P2"].Base);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReplacingDestructionDoesNotCancelFollowingControllerDraw(bool replace)
    {
        var s = Position(); s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with { OwnerId="P1" },
            ["DRAW2"]=s.CardObjects["DRAW"] with { ObjectId="DRAW2" } },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"]=s.PlayerZones["P2"] with { MainDeck=["DRAW","DRAW2"] } } };
        var opened=await Open(s,"OGN·213/298","D");
        var done=await Choose(opened.State,replace?"D":"DECLINE"); Restore(done.State);
        Assert.Contains("DRAW",done.State.PlayerZones["P2"].Hand); Assert.Contains("DRAW2",done.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("DRAW",done.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task PlayerAndSpectatorRecoveryViewsMatchPendingDecisionWithoutExposingCheckpoint()
    {
        var opened = await Open(); var s = opened.State;
        var views = s.Seats.Keys.ToDictionary(id=>id, id=>new RecoveredPlayerView(id,s.Tick,0,opened.Snapshots[id],s.Tick,0,opened.Prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        var errors = MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator);
        Assert.True(errors.Count==0,string.Join("; ",errors));
        var publicJson = JsonSerializer.Serialize(new { views, spectator });
        Assert.DoesNotContain("pendingRuleChoice",publicJson,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Origin",publicJson,StringComparison.Ordinal);
        var session = new MatchSession(OfficialInsightAndSpellLockTests.Restore(s),new CoreRuleEngine(),NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(opened.Prompts["P2"]),MatchStateHasher.HashValue(session.PromptFor("P2")));
    }

}
