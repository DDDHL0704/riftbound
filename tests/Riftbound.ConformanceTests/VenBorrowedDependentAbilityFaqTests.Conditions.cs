using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedConditionPosition(string donor, bool empowered = false)
    {
        var state = HandDiscardObserverTests.Put(AttachedRuleTextTests.Position("OGN·111/298", 0), "DONOR", donor, "P1", "BASE");
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·001/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "STATUS", "VEN·035", "P1");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsEmpowered = empowered },
            ["DONOR"] = state.CardObjects["DONOR"] with { IsExhausted = true, IsEmpowered = !empowered } } };
    }

    [Theory]
    [InlineData("OGN·078/298", false)]
    [InlineData("OGN·078a/298", false)]
    [InlineData("OGN·078/298", true)]
    [InlineData("OGN·078a/298", true)]
    public async Task BorrowedSelfBoonAppliesToTheBorrowerWithoutCopyingTheDonorsUnlimitedBoonRule(string donor, bool alreadyBooned)
    {
        var initial = BorrowedConditionPosition(donor);
        if (alreadyBooned) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = initial.CardObjects["HOST"].Power + 1,
                Tags = [..initial.CardObjects["HOST"].Tags, CardObjectTags.Boon] } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "给予我增益");
        await RejectBorrowedResource(initial, "HOST", id, ["ALLY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(initial.CardObjects["HOST"].Power, game.State.CardObjects["HOST"].Power);
        await RejectBorrowedResource(game.State, "HOST", id, []);
        await game.Top();
        Assert.Equal(initial.CardObjects["HOST"].Power + (alreadyBooned ? 0 : 1), game.State.CardObjects["HOST"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["DONOR"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("GEAR", false)]
    [InlineData("GEAR", true)]
    [InlineData("DONOR", false)]
    [InlineData("ENEMY-GEAR", false)]
    public async Task BorrowedEmpowermentSelectsAnotherEquipmentAndRetainsItsGeneration(string target, bool empowered)
    {
        var initial = BorrowedConditionPosition("VEN·062");
        initial = HandDiscardObserverTests.Put(initial, "ENEMY-GEAR", "SFD·022/221", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { IsEmpowered = empowered } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "强化另一件装备");
        foreach (var invalid in new[] { "HOST", "ALLY", "MISSING" })
            await RejectBorrowedResource(initial, "HOST", id, [invalid]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [target]));
        Assert.Equal(empowered, game.State.CardObjects[target].IsEmpowered);
        await game.Top();
        Assert.True(game.State.CardObjects[target].IsEmpowered);
        Assert.Equal(1, game.State.CardObjects[target].EmpowermentCount);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, "none", false)]
    [InlineData(true, "none", false)]
    [InlineData(false, "toggle", false)]
    [InlineData(true, "toggle", false)]
    [InlineData(false, "toggle-return", false)]
    [InlineData(true, "toggle-return", false)]
    [InlineData(false, "toggle-return", true)]
    [InlineData(true, "toggle-return", true)]
    [InlineData(false, "target-return", false)]
    [InlineData(true, "target-return", false)]
    public async Task BorrowedConditionalMightReadsTheBorrowerAtResolutionAndKeepsItsLastKnownState(bool empowered, string response, bool token)
    {
        var initial = BorrowedConditionPosition("VEN·077", empowered);
        if (token) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "此牌已强化时");
        await RejectBorrowedResource(initial, "HOST", id, ["GEAR"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        if (response.StartsWith("toggle", StringComparison.Ordinal))
        {
            await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["HOST"],
                Mode: empowered ? "REMOVE_THEN_EMPOWER" : "EMPOWER_THEN_REMOVE"));
            await game.Top();
            empowered = !empowered;
        }
        if (response is "toggle-return" or "target-return")
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [response == "target-return" ? "ALLY" : "HOST"]));
            await game.Top();
        }
        var captured = Assert.Single(game.State.StackItems);
        Assert.Equal(empowered, captured.BorrowedExhaust!.LastKnownSourceEmpowered);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [captured with { BorrowedExhaust = captured.BorrowedExhaust with { LastKnownSourceEmpowered = null } }] }));
        await game.Top();
        Assert.Equal(response == "target-return" ? 0 : empowered ? 4 : 2,
            game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        if (token) Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else if (response == "toggle-return")
        {
            var modifier = Assert.Single(game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
            Assert.Equal("HOST", modifier.SourceObjectId);
            var snapshots = ResolutionResult.BuildSnapshots(game.State);
            foreach (var viewer in new[] { "P1", "P2" })
            {
                var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshots[viewer].Timing["continuousEffects"]);
                var row = Assert.Single(effects, e => Equals(e.GetValueOrDefault("effectId"), modifier.EffectId));
                Assert.Equal(viewer == "P1" ? "HOST" : "HIDDEN", row["sourceObjectId"]);
            }
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BorrowedBoonAlternativeChecksTheTargetsBoonAtResolution(bool booned, bool leaves)
    {
        var initial = BorrowedConditionPosition("RAD·027/167");
        if (booned) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Power = initial.CardObjects["ALLY"].Power + 1,
                Tags = [..initial.CardObjects["ALLY"].Tags, CardObjectTags.Boon] } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "已有增益时");
        await RejectBorrowedResource(initial, "HOST", id, ["GEAR"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        if (leaves)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(leaves ? 0 : booned ? 2 : 0, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        if (!leaves) Assert.Equal(initial.CardObjects["ALLY"].Power + (booned ? 2 : 1), game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedBoonAlternativeRechecksAfterAStandbyResponseGrantsBoon()
    {
        var initial = AttackDamageDistributionTests.Spell(BorrowedConditionPosition("RAD·027/167"), "BOON", "OGN·053/298", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "F");
        for (var i = 0; i < 6; i++)
            initial = RadGearTestPosition.Put(initial, "P2-RESPONSE-DECK-" + i, "OGN·001/298", "P2", "MAIN_DECK");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new HideCardCommand("BOON", "OGN·053/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        async ValueTask<ResolutionResult> Submit(string actor, GameCommand command)
        {
            await game.Act(actor, command);
            return new(true, null, game.State, [], ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State));
        }
        foreach (var player in new[] { "P1", "P2" })
            await TurnSequenceTestDriver.Complete(await Submit(player, new EndTurnCommand()), Submit);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "已有增益时"), ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("BOON", "OGN·053/298", ["ALLY"]));
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        var power = game.State.CardObjects["ALLY"].Power;
        await game.Top();
        Assert.Equal(power + 2, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(2, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }
}
