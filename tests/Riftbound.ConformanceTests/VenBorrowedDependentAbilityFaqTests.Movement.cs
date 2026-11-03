using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("OGN·184/298", "EQUIPMENT", false)]
    [InlineData("OGN·184/298", "EQUIPMENT", true)]
    [InlineData("UNL-201/219", "LEGEND", false)]
    [InlineData("UNL-201/219", "LEGEND", true)]
    [InlineData("UNL-236/219", "LEGEND", false)]
    [InlineData("UNL-236/219", "LEGEND", true)]
    [InlineData("UNL-236*/219", "LEGEND", false)]
    [InlineData("UNL-236*/219", "LEGEND", true)]
    public async Task BorrowedMoveToBasePaysTheBorrowersCostsAndUsesTheSharedMovementAction(string donor, string category, bool prohibited)
    {
        var game = await WithTemplate(donor, category, adjust: state => {
            return prohibited ? state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = state.CardObjects["ALLY"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1")] } } }
                : state;
        });
        await game.Act("P1", new AssembleEquipmentCommand("GEAR2", "ALLY", ["ASSEMBLE_RED"]));
        await game.Top();
        var before = game.State;
        var id = TemplateId(before, "移动到基地");
        foreach (var invalid in new[] { "HOST", "ENEMY", "GEAR", "MISSING" })
            await RejectBorrowedResource(before, "HOST", id, [invalid]);
        await RejectBorrowedResource(LocalDestructionRecallTests.At(before, "ALLY", "P1", "BASE"), "HOST", id, ["ALLY"]);
        var insufficient = category == "EQUIPMENT"
            ? before with { RunePools = new Dictionary<string, RunePool>(before.RunePools) { ["P1"] = new(0, 20) } }
            : before with { PlayerExperience = new Dictionary<string, int>(before.PlayerExperience) { ["P1"] = 1 } };
        await RejectBorrowedResource(insufficient, "HOST", id, ["ALLY"]);
        if (category == "LEGEND")
            await RejectBorrowedResource(before with { CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects) {
                ["ALLY"] = before.CardObjects["ALLY"] with { IsExhausted = false } } }, "HOST", id, ["ALLY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        Assert.Equal(before.RunePools["P1"].Mana - (category == "EQUIPMENT" ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(before.PlayerExperience["P1"] - (category == "LEGEND" ? 2 : 0), game.State.PlayerExperience["P1"]);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["ALLY"].Zone);
        await game.Top();
        Assert.Equal(prohibited ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(prohibited ? "F" : null, game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(game.State.ObjectLocations["ALLY"].Zone, game.State.ObjectLocations["GEAR2"].Zone);
        Assert.Equal(game.State.ObjectLocations["ALLY"].BattlefieldObjectId, game.State.ObjectLocations["GEAR2"].BattlefieldObjectId);
        Assert.Equal("ALLY", game.State.CardObjects["GEAR2"].AttachedToObjectId);
        Assert.Equal(before.CardObjects["ALLY"].ObjectGeneration, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.Equal(prohibited ? 0 : 1, game.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BASE"));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BASE"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "GEAR2"));
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·184/298", "EQUIPMENT", "source-return")]
    [InlineData("UNL-201/219", "LEGEND", "source-return")]
    [InlineData("OGN·184/298", "EQUIPMENT", "target-return")]
    [InlineData("UNL-201/219", "LEGEND", "target-return")]
    public async Task BorrowedMoveToBaseRechecksTargetGenerationAfterRealResponses(string donor, string category, string response)
    {
        var game = await WithTemplate(donor, category, adjust: state =>
            AttackDamageDistributionTests.Spell(state, "MOVE-RETURN", "OGN·104/298", "P1"));
        var id = TemplateId(game.State, "移动到基地");
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("MOVE-RETURN", "OGN·104/298", [response == "source-return" ? "HOST" : "ALLY"]));
        await game.Top();
        await game.Top();
        var moves = response != "target-return";
        Assert.Equal(response == "target-return" ? "HAND" : moves ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(response == "target-return", game.State.CardObjects["ALLY"].ObjectGeneration > generation);
        Assert.Equal(moves ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BASE"));
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-201/219")]
    [InlineData("UNL-236/219")]
    [InlineData("UNL-236*/219")]
    public async Task BorrowedMoveToBaseCannotMoveACapturedTargetThatIsNowActive(string donor)
    {
        var game = await WithTemplate(donor, "LEGEND");
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "移动到基地"), ["ALLY"]));
        // Explicit resolution boundary fixture; the accepted journal is not rewritten.
        var frame = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["ALLY"] = game.State.CardObjects["ALLY"] with { IsExhausted = false } } };
        var engine = new CoreRuleEngine();
        var first = await engine.ResolveAsync(frame, new("boundary-pass-1", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var done = await engine.ResolveAsync(first.State, new("boundary-pass-2", "P2", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(done.Accepted, done.ErrorMessage);
        Assert.Equal("BATTLEFIELD", done.State.ObjectLocations["ALLY"].Zone);
        Assert.Empty(done.State.StackItems);
        Assert.Equal(3, done.State.PlayerExperience["P1"]);
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("OGN·259/298", false)]
    [InlineData("OGN·259/298", true)]
    [InlineData("OGN·305/298", false)]
    [InlineData("OGN·305/298", true)]
    [InlineData("OGN·305*/298", false)]
    [InlineData("OGN·305*/298", true)]
    [InlineData("FND-259/298", false)]
    [InlineData("FND-259/298", true)]
    public async Task BorrowedBidirectionalMovementChoosesABattlefieldOnlyWhenStartingInBase(string donor, bool inBase)
    {
        var game = await WithTemplate(donor, "LEGEND", adjust: state => inBase
            ? LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE") : state);
        var id = TemplateId(game.State, "在战场和基地之间移动");
        var before = game.State;
        await RejectBorrowedResource(before, "HOST", id, ["ENEMY"]);
        await RejectBorrowedResource(before, "HOST", id, ["GEAR"]);
        await RejectBorrowedResource(before with { RunePools = new Dictionary<string, RunePool>(before.RunePools) {
            ["P1"] = new(1, 20) } }, "HOST", id, ["ALLY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["ALLY"]));
        Assert.Equal(before.RunePools["P1"].Mana - 2, game.State.RunePools["P1"].Mana);
        await game.Top();
        if (inBase)
        {
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("MOVEMENT_DESTINATION", choice.Request.Kind);
            Assert.All(choice.Request.Options, option => Assert.StartsWith("BATTLEFIELD:", option.Id));
            foreach (var (actor, selected) in new[] { ("P2", "BATTLEFIELD:F"), ("P1", "BASE:P1"), ("P1", "BATTLEFIELD:MISSING") })
                await RadGearTestPosition.Reject(game.State, actor,
                    new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [selected]));
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:F"]));
        }
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(inBase ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(before.CardObjects["ALLY"].ObjectGeneration, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedBattlefieldChoiceResumesThroughTheSharedImposedMovementPayment(bool pay)
    {
        var game = await WithTemplate("OGN·259/298", "LEGEND", adjust: state => {
            state = RadGearTestPosition.Put(LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE"),
                "TAX", "RAD·062/167", "P2", "BATTLEFIELD", "F");
            return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["TAX"] = state.CardObjects["TAX"] with { Tags = [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy] } } };
        });
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(before, "在战场和基地之间移动"), ["ALLY"]));
        await game.Top();
        var destination = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(destination.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:F"]));
        var fee = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", fee.Request.Kind);
        Assert.Equal(1, fee.Request.PowerCost);
        Assert.Equal(1, fee.Request.ManaCost);
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(fee.Request.Id, "RULE_REPLACEMENT", ["PAY"]));
        await game.Act("P1", new PayCostCommand(fee.Request.Id, "RULE_REPLACEMENT", [pay ? "PAY" : "DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(before.RunePools["P1"].Mana - 2 - (pay ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }
}
