using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZedShadowCloneTests
{
    [Theory]
    [InlineData("VEN·023", 37710)]
    [InlineData("VEN·023a", 37711)]
    [InlineData("VEN·169", 37872)]
    public async Task AllForbiddenShadowPrintingsPayTheirExactCostAndCreateOnlyAfterThePaidTriggerResolves(string no, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("禁忌之影", card.SubTitle);
        Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.Equal(new[] { RuneTrait.Red }, PrintedPowerCostRules.ForCard(no).Traits);
        var driver = new VenBasicFlowSpellTests.Driver(PlayPosition(no));
        Assert.Contains("DISCARD_HAND_CARD:FODDER", JsonSerializer.Serialize(driver.PromptFor("P1")));
        await driver.Act("P1", new PlayCardCommand("ZED", no, [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
        Assert.Equal(16, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Contains("ZED", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        Assert.True(Assert.Single(driver.State.StackItems).SourceConfirmed);
        await driver.Top();
        var clone = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        Assert.Equal(0, clone.Power); Assert.True(clone.IsExhausted);
        Assert.Equal("VEN·T05", clone.CardNo); Assert.Equal("P1", clone.OwnerId);
        Assert.Contains(CardObjectTags.UnitCard, clone.Tags);
        Assert.Contains(clone.ObjectId, driver.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(CardCombatKeywordNames.Assault, clone.Tags);
    }

    [Theory]
    [InlineData("VEN·023")]
    [InlineData("VEN·023a")]
    [InlineData("VEN·169")]
    public async Task DecliningTheAdditionalCostDoesNotDiscardCreateATokenOrTriggerTheConditionalAbility(string no)
    {
        var driver = new VenBasicFlowSpellTests.Driver(PlayPosition(no));
        await driver.Act("P1", new PlayCardCommand("ZED", no, []));
        Assert.Contains("ZED", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Hand);
        Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DISCARDED");
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
    }

    [Theory]
    [InlineData("source")]
    [InlineData("enemy")]
    [InlineData("field")]
    [InlineData("graveyard")]
    [InlineData("two")]
    [InlineData("duplicate")]
    [InlineData("no-red")]
    [InlineData("no-mana")]
    public async Task InvalidAdditionalCostsOrInsufficientResourcesChangeNothing(string scenario)
    {
        var state = PlayPosition();
        var cost = scenario switch {
            "source" => "ZED", "enemy" => "ENEMY-HAND", "field" => "HOST", "graveyard" => "GRAVE-UNIT", _ => "FODDER"
        };
        var costs = scenario switch {
            "two" => new[] { "DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:OTHER" },
            "duplicate" => new[] { "DISCARD_HAND_CARD:FODDER", "DISCARD_HAND_CARD:FODDER" },
            _ => new[] { "DISCARD_HAND_CARD:" + cost }
        };
        if (scenario is "no-red" or "no-mana")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = scenario == "no-red" ? new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20 }) : RunePool.Empty
            } };
        await Reject(state, "P1", new PlayCardCommand("ZED", "VEN·023", [], OptionalCosts: costs));
    }

    [Fact]
    public async Task ARealResponseCanDestroyZedWithoutCancellingTheAlreadyCapturedTokenCreation()
    {
        var state = AttackDamageDistributionTests.Spell(PlayPosition(), "DESTROY", "SFD·163/221", "P1");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) } };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await driver.Act("P1", new PlayCardCommand("ZED", "VEN·023", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
        await driver.Act("P1", new PlayCardCommand("DESTROY", "SFD·163/221", ["ZED", "HOST"]));
        await driver.Top();
        Assert.Contains("ZED", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Top();
        Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        Assert.Contains("FODDER", driver.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task TheOfficialZeroMightTokenIsNotADeckCardAndHasItsCompleteAttackAbility()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == "VEN·T05");
        Assert.Equal(37925, card.Id); Assert.Equal("单位", card.CardCategoryName);
        Assert.True(OfficialRuleDomainBehaviorCatalog.IsTokenCard(card));
        Assert.Null(card.Energy); Assert.Equal(0, card.Power);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(card.CardNo, out var factory));
        Assert.Equal(0, factory.DefaultPower); Assert.False(factory.RequiresCopySource);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out _));
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitAttackBanishGraveyardUnitAssault);
        Assert.Equal(4, trigger.KeywordBonus); Assert.Equal(TriggerTargetScopes.ControlledUnitInGraveyard, trigger.TargetScope);
    }

    [Theory]
    [InlineData("VEN·T05")]
    [InlineData("VEN·T06")]
    public async Task TokenPrintingsCategorizedAsUnitsCannotBeSubmittedInAMainDeck(string cardNo)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog, OfficialDeckFormat.CoreRules).Single(d => d.Id == "jhin-lowcurve").Decklist;
        Assert.True(OfficialDeckValidator.Validate(deck, catalog).IsValid);
        var validation = OfficialDeckValidator.Validate(deck with { MainDeck = deck.MainDeck.Append(cardNo).ToArray() }, catalog);
        Assert.False(validation.IsValid);
        Assert.Contains($"主牌堆中不能使用指示物：{cardNo}。", validation.Errors);
    }

    [Fact]
    public async Task AttackConfirmsTheFriendlyGraveyardTargetBeforeEitherPlayerCanRespondAndThenGrantsAssault()
    {
        var driver = new VenBasicFlowSpellTests.Driver(AttackPosition());
        await Attack(driver);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "GRAVE-UNIT" }, choice.LegalObjectIds);
        Assert.Contains("GRAVE-UNIT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, ArmamentMightTests.Might(driver.State));
        await Select(driver, "GRAVE-UNIT");
        Assert.NotNull(driver.State.PriorityPlayerId);
        Assert.Contains("GRAVE-UNIT", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Top();
        Assert.Contains("GRAVE-UNIT", driver.State.PlayerZones["P1"].Banished);
        Assert.Equal(1, driver.State.CardObjects["GRAVE-UNIT"].ObjectGeneration);
        Assert.Equal(4, ArmamentMightTests.Might(driver.State));
        Assert.Equal(0, driver.State.CardObjects["HOST"].Power);
        Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("COMBAT_KEYWORD:强攻4:"));
    }

    [Fact]
    public async Task TheOptionalTriggerCanBeDeclinedWithoutExilingOrGrantingAnything()
    {
        var driver = new VenBasicFlowSpellTests.Driver(AttackPosition());
        await Attack(driver); await Select(driver);
        Assert.Empty(driver.State.StackItems);
        Assert.Contains("GRAVE-UNIT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, ArmamentMightTests.Might(driver.State));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_BANISHED");
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData("spell")]
    [InlineData("field")]
    [InlineData("two")]
    [InlineData("duplicate")]
    [InlineData("opponent")]
    public async Task IllegalGraveyardTargetsAndWrongControllerAreRejectedWithoutMutation(string scenario)
    {
        var driver = new VenBasicFlowSpellTests.Driver(AttackPosition());
        await Attack(driver);
        var choice = driver.State.PendingCardChoice!;
        var selected = scenario switch {
            "enemy" => new[] { "ENEMY-GRAVE" }, "spell" => new[] { "GRAVE-SPELL" }, "field" => new[] { "ENEMY1" },
            "two" => new[] { "GRAVE-UNIT", "GRAVE-SPELL" }, "duplicate" => new[] { "GRAVE-UNIT", "GRAVE-UNIT" }, _ => new[] { "GRAVE-UNIT" }
        };
        await Reject(driver.State, scenario == "opponent" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
    }

    [Fact]
    public async Task WithoutAGraveyardUnitTheOptionalTriggerCannotBeConfirmed()
    {
        var state = AttackPosition();
        state = Place(state, "GRAVE-UNIT", "P1", "HAND");
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await Attack(driver);
        Assert.Null(driver.State.PendingCardChoice); Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_BANISHED");
    }

    [Fact]
    public async Task AReturnedGraveyardIncarnationIsNotTheUnitCapturedAsTarget()
    {
        var driver = new VenBasicFlowSpellTests.Driver(AttackPosition());
        await Attack(driver); await Select(driver, "GRAVE-UNIT");
        var selected = driver.State.CardObjects["GRAVE-UNIT"];
        // A public-zone return creates a fresh object even if the physical card
        // is back in the graveyard when the old skill resolves (CN 124).
        var returned = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
            [selected.ObjectId] = selected with { ObjectGeneration = selected.ObjectGeneration + 1 }
        } };
        var result = await AttackDamageDistributionTests.Top(returned);
        Assert.Contains(selected.ObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(selected.ObjectId, result.State.PlayerZones["P1"].Banished);
        Assert.Equal(0, ArmamentMightTests.Might(result.State));
        Assert.DoesNotContain(result.Events, e => e.Kind == "STATUS_EFFECT_APPLIED");
    }

    [Fact]
    public async Task RecoveryRejectsAConditionalPlayAbilityWithoutItsRecordedAdditionalCost()
    {
        var driver = new VenBasicFlowSpellTests.Driver(PlayPosition());
        await driver.Act("P1", new PlayCardCommand("ZED", "VEN·023", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
        var item = Assert.Single(driver.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [item with { OptionalCosts = [] }] }));
    }

    [Fact]
    public async Task ARealResponseCanRemoveTheTokenWhileItsCapturedAbilityStillExilesTheTarget()
    {
        var state = AttackDamageDistributionTests.Spell(AttackPosition(), "DESTROY", "SFD·163/221", "P1");
        state = OfficialHoldSequenceTests.AddUnit(state, "ALLY", "OGN·096/298", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) } };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await Attack(driver); await Select(driver, "GRAVE-UNIT");
        await driver.Act("P1", new PlayCardCommand("DESTROY", "SFD·163/221", ["HOST", "ALLY"]));
        await driver.Top();
        Assert.DoesNotContain("HOST", driver.State.CardObjects.Keys);
        Assert.True(Assert.Single(driver.State.StackItems).FieldContext!.SourceCeasedToExist);
        await driver.Top();
        Assert.Contains("GRAVE-UNIT", driver.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "HOST"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopiedPrintedAttackAbilitiesResolveIndependentlyAndOnlyActualBanishmentsGrantAssault(bool sameTarget)
    {
        var state = AttackPosition();
        if (!sameTarget) state = Put(state, "SECOND-GRAVE", "OGN·096/298", "P1", "GRAVEYARD");
        var driver = new VenBasicFlowSpellTests.Driver(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { CardNo = "SFD·059/221", AttachedToObjectId = "HOST", AttachmentRevision = 1,
                AttachedRuleText = new(new("HOST", state.CardObjects["HOST"].ObjectGeneration), "VEN·T05", 1, state.Tick, 0) }
        } });
        await Attack(driver);
        var ids = driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        Assert.Equal(2, ids.Length);
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await Select(driver, "GRAVE-UNIT");
        await Select(driver, sameTarget ? "GRAVE-UNIT" : "SECOND-GRAVE");
        Assert.Equal(2, driver.State.StackItems.Count);
        Assert.Single(driver.State.StackItems, i => i.FieldContext?.Origin is not null);
        await driver.Top(); await driver.Top();
        var effects = driver.State.CardObjects["HOST"].UntilEndOfTurnEffects.Where(e => e.StartsWith("COMBAT_KEYWORD:强攻4:")).ToArray();
        Assert.Equal(sameTarget ? 1 : 2, effects.Length);
        Assert.Equal(sameTarget ? 4 : 8, ArmamentMightTests.Might(driver.State));
        Assert.Equal(sameTarget ? 1 : 2, driver.Events().Count(e => e.Event.Kind == "CARD_BANISHED"));
    }

    [Fact]
    public async Task ASourceMovedBackToBaseStillGetsTheKeywordButItOnlyAddsMightWhileAttackingAndExpiresAtTurnEnd()
    {
        var state = AttackDamageDistributionTests.Spell(AttackPosition(), "FLASH", "OGS·011/024", "P1");
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await Attack(driver); await Select(driver, "GRAVE-UNIT");
        await driver.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["HOST"])); await driver.Top();
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Base);
        await driver.Top();
        Assert.Contains("GRAVE-UNIT", driver.State.PlayerZones["P1"].Banished);
        Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("COMBAT_KEYWORD:强攻4:"));
        Assert.Equal(0, ArmamentMightTests.Might(driver.State));
        for (var i = 0; i < 12 && !driver.PromptFor("P1").EnabledActions().Contains(CommandTypes.EndTurn); i++)
        {
            if (driver.State.FocusPlayerId is { } focus) await driver.Act(focus, new PassFocusCommand());
            else if (driver.State.PriorityPlayerId is not null) await driver.Top();
            else break;
        }
        await driver.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(driver.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("COMBAT_KEYWORD:强攻4:"));
    }

    [Theory]
    [InlineData("card")]
    [InlineData("generation")]
    [InlineData("factory")]
    [InlineData("location")]
    public async Task RecoveryRejectsForgedCapturedAttackIdentity(string mutation)
    {
        var driver = new VenBasicFlowSpellTests.Driver(AttackPosition());
        await Attack(driver); await Select(driver, "GRAVE-UNIT");
        var item = Assert.Single(driver.State.StackItems); var context = item.FieldContext!;
        context = mutation switch {
            "card" => context with { CardNo = "OGN·001/298" },
            "generation" => context with { SourceGeneration = 999 },
            "factory" => context with { SourceTokenFactoryCardNo = "UNL·T02" },
            _ => context with { SourceLocation = new("P1", "BASE") }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [item with { FieldContext = context }] }));
    }

    private static async Task Select(VenBasicFlowSpellTests.Driver driver, params string[] ids)
    {
        var choice = driver.State.PendingCardChoice!;
        await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    private static async Task Attack(VenBasicFlowSpellTests.Driver driver)
    {
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && driver.State.FocusPlayerId is not null && driver.State.StackItems.Count == 0; i++)
            await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        if (driver.PromptFor("P1").EnabledActions().Contains(CommandTypes.DeclareBattle))
            await driver.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("zed-reject", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState PlayPosition(string no = "VEN·023")
    {
        var state = AttackDamageDistributionTests.Position("OGN·001/298");
        state = Put(state, "ZED", no, "P1", "HAND");
        state = Put(state, "FODDER", "OGN·096/298", "P1", "HAND");
        state = Put(state, "OTHER", "OGN·096/298", "P1", "HAND");
        state = Put(state, "ENEMY-HAND", "OGN·096/298", "P2", "HAND");
        state = Put(state, "GRAVE-UNIT", "OGN·096/298", "P1", "GRAVEYARD");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 20 }), ["P2"] = new(20, 20)
        } };
    }

    private static MatchState AttackPosition()
    {
        var state = PlayPosition();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T05", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = factory.CreateObject("HOST", "P1", "P1")
        } };
        state = Put(state, "GRAVE-SPELL", "VEN·049", "P1", "GRAVEYARD");
        return Put(state, "ENEMY-GRAVE", "OGN·096/298", "P2", "GRAVEYARD");
    }

    private static MatchState Put(MatchState state, string id, string no, string player, string zone)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player)
        } };
        return Place(state, id, player, zone);
    }

    private static MatchState Place(MatchState state, string id, string player, string zone)
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Hand = p.Value.Hand.Where(x => x != id).ToArray(),
            Graveyard = p.Value.Graveyard.Where(x => x != id).ToArray(),
            Base = p.Value.Base.Where(x => x != id).ToArray(),
            Battlefields = p.Value.Battlefields.Where(x => x != id).ToArray()
        });
        zones[player] = zone switch {
            "HAND" => zones[player] with { Hand = zones[player].Hand.Append(id).ToArray() },
            "GRAVEYARD" => zones[player] with { Graveyard = zones[player].Graveyard.Append(id).ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };
        return state with { PlayerZones = zones, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            [id] = new(player, zone)
        } };
    }
}
