using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class ArmamentMightTests
{
    internal static MatchState Position(string gear="SFD·022/221",string unit="OGN·001/298",int power=4,bool attached=false)
    {
        var s=OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·278/298"),"HOST",unit,"F","P1");
        s=s with {Phase=MatchPhases.Main,TurnStartStep=null,TimingState=TimingStates.NeutralOpen,
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["HOST"]=s.CardObjects["HOST"] with {Power=power},
                ["GEAR"]=new("GEAR",cardNo:gear,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard,CardEquipmentKeywordNames.Weapon],attachedToObjectId:attached?"HOST":null)},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(20,20)}};
        if (attached && gear == "SFD·059/221") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["GEAR"] = s.CardObjects["GEAR"] with { AttachmentRevision = 1, AttachedRuleText = new(new("HOST", 0), unit, 1, s.Tick, 0) } } };
        return At(At(s,"HOST","P1","BASE"),"GEAR","P1","BASE");
    }
    internal static int Might(MatchState s)=>CurrentPowerTargetTests.Snapshot(s).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST").GetProperty("effectivePower").GetInt32();
    [Fact]
    public async Task RealLongSwordAssemblyAddsPrintedBonusWithoutChangingHostBase()
    {
        var s=Position();Assert.Equal(4,Might(s));
        var attached=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        attached=await EquipTestDriver.FinishAsync(attached);
        Assert.Equal(6,Might(attached.State));Assert.Equal(4,attached.State.CardObjects["HOST"].Power);Restore(attached.State);PublicRecovery(attached.State);
    }
    [Fact]
    public void MechFanDoublesTheArmamentsBaseBonus()
    {
        var s=Position("SFD·033/221","SFD·068/221",3,true);Assert.Equal(5,Might(s));Restore(s);PublicRecovery(s);
    }

    internal static void PublicRecovery(MatchState s)
    {
        var snapshots=ResolutionResult.BuildSnapshots(s);var prompts=ResolutionResult.BuildPrompts(s);
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,snapshots[id],s.Tick,0,prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        var errors=MatchRecoveryValidator.ValidateComponentStateAndViews(s,views,spectator);
        Assert.True(errors.Count==0,string.Join("; ",errors));
    }
    [Fact]
    public async Task ExistingDynamicMightProjectionPassesCompletePublicRecoveryValidation()
    {
        var opened=await Act(MundoBeginningTests.Position(5),"P1",new PassPriorityCommand());PublicRecovery(opened.State);
    }

    [Theory]
    [InlineData("powerDelta")] [InlineData("effectivePower")] [InlineData("sourceCardNo")]
    [InlineData("effectId")] [InlineData("sourcePath")] [InlineData("condition")] [InlineData("lifecycle")]
    public async Task CompletePublicRecoveryRejectsForgedDynamicAuraValues(string key)
    {
        var opened=await Act(MundoBeginningTests.Position(5),"P1",new PassPriorityCommand());var s=opened.State;PublicRecovery(s);
        var snapshots=ResolutionResult.BuildSnapshots(s);var prompts=ResolutionResult.BuildPrompts(s);
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,snapshots[id],s.Tick,0,prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        foreach(var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var effects=Assert.IsAssignableFrom<IEnumerable<Dictionary<string,object?>>>(snapshot.Timing["continuousEffects"]);
            var aura=effects.Single(e=>Equals(e.GetValueOrDefault("effectKind"),StaticAuraKinds.SourceGraveyardCountPower));var original=aura[key];
            aura[key]=key is "powerDelta" or "effectivePower"?99:"FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(s,views,spectator));aura[key]=original;
        }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task ZeroAndSingleCardDynamicAurasPassPublicRecovery(int count)
    {
        var opened=await Act(MundoBeginningTests.Position(count),"P1",new PassPriorityCommand());PublicRecovery(opened.State);
    }

    // Expected bonuses transcribed from the independent official EN lower-box fields;
    // the CN Boneshiver alternate was additionally checked against its card image.
    [Theory]
    [InlineData("SFD·009/221", 0)]
    [InlineData("SFD·016/221", 0)]
    [InlineData("SFD·022/221", 2)]
    [InlineData("SFD·030/221", 2)]
    [InlineData("SFD·033/221", 1)]
    [InlineData("SFD·042/221", 1)]
    [InlineData("SFD·051/221", 1)]
    [InlineData("SFD·056/221", 3)]
    [InlineData("SFD·059/221", 0)]
    [InlineData("SFD·064/221", 0)]
    [InlineData("SFD·073/221", 1)]
    [InlineData("SFD·086/221", 2)]
    [InlineData("SFD·090/221", 2)]
    [InlineData("SFD·095/221", 2)]
    [InlineData("SFD·102/221", 1)]
    [InlineData("SFD·108/221", 1)]
    [InlineData("SFD·115/221", 2)]
    [InlineData("SFD·118/221", 2)]
    [InlineData("SFD·118a/221·P", 2)]
    [InlineData("SFD·124/221", 1)]
    [InlineData("SFD·133/221", 2)]
    [InlineData("SFD·134/221", 1)]
    [InlineData("SFD·139/221", 2)]
    [InlineData("SFD·150/221", 2)]
    [InlineData("SFD·153/221", 0)]
    [InlineData("SFD·161/221", 3)]
    [InlineData("SFD·172/221", 1)]
    [InlineData("SFD·178/221", 4)]
    [InlineData("SFD·186/221", 3)]
    [InlineData("SFD·190/221", 3)]
    [InlineData("SFD·191/221", 3)]
    [InlineData("SFD·192/221", 2)]
    [InlineData("UNL-019/219", 4)]
    [InlineData("UNL-039/219", 1)]
    [InlineData("UNL-096/219", 2)]
    [InlineData("UNL-158/219", 2)]
    [InlineData("UNL-188/219", 3)]
    public void EveryCurrentArmamentPrintAddsItsOfficialBaseBonusOnlyWhileAttached(string card,int bonus)
    {
        var s=Position(card,attached:true);Assert.Equal(4+bonus,Might(s));PublicRecovery(s);Restore(s);
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["GEAR"]=s.CardObjects["GEAR"] with {AttachedToObjectId=null,AttachedRuleText=null}}};
        Assert.Equal(4,Might(s));PublicRecovery(s);Restore(s);
    }
    [Fact]
    public async Task DestroyingActualArmamentRemovesBonusAndCanKillDamagedHost()
    {
        var s=Position(attached:true);s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["HOST"]=s.CardObjects["HOST"] with {Damage=5}}};
        s=OfficialCopyIdentityTests.AddSpell(s,"KILL-GEAR","SFD·005/221");Assert.Equal(6,Might(s));
        var cast=await Act(s,"P1",new PlayCardCommand("KILL-GEAR","SFD·005/221",["GEAR"]));var done=await Top(cast.State);
        Assert.Contains("GEAR",done.State.PlayerZones["P1"].Graveyard);Assert.Contains("HOST",done.State.PlayerZones["P1"].Graveyard);Restore(done.State);PublicRecovery(done.State);
    }
    [Fact]
    public async Task RealDetachSpellRemovesBonusButKeepsArmamentOnField()
    {
        var s=OfficialCopyIdentityTests.AddSpell(Position(attached:true),"DETACH","SFD·011/221");
        var cast=await Act(s,"P1",new PlayCardCommand("DETACH","SFD·011/221",["HOST","GEAR"]));var done=await Top(cast.State);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Contains("GEAR",done.State.PlayerZones["P1"].Base);
        Assert.Equal(4,Might(done.State));Restore(done.State);PublicRecovery(done.State);
    }
    [Fact]
    public void OpponentControlledAttachedArmamentStillIncreasesHostsMight()
    {
        var s=At(At(Position(attached:true),"HOST","P1","BATTLEFIELD","F"),"GEAR","P2","BATTLEFIELD","F");
        Assert.Equal(6,Might(s));Restore(s);PublicRecovery(s);
    }
    [Theory]
    [InlineData("hidden")] [InlineData("off-field")] [InlineData("wrong-host")]
    public void InactiveOrUnrelatedArmamentCannotIncreaseMight(string change)
    {
        var s=Position(attached:true);
        if(change=="hidden")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["GEAR"]=s.CardObjects["GEAR"] with {IsFaceDown=true}}};
        else if(change=="off-field")s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Base=["HOST"],Hand=["GEAR"]}}};
        else s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["GEAR"]=s.CardObjects["GEAR"] with {AttachedToObjectId="UNRELATED"}}};
        Assert.Equal(4,Might(s));
    }
    [Fact]
    public async Task WorldAtlasOfficialWeaponIdentitySurvivesRealPlayAndAssembly()
    {
        var s=Position("SFD·086/221");s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Base=["HOST"],Hand=["GEAR"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["GEAR"]=new("P1","HAND")},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["GEAR"]=s.CardObjects["GEAR"] with {Tags=[CardObjectTags.EquipmentCard]}}};
        var play=await Act(s,"P1",new PlayCardCommand("GEAR","SFD·086/221",[]));Assert.Contains(CardEquipmentKeywordNames.Weapon,play.State.CardObjects["GEAR"].Tags);
        var attached=await Act(play.State,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_BLUE"]));
        attached=await EquipTestDriver.FinishAsync(attached);
        Assert.Equal(6,Might(attached.State));Restore(attached.State);PublicRecovery(attached.State);
    }

    [Fact]
    public void OfficialArmamentBonusChangesTargetCostPreviewEligibility()
    {
        var s=CurrentPowerTargetTests.Position("SFD·162/221",2);
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["ARMOR"]=new("ARMOR",cardNo:"SFD·033/221",ownerId:"P2",controllerId:"P2",attachedToObjectId:"D",tags:[CardObjectTags.EquipmentCard,CardEquipmentKeywordNames.Weapon])}};
        s=At(s,"ARMOR","P2",s.ObjectLocations["D"].Zone,s.ObjectLocations["D"].BattlefieldObjectId);
        Assert.False(CurrentPowerTargetTests.Quote(s,"D").IsValid);Assert.Equal(3,CurrentPowerTargetTests.DisplayPower(s));
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["ARMOR"]=s.CardObjects["ARMOR"] with {AttachedToObjectId=null}}};
        // An unattached armament on a battlefield requires cleanup; place it in base before requesting a playable quote.
        s=At(s,"ARMOR","P2","BASE");
        var quote=CurrentPowerTargetTests.Quote(s,"D");Assert.True(quote.IsValid,quote.ErrorCode+": "+quote.Message);Assert.Equal(2,CurrentPowerTargetTests.DisplayPower(s));
    }
    [Theory]
    [InlineData("powerDelta")] [InlineData("effectivePower")] [InlineData("sourceCardNo")] [InlineData("targetObjectId")]
    public void PublicRecoveryRejectsForgedArmamentContribution(string key)
    {
        var s=Position(attached:true);PublicRecovery(s);
        var snapshots=ResolutionResult.BuildSnapshots(s);var prompts=ResolutionResult.BuildPrompts(s);
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,snapshots[id],s.Tick,0,prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        foreach(var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var aura=Assert.Single(Assert.IsAssignableFrom<IEnumerable<Dictionary<string,object?>>>(snapshot.Timing["continuousEffects"]));var original=aura[key];
            aura[key]=key is "powerDelta" or "effectivePower"?99:"FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(s,views,spectator));aura[key]=original;
        }
    }
}
