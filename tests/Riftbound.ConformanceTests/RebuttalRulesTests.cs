using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RebuttalRulesTests
{
    internal const string Card = "VEN·152";
    [Fact]
    public void CurrentExactPrintingExposesItsReactionAndFourManaTargetLimit()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card,out var behavior));
        Assert.Equal(1,behavior.ManaCost);
        Assert.True(behavior.CanPlayDuringPriority);
        Assert.Equal(4,behavior.MaxTargetManaCost);
    }

    internal static MatchState Position(string spell = "OGN·095/298", MatchState? initial = null)
    {
        var s = initial ?? AttachedRuleTextTests.Position();
        s = OfficialHoldSequenceTests.AddUnit(s, "R-TARGET", "OGN·001/298", "F", "P2");
        s = OfficialHoldSequenceTests.AddUnit(s, "R-OTHER", "OGN·001/298", "F", "P1");
        s = LocalDestructionRecallTests.At(s, "R-TARGET", "P2", "BASE");
        s = LocalDestructionRecallTests.At(s, "R-OTHER", "P1", "BASE");
        s = s with { RunePools = new Dictionary<string,RunePool> { ["P1"] = new(30,30), ["P2"] = new(30,30) },
            CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["R-TARGET"] = s.CardObjects["R-TARGET"] with { Power = 9 }, ["R-OTHER"] = s.CardObjects["R-OTHER"] with { Power = 9 } } };
        s = AttackDamageDistributionTests.Spell(s, "R-SPELL", spell, "P1");
        return AttackDamageDistributionTests.Spell(s, "REBUTTAL", Card, "P2");
    }

    internal static async Task<MatchState> Open(MatchState? initial = null, string spell = "OGN·095/298",
        IReadOnlyList<string>? targets = null, string mode = "", IReadOnlyList<string>? costs = null,
        string destination = "", IReadOnlyList<string>? movementDestinations = null)
    {
        var s = initial ?? Position(spell);
        var played = await Act(s, "P1", new PlayCardCommand("R-SPELL", spell, targets ?? ["R-TARGET"], mode, costs,
            destination, MovementDestinations: movementDestinations));
        var handed = await Act(played.State, "P1", new PassPriorityCommand());
        var response = await Act(handed.State, "P2", new PlayCardCommand("REBUTTAL", Card, [played.State.StackItems.Single().StackItemId]));
        Assert.Equal(s.RunePools["P2"].Mana-1, response.State.RunePools["P2"].Mana);
        Assert.Equal(s.RunePools["P2"].TotalPower-1,response.State.RunePools["P2"].TotalPower);
        var opened = await Top(response.State);
        Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT", opened.State.PendingRuleChoice!.Request.Kind);
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
        return opened.State;
    }

    internal static async Task<MatchState> Choose(MatchState s, string id)
    {
        var p = s.PendingRuleChoice!;
        var result = await Act(s, p.Request.PlayerId, new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", [id]));
        Restore(result.State); ArmamentMightTests.PublicRecovery(result.State); return result.State;
    }

    internal static string Option(MatchState s, params string[] labelFragments)
        => Assert.Single(s.PendingRuleChoice!.Request.Options, o => labelFragments.All(f => o.Label.Contains(f, StringComparison.Ordinal))).Id;

    [Theory]
    [InlineData("DECLINE")]
    [InlineData("PAY:ANY")]
    [InlineData("PAY:TRAIT:red")]
    [InlineData("PAY:TRAIT:blue")]
    public async Task OptionalPowerIsChosenOnlyDuringResolutionAndControlDoesNotCounter(string payment)
    {
        var initial = Position();
        if (payment.StartsWith("PAY:TRAIT:", StringComparison.Ordinal))
            initial = initial with { RunePools = new Dictionary<string,RunePool>(initial.RunePools) {
                ["P2"] = new(30,30,new Dictionary<string,int> { [payment[10..]] = 2 }) } };
        var s = await Open(initial);
        var remaining = s.RunePools["P2"];
        s = await Choose(s, payment);
        if (payment == "DECLINE") {
            Assert.Empty(s.StackItems); Assert.Contains("R-SPELL",s.PlayerZones["P1"].Graveyard);
            Assert.Equal(remaining,s.RunePools["P2"]); return;
        }
        Assert.Equal("SPELL_CHOICE_REMAKE",s.PendingRuleChoice!.Request.Kind);
        s = await Choose(s,"KEEP_ALL");
        var spell = Assert.Single(s.StackItems); Assert.Equal("P2",spell.ControllerId);
        Assert.Equal(new[]{"R-TARGET"},spell.TargetObjectIds);
        Assert.DoesNotContain("R-SPELL",s.PlayerZones["P1"].Graveyard);
        if (payment == "PAY:ANY") Assert.Equal(remaining.Power-1,s.RunePools["P2"].Power);
        else Assert.Equal(remaining.PowerByTrait[payment[10..]]-1,s.RunePools["P2"].PowerByTrait.GetValueOrDefault(payment[10..]));
        s=(await Top(s)).State;Assert.Contains("R-SPELL",s.PlayerZones["P1"].Graveyard);Assert.Equal(8,s.CardObjects["R-TARGET"].Power);Restore(s);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProtectedSpellCanChangeControlButCannotBeCountered(bool pay)
    {
        var initial=Position(initial:await MelRulesTests.Enter());var s=await Open(initial);
        s=await Choose(s,pay ? "PAY:ANY" : "DECLINE");
        if(pay)s=await Choose(s,"KEEP_ALL");
        Assert.Equal(pay ? "P2" : "P1",Assert.Single(s.StackItems).ControllerId);
        s=(await Top(s)).State;Assert.Equal(pay ? 8 : 7,s.CardObjects["R-TARGET"].Power);Restore(s);
    }

    [Fact]
    public async Task RemakingOneTargetDoesNotReplayCostsAndUpdatesItsIncarnation()
    {
        var s=await Choose(await Open(),"PAY:ANY");
        s=await Choose(s,Option(s,"重选目标 1 为","（R-OTHER）"));
        Assert.Contains(s.PendingRuleChoice!.Request.Options,o=>o.Id=="CONFIRM");s=await Choose(s,"CONFIRM");
        var spell=Assert.Single(s.StackItems);Assert.Equal(new[]{"R-OTHER"},spell.TargetObjectIds);
        Assert.Equal(s.CardObjects["R-OTHER"].ObjectGeneration,spell.TargetGenerations!["R-OTHER"]);
        Assert.Equal(29,s.RunePools["P2"].Mana);Assert.Equal(28,s.RunePools["P2"].Power);
        s=(await Top(s)).State;Assert.Equal(8,s.CardObjects["R-OTHER"].Power);Assert.Equal(9,s.CardObjects["R-TARGET"].Power);Restore(s);
    }

    [Fact]
    public async Task ModeCanChangeAndNeedsACompleteLegalTargetSet()
    {
        var s=await Open(spell:"UNL-182/219",targets:[],mode:"DRAW_1");s=await Choose(s,"PAY:ANY");
        s=await Choose(s,Option(s,"模式 BASE_UNIT_DAMAGE_3"));
        Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options,o=>o.Id=="CONFIRM");
        s=await Choose(s,Option(s,"添加目标 1 为","（R-OTHER）"));
        s=await Choose(s,"CONFIRM");Assert.Equal("PERFECT_FINALE_BASE_DAMAGE_3",Assert.Single(s.StackItems).EffectKind);
        s=(await Top(s)).State;Assert.Equal(3,s.CardObjects["R-OTHER"].Damage);Restore(s);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task PendingResolutionRejectsForgedActorWindowOptionTranscriptAndPreview(int forgery)
    {
        var s=await Open();var p=s.PendingRuleChoice!;
        var bad=forgery switch {
            0=>p with {Request=p.Request with {PlayerId="P1"}},
            1=>p with {Request=p.Request with {PowerCost=0}},
            2=>p with {Request=p.Request with {Options=[new("PAY:ANY","Forged")]}},
            3=>p with {Answers=[new("UNKNOWN","PAY:ANY")]},
            _=>p with {Request=p.Request with {Preview=p.Origin with {Tick=p.Origin.Tick+1}}} };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {PendingRuleChoice=bad}),e=>e.Contains("replacement choice",StringComparison.OrdinalIgnoreCase));
        Restore(s);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PartialRemakeRequiresEveryRetainedTargetToBeLegalOrKeepsAllOriginalChoices(bool remake)
    {
        var s=await Open(spell:"UNL-202/219",targets:["R-OTHER","R-TARGET"],movementDestinations:["BATTLEFIELD:F","BATTLEFIELD:F"]);
        s=await Choose(s,"PAY:ANY");
        // Both original friendly/enemy selections become illegal after control changes.
        s=await Choose(s,Option(s,"重选目标 1 为","（R-TARGET）"));
        Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options,o=>o.Id=="CONFIRM");
        if(remake) {
            s=await Choose(s,Option(s,"重选目标 2 为","（R-OTHER）"));
            Assert.Contains(s.PendingRuleChoice!.Request.Options,o=>o.Id=="CONFIRM");s=await Choose(s,"CONFIRM");
            Assert.Equal(new[]{"R-TARGET","R-OTHER"},Assert.Single(s.StackItems).TargetObjectIds);
        } else {
            s=await Choose(s,"KEEP_ALL");Assert.Equal(new[]{"R-OTHER","R-TARGET"},Assert.Single(s.StackItems).TargetObjectIds);
        }
        s=(await Top(s)).State;
        Assert.Equal(remake ? "BATTLEFIELD" : "BASE",s.ObjectLocations["R-TARGET"].Zone);
        Assert.Equal(remake ? "BATTLEFIELD" : "BASE",s.ObjectLocations["R-OTHER"].Zone);Restore(s);
    }

    [Fact]
    public async Task RemadeTargetIncarnationCannotResolveAgainstAnOlderBinding()
    {
        var s=await Choose(await Open(),"PAY:ANY");
        s=await Choose(s,Option(s,"重选目标 1 为","（R-OTHER）"));s=await Choose(s,"CONFIRM");
        var original=Assert.Single(s.StackItems);
        Assert.Equal(s.CardObjects["R-OTHER"].ObjectGeneration,original.TargetGenerations!["R-OTHER"]);
        // A new incarnation of an otherwise identical fixture must not inherit a finalized choice.
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["R-OTHER"]=s.CardObjects["R-OTHER"] with {ObjectGeneration=s.CardObjects["R-OTHER"].ObjectGeneration+1} } };
        s=(await Top(s)).State;Assert.Equal(9,s.CardObjects["R-OTHER"].Power);Restore(s);
    }

    [Theory]
    [InlineData("P1","RULE_REPLACEMENT","PAY:ANY")]
    [InlineData("P2","WRONG","PAY:ANY")]
    [InlineData("P2","RULE_REPLACEMENT","PAY:FAKE")]
    public async Task WaitingResolutionRejectsUnauthorizedCommandsWithoutChangingState(string actor,string window,string option)
    {
        var s=await Open();var p=s.PendingRuleChoice!;
        var r=await new CoreRuleEngine().ResolveAsync(s,new PlayerIntent("invalid",actor,CommandTypes.PayCost),
            new PayCostCommand(p.Request.Id,window,[option]),default);
        Assert.False(r.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(r.State));Restore(s);
    }

    [Fact]
    public async Task NoEnergyStillOffersDeclineWithoutInventingAnAffordablePayment()
    {
        var initial=Position() with {RunePools=new Dictionary<string,RunePool> { ["P1"]=new(30,30),["P2"]=new(30,1) }};
        var s=await Open(initial);Assert.DoesNotContain(s.PendingRuleChoice!.Request.Options,o=>o.Id.StartsWith("PAY:"));
        s=await Choose(s,"DECLINE");Assert.Empty(s.StackItems);Restore(s);
    }

    [Theory]
    [InlineData("POWER")]
    [InlineData("RUNE")]
    [InlineData("STELE")]
    [InlineData("GOLD")]
    public async Task ResolutionPaymentCanUseCurrentRuneOrReactionResourceSkill(string branch)
    {
        var initial=Position() with {RunePools=new Dictionary<string,RunePool> { ["P1"]=new(30,30),["P2"]=new(30,1) }};
        if(branch=="RUNE") {
            initial=LocalDestructionRecallTests.At(initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
                ["R-RUNE"]=new("R-RUNE",cardNo:"OGN·007/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.RuneCard,"COLOR:red"]) } },"R-RUNE","P2","BASE");
        } else initial=ReplacementResourceTests.AddResource(initial,
            branch=="POWER" ? P4ActivatedAbilityCatalog.PowerSigilCardNo
                : branch=="STELE" ? P4ActivatedAbilityCatalog.AncientSteleCardNo : P4ActivatedAbilityCatalog.GoldTokenUnlCardNo,"R-RESOURCE");
        var s=await Open(initial);
        if(branch=="RUNE")s=await Choose(s,"PAY:RUNE:R-RUNE");
        else {
            var options=s.PendingRuleChoice!.Request.Options.Where(o=>o.Id.StartsWith("RESOURCE:R-RESOURCE:")).ToArray();
            var option=branch=="STELE" ? Assert.Single(options,o=>o.Id.EndsWith(":"+P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix+"1")) : Assert.Single(options);
            s=await Choose(s,option.Id);Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT",s.PendingRuleChoice!.Request.Kind);
            s=await Choose(s,Assert.Single(s.PendingRuleChoice.Request.Options,o=>o.Id.StartsWith("PAY:")).Id);
        }
        s=await Choose(s,"KEEP_ALL");Assert.Equal("P2",Assert.Single(s.StackItems).ControllerId);
        Assert.Equal(0,s.RunePools["P2"].TotalPower);
        if(branch=="RUNE") {Assert.DoesNotContain("R-RUNE",s.PlayerZones["P2"].Base);Assert.Contains("R-RUNE",s.PlayerZones["P2"].RuneDeck);}
        else if(branch=="GOLD")Assert.DoesNotContain("R-RESOURCE",s.PlayerZones["P2"].Base);
        else Assert.True(s.CardObjects["R-RESOURCE"].IsExhausted);
        Restore(s);
    }

    [Fact]
    public async Task EachEchoExecutionCanRemakeItsOwnTargetsWhileRetainingTheOtherExecution()
    {
        const string spell="SFD·080/221";
        var initial=Position(spell);var s=await Open(initial,spell,targets:["R-TARGET"],costs:["ECHO"]);
        s=await Choose(s,"PAY:ANY");
        s=await Choose(s,Option(s,"第 2 次","重选目标 1 为","（R-OTHER）"));
        s=await Choose(s,"CONFIRM");var item=Assert.Single(s.StackItems);Assert.Equal(2,item.RepeatExecutions!.Count);
        Assert.Equal(new[]{"R-TARGET"},item.RepeatExecutions[0].TargetObjectIds);Assert.Equal(new[]{"R-OTHER"},item.RepeatExecutions[1].TargetObjectIds);
        Assert.Equal(new[]{"ECHO"},item.OptionalCosts);Assert.Equal(2,item.EffectRepeatCount);Restore(s);
        s=(await Top(s)).State;Assert.Empty(s.StackItems);Restore(s);
    }

    [Theory]
    [InlineData("OGN·014/298",false)]
    [InlineData("OGN·005/298",true)]
    public async Task FourManaLimitUsesTheSpellsCardCostInsteadOfManaActuallyPaid(string card,bool allowed)
    {
        var initial=Position(card);initial=LocalDestructionRecallTests.At(initial,"R-TARGET","P2","BATTLEFIELD","F");
        initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["F"]=initial.CardObjects["F"] with {ControllerId="P2"} }};
        var played=await Act(initial,"P1",new PlayCardCommand("R-SPELL",card,["R-TARGET"]));
        var handed=await Act(played.State,"P1",new PassPriorityCommand());
        var command=new PlayCardCommand("REBUTTAL",Card,[Assert.Single(played.State.StackItems).StackItemId]);
        var r=await new CoreRuleEngine().ResolveAsync(handed.State,new PlayerIntent("limit","P2",CommandTypes.PlayCard),command,default);
        Assert.Equal(allowed,r.Accepted);
        if(!allowed)Assert.Equal(MatchStateHasher.Hash(handed.State),MatchStateHasher.Hash(r.State));
        else Assert.Equal(2,r.State.StackItems.Count);Restore(r.State);
    }

    [Fact]
    public async Task ActivatedAbilityCannotBeChosenAsASpell()
    {
        var initial=Position();initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["R-ORB"]=new("R-ORB",cardNo:OrbOfRegretTests.Card,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]) }};
        initial=LocalDestructionRecallTests.At(initial,"R-ORB","P1","BASE");
        var activated=await Act(initial,"P1",new ActivateAbilityCommand("R-ORB",OrbOfRegretTests.Ability,["R-TARGET"]));
        var handed=await Act(activated.State,"P1",new PassPriorityCommand());
        var command=new PlayCardCommand("REBUTTAL",Card,[Assert.Single(activated.State.StackItems).StackItemId]);
        var r=await new CoreRuleEngine().ResolveAsync(handed.State,new PlayerIntent("ability","P2",CommandTypes.PlayCard),command,default);
        Assert.False(r.Accepted);Assert.Equal(MatchStateHasher.Hash(handed.State),MatchStateHasher.Hash(r.State));Restore(r.State);
    }
}
