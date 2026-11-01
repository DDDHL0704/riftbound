using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TargetedPowerAbilitySourceContinuityTests
{
    private static MatchState PutUnitInBase(MatchState state)
    {
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BASE");
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with {
                Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } } };
    }

    private static MatchState AsCopy(MatchState state)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with {
                TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, state.CardObjects["UNIT"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, state.CardObjects["UNIT"].CardNo!)] } } };

    private static MatchState ChangeUnitFace(MatchState state)
    {
        var source = state.CardObjects["UNIT"];
        var replacement = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", "OGN·001/298", "P1") with {
            ObjectGeneration = source.ObjectGeneration, IsExhausted = source.IsExhausted,
            TokenFactoryCardNo = source.TokenFactoryCardNo,
            CopyFaceTransitions = [..(source.CopyFaceTransitions ?? []),
                new(state.Tick + 1, source.ObjectGeneration, source.CardNo!, "OGN·001/298")] };
        return state with { Tick = state.Tick + 1,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = replacement } };
    }

    private static async Task<MatchState> FlockBeforeResponse(bool startsAsCopy)
    {
        var state = PutUnitInBase(VenUnitEntryBasicTests.Position("RAD·027/167"));
        if (startsAsCopy) state = AsCopy(state);
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), item => item.SourceCardNo == "RAD·027/167"
            && item.Kind == ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower);
        return (await OfficialInsightAndSpellLockTests.Act(state, "P1",
            new ActivateAbilityCommand("UNIT", ability.AbilityId, ["ALLY"]))).State;
    }

    private static MatchState GrantedPosition(bool startsAsCopy = false)
    {
        var state = PutUnitInBase(RadNextBatchUnitTests.SoulOfInspirationPosition());
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsExhausted = false } } };
        return startsAsCopy ? AsCopy(state) : state;
    }

    private static async Task<MatchState> GrantedBeforeResponse()
        => (await OfficialInsightAndSpellLockTests.Act(GrantedPosition(), "P1",
            new ActivateAbilityCommand("LEGEND", P4ActivatedAbilityCatalog.SoulOfInspirationAbilityId, ["ALLY"]))).State;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrintedUnitSkillSurvivesItsSourceCopyingAnotherFace(bool startsAsCopy)
    {
        var captured = await FlockBeforeResponse(startsAsCopy);
        var power = captured.CardObjects["ALLY"].Power;
        var changed = OfficialInsightAndSpellLockTests.Restore(ChangeUnitFace(captured));
        Assert.Equal(startsAsCopy ? 1L : 0L, Assert.Single(changed.StackItems).TargetedPowerAbility!.SourceFaceRevision);
        var done = await OfficialInsightAndSpellLockTests.ResolveTop(changed);
        Assert.Equal(power + 1, done.State.CardObjects["ALLY"].Power);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["ALLY"].Tags);
        Assert.Empty(done.State.StackItems);
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RecoveryRejectsInventedPrintedSourceFaceRevisions(long revision)
    {
        var state = ChangeUnitFace(await FlockBeforeResponse(startsAsCopy: true));
        var item = Assert.Single(state.StackItems);
        var forged = state with { StackItems = [item with {
            TargetedPowerAbility = item.TargetedPowerAbility! with { SourceFaceRevision = revision } }] };
        Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(forged),
            error => error.Contains("targeted power ability", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReturnResponseKeepsTheAlreadyGrantedSkillAfterTheGrantingUnitLeaves(bool startsAsCopy)
    {
        const string response = "OGN·104/298";
        var initial = EquipChainTests.Response(GrantedPosition(startsAsCopy), response);
        var power = initial.CardObjects["ALLY"].Power;
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("LEGEND", P4ActivatedAbilityCatalog.SoulOfInspirationAbilityId, ["ALLY"]));
        var grant = Assert.IsType<TargetedPowerAbilityGrant>(Assert.Single(game.State.StackItems).TargetedPowerAbility!.Grant);
        Assert.Equal("UNIT", grant.Source.ObjectId);
        Assert.Equal(startsAsCopy ? 1L : 0L, grant.FaceRevision);
        await game.Act("P1", new PlayCardCommand("RESPONSE", response, ["UNIT"]));
        await game.Top();
        if (startsAsCopy) Assert.False(game.State.CardObjects.ContainsKey("UNIT"));
        else Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(power + 3, game.State.CardObjects["ALLY"].Power);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("controller")]
    [InlineData("face")]
    public async Task GrantedSkillSurvivesAChangeToTheGrantingUnitsControllerOrFace(string change)
    {
        var captured = await GrantedBeforeResponse();
        var power = captured.CardObjects["ALLY"].Power;
        var changed = change == "controller"
            ? LocalDestructionRecallTests.At(captured, "UNIT", "P2", "BASE")
            : ChangeUnitFace(captured);
        var restored = OfficialInsightAndSpellLockTests.Restore(changed);
        var done = await OfficialInsightAndSpellLockTests.ResolveTop(restored);
        Assert.Equal(power + 3, done.State.CardObjects["ALLY"].Power);
        Assert.Empty(done.State.StackItems);
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-source")]
    [InlineData("negative-generation")]
    [InlineData("future-generation")]
    [InlineData("negative-face")]
    [InlineData("future-face")]
    [InlineData("wrong-factory")]
    [InlineData("future-tick")]
    public async Task RecoveryRejectsInventedGrantAcquisitionHistory(string change)
    {
        var state = await GrantedBeforeResponse();
        var item = Assert.Single(state.StackItems);
        var context = item.TargetedPowerAbility!;
        var grant = Assert.IsType<TargetedPowerAbilityGrant>(context.Grant);
        var forgedContext = change switch {
            "missing" => context with { Grant = null },
            "wrong-source" => context with { Grant = grant with { Source = new("ALLY", 0) } },
            "negative-generation" => context with { Grant = grant with { Source = grant.Source with { Generation = -1 } } },
            "future-generation" => context with { Grant = grant with { Source = grant.Source with { Generation = 1 } } },
            "negative-face" => context with { Grant = grant with { FaceRevision = -1 } },
            "future-face" => context with { Grant = grant with { FaceRevision = 1 } },
            "wrong-factory" => context with { Grant = grant with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo } },
            "future-tick" => context with { AcquiredTick = state.Tick + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(change)) };
        var forged = state with { StackItems = [item with { TargetedPowerAbility = forgedContext }] };
        Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(forged),
            error => error.Contains("targeted power ability", StringComparison.Ordinal));
    }
}
