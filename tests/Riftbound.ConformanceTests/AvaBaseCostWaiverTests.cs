using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AvaBaseCostWaiverTests
{
    private const string Pyke = "UNL-028/219", Fee = "SPEND_POWER:red:1";

    private static async Task<Driver> Open()
    {
        var initial = AvaStandbyAttackPlayTests.Position(Pyke);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["FEE_RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("FEE_RUNE", "OGN·007/298", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FEE_RUNE"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "FEE_RUNE"] } } };
        var game = new Driver(initial);
        await AvaStandbyAttackPlayTests.Attack(game);
        await AvaStandbyAttackPlayTests.Pay(game);
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(new[] { "STANDBY" }, pending.Sources.Keys);
        Assert.False(pending.IgnoreAllCosts); Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("额外费用仍需支付", game.PromptFor("P1").Reason);
        return game;
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TheActualOptionalRedFeeIsQuotedFundedAndPaidAfterTheIndependentBlueAttackCost(bool paid)
    {
        var game = await Open();
        var command = new PlayCardCommand("STANDBY", Pyke, [], Destination: "BATTLEFIELD:F", OptionalCosts: paid ? [Fee] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.NotNull(quote.Cost); Assert.Equal(0, quote.Cost.Mana); Assert.Equal(0, quote.Cost.GenericPower);
        Assert.Equal(paid ? 1 : 0, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Equal(!paid, quote.CanPay);
        if (paid)
        {
            await RadGearTestPosition.Reject(game.State, "P1", command);
            await game.Act("P1", new RecycleRuneCommand("FEE_RUNE"));
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        }
        await game.Act("P1", command);
        for (var index = 0; index < 4 && (game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0); index++)
        {
            if (game.State.PriorityPlayerId is null && game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else await game.Top();
        }
        Assert.Equal(!paid, game.State.CardObjects["STANDBY"].IsExhausted);
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("F", game.State.ObjectLocations["STANDBY"].BattlefieldObjectId);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.False(game.State.CardObjects["STANDBY"].IsFaceDown);
        Assert.Null(game.State.CardObjects["STANDBY"].StandbyPlacement);
        await game.Replay();
    }

    [Fact]
    public async Task RefusingADeclaredNonzeroAdditionalFeeLeavesTheOriginalHandCardUnplayed()
    {
        var game = await Open();
        await game.Act("P1", new RefusePlayPaymentCommand("STANDBY", Pyke, [], Destination: "BATTLEFIELD:F", OptionalCosts: [Fee]));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_PLAYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "STANDBY"));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task AZeroCostUnpaidOptionalDeclarationCannotBeFalselyRefused()
    {
        var game = await Open();
        await RadGearTestPosition.Reject(game.State, "P1", new RefusePlayPaymentCommand("STANDBY", Pyke, [], Destination: "BATTLEFIELD:F"));
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("STANDBY", Pyke, [], Destination: "BATTLEFIELD:F"));
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Battlefields);
        await game.Replay();
    }

    [Fact]
    public async Task OriginalRecordRecoveryRejectsPromotionOfThePendingPermissionToIgnoreEveryCost()
    {
        var game = await Open();
        var forged = game.State with { PendingEffectPlay = game.State.PendingEffectPlay! with { IgnoreAllCosts = true } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Act("P1", new PlayCardCommand("STANDBY", Pyke, [], Destination: "BATTLEFIELD:F"));
        await game.Replay();
    }

    [Fact]
    public async Task AControlledTaxBoundaryStillRequiresManaAfterTheBaseCostsAreWaived()
    {
        var game = await Open();
        // Component payment boundary only: this does not claim a real held event.
        var state = game.State with { UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects,
            "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1"] };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var command = new PlayCardCommand("STANDBY", Pyke, [], Destination: "BATTLEFIELD:F");
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.Mana);
        await RadGearTestPosition.Reject(state, "P1", command);
        var tapped = await engine.ResolveAsync(state, new("ava-tax-rune", "P1", CommandTypes.TapRune), new TapRuneCommand("FEE_RUNE"), default);
        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        var played = await engine.ResolveAsync(tapped.State, new("ava-tax-play", "P1", command.CmdType), command, default);
        Assert.True(played.Accepted, played.ErrorMessage); Assert.Equal(0, played.State.RunePools["P1"].Mana);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(played);
    }
}
