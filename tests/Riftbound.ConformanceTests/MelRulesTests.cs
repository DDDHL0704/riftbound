using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MelRulesTests
{
    internal static MatchState Position(string cardNo = "VEN·069", MatchState? initial = null)
    {
        var s = initial ?? EmpowerTests.Position(cardNo);
        if (initial is not null) {
            var cards = s.CardObjects.ToDictionary(p => p.Key,p => p.Value);
            var locations = s.ObjectLocations.ToDictionary(p => p.Key,p => p.Value);
            cards["HOST"] = new("HOST",cardNo:cardNo,manaCost:4,power:4,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]);
            locations["HOST"] = new("P1","HAND");
            var seededDeck = Enumerable.Range(0,5).Select(i => "MEL-DECK-" + i).ToArray();
            foreach (var id in seededDeck) { cards[id]=new(id,cardNo:"OGN·001/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]); locations[id]=new("P1","MAIN_DECK"); }
            s = s with { CardObjects=cards,ObjectLocations=locations,PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {
                ["P1"]=s.PlayerZones["P1"] with { Hand=[..s.PlayerZones["P1"].Hand,"HOST"],MainDeck=seededDeck } } };
        }
        s = s with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(30, 0, new Dictionary<string,int>{{"blue",8}}), ["P2"] = new(30,30) } };
        return s;
    }

    internal static async Task<MatchState> Enter(string cardNo = "VEN·069", bool empower = true, MatchState? initial = null)
    {
        var s = Position(cardNo,initial);
        var deck = s.PlayerZones["P1"].MainDeck.Count;
        var played = await Act(s, "P1", new PlayCardCommand("HOST", cardNo, []));
        Assert.Equal(26, played.State.RunePools["P1"].Mana);
        Assert.Equal(7, played.State.RunePools["P1"].PowerByTrait["blue"]);
        Assert.Contains("HOST", played.State.PlayerZones["P1"].Base);
        Assert.Single(played.State.StackItems); Assert.True(played.State.StackItems.Single().SourceConfirmed);
        Assert.False(played.State.CardObjects["HOST"].IsEmpowered);
        var entered = await Top(played.State); s = entered.State;
        Assert.Equal(deck - 1, s.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(played.State.PlayerZones["P1"].Hand.Count + 1,s.PlayerZones["P1"].Hand.Count);
        if (empower) {
            var activated = await EmpowerTests.Activate(s);
            Assert.False(activated.State.CardObjects["HOST"].IsEmpowered);
            s = (await Top(activated.State)).State;
            Assert.True(s.CardObjects["HOST"].IsEmpowered); Assert.Equal(23, s.RunePools["P1"].Mana);
        }
        Restore(s); ArmamentMightTests.PublicRecovery(s);
        return s;
    }

    internal static MatchState Enemy(MatchState s, int power = 5)
    {
        s = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(s,"D","OGN·001/298","F","P2"),"D","P2","BASE");
        return s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { Power = power } } };
    }

    internal static MatchState Spell(MatchState s, string cardNo, string id = "SPELL", string actor = "P1")
        => AttackDamageDistributionTests.Spell(s, id, cardNo, actor);

    [Theory]
    [InlineData("VEN·069")]
    [InlineData("VEN·069a")]
    [InlineData("VEN·069b")]
    public void CurrentExactPrintingSupportsItsPrintedUnitAndDrawBehavior(string cardNo)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(4, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(1, behavior.DrawCount);
    }

    [Theory]
    [InlineData("VEN·069", true, 3)]
    [InlineData("VEN·069a", true, 3)]
    [InlineData("VEN·069b", true, 3)]
    [InlineData("VEN·069", false, 4)]
    public async Task RealEntryAndEmpowerEnableOnlyTheChosenNegativeMightEvent(string card, bool empowered, int expected)
    {
        var s = Spell(Enemy(await Enter(card, empowered)), "OGN·095/298");
        var done = await Top((await Act(s,"P1",new PlayCardCommand("SPELL","OGN·095/298",["D"]))).State);
        Assert.Equal(expected, done.State.CardObjects["D"].Power);
        var modifier = Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(empowered ? -2 : -1, modifier.RequestedPowerDelta);
        if (empowered) {
            var capture = modifier.ChosenNegativeMight!;
            Assert.Equal(-1,capture.OriginalDelta); Assert.Equal("D",capture.Target.ObjectId);
            var contributor = Assert.Single(capture.Contributors); Assert.Equal(card,contributor.CardNo); Assert.True(contributor.WasEmpowered);
        } else Assert.Null(modifier.ChosenNegativeMight);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(1,1)]
    [InlineData(2,1)]
    [InlineData(3,1)]
    [InlineData(5,3)]
    public async Task ReplacingStupefyInheritsItsMinimumAndNeverCreatesAnExtraReductionFromZero(int initial, int expected)
    {
        var s = Spell(Enemy(await Enter(),initial), "OGN·095/298");
        var done = await Top((await Act(s,"P1",new PlayCardCommand("SPELL","OGN·095/298",["D"]))).State);
        Assert.Equal(expected,done.State.CardObjects["D"].Power);
        if (initial == 1) Assert.Empty(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        else {
            var modifier=Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(1,modifier.MinimumPower); Assert.Equal(initial,modifier.SnapshotMight);
            Assert.Equal(-2,modifier.RequestedPowerDelta); Assert.Equal(expected-initial,modifier.PowerDelta);
        }
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AbandonIsLegalButOnlyUnprotectedSpellsAreCounteredAndReturned(bool empowered)
    {
        var s = Spell(await Enter(empower:empowered), "OGN·048/298");
        s = Spell(s,"UNL-131/219","COUNTER","P2");
        var cast = await Act(s,"P1",new PlayCardCommand("SPELL","OGN·048/298",[]));
        var passed = await Act(cast.State,"P1",new PassPriorityCommand());
        var counter = new PlayCardCommand("COUNTER","UNL-131/219",[cast.State.StackItems.Single().StackItemId]);
        var quote=new CoreRuleEngine().PreviewPlayCard(passed.State,"P2",PlayCostPreviewTests.Request(passed.State,counter));
        Assert.True(quote.CanPay,quote.Message);
        var attempted = await Top((await Act(passed.State,"P2",counter)).State);
        if (attempted.State.PendingCardChoice is { } insight)
            attempted = await Act(attempted.State,"P2",new ChooseCardsCommand(insight.ChoiceId,insight.ChoiceWindow,[]));
        Assert.Equal(empowered ? 1 : 0,attempted.State.StackItems.Count);
        if (empowered) {
            Assert.DoesNotContain("SPELL",attempted.State.PlayerZones["P1"].Hand);
            var done=await Top(attempted.State);
            Assert.Contains("SPELL",done.State.PlayerZones["P1"].Graveyard); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        } else Assert.Contains("SPELL",attempted.State.PlayerZones["P1"].Hand);
        Restore(attempted.State); ArmamentMightTests.PublicRecovery(attempted.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoonfallOnlyReplacesTheChosenEnemyAfterItsOptionalMove(bool chooseEnemy)
    {
        var initial = GroupedTargetTests.OtherField(GroupedTargetTests.Group(3,5),"D2");
        initial = LocalDestructionRecallTests.At(initial,"D3","P2","BASE");
        var s = await Enter(initial:initial);
        s = (await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]))).State;
        s = Spell(s,MoonfallTests.Spell,"MEL-MOONFALL");
        var cast = await Act(s,"P1",new PlayCardCommand("MEL-MOONFALL",MoonfallTests.Spell,chooseEnemy ? ["BF","D2"] : ["BF"]));
        var done = await Top(cast.State);
        Assert.Equal(3,done.State.CardObjects["D"].Power);
        Assert.Null(Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).ChosenNegativeMight);
        Assert.Equal(chooseEnemy ? 2 : 5,done.State.CardObjects["D2"].Power);
        if (chooseEnemy) {
            var captured = Assert.Single(done.State.CardObjects["D2"].UntilEndOfTurnPowerModifiers).ChosenNegativeMight!;
            Assert.Equal(-2,captured.OriginalDelta); Assert.Equal("D2",captured.Target.ObjectId);
        }
        Assert.Equal(5,done.State.CardObjects["D3"].Power);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TargetedExhaustAbilityReceivesTheReplacementAndCounterProtection(bool empowered)
    {
        var s = Enemy(await Enter(empower:empowered));
        s = s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["ORB"]=new("ORB",cardNo:OrbOfRegretTests.Card,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]) },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P1"]=s.PlayerZones["P1"] with { Base=[..s.PlayerZones["P1"].Base,"ORB"] } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["ORB"]=new("P1","BASE") } };
        s=Spell(s,"SFD·045/221","COUNTER","P2");
        var activated=await Act(s,"P1",OrbOfRegretTests.Command());
        Assert.True(activated.State.CardObjects["ORB"].IsExhausted);
        var passed=await Act(activated.State,"P1",new PassPriorityCommand());
        var counter=await Act(passed.State,"P2",new PlayCardCommand("COUNTER","SFD·045/221",[activated.State.StackItems.Single().StackItemId]));
        var attempted=await Top(counter.State);
        Assert.Equal(empowered ? 1 : 0,attempted.State.StackItems.Count);
        Assert.Contains("ORB",attempted.State.PlayerZones["P1"].Base);
        Assert.True(attempted.State.CardObjects["ORB"].IsExhausted);
        if (empowered) {
            var done=await Top(attempted.State);
            Assert.Equal(3,done.State.CardObjects["D"].Power);
            Assert.NotNull(Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).ChosenNegativeMight);
            Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        } else Assert.Equal(5,attempted.State.CardObjects["D"].Power);
        Restore(attempted.State); ArmamentMightTests.PublicRecovery(attempted.State);
    }

    [Fact]
    public async Task SpellControlTransferIsAllowedAndRechecksTheNewControllersEmpoweredRules()
    {
        var s=Spell(Enemy(await Enter()),"OGN·095/298");
        s=Spell(s,"OGN·080/298","CONTROL","P2");
        var cast=await Act(s,"P1",new PlayCardCommand("SPELL","OGN·095/298",["D"]));
        var passed=await Act(cast.State,"P1",new PassPriorityCommand());
        var control=await Act(passed.State,"P2",new PlayCardCommand("CONTROL","OGN·080/298",[cast.State.StackItems.Single().StackItemId]));
        var changed=await Top(control.State);
        var remake = Assert.IsType<RuleChoiceContinuation>(changed.State.PendingRuleChoice);
        changed = await Act(changed.State, "P2", new PayCostCommand(remake.Request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        Assert.Equal("P2",changed.State.StackItems.Single().ControllerId);
        Assert.Single(changed.Events,e=>e.Kind=="STACK_ITEM_CONTROL_GAINED");
        Assert.DoesNotContain(changed.Events,e=>e.Kind=="STACK_ITEM_COUNTERED");
        Restore(changed.State); ArmamentMightTests.PublicRecovery(changed.State);
        var done=await Top(changed.State);
        Assert.Equal(4,done.State.CardObjects["D"].Power);
        Assert.Null(Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).ChosenNegativeMight);
        Assert.Contains("SPELL",done.State.PlayerZones["P1"].Graveyard);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("delta")]
    [InlineData("tick")]
    [InlineData("target")]
    [InlineData("generation")]
    [InlineData("instance")]
    [InlineData("card")]
    [InlineData("status")]
    [InlineData("controller")]
    [InlineData("null-source")]
    public async Task RecoveryRejectsForgedNegativeMightReplacementFields(string mutation)
    {
        var s=Spell(Enemy(await Enter()),"OGN·095/298");
        var done=(await Top((await Act(s,"P1",new PlayCardCommand("SPELL","OGN·095/298",["D"]))).State)).State;
        var card=done.CardObjects["D"]; var entry=Assert.Single(card.UntilEndOfTurnPowerModifiers);
        var context=entry.ChosenNegativeMight!; var source=context.Contributors.Single();
        if (mutation=="delta") context=context with { OriginalDelta=0 };
        if (mutation=="tick") context=context with { AppliedTick=done.Tick+1 };
        if (mutation=="target") context=context with { Target=new("D",999) };
        if (mutation=="generation") source=source with { Source=new("HOST",999) };
        if (mutation=="instance") source=source with { InstanceId="PRINTED:UNRELATED:0" };
        if (mutation=="card") source=source with { CardNo="VEN·074" };
        if (mutation=="status") source=source with { WasEmpowered=false };
        if (mutation=="controller") source=source with { ControllerId="P2" };
        context=context with { Contributors=mutation=="null-source" ? [null!] : [source] };
        var bad=done with { CardObjects=new Dictionary<string,CardObjectState>(done.CardObjects) {
            ["D"]=card with { UntilEndOfTurnPowerModifiers=[entry with { ChosenNegativeMight=context }] } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad),e=>e.Contains("negative Might replacement"));
        Restore(done); ArmamentMightTests.PublicRecovery(done);
    }

    [Fact]
    public async Task ASpellRespondingAbovePendingEmpowerCanStillBeCountered()
    {
        var s=Spell(Enemy(await Enter(empower:false)),"OGN·095/298");
        s=Spell(s,"OGN·064/298","COUNTER","P2");
        var activated=await EmpowerTests.Activate(s);
        var cast=await Act(activated.State,"P1",new PlayCardCommand("SPELL","OGN·095/298",["D"]));
        Assert.False(cast.State.CardObjects["HOST"].IsEmpowered);
        var spell=cast.State.StackItems.Last();
        var passed=await Act(cast.State,"P1",new PassPriorityCommand());
        var counter=await Top((await Act(passed.State,"P2",new PlayCardCommand("COUNTER","OGN·064/298",[spell.StackItemId]))).State);
        Assert.Single(counter.State.StackItems); Assert.False(counter.State.CardObjects["HOST"].IsEmpowered);
        Assert.Single(counter.Events,e=>e.Kind=="STACK_ITEM_COUNTERED");
        Assert.Equal(5,counter.State.CardObjects["D"].Power);
        var done=await Top(counter.State); Assert.True(done.State.CardObjects["HOST"].IsEmpowered);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AWholeUnitCopyUsesItsOwnStatusAndExpiresItsDependentRulesOnDetach()
    {
        var s=ShadySpectaclesTests.Position("VEN·069");
        s=await ShadySpectaclesTests.Attach(s);
        Assert.False(s.CardObjects["HOST"].IsEmpowered);
        var candidate=Assert.Single(ResolutionResult.BuildPrompts(s)["P1"].Candidates!,c=>c.Action==CommandTypes.ActivateAbility);
        var rows=Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string,object?>>>(candidate.Metadata!["sourceRequirements"]);
        var acquired=Assert.Single(rows,r=>Equals(r["sourceObjectId"],"HOST"));
        var id=Assert.IsType<string>(acquired["abilityInstanceId"]);
        var activated=await Act(s,"P1",new ActivateAbilityCommand("HOST","EMPOWER:0:0",[]) { AbilityInstanceId=id });
        s=(await Top(activated.State)).State;
        s=Spell(Enemy(s),"OGN·095/298");
        var changed=await Top((await Act(s,"P1",new PlayCardCommand("SPELL","OGN·095/298",["D"]))).State);
        Assert.Equal(3,changed.State.CardObjects["D"].Power);
        var origin=Assert.Single(changed.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).ChosenNegativeMight!.Contributors.Single().Origin;
        Assert.Equal("GEAR",origin!.Equipment.ObjectId);
        s=Spell(changed.State,"SFD·011/221","DETACH");
        var detached=await Top((await Act(s,"P1",new PlayCardCommand("DETACH","SFD·011/221",["HOST","GEAR"]))).State);
        Assert.Equal("OGN·096/298",detached.State.CardObjects["HOST"].CardNo);
        Assert.True(detached.State.CardObjects["HOST"].IsEmpowered);
        Restore(detached.State); ArmamentMightTests.PublicRecovery(detached.State);
        s=Spell(detached.State,"OGN·095/298","SECOND");
        var done=await Top((await Act(s,"P1",new PlayCardCommand("SECOND","OGN·095/298",["D"]))).State);
        Assert.Equal(2,done.State.CardObjects["D"].Power);
        Assert.Null(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers.Last().ChosenNegativeMight);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task RemovingMelAboveAnAlreadyChosenCounterEndsProtectionBeforeThatCounterResolves()
    {
        var s=Spell(await Enter(),"OGN·048/298");
        s=Spell(s,"OGN·064/298","COUNTER","P2");
        s=Spell(s,"SFD·163/221","REMOVE");
        s=LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(s,"FRIEND","OGN·001/298","F","P1"),"FRIEND","P1","BASE");
        s=s with { RunePools=new Dictionary<string,RunePool>(s.RunePools) { ["P1"]=new(30,30) } };
        var cast=await Act(s,"P1",new PlayCardCommand("SPELL","OGN·048/298",[]));
        var passed=await Act(cast.State,"P1",new PassPriorityCommand());
        var counter=await Act(passed.State,"P2",new PlayCardCommand("COUNTER","OGN·064/298",[cast.State.StackItems.Single().StackItemId]));
        var letRespond=await Act(counter.State,"P2",new PassPriorityCommand());
        var removal=await Top((await Act(letRespond.State,"P1",new PlayCardCommand("REMOVE","SFD·163/221",["HOST","FRIEND"]))).State);
        Assert.Contains("HOST",removal.State.PlayerZones["P1"].Graveyard);
        Assert.False(removal.State.CardObjects["HOST"].IsEmpowered);
        var resolved=await Top(removal.State);
        Assert.Empty(resolved.State.StackItems); Assert.Single(resolved.Events,e=>e.Kind=="STACK_ITEM_COUNTERED");
        Assert.Contains("SPELL",resolved.State.PlayerZones["P1"].Graveyard);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }
}
