using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialReflexiveCopyTests
{
    [Fact]
    public async Task ImageEntersAsZeroBeforeAnIndependentResponseWindow()
    {
        var opened = await Open();
        var token = opened.State.CardObjects["C-TOKEN-001"];
        Assert.Equal("UNL·T06", token.CardNo); Assert.Equal(0, token.Power); Assert.False(token.IsExhausted);
        Assert.DoesNotContain(CardObjectTags.Ephemeral, token.Tags);
        Assert.Contains("C", opened.State.PlayerZones["P1"].Graveyard);
        Assert.NotNull(opened.State.StackItems.Single().ReflexiveCopy); Restore(opened.State);
        Evidence("copy", opened);
        var first = await Act(opened.State, "P1", new PassPriorityCommand());
        Assert.Equal(0, first.State.CardObjects["C-TOKEN-001"].Power);
        var done = await Act(first.State, "P2", new PassPriorityCommand());
        Assert.Equal("SFD·068/221", done.State.CardObjects["C-TOKEN-001"].CardNo);
        Assert.Equal(3, done.State.CardObjects["C-TOKEN-001"].Power);
        Assert.Contains(CardObjectTags.Ephemeral, done.State.CardObjects["C-TOKEN-001"].Tags); Restore(done.State);
    }

    [Fact]
    public async Task ReturningSourceInResponseLeavesZeroImage()
    {
        var opened = await Open(); var state = opened.State;
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["BOUNCE"] = new("BOUNCE", cardNo:"OGN·104/298", ownerId:"P2", controllerId:"P2", tags:[CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Hand = ["BOUNCE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["BOUNCE"] = new("P2", "HAND") } };
        var first = await Act(state, "P1", new PassPriorityCommand());
        var cast = await Act(first.State, "P2", new PlayCardCommand("BOUNCE", "OGN·104/298", ["TARGET"]));
        var bounced = await Top(cast.State); Assert.Contains("TARGET", bounced.State.PlayerZones["P2"].Hand); Restore(bounced.State);
        var done = await Top(bounced.State);
        Assert.Equal("UNL·T06", done.State.CardObjects["C-TOKEN-001"].CardNo);
        Assert.Equal(0, done.State.CardObjects["C-TOKEN-001"].Power);
        Assert.Contains(CardObjectTags.Ephemeral, done.State.CardObjects["C-TOKEN-001"].Tags); Restore(done.State);
    }

    [Fact]
    public async Task CopyPreservesExistingStatusControlDamageAndGrantedPower()
    {
        var opened = await Open(); var s = opened.State;
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["C-TOKEN-001"] = s.CardObjects["C-TOKEN-001"] with { Power=2, UntilEndOfTurnPowerModifier=2, IsExhausted=true,
                UntilEndOfTurnEffects=["STUNNED"], Tags=[CardObjectTags.UnitCard,"游走"], Damage=1, ControllerId="P2" } },
            PlayerZones = new Dictionary<string,PlayerZones>(s.PlayerZones) {
                ["P1"]=s.PlayerZones["P1"] with {Base=[]}, ["P2"]=s.PlayerZones["P2"] with {Base=s.PlayerZones["P2"].Base.Append("C-TOKEN-001").ToArray()} },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["C-TOKEN-001"]=new("P2","BASE")} };
        var done=await Top(s); var token=done.State.CardObjects["C-TOKEN-001"];
        Assert.Equal("P2", token.ControllerId); Assert.Equal("P1", token.OwnerId); Assert.Equal(1,token.Damage);
        Assert.Equal(5,token.Power); Assert.Equal(2,token.UntilEndOfTurnPowerModifier); Assert.True(token.IsExhausted);
        Assert.Contains("STUNNED",token.UntilEndOfTurnEffects); Assert.Contains("游走",token.Tags); Restore(done.State);
    }

    [Fact]
    public async Task ImageRemovedDuringResponseCannotBeRecreatedByCopy()
    {
        var opened = await Open();
        var s = OfficialCopyIdentityTests.AddSpell(opened.State, "LEAVE", "OGN·104/298");
        var cast = await Act(s, "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"]));
        var left = await Top(cast.State);
        Assert.False(left.State.CardObjects.ContainsKey("C-TOKEN-001")); Restore(left.State);
        var done = await Top(left.State);
        Assert.False(done.State.CardObjects.ContainsKey("C-TOKEN-001"));
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_COPY_RESOLVED"); Restore(done.State);
    }

    [Theory]
    [InlineData("TARGET")]
    [InlineData("C-TOKEN-001")]
    public async Task BindingsDoNotAffectANewerObjectWithTheSameId(string changed)
    {
        var opened = await Open();
        var s = opened.State with { CardObjects = new Dictionary<string,CardObjectState>(opened.State.CardObjects) {
            [changed]=opened.State.CardObjects[changed] with {ObjectGeneration=opened.State.CardObjects[changed].ObjectGeneration+2} } };
        Restore(s);
        var done = await Top(s);
        Assert.Equal(0, done.State.CardObjects["C-TOKEN-001"].Power);
        Assert.Equal(changed == "TARGET", done.State.CardObjects["C-TOKEN-001"].Tags.Contains(CardObjectTags.Ephemeral)); Restore(done.State);
    }

    [Fact]
    public async Task RecoveryRejectsForgedOrMissingCopyBindings()
    {
        var s = (await Open()).State; var item=s.StackItems.Single(); var c=item.ReflexiveCopy!;
        foreach (var invalid in new[] {
            item with {ReflexiveCopy=null}, item with {EffectKind="DRAW_ONE"},
            item with {ReflexiveCopy=c with {Recipients=[]}},
            item with {ReflexiveCopy=c with {Recipients=[new("TARGET",0)]}},
            item with {ReflexiveCopy=c with {Recipients=[new("C-TOKEN-001",999)]}},
            item with {ReflexiveCopy=c with {CopySource=new("TARGET",999)}},
            item with {ReflexiveCopy=c with {Recipients=[c.Recipients[0],c.Recipients[0]]}} })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=[invalid]}),e=>e.Contains("reflexive copy"));
    }

    internal static void Evidence(string name, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_COPY_ENTRY_EVIDENCE") is not {Length:>0} root) return;
        Directory.CreateDirectory(root);
        foreach(var player in new[]{"P1","P2"})
            File.WriteAllText(Path.Combine(root,name+"-"+player+"-snapshot.json"),System.Text.Json.JsonSerializer.Serialize(
                result.Snapshots[player], new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    internal static async Task<ResolutionResult> Open()
    {
        var s = OfficialCopyIdentityTests.Position();
        var cast = await Act(s,"P1",new PlayCardCommand("C","UNL-200/219",["TARGET"]));
        return await Top(cast.State);
    }
}
