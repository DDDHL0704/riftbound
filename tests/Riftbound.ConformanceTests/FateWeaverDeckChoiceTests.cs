using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FateWeaverDeckChoiceTests
{
    internal const string Weaver = "UNL-064/219";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PlayAbilityPrivatelyLooksFourAndOptionallyRevealsAndDrawsAnEligibleSpell(bool draw)
    {
        var initial = Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("WEAVER", Weaver, []));
        Assert.Contains("WEAVER", driver.State.PlayerZones["P1"].Base);
        Assert.True(driver.State.CardObjects["WEAVER"].IsExhausted);
        Assert.Null(driver.State.PendingCardChoice);
        foreach (var player in new[] { "P1", "P2" })
            Assert.DoesNotContain("SPELL4", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)[player]));
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("DECK_EFFECT", choice.ChoiceWindow);
        Assert.Equal(["UNIT4", "SPELL3", "SPELL4", "SPELL5"], choice.ContextObjectIds);
        Assert.Equal(["SPELL4", "SPELL5"], choice.LegalObjectIds);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(1, choice.MaxCount);
        Assert.Contains("SPELL4", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P1"]));
        Assert.DoesNotContain("SPELL4", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        Assert.DoesNotContain("SPELL4", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P2"]));
        var resolved = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? ["SPELL4"] : []));
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(draw ? 1 : 0, driver.State.PlayerZones["P1"].Hand.Count);
        if (draw) Assert.Contains("SPELL4", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal("TAIL", driver.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal((draw ? new[] { "UNIT4", "SPELL3", "SPELL5" } : new[] { "UNIT4", "SPELL3", "SPELL4", "SPELL5" }).Order(),
            driver.State.PlayerZones["P1"].MainDeck.Skip(1).Order());
        Assert.Equal(draw, resolved.Events.Any(e => e.Kind == "CARD_REVEALED"));
        Assert.Equal(draw, resolved.Events.Any(e => e.Kind == "CARD_DRAWN"));
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "CARD_ADDED_TO_HAND");
        Assert.Equal(draw ? 3 : 4, Assert.Single(resolved.Events, e => e.Kind == "CARDS_RECYCLED").Payload["count"]);
        Assert.DoesNotContain("UNIT4", JsonSerializer.Serialize(resolved.Events.Select(e => e.Payload)));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("cheap-spell")]
    [InlineData("outside-four")]
    [InlineData("two-spells")]
    [InlineData("duplicate")]
    [InlineData("wrong-player")]
    [InlineData("stale-choice")]
    public async Task IllegalPrivateSelectionsRejectWithoutChangingStateAndTheOriginalChoiceStillCompletes(string branch)
    {
        var initial = Position();
        var driver = await Begin(initial);
        var before = driver.State;
        var choice = before.PendingCardChoice!;
        string[] selected = branch switch {
            "unit" => ["UNIT4"], "cheap-spell" => ["SPELL3"], "outside-four" => ["TAIL"],
            "two-spells" => ["SPELL4", "SPELL5"], "duplicate" => ["SPELL4", "SPELL4"], _ => ["SPELL4"] };
        var command = new ChooseCardsCommand(branch == "stale-choice" ? "STALE" : choice.ChoiceId, choice.ChoiceWindow, selected);
        var rejected = await new CoreRuleEngine().ResolveAsync(before,
            new("illegal-weaver", branch == "wrong-player" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL5"]));
        Assert.Contains("SPELL5", driver.State.PlayerZones["P1"].Hand);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ShortDeckLooksOnlyAtAvailableCardsAndDoesNotBurnOut(int count)
    {
        var initial = Position();
        var kept = initial.PlayerZones["P1"].MainDeck.Take(count).ToArray();
        var removed = initial.PlayerZones["P1"].MainDeck.Except(kept).ToHashSet();
        initial = initial with {
            CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = kept } } };
        var driver = await Begin(initial);
        if (count == 0) Assert.Null(driver.State.PendingCardChoice);
        else
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal(kept, choice.ContextObjectIds);
            Assert.Equal(count == 3 ? new[] { "SPELL4" } : [], choice.LegalObjectIds);
            Assert.Equal(0, choice.RequiredCount);
            Assert.Equal(count == 3 ? 1 : 0, choice.MaxCount);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.Empty(driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(kept.Order(), driver.State.PlayerZones["P1"].MainDeck.Order());
        Assert.Equal(initial.PlayerScores, driver.State.PlayerScores);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind.Contains("BURN"));
        Assert.Empty(driver.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("cheap-spell")]
    [InlineData("unit")]
    [InlineData("outside-four")]
    [InlineData("mandatory")]
    [InlineData("maximum")]
    [InlineData("actor")]
    [InlineData("context")]
    public async Task IndependentRecoveryRejectsForgedSpellEligibilityOrChoiceShape(string branch)
    {
        var driver = await Begin(Position());
        var state = driver.State;
        var choice = state.PendingCardChoice!;
        choice = branch switch {
            "cheap-spell" => choice with { LegalObjectIds = ["SPELL3", "SPELL4", "SPELL5"] },
            "unit" => choice with { LegalObjectIds = ["UNIT4", "SPELL4", "SPELL5"] },
            "outside-four" => choice with { LegalObjectIds = ["SPELL4", "SPELL5", "TAIL"] },
            "mandatory" => choice with { RequiredCount = 1 },
            "maximum" => choice with { MaxCount = 2 },
            "actor" => choice with { PlayerId = "P2" },
            _ => choice with { ContextObjectIds = ["SPELL4", "SPELL5"] } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { PendingCardChoice = choice }),
            e => e.Contains("private deck choice"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ObservedNocturnePermissionCompletesAfterTheSpellChoiceAndDoesNotBecomeAnEligibleSpell(bool banish)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT4"] = initial.CardObjects["UNIT4"] with { CardNo = DeckObservationPlayTests.Nocturne } } };
        var driver = await Begin(initial);
        var replacement = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", replacement.Request.Kind);
        Assert.DoesNotContain("UNIT4", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        await DeckObservationPlayTests.Decide(driver, banish);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(["SPELL4", "SPELL5"], choice.LegalObjectIds);
        Assert.Equal(banish ? new[] { "SPELL3", "SPELL4", "SPELL5" } : ["UNIT4", "SPELL3", "SPELL4", "SPELL5"], choice.ContextObjectIds);
        Assert.Null(driver.State.PendingEffectPlay);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL4"]));
        Assert.Contains("SPELL4", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal("TAIL", driver.State.PlayerZones["P1"].MainDeck[0]);
        if (banish)
        {
            Assert.NotNull(driver.State.PendingEffectPlay);
            Assert.DoesNotContain(driver.State.StackItems, s => s.SourceObjectId == "WEAVER");
            await driver.Act("P1", new PlayCardCommand("UNIT4", DeckObservationPlayTests.Nocturne, []));
            Assert.Contains("UNIT4", driver.State.PlayerZones["P1"].Base);
        }
        else Assert.Contains("UNIT4", driver.State.PlayerZones["P1"].MainDeck);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task<StandbyDefendObserverTests.Driver> Begin(MatchState initial)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("WEAVER", Weaver, []));
        await driver.Top();
        return driver;
    }

    internal static MatchState Position()
    {
        var catalogue = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var definitions = new Dictionary<string, string> {
            ["WEAVER"] = Weaver, ["UNIT4"] = "SFD·125/221", ["SPELL3"] = "OGN·071/298",
            ["SPELL4"] = "OGN·083/298", ["SPELL5"] = "OGN·115/298", ["TAIL"] = "OGN·115/298" };
        var objects = definitions.ToDictionary(p => p.Key, p => {
            var card = Assert.Single(catalogue.Cards, c => c.CardNo == p.Value);
            var tag = card.CardCategoryNameList.Contains("单位") ? CardObjectTags.UnitCard
                : card.CardCategoryNameList.Contains("法术") ? CardObjectTags.SpellCard : CardObjectTags.EquipmentCard;
            return new CardObjectState(p.Key, cardNo: p.Value, manaCost: card.Energy ?? 0, power: card.Power ?? 0,
                ownerId: "P1", controllerId: "P1", tags: [tag]);
        });
        return new("FATE-WEAVER", 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with {
                Hand = ["WEAVER"], MainDeck = ["UNIT4", "SPELL3", "SPELL4", "SPELL5", "TAIL"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: objects, objectLocations: definitions.ToDictionary(p => p.Key,
                p => new ObjectLocationState("P1", p.Key == "WEAVER" ? "HAND" : "MAIN_DECK")));
    }
}
