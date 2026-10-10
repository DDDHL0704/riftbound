using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ReplacementResourceTests
{
    internal static MatchState AddResource(MatchState s, string no, string id = "RESOURCE", bool exhausted = false) => s with {
        PlayerZones = new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = s.PlayerZones["P2"].Base.Append(id).ToArray() } },
        CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { [id] = P6TokenFactoryCatalog.TryGetByCardNo(no,out var token) ? token.CreateObject(id,"P2","P2",exhausted) : new(id,power:no==P4ActivatedAbilityCatalog.DragonSoulSageCardNo ? 2 : 0,cardNo:no,ownerId:"P2",controllerId:"P2",isExhausted:exhausted,tags:[no==P4ActivatedAbilityCatalog.DragonSoulSageCardNo ? CardObjectTags.UnitCard : CardObjectTags.EquipmentCard]) },
        ObjectLocations = new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { [id] = new("P2","BASE") } };

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenSfdCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.HoneyfruitCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.RageSigilCardNo)]
    public async Task ResourceSkillIsOfferedWhenPoolAndRunesCannotPay(string no)
    {
        var opened = await Open(AddResource(Position(power:0),no));
        Assert.NotNull(opened.State.PendingRuleChoice);
        Assert.Contains(opened.State.PendingRuleChoice.Request.Options,o=>o.Id.StartsWith("RESOURCE:"));
    }

    internal static Task<ResolutionResult> Resource(MatchState s, string id = "RESOURCE", string costs = "")
    {
        var request = s.PendingRuleChoice!.Request;
        var option = request.Options.Single(o => o.Id.StartsWith($"RESOURCE:{id}:") && o.Id.EndsWith(":" + costs));
        return Act(s, request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
    }

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, true)]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenSfdCardNo, true)]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, false)]
    [InlineData(P4ActivatedAbilityCatalog.GoldTokenSfdCardNo, false)]
    public async Task GoldProducesOnceAndCanPayOrRemainAfterDeclining(string no, bool accept)
    {
        var opened = await Open(AddResource(Position(power:0), no));
        var generated = await Resource(opened.State); Restore(generated.State);
        Assert.Single(generated.State.PendingRuleChoice!.Answers);
        Assert.Contains("1 符能", generated.State.PendingRuleChoice.Request.Reason);
        Assert.Equal(0, generated.State.RunePools["P2"].Power); // checkpoint board is frozen
        Assert.Contains("RESOURCE", generated.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(generated.State.PendingRuleChoice.Request.Options, o=>o.Id.StartsWith("RESOURCE:"));
        var restored = OfficialInsightAndSpellLockTests.Restore(generated.State);
        var done = await Choose(restored, accept ? "D" : "DECLINE"); Restore(done.State);
        Assert.Equal(accept, done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.DoesNotContain("RESOURCE", done.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("RESOURCE", done.State.CardObjects.Keys);
        Assert.Single(done.Events,e=>e.Kind=="TOKEN_CEASED_TO_EXIST");
        Assert.Equal(accept ? 0 : 1, done.State.RunePools["P2"].Power);
        Assert.Equal(7, done.State.RunePools["P2"].Mana);
        Assert.Single(done.Events, e=>e.Kind=="POWER_GAINED");
        Assert.Single(done.Events, e=>e.Kind=="ABILITY_ACTIVATED");
        Assert.Equal(generated.State.Tick+1, done.State.Tick);
        if (!accept) Assert.Contains("DRAW", (await Top(done.State)).State.PlayerZones["P2"].Hand);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HoneyfruitUsesCurrentExperienceAndExhaustsOnlyOnce(bool bonus)
    {
        var s = AddResource(Position(power:0), P4ActivatedAbilityCatalog.HoneyfruitCardNo);
        s = s with { PlayerExperience = new Dictionary<string,int> { ["P1"] = 0, ["P2"] = bonus ? 6 : 5 } };
        var opened = await Open(s);
        Assert.Equal(bonus ? 2 : 1, opened.State.PendingRuleChoice!.Request.Options.Count(o=>o.Id.StartsWith("RESOURCE:")));
        var generated = await Resource(opened.State, costs: bonus ? P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix+"RESOURCE" : "");
        Restore(generated.State);
        var done = await Choose(generated.State); Restore(done.State);
        Assert.True(done.State.CardObjects["RESOURCE"].IsExhausted);
        Assert.Equal(bonus ? 8 : 7, done.State.RunePools["P2"].Mana);
        Assert.Equal(0, done.State.RunePools["P2"].Power);
        Assert.Single(done.Events, e=>e.Kind=="RESOURCE_SKILL_RESOLVED");
    }

    [Fact]
    public async Task SigilProducesTypedPowerAndChoicePreservesOtherResources()
    {
        var s = AddResource(Position(power:0), P4ActivatedAbilityCatalog.RageSigilCardNo);
        s = s with { RunePools = new Dictionary<string,RunePool>(s.RunePools) { ["P2"] = new(7,0,new Dictionary<string,int> { ["blue"] = 2 }) } };
        var generated = await Resource((await Open(s)).State); Restore(generated.State);
        var done = await Choose(generated.State, payment:"TRAIT:red"); Restore(done.State);
        Assert.Equal(2, done.State.RunePools["P2"].PowerByTrait["blue"]);
        Assert.Equal(0, done.State.RunePools["P2"].PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(7, done.State.RunePools["P2"].Mana);
    }

    [Fact]
    public async Task MultipleResourceStepsRecomputeConversionsAndReplayAfterEveryCheckpoint()
    {
        var s = AddResource(AddResource(Position(power:0,mana:0), P4ActivatedAbilityCatalog.EnergyChannelCardNo,"CHANNEL"), P4ActivatedAbilityCatalog.AncientSteleCardNo,"STELE");
        var opened = await Open(s);
        Assert.DoesNotContain(opened.State.PendingRuleChoice!.Request.Options,o=>o.Id.StartsWith("RESOURCE:STELE:"));
        var mana = await Resource(opened.State,"CHANNEL"); Restore(mana.State);
        var power = await Resource(OfficialInsightAndSpellLockTests.Restore(mana.State),"STELE",P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix+"1");
        Restore(power.State); Assert.Equal(2,power.State.PendingRuleChoice!.Answers.Count);
        var done = await Choose(OfficialInsightAndSpellLockTests.Restore(power.State)); Restore(done.State);
        Assert.Equal(0,done.State.RunePools["P2"].Mana); Assert.Equal(0,done.State.RunePools["P2"].TotalPower);
        Assert.True(done.State.CardObjects["CHANNEL"].IsExhausted); Assert.True(done.State.CardObjects["STELE"].IsExhausted);
        Assert.Equal(2,done.Events.Count(e=>e.Kind=="RESOURCE_SKILL_RESOLVED"));
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("face-down")]
    [InlineData("enemy")]
    [InlineData("standby")]
    [InlineData("wrong-type")]
    [InlineData("wrong-card")]
    [InlineData("swift")]
    [InlineData("restricted")]
    public async Task IllegalSourcesCannotBeUsedDuringReplacementPayment(string reason)
    {
        var s = AddResource(Position(power:1), P4ActivatedAbilityCatalog.HoneyfruitCardNo);
        var card = s.CardObjects["RESOURCE"];
        card = reason switch {
            "exhausted" => card with { IsExhausted = true },
            "face-down" => card with { IsFaceDown = true },
            "enemy" => card with { ControllerId = "P1" },
            "standby" => card with { Tags = [CardObjectTags.EquipmentCard,CardObjectTags.Standby] },
            "wrong-type" => card with { Tags = [CardObjectTags.UnitCard] },
            "wrong-card" => card with { CardNo = "OGN·096/298" },
            "swift" => card with { CardNo = P4ActivatedAbilityCatalog.MalzaharCardNo,Tags = [CardObjectTags.UnitCard] },
            "restricted" => card with { CardNo = P4ActivatedAbilityCatalog.LuxCardNo,Tags = [CardObjectTags.UnitCard] },
            _ => card };
        s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { ["RESOURCE"] = card } };
        var opened = await Open(s);
        Assert.DoesNotContain(opened.State.PendingRuleChoice!.Request.Options,o=>o.Id.StartsWith("RESOURCE:"));
        var request = opened.State.PendingRuleChoice.Request;
        var result = await new CoreRuleEngine().ResolveAsync(opened.State,new("bad","P2",CommandTypes.PayCost),
            new PayCostCommand(request.Id,"RULE_REPLACEMENT",[$"RESOURCE:RESOURCE:{P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId}:"]),default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ResourceTranscriptCannotBeForgedAndPublicRecoveryRemainsConsistent()
    {
        var generated = await Resource((await Open(AddResource(Position(power:0), P4ActivatedAbilityCatalog.GoldTokenUnlCardNo))).State);
        var s = generated.State; var pending = s.PendingRuleChoice!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { PendingRuleChoice = pending with {
            Answers = [pending.Answers[0] with { OptionId = "DECLINE" }] } }));
        var views = s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,generated.Snapshots[id],s.Tick,0,generated.Prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        var errors = MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator);
        Assert.True(errors.Count==0,string.Join("; ",errors));
        var publicJson = System.Text.Json.JsonSerializer.Serialize(new { views,spectator });
        Assert.DoesNotContain("pendingRuleChoice",publicJson,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Origin",publicJson,StringComparison.Ordinal);
    }
    [Fact]
    public async Task DirectDestructionRetainsResourceEventsWhenReplacementIsDeclined()
    {
        var initial = AddResource(Position(power:0),P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["DRAW2"] = initial.CardObjects["DRAW"] with { ObjectId = "DRAW2" } },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["DRAW","DRAW2"] } } };
        var opened = await Open(initial,"OGN·213/298","D");
        var generated = await Resource(opened.State);
        var done = await Choose(generated.State,"DECLINE"); Restore(done.State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1,done.State.RunePools["P2"].Power);
        Assert.Single(done.Events,e=>e.Kind=="ABILITY_ACTIVATED");
        Assert.Single(done.Events,e=>e.Kind=="POWER_GAINED");
        Assert.Single(done.Events,e=>e.Kind=="UNIT_DESTROYED" && e.Payload.GetValueOrDefault("targetObjectId") as string=="D");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task PowerToManaConversionUsesCurrentPoolAndRetainsUnusedPower(int amount)
    {
        var opened = await Open(AddResource(Position(power:3,mana:0),P4ActivatedAbilityCatalog.HextechAnomalyCardNo));
        var generated = await Resource(opened.State,costs:P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix+amount);
        var done = generated.State.PendingRuleChoice is null ? generated : await Choose(generated.State,"DECLINE");
        Restore(done.State);
        Assert.Equal(amount,done.State.RunePools["P2"].Mana);
        Assert.Equal(3-amount,done.State.RunePools["P2"].Power);
        Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);
        Assert.Single(done.Events,e=>e.Kind=="RESOURCE_SKILL_RESOLVED");
    }

    [Fact]
    public async Task SageCanGenerateManaThenSteleConvertsItToReplacementPower()
    {
        var s = AddResource(AddResource(Position(power:0,mana:0),P4ActivatedAbilityCatalog.DragonSoulSageCardNo,"SAGE"),P4ActivatedAbilityCatalog.AncientSteleCardNo,"STELE");
        var opened = await Open(s,"OGN·213/298","D");
        var generated = await Resource(opened.State,"SAGE");
        var converted = await Resource(generated.State,"STELE",P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix+"1");
        var done = await Choose(converted.State); Restore(done.State);
        Assert.True(done.State.CardObjects["SAGE"].IsExhausted);
        Assert.Equal(0,done.State.RunePools["P2"].TotalPower);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base);
        Assert.Equal(2,done.Events.Count(e=>e.Kind=="RESOURCE_SKILL_RESOLVED"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnimatedGoldDestructionPreservesDeathLedgerAndEarnedTriggers(bool endsMatch)
    {
        var s=AddResource(Position(power:0),P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["RESOURCE"]=s.CardObjects["RESOURCE"] with { Power=5,Tags=[..s.CardObjects["RESOURCE"].Tags,CardObjectTags.UnitCard] },
            ["VIKTOR"]=new("VIKTOR",power:4,cardNo:"ARC-006/006",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"]=s.PlayerZones["P2"] with { Base=s.PlayerZones["P2"].Base.Append("VIKTOR").ToArray() } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["VIKTOR"]=new("P2","BASE") } };
        var generated=await Resource((endsMatch ? await Open(s,"OGN·213/298","D") : await Open(s)).State);
        var done=await Choose(generated.State);
        Assert.True(OfficialInsightAndSpellLockTests.Errors(done.State).Count==0,string.Join("; ",OfficialInsightAndSpellLockTests.Errors(done.State))); Restore(done.State);
        Assert.Contains("P2",done.State.DestroyedUnitOwnerIdsThisTurn);
        Assert.Single(done.Events,e=>e.Kind=="UNIT_DESTROYED" && e.Payload.GetValueOrDefault("targetObjectId") as string=="RESOURCE");
        if (endsMatch) { Assert.Equal(MatchStatuses.Finished,done.State.Status); Assert.Empty(done.State.TriggerQueue); }
        else Assert.Contains(done.State.StackItems,t=>t.SourceObjectId=="VIKTOR");
        if (!endsMatch) Assert.Single(done.Events,e=>e.Kind=="TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string=="VIKTOR");
    }
}
