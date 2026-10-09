using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialInsightAndSpellLockTests
{
    [Theory]
    [InlineData("UNL-131/219", false)]
    [InlineData("UNL-131/219", true)]
    [InlineData("UNL-063/219", false)]
    [InlineData("UNL-063/219", true)]
    public async Task InsightIsPrivateAndOptionalAfterTheMainInstruction(string card, bool recycle)
    {
        var initial = Position(card);
        var played = await Act(initial, "P1", Play(card));
        Assert.Null(played.State.PendingCardChoice);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(played.Prompts));
        var opened = await ResolveTop(played.State);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        Assert.Equal(["D1"], choice.LegalObjectIds);
        Assert.DoesNotContain("C", opened.State.PlayerZones["P1"].Graveyard);
        if (card == "UNL-131/219") Assert.Contains("SPELL1", opened.State.PlayerZones["P2"].Hand);
        else Assert.Equal(1, opened.State.CardObjects["U"].Power);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(opened.Prompts["P2"]));
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(opened.Snapshots["P2"]));
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(opened.Events));
        var restored = Restore(opened.State);
        var forged = restored with { PendingCardChoice = choice with { LegalObjectIds = ["D2"] } };
        Assert.Contains(Errors(forged), e => e.Contains("Insight"));
        foreach (var (player, command) in new[] {
            ("P2", (GameCommand)new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", [])),
            ("P1", (GameCommand)new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", ["D2"])),
            ("P1", (GameCommand)new PassPriorityCommand()) })
        {
            var bad = await new CoreRuleEngine().ResolveAsync(restored, new("bad", player, command.CmdType), command, default);
            Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(restored), MatchStateHasher.Hash(bad.State));
        }
        var done = await Act(restored, "P1", new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", recycle ? ["D1"] : []));
        Assert.Null(done.State.PendingCardChoice);
        Assert.Contains("C", done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(recycle ? new[] { "D2", "D1" } : ["D1", "D2"], done.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(done.Events));
        Assert.DoesNotContain(done.Events, e => e.Kind == "CARD_DRAWN");
        Assert.Equal(initial.RngCursor, done.State.RngCursor);
        Restore(done.State);
    }

    [Fact]
    public async Task InsightUsesDeckAtResolutionAndHappensEvenIfTargetIsGone()
    {
        var played = await Act(Position("UNL-131/219"), "P1", Play("UNL-131/219"));
        var state = played.State with { StackItems = played.State.StackItems.Where(s=>s.StackItemId != "S1").ToArray(),
            PlayerZones = new Dictionary<string,PlayerZones>(played.State.PlayerZones) {
                ["P1"] = played.State.PlayerZones["P1"] with { MainDeck=["D2"], Hand=["D1"] },
                ["P2"] = played.State.PlayerZones["P2"] with { Graveyard=["SPELL1"] } } };
        var opened = await ResolveTop(state);
        Assert.Equal(["D2"], opened.State.PendingCardChoice!.ContextObjectIds);
        Assert.DoesNotContain(opened.Events, e=>e.Kind=="STACK_ITEM_COUNTERED");
        Restore(opened.State);
    }

    [Fact]
    public async Task EmptyDeckInsightDoesNotBurnOutOrSuspend()
    {
        var state = Position("UNL-131/219");
        state = state with { PlayerZones = new Dictionary<string,PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { MainDeck=[] } } };
        var played = await Act(state,"P1",Play("UNL-131/219"));
        var done = await ResolveTop(played.State);
        Assert.Null(done.State.PendingCardChoice); Assert.Contains("C",done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(state.PlayerScores, done.State.PlayerScores);
        Assert.DoesNotContain(done.Events,e=>e.Kind.Contains("BURN") || e.Kind=="CARD_DRAWN");
    }

    [Fact]
    public async Task EclipseLethalCleanupWaitsUntilInsightFinishes()
    {
        var state=Position("UNL-063/219");
        state=state with { CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects) { ["U"]=state.CardObjects["U"] with { Power=3, Damage=1 } } };
        var played=await Act(state,"P1",Play("UNL-063/219"));
        var opened=await ResolveTop(played.State);
        Assert.Contains("U",opened.State.PlayerZones["P2"].Base);
        var choice=opened.State.PendingCardChoice!;
        var done=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,"INSIGHT",[]));
        Assert.DoesNotContain("U",done.State.PlayerZones["P2"].Base);
        Assert.Contains("U",done.State.PlayerZones["P2"].Graveyard);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NightfallLocksCurrentControllerNotOwnerAndKeepsExistingSpells(bool stolen)
    {
        var state=Position("UNL-190/219");
        if(stolen) state=state with { StackItems=state.StackItems.Select(s=>s.StackItemId=="S1"?s with {ControllerId="P1"}:s).ToArray(),
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["SPELL1"]=state.CardObjects["SPELL1"] with {ControllerId="P1"}} };
        var played=await Act(state,"P1",Play("UNL-190/219"));
        var done=await ResolveTop(played.State);
        var locked=stolen?"P1":"P2";
        var marker=CardPermissionKeywordRules.SpellPlayProhibitionPrefix+locked;
        Assert.Contains(marker,done.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain(CardPermissionKeywordRules.SpellPlayProhibitionPrefix+(stolen?"P2":"P1"),done.State.UntilEndOfTurnEffects);
        Assert.Contains("SPELL1",done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(done.State.StackItems,s=>s.StackItemId=="S2");
        Restore(done.State);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·064/298",out var spell));
        var open=done.State with {StackItems=[],PriorityPlayerId=null,TimingState=TimingStates.NeutralOpen,TurnPlayerId=locked,ActivePlayerId=locked};
        Assert.False(CardPermissionKeywordRules.EvaluatePlayTiming(open,locked,spell).IsAllowed);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·125/221",out var unit));
        Assert.True(CardPermissionKeywordRules.EvaluatePlayTiming(open,locked,unit).IsAllowed);
        var alreadyQueued=await ResolveTop(done.State);
        Assert.DoesNotContain(alreadyQueued.State.StackItems,s=>s.StackItemId=="S2");
    }

    [Fact]
    public async Task NightfallEchoLocksBothTargetsAndRestrictionExpiresAtTurnEnd()
    {
        var state=Position("UNL-190/219");
        state=state with {StackItems=state.StackItems.Select(s=>s.StackItemId=="S2"?s with {ControllerId="P1"}:s).ToArray()};
        var played=await Act(state,"P1",new PlayCardCommand("C","UNL-190/219",["S1"],OptionalCosts:["ECHO"],RepeatChoices:[new("",["S2"])]));
        var done=await ResolveTop(played.State);
        Assert.All(new[]{"P1","P2"},p=>Assert.Contains(CardPermissionKeywordRules.SpellPlayProhibitionPrefix+p,done.State.UntilEndOfTurnEffects));
        var ended=await Act(done.State,"P1",new EndTurnCommand());
        Assert.DoesNotContain(ended.State.UntilEndOfTurnEffects,e=>e.StartsWith(CardPermissionKeywordRules.SpellPlayProhibitionPrefix));
    }

    [Fact]
    public async Task SpellLockFiltersPromptQuoteAndRejectsWithoutPayment()
    {
        var state=Position("UNL-190/219");
        state=state with {UntilEndOfTurnEffects=[CardPermissionKeywordRules.SpellPlayProhibitionPrefix+"P1"]};
        var command=Play("UNL-190/219");
        var quote=new CoreRuleEngine().PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.False(quote.CanPay); Assert.Contains("不能打出法术",quote.Message);
        var prompt=ResolutionResult.BuildPrompts(state)["P1"];
        Assert.DoesNotContain(prompt.Candidates??[],c=>c.Action==CommandTypes.PlayCard && c.Sources?.Any(s=>s.Id=="C")==true);
        var bad=await new CoreRuleEngine().ResolveAsync(state,new("bad","P1",command.CmdType),command,default);
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(bad.State));
    }

    internal static MatchState Position(string card)
    {
        var state=OfficialCounterRepeatTests.Position(card);
        if(card!="UNL-063/219") return state;
        return state with {StackItems=[],PriorityPlayerId=null,TimingState=TimingStates.NeutralOpen,
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["U"]=new("U",cardNo:"SFD·125/221",ownerId:"P2",controllerId:"P2",power:5,tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=PlayerZones.Empty with {Base=["U"]}} };
    }

    [Fact]
    public async Task StandbySpellCannotBypassNightfallRestriction()
    {
        var state=Position("OGN·083/298");
        state=state with {UntilEndOfTurnEffects=[],
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["C"]=state.CardObjects["C"] with {IsFaceDown=true,Tags=[CardObjectTags.SpellCard,CardObjectTags.Standby]}},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Hand=[],Base=["C"]}}};
        var command=new RevealCardCommand("C","OGN·083/298",[],"STANDBY_REACTION",["STANDBY_REVEAL_0"],"STACK");
        await Act(state,"P1",command);
        var locked=state with {UntilEndOfTurnEffects=[CardPermissionKeywordRules.SpellPlayProhibitionPrefix+"P1"]};
        var bad=await new CoreRuleEngine().ResolveAsync(locked,new("blocked-standby","P1",command.CmdType),command,default);
        Assert.False(bad.Accepted);Assert.Contains("不能打出法术",bad.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(locked),MatchStateHasher.Hash(bad.State));
        var prompt=ResolutionResult.BuildPrompts(locked)["P1"];
        Assert.Contains("不能打出法术",prompt.Reason);
        Assert.DoesNotContain(prompt.Candidates??[],c=>c.Action==CommandTypes.RevealCard && c.Sources?.Any(s=>s.Id=="C")==true);
    }
    internal static PlayCardCommand Play(string card)=>new("C",card,card=="UNL-063/219"?["U"]:["S1"]);
    internal static async Task<ResolutionResult> Act(MatchState state,string player,GameCommand command)
    {
        var result=await new CoreRuleEngine().ResolveAsync(state,new("action-"+state.Tick,player,command.CmdType),command,default);
        Assert.True(result.Accepted,result.ErrorMessage);return result;
    }
    internal static async Task<ResolutionResult> ResolveTop(MatchState state)
    {var first=await Act(state,state.PriorityPlayerId!,new PassPriorityCommand());return await Act(first.State,first.State.PriorityPlayerId!,new PassPriorityCommand());}
    internal static IReadOnlyList<string> Errors(MatchState state)=>MatchRecoveryValidator.Validate(state.RoomId,0,[],[],new Dictionary<string,RecoveredPlayerView>(),state,state.Tick);
    internal static MatchState Restore(MatchState state)
    {var restored=JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(restored));Assert.Empty(Errors(restored));return restored;}
}
