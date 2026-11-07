using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 383.4.c-d, 718 and SFD judge FAQ260114 p11.
public sealed class ArmamentHoldConquestAliasTests
{
    private const string Armament = "SFD·030/221";

    private static MatchState Board(string host, bool hold = false, int copies = 1)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·296/298"),
            "HOST", host, "P1", "BASE");
        if (hold) state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < copies; i++)
        {
            var id = "ARMAMENT" + i;
            state = RadGearTestPosition.Put(state, id, Armament, "P1", hold ? "BATTLEFIELD" : "BASE", hold ? "BF" : null);
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { AttachedToObjectId = "HOST" } } };
        }
        if (hold) state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task AHoldDrawIsEarnedByNaturalConquestAndMultipleAliasArmamentsDoNotDuplicateIt(int copies)
    {
        var game = new Driver(Board("SFD·027/221", copies: copies));
        var top = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await Conquer(game);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(top, game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_HELD");
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task AConquestDrawIsEarnedByNaturalHoldBeforeNormalChannelAndDraw(int copies)
    {
        var game = new Driver(Board("OGN·039/298", hold: true, copies: copies));
        var top = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(top, game.State.PlayerZones["P1"].Hand); // One skill draw, then normal draw.
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Fact]
    public async Task AnUnattachedArmamentDoesNotGiveTheHostItsHoldSkillDuringConquest()
    {
        var state = Board("SFD·027/221", copies: 0);
        state = RadGearTestPosition.Put(state, "LOOSE", Armament, "P1", "BASE");
        var game = new Driver(state); await Conquer(game);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task TheOfficialYoneOpenBattlefieldConditionStillFailsDuringHolding()
    {
        var game = new Driver(Board("SFD·116/221", hold: true));
        await game.Act("P2", new EndTurnCommand());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHostAfterTheConquestKeepsTheEarnedHoldDrawIndependent()
    {
        var initial = RadGearTestPosition.Put(Board("SFD·027/221"), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); var top = initial.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await Conquer(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var publicSkill = Assert.IsType<Dictionary<string, object?>>(Assert.Single(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        Assert.Equal("HIDDEN", publicSkill["sourceObjectId"]);
        Assert.Equal("SFD·027/221", publicSkill["cardNo"]); Assert.Equal("HOLD_DRAW", publicSkill["effectKind"]);
        Assert.False(publicSkill.ContainsKey("timingAlias"));
        await game.Top();
        Assert.All(top, id => Assert.Contains(id, game.State.PlayerZones["P1"].Hand));
        await game.Replay();
    }
    private static async Task ConquerGroup(Driver game, params string[] others)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", [], ["HOST", ..others]));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        await Order(game);
    }

    private static async Task Order(Driver game)
    {
        var prompt = ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p =>
            p.Candidates?.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled) == true);
        if (prompt is not null) await game.Act(prompt.PlayerId, new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Where(t => t.ControllerId == prompt.PlayerId).Select(t => t.TriggerId).ToArray()));
    }

    private static async Task Drain(Driver game)
    {
        for (var i = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; i++) {
            Assert.InRange(i, 0, 20); await Order(game); await game.Top();
        }
    }

    private static async Task CloseDuel(Driver game)
    {
        for (var i = 0; game.State.SpellDuelState.IsActive; i++) {
            Assert.InRange(i, 0, 7);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
    }

    private static IEnumerable<CardObjectState> Gold(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币");

    [Fact]
    public async Task ActualPlayAndRedAssemblyTurnTheHostsHoldIntoAnIndependentConquestSkill()
    {
        var initial = RadGearTestPosition.Put(Board("SFD·027/221", copies: 0), "ARMAMENT0", Armament, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ARMAMENT0", Armament, [], Destination: "BASE"));
        var beforeAssembly = game.State.RunePools["P1"];
        await game.Act("P1", new AssembleEquipmentCommand("ARMAMENT0", "HOST", ["ASSEMBLE_RED"]));
        Assert.Equal(beforeAssembly.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(beforeAssembly.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await CloseDuel(game); await Conquer(game);
        Assert.Equal("ARMAMENT0", Assert.Single(game.State.StackItems).TimingAlias!.Grant.Equipment.ObjectId);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Fact]
    public async Task AHoldGrantedByAnotherArmamentAlsoTriggersOnConquestWithoutCopyingEitherLowerBox()
    {
        var initial = RadGearTestPosition.Put(Board("OGN·001/298"), "BOOK", "SFD·086/221", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BOOK"] = initial.CardObjects["BOOK"] with { AttachedToObjectId = "HOST" } } };
        var game = new Driver(initial); await Conquer(game);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("BOOK", item.UnitEvent!.Origin!.Equipment.ObjectId);
        Assert.Equal("ARMAMENT0", item.TimingAlias!.Grant.Equipment.ObjectId);
        Assert.Empty(Gold(game.State)); await game.Top();
        Assert.Equal(2, Gold(game.State).Count()); Assert.All(Gold(game.State), c => Assert.True(c.IsExhausted));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealSongAssemblyCopiesThePrintedSkillAndEachInstanceChangesTimingIndependently(bool hold)
    {
        var initial = Board(hold ? "OGN·039/298" : "SFD·027/221", hold: hold)
            with { ActivePlayerId = "P1", TurnPlayerId = "P1" };
        initial = RadGearTestPosition.Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top(); await CloseDuel(game);
        if (hold) { await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand()); await Order(game); }
        else await Conquer(game);
        var skills = game.State.StackItems.Where(i => i.TimingAlias is not null).ToArray();
        Assert.Equal(2, skills.Length);
        Assert.Single(skills, i => hold ? i.ConquestSource!.Origin is not null : i.HeldContext!.Origin is not null);
        await Drain(game);
        Assert.Equal(hold ? 3 : 4, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task OnlyTheActualEventSelectsBlueOrCrimsonTriggerAmplification(bool hold, bool blue, bool crimson)
    {
        var initial = Board(hold ? "OGN·039/298" : "SFD·027/221", hold: hold);
        var companions = new List<string>();
        foreach (var (enabled, id, no) in new[] { (blue, "BLUE", "UNL-087/219"), (crimson, "RED", "UNL-029/219") })
            if (enabled) { initial = RadGearTestPosition.Put(initial, id, no, "P1", hold ? "BATTLEFIELD" : "BASE", hold ? "BF" : null); companions.Add(id); }
        var game = new Driver(initial);
        if (hold) { await game.Act("P2", new EndTurnCommand()); await Order(game); }
        else await ConquerGroup(game, companions.ToArray());
        Assert.Equal(1 + (hold ? blue ? 1 : 0 : crimson ? 1 : 0),
            game.State.StackItems.Count(i => i.TimingAlias is not null));
        await Drain(game); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ExactOtherFriendlyConditionsRemainRequiredWhenHoldingTextBecomesConquest(int companions)
    {
        var initial = Board("VEN·042"); var others = new List<string>();
        for (var i = 0; i < companions; i++) { var id = "COMPANION" + i;
            initial = RadGearTestPosition.Put(initial, id, "OGN·219/298", "P1", "BASE"); others.Add(id); }
        var game = new Driver(initial); await ConquerGroup(game, others.ToArray());
        Assert.Equal(companions == 1 ? 1 : 0, game.State.StackItems.Count(i => i.TimingAlias is not null));
        await Drain(game); Assert.Equal(companions == 1 ? 1 : 0, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AHiddenHereConditionCannotBeSatisfiedByACardAtAnotherBattlefield(bool hidden)
    {
        var initial = Board("RAD·109/167");
        if (hidden) {
            initial = RadGearTestPosition.Put(initial, "PRIVATE-STANDBY", "OGN·057/298", "P1", "BATTLEFIELD", "BF");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["PRIVATE-STANDBY"] = initial.CardObjects["PRIVATE-STANDBY"] with { IsFaceDown = true } } };
        }
        var game = new Driver(initial); await Conquer(game);
        Assert.DoesNotContain(game.State.StackItems, i => i.TimingAlias is not null);
        await Drain(game); Assert.Equal(1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnEmpoweredConquestConditionIsStillCheckedWhenTheUnitHolds(bool empowered)
    {
        var initial = Board("VEN·046", hold: true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = empowered } } };
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand()); await Order(game);
        Assert.Equal(empowered ? 1 : 0, game.State.StackItems.Count(i => i.TimingAlias is not null));
        await Drain(game); Assert.Equal(empowered ? 2 : 1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HuntAlreadyHasBothTimingsAndDoesNotGainADuplicateFromTheArmament(bool hold)
    {
        var initial = Board("UNL-100/219", hold: hold);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-100/219", out var definition));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Tags = initial.CardObjects["HOST"].Tags
                .Concat(definition.SourceUnitTags!.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToArray() } } };
        var game = new Driver(initial);
        if (hold) { await game.Act("P2", new EndTurnCommand()); await Order(game); } else await Conquer(game);
        Assert.DoesNotContain(game.State.StackItems, i => i.TimingAlias is not null);
        await Drain(game); Assert.Equal(3, game.State.PlayerExperience["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("grant")] [InlineData("erase")] [InlineData("erase-and-rename")]
    [InlineData("tick")] [InlineData("timing")] [InlineData("field")]
    public async Task ColdRecoveryRejectsChangedTimingOrGrantAgainstTheOriginalAcceptedCommands(string mutation)
    {
        var game = new Driver(Board("SFD·027/221")); await Conquer(game);
        var item = Assert.Single(game.State.StackItems); var alias = item.TimingAlias!;
        var changed = mutation switch {
            "grant" => alias with { Grant = alias.Grant with { Equipment = new("MISSING", 0) } },
            "tick" => alias with { CapturedTick = alias.CapturedTick - 1 },
            "timing" => alias with { OriginalTiming = "CONQUEST", EffectiveTiming = "HOLD" },
            "field" => alias with { Battlefield = new("BF", 0) },
            _ => null };
        var forged = game.State with { StackItems = [item with { TimingAlias = changed,
            StackItemId = mutation == "erase-and-rename" ? item.StackItemId + ":UNRECORDED" : item.StackItemId }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheConvertedBlueDelayWaitsForTheNextOwnMainEvenAfterItsSourceLeaves(bool extraTurn)
    {
        var initial = RadGearTestPosition.Put(Board("UNL-087/219"), "RETURN", "OGN·104/298", "P1", "HAND")
            with { ExtraTurnPlayerId = extraTurn ? "P1" : null };
        var game = new Driver(initial); await Conquer(game);
        var gain = Assert.Single(game.State.DelayedResourceGains);
        Assert.Equal(initial.TurnNumber + 1, gain.DueTurn); Assert.NotNull(gain.TimingAlias);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top(); await CloseDuel(game);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new EndTurnCommand());
        if (!extraTurn) {
            Assert.Equal("P2", game.State.TurnPlayerId); Assert.Single(game.State.DelayedResourceGains);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P2", new EndTurnCommand());
        }
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Empty(game.State.DelayedResourceGains); Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData("due-turn")] [InlineData("erase")] [InlineData("rename")]
    public async Task TheDelayedConversionAlsoRemainsBoundToItsAcceptedRegistration(string field)
    {
        var game = new Driver(Board("UNL-087/219")); await Conquer(game);
        var gain = Assert.Single(game.State.DelayedResourceGains);
        var forged = game.State with { DelayedResourceGains = [gain with {
            DueTurn = field == "due-turn" ? gain.DueTurn + 1 : gain.DueTurn,
            TimingAlias = field == "erase" ? null : gain.TimingAlias,
            Id = field == "rename" ? gain.Id + ":UNRECORDED" : gain.Id }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }

    [Fact]
    public async Task NamedConquestActivationIncludesConvertedHoldTextWithoutInventingAConquestOrCrimsonRepeat()
    {
        var initial = Board("SFD·027/221", hold: true);
        initial = RadGearTestPosition.Put(initial, "BF", "OGN·286/298", "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "RED", "UNL-029/219", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand()); await Order(game);
        for (var i = 0; game.State.StackItems[^1].HeldContext?.Kind != "ACTIVATE_CONQUEST"; i++) {
            Assert.InRange(i, 0, 5); await game.Top();
        }
        await game.Top();
        Assert.Equal(1, game.State.StackItems.Count(i => i.SourceObjectId == "HOST" && i.TimingAlias is not null)
            + game.State.TriggerQueue.Count(i => i.SourceObjectId == "HOST" && i.TimingAlias is not null));
        await Drain(game);
        Assert.Equal(5, game.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED"); await game.Replay();
    }

    [Fact]
    public async Task AConvertedRuneCallUsesItsSharedHoldInstructionDuringTheMainPhase()
    {
        var initial = RadGearTestPosition.Put(Board("RAD·086/167"), "RUNE", "VEN·R01", "P1", "RUNE_DECK");
        var game = new Driver(initial); await Conquer(game);
        Assert.Equal("UNIT_CHANNEL", Assert.Single(game.State.StackItems).HeldContext!.Kind);
        await game.Top(); Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("RUNE", game.State.PlayerZones["P1"].Base); Assert.False(game.State.CardObjects["RUNE"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ConvertedBattlefieldEquipmentStillRequiresItsOwnDestinationConfirmation()
    {
        var game = new Driver(Board("RAD·094/167")); await Conquer(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OTHER"]));
        await game.Top();
        var bombs = game.State.CardObjects.Values.Where(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo).ToArray();
        Assert.Equal(3, bombs.Length); Assert.All(bombs, c => Assert.Equal("OTHER", game.State.ObjectLocations[c.ObjectId].BattlefieldObjectId));
        await game.Replay();
    }

    [Fact]
    public async Task ConvertedSourceUnitTokenCreationRetainsTheConqueredBattlefieldAndReadyStatus()
    {
        var game = new Driver(Board("UNL-048/219")); await Conquer(game); await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null && c.Tags.Contains(CardObjectTags.UnitCard));
        Assert.False(token.IsExhausted); Assert.Equal(3, token.Power);
        Assert.Equal("OTHER", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId);
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData("SFD·069/221", false)] [InlineData("VEN·112", true)]
    public async Task ConvertedConquestTokensKeepTheirOwnFactoryAndDestinationDuringHolding(string no, bool unit)
    {
        var game = new Driver(Board(no, hold: true)); await game.Act("P2", new EndTurnCommand()); await Order(game);
        Assert.NotNull(Assert.Single(game.State.StackItems).ConquestToken); await game.Top();
        var tokens = game.State.CardObjects.Values.Where(c => c.TokenFactoryCardNo is not null).ToArray(); var token = Assert.Single(tokens);
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Contains(unit ? CardObjectTags.UnitCard : CardObjectTags.EquipmentCard, token.Tags);
        Assert.Equal(unit ? P6TokenFactoryCatalog.ShadowCloneTokenCardNo : "SFD·T03", token.TokenFactoryCardNo);
        await game.Replay();
    }

    [Fact]
    public async Task ConvertedConquestReturnRetainsItsOptionalPaymentAndResponseWindow()
    {
        var initial = RadGearTestPosition.Put(Board("OGN·035/298", hold: true), "RUNE", "VEN·R01", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand()); await Order(game);
        var item = Assert.Single(game.State.StackItems); Assert.NotNull(item.TimingAlias); Assert.NotNull(item.UnitConquestReturn);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.TapRunes("P1", ["RUNE"]);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Battlefields);
        await game.Top(); Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(1, game.State.CardObjects["HOST"].ObjectGeneration); await game.Replay();
    }
    private static MatchState WithCopiedPrintedSkill(MatchState state)
    {
        var host = state.CardObjects["HOST"]; var at = state.ObjectLocations["HOST"];
        state = RadGearTestPosition.Put(state, "SONG", "SFD·059/221", "P1", at.Zone, at.BattlefieldObjectId);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SONG"] = state.CardObjects["SONG"] with { AttachedToObjectId = "HOST", AttachmentRevision = 1,
                AttachedRuleText = new(new("HOST", host.ObjectGeneration), host.CardNo!, 1, 0, host.CopyFaceTransitions?.Count ?? 0) } } };
    }

    [Fact]
    public async Task CopiedConquestReturnsEachRetainTheirOwnOptionalPaymentDuringHolding()
    {
        var initial = RadGearTestPosition.Put(WithCopiedPrintedSkill(Board("OGN·035/298", hold: true)),
            "RUNE", "VEN·R01", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand()); await Order(game);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, i => i.UnitConquestReturn!.Origin is not null);
        Assert.Equal(2, game.State.StackItems.Select(i => i.StackItemId).Distinct().Count());
        var declined = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(declined.PaymentId, declined.PaymentWindow, ["DECLINE"]));
        var accepted = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.TapRunes("P1", ["RUNE"]);
        await game.Act("P1", new PayCostCommand(accepted.PaymentId, accepted.PaymentWindow, ["SPEND_MANA:1"]));
        await game.Top(); Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task CopiedConquestRecastsUseTheSameEffectPlayAndRecycleContinuationDuringHolding()
    {
        var initial = RadGearTestPosition.Put(WithCopiedPrintedSkill(Board("OGN·112/298", hold: true)),
            "SPELL", "OGN·104/298", "P1", "GRAVEYARD");
        initial = initial with { PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P1"] = 2 } };
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand()); await Order(game);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, i => i.RecastContext!.Origin is not null);
        await game.Top(); Assert.Equal(["SPELL"], game.State.PendingEffectPlay!.Sources.Keys);
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·104/298", ["ALLY"])); await game.Top();
        await Drain(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].MainDeck); await game.Replay();
    }

    [Fact]
    public async Task CopiedConquestDamageKeepsItsTargetAndOpenBattlefieldConditionOnARealConquest()
    {
        var initial = WithCopiedPrintedSkill(Board("SFD·116/221"));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 30 } } };
        var game = new Driver(initial); await Conquer(game);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, i => i.UnitConquest!.Origin is not null);
        Assert.DoesNotContain(game.State.StackItems, i => i.TimingAlias is not null);
        var might = ArmamentMightTests.Might(game.State);
        for (var i = 0; i < 2; i++) {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
            await game.Top();
        }
        await Drain(game);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY"))
            .Select(e => Convert.ToInt32(e.Event.Payload["damage"])).ToArray();
        Assert.Equal(new[] { might, might }, damage);
        Assert.Equal(might * 2, game.State.CardObjects["ENEMY"].Damage); await game.Replay();
    }
}
