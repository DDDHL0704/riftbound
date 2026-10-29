using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTokenPrintingTests
{
    [Theory]
    [InlineData("VEN·T02", 37922, "金币", "指示物装备", 0)]
    [InlineData("VEN·T03", 37923, "机器人", "指示物单位", 3)]
    public async Task OfficialPrintsUseTokenFactoriesAndCannotBeIncludedInADeck(
        string no, long id, string name, string category, int power)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, card => card.CardNo == no);
        Assert.Equal(id, card.Id);
        Assert.Equal(name, card.CardName);
        Assert.Equal(category, card.CardCategoryName);
        Assert.Null(card.Energy);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory));
        Assert.Equal(category, factory.CategoryName);
        Assert.Equal(power, factory.DefaultPower);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
        var deck = PreconstructedDeckCatalog.Build(catalog, OfficialDeckFormat.CoreRules)
            .Single(deck => deck.Id == "jhin-lowcurve").Decklist;
        var main = deck.MainDeck.ToArray();
        main[Array.FindIndex(main, cardNo => cardNo != deck.ChampionCardNo)] = no;
        var validation = OfficialDeckValidator.Validate(deck with { MainDeck = main }, catalog, OfficialDeckFormat.CoreRules);
        Assert.False(validation.IsValid);
    }

    private static MatchState GoldPosition(bool exhausted = false)
    {
        var state = VenUnitEntryBasicTests.Position("OGN·001/298");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T02", out var factory));
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "GOLD"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GOLD"] = factory.CreateObject("GOLD", "P1", "P1", exhausted) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GOLD"] = new("P1", "BASE") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
    }

    [Fact]
    public async Task GoldExhaustsAndDisappearsToProduceOneWildcardPowerImmediately()
    {
        var initial = GoldPosition();
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("GOLD", P4ActivatedAbilityCatalog.GoldTokenVenResourceAbilityId, []));
        Assert.Equal(1, game.State.RunePools["P1"].Power);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(initial.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Empty(game.State.StackItems);
        Assert.False(game.State.CardObjects.ContainsKey("GOLD"));
        Assert.False(game.State.ObjectLocations.ContainsKey("GOLD"));
        Assert.DoesNotContain("GOLD", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("GOLD", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
        Assert.Single(game.Events(), e => e.Event.Kind == "POWER_GAINED");
        await Reject(game.State, "P1", new ActivateAbilityCommand("GOLD", P4ActivatedAbilityCatalog.GoldTokenVenResourceAbilityId, []));
        await game.Replay();
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("wrong-player")]
    [InlineData("target")]
    [InlineData("extra-cost")]
    public async Task InvalidGoldCommandsCannotSpendTheTokenOrChangeResources(string scenario)
    {
        var state = GoldPosition(scenario == "exhausted");
        await Reject(state, scenario == "wrong-player" ? "P2" : "P1",
            new ActivateAbilityCommand("GOLD", P4ActivatedAbilityCatalog.GoldTokenVenResourceAbilityId,
                scenario == "target" ? ["GOLD"] : [], scenario == "extra-cost" ? ["SPEND_MANA:1"] : []));
    }

    [Fact]
    public async Task RobotHasThreeMightAndCeasesToExistAfterRealDestruction()
    {
        var state = CurrentPowerTargetTests.Position("UNL-159/219", 3);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T03", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = factory.CreateObject("D", "P2", "P2") } };
        Assert.Equal(3, CurrentPowerTargetTests.DisplayPower(state));
        Assert.Contains("机械", state.CardObjects["D"].Tags);
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("AOE", "UNL-159/219", ["D"]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("D"));
        Assert.False(game.State.ObjectLocations.ContainsKey("D"));
        Assert.DoesNotContain("D", game.State.PlayerZones["P2"].Graveyard);
        Assert.Single(game.Events(), e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST");
        await game.Replay();
    }

    [Fact]
    public void RecoveryRejectsARobotWithAnEquipmentFactoryWitness()
    {
        var state = CurrentPowerTargetTests.Position("UNL-159/219", 3);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T03", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = factory.CreateObject("D", "P2", "P2") } };
        ArmamentMightTests.PublicRecovery(state);
        OfficialInsightAndSpellLockTests.Restore(state);
        var forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { TokenFactoryCardNo = "VEN·T02" } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-token", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
