using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DisarmTriggerTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

// Exact reviewed physical forms in controlled positions. No whole-card behavior,
// deck legality, ordinary match birth or accepted original history is certified.
public sealed class DisarmReviewedPrintTests
{
    public static IEnumerable<object[]> PhysicalRows => new object[][] {
        [40107L, "RAD·038/167", 1], [40122L, "RAD·050/167", 2],
        [40126L, "RAD·054/167", 1], [40127L, "RAD·055/167", 1],
        [40136L, "RAD·063/167", 1], [40138L, "RAD·065/167", 2],
        [40139L, "RAD·065a/167", 2], [40171L, "RAD·093/167", 2],
        [40175L, "RAD·097/167", 1], [40187L, "RAD·109/167", 1],
        [40188L, "RAD·109a/167", 1], [40296L, "RAD·SP1/005", 1],
        [40298L, "RAD·SP3/005", 1]
    };

    [Theory]
    [MemberData(nameof(PhysicalRows))]
    public async Task EachReviewedPhysicalPrintUsesItsExactDisarmWhenItFirstAttacks(long id, string no, int amount)
    {
        var initial = await ReviewedPosition(id, no);
        var attack = await Attack(initial);
        var item = Assert.Single(attack.State.StackItems);
        Assert.Empty(attack.State.TriggerQueue);
        Assert.Equal("HOST", item.SourceObjectId);
        var contribution = Assert.Single(item.FieldContext!.Disarm!.Contributions);
        Assert.Equal(no, contribution.CardNo); Assert.Equal(amount, contribution.Amount);
        Assert.Equal(no == "RAD·055/167", contribution.AuraKey is not null);
        Assert.Null(contribution.SourceTokenFactoryCardNo);
        var done = await AttackDamageDistributionTests.Top((await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1")).State);
        Assert.Equal(3 - amount, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.Single(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.DoesNotContain(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [MemberData(nameof(PhysicalRows))]
    public async Task AnActualImageUsesTheCapturedReviewedFaceForItsOwnAttack(long id, string no, int amount)
    {
        var row = "reviewed-image-" + id;
        var copied = await MakeImage(await ReviewedPosition(id, no), row);
        var attack = await AttackImage(copied.State, row);
        var item = Assert.Single(attack.State.StackItems);
        Assert.Equal("COPY-TOKEN-001", item.SourceObjectId);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, item.FieldContext!.SourceTokenFactoryCardNo);
        var contribution = Assert.Single(item.FieldContext.Disarm!.Contributions, c => c.Source.ObjectId == item.SourceObjectId);
        Assert.Equal(no, contribution.CardNo); Assert.Equal(amount, contribution.Amount);
        Assert.True(contribution.FaceRevision > 0);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, contribution.SourceTokenFactoryCardNo);
        var total = no == "RAD·055/167" ? 2 : amount;
        Assert.Equal(total, item.FieldContext.Disarm.Contributions.Sum(c => c.Amount));
        var done = await AttackDamageDistributionTests.Top((await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1")).State);
        Assert.Equal(3 - total, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(no, done.State.CardObjects[item.SourceObjectId].CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, done.State.CardObjects[item.SourceObjectId].TokenFactoryCardNo);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("RAD·054/167", 1)]
    [InlineData("RAD·050/167", 2)]
    [InlineData("RAD·055/167", 1)]
    public async Task AConfirmedImageThatActuallyCeasesKeepsItsHistoryButHasNoSourceHere(string no, int amount)
    {
        var row = "reviewed-primary-ceases-" + no.Replace('/', '-');
        var departed = await ConfirmedDepartedImage(no, amount, row);
        var item = Assert.Single(departed.State.StackItems);
        Assert.True(item.FieldContext!.SourceCeasedToExist);
        var source = Assert.Single(item.FieldContext.Disarm!.Contributions, c => c.Source.ObjectId == item.SourceObjectId);
        var ceased = Assert.IsType<CeasedDisarmSource>(source.CeasedSource);
        Assert.Equal(no, ceased.CardNo); Assert.Equal(source.Source, ceased.Source);
        var done = await AttackDamageDistributionTests.Top(departed.State);
        Assert.Equal(3, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.DoesNotContain(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_RESOLVED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task RecoveryRejectsAConsistentButUnreviewedSP4FaceOnAnActuallyCeasedImage()
    {
        const string no = "RAD·SP4/005";
        const string row = "reviewed-ceased-unphysical-sp4";
        Assert.True(DisarmSourceSpecifications.TryGet(no, out var spec)); Assert.Equal(1, spec.PrintedValue);
        Assert.DoesNotContain(ScopedOfficialCardSources.GetPhysicalPrints(), c => c.CardNo == no);
        Assert.False(Create(no, out _));
        var departed = await ConfirmedDepartedImage("RAD·054/167", 1, row);
        var item = Assert.Single(departed.State.StackItems); var field = item.FieldContext!;
        var original = Assert.Single(field.Disarm!.Contributions);
        var changed = original with { CardNo = no, CeasedSource = original.CeasedSource! with { CardNo = no } };
        var forged = departed.State with { StackItems = [item with { CardNo = no,
            FieldContext = field with { CardNo = no, Disarm = field.Disarm with { Contributions = [changed] } } }] };
        Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(forged),
            e => e.Contains("field trigger stack context", StringComparison.Ordinal));
        Restore(departed.State); ArmamentMightTests.PublicRecovery(departed.State);
    }

    private static async Task<MatchState> ReviewedPosition(long id, string no)
    {
        var source = Assert.Single(ScopedOfficialCardSources.GetPhysicalPrints(), c => c.Id == id && c.CardNo == no);
        Assert.True(Create(no, out var host));
        Assert.Equal(source.Power, host.Power); Assert.Equal(source.Energy, host.ManaCost);
        Assert.Equal(ReviewedScopedPrintUniverseTests.ImplementedPrints.Contains(no, StringComparer.Ordinal),
            CardBehaviorRegistry.TryGetByCardNo(no, out _));
        var initial = await Position("RAD·054/167", 1);
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["HOST"] = host } };
    }

    private static bool Create(string no, out CardObjectState card)
    {
        object?[] args = ["HOST", "P1", no, null];
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        var result = (bool)factory.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        card = (CardObjectState)args[3]!; return result;
    }

    private static async Task<ResolutionResult> MakeImage(MatchState initial, string row)
    {
        var copied = await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.AddSpell(initial, "COPY", "UNL-200/219"), "HOST", "COPY");
        var image = copied.State.CardObjects["COPY-TOKEN-001"];
        Assert.Equal(initial.CardObjects["HOST"].CardNo, image.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.TokenFactoryCardNo);
        Assert.True(TokenObjectRules.IsToken(image));
        Restore(copied.State); ArmamentMightTests.PublicRecovery(copied.State);
        return copied;
    }

    private static async Task<ResolutionResult> AttackImage(MatchState state, string row)
    {
        var attack = await Act(state, "P1", new MoveUnitCommand("COPY-TOKEN-001", "BASE", "BATTLEFIELD:F", []));
        Restore(attack.State); ArmamentMightTests.PublicRecovery(attack.State);
        return attack;
    }

    private static async Task<ResolutionResult> ConfirmedDepartedImage(string no, int amount, string row)
    {
        var source = Assert.Single(ScopedOfficialCardSources.GetPhysicalPrints(), c => c.CardNo == no);
        var initial = AttackDamageDistributionTests.Spell(await ReviewedPosition(source.Id, no), "SOUL", "SFD·163/221", "P1");
        var copied = await MakeImage(initial, row);
        var attack = await AttackImage(copied.State, row);
        Assert.Equal(no == "RAD·055/167" ? 2 : amount, Assert.Single(attack.State.StackItems).FieldContext!.Disarm!.Contributions.Sum(c => c.Amount));
        var confirmed = await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1");
        var cast = await Act(confirmed.State, "P1", new PlayCardCommand("SOUL", "SFD·163/221", ["COPY-TOKEN-001", "HOST"]));
        var departed = await TopWithReceipt(cast.State, row);
        Assert.DoesNotContain("COPY-TOKEN-001", departed.State.CardObjects.Keys);
        Assert.Contains(departed.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");
        return departed;
    }
}
