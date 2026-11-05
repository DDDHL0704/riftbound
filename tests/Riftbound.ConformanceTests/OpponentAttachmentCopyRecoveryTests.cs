using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OpponentAttachmentCopyRecoveryTests
{
    // CN 374 and 740.1.a refer to control. Physical ownership of the equipment
    // or either candidate cannot transfer this replacement effect's choices.
    [Theory]
    [InlineData("P1", "P2")]
    [InlineData("P2", "P1")]
    [InlineData("P1", "P1")]
    public async Task PhysicalOwnershipDoesNotChangeTheChooserOrItsFriendlyCandidates(string gearOwner, string copyOwner)
    {
        var initial = OpponentAttachmentCopyChoiceTests.Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = gearOwner },
            ["COPY-SOURCE"] = initial.CardObjects["COPY-SOURCE"] with { OwnerId = copyOwner },
            ["P1-SOURCE"] = initial.CardObjects["P1-SOURCE"] with { OwnerId = "P2" } } };
        var driver = new Driver(initial);
        await OpponentAttachmentCopyChoiceTests.Cast(driver);
        var preview = driver.State.PendingRuleChoice!.Request.Preview!;
        Assert.Equal(gearOwner, preview.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P2", preview.CardObjects["GEAR"].ControllerId);
        Assert.Equal(copyOwner, preview.CardObjects["COPY-SOURCE"].OwnerId);
        Assert.Equal("P2", preview.CardObjects["COPY-SOURCE"].ControllerId);
        Assert.Equal("P2", preview.CardObjects["P1-SOURCE"].OwnerId);
        Assert.Equal("P1", preview.CardObjects["P1-SOURCE"].ControllerId);
        await OpponentAttachmentCopyChoiceTests.Finish(driver, initial);
        Assert.Equal(gearOwner, driver.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal(copyOwner, driver.State.CardObjects["COPY-SOURCE"].OwnerId);
    }

    [Theory]
    [InlineData("chooser")]
    [InlineData("candidate")]
    public async Task RecoveryRejectsReturningTheChoiceToTheCasterOrAddingItsUnit(string mutation)
    {
        var initial = OpponentAttachmentCopyChoiceTests.Position();
        var driver = new Driver(initial);
        await OpponentAttachmentCopyChoiceTests.Cast(driver);
        var before = driver.State; var pending = before.PendingRuleChoice!;
        var hash = MatchStateHasher.Hash(before);
        var request = mutation == "chooser" ? pending.Request with { PlayerId = "P1" }
            : pending.Request with { Options = [..pending.Request.Options,
                new RuleChoiceOption("COPY:P1-SOURCE", "复制施法者的单位", ["P1-SOURCE"])] };
        var forged = before with { PendingRuleChoice = pending with { Request = request } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged),
            error => error.Contains("invalid replacement choice continuation", StringComparison.Ordinal));
        Assert.Equal(hash, MatchStateHasher.Hash(driver.State));
        await OpponentAttachmentCopyChoiceTests.Finish(driver, initial);
    }
}
