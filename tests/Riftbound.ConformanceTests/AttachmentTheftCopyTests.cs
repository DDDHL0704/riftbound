using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttachmentTheftCopyTests
{
    internal static async Task<MatchState> Position(string face = "SFD·049/221")
    {
        var state = await ShadySpectaclesTests.Attach(ShadySpectaclesTests.Position());
        state = OfficialHoldSequenceTests.AddUnit(state, "AKSHAN", "SFD·109/221", "F", "P2");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with {
                    Battlefields = state.PlayerZones["P2"].Battlefields.Where(id => id != "AKSHAN").ToArray(),
                    Hand = [..state.PlayerZones["P2"].Hand, "AKSHAN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["AKSHAN"] = new("P2", "HAND") }
        };
        state = OfficialHoldSequenceTests.AddUnit(state, "P2-COPY", face, "F", "P2");
        state = LocalDestructionRecallTests.At(state, "P2-COPY", "P2", "BASE");
        var printed = CardBehaviorRegistry.TryGetByCardNo(face, out var behavior) ? behavior : throw new InvalidOperationException();
        return state with {
            ActivePlayerId = "P2", TurnPlayerId = "P2", FocusPlayerId = null, PriorityPlayerId = null,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["AKSHAN"] = state.CardObjects["AKSHAN"] with { ManaCost = 4, Power = 4 },
                ["P2-COPY"] = state.CardObjects["P2-COPY"] with { Power = printed.SourceUnitPower } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P2"] = new(20, 20, new Dictionary<string, int> { ["orange"] = 20, ["red"] = 3 }) }
        };
    }

    internal static async Task<ResolutionResult> Steal(MatchState state)
    {
        var played = await Act(state, "P2", new PlayCardCommand("AKSHAN", "SFD·109/221", [],
            OptionalCosts: ["AKSHAN_STEAL_EQUIPMENT:GEAR"]));
        Assert.Equal(16, played.State.RunePools["P2"].Mana);
        Assert.Equal(18, played.State.RunePools["P2"].PowerByTrait["orange"]);
        return await Top(played.State);
    }

    internal static async Task<MatchState> Choose(ResolutionResult waiting)
    {
        var request = waiting.State.PendingRuleChoice!.Request;
        return (await Act(waiting.State, "P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["COPY:P2-COPY"]))).State;
    }

    internal static MatchState Sword(MatchState state)
        => LocalDestructionRecallTests.At(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["P2-SWORD"] = new("P2-SWORD", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2",
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) }
        }, "P2-SWORD", "P2", "BASE");

    [Fact]
    public async Task ASubsequentRealAttachmentCanUseOnlyTheNewCopiedApheliosInstance()
    {
        var copied = Sword(await Choose(await Steal(await Position())));
        var equipped = await EquipTestDriver.FinishAsync(await Act(copied, "P2",
            new AssembleEquipmentCommand("P2-SWORD", "AKSHAN", ["ASSEMBLE_RED"])));
        var skill = Assert.Single(equipped.State.StackItems);
        Assert.True(skill.AttachmentObserver!.CopiedInstance);
        Assert.Equal("GEAR", skill.AttachmentObserver.Origin!.Equipment.ObjectId);
        Assert.Equal("P2", skill.ControllerId);
        var mode = await AttachmentModeTests.Choose(equipped.State, "GRANT_BOON");
        var target = await AttachmentModeTests.Choose(mode.State, "AKSHAN");
        Assert.DoesNotContain(CardObjectTags.Boon, target.State.CardObjects["AKSHAN"].Tags);
        var done = await Top(target.State);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["AKSHAN"].Tags);
        Assert.Equal(5, done.State.CardObjects["AKSHAN"].Power);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("P1", "COPY:P2-COPY")]
    [InlineData("P2", "COPY:HOST")]
    [InlineData("P2", "COPY:AKSHAN")]
    public async Task TheOriginalGearOwnerAndUnfriendlyOrSelfSelectionsCannotChooseTheNewCopy(string actor, string selection)
    {
        var waiting = await Steal(await Position());
        var request = waiting.State.PendingRuleChoice!.Request;
        Assert.DoesNotContain(request.Options, option => option.Id == "COPY:HOST" || option.Id == "COPY:AKSHAN");
        var invalid = await new CoreRuleEngine().ResolveAsync(waiting.State, new("invalid-copy", actor, CommandTypes.PayCost),
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selection]), default);
        Assert.False(invalid.Accepted); Assert.Empty(invalid.Events);
        Assert.Equal(MatchStateHasher.Hash(waiting.State), MatchStateHasher.Hash(invalid.State));
        Restore(waiting.State); ArmamentMightTests.PublicRecovery(waiting.State);
    }

    [Theory]
    [InlineData("SFD·049/221")]
    [InlineData("SFD·224/221")]
    [InlineData("SFD·224*/221")]
    public async Task ActualTheftEndsTheOldCopyBeforeChoosingAndCannotRetroactivelyTriggerTheNewText(string face)
    {
        var initial = await Position(face); Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var waiting = await Steal(initial);
        var request = waiting.State.PendingRuleChoice!.Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", request.Kind);
        var preview = request.Preview!;
        Assert.Equal("OGN·096/298", preview.CardObjects["HOST"].CardNo);
        Assert.Null(preview.CardObjects["HOST"].AttachmentUnitCopies);
        Assert.Equal("AKSHAN", preview.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("P1", preview.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P2", preview.CardObjects["GEAR"].ControllerId);
        Assert.Contains("GEAR", preview.PlayerZones["P2"].Base);
        Assert.DoesNotContain("GEAR", preview.PlayerZones["P1"].Base);
        Restore(waiting.State); ArmamentMightTests.PublicRecovery(waiting.State);
        var done = await Choose(waiting);
        Assert.Equal(face, done.CardObjects["AKSHAN"].CardNo);
        Assert.Equal("OGN·096/298", done.CardObjects["HOST"].CardNo);
        Assert.Equal("P2", done.CardObjects["AKSHAN"].OwnerId);
        Assert.Equal("P2", done.CardObjects["AKSHAN"].ControllerId);
        Assert.Empty(done.TriggerQueue); Assert.Empty(done.StackItems); Assert.Null(done.PendingCardChoice);
        Assert.Contains(done.CardObjects["GEAR"].Tags, tag => tag.Contains("AKSHAN", StringComparison.Ordinal));
        Restore(done); ArmamentMightTests.PublicRecovery(done);
    }

    [Theory]
    [InlineData("OGN·104/298", "HAND")]
    [InlineData("OGN·229/298", "GRAVEYARD")]
    public async Task AChangedFaceDoesNotEraseTheStealDurationAndLeavingRestoresPhysicalIdentity(string spell, string zone)
    {
        var copied = await Choose(await Steal(await Position()));
        copied = AttackDamageDistributionTests.Spell(copied, "LEAVE", spell, "P2");
        var played = await Act(copied, "P2", new PlayCardCommand("LEAVE", spell, ["AKSHAN"]));
        var done = await Top(played.State);
        Assert.Contains("AKSHAN", zone == "HAND" ? done.State.PlayerZones["P2"].Hand : done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("SFD·109/221", done.State.CardObjects["AKSHAN"].CardNo);
        Assert.True(done.State.CardObjects["AKSHAN"].ObjectGeneration > copied.CardObjects["AKSHAN"].ObjectGeneration);
        Assert.Null(done.State.CardObjects["AKSHAN"].AttachmentUnitCopies);
        Assert.Equal("P1", done.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P1", done.State.CardObjects["GEAR"].ControllerId);
        Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("GEAR", done.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("GEAR", done.State.PlayerZones["P2"].Base);
        Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_CONTROL_RETURNED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
