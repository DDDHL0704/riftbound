using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class ArmamentHoldConquestAliasTests
{
    private static async Task OpenConquestInstruction(Driver game, bool hold)
    {
        if (hold) { await game.Act("P2", new EndTurnCommand()); await Order(game); }
        else await Conquer(game);
    }

    private static int Power(MatchState state, string id) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    [Theory]
    [InlineData(false, "DRAW")] [InlineData(false, "CALL_RUNE")]
    [InlineData(true, "DRAW")] [InlineData(true, "CALL_RUNE")]
    public async Task ConquestDrawOrRuneUsesTheSameMandatoryModeAndResponsesAtEitherTiming(bool hold, string mode)
    {
        var initial = Board("OGN·155/298", hold: hold);
        for (var i = 0; i < 6; i++) initial = RadGearTestPosition.Put(initial, "RUNE" + i, "VEN·R01", "P1", "RUNE_DECK");
        var game = new Driver(initial); await OpenConquestInstruction(game, hold);
        var item = Assert.Single(game.State.StackItems); Assert.Equal(hold, item.TimingAlias is not null);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_EVENT_MODE_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(new[] { "DRAW", "CALL_RUNE" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [mode]));
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(6, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Top();
        Assert.Equal((hold ? 1 : 0) + (mode == "DRAW" ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal((hold ? 2 : 0) + (mode == "CALL_RUNE" ? 1 : 0),
            game.State.PlayerZones["P1"].Base.Count(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)));
        if (mode == "CALL_RUNE") Assert.True(game.State.CardObjects["RUNE0"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task PrintedAndCopiedConquestBoonsAndMightConfirmTargetsAndSurviveSourceReturn(bool hold, bool copied, bool might)
    {
        var initial = Board(might ? "UNL-027/219" : "UNL-029/219", hold: hold);
        if (copied) initial = WithCopiedPrintedSkill(initial);
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "PAYRUNE", "VEN·R01", "P1", "BASE");
        var game = new Driver(initial); var before = Power(initial, "ALLY"); await OpenConquestInstruction(game, hold);
        var count = 1 + (copied ? 1 : 0);
        // Crimson amplifies the real conquest, including every copied instance.
        if (!hold && !might) count *= 2 + (copied ? 1 : 0);
        Assert.Equal(count, game.State.StackItems.Count);
        for (var i = 0; i < count; i++) {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Contains("ALLY", choice.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        }
        Assert.Equal(before, Power(game.State, "ALLY"));
        if (hold) await game.TapRunes("P1", ["PAYRUNE"]);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await Drain(game);
        Assert.Equal(before + (might ? 8 * count : 1), Power(game.State, "ALLY"));
        if (might) {
            var ledger = game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers
                .Where(m => m.EffectKind == CoreRuleEngine.UnitEventEffect && m.SourceCardNo == "UNL-027/219").ToArray();
            Assert.Equal(count, ledger.Length); Assert.All(ledger, entry => Assert.Equal("HOST", entry.SourceObjectId));
            var views = ResolutionResult.BuildSnapshots(game.State);
            foreach (var viewer in new[] { "P1", "P2" }) {
                var snapshot = JsonSerializer.SerializeToElement(views[viewer], new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var effects = snapshot.GetProperty("timing").GetProperty("continuousEffects").EnumerateArray()
                    .Where(e => e.GetProperty("effectKind").GetString() == CoreRuleEngine.UnitEventEffect
                        && e.GetProperty("sourceCardNo").GetString() == "UNL-027/219").ToArray();
                Assert.Equal(count, effects.Length);
                Assert.All(effects, effect => Assert.Equal(viewer == "P1" ? "HOST" : "HIDDEN",
                    effect.GetProperty("sourceObjectId").GetString()));
            }
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task OptionalConquestEquipmentDestructionRequiresAConfirmedChoiceAndSuccessfulDestruction(bool hold, bool accept)
    {
        var initial = RadGearTestPosition.Put(Board("OGN·056/298", hold: hold), "ENEMY-GEAR", "SFD·022/221", "P2", "BASE");
        var game = new Driver(initial); await OpenConquestInstruction(game, hold);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Contains("ENEMY-GEAR", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["ENEMY-GEAR"] : []));
        Assert.Equal(0, game.State.CardObjects["HOST"].BoonCount);
        Assert.Contains("ENEMY-GEAR", game.State.PlayerZones["P2"].Base);
        if (accept) await game.Top();
        Assert.Equal(accept ? 1 : 0, game.State.CardObjects["HOST"].BoonCount);
        Assert.Equal(accept, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY-GEAR")); await game.Replay();
    }

    [Fact]
    public async Task DestroyingItsOwnArmamentGrantsTheSavingBoonBeforeStateBasedCleanup()
    {
        var initial = Board("OGN·056/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Damage = 3 } } };
        var game = new Driver(initial); await Conquer(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ARMAMENT0"]));
        await game.Top();
        Assert.Contains("ARMAMENT0", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(1, game.State.CardObjects["HOST"].BoonCount);
        Assert.Equal(4, Power(game.State, "HOST")); await game.Replay();
    }

    [Fact]
    public async Task EquipmentDestructionSurvivesTheSourceButDoesNotGiveABoonToItsNewHandIncarnation()
    {
        var initial = RadGearTestPosition.Put(Board("OGN·056/298"), "ENEMY-GEAR", "SFD·022/221", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); await Conquer(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY-GEAR"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        await game.Top(); Assert.Contains("ENEMY-GEAR", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand); Assert.Equal(0, game.State.CardObjects["HOST"].BoonCount);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task FirstConquestReadinessIsConsumedAtCaptureAcrossBothTimingsAndRealSubsequentMoves(bool firstHold, bool copied)
    {
        var initial = Board("SFD·113/221", hold: firstHold);
        if (copied) initial = WithCopiedPrintedSkill(initial);
        initial = RadGearTestPosition.Put(initial, "THIRD", "OGN·296/298", "P2", "BATTLEFIELD", "THIRD");
        initial = RadGearTestPosition.Put(initial, "READY", "OGN·146/298", "P1", "HAND");
        for (var i = 0; i < 2; i++) initial = RadGearTestPosition.Put(initial, "PAYRUNE" + i, "VEN·R01", "P1", "BASE");
        var game = new Driver(initial); await OpenConquestInstruction(game, firstHold);
        Assert.Equal(copied ? 2 : 1, game.State.StackItems.Count);
        Assert.All(game.State.StackItems, item => Assert.Equal(game.State.TurnNumber, item.UnitEvent!.FirstConquestTurn));
        Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith("FIRST_CONQUEST_SKILL:"));
        await Drain(game); Assert.False(game.State.CardObjects["HOST"].IsExhausted); await CloseDuel(game);
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD", "BASE", []));
        await CloseDuel(game); await game.TapRunes("P1", ["PAYRUNE0", "PAYRUNE1"]);
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"])); await game.Top(); await CloseDuel(game);
        var destination = firstHold ? "OTHER" : "THIRD";
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:" + destination, []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(copied ? 2 : 1, game.Events().Count(e => e.Event.Kind == "TRIGGER_QUEUED" && e.Event.Payload.ContainsKey("firstConquestUse")));
        await game.Replay();
    }

    [Fact]
    public async Task CopyingThePrintedSkillAfterTheFirstConquestCannotMakeTheNextEventFirstAgain()
    {
        var initial = Board("SFD·113/221");
        initial = RadGearTestPosition.Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "THIRD", "OGN·296/298", "P2", "BATTLEFIELD", "THIRD");
        initial = RadGearTestPosition.Put(initial, "READY", "OGN·146/298", "P1", "HAND");
        var game = new Driver(initial); await Conquer(game); await game.Top(); await CloseDuel(game);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top(); await CloseDuel(game); Assert.NotNull(game.State.CardObjects["SONG"].AttachedRuleText);
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD", "BASE", [])); await CloseDuel(game);
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"])); await game.Top(); await CloseDuel(game);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:THIRD", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED" && e.Event.Payload.ContainsKey("firstConquestUse"));
        await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("append")]
    public async Task FirstConquestUseCannotBeRemovedOrInventedInASelfConsistentRecoveryFrame(string change)
    {
        var game = new Driver(Board("SFD·113/221")); await Conquer(game);
        var forged = game.State with { UntilEndOfTurnEffects = change == "erase"
            ? game.State.UntilEndOfTurnEffects.Where(e => !e.StartsWith("FIRST_CONQUEST_SKILL:")).ToArray()
            : [..game.State.UntilEndOfTurnEffects, "FIRST_CONQUEST_SKILL:" + new string('0', 64)] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AConfirmedConquestModeCannotBeChangedOrErasedAgainstTheAcceptedHistory(bool erase)
    {
        var game = new Driver(Board("OGN·155/298")); await Conquer(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW"]));
        var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = [item with { UnitEvent = item.UnitEvent! with { Mode = erase ? null : "CALL_RUNE" } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task HoldingCannotInventTheOverkillConditionOfAConvertedConquestSkill()
    {
        var game = new Driver(Board("UNL-018/219", hold: true)); await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.Empty(Gold(game.State)); await game.Replay();
    }
}
