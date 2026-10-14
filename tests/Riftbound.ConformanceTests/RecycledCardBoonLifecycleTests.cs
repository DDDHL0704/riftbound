using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RecycledCardBoonLifecycleTests
{
    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task ActualCopiedImageRecycleSkillSurvivesItsSourceCeasingDuringResponse(string card)
    {
        var initial = Position(card);
        var copied = await OfficialCopyIdentityTests.Copy(initial);
        var current = copied.State;
        var opened = await Top((await Act(current, "P1", new PlayCardCommand("LOOK", "UNL-063/219", ["TARGET"]))).State);
        var insight = opened.State.PendingCardChoice!;
        var recycled = await Act(opened.State, "P1", new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, [insight.LegalObjectIds[0]]));
        Assert.Equal("C-TOKEN-001", Assert.Single(recycled.State.StackItems).SourceObjectId);
        var target = recycled.State.PendingCardChoice!;
        var chosen = await Act(recycled.State, "P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ALLY"]));
        var returned = await Act(chosen.State, "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"]));
        var departed = await Top(returned.State);
        Assert.False(departed.State.CardObjects.ContainsKey("C-TOKEN-001"));
        Assert.Single(departed.State.StackItems);
        var errors = OfficialInsightAndSpellLockTests.Errors(departed.State);
        Assert.True(errors.Count == 0, string.Join("; ", errors)); Restore(departed.State);
        var done = await Top(departed.State);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ALLY"].Tags); Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task IndependentRecoveryRejectsMissingForgedAndPrematureCeasedWitness()
    {
        var departed = await Departed(); var item = Assert.Single(departed.StackItems); var context = item.FieldContext!;
        var origin = context.RecycledBoon!; var ceased = Assert.IsType<CeasedRecycledBoonSource>(origin.CeasedSource);
        foreach (var changed in new[] { context with { SourceCeasedToExist = false },
            context with { RecycledBoon = origin with { CeasedSource = null } },
            context with { RecycledBoon = origin with { CeasedSource = ceased with { Source = new("FORGED", ceased.Source.Generation) } } },
            context with { RecycledBoon = origin with { CeasedSource = ceased with { Source = ceased.Source with { Generation = ceased.Source.Generation + 1 } } } },
            context with { RecycledBoon = origin with { CeasedSource = ceased with { FactoryCardNo = P6TokenFactoryCatalog.ZaunMinionTokenCardNo } } },
            context with { RecycledBoon = origin with { CeasedSource = ceased with { Tick = origin.CapturedTick - 1 } } },
            context with { RecycledBoon = origin with { CeasedSource = ceased with { Tick = departed.Tick + 1 } } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(departed with { StackItems = [item with { FieldContext = changed }] }),
                e => e.Contains("field trigger", StringComparison.Ordinal));
        Restore(departed);
        var living = await Chosen(Position("OGN·235/298")); var liveItem = Assert.Single(living.StackItems);
        var liveContext = liveItem.FieldContext!; var liveOrigin = liveContext.RecycledBoon!;
        var fake = liveContext with { SourceCeasedToExist = true, RecycledBoon = liveOrigin with {
            CeasedSource = new(new(liveItem.SourceObjectId, liveContext.SourceGeneration), liveContext.SourceTokenFactoryCardNo!, living.Tick,
                liveContext.CardNo, liveOrigin.FaceRevision, liveOrigin.CapturedTick) } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(living with { StackItems = [liveItem with { FieldContext = fake }] }),
            e => e.Contains("field trigger", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task CeasedCopiedSourceRejectsConsistentSubstitutionOfAnotherOfficialPrinting(string card)
    {
        var state = await Departed(card); var item = Assert.Single(state.StackItems);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        Assert.False(state.CardObjects.ContainsKey(item.SourceObjectId));
        var context = item.FieldContext!; var origin = context.RecycledBoon!;
        var ceased = Assert.IsType<CeasedRecycledBoonSource>(origin.CeasedSource);
        Assert.Equal(card, ceased.CardNo); Assert.Equal(1, ceased.FaceRevision);
        foreach (var other in new[] { "OGN·235/298", "SFD·237/221", "SFD·237*/221" }.Where(other => other != card)) {
            var forged = state with { StackItems = [item with {
                CardNo = other, FieldContext = item.FieldContext! with { CardNo = other } }] };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged),
                e => e.Contains("field trigger", StringComparison.Ordinal));
        }
        foreach (var changed in new[] { ceased with { CardNo = "OGN·096/298" },
            ceased with { FaceRevision = ceased.FaceRevision + 1 }, ceased with { CapturedTick = ceased.CapturedTick + 1 } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with {
                FieldContext = context with { RecycledBoon = origin with { CeasedSource = changed } } }] }),
                e => e.Contains("field trigger", StringComparison.Ordinal));
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        foreach (var field in new[] { "cardNo", "faceRevision", "capturedTick" }) {
            var node = System.Text.Json.JsonSerializer.SerializeToNode(state, json)!;
            node["stackItems"]![0]!["fieldContext"]!["recycledBoon"]!["ceasedSource"]!.AsObject().Remove(field);
            var missing = System.Text.Json.JsonSerializer.Deserialize<MatchState>(node.ToJsonString(), json)!;
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(missing),
                e => e.Contains("field trigger", StringComparison.Ordinal));
        }
        Restore(state);
    }

    internal static MatchState Position(string card)
    {
        var initial = OfficialCopyIdentityTests.Position(card);
        initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·096/298", "F", "P1"), "ALLY", "P1", "BASE");
        return OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.AddSpell(initial, "LOOK", "UNL-063/219"), "LEAVE", "OGN·104/298");
    }
    internal static async Task<MatchState> Chosen(MatchState initial)
    {
        var copied = await OfficialCopyIdentityTests.Copy(initial);
        var opened = await Top((await Act(copied.State, "P1", new PlayCardCommand("LOOK", "UNL-063/219", ["TARGET"]))).State);
        var choice = opened.State.PendingCardChoice!;
        var recycled = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]));
        choice = recycled.State.PendingCardChoice!;
        return (await Act(recycled.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]))).State;
    }
    internal static async Task<MatchState> Departed(string card = "OGN·235/298")
        => (await Top((await Act(await Chosen(Position(card)), "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"]))).State)).State;
}
