using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SpriteLanternLastBreathTests
{
    private const string Lantern = "UNL-078/219", DestroyEquipment = "SFD·005/221";
    private static MatchState Position(bool stolen = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "LANTERN", Lantern, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "DESTROY", DestroyEquipment, "P1", "HAND");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Lantern, out var printed));
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LANTERN"] = state.CardObjects["LANTERN"] with { OwnerId = stolen ? "P2" : "P1",
                Tags = state.CardObjects["LANTERN"].Tags.Concat((printed.SourceEquipmentTags ?? "")
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray() } } };
    }
    private static CardObjectState[] Sprites(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.FaerieTokenCardNo).ToArray();
    private static async Task Destroy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("DESTROY", DestroyEquipment, ["LANTERN"])); await game.Top();
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
    }

    private static MatchState BeforeMainTransformation(bool stolen)
    {
        // Start after the beginning abilities: a normal unattached equipment
        // cannot be placed on a battlefield, and its next Ephemeral has not begun.
        var state = AnimatedArmamentAttachmentTests.WithMainPhaseResources(Position(stolen));
        var legend = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1");
        return state with { Phase = MatchPhases.TurnStart, TurnStartStep = "MAIN", TimingState = TimingStates.NeutralClosed,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["LEGEND"] = legend },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
    }

    private static async Task AnimateAndMove(Driver game)
    {
        await game.Act("P1", new PassPriorityCommand());
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LANTERN"]));
        await game.TapRunes("P1", ["ORI-MANA-0", "ORI-MANA-1"]);
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); await game.Top();
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["LANTERN"].Tags);
        Assert.NotNull(game.State.CardObjects["LANTERN"].LegendEquipmentUnit);
        await game.Act("P1", new RecycleRuneCommand("ORI-POWER-0"));
        await game.Act("P1", new MoveUnitCommand("LANTERN", "BASE", "BATTLEFIELD:BF", []));
        for (var i = 0; i < 6 && game.State.SpellDuelState.IsActive; i++)
            if (game.State.PriorityPlayerId is { } priority)
                await game.Act(priority, new PassPriorityCommand());
            else
                await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        Assert.False(game.State.SpellDuelState.IsActive);
    }

    [Theory]
    [InlineData("BASE", false)] [InlineData("BATTLEFIELD", false)]
    [InlineData("BASE", true)] [InlineData("BATTLEFIELD", true)]
    public async Task DestroyingTheEquipmentEarnsOneRespondableReadyEphemeralSpriteInItsControllersBase(string zone, bool stolen)
    {
        var game = new Driver(zone == "BATTLEFIELD" ? BeforeMainTransformation(stolen) : Position(stolen));
        if (zone == "BATTLEFIELD") await AnimateAndMove(game);
        await Destroy(game);
        Assert.Contains("LANTERN", game.State.PlayerZones[stolen ? "P2" : "P1"].Graveyard);
        Assert.Empty(Sprites(game.State)); var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId); Assert.Equal(Lantern, skill.CardNo);
        Assert.Empty(skill.TargetObjectIds); Assert.NotNull(skill.UnitEvent!.ArmamentDeath);
        await game.Top(); var sprite = Assert.Single(Sprites(game.State));
        Assert.Equal(3, sprite.Power); Assert.False(sprite.IsExhausted); Assert.Contains(CardObjectTags.Ephemeral, sprite.Tags);
        Assert.Equal("P1", sprite.ControllerId); Assert.Contains(sprite.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheEquipmentDoesNotCountAsItsDestruction()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "SFD·135/221", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["LANTERN"])); await game.Top();
        Assert.Contains("LANTERN", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Empty(Sprites(game.State)); await game.Replay();
    }

    [Fact]
    public async Task NaturalEphemeralDestructionEarnsTheLastBreathBeforeScoringAndTheNewSpriteSurvivesThisStart()
    {
        var game = new Driver(Position() with { Phase = MatchPhases.TurnStart,
            TimingState = TimingStates.NeutralClosed });
        await game.Act("P1", new PassPriorityCommand());
        Assert.NotNull(Assert.Single(game.State.StackItems).EphemeralStart); await game.Top();
        Assert.Contains("LANTERN", game.State.PlayerZones["P1"].Graveyard); Assert.Empty(Sprites(game.State));
        Assert.Equal("SCORE", game.State.TurnStartStep);
        Assert.Equal(0, game.State.PlayerScores.GetValueOrDefault("P1"));
        Assert.Equal(Lantern, Assert.Single(game.State.StackItems).CardNo);
        await game.Top(); var sprite = Assert.Single(Sprites(game.State));
        Assert.False(sprite.IsExhausted); Assert.Equal(3, sprite.Power); Assert.Contains(CardObjectTags.Ephemeral, sprite.Tags);
        Assert.Equal(MatchPhases.Main, game.State.Phase); await game.Replay();
    }

    [Theory]
    [InlineData("death")] [InlineData("erase")]
    public async Task RecoveryRejectsARewrittenOrErasedEquipmentDeathSkill(string change)
    {
        var game = new Driver(Position()); await Destroy(game); var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change == "erase" ? [] : [skill with {
            UnitEvent = skill.UnitEvent! with { ArmamentDeath = null } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task DestroyingTheEquipmentAsAnActualAdditionalCostAlsoEarnsItsIndependentLastBreath()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "THUG", "SFD·160/221", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("THUG", "SFD·160/221", [],
            OptionalCosts: ["DESTROY_FRIENDLY_PERMANENT:LANTERN"]));
        Assert.Contains("LANTERN", game.State.PlayerZones["P1"].Graveyard);
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        Assert.Equal(Lantern, Assert.Single(game.State.StackItems).CardNo); Assert.Empty(Sprites(game.State));
        await game.Top(); Assert.Single(Sprites(game.State)); await game.Replay();
    }
}
