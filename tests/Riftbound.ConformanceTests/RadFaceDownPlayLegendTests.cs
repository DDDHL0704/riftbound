using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadFaceDownPlayLegendTests
{
    private const string Normal = "RAD·153/167";
    private const string Effect = TriggerKinds.ControllerFaceDownPlayDrawOutsideTurnDiscard;
    public static IEnumerable<object[]> Prints() => new object[][] {
        [Normal, 39661L], ["RAD·175*/167", 39690L], ["RAD·175/167", 39691L]
    };
    public static IEnumerable<object[]> CardNumbers() => Prints().Select(row => new[] { row[0] });

    private static MatchState Board(string no)
    {
        var state = AttackDamageDistributionTests.Spell(StandbySourceRulesTests.Position(), "KEEP", "OGN·048/298", "P1");
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", no, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") }
        };
    }

    private static async Task<Driver> Ready(string no, bool ownTurn = false, bool second = false)
    {
        var state = Board(no);
        if (second)
        {
            state = AttackDamageDistributionTests.Spell(state, "SECOND", "OGN·057/298", "P1");
            state = (await OfficialGraveyardRecastTests.Act(state, "P1", new HideCardCommand(
                "SECOND", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]))).State;
        }
        state = await StandbySourceRulesTests.ReadyShadow(initial: state);
        if (ownTurn)
        {
            state = (await OfficialGraveyardRecastTests.Top(state)).State;
            state = await StandbySourceRulesTests.NextTurn(state);
        }
        return new(state);
    }

    private static async Task Play(Driver game, string source = "SHADOW")
    {
        if (game.State.PriorityPlayerId is { } priority && priority != "P1")
            await game.Act(priority, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand(source, "OGN·057/298", ["HOST"]));
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task Discard(Driver game, string? chosen = null)
    {
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            [chosen ?? choice.LegalObjectIds[0]]));
    }

    [Theory, MemberData(nameof(Prints))]
    public async Task EveryOfficialPrintingHasTheFullOncePerTurnDrawAndConditionalDiscardText(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("痛苦之拥", card.CardName); Assert.Equal("传奇", card.CardCategoryName);
        Assert.Equal(new[] { RuneTrait.Orange, RuneTrait.Purple }, card.CardColorList);
        var parsed = Assert.IsType<StandbyPlayedDrawEffect>(StandbyParser.DrawWhenControllerPlaysFromFaceDown(card.CardEffect));
        Assert.Equal(1, parsed.DrawCount); Assert.Equal(1, parsed.OutsideTurnDiscardCount); Assert.True(parsed.OncePerTurn);
        var trigger = Assert.Single(RuleTextParser.Parse(card).Triggers, t => t.Kind == Effect);
        Assert.True(trigger.OncePerTurn); Assert.Equal(1, trigger.DrawCount); Assert.Equal(1, trigger.DiscardCount);
        Assert.True(OfficialRuleDomainBehaviorCatalog.IsImplementedLegendActionCard(card));
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task AnOwnTurnFaceDownPlayDrawsAfterResponsesAndDoesNotDiscard(string no)
    {
        var game = await Ready(no, ownTurn: true); var hand = game.State.PlayerZones["P1"].Hand.Count;
        var top = game.State.PlayerZones["P1"].MainDeck[0]; await Play(game);
        var skill = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Empty(skill.TargetObjectIds); Assert.Equal("LEGEND", skill.SourceObjectId);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        await game.Top(); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Contains(top, game.State.PlayerZones["P1"].Hand); Assert.Null(game.State.PendingHandChoice);
        Assert.DoesNotContain(top, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Top(); await game.Replay();
    }

    public static IEnumerable<object[]> Discards() => CardNumbers().SelectMany(row =>
        new[] { false, true }.Select(newCard => new object[] { row[0], newCard }));

    [Theory, MemberData(nameof(Discards))]
    public async Task AnOpponentTurnDrawOffersBothTheNewCardAndExistingHandCardsPrivately(string no, bool newCard)
    {
        var game = await Ready(no); var before = game.State.PlayerZones["P1"].Hand.ToArray();
        var top = game.State.PlayerZones["P1"].MainDeck[0]; await Play(game); await game.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount); Assert.Equal(0, choice.DrawCount);
        Assert.Contains(top, choice.LegalObjectIds); Assert.All(before, id => Assert.Contains(id, choice.LegalObjectIds));
        var other = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]);
        Assert.DoesNotContain(top, other); Assert.DoesNotContain("standbyDraw", other, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("STANDBY_DRAW_USED", other); Assert.DoesNotContain("legalObjectIds", other);
        var chosen = newCard || before.Length == 0 ? top : before[0]; await Discard(game, chosen);
        Assert.Contains(chosen, game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(before.Length, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Null(game.State.PendingHandChoice); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DISCARDED"); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ASecondFaceDownPlayInTheSameTurnDoesNotDrawAgain(string no)
    {
        var game = await Ready(no, second: true); await Play(game); await game.Top(); await Discard(game);
        await game.Top(); await Play(game, "SECOND");
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.Empty(game.State.TriggerQueue); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task ANewTurnRestoresTheAllowanceForTheSameLegend(string no)
    {
        var game = await Ready(no, second: true); await Play(game); await game.Top(); await Discard(game);
        while (game.State.PriorityPlayerId is not null) await game.Top();
        var ended = await OfficialGraveyardRecastTests.Act(game.State, game.State.TurnPlayerId, new EndTurnCommand());
        var completed = await TurnSequenceTestDriver.Complete(ended);
        var next = completed.State;
        Assert.Equal(game.State.TurnNumber + 1, next.TurnNumber);
        Assert.DoesNotContain(next.UntilEndOfTurnEffects, effect => effect.StartsWith("STANDBY_DRAW_USED", StringComparison.Ordinal));
        var publicResult = JsonSerializer.Serialize(new { completed.Events, completed.Snapshots, completed.Prompts });
        Assert.DoesNotContain("STANDBY_DRAW_USED", publicResult);
        game = new Driver(next); await Play(game, "SECOND");
        Assert.Single(game.State.StackItems, i => i.EffectKind == Effect); await game.Top();
        Assert.Null(game.State.PendingHandChoice); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task HidingAndOrdinaryHandPlayDoNotSpendTheFaceDownTriggerAllowance(string no)
    {
        var state = Board(no);
        var hidden = await OfficialGraveyardRecastTests.Act(state, "P1", new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.DoesNotContain(hidden.State.UntilEndOfTurnEffects, effect => effect.StartsWith("STANDBY_DRAW_USED", StringComparison.Ordinal));
        Assert.Empty(hidden.State.StackItems); Assert.Empty(hidden.State.TriggerQueue);
        state = AttackDamageDistributionTests.Spell(state, "ORDINARY", "OGN·048/298", "P1");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("ORDINARY", "OGN·048/298", []));
        Assert.DoesNotContain(game.State.StackItems, i => i.EffectKind == Effect);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith("STANDBY_DRAW_USED", StringComparison.Ordinal));
        await game.Top(); await game.Replay();
    }

    public static IEnumerable<object[]> InvalidChoices() => CardNumbers().SelectMany(row =>
        new[] { "opponent", "deck", "empty", "duplicate", "stale" }.Select(branch => new object[] { row[0], branch }));

    [Theory, MemberData(nameof(InvalidChoices))]
    public async Task DiscardRejectsWrongPlayerPrivateZoneWrongCountAndExpiredChoicesWithoutMutation(string no, string branch)
    {
        var game = await Ready(no); await Play(game); await game.Top();
        var choice = game.State.PendingHandChoice!; var selected = choice.LegalObjectIds[0];
        var command = new ChooseHandCardsCommand(branch == "stale" ? "expired" : choice.ChoiceId, choice.ChoiceWindow,
            branch == "empty" ? [] : branch == "duplicate" ? [selected, selected]
                : branch == "deck" ? [game.State.PlayerZones["P1"].MainDeck[0]] : [selected]);
        await Reject(game.State, branch == "opponent" ? "P2" : "P1", command);
        await Discard(game); await game.Replay();
    }

    [Fact]
    public async Task ATargetlessDrawSkillCannotBeCounteredByASpellThatRequiresAFriendlyTarget()
    {
        var ready = await Ready(Normal);
        var state = AttackDamageDistributionTests.Spell(ready.State, "COUNTER", "SFD·045/221", "P2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) } };
        var game = new Driver(state); await Play(game);
        var skill = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Act("P1", new PassPriorityCommand());
        await Reject(game.State, "P2", new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId]));
        await game.Act("P2", new PassPriorityCommand()); await Discard(game); await game.Replay();
    }

    [Fact]
    public async Task CounteringTheOriginalFaceDownSpellDoesNotUndoTheEarnedDrawSkill()
    {
        var ready = await Ready(Normal);
        var state = AttackDamageDistributionTests.Spell(ready.State, "COUNTER", "OGN·064/298", "P2");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) } };
        var game = new Driver(state); await Play(game);
        var spell = Assert.Single(game.State.StackItems, i => i.SourceObjectId == "SHADOW");
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId]));
        await game.Top(); Assert.Contains("SHADOW", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        await game.Top(); await Discard(game); Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task EmptyDeckUsesNormalBurnoutAndVictoryCompletesBeforeAnyDiscard(bool refill)
    {
        var ready = await Ready(Normal); var state = ready.State; var deck = state.PlayerZones["P1"].MainDeck;
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                MainDeck = [], Graveyard = refill ? deck : [], Banished = refill ? [] : deck } },
            ObjectLocations = state.ObjectLocations.ToDictionary(p => p.Key, p => deck.Contains(p.Key)
                ? new ObjectLocationState("P1", refill ? "GRAVEYARD" : "BANISHED") : p.Value),
            PlayerScores = new Dictionary<string, int>(state.PlayerScores) { ["P2"] = refill ? 0 : 7 }
        };
        var game = new Driver(state); await Play(game); await game.Top();
        if (refill)
        {
            Assert.Equal(1, game.State.PlayerScores["P2"]); Assert.Single(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
            Assert.Single(game.State.PendingHandChoice!.LegalObjectIds.Where(deck.Contains)); await Discard(game);
        }
        else
        {
            Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal("P2", game.State.WinnerPlayerId);
            Assert.Null(game.State.PendingHandChoice); Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DISCARDED");
        }
        await game.Replay();
    }

    [Fact]
    public async Task RestoringCannotForgeTheControllerTurnOrFaceOfTheDrawReceipt()
    {
        var game = await Ready(Normal); await Play(game);
        var skill = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect);
        var context = skill.StandbyObserver!; var birth = context.Draw!;
        foreach (var changed in new[] { context with { Draw = birth with { ControllerId = "P2" } },
            context with { Draw = birth with { TurnNumber = birth.TurnNumber + 1 } },
            context with { Draw = birth with { FaceRevision = 99 } }, context with { CardNo = "OGN·255/298" } })
        {
            var state = game.State with { StackItems = game.State.StackItems.Select(item => item == skill
                ? item with { StandbyObserver = changed } : item).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), error => error.Contains("standby play observer", StringComparison.Ordinal));
        }
        await game.Top();
        var pending = game.State.PendingHandChoice!;
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingHandChoice = pending with { StandbyObserver = null } }),
            error => error.Contains("face-down play draw discard", StringComparison.Ordinal));
        await Discard(game); await game.Replay();
    }

    [Theory, MemberData(nameof(CardNumbers))]
    public async Task EveryLegendPrintingSupportsALegalEvelynnDeck(string no)
    {
        const OfficialDeckFormat format = OfficialDeckFormat.ChinaStandard20260724;
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, format)).ToArray() };
        var legend = Assert.Single(allowed.Cards, c => c.CardNo == no);
        var runes = legend.CardColorList.SelectMany(color => Enumerable.Repeat(allowed.Cards
            .Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(color)).OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo, 6)).ToArray();
        var build = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = (OfficialDecklist)build.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            no, "RAD·090/167", Array.Empty<string>(), new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, runes])!;
        var valid = OfficialDeckValidator.Validate(deck, catalog, format);
        Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-face-down-draw", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
