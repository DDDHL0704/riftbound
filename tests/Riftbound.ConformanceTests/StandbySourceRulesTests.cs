using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class StandbySourceRulesTests
{
    internal static MatchState Position(string card="OGN·057/298")
    {
        var s=ArmamentMightTests.Position();
        s=LocalDestructionRecallTests.At(s,"HOST","P1","BATTLEFIELD","F");
        s=AttackDamageDistributionTests.Spell(s,"SHADOW",card,"P1");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(card,out var printed));
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["SHADOW"]=s.CardObjects["SHADOW"] with {Tags=[printed.PlaysSourceToBaseAsUnit ? CardObjectTags.UnitCard
                : printed.PlaysSourceToBaseAsEquipment ? CardObjectTags.EquipmentCard : CardObjectTags.SpellCard],Power=printed.SourceUnitPower} }};
        s=OfficialHoldSequenceTests.AddUnit(s,"SHADOW-INSIDE","OGN·001/298","F","P1");
        s=OfficialHoldSequenceTests.AddUnit(s,"SHADOW-OUTSIDE","OGN·001/298","F","P1");
        s=LocalDestructionRecallTests.At(s,"SHADOW-OUTSIDE","P1","BASE");
        s=AttackDamageDistributionTests.Spell(s,"SHADOW-REBUTTAL","VEN·152","P2");
        s=s with {RunePools=new Dictionary<string,RunePool> { ["P1"]=new(0,3),["P2"]=new(20,20) }};
        var cards=s.CardObjects.ToDictionary(p=>p.Key,p=>p.Value);var locations=s.ObjectLocations.ToDictionary(p=>p.Key,p=>p.Value);
        var zones=s.PlayerZones.ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var player in new[]{"P1","P2"}) {
            foreach(var old in zones[player].MainDeck) {cards.Remove(old);locations.Remove(old);}
            var deck=Enumerable.Range(0,8).Select(i=>"SHADOW-DECK-"+player+"-"+i).ToArray();
            foreach(var id in deck) {cards[id]=new(id,cardNo:"OGN·001/298",ownerId:player,controllerId:player,tags:[CardObjectTags.UnitCard]);locations[id]=new(player,"MAIN_DECK");}
            zones[player]=zones[player] with {MainDeck=deck};
        }
        foreach(var id in Enumerable.Range(1,5).Select(i=>"SHADOW-P2-RUNE-"+i)) {
            cards[id]=new(id,cardNo:"UNL-R03",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.RuneCard,"COLOR:blue"]);
            locations[id]=new("P2","BASE");zones["P2"]=zones["P2"] with {Base=zones["P2"].Base.Append(id).ToArray()};
        }
        s=s with {CardObjects=cards,ObjectLocations=locations,PlayerZones=zones};
        return s;
    }

    [Theory]
    [InlineData("OGN·057/298")]
    [InlineData("OGN·083/298")]
    [InlineData("OGN·135/298")]
    public async Task PrintedStandbyHidesAtControlledBattlefieldForPowerAndCannotPlayThisTurn(string card)
    {
        var s=Position(card);
        if(CardBehaviorRegistry.TryGetByCardNo(card,out var definition) && definition.PlaysSourceToBaseAsUnit)
            s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["SHADOW"]=s.CardObjects["SHADOW"] with {Tags=[CardObjectTags.UnitCard],Power=definition.SourceUnitPower} }};
        var hidden=await Act(s,"P1",new HideCardCommand("SHADOW",card,"BATTLEFIELD:F",["STANDBY_A"]));
        Assert.Equal(0,hidden.State.RunePools["P1"].Mana);Assert.Equal(2,hidden.State.RunePools["P1"].TotalPower);
        Assert.True(hidden.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal("F",hidden.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Empty(hidden.State.StackItems);Restore(hidden.State);ArmamentMightTests.PublicRecovery(hidden.State);
        var play=await new CoreRuleEngine().ResolveAsync(hidden.State,new PlayerIntent("same-turn","P1",CommandTypes.PlayCard),
            new PlayCardCommand("SHADOW",card,card=="OGN·057/298" ? ["HOST"] : []),default);
        Assert.False(play.Accepted);Assert.Equal(MatchStateHasher.Hash(hidden.State),MatchStateHasher.Hash(play.State));
    }

    [Theory]
    [InlineData("OGN·275/298", 1)]
    [InlineData("OGN·278/298", 2)]
    public async Task ActualPrintedStandbyCapacityAcceptsAvailableSlotsAndRejectsOverflowWithoutMutation(string fieldCard, int capacity)
    {
        var initial = Position("OGN·135/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            { ["F"] = initial.CardObjects["F"] with { CardNo = fieldCard } } };
        foreach (var id in new[] { "STANDBY-TWO", "STANDBY-THREE" })
        {
            initial = AttackDamageDistributionTests.Spell(initial, id, "OGN·135/298", "P1");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
                { [id] = initial.CardObjects[id] with { Tags = [CardObjectTags.UnitCard], Power = 3 } } };
        }
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var sources = new[] { "SHADOW", "STANDBY-TWO", "STANDBY-THREE" };
        for (var index = 0; index < capacity; index++)
        {
            var beforePower = driver.State.RunePools["P1"].TotalPower;
            var result = await driver.Act("P1", new HideCardCommand(sources[index], "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_A"]));
            Assert.Equal(beforePower - 1, result.State.RunePools["P1"].TotalPower);
            Assert.True(result.State.CardObjects[sources[index]].IsFaceDown);
            Assert.Equal("F", result.State.ObjectLocations[sources[index]].BattlefieldObjectId);
            Assert.Equal(index + 1, result.State.CardObjects.Values.Count(card => card.IsFaceDown
                && result.State.ObjectLocations.TryGetValue(card.ObjectId, out var at) && at.BattlefieldObjectId == "F"));
        }
        var before = driver.State;
        var command = new HideCardCommand(sources[capacity], "OGN·135/298", "BATTLEFIELD:F", ["STANDBY_A"]);
        var blocked = await new CoreRuleEngine().ResolveAsync(before, new("overflow", "P1", CommandTypes.HideCard), command, default);
        Assert.False(blocked.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, blocked.ErrorCode);
        Assert.Empty(blocked.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(blocked.State));
        Assert.Equal(before.RunePools["P1"], blocked.State.RunePools["P1"]);
        Assert.Contains(sources[capacity], blocked.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(before)["P1"].Candidates!, c => c.Action == CommandTypes.HideCard && c.Enabled);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    internal static async Task<MatchState> NextTurn(MatchState state)
    {
        var next=await Act(state,state.TurnPlayerId,new EndTurnCommand());
        next=await TurnSequenceTestDriver.Complete(next);Restore(next.State);
        Assert.Equal(state.TurnNumber+1,next.State.TurnNumber);return next.State;
    }

    internal static async Task<MatchState> ReadyShadow(string card="OGN·057/298",MatchState? initial=null)
    {
        var s=initial??Position(card);s=AttackDamageDistributionTests.Spell(s,"REPLY","OGN·048/298","P2");
        if(CardBehaviorRegistry.TryGetByCardNo(card,out var definition) && definition.PlaysSourceToBaseAsUnit)
            s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["SHADOW"]=s.CardObjects["SHADOW"] with {Tags=[CardObjectTags.UnitCard],Power=definition.SourceUnitPower} }};
        var hidden=await Act(s,"P1",new HideCardCommand("SHADOW",card,"BATTLEFIELD:F",["STANDBY_A"]));
        s=await NextTurn(hidden.State);
        s=(await Act(s,"P2",new TapRuneCommand("SHADOW-P2-RUNE-1"))).State;
        s=(await Act(s,"P2",new TapRuneCommand("SHADOW-P2-RUNE-2"))).State;
        s=(await Act(s,"P2",new TapRuneCommand("SHADOW-P2-RUNE-3"))).State;
        s=(await Act(s,"P2",new RecycleRuneCommand("SHADOW-P2-RUNE-4"))).State;
        s=(await Act(s,"P2",new RecycleRuneCommand("SHADOW-P2-RUNE-5"))).State;
        s=(await Act(s,"P2",new PlayCardCommand("REPLY","OGN·048/298",[]))).State;
        s=(await Act(s,"P2",new PassPriorityCommand())).State;Restore(s);return s;
    }

    [Theory]
    [InlineData("OGN·057/298")]
    [InlineData("OGN·083/298")]
    [InlineData("OGN·135/298")]
    public async Task ActualNextTurnAllowsReactionWithoutRepayingPrintedCostsOrMovingSourceToHand(string card)
    {
        var s=await ReadyShadow(card);Assert.Contains("SHADOW",s.PlayerZones["P1"].Battlefields);Assert.DoesNotContain("SHADOW",s.PlayerZones["P1"].Hand);
        var pool=s.RunePools["P1"];
        var r=await Act(s,"P1",new PlayCardCommand("SHADOW",card,card=="OGN·057/298" ? ["HOST"] : []));
        Assert.False(r.State.CardObjects["SHADOW"].IsFaceDown);Assert.Null(r.State.CardObjects["SHADOW"].StandbyPlacement);
        Assert.Equal(pool,r.State.RunePools["P1"]);Assert.Equal(2,r.State.CardObjects["SHADOW"].ObjectGeneration);
        if(definitionIsUnit(card)) {Assert.Contains("SHADOW",r.State.PlayerZones["P1"].Battlefields);Assert.Equal("F",r.State.ObjectLocations["SHADOW"].BattlefieldObjectId);}
        else {var item=Assert.Single(r.State.StackItems,i=>i.SourceObjectId=="SHADOW");Assert.NotNull(item.PlayedFromStandby);Assert.Equal(0,item.PlayCost!.PaidMana);Assert.Equal("F",item.PlayedFromStandby!.Battlefield.ObjectId);}
        Restore(r.State);ArmamentMightTests.PublicRecovery(r.State);
        static bool definitionIsUnit(string no)=>CardBehaviorRegistry.TryGetByCardNo(no,out var b)&&b.PlaysSourceToBaseAsUnit;
    }

    [Fact]
    public async Task OwnerPromptShowsActualPowerCostAndBattlefieldDestinationsForSpellStandby()
    {
        var s=Position();var prompt=ResolutionResult.BuildPrompts(s)["P1"];
        var hide=Assert.Single(prompt.Candidates!,c=>c.Action==CommandTypes.HideCard);
        Assert.Contains(hide.Sources!,c=>c.Id=="SHADOW");Assert.Contains(hide.Destinations!,c=>c.Id=="BATTLEFIELD:F");
        Assert.DoesNotContain(hide.Destinations!,c=>c.Id=="STANDBY");
        Assert.Contains(hide.OptionalCosts!,c=>c.Id=="STANDBY_A"&&c.Label.Contains("符能"));
    }

    [Theory]
    [InlineData("{{待命}}\n抽一张牌。",true)]
    [InlineData("{{反应}}\n{{待命}}",true)]
    [InlineData("每当你从{{待命}}状态打出时，抽一张牌。",false)]
    [InlineData("你可以布置{{待命}}牌。",false)]
    public void KeywordInstancesAreDistinctFromReferences(string text,bool expected)
        =>Assert.Equal(expected,StandbyParser.HasPrintedKeyword(text));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RebuttalRemakesRetainTheOriginalBattlefieldAfterControlChanges(bool remake)
    {
        var s=await ReadyShadow();
        s=(await Act(s,"P1",new PlayCardCommand("SHADOW","OGN·057/298",["HOST"]))).State;
        var origin=Assert.Single(s.StackItems,i=>i.SourceObjectId=="SHADOW");
        s=(await Act(s,"P1",new PassPriorityCommand())).State;
        s=(await Act(s,"P2",new PlayCardCommand("SHADOW-REBUTTAL","VEN·152",[origin.StackItemId]))).State;
        s=(await Top(s)).State;s=await RebuttalRulesTests.Choose(s,
            Assert.Single(s.PendingRuleChoice!.Request.Options,o=>o.Id=="PAY:TRAIT:blue").Id);
        Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options,o=>o.Label.Contains("（SHADOW-OUTSIDE）"));
        if(remake) {
            s=await RebuttalRulesTests.Choose(s,RebuttalRulesTests.Option(s,"重选目标 1 为","（SHADOW-INSIDE）"));
            s=await RebuttalRulesTests.Choose(s,"CONFIRM");
        } else s=await RebuttalRulesTests.Choose(s,"KEEP_ALL");
        var changed=Assert.Single(s.StackItems,i=>i.SourceObjectId=="SHADOW");
        Assert.Equal("P2",changed.ControllerId);Assert.Equal(origin.PlayedFromStandby,changed.PlayedFromStandby);
        Assert.Equal(new[]{remake ? "SHADOW-INSIDE" : "HOST"},changed.TargetObjectIds);
        s=(await Top(s)).State;Restore(s);ArmamentMightTests.PublicRecovery(s);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("turn")]
    [InlineData("hash")]
    [InlineData("field")]
    public async Task RecoveryRejectsSingleFieldPlacementAndPlayedOriginForgeries(string mutation)
    {
        var s=Position();s=(await Act(s,"P1",new HideCardCommand("SHADOW","OGN·057/298","BATTLEFIELD:F",["STANDBY_A"]))).State;
        var p=s.CardObjects["SHADOW"].StandbyPlacement!;
        var bad=mutation switch {"generation"=>p with {SourceGeneration=p.SourceGeneration+1},"turn"=>p with {TurnNumber=s.TurnNumber+1},
            "hash"=>p with {PrintedTextSha256=new string('0',64)},_=>p with {Battlefield=new("OTHER",0)}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["SHADOW"]=s.CardObjects["SHADOW"] with {StandbyPlacement=bad}}}),e=>e.Contains("standby placement"));
        s=await ReadyShadow();s=(await Act(s,"P1",new PlayCardCommand("SHADOW","OGN·057/298",["HOST"]))).State;
        var item=Assert.Single(s.StackItems,i=>i.SourceObjectId=="SHADOW");p=item.PlayedFromStandby!;
        bad=mutation switch {"generation"=>p with {SourceGeneration=p.SourceGeneration+1},"turn"=>p with {TurnNumber=s.TurnNumber},
            "hash"=>p with {PrintedTextSha256=new string('0',64)},_=>p with {Battlefield=new("OTHER",0)}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=s.StackItems.Select(i=>i.StackItemId==item.StackItemId ? i with {PlayedFromStandby=bad} : i).ToArray()}),e=>e.Contains("standby origin"));
        Restore(s);
    }

    [Fact]
    public async Task BattlefieldUnitPlayBanAllowsStandbyPlacementAndEquipmentButRejectsUnitPlay()
    {
        foreach (var card in new[] { "OGN·135/298", "OGN·077/298" })
        {
            var initial = Position(card);
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
            {
                ["F"] = initial.CardObjects["F"] with { CardNo = "SFD·216/221" }
            }};
            var state = await ReadyShadow(card, initial);
            Assert.True(state.CardObjects["SHADOW"].IsFaceDown);
            var result = await new CoreRuleEngine().ResolveAsync(state,
                new PlayerIntent("shadow-unit-ban-" + card, "P1", CommandTypes.PlayCard),
                new PlayCardCommand("SHADOW", card, []), default);
            if (card == "OGN·135/298")
            {
                Assert.False(result.Accepted);
                Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
                Assert.Empty(result.Events);
                Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
            }
            else
            {
                Assert.True(result.Accepted, result.ErrorMessage);
                if (result.State.StackItems.Any(i => i.SourceObjectId == "SHADOW")) result = await Top(result.State);
                Assert.Equal("F", result.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
                Assert.Contains("SHADOW", result.State.PlayerZones["P1"].Battlefields);
                Restore(result.State);
                ArmamentMightTests.PublicRecovery(result.State);
            }
        }
    }

    [Fact]
    public async Task UnitPlayedByStandbyEffectMustEnterTheOriginalBattlefield()
    {
        var initial = Position("SFD·111/221");
        initial = StandbyTestDriver.WithUnitInHand(initial, "SHADOW-EFFECT-UNIT", "OGN·135/298", "P1");
        var state = await ReadyShadow("SFD·111/221", initial);
        state = (await Act(state, "P1", new PlayCardCommand("SHADOW", "SFD·111/221", []))).State;
        state = (await Top(state)).State;
        Assert.NotNull(state.PendingEffectPlay);
        Assert.Equal("F", state.PendingEffectPlay!.Parent.PlayedFromStandby!.Battlefield.ObjectId);
        Restore(state);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Equal(["BATTLEFIELD:F"], play.Destinations!.Select(d => d.Id));
        var wrong = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("shadow-effect-base", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SHADOW-EFFECT-UNIT", "OGN·135/298", [], Destination: "BASE"), default);
        Assert.False(wrong.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(wrong.State));
        state = (await Act(state, "P1", new PlayCardCommand("SHADOW-EFFECT-UNIT", "OGN·135/298", [], Destination: "BATTLEFIELD:F"))).State;
        Assert.Contains("SHADOW-EFFECT-UNIT", state.PlayerZones["P1"].Battlefields);
        Assert.Equal("F", state.ObjectLocations["SHADOW-EFFECT-UNIT"].BattlefieldObjectId);
        Assert.DoesNotContain("SHADOW-EFFECT-UNIT", state.PlayerZones["P1"].Base);
        Restore(state);
        ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task TeemoPaymentReplacementDoesNotGrantStandbyToAPlainCard()
    {
        var state = Position("OGN·001/298");
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                ["SHADOW-TEEMO-LEGEND"] = new("SHADOW-TEEMO-LEGEND", cardNo: "OGN·263/298", ownerId: "P1", controllerId: "P1")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["SHADOW-TEEMO-LEGEND"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            {
                ["SHADOW-TEEMO-LEGEND"] = new("P1", "LEGEND_ZONE")
            },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 0) }
        };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.HideCard);
        Assert.DoesNotContain(candidate.Sources ?? [], s => s.Id == "SHADOW");
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("teemo-plain-hide", "P1", CommandTypes.HideCard),
            new HideCardCommand("SHADOW", "OGN·001/298", "BATTLEFIELD:F", ["STANDBY_TEEMO_MANA"]), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task FaceUpUnitWithPrintedStandbyRemainsAVisibleUnitAndLegalTarget()
    {
        var s=Position("OGN·135/298");s=s with {RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(20,20)},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["SHADOW"]=s.CardObjects["SHADOW"] with {Tags=[CardObjectTags.UnitCard]}}};
        s=(await Act(s,"P1",new PlayCardCommand("SHADOW","OGN·135/298",[]))).State;
        if(s.StackItems.Count>0)s=(await Top(s)).State;
        s=AttackDamageDistributionTests.Spell(s,"BUFF","OGN·057/298","P1");
        var r=await Act(s,"P1",new PlayCardCommand("BUFF","OGN·057/298",["SHADOW"]));
        Assert.Contains("SHADOW",Assert.Single(r.State.StackItems).TargetObjectIds);Restore(r.State);ArmamentMightTests.PublicRecovery(r.State);
    }

    [Theory]
    [InlineData("OGN·094/298")]
    [InlineData("OGN·077/298")]
    public async Task StandbyUnitTokenAndEquipmentEnterTheOriginalBattlefield(string card)
    {
        var s=await ReadyShadow(card);var played=await Act(s,"P1",new PlayCardCommand("SHADOW",card,[]));
        var r=card=="OGN·094/298" ? await Top(played.State) : played;
        var id=card=="OGN·094/298" ? (string)Assert.Single(r.Events,e=>e.Kind=="UNIT_TOKEN_CREATED").Payload["tokenObjectId"]! : "SHADOW";
        Assert.True(r.State.PlayerZones["P1"].Battlefields.Contains(id),System.Text.Json.JsonSerializer.Serialize(new {
            source=r.State.CardObjects[id],location=r.State.ObjectLocations[id],zones=r.State.PlayerZones["P1"],events=r.Events.Select(e=>new {e.Kind,e.Payload})}));
        Assert.DoesNotContain(id,r.State.PlayerZones["P1"].Base);
        Assert.Equal("F",r.State.ObjectLocations[id].BattlefieldObjectId);
        Assert.False(r.State.CardObjects[id].IsFaceDown);Restore(r.State);ArmamentMightTests.PublicRecovery(r.State);
    }

    [Fact]
    public async Task PlayingFromFaceDownQueuesSelfMightObserverButHidingDoesNotTriggerIt()
    {
        var initial=Position();initial=OfficialHoldSequenceTests.AddUnit(initial,"SHADOW-MONK","OGN·167/298","F","P1");
        initial=LocalDestructionRecallTests.At(initial,"SHADOW-MONK","P1","BASE");var before=initial.CardObjects["SHADOW-MONK"].Power;
        var hidden=await Act(initial,"P1",new HideCardCommand("SHADOW","OGN·057/298","BATTLEFIELD:F",["STANDBY_A"]));
        Assert.Equal(before,hidden.State.CardObjects["SHADOW-MONK"].Power);Assert.Empty(hidden.State.TriggerQueue);Assert.Empty(hidden.State.StackItems);
        var s=await ReadyShadow(initial:initial);
        var played=await Act(s,"P1",new PlayCardCommand("SHADOW","OGN·057/298",["HOST"]));
        Assert.Equal(before,played.State.CardObjects["SHADOW-MONK"].Power);
        var observer=Assert.Single(played.State.StackItems,i=>i.StandbyObserver is not null);
        Assert.Equal("SHADOW-MONK",observer.SourceObjectId);Assert.Empty(observer.TargetObjectIds);
        Restore(played.State);ArmamentMightTests.PublicRecovery(played.State);
        var resolved=await Top(played.State);Assert.Equal(before+2,resolved.State.CardObjects["SHADOW-MONK"].Power);
        Assert.DoesNotContain(resolved.State.StackItems,i=>i.StandbyObserver is not null);Restore(resolved.State);
    }
}
