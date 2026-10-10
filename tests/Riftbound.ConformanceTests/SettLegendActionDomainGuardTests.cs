using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;

namespace Riftbound.ConformanceTests;

// The old tests asserted automatic mana payment on a synthetic battlefield.
// These exercise official spell damage, optional power payment and prompt authority.
public sealed class SettLegendActionDomainGuardTests
{
    [Theory]
    [InlineData("exhausted")]
    [InlineData("face-down")]
    [InlineData("enemy-controller")]
    [InlineData("wrong-identity")]
    [InlineData("no-boon")]
    [InlineData("no-power")]
    public async Task InapplicableSourcesNeverReplaceDestruction(string guard)
    {
        var s = Position(power: guard == "no-power" ? 0 : 1);
        var cards = new Dictionary<string,CardObjectState>(s.CardObjects);
        cards["LEGEND"] = guard switch {
            "exhausted" => cards["LEGEND"] with { IsExhausted = true },
            "face-down" => cards["LEGEND"] with { IsFaceDown = true },
            "enemy-controller" => cards["LEGEND"] with { ControllerId = "P1" },
            "wrong-identity" => cards["LEGEND"] with { CardNo = "OGN·268/298" }, _ => cards["LEGEND"] };
        if (guard == "no-boon") cards["D"] = cards["D"] with { Tags = [CardObjectTags.UnitCard] };
        var done = await Open(s with { CardObjects = cards });
        Assert.Null(done.State.PendingRuleChoice); Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_RECALLED_TO_BASE");
    }

    [Fact]
    public async Task ReplacementChoiceDuplicateStaleAndConflictingIntentsAreAtomic()
    {
        var opened = await Open(); var state=opened.State; var session=new MatchSession(state,new CoreRuleEngine(),NoopMatchJournal.Instance);
        var p=state.PendingRuleChoice!.Request; var prompt=session.PromptFor("P2");
        var id=p.Options.First(o=>o.Id!="DECLINE").Id;
        var command=new PayCostCommand(p.Id,"RULE_REPLACEMENT",[id]);
        var raw=JsonSerializer.SerializeToElement(new { cmdType="PAY_COST", paymentId=p.Id, paymentWindow="RULE_REPLACEMENT",
            paymentChoiceIds=new[]{id}, promptId=prompt.PromptId, snapshotTick=prompt.SnapshotTick });
        var done=await session.SubmitAsync("P2","choose",command,raw,default); Assert.True(done.Accepted,done.ErrorMessage);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base); var hash=MatchStateHasher.Hash(done.State);
        var duplicate=await session.SubmitAsync("P2","choose",command,raw,default); Assert.True(duplicate.Accepted);
        var stale=await session.SubmitAsync("P2","stale",command,raw,default); Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired,stale.ErrorCode);
        var changed=JsonSerializer.SerializeToElement(new { cmdType="PAY_COST", paymentId=p.Id, paymentWindow="RULE_REPLACEMENT", paymentChoiceIds=new[]{"DECLINE"} });
        var conflict=await session.SubmitAsync("P2","choose",new PayCostCommand(p.Id,"RULE_REPLACEMENT",["DECLINE"]),changed,default);
        Assert.False(conflict.Accepted); Assert.Equal(ErrorCodes.ClientIntentConflict,conflict.ErrorCode);
        foreach(var result in new[]{duplicate,stale,conflict}) Assert.Equal(hash,MatchStateHasher.Hash(result.State));
    }
}
