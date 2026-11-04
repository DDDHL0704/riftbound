using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadSimultaneousDestructionFaqTests
{
    private const string Orianna = "RAD·145/167";
    private const string Neeko = "RAD·167/167";
    private const string Hourglass = "OGN·077/298";
    private static readonly string[] RuneTraits = [
        RuneTrait.Red, RuneTrait.Green, RuneTrait.Blue, RuneTrait.Yellow, RuneTrait.Orange, RuneTrait.Purple];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoActuallyAnimatedHourglassesFollowTheControllerChosenReplacementOrder(bool destroyBoth)
    {
        var game = await Position(includeSecondHourglass: true);
        await AnimateWithTwoLegendIncarnations(game, "H1", "H2");

        await PlayWithPrintedResources(game, "DESTROY");
        await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        var selected = destroyBoth ? "GEAR:H1:H2" : "GEAR:H1:H1";
        Assert.Contains(pending.Request.Options, option => option.Id == selected);
        await ChooseReplacement(game, selected);

        Assert.Null(game.State.PendingRuleChoice);
        var zones = ZoneSummary(game, "H1", "H2");
        if (destroyBoth)
        {
            Assert.True(game.State.PlayerZones["P1"].Graveyard.Contains("H1"), zones);
            Assert.True(game.State.PlayerZones["P1"].Graveyard.Contains("H2"), zones);
            Assert.DoesNotContain("H1", game.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain("H2", game.State.PlayerZones["P1"].Base);
        }
        else
        {
            Assert.True(game.State.PlayerZones["P1"].Base.Contains("H1"), zones);
            Assert.True(game.State.PlayerZones["P1"].Graveyard.Contains("H2"), zones);
            Assert.DoesNotContain("H1", game.State.PlayerZones["P1"].Graveyard);
        }
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SorakaAndAnActuallyAnimatedHourglassAllowBothFaqOutcomes(bool destroyHourglass)
    {
        var game = await Position(includeSoraka: true);
        await AnimateWithTwoLegendIncarnations(game, "H1");

        await PlayWithPrintedResources(game, "S", destination: "BASE");
        if (game.State.StackItems.Count > 0) await game.Top();
        // A normally played Soraka enters exhausted. Ready both real units
        // and replenish mana through the next own turn before moving them.
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        await ResolveEmptyMainTriggers(game);
        Assert.Null(game.State.PendingCardChoice);
        await game.Act("P1", new MoveUnitCommand("H1", "BASE", "BATTLEFIELD:BF", [], ["H1", "S"]));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
        Assert.False(game.State.SpellDuelState.IsActive);
        Assert.Equal("P1", game.State.BattlefieldStates["BF"].ControllerId);
        Assert.All(new[] { "H1", "S" }, id => Assert.Equal(
            new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations[id]));
        await PlayWithPrintedResources(game, "DESTROY");
        await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        var selected = destroyHourglass ? "GEAR:S:H1" : "LOCAL_RECALL:H1:S";
        Assert.Contains(pending.Request.Options, option => option.Id == selected);
        await ChooseReplacement(game, selected);
        if (game.State.PendingRuleChoice is not null)
            await ChooseReplacement(game, destroyHourglass ? "LOCAL_RECALL:H1:S" : "GEAR:S:H1");

        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("S", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["S"].IsExhausted);
        Assert.Equal(!destroyHourglass, game.State.PlayerZones["P1"].Graveyard.Contains("H1"));
        Assert.Equal(destroyHourglass, game.State.PlayerZones["P1"].Base.Contains("H1"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task HourglassReplacementExhaustsRecklessInventorAndItsExhaustAbilityCanTrigger()
    {
        var game = await Position(includeInventor: true);
        await AnimateWithTwoLegendIncarnations(game, "H1");

        await PlayWithPrintedResources(game, "DESTROY_ONE", ["INVENTOR"]);
        await game.Top();

        Assert.Contains("INVENTOR", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("INVENTOR", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["INVENTOR"].IsExhausted);
        Assert.Contains("H1", game.State.PlayerZones["P1"].Graveyard);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_EVENT_MODE_CONFIRMATION", choice.ChoiceWindow);
        Assert.Contains("CREATE_GOLD", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["CREATE_GOLD"]));
        await game.Top();
        Assert.Contains(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is not null
            && game.State.PlayerZones["P1"].Base.Contains(card.ObjectId));
        await game.ColdCheckpoint();
    }

    private static async Task AnimateWithTwoLegendIncarnations(Driver game, params string[] targets)
    {
        await AddLegend(game);
        await OwnMain(game);
        await ConfirmTransformation(game, targets[0]);
        await game.Top();
        if (targets.Length == 1)
        {
            await PlayWithPrintedResources(game, "RETURN", ["NEEKO"]);
            await game.Top();
            await game.ColdCheckpoint();
            return;
        }

        await PlayWithPrintedResources(game, "RETURN", ["NEEKO"]);
        await game.Top();
        await AddLegend(game);
        await OwnMain(game);
        await ConfirmTransformation(game, targets[1]);
        await game.Top();
        Assert.All(targets, target => Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects[target].Tags));
        Assert.NotEqual(game.State.CardObjects[targets[0]].LegendEquipmentUnit!.SourceLegend.ObjectId,
            game.State.CardObjects[targets[^1]].LegendEquipmentUnit!.SourceLegend.ObjectId);
        await PlayWithPrintedResources(game, "RETURN2", ["NEEKO"]);
        await game.Top();
        await game.ColdCheckpoint();
    }

    private static async Task AddLegend(Driver game)
    {
        await PlayWithPrintedResources(game, "NEEKO", destination: "BASE");
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(Orianna, game.State.CardObjects[Assert.Single(game.State.PlayerZones["P1"].LegendZone)].CardNo);
    }

    private static async Task OwnMain(Driver game)
    {
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.NotNull(game.State.PendingCardChoice);
    }

    private static async Task ConfirmTransformation(Driver game, string target)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await EnsureMana(game, payment.ManaCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
    }

    private static async Task PlayWithPrintedResources(Driver game, string source,
        string[]? targets = null, string destination = "")
    {
        var card = game.State.CardObjects[source];
        var printed = PrintedPowerCostRules.ForCard(card.CardNo!);
        await EnsureMana(game, card.ManaCost, printed.Traits);
        var pool = game.State.RunePools["P1"];
        var available = printed.Traits.Count == 0
            ? pool.TotalPower
            : pool.Power + printed.Traits.Sum(trait => pool.PowerByTrait.GetValueOrDefault(trait));
        var missing = Math.Max(0, printed.Amount - available);
        var payableTraits = printed.Traits.Count == 0 ? RuneTraits : printed.Traits;
        var runes = game.State.PlayerZones["P1"].Base.Where(id =>
            game.State.CardObjects.TryGetValue(id, out var rune)
                && rune.Tags.Contains(CardObjectTags.RuneCard)
                && payableTraits.Any(trait => rune.Tags.Contains("COLOR:" + trait)))
            .Take(missing).ToArray();
        Assert.Equal(missing, runes.Length);
        await game.Act("P1", new PlayCardCommand(source, card.CardNo!, targets ?? [],
            OptionalCosts: runes.Select(id => "RECYCLE_RUNE:" + id).ToArray(), Destination: destination));
    }

    private static async Task EnsureMana(Driver game, int cost, IReadOnlyList<string>? reservedTraits = null)
    {
        var missing = cost - game.State.RunePools["P1"].Mana;
        if (missing <= 0) return;
        var candidates = game.State.PlayerZones["P1"].Base.Where(id =>
            game.State.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsExhausted)
            .ToArray();
        var reserved = reservedTraits?.ToHashSet(StringComparer.Ordinal) ?? [];
        var preferred = candidates.Where(id => !reserved.Any(trait =>
            game.State.CardObjects[id].Tags.Contains("COLOR:" + trait)));
        var fallback = candidates.Where(id => reserved.Any(trait =>
            game.State.CardObjects[id].Tags.Contains("COLOR:" + trait)));
        var available = preferred.Concat(fallback).Take(missing).ToArray();
        Assert.Equal(missing, available.Length);
        await game.TapRunes("P1", available);
    }

    private static async Task ChooseReplacement(Driver game, string option)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Contains(request.Options, choice => choice.Id == option);
        await game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
    }

    private static string ZoneSummary(Driver game, params string[] ids)
    {
        var zones = game.State.PlayerZones.SelectMany(player => new[]
        {
            (player.Key, Zone: "BASE", Ids: player.Value.Base),
            (player.Key, Zone: "BATTLEFIELD", Ids: player.Value.Battlefields),
            (player.Key, Zone: "GRAVEYARD", Ids: player.Value.Graveyard),
            (player.Key, Zone: "BANISHED", Ids: player.Value.Banished)
        }).ToArray();
        return string.Join("; ", ids.Select(id => $"{id}={string.Join(",", zones
            .Where(zone => zone.Ids.Contains(id)).Select(zone => $"{zone.Key}:{zone.Zone}"))}"));
    }

    private static async Task ResolveEmptyMainTriggers(Driver game)
    {
        for (var step = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0
            || game.State.PendingCardChoice is not null; step++)
        {
            Assert.True(step < 12, "Main-phase confirmation did not complete.");
            if (game.State.PendingCardChoice is { } choice)
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            else if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                    game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
            else
                await game.Top();
        }
    }

    private static async Task<Driver> Position(
        bool includeSecondHourglass = false, bool includeSoraka = false, bool includeInventor = false)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(item => item.Id == "jhin-lowcurve").Decklist;
        var main = deck.MainDeck.ToArray();
        main[Array.FindLastIndex(main, no => no != deck.ChampionCardNo)] = Neeko;
        deck = deck with { MainDeck = main, AdditionalLegendCardNos = [Orianna] };
        var state = Put(Create(), "NEEKO", Neeko, "P1", "HAND");
        state = state with {
            CardObjects = state.CardObjects.Where(entry => entry.Key is not ("ALLY" or "ENEMY")).ToDictionary(),
            ObjectLocations = state.ObjectLocations.Where(entry => entry.Key is not ("ALLY" or "ENEMY")).ToDictionary(),
            PlayerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value with {
                Base = entry.Value.Base.Where(id => id is not ("ALLY" or "ENEMY")).ToArray() }) };
        state = Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = Put(state, "RETURN2", "OGN·104/298", "P1", "HAND");
        state = Put(state, "H1", Hourglass, "P1", "BASE");
        if (includeSecondHourglass) state = Put(state, "H2", Hourglass, "P1", "BASE");
        if (includeSoraka) state = Put(state, "S", "SFD·173/221", "P1", "HAND");
        if (includeInventor) state = Put(state, "INVENTOR", "RAD·105/167", "P1", "BASE");
        state = Put(state, "DESTROY", "UNL-180/219", "P1", "HAND");
        state = Put(state, "DESTROY_ONE", "OGN·229/298", "P1", "HAND");
        for (var i = 0; i < 16; i++)
            state = Put(state, "RUNE-" + i, i < 6 ? "UNL-R06" : i < 11 ? "UNL-R02" : "UNL-R03", "P1", "BASE");
        return new Driver(state with
        {
            PlayerDecklists = new Dictionary<string, OfficialDecklist> { ["P1"] = deck },
            RunePools = new Dictionary<string, RunePool>
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            }
        }) { RecoverAfterEveryCommand = false };
    }
}
