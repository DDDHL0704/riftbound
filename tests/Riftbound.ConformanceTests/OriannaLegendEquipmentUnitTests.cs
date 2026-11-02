using System.Text.Json;
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

    private static void AssertMight(Driver game, int expectedBase, int expectedMight)
    {
        foreach (var snapshot in ResolutionResult.BuildSnapshots(game.State).Values.Append(
            MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State).SpectatorSnapshot))
        {
            var gear = JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("GEAR");
            Assert.Equal(expectedBase, gear.GetProperty("basePower").GetInt32());
            Assert.Equal(expectedMight, gear.GetProperty("effectivePower").GetInt32());
        }
        ArmamentMightTests.PublicRecovery(game.State);
    }

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
    public async Task IndependentLegendConversionsEndOnlyTheirOwnDuration(bool endLatest)
    {
        var initial = Position();
        var other = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER-LEGEND", "RAD·171/167", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [other.ObjectId] = other },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND", other.ObjectId] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                [other.ObjectId] = new("P1", "LEGEND") } };
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P2", new EndTurnCommand());
        await ResolveMainTriggers(_ => "GEAR");
        await game.ColdCheckpoint();
        var latest = game.State.CardObjects["GEAR"].LegendEquipmentUnit!;
        var earlier = Assert.Single(latest.EarlierEffects!);
        Assert.NotEqual(latest.SourceLegend, earlier.SourceLegend);
        var selected = endLatest ? latest.SourceLegend.ObjectId : earlier.SourceLegend.ObjectId;
        var retained = endLatest ? earlier : latest with { EarlierEffects = null };
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        await ResolveMainTriggers(source => source == selected ? "SECOND" : null);
        Assert.Equal(retained, game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        Assert.Equal(selected, game.State.CardObjects["SECOND"].LegendEquipmentUnit!.SourceLegend.ObjectId);
        AssertMight(game, 2, 2);
        await game.ColdCheckpoint(); await game.Replay();

        async Task ResolveMainTriggers(Func<string, string?> targetForSource)
        {
            for (var step = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; step++) {
                Assert.True(step < 12, "Main-phase confirmation did not complete.");
                if (game.State.PendingCardChoice is { } choice) {
                    var target = targetForSource(choice.SourceObjectId!);
                    if (target is null) {
                        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
                        continue;
                    }
                    await Select(game, target);
                    if (game.State.RunePools["P1"].Mana == 0)
                        await game.TapRunes("P1", ["RUNE-0", "RUNE-1"]);
                    var payment = game.State.PendingPayment!;
                    await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
                } else if (game.State.TriggerQueue.Count > 0)
                    await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                else await game.Top();
            }
        }
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

    [Theory]
    [InlineData("RAD·071/167", "坚守2", 0)]
    [InlineData("RAD·123/167", "壁垒", 1)]
    public async Task AttachedKeywordsContinueOnTheFormerUnitUntilTheWeaponActuallyReattaches(string no, string keyword, int bonus)
    {
        var initial = Put(Put(Position(), "WEAPON", no, "P1", "BASE"), "TAKE", "SFD·011/221", "P1", "HAND");
        var game = new Driver(initial);
        await BeginMain(game); await Select(game, "GEAR"); await Pay(game); await game.Top();
        await game.TapRunes("P1", ["RUNE-1"]);
        await game.Act("P1", new AssembleEquipmentCommand("WEAPON", "GEAR", ["ASSEMBLE_1_MANA"]));
        await game.Top();
        Assert.Contains(keyword, game.State.CardObjects["GEAR"].Tags);
        AssertMight(game, 2, 2 + bonus);
        await NextOwnMain(game); await Select(game, "SECOND"); await Pay(game);
        var formerUnit = game.State.CardObjects["GEAR"];
        Assert.DoesNotContain(CardObjectTags.UnitCard, formerUnit.Tags);
        Assert.Contains(keyword, formerUnit.Tags);
        Assert.Contains(keyword, formerUnit.RuleDerivedTags!);
        Assert.Equal("GEAR", game.State.CardObjects["WEAPON"].AttachedToObjectId);
        AssertMight(game, 0, 0);
        await game.Top();
        // The attached weapon's printed assembly text is inactive (CN 718.2).
        // Take Up reattaches it through an actual spell instruction instead.
        await game.TapRunes("P1", ["RUNE-1", "RUNE-2"]);
        await game.Act("P1", new PlayCardCommand("TAKE", "SFD·011/221", ["ALLY", "WEAPON"]));
        // Confirmation alone does not remove the existing lower-box text.
        Assert.Contains(keyword, game.State.CardObjects["GEAR"].Tags);
        await game.Top();
        Assert.DoesNotContain(keyword, game.State.CardObjects["GEAR"].Tags);
        Assert.Null(game.State.CardObjects["GEAR"].RuleDerivedTags);
        Assert.Contains(keyword, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal("ALLY", game.State.CardObjects["WEAPON"].AttachedToObjectId);
        await game.Replay();
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
    [InlineData(false)] [InlineData(true)]
    public async Task TheLaterCharacteristicChangeWinsEvenWhenTheLegendTriggeredEarlier(bool replacementFirst)
    {
        var initial = Put(Position(), "LULU", "RAD·110/167", "P1", "BASE");
        initial = Put(initial, "MOVE", "VEN·034", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        // The imported position retains a boon and a weapon from the unit's
        // previous transformation. Neither additive effect replaces base Might.
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { Power = 1,
                Tags = initial.CardObjects["GEAR"].Tags.Append(CardObjectTags.Boon).Order(StringComparer.Ordinal).ToArray() },
            ["SECOND"] = initial.CardObjects["SECOND"] with { AttachedToObjectId = "GEAR" } } };
        var game = new Driver(initial);
        await BeginMain(game); await Select(game, "GEAR"); await Pay(game); await game.Top();
        AssertMight(game, 2, 4);
        await NextOwnMain(game); await Select(game, "GEAR"); await Pay(game);
        if (!replacementFirst) await game.Top();
        await game.TapRunes("P1", ["RUNE-1", "RUNE-2", "RUNE-3"]);
        // A real reaction moves Lulu while the confirmed legend ability can
        // remain lower in the chain. Her movement skill sets this unit to 4.
        await game.Act("P1", new PlayCardCommand("MOVE", "VEN·034", ["BF", "LULU"],
            OptionalCosts: ["RECYCLE_RUNE:GREEN"]));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await game.Top();
        AssertMight(game, 4, 6);
        if (replacementFirst) {
            Assert.Single(game.State.StackItems);
            var transformation = game.State.StackItems[0].UnitEvent!;
            Assert.True(transformation.CapturedTick < game.State.Tick);
            await game.Top();
        }
        AssertMight(game, replacementFirst ? 2 : 4, replacementFirst ? 4 : 6);
        Assert.Equal("GEAR", game.State.CardObjects["SECOND"].AttachedToObjectId);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["GEAR"].Tags);
        var gear = game.State.CardObjects["GEAR"];
        foreach (var corruptTransformation in new[] { false, true }) {
            var badGear = corruptTransformation
                ? gear with { LegendEquipmentUnit = gear.LegendEquipmentUnit! with { AppliedTick = gear.LegendEquipmentUnit.CapturedTick } }
                : gear with { UntilEndOfTurnEffects = gear.UntilEndOfTurnEffects.Select(effect =>
                    effect.StartsWith("BASE_MIGHT_APPLIED_TICK_THIS_TURN:", StringComparison.Ordinal)
                        ? "BASE_MIGHT_APPLIED_TICK_THIS_TURN:1" : effect).Order(StringComparer.Ordinal).ToArray() };
            var bad = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) { ["GEAR"] = badGear } };
            var seq = game.Events()[^1].Sequence;
            var snapshots = ResolutionResult.BuildSnapshots(bad); var prompts = ResolutionResult.BuildPrompts(bad);
            var views = bad.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, bad.Tick,
                seq, snapshots[player], bad.Tick, seq, prompts[player]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(bad.RoomId, bad.Tick, seq, [], bad);
            Assert.Contains(MatchRecoveryValidator.Validate(bad.RoomId, seq, game.Commands(), game.Events(), views,
                bad, bad.Tick, spectator), error => error.Contains(corruptTransformation
                    ? "legend equipment unit timestamp differs" : "base Might replacement differs"));
        }
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.CardObjects["GEAR"].UntilEndOfTurnEffects);
        AssertMight(game, 2, 4);
        await game.Replay();
    }

    [Theory]
    [InlineData("source")] [InlineData("face")] [InlineData("tick")]
    [InlineData("target-generation")] [InlineData("card-no")] [InlineData("source-null")]
    public async Task RecoveryRejectsForgedTransformationProvenance(string change)
    {
        var game = new Driver(Position()); await BeginMain(game);
        await Select(game, "GEAR"); await Pay(game); await game.Top();
        var gear = game.State.CardObjects["GEAR"]; var captured = gear.LegendEquipmentUnit!;
        var forged = captured with {
            SourceLegend = change == "source-null" ? null! : change == "source" ? new("MISSING", 0) : captured.SourceLegend,
            SourceFaceRevision = change == "face" ? 999 : captured.SourceFaceRevision,
            CapturedTick = change == "tick" ? game.State.Tick + 1 : captured.CapturedTick,
            TargetGeneration = change == "target-generation" ? gear.ObjectGeneration + 1 : captured.TargetGeneration,
            SourceCardNo = change == "card-no" ? "OGN·253/298" : captured.SourceCardNo };
        var bad = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["GEAR"] = gear with { LegendEquipmentUnit = forged } } };
        Assert.Contains("invalid legend equipment unit state", OfficialInsightAndSpellLockTests.Errors(bad));
        if (change == "source-null") {
            var seq = game.Events()[^1].Sequence;
            var snapshots = ResolutionResult.BuildSnapshots(bad); var prompts = ResolutionResult.BuildPrompts(bad);
            var views = bad.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, bad.Tick,
                seq, snapshots[player], bad.Tick, seq, prompts[player]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(bad.RoomId, bad.Tick, seq, [], bad);
            Assert.Contains("invalid legend equipment unit state", MatchRecoveryValidator.Validate(bad.RoomId, seq,
                game.Commands(), game.Events(), views, bad, bad.Tick, spectator));
        }
        await game.Replay();
    }
}
