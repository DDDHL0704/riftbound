using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven110EmpoweredBanishConsumerTests
{
    private const string No = "VEN·110";
    private const string Text = "{{强化}} — 弃置一张法术牌（支付此费用：强化我。仅在未强化时可用。）\n当我变为{{已强化}}时，放逐战场上一名不高于3{{S}}的敌方单位。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactNewPrintingPreservesEveryPriorFunctionalGroupAndMember()
    {
        var latestCatalog = await OfficialCardCatalog.LoadDefaultAsync();
        var catalog = latestCatalog with { Total = 1088, Cards = latestCatalog.Cards.Where(c => c.CardNo != "VEN·124").ToArray() };
        var controlCards = catalog.Cards.Where(c => c.CardNo is not ("VEN·128" or "VEN·078")).ToArray();
        var priorCards = controlCards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1085, priorCards.Length); Assert.Equal(1086, controlCards.Length);
        var prior = FunctionalUnitBuilder.Build(priorCards); var current = FunctionalUnitBuilder.Build(controlCards);
        Assert.Equal(866, prior.Count); Assert.Equal(867, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
        var cumulativePriorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1087, cumulativePriorCards.Length); Assert.Equal(1088, catalog.Cards.Count);
        var cumulativePrior = FunctionalUnitBuilder.Build(cumulativePriorCards); var cumulative = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, cumulativePrior.Count); Assert.Equal(869, cumulative.Count);
        foreach (var unit in cumulativePrior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        foreach (var unit in current) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        Assert.Equal(1089, latestCatalog.Cards.Count);
        var latestUnits = FunctionalUnitBuilder.Build(latestCatalog.Cards);
        Assert.Equal(870, latestUnits.Count);
        foreach (var unit in FunctionalUnitBuilder.Build(catalog.Cards))
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(unit), System.Text.Json.JsonSerializer.Serialize(Assert.Single(latestUnits, c => c.Id == unit.Id)));
        Assert.Equal("VEN·124", Assert.Single(Assert.Single(latestUnits, c => c.Cards.Any(card => card.CardNo == "VEN·124")).Cards).CardNo);
    }

    [Fact]
    public async Task ExactPrintingKeepsTheSpellDiscardCostSeparateFromItsEmpoweredBanishTrigger()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37807, card.Id); Assert.Equal("梅尔", card.CardName); Assert.Equal("镜转乾坤", card.SubTitle);
        Assert.Equal(Text, card.CardEffect); Assert.Equal(5, card.Energy); Assert.Equal(4, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Purple], card.CardColorList); Assert.Equal(["hero_unit"], card.CardCategoryList);
        // The raw API region says Shurima while the printed face says Noxus.
        // Preserve the exact API source; region-dependent semantics are not certified here.
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal("弃置一张法术牌", cost.AdditionalCost); Assert.False(cost.SupportedResources);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(5, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Theory]
    [InlineData("VEN·049")] [InlineData("OGN·093/298")]
    public async Task ARealSpellIsDiscardedBeforeEmpowerResolvesWithoutPrechoosingTheFutureBanishTarget(string spell)
    {
        var initial = Position(spell); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(15, game.State.RunePools["P1"].Mana);
            await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            var generation = game.State.CardObjects["FODDER"].ObjectGeneration;
            await game.Act("P1", Activate());
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(game.State.PendingCardChoice);
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
            Assert.Equal(new ObjectBinding("FODDER", generation), item.EmpowerActivation!.DiscardedHandCard);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("real-spell-payment-" + spell.Replace('/', '-'), initial, game); }
    }

    [Theory]
    [InlineData("missing")] [InlineData("unit")] [InlineData("equipment")] [InlineData("duplicate")] [InlineData("target")]
    public async Task ActualMissingOrNonspellCostsCannotPartlyDiscardOrActivate(string branch)
    {
        var initial = Add(Add(Position(), "WRONG-UNIT", "OGN·096/298"), "WRONG-GEAR", "SFD·022/221"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            var command = Activate() with { OptionalCosts = branch switch {
                "missing" => [], "unit" => ["DISCARD_HAND_CARD:WRONG-UNIT"], "equipment" => ["DISCARD_HAND_CARD:WRONG-GEAR"],
                "duplicate" => ["DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:FODDER"], _ => ["DISCARD_HAND_CARD:FODDER"] },
                TargetObjectIds = branch == "target" ? ["ENEMY"] : [] };
            var hash = MatchStateHasher.Hash(game.State); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
            var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
            var result = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-ven110", "P1", command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
            Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
            Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Empty(game.State.StackItems); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("invalid-spell-cost-" + branch, initial, game); }
    }

    [Fact]
    public async Task NoEnemyBattlefieldTargetDoesNotPreventPayingTheCostAndBecomingEmpowered()
    {
        var initial = Position(); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate()); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(4, game.State.CardObjects["UNIT"].Power); Assert.Equal(15, game.State.RunePools["P1"].Mana);
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("no-legal-banish-target", initial, game); }
    }

    [Theory]
    [InlineData("SMALL")] [InlineData("LAST-BREATH")]
    public async Task BecomingEmpoweredOffersOnlySmallEnemyBattlefieldUnitsAndBanishesAfterBothPlayersCanRespond(string target)
    {
        var initial = Add(Add(Add(Add(Position(), "SMALL", "OGN·175/298", "P2", "BATTLEFIELD", "OTHER"),
            "LAST-BREATH", "OGN·096/298", "P2", "BATTLEFIELD", "OTHER"), "BASE-SMALL", "OGN·175/298", "P2", "BASE"),
            "FRIENDLY-SMALL", "OGN·175/298", "P1", "BATTLEFIELD", "BF");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate());
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Null(game.State.PendingCardChoice);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(15, game.State.RunePools["P1"].Mana);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal("UNIT", choice.SourceObjectId);
            Assert.Equal(new[] { "LAST-BREATH", "SMALL" }, choice.LegalObjectIds.Order().ToArray());
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Banished); Restore(initial, game);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
            Assert.Single(game.State.StackItems); var firstPriority = game.State.PriorityPlayerId!;
            Assert.Contains("PASS_PRIORITY", ResolutionResult.BuildPrompts(game.State)[firstPriority].Actions);
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Banished); Restore(initial, game);
            await game.Act(firstPriority, new PassPriorityCommand()); Assert.NotEqual(firstPriority, game.State.PriorityPlayerId);
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Banished);
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); Assert.Contains(target, game.State.PlayerZones["P2"].Banished);
            Assert.DoesNotContain(target, game.State.PlayerZones["P2"].Graveyard); Assert.Equal("BANISHED", game.State.ObjectLocations[target].Zone);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("empowered-banish-" + target, initial, game); }
    }

    [Fact]
    public async Task ReturningTheSourceBeforeEmpowerResolvesKeepsTheDiscardPaidWithoutEmpoweringTheNewGeneration()
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate());
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Restore(initial, game); await game.Top();
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("return-before-empower", initial, game); }
    }

    [Fact]
    public async Task AnEarnedBanishOutlivesItsSourceReturningWithoutRefundingTheDiscard()
    {
        var initial = Add(Add(Position(), "RETURN", "OGN·104/298"), "SMALL", "OGN·175/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate()); await game.Top();
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SMALL"]));
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            Assert.Single(game.State.StackItems); Restore(initial, game); await game.Top();
            Assert.Contains("SMALL", game.State.PlayerZones["P2"].Banished); Assert.DoesNotContain("SMALL", game.State.PlayerZones["P2"].Graveyard);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("earned-banish-source-return", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnActuallyCopiedEmpowerOnlyEarnsBanishIfItsTextRemainsWhenTheHostBecomesEmpowered(bool detachBeforeEmpower)
    {
        var initial = Add(Add(Add(ShadySpectaclesTests.Position(No), "FODDER", "SFD·122/221"), "DETACH", "SFD·011/221"),
            "SMALL", "OGN·096/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        try
        {
            var targetView = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"], Json);
            Assert.Equal(2, targetView.GetProperty("players").GetProperty("P2").GetProperty("objects").GetProperty("SMALL").GetProperty("effectivePower").GetInt32());
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var copy = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", copy.Request.Kind);
            await game.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal(No, game.State.CardObjects["HOST"].CardNo); Assert.False(game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
                r => (string?)r["sourceObjectId"] == "HOST" && (string?)r["abilityId"] == "EMPOWER:0:0");
            var instance = Assert.IsType<string>(row["abilityInstanceId"]);
            await game.Act("P1", Activate("HOST") with { AbilityInstanceId = instance });
            Assert.Equal("GEAR", Assert.Single(game.State.StackItems).EmpowerActivation!.Origin!.Equipment.ObjectId);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            if (detachBeforeEmpower)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
                Assert.Null(game.State.CardObjects["HOST"].AttachmentUnitCopies); Assert.NotEqual(No, game.State.CardObjects["HOST"].CardNo);
            }
            Restore(initial, game); await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.False(game.State.CardObjects["COPY-SOURCE"].IsEmpowered); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            if (detachBeforeEmpower)
            { Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.DoesNotContain("SMALL", game.State.PlayerZones["P2"].Banished); }
            else
            {
                var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
                Assert.Equal("HOST", choice.SourceObjectId); Assert.Equal(new[] { "SMALL" }, choice.LegalObjectIds);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SMALL"])); Restore(initial, game);
                await game.Top(); Assert.Contains("SMALL", game.State.PlayerZones["P2"].Banished);
            }
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("real-copy-text-at-empower-" + detachBeforeEmpower, initial, game); }
    }

    [Fact]
    public async Task AnAlreadyEmpoweredSourceRejectsAnotherActualSpellPaymentWithoutDiscardingIt()
    {
        var initial = Add(Position(), "SPARE", "SFD·122/221"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate()); await game.Top();
            var command = Activate() with { OptionalCosts = ["DISCARD_HAND_CARD:SPARE"] };
            var node = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            node["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; node["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P1", "repeat-ven110-payment", JsonSerializer.SerializeToElement(node, Json)), false);
            Assert.Contains("SPARE", game.State.PlayerZones["P1"].Hand); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(game.State.StackItems); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("already-empowered-rejects-second-payment", initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningPaysANaturallyDrawnSpellAndColdRecoversTheIndependentEmpoweredBanish(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "vex-spells", "UNL-R05",
            [No, No, No, "SFD·122/221", "SFD·122/221", "SFD·122/221", "OGN·175/298", "OGN·175/298", "OGN·175/298"]);
        var opponent = actor == "P1" ? "P2" : "P1";
        try
        {
            for (var turn = 0; turn < 80; turn++)
            {
                if (game.State.TurnPlayerId == opponent && game.Runes(actor).Length >= 5 && game.Runes(opponent).Length >= 5
                    && game.Hand(actor, No).Any() && game.Hand(actor, "SFD·122/221").Any() && game.Hand(opponent, "OGN·175/298").Any()) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.Equal(opponent, game.State.TurnPlayerId); var target = game.Hand(opponent, "OGN·175/298").First();
            await game.Tap(opponent); await game.Act(opponent, new PlayCardCommand(target, "OGN·175/298", []));
            await game.Act(opponent, new EndTurnCommand()); await game.Act(actor, new EndTurnCommand());
            var field = Assert.Single(game.State.PlayerZones[opponent].Battlefields, id => game.State.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
            await game.Act(opponent, new MoveUnitCommand(target, "BASE", "BATTLEFIELD:" + field, []));
            for (var action = 0; action < 12 && game.State.TimingState != TimingStates.NeutralOpen; action++)
            {
                if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
                else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
                else Assert.Fail("The actual uncontested move did not expose a normal response step.");
            }
            Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState); Assert.Equal(3, game.Might(opponent, target));
            await game.Act(opponent, new EndTurnCommand()); await game.Tap(actor);
            var unit = game.Hand(actor, No).First(); var spell = game.Hand(actor, "SFD·122/221").First();
            var beforeMana = game.State.RunePools[actor].Mana; await game.Act(actor, new PlayCardCommand(unit, No, []));
            Assert.Equal(beforeMana - 5, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            var before = game.State; await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:" + spell]));
            Assert.Contains(spell, game.State.PlayerZones[actor].Graveyard); Assert.False(game.State.CardObjects[unit].IsEmpowered); await game.ColdCheckpoint();
            var refund = game.State with {
                CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) { [spell] = before.CardObjects[spell] },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(game.State.ObjectLocations) { [spell] = before.ObjectLocations[spell] },
                PlayerZones = new Dictionary<string, PlayerZones>(game.State.PlayerZones) { [actor] = game.State.PlayerZones[actor] with {
                    Hand = before.PlayerZones[actor].Hand, Graveyard = before.PlayerZones[actor].Graveyard } } };
            await game.RejectCoordinatedForgery("refunded-required-hand-spell", refund);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice); Assert.Contains(target, choice.LegalObjectIds);
            await game.Act(actor, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target])); await game.ColdCheckpoint();
            Assert.DoesNotContain(target, game.State.PlayerZones[opponent].Banished); await game.Top();
            Assert.Contains(target, game.State.PlayerZones[opponent].Banished); Assert.DoesNotContain(target, game.State.PlayerZones[opponent].Graveyard);
            Assert.Equal(beforeMana - 5, game.State.RunePools[actor].Mana); Assert.True(game.State.CardObjects[unit].IsEmpowered); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven110-" + actor); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AWildClawReflexiveCanEmpowerMelWithoutPayingHerPrintedHandSpellCost(bool create)
    {
        var initial = Add(Add(VenUnitEntryBasicTests.Position(No), "PARENT", "VEN·089"), "SMALL", "OGN·175/298", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "MAIN_DECK") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray(), MainDeck = ["UNIT", ..initial.PlayerZones["P1"].MainDeck] } } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("PARENT", "VEN·089", [])); await game.Top();
            var selection = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("DECK_PLAY_SELECTION", selection.ChoiceWindow); Assert.Contains("UNIT", selection.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(selection.ChoiceId, selection.ChoiceWindow, ["UNIT"]));
            var decision = game.State.PendingRuleChoice!; Assert.Equal("DECK_REFLEXIVE_CREATE", decision.Request.Kind);
            await game.Act("P1", new PayCostCommand(decision.Request.Id, "RULE_REPLACEMENT", [create ? "CREATE" : "DECLINE"]));
            Assert.Equal(5, game.State.PendingEffectPlay!.ManaReduction); Assert.Empty(game.State.PlayerZones["P1"].Hand);
            var mana = game.State.RunePools["P1"].Mana; await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.PlayerZones["P1"].Hand);
            if (create)
            {
                var reflexive = Assert.Single(game.State.StackItems); Assert.NotNull(reflexive.DeckPlayReflexive);
                Assert.Null(reflexive.EmpowerActivation); Restore(initial, game); await game.Top();
            }
            Assert.Equal(create, game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_DISCARDED" or "HAND_CARDS_DISCARDED");
            if (create)
            {
                var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
                Assert.Equal("UNIT", choice.SourceObjectId); Assert.Equal(new[] { "SMALL" }, choice.LegalObjectIds);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SMALL"])); await game.Top();
                Assert.Contains("SMALL", game.State.PlayerZones["P2"].Banished);
            }
            else { Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.DoesNotContain("SMALL", game.State.PlayerZones["P2"].Banished); }
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("external-wild-claw-empower-" + create, initial, game); }
    }

    [Theory]
    [InlineData("PAY")] [InlineData("DECLINE")] [InlineData("RECOVER")]
    public async Task AnActuallyPlayedWardTargetUsesRecycledPowerWithoutRefundingTheEmpowerSpell(string branch)
    {
        var initial = Add(Position(), "WARDED", "OGN·013/298", "P2") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        for (var i = 0; i < 6; i++) initial = Add(initial, "WARD-RUNE-" + i, "UNL-R03", "P1", "BASE");
        var game = new Driver(initial);
        try
        {
            await game.Act("P2", new PlayCardCommand("WARDED", "OGN·013/298", [], Destination: "BATTLEFIELD:OTHER"));
            Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["WARDED"].Tags);
            Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["WARDED"].Zone);
            await game.Act("P2", new EndTurnCommand());
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.TapRunes("P1", Enumerable.Range(0, 6).Select(i => "WARD-RUNE-" + i));
            Assert.Equal(6, game.State.RunePools["P1"].Mana);
            if (branch != "RECOVER") await game.Act("P1", new RecycleRuneCommand("WARD-RUNE-0"));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal(new[] { "WARDED" }, choice.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["WARDED"]));
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal("TRIGGER_TARGET_COST", payment.PaymentWindow); Assert.Equal(1, payment.PowerCost); Assert.Null(game.State.PriorityPlayerId);
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Restore(initial, game);
            if (branch == "RECOVER")
            {
                Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                var raw = JsonSerializer.SerializeToNode(new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]), Json)!.AsObject();
                raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
                await game.ActRetained(new("P1", "ven110-unfunded-ward", JsonSerializer.SerializeToElement(raw, Json)), false);
                Assert.Equal(payment.PaymentId, game.State.PendingPayment!.PaymentId);
                await game.Act("P1", new RecycleRuneCommand("WARD-RUNE-0"));
                Assert.Equal(1, game.State.RunePools["P1"].TotalPower); Restore(initial, game);
            }
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [branch == "DECLINE" ? "DECLINE" : "PAY"]));
            Assert.Equal(branch == "DECLINE" ? 1 : 0, game.State.RunePools["P1"].TotalPower);
            Assert.Null(game.State.PendingPayment);
            if (branch == "DECLINE")
            { Assert.Empty(game.State.StackItems); Assert.DoesNotContain("WARDED", game.State.PlayerZones["P2"].Banished); }
            else
            { Assert.DoesNotContain("WARDED", game.State.PlayerZones["P2"].Banished); await game.Top(); Assert.Contains("WARDED", game.State.PlayerZones["P2"].Banished); }
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("physical-ward-payment-" + branch, initial, game); }
    }

    private static ActivateAbilityCommand Activate(string source = "UNIT") => new(source, "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]);
    private static MatchState Position(string spell = "VEN·049") => Add(VenUnitEntryBasicTests.Position(No), "FODDER", spell);
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1", string zone = "HAND", string? field = null) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone, field) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone switch {
            "BASE" => state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] },
            "BATTLEFIELD" => state.PlayerZones[owner] with { Battlefields = [..state.PlayerZones[owner].Battlefields, id] },
            _ => state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } } };
    private static void Restore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var restored = MatchSession.Restore(JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN110_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "VEN110 explicit midgame actual play/spell-discard/Empower consumer; source region conflict, L2/L3 and whole-card certification remain OPEN",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
