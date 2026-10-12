using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CopiedFaceAuthorityTests
{
    [Theory]
    [InlineData("VEN·183")]
    [InlineData("SFD·050/221")]
    public async Task TextCapturedFromImageRemainsBoundAfterTheHostCopiesADifferentFace(string face)
    {
        var attached = await OpenAttached(face);
        var captured = Assert.IsType<AttachedRuleTextState>(attached.CardObjects["GEAR"].AttachedRuleText);
        var copied = await Top(attached);
        Assert.Equal(face, copied.State.CardObjects["C-TOKEN-001"].CardNo);
        Assert.Equal(captured.Unit.Generation, copied.State.CardObjects["C-TOKEN-001"].ObjectGeneration);
        Assert.Equal(captured, copied.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal("C-TOKEN-001", copied.State.CardObjects["GEAR"].AttachedToObjectId);
        Restore(copied.State); ArmamentMightTests.PublicRecovery(copied.State);
    }

    internal static async Task<MatchState> OpenAttached(string face = "VEN·183")
    {
        var initial = OfficialCopyIdentityTests.Position(face);
        var opened = await Top((await Act(initial, "P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"]))).State);
        var state = LocalDestructionRecallTests.At(opened.State with {
            CardObjects = new Dictionary<string, CardObjectState>(opened.State.CardObjects) {
                ["GEAR"] = new("GEAR", cardNo: "SFD·059/221", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) } }, "GEAR", "P1", "BASE");
        state = OfficialCopyIdentityTests.AddSpell(state, "ATTACH", "SFD·011/221");
        var attached = await Top((await Act(state, "P1", new PlayCardCommand("ATTACH", "SFD·011/221", ["C-TOKEN-001", "GEAR"]))).State);
        var captured = Assert.IsType<AttachedRuleTextState>(attached.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, captured.CardNo);
        Restore(attached.State); ArmamentMightTests.PublicRecovery(attached.State);
        return attached.State;
    }

    internal static async Task<MatchState> Copied(string face = "VEN·183")
        => (await Top(await OpenAttached(face))).State;

    [Fact]
    public async Task OriginalBlankTextDoesNotAcquireTheNewFacesSpellSkill()
    {
        var state = OfficialCopyIdentityTests.AddSpell(await Copied(), "SPELL", "OGN·048/298");
        var completed = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []))).State);
        var item = Assert.Single(completed.State.StackItems);
        Assert.Null(item.SpellContext!.Origin);
        var done = await Top(completed.State);
        Assert.Equal(5, done.State.CardObjects["C-TOKEN-001"].Power);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task DetachAndReattachAcquiresTheNewFaceAtANewTextRevision()
    {
        var state = await Copied("SFD·050/221");
        var old = state.CardObjects["GEAR"].AttachedRuleText!;
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        state = OfficialCopyIdentityTests.AddSpell(state, "ATTACH2", "SFD·011/221");
        state = (await Top((await Act(state, "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["C-TOKEN-001", "GEAR"]))).State)).State;
        Assert.Null(state.CardObjects["GEAR"].AttachedRuleText);
        state = (await Top((await Act(state, "P1", new PlayCardCommand("ATTACH2", "SFD·011/221", ["C-TOKEN-001", "GEAR"]))).State)).State;
        var current = Assert.IsType<AttachedRuleTextState>(state.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal("SFD·050/221", current.CardNo);
        Assert.True(current.Revision > old.Revision); Assert.True(current.CapturedTick > old.CapturedTick);
        Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("missing-timeline")]
    [InlineData("null-frame")]
    [InlineData("unknown-face")]
    [InlineData("future-tick")]
    [InlineData("generation")]
    [InlineData("intrinsic")]
    [InlineData("captured-future")]
    [InlineData("captured-missing")]
    [InlineData("copied-face")]
    [InlineData("face-revision")]
    [InlineData("missing-face-revision")]
    public async Task RecoveryRejectsForgedCopyTimelineOrAcquisition(string mutation)
    {
        var state = await Copied(); var host = state.CardObjects["C-TOKEN-001"]; var gear = state.CardObjects["GEAR"];
        var frame = Assert.Single(host.CopyFaceTransitions!);
        var changed = mutation switch {
            "null-frame" => host with { CopyFaceTransitions = [null!] },
            "unknown-face" => host with { CopyFaceTransitions = [frame with { ToCardNo = "UNPRINTED" }] },
            "future-tick" => host with { CopyFaceTransitions = [frame with { Tick = state.Tick + 1 }] },
            "generation" => host with { CopyFaceTransitions = [frame with { Generation = 99 }] },
            "intrinsic" => host with { CopyFaceTransitions = [frame with { FromCardNo = "OGN·001/298" }] },
            "missing-timeline" => host with { CopyFaceTransitions = null },
            _ => host
        };
        if (mutation == "captured-future") gear = gear with { AttachedRuleText = gear.AttachedRuleText! with { CapturedTick = state.Tick + 1 } };
        if (mutation == "captured-missing") gear = gear with { AttachedRuleText = gear.AttachedRuleText! with { CapturedTick = null } };
        if (mutation == "copied-face") gear = gear with { AttachedRuleText = gear.AttachedRuleText! with { CardNo = "VEN·183" } };
        if (mutation == "face-revision") gear = gear with { AttachedRuleText = gear.AttachedRuleText! with { FaceRevision = 1 } };
        if (mutation == "missing-face-revision") gear = gear with { AttachedRuleText = gear.AttachedRuleText! with { FaceRevision = null } };
        var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [host.ObjectId] = changed, [gear.ObjectId] = gear } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Fact]
    public async Task CopiedImageCannotDropItsFaceTimelineEvenWithoutAnAttachedGear()
    {
        var state = await Copied();
        var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["C-TOKEN-001"] = state.CardObjects["C-TOKEN-001"] with { CopyFaceTransitions = null },
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = null, AttachedRuleText = null } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("copied face timeline"));
    }
}
