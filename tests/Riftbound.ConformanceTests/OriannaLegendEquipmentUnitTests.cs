using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class OriannaLegendEquipmentUnitTests
{
    private const string RetiredActivation = "LEGEND_PAY_1_EXHAUST_TURN_EQUIPMENT_INTO_UNIT";

    private static MatchState Position(string legendNo = "RAD·145/167")
    {
        var state = Put(Create(), "GEAR", "SFD·022/221", "P1", "BASE");
        state = Put(state, "SECOND", "SFD·033/221", "P1", "BASE");
        state = Put(state, "ENEMY-GEAR", "SFD·033/221", "P2", "BASE");
        for (var i = 0; i < 4; i++) state = Put(state, "RUNE-" + i, "UNL-R03", "P1", "BASE");
        state = Put(state, "RED", "UNL-R01", "P1", "BASE");
        state = Put(state, "GREEN", "UNL-R02", "P1", "BASE");
        var legend = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", legendNo, "P1");
        return state with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["LEGEND"] = legend },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
    }

    private static async Task BeginMain(Driver game)
    {
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal("MAIN", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        Assert.NotNull(game.State.PendingCardChoice);
    }

    private static async Task Select(Driver game, string target)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.ManaCost);
        Assert.Equal(0, payment.PowerCost);
    }

    private static async Task Pay(Driver game)
    {
        await game.TapRunes("P1", ["RUNE-0"]);
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
    }

    private static int Might(MatchState state, string id = "GEAR")
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    private static async Task NextOwnMain(Driver game)
    {
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.StackItems, item => item.UnitEvent?.Timing == "MAIN");
        await BeginMain(game);
    }

    private static MatchState DeployedOrb(MatchState state)
    {
        state = Put(state, "GEAR", "RAD·146/167", "P1", "BATTLEFIELD", "BF");
        // PrintedCardFactory restores the card's face; battlefield fixtures
        // also need the Deploy keyword materialized by normal equipment entry.
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with {
                Tags = state.CardObjects["GEAR"].Tags.Append(CardEquipmentKeywordNames.Deploy).Order(StringComparer.Ordinal).ToArray() } } };
    }

    [Theory]
    [InlineData("RAD·145/167", 39653)]
    [InlineData("RAD·171*/167", 39682)]
    [InlineData("RAD·171/167", 39683)]
    public async Task EachPrintTriggersOnlyAtOwnMainAndPaysWithoutExhaustingTheLegend(string no, long id)
    {
        var official = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(id, official.Id);
        var initial = Position(no);
        await Reject(initial, "P2", new LegendActCommand("LEGEND", RetiredActivation, ["GEAR"], ["SPEND_MANA:1"]));
        var game = new Driver(initial);
        await BeginMain(game);
        var choice = game.State.PendingCardChoice!;
        Assert.Contains("GEAR", choice.LegalObjectIds);
        Assert.Contains("SECOND", choice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY-GEAR", choice.LegalObjectIds);
        Assert.DoesNotContain("ALLY", choice.LegalObjectIds);
        await Select(game, "GEAR");
        await Pay(game);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await game.Top();
        var gear = game.State.CardObjects["GEAR"];
        Assert.Equal(gear.ManaCost, Might(game.State));
        Assert.Contains(CardObjectTags.EquipmentCard, gear.Tags);
        Assert.Contains(CardObjectTags.UnitCard, gear.Tags);
        Assert.NotNull(gear.LegendEquipmentUnit);
        await Reject(game.State, "P1", new LegendActCommand("LEGEND", RetiredActivation, ["GEAR"], ["SPEND_MANA:1"]));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheOptionalTriggerCanBeDeclinedAtSelectionOrPayment(bool afterSelection)
    {
        var game = new Driver(Position());
        await BeginMain(game);
        if (afterSelection) {
            await Select(game, "GEAR");
            var payment = game.State.PendingPayment!;
            await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        } else {
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task InvalidTargetsAndTheWrongPlayerCannotMutateTheConfirmation()
    {
        var game = new Driver(Position()); await BeginMain(game);
        var choice = game.State.PendingCardChoice!;
        foreach (var id in new[] { "ALLY", "ENEMY-GEAR", "LEGEND", "MISSING" })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await Select(game, "GEAR"); await Pay(game); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task PayingForAnotherEquipmentEndsTheOldEffectBeforeResponsesAndPreservesOtherTags()
    {
        var game = new Driver(Position()); await BeginMain(game);
        await Select(game, "GEAR"); await Pay(game); await game.Top();
        await NextOwnMain(game);
        Assert.NotNull(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await Select(game, "SECOND");
        Assert.NotNull(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await Pay(game);
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.DoesNotContain(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        Assert.Contains(CardEquipmentKeywordNames.Weapon, game.State.CardObjects["GEAR"].Tags);
        Assert.Null(game.State.CardObjects["SECOND"].LegendEquipmentUnit);
        await game.Top();
        Assert.Equal(1, Might(game.State, "SECOND"));
        await game.Replay();
    }

    [Fact]
    public async Task DecliningANewSelectionPreservesTheExistingEffectAcrossTurns()
    {
        var game = new Driver(Position()); await BeginMain(game);
        await Select(game, "GEAR"); await Pay(game); await game.Top();
        await NextOwnMain(game); await Select(game, "SECOND");
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.NotNull(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.Equal(2, Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConfirmingAnotherEquipmentImmediatelyRecallsOnlyTheOrdinaryFormerUnit(bool deployed)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "ALLY", "P1", "BATTLEFIELD", "BF");
        if (deployed) initial = DeployedOrb(initial);
        var game = new Driver(initial);
        await BeginMain(game); await Select(game, "GEAR"); await Pay(game); await game.Top();
        if (!deployed) await game.Act("P1", new MoveUnitCommand("GEAR", "BASE", "BATTLEFIELD:BF"));
        Assert.Equal("BF", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        await NextOwnMain(game); await Select(game, "SECOND");
        Assert.NotNull(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        var generation = game.State.CardObjects["GEAR"].ObjectGeneration;
        await Pay(game);
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.DoesNotContain(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        Assert.Equal(new ObjectLocationState("P1", deployed ? "BATTLEFIELD" : "BASE", deployed ? "BF" : null),
            game.State.ObjectLocations["GEAR"]);
        Assert.Equal(generation, game.State.CardObjects["GEAR"].ObjectGeneration);
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal(deployed ? 0 : 1, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_RECALLED_TO_BASE"
            && Equals(e.Event.Payload["equipmentObjectId"], "GEAR")));
        Assert.Null(game.State.CardObjects["SECOND"].LegendEquipmentUnit);
        Assert.Single(game.State.StackItems);
        Assert.Empty(game.State.PendingCleanupTasks);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ADeployedEquipmentBecomingAUnitStartsTheEnemyOrOpenBattlefieldContestAfterResolution(bool enemy)
    {
        var initial = DeployedOrb(Position());
        if (enemy) {
            initial = RadUnitEffectTestFixture.Place(initial, "BF", "P2", "BATTLEFIELD", "BF");
            initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "BF");
        } else initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = null } } };
        var game = new Driver(initial);
        await BeginMain(game); await Select(game, "GEAR"); await Pay(game);
        Assert.False(game.State.BattlefieldStates["BF"].Contested);
        Assert.False(game.State.SpellDuelState.IsActive);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "SPELL_DUEL_STARTED");
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.True(game.State.BattlefieldStates["BF"].Contested);
        Assert.True(game.State.SpellDuelState.IsActive);
        Assert.Equal("BF", game.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("P1", game.State.SpellDuelState.FocusPlayerId);
        var contested = Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONTESTED");
        Assert.Equal("P1", contested.Event.Payload["playerId"]);
        Assert.Contains(game.State.BattlefieldTasks, task => task.Kind == "START_SPELL_DUEL"
            && task.BattlefieldObjectId == "BF");
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        Assert.False(game.State.SpellDuelState.IsActive);
        if (enemy) Assert.Contains(game.State.PendingTaskQueue.Tasks, task => task.Kind == "START_BATTLE"
            && task.BattlefieldObjectId == "BF");
        else Assert.Equal("P1", game.State.BattlefieldStates["BF"].ControllerId);
        await game.Replay();
    }

    [Fact]
    public async Task RepeatedSelectionDoesNotStackAndARealReturnResponseClearsTheOldGeneration()
    {
        var initial = Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); await BeginMain(game);
        await Select(game, "GEAR"); await Pay(game); await game.Top();
        await NextOwnMain(game); await Select(game, "GEAR"); await Pay(game);
        Assert.NotNull(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await game.TapRunes("P1", ["RUNE-1"]);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["GEAR"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.DoesNotContain(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task BecomingAUnitDetachesTheWeaponAndLaterAssemblyPaysButCannotAttachIt()
    {
        var game = new Driver(Position() with { TurnPlayerId = "P1", ActivePlayerId = "P1" });
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_RED"]));
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await NextOwnMain(game); await Select(game, "GEAR"); await Pay(game); await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["ASSEMBLE_RED", "RECYCLE_RUNE:RED"]));
        Assert.Contains("RED", game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·022/221", "ASSEMBLE_RED", "RED")]
    [InlineData("SFD·059/221", "ASSEMBLE_GREEN", "GREEN")]
    public async Task WeaponsAndCopiedTextStayAttachedWhenTheirHostStopsBeingAUnit(string no, string cost, string rune)
    {
        var game = new Driver(Put(Position(), "WEAPON", no, "P1", "BASE"));
        await BeginMain(game); await Select(game, "GEAR"); await Pay(game); await game.Top();
        await game.TapRunes("P1", ["RUNE-1"]);
        await game.Act("P1", new AssembleEquipmentCommand("WEAPON", "GEAR", [cost, "RECYCLE_RUNE:" + rune]));
        await game.Top();
        Assert.Equal("GEAR", game.State.CardObjects["WEAPON"].AttachedToObjectId);
        var copied = game.State.CardObjects["WEAPON"].AttachedRuleText;
        if (no == "SFD·059/221") Assert.NotNull(copied);
        await NextOwnMain(game); await Select(game, "SECOND"); await Pay(game);
        Assert.DoesNotContain(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        Assert.Equal("GEAR", game.State.CardObjects["WEAPON"].AttachedToObjectId);
        Assert.Equal(copied, game.State.CardObjects["WEAPON"].AttachedRuleText);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task TemporaryMightAndBoonComposeWithTheBaseChangeAndDoNotCorruptRecovery()
    {
        var game = new Driver(Position()); await BeginMain(game);
        await Select(game, "GEAR"); await Pay(game); await game.Top();
        // Seed the independent numeric components to check layer composition;
        // the transformation itself comes from the actual main-phase trigger.
        var gear = game.State.CardObjects["GEAR"];
        var modified = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["GEAR"] = gear with { Power = gear.Power + 4, UntilEndOfTurnPowerModifier = 3,
                Tags = gear.Tags.Append("增益").Order(StringComparer.Ordinal).ToArray() } } };
        Assert.Equal(6, Might(modified));
        OfficialInsightAndSpellLockTests.Restore(modified);
        ArmamentMightTests.PublicRecovery(modified);
        await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("face")] [InlineData("tick")]
    [InlineData("target-generation")] [InlineData("card-no")]
    public async Task RecoveryRejectsForgedTransformationProvenance(string change)
    {
        var game = new Driver(Position()); await BeginMain(game);
        await Select(game, "GEAR"); await Pay(game); await game.Top();
        var gear = game.State.CardObjects["GEAR"]; var captured = gear.LegendEquipmentUnit!;
        var forged = captured with {
            SourceLegend = change == "source" ? new("MISSING", 0) : captured.SourceLegend,
            SourceFaceRevision = change == "face" ? 999 : captured.SourceFaceRevision,
            CapturedTick = change == "tick" ? game.State.Tick + 1 : captured.CapturedTick,
            TargetGeneration = change == "target-generation" ? gear.ObjectGeneration + 1 : captured.TargetGeneration,
            SourceCardNo = change == "card-no" ? "OGN·253/298" : captured.SourceCardNo };
        var bad = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["GEAR"] = gear with { LegendEquipmentUnit = forged } } };
        Assert.Contains("invalid legend equipment unit state", OfficialInsightAndSpellLockTests.Errors(bad));
        await game.Replay();
    }
}
