using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadBombUnitTests
{
    private const string Amateur = "RAD·001/167";
    private const string Battlefield = "BF";

    [Fact]
    public async Task AmateurDemolitionistPlaysABombToTheChosenBattlefield()
    {
        var game = new Driver(Position(withEnemy: true));
        await game.Act("P1", new PlayCardCommand("UNIT", Amateur, [Battlefield], Destination: "BASE"));
        await ResolveAll(game);

        var bombId = Assert.Single(game.State.PlayerZones["P1"].Battlefields,
            id => game.State.CardObjects.TryGetValue(id, out var card)
                && card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        var bomb = game.State.CardObjects[bombId];
        Assert.Equal(P6TokenFactoryCatalog.BombTokenCardNo, bomb.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.BombTokenCardNo, bomb.TokenFactoryCardNo);
        Assert.Contains(CardObjectTags.EquipmentCard, bomb.Tags);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
        Assert.Equal("P1", bomb.OwnerId);
        Assert.Equal("P1", bomb.ControllerId);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations[bombId].Zone);
        Assert.Equal(Battlefield, game.State.ObjectLocations[bombId].BattlefieldObjectId);
        Assert.Contains(game.Events(), e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED"
            && Equals(e.Event.Payload.GetValueOrDefault("tokenCardNo"), P6TokenFactoryCatalog.BombTokenCardNo)
            && Equals(e.Event.Payload.GetValueOrDefault("destinationZone"), "BATTLEFIELD"));
        await game.Replay();
    }

    [Theory]
    [InlineData("ENEMY")]
    [InlineData("BF|OTHER")]
    [InlineData("missing")]
    public async Task AmateurDemolitionistRejectsNonBattlefieldOrMalformedTargets(string targetText)
    {
        var targets = targetText.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var state = Position(withEnemy: true);
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-amateur-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", Amateur, targets, Destination: "BASE"), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }

    [Fact]
    public async Task DestroyedBombWithoutAnEnemyUnitHasNoConfirmableSkill()
    {
        var game = new Driver(AddToHand(Position(withEnemy: false),
            "DESTROY", "SFD·032/221", "P1"));
        await game.Act("P1", new PlayCardCommand("UNIT", Amateur, [Battlefield], Destination: "BASE"));
        await ResolveAll(game);
        var bombId = Bombs(game.State).Single();

        await game.Act("P1", new PlayCardCommand("DESTROY", "SFD·032/221", [bombId]));
        await ResolveUntilChoice(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Empty(choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await ResolveAll(game);

        Assert.False(game.State.CardObjects.ContainsKey(bombId));
        Assert.DoesNotContain(bombId, game.State.ObjectLocations.Keys);
        Assert.DoesNotContain(game.State.StackItems, item => item.FieldContext?.Kind == "RAD_BOMB_DESTROYED");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), bombId));
        await game.Replay();
    }

    [Fact]
    public async Task DestroyedBombOnlyOffersAnEnemyAtItsOwnBattlefieldAndDealsTwoDamage()
    {
        var game = new Driver(AddToHand(Position(withEnemy: true),
            "DESTROY", "SFD·032/221", "P1"));
        await game.Act("P1", new PlayCardCommand("UNIT", Amateur, [Battlefield], Destination: "BASE"));
        await ResolveAll(game);
        var bombId = Bombs(game.State).Single();

        await game.Act("P1", new PlayCardCommand("DESTROY", "SFD·032/221", [bombId]));
        await ResolveUntilChoice(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(["ENEMY"], choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await ResolveAll(game);

        Assert.Equal(2, game.State.CardObjects["ENEMY"].Damage);
        Assert.False(game.State.CardObjects.ContainsKey(bombId));
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), bombId)
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY")
            && Equals(e.Event.Payload.GetValueOrDefault("damage"), 2));
        await game.Replay();
    }

    [Fact]
    public async Task BombDestructionDestroysOtherFriendlyBombsAtTheSameBattlefield()
    {
        var state = AddToHand(Position(withEnemy: true), "DESTROY", "SFD·032/221", "P1");
        state = AddToHand(state, "UNIT2", Amateur, "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Amateur, [Battlefield], Destination: "BASE"));
        await ResolveAll(game);
        await game.Act("P1", new PlayCardCommand("UNIT2", Amateur, [Battlefield], Destination: "BASE"));
        await ResolveAll(game);
        var bombs = Bombs(game.State);
        Assert.Equal(2, bombs.Length);

        await game.Act("P1", new PlayCardCommand("DESTROY", "SFD·032/221", [bombs[0]]));
        await ResolveUntilChoice(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await ResolveAll(game);
        while (game.State.PendingCardChoice is { } chainedChoice)
        {
            Assert.Equal("TRIGGER_CONFIRMATION", chainedChoice.ChoiceWindow);
            Assert.Equal(["ENEMY"], chainedChoice.LegalObjectIds);
            await game.Act("P1", new ChooseCardsCommand(chainedChoice.ChoiceId, chainedChoice.ChoiceWindow, ["ENEMY"]));
            await ResolveAll(game);
        }

        Assert.Empty(Bombs(game.State));
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains(game.Events(), e => e.Event.Kind == "EQUIPMENT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "BOMB_CHAIN"));
        await game.Replay();
    }

    private static MatchState Position(bool withEnemy)
    {
        var state = VenUnitEntryBasicTests.Position(Amateur);
        var cards = state.CardObjects.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        if (withEnemy)
        {
            cards["BF"] = cards["BF"] with { ControllerId = "P2" };
            zones["P2"] = zones["P2"] with
            {
                Base = zones["P2"].Base.Where(id => id != "ENEMY").ToArray(),
                Battlefields = [.. zones["P2"].Battlefields, "ENEMY"]
            };
            locations["ENEMY"] = new("P2", "BATTLEFIELD", Battlefield);
            cards["ENEMY"] = cards["ENEMY"] with { ControllerId = "P2", Power = 10 };
        }
        else
        {
            locations["ENEMY"] = new("P2", "BASE");
        }
        return state with
        {
            CardObjects = cards,
            PlayerZones = zones,
            ObjectLocations = locations
        };
    }

    private static MatchState AddToHand(MatchState state, string id, string cardNo, string player)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        var cards = state.CardObjects.ToDictionary();
        cards[id] = new(id, cardNo: cardNo, manaCost: behavior.ManaCost, power: behavior.SourceUnitPower,
            ownerId: player, controllerId: player, tags: [CardObjectTags.UnitCard]);
        var zones = state.PlayerZones.ToDictionary();
        zones[player] = zones[player] with { Hand = [.. zones[player].Hand, id] };
        var locations = state.ObjectLocations.ToDictionary();
        locations[id] = new(player, "HAND");
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    private static string[] Bombs(MatchState state)
        => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo)
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static async Task ResolveUntilChoice(Driver game)
    {
        for (var i = 0; i < 6 && game.State.PendingCardChoice is null && game.State.StackItems.Count > 0; i++)
            await game.Top();
    }

    private static async Task ResolveAll(Driver game)
    {
        for (var i = 0; i < 12 && game.State.StackItems.Count > 0; i++)
        {
            if (game.State.PendingCardChoice is not null || game.State.PendingPayment is not null)
                break;
            await game.Top();
        }
    }
}
