using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CurrentPowerTargetTests
{
    internal static MatchState Position(string spell, int power, bool aura = false, bool two = false, string auraZone = "BATTLEFIELD")
    {
        var s = NoReplacement(two);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["AOE"] = s.CardObjects["AOE"] with { CardNo = spell },
            ["D"] = s.CardObjects["D"] with { Power = power, Damage = 0 } } };
        if (two) s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D2"] = s.CardObjects["D2"] with { Power = power, Damage = 0 } } };
        if (aura) s = At(Source(s, "OGS·013/024", "GAREN"), "GAREN", "P2", auraZone, auraZone == "BATTLEFIELD" ? "BF" : null);
        return s;
    }

    internal static JsonElement Snapshot(MatchState s, string player = "P1") => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(s)[player], new JsonSerializerOptions(JsonSerializerDefaults.Web));
    internal static int DisplayPower(MatchState s, string id = "D") => Snapshot(s).GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    internal static PlayCostQuoteDto Quote(MatchState s, params string[] ids) {
        var p = ResolutionResult.BuildPrompts(s)["P1"];
        return new CoreRuleEngine().PreviewPlayCard(s, "P1", new("quote", p.PromptId!, s.Tick, new("AOE", s.CardObjects["AOE"].CardNo!, ids)));
    }

    [Theory]
    [InlineData("SFD·162/221",2,true,false)]
    [InlineData("UNL-159/219",3,true,false)]
    [InlineData("OGN·169/298",3,true,false)]
    [InlineData("UNL-159/219",2,true,true)]
    [InlineData("UNL-159/219",3,false,true)]
    public async Task SingleCapUsesAuraAtPreviewAndSubmission(string spell,int power,bool aura,bool legal)
    {
        var s=Position(spell,power,aura); var hash=MatchStateHasher.Hash(s);
        Assert.Equal(power+(aura?1:0),DisplayPower(s)); Assert.Equal(legal,Quote(s,"D").IsValid);
        var prompt=JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(s)["P1"],new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var offered=prompt.GetProperty("candidates").EnumerateArray().Where(c=>c.GetProperty("action").GetString()=="PLAY_CARD")
            .Where(c=>c.TryGetProperty("targets",out var targets) && targets.ValueKind==JsonValueKind.Array)
            .SelectMany(c=>c.GetProperty("targets").EnumerateArray()).Any(t=>t.GetProperty("id").GetString()=="D");
        Assert.Equal(legal,offered);
        var result=await new CoreRuleEngine().ResolveAsync(s,new("single","P1",CommandTypes.PlayCard),new PlayCardCommand("AOE",spell,["D"]),default);
        Assert.Equal(legal,result.Accepted); if(!legal) Assert.Equal(hash,MatchStateHasher.Hash(result.State));
        else { result=await Top(result.State); Assert.True(result.State.PlayerZones["P2"].Graveyard.Contains("D") || result.State.PlayerZones["P2"].Hand.Contains("D")); Restore(result.State); }
    }

    [Theory]
    [InlineData(0)] [InlineData(-2)]
    public async Task ZeroAndNegativePowerRemainLegalTargets(int power)
    {
        var s=Position("UNL-159/219",power); Assert.Equal(0,DisplayPower(s)); Assert.True(Quote(s,"D").IsValid);
        var done=await OfficialSettReplacementTests.Open(s,"UNL-159/219","D"); Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard); Restore(done.State);
    }

    [Theory]
    [InlineData(2,true,false)] [InlineData(1,true,true)] [InlineData(2,false,true)] [InlineData(0,false,true)] [InlineData(-2,false,true)]
    public async Task TotalCapSumsCurrentPowerIncludingZero(int power,bool aura,bool legal)
    {
        var s=Position("OGN·256/298",power,aura,true); var hash=MatchStateHasher.Hash(s);
        Assert.Equal(legal,Quote(s,"D","D2").IsValid);
        var result=await new CoreRuleEngine().ResolveAsync(s,new("total","P1",CommandTypes.PlayCard),new PlayCardCommand("AOE","OGN·256/298",["D","D2"]),default);
        Assert.Equal(legal,result.Accepted); if(!legal) Assert.Equal(hash,MatchStateHasher.Hash(result.State));
        else {result=await Top(result.State);Assert.Contains("D",result.State.PlayerZones["P2"].Graveyard);Assert.Contains("D2",result.State.PlayerZones["P2"].Graveyard);Restore(result.State);}
    }

    [Fact]
    public async Task AuraAtAnotherLocationDoesNotChangeTargetThreshold()
    {
        var s=Position("UNL-159/219",3,true,auraZone:"BASE"); Assert.Equal(3,DisplayPower(s));
        Assert.True(Quote(s,"D").IsValid);var done=await OfficialSettReplacementTests.Open(s,"UNL-159/219","D");Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void BothPlayersSeeTheSameLocalAuraInBaseOrBattlefield(bool inBase)
    {
        var s=Position("UNL-159/219",3,true);
        if(inBase) s=At(At(s,"D","P2","BASE"),"GAREN","P2","BASE");
        foreach(var player in new[]{"P1","P2"}) Assert.Equal(4,Snapshot(s,player).GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty("D").GetProperty("effectivePower").GetInt32());
    }

    [Fact]
    public async Task ResolutionRechecksPowerAfterAResponseChangesAuraLocation()
    {
        var s=Position("UNL-159/219",3,true,auraZone:"BASE");
        var cast=await Act(s,"P1",new PlayCardCommand("AOE","UNL-159/219",["D"]));
        // Seed only the response's resulting field placement; resolve the real spell.
        var changed=At(cast.State,"GAREN","P2","BATTLEFIELD","BF");
        var done=await Top(changed);Assert.Contains("D",done.State.PlayerZones["P2"].Battlefields);Restore(done.State);
    }
    [Theory]
    [InlineData(true,false)] [InlineData(true,true)] [InlineData(false,false)]
    public async Task BattleProjectionIncludesRolePowerButStunOnlySuppressesDamage(bool defenderBonus,bool stunned)
    {
        var s=Position("OGN·169/298",2);
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["D"]=s.CardObjects["D"] with {Tags=defenderBonus?[CardObjectTags.UnitCard,"坚守2"]:[CardObjectTags.UnitCard,"突袭2"], UntilEndOfTurnEffects=stunned?["STUNNED"]:[]}}};
        Assert.Equal(2,DisplayPower(s));
        var moved=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var focus=await Act(moved.State,"P1",new PassFocusCommand());focus=await Act(focus.State,"P2",new PassFocusCommand());
        var battle=await Act(focus.State,"P1",new DeclareBattleCommand("BF",["A"],["D"],["COMBAT_ASSIGNMENT"]));
        Assert.True(battle.State.BattleState.IsActive);
        Assert.Equal(defenderBonus?4:2,DisplayPower(battle.State));
        Restore(battle.State);
    }

}
