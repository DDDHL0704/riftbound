using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEquipmentGoldCostTests
{
    private const string Barrel = "SFD·063/221";
    private const string Thief = "SFD·074/221";
    private const string Return = "OGN·104/298";

    private static MatchState BarrelPosition(bool ownTurn = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "BARREL", Barrel, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", Return, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "OTHER-RETURN", Return, "P2", "HAND");
        state = RadGearTestPosition.Put(state, "COUNTER", "OGN·045/298", "P2", "HAND");
        return state with { TurnPlayerId = ownTurn ? "P1" : "P2", ActivePlayerId = ownTurn ? "P1" : "P2",
            TurnNumber = ownTurn ? 1 : 2, CardPlays = new(ownTurn ? 1 : 2, [], []) };
    }

    private static async Task BeginOpponentTurnSpell(Driver game)
    {
        await game.Act("P2", new PlayCardCommand("OTHER-RETURN", Return, ["ENEMY"]));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", Return, ["ALLY"]));
    }

    private static CardObjectState[] Gold(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "金币"
            && state.PlayerZones.Values.Any(z => z.Base.Contains(c.ObjectId) || z.Battlefields.Contains(c.ObjectId))).ToArray();

    private static async Task Pay(Driver game, string id)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    [Fact]
    public async Task APlayedBarrelObservesAnOpponentTurnSpellButNotRuneResourceActivations()
    {
        var initial = RadUnitEffectTestFixture.Place(BarrelPosition(ownTurn: true), "BARREL", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "P1-RUNE", "OGN·042/298", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "P2-RUNE", "OGN·042/298", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("BARREL", Barrel, []));
        Assert.Contains("BARREL", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["BARREL"].IsExhausted);
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", ["P2-RUNE"]);
        await game.Act("P2", new PlayCardCommand("OTHER-RETURN", Return, ["ENEMY"]));
        await game.Act("P2", new PassPriorityCommand());
        await game.TapRunes("P1", ["P1-RUNE"]);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(Gold(game.State));
        await game.Act("P1", new PlayCardCommand("RETURN", Return, ["ALLY"]));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(Gold(game.State));
        await game.Top(); await Pay(game, "BARREL"); await game.Top();
        Assert.True(Assert.Single(Gold(game.State)).IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task BarrelRequiresSuccessfulCompletionOfYourSpellDuringTheOpponentsTurn(bool ownTurn, bool countered)
    {
        var game = new Driver(BarrelPosition(ownTurn));
        if (ownTurn) await game.Act("P1", new PlayCardCommand("RETURN", Return, ["ALLY"]));
        else await BeginOpponentTurnSpell(game);
        Assert.Empty(Gold(game.State)); Assert.Null(game.State.PendingCardChoice);
        Assert.False(game.State.CardObjects["BARREL"].IsExhausted);
        if (countered)
        {
            var spell = game.State.StackItems[^1].StackItemId;
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [spell]));
            await game.Top();
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        }
        else await game.Top();
        if (!ownTurn && !countered)
        {
            var mana = game.State.RunePools["P1"].Mana;
            await Pay(game, "BARREL");
            Assert.True(game.State.CardObjects["BARREL"].IsExhausted);
            Assert.Equal(mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(Barrel, game.State.StackItems[^1].UnitEvent!.CardNo);
            Assert.Empty(Gold(game.State));
            await game.Top();
            var gold = Assert.Single(Gold(game.State));
            Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.ControllerId);
        }
        else
        {
            Assert.Null(game.State.PendingCardChoice); Assert.Empty(Gold(game.State));
            Assert.False(game.State.CardObjects["BARREL"].IsExhausted);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("hand")] [InlineData("enemy")]
    public async Task ABarrelThatCannotPayOrDoesNotObserveYourPlayCannotProduceGold(string boundary)
    {
        var initial = BarrelPosition();
        if (boundary == "exhausted") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BARREL"] = initial.CardObjects["BARREL"] with { IsExhausted = true } } };
        else initial = RadUnitEffectTestFixture.Place(initial, "BARREL", boundary == "enemy" ? "P2" : "P1",
            boundary == "hand" ? "HAND" : "BASE");
        var game = new Driver(initial); await BeginOpponentTurnSpell(game); await game.Top();
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(Gold(game.State)); await game.Replay();
    }

    [Fact]
    public async Task DecliningTheBarrelsExhaustionKeepsItReadyAndProducesNoGold()
    {
        var game = new Driver(BarrelPosition()); await BeginOpponentTurnSpell(game); await game.Top();
        var choice = game.State.PendingCardChoice!;
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BARREL"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.False(game.State.CardObjects["BARREL"].IsExhausted); Assert.Empty(Gold(game.State));
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent?.CardNo == Barrel); await game.Replay();
    }

    [Fact]
    public async Task TheBarrelsCurrentControllerPaysAndOwnsItsGoldEvenWhenTheBarrelHasAnotherOwner()
    {
        var initial = BarrelPosition();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BARREL"] = initial.CardObjects["BARREL"] with { OwnerId = "P2" } } };
        var game = new Driver(initial); await BeginOpponentTurnSpell(game); await game.Top();
        await Pay(game, "BARREL"); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["BARREL"].OwnerId);
        Assert.Equal("P1", Assert.Single(Gold(game.State)).OwnerId); await game.Replay();
    }

    private static MatchState ThiefPosition(string owner = "P2", bool token = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "THIEF", Thief, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·033/221", owner, "BASE");
        state = RadGearTestPosition.Put(state, "HIGH", "SFD·095/221", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "PRIVATE-GEAR", "SFD·033/221", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", Return, "P1", "HAND");
        if (token)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = factory.CreateObject("GEAR", owner, owner) } };
        }
        return state;
    }

    [Theory]
    [InlineData("P1", false)] [InlineData("P2", false)]
    [InlineData("P1", true)] [InlineData("P2", true)]
    public async Task ThiefPaysAnyPlayersCheapEquipmentBeforeResponsesAndCreatesDormantGoldOnlyAtResolution(string owner, bool token)
    {
        var game = new Driver(ThiefPosition(owner, token));
        await game.Act("P1", new PlayCardCommand("THIEF", Thief, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("GEAR", choice.LegalObjectIds);
        Assert.DoesNotContain("HIGH", choice.LegalObjectIds);
        Assert.DoesNotContain("PRIVATE-GEAR", choice.LegalObjectIds);
        Assert.DoesNotContain("ALLY", choice.LegalObjectIds);
        var beforeMana = game.State.RunePools["P1"].Mana;
        await Pay(game, "GEAR");
        Assert.DoesNotContain("GEAR", game.State.PlayerZones[owner].Base);
        if (!token) Assert.Contains("GEAR", game.State.PlayerZones[owner].Graveyard);
        Assert.Empty(Gold(game.State)); Assert.Equal(beforeMana, game.State.RunePools["P1"].Mana);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(token ? 0 : 1, Assert.Single(skill.TriggerCost!.DestroyedPermanentManaCosts!));
        await game.Top();
        var gold = Assert.Single(Gold(game.State));
        Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.OwnerId); Assert.Equal("P1", gold.ControllerId);
        await game.Replay();
    }

    [Fact]
    public async Task ThiefCanDeclineAndInvalidCostObjectsOrTheWrongPlayerAreRejectedAtomically()
    {
        var game = new Driver(ThiefPosition()); await game.Act("P1", new PlayCardCommand("THIEF", Thief, []));
        var choice = game.State.PendingCardChoice!;
        foreach (var ids in new string[][] { ["HIGH"], ["ALLY"], ["PRIVATE-GEAR"], ["BF"], ["GEAR", "GEAR"] })
            await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base); Assert.Empty(game.State.StackItems);
        Assert.Empty(Gold(game.State)); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheThiefAfterPaymentPreservesTheGoldAndHidesTheCurrentPrivateSource()
    {
        var game = new Driver(ThiefPosition()); await game.Act("P1", new PlayCardCommand("THIEF", Thief, []));
        await Pay(game, "GEAR");
        await game.Act("P1", new PlayCardCommand("RETURN", Return, ["THIEF"])); await game.Top();
        Assert.Contains("THIEF", game.State.PlayerZones["P1"].Hand); Assert.Empty(Gold(game.State));
        await game.Top(); Assert.Single(Gold(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("mana")] [InlineData("target")] [InlineData("erase")] [InlineData("capture")]
    public async Task RecoveryBindsTheDestructionReceiptAndCaptureToTheirUnmodifiedAcceptedHistory(string change)
    {
        var game = new Driver(ThiefPosition()); await game.Act("P1", new PlayCardCommand("THIEF", Thief, []));
        await Pay(game, "GEAR"); var item = Assert.Single(game.State.StackItems);
        var forgedItem = change switch {
            "mana" => item with { TriggerCost = item.TriggerCost! with { DestroyedPermanentManaCosts = [0] } },
            "target" => item with { TriggerCost = item.TriggerCost! with {
                DestroyedPermanents = [new("HIGH", game.State.CardObjects["HIGH"].ObjectGeneration)] } },
            "erase" => item with { TriggerCost = null },
            _ => item with { UnitEvent = item.UnitEvent! with { CapturedTick = item.UnitEvent.CapturedTick + 1 } }
        };
        var forged = game.State with { StackItems = [forgedItem] };
        var errors = ArmamentGrantedCombatTriggerTests.Errors(game, forged);
        Assert.Contains(errors, e => e.Contains(change == "capture" ? "original accepted capture" : "original accepted payment"));
        await game.Replay();
    }
}
