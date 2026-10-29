using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MordekaiserDiscardDestroyTests
{
    private const string No = "RAD·137/167";
    private const string Kind = "PLAY_OR_ATTACK_OPTIONAL_DISCARD_DESTROY";
    private const string CostWindow = "TRIGGER_HAND_DISCARD_COST";

    private static MatchState Position(string no = No)
        => AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position(no), "COST", "OGN·104/298", "P1");

    private static async Task Select(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    private static async Task<Driver> Play(MatchState state, string no = No)
    {
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        return game;
    }

    private static async Task<Driver> Attack(MatchState state)
    {
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; i < 4 && game.State.PendingCardChoice is null && game.State.FocusPlayerId is { } focus; i++)
            await game.Act(focus, new PassFocusCommand());
        if (game.State.PendingCardChoice is null && ResolutionResult.BuildPrompts(game.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        return game;
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-discard-destroy", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Theory]
    [InlineData("RAD·137/167", 39643)] [InlineData("RAD·137a/167", 39644)]
    public async Task EachOfficialPrintingKeepsItsIdentityCostsAndSharedTrigger(string no, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("冥王进军", card.SubTitle); Assert.Equal("莫德凯撒", card.Hero);
        Assert.Equal("英雄单位", card.CardCategoryName);
        Assert.Equal(9, card.Energy); Assert.Equal(2, card.ReturnEnergy); Assert.Equal(8, card.Power);
        Assert.Equal("当你打出我时，或当我进攻时，你可以选择弃置一张手牌，以此摧毁一个单位或装备。", card.CardEffect);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == TriggerKinds.UnitPlayedOrAttacksOptionalDiscardDestroy);
        Assert.Equal(TriggerTargetScopes.UnitOrEquipmentOnField, spec.TargetScope); Assert.Equal(1, spec.DiscardCount);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(9, behavior.ManaCost); Assert.Equal(8, behavior.SourceUnitPower);
        Assert.Equal(2, PrintedPowerCostRules.ForCard(no).Amount);
        var plays = CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(
            b.CardNo, b.EffectKind, b.DisplayName, CardBehaviorRegistry.TriggerEffectKinds(b))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards, plays);
        var compiled = Assert.Single(BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), implemented), s => s.CardNo == no);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, compiled.Status);
    }

    public static IEnumerable<object[]> Entries() => new[] { No, "RAD·137a/167" }
        .SelectMany(no => new[] { "UNIT", "ALLY", "ENEMY", "GEAR" }.Select(target => new object[] { no, target }));

    [Theory]
    [MemberData(nameof(Entries))]
    public async Task PlayingPaysTheDiscardBeforeResponsesAndDestroysAnyChosenPermanentOnlyOnResolution(string no, string target)
    {
        var game = await Play(Position(no), no);
        Assert.Equal(11, game.State.RunePools["P1"].Mana); Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.PendingCardChoice!.RequiredCount);
        Assert.Equal(new[] { "ALLY", "ENEMY", "GEAR", "UNIT" }, game.State.PendingCardChoice.LegalObjectIds.Order());
        await Select(game, target);
        Assert.Equal(CostWindow, game.State.PendingCardChoice!.ChoiceWindow);
        Assert.Equal(new[] { "COST" }, game.State.PendingCardChoice.LegalObjectIds);
        Assert.Contains("COST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("COST", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"]));
        await Select(game, "COST");
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(Assert.Single(game.State.StackItems).FieldContext!.PaidUnitTrigger!.CostPaid);
        Assert.DoesNotContain(target, game.State.PlayerZones[game.State.CardObjects[target].OwnerId!].Graveyard);
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones[game.State.CardObjects[target].OwnerId!].Graveyard);
        Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DISCARDED");
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·137/167", "HOST")] [InlineData("RAD·137/167", "ENEMY1")] [InlineData("RAD·137/167", "GEAR")]
    [InlineData("RAD·137a/167", "HOST")] [InlineData("RAD·137a/167", "ENEMY1")] [InlineData("RAD·137a/167", "GEAR")]
    public async Task AttackingUsesTheSameOptionalDiscardAndTargetConfirmation(string no, string target)
    {
        var initial = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position(no), "COST", "OGN·104/298", "P1");
        var game = await Attack(initial);
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(Kind, Assert.Single(game.State.StackItems).FieldContext!.Kind);
        await Select(game, target); await Select(game, "COST");
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones[game.State.CardObjects[target].OwnerId!].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("COST")] [InlineData("GRAVE")] [InlineData("BF")] [InlineData("UNKNOWN")]
    [InlineData("duplicate")] [InlineData("extra")] [InlineData("opponent")]
    public async Task IllegalTargetsLeaveTheOriginalChoiceUsable(string branch)
    {
        var game = await Play(Position()); var choice = game.State.PendingCardChoice!;
        await Reject(game.State, branch == "opponent" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            branch == "duplicate" ? ["ENEMY", "ENEMY"] : branch == "extra" ? ["ENEMY", "GEAR"] : [branch == "opponent" ? "ENEMY" : branch]));
        await Select(game, "ENEMY"); await Select(game, "COST"); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
    }

    [Theory]
    [InlineData("ENEMY-COST")] [InlineData("GRAVE")] [InlineData("UNIT")] [InlineData("UNKNOWN")]
    [InlineData("duplicate")] [InlineData("extra")] [InlineData("opponent")]
    public async Task IllegalDiscardCostsCannotRevealOrMoveCards(string branch)
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "ENEMY-COST", "OGN·104/298", "P2");
        state = AttackDamageDistributionTests.Spell(state, "COST2", "OGN·104/298", "P1");
        var game = await Play(state); await Select(game, "ENEMY"); var choice = game.State.PendingCardChoice!;
        await Reject(game.State, branch == "opponent" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            branch == "duplicate" ? ["COST", "COST"] : branch == "extra" ? ["COST", "COST2"] : [branch == "opponent" ? "COST" : branch]));
        await Select(game, "COST"); await game.Top();
        Assert.Contains("COST2", game.State.PlayerZones["P1"].Hand); Assert.Contains("ENEMY-COST", game.State.PlayerZones["P2"].Hand);
    }

    public static IEnumerable<object[]> InvalidPlays() => new[] { No, "RAD·137a/167" }
        .SelectMany(no => new[] { "mana", "power", "wrong-turn", "graveyard", "extra-target", "enemy-field" }
            .Select(branch => new object[] { no, branch }));

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task IllegalHeroPlaysCannotPayEnterOrCreateTheTriggeredSkill(string no, string branch)
    {
        var state = Position(no);
        state = branch switch {
            "mana" => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(8, 20) } },
            "power" => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 1) } },
            "wrong-turn" => state with { TurnPlayerId = "P2", ActivePlayerId = "P2" },
            "graveyard" => state with {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with { Hand = ["COST"], Graveyard = ["GRAVE", "UNIT"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } },
            _ => state };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, branch == "extra-target" ? ["ENEMY"] : [],
            Destination: branch == "enemy-field" ? "BATTLEFIELD:OTHER" : "BASE"));
    }

    [Fact]
    public async Task HiddenOrProtectedPermanentsNeverBecomeTargets()
    {
        var state = LocalDestructionRecallTests.At(Position(), "GEAR", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsFaceDown = true, Tags = [CardObjectTags.EquipmentCard, CardObjectTags.Standby] },
            ["ENEMY"] = state.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["ENEMY_SPELL_SKILL_TARGET_PROTECTION_THIS_TURN"] } } };
        var game = await Play(state); var choice = game.State.PendingCardChoice!;
        Assert.DoesNotContain("GEAR", choice.LegalObjectIds); Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        foreach (var id in new[] { "GEAR", "ENEMY" })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        await Select(game, "ALLY"); await Select(game, "COST"); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData("target")] [InlineData("discard")] [InlineData("empty-hand")]
    public async Task DecliningOrHavingNoHandNeverBlocksOrPaysTheSkill(string branch)
    {
        var game = await Play(branch == "empty-hand" ? VenUnitEntryBasicTests.Position(No) : Position());
        if (branch != "target") await Select(game, "ENEMY");
        if (branch != "empty-hand") await Select(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Equal(branch == "empty-hand" ? 0 : 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DISCARDED"); await game.Replay();
    }

    [Theory]
    [InlineData("PAY", true)] [InlineData("DECLINE", true)] [InlineData("PAY", false)]
    public async Task WardAndDiscardCommitTogetherAndAnUnfundedOrDeclinedPaymentKeepsTheHand(string answer, bool funded)
    {
        var state = Position(); state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(9, funded ? 4 : 2) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = await Play(state); await Select(game, "ENEMY"); await Select(game, "COST");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(0, payment.ManaCost); Assert.Equal(2, payment.PowerCost);
        Assert.Contains("COST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("COST", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await Reject(game.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [answer]));
        if (!funded) await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var pay = funded && answer == "PAY";
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(!pay, game.State.PlayerZones["P1"].Hand.Contains("COST"));
        Assert.Equal(funded && !pay ? 2 : 0, game.State.RunePools["P1"].TotalPower);
        if (pay) await game.Top();
        Assert.Equal(pay, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY")); await game.Replay();
    }

    [Fact]
    public async Task CounteringAnAlreadyPaidSkillKeepsItsDiscardCostPaid()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·045/298", "P2");
        var game = await Play(state); await Select(game, "ENEMY"); await Select(game, "COST");
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [Assert.Single(game.State.StackItems).StackItemId]));
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ADiscardObserverUsesItsOwnResponseWindowAfterTheCostIsCommitted(string owner)
    {
        var state = HandDiscardObserverTests.Put(Position(), "JINX", "OGN·202/298", owner, "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["JINX"] = state.CardObjects["JINX"] with { IsExhausted = true } } };
        var power = state.CardObjects["JINX"].Power;
        var game = await Play(state); await Select(game, "ENEMY"); await Select(game, "COST");
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["JINX"].IsExhausted); Assert.Equal(power, game.State.CardObjects["JINX"].Power);
        if (owner == "P1") {
            Assert.Single(game.State.StackItems, i => i.HandDiscardObserver is not null);
            await game.Top(); Assert.False(game.State.CardObjects["JINX"].IsExhausted);
            Assert.Equal(power + 1, game.State.CardObjects["JINX"].Power);
            Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        } else Assert.DoesNotContain(game.State.StackItems, i => i.HandDiscardObserver is not null);
        await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningSourceOrTargetPreservesTheSkillAndHidesTheNewPrivateObject(bool returnTarget)
    {
        var state = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position(No), "COST", "OGN·104/298", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 3 }, ["ENEMY1"] = state.CardObjects["ENEMY1"] with { Power = 3 } } };
        // The base attack fixture already has P2's response card. Keep its original owner.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RESPONSE"] = OfficialGarenConquestBattlefieldCountTests.Printed("RESPONSE", "OGN·169/298", "P2") } };
        var game = await Attack(state); await Select(game, "ENEMY1"); await Select(game, "COST");
        var sourceGeneration = Assert.Single(game.State.StackItems).FieldContext!.SourceGeneration;
        await game.Act("P1", new PassPriorityCommand());
        var recalled = returnTarget ? "ENEMY1" : "HOST";
        await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", [recalled])); await game.Top();
        Assert.Contains(recalled, game.State.PlayerZones[returnTarget ? "P2" : "P1"].Hand);
        var other = returnTarget ? "P1" : "P2";
        Assert.DoesNotContain("\"" + recalled + "\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)[other].Stack));
        var count = game.Events().Length; await game.Top();
        Assert.Equal(!returnTarget, game.State.PlayerZones["P2"].Graveyard.Contains("ENEMY1"));
        var events = game.Events().Skip(count).Select(e => e.Event).ToArray();
        Assert.All(events, e => Assert.NotEqual(recalled, e.Payload.GetValueOrDefault("sourceObjectId")));
        if (!returnTarget) {
            var death = Assert.Single(events, e => e.Kind == "UNIT_DESTROYED");
            Assert.Equal("HIDDEN", death.Payload["sourceObjectId"]);
            var attribution = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
            Assert.Equal(new ObjectBinding("HOST", sourceGeneration), attribution.Source);
            Assert.True(attribution.Source.Generation < game.State.CardObjects["HOST"].ObjectGeneration);
        }
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData("paid-early")] [InlineData("missing-receipt")] [InlineData("wrong-card")]
    [InlineData("wrong-generation")] [InlineData("future-tick")] [InlineData("forged-choice")]
    public async Task RecoveryRejectsForgedCostReceiptsAndPrivateChoices(string change)
    {
        var game = await Play(Position()); await Select(game, "ENEMY");
        if (change is not ("paid-early" or "forged-choice")) await Select(game, "COST");
        var state = game.State; var item = Assert.Single(state.StackItems); var field = item.FieldContext!;
        var origin = field.PaidUnitTrigger!;
        if (change == "forged-choice") state = state with { PendingCardChoice = state.PendingCardChoice! with { LegalObjectIds = ["GRAVE"] } };
        else {
            origin = change switch {
                "paid-early" => origin with { CostPaid = true },
                "missing-receipt" => origin with { DiscardCost = null },
                "wrong-card" => origin with { DiscardCost = origin.DiscardCost! with { Card = new("ENEMY", 0) } },
                "wrong-generation" => origin with { DiscardCost = origin.DiscardCost! with { Card = origin.DiscardCost!.Card with { Generation = -1 } } },
                _ => origin with { DiscardCost = origin.DiscardCost! with { SelectedTick = state.Tick + 1 } } };
            state = state with { StackItems = [item with { FieldContext = field with { PaidUnitTrigger = origin } }] };
        }
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public async Task PrivateDiscardReceiptCannotBeInjectedIntoAnyPublicStackView()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var game = await Play(state); await Select(game, "ENEMY"); await Select(game, "COST");
        state = game.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, state.Tick, 0, p.Value, state.Tick, 0, prompts[p.Key]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var serialized = JsonSerializer.Serialize(snapshot.Stack);
            Assert.DoesNotContain("DiscardCost", serialized); Assert.DoesNotContain("COST", serialized);
            var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            item["paidUnitTrigger"] = Assert.Single(state.StackItems).FieldContext!.PaidUnitTrigger;
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            item.Remove("paidUnitTrigger");
        }
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
    }

    [Fact]
    public async Task CopiedTextCreatesIndependentCostsAndTargets()
    {
        var state = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position(No, copied: true), "COST", "OGN·104/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "COST2", "OGN·104/298", "P1");
        var game = await Attack(state); Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Select(game, "ENEMY1"); await Select(game, "COST");
        await Select(game, "ENEMY2"); await Select(game, "COST2");
        Assert.Equal(2, game.State.StackItems.Count); Assert.Single(game.State.StackItems, i => i.FieldContext!.Origin is not null);
        await game.Top(); await game.Top();
        Assert.All(new[] { "ENEMY1", "ENEMY2" }, id => Assert.Contains(id, game.State.PlayerZones["P2"].Graveyard));
        Assert.All(new[] { "COST", "COST2" }, id => Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·143/167", No)] [InlineData("RAD·143/167", "RAD·137a/167")]
    [InlineData("RAD·170*/167", No)] [InlineData("RAD·170*/167", "RAD·137a/167")]
    [InlineData("RAD·170/167", No)] [InlineData("RAD·170/167", "RAD·137a/167")]
    [InlineData("RAD·143/167", "RAD·020/167")] [InlineData("RAD·143/167", "RAD·020a/167")]
    [InlineData("RAD·170*/167", "RAD·020/167")] [InlineData("RAD·170*/167", "RAD·020a/167")]
    [InlineData("RAD·170/167", "RAD·020/167")] [InlineData("RAD·170/167", "RAD·020a/167")]
    public async Task TheRealMordekaiserFamilyCanSubmitItsChampionAndSignatureSpell(string legendNo, string heroNo)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        var legend = Assert.Single(allowed.Cards, c => c.CardNo == legendNo);
        Assert.Equal("莫德凯撒", legend.Hero);
        var runes = legend.CardColorList.SelectMany(color => Enumerable.Repeat(allowed.Cards
            .Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color)).OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo, 6)).ToArray();
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            legendNo, heroNo, new[] { "RAD·144/167" }, new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, runes])!;
        var valid = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("rad-mordekaiser-family-" + legendNo + heroNo,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, format));
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var submitted = await session.SubmitDeckAsync("P1", "mordekaiser-family", command,
            JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        Assert.Equal(heroNo, submitted.State.PlayerDecklists["P1"].ChampionCardNo);
        Assert.Contains("RAD·144/167", submitted.State.PlayerDecklists["P1"].MainDeck);
    }
}
