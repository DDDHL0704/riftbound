using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadNeekoLegendTests
{
    private const string Neeko = "RAD·167/167";
    private const string Viktor = "OGN·265/298";
    private const string Darius = "OGN·253/298";

    [Fact]
    public async Task RegisteredLegendsRemainPrivateUntilTheResolutionDecisionAndEnterAsRealActiveCards()
    {
        var game = await Position(Viktor, Darius);
        await Play(game, "NEEKO");
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.Null(game.State.PendingRuleChoice);
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("REGISTERED_LEGEND_SELECTION", choice.Kind);
        Assert.Equal(new[] { Viktor, Darius }, choice.Options.Select(option => option.Id));
        Assert.DoesNotContain(game.PromptFor("P2").Candidates!, candidate => candidate.Action == CommandTypes.PayCost && candidate.Enabled);
        var views = ResolutionResult.BuildSnapshots(game.State);
        foreach (var no in new[] { Viktor, Darius })
        {
            Assert.Contains(no, Strings(JsonSerializer.SerializeToElement(game.PromptFor("P1"))));
            Assert.DoesNotContain(no, Strings(JsonSerializer.SerializeToElement(views["P2"])));
            Assert.DoesNotContain(no, Strings(JsonSerializer.SerializeToElement(game.PromptFor("P2"))));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick,
                game.Events().Last().Sequence, [], game.State);
            Assert.DoesNotContain(no, Strings(JsonSerializer.SerializeToElement(spectator)));
        }
        var leaked = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick,
            game.Events().Last().Sequence, [], game.State);
        var leakedPayment = Assert.IsType<Dictionary<string, object?>>(leaked.SpectatorSnapshot.Timing["pendingPayment"]);
        leakedPayment["paymentChoices"] = new[] { Viktor };
        Assert.NotEmpty(HistoryErrors(game, game.State, leaked));
        var sequence = game.Events().Last().Sequence;
        var prompts = ResolutionResult.BuildPrompts(game.State);
        var recoveredViews = game.State.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            game.State.Tick, sequence, views[player], game.State.Tick, sequence, prompts[player]));
        var cleanSpectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
        Assert.Empty(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, game.Commands(), game.Events(),
            recoveredViews, game.State, game.State.Tick, cleanSpectator));
        var opponentPayment = Assert.IsType<Dictionary<string, object?>>(views["P2"].Timing["pendingPayment"]);
        foreach (var key in new[] { "paymentChoices", "paymentResourceActions" })
        {
            var original = opponentPayment[key]; opponentPayment[key] = new[] { Viktor };
            Assert.Contains(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, game.Commands(), game.Events(),
                recoveredViews, game.State, game.State.Tick, cleanSpectator),
                error => error.Contains("private resolution choice exposes", StringComparison.Ordinal));
            opponentPayment[key] = original;
        }
        await game.ColdCheckpoint();
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Viktor]));
        var id = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var legend = game.State.CardObjects[id];
        Assert.Equal(Viktor, legend.CardNo);
        Assert.False(legend.IsExhausted); Assert.Null(legend.TokenFactoryCardNo);
        Assert.Contains("CARD_TYPE:LEGEND", legend.Tags);
        Assert.Equal("LEGEND", game.State.ObjectLocations[id].Zone);
        Assert.All(ResolutionResult.BuildSnapshots(game.State).Values,
            snapshot => Assert.Contains(Viktor, Strings(JsonSerializer.SerializeToElement(snapshot))));
        Assert.DoesNotContain(game.Events(), ev => ev.Event.Kind == "TARGETS_CONFIRMED");
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ZeroOrOneAvailableLegendNeedsNoAdditionalDecision(int count)
    {
        var game = await Position(count == 0 ? [] : [Viktor]);
        await Play(game, "NEEKO"); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(count, game.State.PlayerZones["P1"].LegendZone.Count);
        await game.Replay();
    }

    [Fact]
    public async Task MultipleNeekosShareThePoolAndCannotCreateTheSameRegisteredLegendTwice()
    {
        var game = await Position(Viktor, Darius);
        await Play(game, "NEEKO"); await game.Top();
        var choice = game.State.PendingRuleChoice!.Request;
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Darius]));
        await Play(game, "SECOND"); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(2, game.State.PlayerZones["P1"].LegendZone.Count);
        Assert.Equal(new[] { Darius, Viktor }.Order(), game.State.PlayerZones["P1"].LegendZone
            .Select(id => game.State.CardObjects[id].CardNo).Order());
        await Play(game, "THIRD"); await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].LegendZone.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceBeforeItsPlaySkillResolvesDoesNotCreateALegend()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"]));
        await game.Top(); await game.Top();
        Assert.Contains("NEEKO", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.Null(game.State.BoundLegends);
        await game.Replay();
    }

    [Fact]
    public async Task SourceDepartureCompletelyRemovesTheLegendAndReentryCreatesANewObject()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO"); await game.Top();
        var original = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var generation = game.State.CardObjects["NEEKO"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.False(game.State.CardObjects.ContainsKey(original));
        Assert.False(game.State.ObjectLocations.ContainsKey(original));
        Assert.DoesNotContain(original, JsonSerializer.Serialize(game.State.PlayerZones), StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(game.State.BoundLegends!).DepartedAtTick);
        await game.ColdCheckpoint();
        await Play(game, "NEEKO"); await game.Top();
        var current = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        Assert.NotEqual(original, current);
        Assert.True(game.State.CardObjects["NEEKO"].ObjectGeneration > generation);
        Assert.Equal(Viktor, game.State.CardObjects[current].CardNo);
        Assert.Equal(2, game.State.BoundLegends!.Count);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ASecondNeekoMaySelectTheSameRegisteredLegendAfterTheFirstNeekoIsDestroyed()
    {
        var initial = AttackDamageDistributionTests.Spell((await Position(Viktor, Darius)).State,
            "KILL", "OGN·229/298", "P1");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        var firstChoice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        await game.Act("P1", new PayCostCommand(firstChoice.Id, "RULE_REPLACEMENT", [Viktor]));
        var original = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var originalBinding = Assert.Single(game.State.BoundLegends!);
        Assert.Equal(Viktor, game.State.CardObjects[original].CardNo);
        await game.ColdCheckpoint();
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["NEEKO"]));
        await game.Top();
        Assert.Contains("NEEKO", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        Assert.False(game.State.CardObjects.ContainsKey(original));
        Assert.False(game.State.ObjectLocations.ContainsKey(original));
        Assert.NotNull(Assert.Single(game.State.BoundLegends!).DepartedAtTick);
        await game.ColdCheckpoint();
        await Play(game, "SECOND"); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("REGISTERED_LEGEND_SELECTION", choice.Kind);
        Assert.NotEqual(firstChoice.Id, choice.Id);
        Assert.Equal(new[] { Viktor, Darius }, choice.Options.Select(option => option.Id));
        var selection = new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Viktor]);
        var hash = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("neeko-faq-wrong-player", "P2", CommandTypes.PayCost), selection, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        await game.ColdCheckpoint();
        await game.Act("P1", selection);
        var current = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        Assert.NotEqual(original, current);
        Assert.Equal(Viktor, game.State.CardObjects[current].CardNo);
        Assert.False(game.State.CardObjects[current].IsExhausted);
        Assert.Equal(2, game.State.BoundLegends!.Count);
        Assert.Equal(originalBinding.Source, Assert.Single(game.State.BoundLegends!, b => b.LegendObjectId == original).Source);
        var currentBinding = Assert.Single(game.State.BoundLegends!, b => b.LegendObjectId == current);
        Assert.Equal(new ObjectBinding("SECOND", game.State.CardObjects["SECOND"].ObjectGeneration), currentBinding.Source);
        Assert.Null(currentBinding.DepartedAtTick);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ARealLegendAbilityAlreadyOnTheChainResolvesAfterItsLegendCeasesToExist()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new LegendActCommand(legend, LegendActionAbilityCatalog.ViktorLegendAbilityId, [], ["SPEND_MANA:1"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(legend));
        Assert.Equal(legend, Assert.Single(game.State.StackItems).SourceObjectId);
        var item = game.State.StackItems.Single();
        Assert.Contains(HistoryErrors(game, game.State with { StackItems = [item with { StackItemId = item.StackItemId + "-invented" }] }),
            error => error.Contains("accepted activation receipt", StringComparison.Ordinal));
        await game.ColdCheckpoint(); await game.Top();
        Assert.Single(game.Events(), ev => ev.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task APassiveLegendTriggerSurvivesItsDepartureAndCannotBorrowAnInventedReceipt()
    {
        var position = (await Position("UNL-183/219")).State;
        var game = new Driver(position with {
            CardObjects = new Dictionary<string, CardObjectState>(position.CardObjects) {
                ["RECRUIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("RECRUIT", "OGN·012/298", "P1") },
            PlayerZones = position.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                ? pair.Value with { Hand = [..pair.Value.Hand, "RECRUIT"] } : pair.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(position.ObjectLocations) {
                ["RECRUIT"] = new("P1", "HAND") }
        }) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new PlayCardCommand("RECRUIT", "OGN·012/298", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(legend, choice.SourceObjectId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        var power = game.State.CardObjects["UNIT"].Power;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(legend));
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(legend, item.SourceObjectId);
        foreach (var context in new[] {
            item.UnitPlayMight! with { SourceGeneration = 1 },
            item.UnitPlayMight! with { CapturedTick = game.State.Tick + 1 } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { UnitPlayMight = context }] }));
        Assert.Contains(HistoryErrors(game, game.State with {
            StackItems = [item with { StackItemId = item.StackItemId + "-invented" }] }),
            error => error.Contains("accepted activation receipt", StringComparison.Ordinal));
        await game.ColdCheckpoint(); await game.Top();
        Assert.Equal(power + 1, game.State.CardObjects["UNIT"].Power);
        var unit = game.State.CardObjects["UNIT"];
        var modifier = Assert.Single(unit.UntilEndOfTurnPowerModifiers);
        foreach (var forged in new[] { modifier with { SourceObjectId = legend + "-invented" },
            modifier with { SourceCardNo = Darius }, modifier with { PowerDelta = 2 } })
            Assert.NotEmpty(HistoryErrors(game, game.State with {
                CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                    ["UNIT"] = unit with { UntilEndOfTurnPowerModifiers = [forged] } } }));
        foreach (var (key, value) in new[] {
            ("sourceObjectId", legend + "-invented"), ("sourceCardNo", Darius),
            ("sourcePath", "invented"), ("targetObjectId", legend) })
        {
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick,
                game.Events().Last().Sequence, [], game.State);
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(
                spectator.SpectatorSnapshot.Timing["continuousEffects"]);
            var effect = Assert.Single(effects, effect => Equals(effect.GetValueOrDefault("sourceObjectId"), legend));
            effect[key] = value;
            Assert.NotEmpty(HistoryErrors(game, game.State, spectator));
        }
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnAlreadyGrantedTurnEffectRemainsAfterTheAddedLegendDeparts(bool departBeforeResolution)
    {
        var game = await Position("OGN·267/298");
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new LegendActCommand(legend,
            LegendActionAbilityCatalog.MissFortuneLegendAbilityId, ["UNIT"], []));
        Assert.DoesNotContain("ROAM", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        if (!departBeforeResolution) await game.Top();
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(legend));
        if (departBeforeResolution) await game.Top();
        Assert.Contains("ROAM", game.State.CardObjects["UNIT"].UntilEndOfTurnEffects);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task ForgedChoicesAreAtomicAndForgedDurationBindingsFailRecovery()
    {
        var game = await Position(Viktor, Darius);
        await Play(game, "NEEKO"); await game.Top();
        var choice = game.State.PendingRuleChoice!.Request;
        var original = MatchStateHasher.Hash(game.State);
        foreach (var (player, selected) in new[] { ("P2", Viktor), ("P1", "UNL-181/219"), ("P1", Neeko) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
                new PlayerIntent("forged-neeko", player, CommandTypes.PayCost),
                new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [selected]), default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(original, MatchStateHasher.Hash(rejected.State));
        }
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", [Viktor]));
        var binding = Assert.Single(game.State.BoundLegends!);
        Assert.Contains(HistoryErrors(game, game.State with { BoundLegends = null }),
            error => error.Contains("provenance differs from accepted history", StringComparison.Ordinal));
        foreach (var forged in new[] { binding with { PlayerId = null! },
            binding with { Source = null! }, binding with { LegendObjectId = null! },
            binding with { PlayerId = "P2" },
            binding with { Source = binding.Source with { Generation = binding.Source.Generation + 1 } },
            binding with { LegendCardNo = Darius }, binding with { DepartedAtTick = game.State.Tick } })
        {
            var invalid = game.State with { BoundLegends = [forged] };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(invalid));
            Assert.NotEmpty(HistoryErrors(game, invalid));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnOpponentOwnedNeekoUsesOnlyTheAbilityControllersRegisteredPool(bool hasOwnPool)
    {
        var position = (await Position(Darius)).State;
        var deck = position.PlayerDecklists["P1"];
        var game = new Driver(position with {
            CardObjects = position.CardObjects.ToDictionary(pair => pair.Key,
                pair => pair.Key == "NEEKO" ? pair.Value with { OwnerId = "P2" } : pair.Value),
            PlayerDecklists = new Dictionary<string, OfficialDecklist> {
                ["P1"] = deck with { AdditionalLegendCardNos = hasOwnPool ? [Darius] : null },
                ["P2"] = deck with { AdditionalLegendCardNos = [Viktor] } }
        }) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        Assert.Empty(game.State.PlayerZones["P2"].LegendZone);
        Assert.DoesNotContain(game.State.CardObjects.Values, card => card.CardNo == Viktor);
        Assert.Equal(hasOwnPool ? 1 : 0, game.State.PlayerZones["P1"].LegendZone.Count);
        if (hasOwnPool)
            Assert.Equal(Darius, game.State.CardObjects[Assert.Single(game.State.PlayerZones["P1"].LegendZone)].CardNo);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualControlChangePreservesTheOriginalSkillControllerAndItsRegisteredLegend(bool beforeResolution)
    {
        // Explicit midgame position; standby preparation, replaying Neeko onto
        // the battlefield, and the control change all use ordinary commands.
        var deck = (await Position(Viktor)).State.PlayerDecklists["P1"];
        var initial = BanishEffectPlayTests.Position(unit: Neeko);
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY", "OGN·049/298", "F", "P2");
        initial = initial with {
            ActivePlayerId = "P2", TurnPlayerId = "P2",
            PlayerDecklists = new Dictionary<string, OfficialDecklist> {
                ["P1"] = deck, ["P2"] = deck with { AdditionalLegendCardNos = [Darius] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" },
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·049/298", "P2"),
                ["BAIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("BAIT", "OGN·012/298", "P1") },
            PlayerZones = initial.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                ? pair.Value with { Base = [..pair.Value.Base, "BAIT"] } : pair.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["BAIT"] = new("P1", "BASE") }
        };
        initial = AttackDamageDistributionTests.Spell(initial, "TAKE", "SFD·202/221", "P2");
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·104/298", "P1");
        var runes = Enumerable.Range(0, 4).Select(copy => "FUND-" + copy).ToArray();
        foreach (var id in runes)
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·007/298", "P1") },
                PlayerZones = initial.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                    ? pair.Value with { Base = [..pair.Value.Base, id] } : pair.Value),
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    [id] = new("P1", "BASE") }
            };
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P2", new HideCardCommand("TAKE", "SFD·202/221", "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P2", new EndTurnCommand());
        await game.TapRunes("P1", runes);
        await game.Act("P1", new RecycleRuneCommand(runes[0]));
        await game.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("HOST", Neeko, [], Destination: "BATTLEFIELD:F"));
        var playSkill = Assert.Single(game.State.StackItems);
        var playReceipt = Assert.Single(game.State.CardPlays.Resolved, play => play.PlayId == playSkill.StackItemId);
        Assert.Equal(Neeko, playReceipt.CardNo);
        Assert.Equal("P1", playReceipt.PlayerId);
        Assert.Equal("HOST", playReceipt.Source.ObjectId);
        var source = playReceipt.Source;
        string? originalLegend = null;
        if (!beforeResolution)
        {
            await game.Top();
            originalLegend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BAIT"]));
        }
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("TAKE", "SFD·202/221", ["HOST"])); await game.Top();
        Assert.Equal("P1", game.State.CardObjects["HOST"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(source.Generation, game.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        if (beforeResolution)
        {
            Assert.Equal(playSkill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
            Assert.Equal(playReceipt, Assert.Single(game.State.CardPlays.Resolved, play => play.PlayId == playSkill.StackItemId));
        }
        await game.ColdCheckpoint(); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        if (originalLegend is not null) Assert.Equal(originalLegend, legend);
        Assert.Equal(Viktor, game.State.CardObjects[legend].CardNo);
        Assert.Equal("P1", game.State.CardObjects[legend].ControllerId);
        Assert.Empty(game.State.PlayerZones["P2"].LegendZone);
        Assert.DoesNotContain(game.State.CardObjects.Values, card => card.CardNo == Darius);
        var duration = Assert.Single(game.State.BoundLegends!);
        Assert.Equal(source, duration.Source); Assert.Equal("P1", duration.PlayerId);
        Assert.Null(duration.DepartedAtTick);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Fact]
    public async Task MovingTheSameFieldObjectPreservesItsAddedLegend()
    {
        var game = await Position(Viktor);
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        var generation = game.State.CardObjects["NEEKO"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["NEEKO"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("NEEKO", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(generation, game.State.CardObjects["NEEKO"].ObjectGeneration);
        Assert.Contains(legend, game.State.PlayerZones["P1"].LegendZone);
        Assert.Null(Assert.Single(game.State.BoundLegends!).DepartedAtTick);
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReturningTheLegendSourceEndsItsStaticGrantBeforeTheSameInstructionCreatesAUnit(bool returnsNeeko)
    {
        var position = (await Position("SFD·197/221")).State;
        var game = new Driver(position with {
            CardObjects = new Dictionary<string, CardObjectState>(position.CardObjects) {
                ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "SFD·207/221", "P2") }
        }) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["NEEKO"])); await game.Top();
        var conqueror = returnsNeeko ? "NEEKO" : "UNIT";
        await game.Act("P1", new MoveUnitCommand(conqueror, "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Contains(legend, game.State.PlayerZones["P1"].LegendZone);
        await game.ColdCheckpoint();
        var eventStart = game.Events().Length;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        var events = game.Events().Skip(eventStart).Select(recovered => recovered.Event).ToArray();
        var created = Assert.Single(events, ev => ev.Kind == "UNIT_TOKEN_CREATED");
        var token = game.State.CardObjects[Assert.IsType<string>(created.Payload["tokenObjectId"])];
        Assert.Contains(conqueror, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(!returnsNeeko, token.Tags.Contains(CardEquipmentKeywordNames.Tempered));
        Assert.Equal(!returnsNeeko, Assert.IsType<bool>(created.Payload["azirTempered"]));
        Assert.Equal(!returnsNeeko, events.Any(ev => ev.Kind == "TRIGGER_QUEUED"
            && Equals(ev.Payload.GetValueOrDefault("effectKind"), "PLAYED_UNIT_WEAPONMASTER")));
        Assert.Equal(!returnsNeeko, game.State.CardObjects.ContainsKey(legend));
        var binding = Assert.Single(game.State.BoundLegends!);
        Assert.Equal(returnsNeeko, binding.DepartedAtTick is not null);
        if (returnsNeeko)
        {
            Assert.False(game.State.ObjectLocations.ContainsKey(legend));
            Assert.Empty(game.State.PlayerZones["P1"].LegendZone);
            Assert.Equal(game.State.Tick, binding.DepartedAtTick);
        }
        await game.ColdCheckpoint(); await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingSandSoldiersKeepTheStaticGrantOnlyWhileTheAddedLegendRemains(bool returnsNeeko)
    {
        var position = (await Position("SFD·197/221")).State;
        position = AttackDamageDistributionTests.Spell(position, "SAND", "SFD·031/221", "P1");
        var game = new Driver(position) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new PlayCardCommand("SAND", "SFD·031/221", [])); await game.Top();
        var soldier = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo);
        Assert.Contains(CardEquipmentKeywordNames.Tempered, soldier.Tags);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();

        var returned = returnsNeeko ? "NEEKO" : "UNIT";
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top();
        var remainingSoldier = game.State.CardObjects[soldier.ObjectId];
        Assert.Equal(soldier.ObjectGeneration, remainingSoldier.ObjectGeneration);
        Assert.Equal(soldier.TokenFactoryCardNo, remainingSoldier.TokenFactoryCardNo);
        Assert.Equal(!returnsNeeko, remainingSoldier.Tags.Contains(CardEquipmentKeywordNames.Tempered));
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(!returnsNeeko, game.State.CardObjects.ContainsKey(legend));
        Assert.Equal(returnsNeeko, Assert.Single(game.State.BoundLegends!).DepartedAtTick is not null);
        await game.ColdCheckpoint(); await game.Replay();
    }

    private static IReadOnlyList<string> HistoryErrors(Driver game, MatchState state, MatchReplayFrame? spectator = null)
    {
        var sequence = game.Events().Last().Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        spectator ??= MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
    }

    private static IEnumerable<string> Strings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) yield return element.GetString()!;
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                foreach (var value in Strings(child)) yield return value;
        else if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                foreach (var value in Strings(property.Value)) yield return value;
    }

    [Fact]
    public async Task ATargetSelectionTriggerAlreadyPaidSurvivesItsActualNeekoBoundLegendLeaving()
    {
        var initial = (await Position("SFD·195/221")).State;
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = true } } };
        initial = AttackDamageDistributionTests.Spell(initial, "BOOST", "SFD·034/221", "P1");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await Play(game, "NEEKO"); await game.Top();
        var legend = Assert.Single(game.State.PlayerZones["P1"].LegendZone);
        await game.Act("P1", new PlayCardCommand("BOOST", "SFD·034/221", ["UNIT"]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.True(game.State.CardObjects[legend].IsExhausted);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEEKO"])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(legend));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.ColdCheckpoint(); await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Top(); await game.ColdCheckpoint(); await game.Replay();
    }

    private static Task Play(Driver game, string id)
        => game.Act("P1", new PlayCardCommand(id, Neeko, [], Destination: "BASE"));

    private static async Task<Driver> Position(params string[] pool)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(item => item.Id == "jhin-lowcurve").Decklist;
        var main = deck.MainDeck.ToArray();
        main[Array.FindLastIndex(main, no => no != deck.ChampionCardNo)] = Neeko;
        deck = deck with { MainDeck = main, AdditionalLegendCardNos = pool.Length == 0 ? null : pool };
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298", 0);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in new[] { "NEEKO", "SECOND", "THIRD" }) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, Neeko, "P1");
            locations[id] = new("P1", "HAND");
        }
        state = state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                ? pair.Value with { Hand = ["NEEKO", "SECOND", "THIRD"] } : pair.Value),
            PlayerDecklists = new Dictionary<string, OfficialDecklist> { ["P1"] = deck },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(50, 50), ["P2"] = new(50, 50) } };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
        return new(state) { RecoverAfterEveryCommand = false };
    }
}
