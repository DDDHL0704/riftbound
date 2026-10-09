using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialSpellCompletionTests
{
    [Fact]
    public async Task SpellPlayTriggersWaitForCompletionAndRespondIndependently()
    {
        var state = Position("OGN·114/298");
        var played = await Act(state, "P1", new PlayCardCommand("C", "OGN·114/298", []));
        Assert.Single(played.State.StackItems);
        Assert.Empty(played.State.TriggerQueue);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, played.State.CardObjects["UNIT"].Power);
        Restore(played.State);
        var completed = await Top(played.State);
        Assert.True(completed.State.TriggerQueue.Count == 3, string.Join("; ", completed.State.TriggerQueue.Select(t => t.SourceObjectId + ":" + t.EffectKind + ":" + t.SpellContext?.Kind)));
        Assert.Equal(4, completed.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(2, completed.State.CardObjects["UNIT"].Power);
        Restore(completed.State);
        var finished = await Drain(completed.State);
        Assert.Equal(5, finished.PlayerZones["P1"].Hand.Count);
        Assert.Equal(3, finished.CardObjects["UNIT"].Power);
        Restore(finished);
    }

    [Fact]
    public async Task CounteredSpellNeverCreatesPlayTriggers()
    {
        var state = Position("OGN·114/298");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            { ["P2"] = state.PlayerZones["P2"] with { Hand = ["COUNTER"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                { ["COUNTER"] = new("COUNTER", cardNo: "OGN·064/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["COUNTER"] = new("P2", "HAND") } };
        var played = await Act(state, "P1", new PlayCardCommand("C", "OGN·114/298", []));
        var passed = await Act(played.State, "P1", new PassPriorityCommand());
        var counter = await Act(passed.State, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [played.State.StackItems[0].StackItemId]));
        var done = await Top(counter.State);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue);
        Assert.Empty(done.State.PlayerZones["P1"].Hand); Assert.Equal(2, done.State.CardObjects["UNIT"].Power);
        Assert.Contains("C", done.State.PlayerZones["P1"].Graveyard); Restore(done.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PaidManaExileIsOptionalRespondableAndOwned(bool accept)
    {
        var state = Position("OGN·083/298", "UNL-181/219", false);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            { ["C"] = state.CardObjects["C"] with { OwnerId = "P2" } } };
        var played = await Act(state, "P1", new PlayCardCommand("C", "OGN·083/298", []));
        Assert.Equal(4, played.State.StackItems[0].PlayCost!.PaidMana);
        var completed = await Top(played.State);
        Assert.Equal("SPELL_TRIGGER_CONFIRMATION", completed.State.PendingCardChoice!.ChoiceWindow);
        Assert.Contains("C", completed.State.PlayerZones["P2"].Graveyard);
        Restore(completed.State);
        var selected = await Choose(completed.State, accept ? ["C"] : []);
        if (!accept) { Assert.Empty(selected.State.StackItems); Assert.Empty(selected.State.LinkedExiles); return; }
        Assert.Contains("C", selected.State.PlayerZones["P2"].Graveyard);
        Restore(selected.State);
        var done = await Top(selected.State);
        Assert.Contains("C", done.State.PlayerZones["P2"].Banished);
        Assert.DoesNotContain("C", done.State.PlayerZones["P1"].Banished);
        Assert.Equal("P1", Assert.Single(done.State.LinkedExiles).ControllerId);
        Restore(done.State);
    }

    [Fact]
    public async Task ReducedHighPrintedSpellDoesNotQualifyForPaidManaTrigger()
    {
        var state = Position("OGN·114/298", "UNL-181/219", false) with
            { UntilEndOfTurnEffects = ["RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE"] };
        var played = await Act(state, "P1", new PlayCardCommand("C", "OGN·114/298", []));
        Assert.Equal(new CardPlayCostReceipt(6, 1), played.State.StackItems[0].PlayCost);
        var done = await Top(played.State);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Contains("C", done.State.PlayerZones["P1"].Graveyard); Restore(done.State);
    }

    [Fact]
    public async Task EchoPaysOnceAndProducesOneCompletedPlay()
    {
        var state = Position("OGN·064/298", "UNL-181/219", false);
        var original = OfficialCounterRepeatTests.Position("OGN·064/298");
        state = state with { StackItems = original.StackItems, PriorityPlayerId = "P1", TimingState = TimingStates.NeutralClosed };
        var played = await Act(state, "P1", new PlayCardCommand("C", "OGN·064/298", ["S1"], OptionalCosts: ["ECHO"]));
        Assert.Equal(6, played.State.StackItems[^1].PlayCost!.PaidMana);
        var done = await Top(played.State);
        Assert.Single(done.State.StackItems, s => s.SpellContext is not null);
        Assert.NotNull(done.State.PendingCardChoice);
        Restore(done.State);
    }

    [Fact]
    public async Task ForgedCompletionContextAndReceiptAreRejectedOnRecovery()
    {
        var played = await Act(Position("OGN·083/298", "UNL-181/219", false), "P1", new PlayCardCommand("C", "OGN·083/298", []));
        var item = played.State.StackItems.Single();
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(played.State with
            { StackItems = [item with { PlayCost = new(4, -1) }] }), e => e.Contains("receipt"));
        var completed = await Top(played.State); var trigger = completed.State.StackItems.Single();
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(completed.State with
            { StackItems = [trigger with { SpellContext = trigger.SpellContext! with { Cost = new(4, 0) } }] }), e => e.Contains("spell"));
    }

    [Fact]
    public async Task SelfExilingSpellDoesNotOfferAnotherExile()
    {
        var played = await Act(Position("OGN·122/298", "UNL-181/219", false), "P1", new PlayCardCommand("C", "OGN·122/298", []));
        var done = await Top(played.State);
        Assert.Contains("C", done.State.PlayerZones["P1"].Banished);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice); Assert.Empty(done.State.LinkedExiles);
        Restore(done.State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LinkedExileUsesObjectGenerationsAndReturnsEachCardToItsOwner(bool staleCard, bool newSource)
    {
        var state = Position("OGN·083/298", "UNL-181/219", false);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        var refs = new Dictionary<string,long>();
        foreach (var id in new[]{"OLD1", "OLD2", "OLD3"})
        {
            var owner = id=="OLD3" ? "P1" : "P2";
            cards[id]=new(id,cardNo:"OGN·083/298",ownerId:owner,controllerId:owner,tags:[CardObjectTags.SpellCard]);
            locations[id]=new(owner,"BANISHED"); refs[id]=0;
        }
        if(staleCard)cards["OLD1"]=cards["OLD1"] with {ObjectGeneration=1};
        if(newSource)cards["LEGEND"]=cards["LEGEND"] with {ObjectGeneration=1};
        foreach(var id in new[]{"R1","R2","R3","R4"})
        { cards[id]=new(id,cardNo:"UNL-R03",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.RuneCard,"COLOR:blue"]); locations[id]=new("P1","RUNE_DECK"); }
        state=state with {CardObjects=cards,ObjectLocations=locations, LinkedExiles=[new("LEGEND",0,"P1",refs)],
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Banished=["OLD3"],RuneDeck=["R1","R2","R3","R4"]},["P2"]=state.PlayerZones["P2"] with {Banished=["OLD1","OLD2"]}}};
        Restore(state);
        var played=await Act(state,"P1",new PlayCardCommand("C","OGN·083/298",[]));
        var completed=await Top(played.State); var accepted=await Choose(completed.State,["C"]);
        var result=await Top(accepted.State); Restore(result.State);
        if(staleCard || newSource)
        {
            Assert.Contains("C",result.State.PlayerZones["P1"].Banished);
            Assert.Equal(4,result.State.PlayerZones["P1"].RuneDeck.Count);
            Assert.Equal(2,result.State.PlayerZones["P1"].Hand.Count);
        }
        else
        {
            Assert.Empty(result.State.LinkedExiles);
            Assert.Equal(new[]{"OLD1","OLD2"},result.State.PlayerZones["P2"].Graveyard.Order().ToArray());
            Assert.Contains("OLD3",result.State.PlayerZones["P1"].Graveyard); Assert.Contains("C",result.State.PlayerZones["P1"].Graveyard);
            Assert.Empty(result.State.PlayerZones["P1"].RuneDeck); Assert.Equal(4,result.State.PlayerZones["P1"].Base.Count);
            Assert.Equal(3,result.State.PlayerZones["P1"].Hand.Count);
        }
    }

    [Fact]
    public async Task PrivateSpellChoiceMustFinishBeforePlayTriggersAreQueued()
    {
        var state=Position("UNL-131/219","UNL-181/219",false);
        var original=OfficialCounterRepeatTests.Position("UNL-131/219");
        state=state with {StackItems=original.StackItems,PriorityPlayerId="P1",TimingState=TimingStates.NeutralClosed};
        var played=await Act(state,"P1",new PlayCardCommand("C","UNL-131/219",["S1"]));
        var paused=await Top(played.State);
        Assert.Equal("INSIGHT",paused.State.PendingCardChoice!.ChoiceWindow);
        Assert.DoesNotContain(paused.State.StackItems,i=>i.SpellContext is not null); Restore(paused.State);
        var finished=await Choose(paused.State,[]);
        Assert.Contains("C",finished.State.PlayerZones["P1"].Graveyard);
        // Abandon costs 3, so the paid-mana threshold is not reached.
        Assert.DoesNotContain(finished.State.StackItems,i=>i.SpellContext is not null); Restore(finished.State);
    }

    [Theory]
    [InlineData("OGN·103/298", 1)]
    [InlineData("UNL-149/219", 2)]
    [InlineData("UNL-149a/219", 2)]
    [InlineData("OGS·006/024", 3)]
    public async Task ParsedSourceFamiliesShareCompletionAndResponse(string sourceCard, int delta)
    {
        var state=Position("OGN·114/298");
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["UNIT"]=state.CardObjects["UNIT"] with {CardNo=sourceCard}}};
        var played=await Act(state,"P1",new PlayCardCommand("C","OGN·114/298",[]));
        Assert.Equal(2,played.State.CardObjects["UNIT"].Power);
        var completed=await Top(played.State); Restore(completed.State);
        Assert.Equal(2,completed.State.CardObjects["UNIT"].Power);
        var final=await Drain(completed.State); Assert.Equal(2+delta,final.CardObjects["UNIT"].Power); Restore(final);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    [InlineData(null)]
    public async Task DreamTreeErratumTriggersAtTargetSelectionRegardlessOfBattlefieldController(string? controller)
    {
        var state=ConformanceFixtureRunnerTests.BattlefieldFriendlySpellDrawState();
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["P1-BATTLEFIELD-DREAMTREE"]=state.CardObjects["P1-BATTLEFIELD-DREAMTREE"] with {ControllerId=controller}}};
        state=state with {TimingState=TimingStates.SpellDuelOpen,FocusPlayerId="P1",
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["OPP"]=new("OPP",cardNo:"SFD·001/221",ownerId:"P2",controllerId:"P2",power:3,tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with {Battlefields=["OPP"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["OPP"]=new("P2","BATTLEFIELD","P1-BATTLEFIELD-DREAMTREE")}};
        var played=await Act(state,"P1",new PlayCardCommand("P1-SPELL-SAVAGE-STRENGTH","SFD·034/221",["P1-BATTLEFIELD-ALLY"]));
        Assert.Equal(2,played.State.StackItems.Count); Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Equal(TriggerKinds.BattlefieldFriendlySpellDraw,played.State.StackItems[^1].SpellContext!.Kind);
        var drawn=await Top(played.State);
        Assert.Single(drawn.State.StackItems); Assert.Contains("P1-MAIN-DRAWN",drawn.State.PlayerZones["P1"].Hand);
        Assert.Equal(2,drawn.State.CardObjects["P1-BATTLEFIELD-ALLY"].Power);
        Restore(drawn.State);
    }

    [Theory]
    [InlineData(true, "P1")]
    [InlineData(false, "P1")]
    [InlineData(true, "P2")]
    [InlineData(false, "P2")]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public async Task WasteHallLetsCasterChooseAfterSpellCompletes(bool accept, string? controller)
    {
        var state=ConformanceFixtureRunnerTests.BattlefieldSpellPowerBonusState();
        state=state with {TimingState=TimingStates.SpellDuelOpen,FocusPlayerId="P1",
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["P1-BATTLEFIELD-WASTE-HALL"]=state.CardObjects["P1-BATTLEFIELD-WASTE-HALL"] with {ControllerId=controller},
                ["OPP"]=new("OPP",cardNo:"SFD·001/221",ownerId:"P2",controllerId:"P2",power:3,tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with {Battlefields=["OPP"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["OPP"]=new("P2","BATTLEFIELD","P1-BATTLEFIELD-WASTE-HALL")}};
        var played=await Act(state,"P1",new PlayCardCommand("P1-SPELL-SAVAGE-STRENGTH","SFD·034/221",["P1-BATTLEFIELD-ALLY"]));
        Assert.Single(played.State.StackItems); Assert.Null(played.State.PendingCardChoice);
        var complete=await Top(played.State);
        Assert.Equal(4,complete.State.CardObjects["P1-BATTLEFIELD-ALLY"].Power);
        Assert.Equal("SPELL_TRIGGER_CONFIRMATION",complete.State.PendingCardChoice!.ChoiceWindow); Restore(complete.State);
        var selected=await Choose(complete.State,accept?["P1-BATTLEFIELD-ALLY"]:[]);
        var final=accept?(await Top(selected.State)).State:selected.State;
        Assert.Equal(accept?5:4,final.CardObjects["P1-BATTLEFIELD-ALLY"].Power); Restore(final);
    }

    internal static MatchState Position(string card, string legend = "OGS·021/024", bool withSources = true)
    {
        var state = OfficialInsightTriggerTests.Library(card);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        cards["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["LEGEND_CARD"]);
        cards["UNIT"] = new("UNIT", cardNo: "OGN·103/298", ownerId: "P1", controllerId: "P1", power: 2, tags: [CardObjectTags.UnitCard]);
        locations["LEGEND"] = new("P1", "LEGEND"); locations["UNIT"] = new("P1", "BASE");
        var deck = Enumerable.Range(1, 12).Select(i => "D" + i).ToArray();
        foreach (var id in deck) { cards[id] = new(id, cardNo: "SFD·106/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]); locations[id] = new("P1", "MAIN_DECK"); }
        if (!withSources) { cards.Remove("LIB"); cards.Remove("UNIT"); locations.Remove("LIB"); locations.Remove("UNIT"); }
        return state with { StackItems = [], PriorityPlayerId = null, TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                { ["P1"] = state.PlayerZones["P1"] with { Base = withSources ? ["UNIT"] : [], LegendZone = ["LEGEND"], MainDeck = deck },
                  ["P2"] = state.PlayerZones["P2"] with { Battlefields = withSources ? ["LIB"] : [] } } };
    }
    internal static Task<ResolutionResult> Act(MatchState s, string player, GameCommand command) => OfficialInsightAndSpellLockTests.Act(s, player, command);
    internal static Task<ResolutionResult> Top(MatchState s) => OfficialInsightAndSpellLockTests.ResolveTop(s);
    internal static Task<ResolutionResult> Choose(MatchState s, string[] ids) => OfficialInsightTriggerTests.Choose(s, ids);
    internal static void Restore(MatchState s) => OfficialInsightAndSpellLockTests.Restore(s);
    internal static async Task<MatchState> Drain(MatchState state)
    {
        for (var i = 0; i < 30 && (state.StackItems.Count > 0 || state.TriggerQueue.Count > 0); i++)
        {
            if (state.PendingCardChoice is not null) state = (await Choose(state, [])).State;
            else if (state.TriggerQueue.Count > 0) state = (await Act(state, state.TriggerQueue[0].ControllerId,
                new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray()))).State;
            else state = (await Top(state)).State;
        }
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue); return state;
    }
}
